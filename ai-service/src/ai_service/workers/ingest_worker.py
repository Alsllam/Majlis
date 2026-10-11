"""
Consumes `DocumentUploaded` / `DocumentDeleted` from RabbitMQ (durable fanout exchanges `majlis.{alias}` that
Knowledge declares; ADR-0009) and runs the ingestion pipeline. Runs inside the API process for the walking
skeleton; it becomes its own container when ingestion load needs it (skill §14).
"""

import asyncio
import json
from collections.abc import Awaitable, Callable

import aio_pika
from aio_pika.abc import AbstractIncomingMessage, AbstractRobustChannel

from ai_service.messaging.bus import EXCHANGE_PREFIX, ResultPublisher
from ai_service.messaging.contracts import DocumentDeleted, DocumentUploaded
from ai_service.observability import get_logger
from ai_service.rag.ingestion.pipeline import IngestionPipeline

log = get_logger(__name__)

QUEUE_PREFIX = "ai-service."


class IngestWorker:
    def __init__(
        self,
        channel: AbstractRobustChannel,
        pipeline: IngestionPipeline,
        publisher: ResultPublisher,
        concurrency: int = 2,
    ) -> None:
        self._channel = channel
        self._pipeline = pipeline
        self._publisher = publisher
        self._limit = asyncio.Semaphore(concurrency)
        self._tags: list[str] = []

    async def start(self) -> None:
        await self._channel.set_qos(prefetch_count=4)
        await self._bind("document-uploaded", self._on_uploaded)
        await self._bind("document-deleted", self._on_deleted)

    async def _bind(self, alias: str, handler: Callable[[AbstractIncomingMessage], Awaitable[None]]) -> None:
        exchange = await self._channel.declare_exchange(
            EXCHANGE_PREFIX + alias, aio_pika.ExchangeType.FANOUT, durable=True
        )
        queue = await self._channel.declare_queue(QUEUE_PREFIX + alias, durable=True)
        await queue.bind(exchange)
        self._tags.append(await queue.consume(handler))

    async def _on_uploaded(self, message: AbstractIncomingMessage) -> None:
        async with self._limit, message.process(requeue=False, ignore_processed=True):
            try:
                event = DocumentUploaded.model_validate(json.loads(message.body))
            except ValueError:
                log.warning("ingest_bad_message", body=message.body[:200])
                return
            result = await self._pipeline.ingest(event)
            await self._publisher.publish(result)

    async def _on_deleted(self, message: AbstractIncomingMessage) -> None:
        async with self._limit, message.process(requeue=False, ignore_processed=True):
            try:
                event = DocumentDeleted.model_validate(json.loads(message.body))
            except ValueError:
                log.warning("ingest_bad_message", body=message.body[:200])
                return
            await self._pipeline.delete(event)
