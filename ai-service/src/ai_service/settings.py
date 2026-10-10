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
    agent_prompt_version: str = "agent.v1"

    # Tokens issued by Majlis.Auth.Host (through the BFF).
    auth_issuer: str = "http://localhost:7000/"
    auth_jwks_url: str = ""
    auth_audience: str = "ai-api"

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


@lru_cache
def get_settings() -> Settings:
    return Settings()
