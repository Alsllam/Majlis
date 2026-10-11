# 0007 — Runtime versions: .NET 10 and Angular 22

**Status:** Accepted · 2026-10-09

## Context
The house skills named .NET 9 and Angular 20 (with Nx 21). Both reach end of support next month: .NET 9 on 10 November 2026 (STS releases now get 24 months) and Angular 20 on 28 November 2026. Majlis has no code yet, so starting on versions that stop receiving security fixes within weeks would only create an upgrade task on day one.

## Decision
- Backend: **.NET 10 (LTS, supported to November 2028)**, EF Core 10, ASP.NET Core 10, `net10.0` target.
- Frontend: **Angular 22**, **Nx 23**, **TypeScript 6.0**.
- The in-repo copies of the skills (`.claude/skills/dotnet-modular-backend`, `.claude/skills/angular-nx-frontend`) and the `CLAUDE.md` files are updated to these versions; every other skill rule stays as written.

## Consequences
- Package versions are pinned centrally (`backend/Directory.Packages.props`, `frontend/package.json`).
- Plan the next upgrades by the vendors' lifecycles: .NET 12 (next LTS) in late 2027, and Angular's yearly majors.
