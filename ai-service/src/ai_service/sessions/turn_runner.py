"""
Runs one turn of a shared session (docs/architecture/realtime-collaboration.md §5.1-5.2):
stream the answer, coalesce deltas (~60 ms) onto the stream lane, keep the snapshot, beat every 5 s,
stop when Rooms sets the cancel key, then publish exactly one result for Rooms to sequence.

Tool loop (AI-AGT-006): when the model calls a tool, the service validates the arguments, creates an approval
request for a mutating tool (it never runs it), feeds the outcome back to the model and streams the next round,
up to `max_tool_rounds` per turn.
"""

import asyncio
import contextlib
import json
import time
from dataclasses import dataclass
from uuid import UUID

from redis.asyncio import Redis

from ai_service.llm.models import ModelRole
from ai_service.llm.prompts import load_prompt
from ai_service.llm.provider import (
    ChatMessage,
    Completed,
    FunctionCall,
    InputItem,
    LlmError,
    LlmProvider,
    TextDelta,
    ToolCallMessage,
    ToolResultMessage,
    Usage,
)
from ai_service.llm.tools import ToolValidationError, get_tool, tool_definitions
from ai_service.messaging.bus import ResultPublisher
from ai_service.observability import get_logger
from ai_service.rag.retrieval.context import SourceContext, build_context, resolve_citations
from ai_service.rag.retrieval.search import Retriever
from ai_service.sessions.approvals import ApprovalRequest, ApprovalRequestError, ApprovalsClient
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


@dataclass(slots=True)
class _Round:
    """One model call: the text already went to the stream; the calls are what the model asked for next."""

    calls: list[FunctionCall]
    usage: Usage
    stopped: bool = False


class TurnRunner:
    def __init__(
        self,
        settings: Settings,
        llm: LlmProvider,
        redis: Redis,
        publisher: ResultPublisher,
        *,
        retriever: Retriever | None = None,
        approvals: ApprovalsClient | None = None,
    ) -> None:
        self._settings = settings
        self._llm = llm
        self._redis = redis
        self._publisher = publisher
        self._retriever = retriever
        self._approvals = approvals

    async def run(self, session_id: UUID, request: StartAiTurnRequest, bearer_token: str = "") -> TurnResult:
        stream = TurnStream(self._redis, session_id, request.turn_id, self._settings.snapshot_ttl_s)
        heartbeat = asyncio.create_task(self._beat(stream))
        result: TurnResult
        try:
            await stream.beat(self._heartbeat_ttl)
            result = await self._generate(session_id, request, stream, bearer_token)
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

    async def _generate(
        self, session_id: UUID, request: StartAiTurnRequest, stream: TurnStream, bearer_token: str
    ) -> TurnResult:
        usage = Usage()
        tools = tool_definitions() if self._approvals is not None else []

        async with asyncio.timeout(self._settings.turn_timeout_s):
            context = await self._retrieve(request, stream)
            instructions = build_instructions(load_prompt(self._settings.agent_prompt_version), context)
            items: list[InputItem] = list(build_messages(request))
            for round_no in range(1, self._settings.max_tool_rounds + 1):
                last_round = round_no == self._settings.max_tool_rounds
                result = await self._stream_round(instructions, items, tools if not last_round else [], stream)
                usage = _add(usage, result.usage)
                if result.stopped:
                    return TurnStopped(session_id=session_id, turn_id=request.turn_id, partial_text=stream.text)
                if not result.calls:
                    break
                for call in result.calls:
                    output = await self._run_tool(session_id, request, call, bearer_token, stream)
                    items.append(ToolCallMessage(call.call_id, call.name, call.arguments))
                    items.append(ToolResultMessage(call.call_id, output))

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

    async def _stream_round(
        self, instructions: str, items: list[InputItem], tools: list[dict[str, object]], stream: TurnStream
    ) -> _Round:
        flush_s = self._settings.stream_flush_ms / 1000
        pending = ""
        last_flush = time.monotonic()
        result = _Round(calls=[], usage=Usage())

        events = self._llm.stream(
            ModelRole.CHAT,
            instructions,
            items,
            temperature=self._settings.chat_temperature,
            max_output_tokens=self._settings.max_output_tokens,
            tools=tools or None,
        )
        async for event in events:
            if isinstance(event, TextDelta):
                pending += event.text
                if time.monotonic() - last_flush >= flush_s:
                    await stream.publish_delta(pending)
                    pending, last_flush = "", time.monotonic()
                    if await stream.cancel_requested():
                        # Stop reading the model stream; the partial text is kept and marked stopped.
                        result.stopped = True
                        return result
            elif isinstance(event, FunctionCall):
                result.calls.append(event)
            elif isinstance(event, Completed):
                result.usage = event.usage

        await stream.publish_delta(pending)
        if result.calls and await stream.cancel_requested():
            result.stopped = True
        return result

    async def _run_tool(
        self, session_id: UUID, request: StartAiTurnRequest, call: FunctionCall, bearer_token: str, stream: TurnStream
    ) -> str:
        """Validates the call and turns a mutating tool into an approval request. Returns the tool output (JSON)."""
        tool = get_tool(call.name)
        if tool is None:
            return _tool_output("error", message=f"Unknown tool '{call.name}'.")
        try:
            args = tool.parse(call.arguments)
        except ToolValidationError as exc:
            log.info("tool_args_invalid", turn_id=str(request.turn_id), tool=call.name, detail=str(exc))
            return _tool_output("error", message=f"Invalid arguments: {exc}")
        if not tool.mutates or self._approvals is None:
            return _tool_output("error", message="This tool cannot run here.")

        await stream.publish_progress("Session.Progress.RequestingApproval")
        summary = tool.summarize(args, request.language)
        try:
            request_id = await self._approvals.create(
                ApprovalRequest(
                    workspace_id=request.workspace_id,
                    room_id=request.room_id,
                    session_id=session_id,
                    turn_id=request.turn_id,
                    tool=tool.name,
                    args=args.model_dump(mode="json", by_alias=True, exclude_none=True),
                    summary=summary,
                    reason=None,
                ),
                bearer_token,
                request.language,
            )
        except ApprovalRequestError as exc:
            log.warning("tool_approval_failed", turn_id=str(request.turn_id), tool=call.name, reason=exc.key)
            return _tool_output("rejected" if exc.status in (400, 403) else "error", message=exc.key)
        log.info("tool_approval_requested", turn_id=str(request.turn_id), tool=call.name, request_id=str(request_id))
        return _tool_output("pending_approval", requestId=str(request_id), summary=summary)


def _tool_output(status: str, **fields: str) -> str:
    return json.dumps({"status": status, **fields}, ensure_ascii=False)


def _add(a: Usage, b: Usage) -> Usage:
    return Usage(a.input_tokens + b.input_tokens, a.output_tokens + b.output_tokens, a.cached_tokens + b.cached_tokens)
