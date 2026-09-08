---
title: 'Story 6.1: Folded Snapshot Frozen Spec'
type: 'feature'
created: '2026-09-08'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '7598f67cc94a47734c0f21ae7669b29a931d386c'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-6-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Automatic snapshots persist `DomainServiceCurrentState` (nested prior snapshot plus tail events), so snapshot cost grows with history. Manual snapshots already store folded state via `/replay-state`. There is no approved contract that picks one bounded behavior, so Story 6.2 cannot start.

**Approach:** Write and approve `_bmad-output/implementation-artifacts/spec-folded-snapshot.md` as the AD-13 gate. It inventories current paths, freezes the target folded payload and byte bound, and records named approval that explicitly authorizes Story 6.2. This story delivers no runtime change.

## Boundaries & Constraints

**Always:** Keep `AggregateActor` the sole snapshot-mutation coordinator and `SaveStateAsync` owner. Keep the event stream as replay authority. Reuse one fold seam (`IAggregateStateReconstructor` / `AggregateReplayer` / `/replay-state`) for automatic and manual writes. Automatic snapshots cover the post-command sequence (`persistResult.NewSequenceNumber`); 6.2 folds by applying the just-persisted events onto the already-rehydrated pre-command state (same `Apply` path), not a second full replay. Legacy `DomainServiceCurrentState` blobs are safe-bypass: detect the `currentSequence`+`events` shape, treat as non-authoritative, retain, full-replay; the next successful write overwrites the key. `MaxSnapshotEnvelopeOverheadBytes` is 4096. Measure folded-state bytes as UTF-8 JSON of unprotected `SnapshotRecord.State` and snapshot size as UTF-8 JSON of the full persisted `SnapshotRecord` under actor `JsonSerializerOptions` (fallback `JsonSerializerOptions.Web`). The bound covers Unprotected and Legacy no-op protection only. Support-safe operator evidence may name sequence, size/bound, age, protection/readability, and failure class — never raw state, events, secrets, or stack traces. The named approver of `spec-folded-snapshot.md` is the human who approves this Story 6.1 spec; name, date, and explicit 6.2 authorization are written onto that artifact when it is complete.

**Never:** Do not change runtime, tests, public contracts, or `sprint-status.yaml` in this story. Do not persist event history, `DomainServiceCurrentState`, nested snapshots, command/result payloads, publication state, or a mutable runtime graph. Do not invent a second fold algorithm or keep DSCS as a first-class 6.2 read format. Do not fail-closed on legacy snapshots. Do not claim Epic 8 encryption, physical erasure, production key custody, or crypto-shred. Do not treat a snapshot as authority for an uncommitted append. Do not self-approve or authorize 6.2 without a named human approver, approval date, content digest, numeric bound, and explicit 6.2 authorization line.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Inventory | Current auto and manual snapshot paths | `spec-folded-snapshot.md` names every producer, reader, overwrite path, key, `SnapshotRecord` field, serializer, protection hook, commit boundary, failure path, operator surface, and test seam, plus `DomainServiceCurrentState` vs folded `/replay-state` divergence | Missing inventory item keeps 6.1 backlog |
| Target payload | Approved automatic snapshot | Folded aggregate state at one exact sequence plus minimal versioned envelope/protection metadata | History-bearing or DSCS payload is rejected |
| Byte bound | Identical folded state, different event counts | Same folded-state bytes; `snapshot size <= folded-state size + 4096` for Unprotected/Legacy | Bound or serializer/mode omitted keeps 6.1 backlog |
| Sequence | Snapshot around a command that appends events | Covered sequence is `persistResult.NewSequenceNumber`; tail after the snapshot is empty at write time; same-batch fence, staging, and advisory vs fail-closed stay unambiguous | Snapshot must not claim missing events, omit events at or below its sequence, or commit outside the actor batch |
| Rehydrate | Folded snapshot plus later events | State and sequence equal canonical full replay for that prefix | Absent, corrupt plaintext (delete), opaque/unreadable/cancelled/infra, and legacy DSCS (retain, bypass, full-replay) are typed; partial state is never authoritative |
| Approval | Completion requested | Artifact records scope, digest, numeric bound, invariants, migration, validation matrix, rejected alternatives, empty open decisions, named approver, date, and explicit 6.2 authorization | Missing, stale, self-declared, or conditional approval leaves 6.1 backlog and 6.2 unauthorized |

</frozen-after-approval>

## Code Map

- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:990-1024` -- command-time load + `DomainServiceCurrentState` build; do not change.
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:1118-1186` -- only automatic producer: stages `currentState` at `preEventSequence` after persist, commits with events via `SaveStateAsync`; advisory; fenced. Do not change.
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:1693-1905` -- manual path: inspect, full replay, `/replay-state` fold, fail-closed commit at `CurrentSequence`. Do not change.
- `src/Hexalith.EventStore.Server/Events/SnapshotManager.cs` and `ISnapshotManager.cs` -- stage/load/inspect; `SetStateAsync` only; delete corrupt plaintext on command-time load; retain protected/opaque/unreadable. Do not change.
- `src/Hexalith.EventStore.Server/Events/SnapshotRecord.cs:20-27` -- persisted envelope (`State` is `object`); key from `AggregateIdentity.SnapshotKey` (`{tenant}:{domain}:{aggId}:snapshot`).
- `src/Hexalith.EventStore.Contracts/Commands/DomainServiceCurrentState.cs:12-16` -- history-bearing rehydration DTO; forbidden snapshot `State`.
- `src/Hexalith.EventStore.Server/Events/EventStreamReader.cs`, `src/Hexalith.EventStore.Client/Aggregates/AggregateReplayer.cs`, `src/Hexalith.EventStore.Server/Events/DaprAggregateStateReconstructor.cs` -- shared read/fold oracle; reuse as the single 6.2 write seam.
- `src/Hexalith.EventStore.Contracts/Security/IEventPayloadProtectionService.cs` and `src/Hexalith.EventStore.Server/Events/NoOpEventPayloadProtectionService.cs` -- current protect/unprotect; Epic 8 engine out of scope.
- `src/Hexalith.EventStore.Admin.UI/Pages/Snapshots.razor` and `Storage.razor` -- policy/age/`HasSnapshot` only; no sequence/size/protection evidence yet. Do not implement UI here.
- Tests to cite, not edit: `AggregateActorDomainResultTests.cs`, `AggregateActorManualSnapshotTests.cs`, `SnapshotManagerTests.cs`, `SnapshotRehydrationTests.cs`, `PayloadSerializationConsistencyTests.cs`.
- Deliverable: `_bmad-output/implementation-artifacts/spec-folded-snapshot.md`. Planning: `_bmad-output/planning-artifacts/epics.md` Story 6.1. Do not treat `spec-6-1-p2-dual-principal-query-envelope-safe-denial.md` as this story.

## Tasks & Acceptance

**Execution:**
- [x] `_bmad-output/implementation-artifacts/spec-folded-snapshot.md` -- write the complete frozen specification: inventory, folded payload, `MaxSnapshotEnvelopeOverheadBytes` 4096, post-command sequence, DSCS safe-bypass, typed rehydration failures, shared fold seam, protection/lifecycle, compatibility and rejected alternatives, content digest, this session's approver as named approval, and explicit 6.2 authorization -- this is the only Story 6.1 deliverable; 6.2 stays unauthorized until the approval block is complete

**Acceptance Criteria:**
- Given current automatic and manual snapshot paths, when the artifact is written, then it names every producer, reader, overwrite path, key, field, serializer, protection hook, commit boundary, failure path, operator surface, and test seam, and records where `DomainServiceCurrentState`, prior snapshots, tail events, `/replay-state`, and `SnapshotRecord` diverge.
- Given the frozen target automatic snapshot, when its payload is read, then it is folded aggregate state at one exact sequence plus minimal versioned envelope/protection metadata, with no event history, `DomainServiceCurrentState`, nested snapshot, command/result payload, publication state, or mutable runtime graph.
- Given identical folded state under the same schema and serializer, when event counts differ, then folded-state bytes match and every full snapshot satisfies `snapshot size <= folded-state size + 4096` for Unprotected and Legacy no-op modes.
- Given Story 6.1 completion is requested, when the artifact is reviewed, then it records accepted scope, content digest, numeric bound, invariants, migration posture, validation matrix, rejected alternatives, no leftover open decisions, named approver, approval date, and explicit 6.2 authorization; otherwise 6.1 stays backlog and 6.2 stays unauthorized.

## Implementation Notes

- 2026-09-08: Created `_bmad-output/implementation-artifacts/spec-folded-snapshot.md`. Named approver Jérôme Piquot (`jpiquot`), date 2026-09-08, explicit Story 6.2 authorization. No runtime, test, or public-contract edits.
- 2026-09-08: Review patches: v1 `SnapshotState` MUST NOT run the DSCS detector; envelope version `< 0` is unreadable/retain. Normative SHA-256 now `0b456b5fcc49c6f7431e3476cefd073c184cf181d64bf84fb76414c1110d16b2`.
- 2026-09-08: `spec-6-1-folded-snapshot-frozen-spec.md` was externally reverted to the pre-approval draft (Open Questions restored, `status: draft`). Restored the approved frozen block, `baseline_commit`, and `in-progress` status. Code Map paths corrected to `AggregateReplayer.cs` under Client and `Pages/Snapshots.razor`.

## Spec Change Log

## Review Triage Log

| ID | Verdict and evidence | Route |
|---|---|---|
| BH-1 | `false` — The Never clause bounds this story's deliverable. Admin UI, OQ8, evidence, and submodule hunks in the baseline-wide diff are other work on the same tree. The `sprint-status.yaml` 6.1/`epic-6` bump is the bmad-build tracker, not the AD-13 artifact. | reject |
| BH-2 | `false` — `spec-6-1` `in-review` is this step; `sprint-status` `in-progress` is the tracker; `spec-folded-snapshot.md` `approved-authorized` is the named AD-13 deliverable. Different records, not one broken lifecycle. | reject |
| BH-3 | `false` — Frozen intent names this session's approver. The artifact records Jérôme Piquot (`jpiquot`) on 2026-09-08 with an explicit 6.2 line. A detached second receipt was rejected at planning. | reject |
| BH-4 | `false` — `DomainServiceWireResult` today has no `PreCommandStateJson` (`DomainServiceWireResult.cs:14-35`; `DomainProcessorBase.cs:20-26`). Section 8.3 / D6 authorizes 6.2 to add that additive field. Missing plumbing is 6.2 work, not a 6.1 hole. | reject |
| BH-5 | `medium` — Section 5.3 says v1 `State` stays folded even if it has `currentSequence`+`events`, but §9.1 still puts it in `DomainServiceCurrentState.SnapshotState`, and `DomainProcessorStateRehydrator.cs:56-57` still unwraps that detector. A coincidental v1 property bag would be treated as nested DSCS on `/process`. | patch |
| BH-6 | `low` — `EventStorePayloadSerialization.Options` is read-only `JsonSerializerDefaults.Web` (`EventStorePayloadSerialization.cs:36-43`); actor fallback is `JsonSerializerOptions.Web`. Everyday measurements match. Custom actor options would be a 6.2 host concern. | reject |
| BH-7 | `low` — Identity strings are ULID-sized in this codebase. Capping them would add 6.2 rules nobody hits. | reject |
| BH-8a | `false` — Section 3.2 already says benchmark seeders and test fakes are not production writers. `BenchmarkDatasetBuilder` is that class. | reject |
| BH-8b | `low` — Backup/restore APIs are deferred no-ops (`DaprBackupCommandService.cs:79-81`). §11's "copy the key" is future-engine language, not a live path. | reject |
| BH-8c | `low` — Extra `HasSnapshot` readers do not change the 6.2 write contract. | reject |
| BH-9 | `low` — The normative body already requires new-aggregate fold, no-op skip, rejection events, and mixed-version skip. The matrix is a 6.2 checklist, not a second authority. | reject |
| BH-10 | `low` — The `\\n` in the wrapper Verification span is a copy-paste footgun. Fixing it only edits this build spec, which review rejects. The fenced command in `spec-folded-snapshot.md` is correct and was run. | reject |
| BH-11 | `medium` (unverified as this story) — Snapshots/Backups 401/403 confirm close is 5.4 UI work in the same dirty tree, not this deliverable. | defer |
| BH-12 | `medium` (unverified as this story) — OQ8 remint/DW-496 notes belong to Story 4.15, not 6.1. | defer |
| BH-13 | `low` (unverified as this story) — 5.4/`sprint-status` and 4.7 ledger drift are neighboring stories. | defer |
| BH-14 | `low` (unverified as this story) — `CreateSymbolicLinkOrSkip` is OQ8 test harness, not 6.1. | defer |
| EC-1 | `medium` (unverified as this story) — OQ8 `CreateFixture` reparse skip is 4.15 test code. | defer |
| EC-2 | `low` (unverified as this story) — OQ8 `Process.Kill` swallow is 4.15 harness. | defer |
| EC-3 | `medium` (unverified as this story) — Backups `InvalidOperationException` dialog close is 5.4. | defer |
| EC-4 | `medium` (unverified as this story) — Snapshots create-policy `InvalidOperationException` is 5.4. | defer |
| EC-5 | `medium` (unverified as this story) — Snapshots edit-policy `InvalidOperationException` is 5.4. | defer |
| EC-6 | `medium` (unverified as this story) — Snapshots create-snapshot `InvalidOperationException` is 5.4. | defer |
| EC-7 | `low` (unverified as this story) — Create-policy success focus restore is 5.4. | defer |
| EC-8 | `low` (unverified as this story) — Edit-policy success focus restore is 5.4. | defer |
| EC-9 | `medium` — Section 12 classifies missing/0 as legacy and `> 1` as unreadable. `SnapshotEnvelopeVersion < 0` has no read rule, so a corrupt negative version could be treated as 0 and DSCS-detected. | patch |
| EC-10 | `false` — "No runtime change" is this story's intent. UI/OQ8 hunks are other dirty-tree work, not 6.1 files. | reject |
| EC-11 | `false` — Same as EC-10: extra artifacts in a baseline-wide diff are not this story's deliverable. | reject |
| VG-1 | `medium` (pre-verified; not this story) — Create Backup 401/403 confirm has no test. Surface is `Backups.razor`, Story 5.4. | defer |
| VG-2 | `medium` (pre-verified; not this story) — Snapshots 401/403 confirm has no test. Surface is `Snapshots.razor`, Story 5.4. | defer |

## Design Notes

Today automatic writes persist `DomainServiceCurrentState` at `preEventSequence`; manual writes persist `/replay-state` JSON at `CurrentSequence`. The frozen target is post-command folded state at `NewSequenceNumber` on both paths, with legacy DSCS safe-bypassed (retain, full-replay). Rejected alternatives: keep embedding `DomainServiceCurrentState`; pre-command covered sequence; dual-read unwrap; fail-closed on legacy; two fold algorithms; snapshot as append authority; Epic 8 as a 6.2 dependency.

## Verification

**Commands:**
- `python3 -c "from pathlib import Path; import hashlib; p=Path('_bmad-output/implementation-artifacts/spec-folded-snapshot.md').read_bytes(); b=b'<!-- HX-FS-V1-NORMATIVE-BEGIN -->\\n'; e=b'<!-- HX-FS-V1-NORMATIVE-END -->\\n'; assert p.count(b)==p.count(e)==1 and b'\\r' not in p and not p.startswith(b'\\xef\\xbb\\xbf'); s=p.index(b)+len(b); t=p.index(e,s); print(hashlib.sha256(p[s:t]).hexdigest())"` -- expected: `0b456b5fcc49c6f7431e3476cefd073c184cf181d64bf84fb76414c1110d16b2`

**Manual checks (if no CLI):**
- `_bmad-output/implementation-artifacts/spec-folded-snapshot.md` exists and is non-empty.
- It contains inventory, payload, byte bound (`MaxSnapshotEnvelopeOverheadBytes` = 4096), sequence, rehydration, shared fold, protection, compatibility, rejected alternatives, content digest, named approver, approval date, and an explicit 6.2 authorization line.
- No runtime, test, or public-contract diff is part of this story.
