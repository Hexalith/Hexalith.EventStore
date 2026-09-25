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
caller principal and validates the short-lived asymmetric delegation against
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

The first registered producer must lock its effect family, ordinal, and golden
vectors before using this endpoint. Receipts and collision records are private
actor state, never domain events. Do not log command payloads or delegation
tokens. Release validation should record the package version, source SHA, build
and test commands, named public package-only probe, and restore evidence.
