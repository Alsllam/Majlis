from typing import Any
from unittest.mock import patch
from uuid import uuid4

import httpx
import pytest
from cryptography.hazmat.primitives.asymmetric import rsa
from fastapi import FastAPI

from ai_service.api.errors import register_error_handlers
from ai_service.api.routes import internal_turns
from ai_service.security.tokens import TokenValidator
from ai_service.settings import Settings
from tests.conftest import ISSUER, jwk_for, make_token, turn_request


class RecordingRunner:
    def __init__(self) -> None:
        self.runs: list[Any] = []

    async def run(self, session_id: Any, request: Any) -> None:
        self.runs.append((session_id, request))


@pytest.fixture
def app(settings: Settings, rsa_key: rsa.RSAPrivateKey) -> FastAPI:
    app = FastAPI()
    register_error_handlers(app)
    app.include_router(internal_turns.router)
    validator = TokenValidator(ISSUER, "ai-api", "http://unused/jwks")
    patcher = patch.object(validator._jwks, "get_signing_key_from_jwt", return_value=jwk_for(rsa_key))
    patcher.start()
    app.state.settings = settings
    app.state.token_validator = validator
    app.state.turn_runner = RecordingRunner()
    return app


async def post(app: FastAPI, token: str | None, body: dict[str, Any], lang: str = "ar") -> httpx.Response:
    headers = {"accept-language": lang} | ({"authorization": f"Bearer {token}"} if token else {})
    async with httpx.AsyncClient(transport=httpx.ASGITransport(app=app), base_url="http://test") as client:
        return await client.post(f"/internal/sessions/{uuid4()}/turns", json=body, headers=headers)


def body(**overrides: Any) -> dict[str, Any]:
    return turn_request(**overrides).model_dump(mode="json", by_alias=True)


async def test_accepts_turn_and_runs_it_in_background(app: FastAPI, rsa_key: rsa.RSAPrivateKey) -> None:
    payload = body()
    response = await post(app, make_token(rsa_key), payload)

    assert response.status_code == 202
    assert response.json() == {"turnId": payload["turnId"]}
    assert len(app.state.turn_runner.runs) == 1


async def test_rejects_missing_token_with_backend_error_shape(app: FastAPI) -> None:
    response = await post(app, None, body(), lang="en")

    assert response.status_code == 401
    assert response.json()["error"]["source"] == "Ai"
    assert response.json()["error"]["messages"] == ["You need to sign in."]


@pytest.mark.parametrize(
    "claims",
    [{"aud": "majlis-api"}, {"iss": "http://evil/"}, {"exp": 1}],
    ids=["wrong-audience", "wrong-issuer", "expired"],
)
async def test_rejects_invalid_tokens(app: FastAPI, rsa_key: rsa.RSAPrivateKey, claims: dict[str, Any]) -> None:
    response = await post(app, make_token(rsa_key, **claims), body())

    assert response.status_code == 401


async def test_rejects_body_tenant_that_differs_from_token(app: FastAPI, rsa_key: rsa.RSAPrivateKey) -> None:
    response = await post(app, make_token(rsa_key), body(tenantId=uuid4()))

    assert response.status_code == 403
    assert app.state.turn_runner.runs == []


async def test_returns_503_when_no_model_is_configured(app: FastAPI, rsa_key: rsa.RSAPrivateKey) -> None:
    app.state.settings = app.state.settings.model_copy(update={"azure_openai_endpoint": ""})

    response = await post(app, make_token(rsa_key), body())

    assert response.status_code == 503
    assert response.json()["error"]["messages"] == ["لم يتم إعداد نموذج الذكاء الاصطناعي في هذه البيئة."]
