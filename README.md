# Valinor InterAPI Proxy

A .NET 10 ASP.NET Core web service that proxies two Banco Inter APIs — **Token** (OAuth
client-credentials issuance) and **Extrato** (bank statement retrieval) — over mutual TLS, so
that consumers who cannot establish an MTLS connection to Banco Inter themselves still can. The
proxy sits behind JWT authentication: each endpoint requires a distinct, least-privilege scope
(`token-issue` for `/token`, `extrato-read` for `/extrato`), and the proxy holds Banco Inter's
own client credentials and certificate internally — consumers never supply Inter-specific
credentials or certificates.

The proxy is its own identity provider for these consumer JWTs: it self-issues them via
`/auth/token` to pre-registered consumers, so no external OIDC provider needs to be stood up
(constitution Principle II).

`/extrato` manages its own internally-cached Banco Inter access token, so it works independently
of any prior `/token` call by the consumer.

## Endpoints

| Endpoint | Method | Required scope | Purpose |
|---|---|---|---|
| `/token` | `POST` | `token-issue` | Issues a Banco Inter access token |
| `/extrato` | `GET` | `extrato-read` | Retrieves a bank statement for a date range |
| `/auth/token` | `POST` | *(anonymous)* | Self-issues a consumer JWT in exchange for a registered `client_id`/`client_secret` |

`/token` and `/extrato` are the only two *proxied* endpoints — no generic passthrough.
`/auth/token` is the one explicitly permitted non-proxied exception (constitution Principle III):
it never calls Banco Inter, it only mints the JWT used to call the other two.

## Architecture

A pragmatic Clean Architecture split across three projects:

- `src/ValinorInterApiProxy.Core/` — entities, use cases, ports. No external dependencies.
- `src/ValinorInterApiProxy.Infrastructure/` — the MTLS-bearing Inter HTTP client and the
  internally-cached access token provider. Implements Core's ports.
- `src/ValinorInterApiProxy.Api/` — minimal API endpoints, JWT auth/scope policies, correlation
  logging, and the DI composition root (`Program.cs`).

## Getting started

See [`specs/001-token-extrato-proxy/quickstart.md`](specs/001-token-extrato-proxy/quickstart.md)
for local configuration (User Secrets), running the service, and example requests.

### Environment configuration

**Prerequisites**: .NET 10 SDK.

**Run the automated tests** (no credentials needed — Banco Inter is stubbed via WireMock):

```bash
dotnet test
```

**Configure local secrets** (needed only to run the live service against Banco Inter). Never
commit these values — they are stored outside the repo by `dotnet user-secrets`:

```bash
cd src/ValinorInterApiProxy.Api
dotnet user-secrets set "Inter:ClientId" "<client-id>"
dotnet user-secrets set "Inter:ClientSecret" "<client-secret>"
dotnet user-secrets set "Inter:CertificatePath" "<path-to-mtls-cert.pfx>"
dotnet user-secrets set "Inter:CertificatePassword" "<cert-password>"
dotnet user-secrets set "Inter:Scope" "extrato.read"
dotnet user-secrets set "Inter:ContaCorrente" "<conta-corrente-number>"
dotnet user-secrets set "Jwt:SigningKey" "<base64-encoded-key-at-least-32-bytes>"
dotnet user-secrets set "Jwt:Issuer" "valinor-interapi-proxy"
dotnet user-secrets set "Jwt:Audience" "valinor-interapi-proxy-consumers"
dotnet user-secrets set "Jwt:Consumers:0:ClientId" "<consumer-client-id>"
dotnet user-secrets set "Jwt:Consumers:0:ClientSecret" "<consumer-client-secret>"
dotnet user-secrets set "Jwt:Consumers:0:AllowedScopes:0" "token-issue"
dotnet user-secrets set "Jwt:Consumers:0:AllowedScopes:1" "extrato-read"
```

| Key | Purpose |
|---|---|
| `Inter:ClientId` / `Inter:ClientSecret` | Banco Inter OAuth client-credentials |
| `Inter:CertificatePath` / `Inter:CertificatePassword` | MTLS client certificate used for all Banco Inter calls |
| `Inter:Scope` | OAuth scope requested when issuing a Banco Inter token (e.g. `extrato.read`) |
| `Inter:ContaCorrente` | Conta corrente number used for `/extrato` lookups |
| `Jwt:SigningKey` | Base64-encoded symmetric key the proxy uses to sign and validate its own consumer JWTs |
| `Jwt:Issuer` / `Jwt:Audience` | `iss`/`aud` claims the proxy stamps on tokens it issues, and validates on tokens it receives |
| `Jwt:Consumers:N:ClientId` / `:ClientSecret` / `:AllowedScopes:N` | One entry per registered consumer allowed to call `/auth/token` |

**Getting a JWT**: generate a signing key once, register at least one consumer (as above), then
exchange its credentials for a JWT:

```bash
openssl rand -base64 32   # use this as Jwt:SigningKey

curl -X POST https://localhost:5001/auth/token \
  --data-urlencode "client_id=<consumer-client-id>" \
  --data-urlencode "client_secret=<consumer-client-secret>" \
  --data-urlencode "scope=token-issue extrato-read"
```

Expected: `200 OK` with `access_token`, `token_type`, `expires_in`. Use the returned
`access_token` as the `Authorization: Bearer` value in the requests below.

**Run the service**:

```bash
dotnet run --project src/ValinorInterApiProxy.Api
```

Then see the quickstart linked above for example `curl` requests against `/token` and
`/extrato`, including the least-privilege (401/403) checks.

## Documentation

- [Feature specification](specs/001-token-extrato-proxy/spec.md)
- [Implementation plan](specs/001-token-extrato-proxy/plan.md)
- [Task breakdown](specs/001-token-extrato-proxy/tasks.md)