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

## Tasks / Subtasks

> **Reconstruction note (R4-A2):** All boxes below are checked because the implementation merged via PR #107 (commits `85f55a4` and `0748651`, merge SHA `c870241`) on 2026-03-17. Citations pinned to HEAD `8028cf2` and verified at reconstruction time. The mapping below covers every Story 4.3 AC #1–#12.

- [x] **Task 1 — Configurable BackpressureOptions and DI registration** (AC: #8)
  - [x] 1.1 `BackpressureOptions` record with `MaxPendingCommandsPerAggregate = 100` and `RetryAfterSeconds = 10` defaults (`src/Hexalith.EventStore.Server/Configuration/BackpressureOptions.cs:9-15`).
  - [x] 1.2 `ValidateBackpressureOptions` startup validator rejecting non-positive values (`src/Hexalith.EventStore.Server/Configuration/BackpressureOptions.cs:21-37`).
  - [x] 1.3 DI registration: `AddOptions<BackpressureOptions>().Bind("EventStore:Backpressure").ValidateOnStart()` and `IValidateOptions<BackpressureOptions>` (`src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs:54-57`).

- [x] **Task 2 — Actor-side Step 2b backpressure check (active enforcement)** (AC: #1, #3, #4, #5, #7, #11)
  - [x] 2.1 `BackpressureCheck` activity name constant `EventStore.Actor.BackpressureCheck` (`src/Hexalith.EventStore.Server/Telemetry/EventStoreActivitySource.cs:42`).
  - [x] 2.2 Step 2b runs after tenant validation, before state rehydration / domain invocation (`src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:184-234`).
  - [x] 2.3 `ReadPendingCommandCountAsync` returns 0 when state key missing (`AggregateActor.cs:806-811`) — AC #7 (no initialization ceremony).
  - [x] 2.4 Fail-open on state-read failure: warning logged, `pendingCount = 0`, command continues (`AggregateActor.cs:197-205`) — covered by `ProcessCommand_BackpressureCheckStateReadFails_FailsOpen` (`tests/Hexalith.EventStore.Server.Tests/Actors/BackpressureTests.cs:319`).
  - [x] 2.5 Threshold compare `pendingCount >= MaxPendingCommandsPerAggregate` returns `CommandProcessingResult(Accepted: false, BackpressureExceeded: true, BackpressurePendingCount, BackpressureThreshold)` (`AggregateActor.cs:208-228`) — covered by `ProcessCommand_PendingCountAtThreshold_Rejected` (`BackpressureTests.cs:128`), `_AboveThreshold_Rejected` (`BackpressureTests.cs:144`), `_BelowThreshold_Accepted` (`BackpressureTests.cs:158`).
  - [x] 2.6 `Log.BackpressureRejected` Warning emitted with `ActorId`, `CorrelationId`, `TenantId`, `Domain`, `AggregateId`, `PendingCount`, `Threshold` (`AggregateActor.cs:209-217`) — AC #11.
  - [x] 2.7 Backpressure rejection occurs BEFORE state rehydration / domain invocation — covered by `ProcessCommand_BackpressureRejected_NoStateRehydrationOrDomainInvocation` (`BackpressureTests.cs:302`).
  - [x] 2.8 Per-aggregate isolation (independent counters per actor id) — covered by `ProcessCommand_DifferentAggregates_IndependentBackpressure` (`BackpressureTests.cs:195`) — AC #3.
  - [x] 2.9 Counter survives actor reactivation via persisted `pending_command_count` state key — covered by `PendingCount_SurvivesActorReactivation` (`BackpressureTests.cs:285`) — AC #5.
  - [x] 2.10 Default-zero on missing state key — covered by `PendingCount_DefaultsToZero_WhenMissing` (`BackpressureTests.cs:267`) — AC #7.
  - [x] 2.11 Tenant validation runs before backpressure (preserves tenant-isolation invariant; tenant mismatch never increments counter) — covered by `ProcessCommand_TenantMismatch_DoesNotTouchPendingCount` (`BackpressureTests.cs:394`) — AC #1 ordering.

- [x] **Task 3 — Counter atomicity and lifecycle (increment / decrement)** (AC: #4, #5, #6, #10)
  - [x] 3.1 Counter increment via `StagePendingCommandCountAsync(pendingCount + 1)` after threshold check passes; staged in same `SaveStateAsync` batch as `Processing` checkpoint (`AggregateActor.cs:230`) — AC #4 atomicity.
  - [x] 3.2 Decrement on terminal completion / rejection paths (`AggregateActor.cs:1018`) — covered by `ProcessCommand_Completed_DecrementsPendingCount` (`BackpressureTests.cs:244`).
  - [x] 3.3 Decrement in `finally` block for success / domain-rejection / tenant-rejection / dead-letter / unhandled-exception paths, gated by `pendingCommandTracked && !drainRecordCreated` (`AggregateActor.cs:544-561`) — AC #4 / #10.
  - [x] 3.4 Decrement on drain success: `DrainUnpublishedEventsAsync` → success branch removes record, decrements counter, unregisters reminder (`AggregateActor.cs:702-708`) — covered by `DrainSuccess_DecrementsPendingCount` (`BackpressureTests.cs:351`) — AC #6.
  - [x] 3.5 Counter floor-at-zero with warning log (`AggregateActor.cs:815-826`) — covers AC #10 crash-recovery resume path (resume not double-incremented; previous increment still valid).
  - [x] 3.6 `ProcessCommand_Accepted_IncrementsPendingCount` proves the increment is observable in state (`BackpressureTests.cs:229`) — AC #4.

- [x] **Task 4 — Idempotent command bypass (idempotency BEFORE backpressure)** (AC: #9)
  - [x] 4.1 Cached idempotency result returned without backpressure check — covered by `ProcessCommand_DuplicateCommand_BypassesBackpressure` (`BackpressureTests.cs:178`).

- [x] **Task 5 — Pipeline-side wiring (actor result → MediatR pipeline → exception)** (AC: #2)
  - [x] 5.1 `CommandProcessingResult` adds `BackpressureExceeded`, `BackpressurePendingCount`, `BackpressureThreshold` `[DataMember]` fields for DAPR-serialized actor return (`src/Hexalith.EventStore.Server/Actors/CommandProcessingResult.cs:23-25`).
  - [x] 5.2 Actor-side exception class `Hexalith.EventStore.Server.Actors.BackpressureExceededException` carries `CorrelationId`, `TenantId`, `Domain`, `AggregateId`, `PendingCount`, `Threshold` (`src/Hexalith.EventStore.Server/Actors/BackpressureExceededException.cs:7-35`).
  - [x] 5.3 Pipeline-side exception class `Hexalith.EventStore.Server.Commands.BackpressureExceededException` (server-side logging fields only — never serialized to client per file XML doc) — see Dev Notes "Why there are two `BackpressureExceededException` classes" for the role split.
  - [x] 5.4 `SubmitCommandHandler` constructs and throws the actor-side variant when actor result carries `BackpressureExceeded: true` (`src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs:157-164`).
  - [x] 5.5 `IBackpressureTracker` + `InMemoryBackpressureTracker` defense-in-depth pipeline-layer counter (`ConcurrentDictionary` + CAS) — present at `src/Hexalith.EventStore.Server/Commands/IBackpressureTracker.cs` and `InMemoryBackpressureTracker.cs`. **NOT registered in production DI** (verified by registration grep — see Verification Status caveat AC #5d). Active enforcement is the actor-level Step 2b check (Task 2). The 4-arg `SubmitCommandHandler` constructor (`SubmitCommandHandler.cs:25-30`) is selected at runtime per default `Microsoft.Extensions.DependencyInjection` longest-resolvable-constructor semantics, since the 5-/6-arg overloads (`SubmitCommandHandler.cs:32-47`) require `IBackpressureTracker` which the container cannot resolve. **Expected runtime behavior, not asserted at runtime by Story 4.3.** Wire-or-delete decision deferred to sibling `post-epic-4-r4a2b-backpressure-tracker-di-decision`.

- [x] **Task 6 — HTTP 429 + RFC 7807 ProblemDetails handler** (AC: #2, #11)
  - [x] 6.1 `BackpressureExceptionHandler : IExceptionHandler` walks inner-exception chain (depth 10) and returns 429 with `application/problem+json` body (`src/Hexalith.EventStore/ErrorHandling/BackpressureExceptionHandler.cs`).
  - [x] 6.2 `Retry-After` header set from `BackpressureOptions.RetryAfterSeconds` (`BackpressureExceptionHandler.cs:44, 62`) — AC #2.
  - [x] 6.3 ProblemDetails body includes `correlationId`, `tenantId`, `domain`, `aggregateId` extensions (`BackpressureExceptionHandler.cs:52-58`) — AC #2.
  - [x] 6.4 ProblemTypeUris constant `BackpressureExceeded = "https://hexalith.io/problems/backpressure-exceeded"` (`src/Hexalith.EventStore/ErrorHandling/ProblemTypeUris.cs:17`).
  - [x] 6.5 Handler registered in global `IExceptionHandler` chain via `AddExceptionHandler<BackpressureExceptionHandler>()` (`src/Hexalith.EventStore/Extensions/ServiceCollectionExtensions.cs:38, 43`). **Observation:** the handler is registered twice (lines 38 and 43); first registration wins per ASP.NET Core handler-chain semantics, second is dead code. Pre-existing condition; not in scope for this documentation-only story (AC #8). Worth noting for a future code-cleanup pass.
  - [x] 6.6 OpenAPI error reference page metadata at `/api/v1/errors/backpressure-exceeded` (`src/Hexalith.EventStore/OpenApi/ErrorReferenceEndpoints.cs:85-88`).
  - [x] 6.7 Published documentation page at `docs/reference/problems/backpressure-exceeded.md`.
  - [x] 6.8 Handler-level coverage: 429 status, `Retry-After` header, ProblemDetails body, correlation+tenant extensions, aggregate identity extension, non-backpressure exception ignored, wrapped exception walked — `tests/Hexalith.EventStore.Server.Tests/ErrorHandling/BackpressureExceptionHandlerTests.cs` (7 tests).

- [x] **Task 7 — BackpressureOptions test coverage** (AC: #8)
  - [x] 7.1 Defaults, configuration binding, four validation rejections, one validation acceptance — `tests/Hexalith.EventStore.Server.Tests/Configuration/BackpressureOptionsTests.cs` (7 tests).

- [x] **Task 8 — Defense-in-depth pipeline tracker test coverage** (Defense-in-depth backstop for AC #1, #3)
  - [x] 8.1 Acquire under/at/over threshold, release/decrement, per-aggregate independence, floor-at-zero, default-100, custom threshold, async concurrency at threshold, dictionary-entry removal at zero, threshold-zero disable — `tests/Hexalith.EventStore.Server.Tests/Commands/InMemoryBackpressureTrackerTests.cs` (11 tests).

- [x] **Task 9 — All existing tests pass (no regressions)** (AC: #12)
  - [x] 9.1 At PR #107 close: full Tier 1 + Tier 2 baselines green — see `## Verification Status` block below for the 2026-04-30 reconstruction baseline-equality re-check (Tier 1 = 788/788; Tier 2 = 1620 pass with infra-class skips on Docker absence).

## Dev Notes

### Testing Standards (project-wide rules — apply to every story)

> Reproduced verbatim from `.claude/skills/bmad-create-story/template.md` lines 30–34 (read-only access; pinned to HEAD `8028cf2`). Per-rule applicability annotations follow each rule.

- **Tier 1 (Unit):** xUnit 2.9.3 + Shouldly + NSubstitute. No DAPR runtime, no Docker.
  - **Applicable** — `BackpressureOptionsTests.cs`, `InMemoryBackpressureTrackerTests.cs`, `BackpressureExceptionHandlerTests.cs` are mock-based and compile/run without DAPR runtime, even though physically housed in the `Server.Tests` project.
- **Tier 2 / Tier 3 (Integration) — REQUIRED end-state inspection:** If the story creates or modifies Tier 2 (`Server.Tests`) or Tier 3 (`IntegrationTests`) tests, each test MUST inspect state-store end-state (e.g., Redis key contents, persisted `EventEnvelope`, CloudEvent body, advisory status record). Asserting only API return codes, mock call counts, or pub/sub call invocations is forbidden — that is an API smoke test, not an integration test. *Reference:* Epic 2 retro R2-A6; precedent fixes in Story 2.1 (`CommandRoutingIntegrationTests` missing `messageId`) and Story 2.2 (persistence integration test rewrote to inspect Redis directly).
  - **Applicable** — `BackpressureTests.cs` (Tier 2 mocked actor) inspects actor state-manager mock keys and pending-count values, not just API return codes; Tier 3 backpressure path **not** exercised by Story 4.3 — see Verification Status caveat (AC #5c) and `post-epic-4-r4a5-tier3-pubsub-delivery`.
- **ID validation:** Any controller / validator handling `messageId`, `correlationId`, `aggregateId`, or `causationId` MUST use `Ulid.TryParse` (or accept any non-whitespace string per `AggregateIdentity` rules). `Guid.TryParse` on these fields is forbidden. *Reference:* Epic 2 retro R2-A7; precedent fix in Story 2.4 `CommandStatusController`.
  - **Not applicable** — Story 4.3 does not introduce a new controller or validator surface; the HTTP 429 path is exception-handler-driven and reads `correlationId` from `CorrelationIdMiddleware.HttpContextKey`.

### Why there are two `BackpressureExceededException` classes (and neither is dead code)

There are two separately-namespaced exceptions: `Hexalith.EventStore.Server.Actors.BackpressureExceededException` and `Hexalith.EventStore.Server.Commands.BackpressureExceededException`. **Both are live; neither is dead code.** A future agent doing a "dedupe similar types" cleanup pass MUST NOT delete either without understanding the role split.

- **Actor-side variant** (`Server/Actors/BackpressureExceededException.cs`). Carries `CorrelationId`, `TenantId`, `Domain`, `AggregateId`, `PendingCount`, `Threshold`. **Not** thrown by the actor itself — the actor returns `CommandProcessingResult(BackpressureExceeded: true, ...)` instead. The actor-side class exists as the **target shape** that `SubmitCommandHandler` constructs from the actor result and throws into the MediatR pipeline (`SubmitCommandHandler.cs:157-164`). Naming aligns with the `BackpressureCheck` activity name (`EventStore.Actor.BackpressureCheck`) and the actor-result fields.
- **Pipeline-side variant** (`Server/Commands/BackpressureExceededException.cs`). Pipeline-thrown variant carrying `AggregateActorId`, `TenantId?`, `CorrelationId`, `CurrentDepth`. Per its own XML doc: properties are for server-side structured logging only — they MUST NEVER appear in the client-facing 429 response (UX-DR10, Rule E6). Not serialized across DAPR boundaries (thrown from `SubmitCommandHandler` inside the MediatR pipeline, not from the actor) — no serialization attributes required.
- **Why both exist.** The two classes evolved across the two implementation commits (`85f55a4` introduced the actor-side; `0748651` introduced the pipeline-side). They occupy different namespaces, carry different field sets, and serve different concerns (actor-result-derived vs pipeline-tracker-derived). A future redesign could collapse them, but that's a deliberate design decision, not a deduplication mechanic.

### Sibling cross-references inside the post-Epic-4 cleanup family

| Action | Status | Routing |
|---|---|---|
| R4-A1 | Resolved | Applied directly to `4-2-resilient-publication-and-backlog-draining.md`; no new story. |
| R4-A2 | This reconstruction (review pending) | `post-epic-4-r4a2-story-4-3-execution-record`. |
| R4-A2b | Backlog (party-mode-review carve-out, 2026-04-30) | `post-epic-4-r4a2b-backpressure-tracker-di-decision`. Makes the binary wire-or-delete decision for `IBackpressureTracker` (the AC #5d caveat). Promotion trigger: next backpressure-touching code change OR 2026-05-28 (whichever first). |
| R4-A3 | Done | Covered by `post-epic-3-r3a1-replay-ulid-validation` (`done` 2026-04-28). |
| R4-A4 | Done | Covered by `post-epic-3-r3a7-live-command-surface-verification` (`done` 2026-04-30). |
| R4-A5 | Backlog | `post-epic-4-r4a5-tier3-pubsub-delivery`. Owns the live Tier 3 backpressure verification this reconstruction records as a caveat. |
| R4-A6 | Backlog | `post-epic-4-r4a6-drain-integrity-guard`. |
| R4-A7 | Routed | Covered by `post-epic-1-r1a1-aggregatetype-pipeline` (`done`) + `post-epic-2-r2a2-commandstatus-isterminal-extension` (`done`). |
| R4-A8 | Backlog | `post-epic-4-r4a8-story-numbering-comments`. **Explicitly NOT in scope for R4-A2** — source-comment edits live in their own backlog story. |

Source: `_bmad-output/implementation-artifacts/sprint-status.yaml` rows under the `# Post-Epic-4 Retro Cleanup` section header; `_bmad-output/implementation-artifacts/epic-4-retro-2026-04-26.md` §8.

### Project Structure Notes

Production source files touched by Story 4.3 (read-only at reconstruction time; commits `85f55a4` and `0748651`):

- `src/Hexalith.EventStore.Server/Configuration/`
  - `BackpressureOptions.cs` — options record + startup validator
  - `ServiceCollectionExtensions.cs` — DI registration of `BackpressureOptions` (lines 54–57)
- `src/Hexalith.EventStore.Server/Actors/`
  - `AggregateActor.cs` — Step 2b check, counter increment/decrement, drain-success decrement, helpers
  - `BackpressureExceededException.cs` — actor-side variant
  - `CommandProcessingResult.cs` — `BackpressureExceeded` / `BackpressurePendingCount` / `BackpressureThreshold` `[DataMember]` fields
- `src/Hexalith.EventStore.Server/Commands/`
  - `BackpressureExceededException.cs` — pipeline-side variant (server-side logging only, not client-serialized)
  - `IBackpressureTracker.cs` — defense-in-depth interface
  - `InMemoryBackpressureTracker.cs` — `ConcurrentDictionary` + CAS implementation (NOT registered in production DI)
- `src/Hexalith.EventStore.Server/Pipeline/`
  - `SubmitCommandHandler.cs` — two new constructor overloads, `BackpressureExceeded` flag throw site (lines 157–164)
- `src/Hexalith.EventStore.Server/Telemetry/`
  - `EventStoreActivitySource.cs` — `BackpressureCheck` activity name constant (line 42)
- `src/Hexalith.EventStore/ErrorHandling/`
  - `BackpressureExceptionHandler.cs` — `IExceptionHandler` returning HTTP 429 + RFC 7807
  - `ProblemTypeUris.cs` — `BackpressureExceeded` URI constant (line 17)
- `src/Hexalith.EventStore/Extensions/`
  - `ServiceCollectionExtensions.cs` — `AddExceptionHandler<BackpressureExceptionHandler>()` registration (lines 38 and 43; duplicate registration observed)
- `src/Hexalith.EventStore/OpenApi/`
  - `ErrorReferenceEndpoints.cs` — `/api/v1/errors/backpressure-exceeded` reference page metadata (lines 85–88)
- `docs/reference/problems/`
  - `backpressure-exceeded.md` — published documentation page

Test files (Tier 2, mock-based — no DAPR runtime required):

- `tests/Hexalith.EventStore.Server.Tests/Actors/BackpressureTests.cs` — 13 actor-level tests
- `tests/Hexalith.EventStore.Server.Tests/Configuration/BackpressureOptionsTests.cs` — 7 options tests
- `tests/Hexalith.EventStore.Server.Tests/Commands/InMemoryBackpressureTrackerTests.cs` — 11 tracker tests (defense-in-depth)
- `tests/Hexalith.EventStore.Server.Tests/ErrorHandling/BackpressureExceptionHandlerTests.cs` — 7 handler tests

### References

- [Source: `src/Hexalith.EventStore.Server/Configuration/BackpressureOptions.cs:9-37`] — AC #8: options + validator
- [Source: `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs:54-57`] — AC #8: DI registration
- [Source: `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:184-234`] — AC #1, #3, #4, #7, #11: Step 2b active enforcement
- [Source: `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:230`] — AC #4: counter increment via `StagePendingCommandCountAsync(pendingCount + 1)`
- [Source: `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:544-561`] — AC #4: finally-block decrement on terminal paths
- [Source: `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:702-708`] — AC #6: drain-success decrement
- [Source: `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:806-826`] — AC #5, #7, #10: counter persistence helpers (read / stage / decrement with floor)
- [Source: `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:1018`] — AC #4: terminal completion decrement
- [Source: `src/Hexalith.EventStore.Server/Actors/CommandProcessingResult.cs:23-25`] — AC #1: DAPR-serialized actor-result fields
- [Source: `src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs:32-47`] — AC #2: constructor overloads accepting `IBackpressureTracker`
- [Source: `src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs:157-164`] — AC #2: pipeline throw site
- [Source: `src/Hexalith.EventStore.Server/Telemetry/EventStoreActivitySource.cs:42`] — AC #1: `BackpressureCheck` activity constant
- [Source: `src/Hexalith.EventStore/ErrorHandling/BackpressureExceptionHandler.cs`] — AC #2, #11: HTTP 429 + RFC 7807 handler
- [Source: `src/Hexalith.EventStore/ErrorHandling/ProblemTypeUris.cs:17`] — AC #2: ProblemDetails type URI constant
- [Source: `src/Hexalith.EventStore/Extensions/ServiceCollectionExtensions.cs:38, 43`] — AC #2: handler registration (duplicate observed)
- [Source: `src/Hexalith.EventStore/OpenApi/ErrorReferenceEndpoints.cs:85-88`] — AC #2: error reference page metadata
- [Source: `docs/reference/problems/backpressure-exceeded.md`] — AC #2: published consumer-facing documentation
- [Source: `tests/Hexalith.EventStore.Server.Tests/Actors/BackpressureTests.cs`] — AC #1, #3, #4, #5, #6, #7, #9, #10
- [Source: `tests/Hexalith.EventStore.Server.Tests/Configuration/BackpressureOptionsTests.cs`] — AC #8
- [Source: `tests/Hexalith.EventStore.Server.Tests/Commands/InMemoryBackpressureTrackerTests.cs`] — defense-in-depth backstop
- [Source: `tests/Hexalith.EventStore.Server.Tests/ErrorHandling/BackpressureExceptionHandlerTests.cs`] — AC #2, #11
- [Source: `_bmad-output/implementation-artifacts/epic-4-retro-2026-04-26.md`] — §6 / §8 / §11: R4-A2 driver
- [Source: `_bmad-output/implementation-artifacts/sprint-status.yaml`] — `# Post-Epic-4 Retro Cleanup` block: R4-A2 + R4-A2b sibling carve-out
- [Source: `_bmad-output/implementation-artifacts/post-epic-4-r4a2-story-4-3-execution-record.md`] — reconstruction spec authoring this section
- [Source: `git log 85f55a4`] — original Story 4.3 commit; co-author trailer for Dev Agent Record
- [Source: `git log 0748651`] — pipeline-side hardening commit
- [Source: `git log c870241`] — PR #107 merge SHA

## Dev Agent Record

### Agent Model Used

original implementation: Claude Opus 4.6 (1M context); documentation reconstruction: Claude Opus 4.7 (1M context)

### Completion Notes List

- All 12 ACs of Story 4.3 implemented at PR #107 (commit `85f55a4` initial + `0748651` pipeline hardening; merge SHA `c870241`, 2026-03-17). Citations re-anchored against HEAD `8028cf2` at reconstruction time and verified.
- 38 backpressure-specific tests across 4 test files (method-name grep: 13 + 7 + 11 + 7 = 38). Test-runner output for the 4 files combined: 38 passed / 0 failed / 0 skipped — equal to grep count, no `[Theory]` row expansion.
- This reconstruction (R4-A2) is documentation-only per AC #8: zero source/test diff. AC #11 baseline-equality re-run executed at story close: Tier 1 = 788/788 unchanged; Tier 2 = 1620 pass / 25 infrastructure-class fail (Docker not running on this host — same Docker-absence pattern as `post-epic-3-r3a6` close note) / 1645 total — matches baseline. No regression.
- **Caveat (AC #5c):** Tier 3 (`tests/Hexalith.EventStore.IntegrationTests/`) was NOT exercised by Story 4.3. The `ConcurrencyConflictIntegrationTests.cs:285` reference to `InMemoryBackpressureTracker` is positive-path fixture wiring, not a backpressure assertion. Live-topology backpressure verification is deferred to `post-epic-4-r4a5-tier3-pubsub-delivery`.
- **Caveat (AC #5d):** `IBackpressureTracker` defense-in-depth pipeline tracker ships with full unit tests (11 tests, T3 in inventory) but is NOT registered in production DI as of HEAD `8028cf2`. Confirmed by registration-pattern grep: zero matches in `src/` for `(Try)?Add(Singleton|Scoped|Transient)<.*IBackpressureTracker.*>` and zero matches for the generic-arg pattern. Active enforcement is the actor-level Step 2b check (Task 2 above), which is wired and operational regardless of which `SubmitCommandHandler` constructor the container picks. The 4-arg constructor is expected to win selection per default `Microsoft.Extensions.DependencyInjection` longest-resolvable-constructor semantics. Wire-or-delete decision deferred to sibling `post-epic-4-r4a2b-backpressure-tracker-di-decision` (backlog as of 2026-04-30; promotion triggers per R4-A2 AC #12: next backpressure-touching code change OR 2026-05-28 calendar SLA).
- **Observation (out of scope, not a defect):** `BackpressureExceptionHandler` is registered twice in `src/Hexalith.EventStore/Extensions/ServiceCollectionExtensions.cs` (lines 38 and 43); first registration wins per ASP.NET Core handler-chain semantics, second is dead code. Pre-existing condition; not in scope for this documentation-only story (AC #8). A future code-cleanup pass should consolidate to a single registration.
- **Observation (not a citation defect):** `ErrorReferenceEndpoints.cs` backpressure entry actually spans lines 85–88 (the spec inventory cited 85–87); recorded with the actual span.

### File List

> **Read-only references** — Story 4.3 already shipped these files via PR #107 (commits `85f55a4` and `0748651`). The R4-A2 reconstruction does NOT modify any file in this list.

Production source files (14):

- `src/Hexalith.EventStore.Server/Configuration/BackpressureOptions.cs`
- `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs`
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs`
- `src/Hexalith.EventStore.Server/Actors/BackpressureExceededException.cs`
- `src/Hexalith.EventStore.Server/Actors/CommandProcessingResult.cs`
- `src/Hexalith.EventStore.Server/Commands/BackpressureExceededException.cs`
- `src/Hexalith.EventStore.Server/Commands/IBackpressureTracker.cs`
- `src/Hexalith.EventStore.Server/Commands/InMemoryBackpressureTracker.cs`
- `src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs`
- `src/Hexalith.EventStore.Server/Telemetry/EventStoreActivitySource.cs`
- `src/Hexalith.EventStore/ErrorHandling/BackpressureExceptionHandler.cs`
- `src/Hexalith.EventStore/ErrorHandling/ProblemTypeUris.cs`
- `src/Hexalith.EventStore/Extensions/ServiceCollectionExtensions.cs`
- `src/Hexalith.EventStore/OpenApi/ErrorReferenceEndpoints.cs`

Documentation page (1):

- `docs/reference/problems/backpressure-exceeded.md`

Test files (4):

- `tests/Hexalith.EventStore.Server.Tests/Actors/BackpressureTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Configuration/BackpressureOptionsTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Commands/InMemoryBackpressureTrackerTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/ErrorHandling/BackpressureExceptionHandlerTests.cs`

R4-A2 reconstruction edits (this story — markdown only):

- `_bmad-output/implementation-artifacts/4-3-per-aggregate-backpressure.md` (this file — sections appended after the existing Definition of Done block)
- `_bmad-output/implementation-artifacts/post-epic-4-r4a2-story-4-3-execution-record.md` (R4-A2 spec; status-flipped at Task 5.4)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (R4-A2 row flipped to `review`; R4-A2b sibling carve-out at `backlog` verified to exist; two `last_updated` lines bumped)

## Change Log

| Date | Version | Description | Author |
|---|---|---|---|
| 2026-03-17 | 1.0 | Initial Story 4.3 implementation merged via PR #107 (commit `85f55a4`, merge `c870241`). All 12 ACs implemented; 38 backpressure-specific tests added (method-name count) across `BackpressureTests`, `BackpressureOptionsTests`, `InMemoryBackpressureTrackerTests`, `BackpressureExceptionHandlerTests`. | Quentin Dassi Vignon (Co-Authored-By: Claude Opus 4.6 (1M context)) |
| 2026-03-17 | 1.1 | Pipeline-side hardening: pipeline-layer `BackpressureExceededException` variant, `IBackpressureTracker` + `InMemoryBackpressureTracker`, `SubmitCommandHandler` refactor (commit `0748651`). | Jérôme Piquot |
| 2026-04-30 | 1.2 | R4-A2 documentation reconstruction per party-mode-reviewed + advanced-elicitation-patched spec: added Tasks/Subtasks, Dev Notes, Dev Agent Record, File List, Change Log, Verification Status sections. No source/test diff. AC #5d DI-gap caveat recorded with caveat-update obligation on R4-A2b per AC #12; wire-or-delete decision carved out to sibling `post-epic-4-r4a2b-backpressure-tracker-di-decision`. | Quentin Dassi Vignon (dev) + Claude Opus 4.7 (1M context) |

## Verification Status

**Reconstruction baseline-equality re-run** (per R4-A2 AC #11). Story 4.3 ships zero source or test changes in this reconstruction; the table below records the Tier 1 + Tier 2 numbers captured immediately before and immediately after the markdown additions. The pre/post pass counts are equal — the documentation-only invariant (AC #8) holds empirically, not just by mathematical argument.

| Tier | Suite | Pass | Fail | Skipped | Notes |
|---|---|---|---|---|---|
| 1 | `Hexalith.EventStore.Contracts.Tests` | 281 | 0 | 0 | Baseline 2026-04-30; equal to post-story re-run. |
| 1 | `Hexalith.EventStore.Client.Tests` | 334 | 0 | 0 | Baseline 2026-04-30; equal to post-story re-run. |
| 1 | `Hexalith.EventStore.Sample.Tests` | 63 | 0 | 0 | Baseline 2026-04-30; equal to post-story re-run. |
| 1 | `Hexalith.EventStore.Testing.Tests` | 78 | 0 | 0 | Baseline 2026-04-30; equal to post-story re-run. |
| 1 | `Hexalith.EventStore.SignalR.Tests` | 32 | 0 | 0 | Baseline 2026-04-30; equal to post-story re-run. |
| 1 | **Tier 1 total** | **788** | **0** | **0** | Equality with baseline confirmed (AC #5b, AC #11). |
| 2 | `Hexalith.EventStore.Server.Tests` (full) | 1620 | 25 | 0 | The 25 failures are infrastructure-class only — `DaprTestContainerFixture` pre-flight check fails because Redis/DAPR placement/scheduler are not reachable when Docker is not running on the dev host. Same Docker-absence shape recorded in `post-epic-3-r3a6` close note. Not story-related (AC #8 ⇒ no `.cs` in diff). |
| 2 | `Server.Tests` — backpressure filter | 38 | 0 | 0 | AC #5a: `BackpressureTests` 13 + `BackpressureOptionsTests` 7 + `InMemoryBackpressureTrackerTests` 11 + `BackpressureExceptionHandlerTests` 7 = 38; runner count equals method-name grep — no `[Theory]` row expansion. |
| 3 | `Hexalith.EventStore.IntegrationTests` (live Aspire backpressure path) | — | — | — | **NOT exercised by Story 4.3** (AC #5c caveat). See caveat below. |

**Caveat (AC #5c):** The live Aspire / DAPR Tier 3 backpressure path was NOT exercised by Story 4.3. No Tier 3 test in `tests/Hexalith.EventStore.IntegrationTests/` asserts the end-to-end backpressure path against a running Aspire topology. The `ConcurrencyConflictIntegrationTests.cs:285` reference to `InMemoryBackpressureTracker` is positive-path fixture wiring (so the simulating handler still satisfies the constructor signature when the API host is built), not a backpressure assertion. This is a known and intentional carry-over to **`post-epic-4-r4a5-tier3-pubsub-delivery`** — that story is the right place for live-topology backpressure verification. R4-A2 records the gap as an explicit unrun-test caveat; it does not close it.

**Caveat (AC #5d):** The defense-in-depth pipeline-layer `IBackpressureTracker` (interface + `InMemoryBackpressureTracker` implementation under `src/Hexalith.EventStore.Server/Commands/`) is shipped with full unit tests (`InMemoryBackpressureTrackerTests.cs`, 11 tests) but is **not registered in production DI** as of HEAD `8028cf2`. Confirmed at reconstruction time by registration-pattern grep: zero matches in `src/` for `(Try)?Add(Singleton|Scoped|Transient)<.*IBackpressureTracker.*>` and zero matches for the generic-arg `services.Add<...IBackpressureTracker...>` pattern. Active backpressure enforcement is the actor-level Step 2b check (`AggregateActor.cs:184-234`), which is wired and operational regardless of which `SubmitCommandHandler` constructor the container picks; the 4-arg `SubmitCommandHandler` constructor (`SubmitCommandHandler.cs:25-30`) is expected to be selected at runtime per default `Microsoft.Extensions.DependencyInjection` longest-resolvable-constructor semantics, since the 5-/6-arg overloads (`SubmitCommandHandler.cs:32-47`) require `IBackpressureTracker` which the container cannot resolve. **Expected runtime behavior, not asserted at runtime by Story 4.3.** Note also the dual-`BackpressureExceededException` shape (Server.Actors.* and Server.Commands.* — both live; see Dev Notes "Why there are two `BackpressureExceededException` classes"). The wire-or-delete decision for `IBackpressureTracker` is **deliberately carved out** to sibling story `post-epic-4-r4a2b-backpressure-tracker-di-decision` (status `backlog`, added in the same diff that filed R4-A2). Promotion triggers per R4-A2 AC #12: **Trigger A** = next backpressure-touching code change, OR **Trigger B** = 2026-05-28 calendar SLA. R4-A2b carries a hard caveat-update obligation: when R4-A2b ships, the dev MUST update this AC #5d caveat note to reflect the wire-or-delete resolution.
