using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Streams;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Streams;

/// <summary>Exact finite-cut snapshot and fail-closed source fold regressions.</summary>
public sealed class SourceNamespaceSnapshotReaderTests
{
    private static readonly SourcePublicationScope Scope = new("tenant-a", "conversation", "catalogue", "installation-1");
    private static readonly AggregateIdentity Identity = new("tenant-a", "conversation", "source-a");
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static SourcePublicationCut Cut(long head = 2)
        => new(Scope, "authority-1", Now.AddSeconds(-1), Now.AddMinutes(1), [new(Identity, head)], true);

    private static AuthoritativeEventStream Stream(long head = 2)
        => new(Identity, head, Now, Enumerable.Range(1, (int)head)
            .Select(number => new StreamReadEvent(number, "Created", [1, 2], "json", 1,
                $"message-{number}", null, null, Now, null)).ToArray(), "observation-1");

    private static (SourceNamespaceSnapshotReader Reader, ISourcePublicationNamespaceSource Namespace,
        IAuthoritativeEventStreamReader Streams) Fixture(TimeProvider? clock = null)
    {
        var namespaces = Substitute.For<ISourcePublicationNamespaceSource>();
        namespaces.ReadAsync(Scope, Arg.Any<CancellationToken>()).Returns(Cut());
        var streams = Substitute.For<IAuthoritativeEventStreamReader>();
        streams.ReadAsync(Identity, Arg.Any<CancellationToken>()).Returns(new AuthoritativeStreamReadResult(Stream(), null));
        return (new(namespaces, streams, clock ?? TimeProvider.System), namespaces, streams);
    }

    /// <summary>Only complete exact prefixes from the stable cut produce values.</summary>
    [Fact]
    public async Task CompleteUnchangedCutReturnsFoldedValues()
    {
        var f = Fixture();
        SourceNamespaceSnapshot<string>? result = await f.Reader.ReadFoldedAsync(Scope,
            (source, token) => { token.ThrowIfCancellationRequested(); return $"{source.Identity.AggregateId}:{source.Events.Count}"; },
            TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result.Values.ShouldBe(["source-a:2"]);
        result.Cut.Sources.Single().Head.ShouldBe(2);
        await f.Namespace.Received(2).ReadAsync(Scope, Arg.Any<CancellationToken>());
        await f.Streams.Received(1).ReadAsync(Identity, Arg.Any<CancellationToken>());
    }

    /// <summary>Missing or unqualified namespace coverage never reaches the source reader.</summary>
    [Theory]
    [InlineData("missing")]
    [InlineData("incomplete")]
    [InlineData("foreign-scope")]
    [InlineData("foreign-source")]
    [InlineData("stale")]
    [InlineData("too-many-sources")]
    [InlineData("over-bound-head")]
    public async Task InvalidCutReturnsNoValues(string vector)
    {
        var f = Fixture();
        SourcePublicationCut? cut = vector switch
        {
            "missing" => null,
            "incomplete" => Cut() with { IsComplete = false },
            "foreign-scope" => Cut() with { Scope = new("tenant-b", "conversation", "catalogue", "installation-1") },
            "foreign-source" => Cut() with { Sources = [new(new("tenant-b", "conversation", "source-a"), 2)] },
            "stale" => Cut() with { ValidUntil = Now.AddSeconds(-1) },
            "too-many-sources" => Cut() with { Sources = Enumerable.Range(1, 1001)
                .Select(number => new SourcePublicationHead(new(Scope.Tenant, Scope.Domain, $"source-{number}"), 0)).ToArray() },
            _ => Cut(10001),
        };
        f.Namespace.ReadAsync(Scope, Arg.Any<CancellationToken>()).Returns(cut);
        (await f.Reader.ReadFoldedAsync(Scope, (source, _) => source.Identity.AggregateId,
            TestContext.Current.CancellationToken)).ShouldBeNull();
        f.Streams.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>Unavailable, foreign, truncated, noncontiguous, and over-bound source prefixes are never folded.</summary>
    [Theory]
    [InlineData("missing")]
    [InlineData("foreign")]
    [InlineData("short")]
    [InlineData("long")]
    [InlineData("gap")]
    [InlineData("payload-bound")]
    [InlineData("unreadable")]
    public async Task InvalidSourcePrefixReturnsNoValues(string vector)
    {
        var f = Fixture();
        AuthoritativeStreamReadResult read = vector switch
        {
            "missing" => new(null, "unavailable"),
            "foreign" => new(Stream() with { Identity = new("tenant-b", "conversation", "source-a") }, null),
            "short" => new(Stream(1), null),
            "long" => new(Stream(3), null),
            "gap" => new(Stream() with { Events = [Stream().Events[0], Stream().Events[1] with { SequenceNumber = 3 }] }, null),
            "payload-bound" => new(Stream() with { Events = [Stream().Events[0] with { Payload = new byte[16 * 1024 * 1024 + 1] }, Stream().Events[1]] }, null),
            _ => new(Stream() with { Events = [Stream().Events[0] with { ProtectionMetadata = EventStorePayloadProtectionMetadata.ProviderOpaque() }, Stream().Events[1]] }, null),
        };
        f.Streams.ReadAsync(Identity, Arg.Any<CancellationToken>()).Returns(read);
        bool folded = false;
        (await f.Reader.ReadFoldedAsync(Scope, (source, _) => { folded = true; return source.Identity.AggregateId; },
            TestContext.Current.CancellationToken)).ShouldBeNull();
        folded.ShouldBeFalse();
    }

    /// <summary>A changed authority or source head after folding cannot release partial values.</summary>
    [Theory]
    [InlineData("revision")]
    [InlineData("head")]
    [InlineData("missing")]
    public async Task ChangedFinalCutReturnsNoValues(string vector)
    {
        var f = Fixture(); int reads = 0;
        f.Namespace.ReadAsync(Scope, Arg.Any<CancellationToken>()).Returns(_ => ++reads == 1 ? Cut() : vector switch
        {
            "revision" => Cut() with { AuthorityRevision = "authority-2" },
            "head" => Cut(3),
            _ => null,
        });
        (await f.Reader.ReadFoldedAsync(Scope, (source, _) => source.Identity.AggregateId,
            TestContext.Current.CancellationToken)).ShouldBeNull();
        reads.ShouldBe(2);
    }

    /// <summary>An expired initial authorization cannot cover values even when a renewed cut has the same revision and heads.</summary>
    [Fact]
    public async Task ExpiredInitialCutReturnsNoValues()
    {
        var clock = new AuthoritativeReadTimeProvider(); var f = Fixture(clock);
        DateTimeOffset observed = clock.GetUtcNow();
        SourcePublicationCut first = Cut() with { ObservedAt = observed, ValidUntil = observed.AddMilliseconds(500) };
        SourcePublicationCut renewed = first with { ValidUntil = observed.AddMinutes(1) };
        int reads = 0;
        f.Namespace.ReadAsync(Scope, Arg.Any<CancellationToken>()).Returns(_ => ++reads == 1 ? first : renewed);
        f.Streams.ReadAsync(Identity, Arg.Any<CancellationToken>()).Returns(
            new AuthoritativeStreamReadResult(Stream() with { ObservedAt = observed }, null));
        (await f.Reader.ReadFoldedAsync(Scope, (source, _) =>
        {
            clock.Advance(TimeSpan.FromSeconds(1));
            return source.Identity.AggregateId;
        }, TestContext.Current.CancellationToken)).ShouldBeNull();
        reads.ShouldBe(2);
    }

    /// <summary>A fold exception cannot release earlier folded values.</summary>
    [Fact]
    public async Task FoldFailureReturnsNoPartialValues()
    {
        var f = Fixture();
        var second = new AggregateIdentity(Scope.Tenant, Scope.Domain, "source-b");
        f.Namespace.ReadAsync(Scope, Arg.Any<CancellationToken>()).Returns(Cut() with
        { Sources = [new(Identity, 2), new(second, 1)] });
        f.Streams.ReadAsync(second, Arg.Any<CancellationToken>()).Returns(
            new AuthoritativeStreamReadResult(Stream(1) with { Identity = second }, null));
        int folded = 0;
        (await f.Reader.ReadFoldedAsync<string>(Scope, (source, _) => ++folded == 1
            ? source.Identity.AggregateId : throw new InvalidOperationException("controlled"),
            TestContext.Current.CancellationToken)).ShouldBeNull();
        folded.ShouldBe(2);
    }

    /// <summary>A suspended provider is bounded even when it ignores cancellation; caller cancellation wins.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuspendedNamespaceIsBounded(bool cancelCaller)
    {
        var clock = new AuthoritativeReadTimeProvider(); var f = Fixture(clock);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<SourcePublicationCut?>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Namespace.ReadAsync(Scope, Arg.Any<CancellationToken>()).Returns(_ => { entered.TrySetResult(); return pending.Task; });
        using var caller = new CancellationTokenSource();
        var reading = f.Reader.ReadFoldedAsync(Scope, (source, _) => source.Identity.AggregateId, caller.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        if (cancelCaller)
        {
            caller.Cancel();
            var exception = await Should.ThrowAsync<OperationCanceledException>(
                () => reading.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
            exception.CancellationToken.ShouldBe(caller.Token);
        }
        else
        {
            clock.Advance(TimeSpan.FromSeconds(30));
            (await reading.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken)).ShouldBeNull();
        }
        f.Streams.ReceivedCalls().ShouldBeEmpty(); pending.TrySetResult(Cut());
    }
}
