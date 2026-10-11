"""
Consumes `MinutesRequested` / `TranscriptionRequested` from the Meetings module (exchanges `majlis.{alias}`,
queue `ai-service.{alias}`; ADR-0009) and answers with `MinutesGenerated` / `MinutesFailed` and
`TranscriptionCompleted` / `TranscriptionFailed`. Runs inside the API process like the ingestion worker.
"""

import asyncio
import json
from collections.abc import Awaitable, Callable

import aio_pika
from aio_pika.abc import AbstractIncomingMessage, AbstractRobustChannel

from ai_service.llm.provider import LlmError, LlmProvider
from ai_service.meetings.minutes import MinutesGenerator
from ai_service.messaging.bus import EXCHANGE_PREFIX, ResultPublisher
from ai_service.messaging.contracts import (
    MinutesFailed,
    MinutesGenerated,
    MinutesRequested,
    TranscriptionCompleted,
    TranscriptionFailed,
    TranscriptionRequested,
)
from ai_service.observability import get_logger
from ai_service.rag.ports import BlobStore

log = get_logger(__name__)

QUEUE_PREFIX = "ai-service."
_EXTENSIONS = {"audio/mpeg": "mp3", "audio/mp4": "m4a", "audio/x-m4a": "m4a", "audio/wav": "wav", "audio/webm": "webm"}


class MeetingsWorker:
    def __init__(
        self,
        channel: AbstractRobustChannel | None,
        generator: MinutesGenerator,
        llm: LlmProvider,
        publisher: ResultPublisher,
        recordings: BlobStore | None,
        *,
        max_recording_mb: int = 25,
        concurrency: int = 2,
    ) -> None:
        self._channel = channel
        self._generator = generator
        self._llm = llm
        self._publisher = publisher
        self._recordings = recordings
        self._max_bytes = max_recording_mb * 1024 * 1024
        self._limit = asyncio.Semaphore(concurrency)
        self._tags: list[str] = []

    async def start(self) -> None:
        await self._open().set_qos(prefetch_count=4)
        await self._bind("minutes-requested", self._on_minutes)
        await self._bind("transcription-requested", self._on_transcription)

    async def _bind(self, alias: str, handler: Callable[[AbstractIncomingMessage], Awaitable[None]]) -> None:
        channel = self._open()
        exchange = await channel.declare_exchange(EXCHANGE_PREFIX + alias, aio_pika.ExchangeType.FANOUT, durable=True)
        queue = await channel.declare_queue(QUEUE_PREFIX + alias, durable=True)
        await queue.bind(exchange)
        self._tags.append(await queue.consume(handler))

    def _open(self) -> AbstractRobustChannel:
        if self._channel is None:
            raise RuntimeError("Worker channel not opened.")
        return self._channel

    async def _on_minutes(self, message: AbstractIncomingMessage) -> None:
        async with self._limit, message.process(requeue=False, ignore_processed=True):
            try:
                event = MinutesRequested.model_validate(json.loads(message.body))
            except ValueError:
                log.warning("meetings_bad_message", body=message.body[:200])
                return
            await self._publisher.publish(await self.handle_minutes(event))

    async def _on_transcription(self, message: AbstractIncomingMessage) -> None:
        async with self._limit, message.process(requeue=False, ignore_processed=True):
            try:
                event = TranscriptionRequested.model_validate(json.loads(message.body))
            except ValueError:
                log.warning("meetings_bad_message", body=message.body[:200])
                return
            await self._publisher.publish(await self.handle_transcription(event))

    async def handle_minutes(self, event: MinutesRequested) -> MinutesGenerated | MinutesFailed:
        """Never raises: a failure is reported to Meetings with a localization key (no transcript content in logs)."""
        try:
            result = await self._generator.generate(event)
        except LlmError as exc:
            log.warning("minutes_failed", meeting_id=str(event.meeting_id), key=exc.key)
            return MinutesFailed(tenant_id=event.tenant_id, meeting_id=event.meeting_id, reason_key=exc.key)
        except Exception as exc:
            log.exception("minutes_crashed", meeting_id=str(event.meeting_id))
            return MinutesFailed(
                tenant_id=event.tenant_id,
                meeting_id=event.meeting_id,
                reason_key="General:Errors:Unexpected",
                detail=type(exc).__name__,
            )
        log.info("minutes_generated", meeting_id=str(event.meeting_id), action_items=len(result.action_items))
        return result

    async def handle_transcription(self, event: TranscriptionRequested) -> TranscriptionCompleted | TranscriptionFailed:
        """Never raises: every failure goes back to Meetings as a localization key."""
        loaded = await self._load_recording(event)
        if isinstance(loaded, TranscriptionFailed):
            return loaded
        extension = _EXTENSIONS.get(event.content_type, "mp3")
        try:
            text = await self._llm.transcribe(loaded, f"recording.{extension}", event.content_type, event.language)
        except LlmError as exc:
            return self._failed(event, exc.key)
        except Exception as exc:
            log.exception("transcription_crashed", meeting_id=str(event.meeting_id))
            return self._failed(event, "General:Errors:Unexpected", type(exc).__name__)
        if not text.strip():
            return self._failed(event, "Meetings:Transcription:Empty")
        log.info("transcription_completed", meeting_id=str(event.meeting_id), chars=len(text))
        return TranscriptionCompleted(
            tenant_id=event.tenant_id, meeting_id=event.meeting_id, text=text.strip(), language=event.language
        )

    async def _load_recording(self, event: TranscriptionRequested) -> bytes | TranscriptionFailed:
        if self._recordings is None:
            return self._failed(event, "Meetings:Transcription:NotConfigured")
        try:
            data = await self._recordings.download(event.blob_path)
        except Exception as exc:
            log.exception("recording_download_failed", meeting_id=str(event.meeting_id))
            return self._failed(event, "Meetings:Transcription:RecordingUnavailable", type(exc).__name__)
        if len(data) > self._max_bytes:
            return self._failed(event, "Meetings:Transcription:TooLarge", f"{len(data)} bytes")
        return data

    @staticmethod
    def _failed(event: TranscriptionRequested, key: str, detail: str | None = None) -> TranscriptionFailed:
        log.warning("transcription_failed", meeting_id=str(event.meeting_id), key=key)
        return TranscriptionFailed(
            tenant_id=event.tenant_id, meeting_id=event.meeting_id, reason_key=key, detail=detail
        )
