# ai-service/ — Majlis AI service (Python, Azure)

**Rules:** follow `.claude/skills/python-azure-rag-service/SKILL.md` for everything here. This file gives the Majlis values for its placeholders and wins where they disagree. Product invariants are in the root `CLAUDE.md`.

## Placeholder values

| Skill placeholder | Majlis value |
|---|---|
| `{product}` | `majlis` |
| `{tenant}` | tenant (organization) id from the token claim `tenant_id` |
| Python package | `ai_service` (`src/ai_service/`) |
| API port (dev) | 8000; reached through the BFF at `/ai-api/**` |
| Token audience | `ai-api`; JWKS from `Majlis.Auth.Host` |
| Search index | `majlis-knowledge` (tenant filter by default; one index per tenant for customers that require it) |
| ACL field values | `ws:{workspaceId}`, `room:{roomId}`, `user:{userId}`, `group:{groupId}` |
| Eval datasets | `evals/datasets/majlis_ar.jsonl`, `evals/datasets/majlis_en.jsonl` |
| Deployment profile | **`cloud`** (Azure OpenAI, Azure AI Search, Document Intelligence) — the only profile built now. `on-prem` (OpenAI-compatible model servers, OpenSearch, self-hosted extraction / speech / guard) later — ADR-0006 |
| Region | the tenant's data region; model calls only to regions allowed by the tenant's inference consent (`docs/SRS.md` §6.2) |

## What exists (walking skeleton)

| Module | State |
|---|---|
| `settings.py`, `observability.py`, `api/errors.py` | env configuration, JSON logs, backend error shape with ar/en messages |
| `security/tokens.py`, `api/deps.py` | RS256 access tokens validated against the Auth host JWKS (`aud = ai-api`); tenant and user only from the token |
| `llm/provider.py`, `llm/adapters/azure_openai.py` | provider interface + Azure OpenAI Responses API streaming adapter (the only module importing `azure`) |
| `llm/prompts/agent.v1.md` | the shared-room agent prompt (no knowledge base yet: says so instead of guessing) |
| `sessions/` | `POST /internal/sessions/{id}/turns` → background `TurnRunner`: coalesced deltas, snapshot, heartbeat, cancel, one result |
| `messaging/bus.py` | results to RabbitMQ as plain camelCase JSON on exchange `majlis.{alias}` with the alias in the AMQP `type` property (ADR-0009) |

Not built yet: RAG (ingestion, search, citations), tools and approvals, conversations/usage in SQL, rate limits and quotas, Prompt Shields, evaluation suite.

**Testing note:** the OpenAI SDK 3.x uses `httpx2`, not `httpx`, so `respx` cannot intercept it. Adapter tests pass an `openai.DefaultAsyncHttpx2Client(transport=httpx2.MockTransport(...))`.

## Majlis-specific rules

- **Adapters (ADR-0006).** Business code depends only on these interfaces; implement the `cloud` version now, the `on-prem` version only when an on-prem customer is confirmed: `LlmProvider` (Azure OpenAI Responses API / OpenAI-compatible Chat Completions), `Embedder`, `SearchIndex` (Azure AI Search / OpenSearch + re-ranker), `DocumentExtractor`, `Transcriber`, `SafetyGuard`, `BlobStore`. The skill's Azure rules apply to the `cloud` implementations. Azure SDK imports are allowed only inside adapter modules.
- **Shared sessions** are started by Rooms through `POST /internal/sessions/{sessionId}/turns` (internal only, not routed by the BFF; body `startAiTurnRequest`, the driver's `Authorization` header forwarded; answer 202 and run the turn in the background). Token deltas go to Redis pub/sub `majlis:session:{sessionId}:stream` with the running text in the hash `majlis:turn:{turnId}:text` (`text`, `chunk`); results (`TurnCompleted`, `TurnStopped`, `TurnFailed`) go to RabbitMQ as plain JSON (exchange `majlis.turn-completed` etc., durable fanout, AMQP `type` = alias) for Rooms to sequence; check `majlis:turn:{turnId}:cancel` between deltas; refresh `majlis:turn:{turnId}:hb` every 5 s. Every shape is in `docs/architecture/events.schema.json`.

- **The agent session is shared.** A session belongs to a room, not a user. Each turn records who sent it; the tool loop runs with the token of the person **currently in control** of the session, so their permissions apply.
- **Every mutating tool returns `confirm_required`** and becomes an approval request in the backend Approvals module. The tool only runs after the `ActionApproved` event. The agent never retries a rejected action unless a person asks again.
- MVP tools (SRS AI-AGT-006): `search_knowledge`, `get_document`, `create_task`, `update_task`, `draft_document`, `summarize_meeting`, `extract_action_items`, `list_room_participants`.
- Arabic prompts use formal Modern Standard Arabic; answer in the language of the latest user turn.

## Checks before pushing

```
cd ai-service && uv run ruff check && uv run ruff format --check && uv run mypy --strict src && uv run pytest
```
Run locally: `uv run uvicorn ai_service.main:create_app --factory --port 8000` (variables in `.env.example`), or `backend/scripts/run-local.sh`, which starts it with the backend.
