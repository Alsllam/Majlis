# 0008 — Backend library choices: licences, mapping, Refit and the test runner

**Status:** Accepted · 2026-10-09 (to be confirmed by the product owner)

## Context
Building the first backend code on .NET 10 (ADR-0007) showed that several libraries named by the backend skill changed:

- **MassTransit 9** is commercial (licence at massient.com). **8.5.x** remains Apache-2.0 and runs on .NET 10.
- **AutoMapper 15+** is commercial. **14.0.0**, the last MIT version, has a known high-severity vulnerability (GHSA-rvv3-g6hj-g44x); NuGet audit fails the build on it.
- **Refit 16** generates clients at compile time; the runtime (reflection) builder is now a separate opt-in package.
- **.NET 10** runs `dotnet test` on Microsoft.Testing.Platform; xUnit v3 is built for it, the VSTest adapter and coverlet's collector are not.

## Decision
1. **Messaging:** ~~MassTransit 8.5.x (Apache-2.0), pinned~~ **Superseded by ADR-0009 (Wolverine, 2026-10-10).** Business code depended only on `IEventPublisher`, which is what made the swap a small change.
2. **Mapping:** no AutoMapper. Each module has explicit `ToDto()` extension methods (`{Module}Mappings.cs`). Compile-time checked, no reflection, no licence.
3. **HTTP clients:** Refit with generated clients (`AddRefitGeneratedClient<T>()`).
4. **Validation:** FluentValidation validators are called explicitly once per command (`ApplicationService.ValidateAsync`); no automatic MVC validation, so async database rules never run twice. Same effect as the skill's `ISkipAutoValidation` pattern, with less code.
5. **Tests:** xUnit v3 on Microsoft.Testing.Platform (`global.json` → `"test": { "runner": "Microsoft.Testing.Platform" }`), FakeItEasy, EF Core InMemory, `Microsoft.Testing.Extensions.CodeCoverage` for coverage, NetArchTest for architecture rules.
6. **`[NonActionApi]`:** ASP.NET's `NonActionAttribute` is sealed, so AppService helpers use `[NonAction]` directly.

## Consequences
- The backend skill's AutoMapper and auto-validation rules are superseded by `backend/CLAUDE.md`.
- A licence decision on MassTransit is needed within the MassTransit 8 support window; it is a dependency swap, not a redesign.
