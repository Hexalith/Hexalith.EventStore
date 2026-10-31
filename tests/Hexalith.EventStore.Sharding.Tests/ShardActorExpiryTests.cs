using System.Globalization;
using System.Reflection;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Server.Tests.TestUtilities;
using Hexalith.EventStore.Sharding.TestHost;
using Hexalith.EventStore.Testing.Fakes;

using Shouldly;

namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>Invokes the actual actor at expiry boundaries and inspects complete state.</summary>
public sealed class ShardActorExpiryTests
{
    /// <summary>Already-expired and mid-turn requests deny without committed or pending mutations.</summary>
    /// <param name="boundary">The entry, read, stage, or save boundary.</param>
    [Theory]
    [InlineData("entry")]
    [InlineData("read")]
    [InlineData("stage")]
    [InlineData("save")]
    public async Task ActorDeniesAtExpiryWithoutPendingOrCommittedMutationAsync(string boundary)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var binding = new RunBinding("expiry-actor-unit", Canonical(now.AddSeconds(-1)), Canonical(now.AddSeconds(2)),
            "411005b4ccb2352249c83330c54967c6a22beba1", "160331d25451928ff3c3dea2300b65cab4f97c3b",
            "995fcecd16b3421ec9ff666d0884bfb5e436932aa49529c152fb7c439172a1fd", new(), new(), new(), new());
        using var fence = new RunFence(binding);
        IActorStateManager manager = DispatchProxy.Create<IActorStateManager, GatedStateManager>();
        var gated = (GatedStateManager)manager;
        var existing = new ShardState("tenant", "domain", 7, 9);
        await gated.Inner.SetStateAsync(ShardActor.StateKey, existing);
        await gated.Inner.SetStateAsync("unrelated-committed-sentinel", "unchanged");
        await gated.Inner.SaveStateAsync();
        string committed = JsonSerializer.Serialize(gated.Inner.CommittedState);
        string pending = PendingSnapshot(gated.Inner);
        async Task WaitForExpiryAsync()
        {
            try { await Task.Delay(Timeout.InfiniteTimeSpan, fence.Token); }
            catch (OperationCanceledException) { }
        }

        gated.WaitForExpiry = WaitForExpiryAsync;
        gated.Boundary = boundary;
        if (boundary == "entry") await WaitForExpiryAsync();
        var actor = new ShardActor(ActorHost.CreateForTest<ShardActor>(new ActorTestOptions
        {
            ActorId = new ActorId(ShardIdentity.Encode("tenant", "domain")),
        }), fence);
        ActorStateManagerTestHelper.SetStateManager(actor, manager);
        if (boundary == "save")
        {
            // This fake cancels before committing. A real save-started cancellation
            // is uncertain, so the actor must propagate it instead of denying mutation.
            await Should.ThrowAsync<OperationCanceledException>(() => actor.ApplyAsync(new("Reserve", "tenant", "domain", 7, 3)));
        }
        else
        {
            ShardReply result = await actor.ApplyAsync(new("Reserve", "tenant", "domain", 7, 3));
            result.Accepted.ShouldBeFalse();
            result.Code.ShouldBe("expired");
        }
        gated.ReachedBoundary.ShouldBe(boundary != "entry");
        JsonSerializer.Serialize(gated.Inner.CommittedState).ShouldBe(committed);
        PendingSnapshot(gated.Inner).ShouldBe(pending);
        // Even an accidental subsequent actor turn save cannot publish a denied reservation.
        await gated.Inner.SaveStateAsync();
        JsonSerializer.Serialize(gated.Inner.CommittedState).ShouldBe(committed);
    }

    private static string PendingSnapshot(InMemoryStateManager state)
        => JsonSerializer.Serialize(new
        {
            state = typeof(InMemoryStateManager).GetField("_pendingState", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(state),
            removals = typeof(InMemoryStateManager).GetField("_pendingRemovals", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(state),
        });

    private static string Canonical(DateTimeOffset value)
        => value.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
}
