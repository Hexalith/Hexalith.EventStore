using System.Reflection;
using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Actors.Runtime;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Streams;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Testing.Fakes;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Streams;

/// <summary>Actual DAPR index actor/storage transport with the standard committed-state test provider; no live backend qualification.</summary>
public sealed class DaprSourcePublicationIndexStoreTests
{
    private static readonly SourcePublicationScope Scope = new("tenant-a", "conversation", "approved-deletion-v1", "installation-1");
    private static readonly ConditionalWeakTable<IActorStateManager, ISourcePublicationOperationAuthority> Authorities = new();
    private static SourcePublicationIndexState State(long revision = 1, string? poison = null)
    {
        var source = new AggregateIdentity("tenant-a", "conversation", "source-a");
        return new(Scope, revision, "authority-1", [new(source, 2)], [new(1, new("signal-1", source, 2, "event-2", new string('A', 64)))], poison);
    }
    private static SourcePublicationIndexActor Actor(IActorStateManager state, ISourcePublicationOperationAuthority? operations = null)
    {
        var actor = new SourcePublicationIndexActor(ActorHost.CreateForTest<SourcePublicationIndexActor>(new ActorTestOptions { ActorId = new(Scope.ActorId) }), operations ?? Authorities.GetValue(state, _ => Operations()));
        typeof(Dapr.Actors.Runtime.Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance)!.SetValue(actor, state);
        return actor;
    }
    private static ISourcePublicationOperationAuthority Operations(Func<bool>? journalAvailable = null)
    {
        var operations = Substitute.For<ISourcePublicationOperationAuthority>(); operations.ReadIndexAsync(Arg.Any<SourcePublicationScope>()).Returns(true);
        operations.WriteIndexAsync(Arg.Any<SourcePublicationIndexWrite>()).Returns(true);
        long revision = 0; string digest = Digest(null);
        operations.ValidateIndexStateAsync(Scope, Arg.Any<long>(), Arg.Any<string>()).Returns(call => call.ArgAt<long>(1) == revision && call.ArgAt<string>(2) == digest);
        var admissions = new Dictionary<string, string>(StringComparer.Ordinal);
        operations.AdmitTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(call => {
            var t = call.Arg<AnchoredStateTransition>(); string exact = JsonSerializer.Serialize(t);
            if (admissions.TryGetValue(t.TargetDigest, out var admitted)) { return admitted == exact; }
            if (t.ScopeId != Scope.ActorId + "|source-publications-v1" || t.ExpectedRevision != revision || t.TargetRevision != revision + 1 || t.PredecessorDigest != digest) { return false; }
            admissions.Add(t.TargetDigest, exact); return true;
        });
        operations.RecoverTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(call => {
            var t = call.Arg<AnchoredStateTransition>(); return admissions.TryGetValue(t.TargetDigest, out var admitted) && admitted == JsonSerializer.Serialize(t)
                ? operations.RecordTransitionAsync(t, call.Arg<CancellationToken>()) : Task.FromResult(false);
        });
        var transitions = new Dictionary<string, AnchoredStateTransition>(StringComparer.Ordinal);
        operations.RecordTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(call => {
            var t = call.Arg<AnchoredStateTransition>();
            if (journalAvailable is not null && !journalAvailable()) { return false; }
            if (t.ScopeId != Scope.ActorId + "|source-publications-v1") { return false; }
            if (transitions.TryGetValue(t.TargetDigest, out var original)) { return JsonSerializer.Serialize(original) == JsonSerializer.Serialize(t); }
            if (t.ExpectedRevision != revision || t.TargetRevision != revision + 1 || t.PredecessorDigest != digest) { return false; }
            revision++; digest = t.TargetDigest; transitions.Add(t.TargetDigest, t with { TargetBytes = t.TargetBytes.ToArray() }); return true;
        });
        operations.VerifyTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(call => {
            var t = call.Arg<AnchoredStateTransition>(); return transitions.TryGetValue(t.TargetDigest, out var original) && JsonSerializer.Serialize(original) == JsonSerializer.Serialize(t);
        });
        operations.RecordIndexRevisionAsync(Scope, Arg.Any<long>(), Arg.Any<long>(), Arg.Any<string>()).Returns(call =>
        { if (call.ArgAt<long>(1) != revision || call.ArgAt<long>(2) != revision + 1) { return false; } revision++; digest = call.ArgAt<string>(3); return true; });
        return operations;
    }
    private static DaprSourcePublicationIndexStore Store(SourcePublicationIndexActor actor)
    {
        var proxies = Substitute.For<IActorProxyFactory>();
        proxies.CreateActorProxy<ISourcePublicationIndexActor>(new ActorId(Scope.ActorId), SourcePublicationIndexActor.ActorTypeName).Returns(actor);
        return new(proxies);
    }

    /// <summary>Actual SaveState persists the exact revision/references and a fresh actor instance reads the committed bytes.</summary>
    [Fact]
    public async Task ConditionalCommitAndRestartPreservePersistedExactOutcome()
    {
        var backend = new InMemoryStateManager(); var actor = Actor(backend); var store = Store(actor);
        (await store.ReadAsync(Scope, TestContext.Current.CancellationToken)).ShouldBeNull();
        (await store.TryWriteAsync(Scope, 0, State(), TestContext.Current.CancellationToken)).ShouldBeTrue();
        var saved = backend.CommittedState.Single().Value.ShouldBeOfType<SourcePublicationIndexState>();
        saved.Revision.ShouldBe(1); saved.Entries.Single().Publication.PublicationId.ShouldBe("signal-1");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(saved);
        var restored = new InMemoryStateManager();
        await restored.SetStateAsync(backend.CommittedState.Single().Key, JsonSerializer.Deserialize<SourcePublicationIndexState>(bytes)!);
        await restored.SaveStateAsync();
        var fresh = Store(Actor(restored, Authorities.GetValue(backend, _ => Operations()))); var read = await fresh.ReadAsync(Scope, TestContext.Current.CancellationToken);
        read!.Sources.Single().Head.ShouldBe(2); read.Entries.Single().ShouldBe(saved.Entries.Single());
        (await fresh.TryWriteAsync(Scope, 0, State(), TestContext.Current.CancellationToken)).ShouldBeFalse();
        restored.CommittedState.Single().Value.ShouldBeOfType<SourcePublicationIndexState>().Revision.ShouldBe(1);
    }

    /// <summary>A failed save cannot make its staged cache authoritative; an unknown committed outcome is read from persisted storage.</summary>
    [Theory]
    [InlineData(1, false)][InlineData(1, true)][InlineData(2, false)][InlineData(2, true)]
    public async Task FailedSaveCannotReleaseStagedState(int failSave, bool committed)
    {
        var backend = new InMemoryStateManager(); var manager = Faulting<SourcePublicationIndexState>(backend, failSave, committed); var actor = Actor(manager);
        await Should.ThrowAsync<HttpRequestException>(() => actor.TryWriteAsync(new(0, State())));
        foreach (var item in backend.CommittedState.ToArray())
        {
            if (item.Value is AnchoredStateTransition pending) { await backend.SetStateAsync(item.Key, JsonSerializer.Deserialize<AnchoredStateTransition>(JsonSerializer.Serialize(pending))!); }
            else { await backend.SetStateAsync(item.Key, JsonSerializer.Deserialize<SourcePublicationIndexState>(JsonSerializer.Serialize(item.Value))!); }
        }
        await backend.SaveStateAsync();
        var restarted = Actor(backend, Authorities.GetValue(manager, _ => Operations()));
        if (failSave == 1) { (await restarted.ReadAsync(Scope)).ShouldBeNull(); }
        (await restarted.TryWriteAsync(new(0, State()))).ShouldBe(failSave == 1 || !committed);
        (await restarted.ReadAsync(Scope))!.Entries.Single().Publication.PublicationId.ShouldBe("signal-1");
        backend.CommittedState.Single().Value.ShouldBeOfType<SourcePublicationIndexState>().Revision.ShouldBe(1);
    }

    /// <summary>A stale writer cannot overwrite later exact state, and poison has no implicit clear branch.</summary>
    [Fact]
    public async Task StaleRevisionAndPoisonCannotBeOverwritten()
    {
        var backend = new InMemoryStateManager(); var store = Store(Actor(backend));
        (await store.TryWriteAsync(Scope, 0, State(), TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await store.TryWriteAsync(Scope, 1, State(2, "publication-reference-conflict"), TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await store.TryWriteAsync(Scope, 1, State(2), TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await store.TryWriteAsync(Scope, 2, State(3), TestContext.Current.CancellationToken)).ShouldBeFalse();
        backend.CommittedState.Single().Value.ShouldBeOfType<SourcePublicationIndexState>().PoisonCode.ShouldBe("publication-reference-conflict");
    }

    /// <summary>Actor identity validates exact tenant/feed/installation before state lookup.</summary>
    [Theory]
    [InlineData("tenant")]
    [InlineData("feed")]
    [InlineData("installation")]
    public async Task CrossScopeActorCallsCannotReadOrWrite(string vector)
    {
        var backend = new InMemoryStateManager(); var actor = Actor(backend);
        var wrong = new SourcePublicationScope(vector == "tenant" ? "tenant-b" : Scope.Tenant, Scope.Domain,
            vector == "feed" ? "other-feed" : Scope.FeedName, vector == "installation" ? "other-installation" : Scope.InstallationId);
        await Should.ThrowAsync<ArgumentException>(() => actor.ReadAsync(wrong));
        await Should.ThrowAsync<ArgumentException>(() => actor.TryWriteAsync(new(0, State() with { Scope = wrong })));
        backend.CommittedState.ShouldBeEmpty();
    }

    /// <summary>Caller cancellation stops the exact transport wait with its original token.</summary>
    [Fact]
    public async Task CancelledTransportDoesNotReleaseLateState()
    {
        var proxy = Substitute.For<ISourcePublicationIndexActor>();
        var pending = new TaskCompletionSource<SourcePublicationIndexState?>(TaskCreationOptions.RunContinuationsAsynchronously);
        proxy.ReadAsync(Scope).Returns(pending.Task);
        var factories = Substitute.For<IActorProxyFactory>();
        factories.CreateActorProxy<ISourcePublicationIndexActor>(new ActorId(Scope.ActorId), SourcePublicationIndexActor.ActorTypeName).Returns(proxy);
        using var caller = new CancellationTokenSource();
        var reading = new DaprSourcePublicationIndexStore(factories).ReadAsync(Scope, caller.Token); caller.Cancel();
        var exception = await Should.ThrowAsync<OperationCanceledException>(() => reading);
        exception.CancellationToken.ShouldBe(caller.Token); pending.TrySetResult(State());
    }
    /// <summary>Existing immutable index cannot be released or changed through missing/withdrawn private read/write credentials.</summary>
    [Fact]
    public async Task MissingOrWithdrawnPrivateOperationCredentialDeniesIndex()
    {
        var backend = new InMemoryStateManager(); var actor = Actor(backend); (await actor.TryWriteAsync(new(0, State()))).ShouldBeTrue();
        var host = ActorHost.CreateForTest<SourcePublicationIndexActor>(new ActorTestOptions { ActorId = new(Scope.ActorId) });
        var operations = Authorities.GetValue(backend, _ => Operations()); var restricted = new SourcePublicationIndexActor(host, operations);
        typeof(Dapr.Actors.Runtime.Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance)!.SetValue(restricted, backend);
        operations.ReadIndexAsync(Scope).Returns(false); (await restricted.ReadAsync(Scope)).ShouldBeNull();
        operations.WriteIndexAsync(Arg.Any<SourcePublicationIndexWrite>()).Returns(false); (await restricted.TryWriteAsync(new(1, State(2)))).ShouldBeFalse();
        (await new SourcePublicationIndexActor(host).ReadAsync(Scope)).ShouldBeNull();
        backend.CommittedState.Single().Value.ShouldBeOfType<SourcePublicationIndexState>().Revision.ShouldBe(1);
        operations.ReadIndexAsync(Scope).Returns(true);
        (await actor.ReadAsync(Scope))!.Entries.Single().Publication.PublicationId.ShouldBe("signal-1");
    }

    /// <summary>Admitted writers cannot change, drop or reorder an immutable prefix or regress/drop source coverage; rejected candidates never enter committed state or restart reads.</summary>
    [Theory]
    [InlineData("changed")][InlineData("dropped")][InlineData("reordered")][InlineData("regressed-head")][InlineData("missing-head")]
    public async Task OwningMutationRejectsPrefixAndCoverageSubstitution(string vector)
    {
        var backend = new InMemoryStateManager(); var actor = Actor(backend); var original = State();
        var source = original.Sources.Single().Identity; var emptySource = new AggregateIdentity("tenant-a", "conversation", "source-empty");
        original = original with { Sources = [new(source, 4), new(emptySource, 0)], Entries = [original.Entries.Single(), new(2, new("signal-2", source, 3, "event-3", new string('B', 64)))] };
        (await actor.TryWriteAsync(new(0, original))).ShouldBeTrue(); var committed = backend.CommittedState.Single(); string bytes = JsonSerializer.Serialize(committed.Value);
        var next = original with { Revision = 2 };
        next = vector switch
        {
            "changed" => next with { Entries = [next.Entries[0] with { Publication = next.Entries[0].Publication with { StableFieldsDigest = new string('C', 64) } }, next.Entries[1]] },
            "dropped" => next with { Entries = [] },
            "reordered" => next with { Entries = [next.Entries[1] with { Offset = 1 }, next.Entries[0] with { Offset = 2 }] },
            "regressed-head" => next with { Sources = [new(source, 3), new(emptySource, 0)] },
            _ => next with { Sources = [new(source, 4)] },
        };
        (await actor.TryWriteAsync(new(1, next))).ShouldBeFalse(); JsonSerializer.Serialize(backend.CommittedState.Single().Value).ShouldBe(bytes);
        JsonSerializer.Serialize(await Actor(backend).ReadAsync(Scope)).ShouldBe(bytes);
    }

    /// <summary>Valid append growth retains exact prefix/head vectors, and deliberate poison can retain the exact committed vectors.</summary>
    [Fact]
    public async Task AppendGrowthAndExactPoisonRetentionPersist()
    {
        var backend = new InMemoryStateManager(); var actor = Actor(backend); var first = State(); (await actor.TryWriteAsync(new(0, first))).ShouldBeTrue();
        var next = first with { Revision = 2, Sources = [new(first.Sources.Single().Identity, 3)], Entries = [first.Entries.Single(), new(2, new("signal-2", first.Sources.Single().Identity, 3, "event-3", new string('B', 64)))] };
        (await actor.TryWriteAsync(new(1, next))).ShouldBeTrue(); (await actor.TryWriteAsync(new(2, next with { Revision = 3, PoisonCode = "publication-source-regressed" }))).ShouldBeTrue();
        var persisted = (await Actor(backend).ReadAsync(Scope))!; persisted.Revision.ShouldBe(3); persisted.Entries.ShouldBe(next.Entries); persisted.Sources.ShouldBe(next.Sources);
    }

    /// <summary>An older valid state or equal-revision divergent restore cannot reassign immutable publication offsets; only the independently anchored exact latest state is released.</summary>
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task IndependentExactAnchorRejectsOldAndDivergentRestore(bool divergent)
    {
        var backend = new InMemoryStateManager(); var actor = Actor(backend); var first = State(); await actor.TryWriteAsync(new(0, first));
        var latest = first with { Revision = 2, AuthorityRevision = "authority-2" }; await actor.TryWriteAsync(new(1, latest));
        string key = backend.CommittedState.Single().Key;
        await backend.SetStateAsync(key, divergent ? latest with { Entries = [latest.Entries.Single() with { Publication = latest.Entries.Single().Publication with { StableFieldsDigest = new string('B', 64) } }] } : first); await backend.SaveStateAsync();
        await Should.ThrowAsync<InvalidOperationException>(() => Actor(backend).ReadAsync(Scope));
        await Should.ThrowAsync<InvalidOperationException>(() => Actor(backend).TryWriteAsync(new(divergent ? 2 : 1, latest with { Revision = divergent ? 3 : 2 })));
        await backend.SetStateAsync(key, latest); await backend.SaveStateAsync(); (await Actor(backend).ReadAsync(Scope))!.Revision.ShouldBe(2);
    }

    /// <summary>Backend pending bytes do not authenticate a transition; exact independent predecessor/target proof is mandatory.</summary>
    [Theory]
    [InlineData("unanchored")][InlineData("predecessor")][InlineData("target")][InlineData("scope")]
    public async Task UnauthenticatedOrTamperedPendingCannotAdvanceOrRelease(string vector)
    {
        var backend = new InMemoryStateManager(); var operations = Operations();
        var pending = RecoverableAnchoredState.Prepare(Scope.ActorId + "|source-publications-v1", 0, 1, (SourcePublicationIndexState?)null, State());
        if (vector != "unanchored") { (await operations.RecordTransitionAsync(pending)).ShouldBeTrue(); }
        pending = vector switch
        {
            "predecessor" => pending with { PredecessorDigest = new string('A', 64) },
            "target" => RecoverableAnchoredState.Prepare(pending.ScopeId, 0, 1, (SourcePublicationIndexState?)null, State() with { AuthorityRevision = "changed" }),
            "scope" => pending with { ScopeId = "foreign-owner|source-publications-v1" },
            _ => pending,
        };
        await backend.SetStateAsync("source-publications-v1-pending-transition-v1", JsonSerializer.Deserialize<AnchoredStateTransition>(JsonSerializer.Serialize(pending))!);
        await backend.SaveStateAsync(); operations.ClearReceivedCalls();
        var actor = Actor(backend, operations);
        if (vector == "unanchored")
        {
            (await actor.ReadAsync(Scope)).ShouldBeNull();
            await Should.ThrowAsync<InvalidOperationException>(() => actor.TryWriteAsync(new(0, State() with { AuthorityRevision = "different-original" })));
            (await actor.TryWriteAsync(new(0, State()))).ShouldBeTrue();
        }
        else { await Should.ThrowAsync<InvalidOperationException>(() => actor.ReadAsync(Scope)); }
        await operations.Received(vector == "unanchored" ? 1 : 0).RecordTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>());
        if (vector != "unanchored") { backend.CommittedState.Single().Value.ShouldBeOfType<AnchoredStateTransition>(); }
    }

    /// <summary>A later admitted mutation recovers the retained exact original without its caller; ordinary reads never advance an unanchored stage.</summary>
    [Fact]
    public async Task LaterMutationRecoversPreJournalOriginalAfterRestartWithoutOriginalCaller()
    {
        bool journalAvailable = false; var backend = new InMemoryStateManager(); var operations = Operations(() => journalAvailable);
        (await Actor(backend, operations).TryWriteAsync(new(0, State()))).ShouldBeFalse();
        var original = backend.CommittedState.Single().Value.ShouldBeOfType<AnchoredStateTransition>();
        original.TargetRevision.ShouldBe(1); operations.ClearReceivedCalls();
        (await Actor(backend, operations).ReadAsync(Scope)).ShouldBeNull();
        await operations.DidNotReceive().RecordTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>());
        journalAvailable = true;
        (await Actor(backend, operations).TryWriteAsync(new(1, State(2)))).ShouldBeTrue();
        var saved = backend.CommittedState.Single().Value.ShouldBeOfType<SourcePublicationIndexState>();
        saved.Revision.ShouldBe(2); saved.Entries.ShouldBe(State().Entries);
        (await Actor(backend, operations).ReadAsync(Scope))!.Revision.ShouldBe(2);
    }

    private static string Digest(SourcePublicationIndexState? state) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(state)));

    private static IActorStateManager Faulting<T>(InMemoryStateManager backend, int failSave, bool committed)
    {
        var manager = Substitute.For<IActorStateManager>(); int saves = 0;
        manager.ClearCacheAsync(Arg.Any<CancellationToken>()).Returns(call => backend.ClearCacheAsync(call.Arg<CancellationToken>()));
        manager.TryGetStateAsync<T>(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => backend.TryGetStateAsync<T>(call.Arg<string>(), call.Arg<CancellationToken>()));
        manager.SetStateAsync(Arg.Any<string>(), Arg.Any<T>(), Arg.Any<CancellationToken>()).Returns(call => backend.SetStateAsync(call.Arg<string>(), call.Arg<T>(), call.Arg<CancellationToken>()));
        manager.TryGetStateAsync<AnchoredStateTransition>(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => backend.TryGetStateAsync<AnchoredStateTransition>(call.Arg<string>(), call.Arg<CancellationToken>()));
        manager.SetStateAsync(Arg.Any<string>(), Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(call => backend.SetStateAsync(call.Arg<string>(), call.Arg<AnchoredStateTransition>(), call.Arg<CancellationToken>()));
        manager.TryRemoveStateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => backend.TryRemoveStateAsync(call.Arg<string>(), call.Arg<CancellationToken>()));
        manager.SaveStateAsync(Arg.Any<CancellationToken>()).Returns(async call => {
            if (++saves != failSave) { await backend.SaveStateAsync(call.Arg<CancellationToken>()); return; }
            if (committed) { await backend.SaveStateAsync(call.Arg<CancellationToken>()); }
            throw new HttpRequestException("Controlled exact pending/main save failure or lost acknowledgement.");
        }); return manager;
    }
    /// <summary>A read-only prefix proposal cannot recover an independently admitted unanchored original before separate exact mutation admission.</summary>
    [Fact]
    public async Task ReadOnlyProgressProposalNeverElevatesPendingOriginalAuthority()
    {
        bool journal = true; var backend = new InMemoryStateManager(); var authority = Operations(() => journal); var actor = Actor(backend, authority);
        (await actor.TryWriteAsync(new(0, State()))).ShouldBeTrue(); journal = false;
        (await actor.TryWriteAsync(new(1, State(2)))).ShouldBeFalse();
        string before = JsonSerializer.Serialize(backend.CommittedState); authority.ClearReceivedCalls(); journal = true;
        authority.WriteIndexAsync(Arg.Any<SourcePublicationIndexWrite>()).Returns(false);
        var current = State(); var cut = new SourcePublicationCheckpoint(Scope, 1, current.AuthorityRevision, current.Sources, 1);
        var progress = new SourcePublicationDispatchProgress(Scope, 1, 1, cut.AuthorityRevision,
            Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(cut.Sources))), 1,
            Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(current.Entries))), "receiver", "worker-authority", "independent-original-prefix");
        authority.AuthorizeDispatchAdvanceAsync(Arg.Any<SourcePublicationDispatchAdvance>(), Arg.Any<SourcePublicationIndexState>()).Returns(progress);
        authority.VerifyDispatchProgressAsync(Arg.Any<SourcePublicationCheckpoint>(), Arg.Any<SourcePublicationDispatchProgress>()).Returns(true);
        (await Actor(backend, authority).AdvanceDispatchProgressAsync(new(cut, 0, current.Entries))).ShouldBeNull();
        await authority.DidNotReceive().RecoverTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>());
        await authority.DidNotReceive().RecordTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>());
        JsonSerializer.Serialize(backend.CommittedState).ShouldBe(before);
    }

    /// <summary>The actual DAPR transport retains an independently authenticated exact prefix across serialized actor restart and denies a withdrawn proof without changing the original.</summary>
    [Fact]
    public async Task QualifiedProgressTransportPreservesOriginalAcrossRestartAndWithdrawal()
    {
        var backend = new InMemoryStateManager(); var authority = Operations(); var store = Store(Actor(backend, authority));
        var state = State(); (await store.TryWriteAsync(Scope, 0, state, TestContext.Current.CancellationToken)).ShouldBeTrue();
        var cut = new SourcePublicationCheckpoint(Scope, state.Revision, state.AuthorityRevision, state.Sources, state.Entries.Count);
        bool proofCurrent = true;
        var original = new SourcePublicationDispatchProgress(Scope, 1, 1, cut.AuthorityRevision,
            Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(cut.Sources))), 1,
            Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(state.Entries))), "receiver", "worker-authority", "qualified-original-prefix");
        authority.AuthorizeDispatchAdvanceAsync(Arg.Any<SourcePublicationDispatchAdvance>(), Arg.Any<SourcePublicationIndexState>()).Returns(call =>
        {
            var proposal = call.Arg<SourcePublicationDispatchAdvance>();
            return proposal.ExpectedPrefix == 0 && proposal.AcknowledgedEntries.SequenceEqual(state.Entries) ? original : null;
        });
        authority.VerifyDispatchProgressAsync(Arg.Any<SourcePublicationCheckpoint>(), Arg.Any<SourcePublicationDispatchProgress>()).Returns(call =>
            proofCurrent && JsonSerializer.Serialize(call.Arg<SourcePublicationDispatchProgress>()) == JsonSerializer.Serialize(original));
        (await store.ReadDispatchProgressAsync(cut, TestContext.Current.CancellationToken)).ShouldBeNull();
        JsonSerializer.Serialize(await store.AdvanceDispatchProgressAsync(new(cut, 0, state.Entries), TestContext.Current.CancellationToken)).ShouldBe(JsonSerializer.Serialize(original));
        var persisted = backend.CommittedState.Single(); var restored = new InMemoryStateManager();
        await restored.SetStateAsync(persisted.Key, JsonSerializer.Deserialize<SourcePublicationIndexState>(JsonSerializer.Serialize(persisted.Value))!);
        await restored.SaveStateAsync(); var fresh = Store(Actor(restored, authority));
        JsonSerializer.Serialize(await fresh.ReadDispatchProgressAsync(cut, TestContext.Current.CancellationToken)).ShouldBe(JsonSerializer.Serialize(original));
        var exact = JsonSerializer.Serialize(restored.CommittedState); proofCurrent = false;
        (await fresh.ReadDispatchProgressAsync(cut, TestContext.Current.CancellationToken)).ShouldBeNull();
        JsonSerializer.Serialize(restored.CommittedState).ShouldBe(exact);
        restored.CommittedState.Single().Value.ShouldBeOfType<SourcePublicationIndexState>().Revision.ShouldBe(2);
    }

    /// <summary>A suspended progress owner cannot hold the transport caller after cancellation or release its late proof as caller success.</summary>
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task CancelledProgressTransportDoesNotReleaseLateProof(bool advancing)
    {
        var state = State(); var cut = new SourcePublicationCheckpoint(Scope, state.Revision, state.AuthorityRevision, state.Sources, 1);
        var advance = new SourcePublicationDispatchAdvance(cut, 0, state.Entries);
        var pending = new TaskCompletionSource<SourcePublicationDispatchProgress?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var proxy = Substitute.For<ISourcePublicationIndexActor>();
        proxy.ReadDispatchProgressAsync(cut).Returns(pending.Task); proxy.AdvanceDispatchProgressAsync(advance).Returns(pending.Task);
        var factories = Substitute.For<IActorProxyFactory>();
        factories.CreateActorProxy<ISourcePublicationIndexActor>(new ActorId(Scope.ActorId), SourcePublicationIndexActor.ActorTypeName).Returns(proxy);
        var store = new DaprSourcePublicationIndexStore(factories); using var caller = new CancellationTokenSource();
        var waiting = advancing ? store.AdvanceDispatchProgressAsync(advance, caller.Token) : store.ReadDispatchProgressAsync(cut, caller.Token);
        waiting.IsCompleted.ShouldBeFalse(); caller.Cancel();
        var exception = await Should.ThrowAsync<OperationCanceledException>(() => waiting); exception.CancellationToken.ShouldBe(caller.Token);
        pending.TrySetResult(new(Scope, 1, 1, cut.AuthorityRevision, new string('A', 64), 1, new string('B', 64), "receiver", "authority", "late-original"));
        waiting.IsCanceled.ShouldBeTrue();
    }

    private static SourcePublicationDescriptor ReconciliationPublication(SourcePublicationHead head)
        => new("publication-" + head.Identity.AggregateId, head.Identity, 1, "message-" + head.Identity.AggregateId, new string('A', 64));
    private static void QualifyReconciliation(ISourcePublicationOperationAuthority authority, Func<SourcePublicationCut> currentCut, TimeProvider clock, Func<bool>? proofCurrent = null, bool finiteContinuation = false)
    {
        var proofs = new Dictionary<string, string>(StringComparer.Ordinal); int issued = 0;
        bool Current(SourcePublicationCut cut) => (proofCurrent is null || proofCurrent()) && cut.Scope == currentCut().Scope && cut.AuthorityRevision == currentCut().AuthorityRevision
            && cut.Sources.SequenceEqual(currentCut().Sources) && cut.IsComplete && currentCut().IsComplete && cut.ValidUntil > clock.GetUtcNow()
            && currentCut().ValidUntil > clock.GetUtcNow();
        bool Preserved(SourcePublicationCut cut) => finiteContinuation && (proofCurrent is null || proofCurrent()) && cut.IsComplete
            && cut.Scope == currentCut().Scope && cut.AuthorityRevision == currentCut().AuthorityRevision && currentCut().IsComplete
            && cut.ValidUntil > clock.GetUtcNow() && currentCut().ValidUntil > clock.GetUtcNow()
            && cut.Sources.Count == currentCut().Sources.Count
            && cut.Sources.Zip(currentCut().Sources).All(pair => pair.First.Identity == pair.Second.Identity && pair.First.Head <= pair.Second.Head);
        bool Verified(SourcePublicationCut cut, SourcePublicationReconciliationProgress progress) => (Current(cut) || Preserved(cut)) && progress.MatchesCut(cut)
            && proofs.TryGetValue(progress.ReceiptId, out var original) && original == JsonSerializer.Serialize(progress);
        authority.VerifyReconciliationProgressAsync(Arg.Any<SourcePublicationReconciliationProgress>()).Returns(call =>
        {
            var progress = call.Arg<SourcePublicationReconciliationProgress>();
            return Verified(finiteContinuation ? progress.OriginalCut ?? currentCut() : currentCut(), progress);
        });
        authority.VerifyFiniteCutContinuationAsync(Arg.Any<SourcePublicationReconciliationProgress>(), Arg.Any<SourcePublicationCut>()).Returns(call =>
        {
            var progress = call.Arg<SourcePublicationReconciliationProgress>(); var fresh = call.Arg<SourcePublicationCut>();
            return progress.OriginalCut is { } original && Preserved(original) && Current(fresh) && progress.CanContinueUnder(fresh) && Verified(original, progress);
        });
        authority.AuthorizeReconciliationAdvanceAsync(Arg.Any<SourcePublicationReconciliationAdvance>(), Arg.Any<SourcePublicationIndexState?>()).Returns(call =>
        {
            var proposal = call.Arg<SourcePublicationReconciliationAdvance>(); var state = call.Arg<SourcePublicationIndexState?>(); var original = state?.ReconciliationProgress;
            bool reuse = original is not null && Verified(proposal.Cut, original);
            int predecessor = reuse ? original!.VerifiedSources : 0;
            if (!(Current(proposal.Cut) || Preserved(proposal.Cut) && proposal.CurrentCut is { } fresh && Current(fresh)) || proposal.ExpectedVerifiedSources != predecessor || proposal.Source != proposal.Cut.Sources[predecessor]
                || proposal.ObservedHead < proposal.Source.Head || proposal.ObservationId != "independent-" + proposal.Source.Identity.AggregateId
                || !proposal.Publications.SequenceEqual(ReconciliationPublications(proposal.Source))) { return (SourcePublicationReconciliationProgress?)null; }
            var proof = new SourcePublicationReconciliationProgress(proposal.Cut.Scope, (original?.Version ?? 0) + 1, proposal.Cut.AuthorityRevision,
                proposal.Cut.Sources, predecessor + 1, (reuse ? original!.Publications : []).Concat(proposal.Publications).ToArray(), "reconciliation-original-" + ++issued)
                { OriginalCut = proposal.Cut };
            proofs.Add(proof.ReceiptId, JsonSerializer.Serialize(proof)); return proof;
        });
    }
    private static IReadOnlyList<SourcePublicationDescriptor> ReconciliationPublications(SourcePublicationHead head)
        => head.Head == 1 ? [ReconciliationPublication(head)] : [ReconciliationPublication(head),
            new("append-" + head.Identity.AggregateId, head.Identity, 2, "append-message-" + head.Identity.AggregateId, new string('B', 64))];
    private static AuthoritativeStreamReadResult ReconciliationSource(SourcePublicationHead head, TimeProvider clock)
        => new(new(head.Identity, head.Head, clock.GetUtcNow(), ReconciliationPublications(head).Select(p => new StreamReadEvent(p.SourceRevision,
            "Publication", [1], "json", 1, p.SourceMessageId, null, null, clock.GetUtcNow(), null)).ToArray(), "independent-" + head.Identity.AggregateId), null);

    /// <summary>Forty source reconciliations taking one second each progress across an actual serialized index/dispatcher restart before any complete checkpoint is released.</summary>
    [Fact]
    public Task SlowInitialSourceReconciliationProgressesAcrossActualOwnerRestart() => ReconcileAcrossRestartAsync(false);

    /// <summary>Authenticated monotonic appends between every resumed pass cannot strand the original forty-source cut; subsequent reconciliation delivers the append once.</summary>
    [Fact]
    public Task MonotonicAppendsCompleteOriginalCutAcrossActualOwnerRestart() => ReconcileAcrossRestartAsync(true);

    private static async Task ReconcileAcrossRestartAsync(bool appendBetweenPasses)
    {
        var clock = new AuthoritativeReadTimeProvider(); clock.Advance(DateTimeOffset.UtcNow - DateTimeOffset.UnixEpoch);
        var heads = Enumerable.Range(0, 40).Select(n => new SourcePublicationHead(new(Scope.Tenant, Scope.Domain, "source-" + n.ToString("D2", System.Globalization.CultureInfo.InvariantCulture)), 1)).ToArray();
        var cut = new SourcePublicationCut(Scope, "qualified-cut", clock.GetUtcNow(), clock.GetUtcNow().AddHours(1), heads, true);
        var backend = new InMemoryStateManager(); var authority = Operations(); QualifyReconciliation(authority, () => cut, clock, finiteContinuation: appendBetweenPasses);
        var namespaces = Substitute.For<ISourcePublicationNamespaceSource>(); namespaces.ReadAsync(Scope, Arg.Any<CancellationToken>()).Returns(_ => cut);
        var streams = Substitute.For<IAuthoritativeEventStreamReader>(); var readIdentities = new List<string>();
        streams.ReadAsync(Arg.Any<AggregateIdentity>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var head = cut.Sources.Single(h => h.Identity == call.Arg<AggregateIdentity>()); readIdentities.Add(head.Identity.ActorId);
            clock.Advance(TimeSpan.FromSeconds(1)); return ReconciliationSource(head, clock);
        });
        var projector = Substitute.For<ISourcePublicationProjector>();
        projector.Project(Arg.Any<AuthoritativeEventStream>(), Arg.Any<CancellationToken>()).Returns(call =>
            ReconciliationPublications(new(call.Arg<AuthoritativeEventStream>().Identity, call.Arg<AuthoritativeEventStream>().Head)));
        var acknowledged = new HashSet<string>(StringComparer.Ordinal); var delivered = new List<string>(); var delivery = Substitute.For<ISourcePublicationDelivery>();
        delivery.LookupAcknowledgementAsync(Arg.Any<SourcePublicationIndexEntry>(), Arg.Any<CancellationToken>()).Returns(call =>
            acknowledged.Contains(call.Arg<SourcePublicationIndexEntry>().Publication.PublicationId) ? SourcePublicationDeliveryStatus.Acknowledged : SourcePublicationDeliveryStatus.Pending);
        delivery.DeliverAsync(Arg.Any<SourcePublicationIndexEntry>(), Arg.Any<CancellationToken>()).Returns(call =>
        { var id = call.Arg<SourcePublicationIndexEntry>().Publication.PublicationId; delivered.Add(id); acknowledged.Add(id); return SourcePublicationDeliveryStatus.Acknowledged; });
        var first = new SourcePublicationDispatcher(new(namespaces, streams, projector, Store(Actor(backend, authority)), clock), clock, delivery);
        var partial = await first.DispatchAsync(Scope, 100, TestContext.Current.CancellationToken);
        partial.IsComplete.ShouldBeFalse(); delivered.ShouldBeEmpty(); readIdentities.Count.ShouldBe(20);
        var saved = backend.CommittedState.Single(); var partialState = saved.Value.ShouldBeOfType<SourcePublicationIndexState>();
        partialState.Entries.ShouldBeEmpty(); partialState.Sources.ShouldBeEmpty(); partialState.ReconciliationProgress!.VerifiedSources.ShouldBe(20);
        var restarted = new InMemoryStateManager(); await restarted.SetStateAsync(saved.Key, JsonSerializer.Deserialize<SourcePublicationIndexState>(JsonSerializer.Serialize(saved.Value))!); await restarted.SaveStateAsync();
        if (appendBetweenPasses) { cut = cut with { Sources = heads.Select((h, i) => i == 0 ? h with { Head = 2 } : h).ToArray(), ObservedAt = clock.GetUtcNow() }; }
        var next = new SourcePublicationDispatcher(new(namespaces, streams, projector, Store(Actor(restarted, authority)), clock), clock, delivery);
        var completed = await next.DispatchAsync(Scope, 100, TestContext.Current.CancellationToken);
        completed.IsComplete.ShouldBeTrue(); delivered.Count.ShouldBe(40); delivered.Distinct(StringComparer.Ordinal).Count().ShouldBe(40);
        readIdentities.Count.ShouldBe(40); readIdentities.Distinct(StringComparer.Ordinal).Count().ShouldBe(40);
        delivered.ShouldContain("publication-source-39"); var final = restarted.CommittedState.Single().Value.ShouldBeOfType<SourcePublicationIndexState>();
        final.Sources.ShouldBe(heads); final.Entries.Select(e => e.Offset).ShouldBe(Enumerable.Range(1, 40).Select(n => (long)n));
        final.ReconciliationProgress!.VerifiedSources.ShouldBe(40);
        if (appendBetweenPasses)
        {
            var originalCheckpoint = new SourcePublicationCheckpoint(Scope, final.Revision, final.AuthorityRevision, final.Sources, 40);
            var originalPage = await new SourcePublicationFeed(namespaces, streams, projector, Store(Actor(restarted, authority)), clock)
                .ReadIndexedPageAsync(originalCheckpoint, 0, TestContext.Current.CancellationToken);
            originalPage.Page!.Checkpoint.Sources.ShouldBe(heads); originalPage.Page.Entries.Count.ShouldBe(40);
            (await next.DispatchAsync(Scope, 100, TestContext.Current.CancellationToken)).IsComplete.ShouldBeFalse();
            delivered.Count.ShouldBe(40);
            (await next.DispatchAsync(Scope, 100, TestContext.Current.CancellationToken)).IsComplete.ShouldBeTrue();
            delivered.Count.ShouldBe(41); delivered.Count(p => p == "append-source-00").ShouldBe(1);
            final = restarted.CommittedState.Single().Value.ShouldBeOfType<SourcePublicationIndexState>();
            final.Sources.ShouldBe(cut.Sources); final.Entries.Last().Offset.ShouldBe(41);
            final.Entries.Last().Publication.PublicationId.ShouldBe("append-source-00");
        }
        (await next.DispatchAsync(Scope, 100, TestContext.Current.CancellationToken)).IsComplete.ShouldBeTrue();
        delivered.Count.ShouldBe(appendBetweenPasses ? 41 : 40); readIdentities.Count.ShouldBe(appendBetweenPasses ? 80 : 40);
    }

    /// <summary>Exact retained partial originals deny changed namespace/cut authority, forged carriers, foreign addressing and stale restored bytes.</summary>
    [Theory]
    [InlineData("scope")][InlineData("installation")][InlineData("authority")][InlineData("cut")][InlineData("head")]
    [InlineData("forged-receipt")][InlineData("forged-publication")][InlineData("stale")]
    public async Task RetainedReconciliationCannotAuthenticateDifferentOrForgedCut(string vector)
    {
        ArgumentNullException.ThrowIfNull(vector);
        var head = new SourcePublicationHead(new(Scope.Tenant, Scope.Domain, "source-00"), 1);
        var nextHead = new SourcePublicationHead(new(Scope.Tenant, Scope.Domain, "source-01"), 1);
        var cut = new SourcePublicationCut(Scope, "qualified-cut", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), [head, nextHead], true);
        var current = cut; var backend = new InMemoryStateManager(); var authority = Operations(); QualifyReconciliation(authority, () => current, TimeProvider.System);
        var store = Store(Actor(backend, authority));
        var first = await store.AdvanceReconciliationProgressAsync(new(cut, 0, head, "independent-source-00", 1, [ReconciliationPublication(head)]), TestContext.Current.CancellationToken);
        first.ShouldNotBeNull(); var saved = backend.CommittedState.Single(); var before = JsonSerializer.Serialize(saved.Value);
        if (vector is "scope" or "installation")
        {
            var foreign = vector == "scope" ? new SourcePublicationScope(Scope.Tenant, Scope.Domain, "foreign", Scope.InstallationId) : new SourcePublicationScope(Scope.Tenant, Scope.Domain, Scope.FeedName, "foreign");
            (await Actor(backend, authority).ReadReconciliationProgressAsync(cut with { Scope = foreign })).ShouldBeNull();
        }
        else if (vector.StartsWith("forged", StringComparison.Ordinal))
        {
            var forged = vector == "forged-receipt" ? first with { ReceiptId = "unknown-proof" } : first with { Publications = [ReconciliationPublication(head) with { StableFieldsDigest = new string('B', 64) }] };
            var state = saved.Value.ShouldBeOfType<SourcePublicationIndexState>();
            (await store.TryWriteAsync(Scope, state.Revision, state with { Revision = state.Revision + 1, ReconciliationProgress = forged }, TestContext.Current.CancellationToken)).ShouldBeFalse();
        }
        else if (vector == "stale")
        {
            (await store.AdvanceReconciliationProgressAsync(new(cut, 1, nextHead, "independent-source-01", 1, [ReconciliationPublication(nextHead)]), TestContext.Current.CancellationToken)).ShouldNotBeNull();
            var latest = backend.CommittedState.Single().Value;
            await backend.SetStateAsync(saved.Key, JsonSerializer.Deserialize<SourcePublicationIndexState>(before)!); await backend.SaveStateAsync();
            await Should.ThrowAsync<InvalidOperationException>(() => store.ReadReconciliationProgressAsync(cut, TestContext.Current.CancellationToken));
            await backend.SetStateAsync(saved.Key, latest); await backend.SaveStateAsync();
            (await store.ReadReconciliationProgressAsync(cut, TestContext.Current.CancellationToken))!.VerifiedSources.ShouldBe(2); return;
        }
        else
        {
            current = vector switch { "authority" => cut with { AuthorityRevision = "changed" }, "cut" => cut with { Sources = [head] }, _ => cut with { Sources = [head with { Head = 2 }, nextHead] } };
            (await store.ReadReconciliationProgressAsync(cut, TestContext.Current.CancellationToken)).ShouldBeNull();
            (await store.ReadReconciliationProgressAsync(current, TestContext.Current.CancellationToken)).ShouldBeNull();
        }
        JsonSerializer.Serialize(backend.CommittedState.Single().Value).ShouldBe(before);
    }

    /// <summary>Provider-owned namespace/index/source/projection/progress collections cannot retain a pass past cancellation/deadline or resume it into a late checkpoint.</summary>
    [Theory]
    [InlineData("cut-count", false)][InlineData("cut-count", true)]
    [InlineData("state-count", false)][InlineData("state-count", true)]
    [InlineData("source-events", false)][InlineData("source-events", true)]
    [InlineData("candidate-count", false)][InlineData("candidate-count", true)]
    [InlineData("progress-count", false)][InlineData("progress-count", true)]
    public async Task SuspendedReconciliationSnapshotsReleaseCallerWithoutLateCheckpoint(string vector, bool cancelCaller)
    {
        ArgumentNullException.ThrowIfNull(vector);
        var clock = new AuthoritativeReadTimeProvider(); clock.Advance(DateTimeOffset.UtcNow - DateTimeOffset.UnixEpoch);
        var head = new SourcePublicationHead(new(Scope.Tenant, Scope.Domain, "source-00"), 1);
        var cut = new SourcePublicationCut(Scope, "qualified-cut", clock.GetUtcNow(), clock.GetUtcNow().AddHours(1), [head], true);
        var backend = new InMemoryStateManager(); var authority = Operations(); QualifyReconciliation(authority, () => cut, clock);
        var actual = Store(Actor(backend, authority));
        if (vector is "state-count" or "progress-count")
        { (await actual.AdvanceReconciliationProgressAsync(new(cut, 0, head, "independent-source-00", 1, [ReconciliationPublication(head)]), TestContext.Current.CancellationToken)).ShouldNotBeNull(); }
        string before = JsonSerializer.Serialize(backend.CommittedState);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); using var release = new ManualResetEventSlim();
        void Suspend() { entered.TrySetResult(); release.Wait(); released.TrySetResult(); }
        var namespaces = Substitute.For<ISourcePublicationNamespaceSource>();
        var suppliedCut = cut with { Sources = vector == "cut-count" ? BlockingList(cut.Sources, Suspend, false) : cut.Sources };
        namespaces.ReadAsync(Scope, Arg.Any<CancellationToken>()).Returns(suppliedCut);
        var store = Substitute.For<ISourcePublicationIndexStore>();
        store.ReadAsync(Scope, Arg.Any<CancellationToken>()).Returns(async call =>
        {
            var state = await actual.ReadAsync(Scope, call.Arg<CancellationToken>());
            return state is not null && vector == "state-count" ? state with { Entries = BlockingList(state.Entries, Suspend, false) } : state;
        });
        store.ReadReconciliationProgressAsync(Arg.Any<SourcePublicationCut>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            var proof = await actual.ReadReconciliationProgressAsync(call.Arg<SourcePublicationCut>(), call.Arg<CancellationToken>());
            return proof is not null && vector == "progress-count" ? proof with { Sources = BlockingList(proof.Sources, Suspend, false) } : proof;
        });
        store.TryWriteAsync(Scope, Arg.Any<long>(), Arg.Any<SourcePublicationIndexState>(), Arg.Any<CancellationToken>()).Returns(call =>
            actual.TryWriteAsync(Scope, call.Arg<long>(), call.Arg<SourcePublicationIndexState>(), call.Arg<CancellationToken>()));
        store.AdvanceReconciliationProgressAsync(Arg.Any<SourcePublicationReconciliationAdvance>(), Arg.Any<CancellationToken>()).Returns(call =>
            actual.AdvanceReconciliationProgressAsync(call.Arg<SourcePublicationReconciliationAdvance>(), call.Arg<CancellationToken>()));
        var streams = Substitute.For<IAuthoritativeEventStreamReader>(); var source = ReconciliationSource(head, clock);
        var suppliedSource = source with { Stream = source.Stream! with {
            Events = vector == "source-events" ? BlockingList(source.Stream!.Events, Suspend, true) : source.Stream!.Events } };
        streams.ReadAsync(head.Identity, Arg.Any<CancellationToken>()).Returns(suppliedSource);
        var projector = Substitute.For<ISourcePublicationProjector>();
        IReadOnlyList<SourcePublicationDescriptor> suppliedCandidates = vector == "candidate-count" ? BlockingList<SourcePublicationDescriptor>([ReconciliationPublication(head)], Suspend, false) : [ReconciliationPublication(head)];
        projector.Project(Arg.Any<AuthoritativeEventStream>(), Arg.Any<CancellationToken>()).Returns(suppliedCandidates);
        using var caller = new CancellationTokenSource(); var feed = new SourcePublicationFeed(namespaces, streams, projector, store, clock);
        var pending = feed.ReadAsync(Scope, cancellationToken: caller.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        try
        {
            if (cancelCaller) { caller.Cancel(); (await Should.ThrowAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken))).CancellationToken.ShouldBe(caller.Token); }
            else { clock.Advance(TimeSpan.FromSeconds(30)); (await pending.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken)).FailureReason.ShouldBe("publication-time-bound-exceeded"); }
            JsonSerializer.Serialize(backend.CommittedState).ShouldBe(before);
        }
        finally { release.Set(); }
        await released.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await store.DidNotReceive().TryWriteAsync(Arg.Any<SourcePublicationScope>(), Arg.Any<long>(), Arg.Any<SourcePublicationIndexState>(), Arg.Any<CancellationToken>());
        await store.DidNotReceive().AdvanceReconciliationProgressAsync(Arg.Any<SourcePublicationReconciliationAdvance>(), Arg.Any<CancellationToken>());
        JsonSerializer.Serialize(backend.CommittedState).ShouldBe(before);
    }
    private static IReadOnlyList<T> BlockingList<T>(IReadOnlyList<T> original, Action suspend, bool traversal)
    {
        var list = Substitute.For<IReadOnlyList<T>>();
        list.Count.Returns(_ => { if (!traversal) { suspend(); } return original.Count; });
        IEnumerable<T> Enumerate() { if (traversal) { suspend(); } foreach (var item in original) { yield return item; } }
        list.GetEnumerator().Returns(_ => Enumerate().GetEnumerator()); return list;
    }

    /// <summary>Withdrawal during the final durable/cut await prevents both full and indexed-page checkpoint release under the existing independent retained-proof port.</summary>
    [Theory]
    [InlineData(false, "state")][InlineData(false, "cut")][InlineData(true, "state")][InlineData(true, "cut")]
    public async Task FinalReconciliationReleaseReconfirmsIndependentSourceProof(bool indexedPage, string vector)
    {
        ArgumentNullException.ThrowIfNull(vector);
        var head = new SourcePublicationHead(new(Scope.Tenant, Scope.Domain, "source-00"), 1);
        var cut = new SourcePublicationCut(Scope, "qualified-cut", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), [head], true);
        bool current = true; var authority = Operations(); QualifyReconciliation(authority, () => cut, TimeProvider.System, () => current);
        var backend = new InMemoryStateManager(); var actual = Store(Actor(backend, authority));
        (await actual.AdvanceReconciliationProgressAsync(new(cut, 0, head, "independent-source-00", 1, [ReconciliationPublication(head)]), TestContext.Current.CancellationToken)).ShouldNotBeNull();
        var initialNamespace = Substitute.For<ISourcePublicationNamespaceSource>(); initialNamespace.ReadAsync(Scope, Arg.Any<CancellationToken>()).Returns(cut);
        var streams = Substitute.For<IAuthoritativeEventStreamReader>(); var projector = Substitute.For<ISourcePublicationProjector>();
        var ready = (await new SourcePublicationFeed(initialNamespace, streams, projector, actual, TimeProvider.System).ReadAsync(Scope, cancellationToken: TestContext.Current.CancellationToken)).Page!.Checkpoint;
        string before = JsonSerializer.Serialize(backend.CommittedState);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = Substitute.For<ISourcePublicationIndexStore>(); int reads = 0;
        store.ReadAsync(Scope, Arg.Any<CancellationToken>()).Returns(async call =>
        {
            var result = await actual.ReadAsync(Scope, call.Arg<CancellationToken>());
            if (++reads == 2 && vector == "state") { entered.TrySetResult(); await release.Task; } return result;
        });
        store.ReadReconciliationProgressAsync(Arg.Any<SourcePublicationCut>(), Arg.Any<CancellationToken>()).Returns(call => actual.ReadReconciliationProgressAsync(call.Arg<SourcePublicationCut>(), call.Arg<CancellationToken>()));
        var namespaces = Substitute.For<ISourcePublicationNamespaceSource>(); int cuts = 0;
        namespaces.ReadAsync(Scope, Arg.Any<CancellationToken>()).Returns(async _ =>
        { if (++cuts == (indexedPage ? 2 : 3) && vector == "cut") { entered.TrySetResult(); await release.Task; } return (SourcePublicationCut?)cut; });
        var feed = new SourcePublicationFeed(namespaces, streams, projector, store, TimeProvider.System);
        var pending = indexedPage ? feed.ReadIndexedPageAsync(ready, 0, TestContext.Current.CancellationToken)
            : feed.ReadAsync(Scope, cancellationToken: TestContext.Current.CancellationToken);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken); current = false; release.TrySetResult();
        var result = await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        result.Page.ShouldBeNull(); result.FailureReason.ShouldBe("publication-reconciliation-proof-unavailable");
        JsonSerializer.Serialize(backend.CommittedState).ShouldBe(before); await streams.DidNotReceive().ReadAsync(Arg.Any<AggregateIdentity>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Independent finite-cut continuation denies foreign installation, missing/regressed originals, forgery, expiry, withdrawal and absent monotonic proof without changing retained durable bytes.</summary>
    [Theory]
    [InlineData("scope")][InlineData("installation")][InlineData("missing")][InlineData("regressed")]
    [InlineData("forged-receipt")][InlineData("forged-publication")][InlineData("expired")][InlineData("withdrawn")][InlineData("unbound")]
    public async Task FiniteCutContinuationDeniesUnprovenPreservation(string vector)
    {
        ArgumentNullException.ThrowIfNull(vector);
        var clock = new AuthoritativeReadTimeProvider(); clock.Advance(DateTimeOffset.UtcNow - DateTimeOffset.UnixEpoch);
        var first = new SourcePublicationHead(new(Scope.Tenant, Scope.Domain, "source-00"), 1);
        var second = new SourcePublicationHead(new(Scope.Tenant, Scope.Domain, "source-01"), 1);
        var original = new SourcePublicationCut(Scope, "qualified-cut", clock.GetUtcNow(), clock.GetUtcNow().AddMinutes(1), [first, second], true);
        var current = original; bool proofCurrent = true; var authority = Operations();
        QualifyReconciliation(authority, () => current, clock, () => proofCurrent, true);
        var backend = new InMemoryStateManager(); var store = Store(Actor(backend, authority));
        var retained = (await store.AdvanceReconciliationProgressAsync(new(original, 0, first, "independent-source-00", 1,
            [ReconciliationPublication(first)]), TestContext.Current.CancellationToken))!;
        retained.OriginalCut.ShouldNotBeNull();
        var saved = backend.CommittedState.Single(); string before = JsonSerializer.Serialize(saved.Value);
        current = original with { Sources = [first with { Head = 2 }, second], ObservedAt = clock.GetUtcNow() };
        if (vector.StartsWith("forged", StringComparison.Ordinal))
        {
            var forged = vector == "forged-receipt" ? retained with { ReceiptId = "unknown-original" }
                : retained with { Publications = [ReconciliationPublication(first) with { StableFieldsDigest = new string('B', 64) }] };
            var state = saved.Value.ShouldBeOfType<SourcePublicationIndexState>();
            (await store.TryWriteAsync(Scope, state.Revision, state with { Revision = state.Revision + 1, ReconciliationProgress = forged },
                TestContext.Current.CancellationToken)).ShouldBeFalse();
        }
        else
        {
            if (vector is "scope" or "installation") { current = current with { Scope = new(Scope.Tenant, Scope.Domain,
                vector == "scope" ? "foreign" : Scope.FeedName, vector == "installation" ? "foreign" : Scope.InstallationId) }; }
            if (vector == "missing") { current = current with { Sources = [first with { Head = 2 }] }; }
            if (vector == "regressed") { current = current with { Sources = [first with { Head = 2 }, second with { Head = 0 }] }; }
            if (vector == "expired") { clock.Advance(TimeSpan.FromMinutes(1)); current = current with { ValidUntil = clock.GetUtcNow().AddHours(1) }; }
            if (vector == "withdrawn") { proofCurrent = false; }
            if (vector == "unbound") { authority.VerifyFiniteCutContinuationAsync(Arg.Any<SourcePublicationReconciliationProgress>(), Arg.Any<SourcePublicationCut>()).Returns(false); }
            (await Actor(backend, authority).ReadReconciliationProgressAsync(current)).ShouldBeNull();
        }
        JsonSerializer.Serialize(backend.CommittedState.Single().Value).ShouldBe(before);
        var restored = new InMemoryStateManager(); await restored.SetStateAsync(saved.Key, JsonSerializer.Deserialize<SourcePublicationIndexState>(before)!);
        await restored.SaveStateAsync(); JsonSerializer.Serialize(restored.CommittedState.Single().Value).ShouldBe(before);
        if (!vector.StartsWith("forged", StringComparison.Ordinal)) { (await Actor(restored, authority).ReadReconciliationProgressAsync(current)).ShouldBeNull(); }
    }

    /// <summary>Withdrawing only the independent finite-cut permission during a suspended final durable/cut read denies both original checkpoint and indexed-page release.</summary>
    [Theory]
    [InlineData(false, "state")][InlineData(false, "cut")][InlineData(true, "state")][InlineData(true, "cut")]
    public async Task FinalFiniteCutReleaseReconfirmsCurrentContinuationPermission(bool indexedPage, string vector)
    {
        ArgumentNullException.ThrowIfNull(vector);
        var first = new SourcePublicationHead(new(Scope.Tenant, Scope.Domain, "source-00"), 1);
        var second = new SourcePublicationHead(new(Scope.Tenant, Scope.Domain, "source-01"), 1);
        var original = new SourcePublicationCut(Scope, "qualified-cut", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), [first, second], true);
        var current = original; var authority = Operations(); QualifyReconciliation(authority, () => current, TimeProvider.System, finiteContinuation: true);
        var backend = new InMemoryStateManager(); var actual = Store(Actor(backend, authority));
        (await actual.AdvanceReconciliationProgressAsync(new(original, 0, first, "independent-source-00", 1,
            [ReconciliationPublication(first)]), TestContext.Current.CancellationToken)).ShouldNotBeNull();
        var namespaces = Substitute.For<ISourcePublicationNamespaceSource>(); namespaces.ReadAsync(Scope, Arg.Any<CancellationToken>()).Returns(_ => current);
        var streams = Substitute.For<IAuthoritativeEventStreamReader>(); streams.ReadAsync(second.Identity, Arg.Any<CancellationToken>()).Returns(_ => ReconciliationSource(second, TimeProvider.System));
        var projector = Substitute.For<ISourcePublicationProjector>(); projector.Project(Arg.Any<AuthoritativeEventStream>(), Arg.Any<CancellationToken>()).Returns([ReconciliationPublication(second)]);
        SourcePublicationCheckpoint? ready = null;
        if (indexedPage) { ready = (await new SourcePublicationFeed(namespaces, streams, projector, actual, TimeProvider.System)
            .ReadAsync(Scope, cancellationToken: TestContext.Current.CancellationToken)).Page!.Checkpoint; }
        current = original with { Sources = [first with { Head = 2 }, second] };
        bool continuationCurrent = true;
        authority.VerifyFiniteCutContinuationAsync(Arg.Any<SourcePublicationReconciliationProgress>(), Arg.Any<SourcePublicationCut>()).Returns(call =>
        {
            var progress = call.Arg<SourcePublicationReconciliationProgress>(); var cut = call.Arg<SourcePublicationCut>();
            return continuationCurrent && progress.CanContinueUnder(cut) && cut.Sources.SequenceEqual(current.Sources);
        });
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = Substitute.For<ISourcePublicationIndexStore>(); int reads = 0; int cuts = 0;
        store.ReadAsync(Scope, Arg.Any<CancellationToken>()).Returns(async call =>
        {
            var result = await actual.ReadAsync(Scope, call.Arg<CancellationToken>());
            if (++reads == (indexedPage ? 2 : 3) && vector == "state") { entered.TrySetResult(); await release.Task; } return result;
        });
        store.ReadReconciliationProgressAsync(Arg.Any<SourcePublicationCut>(), Arg.Any<CancellationToken>()).Returns(call => actual.ReadReconciliationProgressAsync(call.Arg<SourcePublicationCut>(), call.Arg<CancellationToken>()));
        store.AdvanceReconciliationProgressAsync(Arg.Any<SourcePublicationReconciliationAdvance>(), Arg.Any<CancellationToken>()).Returns(call => actual.AdvanceReconciliationProgressAsync(call.Arg<SourcePublicationReconciliationAdvance>(), call.Arg<CancellationToken>()));
        store.TryWriteAsync(Scope, Arg.Any<long>(), Arg.Any<SourcePublicationIndexState>(), Arg.Any<CancellationToken>()).Returns(call => actual.TryWriteAsync(Scope, call.Arg<long>(), call.Arg<SourcePublicationIndexState>(), call.Arg<CancellationToken>()));
        namespaces.ReadAsync(Scope, Arg.Any<CancellationToken>()).Returns(async _ =>
        { if (++cuts == (indexedPage ? 2 : 3) && vector == "cut") { entered.TrySetResult(); await release.Task; } return (SourcePublicationCut?)current; });
        var feed = new SourcePublicationFeed(namespaces, streams, projector, store, TimeProvider.System);
        var pending = indexedPage ? feed.ReadIndexedPageAsync(ready!, 0, TestContext.Current.CancellationToken)
            : feed.ReadAsync(Scope, cancellationToken: TestContext.Current.CancellationToken);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        string before = JsonSerializer.Serialize(backend.CommittedState); continuationCurrent = false; release.TrySetResult();
        var denied = await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        denied.Page.ShouldBeNull(); denied.FailureReason.ShouldBe("publication-reconciliation-proof-unavailable");
        JsonSerializer.Serialize(backend.CommittedState).ShouldBe(before);
    }
}
