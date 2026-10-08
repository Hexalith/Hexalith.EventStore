---
title: Folded Snapshot Specification
type: architecture-gate
story: "6.1"
status: approved-authorized
story_6_2_authorized: true
created: 2026-09-08
revised: 2026-10-08
baseline_commit: "7d76df4981fb070c4d84d817bf6fc800f27d0adb"
required_by: AD-13
---

# Folded Snapshot Specification

This file is the single AD-13 normative authority required by Story 6.1. It
inventories current snapshot paths, freezes the target folded payload and
byte bound, and records the owner's approval of the exact normative content
digest. Story 6.1 delivers no runtime change. The owner reopened approval on
2026-10-04 (review decision P-D1) and re-attested the revised bytes on
2026-10-08; section 19 authorizes Story 6.2 within the approved boundary.

## 1. Document Control And Digest Rule

| Field | Value |
| --- | --- |
| ADR / gate | AD-13 (cost and evolution changes are spec-first) |
| Architecture constraints | AD-5, AD-6, AD-12, AD-13 |
| Requirements | FR33 folded-snapshot gate; NFR8 bounded snapshot cost planning; NFR12 compatibility planning |
| Required artifact | `_bmad-output/implementation-artifacts/spec-folded-snapshot.md` |
| Story 6.2 authorization | **AUTHORIZED** for the exact normative digest and scope approved in section 19 |
| Numeric bound | `MaxSnapshotEnvelopeOverheadBytes` = **4096** |
| Inspected source baseline | `7d76df4981fb070c4d84d817bf6fc800f27d0adb` (2026-10-08) |
| Open design decisions | **none**; current named-owner approval is recorded in section 19 |

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

Inventory is taken from source at baseline `7d76df4981fb070c4d84d817bf6fc800f27d0adb`.
Story 6.1 MUST NOT mutate these types. Story 6.2 MUST change only the
behaviors this specification replaces.

### 3.1 Key and envelope

| Item | Current contract |
| --- | --- |
| Snapshot key | `AggregateIdentity.SnapshotKey` = `{tenant}:{domain}:{aggId}:snapshot` |
| Event keys | `{tenant}:{domain}:{aggId}:events:{n}` via `EventStreamKeyPrefix` |
| Metadata key | `AggregateIdentity.MetadataKey` = `{tenant}:{domain}:{aggId}:metadata` |
| Persist type | Public positional record `SnapshotRecord` in the published `Hexalith.EventStore.Server` package; not in Contracts, but its constructor/deconstruction are public package API. |
| `SnapshotRecord` fields | `SequenceNumber` (`long`), `State` (`object`), `CreatedAt` (`DateTimeOffset`), `Domain`, `AggregateId`, `TenantId`, `ProtectionMetadata` (`EventStorePayloadProtectionMetadata?`, default `null`) |
| `State` meaning today | Opaque to EventStore. Automatic writes store a `DomainServiceCurrentState`. Manual writes store `/replay-state` `StateJson` deserialized to `JsonElement`. |
| Protection metadata | `null` means pre-Story-22.7a legacy. Callers map `null` through `EventStorePayloadProtectionMetadataCarrier.Legacy()` (`Unprotected` + `CompatibilityFlags["legacy"]="missing"`). |
| Envelope version today | None. `ProtectionMetadata.MetadataVersion` versions protection metadata only. |

### 3.2 Producers

There are exactly two production snapshot producers. Both stage through
`ISnapshotManager.CreateSnapshotAsync` (`SetStateAsync` only) and only the
owning `AggregateActor` calls `SaveStateAsync`.

| Producer | Location | Sequence written today | `State` written today | Commit | Failure posture |
| --- | --- | --- | --- | --- | --- |
| Automatic (sole automatic producer) | `AggregateActor.cs:1392-1412`, after `EventPersister.PersistEventsAsync`, gated by `ShouldCreateSnapshotAsync` | `preEventSequence` = `persistResult.NewSequenceNumber - domainResult.Events.Count` | Pre-command `DomainServiceCurrentState` (`currentState`) | Same actor batch as events, publication-recovery index, `EventsStored` checkpoint, and event-batch witness | Advisory: `CreateSnapshotAsync` swallows non-cancel failures (`throwOnFailure: false`). Command still commits. |
| Manual | `AggregateActor.CreateManualSnapshotAsync` (`AggregateActor.cs:2219-2463`) | Stream `CurrentSequence` from `GetStreamMetadataAsync` | Folded `/replay-state` `StateJson` as `JsonElement` | Dedicated actor batch: stage snapshot then `SaveStateAsync` | Fail-closed (`throwOnFailure: true`). Cancel discards the batch. Save-then-throw accepts only when a fresh durable `SnapshotRecord` matches the staged witness. |

Automatic write prerequisites today: `persistResult.NewSequenceNumber > 0` and
`currentState is not null`. Interval uses four-tier resolution: persisted
`ISnapshotPolicyResolver` policy, then `SnapshotOptions.TenantDomainIntervals`,
then `DomainIntervals`, then `DefaultInterval` (100, minimum 10).
`ShouldCreateSnapshotAsync` compares
`(currentSequence - lastSnapshotSequence) >= interval` using
`persistResult.NewSequenceNumber` and the loaded snapshot's sequence (0 if
none).

Other snapshot-key mutations MUST be distinguished from those producers:

- `SnapshotManager.LoadSnapshotAsync` may delete on its broad non-cancel
  catch path (section 3.5); it stages removal and does not save.
- `AggregateActor.EraseTrustedEffectEvidenceAsync` deletes the snapshot key
  with metadata and trusted-effect evidence (`AggregateActor.cs:2161`), then
  saves and clears the actor cache. This is an existing actor-owned logical
  erasure path, not snapshot production. Story 6.2 MUST preserve it and MUST
  NOT expand its erasure claims.

Admin UI, Admin Server,
Admin CLI, and `AdminStorageCommandController` only invoke
`IAggregateActor.CreateManualSnapshotAsync` to produce a snapshot.
`BenchmarkDatasetBuilder` in the published `Hexalith.EventStore.Testing.Integration`
package additionally constructs `SnapshotRecord`, upserts it through Dapr's
official actor-state transaction API, validates it by readback, and deletes
the key during cleanup. It uses its own `new JsonSerializerOptions()` and
caller-supplied state/protection metadata. Its documented scope is fresh
identities in disposable benchmark storage with no actor method in flight;
it is test infrastructure, not an additional production mutation coordinator
or evidence of the production byte bound. Test fakes also stage snapshots
outside the production producers.

### 3.3 Readers and overwrite paths

| Reader | Location | Behavior today |
| --- | --- | --- |
| Command-time load | `AggregateActor.cs:1255` → `SnapshotManager.LoadSnapshotAsync` | Typed `TryGetStateAsync<SnapshotRecord>` before any envelope-version inspection. Returns an addressed unprotected record or `null`. Address mismatch, absent, opaque, unreadable and unprotect failure retain/bypass. The outer non-cancel catch also attempts deletion for state-store read failures and typed shape failures, not just proven corrupt plaintext; section 9.2 replaces this unsafe classification. |
| Command-time stream read | `EventStreamReader.RehydrateAsync(identity, existingSnapshot)` | Snapshot-aware: tail from `SequenceNumber + 1` through metadata `CurrentSequence`; currently accepts snapshot-only state with missing metadata or snapshot sequence >= current. Optional `DaprProductionLogicalEventReader` is called with `includeDomainView: false`; evolved replay is refused and this path supplies no effective domain events. Missing event keys throw `MissingEventException`; deserialization throws `EventDeserializationException`. Section 9.1 replaces the snapshot-only/future-sequence acceptance. |
| Command-time domain DTO | `AggregateActor` builds `DomainServiceCurrentState(SnapshotState, readableEvents, LastSnapshotSequence, CurrentSequence)` | DSCS is the `/process` payload, not a fold. Domain `DomainProcessorStateRehydrator` folds it via Apply. |
| Manual inspect | `SnapshotManager.InspectSnapshotForManualOverwriteAsync` | Typed `SnapshotLoadOutcome`: `Absent`, `Readable`, `UnreadableProtected`, `ProviderOpaque`, `Corrupt`. Typed read failures currently map to `Corrupt`; addressed-identity mismatch also refuses overwrite. Does **not** delete. Manual write fails closed on unreadable/opaque/corrupt. `AlreadyCurrent` when readable snapshot sequence >= stream current. |
| Manual fold | `MaterializeManualSnapshotStateAsync` | Full `RehydrateAsync(identity)` (no snapshot) or the separate addressed logical-prefix reconstructor path, which requests the domain view, then refuses evolved replay before reconstruction. Unprotect/verify events before `IAggregateStateReconstructor.ReconstructAsync(..., includeTimeline: false)`. Require `Succeeded`, `LastAppliedSequenceNumber == currentSequence`, and non-empty `StateJson`; unsupported evolved routes do not snapshot stored payloads. |
| Domain rehydrator | `DomainProcessorStateRehydrator` | Detects DSCS by JSON object properties `currentSequence` **and** `events`. Recursively unwraps nested DSCS. Also accepts typed `TState`, JSON objects, and event arrays. |
| Admin / storage indexes | `StreamStorageInfo.HasSnapshot`, `SnapshotAge`; `StreamSummary.HasSnapshot` | Presence and optional age only. `DaprStreamActivityTracker` currently writes `HasSnapshot: false` on new stream entries. |
| Admin actor-state inspector | `KnownActorTypes` lists `{actorId}:snapshot`; `DaprInfrastructureQueryService.GetActorInstanceStateAsync` → `ReadActorStateKeyAsync` / `ReadActorStateKeyFromOwnerSidecarAsync` | Local SDK actor-state read or owner-sidecar actor-state GET; returns formatted raw snapshot JSON in `DaprActorStateEntry.Value` and its formatted UTF-8 size. Section 13 requires snapshot-key redaction on both routes. |
| Admin consistency check | `DaprConsistencyCommandService.CheckSnapshotIntegrityAsync` (`:783-807`) | Reads the snapshot key outside the actor via `GetStateAsync<object>` and extracts sequence to detect snapshot-ahead-of-head; exceptions are logged with an exception object. Inventory of existing behavior, not authorization for a new actor-state access route. |
| Manual commit witness | `AggregateActor.SnapshotRecordsMatch` | Explicitly compares sequence, timestamp, identity, protection metadata and actor-serialized state; no snapshot-envelope version exists today, so the current comparison omits it. Section 7.4 requires equality of the new version too before accepting an ambiguous save. |
| Benchmark readback / cleanup | `BenchmarkDatasetBuilder` | Reads typed snapshots via the official actor-state API, compares sequence/identity/state/protection, and removes snapshot keys during disposable-dataset cleanup. |

### 3.4 Serializer and protection hooks

| Seam | Current contract |
| --- | --- |
| Actor persist / witness compare | `AggregateActor.ActorStateJsonSerializerOptions` = `ActorRuntimeOptions.JsonSerializerOptions` if present, else `JsonSerializerOptions.Web`. Manual save-witness compare uses `JsonSerializer.SerializeToUtf8Bytes(State, ActorStateJsonSerializerOptions)`. |
| Domain fold / `/replay-state` `StateJson` | `EventStorePayloadSerialization.Options` (`JsonSerializerDefaults.Web`, read-only). Guarded by `PayloadSerializationConsistencyTests`. |
| Protection write | `IEventPayloadProtectionService.ProtectSnapshotAsync` → `SnapshotProtectionResult(State, Metadata)`. `SnapshotManager` rejects invalid metadata, then stages `SnapshotRecord` with `protectionResult.State`. |
| Protection read | Metadata normalizer maps missing to Legacy and invalid/future metadata to opaque. `NoOpEventPayloadProtectionService.TryUnprotectSnapshotAsync` classifies protected as missing-key, opaque/malformed/future metadata as unreadable, and protected-format bytes with unprotected metadata as mismatch; valid Unprotected/Legacy returns the same object. Interface defaults delegate legacy unprotect, preserving cancellation and mapping non-cancel provider failure to unavailable. |
| Current pdenc-v2 overloads | `IEventPayloadProtectionService` also accepts exact `JsonTypeInfo` and `PayloadProtectionOccurrenceContext` for snapshot protect/unprotect and exposes completion-lease/completion hooks. Legacy/default implementations refuse v2 through typed unsupported outcomes and do not fabricate completion context. `SnapshotManager` currently calls the older typed overloads without those arguments; their presence does not establish an active production v2 snapshot engine. |
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
| Command-time broad load catch | `LoadSnapshotAsync` outer non-cancel catch covers typed read/deserialization and unexpected load failures; logs the exception object, attempts `RemoveStateAsync`, catches/logs non-cancel removal failure, returns `null`. A failed state-store read can therefore delete a valid snapshot today. Section 9.2 permits deletion only for proven unprotected corruption of a known envelope version. |
| Command-time opaque / unreadable | Retain the key. Return `null`. Fall back to full event read. If the event tail is also unreadable, `EnsureEventsReadableForDomainAsync` throws `ProtectedDataUnreadableException` and the command dead-letters. |
| Manual opaque / unreadable / corrupt | `ManualSnapshotOutcome.UnreadableProtected` (or inspect reason). MUST NOT overwrite. |
| Manual reconstruct failure | `InfrastructureFailure` / `StateReconstructionFailed`. MUST NOT write. |
| Manual not found | Stream missing or `CurrentSequence <= 0` → `NotFound`. |
| Domain `/process` cancel | Distinguished from domain or infrastructure failure; pre-commit cancel leaves zero snapshot mutation. |
| Trusted-effect erasure | `EraseTrustedEffectEvidenceAsync` removes events in bounded actor batches, then snapshot/metadata/evidence keys in its final save. Actor ownership is preserved; logical removal supplies no physical-erasure or crypto-shred proof. |

### 3.6 Operator surfaces

| Surface | Current evidence | Gap vs this spec |
| --- | --- | --- |
| `src/Hexalith.EventStore.Admin.UI/Pages/Snapshots.razor` | Policies (tenant, domain, aggregate type, interval, created age) and manual create. Success toast is not persisted-readback of snapshot bytes. | No sequence, size, bound, or protection/readability columns. |
| `src/Hexalith.EventStore.Admin.UI/Pages/Storage.razor` | `HasSnapshot` badge and `SnapshotAge` on hot streams; link to policies. | No sequence, size, bound, or protection/readability. |
| `AdminStorageCommandController` | Invokes actor; writes `SnapshotJob` (operation id, identity, sequence, status, key, safe error code/message) to `admin:storage-snapshot-jobs:{tenant}` and `...:all`. | Job evidence is not snapshot-byte proof. |
| Actor-state inspector (Admin API/UI) | `DaprActorStateEntry.Value` contains raw snapshot JSON; `SizeBytes` measures formatted JSON. | Story 6.2 MUST replace snapshot values with only sequence, canonical size, envelope version and protection/readability class on every inspector read route. Raw event-key redaction is separately deferred to Epic 7. |
| Consistency service | Reads sequence outside the actor; logs snapshot-read exception objects. | Retain support-safe anomaly evidence; no raw state or exception text in snapshot diagnostics. |
| Logs | `SnapshotManager` advisory-create, broad-load and removal catches currently pass exception objects to `LogWarning`; opaque/unreadable logs use safe reason codes. Manual actor failure logs use reason classes. | Story 6.2 MUST replace exception-bearing snapshot failure logs with support-safe failure classes; current behavior can emit provider exception text and stacks. |

### 3.7 Test seams (cite in 6.2; do not edit in 6.1)

| Seam | What it proves today |
| --- | --- |
| `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorDomainResultTests.cs` | Command-time DSCS construction; `ShouldCreateSnapshotAsync` uses `NewSequenceNumber`; automatic `CreateSnapshotAsync` uses **pre-event** sequence; rejection still considers snapshot. |
| `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorManualSnapshotTests.cs` | Manual state is folded `JsonElement`, not `DomainServiceCurrentState`; fail-closed inspect; save-then-throw witness; pre-commit discard. |
| `tests/Hexalith.EventStore.Server.Tests/Events/SnapshotManagerTests.cs` | Interval tiers, stage-only write, overwrite same key, load/delete corrupt, protection metadata normalization. |
| `tests/Hexalith.EventStore.Server.Tests/Events/SnapshotRehydrationTests.cs` | Snapshot-plus-tail vs full replay through `EventStreamReader`. |
| `tests/Hexalith.EventStore.Client.Tests/Serialization/PayloadSerializationConsistencyTests.cs` | Shared `EventStorePayloadSerialization.Options` on Apply / replay readers. |
| Additional related seams | `SnapshotCreationIntegrationTests`, `PayloadProtectionHookTests`, `AggregateActorInfrastructureFailureTests`, Admin snapshot API/page tests. |
| Current additional readers / writers | `SnapshotManagerTests.LoadSnapshot_AddressMismatchRetainsRecordAndReplaysFromStart`; Admin infrastructure/consistency tests; `BenchmarkDatasetBuilderTests` and live-sidecar benchmark tests. Existing snapshot-plus-tail tests compare event slices, not the full folded-state round-trip proof required by V4/V7. |

### 3.8 Shared fold and command DTO (current)

| Type | Role today |
| --- | --- |
| `DomainServiceCurrentState` | Public command DTO: `SnapshotState`, `Events`, `LastSnapshotSequence`, `CurrentSequence`. **Forbidden** as persisted `SnapshotRecord.State` after 6.2. |
| `DomainServiceRequest.CurrentState` | May be typed state, JSON, or DSCS; additive writer-mode/registry/proof/effective-event properties exist but current legacy routes reject versioned assertions. No snapshot-due flag today. |
| `DomainResult` / `DomainServiceWireResult` | Events, optional `ResultPayload`, and optional wire writer-mode/registry echo. No folded-state echo today. `IDomainServiceInvoker` returns `DomainResult`; `DaprDomainServiceInvoker.ToDomainResult` maps the wire events/result. |
| `/process` transport | `DomainServiceRequestRouter`, `DomainProcessorBase`, `DomainServiceWireResult.FromDomainResult`, optional `BoundedV1DomainResultProducer`, `BoundedV1WireResultAdmission`, `BoundedV1WireResultResponse`, and server `BoundedV1DomainResponseParser`. Explicit bounded serializers/parsers currently write/read the existing members; an additive DTO property alone would be omitted or skipped. |
| `IAggregateStateReconstructor` / `DaprAggregateStateReconstructor` | Dapr invoke of `POST /replay-state`. Side-effect free. Requires full prefix starting at sequence 1 today (`AggregateReplayer`). |
| `AggregateReplayer` | Apply discovery shared with `DomainProcessorStateRehydrator`. Serializes `StateJson` with `EventStorePayloadSerialization.Options`. `Partial` state is never authoritative. |
| `DomainProcessorBase` | `/process` rehydrates via the shared Apply table, optionally detaches declared typed snapshots, then `HandleAsync`; caller cancellation is observed around callbacks. Actor never sees `TState`. |

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
| `SnapshotEnvelopeVersion` | **1** on every 6.2 write. Optional init-only `int` property on `SnapshotRecord`, default **0** when absent; existing positional constructor and deconstruction MUST remain unchanged. |

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

Folded snapshot eligibility requires `TState` JSON round-trip fidelity through
the existing `DomainProcessorStateRehydrator.RehydrateFromJsonObject` path:
serialize with `EventStorePayloadSerialization.Options`, persist/unprotect,
restore into `new TState()` through its settable public instance properties,
then Apply a tail. That result MUST equal full-prefix replay. Fields,
get-only state, constructor-only invariants, custom collection comparers, and
custom naming/converter behavior MUST NOT be assumed to survive that path.
A state that cannot meet the parity proofs in V4/V7 is ineligible for folded
snapshot writes; events remain its replay authority. Story 6.2 MUST NOT
silently drop such state or introduce a second restoration algorithm.

Eligibility MUST come from an explicit, immutable, deployment-owned qualified
capture declaration enrolled for the exact aggregate/state schema, shared
Apply table, event admission/serializer profile, state restore/serializer
options, and serving deployment. The declaration MUST identify its reviewed
round-trip/canonical-event parity evidence, detached state/event capture
contract, and finite resource limits (section 8.6). Selected sample fixtures,
reflection discovery, or registration of an arbitrary consumer state do not
qualify it. Tenant/request input MUST NOT create or change a declaration.
Missing, mismatched, stale, or unqualified declaration means conservative
automatic capture skip; manual materialization MUST return bounded
`InfrastructureFailure` / `SnapshotCaptureUnsupported` before reconstructing
or allocating capture state. Enrollment uses a separate additive trusted
registration/capability seam; existing positional APIs and ordinary command
processing/replay remain unchanged. A declaration qualifies only its named
type/profile, never all consumer states.

### 5.3 DSCS detection and write collision

A value is a **DSCS-shaped** payload when, after unprotection, it is a JSON
object that has both properties `currentSequence` and `events`
(ordinal, camelCase — the same detector as
`DomainProcessorStateRehydrator.IsDomainServiceCurrentState`).

Both writers MUST check the unprotected folded JSON object before protection
or staging. If it has this property pair, they MUST skip the version-1 write
and emit only the support-safe reason class `SnapshotStateShapeCollision`.
Automatic skip is advisory and MUST NOT block event commit. Manual skip
MUST return a bounded failure (`InfrastructureFailure` with that reason),
MUST NOT mutate the key, and MUST NOT report `Created` or `AlreadyCurrent`.

DSCS-shaped payloads are **non-authoritative**. They MUST NOT be unwrapped as
a first-class 6.2 snapshot format. The same safe-bypass applies to an existing
version-1 object that violates this rule. Unknown envelope versions are
classified without interpreting `State` (section 9.2).

This write exclusion preserves mixed-version domain services: DSCS carries no
snapshot envelope version, and the existing domain-side detector always runs
on its `SnapshotState`. There is no version-1 detector exemption or new DSCS
wire flag.

## 6. Byte Model And Bound

### 6.1 Constants

`MaxSnapshotEnvelopeOverheadBytes` = **4096**.

### 6.2 Canonical measurements

Let `options` be `ActorRuntimeOptions.JsonSerializerOptions` when configured
on the actor host, otherwise `JsonSerializerOptions.Web`.

The covered writer profile MUST use compact JSON (`WriteIndented == false`),
the standard `SnapshotRecord` object envelope/property layout, and declared
qualified state/envelope serializers. Indented or otherwise unqualified
options MUST NOT be silently replaced with Web options: automatic creation
skips, manual creation returns bounded `SnapshotSerializerUnsupported`, and
neither stages a snapshot. Read compatibility is unaffected.

| Name | Measurement |
| --- | --- |
| Folded-state bytes | UTF-8 byte length of `JsonSerializer.SerializeToUtf8Bytes(unprotectedSnapshotRecord.State, options)` |
| Snapshot size | UTF-8 byte length of `JsonSerializer.SerializeToUtf8Bytes(persistedSnapshotRecord, options)` where `persistedSnapshotRecord` is the complete final record after protection and before staging, including the exact returned protection metadata (Unprotected / Legacy no-op leave `State` equal to the unprotected folded object) |

Story 6.2 tests MUST use those exact calls. A different pretty-print
serializer, BOM, or non-`options` serializer is non-canonical.

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

Both writers MUST admit the measurement allocations under section 8.6 and
measure the exact unprotected folded-state bytes and full final protected
envelope bytes with those same `options` **before** `SetStateAsync`. For
Unprotected/Legacy no-op, unsupported options or
`snapshot size > folded-state bytes + 4096` means automatic advisory skip or
manual bounded `InfrastructureFailure` / `SnapshotEnvelopeBoundExceeded`,
with no snapshot mutation or completion claim. The guard MUST include JSON
escaping of every metadata/identity field; valid protection metadata is not
proof of compliance. It MUST NOT truncate, drop, rewrite or relabel the
provider's metadata/state to make the bound pass. No-op state must preserve
the admitted unprotected fold, whose canonical bytes MUST be frozen before
the protection callback. The exact measured envelope is the one staged;
it MUST NOT be rebuilt with different fields/options after measurement.

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
This does not swallow caller cancellation or a failed `/process` invocation:
they retain the existing pre-commit cancellation/infrastructure behavior.
Optional post-command capture failures inside a successfully processed
request omit the evidence; they do not turn its valid domain events into a
failed command (section 8.3).

### 7.4 Manual batch

Manual overwrite remains fail-closed and single-key. It MUST refuse
unreadable, provider-opaque, and corrupt existing snapshots. `AlreadyCurrent`
MUST apply only to an **authoritative folded** snapshot whose
`SequenceNumber == CurrentSequence` for an existing addressed positive head
(section 9.1). A readable DSCS-shaped blob MUST NOT
be treated as current; it is a migration write (section 10). Success
(`Created` or `AlreadyCurrent`) MUST NOT be reported unless persisted
readback (actor witness today; Admin surfaces in section 13) confirms it.
The manual ambiguous-save witness MUST compare `SnapshotEnvelopeVersion`
as well as every existing envelope field and canonical state bytes. A
durable record differing only in version is not the staged witness and MUST
NOT establish `Created`.

## 8. Shared Fold Seam

### 8.1 One algorithm

There is one fold algorithm: the domain aggregate `Apply` convention already
shared by `DomainProcessorStateRehydrator` and `AggregateReplayer`, invoked
in-process or through `IAggregateStateReconstructor` / `POST /replay-state`.

Story 6.2 MUST NOT add a second independently evolving fold (custom reducer,
JSON merge, DSCS unwrap-as-write, or actor-local state mutation).

### 8.2 Domain-side post-command Apply

Manual reconstruction and command processing MUST share the existing
`ApplyMethodResolver` table, Apply invocation, event deserialization options,
and state serialization. The automatic fold runs in the owning domain
service during its existing `/process` call. It MUST NOT add a seeded
`/replay-state` request, a second sequence-1 replay, an actor-local reducer,
or another event-store read.

For a requested, qualified and resource-admitted snapshot, preserve an
isolated copy of the already-rehydrated pre-command state before a handler
could mutate it; handler mutations are not replay authority. After
`HandleAsync` produces the complete `DomainResult`, its normal selected wire
producer MUST serialize/admit the event/result representation exactly once,
including any declared bounded event serializer. Post-command Apply/capture
MUST run only after that event admission succeeds. The admitted ordered event
metadata and unprotected payload bytes are frozen privately before any
post-command Apply/capture callback;
these exact bytes MUST be sent and passed unchanged to EventPersister before
the existing protection hook. Capture MUST NOT reserialize the live events,
normalize their wire bytes, or change their list/order/result semantics.

Apply inputs MUST be detached event graphs decoded from private copies of
that exact once-admitted representation using the shared replay deserializer
(`EventStorePayloadSerialization.Options`) and Apply table. Apply runs on the
isolated pre-command state with those detached inputs in admitted order.
Neither original returned event graphs/list nor final wire payload buffers
may be reachable through the inputs supplied to capture. An Apply callback
that mutates an input, whether it succeeds or throws, therefore cannot alter
the valid original event list, result payload, admitted metadata, or final
event wire bytes. Snapshot success/failure MUST return the same admitted
events; there is no second serializer/algorithm. Ignored properties, defaults
and custom bounded serialization are interpreted exactly as canonical replay
of the bytes that will be persisted, rather than as live CLR values.

A new aggregate starts from `new TState()` for a non-empty first result.
Persisted rejection events follow the same canonical Apply path;
missing/ambiguous Apply, invalid state, or incomplete fold produces no
snapshot evidence. No-op commands produce none. Qualification is for trusted
callbacks without undeclared outside effects; input isolation does not claim
confinement of arbitrary callback code.

The materialized object MUST satisfy the round-trip and collision rules in
section 5. Both writers MUST omit replay timelines; manual reconstruction
uses `includeTimeline: false`, and automatic capture returns only state JSON.

### 8.3 Request flag, wire evidence, and failure boundary

Story 6.2 MUST add these optional members without changing existing
constructors, deconstruction, or processing signatures:

| Member | Form and default | Meaning |
| --- | --- | --- |
| `DomainServiceRequest.IncludePostCommandState` | Init-only `bool`, default `false` | Actor requests optional folded post-command evidence only for a snapshot-due command. |
| `DomainServiceWireResult.PostCommandStateJson` | Init-only `string?`, default `null`, omitted when null | Folded state after applying the complete returned event list, serialized with `EventStorePayloadSerialization.Options`. |
| `DomainResult.PostCommandStateJson` | Init-only `string?`, default `null` | Carries the same optional evidence through existing processor/invoker result seams; it MUST NOT become `ResultPayload` or change success/rejection/no-op semantics. |

Request-aware invocation/processing MUST be an additive overload or default
interface member delegating to the existing method when unsupported. The
existing `IDomainServiceInvoker`, `IDomainProcessor`, and
`IAsyncDomainProcessor` members MUST remain usable by old implementations;
their fallback result has absent evidence and causes an advisory skip.

Before `/process`, the actor MUST evaluate the resolved snapshot policy at
the admitted pre-command stream head and authoritative `lastSnapshotSequence`
(0 when absent/bypassed), and set the request flag only when that check is
due. After persistence, the existing due check at
`persistResult.NewSequenceNumber` still controls staging. The emitted event
count is not known at request time: a command that first crosses the
interval with its flag false commits events and skips the optional snapshot;
a later non-no-op command requested while due can create it. A no-op MUST
NOT trigger snapshot production even when the request flag was true.
This advisory deferral is explicit; 6.2 MUST NOT infer an event count, send
state on every reply, or make a second domain call to fill absent evidence.
The flag also requires a matching qualified deployment declaration and
admissible serializer/resource profile; due alone does not confer eligibility.

Both due checks MUST receive the originating caller cancellation token.
Non-cancel policy/resolver failure at either check is snapshot-only advisory
failure: use a support-safe `SnapshotPolicyUnavailable` reason, disable
capture/staging for that command, and continue its normal domain invocation
or event commit. Caller cancellation MUST propagate at either check. This
catch applies only to the optional policy lookup; it MUST NOT swallow fence,
domain, persistence or event-read failures.

When requested, qualified and admitted, the domain service MUST serialize the
section 8.2 fold to
`PostCommandStateJson`. After a successful `/process` and persistence, the
actor MUST require non-empty valid JSON-object evidence for that exact
invocation/result, parse it as folded state, and stage it at
`persistResult.NewSequenceNumber`. The state is speculative until that actor
batch commits. It MUST NOT be used to choose or authorize appended events,
be copied as a result/DTO wrapper, or survive a persistence-conflict retry:
retry reconstructs, invokes, and captures again for the new admitted head.
The evidence is request-scoped and MUST NOT enter persisted command results,
pipeline checkpoints, publication state, Admin raw responses, logs, or traces.

Absence (including an old domain service), blank/malformed/non-object state,
shape collision, unsupported processor, Apply/serialization failure, or
optional capture/resource-limit failure MUST omit/ignore snapshot evidence
and skip automatic staging with a support-safe reason class. This also
applies to a new aggregate with missing evidence; there is no actor-side
full-prefix fallback. A failed optional fold MUST NOT suppress or alter the
valid `DomainResult.Events` or `ResultPayload`.

`DomainServiceRequestRouter`, wire conversion, the optional bounded V1
producer/admission/writer, the bounded server response parser, and
`DaprDomainServiceInvoker.ToDomainResult` MUST preserve the flag/evidence
through their explicit paths. Existing wire, memory, depth, and result limits
MUST remain enforced; optional state is omitted when it cannot fit alongside
the admitted event/result response. An init-only DTO property that those
writers omit or parsers skip does not satisfy this contract. False flag
requests MUST neither capture nor transmit post-command state.

Capture uses the same existing `/process` invocation budget, not a second
remote timeout. Optional non-cancel failures are advisory; caller
`OperationCanceledException` propagates through capture/serialization and
pre-commit staging. Cancellation and `/process` timeout retain their existing
command cancellation/infrastructure classification, with no snapshot write
or partial success. Post-commit cancellation preserves committed truth.

### 8.4 Manual fold

Manual writes keep the existing full-prefix `/replay-state` request at
`CurrentSequence`. Unsupported aggregate, partial replay, sequence mismatch,
malformed `StateJson`, or cancellation MUST NOT write and MUST NOT report
completion.

### 8.5 Parity

For the same aggregate, schema, serializer, and event prefix `1..S`,
automatic domain-side post-command fold and manual full-prefix fold MUST produce
semantically identical folded state. Canonical folded-state bytes (section 6)
MUST match.

### 8.6 Capture workspace and representation admission

Each qualified deployment declaration MUST give finite positive limits for
capture workspace, detached pre-command/working state graphs, detached event
graphs/bytes/count, folded-state JSON bytes, escaped optional wire-state
bytes, actor-side parsed-state graph/bytes, canonical measurement/envelope
buffers, node count and depth. It MUST also declare conservative maxima for
serializer growth/scratch and simultaneous live representations. Unknown,
unqualified or excessive limits mean automatic advisory skip or manual
bounded `SnapshotCaptureUnsupported` / `SnapshotCaptureLimitExceeded` with
no snapshot mutation. Limits MUST be checked with overflow-safe arithmetic
against remaining existing response/replay/resource gates, not added as an
unlimited parallel budget.

Before each optional allocation or callback that creates a capture graph,
the library MUST pre-admit/reserve its declared maximum within the finite
workspace budget: pre-command copy, working state, private canonical event
buffers, decoded Apply inputs, state JSON, worst-case escaped wire string,
actor parsed state and exact full-envelope/state measurement buffers. This
includes construction/Apply expansion under the qualified callback contract;
copy-then-check, unbounded serialization followed by a length test, or
parsing before reserving the parsed representation is forbidden. Existing
normal event serialization/admission is reused, never rerun for capture.
The whole optional addition must fit alongside the already-admitted
event/result, and its library-owned outputs/representation growth MUST stay
within their reserved bounds. A failure discards only optional capture
workspace/evidence and preserves the original event/result and final admitted
wire bytes; manual materialization fails closed before staging. Reservations
are released at failure/completion, not treated as reusable while their
representations remain live.

These declarations and checks bound library-owned capture allocations and
qualified representation contracts. They do not promise a universal heap
bound or prevent a trusted constructor, serializer, converter or Apply
callback from allocating undeclared memory internally. Such callbacks require
deployment qualification; an observed violation removes capture eligibility
and skips/fails the snapshot, without weakening normal resource gates or
claiming hostile-code confinement.

## 9. Rehydration And Typed Failures

### 9.1 Happy path

Command-time rehydration MUST:

1. read the addressed authoritative stream metadata/head independently of
   the snapshot, then `LoadSnapshotAsync` (section 9.2 deletion rules);
2. classify version, identity, covered sequence and unprotected `State`
   (section 5.3 / 9.2 / 10) before accepting a folded snapshot;
3. if the snapshot is authoritative and folded, read only the tail after
   `SequenceNumber`;
4. unprotect events (`EnsureEventsReadableForDomainAsync`);
5. build `DomainServiceCurrentState` for `/process` using folded
   `SnapshotState` plus readable tail (DSCS remains the command DTO);
6. let `DomainProcessorStateRehydrator` Apply the tail onto folded state.

The domain-side DSCS detector remains in place. Accepted folded writes never
have its ambiguous property pair (section 5.3); a violating readable record
is bypassed before constructing the command DTO.

Reconstructed domain state and sequence MUST equal canonical full-prefix
Apply of the same readable events `1..CurrentSequence`.

An accepted snapshot MUST have addressed identity, a known version, an
unprotected JSON object without the DSCS collision, and a positive integral
covered sequence `0 < S <= H`, where `H` is the existing addressed positive
stream head. `CurrentSequence` always comes from that metadata, never from
the snapshot. Null, scalar, array, missing/undefined state, or a missing,
malformed, non-positive or future sequence MUST retain/bypass the record
before domain invocation, contribute **0** to `lastSnapshotSequence` and the
command DTO, and require canonical event replay. Manual inspection fails
closed with bounded `SnapshotStateInvalid` / `SnapshotSequenceInvalid`;
neither may report `AlreadyCurrent` or overwrite the invalid record.

Missing stream metadata with an existing snapshot is an orphan: retain it,
invoke no domain logic using it, and fail command reconstruction with bounded
`SnapshotStreamUnavailable`; manual creation returns `NotFound` without
mutation. Only the ordinary admitted new-aggregate path with both metadata
and snapshot absent may supply null state/head 0. Corrupt/non-positive
existing metadata fails closed as stream infrastructure/validation failure;
no snapshot may invent, repair or advance a stream head.

The authoritative `RetainedFloor` and contiguous event-read checks remain in
force. A required prefix/tail below the floor or missing an event fails with
the existing `ReplayRestartRequired` / `MissingEventException` outcome before
domain invocation. Full-prefix manual reconstruction and bypass replay are
unavailable when the retained floor is greater than 1; retain the snapshot,
fail closed, and grant it no substitute authority over the missing prefix.
A validated snapshot may optimize a complete admitted tail starting at
`S+1 >= RetainedFloor`; it never changes the head/floor, synthesizes missing
events, or proves erased history. `AlreadyCurrent` requires equality `S == H`.

### 9.2 Typed outcomes

| Condition | Command-time | Manual overwrite | Snapshot key |
| --- | --- | --- | --- |
| Absent | Full replay from 1 | Reconstruct and create | None |
| Authoritative folded (addressed known-version object, `0 < S <= H`) | Snapshot + complete admitted tail | `AlreadyCurrent` only if `S == H`; else qualified full-prefix reconstruct and overwrite | Retained then overwritten |
| DSCS-shaped with valid address/covered sequence (legacy version 0 or violating version 1) | Non-authoritative; full replay; sequence contribution 0 | Not `AlreadyCurrent` even at `S == H`; qualified reconstruct of folded head; write only when candidate passes section 5.3 | **Retain** until the successful folded overwrite |
| Known-version corrupt plaintext | Delete only after proven unprotected deserialization failure; full replay | Fail closed; do not delete; do not overwrite | Command-time may delete; manual MUST NOT |
| Provider-opaque | Retain at load; return null; full event read; fail closed if events unreadable | Fail closed; do not overwrite | No delete at load; next successful automatic folded write MAY replace |
| Unreadable protected | Same as opaque | Fail closed; do not overwrite | No delete at load; next successful automatic folded write MAY replace |
| Unknown / malformed envelope version | Inspect version only; do not interpret `State`; return null; full replay | Fail closed; do not overwrite | No delete at load; next successful automatic folded write MAY replace |
| Address mismatch | Retain/bypass; full replay | Fail closed; do not overwrite | Retain at load; no authority from mismatched state |
| Known-version null/scalar/array/missing `State` | Retain/bypass before domain invoke; sequence contribution 0; full replay when available | Bounded `SnapshotStateInvalid`; no overwrite or completion | Retain; no shape-based delete |
| Invalid/non-positive/future covered sequence | Retain/bypass; sequence contribution 0; full replay when available | Bounded `SnapshotSequenceInvalid`; no overwrite or completion | Retain |
| Snapshot with missing stream metadata | No snapshot-derived stream; bounded `SnapshotStreamUnavailable` before domain invoke | `NotFound`; no mutation | Retain orphan |
| Required replay prefix/tail unavailable below retained floor or missing events | Existing typed replay failure; no partial domain state | Bounded reconstruction failure; no mutation/completion | Retain; no substitute snapshot authority |
| Unprotect / load infrastructure (non-cancel) | Retain; return null; full replay; subsequent event-read failures use their normal fail-closed path | `InfrastructureFailure`; do not overwrite | No delete |
| `OperationCanceledException` | Propagate; no snapshot mutation after cancel of a staged batch without commit witness | Discard batch; rethrow | Unchanged unless a prior commit witness proves success |
| Optional post-command fold failure / absent evidence / state collision | Advisory snapshot skip; valid command events still commit | No automatic fallback; manual collision returns bounded failure | Unchanged |
| `/replay-state` `Partial` or `Failed` | N/A for automatic capture | Fail closed; do not write | Unchanged |
| Missing / gap event during read | `MissingEventException` / fail closed | Fail closed | Unchanged |

Partial or unknown state MUST NEVER be presented to domain logic or Admin as
authoritative folded state. `AggregateReconstructionStatus.Partial`
`StateJson` is diagnostic only.

Stream availability and version/address/sequence validation take precedence
over DSCS migration. A DSCS-shaped record with an invalid covered sequence
uses the invalid-sequence outcome; its shape does not authorize a manual
overwrite that section 9.1 refuses.

Load and manual inspection MUST obtain the raw JSON envelope through the
owning actor's state manager and inspect `SnapshotEnvelopeVersion` before
typed `SnapshotRecord` deserialization. Missing version means 0; exactly 0
and 1 are known. Negative, greater-than-1, malformed, or ambiguously encoded
versions are unreadable and retained without interpreting `State`. Raw JSON
parse failure that cannot establish a known version and unprotected metadata
also retains the key. A state-store read/transport failure is infrastructure,
not proof of corrupt plaintext. Protected/provider-opaque data MUST NOT be
deleted because a known-version envelope or its state cannot deserialize.

Only a deserialization failure of a known-version, proven unprotected
snapshot permits command-time deletion; manual inspection never deletes.
Shape/identity/sequence rejection is retention/bypass, not a deserialization
failure permitting deletion. These checks MUST precede typed binding/domain
invocation, with post-unprotection state shape checked again when required.
Removal failure returns to full replay without claiming deletion. These
rules replace the current broad load catch in section 3.5.

Retention at load means no delete during that read, not a permanent write
lock. The next successfully requested, folded automatic write MAY replace
an opaque, unreadable, or unknown-version key in the normal event batch;
it MUST emit a support-safe replacement reason class and no retained bytes.
Manual overwrite remains fail-closed for those keys. The event stream is
audit/replay authority; retaining a derived snapshot supplies no additional
erasure or historical-generation guarantee.

## 10. Migration Posture

Named policy: **DSCS safe-bypass**.

1. Detect DSCS shape (section 5.3).
2. Treat as non-authoritative.
3. **Retain** the key (do not delete audit-relevant bytes; do not fail the
   command solely because the snapshot is DSCS). Its non-authoritative
   sequence MUST contribute **0** to `lastSnapshotSequence` and the
   command DTO, so it cannot postpone a due replacement for another interval.
4. Full-replay the readable event prefix.
5. The next successful automatic or manual **folded** write overwrites the
   same key at the approved sequence.

DSCS MUST NOT be unwrapped, re-persisted, or kept as a 6.2 read format.
There is no dual-read wrapper and no background migrator. Mixed history is
the rolling-upgrade state until each aggregate's next successful snapshot.

Pre-6.2 **manual** snapshots (folded `JsonElement` objects, version 0, not DSCS
shape) remain folded reads only when their identity/sequence/stream metadata
satisfy section 9.1.

## 11. Protection, Lifecycle, And Logging

- Plaintext vs protected storage stays on `IEventPayloadProtectionService`.
  MVP Unprotected / Legacy no-op remain valid.
- Unreadable protected, provider-opaque and unknown-version snapshots are
  retained at load; the automatic replacement policy is section 9.2.
- Corrupt **unprotected**, known-version deserialization may delete only on
  the command-time load path. Infra/read failure does not prove corruption.
- Existing `EraseTrustedEffectEvidenceAsync` logically removes the snapshot
  key under actor ownership with its trusted-effect cleanup. This specification
  preserves that separate lifecycle path and grants no new erasure authority.
- When implemented, retention, backup, and restore copy the snapshot key as
  opaque actor state; existing Admin backup/restore engines are deferred no-ops,
  not proof of such copying.
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
| Public / package | Additive only. `SnapshotRecord`, `DomainServiceRequest`, `DomainServiceWireResult`, and `AggregateReconstructionRequest` are public positional records: existing parameters, constructors and deconstruction MUST remain unchanged. New record members MUST be init-only properties with defaults: envelope version 0, request flag false, optional post-command state null (section 8.3). `DomainResult` evidence is also optional/init-only/default null; existing constructor and virtual `ResultPayload` behavior remain unchanged. New interface seams MUST preserve existing members through additive overloads/default delegation. No new replay seed is required or authorized. |
| Schema negotiation | Writers emit `SnapshotEnvelopeVersion=1`. Readers inspect raw version before typed deserialization, treating missing/0 as legacy (DSCS detect or pre-6.2 manual fold). Unknown `< 0` / `> 1` or malformed version is unreadable (retain, do not delete, do not interpret `State`). |
| Rolling upgrade | New readers + old DSCS writers: safe-bypass with interval contribution 0. Old readers + new folded writers: old code places folded `State` into DSCS.`SnapshotState`; the new write collision rule makes the existing JSON-object restore unambiguous. Old domain services/processors/invokers may omit post-command evidence: automatic skip, manual reconstruction unchanged. Old actors default the request flag false and receive no state echo. |
| Downgrade / rollback | After 6.2 writes, a rolled-back writer MAY overwrite with DSCS. New readers then safe-bypass. Unsupported downgrade MUST NOT corrupt the event stream. |
| Legacy writers / readers | DSCS writers are legacy. DSCS is not a 6.2 write. Manual folded version-0 object reads remain valid only under section 9.1's identity/stream/sequence rules. |
| Capture eligibility | Missing/unqualified deployed declaration defaults to automatic skip and bounded unsupported manual materialization; it does not break ordinary command processing or replay. Enrollment is additive and preserves positional constructors/deconstruction. |
| Provider portability | Snapshot remains one actor state key. No provider-specific snapshot format. |
| AOT / trimming | Out of target while Apply reflection remains load-bearing (AD-13). |

Source, binary, wire, and packaged old/new consumer vectors MUST prove these
decisions, including old positional constructors/deconstruction, optional
interface fallbacks, and every explicit bounded parser/writer. A successful
source compile alone is not binary or packaged compatibility proof.

## 13. Operator Evidence (Story 6.2 Surfaces Only)

Story 6.1 implements no UI. Story 6.2 MAY extend Storage & Snapshots with
support-safe:

- covered sequence;
- snapshot size and bound status (`<= folded-state + 4096` or unknown when
  not Unprotected/Legacy);
- age;
- protection/readability class;
- failure class.

Story 6.2 MUST redact the snapshot key in the Admin actor-state inspector
(`KnownActorTypes` → `DaprInfrastructureQueryService`) on both local and
owner-sidecar reads. Its returned snapshot value MUST contain only covered
sequence, canonical full persisted snapshot size, envelope version, and
protection/readability class, or support-safe unknown/unavailable values when
those cannot be established. Neither an authorization role nor diagnostic
intent permits raw `State`, protection key references, or retained snapshot
JSON to cross that surface. Size MUST be measured before redaction with the
actor serializer (section 6), not from formatted/display JSON; if the owning
serializer cannot be established, report unknown. Unknown-version records
MUST NOT have their `State` interpreted to manufacture evidence.

The inspector's raw event-key exposure is an existing, separately deferred
Epic 7 Admin hygiene issue. This snapshot-specific requirement grants no
permission for new event disclosure and does not claim that deferred work is
complete. Existing consistency/benchmark readers remain inventoried; 6.2
does not authorize direct provider access or new external actor-state writers.

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
5. One Apply seam; qualified requested `/process` capture applies only detached
   inputs from the exact once-admitted event representation
   onto the already-rehydrated pre-command state and persists that exact
   post-command fold after append; no second replay/read or seeded remote call.
6. Folded-state bytes are identical for identical state; every staged covered
   snapshot uses qualified compact options and passes the exact pre-stage
   `snapshot size <= folded-state bytes + 4096` guard without metadata changes.
7. DSCS is safe-bypass only: detect, retain, full-replay, overwrite later.
8. Typed failures in section 9.2; partial state is never authoritative.
9. Automatic snapshot failure is advisory; manual is fail-closed.
10. No new Epic 8, erasure, custody, or crypto-shred claims; preserve the
    inventoried actor-owned logical erasure path.
11. Folded state round-trips through the existing JSON property restore;
    DSCS-shaped folded writes are skipped, not exempted from detection.
12. Raw version precedes typed load; infra, unknown-version and protected
    failures do not delete the key. Retention and later replacement are distinct.
13. Explicit deployment qualification and finite pre-admitted capture limits
    precede optional allocations; unknown eligibility/bounds skip/fail snapshot
    production. Trusted callbacks carry no universal heap/confinement guarantee.
14. Capture mutation/throw cannot alter original events or admitted wire bytes.
15. Folded-object shape and `0 < S <= H` acceptance require authoritative
    addressed stream metadata; equality alone permits `AlreadyCurrent`.
16. Policy lookup is advisory except caller cancellation; manual commit witness
    includes envelope-version equality.

## 15. Story 6.2 Validation Matrix

| ID | Scenario | Required proof |
| --- | --- | --- |
| V1 | Preflight | This file exists; digest matches section 19; named approval valid; no open decisions; Story 6.2 explicitly authorized. Tasks cite these sections. |
| V2 | Automatic write | Qualified, resource-admitted requested `/process` returns the post-command fold using shared Apply over detached inputs from the once-admitted event metadata/bytes, including rejection and new-aggregate events. Persisted `State` is folded at `NewSequenceNumber`; structural inspect shows no events collection, DSCS, nested snapshot, command/result, or publication graph. False flag/no-op/missing evidence skip; request-time boundary crossing defers as section 8.3 specifies; no extra read/domain call/serializer. |
| V3 | Manual write | Same shared seam; fail-closed on inspect/reconstruct/cancel; sequence = `CurrentSequence`. |
| V4 | Auto/manual parity | Same canonical persisted prefix → semantically identical state and matching folded-state bytes. Vectors include ignored event properties, defaulted fields and custom bounded serializers whose admitted representation differs from live CLR values; production capture must match replay of that exact representation, with one event serialization/admission. Persist/unprotect/JSON-property restore the fold, Apply a tail, compare all observable state; field/get-only/comparer-loss declarations are ineligible. |
| V5 | Byte bound | Three materially different event-count prefixes converging to the same state have identical bytes and every staged covered envelope `<=` state + 4096. A large state (including 5,000 array elements) under indented options skips/fails without mutation; qualified compact options pass when measured in bound. Eight valid 256-character escaped compatibility values (for example `<`) prove compact metadata can exceed 4096: exact pre-stage measurement skips/fails and preserves returned metadata byte-for-byte without truncation/relabel. Report actual state/full-envelope counts. |
| V6 | Atomicity / capture isolation | Snapshot commits only with its represented event prefix; fence loss/pre-commit fail/retry leaves no future-sequence, DSCS fallback or snapshot-only commit. Apply mutates a detached event and either succeeds or throws: original returned list/graphs, result payload and final admitted event metadata/wire bytes remain unchanged, and committed events match the normal capture-disabled path. |
| V7 | Replay equivalence | Production persist/unprotect/JSON restore + later events equals full-prefix Apply, including byte round-trip fidelity through `RehydrateFromJsonObject` (AD-12), not HTTP status or mock counts. |
| V8 | Legacy DSCS / collision | Detect, retain, full-replay with `lastSnapshotSequence=0`; next eligible successful folded write overwrites. No fail-closed or unwrap-as-authority. Both automatic/manual writers skip the top-level `currentSequence`+`events` collision without mutation or false completion; violating readable v1 records bypass too. |
| V9 | Corrupt plaintext | Raw known-version/proven-unprotected deserialization failure permits command-time delete + replay; manual fails closed without delete. Read/transport/raw-version failures do not masquerade as corruption. |
| V10 | Opaque / unreadable | Retain at load; command-time bypass; manual fail-closed. Successful folded automatic replacement is allowed, with only a support-safe reason class. |
| V11 | Cancel vs infra | Cancel distinguished; optional capture failure omits evidence and preserves valid events; existing `/process` timeout remains command infrastructure failure. Load/unprotect infrastructure retains/bypasses; pre-commit cancel mutates nothing; post-commit cancel preserves committed truth. Snapshot logs emit no exception object/text/stacks. |
| V12 | Compatibility | Section 12 source/binary/wire/package vectors for old/new readers, writers, processors and invokers; original positional constructors/deconstruction still work. Explicit bounded producer/writer/parser carries optional evidence within existing limits. Raw version check protects negative, future, malformed and shape-incompatible unknown-version fixtures before typed binding; downgrade preserves the stream. |
| V13 | Admin evidence | Inspector snapshot value has only sequence, canonical size, envelope version and protection/readability or unknown values on both local/remote routes. No raw snapshot JSON, state, key references, secrets or stacks; optional snapshot surfaces show completion after readback. Existing event-key redaction remains deferred to Epic 7. |
| V14 | Scope lock | No projection optimization, upcasting, or Epic 8 engine. Warnings-as-errors; no unexpected skips. |
| V15 | Qualified admission | Absent, mismatched, stale or unqualified deployment declaration skips automatic capture without changing normal command behavior; manual materialization returns bounded `SnapshotCaptureUnsupported` before reconstruction/capture allocation. Named type/profile qualification never authorizes arbitrary consumer states; positional API vectors remain unchanged. |
| V16 | Capture resource limits | Unknown/excessive/overflowed limits refuse capture before pre-command clone, working graph, event detachment, state JSON, escaped wire, actor parsed state or measurement allocations/callbacks. Reservation/live-representation fixtures cover exact limit and over-limit cases; automatic skip preserves admitted events/bytes, manual fails closed. Existing resource gates remain enforced; no universal callback heap claim. |
| V17 | Snapshot shape / stream authority | Known v0/v1 fixtures with null, bool/string/number, array, missing State; missing/malformed/zero/negative/future sequences; orphan snapshots; invalid/missing metadata and retained-prefix/tail gaps. Prove retain/bypass and zero sequence contribution before domain use, bounded manual refusal, no orphan-derived stream, equality-only `AlreadyCurrent`, and typed unavailable replay when the authoritative prefix/tail is absent. |
| V18 | Policy / witness | Non-cancel resolver failure at pre-invocation and post-persist due checks skips optional capture/staging while valid command processing/commit continues; originating cancellation propagates at both. Ambiguous manual save readback differing only in `SnapshotEnvelopeVersion` refuses `Created`. |

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
| `PreCommandStateJson` plus seeded remote `/replay-state` | Requires a second domain call/timeout inside the open append batch and missing seed protocol. Domain-side requested post-command capture reuses the already-rehydrated state and Apply table in the existing call. |
| Version-1 exemption from the DSCS detector | Envelope version is absent from DSCS.`SnapshotState`; old domain services cannot implement the exemption. Collision writes skip instead. |
| State echo on every `/process` reply | Unnecessary transfer/serialization cost; optional state is returned only when the actor requested snapshot-due evidence. |
| Broad load exception means corrupt plaintext | State-store/transport and unknown-version failures do not prove corrupt unprotected bytes; raw version/protection classification precedes typed deserialization. |
| Append positional parameters to public records | Breaks old compiled constructor/deconstruction callers; new fields are optional init-only properties with defaults. |
| Permanently retain opaque/unreadable snapshot keys against automatic replacement | Snapshots are derived; retaining at load and later actor-batched folded replacement preserves event-stream authority and migration. Manual overwrite still fails closed. |
| Background DSCS rewriter | Extra mutation coordinator; `AggregateActor` must remain the only writer. |
| Fold live CLR events before wire admission | Ignored/defaulted values and custom serializers can diverge from canonical replay; Apply can mutate events before serialization. Use detached inputs from the exact once-admitted representation. |
| Infer arbitrary-state eligibility from sample parity tests | No qualification for unknown consumer schemas/callbacks; explicit deployment-owned declarations default unavailable. |
| Existing result cap alone bounds optional capture allocations | It does not admit clones/working graphs/JSON/escaped wire/parsed state; finite capture workspace must be reserved before those allocations. |
| Compact JSON or valid metadata alone proves the 4096 bound | Escaped valid metadata can exceed it; indented options add state-dependent overhead. Qualified compact options plus exact final-envelope pre-stage measurement are required. |
| Snapshot-only orphan state or sequence above head | Invents stream authority and can hide missing events; accepted state needs addressed metadata and `0 < S <= H`, with `AlreadyCurrent` only at equality. |

## 17. Closed Decisions

Every design choice required to start Story 6.2 is closed in this document.
There are **no** leftover open decisions.

| ID | Decision |
| --- | --- |
| D1 | Target payload is folded state + `SnapshotEnvelopeVersion=1` + existing `SnapshotRecord` identity/protection fields. |
| D2 | `MaxSnapshotEnvelopeOverheadBytes` = 4096; qualified compact actor options, exact protected-envelope/unprotected-state pre-stage measurements and typed skip/failure in section 6; Unprotected and Legacy no-op only. No metadata/state rewriting to pass. |
| D3 | Automatic sequence = `persistResult.NewSequenceNumber`. |
| D4 | DSCS safe-bypass with sequence contribution 0; writers skip the ambiguous property pair, including manual collision with bounded failure. |
| D5 | Shared Apply/deserializer; qualified requested automatic fold uses detached inputs from the exact once-admitted event representation onto preserved pre-command state. Original events and final wire bytes remain unchanged on success/throw; manual uses full-prefix `/replay-state`. No second serializer or seeded replay protocol. |
| D6 | Optional init-only `PostCommandStateJson` from requested `/process` only; defaults/absence/no-op → advisory skip, including old services and new aggregates. Request flag is evaluated at admitted head; a first interval-crossing command without a request defers capture. Explicit bounded transport and result seams preserve the evidence within existing limits. |
| D7 | Automatic advisory vs manual fail-closed postures unchanged. |
| D8 | Epic 8, erasure, custody, crypto-shred out of scope. |
| D9 | Additive init-only/default properties and optional/default interface overloads preserve old signatures/constructor/deconstruction. Raw version precedes typed load; only proven unprotected known-version corruption permits deletion. |
| D10 | Snapshot-key inspector redaction is required by section 13; optional operator fields are support-safe. No UI in 6.1; event-key hygiene stays deferred to Epic 7. |
| D11 | Retain opaque/unreadable/unknown-version keys at load, allow next successful folded automatic replacement with a safe reason class; manual fails closed. |
| D12 | Folded state must round-trip through the existing JSON property restore; V4/V7 prove parity. Existing logical actor erasure and pdenc-v2 hooks are inventoried, not expanded. |
| D13 | Explicit deployment-owned qualified capture declaration for exact state/Apply/serializer profile; unknown eligibility skips automatic and returns bounded unsupported manual materialization, with additive enrollment only. |
| D14 | Finite workspace/representation maxima reserve every optional capture allocation before creation; excessive/unknown bounds skip/fail and preserve existing gates. Trusted callback allocations are not universally confined. |
| D15 | Known-version object state and addressed authoritative head with `0 < S <= H` precede domain use; invalid shape/sequence retain/bypass with sequence 0, orphan metadata fails, unavailable retained prefix/tail fails closed; `AlreadyCurrent` only at equality. |
| D16 | Both due checks pass caller cancellation and treat non-cancel policy failure as advisory skip; the manual save witness requires envelope-version equality. |

## 18. Story 6.2 Authorization Effect

Only a completed, current named-owner approval in section 19 can authorize
Story 6.2 to implement **exactly** sections 2 through 17. The pending record
does not authorize implementation. Implementation tasks and tests MUST cite
the section they satisfy. Drift, a changed digest, or a reopened decision
stops work; Story 6.2 MUST NOT select a local design. Approval of the Story
6.1 intent or historical bytes is not approval of this revised content.

<!-- HX-FS-V1-NORMATIVE-END -->

## 19. Named Owner Approval And Story 6.2 Authorization

| Field | Value |
| --- | --- |
| Named current approver | Jérôme Piquot (`jpiquot`) |
| Approver roles | `architecture_owner` and `eventstore_owner` per `_bmad-output/implementation-artifacts/1-20-github-approval-role-allowlist.json` |
| Current approval date | **2026-10-08** |
| Approval basis | In this session, the named owner answered **yes** to the explicit request to approve this exact revised normative digest and 4096-byte bound, authorize Story 6.2, and reconcile Story 6.1's completion records |
| Normative content SHA-256 | `a4ca9686628b284fb74da931e8cfb1466e80de45fd3d4e89a3c62358a4498ca5` |
| Approved numeric bound | `MaxSnapshotEnvelopeOverheadBytes` = **4096** |
| Open design decisions | **none** |
| Story 6.1 completion | **Done** for the specification gate; named approval acceptance criterion is complete |
| Story 6.2 | **AUTHORIZED**; implementation remains backlog |

STORY 6.2 AUTHORIZATION: Jérôme Piquot (`jpiquot`) explicitly authorizes
Story 6.2 to implement **exactly** sections 2 through 17 under normative
SHA-256 `a4ca9686628b284fb74da931e8cfb1466e80de45fd3d4e89a3c62358a4498ca5`
and `MaxSnapshotEnvelopeOverheadBytes` = **4096**, with the section 18
preflight and validation requirements. Any normative byte change, design
drift, or reopened decision voids this authorization and stops implementation.
The same owner approval authorizes reconciliation of the wrapper, PRD,
epics, sprint tracker, and Epic 6 context for this digest. This records
completion of the specification gate only; Story 6.2 remains backlog and no
runtime implementation or bounded-cost outcome is claimed.

### Superseded historical approval

The prior record named Jérôme Piquot (`jpiquot`), roles `architecture_owner`
and `eventstore_owner`, date 2026-09-08, normative SHA-256
`0b456b5fcc49c6f7431e3476cefd073c184cf181d64bf84fb76414c1110d16b2`, and the
4096-byte bound. Owner review decision P-D1 on 2026-10-04 reopened that
approval because it did not establish attestation of the final normative
bytes. The name/date/digest are retained only as historical provenance;
they MUST NOT substitute for the current session's approval of this revision.
