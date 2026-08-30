---
title: 'DAPR Global Event Ordering v2: Composite Shard-Local Positions'
type: 'architecture-specification'
created: '2026-08-27'
status: 'awaiting-operator'
schema_version: 2
predecessor_path: '_bmad-output/implementation-artifacts/spec-dapr-global-event-ordering.md'
predecessor_blob: '4c9edb37a8616aa373bd0054057c9e8eace6e0fa'
predecessor_sha256: '4bcff794a3c926f45e790ce462d562799db998feeab24aab1527ac6cf9ef1893'
normative_sha256: '995fcecd16b3421ec9ff666d0884bfb5e436932aa49529c152fb7c439172a1fd'
superseded_normative_sha256: '2310f121dc48f59713a9c6bbc6ffe2e63be374d4d8ecc6e8d710a0d9cf3674de'
approval_state: 'absent'
implementation_authorized: false
operator_actions:
  - 'Approve the exact committed successor as every architecture_owner resolved from the candidate commit immutable allowlist, binding each approval to the candidate commit, successor blob, normative SHA-256, and reviewed content.'
  - 'Commission and preserve every production-provider and topology evidence category required by successor section 7 against the approved successor identity.'
  - 'Authorize a separately reviewed implementation story only after exact-content approval and every blocking evidence category are satisfied.'
---

# DAPR Global Event Ordering v2: Composite Shard-Local Positions

This document is a successor specification, not a runtime change. The frozen
v1 specification and single global allocator remain the only production
authority until the approval and evidence gates below are satisfied through a
separately authorized implementation story.

<!-- HX-GPOS-V2-NORMATIVE-BEGIN -->
# Normative contract

The key words MUST, MUST NOT, REQUIRED, SHALL, SHALL NOT, SHOULD, SHOULD NOT,
and MAY are normative. A component that cannot positively establish a required
identity, comparison domain, authority state, or evidence result MUST fail
closed.

## 1. Authority, predecessor identity, and unchanged guarantees

The protected predecessor is exactly:

| Identity | Required value |
|---|---|
| Baseline commit | `5ddda34f2ff0ffb0f72a60c44b265f2e4838a332` |
| Git blob | `4c9edb37a8616aa373bd0054057c9e8eace6e0fa` |
| Complete file SHA-256 | `4bcff794a3c926f45e790ce462d562799db998feeab24aab1527ac6cf9ef1893` |
| Frozen inner bytes SHA-256 | `90be324c35d1545fd7c4dd53393ef27b08d2e6a3891d1bc9c6f38c9145740c10` |
| Complete frozen element SHA-256 | `c827761ba1f58aa6fde85ca8acedfdfdcc5097cbcbd470d2887a1e4d073d5d2c` |
| Range convention | Strict UTF-8 LF bytes; lines are one-based; byte ranges are zero-based, start-inclusive, and end-exclusive within the line without LF |

All five identities MUST reproduce before this successor is considered. Any
predecessor drift requires a new renegotiation.

Existing positive v1 `globalPosition` values remain immutable identities in
scheme `global-v1`. Existing event `MessageId`, CloudEvent id, aggregate
identity, aggregate sequence, timestamp, payload, and metadata remain
immutable. Aggregate sequence MUST remain gapless and positive. CloudEvent id
MUST remain the persisted event `MessageId`. Allocation MAY leave gaps when a
reservation is not followed by an aggregate commit; neither v1 nor v2 position
labels promise commit order.

### 1.1 Exact disposition of every frozen predecessor clause

The rows below MUST be parsed only from this normative byte range. Each source
range and SHA-256 MUST reproduce from the protected predecessor, and the exact
set of 19 clause IDs MUST be present once.

| Clause ID | Exact predecessor range | Source bytes SHA-256 | Disposition |
|---|---|---|---|
| `V1-PROBLEM-01` | `L15:B13-B152` | `0c68cd6d7d0f2c094d287ed44055803615d70374d5fa6896e48907e8979bd427` | Retained for immutable v1 history; superseded for new v2 writes by the versioned composite position in section 3. |
| `V1-PROBLEM-02` | `L15:B153-B317` | `6edf7a21a1cd7be910fa0305694e45c85f7812547466a48134219fc3f7571f83` | Retained without amendment: persisted `MessageId` remains CloudEvent identity. |
| `V1-APPROACH-01` | `L17:B14-B143` | `69485cd508cd8b029e17f3fb7bc547214d6abac5ac19983a00036f192ef9af5b` | Superseded for v2 writes by one allocation authority per canonical tenant+domain shard; v1 remains authoritative until authorized cutover. |
| `V1-APPROACH-02` | `L17:B145-B276` | `292f6a8c92901b8613d1ca1f2ec1a5835d5f32489864f782f31880bbe8a10803` | Amended: a v2 position is a shard-local allocation label, while event identity remains `MessageId`. |
| `V1-APPROACH-03` | `L17:B277-B384` | `cba5cd2562f1295274d034ee68c5caad2012d796ebc444e06538a1b553e918f1` | Retained: a duplicate command replays its original complete result and cannot acquire a different position identity. |
| `V1-ALWAYS-01` | `L21:B12-B78` | `24f68313de132e96bc578232fb45bb0e0c9b0281a4fc8fd3a6b7b187076c80c1` | Retained exactly: aggregate-local sequence remains gapless. |
| `V1-ALWAYS-02` | `L21:B79-B131` | `c80a0344e68fb4272404b89b92ea0d13a4b51578dac61345ca2060ef4bc51e35` | Retained unless a separately approved append/persistence renegotiation changes it after Story 4.5. |
| `V1-ALWAYS-03` | `L21:B132-B215` | `4cc4c1b73f6421db7072c555aae9c359378b704264a302d0105e66c3d18dc60a` | Amended only by a later authorized implementation: v2 composite allocation becomes authoritative after the rollout gates in section 6. |
| `V1-ALWAYS-04` | `L21:B216-B261` | `ae9052038db96ca79619ea3638b31da38174531b1dbd1a0e8e5e70c8c84aaf28` | Retained for every future awaited implementation call. |
| `V1-ASK-01` | `L23:B15-B81` | `10af81fc0e662671e4e4c9cb25194bef9a59d600403f9f8dc28b158f2de57e3d` | Retained as a separate human boundary; this successor does not move event persistence. |
| `V1-ASK-02` | `L23:B83-B126` | `58545c552fe030a77258c1ebd1af20edcbffb1cea173194eb0840d3de1c75543` | Retained as a separate human boundary; section 3 specifies future versioned semantics but changes no public contract. |
| `V1-ASK-03` | `L23:B131-B194` | `1ae24060b647e77a702b1d4ba8e33e95db35a58686e440cd6e67e348b682fa90` | Retained: every provider dependency beyond current DAPR actor state requires explicit downstream authorization. |
| `V1-NEVER-01` | `L25:B11-B76` | `fb3495c3d6c9e0b3045ed876a65289776e606e21e12e9141195317553010548f` | Retained exactly: no process-local production counter may grant position authority. |
| `V1-NEVER-02` | `L25:B77-B162` | `d35c61b7fc7112389855ef766a32605dc55728f2b34cc67bc38b054166723875` | Retained exactly: CloudEvent identity is the persisted event `MessageId`. |
| `V1-NEVER-03` | `L25:B163-B227` | `eb7af1f2df5fd0291e06ada3f868715ee0bf8f791c23e4dfce29192856736e11` | Retained: this specification changes no projection replay implementation. |
| `V1-MATRIX-01` | `L31:B0-B192` | `537c943ab6fd978efb4e904316a6f5ee2ebc79a07f054763279ad51c723caff1` | Retained for v1; a newly authorized v2 shard starts at counter `1` in a never-before-used generation. |
| `V1-MATRIX-02` | `L32:B0-B197` | `084b4196420fb288edb4defc630e4e65d912015a95895b75d77422329c43f699` | Amended: each v2 comparison partition advances its own checked positive Int64 counter and fails before mutation on overflow. |
| `V1-MATRIX-03` | `L33:B0-B145` | `973e786b859b14b84d0012ed74216ff32cf10097d0898db32458634396ec7110` | Retained: retries return the same stored result and position identities; a conflicting retry fails permanently. |
| `V1-MATRIX-04` | `L34:B0-B180` | `4656e87697efd4547a9ea51ff987f17e346840bbbfccfcbad55e87d19f6ad355` | Retained exactly, including existing publication failure handling and deduplication identity. |

## 2. Shard selection

The v2 shard owner is the exact canonical `(TenantId, Domain)` pair carried by
the aggregate and every event. The selected strategy is
`composite-tenant-domain-v2`, subject to sections 6 and 7.

| Criterion | Tenant shard | Domain shard | Composite tenant+domain shard |
|---|---|---|---|
| Allocation owner | One authority per tenant | One authority per domain | One authority per exact tenant+domain pair |
| Contention | Separates tenants but couples a tenant's unrelated domains | Separates domains but couples unrelated tenants | Separates both dimensions; contention is isolated to one pair |
| Uniqueness boundary | Tenant plus generation | Domain plus generation | Tenant+domain plus generation |
| Monotonicity boundary | Tenant plus generation | Domain plus generation | Tenant+domain plus generation |
| Gaps and commit order | Gaps allowed; not commit order | Gaps allowed; not commit order | Gaps allowed; not commit order |
| Hot-shard behavior | One hot tenant affects every domain it owns | One hot domain affects every tenant using it | One hot pair affects that pair; lifecycle control MUST NOT become the per-reservation hot path |
| Recovery scope | All domains for a tenant | All tenants for a domain | One pair, while preserving its immutable generation lineage |
| Scaling characteristic | Tenant cardinality and skew | Domain cardinality and skew | Pair cardinality and pair skew; independently distributable |
| Provider dependency | Durable tenant-local allocation | Durable domain-local allocation | Durable pair-local allocation plus non-hot-path lifecycle authority |
| Decision | Rejected: cross-domain coupling violates the isolation goal | Rejected: cross-tenant coupling violates the isolation goal | Selected only when production-path capacity and recovery evidence pass |

Composite selection is measurable, not assumed. Each option MUST be exercised
through an equivalent implementation artifact that differs only where its
shard strategy requires it, with the same optimization policy, representative
production trace, provider profile, provider configuration, topology,
equivalent resource budget, measurement method, and acceptance limits. The
composite option's demonstrated sustainable reservation rate MUST exceed both
alternatives by at least 20%, and the observed production peak MUST remain
below 50% of the demonstrated composite rate. The evidence MUST include
latency, error, backpressure, hot-pair, and lifecycle-control observations. The
downstream evidence specification chooses and freezes the sampling and
statistical method; this semantic contract does not prescribe a benchmark
estimator.

## 3. Observable position contract

### 3.1 Canonical shard identity

Canonicalization version `1` uses the exact case-sensitive persisted UTF-8
bytes of `TenantId` and `Domain`, in that order. Neither value may be empty.
There is no trimming, case folding, Unicode normalization, aliasing, or
rename-in-place. A rename creates a different shard identity. The prior
identity remains historical while any event, checkpoint, retry record, backup,
or evidence refers to it.

The logical shard identity is the tagged tuple
`(canonicalizationVersion=1, tenantIdBytes, domainBytes)`. Physical actor ids,
state keys, hashes, encodings, catalog partitions, and collision handling are
implementation concerns. A downstream design MUST prove that its physical
representation is injective for every valid logical shard or detects a
collision before allocation.

### 3.2 Versioned persisted and public representation

Metadata v1 remains unchanged: `metadataVersion` is `1`, `globalPosition` is
the existing signed Int64 member, and no `position` member exists. Positive
values are valid v1 identities; zero retains its existing unknown meaning.
Negative v1 values are invalid.

Metadata v2 retains the v1 metadata members and their meanings except that
`metadataVersion` is `2`, `globalPosition` MUST be absent, and the required
`position` member is exactly this strict object:

```json
{
  "scheme": "tenant-domain-v2",
  "canonicalizationVersion": 1,
  "tenantId": "tenant-a",
  "domain": "orders",
  "generation": "1",
  "counter": "42"
}
```

Member names are case-sensitive, duplicate members are invalid, and unknown or
null members are invalid. `tenantId` and `domain` MUST byte-match the enclosing
event and aggregate routing identity. `generation` and `counter` are canonical
positive base-10 Int64 strings matching `[1-9][0-9]{0,18}` and not exceeding
`9223372036854775807`.

The public position model MUST be a versioned tagged union capable of
losslessly representing:

- `global-v1` plus its signed Int64 scalar; or
- the complete six-member v2 object above; or
- an opaque raw unsupported-position variant that preserves every exact
  unrecognized outer-version, scheme, or canonicalization discriminator and
  the complete raw identity payload bytes needed to round-trip it.

A v2 event MUST NOT expose its counter through the legacy scalar
`GlobalPosition` as though it were globally comparable. A compatibility API
MAY report that the legacy scalar is unavailable, but MUST NOT synthesize zero,
hash the tuple into a scalar, or discard shard/generation identity. Persisted
and public round trips MUST preserve the exact tagged identity.

A v2 position's identity is the full tuple `(scheme,
canonicalizationVersion, tenantId bytes, domain bytes, generation, counter)`.
Counters are unique and strictly increasing only within an equal shard and
generation. A reserved range MAY contain later gaps and is an allocation label
only, not a committed-event cursor or timestamp. No counter or vector of
counters proves commit order.

A recognized metadata v1 or v2 value that violates its applicable structural
rules is malformed and returns `InvalidPosition`. Missing required members,
duplicate or null members, invalid JSON or object shape, noncanonical or
overflowed numeric values, and routing-copy mismatch remain invalid; the opaque
variant MUST NOT sanitize or preserve them as merely unsupported.
An otherwise-well-formed v2 object is not malformed solely because its scheme
or canonicalization discriminator is unrecognized.

Once the applicable outer envelope is structurally valid, an unrecognized
outer metadata version, scheme, or canonicalization version is unsupported and
MUST round-trip its exact discriminator and raw identity payload bytes through
the opaque raw unsupported-position variant without interpretation or
normalization. Unknown outer metadata versions return `UnsupportedScheme`.
An unrecognized scheme or canonicalization version also returns
`UnsupportedScheme`; recognized supported but unequal canonicalization versions
return `UnsupportedCrossCanonicalization`. Unsupported identities MUST NOT be
projected into a known tagged identity or ordered.

### 3.3 Validation, equality, and comparison

Both operands MUST be fully structurally validated before comparison.
Invalid input takes precedence over unknown input and every other outcome.
Unsupported outcomes take precedence over unknown-position results. The
complete ordered result set and precedence is:

1. `InvalidPosition` for malformed, noncanonical, overflowed, duplicate-member,
   or routing-copy-mismatched data;
2. `UnsupportedScheme` when an outer metadata version, position scheme, or
   canonicalization version is not recognized;
3. `UnsupportedCrossScheme` for otherwise-valid v1 versus valid v2;
4. `UnsupportedCrossCanonicalization` for recognized supported unequal
   canonicalization versions;
5. `UnsupportedCrossShard` for unequal tenant or domain bytes;
6. `UnsupportedCrossGeneration` for unequal generations in the same shard;
7. `UnknownPosition` only after every invalid and unsupported condition above
   is excluded, both operands are recognized, supported, and otherwise valid,
   and at least one v1 scalar is zero; and
8. `Less`, `Equal`, or `Greater` by scalar for two positive v1 positions, or by
   counter for two v2 positions whose scheme, canonicalization, shard, and
   generation are equal.

`InvalidPosition` takes precedence over unsupported and unknown outcomes.
Unsupported outcomes take precedence over `UnknownPosition`.
`UnknownPosition` applies only when both operands are recognized, supported,
and otherwise valid and no unsupported relationship exists. Equality is full
tagged identity equality, never counter equality alone. Cross-shard,
cross-generation, and cross-scheme positions are unordered: there is no scalar
fallback, tuple sort, `Max`, timestamp tie-break, or trustworthy global order.

## 4. Consumer, cursor, checkpoint, projection, and diagnostic contract

Allocation positions MUST NOT be used as lossless committed-event cursors.
Cross-aggregate enumeration requires a separately approved committed-event
source that exposes a finite, immutable read boundary and opaque continuation.
If no such source is approved, cross-aggregate resume under v2 is unsupported
and consumers MUST rebuild from before-first or reject the operation.

Future cursor work MUST use the existing `IQueryCursorCodec` and
`QueryCursorScope` seams and the platform's DataProtection-backed cursor
protection. A cursor MUST be opaque to consumers and bound to its query scope,
authorized principal/audience, committed source and immutable boundary,
position scheme, and complete shard/generation set. It MUST fail closed on
tampering, expiry, authorization change, unsupported scheme, changed shard set,
missing source state, or partial page failure. This specification deliberately
does not define cursor bytes, cryptography, key rotation, or server-side
indirection.

`QueryCursorScope.AddProjectionWatermark(long)` remains a v1-only scalar seam.
A v2 or mixed-history consumer MUST NOT pass a shard counter or cross-shard
maximum to it. A later public-contract story MUST provide an explicitly
versioned replacement or committed-source cursor before such a consumer can be
enabled.

Projection checkpoints MUST bind the committed-source continuation and exact
position comparison partition(s), not `Max(long)`. A checkpoint and the read
model state it claims MUST advance atomically or through an idempotent recovery
contract. Shard admission or retirement MUST make an existing checkpoint
detectably stale until the model is rebuilt or rebased at an immutable source
boundary. Signal delivery, allocation labels, and timestamps do not prove
projection progress.

Diagnostics MUST expose `scheme`, `canonicalizationVersion`, redacted shard
identity, `generation`, `counter`, source partition, and comparison outcome.
Logs, metrics, tables, charts, APIs, and operator text MUST NOT say or imply
`global latest`, `lag from max position`, or temporal order across comparison
partitions.

## 5. Mixed history and migration compatibility

V1 history is never rewritten. A mixed store contains immutable `global-v1`
events and new v2 events, each parsed by its own metadata version. Reads MAY
return groups from multiple comparison partitions, but MUST label the scheme,
shard, and generation of each group and MUST NOT merge those groups into a
claimed temporal order. Deduplication remains by `MessageId`; aggregate-local
replay remains by aggregate sequence.

Before cutover, an implementation plan MUST discover every historical and
currently admissible tenant+domain pair, establish collision-free physical
identity and a never-before-used positive generation for each writable shard,
and bind the resulting inventory to the migration evidence. Missing,
conflicting, concurrently changing, or unverifiable inventory blocks cutover.
The discovery source, physical identity, lifecycle storage, and recovery
algorithm belong to a separately approved implementation specification.

After cutover, a newly admissible tenant+domain pair MUST pass fail-closed shard
admission before its first reservation. The lifecycle authority MUST serialize
and durably record collision-free physical identity, a never-before-used
positive generation, allocator readiness, and the recovery path before
enabling the pair. It MUST also make every checkpoint whose exact shard set
omits the pair detectably stale and require the affected model to rebuild or
rebase at an immutable source boundary before progress resumes. Missing,
ambiguous, concurrent, partially persisted, or unrecoverable admission state
MUST reject the first and every later reservation until the transition is
completed; event or allocation maxima MUST NOT manufacture readiness.
This fail-closed shard admission applies before every new pair's first
reservation.

Existing scalar checkpoints MUST NOT be translated by copying their value into
a v2 counter or vector. Each consumer MUST either use a source-specific,
evidenced mapping that preserves its exact committed boundary or rebuild from
before-first. Consumers that cannot distinguish versions or reject unsupported
comparisons MUST remain on v1 and MUST block v2 cutover.

Restore, replay, and migration MUST preserve every accepted position,
generation, `MessageId`, aggregate identity, and aggregate sequence. Missing
allocation authority MUST NOT be reconstructed from event maxima. After
recovery, the next position in a comparison partition MUST be strictly greater
than every position identity already granted there. A retired exact shard
identity MUST NOT be silently reused.

## 6. Rollout, partial deployment, downgrade, and rollback

The rollout boundary is fleet-wide authority, not application-version
presence. Until all gates in section 7 pass and a separate implementation story
authorizes execution, v1 remains authoritative and v2 writers MUST remain
disabled.

An authorized rollout MUST satisfy these observable invariants:

1. all v1-capable writers, sidecars, routes, and credentials that could allocate
   or commit new v1 authority are fenced and drained before v2 allocation opens;
2. every initially writable shard is present, uniquely identified, recoverable,
   and ready before production v2 allocation opens;
3. partial deployment cannot permit concurrent v1 and v2 production authority;
   an old writer that appears after cutover begins is rejected before allocation
   or commit;
4. the first durable production v2 allocation makes rollback to v1 permanently
   forbidden, whether or not its reserved position later appears in an event;
5. after that boundary, downgrade is rejected and recovery is a v2-capable
   forward fix;
6. before that boundary, rollback MAY restore v1 only after all v2 authorities
   are fenced and drained and durable evidence proves that no production v2
   allocation occurred; missing or ambiguous evidence forbids rollback;
7. a pre-write rollback resumes v1 strictly above its previously granted
   ceiling and never reuses a v1 or v2 identity; and
8. failures never regress or reuse aggregate sequence, `MessageId`, CloudEvent
   id, position identity, or accepted generation.

The implementation specification MUST define recoverable authority transitions
and provider operations that prove these invariants, but this semantic contract
does not prescribe their storage records or phase algorithm. If satisfying any
invariant changes append fencing or provider write semantics, Story 4.5 MUST be
completed and approved first. No Story 4.5 prerequisite is inferred merely from
sharding when append behavior is unchanged.

## 7. Blocking evidence, ownership, and authorization

Every empirical evidence row is blocking for implementation authorization.
Every empirical evidence row MUST be bound to this exact normative digest,
candidate commit and successor blob. It MUST also bind the tested
implementation artifact identity, provider profile, provider configuration,
and topology fingerprint.
It MUST exercise the proposed production provider and topology and MUST inspect
persisted end state rather than relying only on HTTP status, mocks, or logs.
These bindings apply to every empirical evidence row.
Drift in any bound normative, candidate, blob, implementation artifact,
provider, configuration, or topology identity invalidates every affected
empirical evidence row, which MUST be re-run before it can satisfy a gate.

Option-capacity evidence MUST additionally bind the representative production
trace identity, acceptance limits, measurement method identity, and the exact
validity profile authority and derivation used to assign its exclusive UTC
expiry. The exclusive UTC expiry MUST use canonical UTC second precision in
the exact `YYYY-MM-DDTHH:MM:SSZ` form; every other encoding is invalid.
Capacity evidence is valid only strictly before that expiry; at or after the
exclusive UTC expiry it is invalid and MUST be re-run. Drift in the trace,
acceptance limits, measurement method identity, implementation artifact
identity, validity profile authority or derivation, or expiry assignment also
invalidates that evidence and requires a re-run.

Before implementation authorization, a separately authorized isolated
non-production evidence candidate MAY exist solely to produce the empirical
evidence in this section. That evidence-only authority MUST carry
authenticated unanimous approval from every identity in the exact
candidate-commit `architecture_owner` set. It MUST bind the exact normative
digest, candidate commit, successor blob, tested implementation artifact, test
environment, and an exclusive canonical UTC second-precision expiry. Solely
inside that isolated
non-production evidence candidate, the authority MAY allow
isolated v2 persisted and public formats. It MAY also allow allocator behavior
and topology required to exercise the evidence.
At evidence collection completion or the authority expiry, whichever occurs
first, every writer, route, credential,
allocator, and lifecycle authority MUST be fenced and drained.
The isolated environment MUST be torn down. Missing or ambiguous approval,
expiry, teardown, or fencing blocks the evidence gate. Evidence-only authority
grants no production, deployment,
migration, cutover, or general persisted-format or public-contract authority,
and no permission to treat v2 positions as authoritative outside that
candidate.

The Story 4.5 applicability declaration is content-bound rather than empirical.
It MUST bind this normative digest, candidate commit, successor blob, and tested
implementation artifact. A declaration that identifies an affected append-
fencing or provider-write seam makes the corresponding approved Story 4.5
empirical evidence mandatory; a declaration of no affected seam requires no
production execution for that row only when it satisfies every rule below.
A no-affected-seam declaration MUST include the exact candidate diff and review
of every append-fencing and provider-write seam. It MUST have authenticated
unanimous approval from every identity in the exact candidate-commit
`architecture_owner` set. Author self-declaration is not approval. It grants no
waiver. The applicability declaration grants no
implementation authority and waives no other evidence row.

| Evidence category | Minimum decision-grade proof |
|---|---|
| Option capacity | Same representative trace and production topology for tenant, domain, and composite options; composite satisfies section 2, including hot-pair and lifecycle-control behavior. |
| Position identity | Persisted and public v1/v2 round trips preserve exact tagged identity; malformed, overflowed, copy-mismatched, duplicate, and colliding inputs fail before allocation. |
| Multi-host concurrency | At least two independent hosts and sidecars sharing the proposed provider produce no duplicate position and strict monotonicity within each exact shard+generation. |
| Aggregate and event identity | Multi-event, retry, failure, and conflict cases preserve gapless aggregate sequence, stable `MessageId`/CloudEvent id, and the original result/position identity. |
| Restart and failover | Writer, sidecar, allocator owner, and provider restart/failover preserve authority, generations, uniqueness, monotonicity, and retry results. |
| Mixed version and consumer safety | V1/v2 reads remain interpretable; every cross-shard, cross-generation, and cross-scheme comparison fails with the specified outcome; no scalar `Max` path accepts v2. |
| Cursor, checkpoint, and rebuild | Existing cursor seams protect exact scope and boundary; before-first migration, stale-set detection, atomic progress, partial failure, and unsupported-source behavior match sections 4 and 5. |
| Inventory and migration | Every historical/admissible pair is accounted for; initialization, collision handling, partial rollout, retirement, and restore prove no identity reuse or sequence regression. |
| Overflow and limits | Counter, generation, batch, provider key/value, cursor, and page limits are measured and fail closed before mutation or checkpoint advance. |
| Hot shard and recovery | Sustained hot-pair load remains isolated; lifecycle control does not become the reservation bottleneck; backup/restore preserves all authority and resumes strictly above prior ceilings. |
| Rollout and rollback | Old writers are actually fenced; rollback succeeds only before any durable v2 allocation; every post-boundary downgrade is rejected and recovered forward. |
| Story 4.5 dependency | A content-bound applicability declaration states whether append fencing or provider write semantics change; when either seam is affected, the implementation carries exact approved Story 4.5 empirical evidence before execution. A no-change declaration is not an empirical production execution claim. |

The accountable approval role is `architecture_owner`, resolved from the
candidate commit's immutable
`_bmad-output/implementation-artifacts/1-20-github-approval-role-allowlist.json`.
The candidate owner set MUST be non-empty and contain unique authenticated
stable identities; case-only duplicates are not unique. Approval MUST come
from every identity in that candidate-commit owner set and bind the exact
candidate commit, successor blob, normative digest, and reviewed content.
Missing, empty, duplicate, mutable, unauthenticated, or non-candidate role
membership invalidates approval. Agent output, story status, a digest alone,
editable text, or a later owner roster is not approval.

Exact-content architecture-owner approval authorizes downstream planning only.
Runtime implementation, public-contract change, migration, deployment, topology
change, and cutover remain unauthorized until all evidence rows pass against a
separately reviewed implementation design and a separate implementation story
explicitly grants that authority. Approval and evidence records remain outside
this normative range and MUST NOT be attached by editing approved content.

## 8. Downstream specification boundary

This successor fixes observable semantics and fail-closed outcomes. It does not
select or prescribe:

- allocator, registry, catalog, lifecycle, archive, or recovery storage layouts
  and phase algorithms;
- physical actor/state-key encodings beyond the injectivity requirement;
- committed-source paging formats or traversal algorithms;
- cursor wire encoding, bespoke cryptography, key rotation, or state
  indirection; implementations MUST use `IQueryCursorCodec`, `QueryCursorScope`,
  and the platform DataProtection-backed cursor seam;
- backup-manifest or restore-record schemas;
- benchmark sampling, estimator, confidence-interval, or evidence-retention
  algorithms; or
- evidence-manifest and approval-record publication formats.

Each omitted mechanism requires a separately approved implementation or
evidence specification and MUST satisfy sections 1 through 7. A downstream
mechanism that changes the observable position identity, comparison outcomes,
mixed-history rule, cursor safety, rollout boundary, or approval effect is a new
renegotiation, not an implementation detail.

## 9. Conformance scenarios

| Scenario | Required outcome |
|---|---|
| Same v2 shard and generation | Full identity equality uses every tuple member; ordering uses only the positive counter. |
| Same shard, different generation | Equality is false and ordering returns `UnsupportedCrossGeneration`. |
| Different tenant or domain | Equality is false and ordering returns `UnsupportedCrossShard`; no scalar fallback exists. |
| V1 versus v2 | Both identities remain readable; ordering returns `UnsupportedCrossScheme`. |
| Unknown or invalid data | A zero v1 value compared with an otherwise-valid positive v1 value returns `UnknownPosition`; malformed or mismatched recognized data returns `InvalidPosition` before every other outcome. |
| Zero v1 with unsupported peer | A valid zero v1 value compared with v2 returns `UnsupportedCrossScheme`; compared with a structurally valid unsupported identity it returns the applicable unsupported outcome, never `UnknownPosition`. |
| Unsupported versus malformed identity | A structurally valid unrecognized outer version, scheme, or canonicalization round-trips through the opaque variant and returns the applicable unsupported outcome; malformed recognized v2 data returns `InvalidPosition`. |
| Unknown outer metadata version | The public model preserves the exact outer version and raw payload in its opaque variant; ordering returns `UnsupportedScheme` unless either operand is invalid. |
| Mixed-history cursor | Continuation is committed-source-owned and version-aware; no counter maximum is accepted as progress. |
| Post-cutover new shard | A newly admissible tenant+domain pair cannot reserve until lifecycle identity, generation, readiness, recovery, and stale-checkpoint handling are durably complete. |
| Partial fleet | An old writer cannot allocate or commit after v2 cutover starts. |
| Pre-allocation rollback | V1 may resume only after positive no-v2-allocation proof and strictly above its prior ceiling. |
| Any durable v2 allocation | V1 rollback and downgrade are forbidden; recovery proceeds forward with v2-capable components. |
| Missing evidence or approval | Composite remains ineligible, v1 remains authoritative, and implementation/deployment/migration is rejected. |

## 10. Content and scope validation

The successor MUST be strict UTF-8 with LF endings and no BOM. The normative
begin and end markers MUST each occur exactly once and in order. SHA-256 over
the bytes after the begin-marker LF through the byte before the end marker MUST
equal the sole declared normative digest outside this range and the wrapper's
`normative_sha256`.

Validation MUST reproduce the predecessor identities in section 1 and all 19
source-range digests in section 1.1 from predecessor bytes. It MUST reject a
missing, duplicate, additional, or out-of-range disposition row.

Historical normative digest
`2310f121dc48f59713a9c6bbc6ffe2e63be374d4d8ecc6e8d710a0d9cf3674de`
is explicitly superseded and non-authoritative. It MUST NOT be used as this
candidate's content identity, approval subject, evidence subject, or source of
implementation authority.

Relative to baseline `1194dfe59bcbc9b235390d1e46a7dfe4ee115d94`,
the reviewed candidate commit MUST contain exactly these two path changes and
no other path:

- `A` `_bmad-output/implementation-artifacts/spec-4-6-global-position-sharding-spec-renegotiation.md`
- `M` `_bmad-output/implementation-artifacts/spec-dapr-global-event-ordering-v2.md`

Pre-existing unrelated work MAY remain in the index or worktree only when the
explicit story commit excludes it and complete scope validation proves it did
not enter the candidate commit.

A successful content or scope check is verification evidence only; it does not
constitute human approval.
<!-- HX-GPOS-V2-NORMATIVE-END -->

## Content identity

| Identity | Value |
|---|---|
| Normative content SHA-256 | `995fcecd16b3421ec9ff666d0884bfb5e436932aa49529c152fb7c439172a1fd` |
| Normative byte range | Bytes after the unique begin marker LF through the byte before the unique end marker |
| Encoding | Strict UTF-8, LF, no BOM |

## Detached approval status

No exact-content human approval or successor-bound production-path evidence is
present. The v1 allocator remains authoritative. Obtain architecture-owner
approval of this content, commission the blocking evidence, and create a
separately authorized implementation story. Do not edit this successor to
publish evidence or approval records.
