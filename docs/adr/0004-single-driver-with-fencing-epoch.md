# 0004 — One driver per session, protected by a fencing epoch

**Status:** Proposed · 2026-10-08

## Context
The SRS (OD-9, FR-SES-004…011) chose one driver at a time with request, hand-off, take-over and timeout. Races are normal: two people click "take control" together, a driver's tab reconnects after a hand-off, a timeout timer fires after the driver came back.

## Decision
`AgentSession` holds `DriverUserId` and `ControlEpoch`. Every control transition is one transaction that increments the epoch (optimistic concurrency with `rowversion`) and appends `control.changed`. Every driver-only command carries the epoch it was issued under; a mismatch returns 409 `Rooms:Session:ControlChanged`. Timers (driver absence, offer expiry) carry the epoch they were created for and do nothing if it changed. Any connection of the driver's own account may drive.

## Alternatives
- **Distributed lock in Redis** — locks expire and are lost on failover; a fencing token is still needed to make stale holders harmless.
- **Multiple simultaneous drivers** — needs instruction merging and conflict rules; rejected for MVP (OD-9).

## Consequences
- "Two drivers at once" cannot happen, even under races.
- Clients must send the epoch and handle 409 by re-reading session state (the shared `SessionChannel` does this).
- A turn already running when control changes finishes under the previous driver's identity; the next turn uses the new driver.
