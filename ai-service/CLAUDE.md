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
| Deployment profile | `cloud` (Azure OpenAI, Azure AI Search, Document Intelligence) or `on-prem` (OpenAI-compatible model servers, OpenSearch, self-hosted extraction / speech / guard) — ADR-0006 |
| Region | the tenant's data region; model calls only to regions allowed by the tenant's inference consent (`docs/SRS.md` §6.2) |

## Majlis-specific rules

- **Adapters (ADR-0006).** Business code depends only on these interfaces, each with a `cloud` and an `on-prem` implementation: `LlmProvider` (Azure OpenAI Responses API / OpenAI-compatible Chat Completions), `Embedder`, `SearchIndex` (Azure AI Search / OpenSearch + re-ranker), `DocumentExtractor`, `Transcriber`, `SafetyGuard`, `BlobStore`. The skill's Azure rules apply to the `cloud` implementations. Tests and the eval run on both.
- **Shared sessions** are started by Rooms through `POST /internal/sessions/{sessionId}/turns` (internal only, not routed by the BFF). Token deltas go to Redis pub/sub (`session:{id}:stream`) with a text snapshot `turn:{id}:text`; durable milestones go to RabbitMQ for Rooms to sequence; check the cancel key `turn:{id}:cancel` between deltas; heartbeat `turn:{id}:hb` every 5 s (`docs/architecture/realtime-collaboration.md`).

- **The agent session is shared.** A session belongs to a room, not a user. Each turn records who sent it; the tool loop runs with the token of the person **currently in control** of the session, so their permissions apply.
- **Every mutating tool returns `confirm_required`** and becomes an approval request in the backend Approvals module. The tool only runs after the `ActionApproved` event. The agent never retries a rejected action unless a person asks again.
- MVP tools (SRS AI-AGT-006): `search_knowledge`, `get_document`, `create_task`, `update_task`, `draft_document`, `summarize_meeting`, `extract_action_items`, `list_room_participants`.
- Arabic prompts use formal Modern Standard Arabic; answer in the language of the latest user turn.

## Checks before pushing

```
cd ai-service && uv run ruff check && uv run ruff format --check && uv run mypy --strict src && uv run pytest
```
