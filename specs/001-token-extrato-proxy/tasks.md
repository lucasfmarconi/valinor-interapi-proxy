---

description: "Task list for Token and Extrato Proxy Endpoints"
---

# Tasks: Token and Extrato Proxy Endpoints

**Input**: Design documents from `/specs/001-token-extrato-proxy/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Included. The project constitution's Development Workflow gate requires, for every
proxied endpoint: an authorization-rejection test, and an integration test against a mocked
Inter API covering success and failure paths — so test tasks are mandatory here, not optional.

**Organization**: Tasks are grouped by user story (US1 = Extrato, US2 = Token) to enable
independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2)
- Paths are relative to the repository root

## Path Conventions

Per `plan.md`'s Project Structure (Core/Infrastructure/Api, 3 projects):

- `src/ValinorInterApiProxy.Core/` — entities, use cases, ports (no external dependencies)
- `src/ValinorInterApiProxy.Infrastructure/` — Inter MTLS HTTP client, token cache
- `src/ValinorInterApiProxy.Api/` — minimal API endpoints, JWT auth, composition root
- `tests/ValinorInterApiProxy.Core.Tests/`, `tests/ValinorInterApiProxy.Infrastructure.Tests/`,
  `tests/ValinorInterApiProxy.Api.Tests/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Solution/project scaffolding per the Project Structure in plan.md

- [ ] T001 Create the .NET 10 solution and three source projects
      (`src/ValinorInterApiProxy.Api`, `src/ValinorInterApiProxy.Core`,
      `src/ValinorInterApiProxy.Infrastructure`) and three test projects
      (`tests/ValinorInterApiProxy.Core.Tests`, `tests/ValinorInterApiProxy.Infrastructure.Tests`,
      `tests/ValinorInterApiProxy.Api.Tests`), wired so `Api` references `Core` and
      `Infrastructure`, `Infrastructure` references `Core`, and `Core` references neither
      (Dependency Rule from plan.md's Structure Decision); add all six projects to the `.sln`.
- [ ] T002 Add NuGet package references: `Microsoft.AspNetCore.Authentication.JwtBearer` and
      `Microsoft.Extensions.Http.Resilience` to `ValinorInterApiProxy.Api`;
      `Microsoft.Extensions.Caching.Memory` and `Microsoft.Extensions.Http.Resilience` to
      `ValinorInterApiProxy.Infrastructure`; `xunit`, `WireMock.Net`, and
      `Microsoft.AspNetCore.Mvc.Testing` to the three test projects.
- [ ] T003 [P] Add a root `.editorconfig` and enable `dotnet format` verification, matching the
      constitution's Development Workflow gate for code review.

**Checkpoint**: `dotnet build` succeeds across the empty solution.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core ports/models, MTLS client scaffolding, and JWT auth plumbing shared by both
Token and Extrato — nothing in Phase 3+ can be implemented before this phase completes.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [ ] T004 Define Core ports `IInterTokenClient`, `IInterStatementClient`, and
      `IInterAccessTokenProvider` in `src/ValinorInterApiProxy.Core/Ports/` per data-model.md
      (Infrastructure will implement these; Core and Api depend only on the interfaces).
- [ ] T005 [P] Define Core models `InterAccessToken`, `StatementQuery`, `BankStatement`, and
      `StatementEntry` in `src/ValinorInterApiProxy.Core/Models/`, matching the fields and
      validation rules in data-model.md exactly (including the `EntryDate`/`Cpmf`/
      `TransactionType`/`OperationType`/`Amount`/`Title`/`Description` shape for
      `StatementEntry`).
- [ ] T006 Implement `InterOptions` in `src/ValinorInterApiProxy.Infrastructure/InterApi/InterOptions.cs`
      bound via the Options pattern from secret configuration: `ClientId`, `ClientSecret`,
      `CertificatePath`, `CertificatePassword`, `Scope`, `ContaCorrente`, `BaseUrl` — no literal
      secret values anywhere in source (constitution Principle IV).
- [ ] T007 Register a typed `HttpClient` for `InterHttpClient` in
      `src/ValinorInterApiProxy.Infrastructure/InterApi/InterHttpClient.cs` via
      `IHttpClientFactory`, with the MTLS client certificate (loaded from `InterOptions`) attached
      to the handler and a standard resilience policy (bounded retry + timeout) applied
      (constitution Principle I). Stub `IssueTokenAsync`/`GetStatementAsync` bodies to be filled
      in by US2/US1 tasks below.
- [ ] T008 [P] Configure JWT Bearer authentication in
      `src/ValinorInterApiProxy.Api/Program.cs` using `Microsoft.AspNetCore.Authentication.JwtBearer`,
      validating signature, issuer, audience, and expiry from `Jwt:Authority`/`Jwt:Audience`
      configuration (constitution Principle II; FR-001).
- [ ] T009 [P] Define authorization policies `token-issue` and `extrato-read` in
      `src/ValinorInterApiProxy.Api/Authorization/ScopePolicies.cs`, each requiring its own
      distinct scope claim (constitution Principle II; FR-002/FR-003).
- [ ] T010 [P] Implement correlation-id and secret-redacting structured logging middleware in
      `src/ValinorInterApiProxy.Api/Middleware/CorrelationLoggingMiddleware.cs`, logging
      correlation id, caller `sub` claim, and outcome for every request without logging token
      values, credentials, or statement contents (constitution Principle V; FR-010).
- [ ] T011 Implement a shared `ErrorResponse` DTO and upstream-error mapping helper in
      `src/ValinorInterApiProxy.Api/Contracts/ErrorResponse.cs`, matching the
      `{ correlationId, message }` shape in `contracts/token.openapi.yaml` and
      `contracts/extrato.openapi.yaml`, used to translate both JWT-auth failures and Inter
      upstream failures into consumer-safe responses (FR-008/FR-009).
- [ ] T012 Wire the composition root in `src/ValinorInterApiProxy.Api/Program.cs`: register
      `InterHttpClient` behind `IInterTokenClient`/`IInterStatementClient`, register JWT auth +
      the two authorization policies, and add the correlation-logging middleware to the pipeline
      (depends on T004–T011).

**Checkpoint**: Foundation ready — JWT auth, MTLS client scaffolding, and error/logging
infrastructure exist; Extrato and Token implementation can now proceed independently.

---

## Phase 3: User Story 1 - Retrieve Bank Statement via Proxy (Priority: P1) 🎯 MVP

**Goal**: A JWT-authenticated consumer with the `extrato-read` scope can retrieve a Banco Inter
bank statement for a date range, without ever needing an MTLS certificate or a prior Token call.

**Independent Test**: Present a JWT with only the `extrato-read` scope and a valid date range to
`GET /extrato`; verify statement data is returned without any preceding `/token` call.

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T013 [P] [US1] Contract test for `GET /extrato` success response shape (matching
      `contracts/extrato.openapi.yaml`) in `tests/ValinorInterApiProxy.Api.Tests/ExtratoEndpointTests.cs`.
- [ ] T014 [P] [US1] Authorization-rejection test in
      `tests/ValinorInterApiProxy.Api.Tests/ExtratoAuthorizationTests.cs`: no JWT → 401; JWT
      without `extrato-read` scope (e.g., only `token-issue`) → 403; no Inter call is made in
      either case (spec Acceptance Scenario US1.2, SC-002).
- [ ] T015 [P] [US1] Integration test for `InterHttpClient.GetStatementAsync` against
      `WireMock.Net` in `tests/ValinorInterApiProxy.Infrastructure.Tests/InterHttpClientExtratoTests.cs`,
      simulating Banco Inter's `GET /banking/v2/extrato` for: success (200 with `transacoes[]`
      mapped correctly), and each documented failure status (400, 403, 404, 503) mapping to the
      proxy's upstream-error shape.
- [ ] T016 [P] [US1] Unit test for `GetStatementUseCase` date-range validation in
      `tests/ValinorInterApiProxy.Core.Tests/GetStatementUseCaseTests.cs`: end-before-start,
      future end date, and range exceeding the configured maximum period all produce a
      validation error (spec Edge Cases, FR-007), with `IInterAccessTokenProvider`/
      `IInterStatementClient` mocked.

### Implementation for User Story 1

- [ ] T017 [US1] Implement `MemoryCacheInterAccessTokenProvider` in
      `src/ValinorInterApiProxy.Infrastructure/Caching/MemoryCacheInterAccessTokenProvider.cs`
      implementing `IInterAccessTokenProvider`: obtains a token via `IInterTokenClient` on first
      use, caches it, and refreshes shortly before `ExpiresAt` (FR-005; depends on T004, T007).
- [ ] T018 [US1] Implement `InterHttpClient.GetStatementAsync` in
      `src/ValinorInterApiProxy.Infrastructure/InterApi/InterHttpClient.cs`: send
      `dataInicio`/`dataFim` query params and the `x-conta-corrente` header (from `InterOptions`)
      with the cached Bearer token, map `transacoes[]` to `StatementEntry` (parsing `valor` to
      `decimal` with `CultureInfo.InvariantCulture`, surfacing a clear error on parse failure),
      and map Inter's 400/403/404/503 to the upstream-error contract (depends on T005, T007,
      T017).
- [ ] T019 [US1] Implement `GetStatementUseCase` in
      `src/ValinorInterApiProxy.Core/UseCases/GetStatementUseCase.cs`: validate the incoming
      `StatementQuery` (per T016's rules), call `IInterAccessTokenProvider` +
      `IInterStatementClient`, and return a `BankStatement` (depends on T004, T005, T018).
- [ ] T020 [US1] Implement `GET /extrato` in
      `src/ValinorInterApiProxy.Api/Endpoints/ExtratoEndpoint.cs`: require the `extrato-read`
      policy, bind `startDate`/`endDate` query parameters to a `StatementQuery`, call
      `GetStatementUseCase`, map `BankStatement` to the `StatementResponse` DTO, and map
      validation/upstream errors to 400/502 via the shared `ErrorResponse` helper (depends on
      T009, T011, T019).
- [ ] T021 [US1] Emit correlation-id, caller-identity, and outcome logging for every `/extrato`
      request via the middleware from T010 (FR-010; depends on T010, T020).

**Checkpoint**: `/extrato` is fully functional and independently testable — this is the MVP.

---

## Phase 4: User Story 2 - Obtain Banco Inter Access Token via Proxy (Priority: P2)

**Goal**: A JWT-authenticated consumer with the `token-issue` scope can obtain a Banco Inter
access token, without ever supplying Inter credentials or a client certificate.

**Independent Test**: Present a JWT with only the `token-issue` scope to `POST /token`; verify a
valid Banco Inter access token and expiry are returned.

### Tests for User Story 2

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T022 [P] [US2] Contract test for `POST /token` success response shape (matching
      `contracts/token.openapi.yaml`) in `tests/ValinorInterApiProxy.Api.Tests/TokenEndpointTests.cs`.
- [ ] T023 [P] [US2] Authorization-rejection test in
      `tests/ValinorInterApiProxy.Api.Tests/TokenAuthorizationTests.cs`: no JWT → 401; JWT
      without `token-issue` scope (e.g., only `extrato-read`) → 403 (spec Acceptance Scenario
      US2.2, SC-002).
- [ ] T024 [P] [US2] Integration test for `InterHttpClient.IssueTokenAsync` against
      `WireMock.Net` in `tests/ValinorInterApiProxy.Infrastructure.Tests/InterHttpClientTokenTests.cs`,
      simulating Banco Inter's `POST /oauth/v2/token` for: success (200 with `access_token`/
      `expires_in`/`scope` mapped to `InterAccessToken`), and each documented failure status
      (400, 403, 404, 503) mapping to the proxy's upstream-error shape.

### Implementation for User Story 2

- [ ] T025 [US2] Implement `InterHttpClient.IssueTokenAsync` in
      `src/ValinorInterApiProxy.Infrastructure/InterApi/InterHttpClient.cs`: POST
      form-urlencoded `client_id`/`client_secret`/`scope`/`grant_type=client_credentials` over
      MTLS to Inter's token endpoint, map the response to `InterAccessToken` (`ExpiresAt` =
      issuance time + `expires_in`), and map Inter's 400/403/404/503 to the upstream-error
      contract (depends on T005, T007).
- [ ] T026 [US2] Implement `IssueTokenUseCase` in
      `src/ValinorInterApiProxy.Core/UseCases/IssueTokenUseCase.cs`: call `IInterTokenClient`
      and return the resulting `InterAccessToken` (depends on T004, T005, T025).
- [ ] T027 [US2] Implement `POST /token` in
      `src/ValinorInterApiProxy.Api/Endpoints/TokenEndpoint.cs`: require the `token-issue`
      policy, call `IssueTokenUseCase`, map `InterAccessToken` to the `TokenResponse` DTO, and
      map upstream errors via the shared `ErrorResponse` helper (depends on T009, T011, T026).
- [ ] T028 [US2] Emit correlation-id, caller-identity, and outcome logging for every `/token`
      request via the middleware from T010 (FR-010; depends on T010, T027).

**Checkpoint**: Both `/extrato` and `/token` are independently functional and testable.

---

## Phase 5: Polish & Cross-Cutting Concerns

**Purpose**: Validation and hardening across both stories

- [ ] T029 [P] Run every scenario in `quickstart.md`'s validation checklist (SC-001–SC-005)
      end-to-end against a running instance with Inter calls pointed at `WireMock.Net` or Banco
      Inter's sandbox.
- [ ] T030 [P] Add a repository root `README.md` describing the proxy's purpose, setup, and links
      to `specs/001-token-extrato-proxy/spec.md` and `plan.md`.
- [ ] T031 Review structured logs produced while running T029 to confirm no credential, token
      value, certificate material, or statement content appears in the clear (SC-005); adjust
      the T010 redaction logic if anything leaks.
- [ ] T032 [P] Run `dotnet format` across `src/` and `tests/` and fix any violations.
- [ ] T033 Re-walk the Constitution Check table in `plan.md` against the actual implementation
      and confirm every gate still reads PASS.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately.
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS both user stories.
- **User Story 1 (Phase 3)**: Depends on Foundational only — no dependency on User Story 2.
- **User Story 2 (Phase 4)**: Depends on Foundational only — no dependency on User Story 1.
- **Polish (Phase 5)**: Depends on both user stories being complete.

### User Story Dependencies

- **US1 (Extrato, P1)**: Can start immediately after Foundational. No dependency on US2.
- **US2 (Token, P2)**: Can start immediately after Foundational. No dependency on US1. Note:
  US2 does not need US1's `MemoryCacheInterAccessTokenProvider` (T017) — Token issuance always
  calls `IInterTokenClient` directly, per FR-005/spec Assumptions.

### Within Each User Story

- Tests (T013–T016 / T022–T024) MUST be written and FAIL before their corresponding
  implementation tasks.
- Models/ports (Phase 2) before use cases; use cases before endpoints.
- Story complete and independently verified before moving to Polish.

### Parallel Opportunities

- T005/T008/T009/T010 (Phase 2) can run in parallel — different files, no cross-dependencies.
- All four US1 test tasks (T013–T016) can run in parallel.
- All three US2 test tasks (T022–T024) can run in parallel.
- Once Foundational (Phase 2) is complete, US1 (Phase 3) and US2 (Phase 4) can be staffed and
  built in parallel by different developers.

---

## Parallel Example: Phase 2 Foundational

```bash
# After T004 (ports) and T006/T007 (Inter client scaffolding) land, these can run together:
Task: "Define Core models in src/ValinorInterApiProxy.Core/Models/"
Task: "Configure JWT Bearer authentication in src/ValinorInterApiProxy.Api/Program.cs"
Task: "Define authorization policies in src/ValinorInterApiProxy.Api/Authorization/ScopePolicies.cs"
Task: "Implement correlation-id logging middleware in src/ValinorInterApiProxy.Api/Middleware/"
```

## Parallel Example: User Story 1 Tests

```bash
Task: "Contract test for GET /extrato in tests/ValinorInterApiProxy.Api.Tests/ExtratoEndpointTests.cs"
Task: "Authorization-rejection test in tests/ValinorInterApiProxy.Api.Tests/ExtratoAuthorizationTests.cs"
Task: "InterHttpClient.GetStatementAsync integration test in tests/ValinorInterApiProxy.Infrastructure.Tests/InterHttpClientExtratoTests.cs"
Task: "GetStatementUseCase validation unit test in tests/ValinorInterApiProxy.Core.Tests/GetStatementUseCaseTests.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL — blocks both stories)
3. Complete Phase 3: User Story 1 (Extrato)
4. **STOP and VALIDATE**: run T013–T016 and the relevant quickstart.md scenarios independently
5. Deploy/demo if ready — Extrato alone already delivers the proxy's primary business value

### Incremental Delivery

1. Setup + Foundational → foundation ready
2. Add User Story 1 (Extrato) → test independently → deploy/demo (MVP!)
3. Add User Story 2 (Token) → test independently → deploy/demo
4. Polish (Phase 5) → final validation against all Success Criteria

### Parallel Team Strategy

With two developers: both complete Setup + Foundational together, then one takes US1 (Extrato)
and the other takes US2 (Token) — they integrate independently since neither story depends on
the other.

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Verify each test task fails before writing its corresponding implementation
- Commit after each task or logical group
- Stop at either story's checkpoint to validate independently
- No task in this list violates the constitution's Explicit Endpoint Allowlist (Principle III):
  only `/token` and `/extrato` are ever implemented
