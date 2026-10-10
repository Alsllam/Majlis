"""
DEVELOPMENT ONLY: a tiny search index on Redis for running the product locally without Azure AI Search
(like the stub model). Brute-force cosine over stored vectors plus token overlap on the normalized search copy,
with the same tenant + ACL trimming as the cloud adapter. Never selected outside `environment=development`.
"""

import json
import math
from collections.abc import Sequence
from dataclasses import asdict
from typing import Any, cast
from uuid import UUID

from redis.asyncio import Redis

from ai_service.rag.models import IndexedChunk, SearchHit

PREFIX = "majlis:devindex:"


def _key(tenant_id: UUID) -> str:
    return f"{PREFIX}{tenant_id}"


def _cosine(a: Sequence[float], b: Sequence[float]) -> float:
    if not a or not b or len(a) != len(b):
        return 0.0
    dot = sum(x * y for x, y in zip(a, b, strict=True))
    na = math.sqrt(sum(x * x for x in a))
    nb = math.sqrt(sum(y * y for y in b))
    return dot / (na * nb) if na and nb else 0.0


_STOP = {"في", "من", "ما", "على", "عن", "الى", "هل", "او", "و", "ان", "the", "a", "an", "of", "in", "is", "what", "to"}


def _stem(token: str) -> str:
    """Tiny Arabic prefix/suffix strip for the dev index only (the cloud analyzers do this properly)."""
    for prefix in ("وال", "بال", "كال", "فال", "ال", "لل"):
        if token.startswith(prefix) and len(token) > len(prefix) + 2:
            token = token[len(prefix) :]
            break
    for suffix in ("ات", "ون", "ين", "ها", "هم", "ه", "ي"):
        if token.endswith(suffix) and len(token) > len(suffix) + 2:
            token = token[: -len(suffix)]
            break
    return token


def _overlap(query_search: str, content_search: str) -> float:
    q = {_stem(t) for t in query_search.split() if len(t) > 1 and t not in _STOP and not t.isdigit()}
    if not q:
        return 0.0
    c = {_stem(t) for t in content_search.split()}
    return len(q & c) / len(q)


class LocalSearchIndex:
    def __init__(self, redis: Redis) -> None:
        self._redis = redis

    async def ensure_index(self) -> None:
        return None

    async def upsert(self, chunks: Sequence[IndexedChunk]) -> None:
        if not chunks:
            return
        key = _key(chunks[0].tenant_id)
        mapping = {c.id: json.dumps(_to_json(c), ensure_ascii=False) for c in chunks}
        await self._redis.hset(key, mapping=cast(dict[Any, Any], mapping))

    async def _all(self, tenant_id: UUID) -> list[IndexedChunk]:
        raw = await self._redis.hgetall(_key(tenant_id))
        return [_from_json(json.loads(v)) for v in raw.values()]

    async def delete_document(self, tenant_id: UUID, document_id: UUID, *, keep_version: UUID | None = None) -> int:
        ids = [
            c.id for c in await self._all(tenant_id) if c.document_id == document_id and c.version_id != keep_version
        ]
        if ids:
            await self._redis.hdel(_key(tenant_id), *ids)
        return len(ids)

    async def version_info(self, tenant_id: UUID, document_id: UUID, version_id: UUID) -> tuple[str, int] | None:
        chunks = [c for c in await self._all(tenant_id) if c.document_id == document_id and c.version_id == version_id]
        if not chunks:
            return None
        return chunks[0].content_sha256, len(chunks)

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
        allowed = set(acl_groups)
        hits: list[SearchHit] = []
        for c in await self._all(tenant_id):
            if not allowed.intersection(c.acl_groups):
                continue  # security trimming (AI-RAG-004)
            semantic = _cosine(vector, c.content_vector) if vector else 0.0
            lexical = _overlap(query_search, c.content_search)
            score = 0.6 * semantic + 0.4 * lexical if vector else lexical
            if score > 0:
                hits.append(SearchHit(c, score))
        hits.sort(key=lambda h: h.score, reverse=True)
        return hits[:top]

    async def aclose(self) -> None:
        return None


def _to_json(c: IndexedChunk) -> dict[str, object]:
    d = asdict(c)
    for k in ("tenant_id", "document_id", "version_id"):
        d[k] = str(d[k])
    return d


def _from_json(d: dict[str, object]) -> IndexedChunk:
    return IndexedChunk(
        id=str(d["id"]),
        tenant_id=UUID(str(d["tenant_id"])),
        document_id=UUID(str(d["document_id"])),
        version_id=UUID(str(d["version_id"])),
        title=str(d["title"]),
        heading_path=str(d["heading_path"]),
        page=int(str(d["page"])),
        language=str(d["language"]),
        doc_type=str(d["doc_type"]),
        acl_groups=[str(g) for g in list(d["acl_groups"])],  # type: ignore[call-overload]
        content=str(d["content"]),
        content_search=str(d["content_search"]),
        content_vector=[float(x) for x in list(d.get("content_vector") or [])],  # type: ignore[call-overload]
        ordinal=int(str(d.get("ordinal") or 0)),
        content_sha256=str(d.get("content_sha256") or ""),
    )
