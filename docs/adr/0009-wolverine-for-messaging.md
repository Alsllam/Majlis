# 0009 — Wolverine for messaging (replaces MassTransit)

**Status:** Accepted · 2026-10-10

## Context
ADR-0008 pinned MassTransit to 8.5.x because 9.x is commercial, and asked for a decision before 8.x support ends. The product owner chose an open-source replacement now, while the system has six handlers and one Python publisher, rather than after more modules depend on the bus.

## Decision
**Wolverine** (`WolverineFx`, MIT) over RabbitMQ, with its SQL Server transactional inbox/outbox stored in each module's own schema (`rooms.wolverine_*`) and enlisted in the module DbContext.

How it is used (`Majlis.Framework.Application/Messaging`):
- `MajlisMessaging.AddMajlisMessaging<TContext>(module, schema, topology, handlersAssembly)` for module hosts; `AddMajlisMessaging(module, topology, handlersAssembly)` for hosts without a database (Realtime).
- A **topology** names the event types a host publishes and listens to; exchange and queue names derive from the type: alias `turn-completed`, exchange `majlis.turn-completed` (durable fanout), queue `{module}.turn-completed`. The alias is set with `RegisterMessageType`, so domain contracts carry no Wolverine attribute.
- Business code keeps `IEventPublisher` and `IUnitOfWork`. In module hosts they are `OutboxEventPublisher` / `OutboxUnitOfWork`: `SaveChangesAsync` saves the rows and the events in one transaction, then sends. Handlers are plain static classes (`[WolverineHandler]`, `Handle(message, services…)`).
- **Interop:** messages are plain camelCase JSON with the alias in the AMQP `type` property. ai-service publishes exactly that to `majlis.{alias}`; the queue's `DefaultIncomingMessage` also fixes the type, so no .NET envelope is involved anywhere.
- Handler code is generated at startup (`WolverineFx.RuntimeCompilation`, `TypeLoadMode.Dynamic`). Production hardening: pre-generate with `TypeLoadMode.Static` and drop the Roslyn package from release builds.

## Consequences
- The module DbContext no longer uses EF's retrying execution strategy: the outbox commits inside a transaction Wolverine opens, which that strategy forbids. Transient failures are retried per message instead.
- Wolverine creates and migrates its own tables at startup (like Hangfire); EF migrations own the business tables only. Migration `RemoveMassTransitOutbox` dropped the old outbox tables.
- The driver-absence timeout still uses the Rooms sweeper. Wolverine's durable scheduled messages (`ScheduleAsync`) can replace it later.
- Alternatives considered: CAP (simpler, pub/sub only) and Rebus (mature, smaller ecosystem). Wolverine was chosen for the transactional outbox with EF Core, native scheduling, and plain-JSON interop.
