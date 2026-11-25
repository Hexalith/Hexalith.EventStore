using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Projections;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public sealed partial class NamedProjectionDispatchCoordinatorTests
{
    [Fact]
    public async Task AlreadyCompletedRetryWritesActorAndUpdatesValidatorBeforeAdvancingCheckpoint()
    {
        var scheduler = new MemoryRetryScheduler();
        var proxies = Substitute.For<IActorProxyFactory>();
        var actor = Substitute.For<IProjectionWriteActor>();
        var eTagActor = Substitute.For<IETagActor>();
        proxies.CreateActorProxy<IProjectionWriteActor>(Arg.Any<ActorId>(), Arg.Any<string>()).Returns(actor);
        proxies.CreateActorProxy<IETagActor>(Arg.Any<ActorId>(), Arg.Any<string>()).Returns(eTagActor);
        eTagActor.RegenerateAsync().Returns("etag-1");
        actor.UpdateProjectionAsync(Arg.Any<ProjectionState>()).Returns(
            _ => Task.FromException(new IOException("actor write unavailable")),
            _ => Task.CompletedTask);

        JsonElement state = JsonSerializer.SerializeToElement(new { value = 42 });
        var snapshot = Snapshot("fingerprint", "widget-detail");
        using var first = new ProjectionDispatchHttpMessageHandler(Reply(ProjectionDispatchStatus.Completed));
        NamedProjectionDispatchCoordinator initial = CreateCoordinator(snapshot, first,
            out IProjectionDeliveryCheckpointStore initialCheckpoints, out _, scheduler, actorProxyFactory: proxies);
        CancellationToken token = TestContext.Current.CancellationToken;

        (await initial.TryDispatchAsync(Identity, Registration(), [Envelope(1)], [ProjectionEvent(1)], token)).ShouldBeTrue();

        ProjectionDeliveryRetryWorkItem pending = scheduler.Work.ShouldNotBeNull();
        pending.PendingRoutes.ShouldBe(["widget-detail"]);
        _ = await initialCheckpoints.DidNotReceiveWithAnyArgs().SaveDeliveredSequenceAsync(default!, default!, default, default);
        _ = await eTagActor.DidNotReceive().RegenerateAsync();
        actor.ClearReceivedCalls();

        using var retry = new ProjectionDispatchHttpMessageHandler(Reply(ProjectionDispatchStatus.AlreadyCompleted));
        NamedProjectionDispatchCoordinator resumed = CreateCoordinator(snapshot, retry,
            out IProjectionDeliveryCheckpointStore checkpoints, out _, scheduler, actorProxyFactory: proxies);
        checkpoints.SaveDeliveredSequenceAsync(Identity, "widget-detail", 1, Arg.Any<CancellationToken>()).Returns(true);

        (await resumed.TryDispatchAsync(Identity, Registration(), [Envelope(1)], [ProjectionEvent(1)], token)).ShouldBeTrue();

        Received.InOrder(() =>
        {
            _ = actor.UpdateProjectionAsync(Arg.Is<ProjectionState>(value => value.GetState().GetProperty("value").GetInt32() == 42));
            _ = eTagActor.RegenerateAsync();
            _ = checkpoints.SaveDeliveredSequenceAsync(Identity, "widget-detail", 1, Arg.Any<CancellationToken>());
        });
        scheduler.Work.ShouldBeNull();

        string Reply(ProjectionDispatchStatus status) => JsonSerializer.Serialize(new ProjectionDispatchResponse(
            ProjectionDispatchProtocol.Version, [new ProjectionDispatchOutcome("widget-detail", status, state, null)]));
    }
}
