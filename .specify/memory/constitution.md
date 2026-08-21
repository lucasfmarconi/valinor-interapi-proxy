<!--
Sync Impact Report
Version change: [TEMPLATE] → 1.0.0 (initial ratification)
Modified principles: n/a (first fill of template placeholders)
Added sections:
  - I. MTLS Isolation Boundary
  - II. JWT-Gated, Least-Privilege Endpoints (NON-NEGOTIABLE)
  - III. Explicit Endpoint Allowlist (No Generic Passthrough)
  - IV. Secret & Credential Hygiene
  - V. Observability & Traceability
  - Technology & Security Constraints
  - Development Workflow & Quality Gates
  - Governance
Removed sections: none (template placeholders only)
Templates requiring updates:
  - .specify/templates/plan-template.md ✅ compatible (generic "Gates determined based on
    constitution file" placeholder already defers to this document; no edit required)
  - .specify/templates/spec-template.md ✅ compatible (no constitution-specific references)
  - .specify/templates/tasks-template.md ✅ compatible (no constitution-specific references)
  - .specify/templates/checklist-template.md ✅ compatible (no constitution-specific references)
  - CLAUDE.md ✅ compatible (defers to "current plan", no principle names embedded)
Follow-up TODOs: none
-->

# Valinor InterAPI Proxy Constitution

## Core Principles

### I. MTLS Isolation Boundary

The proxy is the sole component permitted to establish mutual TLS connections to Banco Inter
APIs. Consumers of this proxy MUST NOT be required to possess, handle, configure, or transmit
MTLS client certificates. Every outbound call to an Inter API MUST use the proxy's own dedicated
MTLS client certificate, loaded exclusively from a secure certificate store (OS/platform
certificate store, mounted secret volume, or secrets manager) — never embedded in source control,
container images, or plaintext environment variables.

Rationale: eliminating the consumer's need to satisfy Inter's MTLS requirement is the entire
reason this proxy exists; any leakage of that responsibility back to consumers defeats the
project's purpose.

### II. JWT-Gated, Least-Privilege Endpoints (NON-NEGOTIABLE)

Every proxied endpoint MUST require a valid JWT bearer token, issued by an approved identity
provider and validated for signature, issuer, audience, and expiry, before any request is
forwarded to Inter. A valid JWT alone is NOT sufficient authorization: each endpoint MUST enforce
the minimum claim/scope required for that specific operation (e.g., a distinct scope for Token
issuance versus a distinct scope for Extrato retrieval). No endpoint may be exposed anonymously,
and no endpoint may accept a token whose scope does not explicitly cover the operation requested.

Rationale: least privilege was called out as a hard project requirement; scoping authorization
per endpoint prevents a token minted for one purpose (e.g., reading a statement) from being reused
to perform an unrelated, potentially more sensitive operation (e.g., issuing tokens).

### III. Explicit Endpoint Allowlist (No Generic Passthrough)

The proxy exposes only endpoints that have been explicitly modeled, specified, and reviewed.
Initial scope is limited to exactly two proxied capabilities: Token and Extrato. A generic or
catch-all passthrough that forwards arbitrary Inter API paths/methods is prohibited. Introducing
a new proxied endpoint requires a spec update and an explicit least-privilege scope definition
before implementation begins.

Rationale: an explicit allowlist keeps the attack surface bounded and keeps Principle II
enforceable — every exposed endpoint must always be traceable to a specific, reviewed scope.

### IV. Secret & Credential Hygiene

Inter API credentials (client id/secret, MTLS certificate and private key, JWT signing/validation
keys) MUST be sourced from secure configuration providers (.NET User Secrets in local development;
environment-injected secrets or a managed secrets store such as Key Vault in production) and MUST
NEVER be committed to source control. Application logs MUST redact bearer tokens, certificate
material, and Extrato/banking payload contents; only metadata needed for correlation and
diagnostics (endpoint name, correlation id, status code, timing) may be logged in the clear.

Rationale: this proxy centralizes highly sensitive banking credentials and financial data on
behalf of every consumer, which raises the blast radius of any credential or data leak far above
a typical service.

### V. Observability & Traceability

Every proxied request MUST be logged with a correlation/trace id, the target Inter endpoint, the
authenticated caller's identity (subject claim), and the outcome (HTTP status code), using
structured logging. Failures — including JWT validation failures, authorization/scope denials,
and MTLS handshake or Inter API failures — MUST be distinguishable from one another in logs
without requiring binary log inspection.

Rationale: the proxy sits on the critical path for banking operations and access is limited by
scoped claims; when something fails or is denied, on-call engineers and auditors must be able to
determine why quickly and without exposing sensitive data.

## Technology & Security Constraints

- **Platform**: .NET 10 ASP.NET Core Web API. Endpoint style (controllers vs. minimal APIs) is
  decided in the implementation plan, not this constitution.
- **Inbound authentication**: JWT bearer authentication middleware (e.g.
  `Microsoft.AspNetCore.Authentication.JwtBearer`) validating signature, issuer, audience, and
  expiry; ASP.NET Core authorization policies MUST map one-to-one with the scopes defined for each
  proxied endpoint per Principle II.
- **Outbound calls to Inter**: performed only through a dedicated, testable Inter API client
  abstraction that owns MTLS `HttpClient` configuration; endpoint handlers MUST NOT construct raw
  `HttpClient` instances or manage certificates directly.
- **Statelessness**: the proxy MUST remain stateless with respect to banking data — it forwards
  and translates requests/responses; it MUST NOT persist Extrato or Token payloads unless a future
  spec explicitly introduces caching with its own justification and data-retention rules.
- **No plaintext secrets**: configuration files checked into source control MUST NOT contain real
  client secrets, certificates, private keys, or signing keys — only placeholders or references to
  a secrets provider.

## Development Workflow & Quality Gates

- Every new or modified proxied endpoint requires: (1) a spec entry naming the Inter API it
  proxies and the exact JWT scope/claim required, (2) an authorization test proving requests with
  no token, an invalid token, or a wrongly-scoped token are rejected, and (3) an integration test
  against a mocked Inter API covering both success and failure (auth, MTLS, upstream error) paths.
- The plan's Constitution Check gate MUST verify, before implementation: JWT protection is present
  on the endpoint, the endpoint's authorization scope is least-privilege and endpoint-specific, no
  plaintext secrets were introduced, and all outbound Inter calls flow through the shared MTLS
  client abstraction.
- Code review MUST reject any change that bypasses JWT authentication, widens a scope beyond what
  an endpoint needs, or introduces a generic/catch-all Inter route.

## Governance

This constitution supersedes any conflicting team practice or ad-hoc convention for this project.
Amendments require: a documented rationale for the change, an explicit version bump following the
semantic versioning policy below, and a review of dependent artifacts (`plan-template.md`,
`spec-template.md`, `tasks-template.md`, and any agent guidance files) for needed updates.

**Versioning policy**:
- **MAJOR**: backward-incompatible removal or redefinition of a principle (e.g., dropping the
  JWT-gating or MTLS-isolation requirement).
- **MINOR**: a new principle or materially expanded governance/constraint section is added.
- **PATCH**: wording clarifications, typo fixes, or non-semantic refinements.

All pull requests and reviews MUST verify compliance with these principles before merge. Any
complexity that appears to violate a principle (e.g., a proxied endpoint without a scoped
authorization policy) must be justified in the plan's Complexity Tracking section or the change
must be redesigned to comply.

**Version**: 1.0.0 | **Ratified**: 2026-08-21 | **Last Amended**: 2026-08-21
