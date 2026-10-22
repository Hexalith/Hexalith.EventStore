using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Streams;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Streams;

/// <summary>Source-qualified finite-cut/backfill/checkpoint regressions; synthetic authority is never production qualification.</summary>
public sealed class SourcePublicationFeedTests
{
    private static readonly SourcePublicationScope Scope = new("tenant-a", "conversation", "approved-deletion-v1", "installation-1");
    private static readonly AggregateIdentity Identity = new("tenant-a", "conversation", "source-a");
    private static readonly DateTimeOffset At = DateTimeOffset.UtcNow;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static AuthoritativeEventStream Stream(long head = 2)
        => new(Identity, head, At, Enumerable.Range(1, (int)head).Select(i => new StreamReadEvent(i, "Publication",
            JsonSerializer.SerializeToUtf8Bytes(new[] { $"safe-publication-{i}" }), "json", 1, $"event-{i}", null, null, At, null)).ToArray(), "observation-1");
    private static SourcePublicationCut Cut(long head = 2) => new(Scope, "namespace-authority-1", At.AddSeconds(-1), At.AddMinutes(1), [new(Identity, head)], true);

    private static (SourcePublicationFeed Feed, ISourcePublicationNamespaceSource Namespace, IAuthoritativeEventStreamReader Streams,
        ISourcePublicationProjector Projector, ISourcePublicationIndexStore Store, Func<SourcePublicationIndexState?> State) Fixture(TimeProvider? clock = null)
    {
        var namespaces = Substitute.For<ISourcePublicationNamespaceSource>();
        namespaces.ReadAsync(Scope, Arg.Any<CancellationToken>()).Returns(Cut());
        var streams = Substitute.For<IAuthoritativeEventStreamReader>();
        streams.ReadAsync(Identity, Arg.Any<CancellationToken>()).Returns(new AuthoritativeStreamReadResult(Stream(), null));
        var projector = Substitute.For<ISourcePublicationProjector>();
        projector.Project(Arg.Any<AuthoritativeEventStream>(), Arg.Any<CancellationToken>()).Returns(call =>
            call.ArgAt<AuthoritativeEventStream>(0).Events.Select(e => new SourcePublicationDescriptor($"publication-{e.SequenceNumber}", Identity,
                e.SequenceNumber, e.MessageId, Convert.ToHexString(SHA256.HashData(e.Payload)))).ToArray());
        SourcePublicationIndexState? state = null;
        var store = Substitute.For<ISourcePublicationIndexStore>();
        store.ReadAsync(Scope, Arg.Any<CancellationToken>()).Returns(_ => Clone(state));
        store.TryWriteAsync(Scope, Arg.Any<long>(), Arg.Any<SourcePublicationIndexState>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            if ((state?.Revision ?? 0) != call.ArgAt<long>(1) || state?.PoisonCode is not null) { return false; }
            state = Clone(call.ArgAt<SourcePublicationIndexState>(2)); return true;
        });
        return (new(namespaces, streams, projector, store, clock ?? TimeProvider.System), namespaces, streams, projector, store, () => Clone(state));
    }
    private static SourcePublicationIndexState? Clone(SourcePublicationIndexState? state)
        => state is null ? null : JsonSerializer.Deserialize<SourcePublicationIndexState>(JsonSerializer.SerializeToUtf8Bytes(state, Json), Json);

    /// <summary>Native metadata survives persisted restart and refuses equal-head replacement without changing retained publications.</summary>
    [Theory]
    [InlineData("incarnation")]
    [InlineData("digest")]
    [InlineData("missing")]
    public async Task NativePrefixReplacementPoisonsPersistedIndex(string change)
    {
        var f = Fixture();
        var source = NativePublicationFixture.Stream(Identity, At);
        NativeSourceProof proof = source.NativeProof!;
        var head = new SourcePublicationHead(Identity, 2) { NativeProof = proof };
        var cut = Cut() with { Sources = [head] };
        f.Namespace.ReadAsync(Scope, Arg.Any<CancellationToken>()).Returns(cut);
        f.Streams.ReadAsync(Identity, Arg.Any<CancellationToken>()).Returns(new AuthoritativeStreamReadResult(source, null));
        (await f.Feed.ReadAsync(Scope, cancellationToken: TestContext.Current.CancellationToken)).IsAvailable.ShouldBeTrue();
        f.State()!.Sources.Single().NativeProof.ShouldBe(proof);
        var retained = f.State()!.Entries.ToArray();
        NativeSourceProof? changed = change switch
        {
            "incarnation" => proof with { SourceIncarnation = "01ARZ3NDEKTSV4RRFFQ69G5FAW" },
            "digest" => proof with { CumulativeDigest = new string('b', 64) },
            _ => null,
        };
        f.Namespace.ReadAsync(Scope, Arg.Any<CancellationToken>()).Returns(cut with { Sources = [head with { NativeProof = changed }] });
        var restart = new SourcePublicationFeed(f.Namespace, f.Streams, f.Projector, f.Store, TimeProvider.System);
        (await restart.ReadAsync(Scope, cancellationToken: TestContext.Current.CancellationToken)).FailureReason.ShouldBe("publication-source-incompatible");
        f.State()!.Entries.ShouldBe(retained); f.State()!.Sources.Single().NativeProof.ShouldBe(proof);
        f.State()!.PoisonCode.ShouldBe("publication-source-incompatible");
    }

    /// <summary>The native cut binds the codec-verified source prefix, rather than only its final message identifier.</summary>
    [Theory]
    [InlineData("matching")]
    [InlineData("advanced")]
    [InlineData("missing-proof")]
    [InlineData("incarnation")]
    [InlineData("digest")]
    [InlineData("event-digest")]
    public async Task NativeCutRequiresActualMatchingSourcePrefixBeforeProjectionOrPersistence(string scenario)
    {
        var f = Fixture();
        var original = NativePublicationFixture.Stream(Identity, At);
        var source = scenario == "advanced" ? NativePublicationFixture.Stream(Identity, At, 3) : original;
        source = scenario switch
        {
            "missing-proof" => source with { NativeProof = null },
            "incarnation" => NativePublicationFixture.Stream(Identity, At, 2, "01ARZ3NDEKTSV4RRFFQ69G5FAW"),
            "digest" => source with { NativeProof = source.NativeProof! with { CumulativeDigest = new string('b', 64) } },
            "event-digest" => source with { Events = [source.Events[0] with { NativeEventDigest = new string('c', 64) }, source.Events[1]] },
            _ => source,
        };
        f.Namespace.ReadAsync(Scope, Arg.Any<CancellationToken>()).Returns(Cut() with { Sources = [new(Identity, 2) { NativeProof = original.NativeProof }] });
        f.Streams.ReadAsync(Identity, Arg.Any<CancellationToken>()).Returns(new AuthoritativeStreamReadResult(source, null));
        var result = await f.Feed.ReadAsync(Scope, cancellationToken: TestContext.Current.CancellationToken);
        if (scenario is not ("matching" or "advanced"))
        {
            result.FailureReason.ShouldBe("publication-source-hole"); f.State().ShouldBeNull();
            f.Projector.ReceivedCalls().ShouldBeEmpty();
            f.Store.ReceivedCalls().ShouldNotContain(call => call.GetMethodInfo().Name == nameof(ISourcePublicationIndexStore.TryWriteAsync)
                || call.GetMethodInfo().Name == nameof(ISourcePublicationIndexStore.AdvanceReconciliationProgressAsync));
            return;
        }
        result.IsAvailable.ShouldBeTrue();
        var selected = f.Projector.ReceivedCalls().Single().GetArguments()[0].ShouldBeOfType<AuthoritativeEventStream>();
        selected.NativeProof.ShouldBe(original.NativeProof);
        selected.Head.ShouldBe(2); JsonSerializer.Serialize(selected.Events).ShouldBe(JsonSerializer.Serialize(original.Events));
        f.State()!.Sources.Single().NativeProof.ShouldBe(original.NativeProof);
    }

    /// <summary>Native enrollment cannot migrate or poison legacy offsets and independently anchored work.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task LegacyToNativeRequiresExplicitUpgradeWithoutChangingRetainedState(int mode)
    {
        var f = Fixture();
        (await f.Feed.ReadAsync(Scope, cancellationToken: TestContext.Current.CancellationToken)).IsAvailable.ShouldBeTrue();
        var prior = f.State()!;
        if (mode > 0)
        {
            var progress = new SourcePublicationReconciliationProgress(Scope, 1, Cut().AuthorityRevision,
                Cut().Sources, 1, prior.Entries.Select(entry => entry.Publication).ToArray(), "original-legacy-receipt") { OriginalCut = Cut() };
            (await f.Store.TryWriteAsync(Scope, prior.Revision, prior with { Revision = prior.Revision + 1, ReconciliationProgress = progress,
                Sources = mode == 2 ? [] : prior.Sources, Entries = mode == 2 ? [] : prior.Entries }, TestContext.Current.CancellationToken)).ShouldBeTrue();
        }
        byte[] retained = JsonSerializer.SerializeToUtf8Bytes(f.State(), Json);
        var native = NativePublicationFixture.Stream(Identity, At);
        f.Namespace.ReadAsync(Scope, Arg.Any<CancellationToken>()).Returns(Cut() with { Sources = [new(Identity, 2) { NativeProof = native.NativeProof }] });
        f.Store.ClearReceivedCalls(); f.Streams.ClearReceivedCalls();
        (await f.Feed.ReadAsync(Scope, cancellationToken: TestContext.Current.CancellationToken)).FailureReason.ShouldBe("publication-source-upgrade-required");
        JsonSerializer.SerializeToUtf8Bytes(f.State(), Json).ShouldBe(retained);
        f.Streams.ReceivedCalls().ShouldBeEmpty();
        f.Store.ReceivedCalls().ShouldAllBe(call => call.GetMethodInfo().Name == nameof(ISourcePublicationIndexStore.ReadAsync));
    }

    /// <summary>Independently retained work selects the original native prefix after the unworked source advances.</summary>
    [Fact]
    public async Task RetainedOriginalNativeCutCompletesAgainstAuthenticAdvancedSource()
    {
        var f = Fixture();
        var secondIdentity = new AggregateIdentity(Scope.Tenant, Scope.Domain, "source-b");
        var first = NativePublicationFixture.Stream(Identity, At, 1);
        var second = NativePublicationFixture.Stream(secondIdentity, At, 1);
        var advanced = NativePublicationFixture.Stream(secondIdentity, At, 2);
        var original = Cut() with { Sources = [new(Identity, 1) { NativeProof = first.NativeProof }, new(secondIdentity, 1) { NativeProof = second.NativeProof }] };
        var current = original with { Sources = [original.Sources[0], new(secondIdentity, 2) { NativeProof = advanced.NativeProof }] };
        static SourcePublicationDescriptor[] Project(AuthoritativeEventStream source) => [.. source.Events.Select(item =>
            new SourcePublicationDescriptor(source.Identity.AggregateId + "-" + item.SequenceNumber, source.Identity,
                item.SequenceNumber, item.MessageId, Convert.ToHexString(SHA256.HashData(item.Payload))))];
        var progress = new SourcePublicationReconciliationProgress(Scope, 1, original.AuthorityRevision,
            original.Sources, 1, Project(first), "independent-original") { OriginalCut = original };
        var partial = new SourcePublicationIndexState(Scope, 1, original.AuthorityRevision, [], [], null) { ReconciliationProgress = progress };
        (await f.Store.TryWriteAsync(Scope, 0, partial, TestContext.Current.CancellationToken)).ShouldBeTrue();
        f.Namespace.ReadAsync(Scope, Arg.Any<CancellationToken>()).Returns(current);
        f.Streams.ReadAsync(secondIdentity, Arg.Any<CancellationToken>()).Returns(new AuthoritativeStreamReadResult(advanced, null));
        f.Projector.Project(Arg.Any<AuthoritativeEventStream>(), Arg.Any<CancellationToken>()).Returns(call => Project(call.Arg<AuthoritativeEventStream>()));
        f.Store.ReadReconciliationProgressAsync(Arg.Any<SourcePublicationCut>(), Arg.Any<CancellationToken>()).Returns(_ => progress);
        f.Store.AdvanceReconciliationProgressAsync(Arg.Any<SourcePublicationReconciliationAdvance>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            var proposal = call.Arg<SourcePublicationReconciliationAdvance>();
            proposal.Cut.Sources.ShouldBe(original.Sources); proposal.CurrentCut!.Sources.ShouldBe(current.Sources);
            proposal.Source.ShouldBe(original.Sources[1]); proposal.ObservedHead.ShouldBe(2);
            proposal.Publications.ShouldBe(Project(second));
            progress = progress with { Version = 2, VerifiedSources = 2, Publications = progress.Publications.Concat(proposal.Publications).ToArray() };
            var state = f.State()!;
            (await f.Store.TryWriteAsync(Scope, state.Revision, state with { Revision = state.Revision + 1, ReconciliationProgress = progress }, call.Arg<CancellationToken>())).ShouldBeTrue();
            return (SourcePublicationReconciliationProgress?)progress;
        });
        var result = await new SourcePublicationFeed(f.Namespace, f.Streams, f.Projector, f.Store, TimeProvider.System)
            .ReadAsync(Scope, cancellationToken: TestContext.Current.CancellationToken);
        result.IsAvailable.ShouldBeTrue();
        result.Page!.Checkpoint.Sources.ShouldBe(original.Sources);
        result.Page.Entries.Select(entry => entry.Offset).ShouldBe([1L, 2L]);
        f.State()!.Sources[1].NativeProof.ShouldBe(second.NativeProof);
        var selected = f.Projector.ReceivedCalls().Single().GetArguments()[0].ShouldBeOfType<AuthoritativeEventStream>();
        selected.Head.ShouldBe(1); selected.NativeProof.ShouldBe(second.NativeProof);
        await f.Streams.DidNotReceive().ReadAsync(Identity, Arg.Any<CancellationToken>());
    }

    /// <summary>Retained work cannot structurally continue under a replaced or downgraded native source.</summary>
    [Fact]
    public void ReconciliationRequiresSameNativeIncarnationAndEqualHeadPrefix()
    {
        NativeSourceProof proof = new(1, Identity.TenantId, Identity.Domain, Identity.AggregateId,
            "01ARZ3NDEKTSV4RRFFQ69G5FAV", 2, "event-2", new string('a', 64), 1, false);
        var head = new SourcePublicationHead(Identity, 2) { NativeProof = proof };
        var cut = Cut() with { Sources = [head] };
        var progress = new SourcePublicationReconciliationProgress(Scope, 1, cut.AuthorityRevision,
            cut.Sources, 1, [], "synthetic-original") { OriginalCut = cut };
        progress.CanContinueUnder(cut).ShouldBeTrue();
        progress.CanContinueUnder(cut with { Sources = [head with { NativeProof = null }] }).ShouldBeFalse();
        progress.CanContinueUnder(cut with { Sources = [head with { NativeProof = proof with { CumulativeDigest = new string('b', 64) } }] }).ShouldBeFalse();
        progress.CanContinueUnder(cut with { Sources = [head with { Head = 3, NativeProof = proof with
            { HeadSequence = 3, SourceIncarnation = "01ARZ3NDEKTSV4RRFFQ69G5FAW" } }] }).ShouldBeFalse();
    }

    /// <summary>Restart and ordered paging preserve exact source references/offsets and complete finite-cut state.</summary>
    [Fact]
    public async Task BackfillRestartAndPagingReuseOriginalOffsets()
    {
        var f = Fixture();
        var first = await f.Feed.ReadAsync(Scope, pageSize: 1, cancellationToken: TestContext.Current.CancellationToken);
        first.IsAvailable.ShouldBeTrue(); first.Page!.NextOffset.ShouldBe(1); first.Page.HasMore.ShouldBeTrue();
        f.State()!.Entries.Count.ShouldBe(2); f.State()!.Sources.Single().Head.ShouldBe(2);
        var restart = new SourcePublicationFeed(f.Namespace, f.Streams, f.Projector, f.Store, TimeProvider.System);
        var second = await restart.ReadAsync(Scope, first.Page.NextOffset, 1, TestContext.Current.CancellationToken);
        second.Page!.Entries.Single().Offset.ShouldBe(2); second.Page.HasMore.ShouldBeFalse();
        second.Page.Checkpoint.IndexRevision.ShouldBe(first.Page.Checkpoint.IndexRevision);
        (await restart.ReadAsync(Scope, cancellationToken: TestContext.Current.CancellationToken)).Page!.Checkpoint.IndexRevision.ShouldBe(first.Page.Checkpoint.IndexRevision);
        f.Store.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(ISourcePublicationIndexStore.TryWriteAsync)).ShouldBe(1);
        f.State()!.Entries.Select(e => e.Offset).ShouldBe([1L, 2L]);
    }

    /// <summary>No namespace completeness/cross-tenant assertion may trigger source disclosure or persistence.</summary>
    [Theory]
    [InlineData("incomplete")]
    [InlineData("foreign")]
    [InlineData("expired")]
    [InlineData("missing-observation")]
    public async Task UnqualifiedNamespaceReturnsNoCheckpointBeforeSourceReads(string vector)
    {
        var f = Fixture();
        var cut = vector switch { "incomplete" => Cut() with { IsComplete = false },
            "foreign" => Cut() with { Sources = [new(new("tenant-b", "conversation", "source-a"), 2)] },
            "missing-observation" => Cut() with { ObservedAt = default },
            _ => Cut() with { ValidUntil = At.AddSeconds(-1) } };
        f.Namespace.ReadAsync(Scope, Arg.Any<CancellationToken>()).Returns(cut);
        (await f.Feed.ReadAsync(Scope, cancellationToken: TestContext.Current.CancellationToken)).Page.ShouldBeNull();
        f.Streams.ReceivedCalls().ShouldBeEmpty(); f.State().ShouldBeNull();
    }

    /// <summary>Reserved/expected maximum cannot certify a missing committed source suffix.</summary>
    [Fact]
    public async Task MissingSourcePrefixDoesNotCommitOrCertifyHighWater()
    {
        var f = Fixture(); f.Streams.ReadAsync(Identity, Arg.Any<CancellationToken>()).Returns(new AuthoritativeStreamReadResult(Stream(1), null));
        var result = await f.Feed.ReadAsync(Scope, cancellationToken: TestContext.Current.CancellationToken);
        result.FailureReason.ShouldBe("publication-source-hole"); f.State().ShouldBeNull();
    }

    /// <summary>Namespace changes during reconciliation cannot release an index/cut.</summary>
    [Fact]
    public async Task ConcurrentNamespaceRevisionChangeRejectsBeforeCommit()
    {
        var f = Fixture(); int reads = 0;
        f.Namespace.ReadAsync(Scope, Arg.Any<CancellationToken>()).Returns(_ => ++reads == 1 ? Cut() : Cut() with { AuthorityRevision = "namespace-authority-2" });
        (await f.Feed.ReadAsync(Scope, cancellationToken: TestContext.Current.CancellationToken)).FailureReason.ShouldBe("publication-namespace-changed");
        f.State().ShouldBeNull();
    }

    /// <summary>Changed same-id evidence durably poisons and cannot later be silently skipped or acknowledged.</summary>
    [Fact]
    public async Task ChangedReferenceIsStickyPoisonAcrossRestart()
    {
        var f = Fixture(); (await f.Feed.ReadAsync(Scope, cancellationToken: TestContext.Current.CancellationToken)).IsAvailable.ShouldBeTrue();
        var original = f.State()!.Entries.Select(e => e.Publication).ToArray();
        f.Projector.Project(Arg.Any<AuthoritativeEventStream>(), Arg.Any<CancellationToken>()).Returns(original.Select(p => p with { StableFieldsDigest = new string('A', 64) }).ToArray());
        var changed = await f.Feed.ReadAsync(Scope, cancellationToken: TestContext.Current.CancellationToken);
        changed.Page.ShouldBeNull(); changed.FailureReason.ShouldBe("publication-reference-conflict");
        f.State()!.PoisonCode.ShouldBe("publication-reference-conflict"); f.State()!.Entries.Select(e => e.Publication).ShouldBe(original);
        f.Projector.Project(Arg.Any<AuthoritativeEventStream>(), Arg.Any<CancellationToken>()).Returns(original);
        var restart = new SourcePublicationFeed(f.Namespace, f.Streams, f.Projector, f.Store, TimeProvider.System);
        (await restart.ReadAsync(Scope, cancellationToken: TestContext.Current.CancellationToken)).FailureReason.ShouldBe("publication-reference-conflict");
    }

    /// <summary>A conflict after a new source candidate preserves only the structurally valid prior index snapshot.</summary>
    [Fact]
    public async Task ConflictAfterNewSourceCandidateCannotPersistUnmatchedNewReferences()
    {
        var f = Fixture(); await f.Feed.ReadAsync(Scope, cancellationToken: TestContext.Current.CancellationToken);
        var original = f.State()!; var other = new AggregateIdentity(Scope.Tenant, Scope.Domain, "source-b");
        f.Namespace.ReadAsync(Scope, Arg.Any<CancellationToken>()).Returns(Cut() with { Sources = [new(Identity, 2), new(other, 2)] });
        f.Streams.ReadAsync(other, Arg.Any<CancellationToken>()).Returns(new AuthoritativeStreamReadResult(Stream() with { Identity = other }, null));
        f.Projector.Project(Arg.Any<AuthoritativeEventStream>(), Arg.Any<CancellationToken>()).Returns(call =>
            call.ArgAt<AuthoritativeEventStream>(0).Identity == Identity ? original.Entries.Select(e => e.Publication).ToArray()
            : [new("new-reference", other, 1, "event-1", new string('B', 64)), new("publication-1", other, 2, "event-2", new string('C', 64))]);
        (await f.Feed.ReadAsync(Scope, cancellationToken: TestContext.Current.CancellationToken)).FailureReason.ShouldBe("publication-reference-conflict");
        f.State()!.Entries.ShouldBe(original.Entries); f.State()!.Sources.ShouldBe(original.Sources);
        (await f.Feed.ReadAsync(Scope, cancellationToken: TestContext.Current.CancellationToken)).FailureReason.ShouldBe("publication-reference-conflict");
    }

    /// <summary>Source restore regression retains original references and blocks a clean checkpoint.</summary>
    [Fact]
    public async Task RegressedSourceCutCannotRollBackIndex()
    {
        var f = Fixture(); await f.Feed.ReadAsync(Scope, cancellationToken: TestContext.Current.CancellationToken);
        f.Namespace.ReadAsync(Scope, Arg.Any<CancellationToken>()).Returns(Cut(1));
        (await f.Feed.ReadAsync(Scope, cancellationToken: TestContext.Current.CancellationToken)).FailureReason.ShouldBe("publication-source-regressed");
        f.State()!.Sources.Single().Head.ShouldBe(2); f.State()!.Entries.Count.ShouldBe(2);
    }

    /// <summary>A durable write with lost acknowledgement releases no verdict; reread reuses exact immutable offsets.</summary>
    [Fact]
    public async Task LostCommitAcknowledgementRequiresRereadWithoutDuplicatePublication()
    {
        var f = Fixture(); var persisted = (await f.Feed.ReadAsync(Scope, cancellationToken: TestContext.Current.CancellationToken)).Page!;
        var state = f.State()!;
        f.Store.TryWriteAsync(Scope, Arg.Any<long>(), Arg.Any<SourcePublicationIndexState>(), Arg.Any<CancellationToken>()).Returns(call =>
        { state = Clone(call.ArgAt<SourcePublicationIndexState>(2))!; return Task.FromException<bool>(new HttpRequestException("Controlled lost acknowledgement.")); });
        f.Store.ReadAsync(Scope, Arg.Any<CancellationToken>()).Returns(_ => Clone(state));
        f.Namespace.ReadAsync(Scope, Arg.Any<CancellationToken>()).Returns(Cut() with { AuthorityRevision = "new-authenticated-cut" });
        (await f.Feed.ReadAsync(Scope, cancellationToken: TestContext.Current.CancellationToken)).Page.ShouldBeNull();
        state.Entries.Count.ShouldBe(2); state.Entries.ShouldBe(f.State()!.Entries);
        f.Store.TryWriteAsync(Scope, Arg.Any<long>(), Arg.Any<SourcePublicationIndexState>(), Arg.Any<CancellationToken>()).Returns(call => { state = Clone(call.ArgAt<SourcePublicationIndexState>(2))!; return true; });
        var repeated = await f.Feed.ReadAsync(Scope, cancellationToken: TestContext.Current.CancellationToken);
        repeated.Page!.Entries.Select(e => e.Offset).ShouldBe(persisted.Entries.Select(e => e.Offset));
    }

    /// <summary>The original token wins cancellation during synchronous projection; no index/evidence is released.</summary>
    [Fact]
    public async Task CancellationDuringProjectionPreservesOriginalToken()
    {
        var f = Fixture(); using var caller = new CancellationTokenSource();
        f.Projector.Project(Arg.Any<AuthoritativeEventStream>(), Arg.Any<CancellationToken>()).Returns(_ => { caller.Cancel(); return Array.Empty<SourcePublicationDescriptor>(); });
        var exception = await Should.ThrowAsync<OperationCanceledException>(() => f.Feed.ReadAsync(Scope, cancellationToken: caller.Token));
        exception.CancellationToken.ShouldBe(caller.Token); f.State().ShouldBeNull();
    }

    /// <summary>Namespace invocation starts before controlled timeout/caller cancellation; noncooperative tasks release no evidence.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuspendedNamespaceIsBoundedWithoutSourceRead(bool cancelCaller)
    {
        var clock = new AuthoritativeReadTimeProvider(); var f = Fixture(clock);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<SourcePublicationCut?>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Namespace.ReadAsync(Scope, Arg.Any<CancellationToken>()).Returns(_ => { entered.TrySetResult(); return pending.Task; });
        using var caller = new CancellationTokenSource();
        var reading = f.Feed.ReadAsync(Scope, cancellationToken: caller.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        if (cancelCaller)
        {
            caller.Cancel(); var exception = await Should.ThrowAsync<OperationCanceledException>(() => reading.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
            exception.CancellationToken.ShouldBe(caller.Token);
        }
        else
        {
            clock.Advance(TimeSpan.FromSeconds(30));
            (await reading.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken)).FailureReason.ShouldBe("publication-time-bound-exceeded");
        }
        f.State().ShouldBeNull(); f.Streams.ReceivedCalls().ShouldBeEmpty(); pending.TrySetResult(Cut());
    }
    /// <summary>Actually suspended synchronous projection releases caller cancellation and deadline promptly without a late checkpoint or index mutation.</summary>
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task SuspendedProjectionCannotRetainTheFeedContinuation(bool callerCancellation)
    {
        var clock = new AuthoritativeReadTimeProvider(); clock.Advance(At - DateTimeOffset.UnixEpoch);
        var f = Fixture(clock); using var caller = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim(); var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Projector.Project(Arg.Any<AuthoritativeEventStream>(), Arg.Any<CancellationToken>()).Returns(_ =>
        { entered.TrySetResult(); release.Wait(); completed.TrySetResult(); return Array.Empty<SourcePublicationDescriptor>(); });
        var pending = f.Feed.ReadAsync(Scope, cancellationToken: caller.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        try
        {
            if (callerCancellation) { caller.Cancel(); (await Should.ThrowAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken))).CancellationToken.ShouldBe(caller.Token); }
            else { clock.Advance(TimeSpan.FromSeconds(30)); (await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).FailureReason.ShouldBe("publication-time-bound-exceeded"); }
            f.State().ShouldBeNull();
        }
        finally { release.Set(); }
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken); f.State().ShouldBeNull();
    }
}
