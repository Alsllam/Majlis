# 0002 — Commands over HTTP, events over the hub

**Status:** Proposed · 2026-10-08

## Context
Users change state constantly in a room (instruct, stop, comment, suggest, hand off, approve). These actions need permissions, validation, localized errors, rate limits, audit and idempotency — all of which the backend skill already provides for AppService endpoints.

## Decision
Every state change is an HTTP command through the BFF to the owning module's AppService (with `clientRequestId` for idempotency and `epoch` for driver commands). The hub only **pushes** events to clients. The only client → hub messages are ephemeral signals that change no business data: join/leave, heartbeat (presence, typing, viewing) and re-authentication.

## Alternatives
- **Commands as hub methods** — lower latency by one round trip, but duplicates authorization, validation, error mapping and audit inside the hub, and couples the hub to every module.

## Consequences
- The hub is stateless and replaceable; it never needs a database.
- The client sees its own change twice (HTTP response, then the event); it reconciles by `seq` / `clientRequestId`.
- Mobile and web share the same REST proxies for everything except the live stream.
