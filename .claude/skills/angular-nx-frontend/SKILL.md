---
name: "angular-nx-frontend"
description: Rules for building or extending an Angular 20 frontend, always in an Nx workspace, with ECharts for charts, AntV X6 for diagrams, typed proxies, list/filter engine, wizard forms, permissions, Arabic/English RTL, and a project brand kit (logo, colors, motion). Use for any frontend work in these projects.
---

# Angular + Nx Frontend Rules

These rules describe the house frontend architecture. They come from two reference workspaces: a Housing frontend (Angular 20, standalone components, its own core library) and an Inspection frontend (Angular 17 on the ABP framework, NgModules). Where the two differ, follow the Housing way, which is the newer one. Known weaknesses in both have been fixed here.

They work together with the `dotnet-modular-backend` skill: the endpoints, permission names and error format below match that backend.

Placeholders: `{scope}` = npm scope (e.g. `@acme-fe`), `{app}` = application, `{feature}` = business domain (kebab-case), `{Entity}` = screen entity (PascalCase), `{entity}` = kebab-case.

---

## 0. Every new project starts with a brand kit

Before writing any screen, create the product's visual identity. Never ship a new project with a placeholder logo, default Bootstrap blue, or no motion.

### 0.1 Logo
- Design an **original** logo that fits the product's domain (housing → shelter/keys/blocks, inspection → shield/checkmark/lens). Never copy or imitate another organization's logo, emblem or wordmark.
- Deliver it as hand-written **SVG**: a simple geometric mark built from a few shapes, using `currentColor` or brand tokens. No embedded raster images.
- Required variants in `apps/{app}/src/assets/brand/`:
  - `logo.svg`: full lockup, mark + wordmark (Arabic and English versions if the product is bilingual: `logo-ar.svg`, `logo-en.svg`)
  - `logo-mark.svg`: icon only, readable at 16–24 px
  - `logo-mono.svg`: single color, for dark backgrounds and print
  - favicon set: `favicon.svg`, `favicon.ico`, `apple-touch-icon.png` (180), `web-app-manifest-192x192.png`, `web-app-manifest-512x512.png`, `site.webmanifest`
- Check it at 16 px, 32 px and 128 px, on light and dark backgrounds, in RTL and LTR layouts.
- Give the logo a short entrance animation on the login/splash screen (stroke draw-in or scale-and-fade, 600–900 ms, once). Never loop it on working screens.

### 0.2 Color system
Define tokens once, in `theme-layout-generator/src/_colors/_{theme}.scss`, and expose them as CSS custom properties. Components use only the tokens, never raw hex values.

```scss
:root {
  --brand-50 … --brand-900;     // primary scale (10 steps), generated from one base hue
  --accent-500, --accent-600;   // secondary, for highlights and charts
  --neutral-0 … --neutral-900;  // surfaces, borders, text
  --success-500; --warning-500; --danger-500; --info-500;   // each with a -50 tint for backgrounds
  --surface: var(--neutral-0);  --surface-raised; --surface-sunken;
  --text-primary; --text-secondary; --text-muted; --border-subtle;
  --radius-sm: 6px; --radius-md: 8px; --radius-lg: 12px; --radius-xl: 16px;
  --shadow-sm; --shadow-md; --shadow-lg;   // soft, layered, tinted with the brand hue
}
```
- Choose a distinctive palette for each product: one confident primary hue, one complementary accent, warm or cool neutrals that match. Avoid default Bootstrap blue.
- Build **light, dark and dim** themes from the same tokens. Dark mode is not inverted colors: use raised surfaces, slightly desaturated brand colors, and lower-contrast borders.
- Text contrast must meet **WCAG AA**: at least 4.5:1 for body text and 3:1 for large text and UI controls. Check every token pair you use.
- Map status colors once (`status-tag-success|warning|danger|neutral|info`) and reuse them for request status tags and charts.
- Charts (ECharts) take their series colors from the token palette, in a fixed order, via one shared theme registered at startup.

### 0.3 Typography
- One font for Arabic and one for Latin, paired so they look balanced, e.g. *IBM Plex Sans Arabic* + *IBM Plex Sans*, or *Tajawal* + *Inter*. Self-host them in `assets/fonts` with `font-display: swap`.
- Type scale tokens: `--fs-xs 12 / sm 14 / md 16 / lg 18 / xl 20 / 2xl 24 / 3xl 30 / 4xl 36`, weights 400/500/600/700, line-height 1.5 for body and 1.25 for headings.
- Numbers in tables use `font-variant-numeric: tabular-nums`. The `enar` pipe switches between Arabic-Indic and Latin digits based on the current language.

### 0.4 Motion and micro-interactions
Motion should explain what changed, not decorate. Every screen has it, and it is always quick.

| Token | Value | Use |
|---|---|---|
| `--motion-fast` | 120 ms | hover, focus, press |
| `--motion-base` | 200 ms | dropdowns, tabs, toasts, filter chips |
| `--motion-slow` | 320 ms | modals, side panels, page transitions |
| `--ease-out` | `cubic-bezier(.2,.8,.2,1)` | elements entering |
| `--ease-in` | `cubic-bezier(.4,0,1,1)` | elements leaving |

Required patterns:
- **Page enter:** route content fades in and rises 8 px (`provideAnimations` + a shared `routeFade` trigger, or a View Transitions API wrapper).
- **Lists:** skeleton rows while loading (already in `ListFilterService`). Rows then stagger in, 30 ms apart, for at most the first 10 rows.
- **Filter sidebar and modals:** slide or scale in from the logical start side (respecting RTL), with a backdrop fade.
- **Buttons:** a 2 px lift and stronger shadow on hover, slight scale-down (0.98) on press. Busy buttons show an inline spinner (`busyButton` directive) and keep their width.
- **Toasts:** slide in from the top end and auto-dismiss with a progress bar.
- **Wizard steps:** the progress indicator animates, and step content cross-fades.
- **Empty states:** an illustration plus a call-to-action button. Make the illustrations simple SVGs in brand colors.
- **Dashboards:** KPI numbers count up once on first load. Charts use ECharts' built-in entry animation.
- Always honor `@media (prefers-reduced-motion: reduce)`: turn transforms and staggers off and keep only opacity changes.

### 0.5 Look and layout
- Layout: sidebar plus top bar, with content on `--surface-sunken` and cards on `--surface` with `--radius-lg` and `--shadow-sm`. Spacing comes from an 8 px scale (`p-8 / p-16 / p-24 / p-32`).
- Page structure: always `app-page-header` (title, breadcrumbs, primary action), then a card containing filters, filter chips and the table.
- Icons: one consistent outline icon set, drawn in `currentColor`.
- Every component looks right in RTL and LTR. Use logical properties (`margin-inline-start`, `inset-inline-end`) and `ms-*`/`me-*` utilities, never left/right.
- Check every new screen in light and dark, Arabic and English, at 375 px, 768 px and 1440 px wide.

---

## 1. Tech stack (do not swap without being asked)

| Concern | Choice |
|---|---|
| Framework | Angular 20, **standalone components only** (no new NgModules), signals for local state |
| Monorepo | Nx 21 (`@nx/angular`), ESLint 9 flat config, Prettier (`singleQuote: true`) |
| Language | TypeScript 5.9 with **`strict: true`** in new workspaces |
| UI kit | Bootstrap 5.3 (themes compiled by `theme-layout-generator`: Sass + PostCSS `rtlcss`, light/dark/dim) + `@ng-bootstrap/ng-bootstrap` |
| Tables | `@swimlane/ngx-datatable` through the shared list engine |
| Selects | `@ng-select/ng-select` (wrapped by `mof-input-dropdown/autocomplete/multiselect`) |
| Forms | Reactive Forms + `@ngx-validate/core` + shared `CustomValidators` |
| i18n | `@ngx-translate` with `public/i18n/{ar,en}.json`, full RTL |
| Auth | `angular-oauth2-oidc`, authorization code flow against the OpenIddict host |
| Charts | **Always ECharts** via `ngx-echarts` (see §6.5). No Chart.js, ngx-charts, Highcharts or ApexCharts |
| Diagrams / graphs | **Always AntV X6** (`@antv/x6` + `x6-angular-shape` + plugins: `history`, `selection`, `minimap`, `scroller`, `stencil`, `snapline`, `keyboard`, `clipboard`) with `dagre` for auto-layout (see §6.6). No GoJS, jsPlumb, mxGraph or hand-rolled SVG editors |
| PDF | `ngx-extended-pdf-viewer` |
| Loading | `ngx-spinner` (global), skeleton directive (inline) |
| Utilities | `ngxtension`, `@rx-angular/template` for heavy lists |
| Tests | Jest (`jest-preset-angular`) for libraries; Playwright for E2E (Arabic and English, multi-role) |
| Node | 22 LTS, pinned in `.nvmrc` |

---

## 2. Workspace layout

**Always an Nx workspace.** Every frontend, even one with a single app, is created with `npx create-nx-workspace@21 {product}-frontend --preset=angular-monorepo --bundler=esbuild --style=scss --e2eTestRunner=playwright`. Never a plain `ng new` Angular CLI project, and never apps without libraries. Every app, library, component and service is created with an Nx generator (`nx g @nx/angular:…`). Every build, serve, test and lint command runs through Nx (`nx run`, `nx affected`, `nx run-many`), so caching and module-boundary rules always apply.

```
{product}-frontend/
├── apps/
│   ├── {app}/                     # e.g. admin, portal, internal — thin shells
│   │   ├── src/app/app.config.ts  # provide* calls only
│   │   ├── src/app/app.routes.ts  # loadChildren from feature ui-common libs
│   │   ├── src/{app}-layout/      # sidebar, top bar, notification dropdown
│   │   ├── src/assets/app-settings.json        # runtime config (replaced per environment)
│   │   ├── src/assets/brand/      # logo + favicon set (§0.1)
│   │   └── public/i18n/{ar,en}.json
├── libs/
│   ├── core/                      # RestService, auth, PermissionService + directive + guard, RoutesService, ListService, LocalizationService, pipes, utils
│   ├── theme-shared/              # page header, modal, toaster, confirmation, list/filter engine, error handlers, skeletons
│   ├── shared/
│   │   ├── ui-common/             # mof-input-* form controls, wizard, file upload, action history, approve/reject modals
│   │   └── {service}-proxy/       # one typed API client lib per backend microservice
│   └── {feature}/
│       ├── config/                # provide{Feature}Config(): menu entries for the sidebar
│       └── ui-common/             # screens, routes, feature-local services
├── theme-layout-generator/        # Sass → bootstrap-{light,dark,dim}[.rtl].css into each app's assets
├── tools/                         # build-version script, generators
├── HLD.md  LLD.md  GENERATING_CODE.md  DEPLOYMENT.md
```

**Path aliases** in `tsconfig.base.json`, PascalCase after the scope: `{scope}/Core`, `{scope}/theme-shared`, `{scope}/SharedUICommon`, `{scope}/{Service}Proxy`, `{scope}/{Feature}Config`, `{scope}/{Feature}UiCommon`.

**Dependency rules.** Give every project an Nx tag and enforce the rules with `@nx/enforce-module-boundaries`. Do not use `'*' → '*'`.

| Tag | May depend on |
|---|---|
| `type:app` | anything except other apps |
| `type:feature` (ui-common) | `type:proxy`, `type:ui`, `type:core` |
| `type:config` | `type:core` |
| `type:proxy` | `type:core` |
| `type:ui` (theme-shared, shared/ui-common, shared/charts, shared/graph) | `type:core` |
| `type:core` | nothing internal |

Features never import other features. If two features need the same thing, move it into `shared/ui-common` or a proxy.

---

## 3. Runtime configuration

- `environment.ts` only points at the remote config: `remoteEnv: { url: '/assets/app-settings.json', mergeStrategy: 'deepmerge' }`.
- `app-settings.json` holds `oAuthConfig` (issuer, clientId, `responseType: 'code'`, scope), `application` (baseUrl, name), and `apis`, a map of `apiName → { url }`, one per backend host behind the BFF gateway.
- The build is environment-agnostic. Deployment replaces `app-settings.json`. Never put secrets in it, since it is public.
- Production uses `requireHttps: true` and does **not** set `skipIssuerCheck`.
- The web server rewrites every unknown route to `index.html`.

## 4. Proxy libraries (`libs/shared/{service}-proxy`)

One library per backend microservice. One `{Entity}Service` per backend AppService, mirroring the backend endpoint table exactly:

```ts
@Injectable({ providedIn: 'root' })
export class BuildingsService {
  private readonly rest = inject(RestService);
  readonly apiName = 'housingEntities';          // MUST exist in app-settings.json → apis

  getList = (input: BuildingFilterDto, config?: RestConfig) =>
    this.rest.request<BuildingFilterDto, PagedResultDto<BuildingListDto>>(
      { method: 'POST', url: '/buildings/list', body: input }, { apiName: this.apiName, ...config });
  get = (id: string, config?: RestConfig) =>
    this.rest.request<EntityIdDto, BuildingDto>(
      { method: 'POST', url: '/buildings/getbyid', body: { id } }, { apiName: this.apiName, ...config });
  create = (input: CreateBuildingDto, config?: RestConfig) =>
    this.rest.request<CreateBuildingDto, string>({ method: 'POST', url: '/buildings', body: input }, { apiName: this.apiName, ...config });
  update = (input: UpdateBuildingDto, config?: RestConfig) =>
    this.rest.request<UpdateBuildingDto, void>({ method: 'PUT', url: '/buildings', body: input }, { apiName: this.apiName, ...config });
  delete = (id: string, config?: RestConfig) =>
    this.rest.request<EntityIdDto, void>({ method: 'DELETE', url: '/buildings', body: { id } }, { apiName: this.apiName, ...config });
  activate / deactivate  → POST /buildings/activate | /deactivate  { id }
  export                 → POST /buildings/export  (filter) → { url }
  lookup / {sub}Lookup   → POST /buildings/lookup | /buildings/{sub}/lookup → PagedResultDto<LookupDto>
}
```
- Write methods as **arrow-function properties**, so they can be passed directly as `ListFilterService(service.getList)`.
- **No `any`.** Every request and response is typed. DTOs live in `src/lib/models/{entity}.model.ts` and match the backend DTO names (`{Entity}Dto`, `{Entity}ListDto`, `Create{Entity}Dto`, `Update{Entity}Dto`, `Filter{Entity}Dto extends BaseFilterDto`).
- `BaseFilterDto = { filterText?, skipCount, maxResultCount, sorting?, activeFilter?: 'All' | 'Active' | 'InActive' }`. The values must match the backend enum spelling exactly.
- Proxies are `providedIn: 'root'`. Do not list them again in component `providers` (that creates a second instance).
- Bilingual fields come in pairs (`nameAr`/`nameEn`). Pick one with the `enar` pipe in templates, or the `localizedName()` util in code. Never write `currentLang === 'ar' ? … : …` inline.
- If the backend publishes OpenAPI or ABP proxy metadata, generate the proxies (`generate-proxy` script) instead of writing them by hand, then commit the result.

## 5. Feature libraries

### 5.1 `{feature}/config`
```ts
export function provide{Feature}Config() {
  return makeEnvironmentProviders([
    provideAppInitializer(() => inject(RoutesService).add([
      { path: '/', name: 'Menu.{Feature}', order: 5, layout: eLayoutType.application, expanded: true,
        requiredPolicy: 'Permissions.Buildings.ViewBuilding || Permissions.Units.ViewUnit' },
      { path: '/buildings', name: 'Menu.Buildings', icon: 'building', order: 2, parentName: 'Menu.{Feature}',
        layout: eLayoutType.application, requiredPolicy: 'Permissions.Buildings.ViewBuilding' },
    ])),
  ]);
}
```
The app registers it in `app.config.ts`. The sidebar shows only entries whose `requiredPolicy` is granted.

### 5.2 `{feature}/ui-common`: one flat folder per entity
```
src/lib/{entity-plural}/
├── {entity}.routes.ts                          # export const {Entity}UICommonRoutes
├── {entity}-list.component.ts|html             # container: table + row actions
├── {entity}-list-filter.component.ts|html      # quick filters + advanced filter sidebar + export
├── {entity}-form-wizard.component.ts|html      # page shell: header + mof-wizard (tabs or steps)
├── {entity}-form-wizard-basic-info.component.ts|html   # one component per wizard step
├── {entity}-form-wizard-{step}.component.ts|html
├── {sub}-modal/{sub}-modal.component.ts|html   # modals get their own folder
└── {entity}.facade.ts                          # optional: feature-local state/orchestration
```
`src/index.ts` exports **only routes** (and any public tokens). Components stay private to the library.

### 5.3 Routes (always this shape)
```ts
export const BuildingUICommonRoutes: Route[] = [
  { path: '',           loadComponent: () => import('./buildings-list.component').then(m => m.BuildingsListComponent),
    canActivate: [permissionGuard], data: { requiredPolicy: 'Permissions.Buildings.ViewBuilding' } },
  { path: 'create',     loadComponent: () => import('./buildings-form-wizard.component').then(m => m.BuildingsFormWizardComponent),
    canActivate: [permissionGuard], canDeactivate: [formDeactivateGuard], data: { requiredPolicy: 'Permissions.Buildings.CreateBuilding' } },
  { path: 'view/:id',   loadComponent: …FormWizard…, canActivate: [permissionGuard], data: { requiredPolicy: '…View…', isReadOnly: true } },
  { path: 'update/:id', loadComponent: …FormWizard…, canActivate: [permissionGuard], canDeactivate: [formDeactivateGuard],
    data: { requiredPolicy: '…Update…' } },
];
```
In `app.routes.ts`: `{ path: 'buildings', loadChildren: () => import('{scope}/HousingEntitiesUiCommon').then(m => m.BuildingUICommonRoutes) }`, plus `''` (home, `authGuard`), `403`, `404`, and `'**' → '/404'`.

## 6. Screen patterns

### 6.1 List screen
```ts
@Component({
  selector: 'app-buildings-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [/* datatable, list directives, page header, filter, pipes, PermissionDirective */],
  templateUrl: './buildings-list.component.html',
  providers: [
    ListService, FilterParamsService,
    { provide: ListFilterService, useFactory: () => new ListFilterService(inject(BuildingsService).getList) },
  ],
})
export class BuildingsListComponent extends AbstractListComponent<BuildingListDto> {
  private readonly buildings = inject(BuildingsService);
  actions: PageHeaderActions[] = [{ actionName: 'Buildings.Create', type: PageHeaderActionTypes.Primary,
    permission: 'Permissions.Buildings.CreateBuilding', callback: () => this.create() }];

  delete(id: string)     { this.confirmAndRun('General.DeleteItemMessage', () => this.buildings.delete(id), 'error'); }
  activate(id: string)   { this.confirmAndRun('General.ActivateItemMessage', () => this.buildings.activate(id)); }
  deactivate(id: string) { this.confirmAndRun('General.DeactivateItemMessage', () => this.buildings.deactivate(id)); }
}
```
- `confirmAndRun(messageKey, action$, severity?)` lives in `AbstractListComponent`. It opens the confirmation, runs the action with the spinner, shows the success toast, and refreshes the list (stepping back a page when the last row on the page was removed). **Never copy that block into each method.**
- Template order: `app-page-header`, then a card with `app-{entity}-list-filter`, `app-filter-params` (chips), `ngx-datatable` with `ngx-datatable-column-labeled` columns, an actions dropdown (`ngbDropdown`, `container="body"`), and `ngx-datatable-paging-with-loader` (handles empty, no results, and loading states).
- The first column links to `view/:id`. A status column uses `status-tag-*`. The actions menu has Details, Edit, Activate/Deactivate and Delete, each wrapped in `*abpPermission` **on the button itself**.
- Use built-in control flow (`@if`, `@for` with `track`), not `*ngIf`/`*ngFor`.

### 6.2 Filter component
Extends `AbstractAdvancedFilterComponent`:
- `initForm()` sets up `addQuickFilters([...])` (shown inline) and `filtersForm = fb.group({ x: new FilterFormControl({ filterTitle, filterValueFun? }) })` (side panel).
- The filter state is saved per screen (ui-status), so it survives navigation. Search is debounced by 300 ms inside `ListFilterService`.
- Export: `service.export(listFilterService.lastQuery)` under the spinner, then `downloadBlob(url, '{Entity}.xlsx')`.
- Dropdown search functions come from a shared helper: `lookupFn(service.compoundLookup, { activeFilter: 'Active' })`, which maps results to `{ id, displayName }` using the current language. Don't redefine that mapping in every component.

### 6.3 Create / edit / view (wizard)
- The page shell (`{entity}-form-wizard`) reads `id` and `data.isReadOnly` from the route. It shows `app-page-header` (title `{Entity}.Create | Edit | Details`, breadcrumb from the step's `dataEmitter`) and `<mof-wizard [isWizardMode]="steps > 1">`.
- Each step extends `WizardStepComponent`, is provided as `{ provide: WizardStepComponent, useExisting: forwardRef(() => Step) }`, and implements:
  - `validateSaveStep(): Observable<boolean> | boolean`: if the form is invalid, mark it touched and return `false`. Otherwise call `create` or `update`, show the success toast, and `map(() => true)`.
  - `canChangeStep()`: return `!form.dirty`.
- Build forms with `fb.nonNullable.group` and typed controls. Validators come from `CustomValidators` (`RequiredValidator`, `InvalidCharacterValidator`, ...), and length limits from shared constants that match the backend `FieldDefinitions` (e.g. `MAX_NAME_LENGTH = 120`).
- Register the form with `FormStateService` so `formDeactivateGuard` warns about unsaved changes. Unregister it in `ngOnDestroy`.
- Read-only mode is `form.disable()` with the wizard buttons hidden.
- Inputs are always `mof-input-*` components (`text`, `number`, `dropdown`, `autocomplete`, `static-dropdown`, `switch`, `date-picker`, `textarea`, `attachment`, ...). They handle the label, required asterisk, validation message, and RTL. Never use a bare `<input>` in a feature.
- Use `(ngSubmit)` on the form, never an inline `onsubmit=` attribute.

### 6.4 Modals
Open with `NgbModal` through the shared `ModalComponent`/`ModalRefService`. Approve/reject/confirm flows reuse `approve-request-modal`, `reject-request-modal` and `confirm-action-modal` from `shared/ui-common`.

### 6.5 Charts: always ECharts
- **One shared chart library:** `libs/shared/charts` (tag `type:ui`). It holds:
  - `provideEchartsCore({ echarts: () => import('./echarts-setup') })`. `echarts-setup.ts` imports **only the modules used** from `echarts/core` (`BarChart`, `LineChart`, `PieChart`, `GaugeChart`, `GridComponent`, `TooltipComponent`, `LegendComponent`, `DatasetComponent`, `CanvasRenderer`). Never `import * as echarts from 'echarts'`.
  - `registerBrandTheme()`: one ECharts theme per color mode (`brand-light`, `brand-dark`) built from the CSS tokens (§0.2). It sets series colors, axis and grid colors, tooltip style, and the Arabic/Latin font family.
  - Option builders: `barOption()`, `lineOption()`, `donutOption()`, `gaugeOption()`, `kpiSparkline()`. They take typed data (`{ label, value }[]`, series arrays) and return `EChartsOption`. Feature components never write a full option object by hand.
  - `<app-chart [options] [loading] [height]>`: a wrapper around the `echarts` directive. It switches theme with the color mode, mirrors axes and legend in RTL (`inverse` on the category axis, legend aligned to the end), uses `autoResize`, shows a skeleton while loading, and shows an empty state when there is no data.
- Labels and tooltips come from translation keys. Numbers are formatted with the `enar` digit rule and `Intl.NumberFormat` for the current locale.
- Animation: keep ECharts' entry animation (`animationDuration: 600`, `animationEasing: 'cubicOut'`). Set `animation: false` when `prefers-reduced-motion` is on.
- Dashboard cards: `@defer (on viewport)` for each chart, and a fixed height so the layout does not jump.
- Export: use the toolbox `saveAsImage` only when the screen asks for it. Data export goes through the backend `export` endpoint.

### 6.6 Diagrams, workflows and graphs: always AntV X6
Use X6 for every node-and-edge screen: workflow/process designers, org charts, approval flows, dependency maps, read-only status diagrams.

**Library layout:** `libs/shared/graph` (tag `type:ui`) holds the reusable engine. Each feature keeps only its node types and rules.
```
libs/shared/graph/src/lib/
├── graph-canvas.component.ts        # <app-graph-canvas>: owns the Graph instance, toolbar, minimap
├── graph.store.ts                   # @Injectable() component-scoped store: graph, readonly, selection, dirty — signals
├── graph-options.ts                 # createGraph(container, opts): grid, panning, mousewheel zoom, connecting rules, highlighting
├── plugins.ts                       # useHistory / useSelection / useMinimap / useSnapline / useKeyboard / useClipboard
├── layout.ts                        # applyDagreLayout(graph, { rankdir: 'LR' | 'TB' }) — flips LR↔RL in Arabic
├── edges.ts                         # registerBrandEdges(): one edge style from tokens, hover/selected states
├── serialization.ts                 # toDto(graph) → { nodes, edges }; fromDto(dto) — the only JSON shape sent to the backend
└── validation.ts                    # GraphRule[] runner → { cellId, messageKey }[] and highlights the offending cells
libs/{feature}/ui-common/src/lib/{diagram}/
├── nodes/{type}-node.component.ts   # Angular node components registered via x6-angular-shape `register({ shape, content, injector })`
├── panels/{type}-properties.component.ts  # side panel form for the selected node (mof-input-*)
├── {diagram}.rules.ts               # domain rules (e.g. a condition node needs ≥ 2 outgoing edges)
└── {diagram}-designer.component.ts  # composes <app-graph-canvas> + stencil + properties panel
```

**Rules:**
- **No module-level singletons.** Graph state lives in the `GraphStore` provided in the canvas component's `providers`, so two diagrams on one page never share state. Never use `document.getElementById`. Use `viewChild.required<ElementRef>('container')`.
- **Commands are pure functions:** `(graph: Graph, args) => void`, e.g. `createNode`, `linkNodes`, `removeNodeWithEdges`, `replaceNodeOnRemove`. They are called from one `GraphCommandService`, so every change can be undone (wrap multi-step commands in `graph.batchUpdate` / `history.startBatch`).
- **Zones and change detection:** create the graph and attach its events inside `ngZone.runOutsideAngular`. Re-enter with `ngZone.run` only to update signals, open modals, or show toasts.
- **Cleanup:** in `ngOnDestroy`, `graph.off()` every handler you registered (keep references to the same functions), then `graph.dispose()`.
- **Brand look:** node and edge colors, port colors and the selection outline come from CSS tokens. Never hard-code hex values. Node components use the same card style as the rest of the app (radius, shadow, status color stripe, icon, name in the current language).
- **Interaction:** mouse-wheel zoom with Ctrl, panning, a snapline while dragging, Ctrl+Z/Ctrl+Y, Delete key with confirmation, a minimap at the bottom end, and a toolbar with zoom in/out, fit, 100% and auto-layout. Edge tools (vertices, remove button) appear on hover.
- **Read-only mode** (`view/:id`): `interacting: false`, no stencil, no edge tools. Highlight the current step, and show finished steps with the success token.
- **Motion:** new nodes fade and scale in (150 ms). Auto-layout animates nodes to their new positions with `node.transition('position', …)` (300 ms). Honor reduced motion.
- **Persistence:** save `serialization.toDto(graph)` (typed nodes and edges with business data in `cell.data`), never raw `graph.toJSON()`. Store layout positions separately from business data.
- **Validation before save:** run the domain rules, highlight the invalid cells with the danger token, and list the errors in a panel with click-to-focus (`graph.centerCell`).
- **Arabic/English:** labels come from translation keys or from `nameAr`/`nameEn` in `cell.data`, and refresh on language change. Auto-layout direction mirrors in RTL.

## 7. Cross-cutting rules

**Permissions.** Policy strings use `Permissions.{Area}.{Action}` and are identical to the backend constants. The same string is checked in three places: the menu (`requiredPolicy`), the route (`permissionGuard` + `data.requiredPolicy`), and the element (`*abpPermission="'…'"`). `||` and `&&` are supported. Keep the strings in a `permissions.ts` constants file per feature instead of repeating them inline.

**Localization.**
- Every visible string is a translation key, used through the `abpLocalization` pipe in templates or `localizationService.instant()` in code. Keys follow `{Entity}.{Label}`, `General.{Label}`, `Menu.{Item}`, `AdvancedFilters.{Label}`, `Validation.{Rule}`.
- Add each key to **both** `ar.json` and `en.json` in the same commit. Never leave Arabic as an English copy.
- Dates support Gregorian and Hijri through the ng-bootstrap `DateAdapter` and `CustomDatepickerI18n` registered in `app.config.ts`.

**HTTP and errors.**
- All calls go through `RestService`. On failure it calls `HttpErrorReporterService.reportError`, and the theme-shared error handler shows a toast with `error.messages` from the backend error shape `{ error: { code, messages[], source } }`. For 401 it starts login again. For 403 it routes to `/403`.
- Pass `skipHandleError: true` only when the component shows the error itself.
- The OAuth interceptor attaches the token automatically, so never set `Authorization` by hand.

**State.** Use signals for component state and `computed` for derived values. RxJS is for HTTP and streams. Use `toSignal` to bridge them. For state shared across screens in one feature, use a feature-scoped `{entity}.facade.ts` built on signals. No NgRx.

**Performance.**
- `ChangeDetectionStrategy.OnPush` on every new component.
- Lazy-load every route, and use `@defer` for heavy widgets (charts, PDF viewer, map).
- Keep the initial bundle budget at 1.5 MB or less, and check it with `build:stats`.
- Use `@for … track item.id`, never track by index.
- Images get `width`/`height` and `loading="lazy"`. Use `NgOptimizedImage` for content images.

**Security.**
- Never use `[innerHTML]` with server data unless it has gone through Angular's sanitizer. Never call `bypassSecurityTrust*` on user content.
- Download files through `downloadBlob`, or the `auth-img` directive for protected images. Never put tokens in URLs.

**Accessibility.**
- Every interactive element is reachable and usable with the keyboard, with a visible focus ring in the brand color.
- Icon-only buttons get an `aria-label` translation key.
- Modals trap focus and close on Esc.

## 8. Testing
- Each library has Jest set up, with `*.spec.ts` files next to the code.
- Minimum per entity: the proxy builds the right URL, verb and body (`HttpTestingController`); the step validator rejects invalid input; the list renders rows and hides actions without permission; the form is disabled in read-only mode.
- The app generator default `unitTestRunner: none` is fine for app shells only. Feature, proxy and ui libraries always have tests.
- E2E (Playwright): one smoke test per role, in both `ar` and `en`, checking login, the menu filtered by role, list → create → edit → delete, and RTL layout.

## 9. Workflow rules for Claude
1. **New project:** brand kit (§0) → Nx workspace (§2) → `core`, `theme-shared`, `shared/ui-common`, `shared/charts` (ECharts), `shared/graph` (X6, when the product has any diagram) → proxies → features → app shells → `DEPLOYMENT.md` + `HLD.md`.
2. **New entity screen, in this order:** proxy DTOs + service → `{entity}.routes.ts` → list + filter → form wizard + steps → menu entry in `config` → app route → `ar`/`en` keys → permissions constants → tests.
3. **New backend microservice:** a new `shared/{service}-proxy` library, an entry in every app's `app-settings.json` `apis`, and a path alias.
4. Before adding a screen, read one existing screen in the same feature and match it.
5. Use Nx generators (`nx g @nx/angular:component … --project=…`), with standalone, OnPush and SCSS.
6. Run `nx affected -t lint test build` before finishing. Do not leave new lint warnings.
7. Look at every new screen in light and dark, `ar` and `en`, and on mobile width before calling it done.
