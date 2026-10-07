using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public class DaprProjectionChangeNotifierTests {
    [Fact]
    public async Task NotifyProjectionChangedAsync_PubSubTransport_PublishesToPubSub() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        ILogger<DaprProjectionChangeNotifier> logger = Substitute.For<ILogger<DaprProjectionChangeNotifier>>();
        IProjectionChangedBroadcaster broadcaster = Substitute.For<IProjectionChangedBroadcaster>();
        IOptions<ProjectionChangeNotifierOptions> options = Options.Create(new ProjectionChangeNotifierOptions { Transport = ProjectionChangeTransport.PubSub });
        var sut = new DaprProjectionChangeNotifier(daprClient, actorProxyFactory, broadcaster, options, logger, Issuer());

        await sut.NotifyProjectionChangedAsync("order-list", "acme", "order-123");

        await daprClient.Received(1).PublishEventAsync(
            "pubsub",
            "acme.order-list.projection-changed",
            Arg.Is<ProjectionChangedNotification>(n =>
                n.ProjectionType == "order-list"
                && n.TenantId == "acme"
                && n.EntityId == "order-123"
                && n.Provenance == SignedProvenance),
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
        var sut = new DaprProjectionChangeNotifier(daprClient, actorProxyFactory, broadcaster, options, logger, Issuer());

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
        IOptions<ProjectionChangeNotifierOptions> options = Options.Create(new ProjectionChangeNotifierOptions { Transport = ProjectionChangeTransport.PubSub });
        var sut = new DaprProjectionChangeNotifier(daprClient, actorProxyFactory, broadcaster, options, logger, Issuer());
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
        var sut = new DaprProjectionChangeNotifier(daprClient, actorProxyFactory, broadcaster, options, logger, Issuer());
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
            Transport = ProjectionChangeTransport.PubSub,
            MaxDetailMetadataEntries = 1,
            MaxDetailMetadataBytes = 100_000,
        });
        var sut = new DaprProjectionChangeNotifier(daprClient, actorProxyFactory, broadcaster, options, logger, Issuer());
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
            Transport = ProjectionChangeTransport.PubSub,
            MaxDetailMetadataEntries = 16,
            MaxDetailMetadataBytes = 2,
        });
        var sut = new DaprProjectionChangeNotifier(daprClient, actorProxyFactory, broadcaster, options, logger, Issuer());
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
        IOptions<ProjectionChangeNotifierOptions> options = Options.Create(new ProjectionChangeNotifierOptions { Transport = ProjectionChangeTransport.PubSub });
        var sut = new DaprProjectionChangeNotifier(daprClient, actorProxyFactory, broadcaster, options, logger, Issuer());
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

    /// <summary>
    /// Story 5.5: a pub/sub notification carries signed publisher provenance requested for EventStore's audience,
    /// the projection-notify operation, and the notification's exact tenant, projection type, and topic.
    /// </summary>
    [Fact]
    public async Task NotifyProjectionChangedAsync_PubSubTransport_RequestsProvenanceBoundToTenantAndTopic() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        IWorkloadAssertionIssuer issuer = Issuer();
        var sut = new DaprProjectionChangeNotifier(
            daprClient,
            Substitute.For<IActorProxyFactory>(),
            Substitute.For<IProjectionChangedBroadcaster>(),
            Options.Create(new ProjectionChangeNotifierOptions { Transport = ProjectionChangeTransport.PubSub }),
            Substitute.For<ILogger<DaprProjectionChangeNotifier>>(),
            issuer);

        await sut.NotifyProjectionChangedAsync("order-list", "acme").ConfigureAwait(true);

        _ = await issuer.Received(1).IssueAsync(
            Arg.Is<WorkloadAssertionRequest>(request =>
                request.Audience == ProjectionChangeNotifierOptions.DefaultProvenanceAudience
                && request.Operation == EventStoreWorkloadOperations.ProjectionNotify
                && request.Bindings != null
                && request.Bindings[EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType] == "acme"
                && request.Bindings[EventStoreWorkloadAuthenticationDefaults.ProjectionTypeBindingClaimType] == "order-list"
                && request.Bindings[EventStoreWorkloadAuthenticationDefaults.TopicBindingClaimType] == "acme.order-list.projection-changed"),
            Arg.Any<CancellationToken>()).ConfigureAwait(true);
    }

    /// <summary>
    /// Story 5.5: without provenance nothing is published, because an unproven callback would be denied anyway.
    /// </summary>
    /// <param name="hasIssuer">Whether an issuer is registered (it then returns no assertion).</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NotifyProjectionChangedAsync_PubSubWithoutProvenance_PublishesNothing(bool hasIssuer) {
        DaprClient daprClient = Substitute.For<DaprClient>();
        IWorkloadAssertionIssuer? issuer = null;
        if (hasIssuer) {
            issuer = Substitute.For<IWorkloadAssertionIssuer>();
            _ = issuer.IssueAsync(Arg.Any<WorkloadAssertionRequest>(), Arg.Any<CancellationToken>())
                .Returns(ValueTask.FromResult<string?>(null));
        }

        var sut = new DaprProjectionChangeNotifier(
            daprClient,
            Substitute.For<IActorProxyFactory>(),
            Substitute.For<IProjectionChangedBroadcaster>(),
            Options.Create(new ProjectionChangeNotifierOptions { Transport = ProjectionChangeTransport.PubSub }),
            Substitute.For<ILogger<DaprProjectionChangeNotifier>>(),
            issuer);

        await sut.NotifyProjectionChangedAsync("order-list", "acme").ConfigureAwait(true);
        await sut.NotifyProjectionChangedAsync(new ProjectionChangedDetail(
            "order-list", "acme", null, new Dictionary<string, string>(StringComparer.Ordinal))).ConfigureAwait(true);

        await daprClient.DidNotReceiveWithAnyArgs()
            .PublishEventAsync<object>(default!, default!, default!, default!, default).ConfigureAwait(true);
        await daprClient.DidNotReceiveWithAnyArgs()
            .PublishEventAsync<ProjectionChangedNotification>(default!, default!, default!, default).ConfigureAwait(true);
    }

    private const string SignedProvenance = "signed-provenance";

    private static IWorkloadAssertionIssuer Issuer() {
        IWorkloadAssertionIssuer issuer = Substitute.For<IWorkloadAssertionIssuer>();
        _ = issuer.IssueAsync(Arg.Any<WorkloadAssertionRequest>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<string?>(SignedProvenance));
        return issuer;
    }
}
