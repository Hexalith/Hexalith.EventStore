# Story 11.2: EventReplayProjectionActor & Projection State Storage

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a platform developer,
I want a concrete ProjectionActor that stores projection state received from domain services,
so that query actors can serve real projection data.

## Acceptance Criteria

1. **Given** `EventReplayProjectionActor` as a concrete implementation of `CachingProjectionActor`,
   **When** registered with DAPR actor type name `"ProjectionActor"`,
   **Then** `QueryRouter` can route queries to it via the existing `ProjectionActorTypeName`.

2. **Given** the actor implements `IProjectionActor` (read) and `IProjectionWriteActor` (write),
   **When** `UpdateProjectionAsync(state)` is called,
   **Then** state is persisted to DAPR actor state
   **And** ETag is regenerated via `IProjectionChangeNotifier.NotifyProjectionChangedAsync()`
   **And** SignalR notification is broadcast via the notifier's `IProjectionChangedBroadcaster`.

3. **Given** a query arrives,
   **When** `ExecuteQueryAsync` is called (cache miss path via `CachingProjectionActor`),
   **Then** it reads the last persisted state from DAPR actor state
   **And** returns `QueryResult { Success=true, Payload=state, ProjectionType=type }`
   **And** returns `QueryResult { Success=false }` with error message if no state is persisted yet
   **And** `CachingProjectionActor` base provides in-memory ETag caching on top (no double-caching).

## Definition of Done

- All 3 ACs verified against actual code
- Build: `dotnet build Hexalith.EventStore.slnx --configuration Release` -- 0 errors, 0 warnings
- Tier 1 + Tier 2 tests pass, no regressions
- Branch: `feat/story-11-2-eventreplayprojectionactor-and-projection-state-storage`

## Tasks / Subtasks

- [x] Task 1: Create `IProjectionWriteActor` interface and `ProjectionState` DTO (AC: 2)
  - [x] Create `src/Hexalith.EventStore.Server/Actors/IProjectionWriteActor.cs`
  - [x] Create `src/Hexalith.EventStore.Server/Actors/ProjectionState.cs`
- [x] Task 2: Create `EventReplayProjectionActor` (AC: 1, 2, 3)
  - [x] Create `src/Hexalith.EventStore.Server/Actors/EventReplayProjectionActor.cs`
  - [x] Implement `UpdateProjectionAsync` -- persist state, trigger ETag regeneration + SignalR broadcast
  - [x] Implement `ExecuteQueryAsync` -- read persisted state from DAPR actor state
- [x] Task 3: Register actor in DI (AC: 1)
  - [x] Modify `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs` -- add `RegisterActor<EventReplayProjectionActor>()`
- [x] Task 4: Update `FakeProjectionWriteActor` in Testing project
  - [x] Create `src/Hexalith.EventStore.Testing/Fakes/FakeProjectionWriteActor.cs`
- [x] Task 5: Create tests (AC: 1, 2, 3)
  - [x] Create `tests/Hexalith.EventStore.Server.Tests/Actors/EventReplayProjectionActorTests.cs`
  - [x] Run Tier 2 Server tests to verify no regression
- [x] Task 6: Full build and test verification
  - [x] `dotnet build Hexalith.EventStore.slnx --configuration Release` -- 0 errors, 0 warnings
  - [x] All Tier 1 tests pass
  - [x] Tier 2 Server tests pass (1552 passed, 0 failed)

## Dev Notes

### Architecture Context: Server-Managed Projection Builder (Mode B)

This story creates the **projection state storage actor** -- the second layer of Epic 11. The full pipeline is:

```
Events persisted -> AggregateActor.GetEventsAsync(fromSequence) -> map to ProjectionEventDto[]
  -> POST /project to domain service (ProjectionRequest)
  -> domain service returns ProjectionResponse { ProjectionType, State }
  -> EventReplayProjectionActor.UpdateProjectionAsync(state)  <-- THIS STORY
  -> EventReplayProjectionActor.ExecuteQueryAsync(envelope)   <-- THIS STORY
  -> ETag regenerated -> SignalR broadcast -> UI refreshes
```

**This story covers:** the `EventReplayProjectionActor` (read + write), `IProjectionWriteActor` interface, `ProjectionState` DTO, and DAPR actor registration.
**Story 11-1 provided:** `ProjectionEventDto`, `ProjectionRequest`, `ProjectionResponse` contracts and `AggregateActor.GetEventsAsync`.
**Stories 11-3 through 11-5 build:** the orchestrator (immediate trigger), discovery/config, and sample endpoint.

### Task 1: IProjectionWriteActor Interface + ProjectionState DTO

**Namespace:** `Hexalith.EventStore.Server.Actors`

#### IProjectionWriteActor

New DAPR actor interface for the write side of projection state updates. This is intentionally separate from `IProjectionActor` (read) to maintain CQRS separation at the actor interface level.

```csharp
// src/Hexalith.EventStore.Server/Actors/IProjectionWriteActor.cs
using Dapr.Actors;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// DAPR actor interface for writing projection state.
/// The projection builder (Story 11-3) calls this after receiving
/// state from the domain service's /project endpoint.
/// </summary>
public interface IProjectionWriteActor : IActor
{
    /// <summary>
    /// Persists projection state, regenerates ETag, and broadcasts SignalR notification.
    /// </summary>
    /// <param name="state">The projection state received from the domain service.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateProjectionAsync(ProjectionState state);
}
```

#### ProjectionState DTO

DAPR actor method parameter. Must use `[DataContract]/[DataMember]` for DAPR actor proxy serialization -- same pattern as `QueryEnvelope` and `QueryResult`.

```csharp
// src/Hexalith.EventStore.Server/Actors/ProjectionState.cs
using System.Runtime.Serialization;
using System.Text.Json;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// Projection state passed to <see cref="IProjectionWriteActor.UpdateProjectionAsync"/>.
/// Contains the opaque state from the domain service plus routing metadata.
/// </summary>
[DataContract]
public record ProjectionState(
    [property: DataMember] string ProjectionType,
    [property: DataMember] string TenantId,
    [property: DataMember] JsonElement State);
```

**Design rationale:** `ProjectionState` is separate from `ProjectionResponse` (Contracts) because:
- `ProjectionResponse` is for HTTP JSON serialization (domain service response)
- `ProjectionState` is for DAPR DataContract serialization (actor proxy call)
- `ProjectionState` includes `TenantId` (needed for ETag/SignalR notification), which `ProjectionResponse` omits (tenant is implicit in the HTTP call context)

### Task 2: EventReplayProjectionActor

**File:** `src/Hexalith.EventStore.Server/Actors/EventReplayProjectionActor.cs`

Concrete implementation of `CachingProjectionActor` that:
1. Persists projection state to DAPR actor state via `IActorStateManager`
2. Reads persisted state on cache miss via `ExecuteQueryAsync`
3. Triggers ETag regeneration + SignalR broadcast via `IProjectionChangeNotifier`

**Constructor dependencies:**
- `ActorHost host` -- DAPR actor host (required by `Actor` base)
- `IETagService eTagService` -- ETag fetching for cache (required by `CachingProjectionActor`)
- `IProjectionChangeNotifier projectionChangeNotifier` -- ETag regeneration + SignalR broadcast
- `ILogger<EventReplayProjectionActor> logger` -- logging (required by `CachingProjectionActor`)

**DAPR actor type name:** `"ProjectionActor"` -- must match `QueryRouter.ProjectionActorTypeName` constant (`src/Hexalith.EventStore.Server/Queries/QueryRouter.cs` line with `const string ProjectionActorTypeName = "ProjectionActor"`).

**State key:** `"projection-state"` -- single key per actor instance storing the latest `ProjectionState`.

**Implementation outline:**

```csharp
// src/Hexalith.EventStore.Server/Actors/EventReplayProjectionActor.cs
namespace Hexalith.EventStore.Server.Actors;

public partial class EventReplayProjectionActor(
    ActorHost host,
    IETagService eTagService,
    IProjectionChangeNotifier projectionChangeNotifier,
    ILogger<EventReplayProjectionActor> logger)
    : CachingProjectionActor(host, eTagService, logger), IProjectionWriteActor
{
    internal const string ProjectionStateKey = "projection-state";

    public async Task UpdateProjectionAsync(ProjectionState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        // 1. Persist to DAPR actor state
        await StateManager.SetStateAsync(ProjectionStateKey, state).ConfigureAwait(false);
        await StateManager.SaveStateAsync().ConfigureAwait(false);

        // 2. Regenerate ETag + broadcast SignalR (fail-open in notifier)
        await projectionChangeNotifier.NotifyProjectionChangedAsync(
            state.ProjectionType,
            state.TenantId)
            .ConfigureAwait(false);
    }

    protected override async Task<QueryResult> ExecuteQueryAsync(QueryEnvelope envelope)
    {
        // Read last persisted state from DAPR actor state
        Dapr.Actors.Runtime.ConditionalValue<ProjectionState> result =
            await StateManager.TryGetStateAsync<ProjectionState>(ProjectionStateKey)
                .ConfigureAwait(false);

        if (!result.HasValue)
        {
            return new QueryResult(false, default, "No projection state available for this aggregate");
        }

        return new QueryResult(true, result.Value.State, ProjectionType: result.Value.ProjectionType);
    }
}
```

**Logging:** Use `LoggerMessage` source-generated partial methods. Follow the same pattern as `CachingProjectionActor` and `DaprProjectionChangeNotifier`. Use event IDs in the 1090+ range (1090-1099 block) to avoid collisions:
- `1090` -- `UpdateProjectionAsync` persisted state
- `1091` -- `ExecuteQueryAsync` no persisted state (miss)
- `1092` -- `ExecuteQueryAsync` returned persisted state

**CRITICAL: SaveStateAsync is required.** DAPR actors buffer state changes until `SaveStateAsync()` is called or the method returns. Call `SaveStateAsync()` explicitly before calling the notifier to ensure state is durable before the ETag is regenerated. If the ETag regenerates before the state is saved, a query could see the new ETag, execute `ExecuteQueryAsync`, and still read stale/missing state.

**CRITICAL: Do NOT read events or call domain services.** `ExecuteQueryAsync` only reads the **already-persisted** state from DAPR actor state. The event reading, mapping, and domain service invocation are the orchestrator's responsibility (Story 11-3). This actor is purely a state store + cache.

**Caching interaction (how CachingProjectionActor base and EventReplayProjectionActor work together):**
1. Orchestrator calls `UpdateProjectionAsync` -> state written to DAPR state -> ETag regenerated
2. Query arrives -> `CachingProjectionActor.QueryAsync` checks in-memory ETag cache
3. Cache miss (ETag changed) -> calls `ExecuteQueryAsync` -> reads from DAPR actor state -> base caches in memory
4. Cache hit (same ETag) -> returns in-memory cached payload directly (no DAPR state read)

### Task 3: Actor Registration in ServiceCollectionExtensions

**File:** `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs`

Add `EventReplayProjectionActor` registration inside the `AddActors` block (line 66-67):

```csharp
options.Actors.RegisterActor<AggregateActor>();
options.Actors.RegisterActor<ETagActor>();
options.Actors.RegisterActor<EventReplayProjectionActor>();  // NEW -- Story 11-2
```

**DAPR actor type name derivation:** DAPR uses the class name without "Actor" suffix by default? NO -- DAPR uses the full class name. But `QueryRouter` uses `ProjectionActorTypeName = "ProjectionActor"`. So we need to explicitly register with the correct type name.

**CRITICAL:** Check how DAPR derives actor type names. In DAPR .NET SDK, `RegisterActor<T>()` uses the class name as the actor type name by default. `EventReplayProjectionActor` would register as `"EventReplayProjectionActor"`, but `QueryRouter` expects `"ProjectionActor"`. We need to register with a custom type name:

```csharp
options.Actors.RegisterActor<EventReplayProjectionActor>(
    typeOptions: new Dapr.Actors.Runtime.ActorRuntimeOptions
    {
        ActorTypeName = "ProjectionActor"
    });
```

**WAIT -- verify the DAPR SDK API.** The `RegisterActor<T>` overload with `ActorRegistrationOptions` may use a different API. Check the DAPR SDK. The correct approach is:

```csharp
options.Actors.RegisterActor<EventReplayProjectionActor>(
    options => options.ActorTypeName = "ProjectionActor");
```

OR: If `RegisterActor` takes a `typeOptions` parameter, use that. Verify the exact API against `Dapr.Actors.Runtime` 1.17.0. The existing registrations (`RegisterActor<AggregateActor>()` and `RegisterActor<ETagActor>()`) don't use custom type names -- check if `AggregateActor` and `ETagActor` are used with their default names. Looking at the code:
- `AggregateActor` class name -> actor type `"AggregateActor"` (default)
- `ETagActor` has `const string ETagActorTypeName = "ETagActor"` (matches default)
- `QueryRouter` has `const string ProjectionActorTypeName = "ProjectionActor"` -- does NOT match `"EventReplayProjectionActor"`

So we MUST register with custom type name `"ProjectionActor"`. Use the DAPR SDK overload:

```csharp
options.Actors.RegisterActor<EventReplayProjectionActor>(
    typeOptions: new ActorTypeOptions { TypeName = "ProjectionActor" });
```

**Verify the exact DAPR 1.17.0 SDK API for `RegisterActor` with custom type name before implementing.** The dev agent should grep for `RegisterActor` usage patterns and check the DAPR SDK API.

### Task 4: FakeProjectionWriteActor

**File:** `src/Hexalith.EventStore.Testing/Fakes/FakeProjectionWriteActor.cs`

Simple fake for test scenarios where callers need an `IProjectionWriteActor`:

```csharp
public class FakeProjectionWriteActor : IProjectionWriteActor
{
    public ProjectionState? LastReceivedState { get; private set; }

    public Task UpdateProjectionAsync(ProjectionState state)
    {
        LastReceivedState = state;
        return Task.CompletedTask;
    }
}
```

**Note:** `IProjectionWriteActor` extends `IActor`, so the fake needs to implement `IActor` too. However, `IActor` is a marker interface with no methods in DAPR. Follow the `FakeAggregateActor` pattern -- check if it inherits from `Actor` or just implements the interface directly.

**Check `FakeAggregateActor` implementation** in `src/Hexalith.EventStore.Testing/Fakes/FakeAggregateActor.cs` -- if it inherits `Actor`, do the same; if it just implements the interface, do the same.

### Task 5: Tests

**File:** `tests/Hexalith.EventStore.Server.Tests/Actors/EventReplayProjectionActorTests.cs`

**Test setup pattern** (from `AggregateActorGetEventsTests.cs`):
- `ActorHost.CreateForTest<EventReplayProjectionActor>()` with actor ID in format `"{queryType}:{tenantId}:{entityId}"` (e.g., `"GetCounterValue:tenant-a:counter-1"`)
- NSubstitute mocks for `IETagService`, `IProjectionChangeNotifier`, `IActorStateManager`
- Access `StateManager` via reflection (same pattern as `AggregateActorGetEventsTests`)

**Tests to create:**

1. `UpdateProjectionAsync_PersistsStateToActorState` (AC: 2)
   - Mock `StateManager.SetStateAsync` and `SaveStateAsync`
   - Call `UpdateProjectionAsync` with valid `ProjectionState`
   - Verify `SetStateAsync` called with key `"projection-state"` and the state
   - Verify `SaveStateAsync` called

2. `UpdateProjectionAsync_TriggersNotification` (AC: 2)
   - Mock `IProjectionChangeNotifier`
   - Call `UpdateProjectionAsync`
   - Verify `NotifyProjectionChangedAsync` called with correct `projectionType` and `tenantId`

3. `UpdateProjectionAsync_SavesBeforeNotifying` (AC: 2)
   - Verify `SaveStateAsync` is called before `NotifyProjectionChangedAsync`
   - Use NSubstitute callback ordering or Received.InOrder()

4. `UpdateProjectionAsync_NullState_ThrowsArgumentNull` (AC: 2)
   - Call with null state
   - Verify `ArgumentNullException` thrown

5. `ExecuteQueryAsync_NoPersistedState_ReturnsFailure` (AC: 3)
   - Mock `TryGetStateAsync<ProjectionState>` to return no value
   - Call `QueryAsync` (which triggers `ExecuteQueryAsync` internally on cache miss)
   - Verify result: `Success=false`, `ErrorMessage` is not null

6. `ExecuteQueryAsync_WithPersistedState_ReturnsState` (AC: 3)
   - Mock `TryGetStateAsync<ProjectionState>` to return a `ProjectionState` with known JSON
   - Mock `IETagService.GetCurrentETagAsync` to return an ETag (forces cache miss on first call)
   - Call `QueryAsync`
   - Verify result: `Success=true`, `Payload` contains expected JSON, `ProjectionType` matches

7. `UpdateThenQuery_ReturnsUpdatedState` (AC: 2, 3)
   - Call `UpdateProjectionAsync` with state
   - Mock state manager to return that state on subsequent `TryGetStateAsync`
   - Call `QueryAsync`
   - Verify the query returns the same state that was written

8. `QueryAsync_CacheHit_DoesNotCallExecuteQuery` (AC: 3)
   - Pre-populate cache by calling `QueryAsync` once (cache miss)
   - Call `QueryAsync` again with same ETag
   - Verify `TryGetStateAsync` only called once (first call)

**Test helper notes:**
- `QueryEnvelope` constructor requires non-null, non-whitespace params -- create a helper to build valid envelopes
- `JsonElement` for state: use `JsonDocument.Parse("{\"count\":42}").RootElement`
- `IETagService.GetCurrentETagAsync` must return non-null string (e.g., `"counter.abc123"`) for caching to work
- For testing `ExecuteQueryAsync` directly: since it's `protected`, test via `QueryAsync` (public) which calls it on cache miss

### CRITICAL: Scope Boundaries

**This story ONLY creates:**
- `IProjectionWriteActor` interface (new DAPR actor interface)
- `ProjectionState` record (DAPR-serializable DTO)
- `EventReplayProjectionActor` class (concrete projection actor)
- `FakeProjectionWriteActor` (test fake)
- Actor registration in `ServiceCollectionExtensions`
- Tests for the above

**Do NOT create or modify:**
- `IProjectionUpdateOrchestrator` -- that's Story 11-3
- `ProjectionUpdateOrchestrator` -- that's Story 11-3
- `ProjectionCheckpointTracker` -- that's Story 11-3
- `ProjectionOptions` / `ProjectionPollerService` -- that's Story 11-4
- Counter `/project` endpoint -- that's Story 11-5
- `CachingProjectionActor` -- no changes needed (base class works as-is)
- `QueryRouter` -- no changes needed (already routes to `"ProjectionActor"` type name)
- `IProjectionActor` -- no changes needed (read interface already exists)
- `ProjectionEventDto` / `ProjectionRequest` / `ProjectionResponse` -- created in Story 11-1
- `AggregateActor.GetEventsAsync` -- created in Story 11-1

### CRITICAL: Do NOT Break Existing Tests

- `CachingProjectionActor` gains no new abstract methods -- existing code compiles unchanged
- `ServiceCollectionExtensions` change is additive (new actor registration)
- All existing query routing works through `IProjectionActor` interface, which `EventReplayProjectionActor` implements via `CachingProjectionActor`
- No existing interfaces modified (only new `IProjectionWriteActor` added)
- All 2250 existing tests (Tier 1: 709, Tier 2: 1541) must continue to pass

### Existing Code Patterns to Follow

**Actor class style** (from `CachingProjectionActor.cs`):
```csharp
public abstract partial class CachingProjectionActor(
    ActorHost host,
    IETagService eTagService,
    ILogger logger)
    : Actor(host), IProjectionActor
{
    // primary constructor pattern with partial for LoggerMessage
}
```

**DAPR actor state operations** (from `AggregateActor.cs`):
```csharp
await StateManager.SetStateAsync(key, value).ConfigureAwait(false);
await StateManager.SaveStateAsync().ConfigureAwait(false);
var result = await StateManager.TryGetStateAsync<T>(key).ConfigureAwait(false);
```

**Notification pattern** (from `DaprProjectionChangeNotifier.cs`):
```csharp
await projectionChangeNotifier.NotifyProjectionChangedAsync(
    projectionType, tenantId).ConfigureAwait(false);
```

**[DataContract] record style** (from `QueryResult.cs`):
```csharp
[DataContract]
public record ProjectionState(
    [property: DataMember] string ProjectionType,
    [property: DataMember] string TenantId,
    [property: DataMember] JsonElement State);
```

**LoggerMessage pattern** (from `CachingProjectionActor.cs` and `DaprProjectionChangeNotifier.cs`):
```csharp
private static partial class Log
{
    [LoggerMessage(EventId = 1090, Level = LogLevel.Debug, Message = "...")]
    public static partial void MethodName(ILogger logger, ...);
}
```

**Test style** (from `AggregateActorGetEventsTests.cs`):
- xUnit `[Fact]` attributes
- Shouldly assertions (`result.ShouldBe()`, `result.ShouldNotBeNull()`)
- `ActorHost.CreateForTest<T>(new ActorTestOptions { ActorId = ... })`
- NSubstitute mocks for `IActorStateManager` and DI dependencies

### Project Structure Notes

```
src/Hexalith.EventStore.Server/Actors/
  CachingProjectionActor.cs              [EXISTS -- base class, no changes]
  IProjectionActor.cs                    [EXISTS -- read interface, no changes]
  IProjectionWriteActor.cs              [NEW -- this story]
  ProjectionState.cs                     [NEW -- this story]
  EventReplayProjectionActor.cs          [NEW -- this story]
  QueryEnvelope.cs                       [EXISTS -- reference for DataContract pattern]
  QueryResult.cs                         [EXISTS -- reference for DataContract pattern]

src/Hexalith.EventStore.Server/Configuration/
  ServiceCollectionExtensions.cs         [MODIFY -- add actor registration]

src/Hexalith.EventStore.Testing/Fakes/
  FakeProjectionWriteActor.cs            [NEW -- this story]

tests/Hexalith.EventStore.Server.Tests/Actors/
  EventReplayProjectionActorTests.cs     [NEW -- this story]
```

### Previous Story Intelligence (Story 11-1)

Key learnings from the previous story in this epic:
- **Branch naming:** `feat/story-11-1-projection-contract-dtos-and-aggregateactor-event-reading`
- **Commit message pattern:** `feat: <description for Story 11-2>`
- **Actor constructor deps:** `AggregateActor` takes 9 constructor params -- `EventReplayProjectionActor` is simpler (4 params: host, eTagService, projectionChangeNotifier, logger)
- **State manager mock pattern:** Tests use NSubstitute to mock `IActorStateManager.TryGetStateAsync<T>()` and `SetStateAsync()`
- **Test count baseline:** Tier 1: 709 passed, Tier 2: 1541 passed (total 2250)
- **Build must pass:** `dotnet build Hexalith.EventStore.slnx --configuration Release` with 0 warnings, 0 errors
- **Batch read pattern for GetEventsAsync** established -- not needed for this story (this actor just stores/reads single state)

### Git Intelligence

Recent commits (last 5):
- `14f9647` feat: Implement Projection Contract DTOs and AggregateActor Event Reading (Story 11-1)
- `31cd5b2` Merge PR #123 (Story 10-2 Redis backplane)
- `c61948a` Mark story 10-2 done
- `9258ff9` Merge PR #122 (Story 10-2)
- `6463ea7` Story 10-1 done, prepare 10-2

Pattern: feature branches merged via PRs, conventional commit messages (`feat: ...`).

### Package/Framework Reference

- .NET 10 SDK `10.0.103` (from `global.json`)
- DAPR SDK `1.17.0` -- `IActor`, `ActorHost`, `IActorStateManager`, `ConditionalValue<T>`, `ActorHost.CreateForTest<T>()`
- xUnit `2.9.3`, Shouldly `4.3.0`, NSubstitute `5.3.0`
- `System.Text.Json` -- for `JsonElement` in `ProjectionState`
- `System.Runtime.Serialization` -- for `[DataContract]`/`[DataMember]` attributes
- TreatWarningsAsErrors = true -- any warning is a build failure

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 11.2] -- Story requirements and acceptance criteria
- [Source: _bmad-output/planning-artifacts/epics.md#Epic 11] -- Epic overview: Server-Managed Projection Builder
- [Source: docs/superpowers/specs/2026-03-15-server-managed-projection-builder-design.md#Components] -- Full design spec (EventReplayProjectionActor, IProjectionWriteActor, caching interaction, error handling)
- [Source: docs/superpowers/plans/2026-03-15-server-managed-projection-builder.md#Chunk 2] -- Implementation plan
- [Source: src/Hexalith.EventStore.Server/Actors/CachingProjectionActor.cs] -- Base class (QueryAsync with ETag caching, abstract ExecuteQueryAsync)
- [Source: src/Hexalith.EventStore.Server/Actors/IProjectionActor.cs] -- Read interface (QueryAsync method)
- [Source: src/Hexalith.EventStore.Server/Actors/QueryResult.cs] -- Return type with [DataContract] pattern
- [Source: src/Hexalith.EventStore.Server/Actors/QueryEnvelope.cs] -- Actor parameter with [DataContract] pattern
- [Source: src/Hexalith.EventStore.Server/Queries/QueryRouter.cs] -- Uses ProjectionActorTypeName = "ProjectionActor"
- [Source: src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs] -- Actor registration point (lines 60-68)
- [Source: src/Hexalith.EventStore.Server/Projections/DaprProjectionChangeNotifier.cs] -- ETag regeneration + SignalR broadcast pattern
- [Source: src/Hexalith.EventStore.Client/Projections/IProjectionChangeNotifier.cs] -- Notification interface
- [Source: src/Hexalith.EventStore.Server/Actors/ETagActor.cs] -- ETag lifecycle management
- [Source: _bmad-output/implementation-artifacts/11-1-projection-contract-dtos-and-aggregateactor-event-reading.md] -- Previous story (test counts, patterns, conventions)

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

- Build verified: 0 errors, 0 warnings
- Tier 1: 709 tests passed (271+297+47+67+27)
- Tier 2: 1552 tests passed, 0 failed (`dotnet test tests/Hexalith.EventStore.Server.Tests`)
- 11 new EventReplayProjectionActor tests pass (`dotnet test tests/Hexalith.EventStore.Server.Tests --filter "FullyQualifiedName~EventReplayProjectionActorTests"`)

### Completion Notes List

- Task 1: Created `IProjectionWriteActor` (DAPR actor interface with `UpdateProjectionAsync`) and `ProjectionState` record (DataContract-serializable DTO with ProjectionType, TenantId, JsonElement State)
- Task 2: Created `EventReplayProjectionActor` extending `CachingProjectionActor`, implementing `IProjectionWriteActor`. `UpdateProjectionAsync` persists state then notifies (SaveState before Notify per design). `ExecuteQueryAsync` reads persisted state from DAPR actor state. LoggerMessage source-generated logging (event IDs 1090-1092).
- Follow-up hardening: added guards for empty `ProjectionType`/`TenantId`, fail-open handling around notifier call after successful persistence, and warning log `EventId 1093` on notifier failure.
- Task 3: Registered `EventReplayProjectionActor` with custom DAPR actor type name `"ProjectionActor"` using `RegisterActor<T>(string actorTypeName)` overload, matching `QueryRouter.ProjectionActorTypeName`.
- Task 4: Created `FakeProjectionWriteActor` following `FakeAggregateActor` pattern (direct interface implementation, no Actor base).
- Task 5: Created 11 tests covering all ACs plus hardening cases: state persistence, notification triggering, save-before-notify ordering, null/invalid guards, no-state failure, state retrieval, update-then-query round-trip, cache hit behavior, and notifier-failure fail-open behavior.
- Task 6: Full build and all tests verified, including full Tier 2 server suite pass when local Dapr dependencies are up.

### Change Log

- 2026-03-20: Story 11-2 implemented — EventReplayProjectionActor, IProjectionWriteActor, ProjectionState, FakeProjectionWriteActor, actor DI registration, and 8 tests
- 2026-03-20: Post-review hardening — input validation + fail-open notifier handling in `EventReplayProjectionActor`, plus 3 additional focused tests.

### File List

- `src/Hexalith.EventStore.Server/Actors/IProjectionWriteActor.cs` (NEW)
- `src/Hexalith.EventStore.Server/Actors/ProjectionState.cs` (NEW)
- `src/Hexalith.EventStore.Server/Actors/EventReplayProjectionActor.cs` (NEW)
- `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs` (MODIFIED)
- `src/Hexalith.EventStore.Testing/Fakes/FakeProjectionWriteActor.cs` (NEW)
- `tests/Hexalith.EventStore.Server.Tests/Actors/EventReplayProjectionActorTests.cs` (NEW)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (MODIFIED)
