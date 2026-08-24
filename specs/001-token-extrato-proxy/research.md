# Phase 0 Research: Token and Extrato Proxy Endpoints

All Technical Context items had reasonable defaults; no unresolved `NEEDS CLARIFICATION` markers
remain. This document records the rationale behind each non-obvious technology/design decision.

## 1. Layering: Core/Infrastructure/Api (3 projects) vs. full 4-layer Clean Architecture

- **Decision**: Merge Domain (Entities) and Application (Use Cases) into a single `Core` project;
  keep `Infrastructure` and `Api` separate.
- **Rationale**: The feature's entities (`InterAccessToken`, `BankStatement`) have no business
  rules beyond basic shape/validation. A standalone Entities project would contain almost nothing
  and add indirection without a corresponding benefit. The Dependency Rule (inward-only
  dependencies) is preserved regardless of project count: Api and Infrastructure both depend on
  Core; Core depends on neither.
- **Alternatives considered**: Full 4-project split (Domain, Application, Infrastructure, Api) —
  rejected as over-engineering for a 2-endpoint proxy with trivial entities. Single project with
  folder-only separation — rejected because the constitution explicitly requires the Inter client
  to be a swappable, testable abstraction, which is easiest to enforce with a project (assembly)
  boundary that prevents Api/Core from accidentally referencing `HttpClient`/certificate types
  directly.

## 2. Inbound authentication: JWT Bearer middleware + per-endpoint authorization policies

- **Decision**: `Microsoft.AspNetCore.Authentication.JwtBearer` for token validation
  (signature/issuer/audience/expiry), with two distinct ASP.NET Core authorization policies —
  `token-issue` and `extrato-read` — each requiring its own scope claim.
- **Rationale**: This is the standard, well-supported .NET mechanism for JWT bearer auth and
  integrates natively with minimal API endpoint filters (`RequireAuthorization("policy-name")`),
  directly satisfying constitution Principle II (distinct scope per endpoint, not just "any valid
  JWT").
- **Alternatives considered**: A custom authentication handler — rejected; no requirement exists
  that isn't already covered by the built-in JWT bearer handler. A single shared scope for both
  endpoints — rejected outright, as it would violate the least-privilege principle.

## 3. Outbound MTLS to Banco Inter: named/typed `HttpClient` via `HttpClientFactory`

- **Decision**: Configure a typed `HttpClient` in Infrastructure via `IHttpClientFactory`, with
  `SocketsHttpHandler.SslOptions.ClientCertificates` (or `HttpClientHandler.ClientCertificates`)
  populated from a certificate loaded through the Options pattern (path/store reference from
  secret configuration, never a literal cert in appsettings).
- **Rationale**: `HttpClientFactory` avoids classic `HttpClient` socket-exhaustion pitfalls and
  is the standard .NET pattern for centrally configuring outbound clients. Isolating certificate
  wiring to a single typed client in Infrastructure directly satisfies constitution Principle I.
- **Alternatives considered**: A raw `HttpClient` instantiated per request — rejected due to
  socket exhaustion risk and because it would scatter certificate-handling code outside
  Infrastructure. A dedicated MTLS reverse-proxy sidecar (e.g., Envoy) instead of in-process
  certificate handling — rejected as unnecessary infrastructure complexity for two endpoints; can
  be revisited later if the proxy needs to front many more Inter capabilities.

## 4. Internally-managed Inter access token (for Extrato): in-memory cache

- **Decision**: `IInterAccessTokenProvider`, implemented in Infrastructure with
  `Microsoft.Extensions.Caching.Memory`, caches the Inter access token obtained via
  `IInterTokenClient` and refreshes it shortly before expiry.
- **Rationale**: Per spec FR-005, Extrato must not require the consumer to supply an Inter token.
  An in-memory cache is the simplest mechanism that avoids calling Inter's token endpoint on every
  Extrato request while keeping the proxy stateless with respect to banking data (constitution
  "Statelessness" constraint) — the cached value is a short-lived credential, not business data.
- **Alternatives considered**: A distributed cache (Redis) — deferred; unnecessary for a
  single-instance deployment and adds an operational dependency not justified by current scale.
  Fetching a fresh Inter token on every Extrato call — rejected as wasteful and slower, working
  against SC-003's 5-second response goal.

## 5. Testing Inter API interactions without live MTLS

- **Decision**: `WireMock.Net` (or an injected fake `HttpMessageHandler`) stands in for Banco
  Inter's Token/Extrato endpoints in Infrastructure and Api integration tests; `xUnit` +
  `WebApplicationFactory` cover endpoint-level JWT/scope authorization.
- **Rationale**: The constitution's Development Workflow gate requires an integration test
  against a mocked Inter API covering both success and failure (auth, MTLS, upstream error)
  paths, without depending on Banco Inter's real sandbox for every test run.
- **Alternatives considered**: Testing exclusively against Banco Inter's sandbox environment —
  rejected as slow, flaky, and dependent on external availability/credentials for routine test
  runs; still valuable for a smaller set of manual/scheduled verification runs, but not the
  primary test strategy.

## 6. Inter wire-format grounding (from official samples)

- **Decision**: Base `InterHttpClient`'s request/response mapping directly on Banco Inter's own
  sample code in `specs/001-token-extrato-proxy/samples/token/request-response-sample.md` and
  `samples/extrato/get-request-response-sample.md`, rather than on assumed field names.
- **Rationale**: The samples reveal two details the original Technical Context guessed wrong:
  (1) `GET /banking/v2/extrato` requires an `x-conta-corrente` header carrying the checking
  account number, in addition to `dataInicio`/`dataFim` query parameters — this was missing from
  the initial design; (2) Inter's response field names (`cpmf`, `dataEntrada`, `tipoTransacao`,
  `tipoOperacao`, `valor`, `titulo`, `descricao`) differ from the originally assumed
  `Date`/`Type`/`Description`/`Amount` shape. Both are now reflected in `data-model.md`'s
  `StatementQuery`/`StatementEntry` definitions and the OpenAPI contracts.
- **Decision — account number placement**: `x-conta-corrente` is treated as proxy-side
  configuration (`Inter:ContaCorrente`), not a consumer-supplied parameter, consistent with the
  spec's "single Banco Inter application/account" assumption. Consumers only ever supply
  `startDate`/`endDate`.
- **Decision — amount parsing**: Inter's sample documents `valor` as a string; `InterHttpClient`
  parses it to `decimal` with `CultureInfo.InvariantCulture` and surfaces a clear upstream-format
  error (not an unhandled exception) if parsing ever fails.
- **Decision — error status mapping**: Inter's documented statuses are `400`, `403`, `404`, `503`
  (no `401`). To avoid a consumer confusing an Inter-side `403` (Inter's own authorization rule)
  with this proxy's own `403` (missing JWT scope), `InterHttpClient`/the use cases map all
  Inter-side error responses to the proxy's own upstream-error contract (`502`-style in the
  OpenAPI specs) rather than passing Inter's status code straight through.
- **Alternatives considered**: Passing Inter's HTTP status through unchanged — rejected because
  it conflates this proxy's own authorization semantics (401/403 = JWT problem) with Inter's
  upstream semantics (403 = Inter denied the request), which would be confusing and could leak
  implementation details (constitution Principle IV/FR-009).

## 7. Resilience for outbound Inter calls

- **Decision**: `Microsoft.Extensions.Http.Resilience` (Polly-based) standard resilience handler
  attached to the Inter typed client, with a bounded retry count and an overall timeout aligned
  with SC-003.
- **Rationale**: Banco Inter calls can transiently fail (network, rate limiting); a small number
  of bounded retries improves reliability without masking genuine upstream outages, which must
  still surface as a clear error per FR-009/SC-004.
- **Alternatives considered**: No retry policy — rejected as it would make the proxy needlessly
  fragile to transient network blips. Unbounded/aggressive retries — rejected as it risks
  violating SC-003's response-time goal and could amplify load against Banco Inter during an
  outage.
