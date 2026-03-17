# Story 4.3: Per-Aggregate Backpressure

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **platform developer**,
I want backpressure applied when aggregate command queues grow too deep,
So that saga storms and head-of-line blocking cascades are prevented.

## Acceptance Criteria

1. **Backpressure rejects commands when pending count exceeds threshold** — Given an aggregate whose non-terminal command count (currently processing + drain-pending `UnpublishedEventsRecord`s) reaches the configurable depth threshold (default: 100), When a new command targets that aggregate and passes tenant validation, Then the actor rejects the command immediately with a backpressure indicator BEFORE state rehydration and domain invocation (FR67). The rejection is fast (no state rehydration, no domain invocation).

2. **HTTP 429 with Retry-After header returned** — Given the actor rejects a command due to backpressure, When the rejection propagates through the MediatR pipeline to the API layer, Then the CommandApi returns HTTP 429 Too Many Requests with a `Retry-After` header (default: 10 seconds) and RFC 7807 ProblemDetails body including `correlationId`, `tenantId`, and the aggregate identity (FR67).

3. **Backpressure is per-aggregate, not system-wide** — Given two aggregates A and B where aggregate A has 150 pending commands and aggregate B has 0, When a new command targets aggregate B, Then aggregate B accepts the command normally. Aggregate A rejects. Backpressure is isolated to the overloaded aggregate.

4. **Pending command counter tracks non-terminal pipeline states** — Given the `AggregateActor` processes commands through the pipeline, When a command passes tenant validation and the backpressure check, Then a `pending_commands_count` integer in actor state is incremented **in the same `SaveStateAsync` batch as the `Processing` checkpoint** (ensuring atomicity — no window where counter is incremented but pipeline hasn't started). When a command reaches terminal state (`Completed`, `Rejected`, or drain-success removes an `UnpublishedEventsRecord`), the counter is decremented and persisted. The counter reflects: currently-processing commands (0 or 1 in turn-based actor) plus drain-pending commands (`PublishFailed` awaiting recovery).

5. **Counter survives actor deactivation and restart** — Given the pending command counter is persisted in actor state via `IActorStateManager`, When the actor deactivates (idle timeout) and reactivates, Then the counter retains its value. No commands are silently lost from the count.

6. **Drain success decrements counter** — Given a drain reminder fires and `DrainUnpublishedEventsAsync` successfully re-publishes events, When the drain record is removed and the reminder unregistered, Then the pending command counter is decremented by 1. The aggregate becomes eligible for new commands as drain backlog clears.

7. **Counter initialization on first use** — Given a new aggregate with no prior state, When the first command arrives and the actor reads the pending count from state, Then a missing state key returns 0 (no pending commands). No initialization ceremony required.

8. **Configurable threshold via BackpressureOptions** — Given `BackpressureOptions` bound to `EventStore:Backpressure` configuration section, Then `MaxPendingCommandsPerAggregate` (default: 100) and `RetryAfterSeconds` (default: 10) are configurable. Options validated at startup (`MaxPendingCommandsPerAggregate > 0`, `RetryAfterSeconds > 0`).

9. **Idempotent commands bypass backpressure** — Given a duplicate command (same CausationId) arrives at an aggregate under backpressure, When the idempotency check finds a cached result, Then the cached result is returned immediately WITHOUT checking backpressure. Idempotency check runs BEFORE backpressure check.

10. **Crash recovery does not corrupt counter** — Given the actor crashes after incrementing the counter but before reaching terminal state, When the actor reactivates and processes the next command, Then the resume path (existing `ResumeFromEventsStoredAsync`) handles the in-flight command correctly. The counter is NOT double-incremented for resumed commands (the previous increment is still valid).

11. **Structured logging for backpressure events** — Given a command is rejected due to backpressure, When the rejection occurs, Then a Warning-level structured log entry is emitted with: `ActorId`, `CorrelationId`, `TenantId`, `Domain`, `AggregateId`, `PendingCount`, `Threshold`, `Stage=BackpressureRejected`.

12. **All existing tests pass** — All Tier 1 (baseline: >= 659) and Tier 2 (baseline: >= 1387 total, >= 1366 mocked) tests continue to pass. No regressions from backpressure implementation.

### Definition of Done

This story is complete when: all 12 ACs are implemented and tested, backpressure correctly prevents saga storms from overwhelming individual aggregates, the HTTP 429 response follows the existing ProblemDetails pattern (matching the per-tenant rate limiter), and no regressions exist in Tier 1 or Tier 2 suites.
