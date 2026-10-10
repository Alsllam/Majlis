"""The pipeline end to end with fakes: extract → chunk → embed → upsert → report; idempotent on the content hash."""

from uuid import uuid4

import fakeredis.aioredis

from ai_service.messaging.contracts import DocumentDeleted, DocumentIndexed, DocumentIndexingFailed, DocumentUploaded
from ai_service.rag.adapters.extractors import SimpleExtractor
from ai_service.rag.adapters.local_index import LocalSearchIndex
from ai_service.rag.ingestion.pipeline import IngestionPipeline

ARABIC_MD = "# اللائحة\n\n" + "\n\n".join(
    f"المادة {n}: نص المادة رقم {n} في اللائحة الداخلية للشركة. " * 4 for n in range(1, 30)
)


class FakeBlobs:
    def __init__(self, files: dict[str, bytes]) -> None:
        self.files = files

    async def download(self, path: str) -> bytes:
        return self.files[path]

    async def aclose(self) -> None:
        return None


class FakeEmbedder:
    dimensions = 3
    calls = 0

    async def embed(self, texts):  # type: ignore[no-untyped-def]
        self.calls += 1
        return [[1.0, 0.0, float(len(t) % 7)] for t in texts]


def event(path: str, content_type: str = "text/markdown", **overrides: object) -> DocumentUploaded:
    base = {
        "tenant_id": uuid4(),
        "workspace_id": uuid4(),
        "document_id": uuid4(),
        "version_id": uuid4(),
        "blob_path": path,
        "file_name": "regulation.md",
        "content_type": content_type,
        "title": "اللائحة الداخلية",
        "doc_type": "Regulation",
        "acl_groups": ["ws:w1"],
    }
    base.update(overrides)
    return DocumentUploaded.model_validate(base)


async def test_ingest_indexes_chunks_then_skips_unchanged_content() -> None:
    redis = fakeredis.aioredis.FakeRedis(decode_responses=True)
    index = LocalSearchIndex(redis)
    embedder = FakeEmbedder()
    pipeline = IngestionPipeline(FakeBlobs({"p": ARABIC_MD.encode()}), [SimpleExtractor()], embedder, index)
    e = event("p")

    first = await pipeline.ingest(e)
    assert isinstance(first, DocumentIndexed)
    assert first.chunk_count >= 2
    assert first.language == "ar"
    assert len(first.sha256) == 64

    again = await pipeline.ingest(e)
    assert isinstance(again, DocumentIndexed)
    assert again.chunk_count == first.chunk_count
    assert embedder.calls == 1  # unchanged content is not embedded again


async def test_ingest_replaces_older_versions_of_the_same_document() -> None:
    index = LocalSearchIndex(fakeredis.aioredis.FakeRedis(decode_responses=True))
    pipeline = IngestionPipeline(
        FakeBlobs({"v1": ARABIC_MD.encode(), "v2": (ARABIC_MD + "\n\nالمادة 99: جديد.").encode()}),
        [SimpleExtractor()],
        FakeEmbedder(),
        index,
    )
    doc, tenant = uuid4(), uuid4()
    v1 = event("v1", tenant_id=tenant, document_id=doc)
    v2 = event("v2", tenant_id=tenant, document_id=doc)
    await pipeline.ingest(v1)
    await pipeline.ingest(v2)
    hits = await index.search(
        tenant_id=tenant, acl_groups=["ws:w1"], query="*", query_search="الماده", vector=None, top=100
    )
    assert hits
    assert {h.chunk.version_id for h in hits} == {v2.version_id}

    removed = await pipeline.delete(DocumentDeleted(tenant_id=tenant, document_id=doc))
    assert removed == len(hits)


async def test_ingest_reports_failures_with_localization_keys() -> None:
    index = LocalSearchIndex(fakeredis.aioredis.FakeRedis(decode_responses=True))
    pipeline = IngestionPipeline(FakeBlobs({"zip": b"PK", "empty": b"   "}), [SimpleExtractor()], FakeEmbedder(), index)
    unsupported = await pipeline.ingest(event("zip", content_type="application/zip"))
    assert isinstance(unsupported, DocumentIndexingFailed)
    assert unsupported.reason_key == "Knowledge:Ingestion:Unsupported"
    empty = await pipeline.ingest(event("empty", content_type="text/plain"))
    assert isinstance(empty, DocumentIndexingFailed)
    assert empty.reason_key == "Knowledge:Ingestion:Empty"
