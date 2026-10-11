---
title: 'Story 6.2: Folded Snapshot Implementation'
type: 'feature'
created: '2026-10-11'
status: 'done'
route: 'dispatch'
baseline_commit: 'e4532baa64f4cdabdbf766b9e38c36289a00adba'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/spec-folded-snapshot.md'
  - '{project-root}/_bmad-output/implementation-artifacts/epic-6-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Automatic snapshots store history-bearing `DomainServiceCurrentState` at the pre-command sequence, so cost grows with history; manual snapshots already store folded state.

**Approach:** Implement approved HX-FS-V1 at SHA-256 `a4ca9686628b284fb74da931e8cfb1466e80de45fd3d4e89a3c62358a4498ca5` and `MaxSnapshotEnvelopeOverheadBytes=4096`: one Apply seam, exact committed head, safe operator evidence.

## Boundaries & Constraints

**Always:** Recompute the digest before edits; mismatch stops work. Apply §§2–17 and V1–V18 without changing normative bytes. Keep actor-owned batch save, events as replay authority, advisory automatic/fail-closed manual behavior, cancellation, and positional compatibility. Version-1 writes require qualified compact serialization, pre-admitted resources and exact byte measurement. Redact both Admin snapshot-key routes.

**Never:** Persist DSCS/history or unqualified capture; add a second fold/replay/domain call, provider storage, Epic 8, projection or upcasting work. No unproven deletion, unverified completion, background migration or event rewrite. Future writes may overwrite legacy snapshots.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Automatic | Qualified due command | Detached once-admitted Apply; version-1 fold at new head in event batch | Absent evidence, no-op, first threshold crossing, limits: advisory skip (§§5–8) |
| Manual/read | Complete prefix or legacy/corrupt/opaque/orphan/gap | Qualified fold at head; validate metadata/version/shape/sequence before using snapshots | §9.2 typed retain/bypass/failure; readback proves success (§§7–10) |
| Admin | Local/owner-sidecar snapshot key | Sequence, canonical size or unknown, version, protection/readability | Redact state, secrets, provider errors and stacks (§§11,13) |

</frozen-after-approval>

## Code Map

- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` -- automatic batch/fence and manual witness; automatic currently writes pre-event DSCS, manual `AlreadyCurrent` accepts `>=`.
- `src/Hexalith.EventStore.Server/Events/SnapshotManager.cs`, `EventStreamReader.cs` -- typed load precedes raw version check; broad load catch deletes, reader trusts snapshot ahead of head.
- `src/Hexalith.EventStore.Client/Handlers/DomainProcessorBase.cs`, `DomainProcessorStateRehydrator.cs`; `src/Hexalith.EventStore.Client/Aggregates/AggregateReplayer.cs` -- shared Apply/restore; current capture lacks deployment qualification.
- `src/Hexalith.EventStore.DomainService/BoundedV1DomainResultProducer.cs`; `src/Hexalith.EventStore.Server/DomainServices/BoundedV1DomainResponseParser.cs` -- once-admitted bytes exist, but optional state has no producer/parser path.
- `src/Hexalith.EventStore.Admin.Server/Services/DaprInfrastructureQueryService.cs` -- local and owner-sidecar inspector exposes raw snapshot JSON.

## Tasks & Acceptance

**Execution:**
- [x] `_bmad-output/implementation-artifacts/spec-folded-snapshot.md` -- verify digest, approval and no open decisions before code edits (V1; §18).
- [x] `src/Hexalith.EventStore.Contracts/Commands/DomainServiceRequest.cs`, `src/Hexalith.EventStore.Contracts/Results/DomainResult.cs`, `DomainServiceWireResult.cs`; `src/Hexalith.EventStore.Server/Events/SnapshotRecord.cs` -- add defaulted init-only request/evidence/version (§§5,8,12).
- [x] `src/Hexalith.EventStore.DomainService/BoundedV1DomainSerializerProfile.cs`, `EventStoreDomainServiceExtensions.cs`; `src/Hexalith.EventStore.Client/Handlers/DomainProcessorBase.cs` -- qualify exact capture profile, reserve finite workspace before allocation, Apply detached admitted bytes (§§5.2,8).
- [x] `src/Hexalith.EventStore.DomainService/DomainServiceRequestRouter.cs`, `BoundedV1DomainResultProducer.cs`, `BoundedV1WireResultAdmission.cs`, `BoundedV1WireResultResponse.cs`; `src/Hexalith.EventStore.Server/DomainServices/BoundedV1DomainResponseParser.cs`, `DaprDomainServiceInvoker.cs` -- carry optional evidence within current gates/fallbacks (§§8,12).
- [x] `src/Hexalith.EventStore.Server/Events/SnapshotManager.cs`, `EventStreamReader.cs`; `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` -- classify raw version, validate head/shape, measure then stage folded state under fence, keep manual readback and §9.2 outcomes (§§5–10).
- [x] `src/Hexalith.EventStore.Admin.Server/Services/DaprInfrastructureQueryService.cs`, `DaprConsistencyCommandService.cs` -- redact both snapshot-key routes and exception-bearing logs (§§11,13).
- [x] `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorDomainResultTests.cs`, `AggregateActorManualSnapshotTests.cs`; `tests/Hexalith.EventStore.Server.Tests/Events/SnapshotManagerTests.cs`, `SnapshotRehydrationTests.cs`; `tests/Hexalith.EventStore.DomainService.Tests/BoundedV1DomainResultProducerTests.cs`; `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprActorQueryServiceTests.cs` -- prove V2–V18, including persisted parity, byte counts and compatibility (§15).

**Acceptance Criteria:**
- Given qualified admitted events, when a due snapshot commits, then version-1 folded state covers that head, equals manual replay and excludes history (V2–V7).
- Given absent qualification/evidence or policy/resource/shape/byte failure, when a valid command commits, then event bytes and result remain unchanged without snapshot-only commit (V5–V6,V15–V18).
- Given legacy, protected, malformed, orphaned or gapped data, when either read path runs, then §9.2 applies without partial authority (V8–V12,V17).
- Given either Admin inspector route reads a snapshot, when it returns, then only §13 safe fields appear and durable readback supports any completion (V13).

## Implementation Notes

- The implementation uses one qualified post-admission fold path and stages version-1 snapshots in the actor's event batch. The command reader validates the authoritative head and raw snapshot fields before typed binding; unverified direct reads retain malformed keys.
- `tools/folded-snapshot-compatibility/run.sh` exercises published pre-6.2 packages, a locally packed current version, binary and positional API use, optional interface fallbacks, and old/new wire and snapshot reads (V12).
- The unrelated `references/Hexalith.FrontComposer` pointer changed during this run and was left untouched.

## Spec Change Log

- 2026-10-11: Completed the implementation task checks and recorded focused verification. The frozen intent and approved normative snapshot specification were not changed.

## Review Triage Log

| Finding | Verdict and evidence | Route |
| --- | --- | --- |
| BH1 Tenants Dapr ACL | medium — Tenants bootstrap invokes `POST api/v1/commands` through Dapr, while `tenants-api` is allowed only `GET /ready`; this is from the separate ACL change after the story baseline. | defer |
| BH2 manifest token key | medium — the manifest declares `EventStore:AppChannelToken`, while the runtime reads `APP_API_TOKEN`; this is from a separate manifest change. | defer |
| BH3 manifest topic | medium — the manifest fixes `eventstore-events`, while publisher conventions use domain and tenant topic names; this is from a separate manifest change. | defer |
| BH4 mutable capture input | medium — the router passes the admitted wire event list and mutable payload arrays directly to a qualified capture callback, so a callback can alter bytes returned after optional failure; §8.2 requires private detached input. | patch |
| BH5 oversized optional scalar | medium — the bounded parser throws when the optional JSON scalar exceeds its field limit, rejecting otherwise valid events instead of omitting only evidence under §8.3. | patch |
| BH6 workspace admission | false — `FoldedSnapshotCaptureLimits` checks the sum of simultaneous declared representation maxima against finite workspace and live budgets before a profile exists; trusted callback allocations remain an explicit contract limit. | reject |
| BH7 null protected state | medium — a protection provider may return null state with valid protected metadata, and the writer currently stages a version-1 record lacking folded content. | patch |
| BH8 enum numbering | medium — inserting `InfrastructureFailure` first in public `SnapshotLoadOutcome` changes existing numeric values and `default` from `Absent`. | patch |
| BH9 P1R admission receipt | medium — the separate P1R runner's refused case calls a standalone `admit` probe and does not exercise a protected historical operation through that guard; the receipt supports only the fixture decision. | defer |
| BH10 P1R candidate pin | false — `validate_execution_inputs` explicitly rejects candidates other than 3.119.0 before `load_support_table`; this source-specific executor is intentionally bound to that version. | reject |
| BH11 production enrollment | false — §8.6 and D13 require explicit deployment-owned qualification; unknown domain states must skip automatic capture and return bounded unsupported manual materialization. The registration API is present for domain owners. | reject |
| EC1 future DSCS manual overwrite | high — raw DSCS collision currently returns no snapshot to the manual actor, bypassing its `SequenceNumber > CurrentSequence` guard and permitting overwrite of an invalid future key. | patch |
| EC2 duplicate Admin metadata | medium — duplicate `protectionMetadata` makes `TryReadSnapshotProperty` false, which the inspector currently conflates with absence and labels `Unprotected`. | patch |
| EC3 mutable parity getter | low — a derived processor could violate its trusted immutable qualification declaration by changing the profile between checks. The public protocol change needed to defend against a deliberately unstable owner callback is disproportionate to this rare defect. | reject |
| VG1 pre-command threshold test | medium — fixed-return policy mocks cannot detect passing an inferred post-command sequence into the first due check; a sequence-sensitive actor test is missing for the first threshold crossing. | patch |

## Verification

**Commands:**
- `dotnet build Hexalith.EventStore.slnx --configuration Release -m:1` -- warnings-as-errors.
- `dotnet test tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Release` -- full Server regression; run other changed projects individually.
- `git diff --check` -- no whitespace errors.

**Results (2026-10-11):**
- Approved normative SHA-256 recomputed as `a4ca9686628b284fb74da931e8cfb1466e80de45fd3d4e89a3c62358a4498ca5`; section 19 names the owner, 4096-byte bound, no open decisions, and Story 6.2 authorization.
- Release solution build passed with 0 warnings and 0 errors. After review patches and the direct-load regression update, the focused Server actor/read/security matrix passed 361/361.
- DomainService project passed 566/566. Admin Server project passed 757, with 18 preexisting skipped DW2 ATDD cases. `bash tools/folded-snapshot-compatibility/run.sh`, `bash -n tools/folded-snapshot-compatibility/run.sh`, and `git diff --check` passed.
- The 5,000-element state measured 23,902 bytes; canonical full envelopes measured 24,207, 24,209, and 24,208 bytes at covered sequences 1, 100, and 1,000. Escaped compatibility metadata rejection preserved its serialized bytes and staged no snapshot.
- The full Server project command `timeout 600s dotnet test tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Release --no-build` exited 124 without a summary. A 120-second direct-run diagnostic discovered 3,440 cases, parallel mode `none`, and long-running event-reader tests; it also timed out (124). The focused Story 6.2 matrix tests above completed and passed. The review logged 15 findings: seven patched, four rejected after checking, and four unrelated findings deferred.

