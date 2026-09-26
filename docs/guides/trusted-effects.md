# Trusted effect submission

`Hexalith.EventStore.Contracts` owns the version-one `EffectIdentityCodec` and
`EffectKindCatalog`. The identity tuple is tenant, source domain, source aggregate,
source EventStore envelope sequence, effect kind, target domain, target aggregate,
and immutable ordinal. Each text field is UTF-8 NFC with a four-byte big-endian
length; each integer is signed eight-byte big-endian. SHA-256 is rendered as 52
uppercase Crockford Base32 characters. Both `MessageId` and `IdempotencyKey`
must be `wrk-<EffectId>`.

The public SDK names are `TrustedEffectSubmission`, `TrustedEffectContext`,
`TrustedEffectResult`, and `ITrustedEffectSubmitter`. The HTTP submitter sends to
`POST /api/v1/trusted-effects`. The gateway derives the workload from its Dapr
internal authentication principal; a bearer principal carrying a
`dapr_caller_app_id` claim does not satisfy this endpoint's authentication
scheme. Production must also restrict direct gateway access and attest the
caller app through Dapr mTLS and deny-by-default ACLs, because the current
internal authentication handler reads the `dapr-caller-app-id` header. Outside
Development, internal authentication also requires the `dapr-api-token` header
to match the gateway's `APP_API_TOKEN` secret. The receiving Dapr sidecar must
be configured with the same app token; a caller-ID header alone is rejected.
Missing token configuration fails readiness outside Development. This is an
application-channel check, not deployment proof of mTLS or ACLs. The
gateway validates the short-lived asymmetric delegation against
the configured OIDC authority. The delegation must bind the complete identity
tuple, command type, server-derived canonical command digest, workload, purpose,
and causation. `EventStore:TrustedEffects:Authority:Rules` is an exact allow-list
of workload, purpose, target domain, and command type. Empty rules deny all.

The target aggregate actor revalidates admission before reading a receipt. The
gateway also signs its attested caller decision with a domain-separated HMAC;
the actor verifies that proof before reading receipt state. The proof binds the
tuple, command type, server semantic digest, workload, purpose, causation, and
delegation-token hash. Retained reader keys permit proof verification during
key rotation. A direct actor call without the gateway proof is denied. The
actor prepares admission without registering retention evidence, then validates
the gateway proof and its own target partition. The gateway registers lifecycle
evidence before signing that proof. After the target actor call returns, the
router asks the lifecycle to complete the registered turn. This ordering avoids
a cycle when tenant purge waits for the target actor to erase its evidence.
An invalid proof cannot mutate lifecycle evidence from the target actor or read a receipt. The
private receipt is staged in the same actor state batch as the first target
event range or no-op terminal outcome. Exact replay returns that receipt without
calling the domain handler. A collision records target-scoped quarantine
evidence and stops. Gateway status records and their expiry do not authorize
effect replay. Ordinary command submission rejects the `wrk-` namespace.

## Production admission gate

`ITrustedEffectRetentionGate` has no default registration. Admission therefore
fails closed until an implementation enforces retained source floors and one
source/target legal-hold and offboarding decision. The accountable data owner
must approve this durable receipt type and a restore drill must prove source
stream, target stream, receipt, collision evidence, audit, and tenant-key order
before non-synthetic shared data or real-data admission. The current actor and
gateway tests use synthetic data. No Works translator is changed by this API.

`ActorTrustedEffectSourceFloorProvider` reads the source aggregate's committed
`AggregateMetadata.RetainedFloor`. Event writes preserve that floor; legacy
untrimmed streams start at sequence one. A missing stream or corrupt floor
fails closed. `TrustedEffectRetentionGate` also reads the exact source envelope
and checks tenant, domain, aggregate, and sequence. EventStore has no partial
stream trim operation; any future trim must advance the floor atomically with
the retained stream evidence.

`ActorTrustedEffectJointRetentionPolicy` uses the tenant lifecycle's persisted
effect inventory to erase each registered source and target aggregate partition.
The target receipt and collision index is staged with those records. Each
aggregate erasure writes a durable progress marker before removing bounded
event batches, then removes stream metadata, snapshot, receipts, and collisions.
A retry resumes from that marker; a completed marker fences a target turn that
was queued before deletion. The lifecycle clears its effect inventory only
after every aggregate erasure completes. Legal hold prevents erasure. A failed
turn leaves the lifecycle purge-eligible for retry. The existing protected
tenant/key admission tombstones, directory aliases, and legacy inventory
entries are purged after the stream evidence and before lifecycle `Purged`.
Those records contain digest-key versions and protected digests. Shared
secret-store digest-key material is not a tenant-owned key and is not deleted
by one tenant's offboarding.

The gateway router reports a target result only after lifecycle completion. If
deletion and purge finish during an in-flight target turn, the router's later
completion fails closed. On deletion entry, the lifecycle first sends a signed,
purpose-separated deletion-fence capability to every registered source and target
partition, then commits its non-active state. The actor persists a deletion fence
before it can read a receipt or execute another trusted effect. A failed fence
leaves deletion entry uncommitted and any already-fenced partitions closed; retry
can finish the transition. Legal hold preserves the fence, so a previously signed
gateway proof cannot disclose a receipt during hold. A deletion-fence capability
cannot authorize erasure, and a purge capability cannot install a deletion fence.
The terminal erasure batch removes the deletion fence together with stream and
receipt evidence; its actor-local erasure marker prevents a queued target turn
from writing afterward. Production trusted-effect admission remains closed until the
Platform mTLS/ACL boundary, audit backend, owner approval, and restore drill
are proved.

These actor policies are available for an explicitly governed host but have no
production registration. `AddEventStoreTrustedEffectRetention()` still installs
only the gate; the host must bind the actor floor and joint policy explicitly.
Aggregate erasure fails before reading state unless the host supplies
`ITrustedEffectErasureAuthority`. EventStore's
`TrustedEffectErasureCapability` signs each partition request inside the
serialized lifecycle purge turn. Its five-minute HMAC binds the tenant,
aggregate partition, complete inventory digest, expected target effect IDs,
persisted deletion decision, and a random one-use nonce. The aggregate actor
validates the signature before any state access. The first durable erasure
progress write consumes the nonce; a failed purge followed by legal hold cannot
reuse that capability to continue an interrupted erasure. An existing receipt
without its actor-local index is rejected during erasure. Before admitting real
data, the rollout must prove no older unindexed receipts or collisions exist,
or migrate them under the same audited retention decision. A capability issued
before the first progress write still needs the production mTLS/ACL boundary to
prevent interception or replay within its short lifetime. The host must also
supply the privileged audit sink, attest the mTLS/ACL caller path, obtain
accountable data-owner approval, and complete the AD-28 restore drill.
The actor inventory covers registered trusted-effect source and target streams;
full tenant offboarding of unrelated streams and other durable catalogs remains
part of the broader AD-28 workflow.

The host must also provide `ITrustedEffectAuditSink`, an append-only privileged
audit implementation. Admission logs payload-free authorization or denial
metadata before any receipt access or target mutation. Evidence registration,
collision quarantine, and offboarding erasure require an audit append before
their writes. A missing
or failed sink closes those paths. The sink belongs outside ordinary application
logs and must retain authorization denial, quarantine, and erasure evidence
under the tenant retention decision. No default audit sink is registered.
These seams do not close the AD-28 owner approval or restore-drill gates.

The first registered producer must lock its effect family, ordinal, and golden
vectors before using this endpoint. Receipts and collision records are private
actor state, never domain events. Do not log command payloads or delegation
tokens. Release validation should record the package version, source SHA, build
and test commands, named public package-only probe, and restore evidence.
