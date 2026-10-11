# Majlis ai-service

FastAPI service that runs the shared agent. It is the only component that talks to models. Rules: [`CLAUDE.md`](CLAUDE.md) and `.claude/skills/python-azure-rag-service`; contracts: [`docs/architecture/events.schema.json`](../docs/architecture/events.schema.json).

```bash
cd ai-service
uv sync
cp .env.example .env          # fill in Azure OpenAI and RABBITMQ_URL (local only, git-ignored)
uv run uvicorn ai_service.main:create_app --factory --port 8000
```

| Endpoint | Caller |
|---|---|
| `POST /internal/sessions/{sessionId}/turns` | Rooms only (internal network, driver's token). Answers 202 and streams the turn |
| `GET /health/live`, `GET /health/ready` | orchestrator (ready checks Redis, RabbitMQ and model configuration) |

Checks: `uv run ruff check && uv run ruff format --check && uv run mypy --strict src && uv run pytest`.
