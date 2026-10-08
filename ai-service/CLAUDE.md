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
| Azure region | Saudi Arabia region for data at rest; model deployment region per `docs/SRS.md` (data residency section) |

## Majlis-specific rules

- **The agent session is shared.** A session belongs to a room, not a user. Each turn records who sent it; the tool loop runs with the token of the person **currently in control** of the session, so their permissions apply.
- **Every mutating tool returns `confirm_required`** and becomes an approval request in the backend Approvals module. The tool only runs after the `ActionApproved` event. The agent never retries a rejected action unless a person asks again.
- Planned tools (provisional): `search_knowledge`, `get_document`, `create_task`, `update_task`, `draft_document`, `summarize_meeting`, `extract_action_items`, `list_room_participants`.
- Session events (agent deltas, tool calls, approvals) are published so every participant sees them live. Transport is decided in step 4; the service must not depend on a single client connection.
- Arabic prompts use formal Modern Standard Arabic; answer in the language of the latest user turn.

## Checks before pushing

```
cd ai-service && uv run ruff check && uv run ruff format --check && uv run mypy --strict src && uv run pytest
```
