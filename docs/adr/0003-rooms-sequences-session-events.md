# 0003 — Rooms sequences every session event; two lanes for agent output

**Status:** Proposed · 2026-10-08

## Context
All participants must see the same timeline in the same order, late joiners must catch up, and reconnecting clients must fill gaps without a reload. Events come from several places (Rooms, ai-service, Approvals). Token-level streaming produces many small messages that are worthless once the turn is complete.

## Decision
- The **Rooms module is the only writer of a session timeline.** Every timeline event becomes a `SessionEvent` row with a per-session, gap-free `seq`, written in the same transaction as the state change and published through the MassTransit EF Core outbox. Events from ai-service and Approvals are consumed by Rooms and appended there.
- **Two lanes** for agent output: durable milestones (turn started, retrieval, tool proposed, completed/stopped/failed, summaries) go through the sequencer; token deltas and progress ticks go from ai-service to Redis pub/sub and straight to the hub, ordered by `(turnId, chunk)`, with a Redis snapshot of the text so far for late joiners. The final text is stored once, in `turn.completed`.
- Clients apply events strictly by `seq`, fetch gaps from `POST /api/rooms/sessions/events`, and drop duplicates.

## Alternatives
- **Each producer pushes to the hub directly** — no single order; approvals could appear before the tool proposal that caused them.
- **Persist every token** — large write amplification for no product value.
- **Event store / Kafka** — more infrastructure than the MVP needs; the SQL log with an outbox is enough at the MVP scale and keeps data in the existing in-Kingdom database.

## Consequences
- Rooms is on the critical path for every durable event; it must stay lean and scale horizontally (ordering comes from the database, not memory).
- `SessionEvent` is the session record for history, export and retention.
- A crashed ai-service worker is detected by heartbeat and turned into `turn.failed`.
