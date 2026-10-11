"""App factory and lifespan: clients are created once and closed on shutdown.

Run: `uvicorn ai_service.main:create_app --factory --port 8000`.
"""

from collections.abc import AsyncIterator, Awaitable, Callable, Sequence
from contextlib import asynccontextmanager
from typing import NamedTuple

from aio_pika.abc import AbstractRobustChannel
from fastapi import FastAPI
from redis.asyncio import Redis

from ai_service.api.errors import register_error_handlers
from ai_service.api.routes import health, internal_turns
from ai_service.llm.adapters.azure_openai import AzureOpenAIProvider
from ai_service.llm.provider import LlmProvider
from ai_service.meetings.minutes import MinutesGenerator
from ai_service.messaging.bus import RabbitResultPublisher
from ai_service.observability import configure_logging, get_logger
from ai_service.rag.adapters.azure_blob import AzureBlobStore
from ai_service.rag.adapters.azure_search import AzureSearchIndex
from ai_service.rag.adapters.extractors import AzureDocumentIntelligenceExtractor, SimpleExtractor
from ai_service.rag.adapters.local_index import LocalSearchIndex
from ai_service.rag.ingestion.pipeline import IngestionPipeline
from ai_service.rag.ports import BlobStore, DocumentExtractor, SearchIndex
from ai_service.rag.retrieval.search import RetrievalOptions, Retriever
from ai_service.security.tokens import TokenValidator
from ai_service.sessions.approvals import ApprovalsClient
from ai_service.sessions.turn_runner import TurnRunner
from ai_service.settings import Settings, get_settings
from ai_service.workers.ingest_worker import IngestWorker
from ai_service.workers.meetings_worker import MeetingsWorker

log = get_logger(__name__)


def build_llm(settings: Settings) -> LlmProvider:
    if settings.deployment_profile == "cloud":
        return AzureOpenAIProvider(settings)
    raise NotImplementedError(
        "The on-prem model adapter is built when the first on-prem customer is confirmed (ADR-0006)."
    )


def build_index(settings: Settings, redis: Redis) -> SearchIndex:
    if settings.search_backend == "local":
        if settings.environment != "development":
            raise RuntimeError("The local search index is for development only (SEARCH_BACKEND=local).")
        return LocalSearchIndex(redis)
    return AzureSearchIndex(settings)


def build_extractors(settings: Settings) -> list[DocumentExtractor]:
    """Document Intelligence first when configured (scans, Office files), the simple extractor as the fallback."""
    extractors: list[DocumentExtractor] = []
    if settings.document_intelligence_endpoint:
        extractors.append(AzureDocumentIntelligenceExtractor(settings))
    extractors.append(SimpleExtractor())
    return extractors


class KnowledgeServices(NamedTuple):
    retriever: Retriever | None
    closers: Sequence[Callable[[], Awaitable[None]]]
    worker_channel: AbstractRobustChannel | None
    recordings: BlobStore | None
    worker: IngestWorker | None = None


async def start_knowledge(
    settings: Settings, redis: Redis, llm: LlmProvider, publisher: RabbitResultPublisher
) -> KnowledgeServices:
    """RAG and the ingestion worker, only when storage, an index backend and the embedding deployment are set."""
    if not settings.knowledge_configured:
        log.warning("knowledge_not_configured", hint="set AZURE_STORAGE_* and the search backend to enable RAG")
        return KnowledgeServices(None, [], None, None)
    index = build_index(settings, redis)
    await index.ensure_index()
    blobs = AzureBlobStore(settings)
    recordings = AzureBlobStore(settings, settings.recordings_container)
    extractors = build_extractors(settings)
    closers = [index.aclose, blobs.aclose, recordings.aclose, *[e.aclose for e in extractors]]
    retriever = Retriever(
        index, llm, RetrievalOptions(top=settings.retrieval_top_k, min_score=settings.retrieval_min_score)
    )
    pipeline = IngestionPipeline(blobs, extractors, llm, index)
    worker_channel = await publisher.open_channel()
    worker = IngestWorker(worker_channel, pipeline, publisher, settings.ingest_concurrency)
    await worker.start()
    return KnowledgeServices(retriever, closers, worker_channel, recordings, worker)


async def start_meetings_worker(
    settings: Settings,
    channel: AbstractRobustChannel,
    llm: LlmProvider,
    publisher: RabbitResultPublisher,
    recordings: BlobStore | None,
) -> MeetingsWorker:
    """Minutes need only the model; transcription also needs the recordings container (None → reported as such)."""
    worker = MeetingsWorker(
        channel,
        MinutesGenerator(settings, llm),
        llm,
        publisher,
        recordings,
        max_recording_mb=settings.transcribe_max_mb,
    )
    await worker.start()
    return worker


def create_app(settings: Settings | None = None) -> FastAPI:
    settings = settings or get_settings()
    configure_logging("DEBUG" if settings.environment == "development" else "INFO")

    @asynccontextmanager
    async def lifespan(app: FastAPI) -> AsyncIterator[None]:
        redis = Redis.from_url(settings.redis_url, decode_responses=True)
        publisher = RabbitResultPublisher(settings.rabbitmq_url.get_secret_value())
        await publisher.start()
        llm = build_llm(settings)
        app.state.settings = settings
        app.state.redis = redis
        app.state.publisher = publisher
        app.state.token_validator = TokenValidator(settings.auth_issuer, settings.auth_audience, settings.jwks_url)

        knowledge = await start_knowledge(settings, redis, llm, publisher)
        retriever, closers, worker_channel, recordings, _ = knowledge
        if knowledge.worker is not None:
            app.state.ingest_worker = knowledge.worker

        meetings_channel = await publisher.open_channel()
        app.state.meetings_worker = await start_meetings_worker(settings, meetings_channel, llm, publisher, recordings)

        approvals = ApprovalsClient(settings.approvals_url, settings.approvals_timeout_s)
        app.state.retriever = retriever
        app.state.turn_runner = TurnRunner(settings, llm, redis, publisher, retriever=retriever, approvals=approvals)
        try:
            yield
        finally:
            await approvals.aclose()
            await meetings_channel.close()
            if worker_channel is not None:
                await worker_channel.close()
            for close in closers:
                await close()
            await llm.aclose()
            await publisher.close()
            await redis.aclose()

    app = FastAPI(title="Majlis AI service", version="0.1.0", lifespan=lifespan)
    register_error_handlers(app)
    app.include_router(health.router)
    app.include_router(internal_turns.router)
    return app
