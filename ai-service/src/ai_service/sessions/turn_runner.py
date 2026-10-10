"""
Runs one turn of a shared session (docs/architecture/realtime-collaboration.md §5.1-5.2):
stream the answer, coalesce deltas (~60 ms) onto the stream lane, keep the snapshot, beat every 5 s,
stop when Rooms sets the cancel key, then publish exactly one result for Rooms to sequence.
"""

import asyncio
import contextlib
import time
from uuid import UUID

from redis.asyncio import Redis

from ai_service.llm.models import ModelRole
from ai_service.llm.prompts import load_prompt
from ai_service.llm.provider import ChatMessage, Completed, LlmError, LlmProvider, TextDelta, Usage
from ai_service.messaging.bus import ResultPublisher
from ai_service.observability import get_logger
from ai_service.rag.retrieval.context import SourceContext, build_context, resolve_citations
from ai_service.rag.retrieval.search import Retriever
from ai_service.sessions.contracts import StartAiTurnRequest, TurnCompleted, TurnFailed, TurnResult, TurnStopped
from ai_service.sessions.stream import TurnStream
from ai_service.settings import Settings

log = get_logger(__name__)


def build_messages(request: StartAiTurnRequest) -> list[ChatMessage]:
    """History oldest first, then the new instruction; each user message is prefixed with who sent it."""
    messages: list[ChatMessage] = []
    for turn in request.history:
        messages.append(ChatMessage("user", f"{turn.instructed_by}: {turn.instruction}"))
        messages.append(ChatMessage("assistant", turn.answer))
    messages.append(ChatMessage("user", f"{request.instructed_by.display_name}: {request.instruction}"))
    return messages


NO_SOURCES_NOTE = (
    "\n\n## Sources\nNo document in this room's knowledge matched the instruction. "
    "If the instruction asks about the organization's documents, say that no matching document was found "
    "and suggest what to upload."
)


def build_instructions(base: str, context: SourceContext | None) -> str:
    """Static prompt first (prompt caching), then the retrieved sources (skill §7)."""
    if context is None or not context.sources:
        return base + NO_SOURCES_NOTE
    return f"{base}\n\n## Sources\n{context.text}"


def scope_groups(request: StartAiTurnRequest) -> list[str]:
    """Retrieval scope of the session (AI-RAG-008): the room's workspace knowledge plus room-only documents.
    The tenant comes from the token; Rooms has already checked the driver is a participant of this room."""
    return [f"ws:{request.workspace_id}", f"room:{request.room_id}"]


class TurnRunner:
    def __init__(
        self,
        settings: Settings,
        llm: LlmProvider,
        redis: Redis,
        publisher: ResultPublisher,
        retriever: Retriever | None = None,
    ) -> None:
        self._settings = settings
        self._llm = llm
        self._redis = redis
        self._publisher = publisher
        self._retriever = retriever

    async def run(self, session_id: UUID, request: StartAiTurnRequest) -> TurnResult:
        stream = TurnStream(self._redis, session_id, request.turn_id, self._settings.snapshot_ttl_s)
        heartbeat = asyncio.create_task(self._beat(stream))
        result: TurnResult
        try:
            await stream.beat(self._heartbeat_ttl)
            result = await self._generate(session_id, request, stream)
        except LlmError as exc:
            log.warning("turn_failed", turn_id=str(request.turn_id), reason=exc.key, error=type(exc).__name__)
            result = TurnFailed(
                session_id=session_id, turn_id=request.turn_id, reason_key=exc.key, partial_text=stream.text or None
            )
        except TimeoutError:
            log.warning("turn_timeout", turn_id=str(request.turn_id))
            result = TurnFailed(
                session_id=session_id,
                turn_id=request.turn_id,
                reason_key="General:Errors:AiBusy",
                partial_text=stream.text or None,
            )
        except Exception:
            log.exception("turn_crashed", turn_id=str(request.turn_id))
            result = TurnFailed(
                session_id=session_id,
                turn_id=request.turn_id,
                reason_key="General:Errors:Unexpected",
                partial_text=stream.text or None,
            )
        finally:
            heartbeat.cancel()
            with contextlib.suppress(asyncio.CancelledError):
                await heartbeat

        await self._publisher.publish(result)
        await stream.finish()
        return result

    async def _retrieve(self, request: StartAiTurnRequest, stream: TurnStream) -> SourceContext | None:
        """Search the room's knowledge before answering; a search failure degrades to an ungrounded answer."""
        if self._retriever is None:
            return None
        await stream.publish_progress("Session.Progress.Searching")
        try:
            hits = await self._retriever.retrieve(
                tenant_id=request.tenant_id, acl_groups=scope_groups(request), question=request.instruction
            )
        except Exception:
            log.exception("retrieval_failed", turn_id=str(request.turn_id))
            return None
        context = build_context(hits, self._settings.context_budget_tokens)
        log.info("retrieval_done", turn_id=str(request.turn_id), sources=len(context.sources))
        return context

    @property
    def _heartbeat_ttl(self) -> int:
        return max(3, int(self._settings.heartbeat_interval_s * 3))

    async def _beat(self, stream: TurnStream) -> None:
        while True:
            await asyncio.sleep(self._settings.heartbeat_interval_s)
            await stream.beat(self._heartbeat_ttl)

    async def _generate(self, session_id: UUID, request: StartAiTurnRequest, stream: TurnStream) -> TurnResult:
        flush_s = self._settings.stream_flush_ms / 1000
        pending = ""
        last_flush = time.monotonic()
        usage = Usage()

        async with asyncio.timeout(self._settings.turn_timeout_s):
            context = await self._retrieve(request, stream)
            events = self._llm.stream(
                ModelRole.CHAT,
                build_instructions(load_prompt(self._settings.agent_prompt_version), context),
                build_messages(request),
                temperature=self._settings.chat_temperature,
                max_output_tokens=self._settings.max_output_tokens,
            )
            async for event in events:
                if isinstance(event, TextDelta):
                    pending += event.text
                    if time.monotonic() - last_flush >= flush_s:
                        await stream.publish_delta(pending)
                        pending, last_flush = "", time.monotonic()
                        if await stream.cancel_requested():
                            # Stop reading the model stream; the partial text is kept and marked stopped.
                            return TurnStopped(session_id=session_id, turn_id=request.turn_id, partial_text=stream.text)
                elif isinstance(event, Completed):
                    usage = event.usage

            await stream.publish_delta(pending)

        log.info(
            "turn_completed",
            turn_id=str(request.turn_id),
            input_tokens=usage.input_tokens,
            output_tokens=usage.output_tokens,
            cached_tokens=usage.cached_tokens,
        )
        return TurnCompleted(
            session_id=session_id,
            turn_id=request.turn_id,
            text=stream.text,
            citations=resolve_citations(stream.text, context) if context else [],
            input_tokens=usage.input_tokens,
            output_tokens=usage.output_tokens,
            cached_tokens=usage.cached_tokens,
        )
