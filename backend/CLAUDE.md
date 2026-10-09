# backend/ — Majlis .NET backend

**Rules:** follow `.claude/skills/dotnet-modular-backend/SKILL.md` for everything here. This file gives the Majlis values for its placeholders and wins where they disagree. Product invariants are in the root `CLAUDE.md`.

## Placeholder values

| Skill placeholder | Majlis value |
|---|---|
| `{Co}` | `Majlis` |
| `{Product}` | dropped (same as `{Co}`): projects are `Majlis.{Module}.*`, not `Majlis.Majlis.{Module}.*` |
| Solution | `backend/Majlis.sln` |
| Framework projects | `Majlis.Framework.Domain`, `Majlis.Framework.Application`, `Majlis.Framework.EntityFrameworkCore` |
| Migrator | `Majlis.DbMigrator` |
| Hosts | `Majlis.{Module}.Host`, `Majlis.BFF.Host`, `Majlis.Auth.Host`, `Majlis.Jobs.Host` |
| Database | `Majlis` on SQL Server, one schema per module |
| OpenIddict audience | `majlis-api` (module APIs), `ai-api` (ai-service) |
| RabbitMQ virtual host | `majlis` |
| Redis key prefix | `majlis:` |

## Modules (accepted — `docs/architecture/backend-modules.md`)

| Module | Owns | Schema | Host | Port |
|---|---|---|---|---|
| Identity | users, roles, permissions, tenant (organization) record | `identity` | `Majlis.Auth.Host` | 7001 |
| Workspaces | workspaces, members, invitations, workspace roles, agent instructions, glossary | `workspaces` | `Majlis.Workspaces.Host` | 7010 |
| Rooms | rooms, participants, agent sessions, turns, comments, suggestions, session control (fencing epoch), the per-session event log (sequencer) | `rooms` | `Majlis.Rooms.Host` | 7020 |
| Approvals | approval requests for agent actions, decisions, policies | `approvals` | `Majlis.Approvals.Host` | 7030 |
| Knowledge | folders, documents, versions, ACL, ingestion status, agent drafts | `knowledge` | `Majlis.Knowledge.Host` | 7040 |
| Tasks | tasks created by people or by the agent | `tasks` | `Majlis.Tasks.Host` | 7050 |
| Meetings | meetings, transcripts, summaries, action items | `meetings` | `Majlis.Meetings.Host` | 7060 |
| Notifications | in-app, email and push notifications, preferences | `notifications` | `Majlis.Notifications.Host` | 7070 |
| Audit | append-only audit log | `audit` | `Majlis.Audit.Host` | 7080 |
| — | BFF gateway (YARP), the only public host | — | `Majlis.BFF.Host` | 7000 |
| — | SignalR hub, presence, fan-out (no database) | — | `Majlis.Realtime.Host` | 7002 |
| — | Hangfire jobs | — | `Majlis.Jobs.Host` | 7090 |

BFF routes: `/api/{module-kebab}/**` → module host (e.g. `/api/rooms/**` → 7020), `/api/realtime/ticket` → Auth.Host, `/ai-api/**` → ai-service (8000), `/hubs/**` → `Majlis.Realtime.Host` (7002, WebSocket upgrade).

Real-time rules: commands are always HTTP AppService calls; the hub only pushes events and receives presence signals (ADR-0002). Only Rooms writes session timelines (ADR-0003). Driver-only commands carry `epoch` (ADR-0004). Details: `docs/architecture/realtime-collaboration.md`.

## Majlis-specific rules

- **Deployment profiles (ADR-0006).** Azure-only services are used only through adapters in `Majlis.Framework.Application`: `IBlobStorage` (Azure Blob / S3-compatible), `IEmailSender`, `IPushSender`, and secrets through the configuration provider (Key Vault / Kubernetes secrets / Vault). The profile is chosen with `Deployment:Profile = cloud | on-prem`. On-prem is single tenant: tenant provisioning APIs are disabled and the tenant is created at install.

- **Permissions** follow `Permissions.{Module}.{Action}{Entity}` (e.g. `Permissions.Rooms.CreateRoom`, `Permissions.Approvals.ApproveAction`). Workspace-level roles (owner, admin, member, viewer) map to permission sets per workspace; the `ISecuredEntity` scope is the **workspace**.
- **User content is not bilingual.** Room names, messages, documents and tasks are stored as written, with a `Language` field. `NameAr`/`NameEn` pairs are for system lookups only.
- **Agent-originated writes** carry `ApprovalRequestId` and `OriginatingSessionId`, and are only accepted from the Approvals flow (never directly from ai-service).
- **Integration events** the AI side depends on: `DocumentUploaded`, `DocumentDeleted` (Knowledge → ai-service), `DocumentIndexed`, `DocumentIndexingFailed` (ai-service → Knowledge), `ActionApproved`, `ActionRejected` (Approvals → owning module).

## Checks before pushing

```
dotnet build backend/Majlis.sln -warnaserror
dotnet test backend/Majlis.sln
```
