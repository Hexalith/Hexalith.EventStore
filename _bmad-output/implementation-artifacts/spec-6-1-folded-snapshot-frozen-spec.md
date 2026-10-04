---
title: 'Story 6.1: Folded Snapshot Frozen Spec'
type: 'feature'
created: '2026-09-08'
status: 'in-progress'
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

### Review Findings

Code review 2026-10-04 of `f4d7b91b` only (the 3 unrelated commits in `7598f67c..f4d7b91b` excluded). Layers: Blind Hunter, Edge Case Hunter, Verification Gap, Acceptance Auditor; none failed. Claims verified against baseline `7598f67c`. Every normative patch or decision below changes the HX-FS-V1 digest `0b456b5f…`, which voids the Story 6.2 authorization (§1) and the digest pinned in `prd.md` NFR8 until re-approved.

- [x] [Review][Decision] Approval provenance does not bind the final normative bytes — **resolved 2026-10-04: reopen and re-approve (option 1) → P-D1.** §1 says any byte change inside the markers invalidates every approval. Implementation Note 2 records review patches (BH-5, EC-9) that re-stamped the digest to `0b456b5f…` under the same approver/date. The frozen block was rewritten with the four Open Question answers (Spec Change Log empty), and was "restored" after an external revert with no committed approved copy to compare. §19's basis is approval of the 6.1 intent, not of these bytes; epics "stale/self-declared" rule and 6.2 V1 ("named approval valid") cannot be satisfied from the record. Since approval, HEAD drifted: `f54d7ea5` (2026-09-26) added trusted-effect erasure that deletes the snapshot key (`AggregateActor.cs:2141`), contradicting §3.2 "no other production path" and §11 erasure non-claims; `IEventPayloadProtectionService` gained pdenc-v2 snapshot overloads. Options: re-baseline + patch + fresh owner attestation of the new digest; or attest the current digest as-is.
- [x] [Review][Decision] §9.1 v1 DSCS-detector exemption cannot be implemented — **resolved 2026-10-04: skip the write on collision → P-D2.** the detector runs domain-side (`DomainProcessorStateRehydrator.IsDomainServiceCurrentState`, Client, private) on `DomainServiceCurrentState.SnapshotState`, which carries no envelope version; old domain services always unwrap. A folded state with top-level `currentSequence`+`events` is misread as nested DSCS. Options: additive version/flag on the DSCS DTO; an aggregate-state authoring constraint forbidding that property pair; or both. [spec-folded-snapshot.md:467]
- [x] [Review][Decision] Automatic-fold domain-service boundary is unspecified — **resolved 2026-10-04: domain returns post-command state → P-D3.** the actor never holds `TState` (§3.8), so seeded Apply (§8.2) is a second remote `/replay-state` call inside the open command batch. `AggregateReconstructionRequest` has no seed field; no timeout budget; "cancellation MUST propagate" (§8.2) conflicts with "failed fold MUST NOT block event commit" (§7.3) because a fold timeout surfaces as `TaskCanceledException`; `PreCommandStateJson` would ride on every `/process` reply with no cost bound or logging rule; JSON-null (new aggregate) vs absent (old service) is indistinguishable; whether seeded Apply consumes unprotected in-memory events is unstated. §16 never weighs "domain returns post-command folded state from `/process`". Violates epics 6.1 ("domain-service boundary, cancellation behavior, and error taxonomy"; "no unresolved … replay decision may be deferred into Story 6.2") and §18. [spec-folded-snapshot.md:376-437]
- [x] [Review][Decision] Automatic writer overwrites snapshots §9.2 says to retain — **resolved 2026-10-04: allow, stated explicitly → P-D4.** opaque/unreadable (and unknown-version) loads return `null`, so `lastSnapshotSequence` is 0 (`AggregateActor.cs:1002` at baseline) and the next interval command stages over the key (`:1119-1129`). §9.2 rows say "Retain"; only the manual path is told not to overwrite. Options: allow (snapshot is derived; events stay authority) and say so; or skip automatic writes while a retained unreadable key exists. [spec-folded-snapshot.md:482-483]
- [x] [Review][Decision] Inventory omits Admin raw-snapshot readers — **resolved 2026-10-04: 6.2 redacts the snapshot key; event-key redaction deferred to Epic 7 → P-D5.** the Admin actor-state inspector (`KnownActorTypes.cs:30` `{actorId}:snapshot` → `DaprInfrastructureQueryService.ReadActorStateKeyAsync`) returns the raw snapshot JSON and size to Admin, contradicting §11/§13 "raw folded state stays hidden"; `DaprConsistencyCommandService.cs:783` reads the snapshot key outside the actor; `BenchmarkDatasetBuilder` (published Testing.Integration) writes `SnapshotRecord`. AC1 requires every reader/operator surface. Options: 6.2 must redact the inspector's snapshot value; or record it as an accepted diagnostic exception owned elsewhere (Epic 7). [spec-folded-snapshot.md:181-188]
- [ ] [Review][Patch] P-D1 Reopen: re-baseline §1/§3 to current HEAD (trusted-effect erasure deleting the snapshot key, pdenc-v2 snapshot overloads, `AggregateActor` drift), set §19 to pending owner re-attestation with Story 6.2 **NOT AUTHORIZED**, and recompute the digest. After the owner attests the new digest, update the `prd.md` NFR8 digest pin and the `epics.md` 6.1 reconciliation. [spec-folded-snapshot.md:19-47,98-210,637-655]
- [ ] [Review][Patch] P-D2 Writers MUST skip (advisory) a v1 snapshot whose folded JSON object has both top-level `currentSequence` and `events`; remove the unimplementable §9.1 exemption and align §5.3. [spec-folded-snapshot.md:261-273,467-469]
- [ ] [Review][Patch] P-D3 Replace `PreCommandStateJson` + seeded `/replay-state` with an optional init-only `PostCommandStateJson` that `/process` returns only when the actor's request flag marks the command snapshot-due; same Apply table over `DomainResult.Events`; absent field or no-op → advisory skip; update §8, §12, §15, §16, §17. [spec-folded-snapshot.md:365-451]
- [ ] [Review][Patch] P-D4 §9.2: opaque, unreadable and unknown-version keys are retained at load (no delete) and MAY be replaced by the next successful folded write, logged with a support-safe reason class; the event stream stays audit authority. [spec-folded-snapshot.md:482-484,535]
- [ ] [Review][Patch] P-D5 Inventory the Admin actor-state inspector, `DaprConsistencyCommandService` snapshot read and `BenchmarkDatasetBuilder`; 6.2 MUST make the inspector return only sequence, size, envelope version and protection/readability class for the snapshot key. [spec-folded-snapshot.md:181-188,542-557]
- [ ] [Review][Patch] "Additive only" must pin the member form — `SnapshotRecord`, `DomainServiceWireResult`, and `AggregateReconstructionRequest` are public positional records and no package/API-compat validation exists; a positional parameter would break binary compatibility. State that new members are init-only properties with defaults. [spec-folded-snapshot.md:534]
- [ ] [Review][Patch] Bypassed legacy DSCS must not feed `lastSnapshotSequence` — otherwise migration waits up to one interval of full-replay commands; §10 step 5 and V8 ("next write overwrites") disagree. A non-authoritative DSCS snapshot contributes 0. [spec-folded-snapshot.md:493-505]
- [ ] [Review][Patch] Load-failure classification — baseline `LoadSnapshotAsync` wraps typed `TryGetStateAsync<SnapshotRecord>` in a non-cancel catch-all that deletes the key (`SnapshotManager.cs:195-211`), so state-store read failures and unknown-version shape failures delete a snapshot §12 says to retain. Correct §3.3/§3.5 inventory, make the §9.2 infra row single-outcome (retain, no delete, full replay; the "dead-letter after a staged batch" branch is unreachable at load), and require reading the envelope version from raw JSON before typed deserialization; delete only on deserialization failure of a known version. [spec-folded-snapshot.md:484]
- [ ] [Review][Patch] Folded snapshots newly require `TState` JSON round-trip fidelity — today automatic snapshots replay nested DSCS exactly; folded writes round-trip through `RehydrateFromJsonObject`, which sets only properties with a setter (fields, get-only members, custom comparers lost). Current aggregates comply. State the constraint and add a round-trip parity proof to V4/V7. [spec-folded-snapshot.md:243-259]
- [ ] [Review][Patch] Logging inventory is wrong — §3.6 says logs "continue to omit … stack traces", but `SnapshotManager` logs exception objects on advisory-create and corrupt-load (`SnapshotManager.cs:93,201` at baseline). Name them as behaviors 6.2 must replace. [spec-folded-snapshot.md:188]
- [ ] [Review][Patch] Story 6.1 deferred-work entries sit under the Story 4.15 heading — add their own heading. [deferred-work.md:3622]
- [x] [Review][Defer] `epics.md` Story 6.1 reconciliation still says the artifact is absent [_bmad-output/planning-artifacts/epics.md:4273] — deferred: fix edits a planning spec; `sprint-change-proposal-2026-09-23.md` item 4 already owns it and `epic-6-context.md` blocks 6.2 until reconciled.

- [x] [Review][Defer] Admin actor-state inspector also returns raw event keys (`{actorId}:events:{N}`) [src/Hexalith.EventStore.Admin.Server/Services/KnownActorTypes.cs:31] — deferred: owner decision D5 2026-10-04; event-key redaction belongs to Epic 7 Admin hygiene, outside snapshot scope.

Rejected:
- `sprint-status.yaml` edited despite the Never clause; manual check relaxed — `low`: letter violation is real but no tool or test reads the 6-1 row; the only fix amends this frozen spec.
- Lifecycle status contradictions (frontmatter `done`, Note 3 `in-progress`, BH-2 `in-review`, tracker `review`) — `low`: stale lines live in this spec; the tracker is resynced by this review.
- Code Map path `Server/Events/DaprAggregateStateReconstructor.cs` (actually `Server/DomainServices/`) — `low`: fix edits this spec.
- Duplicate digest command / prior BH-10 rationale — `false`: both commands print `0b456b5f…` in bash.
- No worst-case 4096 derivation / prior BH-7 rationale — `low`: identity caps (64/64/256) and fixed Unprotected/Legacy metadata bound the envelope by construction.
- No automated digest guard — `low`: 6.2 V1 recomputes the digest before work; a guard is new test code this story forbids.
- No runtime outcome when the bound is exceeded — `false`: the bound is a V5 test invariant over a structurally bounded envelope; no reachable exceed state.
- `WriteIndented` actor options inflate overhead — `false`: no host sets `ActorRuntimeOptions.JsonSerializerOptions` (only read at `AggregateActor.cs:103-104`); the Dapr default is Web.
- PascalCase legacy DSCS evades the camelCase detector — `false`: same; the domain-side detector is equally camelCase, so such a host already fails today.
- Snapshot ahead of head / orphaned snapshot has no §9.2 row — `low`: needs corruption or a partial restore (backup/restore are no-ops); adding a branch is not worth it.
- `State` null/array/primitive — `low`: v1 writes and `/replay-state` always produce objects; legacy arrays replay as events.
- Residual `MAY` wording (§5.2 CLR instance, §7.3 order, §8.3 rolling-upgrade reconstruct) — `low`: each permits a safe alternative; the actor never holds a CLR `TState`.

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
