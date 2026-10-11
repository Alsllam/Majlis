"""Configuration from environment variables. No secrets or deployment names in code (skill §4, §14)."""

from functools import lru_cache
from typing import Literal

from pydantic import Field, SecretStr
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", env_file_encoding="utf-8", extra="ignore")

    environment: Literal["development", "staging", "production"] = "development"
    deployment_profile: Literal["cloud", "on-prem"] = "cloud"

    # Azure OpenAI (cloud profile). The key is for local development only; deployed environments use Entra ID.
    azure_openai_endpoint: str = ""
    azure_openai_api_key: SecretStr | None = None
    azure_openai_chat_deployment: str = ""
    azure_openai_fast_deployment: str = ""
    azure_openai_embed_deployment: str = ""
    llm_timeout_s: float = 60.0
    chat_temperature: float = 0.3
    max_output_tokens: int = 1500
    agent_prompt_version: str = "agent.v3"
    # Agent tools: at most this many model rounds per turn (each tool call costs one round).
    max_tool_rounds: int = Field(default=5, ge=1, le=10)

    # Knowledge: blob storage (documents uploaded through the Knowledge module), extraction, search index.
    azure_storage_connection_string: SecretStr | None = None
    azure_storage_account_url: str = ""
    blob_container: str = "documents"
    document_intelligence_endpoint: str = ""
    document_intelligence_api_key: SecretStr | None = None
    azure_search_endpoint: str = ""
    azure_search_api_key: SecretStr | None = None
    azure_search_index: str = "majlis-knowledge"
    embed_dimensions: int = 1536
    # `azure` when deployed; `local` (a Redis-backed dev index) only for development without Azure AI Search.
    search_backend: Literal["azure", "local"] = "azure"
    ingest_concurrency: int = 2
    retrieval_top_k: int = 8
    retrieval_min_score: float = 0.15
    context_budget_tokens: int = 6000

    # Tokens issued by Majlis.Auth.Host (through the BFF).
    auth_issuer: str = "http://localhost:7000/"
    auth_jwks_url: str = ""
    auth_audience: str = "ai-api"

    # Approvals module through the BFF; called with the driver's forwarded token (never a service identity).
    approvals_url: str = "http://localhost:7000/api/approvals"
    approvals_timeout_s: float = 10.0

    redis_url: str = "redis://localhost:6379/0"
    rabbitmq_url: SecretStr = SecretStr("amqp://guest:guest@localhost:5672/majlis")

    # Turn execution
    stream_flush_ms: int = Field(default=60, ge=10, le=1000)
    turn_timeout_s: float = 90.0
    heartbeat_interval_s: float = 5.0
    snapshot_ttl_s: int = 600

    @property
    def jwks_url(self) -> str:
        return self.auth_jwks_url or self.auth_issuer.rstrip("/") + "/.well-known/jwks"

    @property
    def llm_configured(self) -> bool:
        return bool(self.azure_openai_endpoint and self.azure_openai_chat_deployment)

    @property
    def knowledge_configured(self) -> bool:
        storage = bool(self.azure_storage_connection_string or self.azure_storage_account_url)
        index = self.search_backend == "local" or bool(self.azure_search_endpoint)
        return storage and index and bool(self.azure_openai_embed_deployment)


@lru_cache
def get_settings() -> Settings:
    return Settings()
