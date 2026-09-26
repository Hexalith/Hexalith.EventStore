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
internal authentication handler reads the `dapr-caller-app-id` header. The
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

The optional `AddEventStoreTrustedEffectRetention()` registration installs a
source-evidence gate only after the host supplies
`ITrustedEffectSourceFloorProvider` and `ITrustedEffectJointRetentionPolicy`.
It rejects missing or invalid floors, reads the exact source envelope from its
actor, checks tenant/domain/aggregate/sequence, and rejects a tenant whose
idempotency lifecycle has entered legal hold or deletion. Neither dependency has
a default implementation. A deployment must bind the floor to authoritative
stream retention and make source stream, target stream, receipt, and collision
evidence subject to one audited tenant offboarding decision before registering
this gate. The present lifecycle actor only purges idempotency references; it
does not erase trusted effect evidence. This registration alone does not close
the AD-28 owner, privileged-audit, or restore gates.

The first registered producer must lock its effect family, ordinal, and golden
vectors before using this endpoint. Receipts and collision records are private
actor state, never domain events. Do not log command payloads or delegation
tokens. Release validation should record the package version, source SHA, build
and test commands, named public package-only probe, and restore evidence.
