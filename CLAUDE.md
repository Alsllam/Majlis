# Majlis · مجلس

A shared AI workspace for teams, Arabic-first. Teams create **workspaces** and **rooms**. In a room, several people work with the **same AI agent session** in real time: everyone can watch it, redirect it, comment, take control, and hand the session to a colleague. The agent answers from the team's own documents (RAG with citations) and runs tools (create tasks, draft documents, summarize meetings). **Every action that changes data needs a human approval.**

Target users: teams in Saudi Arabia and the GCC first (consulting firms, legal, operations, later government), starting with private-sector customers in any country. Arabic and English with full RTL. Web first, then mobile. **Cloud SaaS is always the priority**; the code stays ready for on-premises installs later.

## Monorepo map

| Folder | What | Skill (rules) | Folder guide |
|---|---|---|---|
| `backend/` | .NET 10 modular backend, YARP BFF, OpenIddict, Wolverine | `.claude/skills/dotnet-modular-backend` | `backend/CLAUDE.md` |
| `frontend/` | Angular 22 **Nx workspace**, ECharts, AntV X6 | `.claude/skills/angular-nx-frontend` | `frontend/CLAUDE.md` |
| `mobile/` | Flutter app, Clean Architecture + BLoC | `.claude/skills/flutter-clean-mobile` | `mobile/CLAUDE.md` |
| `ai-service/` | Python FastAPI AI service: Azure OpenAI, Azure AI Search | `.claude/skills/python-azure-rag-service` | `ai-service/CLAUDE.md` |
| `docs/` | SRS, architecture, ADRs, brand kit | none | `docs/CLAUDE.md` |

**Before working in a folder, read its `CLAUDE.md` and its skill.** The skill holds the general rules. The folder `CLAUDE.md` holds the Majlis values (names, modules, ports) and wins when the two disagree.

## Product values (shared by every folder)

| Key | Value |
|---|---|
| Product name | Majlis (en) · مجلس (ar) |
| Default language | Arabic (`ar`), RTL. English (`en`) second |
| Code prefix | `Majlis` (.NET `Majlis.*`, npm `@majlis`, Dart `majlis`, Python product slug `majlis`) |
| Tenancy | One tenant = one customer organization. A tenant has many workspaces. A workspace has many rooms |
| Public entry point | YARP BFF only: `/api/{module}/**` → .NET module hosts, `/ai-api/**` → ai-service, `/hubs/**` → real-time hub |
| Auth | OpenIddict (authorization code + PKCE). Audiences: `majlis-api`, `ai-api` |
| Deployment profiles | **`cloud` first** (Azure, multi-tenant, one regional stamp per region) — the only profile built now. `on-prem` (customer data center, single tenant) is built when the first on-prem customer is confirmed; until then Azure-only services are still reached only through adapters — ADR-0006 |
| Brand kit (source of truth) | `docs/brand/` (see its README). Web and mobile copy from it; never invent a second identity |

### Local ports (dev)

| Service | Port |
|---|---|
| `Majlis.BFF.Host` (gateway, only public host) | 7000 |
| `Majlis.Auth.Host` (OpenIddict + login UI) | 7001 |
| `Majlis.Realtime.Host` (SignalR hub) | 7002 |
| .NET module hosts | 7010–7089 (see `backend/CLAUDE.md`) |
| `Majlis.Jobs.Host` (Hangfire) | 7090 |
| `ai-service` API | 8000 |
| Angular `web` app | 4200 |
| SQL Server | 1433 |
| Redis | 6379 |
| RabbitMQ (AMQP / management UI) | 5672 / 15672 |
| Azurite (local Blob Storage, `cloud` adapters) | 10000 |

## Product invariants (never break these)

1. **Human approval for mutations.** Any agent tool with `mutates: true` produces an approval request. Nothing changes until a person with the right permission approves it. The model never approves its own actions.
2. **Only `ai-service` talks to models.** Web, mobile and .NET never hold a model key and never call Azure OpenAI directly.
3. **Tenant isolation everywhere.** Tenant and user come from the validated token, never from a request body. Retrieval always applies the tenant + ACL filter.
4. **Data residency.** A tenant's data (documents, conversations, embeddings, logs that contain content) stays in its data region (cloud) or its data center (on-prem). Model calls go only to regions the tenant approved. Rules in `docs/SRS.md` §6.2.
5. **Citations or "I don't know".** Grounded answers cite sources; when sources don't cover it, the agent says so.
6. **Arabic is first-class.** Every user-facing string has `ar` and `en` in the same commit. Every screen works in RTL. User-generated content is stored as written, with a detected `language`; only system data (lookups, statuses) uses `NameAr`/`NameEn` pairs.
7. **Everything is audited.** Session control changes (take over, hand off), approvals and agent tool runs are written to the audit log.
8. **Cloud first, on-prem possible.** Build and test for the cloud profile. No business code imports an Azure SDK directly; every Azure-only dependency goes through its adapter (checked by an architecture test), so on-prem can be added later (ADR-0006).

## Working rules

- Product decisions live in `docs/` (SRS, architecture, ADRs). Read the relevant doc before building a feature, and update it in the same PR when a decision changes.
- One step or feature per branch and per PR. Branch names: `feature/{area}-{short-name}`, `fix/...`, `docs/...`.
- Run each folder's own checks before pushing (listed in its `CLAUDE.md`).
- No secrets in git. `.env.example` and `appsettings.json` hold names and non-secret defaults only.
- Module list and ports follow `docs/architecture/backend-modules.md` (accepted). Update this file and the folder files together when they change.
