---
title: 'DAPR Global Event Ordering v2: Composite Shards'
type: 'architecture-specification'
created: '2026-08-27'
status: 'awaiting-operator'
schema_version: 2
predecessor_path: '_bmad-output/implementation-artifacts/spec-dapr-global-event-ordering.md'
predecessor_blob: '4c9edb37a8616aa373bd0054057c9e8eace6e0fa'
predecessor_sha256: '4bcff794a3c926f45e790ce462d562799db998feeab24aab1527ac6cf9ef1893'
normative_sha256: '2310f121dc48f59713a9c6bbc6ffe2e63be374d4d8ecc6e8d710a0d9cf3674de'
approval_state: 'absent'
implementation_authorized: false
---

# DAPR Global Event Ordering v2: Composite Shards

This is a successor specification, not a runtime change. The v1 allocator and
the frozen predecessor remain the only production authority. The composite
selection below is conditional on the capacity, bootstrap, committed-source,
and approval gates in this document. No agent-authored status, digest, evidence,
or approval record grants implementation, migration, deployment, or cutover
authority.

<!-- HX-GPOS-V2-NORMATIVE-BEGIN -->
# Normative contract

The key words MUST, MUST NOT, REQUIRED, SHALL, SHALL NOT, SHOULD, SHOULD NOT,
and MAY are normative. Failure to parse, authenticate, reproduce, or positively
prove a required fact fails closed. `durable v2 write` and `durable v2 event`
both mean the first successful durable production reservation evidence in a
pair-local reservation transaction; provisioning, shadow traffic, and catalog
metadata are not durable v2 writes.

## 1. Authority, scope, and predecessor identity

The protected predecessor is exactly:

| Identity | Required value |
|---|---|
| Baseline commit | `5ddda34f2ff0ffb0f72a60c44b265f2e4838a332` |
| Git blob | `4c9edb37a8616aa373bd0054057c9e8eace6e0fa` |
| Complete file SHA-256 | `4bcff794a3c926f45e790ce462d562799db998feeab24aab1527ac6cf9ef1893` |
| Frozen inner bytes SHA-256 | `90be324c35d1545fd7c4dd53393ef27b08d2e6a3891d1bc9c6f38c9145740c10` |
| Complete frozen element SHA-256 | `c827761ba1f58aa6fde85ca8acedfdfdcc5097cbcbd470d2887a1e4d073d5d2c` |
| Byte/range convention | UTF-8 LF bytes; `L` is one-based; `Bstart-Bend` is zero-based, start-inclusive, end-exclusive within the line without LF |

An implementation proposal MUST first reproduce all five identities. Any
predecessor byte drift invalidates this successor and requires renegotiation.
This specification does not edit v1 history. Existing positive v1
`globalPosition` values remain immutable scalar identities within scheme
`global-v1`; historical `MessageId`, CloudEvent id, aggregate identity,
aggregate sequence, payload, and timestamp bytes remain immutable.

### 1.1 Exact disposition of every frozen predecessor clause

The validator MUST parse these rows only from the bytes between the normative
markers. A row whose first cell matches `V1-[A-Z]+-[0-9]{2}` outside that byte
slice is invalid. For each row it MUST extract the declared predecessor byte
range, recompute SHA-256 over exactly that range, and require the stated digest.

| Clause ID | Exact predecessor range | Source bytes SHA-256 | Disposition |
|---|---|---|---|
| `V1-PROBLEM-01` | `L15:B13-B152` | `0c68cd6d7d0f2c094d287ed44055803615d70374d5fa6896e48907e8979bd427` | Retained for v1 history; superseded for new v2 writes by the tagged composite position contract in sections 4 and 5. |
| `V1-PROBLEM-02` | `L15:B153-B317` | `6edf7a21a1cd7be910fa0305694e45c85f7812547466a48134219fc3f7571f83` | Retained without amendment: `MessageId` remains the stable CloudEvent deduplication identity. |
| `V1-APPROACH-01` | `L17:B14-B143` | `69485cd508cd8b029e17f3fb7bc547214d6abac5ac19983a00036f192ef9af5b` | Superseded for v2 by pair-local tenant+domain allocators; the v1 single actor remains authoritative until irreversible cutover. |
| `V1-APPROACH-02` | `L17:B145-B276` | `292f6a8c92901b8613d1ca1f2ec1a5835d5f32489864f782f31880bbe8a10803` | Amended: each v2 position is an allocation label only, while CloudEvent id remains the persisted `MessageId`. |
| `V1-APPROACH-03` | `L17:B277-B384` | `cba5cd2562f1295274d034ee68c5caad2012d796ebc444e06538a1b553e918f1` | Retained: retries replay the original complete result and cannot acquire a different reservation. |
| `V1-ALWAYS-01` | `L21:B12-B78` | `24f68313de132e96bc578232fb45bb0e0c9b0281a4fc8fd3a6b7b187076c80c1` | Retained exactly; preflight and persisted-state tests MUST prove positive contiguous aggregate sequence. |
| `V1-ALWAYS-02` | `L21:B79-B131` | `c80a0344e68fb4272404b89b92ea0d13a4b51578dac61345ca2060ef4bc51e35` | Retained unless a separately approved append/persistence renegotiation authorizes otherwise. |
| `V1-ALWAYS-03` | `L21:B132-B215` | `4cc4c1b73f6421db7072c555aae9c359378b704264a302d0105e66c3d18dc60a` | Amended only after approval: v1 registration is fenced and v2 pair-local actor registration becomes authoritative under the cutover state machine. |
| `V1-ALWAYS-04` | `L21:B216-B261` | `ae9052038db96ca79619ea3638b31da38174531b1dbd1a0e8e5e70c8c84aaf28` | Retained for every future awaited implementation call. |
| `V1-ASK-01` | `L23:B15-B81` | `10af81fc0e662671e4e4c9cb25194bef9a59d600403f9f8dc28b158f2de57e3d` | Retained as a separate human authorization boundary; this successor does not move event persistence. |
| `V1-ASK-02` | `L23:B83-B126` | `58545c552fe030a77258c1ebd1af20edcbffb1cea173194eb0840d3de1c75543` | Retained as a separate human authorization boundary; section 4 specifies a future versioned shape but changes no public contract. |
| `V1-ASK-03` | `L23:B131-B194` | `1ae24060b647e77a702b1d4ba8e33e95db35a58686e440cd6e67e348b682fa90` | Retained; every evidence proposal MUST name and separately authorize any provider beyond existing DAPR actor state. |
| `V1-NEVER-01` | `L25:B11-B76` | `fb3495c3d6c9e0b3045ed876a65289776e606e21e12e9141195317553010548f` | Retained exactly: no process-local production counter may grant position authority. |
| `V1-NEVER-02` | `L25:B77-B162` | `d35c61b7fc7112389855ef766a32605dc55728f2b34cc67bc38b054166723875` | Retained exactly: CloudEvent identity is the persisted event `MessageId`. |
| `V1-NEVER-03` | `L25:B163-B227` | `eb7af1f2df5fd0291e06ada3f868715ee0bf8f791c23e4dfce29192856736e11` | Retained: this planning contract changes no projection replay implementation. |
| `V1-MATRIX-01` | `L31:B0-B192` | `537c943ab6fd978efb4e904316a6f5ee2ebc79a07f054763279ad51c723caff1` | Retained for v1; v2 starts each newly admitted pair at counter `1` under a positive minted lineage and never infers missing state. |
| `V1-MATRIX-02` | `L32:B0-B197` | `084b4196420fb288edb4defc630e4e65d912015a95895b75d77422329c43f699` | Amended for v2: each pair advances its own checked positive Int64 counter; overflow is permanent failure before mutation. |
| `V1-MATRIX-03` | `L33:B0-B145` | `973e786b859b14b84d0012ed74216ff32cf10097d0898db32458634396ec7110` | Retained and strengthened by generation-independent exact planned-batch idempotency in section 7. |
| `V1-MATRIX-04` | `L34:B0-B180` | `4656e87697efd4547a9ea51ff987f17e346840bbbfccfcbad55e87d19f6ad355` | Retained exactly, including existing publication error handling and deduplication identity. |

The predecessor design note is retained for v1 only: a reserved range may have
gaps when aggregate commit fails and reservation order is not commit order.
V2 preserves those facts per shard and explicitly forbids interpreting an
allocation counter, maximum counter, or vector of counters as a committed-event
watermark or lossless cursor.

## 2. Option decision and eligibility

The benchmark in section 14 measures the complete path, including lifecycle
catalog work when present, pair-local reservation, collision lookup, DAPR
routing, state-provider transaction, and read-back. A narrower actor id alone
is not evidence.

| Criterion | Tenant shard | Domain shard | Composite tenant+domain shard |
|---|---|---|---|
| Allocation owner | One allocator per exact canonical tenant | One allocator per exact canonical domain | One allocator per exact canonical tenant+domain pair |
| Contention reduction | Separates tenants but couples all hot domains of one tenant | Separates domains but couples all tenants in a hot domain | Separates both dimensions; pair is the reservation hot path |
| Position uniqueness | Unique only within tenant+generation | Unique only within domain+generation | Unique only within pair+allocator generation |
| Monotonicity | Tenant-local reservation order only | Domain-local reservation order only | Pair-local reservation order only |
| Gaps/commit order | Reservation gaps; not commit order | Reservation gaps; not commit order | Reservation gaps; not commit order |
| Hot-shard behavior | One hot tenant throttles its unrelated domains | One hot domain throttles unrelated tenants | One hot pair affects that pair; catalog is absent from normal reservation writes |
| Recovery | Tenant state has broad blast radius | Domain state has broad blast radius | Pair-local restore plus catalog reconciliation |
| Scaling | Bounded by tenant cardinality/skew | Bounded by domain cardinality/skew | Bounded by pair cardinality/skew; independently partitionable |
| Provider dependencies | DAPR actor transaction and fencing | DAPR actor transaction and fencing | Same pair-local transaction plus infrequent CAS lifecycle catalog |
| Rejection reason | Cross-domain coupling for a hot tenant violates isolation objective | Cross-tenant coupling for a hot domain violates isolation objective | Selected only if every eligibility gate below passes |

The selected strategy is `composite-tenant-domain-v2`, conditionally. It is
ineligible for implementation when any of these is absent, failed, expired, or
bound to different content:

1. capacity evidence satisfying section 14;
2. a complete bootstrap inventory and transcripts satisfying section 10;
3. an approved committed-enumeration source, or a content-bound proof that all
   cross-aggregate resume surfaces reject v2 and require before-first rebuild;
4. provider proof for pair-local atomicity, lifecycle CAS, key limits, cursor
   state, and consistent backup boundaries;
5. exact-content approval satisfying section 17.

No exception may waive identity, comparison, idempotency, fencing, backup, or
authorization requirements. A time-limited capacity exception MAY lower only a
performance threshold; it requires the current architecture owner set, exact
reason, compensating limit, expiry not exceeding 30 days, and a later capacity
gate. Expiry immediately restores ineligibility.

## 3. Canonical logical and physical shard identity

Canonicalization registry entry `tenant-domain/1` is immutable:

- `TenantId` and `Domain` are their exact persisted, case-sensitive UTF-8
  bytes. Empty values, invalid UTF-8, NUL, `/`, ASCII controls, leading or
  trailing Unicode whitespace, and bytes not already in their persisted form
  are invalid. No trimming, case folding, Unicode normalization, aliasing, or
  rename-in-place occurs.
- Each field is 1..64 UTF-8 bytes. A rename creates a new canonical pair; the
  old pair remains retained or retired historical inventory forever while any
  event, retry key, backup, cursor, or evidence refers to it.
- `pairFrame = ASCII("HXSH2") || 0x01 || u32be(tenantLength) || tenantBytes ||
  u32be(domainLength) || domainBytes`.
- `physicalKey = ASCII("hx-gpos-v2-sh:") || base64url_no_padding(pairFrame)`.
  Base64url uses RFC 4648 URL alphabet, no padding, and decoding MUST re-encode
  to the identical string. This direct, length-framed encoding is injective;
  it is not a hash and makes no secrecy claim.
- The maximum `pairFrame` is 142 bytes, its base64url form is 190 bytes, the
  prefix is 14 ASCII bytes, and the complete physical key is 204 bytes. The
  evidence gate MUST prove this exact maximum end to end through actor APIs,
  SDK serialization, HTTP/gRPC, DAPR sidecars, service proxies, telemetry
  sanitization, state-provider key composition, backup tooling, and the backing
  provider. A smaller discovered limit makes the design ineligible.
- `pairId = lowercase-hex(SHA-256(pairFrame))` is a diagnostic/catalog lookup
  digest only. Collision verification always compares the full `pairFrame`.

Normative physical-key vectors:

| Tenant bytes | Domain bytes | Pair-frame hex | Physical key |
|---|---|---|---|
| UTF-8 `tenant-a` | UTF-8 `orders` | `4858534832010000000874656e616e742d61000000066f7264657273` | `hx-gpos-v2-sh:SFhTSDIBAAAACHRlbmFudC1hAAAABm9yZGVycw` |
| 64 ASCII `A` | 64 ASCII `B` | `pairFrame` SHA-256 `2d595e83617e4a285f2401b88cbfddf1e0723ddb514739d051f98f0b83c4c173` | `hx-gpos-v2-sh:SFhTSDIBAAAAQEFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUEAAABAQkJCQkJCQkJCQkJCQkJCQkJCQkJCQkJCQkJCQkJCQkJCQkJCQkJCQkJCQkJCQkJCQkJCQkJCQkJCQkJCQkJCQg` (204 bytes) |

## 4. Versioned persisted metadata and comparison

Parsers MUST reject duplicate JSON properties before schema validation. Member
names are case-sensitive. Unknown members are invalid unless a later immutable
comparison-registry entry explicitly permits them. Integers named below are
JSON numbers only where bounded to Int32; every Int64 is a canonical positive
base-10 JSON string matching `[1-9][0-9]{0,18}` and not exceeding
`9223372036854775807`.

Metadata v1 remains the current 15-member shape with exact camel-case members
`messageId`, `aggregateId`, `aggregateType`, `tenantId`, `domain`,
`sequenceNumber`, `globalPosition`, `timestamp`, `correlationId`, `causationId`,
`userId`, `domainServiceVersion`, `eventTypeName`, `metadataVersion`, and
`serializationFormat`. Its existing string/timestamp semantics do not change;
`sequenceNumber` and `globalPosition` remain JSON Int64 numbers,
`metadataVersion` is JSON number `1`, and no `position` member exists. Zero is
the sole v1 unknown-position representation.

Metadata v2 is the complete strict object below. It uses the same member names
for retained fields, changes positive `sequenceNumber` to a decimal string to
avoid 64-bit JSON precision loss, and replaces `globalPosition` with `position`.
`globalPosition` MUST be absent, not zero or null. No field is nullable.

```json
{
  "aggregateId": "aggregate-2",
  "aggregateType": "order",
  "causationId": "cause-plan",
  "correlationId": "corr-plan",
  "domain": "orders",
  "domainServiceVersion": "2.0.0",
  "eventTypeName": "OrderPlaced",
  "messageId": "msg-plan",
  "metadataVersion": 2,
  "position": {
    "scheme": "tenant-domain-v2",
    "canonicalizationVersion": 1,
    "tenantId": "tenant-a",
    "domain": "orders",
    "physicalKey": "hx-gpos-v2-sh:SFhTSDIBAAAACHRlbmFudC1hAAAABm9yZGVycw",
    "cutoverGeneration": "1",
    "allocatorGeneration": "1",
    "recoveryGeneration": "1",
    "counter": "42"
  },
  "sequenceNumber": "1",
  "serializationFormat": "json",
  "tenantId": "tenant-a",
  "timestamp": "2026-08-27T00:00:00Z",
  "userId": "user-plan"
}
```

The JSON order above is illustrative; validation is by exact member set and
types before JCS use, not input property order. Every identifier/string keeps
its existing semantic validation plus the bounds in this specification.

The envelope's top-level `TenantId`/`Domain`, aggregate routing identity,
canonical `pairFrame`, physical key decoded bytes, allocator state, and position
members MUST be byte-identical. Any mismatch is `InvalidPosition` before state
or source access. Allocation count MUST equal planned event count, and planned
event `i` receives `firstCounter + i` under checked arithmetic.

Comparison dispatch first validates structure, then looks up `(scheme,
canonicalizationVersion)` in an immutable registry. Every recognized future
entry MUST define its own exact field schema, equality relation, and comparator;
it cannot fall through to the v2 comparator. Outcome precedence is:

1. `UnknownPosition` when either structurally valid v1 position is zero;
2. `InvalidPosition` for malformed, noncanonical, overflowed, or copy-mismatched
   data (invalid always wins over unsupported);
3. `UnsupportedScheme` for an unrecognized scheme/version entry;
4. `UnsupportedCrossScheme` for valid v1 versus valid non-v1;
5. `UnsupportedCrossCanonicalization` for recognized unequal canonicalization
   registry entries;
6. `UnsupportedCrossShard` for unequal canonical pair frames;
7. `UnsupportedCrossGeneration` for unequal cutover, allocator, or recovery
   generation within the same pair;
8. `Less`, `Equal`, or `Greater` by positive scalar v1 value for v1/v1, or by
   positive counter for fully equal v2 identity and lineage.

Equality is full tagged-position equality, not counter equality. No cross-shard,
cross-generation, or cross-scheme scalar fallback, encoded sortable scalar,
`Max`, merge order, or wall-clock tie-break exists. A collection containing
more than one comparison partition is unordered.

Diagnostics MUST expose structured fields `scheme`, `metadataVersion`,
`canonicalizationVersion`, `pairId`, `tenantIdRedacted`, `domainRedacted`,
`physicalKeyDigest`, `cutoverGeneration`, `allocatorGeneration`,
`recoveryGeneration`, `counter`, `comparisonOutcome`, and `sourcePartition`.
They MUST NOT log raw cursor, raw physical key, raw tenant/domain when policy
requires redaction, command key, batch bytes, payload, token, or claim. Metrics,
logs, tables, charts, and operator copy MUST NOT use `global latest`, `lag from
max position`, or any display implying cross-shard temporal order.

## 5. Shard-set identity and catalog authority

The durable catalog is lifecycle control, never the synchronous reservation hot
path. One checked generation authority mints positive Int64 `catalogVersion`,
`cutoverGeneration`, `allocatorGeneration`, `recoveryGeneration`, and
`lifecycleGeneration`; overflow is terminal. Accepted lineage is append-only.
Pair records are independently keyed/partitioned, and catalog operations use
CAS on their declared versions.

`memberFrame` is exactly `stateTag` (`0x01 active`, `0x02 retained`, `0x03
retired`) `|| frame(pairFrame) || frame(UTF8(physicalKey)) ||
u64be(membershipGeneration) || u64be(lifecycleGeneration) ||
u32be(allocatorGenerationCount) || sorted u64be allocator generations ||
u32be(recoveryGenerationCount) || sorted u64be recovery generations ||
frame(sourceBoundaryId)`. `frame(bytes)=u32be(length)||bytes`. Counts reject
duplicates or values above UInt32; all generations are positive Int64.

`shardSetBytes = ASCII("HX-GPOS-SHARD-SET") || 0x01 || u64be(catalogVersion) ||
frame(transitionId) || frame(sourceBoundaryId) || u32be(activeCount) ||
sorted(active memberFrame by pairFrame bytes) || u32be(retainedCount) ||
sorted(retained/retired memberFrame by pairFrame bytes)`.
`shardSetId = lowercase-hex(SHA-256(shardSetBytes))`. Catalog, cursor, committed
source, projection checkpoint, backup, restore, and fingerprint MUST carry and
recompute the identical bytes and id. Eventless registered members are present.

Lifecycle work has a stable locator before its request set is frozen:
`operationLocator = lowercase-hex(SHA-256(ASCII("HX-GPOS-OP-LOCATOR")||0x01||
u64be(baseCatalogVersion)||u64be(lifecycleGeneration)))`. Exactly one record is
created at that locator by CAS. While its phase is `collecting`, requests may
join its CAS-protected request set. `actionTag` is exactly `0x01` admission or
`0x02` retirement. On the `collecting -> prepared` CAS, the sorted request set
is frozen and the immutable
`operationId = lowercase-hex(SHA-256(ASCII("HX-GPOS-OP")||0x01||
u64be(baseCatalogVersion)||u64be(lifecycleGeneration)||u32be(requestCount)||
sorted(u8(actionTag)||frame(pairFrame)))))` is computed. The locator record
stores that id permanently; the id never changes or names a movable record.
Base-version and pair indexes map to the locator and are updated by CAS. The
record contains base/target versions, frozen request set, nonce, leader lease
generation, phase, per-pair observations, source boundaries, and terminal
outcome. Recovery starts from the stable indexes and locator, then recomputes
and verifies the operation id before resuming.

Admission phases are `prepared`, `pair-installing`, `pair-verified`,
`set-published`, `pair-activated`, `complete`. Retirement phases are `prepared`,
`routing-revoked`, `drained`, `pair-sealed`, `set-published`, `complete`.
Recovery enumerates nonterminal operation keys and resumes idempotently.

- CAS leader acquisition succeeds for one leader generation. During
  `collecting`, an identical request attaches to the same result and a disjoint
  request joins the sorted request set by CAS. After the request set is frozen,
  every late request queues against the eventual target version; it never
  changes the operation id. A leader lease expiry permits a higher leader
  generation to resume the same locator, frozen request set, and operation id.
- The same pair has at most one admission/retirement operation across all base
  versions. Its pair-generation guard rejects a stale operation. Admission of
  a permanently retired exact pair is `RetiredIdentityPermanent`.
- Provisioning installs exactly one minted allocator/recovery lineage and a
  nonce into new pair state, reads it back, publishes the new shard set, then
  activates allocation. A partial record never implies activation. The pair
  checks both its active state and the published catalog/set version.
- After irreversible cutover, a command for a never-allocated pair receives
  `ProvisioningRequiredRetryable`; it cannot reserve until the source captures
  an immutable before-allocation admission boundary and the admission completes.
- Retirement first revokes routing authority, drains writers/provider work,
  reads back the final ceiling and reservation evidence, seals pair state, then
  publishes retained/retired membership. Exact identity reuse is permanent
  failure; a new business identity produces a new canonical pair.

The authoritative pair lifecycle is exactly `never-allocated`, `provisioning`,
`active`, `retiring`, `retired-unallocated`, or `retired-allocated`.
`never-allocated` is absence proven by the catalog and bootstrap inventory, not
absence of actor state. Both retired states are permanent tombstones;
`retired-allocated` means successful reservation evidence exists and
`retired-unallocated` means its positive absence was proven at seal. Shard-set
member tags project these states as active, retained, or retired but never erase
the authoritative lifecycle distinction.

## 6. Authority and cutover state machine

The global authority record has monotonic phases `v1-active`, `quiescing`,
`v1-provisionally-sealed`, `all-pairs-enabled`, `first-reservation-pending`,
and `v2-irreversible`. Each transition persists operation id, phase generation,
expected catalog/set id, v1 captured ceiling, credential/routing revocations,
drain transcript ids, pair read-back digests, reservation evidence aggregate,
and permit generation. Recovery resumes the recorded phase; it never infers
progress from missing records.

Before `v1-provisionally-sealed`, operators MUST stop every v1/v2 writer and
sidecar, revoke old credentials and routing independently of application code,
observe provider operations quiescent, reconcile the final bootstrap inventory,
read the v1 stored ceiling, and bind immutable drain transcripts. A non-atomic
epoch check is not fencing.

`all-pairs-enabled` is one durable global allocation gate following successful
read-back of every active pair, published shard-set identity, routing permit,
and separate validation of retained tombstones. New-pair admission between this
phase and irreversibility follows section 5 while the global gate remains
closed for that pair.

The first production reservation does not claim an impossible cross-partition
atomic update. A CAS first-reservation permit moves the global record to
`first-reservation-pending`, names exactly one pair, command-key digest, pair
generation, and permit generation, and keeps every other production allocation
closed. The permitted pair transaction either makes no mutation or atomically
stores its range and successful-reservation evidence. Reconciliation reads that
exact pair transaction: positive evidence moves the global record durably to
`v2-irreversible` before general allocation opens; proved absence may return to
`all-pairs-enabled` or enter rollback. Missing, stale, conflicting, or
unavailable pair evidence is forward-only and leaves allocation closed. Thus a
crash at either write cannot create a rollback claim after a successful
reservation and cannot permit a second reservation first. The first reservation
is not returned to its caller and no aggregate event write starts until the
irreversible read-back completes. Old writers cannot
allocate or commit v1 after provisional seal and can never regain authority
after irreversibility. Downgrade is forward-fix only.

Pre-write rollback is allowed only after v2 is again stopped, revoked, drained,
and every bootstrap/admitted pair, catalog operation, archive, and committed
source positively proves zero successful production reservations and no v2
event. Missing, stale, or conflicting evidence forbids rollback. Rollback mints
a fresh v1 authority generation, stores a v1 ceiling at least the captured
final scalar ceiling, and the next grant is strictly greater. It never reuses a
v1 or v2 identity. Any successful production reservation makes rollback
permanently forbidden.

## 7. Pair-local reservation and permanent retry identity

The stable command key is generation-independent:
`commandKeyBytes = ASCII("HX-GPOS-COMMAND") || 0x01 || frame(pairFrame) ||
frame(UTF8(opaqueIdempotencyKey))`. The opaque key is 1..256 UTF-8 bytes and is
never placed in actor/state identifiers or diagnostics. `commandKeyDigest` is
SHA-256 of these bytes, but every lookup result verifies the full framed bytes
or a content-addressed object whose digest and length reproduce them.

Before any mutation, an implementation MUST validate the complete plan:

- event count is 1..1024 and exactly equals requested allocation count;
- one aggregate-sequence preflight requires the current sequence to be
  nonnegative, `startingSequence=current+1` to be positive, and every
  `startingSequence+i` to be contiguous positive Int64 without overflow;
- ordered `MessageId` values are distinct, valid stable identifiers, and
  already fixed; retries use identical values;
- every string is strict UTF-8 and within its contract bound; each payload is
  at most 32 KiB; each extension map is at most 64 entries; no duplicate key;
- exact persisted RFC-3339 timestamp UTF-8 is present, parses strictly, and is
  identical to the eventual stored bytes; parsed equality never replaces byte
  equality;
- `plannedBatchBytes` uses the section 13 frame grammar and binds pair frame,
  aggregate identity, sequence, MessageId, event type, serialization format,
  exact payload bytes, sorted extension bytes, all persisted metadata, and
  timestamp bytes;
- the complete serialized reservation detail, including keys, lengths, batch
  bytes/digest, lineage, ceilings, result, and provider overhead, is at most
  65,536 bytes before allocation. `PlanTooLargePermanent` is returned without
  mutation. A larger external detail protocol is forbidden until a separate
  approved, content-addressed provider contract replaces this bound.

The pair-local atomic transaction stores counter ceiling, successful-reservation
flag, stable command bytes/digest, planned-batch bytes/digest, ordered MessageId
binding, first/count/range, accepted generations, exact timestamps, lifecycle
and permit versions, and terminal result. The global catalog is not written.
On retry, all accepted lineages are searched before any new grant. Exact match
replays the original range/result. Any key match with different bytes or plan is
`ReservationConflictPermanent`.

Live details are retained for 30 days from a durable `completedAt`. At the exact
`compactAfter` instant (`now >= compactAfter`), a continuous scheduler uses the
stable `compactionId = lowercase-hex(SHA-256(ASCII("HX-GPOS-COMPACT")||0x01||
commandKeyBytes||plannedBatchDigest))`. Because pair state and the partitioned
archive need not share a transaction, compaction is a resumable protocol:
`prepared -> archive-written -> archive-verified -> live-tombstoned -> complete`.
It writes full collision-verification command and plan bytes, or an equivalently
injective content-addressed object, with CAS; reads the object back; then
atomically replaces live detail with a tombstone containing the archive locator,
digest, length, original terminal result digest, and compaction id. It never
deletes the tombstone. A crash resumes the same phase. Lookup checks the live
record/tombstone and archive before allocation, requires their bytes and digests
to agree, and fails closed on partial or conflicting state. Archived command
identities are permanent and cannot be evicted for count/capacity; their
deterministic result is `ReservationExpiredPermanent` after detailed replay
expires.

The archive is partitioned by the first two digest bytes, then full digest, and
stores each collision-chain member as a separately bounded CAS record keyed by
its full-byte ordinal; no unbounded chain is one provider value. Each partition
has evidenced key, value, transaction, backup, recovery, byte-capacity, and
operation-rate limits. Compaction runs independent of actor activation and
publishes oldest-uncompacted age, byte utilization, and queue slope. New
reservations are retryably backpressured when projected 30-day live bytes or
archive bytes reach 70% of the evidenced provider limit, uncompacted age exceeds
one hour, or the upper 95% queue-slope bound is positive; they are fail-closed
unavailable at 80%. Evidence MUST show continuous maximum-rate operation for
the retention horizon and permanent archive growth projected to five years,
with a funded extension/rotation plan before year four. A finite tombstone cap,
unbounded provider value, or eviction policy makes the design ineligible.

## 8. Committed enumeration and page semantics

Position labels are not committed-event cursors. A committed enumeration
source, not an allocation counter, owns replay continuity. The approved source
MUST expose `OpenFiniteSnapshot(queryScope, shardSetId)`,
`ReadPage(boundaryId, traversalState, limits)`, `Renew(boundaryId)`, and
`Close(boundaryId)`. Open returns an immutable canonical `boundaryId`, complete
authoritative lineage/shard-set bytes, per-partition before-first tokens, and
terminal tokens. Renewable credentials never change boundary identity or
membership. Identical authorized query, boundary, traversal state, and limits
reproduce identical records and next state.

All source protocol objects are strict duplicate-free UTF-8 JSON,
case-sensitive, JCS canonical, and `additionalProperties=false`. Int64 values
are canonical positive decimal strings; byte tokens are canonical unpadded
base64url. `OpenFiniteSnapshot` returns exactly:

```json
{"boundaryId":"<1..512-byte-source-id>","committedSourceId":"<1..512-byte-source-id>","partitions":[{"beforeFirst":"<base64url>","sourcePartition":"<canonical-partition>","terminal":"<base64url>"}],"schema":"hexalith.gpos.snapshot/v2","shardSetBytes":"<base64url>","shardSetId":"<64-lower-hex>"}
```

Partitions are in the canonical order below, occur exactly once, and include
eventless authoritative members. Decoded source tokens are 0..4,096 bytes; an
empty token is encoded as the empty string. `limits` is exactly
`{"maxPageBytes":<1..1048576>,"maxRecords":<1..10000>}` with JSON-number
Int32 values. `ReadPage` returns exactly:

```json
{"boundaryId":"<same>","groups":[{"continued":false,"nextSourceToken":"<base64url>","records":["<canonical-base64url-source-record>"],"sourcePartition":"<canonical-partition>"}],"nextTraversal":"<base64url>","pageBytes":1,"recordCount":1,"schema":"hexalith.gpos.page/v2","terminal":false}
```

`pageBytes` is the exact JCS response byte length and cannot exceed either
limit; `recordCount` equals the sum of records and cannot exceed either limit.
Decoded traversal state is at most 1 MiB and is an authenticated source-owned
encoding of the current partition index and exact source tokens. Each decoded
record is at most 1 MiB, maps to exactly one metadata v1/v2 event, and carries
the source ordinal needed by section 13. The source adapter validates the event
before encoding it; consumers decode and validate it again. `terminal=true`
requires empty groups, a terminal traversal value, and equality to every
snapshot terminal token. Nonterminal pages require at least one record.

Mixed history is grouped, not temporally merged. Partition order is
`global-v1`, followed by v2 members sorted by `pairFrame`, then generation tuple.
The canonical v2 source-partition string is
`tenant-domain-v2:<pairId>:<allocatorGeneration>:<recoveryGeneration>` with
lowercase pair digest and canonical positive decimal generations.
Records inside a source partition follow that source's immutable enumeration
order. A page returns complete ordered groups. A group MAY split only at an
exact source-issued continuation token and states `continued=true`; no implicit
counter split exists. `continued` is true exactly when the same partition may
continue on the next page; otherwise the next group/partition starts. If one
encoded record plus its response framing exceeds the requested or 1 MiB page
maximum, return `RecordTooLargePermanent` before emitting any record.
Unavailable/partial reads emit no page and do not advance traversal or
checkpoint.

Persisted v1 scalar checkpoints never translate to v2. A consumer MUST use a
source-specific proven immutable mapping or rebuild from the before-first state.
If no committed source is approved, every cross-aggregate cursor/checkpoint
surface MUST reject v2 with `CommittedEnumerationUnsupported`.

Finite snapshots exclude later admissions. A long-lived model is marked old-set
until it reads an admission boundary from before-first to terminal and atomically
rebases read model, checkpoint, and shard-set identity. Retired shards remain in
new snapshots while retained events exist. After retention expiry, shrink first
rebuilds/removes that shard's contribution at a fixed boundary, atomically
rebases the model/checkpoint, and only then publishes the smaller set. A mode
without co-located atomic batch or explicit idempotent recovery is unsupported.

## 9. Cursor envelope, state, confidentiality, and liveness

Cursor and renewal inputs are one canonical unpadded base64url ASCII text of at
most 10,923 characters. It decodes to at most 8,192 raw bytes; 8,192 bytes encode
to exactly 10,923 unpadded characters. Padding, whitespace, a non-URL alphabet,
or decode/re-encode inequality is `InvalidCursor`. JSON is strict UTF-8,
duplicate-free, case-sensitive, JCS canonical, and
`additionalProperties=false`. Both inline and reference modes use AES-256-GCM
with a unique 96-bit nonce, protected header bytes as AAD, a 128-bit tag, and
keys from the named EventStore DataProtection/key-rotation authority. The
authority assigns each key a random immutable 32-bit nonce prefix and allocates
checked non-overlapping UInt64 counter ranges by CAS; the nonce is prefix plus
counter in big-endian order. Counter exhaustion retires the key before reuse.
Evidence MUST prove uniqueness across hosts, restart, restore, and failover.
Both modes are confidential; `opaque` never substitutes for encryption.

The exact decoded binary envelope is:

```text
ASCII("HXGC") || 0x02 ||
u16be(headerLength) || headerJcsUtf8 ||
nonce[12] ||
u32be(ciphertextLength) || ciphertext[ciphertextLength] ||
tag[16]
```

The magic is exactly four bytes `48 58 47 43`; version is exactly `02`.
`headerLength` is unsigned big-endian, is 1..1,024, and counts the exact header
JCS UTF-8 bytes that immediately follow it. The 12 nonce bytes immediately
follow the header. `ciphertextLength` is unsigned big-endian, is 1..6,144, and
counts the exact ciphertext bytes that immediately follow it. The final 16
bytes are the GCM tag. There are no reserved or trailing bytes. Thus
`rawLength = 4 + 1 + 2 + headerLength + 12 + 4 + ciphertextLength + 16`, MUST
equal the decoded length, and MUST be at most 8,192. Header bytes are the exact
AES-GCM AAD. The exact payload JCS UTF-8 bytes are the plaintext, so ciphertext
length equals plaintext length. The complete binary envelope, and no component
separately, is encoded once as canonical unpadded base64url text. This grammar
and every bound apply identically to both inline and reference payload modes.

Before key lookup, decryption, allocation, state access, or committed-source
access, the decoder MUST reject wrong magic/version, zero or out-of-range
lengths, arithmetic overflow, total text/raw overflow, truncated fields,
declared/actual length mismatch, trailing bytes, padding, invalid alphabet, or
decode/re-encode mismatch as the indistinguishable `InvalidCursor`. It then
requires `headerJcsUtf8` to parse and reserialize to byte-identical JCS before
using `kid`; after authenticated decryption it requires the plaintext payload
to parse and reserialize to byte-identical JCS before consuming any claim.

The protected header JCS object is exactly:

```json
{"alg":"A256GCM","enc":"A256GCM","kid":"<key-id>","schema":"hexalith.gpos.cursor/v2","stateMode":"inline|reference","typ":"HX-GPOS-CURSOR"}
```

The decrypted payload has exactly these required members and types:

```json
{"aud":"<audience>","authorizationPolicyVersion":"<version>","authorizationScopeDigest":"<64-lower-hex>","boundaryId":"<source-id>","committedSourceId":"<source-id>","cutoverGeneration":"<positive-decimal-string>","exp":"<unix-seconds-positive-decimal-string>","iat":"<unix-seconds-positive-decimal-string>","jti":"<base64url-128-bit>","mode":"single|multi|rebuild","nbf":"<unix-seconds-positive-decimal-string>","principalDigest":"<64-lower-hex>","queryScopeDigest":"<64-lower-hex>","shardSetId":"<64-lower-hex>","stateId":"<base64url-192-bit-or-empty>","stateIntegrityKid":"<key-id-or-empty>","traversal":"<base64url-source-bytes-or-empty>"}
```

All member strings have 1..512 UTF-8 bytes except empty values explicitly
shown, `traversal` is at most 4,096 decoded bytes, and the exact JCS payload is
1..6,144 bytes. Digests are 64 lowercase hexadecimal characters; `jti` is the
canonical unpadded encoding of exactly 16 bytes; nonempty `stateId` encodes
exactly 24 bytes. `iat`, `nbf`, and `exp` are positive Int64 Unix-second decimal
strings. The exact protected-header JCS is 1..1,024 bytes; `kid` is 1..128
printable ASCII bytes and selects the envelope key. Reference mode
requires nonempty `stateId`/`stateIntegrityKid`, empty `traversal`, and a state
record. Inline mode requires both state fields empty.

Reference state uses the already-authorized DAPR actor state provider under
key `hx-gpos-cursor-state:<base64url-192-bit-random>`. Creation is CAS-if-absent;
random collision retries are capped at 8 then fail `CursorStateIdExhausted`.
The strict state JCS object is exactly:

```json
{"aud":"<audience>","authorizationScopeDigest":"<64-lower-hex>","boundaryId":"<source-id>","committedSourceId":"<source-id>","createdAt":"<unix-seconds-positive-decimal-string>","cutoverGeneration":"<positive-decimal-string>","expiresAt":"<unix-seconds-positive-decimal-string>","mac":"<base64url-32-bytes>","principalDigest":"<64-lower-hex>","queryScopeDigest":"<64-lower-hex>","schema":"hexalith.gpos.cursor-state/v1","shardSetId":"<64-lower-hex>","stateId":"<base64url-192-bit>","stateIntegrityKid":"<key-id>","traversal":"<encrypted-base64url>"}
```

The decoded traversal plaintext is at most 780,000 bytes. Stored `traversal`
decodes exactly to `nonce[12] || ciphertext || tag[16]` and uses a distinct
AES-256-GCM state-confidentiality key selected by `stateIntegrityKid`. Its AAD
is `ASCII("HX-GPOS-CURSOR-STATE-AAD")||0x01||frame(stateId)||JCS(object with
traversal="" and mac="")`. State nonces use the same never-reuse rule as
envelope nonces but a domain-separated key/nonce authority. `mac` is canonical
unpadded base64url of `HMAC-SHA-256(stateMacKey[kid],
ASCII("HX-GPOS-CURSOR-STATE")||0x01||frame(stateId)||JCS(object with mac=""))`.
The complete state JCS is at most 1,048,576 bytes. `createdAt <= expiresAt`,
lifetime is at most 31 days, and both are Unix-second positive Int64 strings.
Every digest, id, source field, and key bound above uses the same size/format
rule as the envelope. Envelope and state IDs/kids/claims MUST match after
authentication. State records are immutable; page advance or renewal creates a
new CAS-if-absent state and only then issues its referencing envelope, so a
crash cannot expose a token for missing state.

State provider evidence MUST prove compare-and-set, replication/failover,
read-after-write, backup, TTL of 31 days, cleanup after boundary close plus a
24-hour safety period, and key retention until every state/token expires.
Missing/corrupt state is `CursorStateUnavailable` and never restarts from
before-first implicitly.

Validation precedence is exact:

1. before authentication, apply the canonical base64url, binary magic/version,
   length, no-trailing-byte, nonce/tag, total-size, and header-JCS checks above;
   then return one indistinguishable `InvalidCursor` for any structural parse,
   unknown key, algorithm, nonce, tag, integrity, payload-JCS, or duplicate-
   member failure; consume no claim and access no allocation, cursor state, or
   committed source;
2. after integrity, authenticate the request principal, then require audience,
   principal digest, current authorization policy version/scope, and query scope;
   all authorization mismatches return `CursorNotAuthorized` before state or
   source availability is probed;
3. validate `iat <= nbf <= exp`, maximum original lifetime 24 hours, `iat` and
   `nbf` no more than 60 seconds in the future, current time no earlier than
   `nbf-60s`, and no later than `exp+60s`; `exp` may be up to 24 hours after
   `iat`; time failure is `CursorExpiredOrNotYetValid`;
4. validate cutover generation and recomputed current shard-set/snapshot rules;
5. authenticate/fetch reference state and validate key/claim equality;
6. access the committed source and boundary.

Renewal is allowed from `nbf-60s` through `exp+60s`, reauthorizes current policy,
preserves boundary/traversal/query/shard-set, rotates keys if needed, and sets a
new lifetime no greater than 24 hours. The source MUST retain/reissue the same
boundary for up to 30 days; every measured rebuild, including the slowest run,
MUST complete inside that window, or evidence MUST prove a boundary-preserving
successor source that reissues the same immutable content identity without
changing records. Otherwise rebuild mode is ineligible.

## 10. Bootstrap inventory and completeness proof

Bootstrap has three immutable artifacts: `precheck`, one `filtered` artifact per
independent source, and `final-union`. Each is strict JSON, duplicate-free,
`additionalProperties=false`, JCS-canonicalized, and SHA-256 bound. Pair counts
are JSON numbers in `[0,4294967295]`; arrays have exactly that count and are
sorted by decoded `pairFrame` bytes with no duplicate.

Artifact references are exactly
`{"pathOrUri":"<immutable-location>","sha256":"<64-lower-hex>","size":"<positive-decimal-string>"}`.
Repository paths are normalized relative paths with no empty, dot, dot-dot,
backslash, or NUL segment and resolve at the evidence commit; URIs must expose
an immutable provider version. Every referenced byte length and digest is
recomputed. Timestamp strings are 1..64 strict RFC-3339 UTF-8 bytes; ceilings
are canonical nonnegative Int64 decimal strings; authority/source identifiers
are 1..256 strict UTF-8 bytes. Pair fields obey section 3 exactly.

Common pair entry schema is exactly:

```json
{"domain":"<canonical>","pairFrame":"<canonical-base64url>","physicalKey":"<canonical>","tenantId":"<canonical>"}
```

`precheck` requires exactly `schema=hexalith.gpos.bootstrap-precheck/v1`,
`captureStartedAt`, `captureCompletedAt`, `v1Ceiling`, `activeIngressBoundary`,
`sourceDefinitions`, `sourceDefinitionsDigest`, `pairCount`, `pairs`, and
`enumerationTranscript`.
`activeIngressBoundary` is an immutable 1..512-byte source boundary;
each sorted source definition has exactly `authorityId`, `independenceClass`,
`interfaceName`, `interfaceVersion`, `definitionBlob`, and `definitionSha256`.
`sourceDefinitionsDigest` is SHA-256 of their exact JCS array;
`enumerationTranscript` is one artifact reference.
`filtered` requires exactly `schema=hexalith.gpos.bootstrap-filtered/v1`,
`authorityId`, `independenceClass`, `formatAlgorithm`, `sourceBlobOrSnapshotId`,
`filterStartedAt`, `filterCompletedAt`, `rawCount`, `pairCount`, `pairs`,
`filteredPairsDigest`, and `enumerationTranscript`. `final-union` requires
exactly `schema=hexalith.gpos.bootstrap-final/v1`, `precheckDigest`, sorted
`filteredArtifactDigests`, `quiescenceStartedAt`, `writersRevokedAt`,
`providerDrainedAt`, `finalCaptureStartedAt`, `finalCaptureCompletedAt`,
`finalV1Ceiling`, `finalIngressBoundary`, `finalIngressPairCount`,
`finalIngressDigest`, `finalIngressTranscript`, `pairCount`, `pairs`, `unionGrammar`,
`drainArtifact`, sorted `attestations`, and `finalDigest`. Counts are
JSON-number UInt32 values. Every field named digest is 64 lowercase
hexadecimal characters. `filteredArtifactDigests` are unique and sorted raw
digest bytes. `formatAlgorithm` and `unionGrammar` each have exactly `name`,
`version`, `blob`, and `sha256`; their blobs are fetched and verified.
`finalIngressTranscript`, `drainArtifact`, and every attestation are artifact
references sorted by decoded SHA-256 bytes.

All timestamps are exact persisted RFC-3339 UTF-8, parse strictly, and order as
capture start <= completion; precheck completes before quiescence; quiescence <=
revocation <= drain <= final capture start <= final capture completion. Source
definitions and custom formats are immutable blobs. A custom
`formatAlgorithm` MUST name the byte decoder version, record framing, field
extraction, invalid-record behavior, filtering predicate, duplicate rule, sort,
and output grammar; prose names are insufficient.

`filteredPairsDigest = SHA-256(ASCII("HX-GPOS-FILTERED")||0x01||
frame(authorityId)||frame(sourceBlobOrSnapshotId)||u32be(pairCount)||
concatenated pairFrame)`. `precheckDigest` and each filtered artifact digest are
SHA-256 of their exact JCS bytes. `finalIngressDigest` is SHA-256 of
`ASCII("HX-GPOS-FINAL-INGRESS")||0x01||frame(finalIngressBoundary)||
u32be(finalIngressPairCount)||concatenated pairFrame` for the independently captured final
ingress list. `finalDigest = SHA-256(ASCII("HX-GPOS-BOOTSTRAP")||0x01||
JCS(final-union object with finalDigest=""))`; therefore timestamps, grammars,
transcript/attestation identities, final ingress, drain evidence, and every
pair are bound rather than merely present.

At least two sources with different `authorityId` and `independenceClass` MUST
independently enumerate every historical and currently admissible pair: one
committed-event/history authority and one active-ingress/routing authority.
The final union is captured while v1 commands remain quiescent and MUST equal
the union of every recomputed filtered list and the independently recomputed
final ingress capture identified by `finalIngressDigest` and
`finalIngressTranscript`.
Differences, concurrent arrivals, UInt32 overflow, unavailable transcripts, or
an unverifiable custom record fail the gate. Events may support this one-time
import but never reconstruct lost allocator/registry state after activation.

Every transcript is an immutable strict-JCS artifact with exact members
`schema`, `authorityId`, `captures`, `toolBlob`, and `toolSha256`; captures are
sorted by phase then start time and each has exactly `phase`,
`sourceRequestDigest`, `startedAt`, `completedAt`, `pages`, `retryCount`,
`terminalTokenDigest`, and `exitStatus`. Phase is `precheck`, `filtered`, or
`final`; each page has exactly `index`, `requestTokenDigest`,
`responseTokenDigest`, `byteDigest`, and `recordCount`. Indices are contiguous
UInt32, counts are UInt32, digests are lowercase SHA-256, and successful
`exitStatus` is JSON number zero. The drain
artifact is strict JCS with exact stopped host/sidecar identities, revoked
credential/route digests, one-second provider-operation samples, observation
start/end, and zero-in-flight terminal sample. The interval is at least the
provider's proved maximum operation timeout plus 60 seconds. Each source owner
signs a strict attestation binding immutable GitHub user ID, login, authority,
all artifact digests, exact completeness statement, submitted time, native API
URL/id/commit, and signature digest. The native record and current role are
refetched as in section 17. Boolean `complete=true` without these artifacts is
invalid.

## 11. Backup, restore, and monotonic reconciliation

Backup MUST first revoke/drain all affected allocation and lifecycle authorities
or use a provider-proven single consistent-snapshot boundary. The immutable
boundary binds catalog versions, shard-set bytes, authority/cutover control,
operation log/index, pair states and ceilings, live ledgers, archive indexes and
objects, MessageId/batch bindings, permits, routing/credential generations,
committed-source boundary state, cursor state, retained/retired tombstones, and
accepted lineage. Independently timed partition dumps are not a backup.

The boundary manifest is strict JCS with exactly `schema`, `backupId`,
`providerBoundaryId`, `captureMode`, `capturedAt`, `authorityState`,
`catalogVersion`, `shardSetId`, `shardSetBytesDigest`, `cutoverGeneration`,
`permitGeneration`, `routingGeneration`, and sorted `partitions`. `captureMode`
is `quiesced` or `provider-consistent`; each partition entry has exactly
`kind`, `partitionId`, `generation`, `terminalKey`, `recordCount`,
`contentDigest`, and `transcript`. Counts are UInt64 decimal strings, IDs are
bounded UTF-8, digests are lowercase SHA-256, and transcripts are section-10
artifact references. The manifest digest is SHA-256 of exact JCS bytes. A
provider-consistent capture additionally binds immutable provider documentation
and a production-path proof that one boundary covers every listed partition.
Missing kinds, duplicate partition IDs, independently issued boundaries, or a
catalog/shard-set recomputation mismatch invalidate the backup.

Restore is a durable idempotent state machine keyed by immutable restore id with
phases `prepared`, `authority-revoked`, `snapshot-verified`, `unioning`,
`read-back`, `reactivating`, `complete`. It persists target recovery generation,
source boundary, per-partition progress, conflicts, and final digest.

For every key, restore monotonically unions all accepted generations, catalog
and lifecycle operations, ceilings, successful-reservation flags, live details,
permanent archive identities and exact collision bytes, MessageId/batch
bindings, cursor/boundary state, permits/control authority, routing revocation,
and retired identity. Authority phases use the section-6 order; successful
reservation is boolean OR; generation/operation/archive sets are exact set
union; ceilings and permit/routing generations use checked maximum; lifecycle
may advance only along its declared state machine; immutable bindings require
byte equality. Expired cursor state stays expired and never resurrects access.
Equal immutable keys with unequal bytes, a missing newer entry,
source-partition mismatch, incomparable lifecycle state, lower authority state,
or absent archive object fail closed. Stored pair ceiling becomes at least every
authoritative reservation ceiling; its next grant is strictly greater. Events
never lower or reconstruct missing ceilings. Reactivation occurs only after
full catalog/pair/archive/source read-back recomputes the same shard-set and
backup-boundary identities and no nonterminal compaction/lifecycle/restore
operation remains unreconciled.

## 12. Projection and mixed-history integrity

A committed-source snapshot declares the complete authoritative source
partition and lineage sets, including eventless registered shards. Authority
entries declare the singleton `global-v1` partition and its accepted v1
lineage; partition entries declare every v2 member, including eventless ones.
Every event MUST map to `global-v1` when metadata v1, or to the exact v2 pair
partition from its canonical identity; mismatch is invalid. Page/checkpoint/
model updates are atomic or use an explicit idempotent recovery record. Signal,
allocation label, or timestamp never proves projection progress.

The fingerprint in section 13 binds precisely the stated fields. It makes no
claim about fields not present in its grammar. Duplicate identical records are
deduplicated before `recordCount`; a v1 base position is its positive scalar
(zero has no base identity), and a v2 base position is its full scheme,
canonical-pair, cutover, allocator, recovery, and counter tuple. Duplicate base
position, aggregate identity+sequence, or MessageId with conflicting
provenance/content is invalid.

## 13. Exact fingerprint and planned-batch byte grammar

Primitive `F(tag,payload) = u8(tag) || u32be(payload.length) || payload`.
`S(tag,text)` is `F` over exact strict UTF-8. `U64(tag,n)` is
`F(tag,u64be(n))`; `I64` and `I32` use two's-complement big-endian; `U32` is
unsigned big-endian. `SET(tag,items)` is
`F(tag,u32be(count)||concatenated items)` after the specified byte sort;
duplicate items are invalid. `SEQ(tag,items)` uses the same count framing but
keeps source order and never sorts. `MAP(tag,entries)` is
`F(tag,concatenated entries sorted by strict UTF-8 key bytes)`; each entry is
self-framed and duplicate keys are invalid, so an empty map has an empty
payload. No payload length or count may exceed UInt32. SHA-256 digests are raw
32 bytes unless called lowercase hex.

Planned event bytes are:

```text
F(0x31, S(0x01,MessageId) || U64(0x02,SequenceNumber) ||
  S(0x03,EventTypeName) || S(0x04,SerializationFormat) ||
  F(0x05,ExactPersistedPayloadBytes) ||
  SET(0x06, sorted F(0x07,S(0x08,key)||S(0x09,value))) ||
  S(0x0a,ExactPersistedRfc3339TimestampUtf8) ||
  S(0x0b,CorrelationId) || S(0x0c,CausationId) || S(0x0d,UserId) ||
  S(0x0e,DomainServiceVersion) ||
  MAP(0x0f,F(0x10,S(0x01,MetadataKey)||F(0x02,ExactMetadataValueBytes))))
```

`plannedBatchBytes = ASCII("HX-GPOS-PLAN") || 0x01 || F(0x20,pairFrame) ||
S(0x21,AggregateId) || S(0x22,AggregateType) ||
SEQ(0x23,planned events in original order)`; digest is SHA-256 of exact bytes.
The metadata map contains every variable persisted pre-allocation metadata field
not otherwise enumerated; it excludes only the position members created by this
reservation. Metadata version `2`, scheme `tenant-domain-v2`, and
canonicalization version `1` are invariant bytes already bound by the plan
prefix and `pairFrame`, so they are not repeated. Key names and exact serialized
value bytes are part of identity. No serializer default or omitted field is
implicit.

History fingerprint bytes are:

```text
ASCII("HX-GPOS-HISTORY") || 0x02 ||
F(0x01,CanonicalCommittedSourceId) || F(0x02,ImmutableBoundaryId) ||
F(0x03,ExactShardSetBytes) ||
SET(0x04, sorted authority entries) || SET(0x05, sorted v2 partition entries) ||
SET(0x06, deduplicated records sorted by partition bytes then source order)
```

Authority entry grammar is exactly:

```text
F(0x11,
  S(0x01,AuthorityState) || U64(0x02,CatalogVersion) ||
  U64(0x03,CutoverGeneration) || U64(0x04,PermitGeneration) ||
  SET(0x05, sorted U64(0x06,AcceptedV1Generation)) ||
  SET(0x07, sorted S(0x08,OperationId)))
```

There is exactly one authority entry. `AuthorityState` is one of the section-6
states, and this entry is the complete declaration of the `global-v1` source
partition even when that partition has no record. Its accepted-v1 generations
are the complete source-authoritative set.

Partition entry grammar is exactly:

```text
F(0x12,
  F(0x01,PairFrame) || S(0x02,PhysicalKey) || S(0x03,MemberState) ||
  S(0x04,SourcePartition) || F(0x05,SourceBoundaryId) ||
  SET(0x06, sorted U64(0x07,AllocatorGeneration)) ||
  SET(0x08, sorted U64(0x09,RecoveryGeneration)) ||
  SET(0x0a, sorted U64(0x0b,LifecycleGeneration)) ||
  U64(0x0c,AuthoritativeCeiling))
```

The header sets MUST exactly equal snapshot authority, not merely event-bearing
members. Member state is one of `active`, `retained`, or `retired`. A zero
eventless ceiling uses `U64(0x0c,0)`; all event positions remain positive.

Common record bytes are exactly:

```text
S(0x01,SourcePartition) || U64(0x02,SourceOrdinal) ||
S(0x03,MessageId) || S(0x04,TenantId) || S(0x05,Domain) ||
S(0x06,AggregateId) || S(0x07,AggregateType) ||
U64(0x08,AggregateSequence) || S(0x09,EventTypeName) ||
S(0x0a,SerializationFormat) || S(0x0b,ExactPersistedRfc3339TimestampUtf8) ||
I64(0x0c,ParsedUtcUnixSeconds) || U32(0x0d,ParsedNanoseconds) ||
I32(0x0e,ParsedOffsetMinutes) || F(0x0f,ExactPersistedPayloadBytes) ||
SET(0x10, sorted F(0x21,S(0x01,ExtensionKey)||S(0x02,ExtensionValue))) ||
S(0x11,CorrelationId) || S(0x12,CausationId) || S(0x13,UserId) ||
S(0x14,DomainServiceVersion) ||
SET(0x15, sorted F(0x22,S(0x01,MetadataKey)||F(0x02,ExactMetadataValueBytes)))
```

Extension and metadata maps sort by strict UTF-8 key bytes and reject duplicate
keys. Record union tags are `0x41` v1 and `0x42` v2:

```text
F(0x41, CommonRecord || (F(0x50,empty) for unknown zero, else U64(0x51,GlobalPosition)))
F(0x42, CommonRecord || F(0x52,PairFrame) || S(0x53,PhysicalKey) ||
  U64(0x54,CanonicalizationVersion) || U64(0x55,CutoverGeneration) ||
  U64(0x56,AllocatorGeneration) || U64(0x57,RecoveryGeneration) ||
  U64(0x58,Counter))
```

Fingerprint is lowercase hex SHA-256 of the complete byte sequence. Parsed
timestamp fields MUST equal the exact timestamp's strict parse; they prevent a
parser from ignoring offset/nanosecond semantics while exact bytes preserve
the persisted identity. `MetadataKey` captures every persisted field not
otherwise enumerated; therefore the declared integrity claim has no silent
persisted-field exclusion.

Section 18 vectors are normative and MUST be reproduced by two independent
implementations. Exact timestamp bytes that parse to the same instant but differ
lexically produce different planned/fingerprint bytes. A partition mapping or
header-set omission makes the input invalid rather than producing a digest.

## 14. Reproducible capacity and saturation gate

The evidence seed MUST pre-exist the evidence commit. The strict JCS input
manifest has exactly `schema`, `repository`, `baselineCommit`, `harnessBlob`,
`harnessSha256`, `traceArtifact`, `captureStartedAt`, `captureCompletedAt`,
`redactionAlgorithmBlob`, `redactionAlgorithmSha256`, `providerImageDigest`,
`providerConfigDigest`, `resiliencyDigest`, `topology`, `hostCount`,
`sidecarCount`, `hardwareDigest`, `warmupSeconds`, `measuredSeconds`,
`minimumOperations`, `offeredRates`, `repetitionCount`, `slos`,
`productionPeakArtifact`, and `seed`. Digests and commits use their canonical
lowercase forms, counts/rates/durations are positive JSON-number Int32,
`offeredRates` are unique ascending whole reservations/second, and artifacts use
section-10 references. `seed` is exactly 16 lowercase hex characters encoding
`first8(SHA-256(JCS(input manifest with seed="")))` in byte order. The evidence
commit may reference but cannot seed itself.

Run tenant, domain, and composite options at identical offered rates, each with
repetition indices `1..N`, `N>=30`. Index `i` uses deterministic shuffle
across every rate and option. Shuffle is Fisher-Yates from last index to one;
draw `j` uses successive big-endian UInt64 words from
`SHA-256(seedBytes||u64be(i)||u64be(drawBlock))`, discards a word when it is at
or above `floor(2^64/(j+1))*(j+1)`, and selects `word mod (j+1)`. This removes
modulo bias and is independently reproducible. Each measured repetition follows
at least 5 minutes warm-up and lasts at least 15 minutes and 1,000,000 offered
reservations. Units are successful reservations/second. Rate execution order is
also deterministically shuffled and recorded. The trace preserves observed
pair/domain/tenant skew and includes concurrent admission and retirement at
production p99 lifecycle rate.

For repetition `(option,rate,i)`, compute over the fixed measured window:

- achieved/offered ratio;
- disjoint success, application-error, infrastructure-error, and timeout counts;
  error rate is both error classes divided by offered count, timeout rate is
  timeout count divided by offered count, each with Wilson 95% intervals;
- HDRHistogram p50/p95/p99/p99.9 reservation latency;
- pair conflict/retry and archive lookup rates;
- for every catalog partition, ordinary-least-squares queue-depth slope in
  items/second over one-second samples, its two-sided 95% Newey-West interval
  with lag 60, maximum depth, and final depth. Missing samples, fewer than 900
  samples, or a singular regression fail the repetition.

A repetition passes only when achieved/offered >= 0.98, upper Wilson error and
timeout <= 0.001, p99 <= 100 ms, p99.9 <= 250 ms, every control queue slope
95% CI upper bound <= 0.05 items/s, maximum depth <= 100, final depth <= 10,
and no identity/integrity failure occurs. For each paired repetition index, its
stable ceiling is the highest rate in the contiguous passing prefix beginning
at the lowest offered rate. A failure followed by a higher-rate pass does not
erase the failure. If the lowest rate fails, ceiling is `0` (left failure). If
every rate passes, ceiling is right-censored at the highest tested lower bound.

The option estimator is the lower median paired ceiling (sorted element
`ceil(N/2)`). One-sided 95% lower confidence bounds use 100,000 deterministic
bootstrap resamples of repetition indices with replacement, resampling the same
index across all options. The stream is the unbiased word/rejection algorithm
above seeded by `SHA-256(seedBytes||ASCII("ceiling-bootstrap"))`; the bound is
the nearest-rank 5th percentile. Right-censored samples contribute their tested
lower bound; zero samples remain zero. Each resample also computes the lower
median of `composite - 1.20*max(tenant,domain)` for its paired indices. The
composite gate passes only when the 5th percentile of that paired advantage is
strictly positive, production peak is strictly below half the composite lower
bound, and no composite repetition is a lowest-rate failure. If the highest
rate is all passing, the result is explicitly a tested lower bound, not a
saturation claim. Raw one-second series, histograms, requests, failures, and
lifecycle transcripts are immutable evidence.

Any harness/provider/config/input change, evidence older than 90 days, higher
production peak, failed control queue test, invalid exception, or review-policy
change invalidates eligibility and requires rerun/reapproval.

## 15. Evidence matrix for later implementation

Every row is blocking and MUST inspect persisted state, not mocks or HTTP status
alone.

| Case | Required production-path proof |
|---|---|
| First and existing pair | counters start at 1 only for registered new state; checked continuation and full tagged metadata |
| Multi-event command | preflight, gapless aggregate sequence, exact ordered MessageIds, contiguous pair counters, one atomic reservation |
| Duplicate/conflicting/late retry | original result/range replay; differing batch permanent conflict; archived identity permanent expiry result without allocation |
| Multi-host contention | at least two independent hosts/sidecars share provider; persisted ranges unique and monotonic per exact lineage |
| Crash points | kill after every reservation/catalog/cutover/restore phase write; recovery resumes one operation and never infers activation |
| Restart/failover | actor, sidecar, host, provider primary and cursor-state failover preserve identity and result |
| Mixed v1/v2 | immutable v1 values and MessageIds; grouped pages; every forbidden comparison returns the exact unsupported outcome |
| Partial fleet/downgrade | revoked old writer cannot allocate/commit; post-reservation downgrade is forward-fix only |
| Bootstrap/admission/retirement | source transcripts reconcile; concurrent operations obey CAS; retired identity never reactivates |
| Cursor/checkpoint | both inline/reference modes prove exact magic/version/header-length/header-JCS/AAD/nonce/ciphertext-length/tag framing; canonical unpadded base64url at 10,923-character and 8,192-byte boundaries; reject padding, alphabet/re-encode mismatch, truncation, trailing bytes, length mismatch, noncanonical header/payload JCS, bad tag, and mode mismatch before allocation/source access; also prove auth precedence, key rotation, split groups, oversize record, renewal, unavailable state, and no partial advance |
| Projection/rebuild | before-first migration, atomic model/checkpoint/set rebase, all measured rebuilds within boundary liveness |
| Backup/restore | one consistent boundary; full monotonic union; conflicting equal key fails; next counter strictly above ceiling |
| Overflow and limits | Int64/UInt32/64-KiB/1-MiB/204-byte limits fail before mutation with named outcomes |
| Hot pair/catalog/archive | capacity estimator, control-queue stability, continuous compaction, provider backpressure |
| Identity stability | persisted `MessageId`, CloudEvent id, aggregate identity/sequence, exact timestamp/payload bytes do not change |
| Fingerprints/vectors | two implementations reproduce every byte/vector and reject duplicate/conflicting/source-mismatched input |

## 16. Immutable specification and evidence manifests

The specification identity is the commit that adds exactly this successor and
its story wrapper, the successor Git blob, full-file SHA-256, and normative
SHA-256. Later benchmark/bootstrap/provider evidence lives in a different
immutable evidence commit. Evidence is never inserted by editing the approved
successor.

The evidence manifest path is exactly
`_bmad-output/implementation-artifacts/evidence/story-4-6/<candidateCommit>/<evidenceCommit>/evidence-manifest.json`.
The verifier is given its evidence commit and path, resolves its Git blob,
requires the path to be added by that commit, and recomputes the blob, exact
bytes, JCS, and SHA-256 before trusting any member. The evidence commit may add
only that manifest and the repo-relative artifacts it lists under the same
directory; URI artifacts are not repository paths. This evidence scope is
validated baseline-to-evidence with name-status, including deletions and
renames.

The strict evidence manifest has exactly these members:

```json
{"artifacts":[{"path":"<repo-relative-or-immutable-uri>","sha256":"<64-lower-hex>","size":"<positive-decimal-string>","type":"<enumerated-type>"}],"candidateCommit":"<40-lower-hex>","candidateNormativeSha256":"<64-lower-hex>","candidateSuccessorBlob":"<40-lower-hex>","evidenceCommit":"<40-lower-hex>","inputManifestBlob":"<40-lower-hex>","inputManifestSha256":"<64-lower-hex>","rolePolicyBlob":"<40-lower-hex>","rolePolicyCommit":"<40-lower-hex>","rolePolicyPath":"<repo-relative>","rolePolicySha256":"<64-lower-hex>","schema":"hexalith.gpos.evidence-manifest/v1"}
```

Members and artifact objects are strict, duplicate-free, and JCS canonical.
Artifacts sort by UTF-8 path/URI and have unique locations. Exactly one artifact
is required for every enumerated type except `bootstrap-filtered`,
`enumeration-transcript`, `source-attestation`, and `backup-partition`; the
first three each require the same source-authority count of at least two and
exactly one of each type per authority, while `backup-partition` requires one
per boundary-manifest partition. The enumerated types are `benchmark-input`, `benchmark-raw`,
`benchmark-summary`, `bootstrap-precheck`, `bootstrap-filtered`,
`bootstrap-final`, `enumeration-transcript`, `drain-transcript`,
`source-attestation`, `provider-profile`, `key-limit-e2e`, `cursor-liveness`,
`backup-restore`, `backup-partition`, `fingerprint-vectors`, and
`scope-validation`. Unknown,
missing, excess, duplicate, or authority-mismatched artifacts fail closed.
Local paths resolve from `evidenceCommit` and their Git blob and bytes are
verified; URI artifacts require an immutable provider object version and
returned byte digest. `inputManifestBlob` and digest MUST identify the sole
`benchmark-input` artifact, and every role-policy identity MUST reproduce the
exact policy artifact used by section 17.

## 17. Native human approval and policy evolution

Approval records are immutable external evidence stored only after review at
`_bmad-output/implementation-artifacts/evidence/story-4-6/<candidateCommit>/<evidenceCommit>/approvals/<approvalRecordSha256>.json`
or an equally immutable content-addressed URI. They MUST NOT be appended to,
replaced, or used to modify this successor. Approval consumption receives an
explicit immutable locator `(recordCommit,path,blob,sha256)` or versioned URI;
the record cannot select or authenticate itself. The strict record is:

```json
{"approvalRecordSha256":"<digest-of-JCS-with-this-member-empty>","candidateCommit":"<40-lower-hex>","candidateNormativeSha256":"<64-lower-hex>","candidateSuccessorBlob":"<40-lower-hex>","evidenceCommit":"<40-lower-hex>","evidenceManifestSha256":"<64-lower-hex>","githubRepository":"Hexalith/Hexalith.EventStore","githubRepositoryId":"<positive-decimal-string>","observedMainCommit":"<40-lower-hex>","policyComparison":"unchanged","priorRolePolicy":{"blob":"<40-lower-hex>","commit":"<40-lower-hex>","path":"<repo-relative>","sha256":"<64-lower-hex>"},"pullRequest":{"base":"main","headSha":"<evidenceCommit>","mergeCommitSha":"<40-lower-hex>","merged":true,"mergedAt":"<rfc3339>","number":"<positive-decimal-string>","state":"closed","url":"<github-url>"},"reviews":[{"authorAssociation":"<association>","commitId":"<evidenceCommit>","dismissed":false,"githubUserId":"<positive-decimal-string>","htmlUrl":"<github-url>","login":"<login>","reviewId":"<positive-decimal-string>","role":"architecture_owner","state":"APPROVED","submittedAt":"<rfc3339>"}],"rolePolicy":{"blob":"<40-lower-hex>","commit":"<40-lower-hex>","path":"<repo-relative>","sha256":"<64-lower-hex>"},"schema":"hexalith.gpos.approval-record/v1"}
```

`approvalRecordSha256` is lowercase SHA-256 of exact JCS with that member set
to the empty string; the filename, explicit locator digest, and recomputation
MUST agree. This avoids a commit-hash or file-hash self-reference.

The verifier fetches native GitHub repository, PR, files, commits, reviews,
dismissals, users, and current main through the API. It requires repository
name and immutable repository ID, PR base `main`, head/evidence commit equality,
the PR closed and merged with the recorded merge commit/time, review
`commit_id` equality, `observedMainCommit` equal to the API main head observed
when the record was created, and the current main to descend from both observed
main and the merged evidence commit. The successor blob at observed and current
main MUST equal the candidate blob. Review HTML/body JSON alone is not
authority. GitHub login is resolved to immutable GitHub user ID and current role
membership.

For each architecture owner in the current immutable role policy, fetch every
review. The latest actionable review orders by parsed `submitted_at`, then
numeric review ID; no unavailable or mutable `updated_at` field participates.
Dismissed reviews are non-actionable. A malformed or unavailable latest review
fails closed. Every current owner's latest actionable decision MUST be
`APPROVED`; any `CHANGES_REQUESTED` or conflicting current decision blocks.

The candidate-bound prior role policy and current policy are fetched by exact
commit/path/blob/digest. Each policy login is resolved through the native user
API and the compared policy set is sorted `(role, immutable user ID, login)`;
duplicate IDs/logins or a changed resolution fail closed. Review history is
separately compared for every recorded dissent. Any owner addition/removal,
login-ID change, role change, or removed/changed dissent makes
`policyComparison` non-`unchanged` and requires a new approval by the entire
current owner set.
Current role membership is revalidated at consumption. An approval record never
implicitly supersedes another: the downstream planning authorization names the
exact locator it consumed, and any later policy/evidence/candidate identity
requires a new explicitly selected record. Evidence/policy drift, stale
candidate, unmerged or mismatched PR, malformed record, or absent review leaves
this specification unapproved.

Approval makes only a later implementation story eligible for planning. A
separate explicitly authorized story is REQUIRED for runtime changes, migration,
deployment, and production cutover.

## 18. Normative vectors and executable validation

The checked-in evidence implementation MUST reproduce these vector definitions
without copying computed output from the other implementation:

- `physical-small-v1`: the exact frame/key in section 3.
- `physical-max-v1`: tenant=`0x41` repeated 64 and domain=`0x42` repeated 64;
  pair frame length 142, base64url length 190, physical key length 204.
- `fingerprint-empty-v2`: committed source `source-v1`, boundary `boundary-0`,
  the canonical empty shard set with catalog version 1, transition `initial`,
  source boundary `boundary-0`, one `v1-active` authority entry with catalog,
  cutover, permit, and accepted-v1 generation all `1`, zero operations,
  partitions, and records. Its exact complete bytes and digest are declared in
  the vector table below.
- `fingerprint-mixed-v2`: one positive v1 record and one v2 record for
  `tenant-a/orders`, including exact timestamp bytes
  `2026-08-27T00:00:00.0000000+00:00`, one eventless retained pair, two allocator
  generations, two recovery generations, payload bytes, and a two-entry
  extension map. The fixture must publish the complete input JSON, canonical
  byte file, byte count, and SHA-256.
- `planned-batch-v1-a` and `planned-batch-v1-b`: identical one-event plans for
  pair `tenant-a/orders`, aggregate `aggregate-2/order`, `msg-plan`, sequence
  `1`, `OrderPlaced`, `json`, payload `{}`, no extensions, correlation/cause/user
  `corr-plan/cause-plan/user-plan`, service `2.0.0`, and empty other-metadata
  bytes. A persists `2026-08-27T00:00:00Z`; B persists
  `2026-08-27T00:00:00+00:00`. Both parse to the same instant but exact bytes
  and planned digests MUST differ.

`fingerprint-mixed-v2` has these exact inputs and no implicit/default fields:
catalog `7`, transition `transition-7`, boundary `boundary-mixed`; active
`tenant-a/orders` member (membership `5`, lifecycle `30`, allocators `10,11`,
recoveries `20,21`); retained eventless `tenant-b/billing` member (membership
`6`, lifecycle `31`, allocator `12`, recovery `22`); authority
`v2-irreversible` (catalog `7`, cutover `3`, permit `4`, accepted v1 `1,2`,
operations `op-a,op-b`). The active partition ceiling is `42`; retained is `0`.
The v1 record is source `global-v1`, ordinal `1`, `msg-v1`,
`tenant-legacy/legacy/aggregate-1/widget`, sequence `1`, `WidgetCreated`, `json`,
payload `{}`, extension `a=1`, correlation/causation/user `corr-1/cause-1/user-1`,
service `1.0.0`, metadata `metadataVersion` exact byte `1`, and position `9`.
The v2 record is active source ordinal `1`, `msg-v2`,
`tenant-a/orders/aggregate-2/order`, sequence `1`, `OrderPlaced`, `json`, payload
`{"amount":1}`, extensions `a=1,b=2`, correlation/causation/user
`corr-2/cause-2/user-2`, service `2.0.0`, metadata `metadataVersion` exact byte
`2`, and canonicalization/cutover/allocator/recovery/counter `1/3/10/20/42`.
Both timestamps use the exact bytes above, parsed Unix seconds `1787788800`,
nanoseconds `0`, offset minutes `0`.

| Vector | Complete canonical byte identity | Expected SHA-256 |
|---|---|---|
| `fingerprint-empty-v2` | 225 bytes; base64url `SFgtR1BPUy1ISVNUT1JZAgEAAAAJc291cmNlLXYxAgAAAApib3VuZGFyeS0wAwAAADtIWC1HUE9TLVNIQVJELVNFVAEAAAAAAAAAAQAAAAdpbml0aWFsAAAACmJvdW5kYXJ5LTAAAAAAAAAAAAQAAABdAAAAAREAAABUAQAAAAl2MS1hY3RpdmUCAAAACAAAAAAAAAABAwAAAAgAAAAAAAAAAQQAAAAIAAAAAAAAAAEFAAAAEQAAAAEGAAAACAAAAAAAAAABBwAAAAQAAAAABQAAAAQAAAAABgAAAAQAAAAA` | `0b4bc930e836765ca62318cb716b48c5316159fc381a09bfb174952740012aa4` |
| `fingerprint-mixed-v2` | 2124 bytes; base64url `SFgtR1BPUy1ISVNUT1JZAgEAAAAJc291cmNlLXYxAgAAAA5ib3VuZGFyeS1taXhlZAMAAAF8SFgtR1BPUy1TSEFSRC1TRVQBAAAAAAAAAAcAAAAMdHJhbnNpdGlvbi03AAAADmJvdW5kYXJ5LW1peGVkAAAAAQEAAAAcSFhTSDIBAAAACHRlbmFudC1hAAAABm9yZGVycwAAADRoeC1ncG9zLXYyLXNoOlNGaFRTRElCQUFBQUNIUmxibUZ1ZEMxaEFBQUFCbTl5WkdWeWN3AAAAAAAAAAUAAAAAAAAAHgAAAAIAAAAAAAAACgAAAAAAAAALAAAAAgAAAAAAAAAUAAAAAAAAABUAAAAOYm91bmRhcnktbWl4ZWQAAAABAgAAAB1IWFNIMgEAAAAIdGVuYW50LWIAAAAHYmlsbGluZwAAADVoeC1ncG9zLXYyLXNoOlNGaFRTRElCQUFBQUNIUmxibUZ1ZEMxaUFBQUFCMkpwYkd4cGJtYwAAAAAAAAAGAAAAAAAAAB8AAAABAAAAAAAAAAwAAAABAAAAAAAAABYAAAAOYm91bmRhcnktbWl4ZWQEAAAAggAAAAERAAAAeQEAAAAPdjItaXJyZXZlcnNpYmxlAgAAAAgAAAAAAAAABwMAAAAIAAAAAAAAAAMEAAAACAAAAAAAAAAEBQAAAB4AAAACBgAAAAgAAAAAAAAAAQYAAAAIAAAAAAAAAAIHAAAAFgAAAAIIAAAABG9wLWEIAAAABG9wLWIFAAACcgAAAAISAAABJwEAAAAdSFhTSDIBAAAACHRlbmFudC1iAAAAB2JpbGxpbmcCAAAANWh4LWdwb3MtdjItc2g6U0ZoVFNESUJBQUFBQ0hSbGJtRnVkQzFpQUFBQUIySnBiR3hwYm1jAwAAAAhyZXRhaW5lZAQAAABXdGVuYW50LWRvbWFpbi12Mjo0OWUxODIxYTkyNmNlYzRlOTlmMzU0NTQ4Nzg2MDZhMTQyY2M4MmU3ODc1YzBjYThiMTI0YTdiYTNlZWU1ODJlOjEyOjIyBQAAAA5ib3VuZGFyeS1taXhlZAYAAAARAAAAAQcAAAAIAAAAAAAAAAwIAAAAEQAAAAEJAAAACAAAAAAAAAAWCgAAABEAAAABCwAAAAgAAAAAAAAAHwwAAAAIAAAAAAAAAAASAAABPQEAAAAcSFhTSDIBAAAACHRlbmFudC1hAAAABm9yZGVycwIAAAA0aHgtZ3Bvcy12Mi1zaDpTRmhUU0RJQkFBQUFDSFJsYm1GdWRDMWhBQUFBQm05eVpHVnljdwMAAAAGYWN0aXZlBAAAAFd0ZW5hbnQtZG9tYWluLXYyOjRkNzkzOTlhYWJmZTM3MTk2YmRlYzg1N2Y0Y2YwNTU0MTQ4MDc3NTFjNWFjYTljY2MwMTg2NzUxZTUyYjgxMjI6MTA6MjAFAAAADmJvdW5kYXJ5LW1peGVkBgAAAB4AAAACBwAAAAgAAAAAAAAACgcAAAAIAAAAAAAAAAsIAAAAHgAAAAIJAAAACAAAAAAAAAAUCQAAAAgAAAAAAAAAFQoAAAARAAAAAQsAAAAIAAAAAAAAAB4MAAAACAAAAAAAAAAqBgAAA5cAAAACQQAAAU0BAAAACWdsb2JhbC12MQIAAAAIAAAAAAAAAAEDAAAABm1zZy12MQQAAAANdGVuYW50LWxlZ2FjeQUAAAAGbGVnYWN5BgAAAAthZ2dyZWdhdGUtMQcAAAAGd2lkZ2V0CAAAAAgAAAAAAAAAAQkAAAANV2lkZ2V0Q3JlYXRlZAoAAAAEanNvbgsAAAAhMjAyNi0wOC0yN1QwMDowMDowMC4wMDAwMDAwKzAwOjAwDAAAAAgAAAAAao9-AA0AAAAEAAAAAA4AAAAEAAAAAA8AAAACe30QAAAAFQAAAAEhAAAADAEAAAABYQIAAAABMREAAAAGY29yci0xEgAAAAdjYXVzZS0xEwAAAAZ1c2VyLTEUAAAABTEuMC4wFQAAACMAAAABIgAAABoBAAAAD21ldGFkYXRhVmVyc2lvbgIAAAABMVEAAAAIAAAAAAAAAAlCAAACPAEAAABXdGVuYW50LWRvbWFpbi12Mjo0ZDc5Mzk5YWFiZmUzNzE5NmJkZWM4NTdmNGNmMDU1NDE0ODA3NzUxYzVhY2E5Y2NjMDE4Njc1MWU1MmI4MTIyOjEwOjIwAgAAAAgAAAAAAAAAAQMAAAAGbXNnLXYyBAAAAAh0ZW5hbnQtYQUAAAAGb3JkZXJzBgAAAAthZ2dyZWdhdGUtMgcAAAAFb3JkZXIIAAAACAAAAAAAAAABCQAAAAtPcmRlclBsYWNlZAoAAAAEanNvbgsAAAAhMjAyNi0wOC0yN1QwMDowMDowMC4wMDAwMDAwKzAwOjAwDAAAAAgAAAAAao9-AA0AAAAEAAAAAA4AAAAEAAAAAA8AAAAMeyJhbW91bnQiOjF9EAAAACYAAAACIQAAAAwBAAAAAWECAAAAATEhAAAADAEAAAABYgIAAAABMhEAAAAGY29yci0yEgAAAAdjYXVzZS0yEwAAAAZ1c2VyLTIUAAAABTIuMC4wFQAAACMAAAABIgAAABoBAAAAD21ldGFkYXRhVmVyc2lvbgIAAAABMlIAAAAcSFhTSDIBAAAACHRlbmFudC1hAAAABm9yZGVyc1MAAAA0aHgtZ3Bvcy12Mi1zaDpTRmhUU0RJQkFBQUFDSFJsYm1GdWRDMWhBQUFBQm05eVpHVnljd1QAAAAIAAAAAAAAAAFVAAAACAAAAAAAAAADVgAAAAgAAAAAAAAAClcAAAAIAAAAAAAAABRYAAAACAAAAAAAAAAq` | `6c78cee9cbcd5cbe80325a0079f9be3bb3a9d5e26423273e72ebd38b5ec714aa` |
| `planned-batch-v1-a` | 236 bytes; base64url `SFgtR1BPUy1QTEFOASAAAAAcSFhTSDIBAAAACHRlbmFudC1hAAAABm9yZGVycyEAAAALYWdncmVnYXRlLTIiAAAABW9yZGVyIwAAAJ8AAAABMQAAAJYBAAAACG1zZy1wbGFuAgAAAAgAAAAAAAAAAQMAAAALT3JkZXJQbGFjZWQEAAAABGpzb24FAAAAAnt9BgAAAAQAAAAACgAAABQyMDI2LTA4LTI3VDAwOjAwOjAwWgsAAAAJY29yci1wbGFuDAAAAApjYXVzZS1wbGFuDQAAAAl1c2VyLXBsYW4OAAAABTIuMC4wDwAAAAA` | `dee929bc1a166f02d49073ad0ec27ba3095c5bd91d298213db30683ba6f9fb6f` |
| `planned-batch-v1-b` | 241 bytes; base64url `SFgtR1BPUy1QTEFOASAAAAAcSFhTSDIBAAAACHRlbmFudC1hAAAABm9yZGVycyEAAAALYWdncmVnYXRlLTIiAAAABW9yZGVyIwAAAKQAAAABMQAAAJsBAAAACG1zZy1wbGFuAgAAAAgAAAAAAAAAAQMAAAALT3JkZXJQbGFjZWQEAAAABGpzb24FAAAAAnt9BgAAAAQAAAAACgAAABkyMDI2LTA4LTI3VDAwOjAwOjAwKzAwOjAwCwAAAAljb3JyLXBsYW4MAAAACmNhdXNlLXBsYW4NAAAACXVzZXItcGxhbg4AAAAFMi4wLjAPAAAAAA` | `fccf0dd0d2a6ef25eecd4630a2ffb0925cc7772a289e49090f7ea6ec0b0a69ba` |

Before evidence or approval, execute these fail-closed checks:

1. strict UTF-8, LF/no-BOM, unique ordered normative markers, one digest
   declaration, and equality of declared/computed normative SHA-256;
2. predecessor Git blob/file/frozen-inner/frozen-element identities;
3. the exact 19 expected IDs, ranges, byte hashes, nonempty dispositions parsed
   from normative bytes only, plus rejection of disposition-shaped rows outside;
4. strict JSON schemas/JCS/digests for input, evidence, bootstrap, cursor state,
   and approval fixtures;
5. two independent physical/fingerprint vector implementations and all negative
   duplicate, overflow, source-mapping, and timestamp-byte cases, plus cursor
   wire tests for inline and reference modes covering wrong magic/version,
   header/ciphertext length underflow/overflow/mismatch, raw/text boundary
   values, padding/invalid alphabet/re-encode mismatch, truncated nonce/tag,
   trailing bytes, noncanonical JCS, decryption failure, and zero state/source/
   allocation access for every structural failure;
6. scope validation below.

Pre-commit scope from baseline permits only these two paths and forbids any
tracked or untracked third path:

```bash
set -euo pipefail
baseline=5ddda34f2ff0ffb0f72a60c44b265f2e4838a332
allowed_a='_bmad-output/implementation-artifacts/spec-4-6-global-position-sharding-spec-renegotiation.md'
allowed_b='_bmad-output/implementation-artifacts/spec-dapr-global-event-ordering-v2.md'
git diff --quiet "$baseline" -- _bmad-output/implementation-artifacts/spec-dapr-global-event-ordering.md _bmad-output/implementation-artifacts/sprint-status.yaml src tests .github
python3 - "$baseline" "$allowed_a" "$allowed_b" <<'PY'
import subprocess, sys
baseline, *allowed = sys.argv[1:]
rows = subprocess.check_output(['git','diff','--name-status','--diff-filter=ACDMRTUXB',baseline,'--']).decode().splitlines()
untracked = subprocess.check_output(['git','ls-files','--others','--exclude-standard']).decode().splitlines()
paths = []
for row in rows:
    columns = row.split('\t')
    assert len(columns) >= 2 and '\\t' not in row
    paths.extend(columns[1:])
assert set(paths + untracked) <= set(allowed), (rows, untracked)
assert '\t' in 'A\tpath' and '\\t' not in 'A\tpath'
PY
git diff --no-index --check /dev/null "$allowed_a" || test $? -eq 1
git diff --no-index --check /dev/null "$allowed_b" || test $? -eq 1
```

Post-commit validation requires exact actual-tab name-status rows `A<TAB>path`
for the two allowlisted files relative to baseline, no rename/deletion/other
status, and an empty tracked/untracked worktree. The validator MUST self-test
that literal backslash-`t` is rejected. Execute exactly:

```bash
set -euo pipefail
baseline=5ddda34f2ff0ffb0f72a60c44b265f2e4838a332
allowed_a='_bmad-output/implementation-artifacts/spec-4-6-global-position-sharding-spec-renegotiation.md'
allowed_b='_bmad-output/implementation-artifacts/spec-dapr-global-event-ordering-v2.md'
python3 - "$baseline" "$allowed_a" "$allowed_b" <<'PY'
import subprocess, sys
baseline, allowed_a, allowed_b = sys.argv[1:]
raw = subprocess.check_output([
    'git', 'diff', '--name-status', '--diff-filter=ACDMRTUXB',
    f'{baseline}..HEAD', '--'
]).decode('utf-8', errors='strict')
rows = raw.splitlines()
assert rows == [f'A\t{allowed_a}', f'A\t{allowed_b}'], rows
assert all('\t' in row and '\\t' not in row for row in rows)
assert '\t' in 'A\tpath' and '\\t' not in 'A\tpath'
assert 'A\\tpath'.split('\t') == ['A\\tpath']
status = subprocess.check_output([
    'git', 'status', '--porcelain', '--untracked-files=all'
]).decode('utf-8', errors='strict')
assert status == '', status
PY
```

A commit, green status row, or successful validator is evidence only and never
approval.
<!-- HX-GPOS-V2-NORMATIVE-END -->

## Content identity

| Identity | Value |
|---|---|
| Normative content SHA-256 | `2310f121dc48f59713a9c6bbc6ffe2e63be374d4d8ecc6e8d710a0d9cf3674de` |
| Normative byte range | Bytes after the unique begin marker LF through the byte before the unique end marker |
| Encoding | Strict UTF-8, LF, no BOM |

## Detached approval status

No evidence manifest or exact-content human approval exists. The current v1
allocator remains authoritative. Produce the section 16 evidence packet in a
later immutable commit, obtain and verify section 17 approval, then create a
separate authorized implementation story. Do not edit this file to attach the
approval record.
