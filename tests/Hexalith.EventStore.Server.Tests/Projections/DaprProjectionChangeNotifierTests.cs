using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

namespace Hexalith.EventStore.Server.Tests.Projections;

public class DaprProjectionChangeNotifierTests {
    [Fact]
    public async Task NotifyProjectionChangedAsync_DefaultTransport_PublishesToPubSub() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        ILogger<DaprProjectionChangeNotifier> logger = Substitute.For<ILogger<DaprProjectionChangeNotifier>>();
        IProjectionChangedBroadcaster broadcaster = Substitute.For<IProjectionChangedBroadcaster>();
        IOptions<ProjectionChangeNotifierOptions> options = Options.Create(new ProjectionChangeNotifierOptions());
        var sut = new DaprProjectionChangeNotifier(daprClient, actorProxyFactory, broadcaster, options, logger);

        await sut.NotifyProjectionChangedAsync("order-list", "acme", "order-123");

        await daprClient.Received(1).PublishEventAsync(
            "pubsub",
            "acme.order-list.projection-changed",
            Arg.Is<ProjectionChangedNotification>(n =>
                n.ProjectionType == "order-list"
                && n.TenantId == "acme"
                && n.EntityId == "order-123"),
            Arg.Any<CancellationToken>());

        actorProxyFactory.DidNotReceiveWithAnyArgs().CreateActorProxy<IETagActor>(default!, default!);
    }

    [Fact]
    public async Task NotifyProjectionChangedAsync_DirectTransport_InvokesActorProxy() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        IETagActor actor = Substitute.For<IETagActor>();
        ILogger<DaprProjectionChangeNotifier> logger = Substitute.For<ILogger<DaprProjectionChangeNotifier>>();
        IProjectionChangedBroadcaster broadcaster = Substitute.For<IProjectionChangedBroadcaster>();
        IOptions<ProjectionChangeNotifierOptions> options = Options.Create(
            new ProjectionChangeNotifierOptions { Transport = ProjectionChangeTransport.Direct });
        var sut = new DaprProjectionChangeNotifier(daprClient, actorProxyFactory, broadcaster, options, logger);

        actorProxyFactory.CreateActorProxy<IETagActor>(Arg.Any<ActorId>(), Arg.Is(ETagActor.ETagActorTypeName))
            .Returns(actor);

        await sut.NotifyProjectionChangedAsync("order-list", "acme");

        await actor.Received(1).RegenerateAsync();
        await daprClient.DidNotReceiveWithAnyArgs().PublishEventAsync<object>(default!, default!, default!, default!, default);
    }
}