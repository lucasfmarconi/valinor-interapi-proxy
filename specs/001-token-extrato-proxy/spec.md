# Feature Specification: Token and Extrato Proxy Endpoints

**Feature Branch**: `001-token-extrato-proxy`

**Created**: 2026-08-21

**Status**: Draft

**Input**: User description: "Proxy endpoints for Inter APIs: \"Token\" and \"Extrato\". The proxy sits behind JWT authentication and forwards authenticated, scoped requests to Banco Inter's Token (OAuth client-credentials token issuance) and Extrato (bank statement retrieval) APIs over MTLS, so that consumers who cannot establish MTLS connections themselves can still use these two Inter capabilities."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Retrieve Bank Statement via Proxy (Priority: P1)

A consumer application that has been granted access requests Banco Inter bank statement
(Extrato) data for a specified period through the proxy, so it can access banking transaction
data without ever needing to establish a direct mutual-TLS connection to Banco Inter.

**Why this priority**: Statement data is the core banking value consumers are after; it is the
primary business outcome the proxy exists to unlock, beyond simply solving the connectivity
problem.

**Independent Test**: Can be fully tested by presenting a JWT carrying the statement-read scope
together with a valid date range, and verifying the proxy returns the statement data supplied by
Banco Inter for that range.

**Acceptance Scenarios**:

1. **Given** a consumer holds a valid JWT with the statement-read scope, **When** it requests a
   bank statement for a valid date range, **Then** the proxy returns the statement data supplied
   by Banco Inter for that range.
2. **Given** a consumer holds a valid JWT that lacks the statement-read scope (e.g., it only has
   the token-issuance scope), **When** it requests a bank statement, **Then** the proxy denies the
   request and returns no statement data.
3. **Given** a consumer requests a bank statement for a date range Banco Inter does not support
   (e.g., exceeding the maximum allowed period), **When** the request is submitted, **Then** the
   proxy returns a clear validation error without exposing internal implementation details.
4. **Given** Banco Inter's statement service is unavailable or returns an error, **When** a
   consumer requests a statement, **Then** the proxy returns a clear upstream-error response
   without leaking internal implementation details.

---

### User Story 2 - Obtain Banco Inter Access Token via Proxy (Priority: P2)

A consumer application that has been granted access requests an access token from Banco Inter
through the proxy, so it can obtain the credential needed for further Inter operations without
ever establishing a direct mutual-TLS connection itself.

**Why this priority**: The Extrato capability manages its own Banco Inter access token
internally, so Token issuance is not a prerequisite for it. This capability instead serves
consumers who need a raw Banco Inter access token for other purposes (e.g., future Inter
operations not yet proxied); it ranks below Extrato because it is a standalone, lower-demand
capability rather than a blocking dependency.

**Independent Test**: Can be fully tested by presenting a JWT carrying the token-issuance scope
and verifying the proxy returns a valid Banco Inter access token, without the caller ever
supplying Inter credentials or a client certificate.

**Acceptance Scenarios**:

1. **Given** a consumer holds a valid JWT with the token-issuance scope, **When** it requests a
   token, **Then** the proxy returns a valid Banco Inter access token along with its expiry.
2. **Given** a consumer holds a valid JWT that lacks the token-issuance scope, **When** it
   requests a token, **Then** the proxy denies the request.
3. **Given** Banco Inter's token service is unavailable or rejects the proxy's own credentials,
   **When** a consumer requests a token, **Then** the proxy returns a clear upstream-error
   response without exposing Banco Inter credential details.

---

### Edge Cases

- What happens when the consumer's JWT is expired, malformed, or has an invalid signature? The
  request MUST be rejected as unauthenticated before it reaches Banco Inter.
- What happens when the proxy's own Banco Inter credentials (client id/secret, MTLS certificate)
  are invalid or expired? All proxied requests MUST fail with a clear upstream-configuration
  error, distinguishable from a caller-side error.
- What happens when Banco Inter rate-limits the proxy? The proxy MUST surface a rate-limited
  error back to the consumer rather than retrying silently or hanging.
- What happens when a consumer requests a statement date range that is only partially valid
  (e.g., start date valid, end date in the future)? The proxy MUST return a validation error
  identifying the invalid part of the range.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The proxy MUST require a valid JWT bearer token on every request to either the
  Token endpoint or the Extrato endpoint; no anonymous access is permitted to either.
- **FR-002**: The proxy MUST authorize the Token endpoint only for JWTs carrying a distinct
  token-issuance scope; possession of a valid JWT alone is not sufficient.
- **FR-003**: The proxy MUST authorize the Extrato endpoint only for JWTs carrying a distinct
  statement-read scope, separate from the token-issuance scope, so a consumer authorized for one
  capability is not automatically authorized for the other.
- **FR-004**: The proxy MUST forward token-issuance requests to Banco Inter's OAuth token service
  using the proxy's own Banco Inter credentials and mutual TLS, without ever requiring the
  consumer to supply Inter credentials or certificates.
- **FR-005**: The proxy MUST manage its own Banco Inter access token internally (obtaining,
  caching, and refreshing it as needed) so that Extrato requests succeed independently of any
  prior Token call by the consumer; the consumer MUST NOT be required to obtain or supply a
  Banco Inter access token to use the Extrato capability.
- **FR-006**: The proxy MUST return statement data to the consumer in the form supplied by Banco
  Inter for the requested account and date range.
- **FR-007**: The proxy MUST validate that a requested statement date range is within what Banco
  Inter supports before returning statement data, and MUST return a clear validation error for
  out-of-range requests.
- **FR-008**: The proxy MUST NOT expose Banco Inter's credentials, MTLS certificate material, or
  the proxy's own JWT signing keys in any response returned to a consumer.
- **FR-009**: The proxy MUST return a distinguishable error when Banco Inter's Token or Extrato
  service is unavailable or returns an error, without leaking internal implementation details to
  the consumer.
- **FR-010**: The proxy MUST record each Token and Extrato request with a correlation id, the
  authenticated caller's identity, and the outcome, without logging token values, credentials, or
  statement contents in the clear.

### Key Entities

- **Consumer (Client Application)**: An external system authenticated via JWT, authorized for the
  Token capability, the Extrato capability, or both, via distinct scopes.
- **Access Token**: The credential issued by Banco Inter through the proxy's Token capability;
  has a value and an expiry, and is required for further Inter operations.
- **Bank Statement (Extrato)**: The set of transaction entries for a given account and date
  range, as supplied by Banco Inter through the proxy's Extrato capability.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Consumers that previously could not integrate with Banco Inter due to the MTLS
  requirement can successfully retrieve a bank statement through the proxy on first attempt,
  without possessing any MTLS certificate themselves.
- **SC-002**: 100% of requests to either proxy endpoint that lack a valid, correctly-scoped JWT
  are denied before any Banco Inter data is returned.
- **SC-003**: Consumers receive a token or statement response, or a clear error, within 5 seconds
  under normal operating conditions.
- **SC-004**: When Banco Inter's services are unavailable, 100% of affected consumer requests
  receive a clear error response, with no silent failures or indefinite hangs.
- **SC-005**: Zero incidents of Banco Inter credentials, certificates, or signing keys being
  exposed to a consumer or found in accessible logs.

## Assumptions

- The proxy is initially configured for a single Banco Inter application/account; multi-account
  or multi-tenant support is out of scope for this feature.
- The proxy holds and manages Banco Inter's own client credentials and MTLS certificate itself;
  consumers never supply Inter-specific credentials or certificates.
- The identity/JWT-issuing system used to authenticate consumers already exists and is out of
  scope for this feature; this feature only validates and authorizes tokens issued by it.
- Extrato requests specify an explicit date range (start and end date); the proxy enforces
  whatever maximum period Banco Inter itself supports for statement retrieval.
- No caching or persistence of statement data is required for this feature; any internal reuse
  of a Banco Inter access token is limited to what is operationally necessary and is not
  user-facing.
- The Token and Extrato capabilities are independent from the consumer's perspective: the proxy
  manages its own Banco Inter access token internally for Extrato, so the Token capability exists
  to serve consumers who need a Banco Inter access token for purposes outside this feature's
  scope, not as a prerequisite step for Extrato.
- Banco Inter's Extrato API requires a checking-account identifier on every request, in addition
  to the statement date range. Consistent with the single-account assumption above, this
  identifier is proxy-side configuration, not a value the consumer supplies or chooses.
- The Banco Inter OAuth scope the proxy requests from Inter (for both the Token capability and
  its own internal Extrato token) is fixed, proxy-side configuration. The consumer's JWT scope
  determines which proxy capability they may call, but never influences which Inter-side
  permission is requested.
