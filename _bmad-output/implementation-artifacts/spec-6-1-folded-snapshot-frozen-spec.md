---
title: 'Story 6.1: Folded Snapshot Frozen Spec'
type: 'feature'
created: '2026-09-08'
status: 'draft'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-6-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Automatic snapshots persist `DomainServiceCurrentState` (nested prior snapshot plus tail events), so snapshot cost grows with history. Manual snapshots already store folded state via `/replay-state`. There is no approved contract that picks one bounded behavior, so Story 6.2 cannot start.

**Approach:** Write and approve `_bmad-output/implementation-artifacts/spec-folded-snapshot.md` as the AD-13 gate. It inventories current paths, freezes the target folded payload and byte bound, and records named approval that explicitly authorizes Story 6.2. This story delivers no runtime change.

## Boundaries & Constraints

**Always:** Keep `AggregateActor` the sole snapshot-mutation coordinator and `SaveStateAsync` owner. Keep the event stream as replay authority. Reuse one fold seam (`IAggregateStateReconstructor` / `AggregateReplayer` / `/replay-state`) for automatic and manual writes. Measure folded-state bytes as UTF-8 JSON of unprotected `SnapshotRecord.State` and snapshot size as UTF-8 JSON of the full persisted `SnapshotRecord` under actor `JsonSerializerOptions` (fallback `JsonSerializerOptions.Web`). The bound covers Unprotected and Legacy no-op protection only. Legacy blobs stay retained. Support-safe operator evidence may name sequence, size/bound, age, protection/readability, and failure class — never raw state, events, secrets, or stack traces.

**Never:** Do not change runtime, tests, public contracts, or `sprint-status.yaml` in this story. Do not persist event history, `DomainServiceCurrentState`, nested snapshots, command/result payloads, publication state, or a mutable runtime graph. Do not invent a second fold algorithm. Do not claim Epic 8 encryption, physical erasure, production key custody, or crypto-shred. Do not treat a snapshot as authority for an uncommitted append. Do not self-approve or authorize 6.2 without a named human approver, approval date, content digest, numeric bound, and explicit 6.2 authorization line.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Inventory | Current auto and manual snapshot paths | `spec-folded-snapshot.md` names every producer, reader, overwrite path, key, `SnapshotRecord` field, serializer, protection hook, commit boundary, failure path, operator surface, and test seam, plus `DomainServiceCurrentState` vs folded `/replay-state` divergence | Missing inventory item keeps 6.1 backlog |
| Target payload | Approved automatic snapshot | Folded aggregate state at one exact sequence plus minimal versioned envelope/protection metadata | History-bearing or DSCS payload is rejected |
| Byte bound | Identical folded state, different event counts | Same folded-state bytes; `snapshot size <= folded-state size + MaxSnapshotEnvelopeOverheadBytes` | Bound or serializer/mode omitted keeps 6.1 backlog |
| Sequence | Snapshot around a command that appends events | Covered sequence, tail boundary, key overwrite, actor fence, staging order, and advisory vs fail-closed are unambiguous | Snapshot must not claim missing events, omit events at or below its sequence, or commit outside the actor batch |
| Rehydrate | Folded snapshot plus later events | State and sequence equal canonical full replay for that prefix | Absent/legacy/corrupt/opaque/unreadable/cancelled/infra paths have typed retain-or-bypass rules; partial state is never authoritative |
| Approval | Completion requested | Artifact records scope, digest, numeric bound, invariants, migration, validation matrix, rejected alternatives, empty open decisions, named approver, date, and explicit 6.2 authorization | Missing, stale, self-declared, or conditional approval leaves 6.1 backlog and 6.2 unauthorized |

</frozen-after-approval>

## Open Questions

- Covered sequence for automatic snapshots — options: Post-command (fold through `persistResult.NewSequenceNumber` so the snapshot includes the just-committed events and matches head) / Pre-command (keep today's `preEventSequence` so the snapshot is the pre-command fold and the new events remain tail).
- Legacy `DomainServiceCurrentState` blobs — options: Safe-bypass (treat as non-authoritative, retain, full-replay) / Dual-read unwrap (keep `DomainProcessorStateRehydrator` nested unwrap until 6.2 rewrites the key) / Fail-closed (refuse processing when only a legacy envelope exists).
- `MaxSnapshotEnvelopeOverheadBytes` — options: 4096 (tight; likely enough for current identity + no-op protection JSON) / 16384 (room for a version field and modest metadata growth without claiming Epic 8) / 65536 (loose cost signal).
- Named approver of `spec-folded-snapshot.md` — options: This session's approver is also the named approver once the artifact is complete / A separately named architecture owner must sign after the artifact is written; 6.1 stays incomplete until then.

## Code Map

- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:990-1024` -- command-time load + `DomainServiceCurrentState` build; do not change.
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:1118-1186` -- only automatic producer: stages `currentState` at `preEventSequence` after persist, commits with events via `SaveStateAsync`; advisory; fenced. Do not change.
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:1693-1905` -- manual path: inspect, full replay, `/replay-state` fold, fail-closed commit at `CurrentSequence`. Do not change.
- `src/Hexalith.EventStore.Server/Events/SnapshotManager.cs` and `ISnapshotManager.cs` -- stage/load/inspect; `SetStateAsync` only; delete corrupt plaintext on command-time load; retain protected/opaque/unreadable. Do not change.
- `src/Hexalith.EventStore.Server/Events/SnapshotRecord.cs:20-27` -- persisted envelope (`State` is `object`); key from `AggregateIdentity.SnapshotKey` (`{tenant}:{domain}:{aggId}:snapshot`).
- `src/Hexalith.EventStore.Contracts/Commands/DomainServiceCurrentState.cs:12-16` -- history-bearing rehydration DTO; forbidden snapshot `State`.
- `src/Hexalith.EventStore.Server/Events/EventStreamReader.cs`, `src/Hexalith.EventStore.DomainService/Replay/AggregateReplayer.cs`, `src/Hexalith.EventStore.Server/Events/DaprAggregateStateReconstructor.cs` -- shared read/fold oracle; reuse as the single 6.2 write seam.
- `src/Hexalith.EventStore.Contracts/Security/IEventPayloadProtectionService.cs` and `src/Hexalith.EventStore.Server/Events/NoOpEventPayloadProtectionService.cs` -- current protect/unprotect; Epic 8 engine out of scope.
- `src/Hexalith.EventStore.Admin.UI/Components/Pages/Snapshots.razor` and `Storage.razor` -- policy/age/`HasSnapshot` only; no sequence/size/protection evidence yet. Do not implement UI here.
- Tests to cite, not edit: `AggregateActorDomainResultTests.cs`, `AggregateActorManualSnapshotTests.cs`, `SnapshotManagerTests.cs`, `SnapshotRehydrationTests.cs`, `PayloadSerializationConsistencyTests.cs`.
- Deliverable: `_bmad-output/implementation-artifacts/spec-folded-snapshot.md` (absent). Planning: `_bmad-output/planning-artifacts/epics.md` Story 6.1. Do not treat `spec-6-1-p2-dual-principal-query-envelope-safe-denial.md` as this story.

## Tasks & Acceptance

**Execution:**
- [ ] `_bmad-output/implementation-artifacts/spec-folded-snapshot.md` -- write the complete frozen specification: inventory, payload contract, byte measurements and numeric bound, sequence/atomicity, typed rehydration failures, shared fold seam, protection/lifecycle, compatibility and rejected alternatives, content digest, named approval, and explicit 6.2 authorization -- this is the only Story 6.1 deliverable; 6.2 stays unauthorized until the approval block is complete

**Acceptance Criteria:**
- Given current automatic and manual snapshot paths, when the artifact is written, then it names every producer, reader, overwrite path, key, field, serializer, protection hook, commit boundary, failure path, operator surface, and test seam, and records where `DomainServiceCurrentState`, prior snapshots, tail events, `/replay-state`, and `SnapshotRecord` diverge.
- Given the frozen target automatic snapshot, when its payload is read, then it is folded aggregate state at one exact sequence plus minimal versioned envelope/protection metadata, with no event history, `DomainServiceCurrentState`, nested snapshot, command/result payload, publication state, or mutable runtime graph.
- Given identical folded state under the same schema and serializer, when event counts differ, then folded-state bytes match and every full snapshot satisfies `snapshot size <= folded-state size + MaxSnapshotEnvelopeOverheadBytes` for the named Unprotected/Legacy modes.
- Given Story 6.1 completion is requested, when the artifact is reviewed, then it records accepted scope, content digest, numeric bound, invariants, migration posture, validation matrix, rejected alternatives, no leftover open decisions, named approver, approval date, and explicit 6.2 authorization; otherwise 6.1 stays backlog and 6.2 stays unauthorized.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Design Notes

Automatic writes persist `DomainServiceCurrentState` at `preEventSequence`; manual writes persist `/replay-state` JSON at `CurrentSequence`. EventStore load is schema-agnostic; `DomainProcessorStateRehydrator` still unwraps nested `DomainServiceCurrentState`. The artifact must pick one covered-sequence rule and one legacy policy, then require 6.2 to write the same fold bytes through the reconstructor on both paths. Rejected alternatives already closed by planning: keep embedding `DomainServiceCurrentState`; two fold algorithms; snapshot as append authority; Epic 8 as a 6.2 dependency.

## Verification

**Manual checks (if no CLI):**
- `_bmad-output/implementation-artifacts/spec-folded-snapshot.md` exists and is non-empty.
- It contains inventory, payload, byte bound (`MaxSnapshotEnvelopeOverheadBytes` as a number), sequence, rehydration, shared fold, protection, compatibility, rejected alternatives, content digest, named approver, approval date, and an explicit 6.2 authorization line.
- No runtime, test, contract, or `sprint-status.yaml` diff is part of this story.
