"""
The model interface business code depends on (ADR-0006). The cloud implementation is Azure OpenAI
(`llm/adapters/azure_openai.py`); an OpenAI-compatible on-prem implementation can be added without touching callers.
"""

from collections.abc import AsyncIterator, Sequence
from dataclasses import dataclass
from typing import Literal, Protocol

from ai_service.llm.models import ModelRole


@dataclass(frozen=True, slots=True)
class ChatMessage:
    role: Literal["user", "assistant"]
    content: str


@dataclass(frozen=True, slots=True)
class TextDelta:
    text: str


@dataclass(frozen=True, slots=True)
class Usage:
    input_tokens: int = 0
    output_tokens: int = 0
    cached_tokens: int = 0


@dataclass(frozen=True, slots=True)
class Completed:
    usage: Usage


StreamEvent = TextDelta | Completed


class LlmError(Exception):
    """Base for model errors. `key` is a localization key shown to people."""

    key = "General:Errors:Unexpected"


class LlmBusyError(LlmError):
    """Rate limit, timeout, connection or 5xx: try again later."""

    key = "General:Errors:AiBusy"


class LlmContentBlockedError(LlmError):
    """The content filter blocked the prompt or the output. Never retried."""

    key = "General:Errors:ContentBlocked"


class LlmNotConfiguredError(LlmError):
    key = "General:Errors:AiNotConfigured"


class LlmProvider(Protocol):
    """Chat streaming plus embeddings (the `Embedder` port in `rag/ports.py`), one adapter per profile."""

    @property
    def dimensions(self) -> int: ...

    async def embed(self, texts: Sequence[str]) -> list[list[float]]: ...

    def stream(
        self,
        role: ModelRole,
        instructions: str,
        messages: Sequence[ChatMessage],
        *,
        temperature: float,
        max_output_tokens: int,
    ) -> AsyncIterator[StreamEvent]:
        """Streams text deltas, then one `Completed` with usage. Raises `LlmError` subclasses."""
        ...

    async def aclose(self) -> None: ...
