"""
Turn results → RabbitMQ for Rooms to sequence. The backend runs Wolverine (ADR-0009), which consumes plain JSON:
camelCase body, the message alias in the AMQP `type` property, published to the durable fanout exchange
`majlis.{alias}` that Rooms declares and binds its queue to. Nothing here depends on the .NET bus library.
"""

import json
from typing import Protocol
from uuid import uuid4

import aio_pika
from aio_pika.abc import AbstractRobustChannel, AbstractRobustConnection

from ai_service.messaging.contracts import DocumentIndexed, DocumentIndexingFailed
from ai_service.sessions.contracts import CamelModel, TurnCompleted, TurnFailed, TurnResult, TurnStopped

OutboundMessage = TurnResult | DocumentIndexed | DocumentIndexingFailed

EXCHANGE_PREFIX = "majlis."
ALIASES: dict[type[CamelModel], str] = {
    TurnCompleted: "turn-completed",
    TurnStopped: "turn-stopped",
    TurnFailed: "turn-failed",
    DocumentIndexed: "document-indexed",
    DocumentIndexingFailed: "document-indexing-failed",
}


def alias_of(message: OutboundMessage) -> str:
    return ALIASES[type(message)]


def exchange_name(message: OutboundMessage) -> str:
    return EXCHANGE_PREFIX + alias_of(message)


def body_of(message: OutboundMessage) -> bytes:
    return json.dumps(message.model_dump(mode="json", by_alias=True), ensure_ascii=False).encode("utf-8")


class ResultPublisher(Protocol):
    async def publish(self, message: OutboundMessage) -> None: ...


class RabbitResultPublisher:
    def __init__(self, url: str) -> None:
        self._url = url
        self._connection: AbstractRobustConnection | None = None
        self._channel: AbstractRobustChannel | None = None

    async def start(self) -> None:
        self._connection = await aio_pika.connect_robust(self._url)
        self._channel = await self._connection.channel(publisher_confirms=True)

    async def open_channel(self) -> AbstractRobustChannel:
        """A separate channel on the same connection, for consumers (one channel per role)."""
        if self._connection is None:
            raise RuntimeError("Publisher not started.")
        return await self._connection.channel()

    @property
    def is_connected(self) -> bool:
        return self._connection is not None and not self._connection.is_closed

    async def publish(self, message: OutboundMessage) -> None:
        if self._channel is None:
            raise RuntimeError("Publisher not started.")
        # Same declaration Wolverine makes (durable fanout); RabbitMQ rejects a mismatch, so the two must stay equal.
        exchange = await self._channel.declare_exchange(
            exchange_name(message), aio_pika.ExchangeType.FANOUT, durable=True
        )
        await exchange.publish(
            aio_pika.Message(
                body=body_of(message),
                content_type="application/json",
                type=alias_of(message),
                message_id=str(uuid4()),
                delivery_mode=aio_pika.DeliveryMode.PERSISTENT,
            ),
            routing_key="",
        )

    async def close(self) -> None:
        if self._connection is not None:
            await self._connection.close()
