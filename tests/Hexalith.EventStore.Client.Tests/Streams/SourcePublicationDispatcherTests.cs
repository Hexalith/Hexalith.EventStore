using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Streams;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Streams;

/// <summary>Ordered runtime/reconnect coverage using actual source reconciliation and serialized index, synthetic exact source acknowledgements; no live qualification.</summary>
public sealed class SourcePublicationDispatcherTests
{
    /// <summary>Unknown or durable quarantine stops at the first gap and cannot deliver a later indexed entry.</summary>
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task FirstUnresolvedSignalStopsWithoutSkipping(bool poison)
    {
        var f = new SourcePublicationDispatcherFixture { StopAt = 2, Poison = poison };
        var result = await f.Dispatcher.DispatchAsync(f.Scope, cancellationToken: TestContext.Current.CancellationToken);
        result.AcknowledgedPrefix.ShouldBe(1); result.IsComplete.ShouldBeFalse(); f.Visited.ShouldBe([1L, 2L]);
        f.Acknowledged.ShouldBe(["publication-1"]); f.Read()!.Entries.Select(e => e.Offset).ShouldBe([1L, 2L, 3L]);
        var restarted = new SourcePublicationDispatcher(f.Feed, TimeProvider.System, f.Delivery); f.Visited.Clear();
        (await restarted.DispatchAsync(f.Scope, cancellationToken: TestContext.Current.CancellationToken)).AcknowledgedPrefix.ShouldBe(1);
        f.Visited.ShouldBe([1L, 2L]);
    }
    /// <summary>Reconnect starts from zero and independently reconstructs the complete prefix with the same durable references.</summary>
    [Fact]
    public async Task ReconnectRebuildsAcknowledgedPrefixAndOriginalReferences()
    {
        var f = new SourcePublicationDispatcherFixture { StopAt = 2 };
        await f.Dispatcher.DispatchAsync(f.Scope, cancellationToken: TestContext.Current.CancellationToken); var original = f.Read()!.Entries.ToArray();
        f.StopAt = null; f.Visited.Clear();
        var result = await new SourcePublicationDispatcher(f.Feed, TimeProvider.System, f.Delivery).DispatchAsync(f.Scope, cancellationToken: TestContext.Current.CancellationToken);
        result.ShouldBe(new SourcePublicationDispatchResult(3, true, null)); f.Visited.ShouldBe([1L, 2L, 3L]); f.Read()!.Entries.ShouldBe(original);
        f.Acknowledged.Count.ShouldBe(3);
    }
    /// <summary>Paging beyond 100 and a smaller operational pass bound both preserve an exact gap-free prefix without inventing completion.</summary>
    [Theory]
    [InlineData(103, 103, true)][InlineData(2, 2, false)]
    public async Task PagingAndPassBoundPreservePrefix(int maximum, long expected, bool complete)
    {
        var f = new SourcePublicationDispatcherFixture(count: 103);
        var result = await f.Dispatcher.DispatchAsync(f.Scope, maximum, TestContext.Current.CancellationToken);
        result.AcknowledgedPrefix.ShouldBe(expected); result.IsComplete.ShouldBe(complete);
        f.Visited.ShouldBe(Enumerable.Range(1, (int)expected).Select(n => (long)n)); f.Read()!.Entries.Count.ShouldBe(103);
    }
    /// <summary>Namespace or adapter absence cannot certify or deliver an apparently complete observed index.</summary>
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task MissingNamespaceOrPrivateAdapterNeverAdvances(bool adapterMissing)
    {
        var f = new SourcePublicationDispatcherFixture();
        if (!adapterMissing) { f.Namespace.ReadAsync(f.Scope, Arg.Any<CancellationToken>()).Returns((SourcePublicationCut?)null); }
        var dispatcher = adapterMissing ? new SourcePublicationDispatcher(f.Feed, TimeProvider.System) : f.Dispatcher;
        var result = await dispatcher.DispatchAsync(f.Scope, cancellationToken: TestContext.Current.CancellationToken);
        result.AcknowledgedPrefix.ShouldBe(0); result.IsComplete.ShouldBeFalse(); f.Visited.ShouldBeEmpty(); f.Persisted.ShouldBeNull();
    }
    /// <summary>A stalled exact-ack provider is entered before controlled timeout/caller cancellation; remaining publications are never visited.</summary>
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task StalledDeliveryPreservesCallerAndDoesNotSkip(bool cancelCaller)
    {
        var clock = new AuthoritativeReadTimeProvider(); var f = new SourcePublicationDispatcherFixture(clock);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var pending = new TaskCompletionSource<SourcePublicationDeliveryStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Delivery.DeliverAsync(Arg.Any<SourcePublicationIndexEntry>(), Arg.Any<CancellationToken>()).Returns(_ => { entered.TrySetResult(); return pending.Task; });
        using var caller = new CancellationTokenSource(); var reading = f.Dispatcher.DispatchAsync(f.Scope, cancellationToken: caller.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        if (cancelCaller)
        {
            caller.Cancel(); var exception = await Should.ThrowAsync<OperationCanceledException>(() => reading.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken)); exception.CancellationToken.ShouldBe(caller.Token);
        }
        else { clock.Advance(TimeSpan.FromSeconds(30)); (await reading.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken)).AcknowledgedPrefix.ShouldBe(0); }
        f.Acknowledged.ShouldBeEmpty(); f.Read()!.Entries.Count.ShouldBe(3); pending.TrySetResult(SourcePublicationDeliveryStatus.Acknowledged);
    }
}
