"""App factory and lifespan: clients are created once and closed on shutdown.

Run: `uvicorn ai_service.main:create_app --factory --port 8000`.
"""

from collections.abc import AsyncIterator
from contextlib import asynccontextmanager

from fastapi import FastAPI
from redis.asyncio import Redis

from ai_service.api.errors import register_error_handlers
from ai_service.api.routes import health, internal_turns
from ai_service.llm.adapters.azure_openai import AzureOpenAIProvider
from ai_service.llm.provider import LlmProvider
from ai_service.messaging.bus import RabbitResultPublisher
from ai_service.observability import configure_logging
from ai_service.security.tokens import TokenValidator
from ai_service.sessions.turn_runner import TurnRunner
from ai_service.settings import Settings, get_settings


def build_llm(settings: Settings) -> LlmProvider:
    if settings.deployment_profile == "cloud":
        return AzureOpenAIProvider(settings)
    raise NotImplementedError(
        "The on-prem model adapter is built when the first on-prem customer is confirmed (ADR-0006)."
    )


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
        app.state.turn_runner = TurnRunner(settings, llm, redis, publisher)
        try:
            yield
        finally:
            await llm.aclose()
            await publisher.close()
            await redis.aclose()

    app = FastAPI(title="Majlis AI service", version="0.1.0", lifespan=lifespan)
    register_error_handlers(app)
    app.include_router(health.router)
    app.include_router(internal_turns.router)
    return app
