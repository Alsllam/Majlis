# 0005 — Backend module list and hosts

**Status:** Proposed · 2026-10-08

## Context
Step 1 recorded a provisional module list. The real-time design (0001–0004) and the SRS functional areas are now known.

## Decision
Nine modules — Identity (Auth.Host, 7001), Workspaces (7010), Rooms (7020), Approvals (7030), Knowledge (7040), Tasks (7050), Meetings (7060), Notifications (7070), Audit (7080) — plus the BFF (7000), the Realtime host (7002) and the Jobs host (7090). Details, ownership and events in `docs/architecture/backend-modules.md`.

## Alternatives
- **Fewer, bigger modules** (e.g. one "Collaboration" module for Workspaces + Rooms + Approvals) — fewer deployables, but mixes the hot session path with low-traffic administration and blurs ownership of the approval gate.
- **More modules** (separate Sessions from Rooms, separate Drafts) — splits the session aggregate from its room or a document from its draft state; no scaling benefit at MVP size.

## Consequences
- Twelve .NET deployables plus the ai-service API and worker. Container Apps keeps idle modules cheap.
- One SQL database with a schema per module; any module can move to its own database later.
