# Implementation Plan: Token and Extrato Proxy Endpoints

**Branch**: `001-token-extrato-proxy` | **Date**: 2026-08-21 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-token-extrato-proxy/spec.md`

## Summary

Build a .NET 10 ASP.NET Core Web API that exposes two JWT-protected endpoints — `Token` and
`Extrato` — proxying Banco Inter's OAuth token issuance and bank-statement retrieval APIs over
mutual TLS. Consumers never handle MTLS or Inter credentials themselves. The `Extrato` endpoint
manages its own internally-cached Banco Inter access token, so it works independently of any
prior `Token` call by the consumer (per spec FR-005). Each endpoint is protected by a distinct,
least-privilege JWT scope. The solution follows a pragmatic Clean Architecture split — Core,
Infrastructure, Api — so the MTLS-bearing Inter client stays isolated behind a testable
abstraction and never leaks into request-handling code.

## Technical Context

**Language/Version**: C# 13 / .NET 10 (per constitution)

**Primary Dependencies**: ASP.NET Core minimal APIs; `Microsoft.AspNetCore.Authentication.JwtBearer`
for inbound JWT validation; `HttpClientFactory` with a named/typed client configured with the
Inter MTLS client certificate for outbound calls; `Microsoft.Extensions.Caching.Memory` for the
short-lived internally-managed Inter access token cache; `Polly` (via
`Microsoft.Extensions.Http.Resilience`) for retry/timeout handling of Inter calls.

**Storage**: N/A — the proxy is stateless with respect to banking data (Token/Extrato payloads
are never persisted). The one exception is the internally-managed Banco Inter access token used
by the Extrato capability, which is held only in an in-process memory cache for its lifetime
(not a durable store).

**Testing**: xUnit for unit and contract tests; `WireMock.Net` (or a hand-rolled
`HttpMessageHandler` stub) to simulate Banco Inter's Token/Extrato APIs in integration tests
without a live MTLS dependency; ASP.NET Core `WebApplicationFactory` for endpoint-level
integration tests exercising JWT authorization.

**Target Platform**: Linux container (Docker), deployed as a standard ASP.NET Core web service.

**Project Type**: Web service — single deployable API, internally split into Core /
Infrastructure / Api projects (see Project Structure below).

**Performance Goals**: Meets spec SC-003 — token/statement responses (or a clear error) returned
within 5 seconds under normal conditions. No high-throughput requirement has been stated; the
proxy is sized for the internal consumer base described in the spec's Assumptions, not
public-internet scale.

**Constraints**: MTLS-only egress to Banco Inter (constitution Principle I); JWT-gated,
least-privilege, endpoint-specific authorization (constitution Principle II); only the Token and
Extrato endpoints may be exposed, no generic passthrough (constitution Principle III); no
plaintext Inter credentials/certificates/JWT signing keys in source control or logs (constitution
Principle IV); structured, correlation-id'd logging that redacts secrets and statement contents
(constitution Principle V).

**Scale/Scope**: Two proxied endpoints, one Banco Inter application/account (per spec
Assumptions). Multi-tenant/multi-account support is explicitly out of scope for this feature.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Gate | Status |
|---|---|---|
| I. MTLS Isolation Boundary | Only the Infrastructure-layer Inter client may hold/use the MTLS client certificate; Core and Api never touch it directly. | PASS — enforced by the Core/Infrastructure/Api split below; certificate is bound via Options in Infrastructure only. |
| II. JWT-Gated, Least-Privilege Endpoints | Both endpoints require JWT auth; Token and Extrato each require a distinct scope (not just "any valid JWT"). | PASS — two distinct authorization policies (`token-issue`, `extrato-read`) planned, one per endpoint. |
| III. Explicit Endpoint Allowlist | Only Token and Extrato are exposed; no generic `{**path}` passthrough. | PASS — Api layer defines exactly two endpoint handlers, each bound to one use case. |
| IV. Secret & Credential Hygiene | Inter credentials/cert/JWT signing keys never committed or logged; sourced from secret config. | PASS — Options pattern bound from User Secrets (dev) / environment or Key Vault (prod); log redaction covered in Api logging middleware design. |
| V. Observability & Traceability | Every proxied request logged with correlation id, caller identity, outcome; no sensitive payloads logged. | PASS — planned via `Microsoft.Extensions.Logging` structured logging + a correlation-id middleware in Api; use cases log outcomes, not payloads. |
| Development Workflow gate | Each endpoint has a spec'd scope, an authorization-rejection test, and an integration test against a mocked Inter API. | PASS — captured as explicit task categories for `/speckit-tasks` (see Complexity Tracking: none needed — no violations to justify). |

No violations identified. Complexity Tracking table below is not needed.

## Project Structure

### Documentation (this feature)

```text
specs/001-token-extrato-proxy/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md         # Phase 1 output (/speckit-plan command)
├── quickstart.md         # Phase 1 output (/speckit-plan command)
├── contracts/             # Phase 1 output (/speckit-plan command)
│   ├── token.openapi.yaml
│   └── extrato.openapi.yaml
└── tasks.md              # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
src/
├── ValinorInterApiProxy.Api/                  # Presentation + composition root
│   ├── Endpoints/
│   │   ├── TokenEndpoint.cs                   # POST /token — requires "token-issue" policy
│   │   └── ExtratoEndpoint.cs                 # GET  /extrato — requires "extrato-read" policy
│   ├── Contracts/                             # HTTP request/response DTOs (mapped from Core DTOs)
│   ├── Authorization/                         # Policy/scope definitions (token-issue, extrato-read)
│   ├── Middleware/                            # Correlation-id + redacting request logging
│   └── Program.cs                             # DI wiring: registers Infrastructure impls behind Core ports
│
├── ValinorInterApiProxy.Core/                 # Domain + Use Cases (merged; see Structure Decision)
│   ├── Models/
│   │   ├── InterAccessToken.cs                # value + expiry
│   │   └── BankStatement.cs / StatementEntry.cs
│   ├── UseCases/
│   │   ├── IssueTokenUseCase.cs               # orchestrates IInterTokenClient
│   │   └── GetStatementUseCase.cs             # orchestrates IInterAccessTokenProvider + IInterStatementClient
│   └── Ports/                                 # interfaces implemented by Infrastructure
│       ├── IInterTokenClient.cs
│       ├── IInterStatementClient.cs
│       └── IInterAccessTokenProvider.cs       # internal cache/refresh abstraction (not consumer-facing)
│
└── ValinorInterApiProxy.Infrastructure/        # Frameworks & Drivers
    ├── InterApi/
    │   ├── InterHttpClient.cs                 # implements IInterTokenClient + IInterStatementClient
    │   │                                       #   (maps transacoes[] -> StatementEntry, sends
    │   │                                       #   x-conta-corrente header, parses valor -> decimal)
    │   └── InterOptions.cs                    # bound from secret config: ClientId, ClientSecret,
    │                                           #   CertificatePath/Password, Scope, ContaCorrente
    └── Caching/
        └── MemoryCacheInterAccessTokenProvider.cs  # implements IInterAccessTokenProvider

tests/
├── ValinorInterApiProxy.Core.Tests/            # unit tests for use cases (Inter ports mocked)
├── ValinorInterApiProxy.Infrastructure.Tests/  # tests InterHttpClient against WireMock.Net (no real MTLS)
└── ValinorInterApiProxy.Api.Tests/             # WebApplicationFactory integration tests:
                                                 #   JWT/scope rejection, success paths, upstream-error mapping
```

**Structure Decision**: Three projects instead of a full four-layer split. `Domain` and
`Application` are merged into a single **Core** project because, per the clean-architecture
guidance to match layering to project scale, this feature's entities (`InterAccessToken`,
`BankStatement`) carry no business rules beyond simple shape/validation — a standalone Entities
project would be near-empty and add indirection without benefit. The Dependency Rule is still
enforced: **Api → Core** and **Infrastructure → Core**, while **Core depends on neither**. The
MTLS-bearing `InterHttpClient` lives exclusively in Infrastructure behind the `IInterTokenClient`
/ `IInterStatementClient` ports (constitution Principle I); Api's `Program.cs` is the only place
allowed to reference Infrastructure concretely, purely for DI registration — endpoint handlers
and use cases depend only on Core's ports. This satisfies the constitution's requirement for "a
dedicated, testable Inter API client abstraction" without a 4th, effectively-empty project.

## Complexity Tracking

> No Constitution Check violations were identified; this table is intentionally empty.
