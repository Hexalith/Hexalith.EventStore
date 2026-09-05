---
title: 'Story 5.1: Infrastructure Failure Cache Clear'
type: 'bugfix'
created: '2026-09-05'
status: 'draft'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-5-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Infrastructure and persistence-conflict rejection paths clear staged actor state on their successful paths, but remediation failures and the unconditional pending-count finalizer can still expose a later save to residual event, metadata, snapshot, publication, pipeline, or pending mutations. Existing tests prove calls and retryability rather than the committed end state.

**Approach:** Harden the `AggregateActor` failure boundary so every retry or rejection clears failed-attempt state before restaging or saving, preserves the earliest causal failure when remediation also fails, and never claims safe discard without durable evidence. Add deterministic fault-injection tests that stage concrete values and inspect committed state after each terminal path.

## Boundaries & Constraints

**Always:** Keep `AggregateActor` as the sole durable event-mutation coordinator; use clear-before-restage/save ordering; preserve support-safe redaction; retain retryable infrastructure/conflict semantics; apply `ConfigureAwait(false)` to every production await; verify exact committed keys and values after failures.

**Never:** Change retry counts, event identity, append fencing, idempotency rules, publication recovery, projection triggering, public contracts, packages, UI, topology, or dead-letter retry policy. Do not treat call counts, HTTP results, or advisory status as durable-state proof, and do not modify `references/` content or unrelated worktree changes.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Infrastructure rejection | Concrete event, metadata, snapshot, or publication state is staged before a pre-terminal failure | Clear completes before rejection/cleanup state is staged and only the permitted cleanup is committed | Return a bounded, redacted rejection; retain transient retryability |
| Conflict retry | A failed attempt staged state and a concurrent winner changed durable state | Clear every failed attempt, rehydrate the winning state, and commit no stale attempt value | Exhaust only the configured budget |
| Conflict exhaustion | Final failed attempt contains staged values | Clear before cleanup/save; leave stream data unchanged and no terminal idempotency/publication residue | Return the existing redacted concurrency rejection |
| Advisory dead-letter failure | Dead-letter returns failure or throws after the primary failure | Continue only where the bounded contract permits; never restore discarded staged state | Preserve and classify the primary cause plus the advisory failure without payload disclosure |
| Remediation failure | Clear, checkpoint, cleanup, rejection save, or pending final save fails | Prevent a later save from silently committing residual state and report the actual durable consequence | Never claim staged state was discarded unless inspection proves it |

</frozen-after-approval>

## Code Map

- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` -- `ProcessCommandCoreAsync`, `HandleInfrastructureFailureAsync`, `CompleteConcurrencyConflictAsync`, and the pending-count `finally` own clear/restage/save ordering and causal classification.
- `src/Hexalith.EventStore.Server/Events/EventPersister.cs` -- stages write-once event keys and aggregate metadata without saving; reuse as the concrete leak seam and do not move commit ownership.
- `src/Hexalith.EventStore.Server/Events/SnapshotManager.cs` -- stages snapshots/removals; preserve advisory snapshot behavior.
- `src/Hexalith.EventStore.Server/Actors/ActorStateMachine.cs` -- stages checkpoints and pipeline cleanup without saving.
- `src/Hexalith.EventStore.Testing/Fakes/InMemoryStateManager.cs` -- models pending versus committed state; reuse its `CommittedState`, clear, and save semantics unchanged.
- `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorTestHelper.cs` -- central actor construction seam for injecting a recording/faulting state manager.
- `tests/Hexalith.EventStore.Server.Tests/Actors/EventDrainRecoveryTests.cs` -- contains the existing stage→clear ordering pattern; reuse the pattern without folding publication-recovery behavior into scope.
- `tests/Hexalith.EventStore.Server.Tests/Actors/DeadLetterRoutingTests.cs` and `AggregateActorDomainResultTests.cs` -- preserve existing retryability, redaction, status, and dead-letter contracts; their call assertions are not sufficient completion evidence.

## Tasks & Acceptance

**Execution:**
- [ ] `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` -- harden infrastructure/conflict remediation and final pending-state persistence so secondary failures cannot expose residual staged state to a later save, while preserving the earliest causal failure and current transient-result contracts.
- [ ] `tests/Hexalith.EventStore.Server.Tests/Actors/FaultInjectingActorStateManager.cs` -- add a test-local wrapper over the real in-memory pending/committed model with ordered tracing, selected-call faults, committed snapshots, and concurrent-winner injection.
- [ ] `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorTestHelper.cs` -- expose the narrow construction seams required by the focused state-manager and collaborator faults.
- [ ] `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorInfrastructureFailureTests.cs` -- cover infrastructure staging, conflict retry/exhaustion, remediation failures, ordering, redaction, retryability, and exact reloaded durable end state.

**Acceptance Criteria:**
- Given concrete stream and auxiliary values are staged before rehydration, domain-service, or event-persistence failure, when rejection completes, then clear precedes every rejection checkpoint, cleanup, dead-letter outcome, or save and no staged stream value reaches durable state.
- Given one or more persistence conflicts, when the actor retries or exhausts its budget, then each attempt clears before rehydration/restaging and only winning or pre-existing durable values remain.
- Given clear, dead-letter, checkpoint, cleanup, rejection-save, or finalizer-save failure, when the command terminates, then the earliest causal failure and observed durable consequence remain support-safe and no unproved discard is reported.
- Given any terminal Story 5.1 lane, when state is inspected through a fresh view, then original event/metadata keys are unchanged, only permitted rejection cleanup is observable, and no snapshot, publication index, drain, trigger, idempotency, or pending-count residue leaked.
- Given implementation is complete, when focused tests, the full Server test project, and the Release solution build run, then all pass without warnings or regressions and every changed production await uses `ConfigureAwait(false)`.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Design Notes

Use the deterministic `InMemoryStateManager` pending/committed model as the completion-grade durable-state oracle because the story gate names focused Server tests and a Release build, not a live-sidecar lane. A test-local wrapper should add fault scheduling and operation order without widening the public Testing package. Treat normal infrastructure rejection as advisory status plus durable pipeline cleanup; do not invent a retained `Rejected` pipeline record where checkpoint and removal share one batch.

## Verification

**Commands:**
- `dotnet restore Hexalith.EventStore.slnx -p:UseHexalithProjectReferences=false` -- expected: solution restores with the repository-pinned SDK and package graph.
- `dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Release -warnaserror -m:1 -p:UseHexalithProjectReferences=false` -- expected: focused test assembly builds without warnings.
- `dotnet tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll -class Hexalith.EventStore.Server.Tests.Actors.AggregateActorInfrastructureFailureTests` -- expected: every focused failure/state lane passes.
- `dotnet test tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --no-build --configuration Release -m:1 -p:UseHexalithProjectReferences=false` -- expected: full Server regression suite passes.
- `dotnet build Hexalith.EventStore.slnx --no-restore --configuration Release -warnaserror -m:1 -p:UseHexalithProjectReferences=false` -- expected: Release solution builds without warnings.
