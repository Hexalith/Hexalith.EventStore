using System.Text.Json;

using Dapr.Actors.Client;
using Dapr.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public sealed class NamedProjectionDispatchCoordinatorTests {
    private static readonly AggregateIdentity Identity = new("tenant-a", "widget", "widget-1");

    [Fact]
    public async Task TryDispatchAsync_ReturnsFalseWhenExactCatalogBindingIsMissing() {
        ProjectionDispatchHttpMessageHandler handler = new("{}");
        NamedProjectionDispatchCoordinator coordinator = CreateCoordinator(
            NamedProjectionRouteCatalogSnapshot.Empty,
            handler,
            out _,
            out _);

        bool handled = await coordinator.TryDispatchAsync(
            Identity,
            Registration(),
            [Envelope(1)],
            [],
            CancellationToken.None);

        handled.ShouldBeFalse();
        handler.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task TryDispatchAsync_UsesStableHeadIdentityAndAdvancesOnlySuccessfulProjection() {
        const string fingerprint = "catalog-fingerprint";
        string responseJson = JsonSerializer.Serialize(new ProjectionDispatchResponse(
            ProjectionDispatchProtocol.Version,
            [
                new ProjectionDispatchOutcome("widget-detail", ProjectionDispatchStatus.Completed, null, null),
                new ProjectionDispatchOutcome("widget-index", ProjectionDispatchStatus.Retryable, null, ProjectionDispatchReasonCodes.PartialRetry),
            ]));
        var handler = new ProjectionDispatchHttpMessageHandler(responseJson);
        NamedProjectionRouteCatalogSnapshot snapshot = Snapshot(fingerprint, "widget-index", "widget-detail");
        NamedProjectionDispatchCoordinator coordinator = CreateCoordinator(
            snapshot,
            handler,
            out IProjectionDeliveryCheckpointStore checkpoints,
            out IProjectionLifecycleGateway lifecycle);
        _ = checkpoints.ReadDeliveredSequenceAsync(Identity, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(0);
        _ = checkpoints.SaveDeliveredSequenceAsync(Identity, Arg.Any<string>(), 2, Arg.Any<CancellationToken>()).Returns(true);
        _ = lifecycle.TryAdmitDeliveryWriteAsync(Identity, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        bool handled = await coordinator.TryDispatchAsync(
            Identity,
            Registration(),
            [Envelope(1), Envelope(2)],
            [],
            CancellationToken.None);

        handled.ShouldBeTrue();
        handler.CallCount.ShouldBe(1);
        ProjectionDispatchRequest request = JsonSerializer.Deserialize<ProjectionDispatchRequest>(
            handler.RequestJson!,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)).ShouldNotBeNull();
        request.DispatchId.ShouldBe("message-2");
        request.CatalogFingerprint.ShouldBe(fingerprint);
        request.ProjectionTypes.ShouldBe(["widget-detail", "widget-index"]);
        _ = await checkpoints.Received(1).SaveDeliveredSequenceAsync(
            Identity,
            "widget-detail",
            2,
            Arg.Any<CancellationToken>());
        _ = await checkpoints.DidNotReceive().SaveDeliveredSequenceAsync(
            Identity,
            "widget-index",
            Arg.Any<long>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TryDispatchAsync_SkipsEndpointWhenEveryRouteIsDeniedBeforePersistence() {
        var handler = new ProjectionDispatchHttpMessageHandler("{}");
        NamedProjectionDispatchCoordinator coordinator = CreateCoordinator(
            Snapshot("fingerprint", "widget-detail", "widget-index"),
            handler,
            out IProjectionDeliveryCheckpointStore checkpoints,
            out IProjectionLifecycleGateway lifecycle);
        _ = checkpoints.ReadDeliveredSequenceAsync(Identity, "widget-detail", Arg.Any<CancellationToken>()).Returns(3);
        _ = checkpoints.ReadDeliveredSequenceAsync(Identity, "widget-index", Arg.Any<CancellationToken>()).Returns(0);
        _ = lifecycle.TryAdmitDeliveryWriteAsync(Identity, "widget-index", Arg.Any<CancellationToken>()).Returns(false);

        bool handled = await coordinator.TryDispatchAsync(
            Identity,
            Registration(),
            [Envelope(1), Envelope(2)],
            [],
            CancellationToken.None);

        handled.ShouldBeTrue();
        handler.CallCount.ShouldBe(0);
    }

    private static NamedProjectionDispatchCoordinator CreateCoordinator(
        NamedProjectionRouteCatalogSnapshot snapshot,
        ProjectionDispatchHttpMessageHandler handler,
        out IProjectionDeliveryCheckpointStore checkpoints,
        out IProjectionLifecycleGateway lifecycle) {
        var catalog = new NamedProjectionRouteCatalog();
        catalog.Replace(snapshot);
        checkpoints = Substitute.For<IProjectionDeliveryCheckpointStore>();
        lifecycle = Substitute.For<IProjectionLifecycleGateway>();
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(new HttpClient(handler));
        return new NamedProjectionDispatchCoordinator(
            catalog,
            checkpoints,
            lifecycle,
            Substitute.For<IActorProxyFactory>(),
            new DaprClientBuilder().Build(),
            httpClientFactory,
            Options.Create(new ProjectionDispatchOptions()),
            NullLogger<NamedProjectionDispatchCoordinator>.Instance);
    }

    private static NamedProjectionRouteCatalogSnapshot Snapshot(string fingerprint, params string[] projectionTypes)
        => new([
            new NamedProjectionRouteCatalogEntry(
                "widget-service",
                "v1",
                "widget",
                ProjectionDispatchProtocol.Version,
                ProjectionDispatchProtocol.Capability,
                fingerprint,
                projectionTypes),
        ]);

    private static DomainServiceRegistration Registration()
        => new("widget-service", "process", "tenant-a", "widget", "v1");

    private static EventEnvelope Envelope(long sequence)
        => new(
            $"message-{sequence}",
            "widget-1",
            "widget",
            "tenant-a",
            "widget",
            sequence,
            sequence,
            DateTimeOffset.UnixEpoch,
            "correlation",
            "causation",
            "user",
            "v1",
            "widget-updated",
            1,
            "json",
            [],
            null);
}
