# Quickstart: Token and Extrato Proxy Endpoints

## Prerequisites

- .NET 10 SDK
- A JWT for a consumer identity, carrying either (or both) the `token-issue` and `extrato-read`
  scopes, issued by the identity provider configured for this environment
- Banco Inter application credentials (client id/secret) and MTLS client certificate, available
  to the proxy via User Secrets (local dev) — never committed to source control

## Local configuration (development)

```bash
cd src/ValinorInterApiProxy.Api
dotnet user-secrets set "Inter:ClientId" "<client-id>"
dotnet user-secrets set "Inter:ClientSecret" "<client-secret>"
dotnet user-secrets set "Inter:CertificatePath" "<path-to-mtls-cert.pfx>"
dotnet user-secrets set "Inter:CertificatePassword" "<cert-password>"
dotnet user-secrets set "Inter:Scope" "extrato.read"
dotnet user-secrets set "Inter:ContaCorrente" "<conta-corrente-number>"
dotnet user-secrets set "Jwt:Authority" "<identity-provider-issuer-url>"
dotnet user-secrets set "Jwt:Audience" "<expected-audience>"
```

## Run

```bash
dotnet run --project src/ValinorInterApiProxy.Api
```

## Verify: issue a token

```bash
curl -X POST https://localhost:5001/token \
  -H "Authorization: Bearer <jwt-with-token-issue-scope>"
```

Expected: `200 OK` with `accessToken`, `tokenType`, `expiresAt`.

## Verify: retrieve a statement

```bash
curl -G https://localhost:5001/extrato \
  -H "Authorization: Bearer <jwt-with-extrato-read-scope>" \
  --data-urlencode "startDate=2026-07-01" \
  --data-urlencode "endDate=2026-07-31"
```

Expected: `200 OK` with `startDate`, `endDate`, and an `entries` array. Note this succeeds even
if the caller never called `/token` first — Extrato manages its own Banco Inter token
internally (spec FR-005).

## Verify: least-privilege enforcement

```bash
# A JWT with only extrato-read must NOT be able to issue a token:
curl -i -X POST https://localhost:5001/token \
  -H "Authorization: Bearer <jwt-with-only-extrato-read-scope>"
# Expected: 403 Forbidden

# No JWT at all must be rejected on both endpoints:
curl -i https://localhost:5001/extrato?startDate=2026-07-01&endDate=2026-07-31
# Expected: 401 Unauthorized
```

## Validation checklist (maps to spec Success Criteria)

- [ ] SC-001: `/extrato` returns data without the caller ever presenting an MTLS certificate.
- [ ] SC-002: Requests without a valid, correctly-scoped JWT are rejected on both endpoints
      (401/403) before any Banco Inter call is made.
- [ ] SC-003: Successful responses (or errors) return within 5 seconds under normal conditions.
- [ ] SC-004: Simulate a Banco Inter outage (e.g., point `Inter:BaseUrl` at an unreachable host)
      and confirm both endpoints return a clear `502`-style error rather than hanging or timing
      out silently.
- [ ] SC-005: Inspect application logs after running the above and confirm no credential, token
      value, certificate material, or statement content appears in the clear.
