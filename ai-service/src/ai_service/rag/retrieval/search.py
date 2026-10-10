"""Retrieval (skill §6): embed the question, hybrid search with security trimming, select and de-duplicate."""

from collections.abc import Sequence
from dataclasses import dataclass
from uuid import UUID

from ai_service.rag.ingestion.normalize import normalize_for_search
from ai_service.rag.models import SearchHit
from ai_service.rag.ports import Embedder, SearchIndex


@dataclass(frozen=True, slots=True)
class RetrievalOptions:
    top: int = 8
    min_score: float = 0.15
    per_document: int = 3


class Retriever:
    def __init__(self, index: SearchIndex, embedder: Embedder, options: RetrievalOptions) -> None:
        self._index = index
        self._embedder = embedder
        self._options = options

    async def retrieve(self, *, tenant_id: UUID, acl_groups: Sequence[str], question: str) -> list[SearchHit]:
        """Best chunks the caller may read; empty when nothing passes the threshold (then the agent says so)."""
        vector = (await self._embedder.embed([question]))[0]
        hits = await self._index.search(
            tenant_id=tenant_id,
            acl_groups=acl_groups,
            query=question,
            query_search=normalize_for_search(question),
            vector=vector,
            top=max(self._options.top * 3, 20),
        )
        selected: list[SearchHit] = []
        per_doc: dict[UUID, int] = {}
        seen: set[str] = set()
        for hit in hits:
            if hit.score < self._options.min_score:
                continue
            key = hit.chunk.content_search[:200]
            if key in seen:
                continue
            if per_doc.get(hit.chunk.document_id, 0) >= self._options.per_document:
                continue
            seen.add(key)
            per_doc[hit.chunk.document_id] = per_doc.get(hit.chunk.document_id, 0) + 1
            selected.append(hit)
            if len(selected) >= self._options.top:
                break
        return selected
