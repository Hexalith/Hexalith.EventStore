using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

namespace Hexalith.EventStore.Server.Tests.Projections;

public sealed class ProjectionDeliveryRetryWorkerTests {
    [Fact]
    public async Task RunOnceAsync_ReloadsOnlyThroughRecordedHeadAndPreservesStableBinding() {
        DateTimeOffset now = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);
        ProjectionDeliveryRetryWorkItem workItem = WorkItem(now);
        IProjectionDeliveryRetryScheduler scheduler = Substitute.For<IProjectionDeliveryRetryScheduler>();
        _ = scheduler.GetDueAsync(now, ProjectionDispatchOptions.DefaultRetryScanBatchSize, Arg.Any<CancellationToken>())
            .Returns([workItem]);
        INamedProjectionDispatchCoordinator coordinator = Substitute.For<INamedProjectionDispatchCoordinator>();
        _ = coordinator.TryDispatchAsync(
                Arg.Any<AggregateIdentity>(),
                Arg.Any<DomainServiceRegistration>(),
                Arg.Any<EventEnvelope[]>(),
                Arg.Any<Hexalith.EventStore.Contracts.Projections.ProjectionEventDto[]>(),
                Arg.Any<CancellationToken>())
            .Returns(true);
        IAggregateActor aggregateActor = Substitute.For<IAggregateActor>();
        _ = aggregateActor.ReadEventsRangeAsync(0, 2, 256)
            .Returns([Envelope(1), Envelope(2)]);
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        _ = actorProxyFactory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), nameof(AggregateActor))
            .Returns(aggregateActor);
        IProjectionRebuildCheckpointStore rebuilds = Substitute.For<IProjectionRebuildCheckpointStore>();
        _ = rebuilds.HasActiveOperatorRebuildForDomainAsync("tenant-a", "widget", Arg.Any<CancellationToken>())
            .Returns(false);
        var worker = new ProjectionDeliveryRetryWorker(
            scheduler,
            coordinator,
            actorProxyFactory,
            new NoOpEventPayloadProtectionService(),
            Options.Create(new EventStoreActorOptions()),
            Options.Create(new ProjectionDispatchOptions()),
            rebuilds,
            new FakeTimeProvider(now),
            NullLogger<ProjectionDeliveryRetryWorker>.Instance);

        await worker.RunOnceAsync(CancellationToken.None);

        _ = await aggregateActor.Received(1).ReadEventsRangeAsync(0, 2, 256);
        _ = await coordinator.Received(1).TryDispatchAsync(
            Arg.Is<AggregateIdentity>(identity => identity.TenantId == "tenant-a"
                && identity.Domain == "widget"
                && identity.AggregateId == "widget-1"),
            Arg.Is<DomainServiceRegistration>(registration => registration.AppId == "widget-service"
                && registration.Version == "v1"),
            Arg.Is<EventEnvelope[]>(events => events.Length == 2
                && events[events.Length - 1].SequenceNumber == 2
                && events[events.Length - 1].MessageId == "message-2"),
            Arg.Is<Hexalith.EventStore.Contracts.Projections.ProjectionEventDto[]>(events => events.Length == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunOnceAsync_MissingExactCatalogBindingDefersDurableWorkWithBackoff() {
        DateTimeOffset now = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);
        ProjectionDeliveryRetryWorkItem workItem = WorkItem(now);
        IProjectionDeliveryRetryScheduler scheduler = Substitute.For<IProjectionDeliveryRetryScheduler>();
        _ = scheduler.GetDueAsync(now, ProjectionDispatchOptions.DefaultRetryScanBatchSize, Arg.Any<CancellationToken>()).Returns([workItem]);
        INamedProjectionDispatchCoordinator coordinator = Substitute.For<INamedProjectionDispatchCoordinator>();
        _ = coordinator.TryDispatchAsync(
                Arg.Any<AggregateIdentity>(),
                Arg.Any<DomainServiceRegistration>(),
                Arg.Any<EventEnvelope[]>(),
                Arg.Any<Hexalith.EventStore.Contracts.Projections.ProjectionEventDto[]>(),
                Arg.Any<CancellationToken>())
            .Returns(false);
        IAggregateActor aggregateActor = Substitute.For<IAggregateActor>();
        _ = aggregateActor.ReadEventsRangeAsync(0, 2, 256).Returns([Envelope(1), Envelope(2)]);
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        _ = actorProxyFactory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), nameof(AggregateActor))
            .Returns(aggregateActor);
        IProjectionRebuildCheckpointStore rebuilds = Substitute.For<IProjectionRebuildCheckpointStore>();
        _ = rebuilds.HasActiveOperatorRebuildForDomainAsync("tenant-a", "widget", Arg.Any<CancellationToken>())
            .Returns(false);
        var worker = new ProjectionDeliveryRetryWorker(
            scheduler,
            coordinator,
            actorProxyFactory,
            new NoOpEventPayloadProtectionService(),
            Options.Create(new EventStoreActorOptions()),
            Options.Create(new ProjectionDispatchOptions()),
            rebuilds,
            new FakeTimeProvider(now),
            NullLogger<ProjectionDeliveryRetryWorker>.Instance);

        await worker.RunOnceAsync(CancellationToken.None);

        await scheduler.Received(1).UpdateAsync(
            Arg.Is<ProjectionDeliveryRetryWorkItem>(item =>
                item.WorkId == workItem.WorkId
                && item.Attempt == 1
                && item.NextDueUtc == now.AddSeconds(1)),
            Arg.Any<CancellationToken>());
    }

    private static ProjectionDeliveryRetryWorkItem WorkItem(DateTimeOffset dueUtc)
        => new(
            ProjectionDeliveryRetryWorkItem.CreateWorkId("tenant-a", "widget", "widget-1", 2),
            "tenant-a",
            "widget",
            "widget-1",
            "widget-service",
            "v1",
            2,
            "message-2",
            ["widget-detail"],
            [],
            "message-2",
            "fingerprint",
            0,
            dueUtc,
            null);

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
