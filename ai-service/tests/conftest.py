import asyncio
import json
import time
from collections.abc import AsyncIterator, Sequence
from typing import Any
from uuid import UUID, uuid4

import fakeredis
import jwt
import pytest
from cryptography.hazmat.primitives.asymmetric import rsa
from jwt.algorithms import RSAAlgorithm

from ai_service.llm.models import ModelRole
from ai_service.llm.provider import ChatMessage, Completed, StreamEvent, TextDelta, Usage
from ai_service.sessions.contracts import AiActor, StartAiTurnRequest, TurnResult
from ai_service.settings import Settings

ISSUER = "http://localhost:7000/"
TENANT = UUID("8f2b6c3e-1d4a-4f6b-9a2e-5c7d8e9f0a1b")
SARA = UUID("eb5324a7-ac88-43f4-ae05-dbef63109d27")


@pytest.fixture
def settings() -> Settings:
    return Settings(
        azure_openai_endpoint="https://unit-test.openai.azure.com",
        azure_openai_api_key="test-key",  # type: ignore[arg-type]
        azure_openai_chat_deployment="chat-test",
        auth_issuer=ISSUER,
        stream_flush_ms=10,
        heartbeat_interval_s=0.05,
        turn_timeout_s=2,
    )


@pytest.fixture
def redis() -> fakeredis.FakeAsyncRedis:
    return fakeredis.FakeAsyncRedis(decode_responses=True)


def turn_request(**overrides: Any) -> StartAiTurnRequest:
    data: dict[str, Any] = {
        "turnId": uuid4(),
        "tenantId": TENANT,
        "workspaceId": uuid4(),
        "roomId": uuid4(),
        "instruction": "لخّص البنود الرئيسية",
        "language": "ar",
        "instructedBy": AiActor(user_id=SARA, display_name="سارة"),
        "history": [],
    }
    data.update(overrides)
    return StartAiTurnRequest.model_validate(data)


class FakeLlm:
    """Yields scripted deltas with small pauses; optionally raises after them."""

    def __init__(self, deltas: Sequence[str], delay_s: float = 0.02, error: Exception | None = None) -> None:
        self.deltas = deltas
        self.delay_s = delay_s
        self.error = error
        self.calls: list[list[ChatMessage]] = []
        self.instructions: list[str] = []

    async def stream(
        self,
        role: ModelRole,
        instructions: str,
        messages: Sequence[ChatMessage],
        *,
        temperature: float,
        max_output_tokens: int,
    ) -> AsyncIterator[StreamEvent]:
        self.calls.append(list(messages))
        self.instructions.append(instructions)
        for delta in self.deltas:
            await asyncio.sleep(self.delay_s)
            yield TextDelta(delta)
        if self.error is not None:
            raise self.error
        yield Completed(Usage(input_tokens=120, output_tokens=30, cached_tokens=64))

    async def aclose(self) -> None:
        return None


class FakePublisher:
    def __init__(self) -> None:
        self.published: list[TurnResult] = []

    async def publish(self, message: TurnResult) -> None:
        self.published.append(message)


@pytest.fixture
def rsa_key() -> rsa.RSAPrivateKey:
    return rsa.generate_private_key(public_exponent=65537, key_size=2048)


def make_token(key: rsa.RSAPrivateKey, **overrides: Any) -> str:
    claims: dict[str, Any] = {
        "iss": ISSUER,
        "aud": ["majlis-api", "ai-api"],
        "sub": str(SARA),
        "tenant_id": str(TENANT),
        "name": "سارة",
        "role": "TenantAdmin",
        "exp": int(time.time()) + 600,
    }
    claims.update(overrides)
    return jwt.encode(claims, key, algorithm="RS256", headers={"kid": "test"})


def jwk_for(key: rsa.RSAPrivateKey) -> jwt.PyJWK:
    data = json.loads(RSAAlgorithm.to_jwk(key.public_key()))
    data.update({"kid": "test", "alg": "RS256", "use": "sig"})
    return jwt.PyJWK.from_dict(data)
