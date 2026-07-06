
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Queries;
using Hexalith.EventStore.Server.Tests.TestUtilities;

using Microsoft.Extensions.Logging;

using NSubstitute;

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

        var host = ActorHost.CreateForTest<EventReplayProjectionActor>(
            new ActorTestOptions { ActorId = new ActorId(ActorIdString) });

        var actor = new EventReplayProjectionActor(host, eTagService, notifier, logger);

        ActorStateManagerTestHelper.SetStateManager(actor, stateManager);

        return (actor, stateManager, notifier, eTagService);
    }

    private static ProjectionState CreateTestState(string? projectionType = null, string? tenantId = null) {
        JsonElement stateJson = JsonDocument.Parse("{\"count\":42}").RootElement;
        return ProjectionState.FromJsonElement(
            projectionType ?? TestProjectionType,
            tenantId ?? TestTenantId,
            stateJson);
    }

    private static QueryEnvelope CreateTestEnvelope(QueryPagingOptions? paging = null) => new(
        tenantId: TestTenantId,
        domain: "counter",
        aggregateId: "counter-1",
        queryType: "GetCounterValue",
        payload: [1, 2, 3],
        correlationId: "corr-1",
        userId: "user-1",
        entityId: null,
        isGlobalAdmin: false,
        paging: paging);

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
    public async Task UpdateProjectionAsync_WithCancellationToken_PassesTokenToActorStateAndNotifier() {
        (EventReplayProjectionActor actor, IActorStateManager stateManager, IProjectionChangeNotifier notifier, _) = CreateActor();
        ProjectionState state = CreateTestState();
        using var cts = new CancellationTokenSource();

        await actor.UpdateProjectionAsync(state, cts.Token);

        await stateManager.Received(1).SetStateAsync(
            EventReplayProjectionActor.ProjectionStateKey,
            state,
            Arg.Is<CancellationToken>(token => token == cts.Token));
        await stateManager.Received(1).SaveStateAsync(Arg.Is<CancellationToken>(token => token == cts.Token));
        await notifier.Received(1).NotifyProjectionChangedAsync(
            TestProjectionType,
            TestTenantId,
            null,
            Arg.Is<CancellationToken>(token => token == cts.Token));
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

        _ = stateManager.SaveStateAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo => {
                callOrder.Add("SaveState");
                return Task.CompletedTask;
            });

        _ = notifier.NotifyProjectionChangedAsync(
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

        _ = await Should.ThrowAsync<ArgumentNullException>(
            () => actor.UpdateProjectionAsync(null!));
    }

    [Fact]
    public async Task UpdateProjectionAsync_EmptyProjectionType_ThrowsArgumentException() {
        (EventReplayProjectionActor actor, _, _, _) = CreateActor();
        ProjectionState state = CreateTestState(projectionType: " ");

        _ = await Should.ThrowAsync<ArgumentException>(
            () => actor.UpdateProjectionAsync(state));
    }

    [Fact]
    public async Task UpdateProjectionAsync_EmptyTenantId_ThrowsArgumentException() {
        (EventReplayProjectionActor actor, _, _, _) = CreateActor();
        ProjectionState state = CreateTestState(tenantId: " ");

        _ = await Should.ThrowAsync<ArgumentException>(
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
    public async Task UpdateProjectionAsync_NotifierThrowsOperationCanceled_RethrowsAndDoesNotLogFailOpen() {
        // AC9: when the notifier rethrows OperationCanceledException, the actor's
        // catch (OperationCanceledException) { throw; } block must propagate the cancellation
        // (not convert it into a ProjectionChangeNotificationFailed fail-open log entry).
        // If the catch block were removed, the generic catch (Exception) would swallow the OCE
        // and log it as a notifier-fail-open warning instead of rethrowing.
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        ILogger<EventReplayProjectionActor> logger = Substitute.For<ILogger<EventReplayProjectionActor>>();
        _ = logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        IETagService eTagService = Substitute.For<IETagService>();
        IProjectionChangeNotifier notifier = Substitute.For<IProjectionChangeNotifier>();

        var host = ActorHost.CreateForTest<EventReplayProjectionActor>(
            new ActorTestOptions { ActorId = new ActorId(ActorIdString) });
        var actor = new EventReplayProjectionActor(host, eTagService, notifier, logger);
        ActorStateManagerTestHelper.SetStateManager(actor, stateManager);

        ProjectionState state = CreateTestState();
        _ = notifier.NotifyProjectionChangedAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => throw new OperationCanceledException("notifier cancelled"));

        OperationCanceledException exception = await Should.ThrowAsync<OperationCanceledException>(
            () => actor.UpdateProjectionAsync(state));

        // The exception is OCE-derived (runtime may surface TaskCanceledException after async
        // re-throw; both are acceptable for AC9 — the requirement is non-conversion to a
        // generic notifier-failed adapter log).
        _ = exception.ShouldBeAssignableTo<OperationCanceledException>();

        // State persistence completed before notifier was invoked (notifier failure happens
        // after state save, per actor implementation).
        await stateManager.Received(1).SaveStateAsync(Arg.Any<CancellationToken>());

        // ProjectionChangeNotificationFailed (EventId 1093) must NOT be emitted: OCE rethrow
        // is the documented behavior, not fail-open.
        logger.DidNotReceive().Log(
            Arg.Any<LogLevel>(),
            Arg.Is<EventId>(id => id.Id == 1093),
            Arg.Any<object?>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object?, Exception?, string>>()!);
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
    public async Task ExecuteQueryAsync_CursorPaging_ReturnsInvalidCursorWithoutReadingState() {
        (EventReplayProjectionActor actor, IActorStateManager stateManager, _, IETagService eTagService) = CreateActor();
        QueryEnvelope envelope = CreateTestEnvelope(new QueryPagingOptions(Cursor: "opaque-cursor"));

        _ = eTagService.GetCurrentETagAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((string?)null);

        QueryResult result = await actor.QueryAsync(envelope);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.InvalidCursor);
        _ = await stateManager.DidNotReceive().TryGetStateAsync<ProjectionState>(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
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
        result.GetPayload().GetProperty("count").GetInt32().ShouldBe(42);
        result.ProjectionType.ShouldBe(TestProjectionType);
    }

    [Fact]
    public async Task ExecuteQueryAsync_WithCancellationToken_PassesTokenToStateRead() {
        (EventReplayProjectionActor actor, IActorStateManager stateManager, _, IETagService eTagService) = CreateActor();
        QueryEnvelope envelope = CreateTestEnvelope();
        ProjectionState state = CreateTestState();
        using var cts = new CancellationTokenSource();

        _ = eTagService.GetCurrentETagAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((string?)null);

        _ = stateManager.TryGetStateAsync<ProjectionState>(
            EventReplayProjectionActor.ProjectionStateKey,
            Arg.Is<CancellationToken>(token => token == cts.Token))
            .Returns(new ConditionalValue<ProjectionState>(true, state));

        QueryResult result = await actor.QueryAsync(envelope, cts.Token);

        result.Success.ShouldBeTrue();
        _ = await stateManager.Received(1).TryGetStateAsync<ProjectionState>(
            EventReplayProjectionActor.ProjectionStateKey,
            Arg.Is<CancellationToken>(token => token == cts.Token));
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
        result.GetPayload().GetProperty("count").GetInt32().ShouldBe(42);
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
        result2.GetPayload().GetProperty("count").GetInt32().ShouldBe(42);

        // TryGetStateAsync should only be called once (first cache miss)
        _ = await stateManager.Received(1).TryGetStateAsync<ProjectionState>(
            EventReplayProjectionActor.ProjectionStateKey, Arg.Any<CancellationToken>());
    }
}
