"""Context budget, citation mapping, the dev index and the retriever selection rules."""

from uuid import uuid4

import fakeredis.aioredis

from ai_service.rag.adapters.local_index import LocalSearchIndex
from ai_service.rag.models import IndexedChunk, SearchHit
from ai_service.rag.retrieval.context import build_context, resolve_citations
from ai_service.rag.retrieval.search import RetrievalOptions, Retriever

TENANT = uuid4()


def chunk(doc, text, *, acl=("ws:w1",), page=1, version=None, vector=(1.0, 0.0), ordinal=0) -> IndexedChunk:  # type: ignore[no-untyped-def]
    return IndexedChunk(
        id=f"{doc}-{ordinal}",
        tenant_id=TENANT,
        document_id=doc,
        version_id=version or uuid4(),
        title="عقد المورد",
        heading_path="الباب الأول",
        page=page,
        language="ar",
        doc_type="Contract",
        acl_groups=list(acl),
        content=text,
        content_search=text,
        content_vector=list(vector),
        ordinal=ordinal,
        content_sha256="abc",
    )


class FakeEmbedder:
    dimensions = 2

    async def embed(self, texts):  # type: ignore[no-untyped-def]
        return [[1.0, 0.0] for _ in texts]


def test_build_context_respects_budget_and_labels_in_order() -> None:
    hits = [SearchHit(chunk(uuid4(), "نص " * 200, ordinal=i), 0.9 - i * 0.1) for i in range(5)]
    context = build_context(hits, budget_tokens=250)
    assert context.labels[0] == "S1"
    assert len(context.sources) < 5
    assert '<source id="S1" title="عقد المورد" page="1" path="الباب الأول">' in context.text


def test_resolve_citations_maps_used_labels_only_once() -> None:
    doc = uuid4()
    hits = [
        SearchHit(chunk(doc, "المادة 12 تنص على الغرامة.", ordinal=0), 0.9),
        SearchHit(chunk(doc, "آخر", ordinal=1), 0.5),
    ]
    context = build_context(hits, 6000)
    citations = resolve_citations("الغرامة واحد بالمائة [S1]. وتكرار [S1] ومصدر غير موجود [S9].", context)
    assert [c.label for c in citations] == ["S1"]
    assert citations[0].document_id == doc
    assert citations[0].page == 1
    assert citations[0].passage.startswith("المادة 12")


async def test_local_index_filters_by_tenant_and_acl_and_ranks_by_similarity() -> None:
    index = LocalSearchIndex(fakeredis.aioredis.FakeRedis(decode_responses=True))
    visible = uuid4()
    hidden = uuid4()
    await index.upsert(
        [
            chunk(visible, "غرامة التأخير", vector=(1.0, 0.0)),
            chunk(hidden, "سري", acl=("room:other",), vector=(1.0, 0.0)),
        ]
    )
    hits = await index.search(
        tenant_id=TENANT, acl_groups=["ws:w1"], query="غرامة", query_search="غرامه التاخير", vector=[1.0, 0.0], top=10
    )
    assert [h.chunk.document_id for h in hits] == [visible]
    assert (
        await index.search(
            tenant_id=uuid4(), acl_groups=["ws:w1"], query="غرامة", query_search="غرامه", vector=[1.0, 0.0], top=10
        )
        == []
    )


async def test_local_index_delete_keeps_the_current_version() -> None:
    index = LocalSearchIndex(fakeredis.aioredis.FakeRedis(decode_responses=True))
    doc, old, new = uuid4(), uuid4(), uuid4()
    await index.upsert([chunk(doc, "v1", version=old, ordinal=0), chunk(doc, "v2", version=new, ordinal=1)])
    assert await index.delete_document(TENANT, doc, keep_version=new) == 1
    assert await index.version_info(TENANT, doc, new) == ("abc", 1)
    assert await index.version_info(TENANT, doc, old) is None


async def test_retriever_limits_chunks_per_document_and_applies_threshold() -> None:
    index = LocalSearchIndex(fakeredis.aioredis.FakeRedis(decode_responses=True))
    doc = uuid4()
    await index.upsert([chunk(doc, f"نص {i} غرامة", ordinal=i, vector=(1.0, 0.0)) for i in range(6)])
    await index.upsert([chunk(uuid4(), "بعيد", ordinal=9, vector=(0.0, 1.0))])
    retriever = Retriever(index, FakeEmbedder(), RetrievalOptions(top=8, min_score=0.5, per_document=3))
    hits = await retriever.retrieve(tenant_id=TENANT, acl_groups=["ws:w1"], question="غرامة")
    assert len(hits) == 3
    assert all(h.chunk.document_id == doc for h in hits)
