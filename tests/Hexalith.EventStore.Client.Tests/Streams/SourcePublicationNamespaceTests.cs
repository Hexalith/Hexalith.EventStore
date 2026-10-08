using System.Reflection;
using System.Text.Json;
using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Actors.Runtime;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Streams;
using Hexalith.EventStore.Testing.Fakes;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Streams;

/// <summary>Actual installed-roster persistence and committed-head production under synthetic independent namespace authority.</summary>
public sealed class SourcePublicationNamespaceTests
{
    private static readonly SourcePublicationScope Scope = new("tenant-a", "conversation", "approved-deletion-v1", "installation-1");
    private static readonly AggregateIdentity Existing = new("tenant-a", "conversation", "existing-source");
    private static readonly AggregateIdentity Created = new("tenant-a", "conversation", "new-source");
    private static SourcePublicationNamespaceState Installation() => new(Scope, 1, "installation-authority-1",
        "synthetic-verified-legacy-coverage", "synthetic-verified-all-writers", [Existing]);
    private static SourcePublicationNamespaceActor Actor(IActorStateManager backend)
    {
        var actor = new SourcePublicationNamespaceActor(ActorHost.CreateForTest<SourcePublicationNamespaceActor>(new ActorTestOptions { ActorId = new(Scope.ActorId) }), Operations());
        typeof(Dapr.Actors.Runtime.Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance)!.SetValue(actor, backend); return actor;
    }
    private static ISourcePublicationOperationAuthority Operations()
    {
        var operations = Substitute.For<ISourcePublicationOperationAuthority>();
        operations.ReadNamespaceAsync(Arg.Any<SourcePublicationScope>()).Returns(true);
        operations.InstallNamespaceAsync(Arg.Any<SourcePublicationNamespaceState>()).Returns(true);
        operations.RegisterSourceAsync(Arg.Any<SourcePublicationScope>(), Arg.Any<long>(), Arg.Any<AggregateIdentity>()).Returns(true); return operations;
    }
    private static ISourcePublicationNamespaceAuthority Authority()
    {
        var authority = Substitute.For<ISourcePublicationNamespaceAuthority>();
        var outcome = new SourcePublicationNamespaceAuthorization("current-qualified-installation", DateTimeOffset.UtcNow.AddMinutes(1));
        authority.AuthorizeAsync(Arg.Any<SourcePublicationNamespaceState>(), Arg.Any<CancellationToken>()).Returns(outcome); return authority;
    }
    private static IActorProxyFactory Proxies(ISourcePublicationNamespaceActor actor, IAggregateActor source)
    {
        var proxies = Substitute.For<IActorProxyFactory>();
        proxies.CreateActorProxy<ISourcePublicationNamespaceActor>(new ActorId(Scope.ActorId), SourcePublicationNamespaceActor.ActorTypeName).Returns(actor);
        proxies.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), "AggregateActor").Returns(source); return proxies;
    }

    /// <summary>Installed inventory survives serialized restart; registration precedes a source's first committed event and conflicts do not overwrite it.</summary>
    [Fact]
    public async Task InstallationAndPreCreateRegistrationPersistAcrossRestart()
    {
        var backend = new InMemoryStateManager(); var actor = Actor(backend);
        (await actor.InstallAsync(Installation())).ShouldBeTrue();
        (await actor.InstallAsync(Installation())).ShouldBeTrue();
        (await actor.InstallAsync(Installation() with { LegacyCoverageReceipt = "changed-coverage" })).ShouldBeFalse();
        var proxies = Proxies(actor, Substitute.For<IAggregateActor>());
        await new DaprSourcePublicationWriterRegistration([Scope], proxies, Authority(), TimeProvider.System)
            .RegisterBeforeWriteAsync(Created, TestContext.Current.CancellationToken);
        var saved = backend.CommittedState.Single().Value.ShouldBeOfType<SourcePublicationNamespaceState>();
        saved.Revision.ShouldBe(2); saved.Sources.ShouldContain(Created);
        var restarted = new InMemoryStateManager();
        await restarted.SetStateAsync(backend.CommittedState.Single().Key, JsonSerializer.Deserialize<SourcePublicationNamespaceState>(JsonSerializer.SerializeToUtf8Bytes(saved))!);
        await restarted.SaveStateAsync(); var fresh = Actor(restarted);
        (await fresh.ReadAsync(Scope))!.Sources.ShouldBe(saved.Sources);
        (await fresh.InstallAsync(Installation())).ShouldBeTrue();
        (await fresh.InstallAsync(Installation() with { Sources = [Existing, Created] })).ShouldBeFalse();
        restarted.CommittedState.Single().Value.ShouldBeOfType<SourcePublicationNamespaceState>().InitialSources!.ShouldBe(new[] { Existing });
        (await fresh.RegisterAsync(Scope, 1, Created)).ShouldBeTrue();
        (await fresh.RegisterAsync(Scope, 1, new("tenant-a", "conversation", "competing-new"))).ShouldBeFalse();
        restarted.CommittedState.Single().Value.ShouldBeOfType<SourcePublicationNamespaceState>().Revision.ShouldBe(2);
    }

    /// <summary>Neither precommit failure nor a lost acknowledgement can certify staged inventory.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedInstallationSaveReadsOnlyCommittedInventory(bool commitBeforeFault)
    {
        var backend = new InMemoryStateManager(); var manager = Substitute.For<IActorStateManager>();
        manager.ClearCacheAsync(Arg.Any<CancellationToken>()).Returns(call => backend.ClearCacheAsync(call.Arg<CancellationToken>()));
        manager.TryGetStateAsync<SourcePublicationNamespaceState>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => backend.TryGetStateAsync<SourcePublicationNamespaceState>(call.Arg<string>(), call.Arg<CancellationToken>()));
        manager.SetStateAsync(Arg.Any<string>(), Arg.Any<SourcePublicationNamespaceState>(), Arg.Any<CancellationToken>())
            .Returns(call => backend.SetStateAsync(call.Arg<string>(), call.Arg<SourcePublicationNamespaceState>(), call.Arg<CancellationToken>()));
        manager.SaveStateAsync(Arg.Any<CancellationToken>()).Returns(async call =>
        { if (commitBeforeFault) { await backend.SaveStateAsync(call.Arg<CancellationToken>()); } throw new HttpRequestException("Controlled installation save fault."); });
        var actor = Actor(manager); await Should.ThrowAsync<HttpRequestException>(() => actor.InstallAsync(Installation()));
        if (commitBeforeFault) { backend.CommittedState.Single().Value.ShouldBeOfType<SourcePublicationNamespaceState>().Revision.ShouldBe(1); }
        else { backend.CommittedState.ShouldBeEmpty(); }
        var read = await actor.ReadAsync(Scope); (read is not null).ShouldBe(commitBeforeFault);
        (await Actor(backend).ReadAsync(Scope) is not null).ShouldBe(commitBeforeFault);
    }

    /// <summary>Actual committed metadata produces a finite vector; an unused pre-registration contributes head zero.</summary>
    [Fact]
    public async Task QualifiedRosterUsesCommittedHeadsIncludingUnusedRegistration()
    {
        var backend = new InMemoryStateManager(); var actor = Actor(backend); await actor.InstallAsync(Installation());
        await actor.RegisterAsync(Scope, 1, Created);
        var proxies = Substitute.For<IActorProxyFactory>();
        proxies.CreateActorProxy<ISourcePublicationNamespaceActor>(new ActorId(Scope.ActorId), SourcePublicationNamespaceActor.ActorTypeName).Returns(actor);
        var existing = Substitute.For<IAggregateActor>(); existing.GetStreamMetadataAsync().Returns(new AggregateStreamMetadata(true, 4));
        var empty = Substitute.For<IAggregateActor>(); empty.GetStreamMetadataAsync().Returns(new AggregateStreamMetadata(false, 0));
        proxies.CreateActorProxy<IAggregateActor>(new ActorId(Existing.ActorId), "AggregateActor").Returns(existing);
        proxies.CreateActorProxy<IAggregateActor>(new ActorId(Created.ActorId), "AggregateActor").Returns(empty);
        var cut = await new DaprSourcePublicationNamespaceSource(proxies, Authority(), TimeProvider.System).ReadAsync(Scope, TestContext.Current.CancellationToken);
        cut!.IsComplete.ShouldBeTrue(); cut.Sources.Single(h => h.Identity == Existing).Head.ShouldBe(4);
        cut.Sources.Single(h => h.Identity == Created).Head.ShouldBe(0); cut.AuthorityRevision.ShouldEndWith(":2");
        await existing.Received(2).GetStreamMetadataAsync(); await empty.Received(2).GetStreamMetadataAsync();
    }

    /// <summary>Stored receipt strings alone cannot qualify an installation or expose source metadata.</summary>
    [Fact]
    public async Task UnqualifiedCoverageReturnsNoCutAndBlocksConfiguredWriter()
    {
        var backend = new InMemoryStateManager(); var actor = Actor(backend); await actor.InstallAsync(Installation());
        var source = Substitute.For<IAggregateActor>(); var proxies = Proxies(actor, source);
        var authority = Substitute.For<ISourcePublicationNamespaceAuthority>();
        authority.AuthorizeAsync(Arg.Any<SourcePublicationNamespaceState>(), Arg.Any<CancellationToken>()).Returns((SourcePublicationNamespaceAuthorization?)null);
        (await new DaprSourcePublicationNamespaceSource(proxies, authority, TimeProvider.System).ReadAsync(Scope, TestContext.Current.CancellationToken)).ShouldBeNull();
        await source.DidNotReceive().GetStreamMetadataAsync();
        await Should.ThrowAsync<InvalidOperationException>(() => new DaprSourcePublicationWriterRegistration([Scope], proxies, authority, TimeProvider.System)
            .RegisterBeforeWriteAsync(Created, TestContext.Current.CancellationToken));
        backend.CommittedState.Single().Value.ShouldBeOfType<SourcePublicationNamespaceState>().Sources.ShouldNotContain(Created);
    }

    /// <summary>Roster changes, committed-head changes or authority changes during collection prevent a complete cut.</summary>
    [Theory]
    [InlineData("roster")]
    [InlineData("head")]
    [InlineData("authority")]
    public async Task ConcurrentChangesCannotCertifyFiniteCut(string vector)
    {
        var actor = Substitute.For<ISourcePublicationNamespaceActor>();
        var initial = Installation();
        actor.ReadAsync(Scope).Returns(initial, vector == "roster" ? initial with { Revision = 2, Sources = [Existing, Created], InitialSources = [Existing] } : initial);
        var source = Substitute.For<IAggregateActor>();
        source.GetStreamMetadataAsync().Returns(new AggregateStreamMetadata(true, 4), new AggregateStreamMetadata(true, vector == "head" ? 5 : 4));
        var authority = Authority();
        if (vector == "authority")
        {
            var until = DateTimeOffset.UtcNow.AddMinutes(1);
            authority.AuthorizeAsync(Arg.Any<SourcePublicationNamespaceState>(), Arg.Any<CancellationToken>())
                .Returns(new SourcePublicationNamespaceAuthorization("authority-before", until), new SourcePublicationNamespaceAuthorization("authority-after", until));
        }
        (await new DaprSourcePublicationNamespaceSource(Proxies(actor, source), authority, TimeProvider.System)
            .ReadAsync(Scope, TestContext.Current.CancellationToken)).ShouldBeNull();
    }
    /// <summary>Guessed actor address and stored coverage cannot bypass missing/withdrawn exact private caller/method authorization.</summary>
    [Fact]
    public async Task MissingOrWithdrawnPrivateOperationCredentialDeniesNamespace()
    {
        var backend = new InMemoryStateManager(); var authorized = Actor(backend); await authorized.InstallAsync(Installation());
        var host = ActorHost.CreateForTest<SourcePublicationNamespaceActor>(new ActorTestOptions { ActorId = new(Scope.ActorId) });
        var operations = Operations(); var denied = new SourcePublicationNamespaceActor(host, operations);
        typeof(Dapr.Actors.Runtime.Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance)!.SetValue(denied, backend);
        operations.ReadNamespaceAsync(Scope).Returns(false); (await denied.ReadAsync(Scope)).ShouldBeNull();
        operations.RegisterSourceAsync(Scope, 1, Created).Returns(false); (await denied.RegisterAsync(Scope, 1, Created)).ShouldBeFalse();
        operations.InstallNamespaceAsync(Arg.Any<SourcePublicationNamespaceState>()).Returns(false); (await denied.InstallAsync(Installation())).ShouldBeFalse();
        (await new SourcePublicationNamespaceActor(host).ReadAsync(Scope)).ShouldBeNull();
        var persisted = backend.CommittedState.Single().Value.ShouldBeOfType<SourcePublicationNamespaceState>(); persisted.Revision.ShouldBe(1); persisted.Sources.ShouldNotContain(Created);
    }
}
