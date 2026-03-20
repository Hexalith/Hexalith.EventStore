# Story 11.3: Immediate Projection Trigger (Fire-and-Forget)

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a platform developer,
I want a fire-and-forget background task that delivers new events to domain services immediately after persistence,
so that projections update in near-real-time without blocking command processing.

## Acceptance Criteria

1. **Given** `RefreshIntervalMs = 0` (default configuration),
   **When** events are persisted by the aggregate actor,
   **Then** a fire-and-forget background task is triggered via `IProjectionUpdateOrchestrator` (non-blocking -- does NOT block command processing).

2. **Given** the background task executes,
   **When** it runs,
   **Then** it reads new events via `AggregateActor.GetEventsAsync(fromSequence)` (actor proxy call)
   **And** maps `EventEnvelope[]` to `ProjectionEventDto[]`
   **And** sends `ProjectionRequest` to the domain service `/project` endpoint via DAPR service invocation
   **And** stores the returned state in `EventReplayProjectionActor` via `UpdateProjectionAsync`.

3. **Given** the projection update fails (domain service unavailable or error),
   **When** the failure is logged,
   **Then** the projection stays at last known state (eventual consistency)
   **And** the next trigger retries.

## Definition of Done

- All 3 ACs verified against actual code
- Build: `dotnet build Hexalith.EventStore.slnx --configuration Release` -- 0 errors, 0 warnings
- Tier 1 + Tier 2 tests pass, no regressions
- Branch: `feat/story-11-3-immediate-projection-trigger-fire-and-forget`

## Tasks / Subtasks

- [x] Task 1: Create `ProjectionOptions` configuration model (AC: 1)
  - [x] Create `src/Hexalith.EventStore.Server/Configuration/ProjectionOptions.cs`
  - [x] Register options binding in `ServiceCollectionExtensions.cs`
- [x] Task 2: Create `IProjectionUpdateOrchestrator` interface (AC: 1, 2, 3)
  - [x] Create `src/Hexalith.EventStore.Server/Projections/IProjectionUpdateOrchestrator.cs`
- [x] Task 3: Create `NoOpProjectionUpdateOrchestrator` fallback (AC: 1)
  - [x] Create `src/Hexalith.EventStore.Server/Projections/NoOpProjectionUpdateOrchestrator.cs`
  - [x] **Note:** NoOp is NOT registered in `AddEventStoreServer` -- it exists only for test/manual construction scenarios where `EventPublisher` is created without full DI
- [x] Task 4: Create `ProjectionUpdateOrchestrator` implementation (AC: 2, 3)
  - [x] Create `src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs`
  - [x] Register in `ServiceCollectionExtensions.cs` via `AddTransient` (the only registration in `AddEventStoreServer`)
- [x] Task 5: Wire fire-and-forget trigger into `EventPublisher` (AC: 1)
  - [x] Modify `src/Hexalith.EventStore.Server/Events/EventPublisher.cs` -- inject `IProjectionUpdateOrchestrator`, add `Task.Run` after successful publication
  - [x] Update all existing `new EventPublisher(...)` call sites in test files to include the new `IProjectionUpdateOrchestrator` parameter (pass `new NoOpProjectionUpdateOrchestrator()` or NSubstitute mock)
- [x] Task 6: Create tests (AC: 1, 2, 3)
  - [x] Create `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorTests.cs`
  - [x] Run full Tier 1 + Tier 2 tests to verify no regression
- [x] Task 7: Full build and test verification
  - [x] `dotnet build Hexalith.EventStore.slnx --configuration Release` -- 0 errors, 0 warnings
  - [x] All Tier 1 tests pass
  - [x] Tier 2 Server tests pass

## Dev Notes

### Architecture Context: Server-Managed Projection Builder (Mode B)

This story creates the **projection update orchestrator and fire-and-forget trigger** -- the third layer of Epic 11. The full pipeline is:

```
Events persisted -> EventPublisher.PublishEventsAsync (pub/sub)
  -> [NEW] Task.Run fire-and-forget:
     -> IProjectionUpdateOrchestrator.UpdateProjectionAsync(identity)
        -> AggregateActor.GetEventsAsync(0)  [actor proxy]
        -> Map EventEnvelope[] to ProjectionEventDto[]
        -> DaprClient.InvokeMethodAsync("appId", "project", ProjectionRequest)
        -> EventReplayProjectionActor.UpdateProjectionAsync(ProjectionState)  [actor proxy]
        -> ETag regenerated -> SignalR broadcast -> UI refreshes
```

**Story 11-1 provided:** `ProjectionEventDto`, `ProjectionRequest`, `ProjectionResponse` contracts and `AggregateActor.GetEventsAsync`.
**Story 11-2 provided:** `EventReplayProjectionActor`, `IProjectionWriteActor`, `ProjectionState` DTO, actor registration.
**This story creates:** `IProjectionUpdateOrchestrator`, `ProjectionUpdateOrchestrator`, `NoOpProjectionUpdateOrchestrator`, `ProjectionOptions`, fire-and-forget wiring in `EventPublisher`.
**Stories 11-4 and 11-5 build:** convention-based discovery/config and sample `/project` endpoint.

### Task 1: ProjectionOptions Configuration Model

**File:** `src/Hexalith.EventStore.Server/Configuration/ProjectionOptions.cs`

**Namespace:** `Hexalith.EventStore.Server.Configuration`

```csharp
public record ProjectionOptions
{
    /// <summary>
    /// Default refresh interval in milliseconds. 0 = immediate (fire-and-forget after persistence).
    /// Values > 0 enable background polling at that interval (Story 11-4).
    /// </summary>
    public int DefaultRefreshIntervalMs { get; init; }

    /// <summary>
    /// Per-domain refresh interval overrides. Key = domain name (kebab-case).
    /// </summary>
    public Dictionary<string, DomainProjectionOptions> Domains { get; init; } = [];

    /// <summary>
    /// Gets the effective refresh interval for a given domain.
    /// </summary>
    public int GetRefreshIntervalMs(string domain)
        => Domains.TryGetValue(domain, out DomainProjectionOptions? domainOptions)
            ? domainOptions.RefreshIntervalMs
            : DefaultRefreshIntervalMs;
}

public record DomainProjectionOptions
{
    public int RefreshIntervalMs { get; init; }
}
```

**Config section:** `EventStore:Projections` -- maps to:
```json
{
  "EventStore": {
    "Projections": {
      "DefaultRefreshIntervalMs": 0,
      "Domains": {
        "counter": { "RefreshIntervalMs": 0 }
      }
    }
  }
}
```

**DI Registration** -- add in `ServiceCollectionExtensions.cs` after the `SnapshotOptions` registration (around line 59):
```csharp
_ = services.AddOptions<ProjectionOptions>()
    .Bind(configuration.GetSection("EventStore:Projections"));
```

### Task 2: IProjectionUpdateOrchestrator Interface

**File:** `src/Hexalith.EventStore.Server/Projections/IProjectionUpdateOrchestrator.cs`

**Namespace:** `Hexalith.EventStore.Server.Projections`

```csharp
using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>
/// Orchestrates projection updates: reads new events, sends to domain service, updates ProjectionActor.
/// </summary>
public interface IProjectionUpdateOrchestrator
{
    /// <summary>
    /// Triggers a projection update for the specified aggregate. Fire-and-forget safe.
    /// </summary>
    /// <param name="identity">The aggregate identity (tenant, domain, aggregateId).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateProjectionAsync(AggregateIdentity identity, CancellationToken cancellationToken = default);
}
```

### Task 3: NoOpProjectionUpdateOrchestrator

**File:** `src/Hexalith.EventStore.Server/Projections/NoOpProjectionUpdateOrchestrator.cs`

**Pattern:** Follows `NoOpProjectionChangedBroadcaster` pattern (`src/Hexalith.EventStore.Server/Projections/NoOpProjectionChangedBroadcaster.cs`).

```csharp
using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>
/// No-op fallback for IProjectionUpdateOrchestrator.
/// Used when projection infrastructure is not fully wired.
/// Same pattern as NoOpProjectionChangedBroadcaster.
/// </summary>
public sealed class NoOpProjectionUpdateOrchestrator : IProjectionUpdateOrchestrator
{
    public Task UpdateProjectionAsync(AggregateIdentity identity, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
```

**DI:** NOT registered in `AddEventStoreServer`. This class is used only for manual test construction (e.g., `new EventPublisher(..., new NoOpProjectionUpdateOrchestrator(), ...)`). The real `ProjectionUpdateOrchestrator` is the only registration in `AddEventStoreServer`.

### Task 4: ProjectionUpdateOrchestrator Implementation

**File:** `src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs`

**Namespace:** `Hexalith.EventStore.Server.Projections`

**Constructor dependencies:**
- `IActorProxyFactory actorProxyFactory` -- create actor proxies (same as `QueryRouter`)
- `DaprClient daprClient` -- DAPR service invocation (same as `DaprDomainServiceInvoker`)
- `IDomainServiceResolver resolver` -- resolve domain service AppId
- `ILogger<ProjectionUpdateOrchestrator> logger` -- structured logging

**Implementation flow:**

1. Resolve domain service registration via `resolver.ResolveAsync(identity.TenantId, identity.Domain, "v1", cancellationToken)`
   - If `null` (no domain service registered for this domain), log at **Information** level and return silently -- this is the primary troubleshooting signal when projections aren't updating, must be visible at default log levels
2. Create `IAggregateActor` proxy via `actorProxyFactory.CreateActorProxy<IAggregateActor>(new ActorId(identity.ActorId), "AggregateActor")`
   - `AggregateIdentity.ActorId` returns `"{tenantId}:{domain}:{aggregateId}"` -- same format used by `CommandRouter`
3. Call `GetEventsAsync(0)` -- replays all events (no checkpoint tracking in this initial implementation, deferred optimization)
   - **KNOWN PERFORMANCE LIMITATION:** Every trigger replays the entire event history. For aggregates with hundreds of events, this is expensive. Add a `// TODO: Story 11-4+ checkpoint tracking -- replace GetEventsAsync(0) with GetEventsAsync(lastCheckpoint)` code comment at the call site. This is acceptable for now because domain services handle idempotent replay and the full event stream is typically small in the current use case.
   - If empty array returned, log debug and return (new aggregate with no events yet)
4. Map `EventEnvelope[]` to `ProjectionEventDto[]`:
   ```csharp
   var projectionEvents = new ProjectionEventDto[events.Length];
   for (int i = 0; i < events.Length; i++)
   {
       EventEnvelope e = events[i];
       projectionEvents[i] = new ProjectionEventDto(
           e.EventTypeName,
           e.Payload,
           e.SerializationFormat,
           e.SequenceNumber,
           e.Timestamp,
           e.CorrelationId);
   }
   ```
5. Create `ProjectionRequest` and invoke domain service via DAPR:
   ```csharp
   var request = new ProjectionRequest(identity.TenantId, identity.Domain, identity.AggregateId, projectionEvents);
   ProjectionResponse response = await daprClient.InvokeMethodAsync<ProjectionRequest, ProjectionResponse>(
       registration.AppId,
       "project",
       request,
       cancellationToken).ConfigureAwait(false);
   ```
   **CRITICAL:** The method name is `"project"` (matching the domain service's `MapPost("/project", ...)` endpoint).
6. Derive projection actor ID using `QueryActorIdHelper`:
   ```csharp
   string projectionActorId = QueryActorIdHelper.DeriveActorId(
       response.ProjectionType,
       identity.TenantId,
       identity.AggregateId,
       []);
   ```
   This follows the same 3-tier routing model as `QueryRouter` -- Tier 1 (entity-scoped) because we have an `AggregateId`.
7. Create `IProjectionWriteActor` proxy and update state:
   ```csharp
   IProjectionWriteActor writeProxy = actorProxyFactory.CreateActorProxy<IProjectionWriteActor>(
       new ActorId(projectionActorId),
       QueryRouter.ProjectionActorTypeName);  // "ProjectionActor"
   await writeProxy.UpdateProjectionAsync(
       new ProjectionState(response.ProjectionType, identity.TenantId, response.State))
       .ConfigureAwait(false);
   ```
   **Note:** `EventReplayProjectionActor.UpdateProjectionAsync` already handles ETag regeneration + SignalR broadcast (Story 11-2). The orchestrator does NOT call `IProjectionChangedBroadcaster` directly -- it delegates to the actor.

**CRITICAL: Entire method wrapped in try/catch.** Log warnings on failure, never throw. This is fire-and-forget safe -- any exception must be swallowed after logging.

**Logging:** Use `LoggerMessage` source-generated partial methods. Use event IDs in the 1110-1119 range (1100-1103 are already used by `CommandRouter`/`SubmitCommandHandler`):
- `1110` -- `UpdateProjectionAsync` started
- `1111` -- No domain service registered for domain (Information level -- critical for troubleshooting when projections aren't updating; visible at default log levels)
- `1112` -- No events found (debug)
- `1113` -- Domain service invocation successful
- `1114` -- Projection state updated
- `1115` -- Projection update failed (warning, with exception)

### Task 5: Wire Fire-and-Forget Trigger into EventPublisher

**File:** `src/Hexalith.EventStore.Server/Events/EventPublisher.cs`

**Change 1:** Add constructor parameter `IProjectionUpdateOrchestrator projectionOrchestrator`:
```csharp
public partial class EventPublisher(
    DaprClient daprClient,
    IOptions<EventPublisherOptions> options,
    ILogger<EventPublisher> logger,
    IEventPayloadProtectionService payloadProtectionService,
    IProjectionUpdateOrchestrator projectionOrchestrator,
    ITopicNameValidator? topicNameValidator = null) : IEventPublisher
```

**CRITICAL:** Place `projectionOrchestrator` BEFORE the optional `topicNameValidator` parameter -- optional params must be last.

**Change 2:** After the `activity?.SetStatus(ActivityStatusCode.Ok)` call (line 121) and before the `return` statement (line 122), add:
```csharp
// Fire-and-forget projection update (Mode B immediate trigger)
// NOTE: Unbounded concurrency -- high-throughput aggregates may spawn many concurrent tasks.
// Acceptable for current scope; checkpoint tracker + SemaphoreSlim would bound this in a follow-up.
_ = Task.Run(async () =>
{
    try
    {
        await projectionOrchestrator.UpdateProjectionAsync(identity, CancellationToken.None)
            .ConfigureAwait(false);
    }
    catch (Exception ex)
    {
        Log.ProjectionUpdateFailed(logger, ex, identity.TenantId, identity.Domain, identity.AggregateId, correlationId);
    }
}, CancellationToken.None);
```

**CRITICAL: Uses `CancellationToken.None`** for both `Task.Run` and `UpdateProjectionAsync` -- the original request's token may already be cancelled by the time the background task runs.

**CRITICAL: `_ = Task.Run(...)` discards the Task** -- this is intentional fire-and-forget. The `try/catch` inside ensures no unobserved exceptions.

**Change 3:** Add required using:
```csharp
using Hexalith.EventStore.Server.Projections;
```

**Change 4:** Add new log message in the `Log` partial class (EventId 3102 continues EventPublisher's existing 3100-3101 range):
```csharp
[LoggerMessage(
    EventId = 3102,
    Level = LogLevel.Warning,
    Message = "Fire-and-forget projection update failed: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, CorrelationId={CorrelationId}, Stage=ProjectionUpdateFailed")]
public static partial void ProjectionUpdateFailed(
    ILogger logger,
    Exception ex,
    string tenantId,
    string domain,
    string aggregateId,
    string correlationId);
```

**DI note:** `IProjectionUpdateOrchestrator` is always registered (NoOp or real). .NET DI does NOT use default parameter values -- it will fail to resolve if the interface is not registered. The NoOp fallback ensures existing tests continue passing without changes to test DI setup.

### Task 6: Tests

**File:** `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorTests.cs`

**Test setup:**
- NSubstitute mocks for `IActorProxyFactory`, `DaprClient`, `IDomainServiceResolver`
- `NullLogger<ProjectionUpdateOrchestrator>` for logging (no need to assert log messages)
- Standard `AggregateIdentity` with tenant, domain, aggregateId

**Tests to create:**

1. `UpdateProjectionAsync_NoDomainServiceRegistered_ReturnsWithoutError` (AC: 3)
   - Mock `resolver.ResolveAsync(...)` to return `null`
   - Call `UpdateProjectionAsync`
   - Verify no actor proxy calls, no DAPR invocations

2. `UpdateProjectionAsync_NoEvents_ReturnsWithoutCallingDomainService` (AC: 3)
   - Mock resolver to return valid registration
   - Mock `IAggregateActor.GetEventsAsync(0)` to return empty array
   - Call `UpdateProjectionAsync`
   - Verify no DAPR service invocation

3. `UpdateProjectionAsync_WithEvents_CallsDomainServiceAndUpdatesActor` (AC: 2)
   - Mock resolver, aggregate proxy with events, DAPR client response
   - Verify `DaprClient.InvokeMethodAsync` called with correct `ProjectionRequest`
   - Verify `IProjectionWriteActor.UpdateProjectionAsync` called with correct `ProjectionState`

4. `UpdateProjectionAsync_MapsEventEnvelopesToProjectionEventDtos` (AC: 2)
   - Create `EventEnvelope` with all 17 fields
   - Verify mapped `ProjectionEventDto` contains only the 6 public fields (EventTypeName, Payload, SerializationFormat, SequenceNumber, Timestamp, CorrelationId)
   - Verify internal fields (CausationId, UserId, DomainServiceVersion, GlobalPosition, MetadataVersion, Extensions, MessageId, AggregateId, AggregateType, TenantId, Domain) are NOT passed through

5. `UpdateProjectionAsync_DomainServiceFails_LogsWarningAndDoesNotThrow` (AC: 3)
   - Mock `DaprClient.InvokeMethodAsync` to throw
   - Call `UpdateProjectionAsync`
   - Verify no exception thrown (fire-and-forget safe)

6. `UpdateProjectionAsync_ActorProxyFails_LogsWarningAndDoesNotThrow` (AC: 3)
   - Mock `IProjectionWriteActor.UpdateProjectionAsync` to throw
   - Verify no exception thrown

7. `UpdateProjectionAsync_UsesCorrectActorTypeNames` (AC: 2)
   - Verify `IAggregateActor` proxy created with `"AggregateActor"` type name
   - Verify `IProjectionWriteActor` proxy created with `"ProjectionActor"` type name (from `QueryRouter.ProjectionActorTypeName`)

8. `UpdateProjectionAsync_DeriveCorrectProjectionActorId` (AC: 2)
   - Verify the projection actor ID follows the `QueryActorIdHelper.DeriveActorId` pattern: `"{ProjectionType}:{TenantId}:{AggregateId}"`

9. `EventPublisher_AfterSuccessfulPublish_TriggersProjectionOrchestrator` (AC: 1)
   - Create `EventPublisher` with an NSubstitute mock for `IProjectionUpdateOrchestrator`
   - Call `PublishEventsAsync` with valid events and mocked DaprClient
   - Wait briefly (e.g., `await Task.Delay(100)`) to allow fire-and-forget to complete
   - Verify `IProjectionUpdateOrchestrator.UpdateProjectionAsync` was called with the correct `AggregateIdentity`
   - **Note:** This verifies AC 1 at the EventPublisher level (trigger wiring), not just at the orchestrator level

10. `UpdateProjectionAsync_MalformedDomainServiceResponse_LogsWarningAndDoesNotThrow` (AC: 3)
  - Validate this scenario in **integration tests** (Tier 3) using a real DAPR invocation path and malformed/invalid `/project` response payload.
  - Unit-level mocking of `DaprClient.InvokeMethodAsync<TRequest, TResponse>` is not reliable with the current NSubstitute setup.
  - At unit level, verify fail-open behavior for resolver/actor failures and validate response guards where test seams exist.

**Test helper notes:**
- `EventEnvelope` requires 17 constructor params (MessageId, AggregateId, AggregateType, TenantId, Domain, SequenceNumber, GlobalPosition, Timestamp, CorrelationId, CausationId, UserId, DomainServiceVersion, EventTypeName, MetadataVersion, SerializationFormat, Payload, Extensions)
- `DomainServiceRegistration` takes (AppId, MethodName, TenantId, Domain, Version)
- `ProjectionResponse` takes (ProjectionType, JsonElement State) -- use `JsonDocument.Parse("{\"count\":1}").RootElement` for test JSON
- `AggregateIdentity.ActorId` returns `"{tenantId}:{domain}:{aggregateId}"` -- used for aggregate actor proxy creation
- Mock `DaprClient.InvokeMethodAsync<TRequest, TResponse>` carefully -- the overload must match exactly

### CRITICAL: Scope Boundaries

**This story ONLY creates:**
- `ProjectionOptions` + `DomainProjectionOptions` configuration records
- `IProjectionUpdateOrchestrator` interface
- `ProjectionUpdateOrchestrator` implementation
- `NoOpProjectionUpdateOrchestrator` fallback
- Fire-and-forget trigger in `EventPublisher`
- DI registrations for orchestrator + options
- Tests for the orchestrator

**Do NOT create or modify:**
- Counter Sample `/project` endpoint -- that's Story 11-5
- `CounterProjectionHandler` -- that's Story 11-5
- Convention-based discovery logic -- that's Story 11-4
- `ProjectionPollerService` (background `IHostedService`) -- deferred, not in this epic sprint
- `ProjectionCheckpointTracker` -- deferred, not in this story (uses `GetEventsAsync(0)` to replay all)
- `EventReplayProjectionActor` -- no changes needed (created in Story 11-2, works as-is)
- `IProjectionWriteActor` -- no changes needed (created in Story 11-2)
- `ProjectionState` -- no changes needed (created in Story 11-2)
- `AggregateActor` -- no changes needed (only `EventPublisher` is modified)
- `CachingProjectionActor` -- no changes needed
- `QueryRouter` -- no changes needed
- Any projection contracts (ProjectionEventDto, ProjectionRequest, ProjectionResponse) -- created in Story 11-1

### CRITICAL: Do NOT Break Existing Tests

- `EventPublisher` gains a new required constructor parameter (`IProjectionUpdateOrchestrator`). **7 test files** with `new EventPublisher(...)` call sites must be updated to include the new parameter. Pass `new NoOpProjectionUpdateOrchestrator()` as the 5th argument:
  - `tests/Hexalith.EventStore.Server.Tests/Events/EventPublisherTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Events/EventPublisherRetryComplianceTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Events/SubscriberIdempotencyTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Telemetry/EndToEndTraceTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Logging/StructuredLoggingCompletenessTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Logging/LogLevelConventionTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Logging/CausationIdLoggingTests.cs`
- `ServiceCollectionExtensions` changes are additive (new registrations)
- No existing interfaces are modified
- All existing tests (Tier 1: 709, Tier 2: 1552) must continue to pass

### Existing Code Patterns to Follow

**DAPR service invocation** (from `DaprDomainServiceInvoker.cs`):
```csharp
wireResult = await daprClient
    .InvokeMethodAsync<DomainServiceRequest, DomainServiceWireResult>(
        registration.AppId,
        registration.MethodName,
        request,
        cancellationToken)
    .ConfigureAwait(false);
```

**Actor proxy creation** (from `QueryRouter.cs`):
```csharp
IProjectionActor proxy = actorProxyFactory.CreateActorProxy<IProjectionActor>(
    new ActorId(actorId),
    ProjectionActorTypeName);
```

**Fire-and-forget pattern** -- Use `_ = Task.Run(async () => { try { ... } catch { log } })` with `CancellationToken.None`.

**NoOp fallback pattern** (from `NoOpProjectionChangedBroadcaster.cs`):
```csharp
public sealed class NoOpProjectionUpdateOrchestrator : IProjectionUpdateOrchestrator
{
    public Task UpdateProjectionAsync(AggregateIdentity identity, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
```

**LoggerMessage pattern** (from `EventReplayProjectionActor.cs` and `DaprProjectionChangeNotifier.cs`):
```csharp
private static partial class Log
{
    [LoggerMessage(EventId = 1110, Level = LogLevel.Debug, Message = "...")]
    public static partial void MethodName(ILogger logger, ...);
}
```

**Options registration pattern** (from `ServiceCollectionExtensions.cs` line 56-59):
```csharp
_ = services.AddOptions<ProjectionOptions>()
    .Bind(configuration.GetSection("EventStore:Projections"));
```

**DI registration** (same pattern as `NoOpProjectionChangedBroadcaster` / `IProjectionChangedBroadcaster`):

In `AddEventStoreServer`, register only the real implementation:
```csharp
_ = services.AddTransient<IProjectionUpdateOrchestrator, ProjectionUpdateOrchestrator>();
```

The `NoOpProjectionUpdateOrchestrator` exists as a fallback for test scenarios where `EventPublisher` is constructed manually without full DI. Tests can pass `new NoOpProjectionUpdateOrchestrator()` directly. If a "lite" registration method is ever needed (e.g., tests without real projection infra), they can `TryAddTransient<NoOp>` before calling `AddEventStoreServer`.

**EventEnvelope to ProjectionEventDto mapping** -- strip internal fields:
```csharp
EventEnvelope e = events[i];
projectionEvents[i] = new ProjectionEventDto(
    e.EventTypeName, e.Payload, e.SerializationFormat,
    e.SequenceNumber, e.Timestamp, e.CorrelationId);
```
Internal-only fields NOT passed through: `MessageId`, `AggregateId`, `AggregateType`, `TenantId`, `Domain`, `GlobalPosition`, `CausationId`, `UserId`, `DomainServiceVersion`, `MetadataVersion`, `Extensions`.

### Project Structure Notes

```
src/Hexalith.EventStore.Server/Configuration/
  ProjectionOptions.cs                          [NEW -- this story]
  ServiceCollectionExtensions.cs                [MODIFY -- register orchestrator + options]

src/Hexalith.EventStore.Server/Projections/
  IProjectionUpdateOrchestrator.cs             [NEW -- this story]
  ProjectionUpdateOrchestrator.cs               [NEW -- this story]
  NoOpProjectionUpdateOrchestrator.cs           [NEW -- this story]
  DaprProjectionChangeNotifier.cs              [EXISTS -- reference for patterns]
  NoOpProjectionChangedBroadcaster.cs          [EXISTS -- reference for NoOp pattern]

src/Hexalith.EventStore.Server/Events/
  EventPublisher.cs                             [MODIFY -- add fire-and-forget trigger]

tests/Hexalith.EventStore.Server.Tests/Projections/
  ProjectionUpdateOrchestratorTests.cs          [NEW -- this story]
```

### Previous Story Intelligence (Story 11-2)

Key learnings from the previous story:
- **Branch naming:** `feat/story-11-2-eventreplayprojectionactor-and-projection-state-storage`
- **Commit message pattern:** `feat: <description for Story 11-3>`
- **DAPR actor registration:** `RegisterActor<EventReplayProjectionActor>(QueryRouter.ProjectionActorTypeName)` -- the `RegisterActor<T>(string actorTypeName)` overload takes a string actor type name directly
- **Test count baseline:** Tier 1: 709 passed, Tier 2: 1552 passed (total 2261)
- **Build must pass:** `dotnet build Hexalith.EventStore.slnx --configuration Release` with 0 warnings, 0 errors (TreatWarningsAsErrors = true)
- **Notification chain:** `EventReplayProjectionActor.UpdateProjectionAsync` already calls `IProjectionChangeNotifier.NotifyProjectionChangedAsync` (ETag regen + SignalR). The orchestrator does NOT duplicate this -- it just calls the actor.
- **`ProjectionState` DTO:** Takes `(string ProjectionType, string TenantId, JsonElement State)`. The `TenantId` comes from `AggregateIdentity.TenantId`, not the response.
- **Fail-open hardening:** Story 11-2 added guards for empty `ProjectionType`/`TenantId` in `EventReplayProjectionActor`. The orchestrator should pass valid values.

### Git Intelligence

Recent commits (last 5):
- `ed47cd6` Merge PR #125 (Story 11-2 EventReplayProjectionActor)
- `d390695` feat: Implement EventReplayProjectionActor and projection state storage
- `6ba7598` Merge PR #124 (Story 11-1 projection contracts)
- `fd136a0` feat: Add projection event DTOs and tests for event handling
- `14f9647` feat: Implement Projection Contract DTOs and AggregateActor Event Reading

Pattern: feature branches merged via PRs, conventional commit messages (`feat: ...`).

### Package/Framework Reference

- .NET 10 SDK `10.0.103` (from `global.json`)
- DAPR SDK `1.17.0` -- `DaprClient.InvokeMethodAsync<TRequest, TResponse>`, `IActorProxyFactory`, `ActorId`
- xUnit `2.9.3`, Shouldly `4.3.0`, NSubstitute `5.3.0`
- `TreatWarningsAsErrors = true` -- any warning is a build failure
- `ConfigureAwait(false)` on all async calls (library code convention)
- File-scoped namespaces, Allman brace style, 4-space indentation

### Architecture Decisions

**ADR-1: Fire-and-forget via `Task.Run` vs `IHostedService` queue.**
Chosen `Task.Run` for simplicity. Counter sample has negligible throughput; a bounded queue adds complexity (backpressure policy, shutdown draining) without current benefit. If high-throughput aggregates appear, a queued approach can be added without modifying existing code.

**ADR-2: Inject into `EventPublisher` vs `AggregateActor`.**
Chosen `EventPublisher` per design spec. AggregateActor already has 9 constructor deps. EventPublisher is the natural boundary -- fires after events are published to pub/sub, keeping projection concerns decoupled from command processing.

**ADR-3: `GetEventsAsync(0)` full replay vs checkpoint-tracked incremental.**
Chosen full replay for correctness and simplicity. Incremental requires checkpoint persistence, failure recovery, and at-least-once delivery guarantees. Deferred to follow-up. O(N) reads acceptable for small event streams.

**ADR-4: Real orchestrator via `AddTransient` in `AddEventStoreServer`.**
Projection pipeline is a core server feature. If you call `AddEventStoreServer`, you get projections. NoOp exists only for manual test construction. Matches existing `DaprProjectionChangeNotifier` registration pattern.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 11.3] -- Story requirements and acceptance criteria
- [Source: _bmad-output/planning-artifacts/epics.md#Epic 11] -- Epic overview: Server-Managed Projection Builder
- [Source: docs/superpowers/specs/2026-03-15-server-managed-projection-builder-design.md#Immediate Trigger] -- Design spec: immediate trigger, error handling, checkpoint deferral
- [Source: docs/superpowers/plans/2026-03-15-server-managed-projection-builder.md#Chunk 3] -- Tasks 5-6: ProjectionOptions + ProjectionUpdateOrchestrator
- [Source: docs/superpowers/plans/2026-03-15-server-managed-projection-builder.md#Chunk 4] -- Task 8: Wire immediate trigger into EventPublisher
- [Source: src/Hexalith.EventStore.Server/Events/EventPublisher.cs] -- Hook point for fire-and-forget trigger (after line 119)
- [Source: src/Hexalith.EventStore.Server/DomainServices/DaprDomainServiceInvoker.cs] -- DAPR service invocation pattern
- [Source: src/Hexalith.EventStore.Server/Queries/QueryRouter.cs] -- Actor proxy creation pattern, `ProjectionActorTypeName = "ProjectionActor"`
- [Source: src/Hexalith.EventStore.Server/Queries/QueryActorIdHelper.cs] -- Projection actor ID derivation (3-tier model)
- [Source: src/Hexalith.EventStore.Server/DomainServices/IDomainServiceResolver.cs] -- Domain service resolution interface
- [Source: src/Hexalith.EventStore.Server/DomainServices/DomainServiceRegistration.cs] -- Registration record (AppId, MethodName, TenantId, Domain, Version)
- [Source: src/Hexalith.EventStore.Server/Projections/NoOpProjectionChangedBroadcaster.cs] -- NoOp fallback pattern
- [Source: src/Hexalith.EventStore.Server/Actors/EventReplayProjectionActor.cs] -- Target actor for UpdateProjectionAsync
- [Source: src/Hexalith.EventStore.Server/Actors/IProjectionWriteActor.cs] -- Write interface
- [Source: src/Hexalith.EventStore.Server/Actors/ProjectionState.cs] -- DTO: (ProjectionType, TenantId, State)
- [Source: src/Hexalith.EventStore.Server/Actors/IAggregateActor.cs] -- GetEventsAsync(long fromSequence) interface
- [Source: src/Hexalith.EventStore.Server/Events/EventEnvelope.cs] -- 17-field event record (source for mapping)
- [Source: src/Hexalith.EventStore.Contracts/Projections/ProjectionEventDto.cs] -- 6-field wire DTO (target for mapping)
- [Source: src/Hexalith.EventStore.Contracts/Projections/ProjectionRequest.cs] -- Domain service request
- [Source: src/Hexalith.EventStore.Contracts/Projections/ProjectionResponse.cs] -- Domain service response
- [Source: src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs] -- DI registration point
- [Source: _bmad-output/implementation-artifacts/11-2-eventreplayprojectionactor-and-projection-state-storage.md] -- Previous story (test counts, patterns, conventions)

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

### Completion Notes List

- Task 1: Created `ProjectionOptions` and `DomainProjectionOptions` records with `GetRefreshIntervalMs(domain)` method. Registered in DI via `AddOptions<ProjectionOptions>().Bind(configuration.GetSection("EventStore:Projections"))`.
- Task 2: Created `IProjectionUpdateOrchestrator` interface with `UpdateProjectionAsync(AggregateIdentity, CancellationToken)`.
- Task 3: Created `NoOpProjectionUpdateOrchestrator` following `NoOpProjectionChangedBroadcaster` pattern. Not registered in DI -- exists for test/manual construction only.
- Task 4: Created `ProjectionUpdateOrchestrator` with full pipeline: resolve domain service -> create aggregate actor proxy -> GetEventsAsync(0) -> map EventEnvelope[] to ProjectionEventDto[] -> DAPR InvokeMethodAsync -> derive projection actor ID -> UpdateProjectionAsync on write actor. Entire method wrapped in try/catch (fire-and-forget safe). LoggerMessage source-generated logging (EventIds 1110-1115). Registered as `AddTransient<IProjectionUpdateOrchestrator, ProjectionUpdateOrchestrator>()`.
- Task 5: Added `IProjectionUpdateOrchestrator projectionOrchestrator` parameter to `EventPublisher` constructor. Added `Task.Run` fire-and-forget trigger after successful publication with `CancellationToken.None`. Added log message (EventId 3102) for projection update failures. Updated 9 call sites across 7 test files with `new NoOpProjectionUpdateOrchestrator()`.
- Task 6: Created 12 unit tests in `ProjectionUpdateOrchestratorTests.cs`. Note: `DaprClient.InvokeMethodAsync<TRequest, TResponse>` is non-virtual and cannot be mocked with NSubstitute (same limitation as `DaprDomainServiceInvokerTests`). Tests verify: resolver calls, actor proxy creation, event reading, error handling (fire-and-forget safety), EventPublisher trigger wiring, DTO mapping, and actor ID derivation. Malformed `/project` response validation is deferred to integration tests.
- Added Tier 3 malformed-response integration path: fault-injection endpoint in sample service, dedicated Aspire fixture/collection, and active `ProjectionMalformedResponseE2ETests` assertions validating fail-open command completion.
- Fixed DAPR actor remoting serialization for projection replay by adding `[DataContract]/[DataMember]` annotations to `Server.Events.EventEnvelope` used by `IAggregateActor.GetEventsAsync`.
- Task 7: Build passes with 0 warnings, 0 errors. Tier 1: 709 tests pass (unchanged). Tier 2: 1564 tests pass (1552 existing + 12 new, no regressions).
- Targeted Tier 3 validation: `dotnet test tests/Hexalith.EventStore.IntegrationTests --filter "FullyQualifiedName~ProjectionMalformedResponseE2ETests"` passes (1/1).

### Change Log

- 2026-03-20: Story 11-3 implementation complete. Created ProjectionOptions, IProjectionUpdateOrchestrator, NoOpProjectionUpdateOrchestrator, ProjectionUpdateOrchestrator, fire-and-forget trigger in EventPublisher, 12 unit tests.
- 2026-03-20: Activated Tier 3 malformed `/project` fail-open test path with Aspire fault fixture and fixed actor remoting serialization for `EventEnvelope`.

### File List

New files:
- src/Hexalith.EventStore.Server/Configuration/ProjectionOptions.cs
- src/Hexalith.EventStore.Server/Projections/IProjectionUpdateOrchestrator.cs
- src/Hexalith.EventStore.Server/Projections/NoOpProjectionUpdateOrchestrator.cs
- src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs
- tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorTests.cs
- tests/Hexalith.EventStore.IntegrationTests/ContractTests/ProjectionMalformedResponseE2ETests.cs
- tests/Hexalith.EventStore.IntegrationTests/Fixtures/AspireProjectionFaultTestFixture.cs
- tests/Hexalith.EventStore.IntegrationTests/Fixtures/AspireProjectionFaultTestCollection.cs

Modified files:
- src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs
- src/Hexalith.EventStore.Server/Events/EventEnvelope.cs
- src/Hexalith.EventStore.Server/Events/EventPublisher.cs
- samples/Hexalith.EventStore.Sample/Program.cs
- tests/Hexalith.EventStore.IntegrationTests/Helpers/ContractTestHelpers.cs
- tests/Hexalith.EventStore.Server.Tests/Events/EventPublisherTests.cs
- tests/Hexalith.EventStore.Server.Tests/Events/EventPublisherRetryComplianceTests.cs
- tests/Hexalith.EventStore.Server.Tests/Events/SubscriberIdempotencyTests.cs
- tests/Hexalith.EventStore.Server.Tests/Telemetry/EndToEndTraceTests.cs
- tests/Hexalith.EventStore.Server.Tests/Logging/StructuredLoggingCompletenessTests.cs
- tests/Hexalith.EventStore.Server.Tests/Logging/LogLevelConventionTests.cs
- tests/Hexalith.EventStore.Server.Tests/Logging/CausationIdLoggingTests.cs
