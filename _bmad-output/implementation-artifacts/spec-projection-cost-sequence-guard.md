---
title: Projection Delivery Cost And Sequence Guard Specification
type: architecture-gate
story: "6.3"
status: approved
story_6_4_authorized: true
approved_by: "Jérôme Piquot (jpiquot)"
approved_on: 2026-10-15
version: "1"
created: 2026-10-15
baseline_commit: "6cd4e738ff259c6a6602650a8f49fd8373e49524"
required_by: [FR33-C3, NFR8, G-NFR8, OR16, AD-13]
---

# Projection Delivery Cost And Sequence Guard

This is the owner-approved normative contract for Story 6.4. It closes the
design decisions required by Story 6.3; it delivers no runtime change or
production qualification. **Jérôme Piquot approved the final normative digest
on 2026-10-15 for the Epic 6, architecture and Test owner roles and explicitly
authorized Story 6.4.** Story 6.3 is complete for this specification gate;
Story 6.4 remains backlog. The detached record below binds the owner's
approval to these unchanged normative bytes.

## Document identity

The inspected source baseline is
`6cd4e738ff259c6a6602650a8f49fd8373e49524`. Current Story 1.18 is the reissued
historical Story 1.13 delivery-idempotency work; current Story 1.19 is the
reissued historical Story 1.14 paged-rebuild work. Their contracts remain
authoritative. The ten numbered sections below correspond, in order, to the
ten acceptance clauses of Story 6.3 in
`_bmad-output/planning-artifacts/epics.md`.

SHA-256 covers the exact UTF-8 bytes strictly between the unique full-line
normative markers, including intervening LF endings and excluding marker
lines. This file MUST use LF and no BOM. The detached approval and digest
record follow the end marker, avoiding a self-referential digest. Any normative
byte change invalidates approval and closes the gate again. Ordinary source
references below are relative to the repository root.

```bash
python3 - <<'PY'
from pathlib import Path
import hashlib
p = Path('_bmad-output/implementation-artifacts/spec-projection-cost-sequence-guard.md').read_bytes()
b = b'<!-- HX-PCSG-V1-NORMATIVE-BEGIN -->\n'
e = b'<!-- HX-PCSG-V1-NORMATIVE-END -->\n'
assert p.count(b) == p.count(e) == 1
assert b'\r' not in p and not p.startswith(b'\xef\xbb\xbf')
s = p.index(b) + len(b)
t = p.index(e, s)
print(hashlib.sha256(p[s:t]).hexdigest())
PY
```

<!-- HX-PCSG-V1-NORMATIVE-BEGIN -->

## 1. Scope, authorities, and current-path inventory

MUST and MUST NOT denote mandatory requirements. Story 6.4 may implement only
the delivery optimization, additive evidence, and guards defined here. It MUST
preserve tenant/domain/aggregate/projection identity; EventStore `MessageId`;
delivery writer protocol v2, CAS, leases and fencing; stable dispatch and batch
identities; proof horizons; staging, readback and promotion. AD-7, AD-8, AD-12,
AD-13 and AD-20 govern. FR33-C3 is this specification gate; FR33-C4 is its
separately authorized runtime implementation. G-NFR8 and OR16 stay closed until
section 10's approval conditions are met.

Runtime/test/UI edits, dependencies, topology, deployment, consumer migration,
snapshots, event upcasting, cancellation-interface changes, sharding, automatic
handler conversion, a generic store/proof redesign and new evidence tooling
are outside Story 6.3. Story 6.4 MUST reuse the existing persistence protocols;
the source and completion anchors in section 2 are the minimal added evidence,
not replacements for delivery receipts or read-model batches.

| Current path / source | Reads and authority | Writes, calls and boundary |
| --- | --- | --- |
| `Server/Projections/ProjectionUpdateOrchestrator.cs`, immediate and poller `DeliverProjectionAsync` | Activation-outbox entry; active operator-rebuild index; domain registration; diagnostic aggregate-wide checkpoint. Mapped-only route reads `GetStreamMetadataAsync` then one head envelope. Legacy route calls `GetEventsAsync(0)` before knowing the returned projection type, builds readable wire events and only later reads the projection-scoped checkpoint. A process-local semaphore is scheduling only. | Named coordinator or legacy `/project`; lifecycle delivery lease; projection actor write; scoped delivery checkpoint (aggregate-wide compatibility fallback when scoped store absent); lifecycle release and activation-outbox completion. Current legacy checkpoint errors can become diagnostic zero; the optimized path MUST NOT inherit that behavior. |
| `Server/Actors/AggregateActor.cs`, `AggregateStreamMetadata.cs`; `Server/Events/EventPersister.cs`, `AggregateMetadata.cs` | Actor-owned metadata is the append head authority. Current stream metadata exposes existence, sequence and erasure, without a content anchor. Range reads are actor-owned, bounded, aggregate-local reads; actor cache barriers remain mandatory. | `EventPersister` stages envelopes and aggregate metadata with `IActorStateManager`; `AggregateActor` owns atomic save, discard and ambiguous-save reconciliation. New source evidence MUST join that save, never a later best-effort write. |
| `Server/Projections/NamedProjectionDispatchCoordinator.cs` | Verified route catalog/refresh; retry work/lease; per-route accepted prefix; lifecycle lease; admission row. Mapped ordinary delivery computes admission history per route, reads application history per base-sequence group, and rereads history per admitted route after dispatch. Its current suffix eligibility is only `C + 1 == H`; other cases reread the prefix. | Persist retry activation before effects; reserve each route; invoke `/project/v2`, or mapped part/stage/commit/verify/abort; optional legacy actor-state compatibility write; delivery completion; retry settlement/defer/delete; lifecycle release. A transport return is never a completion receipt. |
| `Server/Projections/ProjectionDeliveryIdempotencyCoordinator.cs`, `ProjectionDeliveryState.cs`, `ProjectionDeliveryFingerprint*.cs` | Validate scope/schema, retained receipts, prefix fingerprints, active reservation and full `CompletedMessageIdDigests`; detect old-ID reuse even outside exact receipt retention. `ReadAcceptedPrefixAsync` currently returns only sequence/fingerprint, not live-model completion proof. | ETag first-write reservation/reclaim/completion/release. Completion stores contiguous checkpoint, head identity, prefix, retained receipts and complete message-ID digests in the same row. Defaults: 256 receipts, maximum 4096; five-minute lease; eight transition attempts, supported configuration maximum 32. |
| `Server/Projections/DaprProjectionDeliveryStateStore.cs`, `ProjectionCheckpointTracker.cs` | Every delivery-state `ReadAsync` physically reads row+ETag AND the store-global writer marker. Compatibility tracker can additionally probe scoped row, migration marker, old aggregate row, and identity/scope indexes. | Conditional row saves; row+reconciliation-work transaction; writer-marker activation; compatibility migration and identity-index writes. Optimized hot paths MUST use the validated row directly, never lazy migration or aggregate-wide progress as authority. |
| `Server/Projections/ProjectionDeliveryRetryWorker.cs`, `DaprProjectionDeliveryRetryScheduler.cs` | Scan 64 retry shards; bounded due set; aggregate lease; active rebuild exclusion. Non-mapped retry reads the entire admitted prefix with count limit 256. Mapped retry reenters the named coordinator. | Stable per-head retry work, schedule/claim/defer/settle/delete with CAS. Default due batch 64; the default `MaxRetryAttempts=8` saturates the stored Attempt/backoff counter, not the number of lifetime activations. Pending work can retry indefinitely. One-minute retry lease, one-second initial delay and five-minute maximum backoff; scheduler ledger/lease CAS cap is **5**, independent of delivery-row A. Charge scan/occupied-shard reads separately. |
| `Server/Projections/EventStoreProjectionDeliveryHistoryReader.cs`, `ProjectionStreamPageValidation.cs`, `ProjectionEventWireBuilder.cs` | Count limit **1**, including mapped delivery and maintenance reconciliation; loops to admitted inclusive head. Validate identity/order before decoding; decrypt every event or fail. `ReadAsync` accumulates a prefix; `ReadPagesFromAsync` can read a suffix. | No handler/model/checkpoint writes. Temporary provider outage is retryable; unreadable/invalid history cannot become a skipped event or an empty successful page. |
| `Server/Projections/ProjectionDeliveryReconciler.cs`, `ProjectionDeliveryCutover.cs` | Authenticated operator identity, current row/marker, authoritative full source history, same checkpoint. | Atomically hydrate delivery row and attributable reconciliation work; no handler effects, model writes or invented advancement. Store-global v2 marker is not erased with projection scope. Existing cutover runbook remains binding. |
| `Server/Projections/ProjectionUpdateOrchestrator.cs`, rebuild methods; `ProjectionRebuildCheckpointStore.cs`; `NamedProjectionDispatchCoordinator.cs`, rebuild methods | Running operator checkpoint, identity inventory, operator intent rereads, per-aggregate checkpoint, source metadata and pages; required named-route outcomes and promotion readback. Defaults: P=256, full-prefix event ceiling 10,000 and byte ceiling 67,108,864. | Lifecycle rebuild fences; operation-private actor/detail/index candidates; coordinated batch or marker-gated promotion; required-outcome matrix and persisted freshness readback; rebuild checkpoint only after promotion. Terminal failure/cancellation clears owned `Rebuilding`, preserving the complete live model. Delivery and rebuild checkpoints remain distinct. |
| `DomainService/IDomainProjectionHandler.cs`, `LegacyDomainProjectionHandlerAdapter.cs`, `DomainProjectionDispatcher*.cs` | Legacy handler is stateless full replay. Named async interface alone declares no tail capability. Existing single-event, mapped streamed incremental and shared-epoch interfaces declare more specific semantics (section 4). | Full replay returns actor state; named handlers use platform `IReadModelStore`/`IReadModelBatchStore`. SDK awaits durable outcomes and validates catalog, bounded response and per-route statuses. Cancellation propagates through existing seams. |
| `Client/Projections/ReadModelBatchProtocol.cs`; DomainService mapped/shared rebuild protocols; `Server/Projections/DaprProjectionLifecycleGateway.cs` | Batch marker, immutable manifest, candidate/prior/live rows, receipt and freshness readback; selected generation and owner progress; lifecycle snapshot/fence. | Existing same-component transaction or resumable receipt-last protocol owns publication/compensation. No new cross-store transaction is assumed. Shared epoch journaling is not proof that folding/selection has completed. |
| `ProjectionDeliveryDiagnostics.cs`, retry/rebuild diagnostics and `ProjectionUpdateOrchestrator.Log` | Current admission/completion traces and `eventstore.projection.delivery.transitions`, route/status/reason tags; retry/rebuild outcomes. | Best-effort payload-free telemetry, never a durable proof boundary. Section 8 adds cost evidence without authorizing UI work. |

Prefixes `Server/`, `DomainService/`, `Client/` above mean
`src/Hexalith.EventStore.Server/`, `src/Hexalith.EventStore.DomainService/`, and
`src/Hexalith.EventStore.Client/`. Tests in section 7 bind these actual paths.
The legacy full read, mapped suffix, non-mapped retry, full-replay rebuild,
incremental rebuild and authenticated reconciliation are distinct cost modes;
none is a synonym for receiving one page.

## 2. Current-head proof and zero-event contract

### 2.1 Source evidence

Sequence equality, latest `MessageId`, ETag, global position, timestamps,
transport success or an aggregate-wide checkpoint cannot establish content
continuity. Add an optional, versioned actor-owned `SourceProof` to aggregate
metadata and an additive source-proof actor inspection method. Keep existing
constructors, deconstruction and `GetStreamMetadataAsync` behavior compatible.
The proof contains exactly: codec version **1**, scope, stream incarnation ULID,
head sequence H, head EventStore `MessageId` (null at zero), cumulative digest
F(H), retained floor, and actor-proven erasure state. The inspection reads
metadata only, never an envelope, and returns `Unsupported` when absent. The
source incarnation is created once for a new stream, survives append/restart,
and changes on authorized erase/recreation. It is not a correlation ID.

The source codec is **source-v1**. SHA-256 inputs are binary, with ASCII domain
tags length-prefixed like strings. Signed 32/64-bit integers are little-endian;
UTF-8 strings and byte arrays have signed 32-bit byte-length prefixes, -1 for
null, 0 for empty. Strings use exact ordinal bytes without Unicode, case or JSON
normalization. Optional integers use a 32-bit presence value 0/1 before the
integer. Hash values in metadata are 64 lowercase hexadecimal ASCII characters;
chain concatenation uses the decoded 32-byte hashes.

- F(0) = SHA256(encoded tag `hexalith.projection.source.seed.v1`, tenant,
  domain, aggregate, incarnation, in that order).
- E(i) = SHA256(encoded tag `hexalith.projection.source.event.v1`, followed by
  stored `EventEnvelope` fields in this order: `MessageId`, `AggregateId`,
  `AggregateType`, `TenantId`, `Domain`, `SequenceNumber` (64), `GlobalPosition`
  (64), `Timestamp` UTC ticks (64) and offset minutes (32), `CorrelationId`,
  `CausationId`, `UserId`, `DomainServiceVersion`, `EventTypeName`,
  `MetadataVersion` (32), `SerializationFormat`, `Payload`, `Extensions`,
  `EventContractType`, nullable `PayloadVersion` (32), `ApplicationPayloadDigest`.
  Extensions encode count (-1 null, 0 empty) then key/value string pairs in
  ordinal key order; duplicate keys are invalid. This codec ordering does not
  permit sorting events. Bind stored ciphertext/protection metadata as stored,
  never decrypted or upcast bytes. Derived `Identity` is excluded.
- F(i) = SHA256(encoded tag `hexalith.projection.source.step.v1`, raw F(i-1),
  raw E(i)). Positive sequence must be exactly previous+1 before extension.

For a multi-event append, compute the chain from the previous persisted proof
and commit the final head proof with envelopes and metadata in the existing
actor save. Every actor-owned metadata rewrite preserves it; append/restore
ambiguity requires existing actor readback before exposing it. No separate
per-event chain store, history rewrite or application-owned provider access is
introduced. Absent/invalid proof on a nonempty stream never creates a new seed;
use section 9's bounded maintenance bootstrap. Cryptographic integrity here
assumes trusted actor ownership and SHA-256 collision resistance; it is not
provider tamper attestation.

The mandatory source-v1 golden vector uses tenant `tenant-a`, domain `counter`,
aggregate `aggregate-1`, incarnation `01ARZ3NDEKTSV4RRFFQ69G5FAV`.
For event i in {1,2}, use message IDs `01ARZ3NDEKTSV4RRFFQ69G5FAW` and
`01ARZ3NDEKTSV4RRFFQ69G5FAX`, respectively; aggregate type `Counter`, sequence i,
global position 41+i, UTC ticks **639276192000000000**, offset minutes 0,
correlation `correlation-1`, causation `causation-1`, user `user-1`, service
version `v1`, event type `CounterIncremented`, metadata version 1, format
`json`, and UTF-8 payload `{"amount":1}` or `{"amount":2}` without a newline.
Extensions, EventContractType, PayloadVersion and ApplicationPayloadDigest
are null. Expected lowercase SHA-256 values are:

| Value | Required digest |
| --- | --- |
| F(0) | `30f9f4f61a5a25d9ec628880b34d5d6c96e046362034ce02dc15bcaef367f5da` |
| E(1) | `1c3bd96cc9af2f16a0ea23426ab177bb4d63b659d0047ee1deb850d36100cd6b` |
| F(1) | `62e3e50f78399c37449cfc3b04776579ad58e348914bed344d4951205d299fa2` |
| E(2) | `08998bcf008547ee819b50d51c5855039ebacf5cf0527c1e458975934094b0a5` |
| F(2) | `6c18fec56e7c7bc1ba0c4eb760c12a09b2b976ce1fd2887f3d5bc32220a0c636` |

Golden-vector tests must also distinguish null from empty, optional version
absent from explicit 1, extension key/value changes, timestamp offset changes,
and every persisted envelope field. Equivalent extension insertion orders
produce the same digest; reversed events, duplicate keys and invalid sequences
are rejected before chain extension.

### 2.2 Scoped completion evidence

Add optional `GuardEvidenceVersion=1` completion evidence to the internal
delivery row, using **schema 2 with writer protocol 2**. Preserve all existing
v2 fields, fingerprint versions, receipt rules and fences. Evidence binds:
source codec/incarnation/F(C)/head `MessageId`/C; projection route and catalog
fingerprint; negotiated handler semantics version; selected owner/generation
and immutable batch/selection receipt identity; persisted freshness version;
and the actual read-back **Idle** lifecycle revision bound after release as
specified in section 6. Completion first records `IdleBindingPending` with the
reservation fence, admitted H and lifecycle operation token while leaving C
and ActiveReservation unchanged; only the subsequent conditional completion
may bind the read-back Idle revision and advance C. Never predict release's
next revision. C remains
`LastDeliveredSequence`, not a new independently writable checkpoint. A
reservation binds base C/F(C), admitted H/F(H), catalog, stable dispatch ID,
selected generation and fence. Missing fields mean unproven, never defaults.

Read completion and selected-owner proof through their owning platform
store/actor seams. The existing selected-model/batch receipt is the model
authority; the delivery row records its verified binding. This adds a bounded
read-only SDK proof operation, not a domain handler invocation or a parallel
receipt protocol. The proof operation returns the persisted selection/receipt,
freshness and lifecycle generation, without reading event envelopes or
returning model payloads. It fails when selection/receipt validation needs
repair; it MUST NOT reconstruct missing proof by invoking a handler.

### 2.3 Activation representation and scope admission

Keep the existing store-global `projection-delivery-writer-protocol` key,
`ProjectionDeliveryWriterProtocol` serialization/validation, its data properties
(`schemaVersion`, `writerProtocolVersion`, `cutoverCommit`, `activatedAt`),
existing serialized derived members and writer-v2 semantics unchanged.
Guard admission uses one
additional immutable per-scope record in the same configured checkpoint Dapr
state component. Its exact key is `projection-delivery-source-guard:v1:` plus
lowercase SHA-256 of the source-v1 length-prefixed strings tenant, domain,
aggregate, projection, in that order (no tag/other fields). Its JSON properties
are exactly `schemaVersion:1`, `guardVersion:1`, `writerProtocolVersion:2`,
`deliverySchemaVersion:2`, `sourceCodecVersion:1`, `tenantId`, `domain`,
`aggregateId`, `projectionName`, `sourceIncarnation`, `catalogFingerprint`,
`handlerSemantics`, `specSha256`, `cutoverCommit`, `activatedAt`. The last two
are the full canonical Git commit and UTC activation time; `specSha256` is
this approved normative digest. Serialized guard record <=4096 UTF-8 bytes.
Use platform JSON options; duplicate properties/unknown versions fail closed.

Activation is first-write/CAS after section 9's maintenance attestations.
Read both the unchanged writer-v2 marker AND this scope record for every
delivery-row read; no cache substitutes for either read. Exact scope,
incarnation, catalog, handler semantics, approved digest and versions must
match; absent scope record means guard disabled for that scope, with no
wildcards or inferred tenant/domain admission. Scope additions require the
same qualified cutover; a recreated source needs explicit CAS replacement
against the old scope record after reconciliation. Global writer marker
always survives scoped erasure; guard records are retained as admission
evidence, and an obsolete incarnation cannot authorize the recreated stream.
No unbounded scope list is embedded in either marker.

### 2.4 Current decision

For a route already declared in a verified catalog, perform: (1) read source
proof S1; (2) read scoped delivery row plus BOTH writer-v2 and scope-guard markers;
(3) read lifecycle snapshot; (4) read selected-owner completion proof; (5) read
source proof S2. Reuse the catalog snapshot for this attempt. All scope,
schema, fingerprint, receipt, digest-list,
handler-version and fence checks must pass; active reservation/rebuild/erase
prevents this fast path. S1 and S2 must agree on incarnation, H, F(H), erasure
and retained floor. Require C=H, matching head ID and F(C)=F(H), and matching
owner, freshness and bound Idle lifecycle revision. `IdleBindingPending`
cannot authorize current. Zero-head qualification is the reachable
`EmptyNoWork` case: authoritative H=0, C=0/absent, no prior model/completion,
and no erase/rebuild intent performs no event reads, handlers or writes, but
retains `Unknown` provenance; it is never `AlreadyCurrent`. Existing empty
rebuild remains no-write and does not mint an empty receipt. Missing source
proof also stays `Unknown`; H=0 with a pre-existing model/checkpoint needs
drift reconciliation. This version deliberately has no H=C=0 `AlreadyCurrent`
route or parallel empty-model receipt protocol.

The exact uncontended budget is **2 source-metadata operations, 1 delivery-row
read + 1 writer-v2-marker read + 1 scope-guard-marker read, 1 lifecycle-snapshot operation, 1 selected-
owner proof operation; 0 range operations, 0 envelopes, 0 handlers/folds,
0 batches, 0 durable writes/deletes, 0 checkpoint advances, 0 notifications**.
SDK proof operation is at most four metadata-key reads; lifecycle operation at
most two. Including source metadata, the ceiling is **11 logical metadata-key
reads**, including the delivery row. Actor caches never excuse instrumented
logical reads. No activation-outbox completion or retry-ledger deletion is
hidden inside this zero-write outcome; separately scheduled cleanup is charged
as reconciliation and cannot change projection freshness.

Current-path read-body bytes are <=**S(H,R)+114,688**: one proof row, two
markers <=4096 each, two source metadata bodies <=4096 each, two lifecycle-key
bodies <=16,384 each and four selected-proof metadata-key bodies <=16,384
each. Count actual bytes even on early rejection; transport framing/retries
are reported separately. Neither marker is hidden inside the other read.

The selected-owner proof operation also checks that the lifecycle generation
and phase still equal the supplied snapshot after reading selection/receipt;
that check is included in its four-read ceiling. A mismatch is a typed retry,
not current. Evidence is point-in-time observation, not a lock or promise that
the source/lifecycle cannot change after the final sample.

This is `AlreadyCurrent` at observed S2, with persisted `ProjectionBacked`
provenance. It creates no new lifecycle completion or freshness version. Head
growth between S1/S2 returns `head_changed`/`Stale` with the same zero-event and
zero-write ceiling; schedule a later tail attempt. No loop chasing a moving
head. If source/lifecycle/model evidence is unavailable, return `Unavailable`;
invalid or unsupported evidence follows sections 5/9. No path labels an
unscoped, stale or unverifiable checkpoint current.

## 3. Tail algorithm, page model and quantitative cost

### 3.1 Admission, resume and repeated passes

Freeze S1=(incarnation,H,F(H),head ID,floor). Read valid scoped C, where 0<=C<H.
Fresh admission requires floor<=C+1 and compatible durable prior selected-owner
state exactly at C. An active/pending reservation instead follows the
same-reservation recovery rule below; selected state ahead of C is not valid
fresh admission and cannot authorize a new H or new dispatch identity.
After= C is exclusive; H is inclusive. Each page retains supplied order and
must contain exactly the next positive sequences without repeats or foreign
scope. Read no sequence <=C or >H. Validate source chain from stored F(C)
through these stored envelopes and require final F(H) and head ID equal S1.
No prior-envelope proof reads are needed: the cumulative head authenticates
the entire proposed suffix. For C=0 use the trusted scoped incarnation seed.

The admission pass validates and decodes the **entire** tail, including
protected-event readability, before any handler effect. Derive the existing
route fingerprint chain from the durable accepted route prefix, without
changing its v1/v2 semantics. Check every suffix `MessageId` against the full
retained identity-digest list and the suffix itself. Then acquire existing
lifecycle lease and CAS reservation against the exact C/base/selection proof.
Revalidate after a competing CAS; reuse immutable admitted pages/digests where
safe, rather than restarting history on every CAS conflict.

Use at most three source passes per successful route: admission, application,
and verification. Application consumes prior durable owner state and each
verified contiguous event; verification checks source and selected-model
completion before row finalization. All passes use the same C,H,incarnation
and chain. A pass cannot turn a changed input into a new accepted baseline.
Before EACH application handler call, match sequence, persisted MessageId and
source-v1 E(i) against the immutable admitted tuple, then match the existing
route-event fingerprint after readability conversion. A changed reread fails
`source_conflict` BEFORE that event's handler effects, even if a later final
chain check would detect it. Keep these admitted tuples for the reservation;
after restart rebuild them by read-only admission against its frozen C/H/F(H)
before resuming effects. No post-effect verification substitutes for this check.
Retaining already validated bytes can eliminate a later pass, but measured
counts MUST include every actual reread and retry. Source final inspection
S2 may be newer: complete C->H only if the frozen prefix remains valid;
report `Stale` against newer S2, never current at H'. Incarnation change,
retention loss or conflicting prefix prevents finalization and requires
reconciliation. A later work item admits the new head with a new stable ID.

Partial progress uses existing handler/SDK per-event batch, mapped append and
shared journal/applied receipts, not a new progress store. A reclaimed SAME
reservation retains original C,H, admitted source/route prefix, selected owner
generation, outer head-MessageId dispatch ID and immutable per-event identities.
Read the owner's existing persisted applied sequence K and prove the complete
contiguous C+1..K receipts with their original event/dispatch/batch identities;
require C<=K<=H and matching admitted tuples. K can exceed C while aggregate
delivery C remains unchanged. Do not replace these receipts with a new batch
manifest derived from the newer live model. Events <=K are already complete:
validate receipt and skip the domain effect, or replay only the SDK's existing
idempotent receipt check with the SAME identity. Resume K+1..H on the durable
state at K, checking each event before invocation. A partial multi-key event
batch is reconciled by its original immutable manifest before the next event.

Existing mapped append uses source event identity/owner receipt, shared
delivery uses its registered source sequence/position identity, and an explicit
single-event handler must expose its existing stable per-event dispatch/batch
derivation in the capability descriptor. Never pass the outer head dispatch
ID alone as the identity of changing per-event manifests. Routes lacking
durable per-event progress/receipt recovery support cannot negotiate tail mode;
they use canonical staged replay. If K or a receipt is missing, noncontiguous,
conflicting, outside retention, from a different owner generation, or already
beyond H, retain reservation and last complete selected state and require
reconciliation; do not fall back or reset to C. A different pending newer-head
work item waits until the original reservation settles. Recovery scans at
most delta per-event receipts (charge their reads/bytes in D,J/control costs),
and at most delta application attempts in one activation; no repeated logical
model mutation for the completed prefix. Full-range completion still advances
delivery C once to H only after section 6's proof boundary.

### 3.2 Measurement units and fixed profile

All ceilings below bind the approved workload, not hypothetical arbitrary
domain state. Counters are measured at production actor, handler and store
seams; caches, shared pages, failed attempts, compensation and readback remain
visible. A state transaction is one RPC AND its individual key mutations are
counted. Report actor-internal key reads separately from actor invocations;
Dapr transport retries are additional attempts, never free operations.

| Symbol / constant | Frozen value and meaning |
| --- | --- |
| H, C, delta | Aggregate-local admitted head, durable scoped checkpoint, H-C. No global order. Qualified head range 0..10,000; H>10,000 yields `delivery_owner_limit_exceeded` before history/handlers. Preserve the existing total-owner limit, even when delta=1. |
| P | Configured positive requested event-count page limit. Keep history reader default **1**, retry default **256**, rebuild default **256**. Optimized profile permits P=1..256; existing rebuild configuration outside that profile remains compatible but is not qualified by this budget. |
| Page bytes | At most **2,097,152** serialized UTF-8 envelope-response bytes, including array framing. Additive actor-owned bounded-range operation honors BOTH count and byte limit without changing the old actor range signature. An individually oversized event fails `delivery_page_limit_exceeded`; never split/drop an envelope. |
| Q(n,P) | Actual pages produced by greedy ordered packing under both ceilings. Page size is exactly **2 + sum(event JSON byte lengths) + (count-1)** for a nonempty compact JSON array. Uniform fixtures <=**8190** bytes/event and P<=256 therefore give Q=ceil(n/P), Q(0,P)=0. At 8191 or 8192 bytes/event, 256 events exceed the cap once framing is included. Mixed sizes have ceil(n/P)<=Q<=n. No terminal empty probe when H is known; report actual P,Q,bytes. |
| Event/byte ceilings | At most **10,000** unique admitted events and **67,108,864** bytes per source pass AND per decoded wire-event array, counting JSON/base64/metadata. Same default complete-prefix bounds as 1.19. Test the exact limit and one over. |
| R | Exact receipt retention configured 1..4096, default **256**. Full message-ID digest list still contains C entries; never truncate it to meet a budget. |
| A, N | Delivery-row transition attempt cap A: default **8**, maximum **32**. Actual work-item activation count N is measured independently; qualification observes an initial window **N=8**, then a ninth activation proving work remains pending when Attempt saturates (report N=9 too). This is no lifetime retry cap or new parking policy. Lease remains five minutes by default (supported 30 seconds..24 hours); handler redelivery horizon remains at least **24 hours** by default. |
| D, J | Actual logical read-model writes/deletes in immutable manifests, and actual coordinated batch executions. Qualification fixture: D=2 per source event, J=delta for single-event detail/index delivery. Full replacement fixture: D=2, J=1. Arbitrary consumer output is measured using actual D,J and cannot be declared fixed-size. |
| B(n), V(n) | Sum of exact stored-envelope response bytes and readable wire bytes for the admitted n events, respectively, each <=67,108,864 per pass. Payload-free reports retain counts/digests, not event bytes. |
| L, Blook | Each count/byte-bounded range may read at most **one unreturned lookahead** when the next envelope would exceed the byte cap. No lookahead after P returned events or beyond admitted H. Per completed pass L<=max(Q-1,0); Blook is actual serialized lookahead bytes, <=min(2,097,152 L,B(n)). Charge these actor-key reads, sizing serialization and source decoding separately from returned envelopes. Never decrypt, route-decode, extend the chain or invoke a handler for lookahead; its next-page returned copy is validated normally. |
| S(h,r) | Delivery proof-row serialized-byte ceiling: **16,384 + 80h + 512 min(h,r)**; additionally <=**4,194,304**. Bound includes added evidence, complete Base64 message-ID digests and retained exact receipts. Fixture IDs are 26-character ULIDs plus legacy 36-character message-ID cases, route/scope strings <=64 bytes each. Longer permitted identifiers require measured bytes under the absolute cap; no truncation. |

Larger existing mapped streamed rebuilds (for example the >64 MiB transport
regression) remain supported by their existing explicit configuration and
part protocol. They MUST NOT be routed into, rejected by, or claimed to pass
this 64 MiB optimization profile solely because the feature is enabled.
Fallback/rebuild configuration may authorize their established wider limits;
report the actual limits, mode and cost separately. No default is silently
raised and no previously supported API configuration is silently narrowed.

### 3.3 Budgets per route and attempt

Successful uncontended attempts have the following maximums. These are pass
ceilings, not a performance timing promise. A=1 in uncontended rows.

| Mode | Head-inspection actor operations; range calls; envelopes read | Application and durable completion |
| --- | --- | --- |
| Proven current | Exact section 2.4 budget; Q=0, events=0 | No calls, writes or advancement; reads of history-sized S(H,R) still count. |
| Incremental tail | Metadata <=3; ranges <=3Q(delta,P); returned envelopes <=3delta, lookahead <=3max(Q-1,0); response bytes <=3B(delta), additional lookahead sizing bytes <=3Blook and wire bytes <=3V(delta) | Handler event-application attempts <=delta; batch calls <=J; model logical mutations <=D; delivery-row successful transitions <=3 (reserve+prepare-idle-binding+complete-with-idle), checkpoint advance exactly once to H after proof. Never H-based event reads. |
| Canonical full replay, full-replay-only legacy/named route | Metadata <=3; ranges <=3Q(H,P), envelopes <=3H; stored/wire bytes <=3B(H),3V(H) | Full-replay handler receives the complete 1..H prefix once, not once/page. One required preparation per route; J batch promotions, D logical model mutations; completion boundary unchanged. |
| Mapped streamed full replay | Admission Q(H,P), two measure/upload passes 2Q(H,P), verification Q(H,P): <=4Q and <=4H envelopes, <=4B(H),4V(H) | Part uploads <=ceil(Varray(H)/262,144), existing part ceiling 262,144; one stage/commit/verify each, abort at most once on ambiguity. Domain fold calls <=H plus empty/finalize calls <=2. Count persisted parts and their cleanup as mutations; no tail is represented as full replay. |
| Incremental rebuild | Operation-scoped prefix through toPosition, <=3Q(H,P), <=3H envelopes | Prior **staged** state+contiguous pages; complete candidate readback and existing required-route promotion. Rebuild checkpoint only; delivery checkpoint unchanged. |
| Reconciliation/bootstrap | <=2Q(H,P), <=2H envelopes and <=2B(H),2V(H); source metadata <=3 | 0 handler/model/batch calls; bounded source-anchor save and row+audit reconciliation transaction only. It may hydrate proof at the SAME C, never assert model completion or advance C. |

Live tail and retry share the same algorithm and budget. A legacy retry that
still loads 1..H cannot be recorded as optimized tail. If q routes share one
base and application stream, independent admission/verification still cost
`(2q + 1)delta` envelopes and `(2q + 1)Q` pages; without sharing the upper bound
is `3q delta`, `3q Q`. Different bases sum their actual delta. The existing
three-route near-limit regression reads seven suffix events for delta=1:
three admission + one shared application + three verification. Its count is
seven, not one; the orchestrator's separate head-event probe is an additional
baseline read that the metadata anchor MUST eliminate on optimized paths.

Each bounded-range invocation also performs at most two actor-internal
metadata/safety-key reads plus one event-key read per returned envelope AND
one per actual unreturned lookahead. Thus a tail's source metadata/safety-key
ceiling is **3 + 6Q(delta,P)** and event-key/source-deserialization ceiling is
**3delta + 3max(Q-1,0)**. Full replay substitutes H; four-pass mapped replay
uses **3 + 8Q(H,P)** metadata and **4H + 4max(Q-1,0)** event-key/source decodes.
Two-pass reconciliation substitutes factors 2. Source key-read/sizing bytes
are response B plus actual Blook per pass (<=2B); wire decode remains V for
returned events only. All modes' table byte/returned-envelope budgets exclude
lookahead only because it is explicitly added here; it is never free I/O.
Current has Q=L=0 and uses section 2.4's budget.
Actor cache-barrier recovery is a separately reasoned failed/recovery attempt,
not an uncounted expansion of a successful hot-path budget.

For tail/full-replay attempts, proof-row reads <=**3A+5**, attempted proof-row
writes <=**3A+1**, successful completion advances <=1. These include the
additional Idle-binding CAS transition and post-bind row readback. Default
worst-case proof reads <=29 and attempted writes <=25; A=32 gives 101/97.
Each proof read also reads BOTH writer-v2 and scope-guard markers. After
completion, charge three lifecycle-snapshot operations (post-release,
owner-proof generation recheck and post-bind), at most six lifecycle-key reads,
within the following control ceilings; lifecycle begin/release key mutations
remain charged too. Other metadata/control reads <=
**64 + 32A + 16J + 8D**, key mutation attempts <=
**64 + 32A + 16J + 8D**, including retry leases/ledger, activation, lifecycle,
batch markers/receipts, selection, freshness and cleanup. For mapped full
replay add **4K** reads and **4K** mutations, K=uploaded parts, including delete.
These generous fixed protocol ceilings do not permit extra history passes or
duplicate logical model writes. Fail if any category exceeds its own ceiling,
even when the combined count is small. Shared epoch routes also charge
membership/journal/catch-up/selection operations within these same ceilings;
routes that cannot do so use explicit full-replay mode rather than claiming
the optimized profile.

Proof byte I/O <=(3A+5)S(H,R) reads and (3A+1)S(H,R) writes, plus
**2(3A+5)** marker reads <=4096 bytes each. Non-proof control rows other than
shared retry-ledger shards are <=16,384 bytes each; shard bytes are actual
serialized occupancy-dependent bytes under section 3.4, not this cap.
read-model bytes are measured separately as actual M (read+write+candidate+
prior/readback bytes), and each manifest <=1,048,576 bytes. Total reported
bytes MUST include M, part bytes, source responses, decoded arrays and proof
I/O; there is no claim that projection state itself is size-independent.

Proof validation/ID membership work permits <=
**(8A+16)(H+min(H,R)+delta)** digest/receipt visits per route attempt, plus
<=3delta source-chain extensions, <=3delta route-event hashes and
<=**3delta+3max(Q-1,0)** source-event hashes for tail (any lookahead E(i)
computed for sizing is charged, never used for admission). Source-deserialization
and sizing bytes include Blook; domain decoding/hashes apply only to returned
events. Serialized
proof parsing/copying is O(S(H,R)); ID set construction is O(H) on each actual
row validation. Allocate no retained event prefix for optimized tail: event
working bytes <=**69,206,016** (64 MiB admitted work plus one 2-MiB lookahead)
and proof working bytes <=4S(H,R). This explicitly
retains history-sized identity-proof bytes/CPU. No O(delta) total-storage,
total-byte, total-CPU, unbounded-stream or constant-memory claim is authorized.

### 3.4 Failure ceilings and workload vectors

An attempt admits one head and at most one incremental-to-canonical fallback.
CAS attempts never trigger extra source passes; exhausted attempts return
typed retry/reconciliation. Existing automatic retry semantics remain:
Attempt saturates at configured MaxRetryAttempts and work remains pending;
no lifetime cap, automatic parking or new terminal/ledger-expiry protocol is
introduced. For actual **N** activations, aggregate envelope/page/metadata/
proof/mutation ceilings are the sum of the N per-attempt ceilings (<=N times
the largest applicable per-attempt bound when parameters agree). Report N
independently of the saturated Attempt. Qualification's eight-activation
window is measurement only; a ninth observed activation has its own charged
budget and preserves pending work. No unbounded inline loop or extra history
passes within an activation are allowed.
One fallback's combined ceiling is tail budget PLUS its chosen full-prefix
budget; one must not report it as a small-tail success. Failure stops at the
first invalid page/event, invokes no later handler, and never advances past
the last independently proven durable completion. Cleanup attempts count
against control-mutation ceilings; exhaustion retains fenced reconciliation.

Worker scanning is separate scheduler overhead: 64 shard reads per scan,
at most the configured due batch (default 64), and the existing **five** CAS
attempts per scheduler ledger/lease transition (not delivery-row A).
For shard s record actual item count I(s), actual serialized bytes T(s),
entries scanned and every pre/post-mutation size. A scan charges sum(T(s));
each shard CAS attempt charges its actual read/write T(s), including other
aggregates. These bytes/entries are separate from route proof/control bytes.
Numeric qualification occupancy is **64 items per each of 64 shards**
(4096 total), each work item <=4096 serialized bytes; fixture shard ceiling
is **512+4096 I(s)** =262,656 bytes, scan ceiling 16,809,984 bytes. Also run
one unevenly occupied shard with **128** items (524,800-byte fixture ceiling).
These are workload fixtures, not new scheduler limits: larger production
occupancy reports actual I,T and cannot claim this fixed-occupancy byte
qualification; preserve pending items, never reject/prune them to fit 16 KiB.
Do not divide away scan or unrelated-item bytes when reporting one item.
Activation/catalog cold-start work is a separately named control phase,
never a hidden current-path write. Qualification measures cold and warm runs.

| Vector | Numeric workload and required result |
| --- | --- |
| W0 current / empty | H=C in {1,256,257,9999,10000}, P in {1,16,256}, R in {256,4096}: section 2.4's exact current zero-work result, bound actual Idle revision, unchanged proof/models/checkpoints/versions. H=C=0/absent with no model => `EmptyNoWork`/`Unknown`, zero event/model/checkpoint work; existing empty rebuild still writes no empty receipt/model. Missing proof stays Unknown; pre-existing model or nonzero C at H=0 requires drift reconciliation. |
| W1 long small tail | H=10000, C=9999, delta=1, P=1 and 256. Single route <=3 envelopes/pages, three-route shared fixture <=7; no sequence <=9999, no full replay or staged replacement. |
| W2 multiple pages | C=9000, H=9771, delta=771, P=256. Single route <=12 range calls, <=2313 envelopes; application <=771. Repeat with P=1: <=2313 range calls. Persisted result equals canonical 1..9771. |
| W3 boundaries | delta in {0,P,P+1,2P,2P+1}; H<=10000. Q=ceil(delta/P) for uniform <=8190-byte envelopes. For n=P=256 and exactly 8191 or 8192 event-JSON bytes, exact framed lengths are 2,097,153 and 2,097,409: Q=2, one lookahead/pass, <=257 event-key reads/source decodes per pass. At 8190 bytes, framed length 2,096,897 fits, Q=1,L=0. No read beyond H or extra terminal empty call. Include empty touched, missing and actor-proven erased streams distinctly. |
| W4 bytes / shared shards | Mixed near-1-MiB stored payloads, protected/clear data, P=256. Enforce actual 2-MiB page cap and charge up to one lookahead per boundary, duplicate actor decode/sizing bytes and any E hash, but no lookahead decryption/handler/chain extension. Verify all eventual returned copies against admission. Q may exceed ceil(delta/P), never delta; unique 64-MiB pass/wire ceilings fail one byte over without effects. Include R=4096, proof-byte cap, 64x64-item and one 128-item retry-shard occupancy fixtures; preserve every receipt/digest/pending item on rejection. |
| W5 fallback | H in {0,256,257,771,10000}, P=256, full-prefix detail/index fixture D=2,J=1. Required full prefix and actor/detail/index/freshness/checkpoint equality; H=10001 and 64-MiB+1 fail within the configured profile without publishing candidates. Separately rerun established wider streamed-rebuild compatibility cases. |
| W6 failures/races | Two replicas; overlapping tails; duplicate/reverse/conflicting ID/content/position/provenance; gaps; C ahead; changed incarnation; missing proof; provider outage; cancellation/crash at EVERY section 6 boundary. Change an application-reread event after admission but before its handler: reject before that event's effects. Commit C+1..K then crash: resume SAME reservation with original per-event receipts/identities and state at K; missing/conflicting receipt fails closed. Crash/race after release/before Idle binding and after binding/before reread: no predicted revision or false Current. A=8/32; measure N=8 and ninth pending activation; exact R/R+1 horizons. Preserve proven state and pending/reconciliation work. |

All numeric equalities and ceilings are blocking assertions, not averages or
percentiles. Timing/RSS may be recorded diagnostically without inventing
latency acceptance. Workload, ceiling or mode changes require revised approval.

## 4. Handler capability and fallback negotiation

Negotiate per named route using one verified catalog generation. Add optional
catalog capability `projection-source-guard/v1`, declaring source-v1,
guard-evidence v1, handler semantics (`single-event-v1`,
`mapped-incremental-replay-v1`, `shared-epoch-v1`, `full-replay-v1`) and the
selected-owner proof support. Bind the descriptor to the catalog fingerprint;
do not accept a process-local capability cache as cross-replica authority.
Existing `/project/v2` request/response/status/dispatch identity stay intact;
guard evidence is server-owned, not a new public dispatch-result family.

| Handler / route | Allowed live and rebuild behavior |
| --- | --- |
| `IDomainProjectionHandler` and explicit legacy adapter | Full 1..H replay only, bounded and staged through the existing rebuild/promotion path when used as fallback. Legacy discovery of projection type after `/project` cannot authorize a prior current check; require catalog-declared stable route identity before enabling it. Preserve public v1 `/project` semantics and explicit adapter registration. |
| `IAsyncDomainProjectionHandler` alone | No inferred incremental capability. Ordinary existing delivery remains compatible; guarded optimization chooses complete-prefix fallback. Async or successful HTTP alone proves neither prior state nor safe append. |
| `IAsyncDomainProjectionSingleEventHandler` | One contiguous event per call, prior durable state loaded through platform store and stable dispatch/batch idempotency. Calls may repeat safely inside the retained redelivery contract. A missing prior state at C>0 returns rebuild-required before mutations. |
| `IAsyncDomainProjectionIncrementalReplayHandler` | Explicit mapped streamed rebuild + verified append capability. Require selected owner prefix/generation/receipt at C, then exactly one next event per handler call. Support multi-event tail by repeated contiguous single-event calls, never by changing its existing contract to accept a page. Cold/missing selected state triggers canonical streamed replay. |
| `IAsyncDomainSharedProjectionEpochHandler` | Require `EpochEnabled`, registered writer/scope, selected generation and per-source durable applied position. Registration/journal acceptance alone cannot advance delivery completion. Await fold and selected-generation proof, keep required writer/inventory fences and tenant/global owner-key rules. Per-owner current proof cannot label the entire shared projection current; that needs its existing complete writer/owner evidence. |
| Incremental rebuild interface | Prior operation-private staged state+contiguous page, never prior live state plus a partial replacement. Required-route promotion remains atomic/resumable as 1.19 specifies. |
| Unknown, absent, mismatched or unsupported capability/version | `fallback_full_replay` only when canonical readable full prefix and a compatible full-replay handler exist within bounds. Otherwise `unsupported_capability` or `delivery_rebuild_required`, with no effects. Do not assume sequence zero or silently try an alternate public route. |

A handler's explicit rebuild-required result may trigger one fallback only
before effects, or after durable reconciliation proves any partial effects
complete/idempotent under the SAME stable identity. A timeout, generic failure,
local cancellation or unknown partial batch is indeterminate, not permission
to invoke a different handler. Reconcile first. Full replay accumulates or
streams the complete bounded prefix to the existing staging protocol; a page
never becomes a complete-stream argument. Frozen H/toPosition excludes later
events. If retained source floor>1 and full prefix is unavailable, return
`history_unavailable` without using a snapshot, inventing missing events or
overwriting the previous live model.

## 5. Source-sequence guard and concurrency outcomes

The sole guard scope is canonical tenant/domain/aggregate/projection route;
shared projections additionally retain their existing generation/writer
scope. Aggregate sequence is positive and local, never global ordering.
Exact duplicate identity is the persisted EventStore `MessageId` together
with the existing event/prefix fingerprint, source anchor and scoped receipt.
Never replace it with payload hash alone, GUID generation, delivery time,
retry-work ID or transport correlation.

| Input / persisted state | Outcome and allowed mutation |
| --- | --- |
| Malformed, foreign-scope, repeated/reversed sequence or repeated ID in tail | `delivery_identity_conflict`/`delivery_gap` before reservation/handlers. Never sort/deduplicate into validity. |
| Completed exact duplicate inside receipt horizon | `delivery_already_completed`, no model/checkpoint mutation, after validating the entire row. Current-head trigger uses section 2.4; arbitrary supplied conflicting event bytes cannot be ignored merely because metadata is current. |
| Lower sequence with verifiable matching retained receipt | Stale no-op, no model/checkpoint regression. A lower trigger is a wake-up hint only; observed source head still determines later catch-up work. |
| Lower sequence outside exact receipt horizon | `delivery_reconciliation_required`, not an invented exact duplicate. Preserve floor/cumulative proof/full ID digests; authenticated history reconciliation only if source is available. |
| Same sequence with changed ID/content, or v2 position/provenance-only change | `delivery_identity_conflict`, fail closed and retain diagnostic reason. Source chain comparison is additional protection; existing v1 identity semantics remain v1 until an explicit full rebuild upgrades them. |
| Future gap, input starts >C+1, early empty page or missing event | Retry/defer `delivery_gap`; no advancement past contiguous proven completion, no later event effects. |
| C>H, wrong incarnation, wrong source digest, invalid row/receipt/fingerprint/digest-list | Typed `checkpoint_drift`, `source_conflict`, `delivery_schema_regression` or `delivery_reconciliation_required`; no handler effects, no resetting C to zero. |
| Active same work held by another replica | `delivery_in_progress`, retry. It must not acquire/release the winner's lifecycle token or dispatch concurrently. |
| Same rightful durable retry fence, or expired replay-safe reservation | CAS reclaim increments fence and preserves stable dispatch ID/manifest. Only one reclaimer wins; stale completion/release is fenced. Unsafe reclaim requires reconciliation. |
| New contiguous suffix with verified prior state | CAS reserve exact base/source/selection; complete only through section 6. Loser rereads/reclassifies within A, never last-write-wins. |
| Erased, rebuilding, unavailable or unsupported version | Existing lifecycle exclusion; typed defer/reconciliation/unavailable. No resurrection, implicit prune or claimed success. |

Use the existing first-write ETag/transaction implementation, monotonic fences,
unique per-invocation lifecycle lease and stable persisted retry fence.
Reservation expiry is a recovery trigger, not proof of failure or permission
to blindly reapply. A stale server attempt must also fail the model/batch or
selected-generation fence before publication: final row CAS alone cannot
undo a stale model write. Every model write/readback retains lifecycle and
generation validation. Same-store transactional batches or the existing
marker-gated resumable protocol provide this boundary; never claim atomicity
across actor state, components or external effects.

## 6. Commit, visibility and crash recovery

There is one **logical proof boundary** for advancement: the final conditional
delivery-row completion, with actual post-release Idle revision and immutable
selected-model proof, under the exact reservation fence/base. It atomically
records C=H, receipts/digests and section 2 bindings and clears the reservation.
Cross-component work before it is resumable, not physically atomic. `Current`
additionally needs matching final source, lifecycle and model readback,
ProjectionBacked provenance and no incomplete required route. Rebuild success
uses its existing promotion/readback/checkpoint rather than substituting this
delivery checkpoint.

Normal finalization has three successful delivery-row transitions, charged
explicitly in section 3: reserve; CAS `IdleBindingPending` after all admitted
handlers, receipts, selected output/freshness and source verification prove H
(C and ActiveReservation remain unchanged); and final completion. Pending
evidence stores only bounded immutable head/model references and the exact
reservation fence/lifecycle token, not a second receipt ledger. While pending,
fresh delivery admission and ordinary release/reclaim that could redispatch
are refused; only same-work fenced finalization recovery is admitted.

Release the exact owned lifecycle delivery token with existing
`CompleteDeliveryWriteAsync`; it increments the persisted revision and removes
the transient row. THEN read actual `Idle` snapshot L1; read/verify the same
selected model and persisted receipt/freshness, with an Idle-revision recheck
L2 inside that proof operation; require L1=L2. CAS-complete the unchanged
pending reservation/base using that observed revision. Read back the completed
row and Idle snapshot L3; only identical bound revision/model/source may
report current. Never store `Delivering.Revision+1` as a predicted Idle value.
A lifecycle change after CAS leaves a stale binding and cannot produce
current; it does not undo a proven completed checkpoint or overwrite newer work.

Every upgraded writer for this scope, including rebuild, erase and operator
reconciliation, must honor the pending delivery-row fence before acquiring a
competing lifecycle/write admission. Pending finalization defers those writers;
an authorized invalidation must first CAS-invalidate the exact reservation
fence in the SAME delivery row before changing selection/lifecycle. That
invalidation makes the old final CAS fail; it never silently clears evidence.
This reuses delivery-row CAS and lifecycle fencing, with no extra store or
cross-component transaction. Its reads/transitions are charged to that
competing/recovery attempt. Legacy writers cannot participate after activation.

If release response, process or Idle-binding CAS is lost, durable pending
evidence and original retry identity remain. A recoverer must claim/reclaim
the same reservation with existing v2 fence rules, acquire its own lifecycle
lease without releasing another token, and prove the pending H/model/receipts
under that fence. Record its current operation token in the pending CAS,
release its own lease, read actual Idle and finish the same sequence above.
No handler calls or new logical batches occur during finalization recovery.
An active competitor/rebuild/erase, changed selected generation, unreadable
receipt or failed ownership proof defers/reconciles; it cannot adopt an
arbitrary idle revision. Recovery has its own charged activation (<=3 row
transitions and section 3's read/CAS ceilings), and expired unsafe work never
redispatches. If the final completion already persisted, verify it read-only;
cleanup never advances C again.

| Durable boundary / injected interruption | Recovery and visibility |
| --- | --- |
| Before source read or during admission/decryption | No handler/model writes. Preserve last complete view/C. Retry provider outage; reject unreadable/invalid input. Caller cancellation stops the next read and later effects. |
| After retry activation/lease, before reservation | Persisted same work remains pending. Reacquire within lease rules; checkpoint unchanged; no activation success. |
| After CAS reservation, before handler | Keep/release only the matching fence. Safe resume retains dispatch ID; expired unsafe work reconciles. Never release a competitor's lifecycle lease. |
| During event application, detail/index batch, candidate or part upload | Existing batch/SDK ownership prevents partial-page replacement. Completed contiguous event batches may be selected through K while delivery C stays unchanged. Section 3.1's SAME-reservation recovery proves original receipts/identities through K, reconciles the original partial event manifest and resumes K+1; no fresh admission at C against an advanced model. Rebuild candidates stay non-live. |
| Durable batch/selected model committed, response lost or caller canceled | Read same immutable manifest/receipt/selection under the same dispatch identity. `AlreadyCompleted` is valid only with persisted proof. Use bounded independent finalization token (maximum **30 seconds**) solely for readback/fence completion/cleanup, never to start later handlers; timeout retains reconciliation. |
| Readback conflict, missing receipt, changed generation or freshness mismatch | No delivery completion or promotion. Keep old complete visible selection or existing committed model as unresolved; record retry/reconciliation. Compensation uses existing batch ownership and prior-image rules only, never unconditional writes over a newer generation. |
| Verification source pass fails after handler effects | Retain reservation/effects for stable-ID reconciliation; C unchanged. No canonical fallback until effects are proven/compensated. Distinguish unreadable source from handler completion; do not erase completion receipts. |
| Pending CAS persisted, lifecycle release/response lost, or crash before Idle binding | C remains unchanged; pending fence/model/head references survive. Use fenced finalization recovery above; release only owned token, read actual Idle, bind it in final completion and reread. No predicted revision, fresh admission or handler replay. |
| Final completion CAS response lost or lifecycle changed before readback | Reread row, actual Idle and selected proof. Exact bound completion => settled; changed Idle/model => no current, reconcile; newer completion => stale/fenced no-op; unresolved => retry. No second logical write/version/notification. |
| Row complete, outbox/ledger cleanup fails | Durable bound proof survives; retry cleanup under original ownership as charged control work. Never advance C or rewrite Idle evidence merely for cleanup. |
| Rebuild promotion partial, failure/cancellation or crash | Existing staged/resumable batch readback, compensation or same-operation resume; no partial selected model; no rebuild completion checkpoint until all required routes and output versions prove durable. Clear only owned terminal `Rebuilding`; failed cleanup remains explicitly reconcilable. |
| Restart, fleet race, restore or retention change | Actor cache barrier and persisted proofs are reread; no process memory authority. Verify incarnation, floor, source/row/model generation and marker before resume. Inconsistent restore yields drift/unavailable, not a successful current check. |

Retain stable EventStore head `MessageId` as dispatch ID and the existing
batch identity derivation. A retry must preserve immutable manifest identity
or prove a conflict; no timestamp/random replacement IDs. Persisted exact
receipt count/floor and complete message-ID digests remain intact. Store TTL,
retry aging and notification acknowledgement cannot erase correctness proof.
Lifecycle cleanup failure is not a license to replace a complete live model.

## 7. Preservation, compatibility vectors and persisted pass conditions

The canonical oracle remains `AggregateReplayer.Replay<TState>` through the
same inclusive H/toPosition, using platform serialization and production
DomainService handlers/stores. Cost recorder substitutes alone are inadequate.
Across canonical replay, incremental and fallback modes compare **semantic**
actor state, selected detail/index values/deletes and owner coverage through
the same H, source content proof, completed sequence, authoritative freshness
meaning and terminal lifecycle/pending-work meaning. Different modes may
legitimately have different manifests, batch/selection receipts, operation IDs,
projection-version values, lifecycle revisions and delivery/rebuild checkpoint
families; do not require byte/identity equality between them. Independently
validate each mode's expected immutable protocol identities and persisted
receipt/status, scope, floor/digests, freshness/version binding, checkpoint
and retry/reconciliation evidence. Compare stored source envelopes unchanged.
Identity equality applies to retries/races of the SAME mode/work: its original
dispatch, per-event batches, manifest and receipt identities must persist;
only audit time, opaque ETag and monotonic fence/attempt/revision bookkeeping
may differ as explicitly asserted. ETags never become freshness versions.

Every row below MUST run through production orchestration/handler/store seams
and the live Dapr lane where indicated. Existing tests are preservation seeds,
not claims that they already prove the proposed optimization. Story 6.4 adds
the named vectors to those files without weakening existing assertions.

| Invariant / vector | Design sections and existing production evidence seam |
| --- | --- |
| Atomic source anchor, codec and append/restore ambiguity | 2,3,6,9; `tests/Hexalith.EventStore.Server.Tests/Events/EventPersisterTests.cs`, `Events/AggregateMetadataTests.cs`, `Actors/AggregateActorTests.cs`, `Actors/AggregateActorInfrastructureFailureTests.cs`, `Actors/AggregateActorGetEventsTests.cs`, all under the same Server.Tests root. Add exact golden hashes, every metadata rewrite preserving the anchor, multi-event atomic save/discard, lost-save readback, source bootstrap, incarnation and retained-floor vectors, without modifying snapshot semantics. |
| Duplicate, active duplicate, CAS race, expired/rightful fence, stale completion | 2,5,6; `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionDeliveryIdempotencyCoordinatorTests.cs` (`ConcurrentClaims_ProduceOneReservationAndOneDeferredDuplicate`, `ExpiredReservation_ReclaimsSameIdentityAndFencesLateCompletion`, `ActiveReservation_WithMatchingDurableFence_ReclaimsImmediatelyForRightfulOwner`); live `ConcurrentDuplicateReverseAndConflict_StayEquivalentToOneInOrderDelivery`. Add W0/W6 source-anchor/model-fence assertions and crash points. |
| Gap/reversal/conflicting identity/content and old-ID reuse | 3,5; same coordinator tests (`NonContiguousInput_IsGapWithoutStoreReadOrMutation`, `IncrementalSuffixRejectsMessageIdOlderThanReceiptWindow`, `MappedV2CompletedReceiptRejectsPositionAndProvenanceOnlyRedelivery`), live `GapThenCanonicalMissingEvent_DoesNotInvokeUntilContiguousAndThenConverges`. Add every raw source-codec field mutation and no sorting. |
| Proof horizon/byte/CPU preservation | 2,3,5; same file (`BelowRetentionFloor_RecordsReconciliationAndPreservesState`, `Compaction_RespectsConfiguredBoundaryAndKeepsCumulativeHead`, `IncrementalSuffixEnforcesTotalOwnerEventLimit`, `AnchoredSuffixAdvancesDurablePrefixWithoutReplayingPriorEvents`). W1/W4 measure complete digest-list bytes/visits and R=256/4096, R+1 duplicates; no proof truncation. |
| Exact bounded pages, identity validation, protection and cancellation | 3,6; `tests/Hexalith.EventStore.Server.Tests/Projections/EventStoreProjectionDeliveryHistoryReaderTests.cs` (`ReadAsync_PagesExactPrefixAndDecryptsProtectedPayloads`, `ReadAsync_ForeignEnvelopeFailsBeforePayloadDecoding`, `ReadAsync_IncompleteSecondPageFailsClosed`, `ReadAsync_CancellationAfterPage_PreventsNextActorCall`, `ReadAsync_ProviderUnavailable_IsTransientRatherThanValidationFailure`). Add W2/W3/W4 byte packing and same-head conflicting prefix. |
| Existing mapped suffix and real repeated passes | 3,4; `tests/Hexalith.EventStore.Server.Tests/Projections/NamedProjectionDispatchCoordinatorTests.cs`, `SequentialNearLimitDeliveryReadsOnlyNextSourceEventAfterCompletedPrefix`. Preserve the three-route seven-event count and owner limit; add arbitrary delta, current zero head-envelope probe, fallback, catalog drift and no repeated logical writes. |
| Full-prefix semantics, empty, bounded target and replay equivalence | 3,4,6; `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionRebuildProductionPathTests.cs` (`Rebuild_MoreThanTwoPages_PersistsOracleEquivalentActorDetailIndexVersionsAndCheckpoints`, `Rebuild_ExactBoundaryAndBoundedTarget_PersistOracleEquivalentState`, `Rebuild_EmptyStream_PreservesExistingLiveStateAndCompletesWithoutProjectionWrite`, `Rebuild_ActorProvenErasedEmptyStreamDeletesSelectedNamedRows`). W5 must preserve empty versus erased distinction and every owned surface. |
| Staging, promotion, failure, cancellation, resume and lifecycle | 6; same rebuild test file (`Rebuild_CanceledAfterFirstPage_PreservesEveryLiveSurfaceAndClearsLifecycle`, `Rebuild_InterruptedStorePromotion_PreservesLiveViewAndSameOperationRetryConverges`, `Rebuild_PromotionFenceReleaseFailure_RemainsRunningAndRetryCompletesCleanup`, `Rebuild_ResumeFromPersistedProgress_ReplaysCompletePrefixAndConverges`); `Client.Tests/Projections/DaprReadModelBatchTests.cs` and live `ReadModelBatchLiveSidecarTests`. W6 injects each boundary on tail and fallback as well. |
| Durable handler barrier, outcome/cancellation truth, old public semantics | 4,6,9; `tests/Hexalith.EventStore.DomainService.Tests/DomainProjectionHandlerCompatibilityTests.cs`; `DomainProjectionDispatcherV2Tests.cs` (`DispatchAsync_DoesNotCompleteBeforeDurableHandlerBarrier`, `DispatchAsync_PropagatesCancellationAndDoesNotStartLaterHandler`, `DispatchAsync_HandlerLocalCancellationMapsFailureAndContinues`, `RebuildAsync_NonCanonicalEventHistoriesAreRejectedBeforePreparation`); `MixedProjectionCatalogTests.cs`. Vectors: old handler/new server, new handler/old server (guard off), unknown version, same catalog on restarted replicas, missing prior model and unsupported descriptor. |
| Shared owner/generation and tenant isolation | 2,4,5; `DomainFencedSharedProjectionIntegrationTests.cs` (`GlobalEpochDeliverySeparatesIdenticalOwnerIdsAcrossTenants`, `NamedDeliveryDuringSharedRebuild_JournalsThenConvergesAfterAtomicPromotion`, `MultiEnvelopeDelivery_RejectsSequenceGapBeforeAnyJournalWrite`, `LegacyOwnerCheckpointRequiresExplicitRebuildBeforeLaterSequence`); `DomainPartitionedSharedProjectionRebuildTests.cs`. Journal-only success cannot complete delivery; one current owner cannot label shared projection current. |
| Migration, mixed writer, downgrade and erasure | 5,9; Server `ProjectionDeliveryCutoverTests.cs`, `ProjectionDeliveryReconcilerTests.cs`, `ProjectionDeliveryStateContractTests.cs`, `ProjectionEraseCoordinatorTests.cs`; live `ProjectionDeliveryCutoverLiveSidecarTests`, `ProjectionEraseLiveSidecarTests`, `DowngradedDetailRow_FailsClosedWithoutHandlerWhileIndexSiblingCompletes`. Add absent/unsupported anchors, schema1->2, dropped fields, partial-fleet activation denial, pre/post-activation restore, retained floor>C+1 and marker survival. |
| Payload-free telemetry and provenance | 2,8; Server `ProjectionDeliveryDiagnosticsTests.cs`, `ProjectionDeliveryDocumentationTests.cs`, rebuild production tests and live normal-delivery test. W0..W6 assert counters against actual calls/bytes and no secrets/payloads/raw IDs. |

Abbreviated `Client.Tests` above means
`tests/Hexalith.EventStore.Client.Tests`. Live methods refer to
`tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/NamedProjectionDispatchLiveSidecarTests.cs`.
DomainService filenames after the first entry are in the same test directory.
The preserved normative rebuild sources are
`_bmad-output/specs/spec-correct-paged-rebuild-and-replay-equivalence/SPEC.md`
and `rebuild-semantics.md`; delivery source is
`_bmad-output/implementation-artifacts/1-13-projection-handler-delivery-idempotency.md`.
Protected stored bytes/identity remain unchanged, missing keys/provider errors
never skip events, and current proof cannot outlive a changed lifecycle/model
generation. Current inspection does not decrypt historical events; an
unavailable selected-model proof cannot authorize current. These requirements
neither implement Epic 8 nor claim encryption, legal erasure or production key
custody.

## 8. Telemetry, lifecycle and provenance

Extend existing EventStore diagnostic sources only. Counters/histograms report
head/C/lag as numeric measurements, actual metadata/range/key reads, pages,
envelopes, wire/source/proof/model bytes, proof visits, handler/fold/batch calls,
key mutations, CAS attempts, selected mode, short-circuit, fallback,
reconciliation and durable terminal outcome. Use bounded enums for mode
(`current`, `tail`, `full_replay`, `mapped_replay`, `incremental_rebuild`,
`reconciliation`) and status (`completed`, `already_completed`, `retryable`,
`failed`, `indeterminate`, `fenced`). Route tags are limited to the verified
catalog's configured routes; unknown becomes `unknown`. Reasons are bounded
ASCII <=64 bytes from the closed codes here and existing dispatch/readability
reason catalog. Unknown exceptions map to `delivery_state_unavailable` or
`handler_failure`, never exception text.

New reason values are `head_changed`, `source_conflict`, `source_proof_missing`,
`source_proof_unsupported`, `checkpoint_drift`, `history_unavailable`,
`delivery_owner_limit_exceeded`, `delivery_page_limit_exceeded`,
`delivery_byte_limit_exceeded`, `delivery_proof_limit_exceeded`,
`fallback_full_replay`, `guard_activation_required`. Existing duplicate, gap,
in-progress, conflict, schema, reconciliation, rebuild-required, provider and
cancellation codes retain meaning. Export measurements even on failed partial
attempts. Telemetry failure never authorizes checkpoint/model mutation.

No metric labels contain tenant, aggregate, message/work/dispatch IDs, ETags,
state keys, digests/fingerprints, provider names, exception messages or payloads.
Authorized tenant-scoped support records may correlate via the existing
validated first-boundary correlation token, with access control and no raw
cross-tenant identifiers. Logs/traces/DTOs contain no event/read-model content,
keys, secrets, stack traces or provider internals. Byte/hash evidence kept for
tests remains private test evidence; public telemetry exposes sizes only.

`Current` requires section 2/6's persisted ProjectionBacked proof for every
required route at its observed head. Lag>0 => `Stale`; active owned rebuild =>
`Rebuilding`; failed required work with an intact last complete view =>
`Degraded`; inaccessible authority => `Unavailable`; local model only =>
`Local-only`; missing route/anchor/provenance => `Unknown`. Handler-computed
and unknown routes cannot acquire projection freshness through this feature.
Freshness notifications remain best-effort hints. No transport acknowledgement,
started attempt, page count, registration, journal append, ETag or scheduled
cleanup changes lifecycle into success. Terminal rebuild failure clears only
owned rebuild state; cleanup uncertainty remains reconcilable. Story 6.3
authorizes no Admin implementation or new actions.

## 9. Compatibility, migration and rejected alternatives

Public legacy projection interfaces, `/project` and `/project/v2` carriers,
`IReadModelStore`, query/lifecycle/freshness ABI, EventStore identity and v1/v2
route-fingerprint bytes remain unchanged. Optional source inspection/catalog
fields and SDK proof support are additive package APIs. Delivery schema 2 is
an explicit internal persisted-format upgrade: schema1 can be inspected and
reconciled but cannot authorize optimized work. Writer protocol remains v2's
CAS/fencing protocol; versioned guard activation is additional readiness
evidence. Old schema1-only v2 binaries encountering schema2 must retain their
existing unsupported-schema fail-closed behavior, never downgrade it.

| Combination / transition | Frozen rule |
| --- | --- |
| New server/old handler | No descriptor => canonical full replay, within existing handler/configuration bounds. No automatic handler conversion. Legacy full path stays compatible. |
| Old server/new handler, guard not active | Existing advertised semantics only. New fields are optional; handler must not require tails. No optimized cost claim. |
| New source writer with missing old source proof | Continue compatible append without authorizing guard until bounded explicit bootstrap. Do not seed a chain at the latest sequence or drop old fields while reconstructing metadata. |
| Existing complete schema1 delivery row | Quiescent reconciliation may upgrade to schema2, preserve C/receipts/IDs/fingerprint version/floor, and add source/model binding ONLY after canonical source and selected-model receipt/readback prove it. If that model proof is absent, retain unproven schema1/reconciliation status; an explicit canonical staged rebuild is required. History alone cannot assert the model is complete. |
| Missing/zero delivery row | Proven new empty source may initialize C=0 with no model completion claim. Nonempty/unknown prior scope uses authenticated reconciliation or explicit full rebuild; never assume an absent row means a fresh model. |
| Mixed old/new append or projection writers | **No mixed-writer activation.** Old writers may ignore/drop added metadata or mutate outside the new fences. Keep guard disabled until all append, immediate, poller, retry, reconciliation, rebuild and erasure writers are upgraded/quiesced. Catalog handler-version agreement is required independently. |
| Unsupported codec/schema, corrupt/opaque provider state, field regression | Fail closed with typed reason and retain evidence; no tail/current optimization. Compatibility fallback is allowed only with readable validated source and safe model protocol, never to bypass corruption or active reservation. |
| Trimmed history | Current can use complete source/model evidence; tail requires floor<=C+1. Bootstrap or canonical fallback requires floor=1. No snapshot substitution, history rewrite or fabricated anchor. If needed source is unavailable, preserve live state and require reconciliation. |
| Rollback before activation | Stop writers; restore verified full pre-upgrade backup and old fleet as one maintenance operation, or leave new fleet guard disabled with unchanged compatibility behavior. Never selectively restore a checkpoint/model/source component. |
| Rollback after any guarded schema2/source completion | No in-place downgrade or marker lowering/deletion. Stop all writers, retain diagnostic evidence, restore consistent pre-cutover source, rows, models, receipts, lifecycle and retry backup into an isolated/replacement environment, and switch the entire fleet. Old binaries never write the active guarded store. |

The maintenance procedure is fixed: capture exact full repository/package
identities and backups; stop traffic and every writer; verify backup restore;
deploy all new binaries with guard readiness closed; bootstrap each eligible
actor-owned source chain by bounded canonical 1..H replay under the actor's
exclusive operation, reread identical head before its atomic metadata save;
reconcile model/row bindings as above; read back all participating components;
record authenticated operator, fleet and capability inventory, backup identity,
spec digest and quiescence/no-downgrade attestations in the existing cutover
control record; activate the exact per-scope guard records in section 2.3
using first-write/CAS beside the UNCHANGED existing v2 marker; then restore
traffic. Store-global marker survives scoped
erasure. Missing proof/over-limit/trimmed streams remain explicitly unqualified
and cannot prevent their compatible nonoptimized routes from being diagnosed.
Activation may select only the inventoried qualified route/scopes; readiness
MUST NOT globally declare unqualified scopes guarded. Partial activation is
explicit scope admission, never a mixed writer fleet. Legacy live work may
continue only through the same upgraded writers and established safe protocols.

Source bootstrap event/byte work obeys section 3's reconciliation ceiling,
uses no handlers and changes no historical envelope. It is an actor-owned
maintenance capability within Story 6.4, not new evidence tooling. A source
metadata checksum and codec version do not attest storage-provider behavior.
All state operations remain through owning Dapr actor/state seams, with ETag
first-write/qualified same-component transactions or existing resumable
equivalent. No provider SDK, private actor-state access, TTL-based proof or
direct database verification is allowed. Dapr/Redis live evidence proves
Development/test correctness only; AD-26 production ratification/evidence
remains separate and is not supplied by this gate.

Rejected alternatives are closed decisions:

- `C==H`, latest-ID-only, ETag-only or timestamp-only checks: no whole-prefix
  content continuity or completed-model evidence.
- A second independent checkpoint, aggregate-wide checkpoint or in-process
  semaphore as authority: wrong scope or no cross-replica fencing.
- Replacing v1/v2 fingerprints or trimming `CompletedMessageIdDigests` to make
  cost constant: breaks retained-identity/horizon compatibility.
- Hashing decrypted/upcast payload into the append anchor: unstable read-time
  representation and protection/provider dependence; bind immutable storage.
- Per-event source-proof store, Merkle-tree migration or generic receipt/store
  redesign: unnecessary; one actor-owned cumulative head and scoped completed
  base suffice for suffix-chain verification.
- Paging a full-replay handler, sorting malformed events, live page-local
  replacement or best-effort checkpoint after an unfenced write: violates
  1.18/1.19 and AD-20.
- Full replay after an ambiguous incremental effect, blind compensation, new
  random retry identity or lease expiry as success proof: repeats/regresses
  logical mutations; require stable-ID reconciliation.
- Rolling old/new writers after activation, deleting markers to downgrade,
  partial restore, TTL proof retention or direct-provider fast paths: loses
  authoritative state/fences and portability.

No unresolved design decision is deferred to Story 6.4. Only human approval,
implementation, and execution of the fixed validation vectors remain pending.

## 10. Validation contract and authorization gate

Story 6.3 verification is document-only: inspect every reference and W/vector
against Story 6.3's ten clauses, FR33-C3/NFR8/G-NFR8/OR16 and AD-7/8/12/13/20;
check tracked and untracked whitespace/LF/BOM and recompute normative SHA-256.
Do not execute runtime suites or mark runtime evidence passed for this story.
No test results are asserted by listing these future commands.

After named final approval, Story 6.4 MUST record its full source revision,
this normative digest, approved scope, workload/capability/limits, SDK/Dapr and
component versions, exact command/exit status, test counts and declared skips,
per-mode measured cost vector, persisted equality assertions, and retained
sanitized evidence identity. Missing/extra skips or environment failure keep
the affected runtime gate unproven. Use existing runner/fixtures and artifact
capture; this spec creates no new evidence tooling.

Commands below run from the repository root. Debug/source mode is local
development; CI Release/package mode is the final build AND affected-suite
execution gate. Use the pinned SDK
and existing central packages; restore after changing modes. xUnit v3 focused
selection uses the built assembly's single-dash `-class`, never project
`dotnet test --filter`. Namespace wildcards select the existing and newly
added vectors in the same classes.

```bash
# Story 6.3: tracked whitespace, then explicit new-file whitespace/LF check.
git diff --check
python3 - <<'PY'
from pathlib import Path
p = Path('_bmad-output/implementation-artifacts/spec-projection-cost-sequence-guard.md')
b = p.read_bytes()
assert not b.startswith(b'\xef\xbb\xbf') and b'\r' not in b and b.endswith(b'\n')
assert all(line == line.rstrip(b' \t') for line in b.splitlines())
print('Projection spec whitespace/LF/BOM: PASS')
PY
# Recompute digest using the command above Document identity.

# Story 6.4 focused Server production coordinator/history/rebuild and guard vectors.
dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -m:1
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -class '*ProjectionDeliveryIdempotencyCoordinatorTests' -class '*NamedProjectionDispatchCoordinatorTests' -class '*EventStoreProjectionDeliveryHistoryReaderTests' -class '*ProjectionRebuildProductionPathTests' -class '*ProjectionDeliveryCutoverTests' -class '*ProjectionDeliveryReconcilerTests' -class '*ProjectionDeliveryStateContractTests' -class '*ProjectionDeliveryDiagnosticsTests' -class '*ProjectionEraseCoordinatorTests' -class '*EventPersisterTests' -class '*AggregateMetadataTests' -class '*AggregateActorTests' -class '*AggregateActorInfrastructureFailureTests' -class '*AggregateActorGetEventsTests'

# DomainService semantics, existing public ABI, shared-owner and completion barriers.
dotnet build tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -m:1
dotnet tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests.dll -class '*DomainProjectionHandlerCompatibilityTests' -class '*DomainProjectionDispatcherV2Tests' -class '*MixedProjectionCatalogTests' -class '*DomainFencedSharedProjectionIntegrationTests' -class '*DomainPartitionedSharedProjectionRebuildTests'

# Existing real Dapr/Redis fixture; Docker and pinned daprd must be available.
dotnet build tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -m:1
dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -class '*NamedProjectionDispatchLiveSidecarTests' -class '*ReadModelBatchLiveSidecarTests' -class '*ProjectionDeliveryCutoverLiveSidecarTests' -class '*ProjectionEraseLiveSidecarTests'

# Full affected regressions; run each project assembly individually.
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll
dotnet tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests.dll
dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll
dotnet build tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -m:1
dotnet tests/Hexalith.EventStore.Client.Tests/bin/Debug/net10.0/Hexalith.EventStore.Client.Tests.dll
dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -m:1
dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Debug/net10.0/Hexalith.EventStore.Contracts.Tests.dll

# CI Release/package gate; restore/build Release and execute Release assets.
dotnet restore Hexalith.EventStore.slnx -p:Configuration=Release -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true
dotnet build Hexalith.EventStore.slnx -c Release --no-restore -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true -m:1
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll
dotnet tests/Hexalith.EventStore.DomainService.Tests/bin/Release/net10.0/Hexalith.EventStore.DomainService.Tests.dll
dotnet tests/Hexalith.EventStore.Client.Tests/bin/Release/net10.0/Hexalith.EventStore.Client.Tests.dll
dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll
dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll
```

The live vectors MUST access persisted actor state through the actor boundary
and read-model/checkpoint/receipt/lifecycle/retry state through their owning
Dapr/platform seams. Existing direct backend assertions may be diagnostic
only and cannot substitute for these vectors. No backend credential, test-only
mock success, HTTP status or log line is sufficient persisted proof. Release
build must have zero errors and warnings; tests must pass with no unexpected
skips, and fixture processes must be released. Environment blockers record
the exact failing command/result separately from focused evidence; never
weaken gates to hide them.

The final detached approval MUST identify real human names/logins for all
three required roles: **Epic 6 owner, architecture owner, Test owner**. A
human may hold multiple roles only with explicit role authority recorded;
tool personas/CI identities cannot fill a human role. It MUST bind this exact
normative SHA-256, version1, numeric budgets/W0..W6, validation commands and
scope, an approval date, and explicit unconditional **authorization for Story
6.4**. The existing allowlist identifies Jérôme Piquot (`jpiquot`) as
architecture/EventStore owner only; it does not by itself assign Epic 6/Test
authority. Obtain those named role attestations, do not invent them.

Missing, stale, conditional, self-declared, role-incomplete, digest-mismatched
or scope-mismatched approval keeps Story 6.3 backlog, Story 6.4 unauthorized,
G-NFR8 FAIL/BLOCKED and OR16 open. Approval does not establish runtime NFR8
acceptance, readiness, release, production qualification or deployment
authority; any applicable AD-12 assurance/transition controls remain required.
Story 6.4 preflight MUST recompute this digest and check detached approval
before any runtime/test change. Neither the document author nor an automated
review may substitute its own approval.

<!-- HX-PCSG-V1-NORMATIVE-END -->

## Detached final approval record

| Field | Recorded value |
| --- | --- |
| Normative version | 1 |
| Final normative SHA-256 | 78e453a7af3daca5191731c2a6aff5e631db73d6d91993ca1686b6ebd70b8711 |
| Epic 6 owner name/login and role authority | **Approved — Jérôme Piquot (`jpiquot`)**, sole project owner and contributor; this session's response accepts the requested Epic 6 owner role |
| Architecture owner name/login and role authority | **Approved — Jérôme Piquot (`jpiquot`)**, sole project owner and contributor; existing architecture-owner allowlist and this session's confirmation |
| Test owner name/login and role authority | **Approved — Jérôme Piquot (`jpiquot`)**, sole project owner and contributor; this session's response accepts the requested Test owner role |
| Approval date and immutable approval reference | **2026-10-15**; owner message in this conversation, reproduced below; exact UTF-8 statement SHA-256 `657f0dc25b18b826b37ab3be53273043fa98ca61a40923c9a913922816bf531f` |
| Approval of these final bytes/budgets/workloads/commands | **Approved**, version 1, the normative digest above, accepted scope, numeric budgets, W0–W6 and section 10 commands |
| Explicit Story 6.4 authorization | **AUTHORIZED** to implement exactly this approved specification and its preflight/validation requirements |
| Story 6.3 / G-NFR8 specification prerequisite / OR16 | **Done / owner-approved specification / resolved for the specification gate** |
| Approval posture | One named human holding the three roles; no independent review or achieved AD-12 sealed assurance level is claimed |
| Runtime or production evidence claimed | **None** |

In direct response to the request for named Epic 6, architecture and Test
owner approval of the recorded digest, role authority, date and explicit
Story 6.4 authorization, the owner stated on 2026-10-15:

> I Jérôme Piquot Owner and only contributor authorize and approve

This session approval binds normative SHA-256
`78e453a7af3daca5191731c2a6aff5e631db73d6d91993ca1686b6ebd70b8711`
and version 1's complete scope, numeric budgets, W0–W6, preservation and
compatibility vectors, and validation commands. The sole owner holds the
three requested roles for this approval; no second human or tool persona is
invented as an approver.

STORY 6.4 AUTHORIZATION: Jérôme Piquot (`jpiquot`) authorizes Story 6.4 to
implement exactly this approved normative specification, subject to its
preflight, preserved correctness requirements and runtime validation. The
same acceptance closes Story 6.3's named-approval criterion and permits
reconciliation of its build, sprint and planning records. Story 6.4 remains
backlog; implementation has not started. Any normative byte change, reopened
decision or scope drift invalidates this approval and requires re-approval.

This is the AD-13 specification-to-implementation handoff only. Runtime NFR8
acceptance, AD-12 assurance/transition evidence, G-HIGH-RISK, overall readiness,
release, deployment and consumer-migration gates remain separate and unchanged.
This record supplies no CI seal, time-separated assurance attestation or
production qualification.
