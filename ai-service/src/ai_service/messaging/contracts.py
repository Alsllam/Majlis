"""Knowledge ↔ ai-service messages (docs/architecture/events.schema.json, camelCase on the wire)."""

from uuid import UUID

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
