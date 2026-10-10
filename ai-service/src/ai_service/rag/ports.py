"""
The adapter interfaces business code depends on (ADR-0006). Cloud implementations live in `rag/adapters`;
on-prem ones are added without touching callers. A small local index exists for development only.
"""

from collections.abc import Sequence
from typing import Protocol
from uuid import UUID

from ai_service.rag.models import ExtractedDocument, IndexedChunk, SearchHit


class BlobStore(Protocol):
    async def download(self, path: str) -> bytes: ...

    async def aclose(self) -> None: ...


class DocumentExtractor(Protocol):
    def supports(self, content_type: str) -> bool: ...

    async def extract(self, data: bytes, content_type: str, file_name: str) -> ExtractedDocument: ...

    async def aclose(self) -> None: ...


class Embedder(Protocol):
    @property
    def dimensions(self) -> int: ...

    async def embed(self, texts: Sequence[str]) -> list[list[float]]:
        """One vector per input, in order. Raises `LlmError` subclasses."""
        ...


class SearchIndex(Protocol):
    async def ensure_index(self) -> None: ...

    async def upsert(self, chunks: Sequence[IndexedChunk]) -> None: ...

    async def delete_document(self, tenant_id: UUID, document_id: UUID, *, keep_version: UUID | None = None) -> int:
        """Removes the document's chunks (all versions, or every version but `keep_version`). Returns the count."""
        ...

    async def version_info(self, tenant_id: UUID, document_id: UUID, version_id: UUID) -> tuple[str, int] | None:
        """(content hash, chunk count) already indexed for this version, for idempotent re-runs; None when absent."""
        ...

    async def search(
        self,
        *,
        tenant_id: UUID,
        acl_groups: Sequence[str],
        query: str,
        query_search: str,
        vector: Sequence[float] | None,
        top: int,
    ) -> list[SearchHit]:
        """Hybrid search trimmed to the tenant and the ACL groups (AI-RAG-004), best first."""
        ...

    async def aclose(self) -> None: ...
