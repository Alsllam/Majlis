"""
Ingestion (skill §5): download → extract → normalize → chunk → embed → upsert → report. Idempotent on
(version, content SHA-256): a version whose hash is already indexed is skipped. Older versions of the document are
removed after the new chunks are written (FR-KNW-004), so the index never serves two versions at once.
"""

import hashlib
import time
from dataclasses import dataclass

from ai_service.llm.provider import LlmError
from ai_service.messaging.contracts import DocumentDeleted, DocumentIndexed, DocumentIndexingFailed, DocumentUploaded
from ai_service.observability import get_logger
from ai_service.rag.ingestion.chunker import chunk_text
from ai_service.rag.ingestion.normalize import clean_display, detect_language, normalize_for_search
from ai_service.rag.models import IndexedChunk, IngestionError, UnsupportedDocumentError
from ai_service.rag.ports import BlobStore, DocumentExtractor, Embedder, SearchIndex

log = get_logger(__name__)

EMBED_BATCH = 64


@dataclass(frozen=True, slots=True)
class IngestionReport:
    chunks: int
    skipped: bool
    sha256: str


class IngestionPipeline:
    def __init__(
        self,
        blobs: BlobStore,
        extractors: list[DocumentExtractor],
        embedder: Embedder,
        index: SearchIndex,
    ) -> None:
        self._blobs = blobs
        self._extractors = extractors
        self._embedder = embedder
        self._index = index

    async def ingest(self, event: DocumentUploaded) -> DocumentIndexed | DocumentIndexingFailed:
        started = time.monotonic()
        try:
            report, language, pages = await self._run(event)
        except IngestionError as exc:
            log.warning("ingest_failed", document_id=str(event.document_id), reason=exc.key, detail=exc.detail)
            return DocumentIndexingFailed(
                tenant_id=event.tenant_id,
                document_id=event.document_id,
                version_id=event.version_id,
                reason_key=exc.key,
                detail=exc.detail,
            )
        except LlmError as exc:
            log.warning("ingest_embed_failed", document_id=str(event.document_id), reason=exc.key)
            return DocumentIndexingFailed(
                tenant_id=event.tenant_id,
                document_id=event.document_id,
                version_id=event.version_id,
                reason_key=exc.key,
                detail=None,
            )
        except Exception as exc:
            log.exception("ingest_crashed", document_id=str(event.document_id))
            return DocumentIndexingFailed(
                tenant_id=event.tenant_id,
                document_id=event.document_id,
                version_id=event.version_id,
                reason_key=IngestionError.key,
                detail=type(exc).__name__,
            )
        log.info(
            "ingest_done",
            document_id=str(event.document_id),
            version_id=str(event.version_id),
            chunks=report.chunks,
            skipped=report.skipped,
            seconds=round(time.monotonic() - started, 2),
        )
        return DocumentIndexed(
            tenant_id=event.tenant_id,
            document_id=event.document_id,
            version_id=event.version_id,
            chunk_count=report.chunks,
            sha256=report.sha256,
            language=language,
            page_count=pages,
        )

    async def delete(self, event: DocumentDeleted) -> int:
        removed = await self._index.delete_document(event.tenant_id, event.document_id)
        log.info("ingest_deleted", document_id=str(event.document_id), chunks=removed)
        return removed

    async def _run(self, event: DocumentUploaded) -> tuple[IngestionReport, str, int]:
        data = await self._blobs.download(event.blob_path)
        sha = hashlib.sha256(data).hexdigest()
        existing = await self._index.version_info(event.tenant_id, event.document_id, event.version_id)
        if existing is not None and existing[0] == sha:
            return IngestionReport(existing[1], True, sha), event.language or "ar", 0

        extractor = next((e for e in self._extractors if e.supports(event.content_type)), None)
        if extractor is None:
            raise UnsupportedDocumentError(event.content_type)
        extracted = await extractor.extract(data, event.content_type, event.file_name)
        language = event.language or detect_language(extracted.text)

        pieces = chunk_text(extracted.text)
        if not pieces:
            raise UnsupportedDocumentError("no chunks")
        vectors: list[list[float]] = []
        for start in range(0, len(pieces), EMBED_BATCH):
            vectors.extend(await self._embedder.embed([p.text for p in pieces[start : start + EMBED_BATCH]]))

        chunks = [
            IndexedChunk(
                id=f"{event.version_id}-{piece.ordinal:05d}",
                tenant_id=event.tenant_id,
                document_id=event.document_id,
                version_id=event.version_id,
                title=event.title,
                heading_path=piece.heading_path,
                page=piece.page,
                language=language,
                doc_type=event.doc_type,
                acl_groups=list(event.acl_groups),
                content=clean_display(piece.text),
                content_search=normalize_for_search(piece.text),
                content_vector=vector,
                ordinal=piece.ordinal,
                content_sha256=sha,
            )
            for piece, vector in zip(pieces, vectors, strict=True)
        ]
        await self._index.upsert(chunks)
        await self._index.delete_document(event.tenant_id, event.document_id, keep_version=event.version_id)
        return IngestionReport(len(chunks), False, sha), language, extracted.page_count
