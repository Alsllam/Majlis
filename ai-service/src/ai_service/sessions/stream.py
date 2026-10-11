"""
The stream lane (ADR-0003) and turn signals, in Redis:
- PUBLISH `majlis:session:{sessionId}:stream` — delta / progress messages, forwarded by the Realtime host;
- HSET `majlis:turn:{turnId}:text` {text, chunk} — the running text for late joiners;
- `majlis:turn:{turnId}:cancel` — set by Rooms on stop;
- `majlis:turn:{turnId}:hb` — refreshed while the turn runs.
"""

import json
from uuid import UUID

from redis.asyncio import Redis


def stream_channel(session_id: UUID) -> str:
    return f"majlis:session:{session_id}:stream"


def text_key(turn_id: UUID) -> str:
    return f"majlis:turn:{turn_id.hex}:text"


def cancel_key(turn_id: UUID) -> str:
    return f"majlis:turn:{turn_id.hex}:cancel"


def heartbeat_key(turn_id: UUID) -> str:
    return f"majlis:turn:{turn_id.hex}:hb"


class TurnStream:
    """Publishes coalesced deltas for one turn and keeps its snapshot."""

    def __init__(self, redis: Redis, session_id: UUID, turn_id: UUID, snapshot_ttl_s: int) -> None:
        self._redis = redis
        self._session_id = session_id
        self._turn_id = turn_id
        self._ttl = snapshot_ttl_s
        self.text = ""
        self.chunk = 0

    async def publish_delta(self, delta: str) -> None:
        if not delta:
            return
        self.chunk += 1
        self.text += delta
        message = {"type": "turn.delta", "turnId": str(self._turn_id), "chunk": self.chunk, "data": {"text": delta}}
        async with self._redis.pipeline(transaction=False) as pipe:
            pipe.hset(text_key(self._turn_id), mapping={"text": self.text, "chunk": self.chunk})
            pipe.expire(text_key(self._turn_id), self._ttl)
            pipe.publish(stream_channel(self._session_id), json.dumps(message, ensure_ascii=False))
            await pipe.execute()

    async def publish_progress(self, key: str, params: dict[str, object] | None = None) -> None:
        self.chunk += 1
        message = {
            "type": "turn.progress",
            "turnId": str(self._turn_id),
            "chunk": self.chunk,
            "data": {"key": key, "params": params or {}},
        }
        await self._redis.publish(stream_channel(self._session_id), json.dumps(message, ensure_ascii=False))

    async def cancel_requested(self) -> bool:
        return bool(await self._redis.exists(cancel_key(self._turn_id)))

    async def beat(self, ttl_s: int) -> None:
        await self._redis.set(heartbeat_key(self._turn_id), "1", ex=ttl_s)

    async def finish(self) -> None:
        await self._redis.delete(heartbeat_key(self._turn_id), cancel_key(self._turn_id))
