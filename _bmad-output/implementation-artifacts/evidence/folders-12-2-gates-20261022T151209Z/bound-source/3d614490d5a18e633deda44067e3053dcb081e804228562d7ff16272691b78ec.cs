using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public class DaprProjectionChangeNotifierSignalRTests {
    [Fact]
    public async Task NotifyProjectionChangedAsync_DirectTransport_CallsBroadcasterAfterRegenerate() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        IETagActor actor = Substitute.For<IETagActor>();
        IProjectionChangedBroadcaster broadcaster = Substitute.For<IProjectionChangedBroadcaster>();
        ILogger<DaprProjectionChangeNotifier> logger = Substitute.For<ILogger<DaprProjectionChangeNotifier>>();
        IOptions<ProjectionChangeNotifierOptions> options = Options.Create(
            new ProjectionChangeNotifierOptions { Transport = ProjectionChangeTransport.Direct });
        var sut = new DaprProjectionChangeNotifier(daprClient, actorProxyFactory, broadcaster, options, logger);

        _ = actorProxyFactory.CreateActorProxy<IETagActor>(Arg.Any<ActorId>(), Arg.Is(ETagActor.ETagActorTypeName))
            .Returns(actor);

        await sut.NotifyProjectionChangedAsync("order-list", "acme");

        await broadcaster.Received(1).BroadcastChangedAsync("order-list", "acme", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyProjectionChangedAsync_PubSubTransport_DoesNotCallBroadcaster() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        IProjectionChangedBroadcaster broadcaster = Substitute.For<IProjectionChangedBroadcaster>();
        ILogger<DaprProjectionChangeNotifier> logger = Substitute.For<ILogger<DaprProjectionChangeNotifier>>();
        IOptions<ProjectionChangeNotifierOptions> options = Options.Create(new ProjectionChangeNotifierOptions { Transport = ProjectionChangeTransport.PubSub });

        // Story 5.5: the pub/sub path publishes only with signed provenance, so the issuer must answer for the test to
        // reach publication instead of returning early.
        IWorkloadAssertionIssuer issuer = Substitute.For<IWorkloadAssertionIssuer>();
        _ = issuer.IssueAsync(Arg.Any<WorkloadAssertionRequest>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<string?>("signed-provenance"));
        var sut = new DaprProjectionChangeNotifier(daprClient, actorProxyFactory, broadcaster, options, logger, issuer);

        await sut.NotifyProjectionChangedAsync("order-list", "acme");

        await daprClient.Received(1).PublishEventAsync(
            options.Value.PubSubName,
            "acme.order-list.projection-changed",
            Arg.Is<Hexalith.EventStore.Contracts.Projections.ProjectionChangedNotification>(notification => notification.Provenance == "signed-provenance"),
            Arg.Any<CancellationToken>());
        await broadcaster.DidNotReceiveWithAnyArgs().BroadcastChangedAsync(default!, default!, default);
        actorProxyFactory.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task NotifyProjectionChangedAsync_DirectTransport_BroadcasterFailure_StillSucceeds() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        IETagActor actor = Substitute.For<IETagActor>();
        IProjectionChangedBroadcaster broadcaster = Substitute.For<IProjectionChangedBroadcaster>();
        ILogger<DaprProjectionChangeNotifier> logger = Substitute.For<ILogger<DaprProjectionChangeNotifier>>();
        IOptions<ProjectionChangeNotifierOptions> options = Options.Create(
            new ProjectionChangeNotifierOptions { Transport = ProjectionChangeTransport.Direct });
        var sut = new DaprProjectionChangeNotifier(daprClient, actorProxyFactory, broadcaster, options, logger);

        _ = actorProxyFactory.CreateActorProxy<IETagActor>(Arg.Any<ActorId>(), Arg.Is(ETagActor.ETagActorTypeName))
            .Returns(actor);

        _ = broadcaster.BroadcastChangedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("SignalR down"));

        // Should not throw — fail-open
        await Should.NotThrowAsync(() =>
            sut.NotifyProjectionChangedAsync("order-list", "acme"));
    }

    [Fact]
    public async Task NotifyProjectionChangedAsync_DirectTransport_RegenerateCompletesBeforeBroadcast() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        IETagActor actor = Substitute.For<IETagActor>();
        IProjectionChangedBroadcaster broadcaster = Substitute.For<IProjectionChangedBroadcaster>();
        ILogger<DaprProjectionChangeNotifier> logger = Substitute.For<ILogger<DaprProjectionChangeNotifier>>();
        IOptions<ProjectionChangeNotifierOptions> options = Options.Create(
            new ProjectionChangeNotifierOptions { Transport = ProjectionChangeTransport.Direct });
        var sut = new DaprProjectionChangeNotifier(daprClient, actorProxyFactory, broadcaster, options, logger);

        _ = actorProxyFactory.CreateActorProxy<IETagActor>(Arg.Any<ActorId>(), Arg.Is(ETagActor.ETagActorTypeName))
            .Returns(actor);

        await sut.NotifyProjectionChangedAsync("order-list", "acme");

        Received.InOrder(() => {
            _ = actor.RegenerateAsync();
            _ = broadcaster.BroadcastChangedAsync("order-list", "acme", Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task NotifyProjectionChangedAsync_DetailDirectTransport_RegenerateCompletesBeforeBroadcast() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        IETagActor actor = Substitute.For<IETagActor>();
        IProjectionChangedBroadcaster broadcaster = Substitute.For<IProjectionChangedBroadcaster>();
        ILogger<DaprProjectionChangeNotifier> logger = Substitute.For<ILogger<DaprProjectionChangeNotifier>>();
        IOptions<ProjectionChangeNotifierOptions> options = Options.Create(
            new ProjectionChangeNotifierOptions { Transport = ProjectionChangeTransport.Direct });
        var sut = new DaprProjectionChangeNotifier(daprClient, actorProxyFactory, broadcaster, options, logger);
        var detail = new ProjectionChangedDetail(
            "order-list",
            "acme",
            "conv-1",
            new Dictionary<string, string>(StringComparer.Ordinal));

        _ = actorProxyFactory.CreateActorProxy<IETagActor>(Arg.Any<ActorId>(), Arg.Is(ETagActor.ETagActorTypeName))
            .Returns(actor);

        await sut.NotifyProjectionChangedAsync(detail).ConfigureAwait(true);

        Received.InOrder(() => {
            _ = actor.RegenerateAsync();
            _ = broadcaster.BroadcastChangedAsync(
                Arg.Is<ProjectionChangedDetail>(d => d.GroupScope == "conv-1"),
                Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task NotifyProjectionChangedAsync_DetailDirectTransport_BroadcasterFailure_StillSucceeds() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        IETagActor actor = Substitute.For<IETagActor>();
        IProjectionChangedBroadcaster broadcaster = Substitute.For<IProjectionChangedBroadcaster>();
        ILogger<DaprProjectionChangeNotifier> logger = Substitute.For<ILogger<DaprProjectionChangeNotifier>>();
        IOptions<ProjectionChangeNotifierOptions> options = Options.Create(
            new ProjectionChangeNotifierOptions { Transport = ProjectionChangeTransport.Direct });
        var sut = new DaprProjectionChangeNotifier(daprClient, actorProxyFactory, broadcaster, options, logger);
        var detail = new ProjectionChangedDetail(
            "order-list",
            "acme",
            "conv-1",
            new Dictionary<string, string>(StringComparer.Ordinal));

        _ = actorProxyFactory.CreateActorProxy<IETagActor>(Arg.Any<ActorId>(), Arg.Is(ETagActor.ETagActorTypeName))
            .Returns(actor);

        _ = broadcaster.BroadcastChangedAsync(Arg.Any<ProjectionChangedDetail>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("SignalR down"));

        await Should.NotThrowAsync(() =>
            sut.NotifyProjectionChangedAsync(detail)).ConfigureAwait(true);
    }

    [Fact]
    public async Task NotifyProjectionChangedAsync_DirectTransport_WithNoOpBroadcaster_RegeneratesETagSuccessfully() {
        // Simulates disabled-state: NoOpProjectionChangedBroadcaster is injected (as when SignalR Enabled=false)
        DaprClient daprClient = Substitute.For<DaprClient>();
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        IETagActor actor = Substitute.For<IETagActor>();
        var broadcaster = new NoOpProjectionChangedBroadcaster();
        ILogger<DaprProjectionChangeNotifier> logger = Substitute.For<ILogger<DaprProjectionChangeNotifier>>();
        IOptions<ProjectionChangeNotifierOptions> options = Options.Create(
            new ProjectionChangeNotifierOptions { Transport = ProjectionChangeTransport.Direct });
        var sut = new DaprProjectionChangeNotifier(daprClient, actorProxyFactory, broadcaster, options, logger);

        _ = actorProxyFactory.CreateActorProxy<IETagActor>(Arg.Any<ActorId>(), Arg.Is(ETagActor.ETagActorTypeName))
            .Returns(actor);

        await sut.NotifyProjectionChangedAsync("order-list", "acme");

        // ETag regeneration must still occur even when broadcast is NoOp
        _ = await actor.Received(1).RegenerateAsync();
    }
}
