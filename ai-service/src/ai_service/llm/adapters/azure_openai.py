"""
Cloud adapter: Azure OpenAI through the official `openai` SDK on the v1 endpoint, Responses API (skill §4, §7).
The only module that imports Azure libraries.
"""

from collections.abc import AsyncIterator, Sequence
from typing import Any, cast

import openai
from azure.identity.aio import DefaultAzureCredential, get_bearer_token_provider
from openai import AsyncOpenAI, omit
from openai.types.responses import ResponseInputItemParam, ToolParam

from ai_service.llm.models import ModelRole
from ai_service.llm.provider import (
    ChatMessage,
    Completed,
    FunctionCall,
    InputItem,
    LlmBusyError,
    LlmContentBlockedError,
    LlmError,
    LlmNotConfiguredError,
    StreamEvent,
    TextDelta,
    ToolCallMessage,
    Usage,
)
from ai_service.settings import Settings


def build_client(settings: Settings) -> AsyncOpenAI:
    """API key only in local development; Entra ID (managed identity) elsewhere. Retries are ours, not the SDK's."""
    base_url = f"{settings.azure_openai_endpoint.rstrip('/')}/openai/v1/"
    if settings.azure_openai_api_key is not None:
        return AsyncOpenAI(
            base_url=base_url,
            api_key=settings.azure_openai_api_key.get_secret_value(),
            max_retries=0,
            timeout=settings.llm_timeout_s,
        )
    token_provider = get_bearer_token_provider(DefaultAzureCredential(), "https://ai.azure.com/.default")
    return AsyncOpenAI(base_url=base_url, api_key=token_provider, max_retries=0, timeout=settings.llm_timeout_s)


_TRANSIENT = (openai.RateLimitError, openai.APITimeoutError, openai.APIConnectionError, openai.InternalServerError)


def _input_item(item: InputItem) -> ResponseInputItemParam:
    if isinstance(item, ChatMessage):
        return {"role": item.role, "content": item.content}
    if isinstance(item, ToolCallMessage):
        return {"type": "function_call", "call_id": item.call_id, "name": item.name, "arguments": item.arguments}
    return {"type": "function_call_output", "call_id": item.call_id, "output": item.output}


def _stream_error(code: str | None) -> LlmError:
    """Errors can arrive mid-stream with HTTP 200 (skill §7); map them like HTTP errors."""
    return LlmContentBlockedError() if code == "content_filter" else LlmBusyError()


class AzureOpenAIProvider:
    def __init__(self, settings: Settings, client: AsyncOpenAI | None = None) -> None:
        self._deployments = {
            ModelRole.CHAT: settings.azure_openai_chat_deployment,
            ModelRole.FAST: settings.azure_openai_fast_deployment or settings.azure_openai_chat_deployment,
            ModelRole.EMBED: settings.azure_openai_embed_deployment,
        }
        self._dimensions = settings.embed_dimensions
        self._client = client or (build_client(settings) if settings.llm_configured else None)

    async def stream(
        self,
        role: ModelRole,
        instructions: str,
        messages: Sequence[InputItem],
        *,
        temperature: float,
        max_output_tokens: int,
        tools: Sequence[dict[str, Any]] | None = None,
    ) -> AsyncIterator[StreamEvent]:
        deployment = self._deployments.get(role)
        if self._client is None or not deployment:
            raise LlmNotConfiguredError

        tool_params = [cast(ToolParam, t) for t in tools] if tools else None
        try:
            events = await self._client.responses.create(
                model=deployment,
                instructions=instructions,
                input=[_input_item(m) for m in messages],
                temperature=temperature,
                max_output_tokens=max_output_tokens,
                store=False,
                stream=True,
                tools=tool_params if tool_params else omit,
                tool_choice="auto" if tool_params else omit,
                parallel_tool_calls=False if tool_params else omit,
            )
            async for event in events:
                if event.type == "response.output_text.delta":
                    yield TextDelta(event.delta)
                elif event.type == "response.output_item.done":
                    item = event.item
                    if item.type == "function_call":
                        yield FunctionCall(item.call_id, item.name, str(item.arguments))
                elif event.type == "response.completed":
                    usage = event.response.usage
                    yield Completed(
                        Usage(
                            input_tokens=usage.input_tokens if usage else 0,
                            output_tokens=usage.output_tokens if usage else 0,
                            cached_tokens=usage.input_tokens_details.cached_tokens if usage else 0,
                        )
                    )
                elif event.type == "error":
                    raise _stream_error(event.code)
                elif event.type == "response.failed":
                    raise _stream_error(event.response.error.code if event.response.error else None)
        except openai.BadRequestError as exc:
            if exc.code == "content_filter":
                raise LlmContentBlockedError from exc
            raise LlmError from exc
        except _TRANSIENT as exc:
            raise LlmBusyError from exc
        except (openai.AuthenticationError, openai.PermissionDeniedError, openai.NotFoundError) as exc:
            # Misconfiguration (identity, role or deployment name). Alert-worthy; shown to people as a generic error.
            raise LlmError from exc

    @property
    def dimensions(self) -> int:
        return self._dimensions

    async def embed(self, texts: Sequence[str]) -> list[list[float]]:
        """Embeddings for ingestion and queries (`Embedder`). Batching is the caller's job (skill §5 step 6)."""
        deployment = self._deployments.get(ModelRole.EMBED)
        if self._client is None or not deployment:
            raise LlmNotConfiguredError
        if not texts:
            return []
        try:
            response = await self._client.embeddings.create(
                model=deployment, input=list(texts), dimensions=self._dimensions
            )
        except _TRANSIENT as exc:
            raise LlmBusyError from exc
        except openai.BadRequestError as exc:
            if "content_filter" in str(exc):
                raise LlmContentBlockedError from exc
            raise LlmError from exc
        except openai.APIStatusError as exc:
            raise LlmError from exc
        vectors = sorted(response.data, key=lambda d: d.index)
        return [list(v.embedding) for v in vectors]

    async def aclose(self) -> None:
        if self._client is not None:
            await self._client.close()
