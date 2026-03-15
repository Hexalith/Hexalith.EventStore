# Server-Managed Projection Builder Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Enable queries to return real projection state by building a server-managed projection pipeline that delivers events to domain services and caches the returned state.

**Architecture:** CommandApi reads new events from AggregateActor, sends them to the domain service's `/project` endpoint via DAPR service invocation, and stores the returned projection state in a generic ProjectionActor. Immediate (fire-and-forget) or polled delivery modes are supported. The domain microservice owns the Apply logic and projection state.

**Tech Stack:** .NET 10, DAPR Actors, DAPR Service Invocation, MediatR, xUnit + Shouldly + NSubstitute

**Spec:** `docs/superpowers/specs/2026-03-15-server-managed-projection-builder-design.md`

---

## File Map

| File | Action | Responsibility |
|------|--------|---------------|
| `src/Hexalith.EventStore.Contracts/Projections/ProjectionEventDto.cs` | Create | Wire-format event DTO for `/project` endpoint |
| `src/Hexalith.EventStore.Contracts/Projections/ProjectionRequest.cs` | Create | `/project` endpoint request DTO |
| `src/Hexalith.EventStore.Contracts/Projections/ProjectionResponse.cs` | Create | `/project` endpoint response DTO |
| `src/Hexalith.EventStore.Server/Actors/IAggregateActor.cs` | Modify | Add `GetEventsAsync(long fromSequence)` |
| `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` | Modify | Implement `GetEventsAsync` |
| `src/Hexalith.EventStore.Server/Actors/IProjectionWriteActor.cs` | Create | Write interface for projection state updates |
| `src/Hexalith.EventStore.Server/Actors/EventReplayProjectionActor.cs` | Create | Concrete ProjectionActor (read + write) |
| `src/Hexalith.EventStore.Server/Projections/IProjectionUpdateOrchestrator.cs` | Create | Interface for triggering projection updates |
| `src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs` | Create | Immediate trigger + domain service invocation |
| `src/Hexalith.EventStore.Server/Configuration/ProjectionOptions.cs` | Create | Configuration model |
| `src/Hexalith.EventStore.Server/Projections/NoOpProjectionUpdateOrchestrator.cs` | Create | No-op fallback for DI when orchestrator not yet wired |
| `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs` | Modify | Register ProjectionActor + projection services |
| `samples/Hexalith.EventStore.Sample/Counter/CounterProjectionHandler.cs` | Create | Thin projection handler reusing existing Apply logic |
| `samples/Hexalith.EventStore.Sample/Program.cs` | Modify | Add `/project` endpoint |

---

## Chunk 1: Contract DTOs and AggregateActor.GetEventsAsync

### Task 1: Projection Contract DTOs

**Files:**
- Create: `src/Hexalith.EventStore.Contracts/Projections/ProjectionEventDto.cs`
- Create: `src/Hexalith.EventStore.Contracts/Projections/ProjectionRequest.cs`
- Create: `src/Hexalith.EventStore.Contracts/Projections/ProjectionResponse.cs`
- Test: `tests/Hexalith.EventStore.Contracts.Tests/Projections/ProjectionContractTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/Hexalith.EventStore.Contracts.Tests/Projections/ProjectionContractTests.cs
using System.Text.Json;

using Hexalith.EventStore.Contracts.Projections;

using Shouldly;

namespace Hexalith.EventStore.Contracts.Tests.Projections;

public class ProjectionContractTests {
    [Fact]
    public void ProjectionEventDto_RoundTrips_Json() {
        var dto = new ProjectionEventDto(
            "CounterIncremented",
            [1, 2, 3],
            "json",
            42,
            DateTimeOffset.UtcNow,
            "corr-1");

        string json = JsonSerializer.Serialize(dto);
        ProjectionEventDto? deserialized = JsonSerializer.Deserialize<ProjectionEventDto>(json);

        deserialized.ShouldNotBeNull();
        deserialized.EventTypeName.ShouldBe("CounterIncremented");
        deserialized.SequenceNumber.ShouldBe(42);
        deserialized.SerializationFormat.ShouldBe("json");
    }

    [Fact]
    public void ProjectionRequest_RoundTrips_Json() {
        var request = new ProjectionRequest(
            "tenant-a",
            "counter",
            "counter-1",
            [new ProjectionEventDto("CounterIncremented", [], "json", 1, DateTimeOffset.UtcNow, "corr-1")]);

        string json = JsonSerializer.Serialize(request);
        ProjectionRequest? deserialized = JsonSerializer.Deserialize<ProjectionRequest>(json);

        deserialized.ShouldNotBeNull();
        deserialized.TenantId.ShouldBe("tenant-a");
        deserialized.Domain.ShouldBe("counter");
        deserialized.AggregateId.ShouldBe("counter-1");
        deserialized.Events.Length.ShouldBe(1);
    }

    [Fact]
    public void ProjectionResponse_RoundTrips_Json() {
        JsonElement state = JsonDocument.Parse("{\"count\":5}").RootElement;
        var response = new ProjectionResponse("counter", state);

        string json = JsonSerializer.Serialize(response);
        ProjectionResponse? deserialized = JsonSerializer.Deserialize<ProjectionResponse>(json);

        deserialized.ShouldNotBeNull();
        deserialized.ProjectionType.ShouldBe("counter");
        deserialized.State.GetProperty("count").GetInt32().ShouldBe(5);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Hexalith.EventStore.Contracts.Tests/ --filter "FullyQualifiedName~ProjectionContractTests" -v n`
Expected: FAIL — types do not exist yet

- [ ] **Step 3: Create ProjectionEventDto**

```csharp
// src/Hexalith.EventStore.Contracts/Projections/ProjectionEventDto.cs
namespace Hexalith.EventStore.Contracts.Projections;

/// <summary>
/// Wire-format event DTO sent to domain services for projection building.
/// Deliberately excludes Server-internal fields (CausationId, UserId, DomainServiceVersion, Extensions).
/// </summary>
public record ProjectionEventDto(
    string EventTypeName,
    byte[] Payload,
    string SerializationFormat,
    long SequenceNumber,
    DateTimeOffset Timestamp,
    string CorrelationId);
```

- [ ] **Step 4: Create ProjectionRequest**

```csharp
// src/Hexalith.EventStore.Contracts/Projections/ProjectionRequest.cs
namespace Hexalith.EventStore.Contracts.Projections;

/// <summary>
/// Request DTO for the domain service /project endpoint.
/// Per-aggregate granularity: one call per aggregate instance.
/// </summary>
public record ProjectionRequest(
    string TenantId,
    string Domain,
    string AggregateId,
    ProjectionEventDto[] Events);
```

- [ ] **Step 5: Create ProjectionResponse**

```csharp
// src/Hexalith.EventStore.Contracts/Projections/ProjectionResponse.cs
using System.Text.Json;

namespace Hexalith.EventStore.Contracts.Projections;

/// <summary>
/// Response DTO from the domain service /project endpoint.
/// State is opaque JSON — CommandApi stores and serves it without understanding the schema.
/// </summary>
public record ProjectionResponse(
    string ProjectionType,
    JsonElement State);
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test tests/Hexalith.EventStore.Contracts.Tests/ --filter "FullyQualifiedName~ProjectionContractTests" -v n`
Expected: PASS — all 3 tests green

- [ ] **Step 7: Commit**

```bash
git add src/Hexalith.EventStore.Contracts/Projections/ProjectionEventDto.cs \
        src/Hexalith.EventStore.Contracts/Projections/ProjectionRequest.cs \
        src/Hexalith.EventStore.Contracts/Projections/ProjectionResponse.cs \
        tests/Hexalith.EventStore.Contracts.Tests/Projections/ProjectionContractTests.cs
git commit -m "feat: add projection contract DTOs (ProjectionEventDto, ProjectionRequest, ProjectionResponse)"
```

---

### Task 2: AggregateActor.GetEventsAsync

**Files:**
- Modify: `src/Hexalith.EventStore.Server/Actors/IAggregateActor.cs`
- Modify: `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs`
- Test: `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorGetEventsTests.cs`

**Context:** `IAggregateActor` currently only has `ProcessCommandAsync`. Add a read-only `GetEventsAsync` method. The implementation reuses the existing `EventStreamReader` pattern (per-call, uses `IActorStateManager`). Check `AggregateActor.cs` for the existing constructor parameters and the `EventStreamReader` usage in Step 3 of the command pipeline.

- [ ] **Step 1: Write the failing test**

```csharp
// tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorGetEventsTests.cs
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Events;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Actors;

public class AggregateActorGetEventsTests {
    [Fact]
    public async Task GetEventsAsync_NewAggregate_ReturnsEmptyArray() {
        // Arrange — mock IActorStateManager to return no metadata
        // Use the same test setup pattern as AggregateActorTests.cs in this folder
        // Create AggregateActor with ActorHost.CreateForTest and mocked dependencies
        // Set actor ID to "tenant-a:counter:counter-1"

        // The exact mocking setup depends on AggregateActor's constructor.
        // Read AggregateActor.cs constructor to identify all required dependencies.
        // Mock IActorStateManager.TryGetStateAsync<AggregateMetadata> to return no value.

        // Act
        // EventEnvelope[] events = await actor.GetEventsAsync(0);

        // Assert
        // events.ShouldBeEmpty();

        // NOTE: Implementer must read AggregateActor.cs constructor and
        // AggregateActorTests.cs for the exact test setup pattern.
        // This test skeleton shows the expected behavior.
        true.ShouldBeTrue(); // placeholder until wired up
    }

    [Fact]
    public async Task GetEventsAsync_WithEvents_ReturnsEventsAfterSequence() {
        // Arrange — mock state manager with metadata (CurrentSequence=3)
        // and 3 stored EventEnvelope records at sequence keys

        // Act
        // EventEnvelope[] events = await actor.GetEventsAsync(1);

        // Assert
        // events.Length.ShouldBe(2); // sequences 2 and 3
        // events[0].SequenceNumber.ShouldBe(2);
        // events[1].SequenceNumber.ShouldBe(3);

        true.ShouldBeTrue(); // placeholder
    }
}
```

- [ ] **Step 2: Run tests to verify they compile (placeholders pass)**

Run: `dotnet test tests/Hexalith.EventStore.Server.Tests/ --filter "FullyQualifiedName~AggregateActorGetEventsTests" -v n`
Expected: PASS (placeholders)

- [ ] **Step 3: Add GetEventsAsync to IAggregateActor**

In `src/Hexalith.EventStore.Server/Actors/IAggregateActor.cs`, add after `ProcessCommandAsync`:

```csharp
/// <summary>
/// Reads events from the aggregate's event stream starting after the given sequence number.
/// Used by the projection builder to fetch new events for projection updates.
/// </summary>
/// <param name="fromSequence">Return events with sequence number greater than this value. Use 0 for all events.</param>
/// <returns>Events after the specified sequence, ordered by sequence number.</returns>
Task<EventEnvelope[]> GetEventsAsync(long fromSequence);
```

Add `using Hexalith.EventStore.Server.Events;` to the file.

- [ ] **Step 4: Implement GetEventsAsync in AggregateActor**

In `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs`, add the implementation. It should:
1. Parse the actor ID to get `AggregateIdentity` (existing pattern — see `ParseActorId` or how the actor derives identity)
2. Use `IActorStateManager.TryGetStateAsync<AggregateMetadata>(identity.MetadataKey)` to get current sequence
3. If no metadata, return empty array
4. Read events from `fromSequence + 1` to `metadata.CurrentSequence` using the event stream key pattern (`identity.EventStreamKeyPrefix + seq`)
5. Return sorted by sequence number

Follow the existing `EventStreamReader.RehydrateAsync` pattern for reading events from state, but simplified (no snapshot handling needed).

- [ ] **Step 5: Wire up the real tests (replace placeholders)**

Replace the placeholder assertions with actual AggregateActor instantiation. Follow the constructor pattern from `AggregateActorTests.cs` — mock all required dependencies, use `ActorHost.CreateForTest<AggregateActor>()`.

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test tests/Hexalith.EventStore.Server.Tests/ --filter "FullyQualifiedName~AggregateActorGetEventsTests" -v n`
Expected: PASS

- [ ] **Step 7: Run existing AggregateActor tests to verify no regression**

Run: `dotnet test tests/Hexalith.EventStore.Server.Tests/ --filter "FullyQualifiedName~AggregateActorTests" -v n`
Expected: PASS — existing tests still green

- [ ] **Step 8: Commit**

```bash
git add src/Hexalith.EventStore.Server/Actors/IAggregateActor.cs \
        src/Hexalith.EventStore.Server/Actors/AggregateActor.cs \
        tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorGetEventsTests.cs
git commit -m "feat: add GetEventsAsync to IAggregateActor for projection event reading"
```

---

## Chunk 2: EventReplayProjectionActor

### Task 3: IProjectionWriteActor Interface

**Files:**
- Create: `src/Hexalith.EventStore.Server/Actors/IProjectionWriteActor.cs`

- [ ] **Step 1: Create the interface**

```csharp
// src/Hexalith.EventStore.Server/Actors/IProjectionWriteActor.cs
using System.Runtime.Serialization;
using System.Text.Json;

using Dapr.Actors;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// Write interface for projection actors. Used by the projection builder
/// to push updated state after processing events through the domain service.
/// Separate from IProjectionActor (read-only) to maintain clear CQRS boundaries.
/// </summary>
public interface IProjectionWriteActor : IActor {
    /// <summary>
    /// Updates the projection state with new data from the domain service.
    /// </summary>
    /// <param name="state">The updated projection state containing the domain service response.</param>
    Task UpdateProjectionAsync(ProjectionStateUpdate state);
}

/// <summary>
/// State update payload sent to the projection actor by the projection builder.
/// </summary>
[DataContract]
public record ProjectionStateUpdate(
    [property: DataMember] JsonElement State,
    [property: DataMember] string? ProjectionType);
```

- [ ] **Step 2: Commit**

```bash
git add src/Hexalith.EventStore.Server/Actors/IProjectionWriteActor.cs
git commit -m "feat: add IProjectionWriteActor interface for projection state updates"
```

---

### Task 4: EventReplayProjectionActor

**Files:**
- Create: `src/Hexalith.EventStore.Server/Actors/EventReplayProjectionActor.cs`
- Test: `tests/Hexalith.EventStore.Server.Tests/Actors/EventReplayProjectionActorTests.cs`

**Context:** Extends `CachingProjectionActor` (which provides in-memory ETag caching via `QueryAsync`). Also implements `IProjectionWriteActor`. The actor persists state in DAPR actor state (via `IActorStateManager`). `ExecuteQueryAsync` reads from persisted state (cache miss path). `UpdateProjectionAsync` writes state, regenerates ETag (via `IETagActor` proxy), and broadcasts SignalR change notification. Without ETag regeneration, the `CachingProjectionActor` in-memory cache never invalidates and queries return stale data.

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/Hexalith.EventStore.Server.Tests/Actors/EventReplayProjectionActorTests.cs
using System.Text.Json;

using Dapr.Actors.Runtime;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Queries; // IETagService

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Actors;

public class EventReplayProjectionActorTests {
    private const string ProjectionStateKey = "projection-state";
    private const string ProjectionTypeKey = "projection-type";

    [Fact]
    public async Task UpdateProjectionAsync_PersistsState() {
        // Arrange
        IETagService eTagService = Substitute.For<IETagService>();
        var host = ActorHost.CreateForTest<EventReplayProjectionActor>();
        var actor = new EventReplayProjectionActor(host, eTagService, NullLogger<EventReplayProjectionActor>.Instance);

        JsonElement state = JsonDocument.Parse("{\"count\":7}").RootElement;
        var update = new ProjectionStateUpdate(state, "counter");

        // Act
        await actor.UpdateProjectionAsync(update);

        // Assert — state should be persisted in actor state manager
        // Verify via IActorStateManager mock that SetStateAsync was called
        // (Actor base class provides StateManager)
    }

    [Fact]
    public async Task ExecuteQueryAsync_AfterUpdate_ReturnsPersistedState() {
        // Arrange
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((string?)null); // No ETag = skip caching, always call ExecuteQueryAsync

        var host = ActorHost.CreateForTest<EventReplayProjectionActor>();
        var actor = new EventReplayProjectionActor(host, eTagService, NullLogger<EventReplayProjectionActor>.Instance);

        // First update the projection
        JsonElement state = JsonDocument.Parse("{\"count\":7}").RootElement;
        await actor.UpdateProjectionAsync(new ProjectionStateUpdate(state, "counter"));

        // Act — query should return the persisted state
        var envelope = new QueryEnvelope("tenant-a", "counter", "counter-1", "get-counter-status", [], "corr-1", "user-1");
        QueryResult result = await actor.QueryAsync(envelope);

        // Assert
        result.Success.ShouldBeTrue();
        result.Payload.GetProperty("count").GetInt32().ShouldBe(7);
        result.ProjectionType.ShouldBe("counter");
    }

    [Fact]
    public async Task ExecuteQueryAsync_NoState_ReturnsNotFound() {
        // Arrange
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((string?)null);

        var host = ActorHost.CreateForTest<EventReplayProjectionActor>();
        var actor = new EventReplayProjectionActor(host, eTagService, NullLogger<EventReplayProjectionActor>.Instance);

        // Act
        var envelope = new QueryEnvelope("tenant-a", "counter", "counter-1", "get-counter-status", [], "corr-1", "user-1");
        QueryResult result = await actor.QueryAsync(envelope);

        // Assert
        result.Success.ShouldBeFalse();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Hexalith.EventStore.Server.Tests/ --filter "FullyQualifiedName~EventReplayProjectionActorTests" -v n`
Expected: FAIL — `EventReplayProjectionActor` does not exist

- [ ] **Step 3: Implement EventReplayProjectionActor**

```csharp
// src/Hexalith.EventStore.Server/Actors/EventReplayProjectionActor.cs
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Server.Queries;

using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// Generic projection actor that persists state received from the projection builder.
/// Extends CachingProjectionActor for in-memory ETag caching.
/// Implements IProjectionWriteActor to receive state updates.
/// Registered as DAPR actor type "ProjectionActor" (matching QueryRouter.ProjectionActorTypeName).
/// </summary>
public partial class EventReplayProjectionActor(
    ActorHost host,
    IETagService eTagService,
    IActorProxyFactory actorProxyFactory,
    IProjectionChangedBroadcaster projectionChangedBroadcaster,
    ILogger<EventReplayProjectionActor> logger)
    : CachingProjectionActor(host, eTagService, logger), IProjectionWriteActor {
    private const string ProjectionStateKey = "projection-state";
    private const string ProjectionTypeKey = "projection-type";

    /// <inheritdoc/>
    public async Task UpdateProjectionAsync(ProjectionStateUpdate state) {
        ArgumentNullException.ThrowIfNull(state);

        // Persist state to DAPR actor state (survives actor deactivation)
        await StateManager.SetStateAsync(ProjectionStateKey, state.State).ConfigureAwait(false);
        if (state.ProjectionType is not null) {
            await StateManager.SetStateAsync(ProjectionTypeKey, state.ProjectionType).ConfigureAwait(false);
        }

        await StateManager.SaveStateAsync().ConfigureAwait(false);

        // Regenerate ETag to invalidate CachingProjectionActor in-memory cache.
        // Without this, QueryAsync returns stale cached data.
        if (state.ProjectionType is not null && state.TenantId is not null) {
            try {
                IETagActor eTagProxy = actorProxyFactory.CreateActorProxy<IETagActor>(
                    new ActorId($"{state.ProjectionType}:{state.TenantId}"),
                    ETagActor.ActorTypeName);
                await eTagProxy.RegenerateAsync().ConfigureAwait(false);
            }
            catch (Exception ex) {
                Log.ETagRegenerationFailed(logger, ex, Id.GetId(), state.ProjectionType);
            }

            // Broadcast SignalR notification (fail-open)
            try {
                await projectionChangedBroadcaster
                    .BroadcastChangedAsync(state.ProjectionType, state.TenantId)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) {
                Log.BroadcastFailed(logger, ex, Id.GetId(), state.ProjectionType);
            }
        }

        Log.ProjectionStateUpdated(logger, Id.GetId(), state.ProjectionType);
    }

    /// <inheritdoc/>
    protected override async Task<QueryResult> ExecuteQueryAsync(QueryEnvelope envelope) {
        ConditionalValue<JsonElement> stateResult = await StateManager
            .TryGetStateAsync<JsonElement>(ProjectionStateKey)
            .ConfigureAwait(false);

        if (!stateResult.HasValue) {
            return new QueryResult(false, default, ErrorMessage: "No projection state available");
        }

        ConditionalValue<string> typeResult = await StateManager
            .TryGetStateAsync<string>(ProjectionTypeKey)
            .ConfigureAwait(false);

        return new QueryResult(
            true,
            stateResult.Value,
            ProjectionType: typeResult.HasValue ? typeResult.Value : null);
    }

    private static partial class Log {
        [LoggerMessage(
            EventId = 1300,
            Level = LogLevel.Debug,
            Message = "Projection state updated: ActorId={ActorId}, ProjectionType={ProjectionType}, Stage=ProjectionStateUpdated")]
        public static partial void ProjectionStateUpdated(
            ILogger logger,
            string actorId,
            string? projectionType);

        [LoggerMessage(
            EventId = 1301,
            Level = LogLevel.Warning,
            Message = "ETag regeneration failed: ActorId={ActorId}, ProjectionType={ProjectionType}, Stage=ETagRegenerationFailed")]
        public static partial void ETagRegenerationFailed(
            ILogger logger,
            Exception ex,
            string actorId,
            string? projectionType);

        [LoggerMessage(
            EventId = 1302,
            Level = LogLevel.Warning,
            Message = "SignalR broadcast failed: ActorId={ActorId}, ProjectionType={ProjectionType}, Stage=BroadcastFailed")]
        public static partial void BroadcastFailed(
            ILogger logger,
            Exception ex,
            string actorId,
            string? projectionType);
    }
}
```

**Note:** `ProjectionStateUpdate` must also include `TenantId` for ETag regeneration. Update the record in `IProjectionWriteActor.cs`:

```csharp
[DataContract]
public record ProjectionStateUpdate(
    [property: DataMember] JsonElement State,
    [property: DataMember] string? ProjectionType,
    [property: DataMember] string? TenantId);
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Hexalith.EventStore.Server.Tests/ --filter "FullyQualifiedName~EventReplayProjectionActorTests" -v n`
Expected: PASS

- [ ] **Step 5: Register EventReplayProjectionActor in DI**

In `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs`, add inside the `AddActors` callback (after line 63):

```csharp
options.Actors.RegisterActor<EventReplayProjectionActor>(typeOptions => {
    typeOptions.ActorTypeName = QueryRouter.ProjectionActorTypeName;
});
```

Add `using Hexalith.EventStore.Server.Queries;` if not already present (it is — `QueryRouter` is in that namespace, and `IQueryRouter` is already registered at line 32).

- [ ] **Step 6: Build to verify registration compiles**

Run: `dotnet build src/Hexalith.EventStore.Server/ -v n`
Expected: PASS

- [ ] **Step 7: Run all existing Server tests for regression**

Run: `dotnet test tests/Hexalith.EventStore.Server.Tests/ -v n`
Expected: PASS

- [ ] **Step 8: Commit**

```bash
git add src/Hexalith.EventStore.Server/Actors/EventReplayProjectionActor.cs \
        tests/Hexalith.EventStore.Server.Tests/Actors/EventReplayProjectionActorTests.cs \
        src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs
git commit -m "feat: add EventReplayProjectionActor with DAPR state persistence and DI registration"
```

---

## Chunk 3: Projection Update Orchestrator

### Task 5: ProjectionOptions Configuration

**Files:**
- Create: `src/Hexalith.EventStore.Server/Configuration/ProjectionOptions.cs`

- [ ] **Step 1: Create the configuration model**

```csharp
// src/Hexalith.EventStore.Server/Configuration/ProjectionOptions.cs
namespace Hexalith.EventStore.Server.Configuration;

/// <summary>
/// Configuration for server-managed projection updates.
/// </summary>
public record ProjectionOptions {
    /// <summary>
    /// Default refresh interval in milliseconds. 0 = immediate (fire-and-forget after persistence).
    /// Values > 0 enable background polling at that interval.
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

/// <summary>
/// Per-domain projection configuration.
/// </summary>
public record DomainProjectionOptions {
    /// <summary>
    /// Refresh interval in milliseconds for this domain. 0 = immediate.
    /// </summary>
    public int RefreshIntervalMs { get; init; }
}
```

- [ ] **Step 2: Register in DI**

In `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs`, add after the `SnapshotOptions` registration (around line 55):

```csharp
_ = services.AddOptions<ProjectionOptions>()
    .Bind(configuration.GetSection("EventStore:Projections"));
```

- [ ] **Step 3: Build to verify**

Run: `dotnet build src/Hexalith.EventStore.Server/ -v n`
Expected: PASS

- [ ] **Step 4: Commit**

```bash
git add src/Hexalith.EventStore.Server/Configuration/ProjectionOptions.cs \
        src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs
git commit -m "feat: add ProjectionOptions configuration model"
```

---

### Task 6: ProjectionUpdateOrchestrator

**Files:**
- Create: `src/Hexalith.EventStore.Server/Projections/IProjectionUpdateOrchestrator.cs`
- Create: `src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs`
- Test: `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorTests.cs`

**Context:** This is the core component. After events are persisted, it:
1. Calls `AggregateActor.GetEventsAsync(fromSequence)` to get new events
2. Maps `EventEnvelope[]` to `ProjectionEventDto[]`
3. Calls domain service `/project` endpoint via DAPR service invocation
4. Calls `ProjectionActor.UpdateProjectionAsync(state)` via actor proxy
5. Regenerates ETag and broadcasts SignalR notification
6. Updates checkpoint

The orchestrator uses `DaprClient.InvokeMethodAsync` (same pattern as `DaprDomainServiceInvoker`), `IActorProxyFactory` for actor calls, and `IProjectionChangedBroadcaster` for SignalR.

- [ ] **Step 1: Create the interface**

```csharp
// src/Hexalith.EventStore.Server/Projections/IProjectionUpdateOrchestrator.cs
using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>
/// Orchestrates projection updates: reads new events, sends to domain service, updates ProjectionActor.
/// </summary>
public interface IProjectionUpdateOrchestrator {
    /// <summary>
    /// Triggers a projection update for the specified aggregate. Fire-and-forget safe.
    /// </summary>
    /// <param name="identity">The aggregate identity (tenant, domain, aggregateId).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateProjectionAsync(AggregateIdentity identity, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2: Write the failing tests**

```csharp
// tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorTests.cs
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public class ProjectionUpdateOrchestratorTests {
    [Fact]
    public async Task UpdateProjectionAsync_WithNewEvents_CallsDomainServiceAndUpdatesActor() {
        // Arrange
        var identity = new AggregateIdentity("tenant-a", "counter", "counter-1");

        // Mock AggregateActor proxy to return events
        var events = new[] {
            new EventEnvelope("counter-1", "tenant-a", "counter", 1,
                DateTimeOffset.UtcNow, "corr-1", "cause-1", "user-1",
                "v1", "CounterIncremented", "json", [], null),
        };
        IAggregateActor aggregateProxy = Substitute.For<IAggregateActor>();
        _ = aggregateProxy.GetEventsAsync(0).Returns(events);

        // Mock domain service response
        JsonElement stateJson = JsonDocument.Parse("{\"count\":1}").RootElement;
        var projectionResponse = new ProjectionResponse("counter", stateJson);

        DaprClient daprClient = Substitute.For<DaprClient>();
        // DaprClient.InvokeMethodAsync mock setup depends on the exact overload used

        // Mock ProjectionWriteActor proxy
        IProjectionWriteActor projectionProxy = Substitute.For<IProjectionWriteActor>();
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        _ = actorProxyFactory
            .CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(aggregateProxy);
        _ = actorProxyFactory
            .CreateActorProxy<IProjectionWriteActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(projectionProxy);

        // Mock domain service resolver
        IDomainServiceResolver resolver = Substitute.For<IDomainServiceResolver>();
        _ = resolver.ResolveAsync("tenant-a", "counter", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new DomainServiceRegistration("sample", "process", "tenant-a", "counter", "v1"));

        // NOTE: The exact implementation may need additional mocking.
        // The test verifies the orchestration flow: GetEvents -> InvokeMethod -> UpdateProjection.
        // Implementer should wire up the real constructor dependencies.

        true.ShouldBeTrue(); // placeholder — replace when implementing
    }
}
```

- [ ] **Step 3: Implement ProjectionUpdateOrchestrator**

Create `src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs`. The implementation should:

1. Accept `IActorProxyFactory`, `DaprClient`, `IDomainServiceResolver`, `IProjectionChangedBroadcaster`, `IOptions<DomainServiceOptions>`, `ILogger` via constructor injection
2. In `UpdateProjectionAsync`:
   a. Resolve domain service registration via `IDomainServiceResolver.ResolveAsync(identity.TenantId, identity.Domain, "v1")`
   b. Create `IAggregateActor` proxy via `actorProxyFactory.CreateActorProxy<IAggregateActor>(new ActorId(identity.ActorId), "AggregateActor")`
   c. Call `GetEventsAsync(0)` to get all events (first call — no checkpoint tracking in this initial implementation)
   d. Map `EventEnvelope[]` → `ProjectionEventDto[]` (strip internal fields)
   e. Create `ProjectionRequest` and call domain service via `daprClient.InvokeMethodAsync<ProjectionRequest, ProjectionResponse>(registration.AppId, "project", request)`
   f. Derive the projection actor ID using `QueryActorIdHelper` (same pattern as `QueryRouter`)
   g. Create `IProjectionWriteActor` proxy and call `UpdateProjectionAsync(new ProjectionStateUpdate(response.State, response.ProjectionType))`
   h. Call `IProjectionChangedBroadcaster.BroadcastChangedAsync(response.ProjectionType, identity.TenantId)`
3. Wrap everything in try/catch — log warnings on failure, never throw (fire-and-forget safe)

Reference `DaprDomainServiceInvoker.cs` for the DAPR service invocation pattern.
Reference `QueryRouter.cs` for actor proxy creation pattern.
Reference `QueryActorIdHelper.cs` for projection actor ID derivation.

- [ ] **Step 4: Wire up tests and verify**

Replace placeholder assertion with real orchestrator instantiation and verify the mock interactions.

Run: `dotnet test tests/Hexalith.EventStore.Server.Tests/ --filter "FullyQualifiedName~ProjectionUpdateOrchestratorTests" -v n`
Expected: PASS

- [ ] **Step 5: Register in DI**

In `ServiceCollectionExtensions.cs`, add:

```csharp
services.TryAddTransient<IProjectionUpdateOrchestrator, ProjectionUpdateOrchestrator>();
```

- [ ] **Step 6: Commit**

```bash
git add src/Hexalith.EventStore.Server/Projections/IProjectionUpdateOrchestrator.cs \
        src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs \
        tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorTests.cs \
        src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs
git commit -m "feat: add ProjectionUpdateOrchestrator for event delivery and state updates"
```

---

## Chunk 4: Sample Domain Integration and Immediate Trigger

### Task 7: Counter Projection Handler + /project Endpoint

**Files:**
- Create: `samples/Hexalith.EventStore.Sample/Counter/CounterProjectionHandler.cs`
- Modify: `samples/Hexalith.EventStore.Sample/Program.cs`
- Test: `samples/Hexalith.EventStore.Sample.Tests/Counter/CounterProjectionHandlerTests.cs` (if exists) or `tests/Hexalith.EventStore.Sample.Tests/`

**Context:** The domain service needs a thin `/project` endpoint. The `CounterProcessor.RehydrateCount()` already knows how to count events. The projection handler stores state in-memory (keyed by `{tenantId}:{aggregateId}`) and returns it.

- [ ] **Step 1: Create CounterProjectionHandler**

```csharp
// samples/Hexalith.EventStore.Sample/Counter/CounterProjectionHandler.cs
using System.Collections.Concurrent;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.Sample.Counter;

/// <summary>
/// Thin projection handler for the counter domain.
/// Applies events to build projection state and stores it in memory.
/// </summary>
public sealed class CounterProjectionHandler {
    private readonly ConcurrentDictionary<string, CounterProjectionState> _states = new();

    public ProjectionResponse HandleProjection(ProjectionRequest request) {
        ArgumentNullException.ThrowIfNull(request);

        string key = $"{request.TenantId}:{request.AggregateId}";
        CounterProjectionState state = _states.GetOrAdd(key, _ => new CounterProjectionState());

        foreach (ProjectionEventDto evt in request.Events) {
            state.ApplyEvent(evt.EventTypeName);
        }

        JsonElement stateJson = JsonSerializer.SerializeToElement(new { count = state.Count });
        return new ProjectionResponse("counter", stateJson);
    }

    private sealed class CounterProjectionState {
        public int Count { get; private set; }

        public void ApplyEvent(string eventTypeName) {
            if (eventTypeName.EndsWith("CounterIncremented", StringComparison.Ordinal)) {
                Count++;
            }
            else if (eventTypeName.EndsWith("CounterDecremented", StringComparison.Ordinal)) {
                Count = Math.Max(0, Count - 1);
            }
            else if (eventTypeName.EndsWith("CounterReset", StringComparison.Ordinal)) {
                Count = 0;
            }
        }
    }
}
```

- [ ] **Step 2: Add /project endpoint to Sample Program.cs**

In `samples/Hexalith.EventStore.Sample/Program.cs`, add after `builder.Services.AddEventStore();`:

```csharp
builder.Services.AddSingleton<CounterProjectionHandler>();
```

Add after the existing `app.MapPost("/process", ...)`:

```csharp
app.MapPost("/project", (ProjectionRequest request, CounterProjectionHandler handler) =>
    Results.Ok(handler.HandleProjection(request)));
```

Add the required using:
```csharp
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Sample.Counter;
```

- [ ] **Step 3: Build to verify**

Run: `dotnet build samples/Hexalith.EventStore.Sample/ -v n`
Expected: PASS

- [ ] **Step 4: Write a test for CounterProjectionHandler**

```csharp
// tests/Hexalith.EventStore.Sample.Tests/Counter/CounterProjectionHandlerTests.cs
[Fact]
public void HandleProjection_WithIncrementEvents_ReturnsCorrectCount() {
    var handler = new CounterProjectionHandler();
    var request = new ProjectionRequest("tenant-a", "counter", "counter-1", [
        new ProjectionEventDto("CounterIncremented", [], "json", 1, DateTimeOffset.UtcNow, "c1"),
        new ProjectionEventDto("CounterIncremented", [], "json", 2, DateTimeOffset.UtcNow, "c2"),
        new ProjectionEventDto("CounterDecremented", [], "json", 3, DateTimeOffset.UtcNow, "c3"),
    ]);

    ProjectionResponse response = handler.HandleProjection(request);

    response.ProjectionType.ShouldBe("counter");
    response.State.GetProperty("count").GetInt32().ShouldBe(1); // 2 increments - 1 decrement
}
```

- [ ] **Step 5: Run test**

Run: `dotnet test tests/Hexalith.EventStore.Sample.Tests/ --filter "FullyQualifiedName~CounterProjectionHandler" -v n` (or whichever test project contains it)
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add samples/Hexalith.EventStore.Sample/Counter/CounterProjectionHandler.cs \
        samples/Hexalith.EventStore.Sample/Program.cs \
        tests/Hexalith.EventStore.Sample.Tests/Counter/CounterProjectionHandlerTests.cs
git commit -m "feat: add /project endpoint to counter sample domain service"
```

---

### Task 8: Wire Immediate Trigger into Event Publication Path

**Files:**
- Modify: `src/Hexalith.EventStore.Server/Events/EventPublisher.cs`
- Modify: `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs`

**Context:** When `RefreshIntervalMs = 0` (default), the projection update fires as a fire-and-forget `Task.Run` after `EventPublisher.PublishEventsAsync` completes. The `IProjectionUpdateOrchestrator` is injected into `EventPublisher`. This does NOT touch `AggregateActor` — only `EventPublisher`.

**DI note:** .NET DI does NOT use default parameter values — it will fail to resolve if `IProjectionUpdateOrchestrator` is not registered. We register a `NoOpProjectionUpdateOrchestrator` fallback (same pattern as `NoOpProjectionChangedBroadcaster`), then override it when the real orchestrator is wired.

- [ ] **Step 1: Create NoOpProjectionUpdateOrchestrator**

```csharp
// src/Hexalith.EventStore.Server/Projections/NoOpProjectionUpdateOrchestrator.cs
using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>
/// No-op fallback for IProjectionUpdateOrchestrator.
/// Used when projection infrastructure is not fully wired (same pattern as NoOpProjectionChangedBroadcaster).
/// </summary>
public sealed class NoOpProjectionUpdateOrchestrator : IProjectionUpdateOrchestrator {
    public Task UpdateProjectionAsync(AggregateIdentity identity, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
```

- [ ] **Step 2: Register in DI (fallback, then real)**

In `ServiceCollectionExtensions.cs`, add the fallback registration (uses `TryAdd` so the real one can override):

```csharp
services.TryAddTransient<IProjectionUpdateOrchestrator, NoOpProjectionUpdateOrchestrator>();
```

This was already added in Task 6 Step 5 — but now it registers `NoOpProjectionUpdateOrchestrator` first, and the real `ProjectionUpdateOrchestrator` overrides via a non-`TryAdd` registration:

```csharp
// Replace the TryAdd from Task 6 with:
services.AddTransient<IProjectionUpdateOrchestrator, ProjectionUpdateOrchestrator>();
```

Or keep `TryAdd` with NoOp and use `Replace` for the real one. The simplest approach: register the real one directly (not TryAdd), which always wins:

```csharp
_ = services.AddTransient<IProjectionUpdateOrchestrator, ProjectionUpdateOrchestrator>();
```

- [ ] **Step 3: Inject IProjectionUpdateOrchestrator into EventPublisher**

In `src/Hexalith.EventStore.Server/Events/EventPublisher.cs`, add `IProjectionUpdateOrchestrator` as a required constructor parameter (it's always registered — NoOp or real):

Add to constructor parameters:
```csharp
IProjectionUpdateOrchestrator projectionOrchestrator
```

Add using:
```csharp
using Hexalith.EventStore.Server.Projections;
```

After successful publication (after `Log.EventsPublished`), add:

```csharp
// Fire-and-forget projection update (Mode B immediate trigger)
_ = Task.Run(async () => {
    try {
        await projectionOrchestrator.UpdateProjectionAsync(identity, CancellationToken.None).ConfigureAwait(false);
    }
    catch (Exception ex) {
        Log.ProjectionUpdateFailed(logger, ex, identity.TenantId, identity.Domain, identity.AggregateId, correlationId);
    }
}, CancellationToken.None);
```

**Note:** Uses `CancellationToken.None` for both `Task.Run` and `UpdateProjectionAsync` — the original request's token may already be cancelled by the time the background task runs.

Add a new log message:
```csharp
[LoggerMessage(
    EventId = 1310,
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

- [ ] **Step 4: Build and run existing tests**

Run: `dotnet build src/Hexalith.EventStore.Server/ -v n && dotnet test tests/Hexalith.EventStore.Server.Tests/ -v n`
Expected: PASS — existing tests pass (NoOp orchestrator injected in test DI)

- [ ] **Step 5: Commit**

```bash
git add src/Hexalith.EventStore.Server/Projections/NoOpProjectionUpdateOrchestrator.cs \
        src/Hexalith.EventStore.Server/Events/EventPublisher.cs \
        src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs
git commit -m "feat: wire immediate projection trigger into EventPublisher (fire-and-forget)"
```

---

### Deferred: Checkpoint Tracker and Background Poller

The following spec components are deferred to a follow-up plan:

- **`ProjectionCheckpointTracker`**: Currently `GetEventsAsync(0)` replays all events on every trigger. This works correctly (domain service handles idempotent replay) but is wasteful for large event streams. The checkpoint tracker will store the last-sent sequence per aggregate and enable incremental delivery.
- **`ProjectionPollerService`** (background `IHostedService`): Required for `RefreshIntervalMs > 0` mode. The configuration model (`ProjectionOptions`) is in place but the poller is not yet implemented. Default mode (`RefreshIntervalMs = 0`, immediate) works without it.

Both are independent of the core projection pipeline and can be added without modifying existing code.

---

### Task 9: Full Build and Tier 1 Test Verification

- [ ] **Step 1: Full solution build**

Run: `dotnet build Hexalith.EventStore.slnx --configuration Release`
Expected: 0 warnings, 0 errors

- [ ] **Step 2: Run all Tier 1 tests**

Run: `dotnet test tests/Hexalith.EventStore.Contracts.Tests/ && dotnet test tests/Hexalith.EventStore.Client.Tests/ && dotnet test tests/Hexalith.EventStore.Sample.Tests/ && dotnet test tests/Hexalith.EventStore.Testing.Tests/`
Expected: All PASS

- [ ] **Step 3: Run Tier 2 tests (if DAPR slim available)**

Run: `dotnet test tests/Hexalith.EventStore.Server.Tests/`
Expected: All PASS

- [ ] **Step 4: Manual smoke test with Aspire**

Run: `dotnet run --project src/Hexalith.EventStore.AppHost/`

1. Open Blazor UI at the sample-blazor-ui endpoint
2. Click Increment on the counter
3. Verify the counter value updates from 0 to 1
4. Check commandapi logs for projection update activity (no more `QueryNotFoundException` errors)

- [ ] **Step 5: Commit all remaining changes**

```bash
git add -A
git commit -m "feat: complete server-managed projection builder (Mode B)"
```
