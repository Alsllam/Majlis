"""Knowledge ↔ ai-service messages (docs/architecture/events.schema.json, camelCase on the wire)."""

from datetime import date, datetime
from uuid import UUID

from pydantic import Field

from ai_service.sessions.contracts import CamelModel


class DocumentUploaded(CamelModel):
    tenant_id: UUID
    workspace_id: UUID
    room_id: UUID | None = None
    document_id: UUID
    version_id: UUID
    blob_path: str
    file_name: str
    content_type: str
    title: str
    doc_type: str = "Other"
    language: str | None = None
    acl_groups: list[str]


class DocumentDeleted(CamelModel):
    tenant_id: UUID
    document_id: UUID


class DocumentIndexed(CamelModel):
    tenant_id: UUID
    document_id: UUID
    version_id: UUID
    chunk_count: int
    sha256: str
    language: str | None = None
    page_count: int = 0


class DocumentIndexingFailed(CamelModel):
    tenant_id: UUID
    document_id: UUID
    version_id: UUID
    reason_key: str
    detail: str | None = None


# Meetings ↔ ai-service (FR-MTG-002/003).


class TranscriptionRequested(CamelModel):
    tenant_id: UUID
    meeting_id: UUID
    blob_path: str
    content_type: str
    language: str | None = None


class TranscriptionCompleted(CamelModel):
    tenant_id: UUID
    meeting_id: UUID
    text: str
    language: str | None = None


class TranscriptionFailed(CamelModel):
    tenant_id: UUID
    meeting_id: UUID
    reason_key: str
    detail: str | None = None


class MinutesRequested(CamelModel):
    tenant_id: UUID
    meeting_id: UUID
    title: str
    held_at: datetime
    attendees: list[str] = Field(default_factory=list)
    language: str = "ar"
    transcript: str


class ActionItemContract(CamelModel):
    text: str
    owner_name: str | None = None
    due_date: date | None = None


class MinutesGenerated(CamelModel):
    tenant_id: UUID
    meeting_id: UUID
    summary: str
    decisions: list[str] = Field(default_factory=list)
    open_questions: list[str] = Field(default_factory=list)
    action_items: list[ActionItemContract] = Field(default_factory=list)
    language: str = "ar"


class MinutesFailed(CamelModel):
    tenant_id: UUID
    meeting_id: UUID
    reason_key: str
    detail: str | None = None
