using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Fakes;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

public sealed class InMemoryDaprStateClientTests
{
    [Fact]
    public async Task CompetingWritersCannotBothReplaceTheSameEtag()
    {
        using var client = new InMemoryDaprStateClient();
        var store = new DaprReadModelStore(client);
        await store.SaveAsync("models", "selected", new[] { "original" }, TestContext.Current.CancellationToken);
        var original = await store.GetAsync<string[]>("models", "selected", TestContext.Current.CancellationToken);
        using var barrier = new Barrier(2);
        client.BeforeWrite = _ => barrier.SignalAndWait(TimeSpan.FromSeconds(5)).ShouldBeTrue();
        var writes = await Task.WhenAll(Enumerable.Range(1, 2).Select(index => Task.Run(() =>
            store.TrySaveAsync("models", "selected", new[] { "writer-" + index }, original.ETag!, TestContext.Current.CancellationToken))));
        writes.Count(static won => won).ShouldBe(1);
        var winner = await store.GetAsync<string[]>("models", "selected", TestContext.Current.CancellationToken);
        winner.ETag.ShouldNotBe(original.ETag);
        winner.Value.ShouldNotBe(original.Value);
    }
}
