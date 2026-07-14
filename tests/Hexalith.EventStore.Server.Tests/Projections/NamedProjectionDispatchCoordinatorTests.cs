using System.Text.Json;

using Dapr.Actors.Client;
using Dapr.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

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
    public async Task TryDispatchAsync_MissingBindingRefreshesFromResolverAlignedRegistration() {
        var catalog = new NamedProjectionRouteCatalog();
        INamedProjectionCatalogRefresher refresher = Substitute.For<INamedProjectionCatalogRefresher>();
        _ = refresher.RefreshAsync(Arg.Any<DomainServiceRegistration>(), Arg.Any<CancellationToken>())
            .Returns(_ => {
                catalog.Upsert(Snapshot("fingerprint", "widget-detail").Entries);
                return true;
            });
        string responseJson = JsonSerializer.Serialize(new ProjectionDispatchResponse(
            ProjectionDispatchProtocol.Version,
            [new ProjectionDispatchOutcome("widget-detail", ProjectionDispatchStatus.Completed, null, null)]));
        var handler = new ProjectionDispatchHttpMessageHandler(responseJson);
        IProjectionDeliveryCheckpointStore checkpoints = Substitute.For<IProjectionDeliveryCheckpointStore>();
        _ = checkpoints.ReadDeliveredSequenceAsync(Identity, "widget-detail", Arg.Any<CancellationToken>()).Returns(0);
        _ = checkpoints.SaveDeliveredSequenceAsync(Identity, "widget-detail", 1, Arg.Any<CancellationToken>()).Returns(true);
        IProjectionLifecycleGateway lifecycle = Substitute.For<IProjectionLifecycleGateway>();
        _ = lifecycle.TryAdmitDeliveryWriteAsync(Identity, "widget-detail", Arg.Any<CancellationToken>()).Returns(true);
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(new HttpClient(handler));
        var coordinator = new NamedProjectionDispatchCoordinator(
            catalog,
            checkpoints,
            lifecycle,
            Substitute.For<IActorProxyFactory>(),
            new DaprClientBuilder().Build(),
            httpClientFactory,
            Options.Create(new ProjectionDispatchOptions()),
            NullLogger<NamedProjectionDispatchCoordinator>.Instance,
            catalogRefresher: refresher);

        bool handled = await coordinator.TryDispatchAsync(
            Identity,
            Registration(),
            [Envelope(1)],
            [],
            CancellationToken.None);

        handled.ShouldBeTrue();
        handler.CallCount.ShouldBe(1);
        _ = await refresher.Received(1).RefreshAsync(
            Arg.Is<DomainServiceRegistration>(registration => registration.AppId == "widget-service"),
            Arg.Any<CancellationToken>());
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

    [Fact]
    public async Task TryDispatchAsync_PersistsWorkBeforeDispatchAndRetainsOnlyPendingRouteWithBackoff() {
        DateTimeOffset now = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(now);
        IProjectionDeliveryRetryScheduler scheduler = Substitute.For<IProjectionDeliveryRetryScheduler>();
        _ = scheduler.ScheduleAsync(Arg.Any<ProjectionDeliveryRetryWorkItem>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<ProjectionDeliveryRetryWorkItem>(0));
        string responseJson = JsonSerializer.Serialize(new ProjectionDispatchResponse(
            ProjectionDispatchProtocol.Version,
            [
                new ProjectionDispatchOutcome("widget-detail", ProjectionDispatchStatus.Completed, null, null),
                new ProjectionDispatchOutcome("widget-index", ProjectionDispatchStatus.Indeterminate, null, ProjectionDispatchReasonCodes.HandlerFailure),
            ]));
        var handler = new ProjectionDispatchHttpMessageHandler(responseJson);
        NamedProjectionDispatchCoordinator coordinator = CreateCoordinator(
            Snapshot("fingerprint", "widget-detail", "widget-index"),
            handler,
            out IProjectionDeliveryCheckpointStore checkpoints,
            out IProjectionLifecycleGateway lifecycle,
            scheduler,
            timeProvider);
        _ = checkpoints.ReadDeliveredSequenceAsync(Identity, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(0);
        _ = checkpoints.SaveDeliveredSequenceAsync(Identity, Arg.Any<string>(), 2, Arg.Any<CancellationToken>()).Returns(true);
        _ = lifecycle.TryAdmitDeliveryWriteAsync(Identity, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        _ = await coordinator.TryDispatchAsync(
            Identity,
            Registration(),
            [Envelope(1), Envelope(2)],
            [],
            CancellationToken.None);

        _ = await scheduler.Received(1).ScheduleAsync(
            Arg.Is<ProjectionDeliveryRetryWorkItem>(item =>
                item.HeadSequence == 2
                && item.HeadMessageId == "message-2"
                && item.DispatchId == "message-2"
                && item.PendingRoutes.SequenceEqual(new[] { "widget-detail", "widget-index" })),
            Arg.Any<CancellationToken>());
        scheduler.ReceivedCalls().First().GetMethodInfo().Name.ShouldBe(nameof(IProjectionDeliveryRetryScheduler.ScheduleAsync));
        _ = await scheduler.Received(1).TryUpdateAsync(
            Arg.Is<ProjectionDeliveryRetryWorkItem>(item =>
                item.PendingRoutes.SequenceEqual(new[] { "widget-index" })
                && item.TerminalRoutes.Count == 0
                && item.Attempt == 1
                && item.NextDueUtc == now.AddSeconds(1)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TryDispatchAsync_TransportFailureRetainsPendingRoutesWithBackoff() {
        DateTimeOffset now = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(now);
        IProjectionDeliveryRetryScheduler scheduler = Substitute.For<IProjectionDeliveryRetryScheduler>();
        _ = scheduler.ScheduleAsync(Arg.Any<ProjectionDeliveryRetryWorkItem>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<ProjectionDeliveryRetryWorkItem>(0));
        var handler = new ProjectionDispatchHttpMessageHandler("{}", System.Net.HttpStatusCode.ServiceUnavailable);
        NamedProjectionDispatchCoordinator coordinator = CreateCoordinator(
            Snapshot("fingerprint", "widget-detail"),
            handler,
            out IProjectionDeliveryCheckpointStore checkpoints,
            out IProjectionLifecycleGateway lifecycle,
            scheduler,
            timeProvider);
        _ = checkpoints.ReadDeliveredSequenceAsync(Identity, "widget-detail", Arg.Any<CancellationToken>()).Returns(0);
        _ = lifecycle.TryAdmitDeliveryWriteAsync(Identity, "widget-detail", Arg.Any<CancellationToken>()).Returns(true);

        _ = await coordinator.TryDispatchAsync(
            Identity,
            Registration(),
            [Envelope(1), Envelope(2)],
            [],
            CancellationToken.None);

        _ = await scheduler.Received(1).TryUpdateAsync(
            Arg.Is<ProjectionDeliveryRetryWorkItem>(item =>
                item.PendingRoutes.SequenceEqual(new[] { "widget-detail" })
                && item.Attempt == 1
                && item.NextDueUtc == now.AddSeconds(1)
                && item.LastReasonCode == ProjectionDispatchReasonCodes.PartialRetry),
            Arg.Any<CancellationToken>());
        _ = await scheduler.DidNotReceiveWithAnyArgs().TryDeleteAsync(default!, default);
    }

    [Fact]
    public async Task TryDispatchAsync_KnownTerminalOutcomeRemainsOperatorVisibleWithoutRetry() {
        DateTimeOffset now = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);
        IProjectionDeliveryRetryScheduler scheduler = Substitute.For<IProjectionDeliveryRetryScheduler>();
        _ = scheduler.ScheduleAsync(Arg.Any<ProjectionDeliveryRetryWorkItem>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<ProjectionDeliveryRetryWorkItem>(0));
        string responseJson = JsonSerializer.Serialize(new ProjectionDispatchResponse(
            ProjectionDispatchProtocol.Version,
            [new ProjectionDispatchOutcome(
                "widget-detail",
                ProjectionDispatchStatus.Failed,
                null,
                ProjectionDispatchReasonCodes.HandlerFailure)]));
        NamedProjectionDispatchCoordinator coordinator = CreateCoordinator(
            Snapshot("fingerprint", "widget-detail"),
            new ProjectionDispatchHttpMessageHandler(responseJson),
            out IProjectionDeliveryCheckpointStore checkpoints,
            out IProjectionLifecycleGateway lifecycle,
            scheduler,
            new FakeTimeProvider(now));
        _ = checkpoints.ReadDeliveredSequenceAsync(Identity, "widget-detail", Arg.Any<CancellationToken>()).Returns(0);
        _ = lifecycle.TryAdmitDeliveryWriteAsync(Identity, "widget-detail", Arg.Any<CancellationToken>()).Returns(true);

        _ = await coordinator.TryDispatchAsync(
            Identity,
            Registration(),
            [Envelope(1), Envelope(2)],
            [],
            CancellationToken.None);

        _ = await scheduler.Received(1).TryUpdateAsync(
            Arg.Is<ProjectionDeliveryRetryWorkItem>(item =>
                item.PendingRoutes.Count == 0
                && item.TerminalRoutes.SequenceEqual(new[] { "widget-detail" })
                && item.LastReasonCode == ProjectionDispatchReasonCodes.HandlerFailure),
            Arg.Any<CancellationToken>());
        _ = await scheduler.DidNotReceiveWithAnyArgs().TryDeleteAsync(default!, default);
        _ = await checkpoints.DidNotReceiveWithAnyArgs().SaveDeliveredSequenceAsync(default!, default!, default, default);
    }

    [Fact]
    public async Task TryDispatchAsync_ExhaustedAttemptRemainsPendingAtBoundedBackoff() {
        DateTimeOffset now = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);
        IProjectionDeliveryRetryScheduler scheduler = Substitute.For<IProjectionDeliveryRetryScheduler>();
        _ = scheduler.ScheduleAsync(Arg.Any<ProjectionDeliveryRetryWorkItem>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<ProjectionDeliveryRetryWorkItem>(0) with { Attempt = 8 });
        string responseJson = JsonSerializer.Serialize(new ProjectionDispatchResponse(
            ProjectionDispatchProtocol.Version,
            [new ProjectionDispatchOutcome(
                "widget-detail",
                ProjectionDispatchStatus.Retryable,
                null,
                ProjectionDispatchReasonCodes.PartialRetry)]));
        NamedProjectionDispatchCoordinator coordinator = CreateCoordinator(
            Snapshot("fingerprint", "widget-detail"),
            new ProjectionDispatchHttpMessageHandler(responseJson),
            out IProjectionDeliveryCheckpointStore checkpoints,
            out IProjectionLifecycleGateway lifecycle,
            scheduler,
            new FakeTimeProvider(now));
        _ = checkpoints.ReadDeliveredSequenceAsync(Identity, "widget-detail", Arg.Any<CancellationToken>()).Returns(0);
        _ = lifecycle.TryAdmitDeliveryWriteAsync(Identity, "widget-detail", Arg.Any<CancellationToken>()).Returns(true);

        _ = await coordinator.TryDispatchAsync(
            Identity,
            Registration(),
            [Envelope(1), Envelope(2)],
            [],
            CancellationToken.None);

        _ = await scheduler.Received(1).TryUpdateAsync(
            Arg.Is<ProjectionDeliveryRetryWorkItem>(item =>
                item.PendingRoutes.SequenceEqual(new[] { "widget-detail" })
                && item.Attempt == 8
                && item.NextDueUtc == now.AddSeconds(128)),
            Arg.Any<CancellationToken>());
        _ = await scheduler.DidNotReceiveWithAnyArgs().TryDeleteAsync(default!, default);
    }

    [Fact]
    public async Task TryDispatchAsync_StateBearingSuccessPreservesAdmissionWriteEtagCheckpointOrder() {
        JsonElement state = JsonSerializer.SerializeToElement(new { value = 42 });
        string responseJson = JsonSerializer.Serialize(new ProjectionDispatchResponse(
            ProjectionDispatchProtocol.Version,
            [new ProjectionDispatchOutcome(
                "widget-detail",
                ProjectionDispatchStatus.Completed,
                state,
                null)]));
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        IProjectionWriteActor writeActor = Substitute.For<IProjectionWriteActor>();
        IETagActor eTagActor = Substitute.For<IETagActor>();
        _ = actorProxyFactory.CreateActorProxy<IProjectionWriteActor>(Arg.Any<Dapr.Actors.ActorId>(), Arg.Any<string>())
            .Returns(writeActor);
        _ = actorProxyFactory.CreateActorProxy<IETagActor>(Arg.Any<Dapr.Actors.ActorId>(), Arg.Any<string>())
            .Returns(eTagActor);
        _ = eTagActor.RegenerateAsync().Returns("etag-1");
        NamedProjectionDispatchCoordinator coordinator = CreateCoordinator(
            Snapshot("fingerprint", "widget-detail"),
            new ProjectionDispatchHttpMessageHandler(responseJson),
            out IProjectionDeliveryCheckpointStore checkpoints,
            out IProjectionLifecycleGateway lifecycle,
            actorProxyFactory: actorProxyFactory);
        _ = checkpoints.ReadDeliveredSequenceAsync(Identity, "widget-detail", Arg.Any<CancellationToken>()).Returns(0);
        _ = checkpoints.SaveDeliveredSequenceAsync(Identity, "widget-detail", 2, Arg.Any<CancellationToken>()).Returns(true);
        _ = lifecycle.TryAdmitDeliveryWriteAsync(Identity, "widget-detail", Arg.Any<CancellationToken>()).Returns(true);

        _ = await coordinator.TryDispatchAsync(
            Identity,
            Registration(),
            [Envelope(1), Envelope(2)],
            [],
            CancellationToken.None);

        Received.InOrder(() => {
            _ = lifecycle.TryAdmitDeliveryWriteAsync(Identity, "widget-detail", Arg.Any<CancellationToken>());
            _ = writeActor.UpdateProjectionAsync(Arg.Any<ProjectionState>());
            _ = eTagActor.RegenerateAsync();
            _ = checkpoints.SaveDeliveredSequenceAsync(Identity, "widget-detail", 2, Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task TryDispatchAsync_InvalidOutcomeSetsFailClosedWithoutAdvancingCheckpoint() {
        ProjectionDispatchResponse[] invalidResponses = [
            new ProjectionDispatchResponse(
                ProjectionDispatchProtocol.Version,
                [
                    new ProjectionDispatchOutcome("widget-detail", ProjectionDispatchStatus.Completed, null, null),
                    new ProjectionDispatchOutcome("widget-detail", ProjectionDispatchStatus.Completed, null, null),
                ]),
            new ProjectionDispatchResponse(
                ProjectionDispatchProtocol.Version,
                [new ProjectionDispatchOutcome("widget-detail", (ProjectionDispatchStatus)999, null, null)]),
            new ProjectionDispatchResponse(
                ProjectionDispatchProtocol.Version,
                [new ProjectionDispatchOutcome("widget-detail", ProjectionDispatchStatus.Retryable, null, new string('r', 129))]),
            new ProjectionDispatchResponse(
                ProjectionDispatchProtocol.Version,
                [.. Enumerable.Range(0, 33).Select(_ =>
                    new ProjectionDispatchOutcome("widget-detail", ProjectionDispatchStatus.Completed, null, null))]),
        ];

        foreach (ProjectionDispatchResponse invalidResponse in invalidResponses) {
            NamedProjectionDispatchCoordinator coordinator = CreateCoordinator(
                Snapshot("fingerprint", "widget-detail"),
                new ProjectionDispatchHttpMessageHandler(JsonSerializer.Serialize(invalidResponse)),
                out IProjectionDeliveryCheckpointStore checkpoints,
                out IProjectionLifecycleGateway lifecycle);
            _ = checkpoints.ReadDeliveredSequenceAsync(Identity, "widget-detail", Arg.Any<CancellationToken>()).Returns(0);
            _ = lifecycle.TryAdmitDeliveryWriteAsync(Identity, "widget-detail", Arg.Any<CancellationToken>()).Returns(true);

            _ = await coordinator.TryDispatchAsync(
                Identity,
                Registration(),
                [Envelope(1)],
                [],
                CancellationToken.None);

            _ = await checkpoints.DidNotReceiveWithAnyArgs().SaveDeliveredSequenceAsync(default!, default!, default, default);
        }
    }

    private static NamedProjectionDispatchCoordinator CreateCoordinator(
        NamedProjectionRouteCatalogSnapshot snapshot,
        ProjectionDispatchHttpMessageHandler handler,
        out IProjectionDeliveryCheckpointStore checkpoints,
        out IProjectionLifecycleGateway lifecycle,
        IProjectionDeliveryRetryScheduler? scheduler = null,
        TimeProvider? timeProvider = null,
        IActorProxyFactory? actorProxyFactory = null) {
        var catalog = new NamedProjectionRouteCatalog();
        catalog.Replace(snapshot);
        checkpoints = Substitute.For<IProjectionDeliveryCheckpointStore>();
        lifecycle = Substitute.For<IProjectionLifecycleGateway>();
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(new HttpClient(handler));
        if (scheduler is not null) {
            _ = scheduler.TryAcquireAsync(
                    Arg.Any<ProjectionDeliveryRetryWorkItem>(),
                    Arg.Any<string>(),
                    Arg.Any<DateTimeOffset>(),
                    Arg.Any<TimeSpan>(),
                    Arg.Any<CancellationToken>())
                .Returns(call => {
                    ProjectionDeliveryRetryWorkItem item = call.ArgAt<ProjectionDeliveryRetryWorkItem>(0);
                    return item with {
                        Revision = item.Revision + 1,
                        LeaseOwner = call.ArgAt<string>(1),
                        LeaseExpiresUtc = call.ArgAt<DateTimeOffset>(2) + call.ArgAt<TimeSpan>(3),
                    };
                });
            _ = scheduler.TryUpdateAsync(Arg.Any<ProjectionDeliveryRetryWorkItem>(), Arg.Any<CancellationToken>())
                .Returns(true);
            _ = scheduler.TryDeleteAsync(Arg.Any<ProjectionDeliveryRetryWorkItem>(), Arg.Any<CancellationToken>())
                .Returns(true);
        }

        return new NamedProjectionDispatchCoordinator(
            catalog,
            checkpoints,
            lifecycle,
            actorProxyFactory ?? Substitute.For<IActorProxyFactory>(),
            new DaprClientBuilder().Build(),
            httpClientFactory,
            Options.Create(new ProjectionDispatchOptions()),
            NullLogger<NamedProjectionDispatchCoordinator>.Instance,
            scheduler,
            timeProvider);
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
