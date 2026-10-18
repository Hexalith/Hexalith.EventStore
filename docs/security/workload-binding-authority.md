# Production workload resource-binding authority

The opt-in EventStore HTTP host can now issue short-lived, per-request RSA
workload assertions after authenticating a protected client and checking a
current server-configured grant. This is technical capability only. No production
Timesheets signer, client grant, endpoint or deployment is enrolled by this change.
The sole owner may review this project document; operational facts still require
configuration and observations.

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

The host maps `/workload-authority/token`, `/workload-authority/jwks` and
`/workload-authority/.well-known/openid-configuration` only when
`Authentication:WorkloadAuthority:Enabled=true`. Its default is disabled.
The configured issuer path must match the mapped prefix at startup. The standard
EventStore host maps `/workload-authority`; a different issuer path is rejected.
An explicitly composed technical host may map the matching custom prefix.
Changing the issuer path after mapping fails closed rather than advertising
nonexistent endpoints. Discovery, verification keys and token requests each use
one fixed, bounded partition in both host global limiters, isolated from unrelated
anonymous traffic. These three partitions attest no client identity: form fields,
request paths and user subjects cannot create additional authority buckets.
Production token requests require HTTPS at the application. TLS termination must
be configured directly in Kestrel or through an already approved trusted proxy;
never enable unrestricted forwarded-header trust. Credentials stay in a form
body; token responses use `Cache-Control: no-store`. Form size, field set,
duplicate fields and profile structure are bounded and checked before issuance.
The endpoint authenticates its own client credential rather than user cookies.

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

## Exact production inputs and operational boundary

The target deployment preparation must bind these inputs before any live action:

- The selected environment/namespace and immutable EventStore artifact containing
  this implementation, plus an exact broker HTTPS issuer and the existing
  validated TLS route/client trust. A public application JWT issuer and this
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

Disabling the feature removes the mapped broker on the next host rollout.
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

## Validation and evidence

On 2026-10-18 the user selected option **1**, approving retirement of exactly
three preserved `WorkloadBindingAuthorityTests.cs` snapshots and two historical
Server reports. The [authority overlay](../../_bmad-output/implementation-artifacts/evidence/6-4-historical-retirement-20261018/authority-overlay.json)
withdraws present qualification authority from the dependent preparation and
review results (187/187 and the two 83-case runs). Their original source bytes,
receipts and dated outcomes remain preserved. Those runs do not establish
current-source validation, replacement evidence or deployed acceptance.

Production-mode tests use a temporary self-signed synthetic certificate and an
in-memory HTTP transport. They exercise the actual broker, outbound client,
cryptographic validation and mapped endpoint boundaries. They are local software
validation only, not real TLS/client identity, current operational grants or
runtime acceptance. Story 3.6 remains incomplete until its deployed acceptance
records pass. Current bounded native rights checks are retained separately under
`docs/evidence/story-3-6/20261017t142649z-production-authority-preparation/`.
