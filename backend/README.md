# Majlis backend

.NET 10 modular backend. Rules: [`CLAUDE.md`](CLAUDE.md) and `.claude/skills/dotnet-modular-backend`. Architecture: [`docs/architecture`](../docs/architecture).

## Run locally

Prerequisites: .NET 10 SDK, Docker.

```bash
cp .env.example .env            # at the repo root; fill in every value (local only, git-ignored)
docker compose up -d             # SQL Server, Redis, RabbitMQ
backend/scripts/run-local.sh     # migrations + seed, then Auth (7001), Workspaces (7010), Rooms (7020), Knowledge (7040), Realtime (7002), BFF (7000)
backend/scripts/stop-local.sh
```

Logs: `backend/.local/logs/*.log`. Everything is reached through the BFF at `http://localhost:7000`:

| Path | Host |
|---|---|
| `/connect/*`, `/.well-known/*`, `/Account/*` | Auth (OpenIddict, login page) |
| `/api/realtime/ticket` | Auth (hub ticket) |
| `/api/identity/users/lookup` | Auth host (tenant user picker) |
| `/api/workspaces/workspaces/*` | Workspaces |
| `/api/rooms/rooms/*`, `/api/rooms/sessions/*` | Rooms |
| `/api/knowledge/documents/*` | Knowledge (uploads go straight to blob storage through pre-signed urls) |
| `/hubs/session` | Realtime (SignalR) |
| `/ai-api/*` | ai-service (next PR) |

Seeded demo data (only when `MAJLIS_SEED_PASSWORD` is set): tenant "جهة تجريبية", users `sara@` (TenantAdmin), `khalid@`, `noura@` `demo.majlis.local`, all with that password, and the room "غرفة العقود" with all three as contributors. The web client `majlis-web` accepts redirects to `http://localhost:4200`.

## Test

```bash
cd backend && dotnet build Majlis.sln && dotnet test
```

## Add a migration

```bash
cd backend && dotnet tool restore
dotnet ef migrations add <Name> -p Modules/<Module>/Majlis.<Module>.EntityFrameworkCore -s Modules/<Module>/Majlis.<Module>.EntityFrameworkCore -o Migrations
```
Migrations are applied only by `Shared/Majlis.DbMigrator`, never at host startup. The exception is Wolverine, which creates and migrates its own `wolverine_*` tables in each module schema when the host starts (ADR-0009).

## Messaging

Wolverine over RabbitMQ (ADR-0009). Each host declares a topology (`RoomsApplicationModule.Topology`); exchanges `majlis.{alias}` and queues `{module}.{alias}` are provisioned at startup. Handlers are `[WolverineHandler]` static classes in `Application/EventHandlers`. If queues from an older topology linger (e.g. after a bus change), remove them in the RabbitMQ console or reset with `docker compose down -v`.
