# Clean Architecture Dependency Rule Checklist: Token and Extrato Proxy Endpoints

**Purpose**: Track follow-up actions from the Clean Architecture dependency-rule review, to be
verified as each phase of `tasks.md` lands real code in Core and Infrastructure.
**Created**: 2026-08-21
**Feature**: [spec.md](../spec.md) | [plan.md](../plan.md)

## Structural Dependency Rule (verified 2026-08-21, Phase 1 scaffolding)

- [x] `ValinorInterApiProxy.Core` has zero `ProjectReference`s (innermost layer)
- [x] `ValinorInterApiProxy.Infrastructure` references only `Core`
- [x] `ValinorInterApiProxy.Api` references `Core` and `Infrastructure` only (composition root)
- [x] No circular project references exist
- [x] Each test project references only its own SUT project (`Core.Tests` → `Core`,
      `Infrastructure.Tests` → `Infrastructure`, `Api.Tests` → `Api`)

## To Do — Before/During Phase 2 (Foundational)

- [x] Remove the `dotnet new webapi` template leftovers from `Program.cs` — the `/weatherforecast`
      minimal API and `WeatherForecast` record — before or during T012 (composition root wiring),
      so no endpoint outside the `/token`/`/extrato` allowlist ever ships (constitution
      Principle III). *(Verified 2026-08-21: removed as part of T012's Program.cs rewrite.)*
- [x] When implementing T004 (Core ports) and T005 (Core models), confirm `Core` adds no package
      or project reference to any HTTP/MTLS/framework type (e.g. no `Microsoft.Extensions.Http`,
      no `System.Net.Http.HttpClient` in method signatures) — ports must be expressed in
      Core-owned types only. *(Verified 2026-08-21: `Core.csproj` has zero `ProjectReference`s;
      only `using`s in `Core/**/*.cs` are `ValinorInterApiProxy.Core.Models`.)*
- [x] When implementing T007 (`InterHttpClient`) and T017
      (`MemoryCacheInterAccessTokenProvider`), confirm `Infrastructure` implements Core's ports
      (`IInterTokenClient`, `IInterStatementClient`, `IInterAccessTokenProvider`) rather than
      Core referencing Infrastructure types — this is the DIP crossing-boundary check.
      *(Verified 2026-08-24: `InterHttpClient` implements `IInterTokenClient`/
      `IInterStatementClient`; `MemoryCacheInterAccessTokenProvider` implements
      `IInterAccessTokenProvider`. `Core.csproj` still has zero `ProjectReference`s.)*
- [x] When implementing T012 (composition root), confirm `Program.cs` is the *only* place in `Api`
      that references Infrastructure concrete types (e.g. `InterHttpClient`,
      `MemoryCacheInterAccessTokenProvider`); endpoint handlers and use cases should depend only
      on Core's ports. *(Verified 2026-08-21: `Program.cs` is the only Api file referencing
      `InterHttpClient`; it's registered behind `IInterTokenClient`/`IInterStatementClient`.)*

## To Do — Before Phase 5 (Polish) Sign-off

- [x] Re-run this checklist's structural checks (`ProjectReference` inspection) against the final
      `.csproj` files to confirm no dependency-rule drift was introduced during US1/US2
      implementation. *(Verified 2026-08-24: `Core.csproj` — zero `ProjectReference`s.
      `Infrastructure.csproj` — references only `Core`. `Api.csproj` — references `Core` and
      `Infrastructure` only. `Program.cs` is the only `Api` file referencing
      `ValinorInterApiProxy.Infrastructure`. Each test project references only its own SUT
      project. No drift.)*
- [x] Spot-check `using` statements in `src/ValinorInterApiProxy.Core/**/*.cs` for any
      Infrastructure- or framework-originating namespace that would indicate an inward-pointing
      violation. *(Verified 2026-08-24: every `using` across `Core/**/*.cs` resolves to
      `ValinorInterApiProxy.Core.{Exceptions,Models,Ports}` — no framework or Infrastructure
      namespace present.)*
- [x] Confirm no DTO/data crossing a use-case boundary is an Entity or Infrastructure-specific
      type (e.g. Inter's raw `transacoes[]` JSON shape) — only Core models (`BankStatement`,
      `StatementEntry`, `InterAccessToken`) should cross into `Api`. *(Verified 2026-08-24:
      `ExtratoEndpoint`/`TokenEndpoint` only ever receive `BankStatement`/`InterAccessToken` from
      their use cases, immediately mapped to `StatementResponse`/`TokenResponse` Api DTOs.
      `InterHttpClient`'s raw wire-format types (`ExtratoResponse`, `ExtratoTransacao`,
      `TokenResponse`) are `private` nested classes — they never leave `InterHttpClient.cs`.)*

## Notes

- This checklist supplements `checklists/requirements.md`; it does not gate `/speckit-clarify` or
  `/speckit-plan`, but should be reviewed at each phase checkpoint in `tasks.md` and again during
  T033 (Constitution Check re-walk).
