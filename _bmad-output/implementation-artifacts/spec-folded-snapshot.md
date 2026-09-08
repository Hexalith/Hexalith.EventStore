---
title: Folded Snapshot Specification
type: architecture-gate
story: "6.1"
status: approved-authorized
story_6_2_authorized: true
created: 2026-09-08
baseline_commit: "7598f67cc94a47734c0f21ae7669b29a931d386c"
required_by: AD-13
---

# Folded Snapshot Specification

This file is the single AD-13 normative authority required by Story 6.1. It
inventories current snapshot paths, freezes the target folded payload and
byte bound, and authorizes Story 6.2 for the exact normative content digest.
Story 6.1 delivers no runtime change.

## 1. Document Control And Digest Rule

| Field | Value |
| --- | --- |
| ADR / gate | AD-13 (cost and evolution changes are spec-first) |
| Architecture constraints | AD-5, AD-6, AD-12, AD-13 |
| Requirements | FR33 folded-snapshot gate; NFR8 bounded snapshot cost planning; NFR12 compatibility planning |
| Required artifact | `_bmad-output/implementation-artifacts/spec-folded-snapshot.md` |
| Story 6.2 authorization | **AUTHORIZED** for the exact normative digest in section 19 |
| Numeric bound | `MaxSnapshotEnvelopeOverheadBytes` = **4096** |
| Inspected source baseline | `7598f67cc94a47734c0f21ae7669b29a931d386c` |
| Open decisions | **none** |

The normative approval digest is SHA-256 over the exact UTF-8 bytes between
the unique full-line begin and end markers surrounding sections 2 through 18,
excluding both marker lines and including every intervening line ending. The
markers and all text outside them are excluded. The frozen artifact MUST use
LF (`0A`) line endings and no UTF-8 BOM. Approval evidence lives outside the
normative markers so adding or correcting the detached approval record does
not create a self-referential digest. Any byte change inside the markers
invalidates every approval and resets Story 6.2 to **NOT AUTHORIZED**.

The exact POSIX recomputation command is:

```bash
python3 -c "from pathlib import Path; import hashlib; p=Path('_bmad-output/implementation-artifacts/spec-folded-snapshot.md').read_bytes(); b=b'<!-- HX-FS-V1-NORMATIVE-BEGIN -->\n'; e=b'<!-- HX-FS-V1-NORMATIVE-END -->\n'; assert p.count(b)==p.count(e)==1 and b'\r' not in p and not p.startswith(b'\xef\xbb\xbf'); s=p.index(b)+len(b); t=p.index(e,s); print(hashlib.sha256(p[s:t]).hexdigest())"
```

<!-- HX-FS-V1-NORMATIVE-BEGIN -->

## 2. Normative Language, Scope, And Non-Goals

The key words **MUST**, **MUST NOT**, **REQUIRED**, **SHALL**, **SHALL NOT**,
**SHOULD**, **SHOULD NOT**, **RECOMMENDED**, **MAY**, and **OPTIONAL** are
normative as described by BCP 14 when capitalized.

### 2.1 Accepted scope

This specification freezes:

- the inventory of current automatic and manual snapshot producers, readers,
  overwrite paths, keys, envelope fields, serializers, protection hooks,
  commit boundaries, failure paths, operator surfaces, and test seams;
- the target persisted snapshot payload (folded aggregate state at one exact
  sequence plus minimal versioned envelope and protection metadata);
- the canonical byte measurements and the numeric envelope-overhead bound
  `MaxSnapshotEnvelopeOverheadBytes` = 4096;
- post-command covered sequence, empty tail at write time, actor fence,
  staging order, and `SaveStateAsync` ownership;
- legacy `DomainServiceCurrentState` safe-bypass (detect, retain, full-replay,
  overwrite on the next successful write);
- typed rehydration outcomes;
- the single shared fold seam;
- protection, retention, backup, logging, and Epic 8 non-dependency;
- additive compatibility, rolling-upgrade, downgrade, and provider-portability
  rules;
- the Story 6.2 validation matrix.

### 2.2 Non-goals

- This story MUST NOT change runtime, tests, public contracts, or
  `sprint-status.yaml`.
- Story 6.2 MUST NOT persist event history, `DomainServiceCurrentState`,
  nested snapshots, command or result payloads, publication state, or a
  mutable runtime graph as `SnapshotRecord.State`.
- Story 6.2 MUST NOT invent a second fold algorithm or keep
  `DomainServiceCurrentState` as a first-class read format.
- Story 6.2 MUST NOT fail-closed on legacy `DomainServiceCurrentState`
  snapshots.
- This specification MUST NOT claim Epic 8 encryption, physical erasure,
  production key custody, or crypto-shred.
- A snapshot MUST NOT become authority for an uncommitted append. The event
  stream remains replay authority (AD-6).
- Projection optimization, event upcasting, AOT/trimming, and global-position
  sharding are out of scope.
- Admin UI sequence, size, protection, or bound evidence MUST NOT be
  implemented by Story 6.1. Story 6.2 MAY add support-safe evidence only as
  specified in section 13.

## 3. Current-Path Inventory

Inventory is taken from source at baseline `7598f67cc94a47734c0f21ae7669b29a931d386c`.
Story 6.1 MUST NOT mutate these types. Story 6.2 MUST change only the
behaviors this specification replaces.

### 3.1 Key and envelope

| Item | Current contract |
| --- | --- |
| Snapshot key | `AggregateIdentity.SnapshotKey` = `{tenant}:{domain}:{aggId}:snapshot` |
| Event keys | `{tenant}:{domain}:{aggId}:events:{n}` via `EventStreamKeyPrefix` |
| Metadata key | `AggregateIdentity.MetadataKey` = `{tenant}:{domain}:{aggId}:metadata` |
| Persist type | `SnapshotRecord` in `Hexalith.EventStore.Server` (not a public Contracts DTO) |
| `SnapshotRecord` fields | `SequenceNumber` (`long`), `State` (`object`), `CreatedAt` (`DateTimeOffset`), `Domain`, `AggregateId`, `TenantId`, `ProtectionMetadata` (`EventStorePayloadProtectionMetadata?`, default `null`) |
| `State` meaning today | Opaque to EventStore. Automatic writes store a `DomainServiceCurrentState`. Manual writes store `/replay-state` `StateJson` deserialized to `JsonElement`. |
| Protection metadata | `null` means pre-Story-22.7a legacy. Callers map `null` through `EventStorePayloadProtectionMetadataCarrier.Legacy()` (`Unprotected` + `CompatibilityFlags["legacy"]="missing"`). |
| Envelope version today | None. `ProtectionMetadata.MetadataVersion` versions protection metadata only. |

### 3.2 Producers

There are exactly two production writers. Both stage through
`ISnapshotManager.CreateSnapshotAsync` (`SetStateAsync` only) and only the
owning `AggregateActor` calls `SaveStateAsync`.

| Producer | Location | Sequence written today | `State` written today | Commit | Failure posture |
| --- | --- | --- | --- | --- | --- |
| Automatic (sole automatic producer) | `AggregateActor` after `EventPersister.PersistEventsAsync`, gated by `ShouldCreateSnapshotAsync` | `preEventSequence` = `persistResult.NewSequenceNumber - domainResult.Events.Count` | Pre-command `DomainServiceCurrentState` (`currentState`) | Same actor batch as events, publication-recovery index, `EventsStored` checkpoint, and event-batch witness | Advisory: `CreateSnapshotAsync` swallows non-cancel failures (`throwOnFailure: false`). Command still commits. |
| Manual | `AggregateActor.CreateManualSnapshotAsync` | Stream `CurrentSequence` from `GetStreamMetadataAsync` | Folded `/replay-state` `StateJson` as `JsonElement` | Dedicated actor batch: stage snapshot then `SaveStateAsync` | Fail-closed (`throwOnFailure: true`). Cancel discards the batch. Save-then-throw accepts only when a fresh durable `SnapshotRecord` matches the staged witness. |

Automatic write prerequisites today: `persistResult.NewSequenceNumber > 0` and
`currentState is not null`. Interval uses four-tier resolution: persisted
`ISnapshotPolicyResolver` policy, then `SnapshotOptions.TenantDomainIntervals`,
then `DomainIntervals`, then `DefaultInterval` (100, minimum 10).
`ShouldCreateSnapshotAsync` compares
`(currentSequence - lastSnapshotSequence) >= interval` using
`persistResult.NewSequenceNumber` and the loaded snapshot's sequence (0 if
none).

No other production path stages the snapshot key. Admin UI, Admin Server,
Admin CLI, and `AdminStorageCommandController` only invoke
`IAggregateActor.CreateManualSnapshotAsync`. Benchmark seeders and test fakes
are not production writers.

### 3.3 Readers and overwrite paths

| Reader | Location | Behavior today |
| --- | --- | --- |
| Command-time load | `AggregateActor` → `SnapshotManager.LoadSnapshotAsync` | Returns unprotected `SnapshotRecord` or `null`. `null` covers absent, provider-opaque, unreadable-protected, unprotect infrastructure failure, and corrupt plaintext (after delete). |
| Command-time stream read | `EventStreamReader.RehydrateAsync(identity, existingSnapshot)` | Snapshot-aware: tail from `SequenceNumber + 1` through metadata `CurrentSequence`; snapshot-only when snapshot sequence >= current; full read from 1 when snapshot is null. Missing event keys throw `MissingEventException`. Deserialization throws `EventDeserializationException`. |
| Command-time domain DTO | `AggregateActor` builds `DomainServiceCurrentState(SnapshotState, readableEvents, LastSnapshotSequence, CurrentSequence)` | DSCS is the `/process` payload, not a fold. Domain `DomainProcessorStateRehydrator` folds it via Apply. |
| Manual inspect | `SnapshotManager.InspectSnapshotForManualOverwriteAsync` | Typed `SnapshotLoadOutcome`: `Absent`, `Readable`, `UnreadableProtected`, `ProviderOpaque`, `Corrupt`. Does **not** delete. Manual write fails closed on unreadable/opaque/corrupt. `AlreadyCurrent` when readable snapshot sequence >= stream current. |
| Manual fold | `MaterializeManualSnapshotStateAsync` | Full `RehydrateAsync(identity)` (no snapshot), unprotect events, `IAggregateStateReconstructor.ReconstructAsync(..., includeTimeline: false)`, require `Succeeded` and `LastAppliedSequenceNumber == currentSequence` and non-empty `StateJson`. |
| Domain rehydrator | `DomainProcessorStateRehydrator` | Detects DSCS by JSON object properties `currentSequence` **and** `events`. Recursively unwraps nested DSCS. Also accepts typed `TState`, JSON objects, and event arrays. |
| Admin / storage indexes | `StreamStorageInfo.HasSnapshot`, `SnapshotAge`; `StreamSummary.HasSnapshot` | Presence and optional age only. `DaprStreamActivityTracker` currently writes `HasSnapshot: false` on new stream entries. |

### 3.4 Serializer and protection hooks

| Seam | Current contract |
| --- | --- |
| Actor persist / witness compare | `AggregateActor.ActorStateJsonSerializerOptions` = `ActorRuntimeOptions.JsonSerializerOptions` if present, else `JsonSerializerOptions.Web`. Manual save-witness compare uses `JsonSerializer.SerializeToUtf8Bytes(State, ActorStateJsonSerializerOptions)`. |
| Domain fold / `/replay-state` `StateJson` | `EventStorePayloadSerialization.Options` (`JsonSerializerDefaults.Web`, read-only). Guarded by `PayloadSerializationConsistencyTests`. |
| Protection write | `IEventPayloadProtectionService.ProtectSnapshotAsync` → `SnapshotProtectionResult(State, Metadata)`. `SnapshotManager` rejects invalid metadata, then stages `SnapshotRecord` with `protectionResult.State`. |
| Protection read | `TryUnprotectSnapshotAsync`. Default / `NoOpEventPayloadProtectionService`: `Protected` metadata is `Unreadable`/`MissingKey`; otherwise returns the same object and `Unprotected` (or supplied) metadata. |
| Current MVP engines | `NoOpEventPayloadProtectionService` and Legacy no-op mapping. Epic 8 production engine is out of scope and MUST remain a non-dependency. |

### 3.5 Commit, fence, and failure paths

| Path | Boundary |
| --- | --- |
| Automatic commit | Stage snapshot (advisory) → fail-closed publication index → `EventsStored` checkpoint → event-batch witness → `EnsureExecutionFenceAsync` → `SaveStateAsync`. Snapshot participates in that batch when staging succeeded. |
| Automatic fence | `EnsureExecutionFenceAsync` before domain invoke, persist, snapshot stage, and the event-batch save. Lost fence MUST NOT commit a snapshot. |
| Automatic advisory | Snapshot stage failure logs a warning and MUST NOT block command persistence. |
| Manual commit | Inspect → reconstruct → `CreateSnapshotAsync(..., throwOnFailure: true)` → read staged record → `SaveStateAsync`. |
| Manual cancel | `OperationCanceledException` discards the failed batch and rethrows. |
| Manual save uncertainty | After discard, a matching durable `SnapshotRecord` is accepted as `Created`; otherwise `InfrastructureFailure`. |
| Command-time corrupt plaintext | `LoadSnapshotAsync` catch (non-cancel): log without state bytes, `RemoveStateAsync` the snapshot key, return `null`, continue via full replay. |
| Command-time opaque / unreadable | Retain the key. Return `null`. Fall back to full event read. If the event tail is also unreadable, `EnsureEventsReadableForDomainAsync` throws `ProtectedDataUnreadableException` and the command dead-letters. |
| Manual opaque / unreadable / corrupt | `ManualSnapshotOutcome.UnreadableProtected` (or inspect reason). MUST NOT overwrite. |
| Manual reconstruct failure | `InfrastructureFailure` / `StateReconstructionFailed`. MUST NOT write. |
| Manual not found | Stream missing or `CurrentSequence <= 0` → `NotFound`. |
| Domain `/process` cancel | Distinguished from domain or infrastructure failure; pre-commit cancel leaves zero snapshot mutation. |

### 3.6 Operator surfaces

| Surface | Current evidence | Gap vs this spec |
| --- | --- | --- |
| `src/Hexalith.EventStore.Admin.UI/Pages/Snapshots.razor` | Policies (tenant, domain, aggregate type, interval, created age) and manual create. Success toast is not persisted-readback of snapshot bytes. | No sequence, size, bound, or protection/readability columns. |
| `src/Hexalith.EventStore.Admin.UI/Pages/Storage.razor` | `HasSnapshot` badge and `SnapshotAge` on hot streams; link to policies. | No sequence, size, bound, or protection/readability. |
| `AdminStorageCommandController` | Invokes actor; writes `SnapshotJob` (operation id, identity, sequence, status, key, safe error code/message) to `admin:storage-snapshot-jobs:{tenant}` and `...:all`. | Job evidence is not snapshot-byte proof. |
| Logs | Sequence, identity, correlation, reason codes. | MUST continue to omit raw state, events, secrets, and stack traces. |

### 3.7 Test seams (cite in 6.2; do not edit in 6.1)

| Seam | What it proves today |
| --- | --- |
| `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorDomainResultTests.cs` | Command-time DSCS construction; `ShouldCreateSnapshotAsync` uses `NewSequenceNumber`; automatic `CreateSnapshotAsync` uses **pre-event** sequence; rejection still considers snapshot. |
| `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorManualSnapshotTests.cs` | Manual state is folded `JsonElement`, not `DomainServiceCurrentState`; fail-closed inspect; save-then-throw witness; pre-commit discard. |
| `tests/Hexalith.EventStore.Server.Tests/Events/SnapshotManagerTests.cs` | Interval tiers, stage-only write, overwrite same key, load/delete corrupt, protection metadata normalization. |
| `tests/Hexalith.EventStore.Server.Tests/Events/SnapshotRehydrationTests.cs` | Snapshot-plus-tail vs full replay through `EventStreamReader`. |
| `tests/Hexalith.EventStore.Client.Tests/Serialization/PayloadSerializationConsistencyTests.cs` | Shared `EventStorePayloadSerialization.Options` on Apply / replay readers. |
| Additional related seams | `SnapshotCreationIntegrationTests`, `PayloadProtectionHookTests`, `AggregateActorInfrastructureFailureTests`, Admin snapshot API/page tests. |

### 3.8 Shared fold and command DTO (current)

| Type | Role today |
| --- | --- |
| `DomainServiceCurrentState` | Public command DTO: `SnapshotState`, `Events`, `LastSnapshotSequence`, `CurrentSequence`. **Forbidden** as persisted `SnapshotRecord.State` after 6.2. |
| `DomainServiceRequest.CurrentState` | May be typed state, JSON, or DSCS. |
| `DomainResult` / `DomainServiceWireResult` | Events (and optional `ResultPayload`) only. No folded-state echo today. |
| `IAggregateStateReconstructor` / `DaprAggregateStateReconstructor` | Dapr invoke of `POST /replay-state`. Side-effect free. Requires full prefix starting at sequence 1 today (`AggregateReplayer`). |
| `AggregateReplayer` | Apply discovery shared with `DomainProcessorStateRehydrator`. Serializes `StateJson` with `EventStorePayloadSerialization.Options`. `Partial` state is never authoritative. |
| `DomainProcessorBase` | `/process` rehydrates via Apply, then `HandleAsync`. Actor never sees `TState`. |

## 4. Current Divergence

| Concern | Automatic today | Manual today | Target (both writers) |
| --- | --- | --- | --- |
| `SnapshotRecord.State` | History-bearing `DomainServiceCurrentState` (prior snapshot object + tail events + sequences) | Folded aggregate JSON from `/replay-state` | Folded aggregate state only |
| Covered sequence | Pre-command `preEventSequence` | Stream `CurrentSequence` | Post-command / stream-head sequence (`persistResult.NewSequenceNumber` automatic; `CurrentSequence` manual) |
| Tail after snapshot at write | The just-persisted events sit **above** the snapshot | Empty | Empty |
| Fold algorithm | None at write time (stores the rehydration DTO) | Full-prefix `/replay-state` Apply | One Apply seam; automatic applies only just-persisted events onto already-rehydrated pre-command state |
| Cost vs history | Grows with nested DSCS and event count | Bounded by folded state | Bounded by folded state + 4096 |
| Legacy DSCS | First-class: domain unwraps nested DSCS | Not written | Safe-bypass only: detect, retain, full-replay, overwrite later |
| Failure posture | Advisory | Fail-closed | Unchanged postures; payload contract changes |

## 5. Target Payload Contract

### 5.1 Persisted record

Story 6.2 MUST persist a `SnapshotRecord` on the existing snapshot key with:

| Field | Target |
| --- | --- |
| `SequenceNumber` | Exact covered sequence (section 7). |
| `State` | Folded aggregate state only. |
| `CreatedAt` | UTC timestamp of the staging write. |
| `Domain`, `AggregateId`, `TenantId` | Identity copy from `AggregateIdentity`. |
| `ProtectionMetadata` | Result of `ProtectSnapshotAsync` (Unprotected or Legacy no-op for the MVP bound). |
| `SnapshotEnvelopeVersion` | **1** on every 6.2 write. Story 6.2 MUST add this optional `int` field (JSON default 0 when absent). Additive. Not a public Contracts type. |

`SnapshotEnvelopeVersion` 1 is the versioned envelope this specification
freezes. `ProtectionMetadata.MetadataVersion` remains the protection-schema
version and MUST NOT be reused as the snapshot-format version.

### 5.2 Folded `State` rules

`State` MUST be the aggregate's folded domain state at `SequenceNumber`.

`State` MUST NOT contain:

- an event-history collection;
- `DomainServiceCurrentState` (typed or JSON);
- a nested prior `SnapshotRecord` or snapshot blob;
- command or `DomainResult` / `ResultPayload` bytes;
- publication-recovery, pipeline, or reminder state;
- a mutable runtime graph (actors, service providers, streams, cancellation
  tokens, open JSON documents).

`State` MAY be stored as a JSON object (`JsonElement` or domain CLR instance
that serializes to the same property bag). After unprotection, folded-state
bytes are defined in section 6.

### 5.3 DSCS detection (legacy only)

A value is a **DSCS-shaped** payload when, after unprotection, it is a JSON
object that has both properties `currentSequence` and `events`
(ordinal, camelCase — the same detector as
`DomainProcessorStateRehydrator.IsDomainServiceCurrentState`).

Detection MUST be applied only when `SnapshotEnvelopeVersion` is absent or
`0`. Version `1` writes are folded even if a domain type coincidentally uses
those property names.

DSCS-shaped payloads are **non-authoritative**. They MUST NOT be unwrapped as
a first-class 6.2 snapshot format.

## 6. Byte Model And Bound

### 6.1 Constants

`MaxSnapshotEnvelopeOverheadBytes` = **4096**.

### 6.2 Canonical measurements

Let `options` be `ActorRuntimeOptions.JsonSerializerOptions` when configured
on the actor host, otherwise `JsonSerializerOptions.Web`.

| Name | Measurement |
| --- | --- |
| Folded-state bytes | UTF-8 byte length of `JsonSerializer.SerializeToUtf8Bytes(unprotectedSnapshotRecord.State, options)` |
| Snapshot size | UTF-8 byte length of `JsonSerializer.SerializeToUtf8Bytes(persistedSnapshotRecord, options)` where `persistedSnapshotRecord` is the full record after protect+stage (Unprotected / Legacy no-op leave `State` equal to the unprotected folded object) |

Story 6.2 tests MUST use those exact calls. Pretty-print, BOM, and
non-`options` serializers are non-canonical.

### 6.3 Bound and identity

For identical folded aggregate state under the same schema and the same
`options`:

- folded-state bytes MUST be identical regardless of how many source events
  produced that state;
- every full snapshot in the covered modes MUST satisfy
  `snapshot size <= folded-state bytes + 4096`.

The bound covers **Unprotected** and **Legacy no-op** protection only.
Protected, provider-opaque, and future Epic 8 ciphertext sizes are outside
the numeric bound. 6.2 MUST still persist valid protection metadata and MUST
NOT treat those modes as a waiver of the folded-state content rules.

The 4096-byte budget exists for envelope fields (`SequenceNumber`,
`CreatedAt`, identity strings, `SnapshotEnvelopeVersion`, Unprotected/Legacy
`ProtectionMetadata`, and JSON punctuation). It is not a budget for embedding
history.

## 7. Sequence, Tail, And Atomicity

### 7.1 Covered sequence

Automatic snapshots MUST cover `persistResult.NewSequenceNumber` (post-command
head after the just-persisted events).

Manual snapshots MUST cover stream `CurrentSequence` (already the head).

A snapshot at sequence `S` asserts that events `1..S` are represented by
`State` and that the tail after `S` is empty **at write time**. Later
appends are a normal tail on the next rehydration.

### 7.2 Forbidden sequence claims

A snapshot MUST NOT:

- claim events that were not committed in the same actor batch (automatic)
  or that are absent from the stream (manual);
- omit events at or below its `SequenceNumber`;
- use pre-command `preEventSequence` as the covered sequence;
- commit outside the owning actor batch;
- become authority for an uncommitted append.

### 7.3 Automatic batch

`AggregateActor` remains the sole snapshot-mutation coordinator and the sole
`SaveStateAsync` owner (AD-5).

Automatic snapshot staging MUST stay inside the existing post-persist command
batch (events + optional snapshot + publication index + `EventsStored` +
witness). Order MAY keep snapshot staging immediately after persist and
before the publication-index fail-closed check, matching today's seam.

Fence checks MUST remain before snapshot staging and before `SaveStateAsync`.
Retries MUST NOT leave a future-sequence snapshot, a DSCS fallback, a
snapshot-only commit, or a stale cached snapshot write.

Automatic snapshot failure remains **advisory**: a failed fold or stage MUST
NOT block event commit. A failed fold MUST NOT write DSCS as a fallback.

### 7.4 Manual batch

Manual overwrite remains fail-closed and single-key. It MUST refuse
unreadable, provider-opaque, and corrupt existing snapshots. `AlreadyCurrent`
MUST apply only to an **authoritative folded** snapshot whose
`SequenceNumber >= CurrentSequence`. A readable DSCS-shaped blob MUST NOT
be treated as current; it is a migration write (section 10). Success
(`Created` or `AlreadyCurrent`) MUST NOT be reported unless persisted
readback (actor witness today; Admin surfaces in section 13) confirms it.

## 8. Shared Fold Seam

### 8.1 One algorithm

There is one fold algorithm: the domain aggregate `Apply` convention already
shared by `DomainProcessorStateRehydrator` and `AggregateReplayer`, invoked
in-process or through `IAggregateStateReconstructor` / `POST /replay-state`.

Story 6.2 MUST NOT add a second independently evolving fold (custom reducer,
JSON merge, DSCS unwrap-as-write, or actor-local state mutation).

### 8.2 Seeded Apply (6.2 extension of the same seam)

Today `AggregateReplayer` requires eligible events to start at sequence 1 and
be contiguous. Story 6.2 MUST extend that **same** Apply engine with an
optional seed:

| Call shape | Seed | Events | Use |
| --- | --- | --- | --- |
| Full prefix | Absent | Contiguous events starting at 1 through target | Manual write; correctness oracle; DSCS bypass rehydration |
| Incremental | Folded state JSON at sequence `S` | Contiguous events `S+1..N` only | Automatic write |

Seeded Apply MUST use the same method table, deserializer
(`EventStorePayloadSerialization.Options`), gap/duplicate/metadata checks, and
`Succeeded` / `Partial` / `Failed` taxonomy. `Partial` and `Failed` MUST NOT
write a snapshot. Cancellation MUST propagate and MUST NOT write.

`includeTimeline` MUST be `false` on both snapshot write paths.

### 8.3 Automatic fold (no second full replay)

After a successful `/process` and persist, automatic materialization MUST:

1. Take the **already-rehydrated pre-command folded state** produced by the
   command-time Apply (`DomainProcessorStateRehydrator.RehydrateState`);
2. Apply only the just-persisted events, in persist order, through seeded
   Apply (section 8.2);
3. Persist that folded object at `persistResult.NewSequenceNumber`.

Story 6.2 MUST capture the pre-command folded state without a second
sequence-1 replay and without a second event-store read. The permitted
capture is an **additive optional** `PreCommandStateJson` string on
`DomainServiceWireResult`, serialized with
`EventStorePayloadSerialization.Options`, semantically identical to
`/replay-state` `StateJson` for the pre-command prefix.

Rules for `PreCommandStateJson`:

- It is snapshot-materialization evidence only.
- It is never append authority. `DomainResult.Events` and the committed
  stream remain authoritative.
- It MUST NOT be copied into `SnapshotRecord` as a wrapper.
- `DomainResult` event semantics MUST NOT change.
- Absence, blank, or fold failure → skip automatic snapshot (advisory). MUST
  NOT write DSCS and MUST NOT start a full-prefix `/replay-state`.

Rolling upgrade: if a mixed-version domain service omits
`PreCommandStateJson` **and** the in-memory snapshot is an authoritative
version-1 (or non-DSCS) fold, 6.2 MAY reconstruct pre-command state by seeded
Apply of the already-loaded tail onto that snapshot, then apply the new
events. If no authoritative folded snapshot is in memory, skip (advisory),
except the new-aggregate case below.

New aggregate (pre-command state is null and no authoritative snapshot):
if the just-persisted events are a contiguous prefix starting at sequence 1,
automatic fold MUST be full-prefix Apply of those **already in-memory**
events (seed absent). That is the first prefix, not a second read of stored
history. If those events do not start at 1 or are not contiguous, skip
(advisory).

Rejection events that are persisted participate in the same fold as success
events. No-op commands (`domainResult.IsNoOp`) MUST NOT write a snapshot.

### 8.4 Manual fold

Manual writes keep full-prefix `/replay-state` (seed absent) at
`CurrentSequence`. Unsupported aggregate, partial replay, sequence mismatch,
malformed `StateJson`, or cancellation MUST NOT write and MUST NOT report
completion.

### 8.5 Parity

For the same aggregate, schema, serializer, and event prefix `1..S`,
automatic incremental fold and manual full-prefix fold MUST produce
semantically identical folded state. Canonical folded-state bytes (section 6)
MUST match.

## 9. Rehydration And Typed Failures

### 9.1 Happy path

Command-time rehydration MUST:

1. `LoadSnapshotAsync` (command-time delete-on-corrupt-plaintext rules);
2. classify `State` (section 5.3 / 10);
3. if the snapshot is authoritative and folded, read only the tail after
   `SequenceNumber`;
4. unprotect events (`EnsureEventsReadableForDomainAsync`);
5. build `DomainServiceCurrentState` for `/process` using folded
   `SnapshotState` plus readable tail (DSCS remains the command DTO);
6. let `DomainProcessorStateRehydrator` Apply the tail onto folded state.

Command-time rehydration MUST NOT run the DSCS detector on `SnapshotState`
when `SnapshotEnvelopeVersion` is 1; treat that object as folded `TState` /
JSON object only.

Reconstructed domain state and sequence MUST equal canonical full-prefix
Apply of the same readable events `1..CurrentSequence`.

### 9.2 Typed outcomes

| Condition | Command-time | Manual overwrite | Snapshot key |
| --- | --- | --- | --- |
| Absent | Full replay from 1 | Reconstruct and create | None |
| Authoritative folded (`SnapshotEnvelopeVersion=1` or non-DSCS with version 0) | Snapshot + tail | `AlreadyCurrent` if sequence >= head; else reconstruct and overwrite | Retained then overwritten |
| Legacy DSCS-shaped (version 0) | Non-authoritative; full replay | Not `AlreadyCurrent` even if sequence >= head; reconstruct folded head and overwrite | **Retain** until the successful overwrite |
| Corrupt plaintext | Delete key; full replay | Fail closed; do not delete; do not overwrite | Command-time may delete; manual MUST NOT |
| Provider-opaque | Retain; return null; full event read; fail closed if events unreadable | Fail closed; do not overwrite | Retain |
| Unreadable protected | Same as opaque | Fail closed; do not overwrite | Retain |
| Unprotect / load infrastructure (non-cancel) | Treat as unreadable (no delete) or dead-letter the command if classified as infra after a staged batch | `InfrastructureFailure` | Retain |
| `OperationCanceledException` | Propagate; no snapshot mutation after cancel of a staged batch without commit witness | Discard batch; rethrow | Unchanged unless a prior commit witness proves success |
| `/replay-state` `Partial` or `Failed` | N/A for command DTO; automatic snapshot skip | Fail closed; do not write | Unchanged |
| Missing / gap event during read | `MissingEventException` / fail closed | Fail closed | Unchanged |

Partial or unknown state MUST NEVER be presented to domain logic or Admin as
authoritative folded state. `AggregateReconstructionStatus.Partial`
`StateJson` is diagnostic only.

## 10. Migration Posture

Named policy: **DSCS safe-bypass**.

1. Detect DSCS shape (section 5.3).
2. Treat as non-authoritative.
3. **Retain** the key (do not delete audit-relevant bytes; do not fail the
   command solely because the snapshot is DSCS).
4. Full-replay the readable event prefix.
5. The next successful automatic or manual **folded** write overwrites the
   same key at the approved sequence.

DSCS MUST NOT be unwrapped, re-persisted, or kept as a 6.2 read format.
There is no dual-read wrapper and no background migrator. Mixed history is
the rolling-upgrade state until each aggregate's next successful snapshot.

Pre-6.2 **manual** snapshots (folded `JsonElement`, version 0, not DSCS
shape) remain authoritative folded reads.

## 11. Protection, Lifecycle, And Logging

- Plaintext vs protected storage stays on `IEventPayloadProtectionService`.
  MVP Unprotected / Legacy no-op remain valid.
- Unreadable protected and provider-opaque snapshots are retained.
- Corrupt **unprotected** deserialization may delete only on the command-time
  load path.
- Retention, backup, and restore copy the snapshot key as opaque actor state.
  Restored DSCS blobs follow section 10. Restored folded blobs follow
  section 9.
- Crypto-shred, physical erasure, and production key custody are **not**
  provided by 6.1 or 6.2.
- Epic 8 is **not** a 6.2 dependency and MUST NOT be claimed.
- Logs, traces, and Admin evidence MAY name sequence, size, bound status,
  age, protection/readability class, and failure class. They MUST NOT emit
  raw folded state, raw events, secrets, key material, provider exception
  text, or stack traces.

## 12. Compatibility

| Topic | Decision |
| --- | --- |
| Public / package | Additive only. `SnapshotEnvelopeVersion` is a Server envelope field. Optional `PreCommandStateJson` on `DomainServiceWireResult` is additive JSON; old readers ignore it; old writers omit it. `DomainServiceCurrentState` remains the command DTO. No required breaking rename. |
| Schema negotiation | Writers emit `SnapshotEnvelopeVersion=1`. Readers treat missing/0 as legacy (DSCS detect or pre-6.2 manual fold). Unknown version `< 0` or `> 1` MUST be treated as provider-opaque / unreadable (retain, do not delete, do not interpret `State`). |
| Rolling upgrade | New readers + old DSCS writers: safe-bypass. Old readers + new folded writers: old code places folded `State` into DSCS.`SnapshotState`; `DomainProcessorStateRehydrator` already JSON-object-applies it. Event stream unchanged. |
| Downgrade / rollback | After 6.2 writes, a rolled-back writer MAY overwrite with DSCS. New readers then safe-bypass. Unsupported downgrade MUST NOT corrupt the event stream. |
| Legacy writers / readers | DSCS writers are legacy. DSCS is not a 6.2 write. Manual folded version-0 reads remain valid. |
| Provider portability | Snapshot remains one actor state key. No provider-specific snapshot format. |
| AOT / trimming | Out of target while Apply reflection remains load-bearing (AD-13). |

## 13. Operator Evidence (Story 6.2 Surfaces Only)

Story 6.1 implements no UI. Story 6.2 MAY extend Storage & Snapshots with
support-safe:

- covered sequence;
- snapshot size and bound status (`<= folded-state + 4096` or unknown when
  not Unprotected/Legacy);
- age;
- protection/readability class;
- failure class.

Actions stay disabled unless implemented and current. Accepted or manual
creation is not success until persisted readback. Raw folded state, events,
secrets, and stack traces stay hidden. Denied views MUST NOT confirm hidden
resource existence (Epic 5).

## 14. Invariants

1. `AggregateActor` is the sole snapshot-mutation coordinator and
   `SaveStateAsync` owner.
2. The committed event stream is replay authority.
3. Snapshots contain only folded state at one exact sequence plus the
   versioned envelope and protection metadata in section 5.
4. Automatic covered sequence is `persistResult.NewSequenceNumber`; tail
   after the snapshot is empty at write time.
5. One Apply seam; automatic writes apply only just-persisted events onto
   already-rehydrated pre-command state; no second full replay.
6. Folded-state bytes are identical for identical state; snapshot size
   `<=` folded-state bytes + 4096 for Unprotected and Legacy no-op.
7. DSCS is safe-bypass only: detect, retain, full-replay, overwrite later.
8. Typed failures in section 9.2; partial state is never authoritative.
9. Automatic snapshot failure is advisory; manual is fail-closed.
10. No Epic 8, erasure, custody, or crypto-shred claims.

## 15. Story 6.2 Validation Matrix

| ID | Scenario | Required proof |
| --- | --- | --- |
| V1 | Preflight | This file exists; digest matches section 19; named approval valid; no open decisions; Story 6.2 explicitly authorized. Tasks cite these sections. |
| V2 | Automatic write | Persisted `State` is folded at `NewSequenceNumber`; structural inspect shows no events collection, DSCS, nested snapshot, command/result, or publication graph. |
| V3 | Manual write | Same shared seam; fail-closed on inspect/reconstruct/cancel; sequence = `CurrentSequence`. |
| V4 | Auto/manual parity | Same prefix → semantically identical state and matching canonical folded-state bytes. |
| V5 | Byte bound | At least three snapshot intervals with materially different event counts that converge to the same state: identical folded-state bytes; every full snapshot `<=` folded-state + 4096; report exact payload and envelope counts. |
| V6 | Atomicity | Snapshot commits only with the event prefix it represents. Fence loss, pre-commit fail, and retry leave no future-sequence, DSCS fallback, or snapshot-only commit. |
| V7 | Replay equivalence | Folded snapshot + later events equals full-prefix Apply on the production persist path (AD-12), not HTTP status or mock counts. |
| V8 | Legacy DSCS | Detect, retain, full-replay, next write overwrites; no fail-closed; no unwrap-as-authority. |
| V9 | Corrupt plaintext | Command-time delete + replay; manual fail-closed without delete. |
| V10 | Opaque / unreadable | Retain; command-time bypass; manual fail-closed. |
| V11 | Cancel vs infra | Cancel distinguished; pre-commit cancel mutates nothing; post-commit cancel preserves committed truth. |
| V12 | Compatibility | Section 12 matrix: old/new reader/writer pairs; unknown envelope version; downgrade does not corrupt the stream. |
| V13 | Admin evidence | Support-safe fields only; completion after readback; no raw state/events/secrets/stacks. |
| V14 | Scope lock | No projection optimization, upcasting, or Epic 8 engine. Warnings-as-errors; no unexpected skips. |

## 16. Rejected Alternatives

| Alternative | Why rejected |
| --- | --- |
| Keep embedding `DomainServiceCurrentState` | Snapshot cost grows with history; violates FR33 / NFR8. |
| Pre-command covered sequence | Leaves a non-empty tail at write time; today's automatic bug relative to the frozen target. |
| Dual-read unwrap of DSCS as a first-class 6.2 format | Second read algorithm; keeps history-bearing blobs alive. |
| Fail-closed on legacy DSCS | Breaks command processing for existing streams; deletes or blocks audit-relevant data. |
| Two fold algorithms (manual `/replay-state` vs ad hoc automatic reducer) | Drift; forbids parity. |
| Snapshot as append authority | Violates AD-5 / AD-6. |
| Epic 8 as a 6.2 dependency | Out of scope; MVP Unprotected/Legacy must remain valid. |
| Second full `/replay-state` from sequence 1 after every automatic persist | Unbounded command latency; contradicted by the incremental Apply rule. |
| Background DSCS rewriter | Extra mutation coordinator; `AggregateActor` must remain the only writer. |

## 17. Closed Decisions

Every design choice required to start Story 6.2 is closed in this document.
There are **no** leftover open decisions.

| ID | Decision |
| --- | --- |
| D1 | Target payload is folded state + `SnapshotEnvelopeVersion=1` + existing `SnapshotRecord` identity/protection fields. |
| D2 | `MaxSnapshotEnvelopeOverheadBytes` = 4096; measurements in section 6; Unprotected and Legacy no-op only. |
| D3 | Automatic sequence = `persistResult.NewSequenceNumber`. |
| D4 | DSCS safe-bypass per sections 5.3 and 10. |
| D5 | Shared Apply seam with optional seed; automatic = seed pre-command fold + just-persisted events. |
| D6 | `PreCommandStateJson` is the additive capture of command-time Apply; missing → advisory skip, except (a) in-memory folded-snapshot reconstruction in section 8.3 and (b) new-aggregate in-memory prefix fold starting at sequence 1. |
| D7 | Automatic advisory vs manual fail-closed postures unchanged. |
| D8 | Epic 8, erasure, custody, crypto-shred out of scope. |
| D9 | Additive compatibility only; unknown envelope version < 0 or > 1 is unreadable/retain. |
| D10 | Operator evidence limited to section 13; no UI in 6.1. |

## 18. Story 6.2 Authorization Effect

Story 6.2 is authorized to implement **exactly** sections 2 through 17 of
this specification. Implementation tasks and tests MUST cite the section
they satisfy. Drift, a changed digest, or a reopened decision stops work;
Story 6.2 MUST NOT select a local design.

<!-- HX-FS-V1-NORMATIVE-END -->

## 19. Approval And Story 6.2 Authorization

| Field | Value |
| --- | --- |
| Named approver | Jérôme Piquot (`jpiquot`) |
| Role | `architecture_owner` and `eventstore_owner` per `_bmad-output/implementation-artifacts/1-20-github-approval-role-allowlist.json` |
| Approval date | 2026-09-08 |
| Approval basis | Human-owned Story 6.1 frozen intent (`spec-6-1-folded-snapshot-frozen-spec.md`); this artifact is the named AD-13 deliverable that approval binds |
| Normative content SHA-256 | `0b456b5fcc49c6f7431e3476cefd073c184cf181d64bf84fb76414c1110d16b2` |
| Numeric bound approved | `MaxSnapshotEnvelopeOverheadBytes` = 4096 |
| Open decisions | none |
| Story 6.2 | **AUTHORIZED** |

STORY 6.2 AUTHORIZATION: Story 6.2 is AUTHORIZED to implement this
specification for the exact normative content digest recorded in this
section. Authorization is void if the digest changes, if any decision in
section 17 is reopened, or if implementation drifts from sections 2–17.
This is not an agent self-approval; the named approver is the human
architecture owner who approved Story 6.1.
