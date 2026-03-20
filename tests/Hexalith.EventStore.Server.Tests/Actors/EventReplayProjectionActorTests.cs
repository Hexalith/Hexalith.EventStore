
using System.Reflection;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Queries;

using Microsoft.Extensions.Logging;

using NSubstitute;
using NSubstitute.Core;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Actors;

public class EventReplayProjectionActorTests {
    private const string ActorIdString = "GetCounterValue:tenant-a:counter-1";
    private const string TestProjectionType = "counter-projection";
    private const string TestTenantId = "tenant-a";

    private static (EventReplayProjectionActor Actor, IActorStateManager StateManager, IProjectionChangeNotifier Notifier, IETagService ETagService) CreateActor() {
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        ILogger<EventReplayProjectionActor> logger = Substitute.For<ILogger<EventReplayProjectionActor>>();
        _ = logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        IETagService eTagService = Substitute.For<IETagService>();
        IProjectionChangeNotifier notifier = Substitute.For<IProjectionChangeNotifier>();

        ActorHost host = ActorHost.CreateForTest<EventReplayProjectionActor>(
            new ActorTestOptions { ActorId = new ActorId(ActorIdString) });

        var actor = new EventReplayProjectionActor(host, eTagService, notifier, logger);

        PropertyInfo? prop = typeof(Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance);
        prop?.SetValue(actor, stateManager);

        return (actor, stateManager, notifier, eTagService);
    }

    private static ProjectionState CreateTestState(string? projectionType = null, string? tenantId = null) {
        JsonElement stateJson = JsonDocument.Parse("{\"count\":42}").RootElement;
        return new ProjectionState(
            projectionType ?? TestProjectionType,
            tenantId ?? TestTenantId,
            stateJson);
    }

    private static QueryEnvelope CreateTestEnvelope() => new(
        tenantId: TestTenantId,
        domain: "counter",
        aggregateId: "counter-1",
        queryType: "GetCounterValue",
        payload: [1, 2, 3],
        correlationId: "corr-1",
        userId: "user-1");

    [Fact]
    public async Task UpdateProjectionAsync_PersistsStateToActorState() {
        (EventReplayProjectionActor actor, IActorStateManager stateManager, _, _) = CreateActor();
        ProjectionState state = CreateTestState();

        await actor.UpdateProjectionAsync(state);

        await stateManager.Received(1).SetStateAsync(
            EventReplayProjectionActor.ProjectionStateKey,
            state,
            Arg.Any<CancellationToken>());
        await stateManager.Received(1).SaveStateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateProjectionAsync_TriggersNotification() {
        (EventReplayProjectionActor actor, _, IProjectionChangeNotifier notifier, _) = CreateActor();
        ProjectionState state = CreateTestState();

        await actor.UpdateProjectionAsync(state);

        await notifier.Received(1).NotifyProjectionChangedAsync(
            TestProjectionType,
            TestTenantId,
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateProjectionAsync_SavesBeforeNotifying() {
        (EventReplayProjectionActor actor, IActorStateManager stateManager, IProjectionChangeNotifier notifier, _) = CreateActor();
        ProjectionState state = CreateTestState();
        var callOrder = new List<string>();

        stateManager.SaveStateAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo => {
                callOrder.Add("SaveState");
                return Task.CompletedTask;
            });

        notifier.NotifyProjectionChangedAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => {
                callOrder.Add("Notify");
                return Task.CompletedTask;
            });

        await actor.UpdateProjectionAsync(state);

        callOrder.Count.ShouldBe(2);
        callOrder[0].ShouldBe("SaveState");
        callOrder[1].ShouldBe("Notify");
    }

    [Fact]
    public async Task UpdateProjectionAsync_NullState_ThrowsArgumentNull() {
        (EventReplayProjectionActor actor, _, _, _) = CreateActor();

        await Should.ThrowAsync<ArgumentNullException>(
            () => actor.UpdateProjectionAsync(null!));
    }

    [Fact]
    public async Task UpdateProjectionAsync_EmptyProjectionType_ThrowsArgumentException() {
        (EventReplayProjectionActor actor, _, _, _) = CreateActor();
        ProjectionState state = CreateTestState(projectionType: " ");

        await Should.ThrowAsync<ArgumentException>(
            () => actor.UpdateProjectionAsync(state));
    }

    [Fact]
    public async Task UpdateProjectionAsync_EmptyTenantId_ThrowsArgumentException() {
        (EventReplayProjectionActor actor, _, _, _) = CreateActor();
        ProjectionState state = CreateTestState(tenantId: " ");

        await Should.ThrowAsync<ArgumentException>(
            () => actor.UpdateProjectionAsync(state));
    }

    [Fact]
    public async Task UpdateProjectionAsync_NotificationFailure_DoesNotThrow() {
        (EventReplayProjectionActor actor, IActorStateManager stateManager, IProjectionChangeNotifier notifier, _) = CreateActor();
        ProjectionState state = CreateTestState();

        _ = notifier.NotifyProjectionChangedAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => throw new InvalidOperationException("test failure"));

        await actor.UpdateProjectionAsync(state);

        await stateManager.Received(1).SaveStateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteQueryAsync_NoPersistedState_ReturnsFailure() {
        (EventReplayProjectionActor actor, IActorStateManager stateManager, _, IETagService eTagService) = CreateActor();
        QueryEnvelope envelope = CreateTestEnvelope();

        // Return null ETag to avoid caching and force ExecuteQueryAsync call
        _ = eTagService.GetCurrentETagAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((string?)null);

        _ = stateManager.TryGetStateAsync<ProjectionState>(
            EventReplayProjectionActor.ProjectionStateKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<ProjectionState>(false, default!));

        QueryResult result = await actor.QueryAsync(envelope);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ExecuteQueryAsync_WithPersistedState_ReturnsState() {
        (EventReplayProjectionActor actor, IActorStateManager stateManager, _, IETagService eTagService) = CreateActor();
        QueryEnvelope envelope = CreateTestEnvelope();
        ProjectionState state = CreateTestState();

        // Non-null ETag triggers cache miss on first call
        _ = eTagService.GetCurrentETagAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("etag-abc123");

        _ = stateManager.TryGetStateAsync<ProjectionState>(
            EventReplayProjectionActor.ProjectionStateKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<ProjectionState>(true, state));

        QueryResult result = await actor.QueryAsync(envelope);

        result.Success.ShouldBeTrue();
        result.Payload.GetProperty("count").GetInt32().ShouldBe(42);
        result.ProjectionType.ShouldBe(TestProjectionType);
    }

    [Fact]
    public async Task UpdateThenQuery_ReturnsUpdatedState() {
        (EventReplayProjectionActor actor, IActorStateManager stateManager, _, IETagService eTagService) = CreateActor();
        ProjectionState state = CreateTestState();

        // Update projection
        await actor.UpdateProjectionAsync(state);

        // Configure state manager to return the state on subsequent read
        _ = stateManager.TryGetStateAsync<ProjectionState>(
            EventReplayProjectionActor.ProjectionStateKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<ProjectionState>(true, state));

        // Non-null ETag triggers cache miss on first query
        _ = eTagService.GetCurrentETagAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("etag-after-update");

        QueryEnvelope envelope = CreateTestEnvelope();
        QueryResult result = await actor.QueryAsync(envelope);

        result.Success.ShouldBeTrue();
        result.Payload.GetProperty("count").GetInt32().ShouldBe(42);
    }

    [Fact]
    public async Task QueryAsync_CacheHit_DoesNotCallStateManagerAgain() {
        (EventReplayProjectionActor actor, IActorStateManager stateManager, _, IETagService eTagService) = CreateActor();

        // Use projection type matching the envelope domain to avoid projection type
        // discovery skip (CachingProjectionActor skips caching on first call when
        // ProjectionType differs from envelope.Domain).
        ProjectionState state = CreateTestState(projectionType: "counter");
        QueryEnvelope envelope = CreateTestEnvelope();

        // Same ETag on both calls -> second call should be a cache hit
        _ = eTagService.GetCurrentETagAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("etag-stable");

        _ = stateManager.TryGetStateAsync<ProjectionState>(
            EventReplayProjectionActor.ProjectionStateKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<ProjectionState>(true, state));

        // First call: cache miss -> reads from state manager
        QueryResult result1 = await actor.QueryAsync(envelope);
        result1.Success.ShouldBeTrue();

        // Second call: cache hit -> should NOT read from state manager again
        QueryResult result2 = await actor.QueryAsync(envelope);
        result2.Success.ShouldBeTrue();
        result2.Payload.GetProperty("count").GetInt32().ShouldBe(42);

        // TryGetStateAsync should only be called once (first cache miss)
        await stateManager.Received(1).TryGetStateAsync<ProjectionState>(
            EventReplayProjectionActor.ProjectionStateKey, Arg.Any<CancellationToken>());
    }
}
