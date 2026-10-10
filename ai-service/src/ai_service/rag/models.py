"""RAG data shapes shared by ingestion, the index adapters and retrieval."""

from dataclasses import dataclass, field
from uuid import UUID


@dataclass(frozen=True, slots=True)
class IndexedChunk:
    """One chunk as stored in the search index (skill §5 schema)."""

    id: str
    tenant_id: UUID
    document_id: UUID
    version_id: UUID
    title: str
    heading_path: str
    page: int
    language: str
    doc_type: str
    acl_groups: list[str]
    content: str
    content_search: str
    content_vector: list[float] = field(default_factory=list)
    ordinal: int = 0
    content_sha256: str = ""


@dataclass(frozen=True, slots=True)
class SearchHit:
    chunk: IndexedChunk
    score: float


@dataclass(frozen=True, slots=True)
class ExtractedDocument:
    """Markdown-ish text with `[[page N]]` markers (see chunker.parse_blocks) and the page count."""

    text: str
    page_count: int


class IngestionError(Exception):
    """`key` is a localization key the backend shows (Knowledge resources)."""

    key = "Knowledge:Ingestion:Failed"

    def __init__(self, detail: str | None = None) -> None:
        super().__init__(detail or self.key)
        self.detail = detail


class UnsupportedDocumentError(IngestionError):
    key = "Knowledge:Ingestion:Unsupported"


class EmptyDocumentError(IngestionError):
    key = "Knowledge:Ingestion:Empty"
