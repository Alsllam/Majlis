---
name: flutter-clean-mobile
description: Rules for building or extending a Flutter mobile app with feature-first Clean Architecture (data/domain/presentation), BLoC, get_it, Dio + Retrofit, offline-first caching and background sync, Firebase, Arabic/English RTL, AI features through the backend AI service, and the project brand kit. Use for any mobile work in these projects.
---

# Flutter Mobile Rules

These rules describe the house mobile architecture. They come from a reference app: an inspection field app for inspectors working offline, with task lists, surveys, attachments, maps, push notifications and an AI assistant. Its proven parts are kept and its weaknesses are fixed here.

They work together with `dotnet-modular-backend` (API, permissions, error shape) and `angular-nx-frontend` (brand kit, colors, typography, motion). The mobile app uses **the same brand kit** as the web app.

Placeholders: `{app}` = Dart package name (snake_case), `{feature}` = feature folder (snake_case), `{Entity}` = PascalCase.

---

## 0. Brand kit and look (every new app)

- Reuse the product brand kit from the frontend skill §0: logo, palette, typography and motion tokens. Do not invent a second identity.
- **App icon:** `flutter_launcher_icons`, built from the logo mark on a brand background. Android gets an adaptive icon (foreground + background layers) plus a monochrome themed icon. Remove alpha for iOS.
- **Splash:** `flutter_native_splash`, with the logo mark centered on the brand color and separate light and dark versions. After the native splash, the in-app splash animates the logo once (scale and fade, 600–800 ms) while startup tasks run.
- **Theme:** Material 3. Build `ColorScheme.fromSeed` from the brand primary, then override it with the exact token values. Add **light and dark** `ThemeData`. Put app-specific tokens (status colors, surfaces, radii, shadows, spacing, motion durations) in a `ThemeExtension<AppTokens>`. Widgets read `context.tokens.x` and never use raw `Color(0x…)`.
- **Fonts:** an Arabic and Latin pair bundled in `assets/fonts`, e.g. *Noto Kufi Arabic* + *Cairo*, or *IBM Plex Sans Arabic* + *IBM Plex Sans*. Choose the font family by locale in the theme.
- **Sizes:** `flutter_screenutil` (design size set once in `App`), with spacing on an 8-pt scale (`AppSpacing.s8/s16/s24/s32`) and radii from tokens.
- **Motion** (durations from the token table in the frontend skill §0.4):
  - screen transitions are a shared-axis or fade-through page transition set in `pageTransitionsTheme`;
  - lists animate items in with a stagger of up to 8 items, 30 ms apart;
  - list item → detail uses a `Hero` on the card or avatar;
  - buttons scale to 0.97 on press;
  - pull-to-refresh uses a custom indicator in the brand color;
  - KPI counters count up once (`AnimatedCounter`);
  - skeleton placeholders (`skeletonizer`) show while loading, never a bare spinner;
  - empty and error states use an illustration plus an action button (Lottie or SVG in brand colors).
  - Respect `MediaQuery.disableAnimations` and reduce motion when it is set.
- **Accessibility and RTL:** use `EdgeInsetsDirectional`, `AlignmentDirectional` and `PositionedDirectional`, never left/right. Directional icons flip in RTL. Tap targets are at least 48×48. Every icon button has a `Semantics`/`tooltip` label. Text scaling up to 1.3× must not break layouts.
- Check every new screen in light and dark, `ar` and `en`, on a small phone (360×640) and a large one, and on a tablet when the app supports tablets.

---

## 1. Tech stack (do not swap without being asked)

| Concern | Choice |
|---|---|
| SDK | Flutter stable, pinned with **FVM** (`.fvmrc`). Dart 3 with `sealed`, records and patterns |
| Architecture | Feature-first Clean Architecture: `data` → `domain` ← `presentation` |
| State | `flutter_bloc` (Bloc for flows, Cubit for simple screens) + `equatable` or `freezed` states |
| DI | `get_it` (+ `injectable` for generated registration in big apps) |
| HTTP | `dio` + `retrofit` (generated clients) + `json_serializable` |
| Results | `Either<Failure, T>` (`dartz` or `fpdart`) returned from every use case |
| Routing | `go_router` with typed routes and an auth `redirect` |
| Local DB / cache | `sembast` for structured offline data, `hive` for key-value and vector data, `flutter_cache_manager` for files |
| Secure storage | `flutter_secure_storage` for tokens and secrets. `shared_preferences` only for non-sensitive settings |
| Background work | `background_fetch` (headless) for the offline sync queue |
| Firebase | `firebase_core`, `firebase_messaging` + `flutter_local_notifications`, `firebase_crashlytics`, `firebase_analytics`, `firebase_remote_config` |
| Localization | `flutter_localizations` + **gen_l10n ARB** (`lib/l10n/app_{ar,en}.arb`). One localization system only |
| Assets | `flutter_gen` (`Assets.svg.x`, `Assets.png.x`), `flutter_svg` |
| Lists | `infinite_scroll_pagination` + one pull-to-refresh package (`custom_refresh_indicator`) |
| Maps / location | `google_maps_flutter`, `geolocator`, `permission_handler` |
| Media | `image_picker`, `file_picker`, `video_player`, `photo_view`, `record` (audio), `ffmpeg_kit_flutter_new` for compression |
| Documents | `syncfusion_flutter_pdfviewer`, `flutter_inappwebview` (one webview package) |
| Charts | `fl_chart`, styled from the same palette and series order as the web ECharts theme (`AppChartTheme`) |
| Logging | `logger` through one `LogManager`, with `pretty_dio_logger` in debug only |
| Lints | `very_good_analysis` (or `flutter_lints` + strict rules). Never silence a rule globally |
| Tests | `flutter_test`, `bloc_test`, `mocktail`, golden tests (`alchemist`), `integration_test` |

**One package per job.** Never add a second package for something already covered: no `provider` next to bloc, no `easy_localization` next to gen_l10n, no second pull-to-refresh, webview or shimmer package.

---

## 2. Project layout (feature-first)

```
{app}/
├── .fvmrc                        # pinned Flutter version
├── analysis_options.yaml
├── l10n.yaml                     # arb-dir: lib/l10n, output-dir: lib/l10n/generated
├── dart_defines/                 # dev.json, staging.json, uat.json, prod.json — NO secrets, committed
├── flutter_launcher_icons.yaml   flutter_native_splash.yaml
├── assets/  fonts/ svg/ png/ lottie/ icon/
├── lib/
│   ├── main_dev.dart main_staging.dart main_uat.dart main_prod.dart   # call bootstrap(Flavor.x)
│   ├── bootstrap.dart            # ordered startup (see §3)
│   ├── app/
│   │   ├── app.dart              # MaterialApp.router, theme, locale, BlocProviders for global blocs
│   │   ├── di/injection.dart     # registerCore(), then each feature's register{Feature}()
│   │   ├── env/env.dart          # Flavor + URLs from --dart-define-from-file, Remote Config overrides
│   │   ├── router/app_router.dart  routes.dart (typed)  # go_router
│   │   └── theme/                # app_theme.dart, app_tokens.dart (ThemeExtension), app_chart_theme.dart
│   ├── core/
│   │   ├── network/              # dio_factory.dart, auth_interceptor.dart, language_interceptor.dart, error_mapper.dart
│   │   ├── error/                # failure.dart (sealed), exceptions.dart
│   │   ├── storage/              # secure_storage.dart, prefs.dart, sembast_db.dart, hive_boxes.dart
│   │   ├── cache/                # cache_policy.dart, generic_cache.dart
│   │   ├── sync/                 # sync_queue.dart, background_sync_service.dart
│   │   ├── connectivity/         # network_info.dart (stream)
│   │   ├── notifications/        # push_service.dart, local_notifications.dart, deep_link_handler.dart
│   │   ├── usecase/usecase.dart  # base UseCase<T, P>
│   │   └── widgets/              # app_button, app_text_field, app_dropdown, app_snackbar, skeletons, empty_state, error_state, paged_list_view
│   ├── features/
│   │   └── {feature}/
│   │       ├── data/
│   │       │   ├── datasources/  {feature}_api.dart (Retrofit)  {feature}_local_data_source.dart
│   │       │   ├── dtos/         {entity}_dto.dart (+ .g.dart)
│   │       │   ├── mappers/      {entity}_mapper.dart  (extension Dto.toDomain())
│   │       │   └── repositories/ {feature}_repository_impl.dart
│   │       ├── domain/
│   │       │   ├── entities/     {entity}.dart (pure Dart, no json, no flutter)
│   │       │   ├── repositories/ {feature}_repository.dart (abstract)
│   │       │   └── usecases/     get_{entities}.dart, assign_{entity}.dart …
│   │       ├── presentation/
│   │       │   ├── bloc/         {feature}_bloc.dart, _event.dart, _state.dart
│   │       │   ├── pages/        {feature}_page.dart
│   │       │   └── widgets/
│   │       └── {feature}_injection.dart   # registers this feature's api, datasources, repo, use cases, blocs
│   └── l10n/  app_ar.arb  app_en.arb  generated/
└── test/  (mirrors lib/)  integration_test/
```

**Dependency rule.** Enforce it in review and with an import lint such as `import_lint` or `dart_code_linter`:
- `domain` imports **nothing** from `data`, `presentation`, Flutter or packages other than `dartz`/`equatable`. Repository interfaces live in `domain/repositories`.
- `data` imports `domain` (it implements the interfaces and maps DTOs to entities).
- `presentation` imports `domain` only, never DTOs or APIs.
- A feature never imports another feature's `data` or `presentation`. Shared pieces move to `core/`, and a cross-feature call goes through the other feature's domain use case, registered in DI.

---

## 3. Startup, flavors and configuration

- One entry file per flavor (`main_{flavor}.dart`) calls `bootstrap(Flavor.x)`. Run and build with `fvm flutter run --flavor dev -t lib/main_dev.dart --dart-define-from-file=dart_defines/dev.json`.
- `dart_defines/*.json` holds **only public values** (base URLs, auth URL, client id, feature flags). Anything in a dart-define, Remote Config or assets can be pulled out of the APK/IPA, so **no API keys or secrets ever go in them**.
- `bootstrap()` order:
  1. `WidgetsFlutterBinding.ensureInitialized()`
  2. Firebase
  3. Crashlytics handlers (`FlutterError.onError`, `PlatformDispatcher.instance.onError`)
  4. Remote Config fetch, with a timeout and defaults
  5. storage (Hive, Sembast)
  6. DI
  7. localization
  8. push notifications
  9. background sync
  10. `runApp(App())` inside `runZonedGuarded`

  Keep the critical path short. Defer non-essential work until after the first frame.
- `Env` gives the base URLs per flavor. Remote Config may override them. A dart-define override has the highest priority (for QA).
- Feature flags: `FeatureFlags` constants for code-level switches, and Remote Config booleans for remote kill switches. Every flag has a comment naming the backend endpoint or condition it waits for.

---

## 4. Networking

- **One `Dio` per backend host** (API gateway, auth), built by `DioFactory` and registered as a named singleton in get_it. Timeouts come from config.
- **Retrofit clients** per feature (`@RestApi()` + `@POST('/api/{service}/{entities}/list')`), mirroring the backend endpoint table: `list`, `getbyid`, create (POST), update (PUT), delete, `activate`/`deactivate`, `lookup`. They return DTOs.
- **Interceptors**, in this order:
  - `LanguageInterceptor`: sets `Accept-Language` from the current locale, plus a timezone offset header.
  - `AuthInterceptor` (a `QueuedInterceptor`):
    - reads the access token from **secure storage on every request**, never once at Dio creation;
    - on 401, refreshes the token **once** with a separate bare Dio. Concurrent 401s wait for that same refresh;
    - saves the new tokens to secure storage and retries the original request;
    - if the refresh fails, clears the session and sends an `AuthEvent.loggedOut` through the auth bloc. It never navigates from the interceptor.
  - `PrettyDioLogger` in debug builds only. Never log `Authorization` headers or bodies that contain personal data.
- **Errors:** `ErrorMapper` turns a `DioException` or the backend error body (`{ error: { code, messages[], source } }`) into a typed `Failure`:
  ```dart
  sealed class Failure { final String message; const Failure(this.message); }
  final class NetworkFailure extends Failure …      // no connection / timeout
  final class UnauthorizedFailure extends Failure … // 401 after refresh failed
  final class ForbiddenFailure extends Failure …    // 403
  final class NotFoundFailure extends Failure …     // 404
  final class ValidationFailure extends Failure { final List<String> messages; … } // 400
  final class ServerFailure extends Failure …       // 5xx
  final class CacheFailure extends Failure …
  ```
  Messages are already localized by the backend (via `Accept-Language`). Fallback texts come from ARB keys.
- **Crashlytics:** record 5xx and unexpected exceptions as **non-fatal**, with the path and status code but no body or token. Never record 4xx errors or connectivity errors, and never mark HTTP errors as `fatal: true`.
- **Uploads:** multipart upload with progress (`onSendProgress`), compression before upload (images to ≤ 1920 px and quality 80, video through ffmpeg), and resumable retries through the sync queue.

## 5. Domain and data

- **Entities** are immutable (`final` fields, `const` constructors, `copyWith`). Use `freezed` when an entity has more than ~6 fields or unions.
- **DTOs** are `@JsonSerializable()` and match the backend names. A mapper extension `toDomain()` turns DTO → entity, with all null-handling and defaults inside it. Bilingual pairs (`nameAr`/`nameEn`) become a `LocalizedText` value object with `.of(locale)`.
- **Use cases** have one public `call()` each and return `Future<Either<Failure, T>>` (or a `Stream` for live data). They contain business rules only: no `BuildContext`, no navigation, no `SharedPreferences`, no static flags.
- **Repositories** decide between remote, cache and offline using an explicit `CachePolicy` (`networkFirst`, `cacheFirst`, `cacheOnly`, `networkOnly`) passed by the use case. The "last updated" time is returned in the result (`Paged<T>{ items, totalCount, fetchedAt, fromCache }`), not kept in global prefs.
- **Pagination:** `skipCount`/`maxResultCount` matching the backend. The page size comes from one constant.

## 6. Offline-first and background sync

The reference app's biggest strength. Keep it in every field app:
- **Read path:** a list or detail request goes through `GenericCache` (Sembast store per entity, key = endpoint + filter hash, TTL from config). When offline, serve the cache and show a "last updated {time}" banner. When online, refresh in the background and update the UI.
- **Write path:** every mutation that must survive being offline (submit survey, upload attachment, complete task) becomes a `SyncJob { id, type, payload, attempts, status, createdAt }` in the Sembast `sync_queue`. It runs immediately when online, otherwise later.
- **Sync engine** (`core/sync`):
  - triggered when connectivity comes back, when the app resumes, and by `background_fetch` (headless, minimum interval 15 min, `requiresNetworkConnectivity`);
  - processes jobs in creation order per entity, with exponential backoff and a maximum attempt count;
  - is idempotent: each job sends a client-generated id (UUID) so the backend can ignore duplicates;
  - after success, invalidates the related cache keys through an event bus (`CacheInvalidated(keys)`) that blocs listen to. **No static `isForceRefresh` flags.**
- **UI:** a sync status chip in the app bar (synced / pending N / failed), a "pending sync" list with retry, and an offline banner when `NetworkInfo.onStatusChange` reports offline.

## 7. Presentation (BLoC)

- **One bloc per screen or flow.** It is created by the page through `BlocProvider(create: (_) => getIt<XBloc>()..add(XStarted()))`, and all dependencies arrive through its **constructor**. Never call `getIt` inside a bloc.
- **Events and states** are `sealed` classes, or `freezed` unions. A list state looks like:
  ```dart
  enum Status { initial, loading, success, failure }
  final class TasksState extends Equatable {
    final Status status; final List<Task> items; final bool hasReachedEnd;
    final DateTime? fetchedAt; final bool fromCache; final Failure? failure;
    final UiEffect? effect;   // one-shot: ShowMessage, NavigateTo — consumed by BlocListener
    …
  }
  ```
- **Blocs never touch the UI.** No `BuildContext`, no global `navigatorKey.currentContext`, no `Navigator`, no snackbars, no `AppLocalizations` inside a bloc. They emit state or a one-shot `UiEffect`. The page's `BlocListener` shows the snackbar or navigates with `context.go(...)`.
- Use event transformers for search/filter (`debounce(300ms)` + `restartable()`) and for submit (`droppable()`) to avoid double taps.
- Paged lists: one shared `PagedListView<T>` widget that wraps `infinite_scroll_pagination` with skeleton first-page loading, empty, error-with-retry and end-of-list states, pull-to-refresh, and staggered item animation. Feature pages give it only an item builder and the bloc.
- **Pages** are thin: Scaffold, app bar, `BlocConsumer`, and widgets. Split any widget over ~150 lines into `widgets/`. Use `const` constructors everywhere they are possible.
- **Forms:** use `Form` + `AppTextField` validators that match backend `FieldDefinitions` (lengths, patterns, Saudi mobile pattern `^05[503649187]\d{7}$`). The submit button is disabled while the bloc reports `submitting`.
- **Permissions:** the user's granted permission list (from login/profile) lives in `AuthBloc`. Use the `PermissionGate(permission: 'Permissions.Tasks.Assign')` widget and the go_router `redirect` to check it, with the same policy strings as the backend.

## 8. Routing

- `go_router` with a `ShellRoute` for the bottom-nav or drawer shell. Typed routes (`go_router_builder`) or a `Routes` constants class with path params. No `switch`-based `onGenerateRoute`.
- `redirect` handles the auth state (splash → login → home), forced update (version check against Remote Config `min_supported_version`), and permission guards.
- Deep links from push notifications are parsed in `DeepLinkHandler`, which turns them into a route and calls `router.go(...)` after the app is ready.

## 9. Push notifications

- FCM token registration after login, refreshed on `onTokenRefresh`, and removed on logout.
- Foreground messages show a local notification. Taps (foreground, background, terminated) go through `DeepLinkHandler`.
- The background handler is a top-level `@pragma('vm:entry-point')` function that only writes a small marker such as an unread flag. No heavy work there.
- Android notification channels are declared once. On iOS, ask for permission at a meaningful moment, not at first launch.

## 10. AI features (assistant, voice, RAG)

The reference app's assistant (chat, voice, tool calling, retrieval over local questions) is a pattern to keep. **But the app never calls OpenAI, Azure OpenAI or Gemini directly, and never holds an AI key.** Keys in dart-defines or Remote Config can be extracted from the app by anyone who installs it.
- All model calls go to the **Python AI service** through the gateway (`POST /ai/chat`, streamed with SSE; `POST /ai/transcribe`; `POST /ai/embed`), authenticated with the user's normal access token. The service holds the provider keys, applies rate limits per user, and logs usage.
- **Tool calling:** the AI service returns tool calls. The app runs them through a `ToolRegistry` (`Map<String, AiTool>`) where each tool is a domain use case (`get_closest_task`, `assign_task`, `open_support_ticket`, …). Each tool declares its JSON schema, checks its arguments, checks the user's permission, and **asks the user to confirm any action that changes data** before running it. Unknown tools are ignored and logged.
- **RAG:** large or shared knowledge is indexed and searched server-side. On-device retrieval (Hive vector store) is only for small offline reference sets, with embeddings precomputed by the service and shipped or synced, never computed with a key on the device.
- **Voice:** record with `record` (AAC/m4a, 16 kHz mono), compress, and upload to `/ai/transcribe`. Show live recording time and level, and allow cancel.
- **UI:** streaming assistant bubbles with a typing shimmer, quick-action chips, chat history stored locally (Hive) and clearable by the user, an RTL-correct markdown renderer, and a clear "AI-generated" label. Turn the whole feature on or off with a Remote Config flag.

## 11. Security
- Tokens and secrets live **only** in `flutter_secure_storage` (Keychain/Keystore), never in SharedPreferences. On logout, clear secure storage, caches that hold personal data, the FCM registration, and the chat history.
- Release builds use `--obfuscate --split-debug-info=build/symbols`, and the symbols are uploaded to Crashlytics.
- Android: `usesCleartextTraffic=false` and a network security config. Add certificate pinning when the client requires it.
- Never log personal data (national ID, phone, location) or tokens. Mask them in analytics events.
- Ask for location, camera, microphone and storage permissions only when the feature needs them, with an explanation first and a path to settings if the user denied them.

## 12. Localization
- ARB files `app_en.arb` (template) and `app_ar.arb`. Every key is added to both in the same commit. Use ICU plurals and selects for counts and gender. Run `flutter gen-l10n` and keep `untranslated-messages-file` empty.
- Read strings with `context.l10n.key` (an extension on `AppLocalizations.of(context)`). No hard-coded user-facing text in widgets.
- The locale is stored in prefs and applied through an app-level `LocaleCubit`. Do not restart the app (`Phoenix`/`restart_app`) to change the language.
- Dates show Gregorian and Hijri where the domain needs both. Number digits follow the locale.

## 13. Testing and quality
- **Unit tests:** every use case (success, each failure type), every mapper, `ErrorMapper`, the `AuthInterceptor` refresh logic (including concurrent 401s), and `SyncQueue` (ordering, retry, idempotency).
- **Bloc tests** (`bloc_test`): every event, including the failure and offline paths.
- **Widget and golden tests:** the shared widgets (`PagedListView`, empty and error states, buttons) in light/dark × ar/en.
- **Integration test:** login → list → detail → an offline action → reconnect → synced.
- CI runs `fvm flutter analyze` (zero warnings), `dart format --set-exit-if-changed`, `flutter test --coverage` (≥ 70% on domain and data), and builds every flavor.
- The default `widget_test.dart` counter test is deleted on day one.

## 14. Workflow rules for Claude
1. **New app:** brand kit (§0) → `fvm use stable` → flavors and dart_defines → `core/` (network, error, storage, sync, widgets, theme) → auth feature → features → CI.
2. **New feature, in this order:** domain entity + repository interface + use cases → DTOs + Retrofit API + mapper → repository impl (with cache policy) → `{feature}_injection.dart` → bloc (events/states) → page + widgets → route → ARB keys (ar + en) → tests.
3. **New offline mutation:** add a `SyncJob` type + handler + cache keys to invalidate, plus a test for retry and duplicate delivery.
4. Before writing a feature, read one existing feature and match it.
5. Run `build_runner build --delete-conflicting-outputs` after changing DTOs, APIs or freezed classes, and commit the generated files only if the repo already does.
6. Before calling a screen done, run it in light/dark and ar/en, offline and online, on a small and a large device.
