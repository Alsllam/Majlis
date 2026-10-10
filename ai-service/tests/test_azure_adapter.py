import json
from collections.abc import Callable

import httpx2
import openai
import pytest

from ai_service.llm.adapters.azure_openai import AzureOpenAIProvider
from ai_service.llm.models import ModelRole
from ai_service.llm.provider import (
    ChatMessage,
    Completed,
    LlmBusyError,
    LlmContentBlockedError,
    LlmNotConfiguredError,
    TextDelta,
)
from ai_service.settings import Settings

URL = "https://unit-test.openai.azure.com/openai/v1/responses"


def sse(*events: dict[str, object]) -> bytes:
    return "".join(f"event: {e['type']}\ndata: {json.dumps(e, ensure_ascii=False)}\n\n" for e in events).encode()


def delta(text: str, seq: int) -> dict[str, object]:
    return {
        "type": "response.output_text.delta",
        "delta": text,
        "item_id": "msg_1",
        "output_index": 0,
        "content_index": 0,
        "sequence_number": seq,
        "logprobs": [],
    }


COMPLETED = {
    "type": "response.completed",
    "sequence_number": 9,
    "response": {
        "id": "resp_1",
        "object": "response",
        "created_at": 0,
        "model": "chat-test",
        "status": "completed",
        "output": [],
        "parallel_tool_calls": True,
        "tool_choice": "auto",
        "tools": [],
        "usage": {
            "input_tokens": 120,
            "output_tokens": 7,
            "total_tokens": 127,
            "input_tokens_details": {"cached_tokens": 64},
            "output_tokens_details": {"reasoning_tokens": 0},
        },
    },
}


Handler = Callable[[httpx2.Request], httpx2.Response]


def provider_with(settings: Settings, handler: Handler) -> AzureOpenAIProvider:
    """The OpenAI SDK 3.x speaks httpx2; requests go to a mock transport, never the network."""
    client = openai.AsyncOpenAI(
        base_url="https://unit-test.openai.azure.com/openai/v1/",
        api_key="test-key",
        max_retries=0,
        http_client=openai.DefaultAsyncHttpx2Client(transport=httpx2.MockTransport(handler)),
    )
    return AzureOpenAIProvider(settings, client=client)


def stream_response(*events: dict[str, object]) -> Handler:
    return lambda _: httpx2.Response(200, content=sse(*events), headers={"content-type": "text/event-stream"})


async def collect(provider: AzureOpenAIProvider) -> list[object]:
    return [
        e
        async for e in provider.stream(
            ModelRole.CHAT, "instructions", [ChatMessage("user", "مرحبا")], temperature=0.3, max_output_tokens=100
        )
    ]


async def test_stream_yields_deltas_and_usage_from_azure_sse(settings: Settings) -> None:
    requests: list[httpx2.Request] = []

    def handler(request: httpx2.Request) -> httpx2.Response:
        requests.append(request)
        return stream_response(delta("وفقاً ", 1), delta("للمادة ١٢", 2), COMPLETED)(request)

    events = await collect(provider_with(settings, handler))

    assert [e.text for e in events if isinstance(e, TextDelta)] == ["وفقاً ", "للمادة ١٢"]
    assert isinstance(events[-1], Completed)
    usage = events[-1].usage
    assert (usage.input_tokens, usage.output_tokens, usage.cached_tokens) == (120, 7, 64)
    assert str(requests[0].url) == URL
    body = json.loads(requests[0].content)
    assert (body["model"], body["stream"], body["store"]) == ("chat-test", True, False)
    assert requests[0].headers["authorization"] == "Bearer test-key"


async def test_rate_limit_maps_to_busy(settings: Settings) -> None:
    handler = lambda _: httpx2.Response(429, json={"error": {"code": "429", "message": "slow down"}})  # noqa: E731

    with pytest.raises(LlmBusyError):
        await collect(provider_with(settings, handler))


async def test_content_filter_on_prompt_maps_to_blocked(settings: Settings) -> None:
    error = {"error": {"code": "content_filter", "message": "filtered", "type": "invalid_request_error"}}
    handler = lambda _: httpx2.Response(400, json=error)  # noqa: E731

    with pytest.raises(LlmContentBlockedError):
        await collect(provider_with(settings, handler))


async def test_content_filter_mid_stream_maps_to_blocked(settings: Settings) -> None:
    error = {"type": "error", "code": "content_filter", "message": "filtered", "param": None, "sequence_number": 2}

    with pytest.raises(LlmContentBlockedError):
        await collect(provider_with(settings, stream_response(delta("نص", 1), error)))


async def test_missing_configuration_raises_not_configured() -> None:
    with pytest.raises(LlmNotConfiguredError):
        await collect(AzureOpenAIProvider(Settings(azure_openai_endpoint="", azure_openai_chat_deployment="")))
