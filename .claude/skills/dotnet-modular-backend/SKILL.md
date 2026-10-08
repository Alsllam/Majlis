---
name: dotnet-modular-backend
description: Rules for building or extending a .NET 9 modular backend (DDD layers per module, dynamic controllers from AppServices, YARP BFF, OpenIddict, MassTransit). Use for any backend work in these projects.
---

# .NET Modular Backend Rules

These rules describe the house backend architecture. It comes from an existing reference solution, with its known weaknesses fixed. Apply them whenever you create a new backend or add features to one. If the repo's own `CLAUDE.md` gives product-specific values (names, modules, ports), those win.

Naming placeholders: `{Co}` = company/org prefix, `{Product}` = product name, `{Module}` = business module (e.g. `WaitingList`, `CompoundManagement`).

---

## 1. Tech stack (do not swap without being asked)

| Concern | Choice |
|---|---|
| Runtime | .NET 9, C# with `Nullable` + `ImplicitUsings` enabled |
| Web | ASP.NET Core, controllers generated from AppServices |
| ORM | EF Core 9 + SQL Server (`Microsoft.EntityFrameworkCore.SqlServer`) |
| Auth | OpenIddict server (separate host) + OpenIddict validation in every API |
| Identity | ASP.NET Core Identity with `Guid` keys |
| Validation | FluentValidation (`AbstractValidator<T>`) |
| Mapping | AutoMapper, one `Profile` per module |
| Messaging | MassTransit + RabbitMQ (events between modules) |
| Service-to-service HTTP | Refit clients |
| Background jobs | Hangfire (SQL Server storage) in its own host |
| Caching | `IDistributedCache` (Redis), in-memory fallback |
| Gateway | YARP reverse proxy as BFF (`*.BFF.Host`) |
| Logging | Serilog (console + rolling file, 7-day retention) |
| Localization | JSON resource files `Resources/ar.json`, `Resources/en.json` |
| Docs/UI | Swagger (Swashbuckle), only when environment is Development |
| Tests | xUnit + FakeItEasy + EF Core InMemory + coverlet |
| CI | Azure Pipelines or GitHub Actions: restore → build → test → publish per host |

---

## 2. Solution layout

```
backend/
├── {Co}.{Product}.sln
├── Directory.Build.props            # shared TargetFramework, Nullable, analyzers, package versions
├── .editorconfig
├── Shared/
│   ├── {Co}.Framework.Domain/            # base entities, IRepository, IUnitOfWork, exceptions, events, specs, localization, security
│   ├── {Co}.Framework.Application/       # ApplicationService base, dynamic controllers, middleware, auth, base DTOs, Refit clients, extensions
│   ├── {Co}.Framework.EntityFrameworkCore/ # Repository<T>, UnitOfWork, DefaultEntityTypeConfiguration, value converters, seeding
│   └── {Co}.{Product}.DbMigrator/        # console app: applies migrations + runs seeders
├── Modules/
│   └── {Module}/
│       ├── {Co}.{Product}.{Module}.Domain/
│       ├── {Co}.{Product}.{Module}.Application/
│       ├── {Co}.{Product}.{Module}.EntityFrameworkCore/
│       └── {Co}.{Product}.{Module}.Tests/
├── Hosts/
│   ├── {Co}.{Product}.{Module}.Host/      # one thin web host per deployable module
│   ├── {Co}.{Product}.BFF.Host/           # YARP gateway — the only public entry point
│   ├── {Co}.{Product}.Auth.Host/          # OpenIddict server + login UI
│   └── {Co}.{Product}.Jobs.Host/          # Hangfire server + dashboard
└── tests/                                 # cross-module / framework tests
```

**Dependency direction (enforce it):**
`Domain` ← `EntityFrameworkCore` ← `Application` ← `Host`.
- `Domain` references only `Framework.Domain`.
- A module never references another module's projects. Modules talk through **MassTransit events** or **Refit clients** declared in `Framework.Application/RefitClients/{Module}`.
- Hosts contain no business logic, only composition (`Program.cs`, `appsettings*.json`).

**Spell names correctly.** Use `Management`, `Extensions`, `Definitions`. No placeholder files (`Class1.cs`, `TextFile1.txt`, `WeatherForecast.cs`). Delete them when a template creates them.

---

## 3. Domain layer (`{Module}.Domain`)

```
Entities/            # aggregate roots + child entities of THIS module
Repositories/        # custom repository interfaces (only when generic ones are not enough)
Specifications/      # Specification<T> classes, e.g. ActiveCompoundSpecification
Events/              # integration events (ETOs) this module publishes
Enums/
Constants/           # field lengths, regex patterns, permission names
DomainServices/      # *Manager classes for logic spanning several entities
```

- Entities belong to their module's Domain project. `Framework.Domain` holds only base types and truly cross-cutting entities (Identity, Lookups, Attachments, Notifications).
- Base classes, from smallest to largest: `Entity<TKey>` → `CreationAuditedEntity<TKey>` → `AuditedEntity<TKey>` → `FullAuditedEntity<TKey>` (adds `IsDeleted`, `DeleterId`, `DeletionTime`). Default to `FullAuditedEntity<Guid>` for business data.
- Use `IActivableEntity` (`IsActive`) for anything that can be enabled or disabled.
- Use `ISecuredEntity` for data scoped by ownership (e.g. per compound/branch). The DbContext applies a global query filter so non-admins see only their scope.
- Mark PII properties with `[Sensitive]`. They are encrypted at rest by `SensitiveDataValueConverter`.
- Bilingual display fields come in pairs: `NameAr` / `NameEn`, `CodeAr` / `CodeEn`.
- Shared limits and patterns live in `FieldDefinitions` (e.g. `MaxNameLength = 120`, `NamePattern = @"^[\p{L}\d ]+$"`, mobile pattern). Never hard-code them in validators.
- Status values are enums persisted as `int` (`UnitStatusId = (int)UnitStatus.Reserved`). Keep the previous status when it matters (`LastUnitStatusId`).

## 4. Persistence (`{Module}.EntityFrameworkCore` + `Framework.EntityFrameworkCore`)

- Generic repositories are registered once: `IRepository<T>`, `IRepository<T,TKey>` (write), `IReadOnlyRepository<T>`, `IReadOnlyRepository<T,TKey>` (read, `AsNoTracking`).
- Read through `IReadOnlyRepository` and write through `IRepository`. Never inject `DbContext` into AppServices.
- Write methods take `autoSave`. Pass `true` for single-step operations. For multi-step operations use `IUnitOfWork.BeginTransactionAsync / CommitTransactionAsync / RollbackTransactionAsync` with `autoSave: false`.
- Paging: `GetPagedListAsync(predicate, skipCount, maxResultCount, sorting, ct, includes...)` returns `PagedResultDto<T> { Items, TotalCount }`. Sorting is a dynamic LINQ string; default `"CreationTime Desc"`.
- Complex queries (joins, projections, aggregates) go in a custom read-only repository: interface in `Domain/Repositories/I{Entity}ReadOnlyRepository.cs`, implementation in `EntityFrameworkCore/Repositories/`. Project straight to DTOs with `Select`, not `Include` plus mapping.
- Entity configuration: one `IEntityTypeConfiguration<T>` per entity, inheriting `DefaultEntityTypeConfiguration<T>`. That base sets the `IsActive` default and the `[Sensitive]` converters. Configure max lengths from `FieldDefinitions`, indexes on unique codes, and the soft-delete filter `HasQueryFilter(x => !x.IsDeleted)`.
- `UnitOfWork.SaveChangesAsync` fills audit columns (`CreatorId`, `CreationTime`, `LastModifierId`, `LastModificationTime`, soft delete) from `ICurrentUser`. Never set them by hand.
- **Each module owns its DbContext and its own schema** (`modelBuilder.HasDefaultSchema("{module}")`), plus its own migrations folder. This avoids one giant shared DbContext coupling every module. Cross-module reads go through events, Refit, or read models, never through a shared table.
- Each module exposes one registration method: `Add{Module}EntityFrameworkCoreModule(this IServiceCollection services)`.
- Migrations run only from the DbMigrator. Never call `Database.Migrate()` at host startup.
- Seed data uses `IDataSeeder` classes in `DataSeeding/`. They must be idempotent: check before inserting.

## 5. Application layer (`{Module}.Application`)

### Folder per feature (aggregate)
```
{Feature}/                       # e.g. Buildings/
├── I{Feature}AppService.cs
├── {Feature}AppService.cs
├── DTOs/
│   ├── {Feature}Dto.cs              # details (GetById)
│   ├── {Feature}ListDto.cs          # list row
│   ├── {Feature}ListExcelDto.cs     # export row
│   ├── {Feature}IdDto.cs            # { Guid Id }
│   ├── Create{Feature}Dto.cs
│   ├── Update{Feature}Dto.cs
│   └── Filter{Feature}Dto.cs        # : BaseFilterRequestDto
├── Validations/
│   ├── Create{Feature}Validator.cs
│   └── Update{Feature}Validator.cs
└── Rules/                        # optional: business-rule classes too big for the service
EventHandlers/                    # MassTransit IConsumer<T> classes for this module
{Module}AutoMapperProfile.cs      # split per feature once it passes ~300 lines
{Module}ApplicationModule.cs      # Add{Module}ApplicationModule(services, configuration)
```

### AppServices are the controllers
- An AppService inherits `ApplicationService`, or `ActivableAppService<TEntity,TKey>` when the entity is `IActivableEntity`. That adds `POST activate` and `POST deactivate`.
- The base is `[ApiController]` and `[Authorize]` with the OpenIddict validation scheme. Use `[AllowAnonymous]` only on purpose.
- `services.AddDynamicControllers(assembly)` exposes them. The "AppService" suffix is removed from the route. Set the route explicitly: `[Route("buildings")]` (kebab-case, plural).
- Every public method has an interface entry, XML docs, a `CancellationToken cancellationToken = default` last parameter, and an `Async` suffix.
- Utility methods that must not become endpoints get `[NonActionApi]`.

### Endpoint convention (use `RouteDefinitions` constants)

| Operation | Verb | Route | Body | Returns |
|---|---|---|---|---|
| List (paged + filter) | POST | `list` | `Filter{X}Dto` | `PagedResultDto<{X}ListDto>` |
| Lookup (dropdown) | POST | `lookup` | `Filter{X}Dto` | `PagedResultDto<BaseLookupResponseDto<TKey>>` |
| Get by id | POST | `getbyid` | `{X}IdDto` | `{X}Dto` |
| Create | POST | `""` | `Create{X}Dto` | `Guid` (new id) |
| Update | PUT | `""` | `Update{X}Dto` (includes `Id`) | nothing |
| Delete | DELETE | `""` | `{X}IdDto` | nothing |
| Activate / Deactivate | POST | `activate` / `deactivate` | `EntityIdDto<TKey>` | nothing |
| Export | POST | `export` | `Filter{X}Dto` | `UrlStringBaseResponseDto` (download URL) |
| Import | POST | `import` | file | result summary |
| Sub-resource lookup | POST | `{sub}/lookup` | filter | lookup list |

Reads use POST with a body so filters never land in URLs or logs. This is intentional; do not switch to GET.

### Method body pattern
1. **Authorize:** `[HasPermission(Permissions.{Module}.{Action})]` on every non-public method. Permission names are constants in `Permissions` (`{Module}.View{X}`, `Create{X}`, `Update{X}`, `Delete{X}`, `Export{X}`). Never leave Export or a lookup without a permission unless it is meant to be public.
2. **Validate:** Create/Update DTOs implement `ISkipAutoValidation` (`[JsonIgnore] SkipAutoValidations = true`). In the method set `input.SkipAutoValidations = false`, call the injected `IValidator<T>.ValidateAsync`, and throw `ValidationException(result.Errors)` if it is invalid. (This stops the async DB checks from running twice.)
3. **Load:** `GetAsync(...)`, or `FindAsync(...) ?? throw new EntityNotFoundException(_l["General:Business:NotFound"])`.
4. **Business checks:** throw `CustomValidationException(_l["..."])` with a localized key, e.g. `General:Business:DeleteNotAllowed` when children exist.
5. **Map + persist:** `_mapper.Map<Create{X}Dto, {X}>(input)` then `InsertAsync(entity, autoSave: true, ct)`. For updates, load the entity and change it (or `_mapper.Map(input, entity)`), then `UpdateAsync`.
6. **Publish events** after a successful save: `IEventPublisher.PublishAsync(new {X}CreatedEto(...), ct)`.
7. **Return** a DTO or id. Never return entities.

List filter pattern, with each criterion optional:
```csharp
input.ActiveFilter ??= ActiveFilter.All;
input.Sorting = string.IsNullOrEmpty(input.Sorting) ? "CreationTime Desc" : input.Sorting;
var page = await _readOnlyRepository.GetPagedListAsync(
    x => (input.CompoundId == null || x.CompoundId == input.CompoundId)
      && (string.IsNullOrEmpty(input.FilterText) || x.CodeAr.Contains(input.FilterText) || x.CodeEn.Contains(input.FilterText))
      && (input.ActiveFilter == ActiveFilter.All
          || (input.ActiveFilter == ActiveFilter.Active && x.IsActive)
          || (input.ActiveFilter == ActiveFilter.InActive && !x.IsActive)),
    input.SkipCount, input.MaxResultCount, input.Sorting, cancellationToken,
    x => x.Compound);
return _mapper.Map<PagedResultDto<{X}ListDto>>(page);
```
Export reuses the same private query with `SkipCount = 0` and a capped `MaxResultCount` (e.g. 50 000, not `int.MaxValue`).

### Dependency injection
- Use constructor injection with `private readonly` fields. Do not resolve services from `IServiceProvider` in properties, and do not create new scopes inside a request (`CreateAsyncScope()`).
- Register every AppService in `{Module}ApplicationModule` as `AddScoped<I{X}AppService, {X}AppService>()`. Also register validators (`AddValidatorsFromAssembly`) and the AutoMapper profile there.

### Validators
- `AbstractValidator<T>`, with all rules inside `When(x => !x.SkipAutoValidations, () => { ... })`.
- Messages are localization keys: `General:Fields:Required`, `General:Fields:InvalidCharacters`, `General:Fields:AlreadyExist`, `General:Fields:MaxLength`.
- Check uniqueness with `MustAsync` against `IReadOnlyRepository.AnyAsync(...)`, scoped to the parent where relevant (e.g. code unique per compound). Exclude the current `Id` on update.

### Events (MassTransit)
- Event contracts (`*Event` / `*Eto`) implement `IEvent`. Put them in `Framework.Domain/Events` if several modules consume them, otherwise in the module's `Domain/Events`.
- Consumers: `{EventName}Consumer : IConsumer<TEvent>` in `Application/EventHandlers`. Log with structured templates (`"... {UnitId}"`, not string interpolation), then rethrow so MassTransit retries.
- Consumers must be idempotent, since the same message can arrive twice.
- The host passes its consumers namespace to `AddSharedEntityFrameworkCoreModule(config, "{Co}.{Product}.{Module}.Application.EventHandlers")`.

## 6. Cross-cutting rules

**Errors.** `ExceptionHandlingMiddleware` is the only place exceptions become HTTP responses. Every error has this shape:
```json
{ "error": { "code": "400", "date": "2026-01-01T00:00:00Z", "messages": ["..."], "source": "Validation|Application|Binding|Parsing" } }
```
Map exceptions to status codes: `ValidationException`/`CustomValidationException`/`DotNetValidationException` → 400, `EntityNotFoundException`/`NotFoundException` → 404, `ForbiddenException` → 403, `ServiceUnAvailableException` → 503, `UserFriendlyException` → its `HttpStatusCode`, anything else → 500 with a generic localized message (never a stack trace). Set `SuppressModelStateInvalidFilter = true` so all errors go through the middleware.

**Localization.** Every user-facing string is a key in `ar.json` and `en.json`, added to both files in the same change. Keys follow `Area:Category:Name`. The language comes from the `Accept-Language` header via `LocalizationMiddleware`.

**Security.**
- Permissions are checked by `PermissionHandler`, which reads the user's permissions from the distributed cache (key from `IdentityEntitiesConsts.GetUserPermissionsCacheKey(userId)`) and falls back to the DB.
- CORS uses an explicit origin list from config. Never `SetIsOriginAllowed(_ => true)` together with `AllowCredentials()`.
- The Hangfire dashboard requires an authenticated admin. No anonymous filter outside Development.
- Secrets never go in `appsettings.json`. Use environment variables, user-secrets locally, or a vault. The encrypted-connection-string helper is fine, but its key also comes from the environment.
- Sanitize HTML input with `IHtmlSanitizer`. Validate uploaded files with `ImageFileSecurity` (magic bytes + extension + size).
- Rate limiting: sliding window policy `SlidingPolicy` (config `RateLimiter:Window`, `RateLimiter:PermitLimit`) applied with `MapControllers().RequireRateLimiting("SlidingPolicy")`.
- The BFF adds security headers and response compression (Brotli/Gzip) and is the only internet-facing host.

**Host `Program.cs` order (copy exactly):**
```
services: Add{Module}ApplicationModule → AddControllers → AddFluentValidationAutoValidation → AddCORSExtensions
          → AddApiDefinition → AddLocalizationService → AddSwaggerGen → Add{Product}DbContext
          → AddSharedEntityFrameworkCoreModule(config, consumersNamespace) → AddHttpContextAccessor
          → AddLoggingService → AddOpenIddictExtension → AddHangfireWithSqlServer (if needed)
          → Configure<JsonOptions> → AddDynamicControllers → SuppressModelStateInvalidFilter
          → AddSlidingWindowRateLimiterStrategy → AddHealthChecks().AddCheck("self")
pipeline: UseLocalizationMiddleware → UseLoggingMiddleware → ExceptionHandlingMiddleware
          → UseHttpsRedirection → UseCors → UseAuthentication → UseAuthorization
          → UseStaticFiles → UseRateLimiter → Swagger (Development only)
          → MapHealthChecks("/health").AllowAnonymous() → MapControllers().RequireRateLimiting("SlidingPolicy")
```

**Config.** `appsettings.json` holds non-secret defaults. `appsettings.{Environment}.json` holds overrides. Read options through `IOptions<T>` classes, not `configuration["..."]` strings scattered through code.

**Logging.** Serilog, with structured properties only. Never log tokens, passwords, national IDs, or other `[Sensitive]` fields.

## 7. Testing (`{Module}.Tests`)

```
Application/
├── AppServices/{Feature}/{Feature}AppServiceTests.cs
├── Validations/{Feature}/Create{Feature}ValidatorTests.cs
└── TestInfrastructure/   # Test{Module}DbContext (InMemory), DbContextHelper, builders
TestData/                 # sample files (CopyToOutputDirectory=Always)
```
- xUnit `[Fact]` / `[Theory]`, with Arrange / Act / Assert sections.
- Name tests `{Method}_Should{Expected}_When{Condition}`.
- Fake dependencies with `A.Fake<T>()`. Use EF Core InMemory for repository and validator tests.
- **Every test asserts something.** No test that only calls the method. Check the returned value, `A.CallTo(...).MustHaveHappenedOnceExactly()`, or `Assert.ThrowsAsync<T>`.
- Minimum per feature: create succeeds, create rejects invalid input, get-by-id not found → `EntityNotFoundException`, delete blocked when children exist, list filter works, permission attribute present.

## 8. Workflow rules for Claude

1. Before adding a feature, read one existing feature in the same module and match it.
2. A new feature means, in this order: entity + configuration → migration (via DbMigrator project) → DTOs → validators → AutoMapper maps → interface → AppService → DI registration → permissions constants + seed → localization keys (ar + en) → tests.
3. A new module means: four projects (Domain, Application, EntityFrameworkCore, Tests) + host + YARP route in the BFF `ReverseProxySettings` + pipeline publish step.
4. Run `dotnet build` and `dotnet test` before finishing. Do not leave warnings you introduced.
5. Never edit generated migrations by hand except to fix seed data. Never delete applied migrations.
6. Keep each PR to one feature or fix, on a `feature/{module}-{short-name}` or `fix/...` branch.
