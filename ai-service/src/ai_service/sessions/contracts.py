"""Shapes shared with Rooms; see docs/architecture/events.schema.json."""

from typing import Literal
from uuid import UUID

from pydantic import BaseModel, ConfigDict, Field
from pydantic.alias_generators import to_camel


class CamelModel(BaseModel):
    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=True, frozen=True)


class AiActor(CamelModel):
    user_id: UUID
    display_name: str


class AiHistoryTurn(CamelModel):
    instruction: str
    answer: str
    instructed_by: str


class StartAiTurnRequest(CamelModel):
    """Rooms → ai-service: `POST /internal/sessions/{sessionId}/turns`."""

    turn_id: UUID
    tenant_id: UUID
    workspace_id: UUID
    room_id: UUID
    instruction: str = Field(min_length=1, max_length=8000)
    language: Literal["ar", "en"]
    instructed_by: AiActor
    history: list[AiHistoryTurn] = Field(default_factory=list, max_length=20)


class Citation(CamelModel):
    label: str
    document_id: UUID
    version_id: UUID
    title: str
    page: int | None = None
    passage: str


class TurnCompleted(CamelModel):
    session_id: UUID
    turn_id: UUID
    text: str
    citations: list[Citation]
    input_tokens: int
    output_tokens: int
    cached_tokens: int


class TurnStopped(CamelModel):
    session_id: UUID
    turn_id: UUID
    partial_text: str


class TurnFailed(CamelModel):
    session_id: UUID
    turn_id: UUID
    reason_key: str
    partial_text: str | None = None


TurnResult = TurnCompleted | TurnStopped | TurnFailed
