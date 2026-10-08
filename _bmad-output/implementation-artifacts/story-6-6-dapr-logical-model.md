# Dapr actor logical model v1

This companion selects the remaining codec choices authorized by the
[selected-model amendment](story-6-6-dapr-logical-model-amendment.md). It does not
grant catalog, peer, production-key, component or activation authority. Historical
AD-13 records and `StoredDigest` retain their original meanings.

All separators below include their final NUL byte. U is strict UTF-8 preceded by
u32 byte length; B is u32 length and exact bytes; B32 is exactly 32 unframed bytes;
I/N are signed big-endian i32/i64. T is UTC ticks followed by the original signed
i16 offset in minutes. O(X) is 00 for absence or 01 followed by X. Presence is
significant, including null versus empty extensions and null versus empty
optional strings. This is logical null/value presence: typed actor readback cannot
establish original JSON property absence versus explicit null. Scalars and tags
are never normalized. SHA256 means exact
SHA-256 over the complete preimage. No historical provider assurance is supplied.

## Source binding

`HX-EV-DAPR-SOURCE-1\0 || 01 || 0013` has tags 01..13 in order:

| Tag | Encoding and meaning |
| --- | --- |
| 01..03 | U actual Dapr application ID, U actual namespace, U registered source actor type |
| 04 | U canonical actor ID from `AggregateIdentity.ActorId` |
| 05..08 | U tenant, U domain, U aggregate ID, U aggregate type |
| 09..0b | N fixed actor head, N target (0..head), N retained floor (must be 1 for initial event-only replay) |
| 0c..0d | U exact `EventStreamKeyPrefix`, U exact `MetadataKey` |
| 0e | U literal `aggregate-identity-decimal-v1` |
| 0f | B32 SHA-256 of exact deployment-pinned source configuration bytes |
| 10 | U literal `eventstore.logical-payload.v1` |
| 11..12 | O(U) observed actor metadata ETag, T observed metadata LastModified |
| 13 | I metadata-present discriminator, exactly 0 (absent actor value) or 1 (present actor value) |

`SourceBindingHash = SHA256(exact source record)`. Application/namespace/type and
configuration come from trusted host composition, never a tenant DTO. The source
reads only its owning `IActorStateManager`; event keys are prefix plus signed
sequence in invariant decimal. A varying event key is excluded from this range
binding. The same source record binds every page in an operation. Every source
observation compares head/floor/ETag/LastModified and presence with this binding.
An absent metadata record is allowed only for head/target 0, floor 1, absent ETag
and Unix epoch LastModified. A changed observation restarts the operation.

The existing application digest codec is preserved. Every admitted readable
payload is checked against a same-save digest when present; metadata V2 without
it refuses. Historical V1 absence is bound below, while its logical digest is
computed over the admitted current application bytes using the same existing
codec. This computed value is current logical readback, not a historical witness.

## Consumed metadata

`ConsumedMetadataHash = SHA256("HX-EV-DAPR-CONSUMED-1\0" || 01 ||` the following
ordered primitives `)`, without tags or a count:

U MessageId, U AggregateId, U AggregateType, U TenantId, U Domain, N sequence,
N globalPosition, T timestamp, U correlationId, O(U) causationId, O(U) userId,
O(U) domainServiceVersion, U stored event type, I metadataVersion, U stored
serialization format, O(U) stored canonical type, O(I) stored payload version,
O(U) exact stored same-save digest string, O(extensions), U readable application
format, B32 ApplicationLogicalDigest, B32 SourceBindingHash.

Present extensions encode u32 count followed by pairs U exact key/U exact value,
sorted by unsigned strict UTF-8 key bytes (ordinal UTF-8 byte ordering), rejecting
duplicate keys. The entire consumed preimage is at most 512 KiB. The logical
digest covers application bytes; this separate hash binds all consumed metadata,
including aggregate type, offset, protection extensions and optional presence.

## Route and prefix

Route: `HX-EV-DAPR-ROUTE-1\0 || 01 || 0014`, ascending tags 01..14. Its first 18
fields use the historical scalar order: B32 ApplicationLogicalDigest; U tenant;
U domain; U aggregate ID; U aggregate type; N sequence; U MessageId; U stored
event type; I stored metadata version; O(U) stored canonical type; O(I) stored
payload version; U stored format; U target canonical type; I target version;
B32 current registry fingerprint; B32 effective payload SHA-256; U effective
format; B32 ConsumedMetadataHash. Tag 13 is B32 SourceBindingHash and tag 14 is
U literal `dapr-actor-logical-v1`. Encoded route limit is 1 MiB.

Prefix: `HX-EV-DAPR-PREFIX-1\0 || 01 || 0011`, ascending tags 01..11. Tags 01..0c
are U tenant/domain/aggregate ID/aggregate type; N start/end/head/target; I count;
B32 OrderedLogicalDigestListHash; B32 cumulative logical accumulator; B32 current
registry fingerprint. Tags 0d..0f are O(B32) and MUST be absent (checkpoint,
snapshot and snapshot accumulator remain unavailable). Tags 10/11 are B32
SourceBindingHash and U literal `dapr-actor-logical-v1`. Limit is 1 MiB.

For a nonempty page, 1 <= start <= end <= target <= head, count=end-start+1 <=256.
The first page starts at 1; each successor is derived from the committed operation
ledger end, without incrementing long.MaxValue. Count zero has start=1/end=0 and
target=0 (head may be zero or positive); no routes or anchors. A terminal nonzero
target is completed by its last nonempty page, without a sentinel successor.

## Lists and accumulator

`OrderedLogicalDigestListHash = SHA256("HX-EV-DAPR-PREFIX-LIST-1\0" || 01 ||
B32 SourceBindingHash || U model ID || u32 count || ordered pairs)`; each pair is
N sequence || B32 ApplicationLogicalDigest, strictly contiguous and positive.

`A0 = SHA256("HX-EV-DAPR-ACC-GENESIS-1\0" || 01 || B32 SourceBindingHash ||
U model ID || B32 current RegistryFingerprint)`.

`Ai = SHA256("HX-EV-DAPR-ACC-STEP-1\0" || 01 || B32 SourceBindingHash ||
U model ID || B32 current RegistryFingerprint || B32 A(i-1) || N sequence ||
B32 ApplicationLogicalDigest)`. Each successor sequence comes from an admitted
ledger predecessor. A caller hash/token cannot admit prior progress.

## Signatures, ownership and controls

Reuse purposes 01/03 with the existing exact `HX-EV-SIG-1` input and P-256,
SHA-256, 64-byte P1363 signatures. A separate explicit logical-model trust object
pins current key ID, exact SPKI, domain, model ID, registry fingerprint, validity
interval and capability-loss scope. Verify all pins and current validity for each
claim, then decode its exact new separator/schema. An old purpose-01/03 trust
configuration never implicitly supplies this object. Production issuance and
registration remain unavailable. Local generated keys qualify local tests only.

The source produces private logical pages through the existing shared evolution
service. It rechecks the complete addressed source and capability before signing
and before returning. The dedicated replay-operation actor owns its stable
tenant/operation identity, owner generation/fence, immutable page ledger,
response blob and final result. It stages these application keys in one actor
save. AggregateActor retains exclusive aggregate/event/snapshot/outbox authority.
Source reads and operation writes are a recoverable cross-owner sequence.

Exact retry returns the retained response only after fresh operation-state logical
readback and current source/trust/capability verification. Ambiguous save clears
the actor state cache and reads all participant values with an independent bounded
recovery token. Exact expected participants prove commit; exact predecessor and
absent new participants prove no commit; anything else is indeterminate. No
continuation is admitted while uncertainty remains. Takeover increases generation;
stale generations cannot create new pages. Pre-save cancellation stages no durable
effect; post-save cancellation reconciles committed truth, without repeating Apply.

Independent Python vectors in `scripts/verify-dapr-logical-model-vectors.py` retain
exact source/metadata/list/genesis/step/route/prefix preimages in
`evidence/story-6-6/dapr-logical-model-2026-10-08/vectors.json`. They use Python
struct/hashlib only and are recomputed independently of the production codecs.
Fixture scope is local; no vector grants source or production authority.
