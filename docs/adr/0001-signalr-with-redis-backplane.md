# 0001 — Real-time transport: SignalR in its own host with a Redis backplane

**Status:** Accepted · 2026-10-09 (proposed 2026-10-08)

## Context
Shared sessions need server push to up to 25 participants per room with p95 ≤ 300 ms (NFR-PRF-002), automatic reconnect (NFR-AVL-005), web and mobile clients, and all data in Saudi Arabia (NFR-RES-001). Some government networks block or break WebSockets through proxies.

## Decision
Use **ASP.NET Core SignalR** in a dedicated `Majlis.Realtime.Host`, scaled out with the **Redis backplane** (Redis in Saudi Arabia East). Transports: WebSockets, then Server-Sent Events, then long polling (BFF affinity for the fallbacks). JSON protocol for the MVP. Clients: `@microsoft/signalr` (Angular), `signalr_netcore` (Flutter).

## Alternatives
- **Azure SignalR Service / Azure Web PubSub** — managed scale-out, but availability in Saudi Arabia East is not confirmed; adopt later behind the same hub code if it is.
- **Raw WebSockets** — we would rebuild groups, reconnect, fallback and scale-out ourselves.
- **SSE from each API** — one-way only, and fan-out across instances would still need a backplane.
- **Hub inside the Rooms host** — couples connection scaling to API scaling and makes Rooms restarts drop every socket.

## Consequences
- One more deployable; it holds no business state and no database.
- Redis becomes critical for live updates (not for commands); fallback to polling is designed in (realtime-collaboration §9).
- Moving to Azure SignalR Service later changes only host configuration.
