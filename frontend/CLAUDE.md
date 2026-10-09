# frontend/ — Majlis web (Angular + Nx)

**Rules:** follow `.claude/skills/angular-nx-frontend/SKILL.md` for everything here. This file gives the Majlis values for its placeholders and wins where they disagree. Product invariants are in the root `CLAUDE.md`.

`frontend/` **is** the Nx workspace root (always Nx, never a plain Angular CLI project). Create it from the repo root with:

```
npx create-nx-workspace@23 frontend --preset=angular-monorepo --appName=web \
  --bundler=esbuild --style=scss --e2eTestRunner=playwright --nxCloud=skip
```

## Placeholder values

| Skill placeholder | Majlis value |
|---|---|
| `{scope}` | `@majlis` |
| `{product}` | `majlis` |
| Apps (`{app}`) | `web` (workspaces, rooms, admin settings) — port 4200 |
| Path aliases | `@majlis/Core`, `@majlis/theme-shared`, `@majlis/SharedUICommon`, `@majlis/{Service}Proxy`, `@majlis/{Feature}Config`, `@majlis/{Feature}UiCommon` |
| OAuth client id | `majlis-web` (issuer = `Majlis.Auth.Host`, via the BFF) |
| Brand assets | `docs/brand/logo` + `favicon` → `apps/web/src/assets/brand/`, `docs/brand/fonts/*.woff2` → `assets/fonts/`, theme from `docs/brand/tokens/scss/_majlis-tokens.scss`, ECharts themes from `docs/brand/tokens/echarts/` (never edited here first) |
| Chart theme names | `majlis-light`, `majlis-dark`, `majlis-dim` |

## Libraries

| Library | Tag | Purpose |
|---|---|---|
| `libs/core` | `type:core` | RestService, auth, permissions, routes, localization, pipes |
| `libs/theme-shared` | `type:ui` | layout, page header, modals, toasts, list/filter engine |
| `libs/shared/ui-common` | `type:ui` | `mof-input-*`, wizard, upload, approve/reject modals |
| `libs/shared/charts` | `type:ui` | ECharts setup, brand themes, option builders (usage and analytics dashboards) |
| `libs/shared/graph` | `type:ui` | AntV X6 engine (agent plan / run graph, approval flow designer) |
| `libs/shared/realtime` | `type:core` | SignalR client: ticket auth, reconnect, `SessionChannel` (reorder buffer by `seq`, gap fetch, stream-lane merge), presence — `docs/architecture/realtime-collaboration.md` §10 |
| `libs/shared/{service}-proxy` | `type:proxy` | one per backend host: `workspaces`, `rooms`, `knowledge`, `approvals`, `tasks`, `meetings`, `notifications`, `audit`, `ai` |
| `libs/{feature}/config` + `ui-common` | `type:config` / `type:feature` | features: `workspaces`, `rooms`, `knowledge`, `approvals`, `tasks`, `meetings`, `admin` |

`app-settings.json` → `apis` keys match the proxy names above, all pointing at the BFF (`http://localhost:7000` in dev).

## Majlis-specific rules

- The **room screen** (live agent session, presence, comments, approvals inline) is the core screen. It is not a list/wizard screen; build it as a feature-local facade on signals fed by `shared/realtime`.
- AI output is always labelled as AI-generated, with clickable citations that open the source document at the cited page.
- Mutating agent actions render as an approval card (approve / reject / edit), using the shared approve/reject modals.
- Default language `ar`, default direction `rtl`.

## Checks before pushing

```
cd frontend && npx nx affected -t lint test build
```
