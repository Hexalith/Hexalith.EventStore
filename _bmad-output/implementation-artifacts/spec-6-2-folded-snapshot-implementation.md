---
title: 'Story 6.2: Folded Snapshot Implementation'
type: 'feature'
created: '2026-10-11'
status: 'in-progress'
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
- [ ] `_bmad-output/implementation-artifacts/spec-folded-snapshot.md` -- verify digest, approval and no open decisions before code edits (V1; §18).
- [ ] `src/Hexalith.EventStore.Contracts/Commands/DomainServiceRequest.cs`, `src/Hexalith.EventStore.Contracts/Results/DomainResult.cs`, `DomainServiceWireResult.cs`; `src/Hexalith.EventStore.Server/Events/SnapshotRecord.cs` -- add defaulted init-only request/evidence/version (§§5,8,12).
- [ ] `src/Hexalith.EventStore.DomainService/BoundedV1DomainSerializerProfile.cs`, `EventStoreDomainServiceExtensions.cs`; `src/Hexalith.EventStore.Client/Handlers/DomainProcessorBase.cs` -- qualify exact capture profile, reserve finite workspace before allocation, Apply detached admitted bytes (§§5.2,8).
- [ ] `src/Hexalith.EventStore.DomainService/DomainServiceRequestRouter.cs`, `BoundedV1DomainResultProducer.cs`, `BoundedV1WireResultAdmission.cs`, `BoundedV1WireResultResponse.cs`; `src/Hexalith.EventStore.Server/DomainServices/BoundedV1DomainResponseParser.cs`, `DaprDomainServiceInvoker.cs` -- carry optional evidence within current gates/fallbacks (§§8,12).
- [ ] `src/Hexalith.EventStore.Server/Events/SnapshotManager.cs`, `EventStreamReader.cs`; `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` -- classify raw version, validate head/shape, measure then stage folded state under fence, keep manual readback and §9.2 outcomes (§§5–10).
- [ ] `src/Hexalith.EventStore.Admin.Server/Services/DaprInfrastructureQueryService.cs`, `DaprConsistencyCommandService.cs` -- redact both snapshot-key routes and exception-bearing logs (§§11,13).
- [ ] `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorDomainResultTests.cs`, `AggregateActorManualSnapshotTests.cs`; `tests/Hexalith.EventStore.Server.Tests/Events/SnapshotManagerTests.cs`, `SnapshotRehydrationTests.cs`; `tests/Hexalith.EventStore.DomainService.Tests/BoundedV1DomainResultProducerTests.cs`; `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprActorQueryServiceTests.cs` -- prove V2–V18, including persisted parity, byte counts and compatibility (§15).

**Acceptance Criteria:**
- Given qualified admitted events, when a due snapshot commits, then version-1 folded state covers that head, equals manual replay and excludes history (V2–V7).
- Given absent qualification/evidence or policy/resource/shape/byte failure, when a valid command commits, then event bytes and result remain unchanged without snapshot-only commit (V5–V6,V15–V18).
- Given legacy, protected, malformed, orphaned or gapped data, when either read path runs, then §9.2 applies without partial authority (V8–V12,V17).
- Given either Admin inspector route reads a snapshot, when it returns, then only §13 safe fields appear and durable readback supports any completion (V13).

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Verification

**Commands:**
- `dotnet build Hexalith.EventStore.slnx --configuration Release -m:1` -- warnings-as-errors.
- `dotnet test tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Release` -- full Server regression; run other changed projects individually.
- `git diff --check` -- no whitespace errors.

