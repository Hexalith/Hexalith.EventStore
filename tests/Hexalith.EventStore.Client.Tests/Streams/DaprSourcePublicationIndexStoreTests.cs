using System.Reflection;
using System.Text.Json;
using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Actors.Runtime;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Server.Streams;
using Hexalith.EventStore.Testing.Fakes;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Streams;

/// <summary>Actual DAPR index actor/storage transport with the standard committed-state test provider; no live backend qualification.</summary>
public sealed class DaprSourcePublicationIndexStoreTests
{
    private static readonly SourcePublicationScope Scope = new("tenant-a", "conversation", "approved-deletion-v1", "installation-1");
    private static SourcePublicationIndexState State(long revision = 1, string? poison = null)
    {
        var source = new AggregateIdentity("tenant-a", "conversation", "source-a");
        return new(Scope, revision, "authority-1", [new(source, 2)], [new(1, new("signal-1", source, 2, "event-2", new string('A', 64)))], poison);
    }
    private static SourcePublicationIndexActor Actor(IActorStateManager state)
    {
        var actor = new SourcePublicationIndexActor(ActorHost.CreateForTest<SourcePublicationIndexActor>(new ActorTestOptions { ActorId = new(Scope.ActorId) }), Operations());
        typeof(Dapr.Actors.Runtime.Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance)!.SetValue(actor, state);
        return actor;
    }
    private static ISourcePublicationOperationAuthority Operations()
    {
        var operations = Substitute.For<ISourcePublicationOperationAuthority>(); operations.ReadIndexAsync(Arg.Any<SourcePublicationScope>()).Returns(true);
        operations.WriteIndexAsync(Arg.Any<SourcePublicationIndexWrite>()).Returns(true); return operations;
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
        var fresh = Store(Actor(restored)); var read = await fresh.ReadAsync(Scope, TestContext.Current.CancellationToken);
        read!.Sources.Single().Head.ShouldBe(2); read.Entries.Single().ShouldBe(saved.Entries.Single());
        (await fresh.TryWriteAsync(Scope, 0, State(), TestContext.Current.CancellationToken)).ShouldBeFalse();
        restored.CommittedState.Single().Value.ShouldBeOfType<SourcePublicationIndexState>().Revision.ShouldBe(1);
    }

    /// <summary>A failed save cannot make its staged cache authoritative; an unknown committed outcome is read from persisted storage.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedSaveCannotReleaseStagedState(bool committedBeforeFault)
    {
        var backend = new InMemoryStateManager();
        var transport = Substitute.For<IActorStateManager>();
        transport.ClearCacheAsync(Arg.Any<CancellationToken>()).Returns(call => backend.ClearCacheAsync(call.Arg<CancellationToken>()));
        transport.TryGetStateAsync<SourcePublicationIndexState>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => backend.TryGetStateAsync<SourcePublicationIndexState>(call.Arg<string>(), call.Arg<CancellationToken>()));
        transport.SetStateAsync(Arg.Any<string>(), Arg.Any<SourcePublicationIndexState>(), Arg.Any<CancellationToken>())
            .Returns(call => backend.SetStateAsync(call.Arg<string>(), call.Arg<SourcePublicationIndexState>(), call.Arg<CancellationToken>()));
        transport.SaveStateAsync(Arg.Any<CancellationToken>()).Returns(async call =>
        {
            if (committedBeforeFault) { await backend.SaveStateAsync(call.Arg<CancellationToken>()); }
            throw new HttpRequestException("Controlled failed save acknowledgement.");
        });
        var actor = Actor(transport);
        await Should.ThrowAsync<HttpRequestException>(() => actor.TryWriteAsync(new(0, State())));
        // The actual staged provider contains the candidate even when its committed dictionary is empty.
        if (committedBeforeFault) { backend.CommittedState.Single().Value.ShouldBeOfType<SourcePublicationIndexState>().Revision.ShouldBe(1); }
        else { backend.CommittedState.ShouldBeEmpty(); }
        SourcePublicationIndexState? durable = await actor.ReadAsync(Scope);
        if (committedBeforeFault) { durable!.Entries.Single().Publication.PublicationId.ShouldBe("signal-1"); }
        else { durable.ShouldBeNull(); backend.CommittedState.ShouldBeEmpty(); }
        // Restart independently sees the same committed outcome, never an uncommitted candidate.
        SourcePublicationIndexState? restarted = await Actor(backend).ReadAsync(Scope);
        (restarted?.Revision).ShouldBe(durable?.Revision);
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
        var operations = Operations(); var restricted = new SourcePublicationIndexActor(host, operations);
        typeof(Dapr.Actors.Runtime.Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance)!.SetValue(restricted, backend);
        operations.ReadIndexAsync(Scope).Returns(false); (await restricted.ReadAsync(Scope)).ShouldBeNull();
        operations.WriteIndexAsync(Arg.Any<SourcePublicationIndexWrite>()).Returns(false); (await restricted.TryWriteAsync(new(1, State(2)))).ShouldBeFalse();
        (await new SourcePublicationIndexActor(host).ReadAsync(Scope)).ShouldBeNull();
        backend.CommittedState.Single().Value.ShouldBeOfType<SourcePublicationIndexState>().Revision.ShouldBe(1);
        (await actor.ReadAsync(Scope))!.Entries.Single().Publication.PublicationId.ShouldBe("signal-1");
    }
}
