using Dapr.Actors;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Tests.TestUtilities;

using Microsoft.Extensions.Logging;

using NSubstitute;

using Shouldly;

using InMemoryStateManager = Hexalith.EventStore.Testing.Fakes.InMemoryStateManager;

namespace Hexalith.EventStore.Server.Tests.Actors;

public class GlobalPositionActorTests {
    private static (GlobalPositionActor Actor, InMemoryStateManager StateManager) CreateActor() {
        var stateManager = new InMemoryStateManager();
        ILogger<GlobalPositionActor> logger = Substitute.For<ILogger<GlobalPositionActor>>();
        var host = ActorHost.CreateForTest<GlobalPositionActor>(
            new ActorTestOptions { ActorId = new ActorId("global") });
        var actor = new GlobalPositionActor(host, logger);

        ActorStateManagerTestHelper.SetStateManager(actor, stateManager);

        return (actor, stateManager);
    }

    [Fact]
    public async Task AllocateAsync_FirstBatch_StartsAtOneAndPersistsCurrentPosition() {
        (GlobalPositionActor actor, InMemoryStateManager stateManager) = CreateActor();

        long first = await actor.AllocateAsync(3);

        first.ShouldBe(1);
        long current = await actor.GetCurrentAsync();
        current.ShouldBe(3);
        stateManager.CommittedState["current-global-position"].ShouldBe(3L);
    }

    [Fact]
    public async Task AllocateAsync_SubsequentBatch_ContinuesAfterStoredPosition() {
        (GlobalPositionActor actor, _) = CreateActor();

        long first = await actor.AllocateAsync(3);
        long second = await actor.AllocateAsync(2);

        first.ShouldBe(1);
        second.ShouldBe(4);
        long current = await actor.GetCurrentAsync();
        current.ShouldBe(5);
    }

    [Fact]
    public async Task AllocateAsync_InvalidCount_ThrowsArgumentOutOfRangeException() {
        (GlobalPositionActor actor, _) = CreateActor();

        _ = await Should.ThrowAsync<ArgumentOutOfRangeException>(() => actor.AllocateAsync(0));
    }
}
