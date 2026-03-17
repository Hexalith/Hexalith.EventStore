# Story 4.3: Per-Aggregate Backpressure

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **platform developer**,
I want backpressure applied when aggregate command queues grow too deep,
So that saga storms and head-of-line blocking cascades are prevented.

## Acceptance Criteria

1. **Configurable depth threshold** — Given a `BackpressureOptions` configuration bound to `EventStore:Backpressure`, Then a `MaxPendingCommandsPerAggregate` setting exists with default value 100 (FR67). Setting to 0 disables backpressure entirely.

2. **HTTP 429 on threshold breach** — Given an aggregate with in-flight commands exceeding the configurable depth threshold (default: 100 pending commands), When a new command targets that aggregate, Then the system returns HTTP 429 with `Retry-After` header (FR67).

3. **Per-aggregate scope** — Given backpressure is triggered for aggregate A, When commands target aggregate B (same or different tenant/domain), Then aggregate B's commands are NOT blocked (FR67). Backpressure is per-aggregate, not system-wide.

4. **ProblemDetails response format** — Given a backpressure 429 response, Then it follows RFC 9457 ProblemDetails format with `type` URI `https://hexalith.io/problems/backpressure-exceeded` (D5, UX-DR7), `title` "Too Many Requests", `detail` explaining per-aggregate backpressure, `correlationId` extension, and `Retry-After` header.

5. **Counter accuracy** — Given commands are in-flight for an aggregate, When a command completes (success, rejection, failure, or exception), Then the in-flight counter for that aggregate is decremented. No counter leak on any code path (success, rejection, exception, cancellation).

6. **Backpressure does not reach actor** — Given backpressure is triggered, When the 429 is returned, Then the command never reaches the AggregateActor (no actor proxy call, no status write, no idempotency record).

7. **No status pollution** — Given a command is rejected by backpressure, Then no `CommandStatusRecord` with status `Received` is written for that command. The command was never accepted into the pipeline.

8. **Error reference documentation** — Given the backpressure error type URI, When resolved, Then an error reference page documents the error, example JSON, and resolution steps (consistent with existing `/problems/{errorType}` pages).

9. **DI registration** — Given the EventStore server is configured, Then `BackpressureOptions` is bound from configuration, `IBackpressureTracker` is registered as singleton, and the tracker is injected into `SubmitCommandHandler`.

10. **All existing tests pass** — All Tier 1 (baseline: >= 659) and Tier 2 (baseline: >= 1387 total, >= 1366 passed mocked) tests continue to pass.

### Definition of Done

This story is complete when: all 10 ACs are verified, new components have unit/integration tests, and no regressions exist in Tier 1 or Tier 2 suites.

## Cross-Story Dependencies

- **Story 4.1 (CloudEvents Publication & Topic Routing)** — done. Established EventPublisher patterns.
- **Story 4.2 (Resilient Publication & Backlog Draining)** — done. Established drain infrastructure. Backpressure is complementary — it prevents queue buildup; drain handles publication failures.
- **Story 3.5 (Concurrency, Auth & Infrastructure Error Responses)** — done. Established the `IExceptionHandler` pattern for 409/503/etc. Backpressure exception handler follows the same pattern.
- **Story 3.6 (OpenAPI Specification & Swagger UI)** — done. Error reference endpoint pattern established. Backpressure error reference follows the same pattern.
- **Epic 5 (Security)** — future. Backpressure check runs AFTER authentication/authorization in the middleware pipeline. Rate limiting (D8, per-tenant) and backpressure (per-aggregate) are independent and can both apply.
- **Epic 7 Story 7.2/7.3 (Rate Limiting)** — future. Per-tenant (D8) rate limiting is already implemented at the API middleware layer. Per-aggregate backpressure is different: it operates at the SubmitCommandHandler level, inside the MediatR pipeline, and targets individual aggregate queue depth rather than tenant-wide request rates.

## Tasks / Subtasks

- [x] Task 1: Create `BackpressureOptions` configuration (AC: #1, #9)
  - [x]1.1 Create `src/Hexalith.EventStore.Server/Configuration/BackpressureOptions.cs` — record with `MaxPendingCommandsPerAggregate` (default: 100), following `EventDrainOptions` pattern
  - [x]1.2 Register `BackpressureOptions` in `AddEventStoreServer()` bound to `EventStore:Backpressure` configuration section
  - [x]1.3 Add `ValidateBackpressureOptions : IValidateOptions<BackpressureOptions>` in the SAME file as `BackpressureOptions` to validate `MaxPendingCommandsPerAggregate >= 0` (0 = disabled/unlimited, >= 1 = active threshold). Follow `RateLimitingOptions.cs` pattern where both record and validator are in one file. Register with `.ValidateOnStart()` in `AddEventStoreServer()`.

- [x] Task 2: Create `IBackpressureTracker` and `InMemoryBackpressureTracker` (AC: #2, #3, #5)
  - [x]2.1 Create `src/Hexalith.EventStore.Server/Commands/IBackpressureTracker.cs` — interface with `bool TryAcquire(string aggregateActorId)` (returns true if under threshold) and `void Release(string aggregateActorId)` (decrements counter)
  - [x]2.2 Create `src/Hexalith.EventStore.Server/Commands/InMemoryBackpressureTracker.cs` — implementation using `ConcurrentDictionary<string, int>` with `Interlocked.Increment`/`Interlocked.Decrement`. Reads threshold from `IOptions<BackpressureOptions>`. `TryAcquire`: if threshold is 0 (disabled), return true immediately; otherwise increment first, check threshold, decrement if over. `Release`: decrement with floor at 0; if counter reaches 0, call `TryRemove` to evict the key and prevent unbounded dictionary growth from abandoned aggregates.
  - [x]2.3 Register `IBackpressureTracker` as singleton in `AddEventStoreServer()` — `services.TryAddSingleton<IBackpressureTracker, InMemoryBackpressureTracker>()`

- [x] Task 3: Create `BackpressureExceededException` (AC: #2, #6)
  - [x]3.1 Create `src/Hexalith.EventStore.Server/Commands/BackpressureExceededException.cs` — custom exception with `AggregateActorId`, `TenantId`, `CorrelationId`, and `CurrentDepth` properties. These properties are for **server-side structured logging only** — they must NEVER appear in the client-facing 429 response (UX-DR10). Follow `ConcurrencyConflictException` class structure but note: this exception does NOT need serialization attributes since it is never serialized across DAPR boundaries.

- [x] Task 4: Integrate backpressure check into `SubmitCommandHandler` (AC: #2, #3, #5, #6, #7)
  - [x]4.1 Add `IBackpressureTracker` parameter to `SubmitCommandHandler` constructor (primary constructor pattern — add after existing `ICommandRouter` parameter)
  - [x]4.2 In `Handle()`, as the FIRST action after `ArgumentNullException.ThrowIfNull(request)`: compute `actorId` via `new AggregateIdentity(request.Tenant, request.Domain, request.AggregateId).ActorId` (same construction as `CommandRouter.RouteCommandAsync` line 28, ensures canonical key).
  - [x]4.3 **CRITICAL: Acquire INSIDE try block to prevent cancellation leak.** Use this pattern:
    ```csharp
    string actorId = new AggregateIdentity(request.Tenant, request.Domain, request.AggregateId).ActorId;
    bool acquired = false;
    try
    {
        acquired = backpressureTracker.TryAcquire(actorId);
        if (!acquired)
        {
            throw new BackpressureExceededException(actorId, request.Tenant, request.CorrelationId, /* currentDepth */);
        }
        // ... existing status write, archive, commandRouter.RouteCommandAsync() ...
    }
    finally
    {
        if (acquired)
        {
            backpressureTracker.Release(actorId);
        }
    }
    ```
    The `acquired` flag ensures `Release` is only called if `TryAcquire` returned true. If `OperationCanceledException` fires between `TryAcquire` and the `try` block entry, the counter would leak — placing acquire INSIDE the try block eliminates this race. The `if (acquired)` guard prevents decrementing when backpressure rejects (TryAcquire returned false → exception thrown → finally runs with acquired=false).
  - [x]4.4 Add structured log (EventId=1110) for backpressure rejection: `Log.BackpressureExceeded(logger, request.CorrelationId, request.MessageId, request.Tenant, request.Domain, request.AggregateId, request.CommandType, actorId)` with `Stage=BackpressureExceeded`
  - [x]4.5 **Pipeline flow after change:**
    ```
    SubmitCommandHandler.Handle():
      1. Compute actorId from AggregateIdentity
      2. acquired = false
      3. try {
           acquired = tracker.TryAcquire(actorId)  ← INSIDE try
           if !acquired: throw BackpressureExceededException (no status, no archive, no actor)
           status write → archive → commandRouter.RouteCommandAsync()
         } finally {
           if (acquired) tracker.Release(actorId)
         }
    ```

- [x] Task 5: Create `BackpressureExceptionHandler` (AC: #2, #4)
  - [x]5.1 Create `src/Hexalith.EventStore.CommandApi/ErrorHandling/BackpressureExceptionHandler.cs` — implements `IExceptionHandler`. Check if exception is `BackpressureExceededException` directly (no DAPR unwrap needed — this exception is thrown from `SubmitCommandHandler` inside the MediatR pipeline, NOT from the actor, so it is never wrapped in `ActorMethodInvocationException`). Returns HTTP 429 with ProblemDetails.
  - [x]5.2 ProblemDetails: `Type = ProblemTypeUris.BackpressureExceeded`, `Title = "Too Many Requests"`, `Status = 429`, `Detail = "Too many pending commands for this entity. Please retry after the specified interval."`, extension `correlationId` from HTTP context (`CorrelationIdMiddleware.HttpContextKey`) with fallback to exception's `CorrelationId` property if HTTP context key is missing. Add `Retry-After: 1` header (short interval — backpressure is transient, in-flight commands complete quickly).
  - [x]5.3 **Security:** Do NOT include `aggregateId`, `actorId`, `tenantId`, or `CurrentDepth` in the client-facing response (UX-DR10, Rule E6 — no event sourcing terminology or internal details).
  - [x]5.4 **No advisory status write.** Unlike `ConcurrencyConflictExceptionHandler`, do NOT write a `CommandStatusRecord`. The command was never accepted into the pipeline — there should be no status entry. This is intentionally different from the concurrency handler pattern.
  - [x]5.5 Register handler in `CommandApi/Extensions/ServiceCollectionExtensions.cs` — add to `IExceptionHandler` chain (before `GlobalExceptionHandler`, after specific handlers)

- [x] Task 6: Update `ProblemTypeUris` and `ErrorReferenceEndpoints` (AC: #4, #8)
  - [x]6.1 Add `public const string BackpressureExceeded = "https://hexalith.io/problems/backpressure-exceeded";` to `ProblemTypeUris.cs`
  - [x]6.2 Add new `ErrorReferenceModel` to `ErrorReferenceEndpoints.ErrorModels` array: slug `"backpressure-exceeded"`, title `"Backpressure Exceeded"`, status 429, description about per-aggregate command queue depth, example JSON matching the ProblemDetails format, resolution steps: "Wait for the interval specified in the Retry-After response header", "The targeted entity is processing a large number of commands. Spread commands across time or entities."

- [x] Task 7: Unit tests for `InMemoryBackpressureTracker` (AC: #1, #2, #3, #5)
  - [x]7.1 Create `tests/Hexalith.EventStore.Server.Tests/Commands/InMemoryBackpressureTrackerTests.cs`
  - [x]7.2 Test: `TryAcquire_UnderThreshold_ReturnsTrue` — acquire under limit succeeds
  - [x]7.3 Test: `TryAcquire_AtThreshold_ReturnsFalse` — acquire at exact limit fails
  - [x]7.4 Test: `TryAcquire_OverThreshold_ReturnsFalse` — already over limit continues to fail
  - [x]7.5 Test: `Release_DecrementsCounter_AllowsNewAcquire` — release frees a slot
  - [x]7.6 Test: `TryAcquire_DifferentAggregates_Independent` — aggregate A at threshold does not block aggregate B
  - [x]7.7 Test: `Release_BelowZero_FloorsAtZero` — release when count is 0 does not go negative
  - [x]7.8 Test: `TryAcquire_DefaultThreshold100_Allows100` — verify default threshold of 100
  - [x]7.9 Test: `TryAcquire_CustomThreshold_Respected` — custom options are read correctly
  - [x]7.10 Test: `TryAcquire_ConcurrentAccess_ExactlyThresholdSucceed` — fire 100 parallel `Task.Run(() => tracker.TryAcquire(actorId))` with threshold 50. Assert exactly 50 return true and 50 return false. Validates thread safety of the increment-check-rollback pattern under contention.
  - [x]7.11 Test: `Release_ToZero_RemovesDictionaryEntry` — acquire then release, verify the internal dictionary no longer contains the key (prevents unbounded memory growth from abandoned aggregates)
  - [x]7.12 Test: `TryAcquire_ThresholdZero_AlwaysReturnsTrue` — with `MaxPendingCommandsPerAggregate = 0` (disabled), TryAcquire returns true unconditionally regardless of call count

- [x] Task 8: Unit tests for `SubmitCommandHandler` backpressure integration (AC: #2, #5, #6, #7)
  - [x]8.1 Create `tests/Hexalith.EventStore.Server.Tests/Pipeline/SubmitCommandHandlerBackpressureTests.cs` — uses NSubstitute mocks for `ICommandStatusStore`, `ICommandArchiveStore`, `ICommandRouter`, `IBackpressureTracker`
  - [x]8.2 Test: `Handle_BackpressureExceeded_ThrowsBackpressureExceededException` — when tracker.TryAcquire returns false
  - [x]8.3 Test: `Handle_BackpressureExceeded_DoesNotCallCommandRouter` — verify commandRouter.RouteCommandAsync is never called
  - [x]8.4 Test: `Handle_BackpressureExceeded_DoesNotWriteStatus` — verify statusStore.WriteStatusAsync is never called (AC #7)
  - [x]8.5 Test: `Handle_BackpressureExceeded_DoesNotArchiveCommand` — verify archiveStore.WriteCommandAsync is never called
  - [x]8.6 Test: `Handle_Success_ReleasesBackpressure` — counter is released after successful processing
  - [x]8.7 Test: `Handle_RouterThrows_ReleasesBackpressure` — counter is released even when commandRouter throws
  - [x]8.8 Test: `Handle_UnderThreshold_CallsRouter` — normal flow proceeds when under threshold
  - [x]8.9 Test: `Handle_Cancelled_ReleasesBackpressure` — cancel the CancellationToken during processing, verify tracker.Release is still called (acquired=true path in finally)

- [x] Task 8b: Update existing `SubmitCommandHandler` tests (AC: #10)
  - [x]8b.1 Find existing test files that construct `SubmitCommandHandler` (search for `SubmitCommandHandler` in `tests/` directory). Since `SubmitCommandHandler` gains a new `IBackpressureTracker` constructor parameter, all existing tests must be updated to provide a mock tracker that always returns `true` from `TryAcquire`.
  - [x]8b.2 Provide a default mock: `var tracker = Substitute.For<IBackpressureTracker>(); tracker.TryAcquire(Arg.Any<string>()).Returns(true);`

- [x] Task 9: Unit tests for `BackpressureExceptionHandler` (AC: #4)
  - [x]9.1 Create `tests/Hexalith.EventStore.Server.Tests/ErrorHandling/BackpressureExceptionHandlerTests.cs` — uses `DefaultHttpContext` (available in Server.Tests via existing `Microsoft.AspNetCore` references). Follow existing handler test pattern in `Server.Tests/ErrorHandling/` (e.g., `DaprSidecarUnavailableHandlerTests.cs`, `ValidationExceptionHandlerTests.cs`) and `Server.Tests/Commands/ConcurrencyConflictExceptionHandlerTests.cs`.
  - [x]9.2 Test: `TryHandleAsync_BackpressureExceededException_Returns429WithProblemDetails`
  - [x]9.3 Test: `TryHandleAsync_BackpressureExceededException_IncludesRetryAfterHeader`
  - [x]9.4 Test: `TryHandleAsync_BackpressureExceededException_IncludesCorrelationId`
  - [x]9.5 Test: `TryHandleAsync_BackpressureExceededException_DoesNotExposeInternalDetails` — response body must NOT contain aggregateId, actorId, tenantId, or CurrentDepth
  - [x]9.6 Test: `TryHandleAsync_NonBackpressureException_ReturnsFalse`
  - [x]9.7 Verify: `BackpressureExceptionHandler` constructor has NO `ICommandStatusStore` dependency — the handler must not write any advisory status (unlike `ConcurrencyConflictExceptionHandler`). This is a design constraint, not a runtime test.
  - [x]9.8 Test: `BackpressureExceptionHandler_IsRegisteredInDI` — build a service provider from `CommandApi` DI registration and verify `BackpressureExceptionHandler` is resolvable from the `IExceptionHandler` collection. Prevents silent handler registration omissions.

- [x] Task 10: Final verification (AC: #10)
  - [x]10.1 Run `dotnet build Hexalith.EventStore.slnx --configuration Release` — zero warnings
  - [x]10.2 Run all Tier 1 tests — pass count >= 659
  - [x]10.3 Run all Tier 2 tests — pass count >= 1366 mocked tests
  - [x]10.4 Report final test count delta

## Dev Notes

### Architecture Compliance

- **FR67:** Per-aggregate backpressure at configurable depth (default 100), returning HTTP 429 with Retry-After. Per-aggregate, not system-wide.
- **D5 (ProblemDetails):** All error responses use RFC 9457 ProblemDetails with `type` URI, `correlationId` extension.
- **D8 (Rate Limiting):** Per-tenant rate limiting (existing, at API middleware level) and per-aggregate backpressure (this story, at SubmitCommandHandler level inside MediatR pipeline) are INDEPENDENT and COMPLEMENTARY. A request can pass rate limiting but be rejected by backpressure, or vice versa. They operate at different layers.
- **Rule #12 (Advisory Status):** Backpressure rejection occurs BEFORE the command enters the pipeline. No `CommandStatusRecord` is written. No advisory status entry exists for backpressure-rejected commands.
- **Rule #13 (No Stack Traces):** Error responses contain no stack traces in production.
- **UX-DR10 / Rule E6:** No internal details (aggregateId, actorId, tenantId, queue depth) in client-facing 429 response. These are logged server-side.

### Design Decision: In-Memory Tracking

**Why in-memory `ConcurrentDictionary` instead of DAPR state store:**
- Zero additional latency (no state store round-trip per command)
- DAPR actor placement ensures one actor instance per aggregate, but API instances are load-balanced. In-memory tracking counts commands in-flight from THIS API instance.
- For single-instance deployment: counter is fully accurate.
- For multi-instance deployment: each instance tracks its own contribution. Total in-flight across instances may exceed the per-instance threshold. This is an acceptable approximation — the threshold is a protective limit, not a precise quota. The alternative (DAPR state store counter) adds ~5-10ms latency to every command.
- **Future enhancement:** If exact cross-instance accuracy is needed, replace `InMemoryBackpressureTracker` with a `DaprStateBackpressureTracker` behind the same `IBackpressureTracker` interface. No other code changes needed.
- **Configuration changes require restart.** The tracker is a singleton using `IOptions<BackpressureOptions>` (resolved once at startup), consistent with `EventDrainOptions`. Dynamic config updates via `IOptionsMonitor` would add complexity without clear benefit — backpressure threshold changes are rare operational events.
- **Threshold 0 = disabled.** Setting `MaxPendingCommandsPerAggregate = 0` disables backpressure entirely — `TryAcquire` returns true immediately without touching the dictionary. This allows operators to disable the feature during incidents without removing configuration.

### Design Decision: SubmitCommandHandler Integration Point

**Why SubmitCommandHandler, not CommandRouter or API middleware:**
- `SubmitCommandHandler.Handle()` is the FIRST point in the MediatR pipeline that has access to the command's tenant/domain/aggregateId (needed to compute `actorId`).
- Checking in `SubmitCommandHandler` BEFORE the status write and archive ensures AC #7 (no status pollution) — a backpressure-rejected command never gets a `Received` status entry.
- Checking in `CommandRouter` would be TOO LATE — `SubmitCommandHandler` writes `Received` status and archives the command before calling `commandRouter.RouteCommandAsync()`. A rejection from CommandRouter would leave a phantom status entry.
- API middleware level doesn't have access to aggregate identity (it only has the raw HTTP request).
- The canonical `actorId` is computed via `new AggregateIdentity(tenant, domain, aggregateId).ActorId` — the same construction used in `CommandRouter.RouteCommandAsync()` (line 28).

**Pipeline flow with backpressure:**
```
HTTP Request → Rate Limiting (D8, per-tenant) → Auth → MediatR
  → SubmitCommandHandler.Handle():
    1. Compute actorId from AggregateIdentity
    2. acquired = false
    3. try {
         acquired = tracker.TryAcquire(actorId)  ← INSIDE try (cancellation-safe)
         if !acquired: throw BackpressureExceededException (no status, no archive, no actor)
         Write "Received" status (advisory)
         Archive command (advisory)
         commandRouter.RouteCommandAsync() → Actor Proxy → AggregateActor
       } finally {
         if (acquired) tracker.Release(actorId)
       }
```

### Key Source Files to Create/Modify

| Action | File | Purpose |
|--------|------|---------|
| **Create** | `src/Hexalith.EventStore.Server/Configuration/BackpressureOptions.cs` | Configuration record + validator (both in same file, follow `RateLimitingOptions.cs` pattern) |
| **Create** | `src/Hexalith.EventStore.Server/Commands/IBackpressureTracker.cs` | Interface |
| **Create** | `src/Hexalith.EventStore.Server/Commands/InMemoryBackpressureTracker.cs` | In-memory implementation |
| **Create** | `src/Hexalith.EventStore.Server/Commands/BackpressureExceededException.cs` | Custom exception (for structured logging, not DAPR serialization) |
| **Create** | `src/Hexalith.EventStore.CommandApi/ErrorHandling/BackpressureExceptionHandler.cs` | IExceptionHandler — no DAPR unwrap, no status write |
| **Modify** | `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs` | DI registration for BackpressureOptions + IBackpressureTracker |
| **Modify** | `src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs` | Add IBackpressureTracker param + backpressure check as first action |
| **Modify** | `src/Hexalith.EventStore.CommandApi/ErrorHandling/ProblemTypeUris.cs` | Add BackpressureExceeded URI |
| **Modify** | `src/Hexalith.EventStore.CommandApi/OpenApi/ErrorReferenceEndpoints.cs` | Add backpressure error reference |
| **Modify** | `src/Hexalith.EventStore.CommandApi/Extensions/ServiceCollectionExtensions.cs` | Register BackpressureExceptionHandler |
| **Create** | `tests/Hexalith.EventStore.Server.Tests/Commands/InMemoryBackpressureTrackerTests.cs` | Tracker unit tests (Tier 1) |
| **Create** | `tests/Hexalith.EventStore.Server.Tests/Pipeline/SubmitCommandHandlerBackpressureTests.cs` | Handler backpressure tests (Tier 2) |
| **Create** | `tests/Hexalith.EventStore.Server.Tests/ErrorHandling/BackpressureExceptionHandlerTests.cs` | Exception handler tests (Tier 2, `DefaultHttpContext` available) |
| **Modify** | Existing `SubmitCommandHandler` test files | Add mock `IBackpressureTracker` to constructor calls |

### Existing Patterns to Follow

- **Configuration options:** Follow `EventDrainOptions` (record, no validation) or `RateLimitingOptions` (record + `IValidateOptions`). Use the validation pattern since `MaxPendingCommandsPerAggregate >= 1` is a hard requirement.
- **Exception handler:** Follow `ConcurrencyConflictExceptionHandler` pattern for ProblemDetails structure and `Retry-After` header. Key differences: (1) no DAPR exception unwrap needed (backpressure is thrown from MediatR pipeline, not actor), (2) no advisory status write (command was never accepted).
- **DI registration:** Follow existing pattern in `AddEventStoreServer()` — `TryAddSingleton` for tracker, `Configure<BackpressureOptions>` bound to config section.
- **Structured logging:** Follow existing `partial class Log` pattern with `[LoggerMessage]` attributes and `Stage=` suffix.
- **Test naming:** `{Method}_{Scenario}_{ExpectedResult}` convention.
- **Test framework:** xUnit 2.9.3 / Shouldly 4.3.0 / NSubstitute 5.3.0.

### Anti-Patterns to Avoid

- **Do NOT use DAPR state store for the counter.** The latency cost (~5-10ms per command) is not justified for v1. Use in-memory `ConcurrentDictionary`.
- **Do NOT add backpressure to the AggregateActor itself.** DAPR actors are turn-based (one call at a time). The actor cannot see its own queue depth. Backpressure must be checked BEFORE the actor proxy call.
- **Do NOT expose queue depth, aggregate ID, or actor ID in the 429 response.** These are internal implementation details (UX-DR10, Rule E6).
- **Do NOT reuse `ProblemTypeUris.RateLimitExceeded` for backpressure.** Per-tenant rate limiting and per-aggregate backpressure are distinct error categories with different causes and different resolution guidance.
- **Do NOT write a `Received` CommandStatusRecord before the backpressure check.** The command was never accepted — there should be no phantom status entries.
- **Do NOT add custom retry logic.** DAPR resiliency handles transient failures (Rule #4). Backpressure is a deliberate rejection, not a transient failure.
- **Do NOT leave zero-value entries in the ConcurrentDictionary.** When `Release` decrements to 0, remove the key via `TryRemove`. Without this, the dictionary grows unbounded as unique aggregates are processed over the lifetime of the process.

### Previous Story Intelligence

**Story 4.2 (Resilient Publication & Backlog Draining)** — status: review:
- Verification story pattern with implementation status assessment
- Drain infrastructure confirmed correct: `UnpublishedEventsRecord`, `EventDrainOptions`, `AggregateActor` drain handler
- Test baseline: Tier 1: 659 passed, Tier 2: 1366 passed mocked (1387 total, 21 pre-existing DAPR infrastructure failures)
- 5 code review patches applied: assertion granularity, publish verification, MaxDrainPeriod clamping, boundary tests
- `EventDrainOptions` at `Server/Configuration/EventDrainOptions.cs` — follow same record pattern for `BackpressureOptions`
- `ConcurrencyConflictExceptionHandler` established the exception handler pattern with DAPR unwrap, advisory status, correlation ID extraction
- **Known risk from Story 4.2:** Drain storm on mass recovery — many aggregates draining simultaneously. Backpressure does NOT prevent this (drain uses reminders, not command pipeline). Separate concern.

### Git Intelligence

Recent commits (relevant context):
- `2b71890` — Merge PR #106 for Story 4.2 (resilient publication)
- `e2bc377` — Story 4.2 verification complete
- `50b6e75` — Story 4.1 documentation and sprint status update
- `2698892` — Story 3.6 merge (OpenAPI/Swagger) — established ErrorReferenceEndpoints pattern
- `8839055` — Story 3.5 merge — established ConcurrencyConflictExceptionHandler pattern

### Testing Standards

- **Framework:** xUnit 2.9.3 / Shouldly 4.3.0 / NSubstitute 5.3.0
- **Tier 1 tests** (no external dependencies): `InMemoryBackpressureTrackerTests`
- **Tier 2 tests** (NSubstitute mocks): `SubmitCommandHandlerBackpressureTests` with mocked `ICommandStatusStore`, `ICommandArchiveStore`, `ICommandRouter`, `IBackpressureTracker`; `BackpressureExceptionHandlerTests` with `DefaultHttpContext`
- **Test naming:** `{Method}_{Scenario}_{ExpectedResult}`
- **Existing test impact:** `SubmitCommandHandler` gains a new `IBackpressureTracker` constructor parameter — all existing tests constructing the handler must be updated to pass a mock tracker that returns `true` from `TryAcquire`

### Project Structure Notes

All new files align with existing architecture file tree:
- Configuration options in `Server/Configuration/`
- Command infrastructure in `Server/Commands/`
- Exception handlers in `CommandApi/ErrorHandling/`
- Error references in `CommandApi/OpenApi/`
- Tests mirror source structure in `Server.Tests/Commands/`

No file relocations or restructuring needed.

### References

- [Source: prd.md#FR67] Per-aggregate backpressure (HTTP 429 with Retry-After, default 100 pending)
- [Source: architecture.md#D5] ProblemDetails error response format
- [Source: architecture.md#D8] Per-tenant rate limiting (complementary, different layer)
- [Source: architecture.md#Rule-4] No custom retry — DAPR resiliency only
- [Source: architecture.md#Rule-12] Advisory status — never blocks pipeline
- [Source: architecture.md#Rule-13] No stack traces in production error responses
- [Source: epics.md#Story-4.3] Per-Aggregate Backpressure acceptance criteria
- [Source: ux-design-specification.md#Rule-E5] Retry-After header on retryable errors
- [Source: ux-design-specification.md#Rule-E6] No event sourcing terminology in error responses

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

- Build: 0 warnings, 0 errors
- Tier 1: 659 passed (267 Contracts + 293 Client + 32 Sample + 67 Testing)
- Tier 2: 1414 total, 1414 passed
- Delta: +27 new tests

### Completion Notes List

- Created BackpressureOptions record with ValidateBackpressureOptions validator (MaxPendingCommandsPerAggregate >= 0, default 100, 0 = disabled)
- Created IBackpressureTracker interface and InMemoryBackpressureTracker using ConcurrentDictionary with atomic compare-and-swap spin loops for thread safety
- Created BackpressureExceededException with AggregateActorId, TenantId, CorrelationId, CurrentDepth properties (logging only, not serialized)
- Integrated backpressure check as FIRST action in SubmitCommandHandler.Handle() with try/finally pattern (acquire inside try, release in finally with acquired flag)
- Created BackpressureExceptionHandler returning HTTP 429 with ProblemDetails, Retry-After: 1 header, no internal details exposed (UX-DR10)
- Added BackpressureExceeded URI to ProblemTypeUris and error reference to ErrorReferenceEndpoints
- Registered BackpressureExceptionHandler in exception handler chain (before GlobalExceptionHandler)
- Registered BackpressureOptions, ValidateBackpressureOptions, and IBackpressureTracker in AddEventStoreServer() DI
- Added structured log EventId=1110 for backpressure rejection with Stage=BackpressureExceeded
- Updated 9 existing test files to include mock IBackpressureTracker in SubmitCommandHandler constructor calls
- Created 12 InMemoryBackpressureTracker tests (including concurrent access test)
- Created 8 SubmitCommandHandler backpressure integration tests
- Created 7 BackpressureExceptionHandler tests (including no-internal-details and no-status-store-dependency checks)
- Added DI registration coverage for BackpressureExceptionHandler in CommandApi service registration tests
- Patched backpressure diagnostics to propagate real queue depth via IBackpressureTracker.GetCurrentDepth
- Strengthened backpressure pipeline tests to assert actor-id symmetry for TryAcquire/Release

### Change Log

- 2026-03-17: Implemented Story 4.3 — Per-Aggregate Backpressure (all 10 ACs satisfied, 27 new tests)
- 2026-03-17: Completed code review fixes and final verification (Build + Tier 1 + Server tests all passing with Dapr prerequisites available)

### File List

**Created:**
- src/Hexalith.EventStore.Server/Configuration/BackpressureOptions.cs
- src/Hexalith.EventStore.Server/Commands/IBackpressureTracker.cs
- src/Hexalith.EventStore.Server/Commands/InMemoryBackpressureTracker.cs
- src/Hexalith.EventStore.Server/Commands/BackpressureExceededException.cs
- src/Hexalith.EventStore.CommandApi/ErrorHandling/BackpressureExceptionHandler.cs
- tests/Hexalith.EventStore.Server.Tests/Commands/InMemoryBackpressureTrackerTests.cs
- tests/Hexalith.EventStore.Server.Tests/Pipeline/SubmitCommandHandlerBackpressureTests.cs
- tests/Hexalith.EventStore.Server.Tests/ErrorHandling/BackpressureExceptionHandlerTests.cs

**Modified:**
- src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs (DI registration)
- src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs (backpressure check + log)
- src/Hexalith.EventStore.CommandApi/ErrorHandling/ProblemTypeUris.cs (new URI)
- src/Hexalith.EventStore.CommandApi/OpenApi/ErrorReferenceEndpoints.cs (new error model)
- src/Hexalith.EventStore.CommandApi/Extensions/ServiceCollectionExtensions.cs (handler registration)
- tests/Hexalith.EventStore.Server.Tests/Pipeline/SubmitCommandHandlerTests.cs (mock tracker)
- tests/Hexalith.EventStore.Server.Tests/Commands/SubmitCommandHandlerArchiveTests.cs (mock tracker)
- tests/Hexalith.EventStore.Server.Tests/Commands/SubmitCommandHandlerStatusTests.cs (mock tracker)
- tests/Hexalith.EventStore.Server.Tests/Commands/SubmitCommandHandlerRoutingTests.cs (mock tracker)
- tests/Hexalith.EventStore.Server.Tests/Logging/StructuredLoggingCompletenessTests.cs (mock tracker)
- tests/Hexalith.EventStore.Server.Tests/Logging/PayloadProtectionTests.cs (mock tracker)
- tests/Hexalith.EventStore.Server.Tests/Logging/LogLevelConventionTests.cs (mock tracker)
- tests/Hexalith.EventStore.Server.Tests/Logging/CausationIdLoggingTests.cs (mock tracker)
- tests/Hexalith.EventStore.IntegrationTests/CommandApi/ConcurrencyConflictIntegrationTests.cs (mock tracker)
- tests/Hexalith.EventStore.IntegrationTests/CommandApi/ServiceCollectionExtensionsTests.cs (BackpressureExceptionHandler DI registration test)
