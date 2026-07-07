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

using Shouldly;

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

        _ = actorProxyFactory.DidNotReceiveWithAnyArgs().CreateActorProxy<IETagActor>(default!, default!);
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

        _ = actorProxyFactory.CreateActorProxy<IETagActor>(Arg.Any<ActorId>(), Arg.Is(ETagActor.ETagActorTypeName))
            .Returns(actor);

        await sut.NotifyProjectionChangedAsync("order-list", "acme");

        _ = await actor.Received(1).RegenerateAsync();
        await daprClient.DidNotReceiveWithAnyArgs().PublishEventAsync<object>(default!, default!, default!, default!, default);
    }

    [Fact]
    public async Task NotifyProjectionChangedAsync_DetailPubSubTransport_PublishesExtendedNotification() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        ILogger<DaprProjectionChangeNotifier> logger = Substitute.For<ILogger<DaprProjectionChangeNotifier>>();
        IProjectionChangedBroadcaster broadcaster = Substitute.For<IProjectionChangedBroadcaster>();
        IOptions<ProjectionChangeNotifierOptions> options = Options.Create(new ProjectionChangeNotifierOptions());
        var sut = new DaprProjectionChangeNotifier(daprClient, actorProxyFactory, broadcaster, options, logger);
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal) {
            ["freshness"] = "changed",
        };
        var detail = new ProjectionChangedDetail("order-list", "acme", "order-123", metadata);

        await sut.NotifyProjectionChangedAsync(detail).ConfigureAwait(true);

        await daprClient.Received(1).PublishEventAsync(
            "pubsub",
            "acme.order-list.projection-changed",
            Arg.Is<ProjectionChangedNotification>(n =>
                n.ProjectionType == "order-list"
                && n.TenantId == "acme"
                && n.EntityId == null
                && n.GroupScope == "order-123"
                && n.Metadata != null
                && n.Metadata.Count == 1
                && n.Metadata["freshness"] == "changed"),
            Arg.Any<CancellationToken>()).ConfigureAwait(true);

        _ = actorProxyFactory.DidNotReceiveWithAnyArgs().CreateActorProxy<IETagActor>(default!, default!);
    }

    [Fact]
    public async Task NotifyProjectionChangedAsync_DetailDirectTransport_InvokesActorAndBroadcaster() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        IETagActor actor = Substitute.For<IETagActor>();
        ILogger<DaprProjectionChangeNotifier> logger = Substitute.For<ILogger<DaprProjectionChangeNotifier>>();
        IProjectionChangedBroadcaster broadcaster = Substitute.For<IProjectionChangedBroadcaster>();
        IOptions<ProjectionChangeNotifierOptions> options = Options.Create(
            new ProjectionChangeNotifierOptions { Transport = ProjectionChangeTransport.Direct });
        var sut = new DaprProjectionChangeNotifier(daprClient, actorProxyFactory, broadcaster, options, logger);
        var detail = new ProjectionChangedDetail(
            "order-list",
            "acme",
            "order-123",
            new Dictionary<string, string>(StringComparer.Ordinal));

        _ = actorProxyFactory.CreateActorProxy<IETagActor>(Arg.Any<ActorId>(), Arg.Is(ETagActor.ETagActorTypeName))
            .Returns(actor);

        await sut.NotifyProjectionChangedAsync(detail).ConfigureAwait(true);

        _ = await actor.Received(1).RegenerateAsync().ConfigureAwait(true);
        await broadcaster.Received(1).BroadcastChangedAsync(
            Arg.Is<ProjectionChangedDetail>(d =>
                d.ProjectionType == "order-list"
                && d.TenantId == "acme"
                && d.GroupScope == "order-123"),
            Arg.Any<CancellationToken>()).ConfigureAwait(true);
    }

    [Fact]
    public async Task NotifyProjectionChangedAsync_DetailPubSubTransport_ClipsMetadataBeforePublish() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        ILogger<DaprProjectionChangeNotifier> logger = Substitute.For<ILogger<DaprProjectionChangeNotifier>>();
        IProjectionChangedBroadcaster broadcaster = Substitute.For<IProjectionChangedBroadcaster>();
        IOptions<ProjectionChangeNotifierOptions> options = Options.Create(new ProjectionChangeNotifierOptions {
            MaxDetailMetadataEntries = 1,
            MaxDetailMetadataBytes = 100_000,
        });
        var sut = new DaprProjectionChangeNotifier(daprClient, actorProxyFactory, broadcaster, options, logger);
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal) {
            ["a"] = "1",
            ["b"] = "2",
        };
        var detail = new ProjectionChangedDetail("order-list", "acme", "order-123", metadata);

        await sut.NotifyProjectionChangedAsync(detail).ConfigureAwait(true);

        await daprClient.Received(1).PublishEventAsync(
            "pubsub",
            "acme.order-list.projection-changed",
            Arg.Is<ProjectionChangedNotification>(n =>
                n.Metadata != null
                && n.Metadata.Count == 1
                && n.Metadata.ContainsKey("a")),
            Arg.Any<CancellationToken>()).ConfigureAwait(true);
    }

    [Fact]
    public async Task NotifyProjectionChangedAsync_DetailPubSubTransport_ClipsMetadataByByteLimitBeforePublish() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        ILogger<DaprProjectionChangeNotifier> logger = Substitute.For<ILogger<DaprProjectionChangeNotifier>>();
        IProjectionChangedBroadcaster broadcaster = Substitute.For<IProjectionChangedBroadcaster>();
        IOptions<ProjectionChangeNotifierOptions> options = Options.Create(new ProjectionChangeNotifierOptions {
            MaxDetailMetadataEntries = 16,
            MaxDetailMetadataBytes = 2,
        });
        var sut = new DaprProjectionChangeNotifier(daprClient, actorProxyFactory, broadcaster, options, logger);
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal) {
            ["a"] = "1",
            ["b"] = "2",
        };
        var detail = new ProjectionChangedDetail("order-list", "acme", "order-123", metadata);

        await sut.NotifyProjectionChangedAsync(detail).ConfigureAwait(true);

        await daprClient.Received(1).PublishEventAsync(
            "pubsub",
            "acme.order-list.projection-changed",
            Arg.Is<ProjectionChangedNotification>(n =>
                n.Metadata != null
                && n.Metadata.Count == 1
                && n.Metadata.ContainsKey("a")),
            Arg.Any<CancellationToken>()).ConfigureAwait(true);
    }

    [Fact]
    public async Task NotifyProjectionChangedAsync_DetailPubSubTransport_LongScopeThrowsBeforePublish() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        ILogger<DaprProjectionChangeNotifier> logger = Substitute.For<ILogger<DaprProjectionChangeNotifier>>();
        IProjectionChangedBroadcaster broadcaster = Substitute.For<IProjectionChangedBroadcaster>();
        IOptions<ProjectionChangeNotifierOptions> options = Options.Create(new ProjectionChangeNotifierOptions());
        var sut = new DaprProjectionChangeNotifier(daprClient, actorProxyFactory, broadcaster, options, logger);
        var detail = new ProjectionChangedDetail(
            "order-list",
            "acme",
            new string('a', 65),
            new Dictionary<string, string>(StringComparer.Ordinal));

        _ = await Should.ThrowAsync<ArgumentException>(() =>
            sut.NotifyProjectionChangedAsync(detail)).ConfigureAwait(true);

        await daprClient.DidNotReceiveWithAnyArgs()
            .PublishEventAsync<object>(default!, default!, default!, default!, default).ConfigureAwait(true);
    }
}
