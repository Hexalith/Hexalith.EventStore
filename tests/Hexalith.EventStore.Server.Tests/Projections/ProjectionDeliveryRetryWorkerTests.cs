using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using Shouldly;

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
        ProjectionDeliveryRetryWorker worker = CreateWorker(now, scheduler, coordinator, actorProxyFactory, rebuilds);

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
        ProjectionDeliveryRetryWorker worker = CreateWorker(now, scheduler, coordinator, actorProxyFactory, rebuilds);

        await worker.RunOnceAsync(CancellationToken.None);

        _ = await scheduler.Received(1).TryUpdateAsync(
            Arg.Is<ProjectionDeliveryRetryWorkItem>(item =>
                item.WorkId == workItem.WorkId
                && item.Attempt == 1
                && item.NextDueUtc == now.AddSeconds(1)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunOnceAsync_ReconstructsRecordedHistoryAcrossMultiplePages() {
        DateTimeOffset now = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);
        ProjectionDeliveryRetryWorkItem workItem = WorkItem(now) with {
            HeadSequence = 300,
            HeadMessageId = "message-300",
            DispatchId = "message-300",
        };
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
        _ = aggregateActor.ReadEventsRangeAsync(0, 300, 256)
            .Returns([.. Enumerable.Range(1, 256).Select(sequence => Envelope(sequence))]);
        _ = aggregateActor.ReadEventsRangeAsync(256, 300, 256)
            .Returns([.. Enumerable.Range(257, 44).Select(sequence => Envelope(sequence))]);
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        _ = actorProxyFactory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), nameof(AggregateActor))
            .Returns(aggregateActor);
        IProjectionRebuildCheckpointStore rebuilds = Substitute.For<IProjectionRebuildCheckpointStore>();
        _ = rebuilds.HasActiveOperatorRebuildForDomainAsync("tenant-a", "widget", Arg.Any<CancellationToken>()).Returns(false);
        ProjectionDeliveryRetryWorker worker = CreateWorker(now, scheduler, coordinator, actorProxyFactory, rebuilds);

        await worker.RunOnceAsync(CancellationToken.None);

        _ = await coordinator.Received(1).TryDispatchAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<DomainServiceRegistration>(),
            Arg.Is<EventEnvelope[]>(events => events.Length == 300 && events[events.Length - 1].SequenceNumber == 300),
            Arg.Is<Hexalith.EventStore.Contracts.Projections.ProjectionEventDto[]>(events => events.Length == 300),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunOnceAsync_ActiveRebuild_DefersWithoutReadingOrDispatching() {
        DateTimeOffset now = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);
        ProjectionDeliveryRetryWorkItem workItem = WorkItem(now);
        IProjectionDeliveryRetryScheduler scheduler = Substitute.For<IProjectionDeliveryRetryScheduler>();
        _ = scheduler.GetDueAsync(now, ProjectionDispatchOptions.DefaultRetryScanBatchSize, Arg.Any<CancellationToken>()).Returns([workItem]);
        INamedProjectionDispatchCoordinator coordinator = Substitute.For<INamedProjectionDispatchCoordinator>();
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        IProjectionRebuildCheckpointStore rebuilds = Substitute.For<IProjectionRebuildCheckpointStore>();
        _ = rebuilds.HasActiveOperatorRebuildForDomainAsync("tenant-a", "widget", Arg.Any<CancellationToken>()).Returns(true);
        ProjectionDeliveryRetryWorker worker = CreateWorker(now, scheduler, coordinator, actorProxyFactory, rebuilds);

        await worker.RunOnceAsync(CancellationToken.None);

        _ = actorProxyFactory.DidNotReceiveWithAnyArgs().CreateActorProxy<IAggregateActor>(default!, default!);
        _ = await coordinator.DidNotReceiveWithAnyArgs().TryDispatchAsync(default!, default!, default!, default!, default);
        _ = await scheduler.Received(1).TryUpdateAsync(
            Arg.Is<ProjectionDeliveryRetryWorkItem>(item => item.Attempt == 1 && item.NextDueUtc == now.AddSeconds(1)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunOnceAsync_RecordedHeadMismatch_DefersWithoutDispatching() {
        DateTimeOffset now = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);
        ProjectionDeliveryRetryWorkItem workItem = WorkItem(now);
        IProjectionDeliveryRetryScheduler scheduler = Substitute.For<IProjectionDeliveryRetryScheduler>();
        _ = scheduler.GetDueAsync(now, ProjectionDispatchOptions.DefaultRetryScanBatchSize, Arg.Any<CancellationToken>()).Returns([workItem]);
        INamedProjectionDispatchCoordinator coordinator = Substitute.For<INamedProjectionDispatchCoordinator>();
        IAggregateActor aggregateActor = Substitute.For<IAggregateActor>();
        _ = aggregateActor.ReadEventsRangeAsync(0, 2, 256).Returns([Envelope(1), Envelope(2) with { MessageId = "different-head" }]);
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        _ = actorProxyFactory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), nameof(AggregateActor)).Returns(aggregateActor);
        IProjectionRebuildCheckpointStore rebuilds = Substitute.For<IProjectionRebuildCheckpointStore>();
        _ = rebuilds.HasActiveOperatorRebuildForDomainAsync("tenant-a", "widget", Arg.Any<CancellationToken>()).Returns(false);
        ProjectionDeliveryRetryWorker worker = CreateWorker(now, scheduler, coordinator, actorProxyFactory, rebuilds);

        await worker.RunOnceAsync(CancellationToken.None);

        _ = await coordinator.DidNotReceiveWithAnyArgs().TryDispatchAsync(default!, default!, default!, default!, default);
        _ = await scheduler.Received(1).TryUpdateAsync(
            Arg.Is<ProjectionDeliveryRetryWorkItem>(item => item.Attempt == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunOnceAsync_UnreadableHistory_DefersWithoutDispatching() {
        DateTimeOffset now = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);
        ProjectionDeliveryRetryWorkItem workItem = WorkItem(now);
        IProjectionDeliveryRetryScheduler scheduler = Substitute.For<IProjectionDeliveryRetryScheduler>();
        _ = scheduler.GetDueAsync(now, ProjectionDispatchOptions.DefaultRetryScanBatchSize, Arg.Any<CancellationToken>()).Returns([workItem]);
        INamedProjectionDispatchCoordinator coordinator = Substitute.For<INamedProjectionDispatchCoordinator>();
        IDictionary<string, string> extensions = EventStorePayloadProtectionMetadataCarrier.Write(
            (IDictionary<string, string>?)null,
            EventStorePayloadProtectionMetadata.ProviderOpaque("parseError"));
        IAggregateActor aggregateActor = Substitute.For<IAggregateActor>();
        _ = aggregateActor.ReadEventsRangeAsync(0, 2, 256).Returns([
            Envelope(1),
            Envelope(2) with { Extensions = extensions },
        ]);
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        _ = actorProxyFactory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), nameof(AggregateActor)).Returns(aggregateActor);
        IProjectionRebuildCheckpointStore rebuilds = Substitute.For<IProjectionRebuildCheckpointStore>();
        _ = rebuilds.HasActiveOperatorRebuildForDomainAsync("tenant-a", "widget", Arg.Any<CancellationToken>()).Returns(false);
        ProjectionDeliveryRetryWorker worker = CreateWorker(now, scheduler, coordinator, actorProxyFactory, rebuilds);

        await worker.RunOnceAsync(CancellationToken.None);

        _ = await coordinator.DidNotReceiveWithAnyArgs().TryDispatchAsync(default!, default!, default!, default!, default);
        _ = await scheduler.Received(1).TryUpdateAsync(
            Arg.Is<ProjectionDeliveryRetryWorkItem>(item => item.Attempt == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunOnceAsync_HistoryAndLedgerFailures_DoNotEscapeWorkerActivation() {
        DateTimeOffset now = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);
        ProjectionDeliveryRetryWorkItem workItem = WorkItem(now);
        IProjectionDeliveryRetryScheduler scheduler = Substitute.For<IProjectionDeliveryRetryScheduler>();
        _ = scheduler.GetDueAsync(now, ProjectionDispatchOptions.DefaultRetryScanBatchSize, Arg.Any<CancellationToken>()).Returns([workItem]);
        INamedProjectionDispatchCoordinator coordinator = Substitute.For<INamedProjectionDispatchCoordinator>();
        IAggregateActor aggregateActor = Substitute.For<IAggregateActor>();
        _ = aggregateActor.ReadEventsRangeAsync(0, 2, 256)
            .Returns<Task<EventEnvelope[]>>(_ => throw new InvalidOperationException("actor unavailable"));
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        _ = actorProxyFactory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), nameof(AggregateActor)).Returns(aggregateActor);
        IProjectionRebuildCheckpointStore rebuilds = Substitute.For<IProjectionRebuildCheckpointStore>();
        _ = rebuilds.HasActiveOperatorRebuildForDomainAsync("tenant-a", "widget", Arg.Any<CancellationToken>()).Returns(false);
        ProjectionDeliveryRetryWorker worker = CreateWorker(now, scheduler, coordinator, actorProxyFactory, rebuilds);
        _ = scheduler.TryUpdateAsync(Arg.Any<ProjectionDeliveryRetryWorkItem>(), Arg.Any<CancellationToken>())
            .Returns<Task<bool>>(_ => throw new InvalidOperationException("ledger unavailable"));

        await Should.NotThrowAsync(() => worker.RunOnceAsync(CancellationToken.None));

        _ = await scheduler.Received(1).TryUpdateAsync(
            Arg.Is<ProjectionDeliveryRetryWorkItem>(item => item.Attempt == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ActivationFailure_ContinuesOnNextTick() {
        var secondActivation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int activationCount = 0;
        IProjectionDeliveryRetryScheduler scheduler = Substitute.For<IProjectionDeliveryRetryScheduler>();
        _ = scheduler.GetDueAsync(
                Arg.Any<DateTimeOffset>(),
                ProjectionDispatchOptions.DefaultRetryScanBatchSize,
                Arg.Any<CancellationToken>())
            .Returns(_ => {
                if (Interlocked.Increment(ref activationCount) == 1) {
                    throw new InvalidOperationException("state store unavailable");
                }

                secondActivation.TrySetResult();
                return [];
            });
        var options = new ProjectionDispatchOptions { RetryWorkerInterval = TimeSpan.FromMilliseconds(10) };
        var worker = new ProjectionDeliveryRetryWorker(
            scheduler,
            Substitute.For<INamedProjectionDispatchCoordinator>(),
            Substitute.For<IActorProxyFactory>(),
            new NoOpEventPayloadProtectionService(),
            Options.Create(new EventStoreActorOptions()),
            Options.Create(options),
            Substitute.For<IProjectionRebuildCheckpointStore>(),
            TimeProvider.System,
            NullLogger<ProjectionDeliveryRetryWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        try {
            await secondActivation.Task.WaitAsync(TimeSpan.FromSeconds(2));
        }
        finally {
            await worker.StopAsync(CancellationToken.None);
        }

        activationCount.ShouldBeGreaterThanOrEqualTo(2);
    }

    private static ProjectionDeliveryRetryWorker CreateWorker(
        DateTimeOffset now,
        IProjectionDeliveryRetryScheduler scheduler,
        INamedProjectionDispatchCoordinator coordinator,
        IActorProxyFactory actorProxyFactory,
        IProjectionRebuildCheckpointStore rebuilds) {
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
        return new(
            scheduler,
            coordinator,
            actorProxyFactory,
            new NoOpEventPayloadProtectionService(),
            Options.Create(new EventStoreActorOptions()),
            Options.Create(new ProjectionDispatchOptions()),
            rebuilds,
            new FakeTimeProvider(now),
            NullLogger<ProjectionDeliveryRetryWorker>.Instance);
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
