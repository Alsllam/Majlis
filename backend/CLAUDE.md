# backend/ — Majlis .NET backend

**Rules:** follow `.claude/skills/dotnet-modular-backend/SKILL.md` for everything here. This file gives the Majlis values for its placeholders and wins where they disagree. Product invariants are in the root `CLAUDE.md`.

## Placeholder values

| Skill placeholder | Majlis value |
|---|---|
| `{Co}` | `Majlis` |
| `{Product}` | dropped (same as `{Co}`): projects are `Majlis.{Module}.*`, not `Majlis.Majlis.{Module}.*` |
| Solution | `backend/Majlis.sln` (central package versions in `Directory.Packages.props`, SDK + test runner in `global.json`) |
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

## What exists (walking skeleton)

| Project | State |
|---|---|
| `Shared/Majlis.Framework.{Domain,EntityFrameworkCore,Application}` | base entities, repositories, unit of work, tenant + soft-delete filters, dynamic controllers, error middleware, ar/en JSON localization, permissions, OpenIddict validation, Wolverine messaging with the transactional outbox, real-time tickets, service tokens |
| `Modules/Identity` + `Hosts/Majlis.Auth.Host` | tenants, users, roles; OpenIddict server (code + PKCE, refresh, client credentials); ar/en login page; `/realtime/ticket`; `/users/lookup` (tenant user picker, routed as `/api/identity/users/lookup`) |
| `Modules/Workspaces` + `Hosts/Majlis.Workspaces.Host` | workspaces, members with workspace roles (Owner/Admin/Contributor/Viewer), agent instructions, archive/restore; publishes `WorkspaceCreated/Archived/InstructionsChanged`, `MemberAdded/RoleChanged/Removed`, `AccessRevoked`; writes each user's effective grants to the permission cache (`majlis:permissions:{userId}`); `/internal/workspaces/access` |
| `Modules/Knowledge` + `Hosts/Majlis.Knowledge.Host` | documents and versions per workspace (or room), two-step upload (pre-signed url → confirm), ingestion status from ai-service (`DocumentIndexed` / `DocumentIndexingFailed`), download urls, delete (`DocumentDeleted`), membership read model; `IBlobStorage` adapter (`Adapters/Storage/AzureBlobStorage`, Azurite locally, CORS set at startup for browser uploads) |
| `Modules/Approvals` + `Hosts/Majlis.Approvals.Host` | approval requests for agent tools (`KnownTools`: `create_task`, `update_task`, `draft_document` with their risk), the default decision policy (`ApprovalPolicyManager`: low risk → any contributor, medium/high → an owner or admin who is not the requester), edit-then-approve, reject with reason, 24 h expiry sweeper, execution outcome from the owning module; publishes `ApprovalRequested/Decided/Executed` (for the room timeline) and `ActionApproved/Rejected` (for the owning module); `/requests` list (`forMe` = approver inbox), getbyid, create (ai-service with the driver's token), approve, reject; membership read model |
| `Modules/Tasks` + `Hosts/Majlis.Tasks.Host` | tasks per workspace (people create them here; the agent only through an approval): list, getbyid, create, update, status; `ActionApprovedHandler` runs `create_task` once per `ApprovalRequestId` and reports `ActionExecuted` / `ActionExecutionFailed`; membership read model |
| `Modules/Rooms` + `Hosts/Majlis.Rooms.Host` | rooms, sessions, control state machine with epoch, turns, timeline sequencer, AI result + presence handlers, absence/stuck-turn sweeper, approval events appended to the session timeline once per request (`approval.requested/decided/executed/expired`), `WorkspaceMembership` read model fed by the member events (checked on create room / add participant) |
| `Hosts/Majlis.Realtime.Host` | SignalR hub, Redis presence, fan-out, stream relay |
| `Hosts/Majlis.BFF.Host` | YARP routes, security headers, compression |
| `Shared/Majlis.DbMigrator` | migrations + idempotent seed (demo tenant, users, clients, room) |
| `Modules/Rooms/Majlis.Rooms.Tests`, `Modules/Workspaces/Majlis.Workspaces.Tests`, `Modules/Knowledge/Majlis.Knowledge.Tests`, `Modules/Approvals/Majlis.Approvals.Tests`, `Modules/Tasks/Majlis.Tasks.Tests`, `tests/Majlis.Architecture.Tests` | domain, app service, validator, consumer and architecture tests (module isolation: no module references another) |

Not built yet: Meetings, Notifications, Audit, Jobs host. Workspaces still lacks groups, invitations and the glossary; Knowledge still lacks folders with ACL groups, agent drafts (`draft_document` is registered but has no executor yet), live status push (`document.status`) and the viewer; Approvals still lacks per-workspace policies, multiple approvers and `update_task`; the approval expiry sweeper moves to the Jobs host when it exists.

## Deviations from the skill (ADR-0008)

- No AutoMapper: explicit `ToDto()` methods in `{Module}Mappings.cs`.
- Validators are called explicitly once per command (`ValidateAsync` in `ApplicationService`); no auto-validation.
- Messaging is Wolverine, not MassTransit (ADR-0009): `MajlisMessaging.AddMajlisMessaging…` with a per-host topology; handlers are `[WolverineHandler]` static classes with `Handle(message, services…)`; `IEventPublisher`/`IUnitOfWork` write through the transactional outbox in the module schema.
- Refit clients are source-generated (`AddRefitGeneratedClient`).
- Tests run on Microsoft.Testing.Platform (xUnit v3); coverage via `Microsoft.Testing.Extensions.CodeCoverage`.
- `[NonAction]` instead of `[NonActionApi]` (ASP.NET's attribute is sealed).

## Majlis-specific rules

- **Deployment profiles (ADR-0006).** Azure-only services are used only through adapters in `Majlis.Framework.Application`: `IBlobStorage` (`Storage/IBlobStorage.cs`; Azure Blob implementation in `Adapters/Storage`, S3-compatible later), `IEmailSender`, `IPushSender`, and secrets through the configuration provider (Key Vault / Kubernetes secrets / Vault). Build the `cloud` implementations now; `on-prem` ones (S3, SMTP, Vault) come when an on-prem customer is confirmed. The profile is configuration (`Deployment:Profile`, default `cloud`). An architecture test forbids Azure SDK references outside adapter projects.

- **Permissions** follow `Permissions.{Module}.{Action}{Entity}` (e.g. `Permissions.Rooms.CreateRoom`, `Permissions.Approvals.ApproveAction`). `PermissionChecker` grants the union of the tenant role's permissions (`RolePermissions` in each host's appsettings) and the cached workspace grants; Workspaces computes those from `WorkspaceRolePermissions` (Owner/Admin/Contributor/Viewer) as one set **per user** across their workspaces, so the owning app service must still check the role in the specific workspace (membership read model or `EnsureManager`). The `ISecuredEntity` scope is the **workspace**.
- **Cross-module membership.** Modules never call Workspaces on the hot path: they keep a read model from `MemberAdded/MemberRoleChanged/MemberRemoved` (Rooms: `WorkspaceMembership`) and declare their own copy of those event records (same alias and JSON; the role travels as its name).
- **User content is not bilingual.** Room names, messages, documents and tasks are stored as written, with a `Language` field. `NameAr`/`NameEn` pairs are for system lookups only.
- **Agent-originated writes** carry `ApprovalRequestId` and `OriginatingSessionId`, and are only accepted from the Approvals flow (never directly from ai-service).
- **Integration events** the AI side depends on: `DocumentUploaded`, `DocumentDeleted` (Knowledge → ai-service), `DocumentIndexed`, `DocumentIndexingFailed` (ai-service → Knowledge). ai-service creates approval requests over HTTP (`POST /api/approvals/requests`, the driver's token); Approvals then publishes `ActionApproved`, `ActionRejected` (→ owning module) and `ApprovalRequested/Decided/Executed` (→ Rooms timeline); the owning module answers `ActionExecuted` / `ActionExecutionFailed`.

## Checks before pushing

```
cd backend && dotnet build Majlis.sln && dotnet test
```
Warnings are errors (`Directory.Build.props`). Run locally: `docker compose up -d` at the repo root, then `backend/scripts/run-local.sh` (see `backend/README.md`).
