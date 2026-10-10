"""
Turn results → RabbitMQ for Rooms to sequence. Rooms consumes with MassTransit 8, so messages are published in the
MassTransit JSON envelope to the exchange named after the .NET message type (ADR-0008). This is the only module that
knows the envelope; changing the .NET bus changes only this adapter.
"""

import json
from datetime import UTC, datetime
from typing import Protocol
from uuid import uuid4

import aio_pika
from aio_pika.abc import AbstractRobustChannel, AbstractRobustConnection

from ai_service.sessions.contracts import TurnCompleted, TurnFailed, TurnResult, TurnStopped

DOTNET_NAMESPACE = "Majlis.Framework.Domain.Events"
MESSAGE_TYPES: dict[type[TurnResult], str] = {
    TurnCompleted: "TurnCompleted",
    TurnStopped: "TurnStopped",
    TurnFailed: "TurnFailed",
}


def exchange_name(message: TurnResult) -> str:
    return f"{DOTNET_NAMESPACE}:{MESSAGE_TYPES[type(message)]}"


def envelope(message: TurnResult, source_host: str = "ai-service") -> dict[str, object]:
    """The MassTransit envelope (application/vnd.masstransit+json)."""
    name = exchange_name(message)
    return {
        "messageId": str(uuid4()),
        "sourceAddress": f"rabbitmq://localhost/{source_host}",
        "destinationAddress": f"rabbitmq://localhost/{name}",
        "messageType": [f"urn:message:{name}"],
        "message": message.model_dump(mode="json", by_alias=True),
        "sentTime": datetime.now(UTC).isoformat(),
        "headers": {},
        "host": {"machineName": source_host, "processName": "ai-service", "frameworkVersion": "python"},
    }


class ResultPublisher(Protocol):
    async def publish(self, message: TurnResult) -> None: ...


class RabbitResultPublisher:
    def __init__(self, url: str) -> None:
        self._url = url
        self._connection: AbstractRobustConnection | None = None
        self._channel: AbstractRobustChannel | None = None

    async def start(self) -> None:
        self._connection = await aio_pika.connect_robust(self._url)
        self._channel = await self._connection.channel(publisher_confirms=True)

    @property
    def is_connected(self) -> bool:
        return self._connection is not None and not self._connection.is_closed

    async def publish(self, message: TurnResult) -> None:
        if self._channel is None:
            raise RuntimeError("Publisher not started.")
        # Same exchange shape MassTransit declares: durable fanout, named after the message type.
        exchange = await self._channel.declare_exchange(
            exchange_name(message), aio_pika.ExchangeType.FANOUT, durable=True
        )
        await exchange.publish(
            aio_pika.Message(
                body=json.dumps(envelope(message), ensure_ascii=False).encode("utf-8"),
                content_type="application/vnd.masstransit+json",
                delivery_mode=aio_pika.DeliveryMode.PERSISTENT,
                message_id=str(uuid4()),
            ),
            routing_key="",
        )

    async def close(self) -> None:
        if self._connection is not None:
            await self._connection.close()
