---
title: 'Story 4.12: Expiry Compaction And Tombstone Retention'
type: 'feature'
created: '2026-08-09'
status: 'done'
review_loop_iteration: 0
story_key: '4-12-expiry-compaction-and-tombstone-retention'
baseline_commit: '5bcfdbc8b28ac2706053075cc4e71160ee029ad8'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/epic-4-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Cross-tier reuse can bypass expiry precedence; compaction runs only on reuse; purge can race legal hold; and an old signed context remains valid after expiry. Expired keys can become distinguishable or executable again.

**Approach:** Make expiry authoritative, schedule durable compaction, validate current state before protected work, and serialize purge with lifecycle transitions. Prove committed end state and zero downstream work.

## Boundaries & Constraints

**Always:** Preserve Story 4.11 changes. Retention starts at durable finalization: mutation is 86,400 seconds; commit is `DateTimeOffset.AddYears(7)`. Use monotonic time and inclusive expiry. Atomically replace live state with the AD-25 fence-free tombstone. Every expired intent/tier is indistinguishable and performs zero protected work. Only active tenants admit work.

**Ask First:** Changing AD-25, tombstone fields, retention, hold policy, public APIs, submodules, Story 4.14/4.15 evidence, or Story 4.11 beyond post-expiry currentness.

**Never:** Store a tombstone fence; delete live evidence first; expose protected data; convert non-executable state to missing; weaken legacy guards; implement migration, multi-host closure, or UI.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Live/boundary | Terminal before / at / after expiry | Replay before; otherwise compact and expire | Rollback uses high-water time |
| Tombstone | Any trusted intent/tier | Same non-retryable expired result | No hints or downstream work |
| No reuse/context | Expiry across restart or old signed context | Durable tombstone; reject execution | Never expose missing |
| Purge/hold | Held, eligible, corrupt, or racing | Only serialized eligibility removes evidence | Otherwise block deletion/readmission |

</frozen-after-approval>

## Code Map

- `src/Hexalith.EventStore.Server/Actors/IdempotencyAdmissionActor.cs:74-301,445-660` -- classification, retention, purge, validation, and compaction baseline; `IdempotencyAdmissionRecord.cs` / `IdempotencyAdmissionTombstone.cs` define the fixed schemas.
- `src/Hexalith.EventStore.Server/Commands/IdempotencyAdmissionCoordinator.cs:48-225`, `Actors/AggregateActor.cs:2838-2851`, and `Commands/IdempotencyExecutionContextProtector.cs` -- signature checks lack durable currentness.
- `src/Hexalith.EventStore.Server/Actors/IdempotencyTenantLifecycleActor.cs:25-250` and `Commands/IdempotencyTenantLifecyclePurger.cs:12-59` -- 400-day hold/resume and non-serialized purge.
- `src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs:65-166` and corresponding Server/LiveSidecar tests -- non-execute, persisted-state, and restart reuse points.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.EventStore.Server/Actors/IdempotencyAdmissionActor.cs` and contracts -- classify expiry before tier/intent, schedule recoverable compaction, and expose current-authority validation.
- [x] `src/Hexalith.EventStore.Server/Commands/IdempotencyAdmissionCoordinator.cs` and fenced route -- reject contexts whose durable admission cannot execute.
- [x] `src/Hexalith.EventStore.Server/Actors/IdempotencyTenantLifecycleActor.cs` and `Commands/IdempotencyTenantLifecyclePurger.cs` -- validate lifecycle, block post-deletion admission, and serialize purge, holds, and acknowledgement.
- [x] `tests/Hexalith.EventStore.Server.Tests/Actors/IdempotencyAdmissionExpiryTests.cs` plus lifecycle/purger/handler tests -- cover the matrix, committed state, corruption, races, and zero work.
- [x] `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Actors/IdempotencyAdmissionExpiryLiveSidecarTests.cs` -- prove Redis retains only the tombstone after expiry/restart; exclude multi-host claims.

**Acceptance Criteria:**
- Given either tier, when finalization expires, then exact retention is persisted and inclusive compaction occurs without reuse.
- Given any intent/tier after expiry, when classified, then one safe outcome returns and protected work remains untouched.
- Given an earlier signed context, when admission is terminal/expired/compacted, then durable currentness rejects it before protected work.
- Given deletion, hold, corruption, or purge races, when lifecycle changes, then only serialized eligibility removes evidence and missing never authorizes work.

## Spec Change Log

- 2026-08-09: Applied review fixes for pre-begin capability validation, terminal-last handler ordering, promotion/redirect currentness, reminder-before-save scheduling, strict tombstone/lifecycle validation, lifecycle-serialized admission, and bounded cancellable purge turns.

## Design Notes

Order checks as identity/collision, expiry, then semantics. Purge and hold share one lifecycle serialization boundary. Signatures do not prove current authority.

## Verification

**Commands:**
- Focused expiry/admission/lifecycle/purger/context/aggregate/handler xUnit classes -- passed: 106; failed: 0; skipped: 0.
- `dotnet test tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Release --no-build -m:1` -- passed: 3,045; failed: 0; skipped: 25 pre-existing ATDD tests; build warnings: 0.
- `dotnet build tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj --configuration Release -m:1` -- succeeded with 0 warnings and 0 errors.
- `dotnet test tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~IdempotencyAdmissionExpiryLiveSidecarTests"` -- passed: 1; failed: 0; skipped: 0.

**Matrix audit:**
- Live/boundary: fixed mutation and calendar-year commit retention plus tick-before/exact/tick-after reminder tests passed.
- Tombstone: exact fence-free schema, cross-tier/cross-intent indistinguishability, and handler zero-downstream tests passed.
- No reuse/context: the Redis restart/reminder proof and aggregate durable-currentness rejection test passed.
- Purge/hold: legal-hold pause/resume, corrupt-state refusal, bounded purge, and live-admission retention tests passed.

## Suggested Review Order

**Protected execution lifecycle**

- Validate capabilities before mutation; finalize only after every protected boundary completes.
  [`SubmitCommandHandler.cs:239`](../../src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs#L239)

- Separate immutable proof checking from mutable durable-authority validation.
  [`IdempotencyExecutionContextProtector.cs:68`](../../src/Hexalith.EventStore.Server/Commands/IdempotencyExecutionContextProtector.cs#L68)

- Bind signed contexts to current state, identity, promotion, and redirect authority.
  [`IdempotencyAdmissionActor.cs:367`](../../src/Hexalith.EventStore.Server/Actors/IdempotencyAdmissionActor.cs#L367)

- Recheck reconciliation purpose inside the aggregate actor boundary.
  [`AggregateActor.cs:141`](../../src/Hexalith.EventStore.Server/Actors/AggregateActor.cs#L141)

**Expiry and compaction**

- Arm durable expiry before persisting exact tier-specific terminal retention.
  [`IdempotencyAdmissionActor.cs:308`](../../src/Hexalith.EventStore.Server/Actors/IdempotencyAdmissionActor.cs#L308)

- Classify inclusive expiry before retention-tier or intent semantics.
  [`IdempotencyAdmissionActor.cs:98`](../../src/Hexalith.EventStore.Server/Actors/IdempotencyAdmissionActor.cs#L98)

- Replace live replay evidence atomically with the fence-free tombstone.
  [`IdempotencyAdmissionActor.cs:790`](../../src/Hexalith.EventStore.Server/Actors/IdempotencyAdmissionActor.cs#L790)

**Tenant deletion governance**

- Serialize active-lifecycle admission with the exact registered actor reference.
  [`IdempotencyTenantLifecycleActor.cs:73`](../../src/Hexalith.EventStore.Server/Actors/IdempotencyTenantLifecycleActor.cs#L73)

- Keep eligibility, tombstone deletion, alias cleanup, and acknowledgement in one turn.
  [`IdempotencyTenantLifecycleActor.cs:189`](../../src/Hexalith.EventStore.Server/Actors/IdempotencyTenantLifecycleActor.cs#L189)

- Reject contradictory retention, hold, timestamp, and actor-identity state.
  [`IdempotencyTenantLifecycleActor.cs:366`](../../src/Hexalith.EventStore.Server/Actors/IdempotencyTenantLifecycleActor.cs#L366)

- Bound purge iterations and honor cancellation between destructive actor turns.
  [`IdempotencyTenantLifecyclePurger.cs:15`](../../src/Hexalith.EventStore.Server/Commands/IdempotencyTenantLifecyclePurger.cs#L15)

**Verification**

- Exercise reminder ordering, exact retention, currentness, corruption, and compaction boundaries.
  [`IdempotencyAdmissionExpiryTests.cs:50`](../../tests/Hexalith.EventStore.Server.Tests/Actors/IdempotencyAdmissionExpiryTests.cs#L50)

- Prove pre-begin validation and terminal-last handler sequencing.
  [`SubmitCommandHandlerIdempotencyAdmissionTests.cs:198`](../../tests/Hexalith.EventStore.Server.Tests/Pipeline/SubmitCommandHandlerIdempotencyAdmissionTests.cs#L198)

- Prove deletion cannot interleave with registered admission or governed purge.
  [`IdempotencyTenantLifecycleActorTests.cs:68`](../../tests/Hexalith.EventStore.Server.Tests/Actors/IdempotencyTenantLifecycleActorTests.cs#L68)

- Inspect Redis after application and sidecar restart for tombstone-only persistence.
  [`IdempotencyAdmissionExpiryLiveSidecarTests.cs:18`](../../tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Actors/IdempotencyAdmissionExpiryLiveSidecarTests.cs#L18)
