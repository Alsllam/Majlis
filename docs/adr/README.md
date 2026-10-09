# Architecture decision records

One file per decision: `NNNN-short-title.md` with **Status**, **Context**, **Decision**, **Alternatives** and **Consequences**. A decision is never edited after it is accepted; a new ADR supersedes it.

| ADR | Title | Status |
|---|---|---|
| [0001](0001-signalr-with-redis-backplane.md) | Real-time transport: SignalR in its own host with a Redis backplane | Accepted |
| [0002](0002-commands-over-http-events-over-hub.md) | Commands over HTTP, events over the hub | Accepted |
| [0003](0003-rooms-sequences-session-events.md) | Rooms sequences every session event; two lanes for agent output | Accepted |
| [0004](0004-single-driver-with-fencing-epoch.md) | One driver per session, protected by a fencing epoch | Accepted |
| [0005](0005-backend-module-list.md) | Backend module list and hosts | Accepted |
| [0006](0006-cloud-and-on-prem-deployment-profiles.md) | Cloud and on-prem deployment profiles from one codebase | Accepted |
