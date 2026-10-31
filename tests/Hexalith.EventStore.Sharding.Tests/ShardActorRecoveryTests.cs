using System.Globalization;
using System.Reflection;

using Dapr.Actors;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Server.Tests.TestUtilities;
using Hexalith.EventStore.Sharding.TestHost;

using Shouldly;

namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>Verifies conservative actor behavior for both possible durable save outcomes.</summary>
public sealed class ShardActorRecoveryTests
{
    /// <summary>An ambiguous save propagates and cannot leak staged authority to another turn.</summary>
    /// <param name="committed">Whether the provider committed before losing the response.</param>
    /// <param name="stage">Whether to fail immediately after staging instead of during save.</param>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task FailedSaveEvictsPendingStateAndNextTurnReadsDurableOutcomeAsync(bool committed, bool stage)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var binding = new RunBinding("save-unit", now.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture), now.AddMinutes(1).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            "", "", "", new(), new(), new(), new());
        using var fence = new RunFence(binding);
        IActorStateManager manager = DispatchProxy.Create<IActorStateManager, SaveFailureStateManager>();
        var fake = (SaveFailureStateManager)manager;
        fake.CommitBeforeFailure = committed;
        fake.FailDuringStage = stage;
        var previous = new ShardState("tenant", "domain", 7, 9);
        await fake.Inner.SetStateAsync(ShardActor.StateKey, previous);
        await fake.Inner.SaveStateAsync();
        var actor = new ShardActor(ActorHost.CreateForTest<ShardActor>(new ActorTestOptions
        {
            ActorId = new ActorId(ShardIdentity.Encode("tenant", "domain")),
        }), fence);
        ActorStateManagerTestHelper.SetStateManager(actor, manager);
        await Should.ThrowAsync<IOException>(() => actor.ApplyAsync(new("Reserve", "tenant", "domain", 7, 3)));
        // Fake instrumentation knows both outcomes; an external failure alone never proves either.
        ShardState durable = previous with { Ceiling = committed ? 12 : 9 };
        fake.Inner.CommittedState[ShardActor.StateKey].ShouldBe(durable);
        await manager.SaveStateAsync(); // Runtime completion must not flush a failed pending write.
        fake.Inner.CommittedState[ShardActor.StateKey].ShouldBe(durable);
        ShardReply next = await actor.ApplyAsync(new("Reserve", "tenant", "domain", 7, 2));
        next.Accepted.ShouldBeTrue();
        next.First.ShouldBe(durable.Ceiling + 1);
        next.Count.ShouldBe(2);
        next.State.ShouldBe(durable with { Ceiling = durable.Ceiling + 2 });
    }

    /// <summary>A discarded success is delivered once and cannot become an automatic retry.</summary>
    [Fact]
    public async Task LostResponseConsumesSuccessExactlyOnceAsync()
    {
        using var handler = new SingleAttemptHandler(true, new CallbackHttpHandler((_, _) => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("durably saved"),
        })));
        using var client = new HttpClient(handler);
        await Should.ThrowAsync<HttpRequestException>(() => client.GetAsync("http://unit.invalid/"));
        handler.DiscardedSuccess.ShouldBeTrue();
        handler.SendCount.ShouldBe(1);
    }
}
