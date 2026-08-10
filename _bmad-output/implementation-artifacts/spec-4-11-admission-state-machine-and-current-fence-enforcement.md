---
title: 'Admission State Machine And Current-Fence Enforcement'
type: 'feature'
created: '2026-08-09'
status: 'in-review'
baseline_commit: '5bcfdbc8b28ac2706053075cc4e71160ee029ad8'
review_loop_iteration: 0
story_key: '4-11-admission-state-machine-and-current-fence-enforcement'
context:
  - '/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/epic-4-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Durable idempotency admission has the state, fence, recovery, and signed-command seams, but the complete state-transition and side-effect-boundary contract must be made explicit and load-bearing. Without that contract, retries, restarts, stale capabilities, or partial downstream work could duplicate mutation or turn uncertain state into a fresh execution.

**Approach:** Complete and test the tenant/key actor state machine and its coordinator/aggregate integration. Preserve one monotonic non-zero current fence, permit it only for safe resume, and fail closed for invalid transitions, stale or missing capabilities, conflicting/replayed state, and uncertain outcomes before aggregate, domain, provider, repository, projection, audit, or scheduling work.

## Boundaries & Constraints

**Always:** Actor-serialized durable admission owns reservation and the current fence; `Reserved`/`Pending`/`Recoverable`/`UnknownProviderOutcome`/`Terminal`/`Expired` outcomes remain bounded and deterministic; equivalent resume reuses the persisted execution identity and fence; every fenced boundary validates the exact tenant, domain, aggregate, command, message, correlation, digest version, proof, and positive fence; non-execute outcomes perform zero downstream work; corrupt, unavailable, or ambiguous state fails closed; raw keys and canonical intent never enter state, telemetry, logs, errors, or evidence.

**Ask First:** Any provider-specific storage fencing or new public API that changes the approved Epic 4 architecture.

**Never:** Do not make the public idempotency key or caller-selected intent authoritative; do not issue a second live fence for the same admission; do not treat `Pending` or unknown provider outcome as a fresh miss; do not add direct state-store writes outside actor boundaries; do not claim live multi-host production evidence in this story.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|-----------------------------|----------------|
| First execution | Protected equivalent key absent | Atomically reserve with fence 1 and stable execution identities | State-store failure returns unavailable; no reservation is assumed |
| Safe resume | Recoverable state, same intent and identities | Reuse the current fence/checkpoint and execute or reconcile once | Invalid proof, identity, or fence fails closed |
| Live duplicate/conflict | Pending/terminal state; equivalent or different intent | Pending/replay or bounded conflict; no new downstream work | Conflict is permanent; pending remains retry/poll semantics |
| Unknown outcome | Unknown provider outcome before expiry | Read-only reconciliation only | Uncertainty remains retryable and never executes fresh work |
| Invalid transition | Wrong state, zero/stale fence, terminal/expired mutation | Durable state unchanged | Typed failure with no downstream invocation |

</frozen-after-approval>

## Code Map

- `src/Hexalith.EventStore.Server/Actors/IdempotencyAdmissionState.cs` -- durable state vocabulary; transition rules must remain explicit and exhaustive.
- `src/Hexalith.EventStore.Server/Actors/IdempotencyAdmissionRecord.cs` -- versioned persisted identity, execution checkpoint, replay result, expiry, and current fence; no raw key fields.
- `src/Hexalith.EventStore.Server/Actors/IdempotencyAdmissionActor.cs` -- serialized reserve/admit/begin/recovery/complete logic, fence validation, replay classification, and persistence boundary.
- `src/Hexalith.EventStore.Server/Commands/IdempotencyAdmissionCoordinator.cs` -- protected identity routing, actor session creation, resume/reconcile orchestration, and execution-context issuance.
- `src/Hexalith.EventStore.Server/Commands/IdempotencyExecutionContextProtector.cs` -- exact command-bound capability proof and fail-closed validation.
- `src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs` -- admission lifecycle ordering, side-effect-boundary classification, recovery marking, and zero-work dispositions.
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` -- fenced aggregate entry and repeated pre-boundary validation before domain, persistence, snapshot, and commit work.
- `src/Hexalith.EventStore.Server/Actors/FencedCommandEnvelope.cs` and `src/Hexalith.EventStore.Server/Actors/IAggregateActor.cs` -- internal-only command/fence transport and read-only reconciliation seam.
- `tests/Hexalith.EventStore.Server.Tests/Actors/IdempotencyAdmissionActorTests.cs` -- existing reservation, resume, conflict, expiry, corruption, and promotion fixtures to extend with transition and mutation assertions.
- `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorFencingTests.cs` and `tests/Hexalith.EventStore.Server.Tests/Pipeline/SubmitCommandHandlerIdempotencyAdmissionTests.cs` -- stale/tampered capability and handler orchestration evidence.
- `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorTestHelper.cs` -- controlled actor dependencies for asserting no state/domain/provider calls.
- `_bmad-output/implementation-artifacts/epic-4-context.md` and `spec-4-5-append-durability-race-evidence.md` -- authoritative invariants and prior persisted-state evidence discipline; Story 4.5's deferred provider-fencing decision remains intact.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.EventStore.Server/Actors/IdempotencyAdmissionActor.cs` and adjacent contract records -- enforce an explicit legal transition matrix, current-fence invariants, idempotent safe-resume behavior, and fail-closed handling for invalid/corrupt/unknown states without changing expiry ownership.
- [x] `src/Hexalith.EventStore.Server/Commands/IdempotencyAdmissionCoordinator.cs`, `SubmitCommandHandler.cs`, and fenced aggregate seams -- ensure every executable/recovery path carries one exact signed context and marks `Recoverable` versus `UnknownProviderOutcome` at the correct boundary; keep non-execute paths read-only.
- [x] `tests/Hexalith.EventStore.Server.Tests/Actors/IdempotencyAdmissionActorTests.cs`, `AggregateActorFencingTests.cs`, and `Pipeline/SubmitCommandHandlerIdempotencyAdmissionTests.cs` -- add transition-table, retry/resume, stale-fence, replay/conflict, restart/recovery, and zero-downstream-work tests with persisted-state assertions.

**Acceptance Criteria:**
- Given a reserved or recoverable admission, when it begins, resumes, or completes, then one persisted positive current fence and stable execution identities govern the whole attempt, and replay stores the exact result.
- Given any stale, tampered, missing, zero, or mismatched fence, when a fenced boundary is entered, then validation fails before domain, aggregate mutation, provider, repository, projection, audit, scheduling, or commit work.
- Given equivalent, conflicting, pending, terminal, unknown, corrupt, or unavailable state, when the same key is evaluated, then the approved bounded outcome is returned without fresh execution; unknown state permits reconciliation only.
- Given restart or an interruption before expiry, when the admission is resumed, then persisted state selects one safe checkpoint and current authority; uncertainty never becomes a missing reservation.
- Given the focused tests and deliberate mutation of each named invariant, when validation runs, then the intended test fails and the restored suite passes with no raw-key or intent leakage.

## Verification

**Commands:**
- `dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj -c Release --no-restore -m:1` -- expected: zero warnings and errors.
- `dotnet test tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj -c Release --no-build` -- expected: all focused server tests pass.
- `dotnet test tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj -c Release --no-build --filter FullyQualifiedName~IdempotencyAdmissionActorTests|FullyQualifiedName~AggregateActorFencingTests|FullyQualifiedName~SubmitCommandHandlerIdempotencyAdmissionTests` -- expected: all story-specific tests pass, using the repository's xUnit v3 fallback if project filtering is unsupported.

## Design Notes

The current fence is an internal capability, not a storage-provider claim. Repeated validation immediately before each side-effect boundary limits the window between admission and mutation; actor state remains the authority for currentness. Recovery classification must be based on whether any downstream boundary may have been crossed, and reconciliation must inspect authoritative aggregate/idempotency state without invoking domain or external work.
