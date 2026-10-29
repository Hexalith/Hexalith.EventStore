# Workload resource-binding authority

Conforming EventStore HTTP hosts refuse embedded workload-authority activation in
every environment. Production and Staging cannot enable or map the embedded
issuer. No anonymous issuer exception is added to the shared probes-only host
boundary. Production workload issuance belongs to the external platform/identity
authority owner, including signer custody, grants, trust publication, rotation,
and deployment approval; this repository supplies receiver and outbound-client
contracts, not production enrollment authority.

The embedded implementation survives only as an explicitly nonconforming
Development fixture with both `Authentication:WorkloadAuthority:Enabled=true` and
`Authentication:WorkloadAuthority:NonconformingDevelopmentFixture=true`. Such a
fixture cannot enroll shared conforming HTTP-host security and supplies no host
conformance or production authority. Its HTTPS bootstrap and synthetic signing
material exist for bounded protocol/cryptographic tests.

The token request uses OAuth client credentials plus the bounded
[authorization_details profile](https://www.rfc-editor.org/rfc/rfc9396.html).
Profile `https://hexalith.com/security/workload-binding/v1` carries one audience,
one operation, and exact `eventstore:bound-*` claims. Tenant and domain are
mandatory when any binding is present. The broker checks one unambiguous current
grant, a constant-time comparison with its configured secret digest, the full
requested scope, and every exact permitted binding value. Every binding key in a
grant is mandatory for a bound request; omitting an actor required by that grant
is denied. A request never chooses the issuing workload. Actor binding additionally requires an explicit actor value
in the server grant; caller claims do not establish it. No wildcard expansion is
implemented. Grant revision and identity accompany the signed assertion.

### Exact query authority capability

The v1 profile also supports the explicit, default-off grant capability
`AllowQueryAuthorityDigest`. For `domain-service:query`, an enabled grant requires
exactly these request bindings: `eventstore:bound-tenant`,
`eventstore:bound-domain`, and `eventstore:bound-query-authority-v1`. The first two
must match the grant's exact allowlists. The digest is exactly 64 uppercase
hexadecimal SHA-256 characters and covers the complete Web-JSON `QueryEnvelope`.
The grant contains only the Tenant/domain allowlists; it does not pre-enumerate
per-request digests. Unknown, additional, missing, wildcard or malformed query
bindings fail closed. The capability cannot authorize a digest for any other
operation. Other exact-value binding families retain their existing policy.
`AllowUnbound` does not waive the digest requirement for the capability's query
operation. The configured audience, workload and credentials remain independent
checks. No client is enrolled by adding this capability to the source.

The receiving SDK requires all three exact signed claims before exposing
`VerifiedQueryCaller`. For an owner query, the gateway validates the nested
originating assertion against the requesting workload's audience and the exact
originating envelope before constructing
`QueryCallerAuthority.OriginatingContext` (`OriginatingQueryContext`). This
context is covered by the owner-query envelope digest and is available through
`VerifiedQueryCaller.OriginatingContext` only after receiving SDK verification.
Owner allowlists, requester proof checks and current actor/resource authorization
still apply. A valid digest does not authorize a resource read.

| Originating context field | Authenticated source on the original gateway query |
| --- | --- |
| `OriginalActorId` | `sub`, through `DualPrincipalClaimsHelper` / `OriginalActorId` |
| `AuthenticatedWorkloadId` | Existing `azp`, then `client_id`, then first `aud` resolution |
| `IsDelegated` | Existing `act` presence or differing `azp`/`client_id` rule |
| `DelegationId` | Bounded, unambiguous RFC 8693 `act.sub` |
| `Scopes` | Existing whitespace-split `scope`, then `scp` |
| `Audience` | Existing authenticated `aud` values |

Absent collections remain absent; context collections are defensively copied.
The inherited `DualPrincipalClaimsHelper` extraction preserves at most 64 scope
entries and 64 audience entries, truncating each retained value to 512 characters
and discarding entries beyond the count limit. These existing routing-metadata
rules are unchanged; originating context captures the resulting bounded values.
Scopes and audiences do not become distinct permission authority.
The owner-hop `Query.AuthenticatedWorkloadId` remains the requesting workload,
while `Query.Audience` comes from the gateway's validated named receiver options.
The transport principal at the owner remains the authenticated gateway workload
and intentionally contains no copied user `aud` claims. Original delegation and
scopes are kept in the separate context. Roles are not forwarded to the owner;
the existing distinct permission claims still are. JSON and DataContract omit a
null new context, preserving the legacy authority/proof bytes. Existing callers
without a context retain their prior behavior.

Owner-query and stream-read proofs accept aggregate IDs up to 256 characters.
The independent 200-character principal and proof Tenant/domain limits, and the
aggregate identity's 64-character Tenant/domain validation, remain unchanged.

Only the explicitly nonconforming Development fixture may map
`/workload-authority/token`, `/workload-authority/jwks`, and
`/workload-authority/.well-known/openid-configuration` through the existing
registration/mapping helpers. The configured HTTPS issuer path must match the
mapped prefix; changing it after mapping fails closed. A conforming host rejects
this activation even when the fixture flag is set. No new enrollment protocol is
introduced.

Fixture token requests require HTTPS at the application. Credentials stay in a
bounded form body; exact fields, duplicate fields, profile structure, and client
credentials are checked before issuance. Token responses use
`Cache-Control: no-store`. Production clients instead use the externally owned
HTTPS authority through the same validated outbound contract.

A protected PKCS#12 certificate supplies RSA signing (at least 2048 bits,
`RS256`, digital-signature usage, current validity). The application never generates
or enrolls a signer automatically. Public metadata exposes only a public RSA JWKS
and an explicitly configured issuer, never a request-supplied Host. The signer
must already be approved and its configuration delivered using the owning
platform's protected secret mechanism with a read-only mount. Runtime private
keys, passwords and client secrets must never appear in repository files,
command-line arguments, evidence or logs.

The client opt-in is
`Authentication:WorkloadIssuer:EnableResourceBindingRequests=true`, alongside
existing `Workload`, `ClientId`, protected `ClientSecret` and HTTPS `TokenEndpoint`
settings. The dedicated `Authentication:WorkloadJwtBearer` contract names the actual
workload `Authority`, identical `Issuer`, audience and `AllowedAlgorithms=[RS256]`.
The public `Authentication:JwtBearer` contract continues to validate user tokens
using its existing issuer. Issuers and workload receivers share the dedicated
named contract. Only when the dedicated section is absent does workload validation
fall back to the existing shared public contract, preserving earlier deployments.
A partially configured dedicated section never inherits public signing keys or
issuer values; it fails closed.
Before attaching a bound token, the client independently validates the configured
issuer's JWKS signature, lifetime, exact audience/operation/caller and the full
binding claim set. Validated discovery/JWKS share one trust-keyed snapshot for at
most five minutes; trust configuration changes invalidate it. Expired or changed
trust never falls back to the old snapshot. A same-authority key rotation may deny
new tokens until that bounded refresh; no rejected token is reused. Discovered
token destinations and ordinary token cache entries are isolated by their trust
and credential configuration, so authority changes do not reuse old destinations.
Token responses are consumed with headers-only completion and a hard 64 KiB
streaming limit, including responses without Content-Length. Receivers still independently validate all assertions and
server-side authorization. Clients explicitly enrolled in this profile use it
for unbound internal requests too; the grant must separately enable `AllowUnbound`
for those requests. Profile tokens are never cached, so current grant
changes apply on every acquisition from the current configuration snapshot.
A provider must actually reload for configuration changes to take effect;
Kubernetes environment-variable changes require a pod rollout. The broker does
not claim an external live revocation feed. Removing/revoking a grant in the
loaded configuration stops new issuance;
already issued tokens can remain valid for at most 120 seconds, bounded further
by grant and signing-certificate expiry. Emergency retirement must block the
client at the receiver/network or retire the trust key as well; merely removing
a grant cannot retroactively invalidate a JWT.

## External production authority inputs and operational boundary

The external authority owner must establish these inputs before any live action; they do not authorize activating the embedded EventStore issuer:

- The selected environment/namespace, immutable EventStore receiver artifact,
  and the external authority's exact HTTPS issuer and existing validated TLS
  route/client trust. A public application JWT issuer and this
  workload trust must not be silently interchanged.
- An authority-approved key id and protected existing signer reference,
  certificate public fingerprint/JWKS, custody and actual validity/revocation
  facts. No self-declared project document proves current PKI status.
- A protected high-entropy client secret/reference and its SHA-256 digest;
  an approved `GrantId`, positive `Revision`, `ClientId`, server-owned `Workload`,
  exact `Audience`, `Operations`, exact `Bindings` allowlists, actual `NotBefore`
  and `ExpiresAt`, and `Revoked=false`. `AllowUnbound` is false by default.
- Receiver allowlists, tenant/domain/operation policies and all other required
  EventStore clients. A Timesheets-only enrollment cannot establish that other
  workloads are authorized; each required client needs its own scoped grant.
- An exact local manifest/configuration and protected delivery command, target,
  expected effects, post-apply read-only verification and rollback/disable
  procedure for owner approval. This source change authorizes no live action.

Disabling the fixture removes its bootstrap on the next fixture restart. External production issuer rollback remains the authority owner's responsibility.
Before rollback, retain immutable artifact/configuration evidence, revoke the
client/receiver access if required, and allow the maximum assertion lifetime to
elapse. Never delete EventStore data or migrate projection state as an issuer
rollback. Changing grant files/configuration is itself an operational permission
change and remains subject to approval.

## Timesheets legacy inventory and maintenance

The runtime migration services retain `DenyAllMagicLinkIndexMigrationAuthority`
and `UnavailableMagicLinkV1VerifiedInventoryProvider`. The broker supplies neither
port and does not authorize migration. Native Kubernetes rights are separate from
Timesheets domain and migration authority.

An absent identifiable Timesheets deployment does not prove no historic Timesheets
issuance exists. A bounded namespace census cannot certify an empty global legacy
inventory. Before any legacy migration, the owning installation/source custodian
must establish complete installation coverage, an immutable full cross-tenant
source cut (including terminal capabilities, omitted collisions and independently
verified erasure exclusions), owner source continuity, pending-delivery recovery
and enforced source-writer/delivery quiescence through promotion or abort. The
existing EventStore-backed migration already rechecks these provider receipts and
current authority; it must stay closed until those factual inputs exist. No
checkpoint, legacy fold, empty receipt or maintenance result is fabricated here.
A newly isolated, never-used installation may use its actual initial state only
after the broader installation/history coverage is established separately.

## Validation

Earlier Story 3.6 preparation and review runs (187/187 and two 83-case runs)
carry no present qualification authority: on 2026-10-18 the user approved
retiring the source snapshots and reports they depended on. Those runs do not
establish current-source validation or deployed acceptance; proof is the
current tests and CI.

Explicitly nonconforming Development issuer tests use a temporary self-signed
synthetic certificate and an in-memory HTTP transport; independent receivers retain
Production JWT validation controls. Production/Staging embedded-activation refusal
is tested directly. They exercise the actual broker, outbound client,
cryptographic validation and mapped endpoint boundaries. They are local software
validation only, not real TLS/client identity, current operational grants or
runtime acceptance. Story 3.6 remains incomplete until its deployed acceptance
records pass.
