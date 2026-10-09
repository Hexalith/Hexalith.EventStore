using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Streams;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Streams;

/// <summary>Finite namespace completeness, detachment and release regressions; authority is synthetic.</summary>
public sealed class SourceNamespaceSnapshotReaderTests
{
    private static readonly SourcePublicationScope Scope = new("tenant-a", "conversation", "catalogue", "installed-1");
    private static readonly AggregateIdentity Identity = new("tenant-a", "conversation", "source-a");

    /// <summary>Streaming small summaries handles complete namespaces without retaining the sum of their payloads; raw snapshots retain the existing whole-read byte bound.</summary>
    [Fact]
    public async Task FoldReleasesEachPrefixBeforeNextAndRawSnapshotBoundsAggregateBytes()
    {
        var now = DateTimeOffset.UtcNow; var second = new AggregateIdentity("tenant-a", "conversation", "source-b");
        var cut = new SourcePublicationCut(Scope, "authority", now, now.AddMinutes(1), [new(Identity, 1), new(second, 1)], true);
        var namespaces = Substitute.For<ISourcePublicationNamespaceSource>(); namespaces.ReadAsync(Scope, Arg.Any<CancellationToken>()).Returns(cut);
        var streams = Substitute.For<IAuthoritativeEventStreamReader>(); var foldedIds = new List<string>();
        streams.ReadAsync(Arg.Any<AggregateIdentity>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var identity = call.Arg<AggregateIdentity>();
            if (identity == second && foldedIds.Count > 0) { foldedIds.ShouldBe([Identity.AggregateId]); }
            var source = new AuthoritativeEventStream(identity, 1, now,
                [new(1, "Created", new byte[9 * 1024 * 1024], "json", 1, "event-" + identity.AggregateId, null, null, now, null)], "exact-read");
            return new AuthoritativeStreamReadResult(source, null);
        });
        var reader = new SourceNamespaceSnapshotReader(namespaces, streams, TimeProvider.System);
        var folded = await reader.ReadFoldedAsync<string>(Scope, (source, token) =>
        { token.ThrowIfCancellationRequested(); foldedIds.Add(source.Identity.AggregateId); return source.Identity.AggregateId; }, TestContext.Current.CancellationToken);
        folded!.Values.ShouldBe([Identity.AggregateId, second.AggregateId]); folded.Cut.Sources.ShouldBe(cut.Sources);
        foldedIds.Clear();
        (await reader.ReadAsync(Scope, TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    /// <summary>Registered empty streams and zero Agent Call streams retain complete exact namespace membership.</summary>
    [Fact]
    public async Task CompleteSnapshotDetachesBytesAndPreservesRegisteredEmptySources()
    {
        var now = DateTimeOffset.UtcNow;
        var empty = new AggregateIdentity("tenant-a", "conversation", "source-empty");
        var cut = new SourcePublicationCut(Scope, "authority-1", now, now.AddMinutes(1), [new(Identity, 1), new(empty, 0)], true);
        var namespaces = Substitute.For<ISourcePublicationNamespaceSource>();
        namespaces.ReadAsync(Scope, Arg.Any<CancellationToken>()).Returns(cut);
        var payload = new byte[] { 1, 2, 3 };
        var source = new AuthoritativeEventStream(Identity, 1, now,
            [new(1, "Created", payload, "json", 1, "event-1", null, null, now, null)], "read-1");
        var streams = Substitute.For<IAuthoritativeEventStreamReader>();
        streams.ReadAsync(Identity, Arg.Any<CancellationToken>()).Returns(new AuthoritativeStreamReadResult(source, null));
        streams.ReadAsync(empty, Arg.Any<CancellationToken>()).Returns(new AuthoritativeStreamReadResult(null, "source-unavailable"));
        var result = await new SourceNamespaceSnapshotReader(namespaces, streams, TimeProvider.System).ReadAsync(Scope, TestContext.Current.CancellationToken);
        result.ShouldNotBeNull(); result.Cut.Sources.Count.ShouldBe(2); result.Sources.Count.ShouldBe(1);
        await streams.DidNotReceive().ReadAsync(empty, Arg.Any<CancellationToken>());
        payload[0] = 99;
        result.Sources.Single(s => s.Identity == Identity).Events.Single().Payload.ShouldBe(new byte[] { 1, 2, 3 });
    }

    /// <summary>Partial, expired and foreign cuts cannot read source bytes or certify zero.</summary>
    [Theory]
    [InlineData("partial")]
    [InlineData("foreign")]
    [InlineData("expired")]
    [InlineData("duplicate")]
    public async Task InvalidCutDeniesBeforeSourceReads(string vector)
    {
        var now = DateTimeOffset.UtcNow;
        var cut = new SourcePublicationCut(Scope, "authority", now, now.AddMinutes(1), [new(Identity, 1)], true);
        cut = vector switch
        {
            "partial" => cut with { IsComplete = false },
            "foreign" => cut with { Sources = [new(new("tenant-b", "conversation", "source-a"), 1)] },
            "duplicate" => cut with { Sources = [new(Identity, 1), new(Identity, 1)] },
            _ => cut with { ValidUntil = now },
        };
        var namespaces = Substitute.For<ISourcePublicationNamespaceSource>();
        namespaces.ReadAsync(Scope, Arg.Any<CancellationToken>()).Returns(cut);
        var streams = Substitute.For<IAuthoritativeEventStreamReader>();
        (await new SourceNamespaceSnapshotReader(namespaces, streams, TimeProvider.System).ReadAsync(Scope,
            TestContext.Current.CancellationToken)).ShouldBeNull();
        streams.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>A changed head or coverage revision during capture releases no current catalogue.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task NamespaceChangesDuringSourceReadDenyRelease(bool headChanges)
    {
        var now = DateTimeOffset.UtcNow;
        var cut = new SourcePublicationCut(Scope, "authority", now, now.AddMinutes(1), [new(Identity, 0)], true);
        var namespaces = Substitute.For<ISourcePublicationNamespaceSource>();
        namespaces.ReadAsync(Scope, Arg.Any<CancellationToken>()).Returns(cut, headChanges
            ? cut with { Sources = [new(Identity, 1)] } : cut with { AuthorityRevision = "authority-2" });
        var streams = Substitute.For<IAuthoritativeEventStreamReader>();
        streams.ReadAsync(Identity, Arg.Any<CancellationToken>()).Returns(new AuthoritativeStreamReadResult(new(Identity, 0, now, [], "read"), null));
        (await new SourceNamespaceSnapshotReader(namespaces, streams, TimeProvider.System).ReadAsync(Scope,
            TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    /// <summary>Original caller cancellation bounds a stalled namespace invocation and retains no evidence.</summary>
    [Fact]
    public async Task CallerCancellationStopsNoncooperativeNamespaceWait()
    {
        var namespaces = Substitute.For<ISourcePublicationNamespaceSource>();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<SourcePublicationCut?>(TaskCreationOptions.RunContinuationsAsynchronously);
        namespaces.ReadAsync(Scope, Arg.Any<CancellationToken>()).Returns(_ => { entered.TrySetResult(); return pending.Task; });
        using var caller = new CancellationTokenSource();
        var read = new SourceNamespaceSnapshotReader(namespaces, Substitute.For<IAuthoritativeEventStreamReader>(), TimeProvider.System)
            .ReadAsync(Scope, caller.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        caller.Cancel();
        var exception = await Should.ThrowAsync<OperationCanceledException>(() => read.WaitAsync(TimeSpan.FromSeconds(5)));
        exception.CancellationToken.ShouldBe(caller.Token);
        pending.SetResult(null);
    }
    /// <summary>A still-certified cut reads only its exact consecutive prefix after source advancement, without retaining later bytes.</summary>
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task AdvancedSourceReleasesOnlyCertifiedConsecutivePrefix(bool gapped)
    {
        var now = DateTimeOffset.UtcNow; var cut = new SourcePublicationCut(Scope, "authority", now, now.AddMinutes(1), [new(Identity, 1)], true);
        var namespaces = Substitute.For<ISourcePublicationNamespaceSource>(); namespaces.ReadAsync(Scope, Arg.Any<CancellationToken>()).Returns(cut);
        var streams = Substitute.For<IAuthoritativeEventStreamReader>();
        var bytes = new byte[] { 1, 2, 3 };
        var source = new AuthoritativeEventStream(Identity, 2, now, [new(gapped ? 2 : 1, "Created", bytes, "json", 1, "original", null, null, now, null),
            new(2, "Later", new byte[17 * 1024 * 1024], "json", 1, "later", null, null, now, null)], "independent-source");
        streams.ReadAsync(Identity, Arg.Any<CancellationToken>()).Returns(new AuthoritativeStreamReadResult(source, null));
        var result = await new SourceNamespaceSnapshotReader(namespaces, streams, TimeProvider.System).ReadAsync(Scope, TestContext.Current.CancellationToken);
        if (gapped) { result.ShouldBeNull(); return; }
        result!.Sources.Single().Head.ShouldBe(1); result.Sources.Single().Events.Single().MessageId.ShouldBe("original");
        bytes[0] = 99; result.Sources.Single().Events.Single().Payload.ShouldBe(new byte[] { 1, 2, 3 });
    }

    /// <summary>Provider-returned cuts, prefix events and flags cannot hold completion beyond the same whole-operation cancellation/deadline, or release a late fold.</summary>
    [Theory]
    [InlineData("initial-cut-count", false)][InlineData("initial-cut-count", true)]
    [InlineData("initial-cut-items", false)][InlineData("initial-cut-items", true)]
    [InlineData("final-cut-count", false)][InlineData("final-cut-count", true)]
    [InlineData("final-cut-items", false)][InlineData("final-cut-items", true)]
    [InlineData("first-source-count", false)][InlineData("first-source-count", true)]
    [InlineData("first-source-items", false)][InlineData("first-source-items", true)]
    [InlineData("last-source-count", false)][InlineData("last-source-count", true)]
    [InlineData("last-source-items", false)][InlineData("last-source-items", true)]
    [InlineData("first-flags-count", false)][InlineData("first-flags-count", true)]
    [InlineData("first-flags-items", false)][InlineData("first-flags-items", true)]
    [InlineData("last-flags-count", false)][InlineData("last-flags-count", true)]
    [InlineData("last-flags-items", false)][InlineData("last-flags-items", true)]
    public async Task SuspendedCollectionsReleaseCallerWithoutLateNamespaceOrFold(string vector, bool cancelCaller)
    {
        ArgumentNullException.ThrowIfNull(vector);
        var clock = new AuthoritativeReadTimeProvider(); var now = clock.GetUtcNow();
        var second = new AggregateIdentity("tenant-a", "conversation", "source-b");
        SourcePublicationHead[] heads = [new(Identity, 1), new(second, 1)];
        var cut = new SourcePublicationCut(Scope, "authority", now, now.AddMinutes(1), heads, true);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim(); using var caller = new CancellationTokenSource();
        void Suspend() { entered.TrySetResult(); release.Wait(); released.TrySetResult(); }
        var suppliedHeads = Substitute.For<IReadOnlyList<SourcePublicationHead>>();
        suppliedHeads.Count.Returns(_ => { if (vector.EndsWith("count", StringComparison.Ordinal)) { Suspend(); } return 2; });
        IEnumerable<SourcePublicationHead> Heads() { if (vector.EndsWith("items", StringComparison.Ordinal)) { Suspend(); } foreach (var head in heads) { yield return head; } }
        suppliedHeads.GetEnumerator().Returns(_ => Heads().GetEnumerator());
        var namespaces = Substitute.For<ISourcePublicationNamespaceSource>(); int namespaceCalls = 0;
        namespaces.ReadAsync(Scope, Arg.Any<CancellationToken>()).Returns(_ =>
        {
            int call = Interlocked.Increment(ref namespaceCalls);
            return (vector.StartsWith("initial-cut-", StringComparison.Ordinal) && call == 1)
                || (vector.StartsWith("final-cut-", StringComparison.Ordinal) && call == 2) ? cut with { Sources = suppliedHeads } : cut;
        });
        byte[] original = [1, 2, 3];
        var flags = Substitute.For<IReadOnlyDictionary<string, string>>();
        flags.Count.Returns(_ => { if (vector.EndsWith("count", StringComparison.Ordinal)) { Suspend(); } return 1; });
        IEnumerable<KeyValuePair<string, string>> Flags() { if (vector.EndsWith("items", StringComparison.Ordinal)) { Suspend(); } yield return new("mode", "safe"); }
        flags.GetEnumerator().Returns(_ => Flags().GetEnumerator());
        StreamReadEvent item = new(1, "Created", original, "json", 1, "original", null, null, now, null);
        var events = Substitute.For<IReadOnlyList<StreamReadEvent>>();
        events.Count.Returns(_ => { if (vector.EndsWith("count", StringComparison.Ordinal)) { Suspend(); } return 1; });
        IEnumerable<StreamReadEvent> Events() { if (vector.EndsWith("items", StringComparison.Ordinal)) { Suspend(); } yield return item; }
        events.GetEnumerator().Returns(_ => Events().GetEnumerator());
        var streams = Substitute.For<IAuthoritativeEventStreamReader>(); int streamCalls = 0; int folds = 0;
        streams.ReadAsync(Arg.Any<AggregateIdentity>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            Interlocked.Increment(ref streamCalls); var identity = call.Arg<AggregateIdentity>();
            bool selected = vector.StartsWith("first-", StringComparison.Ordinal) ? identity == Identity : identity == second;
            IReadOnlyList<StreamReadEvent> supplied = selected && vector.Contains("-source-", StringComparison.Ordinal) ? events
                : selected && vector.Contains("-flags-", StringComparison.Ordinal) ? [item with { ProtectionMetadata = new(PayloadProtectionState.Unprotected, 1, null, null, null, flags) }] : [item];
            return new AuthoritativeStreamReadResult(new(identity, 1, now, supplied, "exact-original"), null);
        });
        var reader = new SourceNamespaceSnapshotReader(namespaces, streams, clock);
        var reading = Task.Run(() => reader.ReadFoldedAsync<string>(Scope, (source, token) =>
        { token.ThrowIfCancellationRequested(); Interlocked.Increment(ref folds); return source.Identity.ActorId; }, caller.Token), TestContext.Current.CancellationToken);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            if (cancelCaller)
            {
                caller.Cancel();
                (await Should.ThrowAsync<OperationCanceledException>(() => reading.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken))).CancellationToken.ShouldBe(caller.Token);
            }
            else
            {
                clock.Advance(TimeSpan.FromSeconds(30));
                (await reading.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken)).ShouldBeNull();
            }
            int originalFolds = Volatile.Read(ref folds); int originalReads = Volatile.Read(ref streamCalls); int originalCuts = Volatile.Read(ref namespaceCalls);
            release.Set(); await released.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            reading.IsCompleted.ShouldBeTrue(); Volatile.Read(ref folds).ShouldBe(originalFolds);
            Volatile.Read(ref streamCalls).ShouldBe(originalReads); Volatile.Read(ref namespaceCalls).ShouldBe(originalCuts);
            original.ShouldBe(new byte[] { 1, 2, 3 });
        }
        finally { release.Set(); }
    }


}
