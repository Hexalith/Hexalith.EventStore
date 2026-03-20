# Story 11.5: Counter Sample /project Endpoint

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer evaluating EventStore,
I want the counter sample domain service to expose a /project endpoint,
so that I have a working reference for how domain services build projection state from events.

## Acceptance Criteria

1. **Given** the Sample domain service,
   **When** a POST `/project` request arrives with `ProjectionRequest`,
   **Then** `CounterProjectionHandler` applies events to in-memory state
   **And** returns `ProjectionResponse { "counter", { "count": N } }`.

2. **Given** `CounterProjectionHandler` receives increment/decrement/reset events,
   **When** it applies them,
   **Then** Count is updated correctly.

3. **Given** the Blazor UI increments the counter,
   **When** the full pipeline executes (command -> events -> /project -> ProjectionActor -> query),
   **Then** the counter value card displays the correct count
   **And** no `QueryNotFoundException` errors appear in commandapi logs.

## Definition of Done

- All 3 ACs verified against actual code
- Build: `dotnet build Hexalith.EventStore.slnx --configuration Release` -- 0 errors, 0 warnings
- Tier 1 tests pass, no regressions
- Branch: `feat/story-11-5-counter-sample-project-endpoint`

## Tasks / Subtasks

- [x] Task 1: Create `CounterProjectionHandler` (AC: 1, 2)
  - [x] Create `samples/Hexalith.EventStore.Sample/Counter/Projections/CounterProjectionHandler.cs`
  - [x] Implement `static ProjectionResponse Project(ProjectionRequest request)` method
  - [x] Create fresh `CounterState`, iterate `request.Events ?? []` (null-coalesce guard -- DAPR deserialization may produce null), apply each event by matching `EventTypeName`
  - [x] Return `ProjectionResponse("counter", JsonSerializer.SerializeToElement(new { count = state.Count }))`
  - [x] Guard against null/empty `EventTypeName` in `ApplyEvent` -- early return, no exception
  - [x] Skip unknown/rejection events gracefully (no exception for `CounterCannotGoNegative` or `AggregateTerminated`)
- [x] Task 2: Wire `/project` endpoint in `Program.cs` (AC: 1, 3)
  - [x] Add real `/project` handler in the `else` branch of the `malformedProjectionResponse` conditional. **CRITICAL: Do NOT remove or flatten the if/else -- the `if (malformedProjectionResponse)` branch is used by Tier 3 fault injection tests. The real handler goes in the `else` only.**
  - [x] Endpoint deserializes `ProjectionRequest` from body, calls `CounterProjectionHandler.Project()`, returns result
  - [x] Verify `Results.Ok(response)` serializes `ProjectionResponse` as flat JSON (not wrapped). If it wraps, use `Results.Json(response)` instead. Add comment noting DAPR `InvokeMethodAsync` round-trip dependency.
  - [x] Add `using` for `Hexalith.EventStore.Contracts.Projections` and `Hexalith.EventStore.Sample.Counter.Projections`
- [x] Task 3: Create unit tests for `CounterProjectionHandler` (AC: 1, 2)
  - [x] Create `tests/Hexalith.EventStore.Sample.Tests/Counter/Projections/CounterProjectionHandlerTests.cs`
  - [x] Test: single increment -> count = 1
  - [x] Test: multiple increments -> count = N
  - [x] Test: increment + decrement -> count = 0
  - [x] Test: increment + reset -> count = 0
  - [x] Test: empty events -> count = 0
  - [x] Test: rejection event (CounterCannotGoNegative) is skipped
  - [x] Test: ProjectionType is "counter"
  - [x] Test: mixed event stream with closed counter -> count reflects pre-close value
  - [x] Test: null/empty EventTypeName in event stream -> skipped gracefully, no exception
  - [x] Test: ProjectionResponse survives JSON serialization round-trip (serialize to string, deserialize back, assert ProjectionType and State.count intact)
  - [x] Test: post-tombstone events in stream (Incremented, Incremented, Closed, AggregateTerminated, Incremented) -> count = 3 (handler does NOT enforce tombstone semantics)
  - [x] Test: null Events array -> count = 0, no exception (DAPR deserialization edge case)
- [x] Task 4: Full build and test verification
  - [x] `dotnet build Hexalith.EventStore.slnx --configuration Release` -- 0 errors, 0 warnings
  - [x] All Tier 1 tests pass (baseline: 709 + new tests)
  - [x] Verify no regressions in existing Sample.Tests
  - [x] AC 3 (full pipeline) is manual/Tier 3 verification only -- do NOT attempt to create an automated test for it in this story. Document how to verify manually in completion notes.

## Dev Notes

### Architecture Context: Server-Managed Projection Builder (Mode B)

This story adds the **counter sample /project endpoint** -- the fifth and final layer of Epic 11. The full pipeline is now complete end-to-end:

```
Command submitted via REST API
  -> AggregateActor processes command, persists events
  -> EventPublisher.PublishEventsAsync (pub/sub)
  -> Task.Run fire-and-forget:
     -> ProjectionUpdateOrchestrator.UpdateProjectionAsync(identity)
        -> Check ProjectionOptions.GetRefreshIntervalMs(domain)
           -> If 0: proceed with immediate trigger
        -> DomainServiceResolver.ResolveAsync (convention: uses DomainServices registration)
        -> AggregateActor.GetEventsAsync(0) -- all events from start (no checkpoint yet)
        -> Map EventEnvelope[] to ProjectionEventDto[]
        -> DaprClient.InvokeMethodAsync("appId", "project", ProjectionRequest)
        -> [THIS STORY] Counter domain service /project endpoint
           -> CounterProjectionHandler.Project(request)
           -> Returns ProjectionResponse("counter", {"count": N})
        -> EventReplayProjectionActor.UpdateProjectionAsync(ProjectionState)
        -> ETag regenerated -> SignalR broadcast -> UI refreshes
```

**Story 11-1 provided:** `ProjectionEventDto`, `ProjectionRequest`, `ProjectionResponse` contracts and `AggregateActor.GetEventsAsync`.
**Story 11-2 provided:** `EventReplayProjectionActor`, `IProjectionWriteActor`, `ProjectionState` DTO, actor registration.
**Story 11-3 provided:** `IProjectionUpdateOrchestrator`, `ProjectionUpdateOrchestrator`, fire-and-forget wiring in `EventPublisher`.
**Story 11-4 provided:** Refresh interval gating in orchestrator, startup-time projection discovery, configuration validation.
**This story adds:** Real `/project` endpoint in the counter sample domain service that completes the pipeline.

### Task 1: CounterProjectionHandler

**File:** `samples/Hexalith.EventStore.Sample/Counter/Projections/CounterProjectionHandler.cs`

**Namespace:** `Hexalith.EventStore.Sample.Counter.Projections`

**Design decisions:**

1. **Stateless replay:** The orchestrator currently sends ALL events from sequence 0 (checkpoint tracking deferred -- see `ProjectionUpdateOrchestrator.cs` line 65 TODO). The handler creates a fresh `CounterState` each invocation and replays all events. This is correct and sufficient for the sample.

2. **Reuse `CounterState`:** The design spec says "Apply logic reuses existing `CounterProcessor.RehydrateCount()` or `CounterState`". Using `CounterState` is cleaner -- it has typed `Apply()` methods for all counter events and handles `AggregateTerminated` as a no-op.

3. **Event type matching by suffix:** Match `EventTypeName` using `EndsWith` (same pattern as `CounterProcessor.ApplyEventToCount`). This handles both short names (`"CounterIncremented"`) and fully-qualified names (`"Hexalith.EventStore.Sample.Counter.Events.CounterIncremented"`).

4. **Skip unknown events:** Rejection events (`CounterCannotGoNegative`) and framework events (`AggregateTerminated`) that don't affect the count are silently skipped. No exception for unrecognized event types.

5. **ProjectionType = "counter":** Matches `GetCounterStatusQuery.ProjectionType` (see `samples/Hexalith.EventStore.Sample/Counter/Queries/GetCounterStatusQuery.cs` line 20).

6. **State JSON format:** `{ "count": N }` using `new { count = state.Count }` -- anonymous type with lowercase property name serializes correctly with default `System.Text.Json` options (no naming policy = exact match).

**Implementation:**

```csharp
using System.Text.Json;

using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Sample.Counter.Events;
using Hexalith.EventStore.Sample.Counter.State;

namespace Hexalith.EventStore.Sample.Counter.Projections;

/// <summary>
/// Projection handler for the Counter domain. Replays events from a
/// <see cref="ProjectionRequest"/> onto a fresh <see cref="CounterState"/>
/// and returns the current count as a <see cref="ProjectionResponse"/>.
/// </summary>
public static class CounterProjectionHandler
{
    /// <summary>
    /// Projects a sequence of events into a counter projection response.
    /// </summary>
    /// <param name="request">The projection request containing events to replay.</param>
    /// <returns>A projection response with type "counter" and the current count state.</returns>
    public static ProjectionResponse Project(ProjectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var state = new CounterState();
        foreach (ProjectionEventDto evt in request.Events ?? [])
        {
            ApplyEvent(state, evt.EventTypeName);
        }

        return new ProjectionResponse(
            "counter",
            JsonSerializer.SerializeToElement(new { count = state.Count }));
    }

    private static void ApplyEvent(CounterState state, string eventTypeName)
    {
        if (string.IsNullOrEmpty(eventTypeName))
        {
            return;
        }

        // EventTypeName may be short ("CounterIncremented") or fully qualified; suffix match handles both.
        if (eventTypeName.EndsWith(nameof(CounterIncremented), StringComparison.Ordinal))
        {
            state.Apply(new CounterIncremented());
        }
        else if (eventTypeName.EndsWith(nameof(CounterDecremented), StringComparison.Ordinal))
        {
            state.Apply(new CounterDecremented());
        }
        else if (eventTypeName.EndsWith(nameof(CounterReset), StringComparison.Ordinal))
        {
            state.Apply(new CounterReset());
        }
        else if (eventTypeName.EndsWith(nameof(CounterClosed), StringComparison.Ordinal))
        {
            state.Apply(new CounterClosed());
        }
        // Unknown events (CounterCannotGoNegative, AggregateTerminated): silently skipped.
        // These don't affect counter projection state.
    }
}
```

**Why not deserialize Payload:** Counter events are empty records (`{}` payload). Deserializing `byte[]` to empty records is pure overhead. The `EventTypeName` alone determines the state transition. If future events gain properties, the handler can be updated to deserialize payloads then.

**Why `CounterClosed` is handled:** Even though `IsTerminated` is not in the projection response, the handler should apply it to `CounterState` for correctness. The state's `Count` value is unaffected by `CounterClosed`, but if the projection response ever includes `isTerminated`, the state is ready.

### Task 2: Wire /project Endpoint in Program.cs

**File:** `samples/Hexalith.EventStore.Sample/Program.cs`

**Current state:** Lines 29-40 conditionally map a malformed `/project` endpoint for fault injection testing (`EventStore:SampleFaults:MalformedProjectResponse = true`). When the flag is false (normal operation), **no `/project` endpoint exists**.

**Change:** Add the real handler in an `else` branch:

```csharp
if (malformedProjectionResponse) {
    app.MapPost("/project", () => {
        _ = Interlocked.Increment(ref malformedProjectionResponseHitCount);
        return Results.Content("{\"projectionType\":", "application/json");
    });
    app.MapGet("/faults/project-hit-count", () => Results.Ok(new {
        Count = Volatile.Read(ref malformedProjectionResponseHitCount),
    }));
} else {
    app.MapPost("/project", (ProjectionRequest request)
        => Results.Ok(CounterProjectionHandler.Project(request)));
}
```

**Required usings at top of Program.cs:**
```csharp
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Sample.Counter.Projections;
```

**CRITICAL: `Results.Ok(response)`** wraps the `ProjectionResponse` in a 200 JSON response. The `DaprClient.InvokeMethodAsync<TRequest, TResponse>` call in the orchestrator deserializes the body as `ProjectionResponse`. The JSON serialization uses ASP.NET Core's default `System.Text.Json` with camelCase naming policy (via `JsonSerializerDefaults.Web`). This means the response body is:
```json
{"projectionType":"counter","state":{"count":5}}
```
The `DaprClient` deserializer also uses `JsonSerializerDefaults.Web` (camelCase), so `ProjectionResponse.ProjectionType` and `ProjectionResponse.State` bind correctly.

**CRITICAL: No `async` needed.** `CounterProjectionHandler.Project()` is synchronous. The endpoint delegate returns `IResult` directly.

### Task 3: Unit Tests

**File:** `tests/Hexalith.EventStore.Sample.Tests/Counter/Projections/CounterProjectionHandlerTests.cs`

**Test pattern:** Follow existing `CounterAggregateTests` style:
- xUnit `[Fact]` methods
- `Assert.*` for simple assertions (matching existing style in `CounterAggregateTests.cs`)
- Helper method to create `ProjectionEventDto` instances

**Helper method:**
```csharp
private static ProjectionEventDto CreateEvent(string eventTypeName)
    => new(
        EventTypeName: eventTypeName,
        Payload: System.Text.Encoding.UTF8.GetBytes("{}"),
        SerializationFormat: "json",
        SequenceNumber: 1,
        Timestamp: DateTimeOffset.UtcNow,
        CorrelationId: "test-corr");
```

**Tests:**

1. `Project_SingleIncrement_ReturnsCountOne` (AC: 2)
   - Events: `[CounterIncremented]`
   - Assert: state.count = 1, projectionType = "counter"

2. `Project_MultipleIncrements_ReturnsCorrectCount` (AC: 2)
   - Events: `[CounterIncremented, CounterIncremented, CounterIncremented]`
   - Assert: state.count = 3

3. `Project_IncrementThenDecrement_ReturnsZero` (AC: 2)
   - Events: `[CounterIncremented, CounterDecremented]`
   - Assert: state.count = 0

4. `Project_IncrementThenReset_ReturnsZero` (AC: 2)
   - Events: `[CounterIncremented, CounterIncremented, CounterReset]`
   - Assert: state.count = 0

5. `Project_EmptyEvents_ReturnsZeroCount` (AC: 1)
   - Events: `[]`
   - Assert: state.count = 0

6. `Project_RejectionEvent_Skipped` (AC: 2)
   - Events: `[CounterIncremented, CounterCannotGoNegative, CounterIncremented]`
   - Assert: state.count = 2 (rejection event ignored)

7. `Project_ProjectionType_IsCounter` (AC: 1)
   - Events: `[CounterIncremented]`
   - Assert: response.ProjectionType = "counter"

8. `Project_MixedStreamWithClose_ReturnsPreCloseCount` (AC: 2)
   - Events: `[CounterIncremented, CounterIncremented, CounterClosed]`
   - Assert: state.count = 2 (close doesn't reset count)

9. `Project_FullyQualifiedEventTypeName_StillMatches` (AC: 1)
   - Events with `EventTypeName = "Hexalith.EventStore.Sample.Counter.Events.CounterIncremented"`
   - Assert: state.count = 1 (suffix matching works)

10. `Project_NullRequest_ThrowsArgumentNullException` (AC: 1)
    - Call with `null`
    - Assert: `ArgumentNullException` thrown

11. `Project_NullEventTypeName_SkippedGracefully` (AC: 1)
    - Events: `[CounterIncremented, {EventTypeName=null}, CounterIncremented]`
    - Assert: count = 2, no exception thrown

12. `Project_ResponseSurvivesJsonRoundTrip` (AC: 1)
    - Call `Project()` with one increment event
    - Serialize response to JSON string via `JsonSerializer.Serialize(response)`
    - Deserialize back to `ProjectionResponse`
    - Assert: `deserialized.ProjectionType == "counter"` and `deserialized.State.GetProperty("count").GetInt32() == 1`
    - **Why:** Validates DAPR `InvokeMethodAsync` round-trip won't lose data

13. `Project_PostTombstoneEventsInStream_StillCounted` (AC: 2)
    - Events: `[Incremented, Incremented, Closed, AggregateTerminated, Incremented]`
    - Assert: count = 3
    - **Why:** Handler is stateless replay -- it does NOT enforce tombstone semantics. The server (AggregateActor) enforces tombstoning; the projection handler just counts.

14. `Project_NullEventsArray_ReturnsZeroCount` (AC: 1)
    - Create `ProjectionRequest` with `Events = null` (simulates DAPR deserialization edge case)
    - Assert: count = 0, no exception
    - **Why:** DAPR `InvokeMethodAsync` deserialization may produce null for JSON `"events": null`

**State JSON assertion pattern:**
```csharp
ProjectionResponse response = CounterProjectionHandler.Project(request);
Assert.Equal("counter", response.ProjectionType);
int count = response.State.GetProperty("count").GetInt32();
Assert.Equal(expectedCount, count);
```

### CRITICAL: Scope Boundaries

**This story ONLY creates/modifies:**
- `samples/Hexalith.EventStore.Sample/Counter/Projections/CounterProjectionHandler.cs` -- NEW: projection handler
- `samples/Hexalith.EventStore.Sample/Program.cs` -- MODIFY: add real `/project` endpoint in else branch, add usings
- `tests/Hexalith.EventStore.Sample.Tests/Counter/Projections/CounterProjectionHandlerTests.cs` -- NEW: unit tests

**Do NOT create or modify:**
- `ProjectionRequest.cs`, `ProjectionResponse.cs`, `ProjectionEventDto.cs` -- no changes (existing contracts from Story 11-1)
- `CounterState.cs` -- no changes (existing state class with all Apply methods)
- `CounterAggregate.cs` -- no changes (command handling, not projection)
- `CounterProcessor.cs` -- no changes (legacy processor, not used by projection handler)
- `GetCounterStatusQuery.cs` -- no changes (query contract already defines projectionType "counter")
- `DomainServiceRequestRouter.cs` -- no changes (routes `/process`, not `/project`)
- `ProjectionUpdateOrchestrator.cs` -- no changes (server-side orchestration, unchanged)
- `EventStoreProjection<T>` -- no changes (client-side base class, not used server-side)
- Any Server project files -- no changes needed
- Any SignalR or Client project files
- `Hexalith.EventStore.Sample.csproj` -- no changes needed (already references `Hexalith.EventStore.Client` which transitively includes Contracts)

### CRITICAL: Do NOT Break Existing Tests

- `Program.cs` changes are additive (new else branch for the existing conditional)
- The malformed response stub (`EventStore:SampleFaults:MalformedProjectResponse = true`) continues to work unchanged for Tier 3 fault injection tests
- No existing interfaces or classes are modified
- All existing tests (Tier 1 baseline: 709) must continue to pass
- No existing test constructor signatures are changed

### CRITICAL: Dependency Verification

The sample project (`Hexalith.EventStore.Sample.csproj`) references:
- `Hexalith.EventStore.Client` (which depends on `Hexalith.EventStore.Contracts`)

`ProjectionRequest` and `ProjectionResponse` are in `Hexalith.EventStore.Contracts.Projections` -- already available as a transitive dependency. No new package or project references needed.

The test project (`Hexalith.EventStore.Sample.Tests.csproj`) references:
- `Hexalith.EventStore.Sample` (which brings all dependencies)

`ProjectionEventDto` is in `Hexalith.EventStore.Contracts.Projections` -- also transitively available. No new references needed.

### Existing Code Patterns to Follow

**Endpoint registration pattern** (from `Program.cs` line 25-27):
```csharp
app.MapPost("/process", async (DomainServiceRequest request, IServiceProvider serviceProvider) => {
    return Results.Ok(await DomainServiceRequestRouter.ProcessAsync(serviceProvider, request).ConfigureAwait(false));
});
```

**Static handler pattern** (from `DomainServiceRequestRouter.cs`):
```csharp
public static class DomainServiceRequestRouter {
    public static async Task<DomainServiceWireResult> ProcessAsync(...) { ... }
}
```

**Test helper pattern** (from `CounterAggregateTests.cs` lines 22-34):
```csharp
private static CommandEnvelope CreateCommand<T>(T command) where T : notnull
    => new(MessageId: ..., TenantId: "sample-tenant", ...);
```

**Event type suffix matching** (from `CounterProcessor.cs` line 179):
```csharp
if (eventTypeName.EndsWith("CounterIncremented", StringComparison.Ordinal)) { ... }
```

### AC 3 Verification: Full Pipeline (Tier 3)

AC 3 requires the full pipeline (command -> events -> /project -> ProjectionActor -> query) to work. This is verified via:

1. **Manual verification:** Run `dotnet run` on `Hexalith.EventStore.AppHost`, open Blazor UI, click increment, observe counter value updates
2. **Tier 3 tests** (in `tests/Hexalith.EventStore.IntegrationTests/`): These are end-to-end Aspire tests that submit commands and verify query results. They run with `malformedProjectionResponse = false` (default), so they exercise the real `/project` endpoint. Note: Tier 3 tests require full DAPR init + Docker and are optional in CI.

**For this story's Definition of Done:** AC 3 can be verified manually with the AppHost. The existing Tier 3 test infrastructure exercises the pipeline when available.

### Previous Story Intelligence (Story 11-4)

Key learnings from Story 11-4:
- **Branch naming:** `feat/story-11-4-convention-based-projection-discovery-and-configuration`
- **Commit message pattern:** `feat: <description>`
- **Test count baseline:** Tier 1: 709 passed, Tier 2: 1577 passed
- **Build must pass:** `dotnet build Hexalith.EventStore.slnx --configuration Release` with 0 warnings, 0 errors (`TreatWarningsAsErrors = true`)
- **Code style:** File-scoped namespaces, Allman braces, 4-space indent, CRLF line endings
- **LoggerMessage EventId range:** 1110-1124 used by projection infrastructure. This story does NOT add LoggerMessages (sample code, not infrastructure)
- **`nameof()` for event matching:** Use `nameof(CounterIncremented)` instead of string literal `"CounterIncremented"` for compile-time safety

### Git Intelligence

Recent commits (last 5):
- `69f96d2` feat: Add convention-based projection discovery and configuration gating
- `25b6a4c` Merge PR #126 (Story 11-3)
- `c96503c` feat: Add immediate projection trigger with fire-and-forget background task
- `ed47cd6` Merge PR #125 (Story 11-2)
- `d390695` feat: Implement EventReplayProjectionActor and projection state storage

Pattern: feature branches merged via PRs, conventional commit messages (`feat: ...`).

### Package/Framework Reference

- .NET 10 SDK `10.0.103` (from `global.json`)
- `System.Text.Json` for `JsonSerializer.SerializeToElement` and `JsonElement` (in-box, no external package)
- xUnit `2.9.3` for tests
- `Hexalith.EventStore.Contracts` for `ProjectionRequest`, `ProjectionResponse`, `ProjectionEventDto`
- `TreatWarningsAsErrors = true` -- any warning is a build failure
- File-scoped namespaces, Allman brace style, 4-space indentation

### Project Structure Notes

```
samples/Hexalith.EventStore.Sample/
  Program.cs                                          [MODIFY -- add else branch with real /project endpoint]
  Counter/
    Projections/
      CounterProjectionHandler.cs                     [NEW -- static projection handler]
    CounterAggregate.cs                               [EXISTS -- no changes]
    CounterProcessor.cs                               [EXISTS -- no changes]
    State/
      CounterState.cs                                 [EXISTS -- no changes, reused by handler]
    Events/
      CounterIncremented.cs                           [EXISTS -- no changes]
      CounterDecremented.cs                           [EXISTS -- no changes]
      CounterReset.cs                                 [EXISTS -- no changes]
      CounterClosed.cs                                [EXISTS -- no changes]
      CounterCannotGoNegative.cs                      [EXISTS -- no changes]
    Queries/
      GetCounterStatusQuery.cs                        [EXISTS -- no changes, ProjectionType="counter" matches]

tests/Hexalith.EventStore.Sample.Tests/
  Counter/
    Projections/
      CounterProjectionHandlerTests.cs                [NEW -- 14 unit tests]
    CounterAggregateTests.cs                          [EXISTS -- no changes]
    CounterProcessorTests.cs                          [EXISTS -- no changes]
```

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 11.5] -- Story requirements and acceptance criteria
- [Source: _bmad-output/planning-artifacts/epics.md#Epic 11] -- Epic overview: Server-Managed Projection Builder
- [Source: docs/superpowers/specs/2026-03-15-server-managed-projection-builder-design.md#Counter Sample Integration] -- Design spec: counter sample needs
- [Source: docs/superpowers/specs/2026-03-15-server-managed-projection-builder-design.md#Domain Service /project Endpoint] -- Design spec: /project endpoint contract
- [Source: samples/Hexalith.EventStore.Sample/Program.cs] -- Current endpoint wiring with malformed response conditional
- [Source: samples/Hexalith.EventStore.Sample/Counter/State/CounterState.cs] -- State class to reuse (Apply methods)
- [Source: samples/Hexalith.EventStore.Sample/Counter/CounterProcessor.cs#ApplyEventToCount] -- Event type suffix matching pattern
- [Source: samples/Hexalith.EventStore.Sample/Counter/Queries/GetCounterStatusQuery.cs] -- Query contract: ProjectionType = "counter"
- [Source: samples/Hexalith.EventStore.Sample/DomainServiceRequestRouter.cs] -- Existing /process routing pattern
- [Source: src/Hexalith.EventStore.Contracts/Projections/ProjectionRequest.cs] -- Request contract
- [Source: src/Hexalith.EventStore.Contracts/Projections/ProjectionResponse.cs] -- Response contract
- [Source: src/Hexalith.EventStore.Contracts/Projections/ProjectionEventDto.cs] -- Event DTO contract
- [Source: src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs] -- Orchestrator that invokes /project endpoint (lines 88-96)
- [Source: tests/Hexalith.EventStore.Sample.Tests/Counter/CounterAggregateTests.cs] -- Test patterns to follow
- [Source: _bmad-output/implementation-artifacts/11-4-convention-based-projection-discovery-and-configuration.md] -- Previous story (test counts, patterns, conventions)

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

### Completion Notes List

- Created `CounterProjectionHandler` as a static class with `Project()` method that replays events onto fresh `CounterState` and returns `ProjectionResponse("counter", { count: N })`
- Event matching uses `EndsWith` with `nameof()` for compile-time safety, handling both short and fully-qualified event type names
- Null/empty `EventTypeName` guarded with early return; unknown events (rejection, framework) silently skipped
- Wired real `/project` endpoint in `Program.cs` `else` branch, preserving the `if (malformedProjectionResponse)` fault injection branch for Tier 3 tests
- Added 15 unit tests covering: single/multiple increments, decrement, reset, empty events, rejection event skip, projection type, mixed stream with close, fully-qualified names, null request, null event type, JSON round-trip, post-tombstone events, null events array, null event element in array
- Build: 0 errors, 0 warnings (Release mode)
- Tier 1 tests: 724 passed (709 baseline + 15 new), 0 failures
- Post-review hardening: projection replay now skips null `ProjectionEventDto` entries to avoid null-reference failures on malformed payload arrays.
- **AC 3 manual verification:** Run `dotnet run --project src/Hexalith.EventStore.AppHost`, open Blazor UI, click increment, observe counter value updates. The full pipeline (command -> events -> /project -> ProjectionActor -> query) is exercised end-to-end. Tier 3 integration tests also exercise this when run with full DAPR init + Docker.

### Change Log

- 2026-03-20: Implemented Story 11-5 — CounterProjectionHandler, /project endpoint wiring, 14 unit tests
- 2026-03-20: Post-review hardening — added null event element guard in projection loop and corresponding unit test

### File List

- samples/Hexalith.EventStore.Sample/Counter/Projections/CounterProjectionHandler.cs (NEW)
- samples/Hexalith.EventStore.Sample/Program.cs (MODIFIED — added else branch with real /project endpoint, added usings)
- tests/Hexalith.EventStore.Sample.Tests/Counter/Projections/CounterProjectionHandlerTests.cs (NEW)
