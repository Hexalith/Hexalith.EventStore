---
title: 'Story 5.1: Infrastructure Failure Cache Clear'
type: 'bugfix'
created: '2026-09-05'
status: 'in-progress'
route: 'dispatch'
review_loop_iteration: 1
baseline_commit: 'b43d64f906665e2bf3015eb2d3f16b771598d352'
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
- `src/Hexalith.EventStore.Server/Actors/ActorStateRemediationException.cs` -- support-safe secondary-failure classification; retain stage/type/discard facts without exposing exception messages.
- `src/Hexalith.EventStore.Server/Events/IDeadLetterPublisher.cs` -- cancellation contract is authoritative: requested cancellation propagates and is never converted into advisory publication failure.
- `src/Hexalith.EventStore.Server/Events/EventPersister.cs` -- stages write-once event keys and aggregate metadata without saving; reuse as the concrete leak seam and do not move commit ownership.
- `src/Hexalith.EventStore.Server/Events/SnapshotManager.cs` -- stages snapshots/removals; preserve advisory snapshot behavior.
- `src/Hexalith.EventStore.Server/Actors/ActorStateMachine.cs` -- stages checkpoints and pipeline cleanup without saving.
- `src/Hexalith.EventStore.Testing/Fakes/InMemoryStateManager.cs` -- models pending versus committed state; reuse its `CommittedState`, clear, and save semantics unchanged.
- `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorTestHelper.cs` -- central actor construction seam for injecting a recording/faulting state manager.
- `tests/Hexalith.EventStore.Server.Tests/Actors/EventDrainRecoveryTests.cs` -- contains the existing stage→clear ordering pattern; reuse the pattern without folding publication-recovery behavior into scope.
- `tests/Hexalith.EventStore.Server.Tests/Actors/DeadLetterRoutingTests.cs` and `AggregateActorDomainResultTests.cs` -- preserve existing retryability, redaction, status, and dead-letter contracts; their call assertions are not sufficient completion evidence.

## Tasks & Acceptance

**Execution:**
- [ ] `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` -- harden infrastructure/conflict/finalizer clear-restage-save ordering; preserve caller cancellation and dead-letter contracts; record failed activities; and install a poisoned-activation barrier that prevents every later state-bearing turn from staging or saving until a non-cancelable cache clear succeeds.
- [ ] `src/Hexalith.EventStore.Server/Actors/ActorStateRemediationException.cs` -- carry support-safe primary/remediation classification and a narrowly defined discard fact without raw exception messages or an ambiguous claim about later finalizer state.
- [ ] `tests/Hexalith.EventStore.Server.Tests/Actors/FaultInjectingActorStateManager.cs` -- add a test-local wrapper over the real pending/committed model with ordered tracing, before-delegate and commit-then-throw faults, repeated/double failures, committed snapshots, and concurrent-winner injection.
- [ ] `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorTestHelper.cs` -- expose only the state-manager, logger/activity, and collaborator seams required by the focused fault matrix.
- [ ] `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorInfrastructureFailureTests.cs` -- prove infrastructure staging, conflict retry/exhaustion remediation, every finalizer operation, double-clear poisoning/recovery, pre/post-commit save ambiguity, cancellation, activity/log redaction fields, and exact durable end state.

**Acceptance Criteria:**
- Given concrete stream and auxiliary values are staged before rehydration, domain-service, or event-persistence failure, when rejection completes, then clear precedes every rejection checkpoint, cleanup, dead-letter outcome, or save and no staged stream value reaches durable state.
- Given one or more persistence conflicts, when the actor retries or exhausts its budget, then each attempt clears before rehydration/restaging and only winning or pre-existing durable values remain.
- Given clear, dead-letter, checkpoint, cleanup, rejection-save, or finalizer-save failure, when the command terminates, then the earliest causal failure and observed durable consequence remain support-safe and no unproved discard is reported.
- Given any terminal Story 5.1 lane, when state is inspected through a fresh view, then original event/metadata keys are unchanged, only permitted rejection cleanup is observable, and no snapshot, publication index, drain, trigger, idempotency, or pending-count residue leaked.
- Given a cache clear cannot prove discard, when the same activation receives any later state-bearing turn, then it performs no state mutation or save until a clean-cache barrier succeeds, so residual mutations cannot hitchhike onto later work.
- Given a finalizer save commits and then throws or fails before commit, when remediation inspects durable pending state, then it neither double-decrements nor strands a recoverable pending slot and reports an unambiguous observed consequence.
- Given caller cancellation reaches conflict clearing or dead-letter publication, when the path terminates, then cancellation propagates after non-cancelable state-safety cleanup and is not reclassified as advisory or remediation failure.
- Given remediation or finalization diagnostics are emitted, when focused tests inspect activities and structured logs, then stage, operation, exception type, discard/observation fields, and redaction are exact and no raw secret appears.
- Given implementation is complete, when focused tests, the full Server test project, and the Release solution build run, then all pass without warnings or regressions and every changed production await uses `ConfigureAwait(false)`.

## Implementation Notes

- 2026-09-05 -- Hardened conflict retry, infrastructure rejection, conflict exhaustion, and pending-command finalization around explicit clear/restage/save boundaries. Remediation failures now retain support-safe primary/remediation classifications, and throwing dead-letter publication remains advisory.
- Added `ActorStateRemediationException`, a test-local fault-injecting wrapper over `InMemoryStateManager`, 16 focused committed-state cases, and the narrow actor-helper injection seam. A finalizer-save failure intentionally leaves the previously committed pending count at `1`, clears its staged decrement, and logs the observed durable consequence so no later save can hitchhike it.
- Verification: focused infrastructure matrix 16/16 passed; full Server assembly 3,156 passed with 25 pre-existing skips and no failures/errors; Release solution build passed with 0 warnings/errors.
- The project-level `dotnet test --no-build` command discovered zero tests and exited 5 under Microsoft.Testing.Platform. The repository-prescribed direct built-assembly fallback ran the complete Server suite successfully.
- Concurrent commit `68d04065576d31490e14346ffd81fdaf182fe724` landed after the recorded baseline. Its unrelated Story 3.14 changes were preserved; its overwrite of the Story 5.1 sprint transition was reapplied.
- Review loop 1 reverted the Story 5.1 code/tests after finding that a finalizer-operation plus cleanup-clear double failure left cached mutations available to a later actor turn. The passing first-pass behavior remains evidence only; implementation is being re-derived from the strengthened design below.

## Spec Change Log

- 2026-09-05 -- Review found that the generic remediation/finalizer task allowed an unproved double-clear failure to return without preventing a later save. Strengthened Tasks, Acceptance, and Design Notes with a poisoned-activation recovery barrier, commit-then-throw inspection, bounded pending-count reconciliation, explicit cancellation/activity/log contracts, and the missing conflict/finalizer fault matrix. Known-bad state avoided: residual failed-turn mutations hitchhiking on a later save, misleading discard claims, phantom backpressure, and swallowed cancellation. KEEP: clear-before-restage/save ordering; primary failure logged before remediation; support-safe type/stage classification; non-cancellation dead-letter failure remains advisory without custom retry; `InMemoryStateManager` remains the durable oracle; concrete staged-state, concurrent-winner, and one/multiple-conflict tests remain.

## Review Triage Log

- blind-1 -- **medium / patch** -- `ClearCacheAsync(cancellationToken)` in the conflict-retry path catches caller-token cancellation and wraps it as remediation failure; this changes cancellation classification.
- blind-2 -- **medium / patch** -- `IDeadLetterPublisher` explicitly propagates `OperationCanceledException`, but the new caller catch converts every thrown cancellation into advisory failure.
- blind-3 -- **medium / patch** -- the finalizer now catches every `OperationCanceledException` whereas the replaced code propagated it; dependency cancellation would be hidden behind a warning.
- blind-4 -- **medium / bad_spec** -- a successful discard after a finalizer failure does not retry or reconcile the decrement, and the next backpressure read can indefinitely observe the committed count as inflated.
- blind-5 -- **medium / bad_spec** -- `StagedStateDiscarded` is computed before finalization; a later finalizer/discard double failure can leave new cached mutations while the propagated exception still reports discard success for the earlier batch.
- blind-6 -- **false / reject** -- the internal exception intentionally retains support-safe stage/type classification rather than raw exceptions; callers have no typed remediation retry contract, and retaining raw inner exceptions would undermine the redaction boundary.
- blind-7 -- **medium / patch** -- event-persistence remediation exceptions bypass the only catch that records the persistence activity failure, leaving that span without the error status/event even though a structured log is emitted.
- blind-8 -- **medium / bad_spec** -- the fault manager throws only before delegation, so no test models a state save that commits and then reports failure; the durable-consequence requirement needs both pre-commit and ambiguous post-commit faults.
- blind-9 -- **medium / patch** -- conflict-exhaustion clear, cleanup, and rejection-save remediation branches have no injected-failure coverage.
- blind-10 -- **medium / patch** -- finalizer clear/read/write and cleanup double-failure branches are untested; only the final save failure runs.
- blind-11 -- **medium / patch** -- none of the new structured remediation/dead-letter/finalizer diagnostic fields or redaction outcomes is asserted.
- blind-12 -- **high / defer** -- concurrent Story 3.14 work filters both real container-publish tests from CI and provides no automatic heavyweight lane, permitting OCI provenance regressions outside Story 5.1.
- blind-13 -- **low / defer** -- concurrent Story 3.14 ledger text says the malformed-input theory is heavyweight/excluded although the code and CI intentionally keep it in the default gate.
- blind-14 -- **medium / defer** -- the unrelated Windows governance binder scans a fixed 280-character substring rather than the guard body, allowing nearby text to satisfy it.
- blind-15 -- **low / defer** -- the concurrently edited Story 4.7 spec changes its historical `created` date from 2026-08-27 to 2026-09-05 instead of recording a separate replanning timestamp.
- verification-gap-1 -- **high / defer** -- pre-verified: no automatic workflow selects `HeavyweightContainerPublish`; mutating the real multi-RID label hook would evade all in-gate tests.
- verification-gap-2 -- **medium / defer** -- pre-verified: the seven Windows skip outcomes never execute on a Windows runner and are covered only by a weak source-text binder.
- verification-gap-3 -- **medium / patch** -- pre-verified: no test faults conflict-exhaustion clear, cleanup, or save and asserts primary conflict classification plus winner-state durability.
- verification-gap-4 -- **medium / patch** -- pre-verified: remediation tests omit exact operation/type/discard-field assertions, so false safe-discard diagnostics remain possible.
- verification-gap-5 -- **medium / patch** -- pre-verified: finalizer clear, pending-count read, and pending-count write failures have no behavioral coverage.
- verification-gap-6 -- **low / defer** -- the unrelated DW-372 completion entry contradicts the current malformed-input trait and default-gate behavior.
- edge-1 -- **medium / patch** -- caller cancellation during conflict-retry clearing is demonstrably wrapped rather than propagated.
- edge-2 -- **medium / patch** -- finalizer state operations now swallow `OperationCanceledException`, a behavior change from the replaced catch filter.
- edge-3 -- **high / bad_spec** -- if both a finalizer operation and `TryDiscardStagedStateAsync` fail, the method returns and the next actor turn can commit the residual cache; no poison/recovery barrier exists.
- edge-4 -- **medium / defer** -- the unrelated fixed-window Windows source scan can match `Assert.Skip` outside the actual guard body.
- edge-5 -- **false / reject** -- no current test reconfigures the same fault slot, so the stale callback branch is unreachable in the helper's present consumers.
- edge-6 -- **false / reject** -- every current seed call uses a fresh wrapper and the helper promises seeding, not wholesale replacement; no old-key contamination occurs at a cited consumer.
- edge-7 -- **medium / defer** -- concurrent Story 3.14 work replaced the exact 86,401-second upper-bound case with 90,001 seconds, allowing a widened 24-hour authority window to escape boundary detection.
- edge-8 -- **medium / patch** -- the spec's preservation claim is contradicted by conflict-retry cancellation being wrapped as remediation failure.
- edge-9 -- **high / bad_spec** -- the claim that later saves cannot see residual state is false after a finalizer/discard double failure.
- edge-10 -- **medium / bad_spec** -- finalizer failure deliberately leaves a durable pending count of one; repeated failures can create phantom backpressure and the spec did not define a safe reconciliation boundary.

## Design Notes

Use the deterministic `InMemoryStateManager` pending/committed model as the completion-grade durable-state oracle because the story gate names focused Server tests and a Release build, not a live-sidecar lane. A test-local wrapper should add fault scheduling and operation order without widening the public Testing package. Treat normal infrastructure rejection as advisory status plus durable pipeline cleanup; do not invent a retained `Rejected` pipeline record where checkpoint and removal share one batch.

The pinned Dapr Actors 1.18.5 `Actor`/`ActorHost` surface has no immediate self-deactivation API. Maintain an activation-local unsafe-cache flag after any failed discard. Before every later actor entry point that can stage or save state, require a non-cancelable clear; on failure, fail closed without touching state and keep the flag set. Clear the flag only after the cache-clear call completes. This is a turn barrier, not a retry loop or public contract.

For pending-count finalization, establish the committed pre-decrement count after a clean clear. If save fails, clear and inspect durable state: an already-decremented value proves commit-then-throw; the unchanged value permits one clean recovery decrement/save; any other value or failed inspection keeps the activation unsafe and is reported without another save. Never allow a later turn to heal by accidentally committing an old batch.

Propagate caller/request cancellation from conflict clearing and dead-letter publication after required non-cancelable cleanup; non-cancellation publication failures remain advisory. Preserve the prior finalizer cancellation contract after best-effort discard. Mark the relevant child and process activities failed whenever remediation escapes, and verify all structured diagnostic fields and redaction.

## Verification

**Commands:**
- `dotnet restore Hexalith.EventStore.slnx -p:UseHexalithProjectReferences=false` -- expected: solution restores with the repository-pinned SDK and package graph.
- `dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Release -warnaserror -m:1 -p:UseHexalithProjectReferences=false` -- expected: focused test assembly builds without warnings.
- `dotnet tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll -class Hexalith.EventStore.Server.Tests.Actors.AggregateActorInfrastructureFailureTests` -- expected: every focused failure/state lane passes.
- `dotnet tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll` -- expected: full Server regression suite passes with only the pre-existing skips.
- `dotnet build Hexalith.EventStore.slnx --no-restore --configuration Release -warnaserror -m:1 -p:UseHexalithProjectReferences=false` -- expected: Release solution builds without warnings.
