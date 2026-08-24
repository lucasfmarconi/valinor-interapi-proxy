# Phase 1 Data Model: Token and Extrato Proxy Endpoints

These are the Core-layer models used to shape use case inputs/outputs. They are internal
shapes, not Banco Inter's wire format — the Infrastructure layer's `InterHttpClient` is
responsible for translating Inter's own request/response schema into these models (and vice
versa), so a change in Inter's API surface doesn't ripple past Infrastructure.

> Grounded against Banco Inter's official samples in
> `specs/001-token-extrato-proxy/samples/token/request-response-sample.md` and
> `specs/001-token-extrato-proxy/samples/extrato/get-request-response-sample.md`. See the
> "Inter wire format" note under each model for the exact upstream field mapping.

## InterAccessToken

Represents a Banco Inter OAuth access token.

| Field | Type | Notes |
|---|---|---|
| `Value` | `string` | The bearer token value. Never logged in the clear (constitution Principle IV / V). |
| `TokenType` | `string` | Typically `"Bearer"`, as returned by Inter. |
| `ExpiresAt` | `DateTimeOffset` | Absolute expiry, computed from Inter's `expires_in` at issuance time. Used by `IInterAccessTokenProvider` to decide when to refresh. |
| `Scope` | `string` | The Inter-side OAuth scope granted, as returned by Inter (e.g. `"extrato.read"`). Distinct from and unrelated to this proxy's own JWT scopes (`token-issue`, `extrato-read`) — do not conflate the two. |

**Validation rules**: `Value` must be non-empty; `ExpiresAt` must be in the future at the moment
the token is returned to a consumer (Token endpoint) or used internally (Extrato use case) — if
expired, it MUST be refreshed before use, not handed out.

**Inter wire format** (`POST https://cdpj.partners.bancointer.com.br/oauth/v2/token`,
form-urlencoded, MTLS): request carries `client_id`, `client_secret`, `scope`,
`grant_type=client_credentials`. Response JSON: `{ access_token, token_type, expires_in, scope }`
— `expires_in` is a relative integer number of seconds, not an absolute timestamp; `ExpiresAt` is
computed as `issuedAt + expires_in` by `InterHttpClient`. The `scope` value requested is a fixed
string configured for this proxy's Inter application (e.g. `Inter:Scope` in Infrastructure
options) — **the consumer never chooses or influences which Inter scope is requested**, on either
the Token or Extrato endpoint, since that would let a consumer request Inter permissions beyond
what the proxy's own least-privilege JWT scope authorized them for.

**Lifecycle**: Issued by `IInterTokenClient.IssueTokenAsync()`. For the Token endpoint, an
`InterAccessToken` is mapped to the HTTP response and then discarded by the proxy — it is not
persisted. For the Extrato use case, an `InterAccessToken` is held only inside
`IInterAccessTokenProvider`'s in-memory cache for its lifetime and re-used until it nears expiry.

## StatementQuery

Represents a validated request for a bank statement.

| Field | Type | Notes |
|---|---|---|
| `StartDate` | `DateOnly` | Inclusive start of the requested period. |
| `EndDate` | `DateOnly` | Inclusive end of the requested period. |

**Validation rules** (enforced in `GetStatementUseCase` before calling Infrastructure, per spec
FR-007 / Edge Cases):
- `EndDate` must not be before `StartDate`.
- `EndDate` must not be in the future.
- The `(StartDate, EndDate)` span must not exceed the maximum period Banco Inter supports for
  statement retrieval (exact limit confirmed against Inter's API documentation during
  implementation; treated as configuration so it can be adjusted without a code change).

**Inter wire format** (`GET https://cdpj.partners.bancointer.com.br/banking/v2/extrato`, MTLS):
`StartDate`/`EndDate` map to the `dataInicio`/`dataFim` query parameters. The request also
requires an `Authorization: Bearer <token>` header (the internally-managed `InterAccessToken`)
and an **`x-conta-corrente` header carrying the checking-account number** — this is a required
Inter-side parameter that is **not** part of `StatementQuery`. Per the spec Assumption of a
single Banco Inter application/account for this feature, the account number is proxy-side
configuration (e.g. `Inter:ContaCorrente`), supplied by `InterHttpClient` on every call, and is
never accepted from or exposed to the consumer. If multi-account support is added later, this
becomes a `StatementQuery` field and a constitution-reviewed scope/authorization change
(consistent with Principle III's requirement that new capabilities get explicit review).

## BankStatement

Represents the statement data returned to a consumer.

| Field | Type | Notes |
|---|---|---|
| `StartDate` | `DateOnly` | Echoes the requested period start. |
| `EndDate` | `DateOnly` | Echoes the requested period end. |
| `Entries` | `IReadOnlyList<StatementEntry>` | The transaction entries for the period, in the order returned by Banco Inter. |

## StatementEntry

A single transaction line within a `BankStatement`.

| Field | Type | Notes |
|---|---|---|
| `EntryDate` | `DateOnly` | Transaction date. |
| `Cpmf` | `string` | CPMF indicator as supplied by Banco Inter (kept as an opaque string; not interpreted by Core). |
| `TransactionType` | `string` | Transaction type as supplied by Banco Inter (e.g. credit/debit classification). |
| `OperationType` | `string` | Operation type as supplied by Banco Inter. |
| `Amount` | `decimal` | Signed transaction amount, parsed from Inter's numeric string via `decimal.Parse(..., CultureInfo.InvariantCulture)` in `InterHttpClient`. |
| `Title` | `string` | Short title/label as supplied by Banco Inter. |
| `Description` | `string` | Free-text description as supplied by Banco Inter. |

**Inter wire format**: Inter's `200` response body is `{ "transacoes": [ { cpmf, dataEntrada,
tipoTransacao, tipoOperacao, valor, titulo, descricao } ] }`, with every field documented as a
string (including `valor`, the amount). `InterHttpClient` maps
`dataEntrada → EntryDate`, `tipoTransacao → TransactionType`, `tipoOperacao → OperationType`,
`valor → Amount` (parsed to `decimal`), `titulo → Title`, `descricao → Description`, and
`cpmf → Cpmf` (left as `string`, no proxy-side semantics assumed). If `valor` ever contains a
non-numeric value in practice, `InterHttpClient` must surface a clear upstream-format error
(FR-009) rather than let a parse exception propagate as an unhandled 500.
**HTTP status codes observed from Inter**: `200`, `400`, `403`, `404`, `503` — notably no `401`;
Inter uses `403` for its own authorization-rule violations. `InterHttpClient` maps all Inter-side
error statuses to the proxy's upstream-error response (constitution: no leaking of internal/Inter
details) rather than passing Inter's status code straight through, so a `403` from Inter is never
confused with this proxy's own `403` (missing JWT scope).

## AuthenticatedCaller (not a Core entity — Api-layer context)

Represents the JWT-authenticated consumer making a request. Not persisted or passed into Core as
a rich model; only the fields needed for authorization and logging are used directly from the
validated JWT's claims:

| Claim | Used for |
|---|---|
| `sub` (subject) | Logged as the caller identity per constitution Principle V / spec FR-010. |
| `scope` (or equivalent) | Evaluated by the `token-issue` / `extrato-read` authorization policies. |

## Relationships

```text
StatementQuery ──(GetStatementUseCase)──> BankStatement (1) ──contains──> StatementEntry (0..n)
InterAccessToken ──(used internally by)──> GetStatementUseCase
InterAccessToken ──(returned by)──> IssueTokenUseCase
```

No entity in this feature has persistent state or a state machine — every model is either a
transient request/response shape or a short-lived, in-memory-cached credential.
