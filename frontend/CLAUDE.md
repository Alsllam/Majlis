# frontend/ — Majlis web (Angular + Nx)

**Rules:** follow `.claude/skills/angular-nx-frontend/SKILL.md` for everything here. This file gives the Majlis values for its placeholders and wins where they disagree. Product invariants are in the root `CLAUDE.md`.

`frontend/` **is** the Nx workspace root (always Nx, never a plain Angular CLI project): Nx 23 in the **integrated layout** (`apps/`, `libs/`, one root `package.json`, path aliases in `tsconfig.base.json`). Generate every app, library and component with `nx g @nx/angular:… --name=… --directory=…` (`--unitTestRunner=jest --linter=eslint --prefix=majlis --standalone --strict`).

## Placeholder values

| Skill placeholder | Majlis value |
|---|---|
| `{scope}` | `@majlis` |
| `{product}` | `majlis` |
| Apps (`{app}`) | `web` (workspaces, rooms, admin settings) — port 4200 |
| Path aliases | lowercase, as Nx 23 generates them: `@majlis/core`, `@majlis/theme-shared`, `@majlis/shared-ui-common`, `@majlis/shared-charts`, `@majlis/shared-realtime`, `@majlis/{service}-proxy`, `@majlis/{feature}-config`, `@majlis/{feature}-ui-common` |
| OAuth client id | `majlis-web` (issuer = `Majlis.Auth.Host`, via the BFF) |
| Brand assets | copied from `docs/brand` (never edited here first): logos → `apps/web/public/brand/`, favicon set → `apps/web/public/`, fonts → `apps/web/public/fonts/`, token CSS → `apps/web/src/styles/brand/` (`fonts.css` with `/fonts/` urls), ECharts themes from `docs/brand/tokens/echarts/` |
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

`app-settings.json` (`apps/web/public/assets/`) → `apis` keys match the proxy names above, all pointing at the BFF (`http://localhost:7000` in dev); it also holds `realtime` (hub + ticket urls) and `rolePermissions`, a mirror of each host's `RolePermissions` used only to decide what to show (the backend re-checks every call).

## What exists (walking skeleton)

| Project | State |
|---|---|
| `apps/web` | shell: `provideMajlisCore()` + `provideThemeShared()` + feature configs, routes under `LayoutComponent` behind `authGuard`, `403`/`404` pages, `ar`/`en` JSON in `public/i18n`, brand assets, `styles.scss` wiring the brand tokens into Bootstrap variables |
| `libs/core` | `ConfigService` (loads `app-settings.json` before bootstrap), `AuthService` (angular-oauth2-oidc, code + PKCE through the BFF, claims from the access token), `authGuard`, `PermissionService` + `permissionGuard` + `*majlisPermission` (`\|\|`/`&&`), `RestService` + `HttpErrorReporterService`, `LocalizationService` (signals for `lang`/`dir`, sets `<html lang dir>`, swaps the Bootstrap LTR/RTL build), `enar` pipe, `ThemeService` (`data-theme` light/dark/dim), `RoutesService` (menu) |
| `libs/theme-shared` | `LayoutComponent` (sidebar + top bar, language/theme/user menus), `PageHeaderComponent`, `ToastService` + `<majlis-toasts>`, `HttpErrorHandler` (backend `error.messages` → toast, 401 → login, 403 → `/403`) |
| `libs/shared/ui-common` | `<majlis-avatar>` (brass for the agent, ring for the driver), `<majlis-empty-state>`, `majlisBusy` button directive. The `mof-input-*` controls, wizard and approve/reject modals come with the first form and approval screens |
| `libs/shared/charts` | theme name constants only; `ngx-echarts` setup and option builders come with the first dashboard |
| `libs/shared/realtime` | `RealtimeConnectionService` (one hub connection, ticket per (re)connect, 0/2/5/10/30 s reconnect, `Reauthenticate` every 10 min, state signal), `SessionChannel` (join, reorder buffer by `seq`, 500 ms gap fetch, stream-lane relay, snapshot for late joiners, 15 s heartbeats, idle after 2 min hidden) |
| `libs/shared/rooms-proxy` | DTOs mirroring `Majlis.Rooms.Application` (enums as strings), `RoomsService`, `SessionsService` (one method per AppService endpoint; driver commands carry `epoch`) |
| `libs/rooms/config` | `ROOMS_PERMISSIONS` (same strings as `RoomsPermissions`), `provideRoomsConfig()` menu entry |
| `libs/rooms/ui-common` | routes, rooms list (cards) + create modal, the **room screen**: `RoomSessionFacade` (timeline from durable events, streaming text merged by `chunk`, control state with the epoch, 409 → reload, presence) and the timeline / composer / hand-off components |
| `apps/web-e2e` | Playwright smoke tests against the local stack: two users share one stream, request control, hand off; menu and language switch. They run only with `MAJLIS_SEED_PASSWORD` set (`PLAYWRIGHT_CHROMIUM_PATH` points at a pre-installed Chromium) |

Not built yet: workspaces, knowledge (citations open a document), approvals cards, comments and suggestions, notifications, charts, X6 graphs, list/filter engine, wizard forms, mobile layout polish. Room creation uses the workspace of an existing room until the Workspaces module and its proxy exist.

## Deviations from the skill

- Nx 23 generates **lowercase** path aliases and the `apps/`/`libs/` integrated layout by hand-converted `nx.json` + `tsconfig.base.json` (the `angular-monorepo` preset is gone in Nx 23). Keep the aliases lowercase.
- `type:feature` libraries may also depend on `type:config` (permission constants and menu entries live there), otherwise the skill's boundary table applies (`eslint.config.mjs`).
- No `theme-layout-generator` yet: Bootstrap's own `bootstrap.min.css` / `bootstrap.rtl.min.css` are copied to `/vendor` by the app build and swapped by `LocalizationService`; brand tokens override Bootstrap variables in `styles.scss`. The Sass generator comes when a theme needs more than variables.
- Change detection is zoneless (`provideZonelessChangeDetection`); component state is signals, `OnPush` everywhere.
- The `abpLocalization` pipe is `translate` (`@ngx-translate/core` 18, `provideTranslateService` + HTTP loader); `*abpPermission` is `*majlisPermission`.
- Permissions on the client come from the token roles + `rolePermissions` in `app-settings.json` (there is no permissions endpoint yet).

## Majlis-specific rules

- The **room screen** (live agent session, presence, comments, approvals inline) is the core screen. It is not a list/wizard screen; build it as a feature-local facade on signals fed by `shared/realtime`.
- AI output is always labelled as AI-generated, with clickable citations that open the source document at the cited page.
- Mutating agent actions render as an approval card (approve / reject / edit), using the shared approve/reject modals.
- Default language `ar`, default direction `rtl`.

## Checks before pushing

```
cd frontend && npx nx affected -t lint test build
```

Run locally: `npx nx serve web` (4200) with `docker compose up -d` and `backend/scripts/run-local.sh` running. Browser tests: `MAJLIS_SEED_PASSWORD=… npx nx e2e web-e2e` (set `PLAYWRIGHT_CHROMIUM_PATH` to reuse an installed Chromium; sign-in goes through the real login page at `http://localhost:4200`, the origin the seeded `majlis-web` client and the hosts' CORS allow).
