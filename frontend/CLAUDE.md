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
| `libs/core` | `ConfigService` (loads `app-settings.json` before bootstrap), `AuthService` (angular-oauth2-oidc, code + PKCE through the BFF, claims from the access token), `authGuard`, `PermissionService` + `permissionGuard` + `*majlisPermission` (`\|\|`/`&&`), `RestService` + `HttpErrorReporterService`, `LocalizationService` (signals for `lang`/`dir`, sets `<html lang dir>`, swaps the Bootstrap LTR/RTL build), `enar` pipe, `ThemeService` (`data-theme` light/dark/dim), `RoutesService` (menu), `TOPBAR_WIDGETS` (lazy feature components the layout shows in the top bar, provided by the app) |
| `libs/theme-shared` | `LayoutComponent` (sidebar + top bar with the `TOPBAR_WIDGETS` slot, language/theme/user menus), `PageHeaderComponent`, `ToastService` + `<majlis-toasts>`, `HttpErrorHandler` (backend `error.messages` → toast, 401 → login, 403 → `/403`) |
| `libs/shared/ui-common` | `<majlis-avatar>` (brass for the agent, ring for the driver), `<majlis-empty-state>`, `majlisBusy` button directive, `ReasonModalComponent` (reject with reason / approve with note). The `mof-input-*` controls and the wizard come with the first form screens |
| `libs/shared/charts` | theme name constants only; `ngx-echarts` setup and option builders come with the first dashboard |
| `libs/shared/realtime` | `RealtimeConnectionService` (one hub connection, ticket per (re)connect, 0/2/5/10/30 s reconnect, `Reauthenticate` every 10 min, state signal), `SessionChannel` (join, reorder buffer by `seq`, 500 ms gap fetch, stream-lane relay, snapshot for late joiners, 15 s heartbeats, idle after 2 min hidden) |
| `libs/shared/rooms-proxy` | DTOs mirroring `Majlis.Rooms.Application` (enums as strings), `RoomsService`, `SessionsService` (one method per AppService endpoint; driver commands carry `epoch`) |
| `libs/shared/workspaces-proxy`, `libs/shared/identity-proxy` | `WorkspacesService` (list, get, create, update, instructions, archive/restore, set/remove member, set/remove group grant), `UsersService.lookup` (tenant user picker), `GroupsService` (list, get, create, update, delete, set/remove member) and `InvitationsService` (create, list pending, revoke), all on `/api/identity` |
| `libs/shared/knowledge-proxy` | `DocumentsService` (list, get, begin-upload, PUT to the pre-signed url with progress, confirm, download url, update, delete) and the accepted content types |
| `libs/shared/approvals-proxy`, `libs/shared/tasks-proxy` | `ApprovalsService` (list with `forMe`, get, approve with optional edited args and note, reject with reason) and `TasksService` (list, get, create, update, status), DTOs mirroring the backend |
| `libs/shared/notifications-proxy` | `NotificationsService` (list, unread count, mark read, mark all read, settings) and the notification shapes (the hub `notification` message has the same shape as a row) |
| `libs/notifications/config` + `ui-common` | `NOTIFICATIONS_PERMISSIONS` (no sidebar entry); `NotificationCenterFacade` (root: unread count + latest rows, live through the hub message `notification`, re-attached and refreshed after every reconnect, toast per new row), the top-bar **bell** (badge, last 10, click → mark read + follow the link), `/notifications` (full list, mark all read) and `/notifications/settings` (email on/off, muted kinds). Texts come from `Notifications.Types.<type>` with the row's params |
| `libs/shared/meetings-proxy` | `MeetingsService` (list, get, create, begin-recording-upload + PUT to the pre-signed url, confirm, generate minutes, update minutes, approve, propose action items, publish, export as a blob, delete) and the meeting / minutes / action item shapes |
| `libs/meetings/config` + `ui-common` | `MEETINGS_PERMISSIONS`, the Meetings menu entry; `/meetings`: list per workspace + create form (notes, transcript file read in the browser, or recording upload with progress); `/meetings/:id`: status polled while transcribing or summarizing, failure reason, transcript, the minutes **editor** while the draft is open (summary, decisions and open questions one per line, action items with owner and due date), approve, then propose the action items (one approval each, state per item: not proposed / awaiting approval / became a task), publish to knowledge, download Markdown |
| `libs/shared/audit-proxy` | `AuditService` (list with filters, verify, CSV export as a blob) and the entry shapes |
| `libs/admin/config` + `ui-common` | `ADMIN_PERMISSIONS`, the Groups menu entry (tenant admins: `Permissions.Identity.ManageGroups`); `/admin/groups`: create, rename, delete a group, add members through the user lookup |
| `libs/audit/config` + `ui-common` | `AUDIT_PERMISSIONS`, the Audit menu entry (admins: `Member` has no audit permission); `/audit`: filters (category, workspace, dates, text), paged table with expandable details and hash, chain verification banner, CSV download |
| `libs/approvals/config` + `ui-common` | `APPROVALS_PERMISSIONS`, the Approvals menu entry; the approver inbox at `/approvals` (tabs: for me / all pending / history; approve, reject with reason, link to the room) |
| `libs/tasks/config` + `ui-common` | `TASKS_PERMISSIONS`, the Tasks menu entry; the tasks list per workspace at `/tasks` (status filter, quick add, inline status change, agent-created tasks marked and linked to their room) |
| `libs/knowledge/config` + `ui-common` | `KNOWLEDGE_PERMISSIONS`, the Documents menu entry; documents list per workspace (upload with progress, status polled while queued/processing, agent-draft tag, download, delete) and the document page (versions, cited passage from `?page=&passage=`, in-page reader for markdown/text versions through a small safe block parser, **Add to knowledge** for agent drafts) |
| `libs/workspaces/config` + `ui-common` | `WORKSPACES_PERMISSIONS`, the Home and Workspaces menu entries; the **workspace home** at `/home` (landing page after sign-in: the last-used workspace remembered in `localStorage`, tiles for live rooms / approvals awaiting me / my open tasks / role, blocks for rooms, approvals, tasks and latest documents from each module's proxy, refreshed every 30 s); workspaces list (cards with icon, brand color, role) + create/edit modal; workspace page: members table with role changes and removal (members brought in by a group are tagged), add member through the user lookup, **groups** section (grant a tenant group Admin / Contributor / Viewer, change or remove), **invitations** section (invite by email with a role; pending invitations with revoke; an existing user is added at once), agent instructions editor, archive/restore (owner) |
| `libs/rooms/config` | `ROOMS_PERMISSIONS` (same strings as `RoomsPermissions`), `provideRoomsConfig()` menu entry |
| `libs/rooms/ui-common` | routes, rooms list (cards, `?workspaceId=` filter) + create modal that picks one of the user's workspaces, the **room screen** (citations `[S#]` link to the document page at the cited page and passage): `RoomSessionFacade` (timeline from durable events, streaming text merged by `chunk`, control state with the epoch, 409 → reload, presence, **approval cards** merged by `requestId` from `approval.requested/decided/executed/expired` with approve / reject-with-reason through `ApprovalsService`, long text arguments collapsed, a link to the draft once `draft_document` ran) and the timeline / approval card / composer / hand-off components |
| `apps/web-e2e` | Playwright smoke tests against the local stack (one worker: they share the seeded room): the home page lands on the last workspace with its blocks; an admin sees control changes in the audit log, verifies the chain and exports it, a member cannot open it; two users share one stream, request control, hand off; an owner creates a workspace, adds a member and writes instructions, the member creates a room in it; a tenant admin builds a group, an owner grants it a role and a group member sees the workspace, an invited stranger registers through the emailed link (read from the Auth host's `/dev/emails`) and lands in the workspace, leaving the group takes the workspace away; a contributor uploads a document, sees it indexed and gets a cited answer that opens the document; the agent proposes a task, a person approves it on the card and the task appears in the list, a rejected proposal never becomes a task; the agent drafts a document, a person approves it, reads it and adds it to the knowledge; a meeting from notes gets its minutes from the agent, a person edits and approves them, the action items become approvals and one becomes a task, the minutes reach the documents list and the Markdown export downloads; an approval request reaches the approver's bell live and the decision comes back to the requester, plus the list and settings pages; menu and language switch. They run only with `MAJLIS_SEED_PASSWORD` set (`PLAYWRIGHT_CHROMIUM_PATH` points at a pre-installed Chromium) |

Not built yet: live meeting status through the hub (polled today), transcript speakers, a server-side home summary (counts are client-side today), a users admin page (deactivation), the PDF viewer with highlighting (FR-KNW-006), folders, draft editing and export, edit-then-approve on the card, task editing and assignment pickers, comments and suggestions, quiet hours (FR-NTF-004), push, charts, X6 graphs, list/filter engine, wizard forms, mobile layout polish.

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
- Mutating agent actions render as an approval card in the timeline (approve / reject with reason; edit-then-approve later), using the shared `ReasonModalComponent`. The buttons show to users with `Permissions.Approvals.ApproveAction`; the backend policy decides (a 403 is reported by the global handler).
- Default language `ar`, default direction `rtl`.

## Checks before pushing

```
cd frontend && npx nx affected -t lint test build
```

Run locally: `npx nx serve web` (4200) with `docker compose up -d` and `backend/scripts/run-local.sh` running. Browser tests: `MAJLIS_SEED_PASSWORD=… npx nx e2e web-e2e` (set `PLAYWRIGHT_CHROMIUM_PATH` to reuse an installed Chromium; sign-in goes through the real login page at `http://localhost:4200`, the origin the seeded `majlis-web` client and the hosts' CORS allow).
