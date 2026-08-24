# Valinor InterAPI Proxy

A .NET 10 ASP.NET Core web service that proxies two Banco Inter APIs — **Token** (OAuth
client-credentials issuance) and **Extrato** (bank statement retrieval) — over mutual TLS, so
that consumers who cannot establish an MTLS connection to Banco Inter themselves still can. The
proxy sits behind JWT authentication: each endpoint requires a distinct, least-privilege scope
(`token-issue` for `/token`, `extrato-read` for `/extrato`), and the proxy holds Banco Inter's
own client credentials and certificate internally — consumers never supply Inter-specific
credentials or certificates.

`/extrato` manages its own internally-cached Banco Inter access token, so it works independently
of any prior `/token` call by the consumer.

## Endpoints

| Endpoint | Method | Required scope | Purpose |
|---|---|---|---|
| `/token` | `POST` | `token-issue` | Issues a Banco Inter access token |
| `/extrato` | `GET` | `extrato-read` | Retrieves a bank statement for a date range |

These are the *only* two endpoints exposed by the proxy — no generic passthrough.

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

## Documentation

- [Feature specification](specs/001-token-extrato-proxy/spec.md)
- [Implementation plan](specs/001-token-extrato-proxy/plan.md)
- [Task breakdown](specs/001-token-extrato-proxy/tasks.md)