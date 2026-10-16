using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

public sealed class DomainFencedSharedProjectionIntegrationTests
{
    private const string StoreName = "statestore";
    private const string IndexKey = "widget:shared-index";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GuardEnrolledSharedDispatch_RequiresFoldReceiptAndCompleteDeclaredWriterCoverageBeforeCompletion(bool publishedCatalog)
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var handler = new FencedIndexHandler { RequireOtherWriter = true };
        var capability = new ProjectionSourceGuardCapability(1, 1, 1, "shared-epoch-v1", "projection-owner-proof/v1",
            "source-sequence-position/v1", "shared-applied/v1");
        var route = new ProjectionDispatchRoute(handler.Domain, handler.ProjectionType) { SourceGuard = capability };
        string fingerprint = ProjectionRouteCatalogFingerprint.Compute("widget-service", "v1", [route]);
        var registry = new DomainProjectionCatalogRegistry();
        registry.Register(fingerprint, [route]);
        var services = new ServiceCollection();
        _ = services.AddSingleton<IReadModelStore>(store);
        _ = services.AddSingleton<IReadModelBatchStore>(store);
        _ = services.AddSingleton(coordinator);
        if (publishedCatalog) _ = services.AddSingleton(registry);
        _ = services.AddSingleton<IAsyncDomainProjectionHandler>(handler);
        _ = services.AddSingleton(new DomainProjectionOwnerProofRegistration(handler.Domain, handler.ProjectionType,
            capability, new(StoreName, "unused")));
        using ServiceProvider provider = services.BuildServiceProvider();
        var dispatch = new ProjectionDispatchRequest(new("tenant-a", handler.Domain, "owner", [Event(1, 1)]),
            [handler.ProjectionType], "same-work", fingerprint);
        ProjectionDispatchOutcome incomplete = (await DomainProjectionDispatcher.DispatchAsync(provider, dispatch,
            new ProjectionDispatchOptions(), IdentityOptions(), CancellationToken.None)).Outcomes.Single();
        incomplete.Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        incomplete.ReasonCode.ShouldBe("delivery_in_progress");
        SharedProjectionScope scope = handler.CreateScope("tenant-a");
        (await coordinator.ReadAsync<Index>(scope, IndexKey)).Value.ShouldNotBeNull().Count.ShouldBe(1);
        _ = await coordinator.RegisterWriterAsync(scope, "other-writer");
        ProjectionDispatchOutcome completed = (await DomainProjectionDispatcher.DispatchAsync(provider, dispatch,
            new ProjectionDispatchOptions(), IdentityOptions(), CancellationToken.None)).Outcomes.Single();
        completed.Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        (await coordinator.ReadAsync<Index>(scope, IndexKey)).Value.ShouldNotBeNull().Count.ShouldBe(1);
    }

    [Fact]
    public async Task AppliedProof_JournalAcceptanceCannotProveFoldAndInspectionPerformsNoRepairs()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var scope = new SharedProjectionScope(StoreName, "tenant-a", "widget", "shared-proof", ["writer-a"]);
        SharedProjectionLease lease = await coordinator.RegisterWriterAsync(scope, "writer-a");
        var request = new ProjectionRequest("tenant-a", "widget", "owner", [Event(1, 1)]);
        var delivery = new SharedProjectionDelivery("owner", 1,
            JsonSerializer.SerializeToUtf8Bytes(request, JsonSerializerOptions.Web), "1"u8.ToArray(), OwnerSequenceNumber: 1);
        (await coordinator.JournalAsync(scope, lease, delivery)).ShouldBe(SharedProjectionJournalResult.Journaled);
        (await coordinator.GetCheckpointAsync(scope, "owner"))!.OwnerSequenceNumber.ShouldBe(1);
        (await coordinator.ReadAppliedDeliveryProofAsync(scope, delivery)).IsApplied.ShouldBeFalse();
        _ = await coordinator.CatchUpAsync(scope, (_, _, _) => Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>(
            [ReadModelBatchOperation.Write(IndexKey, new Index(1), ReadModelBatchConcurrency.LastWrite)]));
        SharedProjectionReadResult<Index> model = await coordinator.ReadAsync<Index>(scope, IndexKey);
        int keys = store.Count;
        int mutations = 0;
        store.ConcurrentWriteBeforeTrySave = () => mutations++;
        store.ConcurrentWriteBeforeTryErase = () => mutations++;
        store.BatchFaultHook = (_, _, _) => { mutations++; return Task.CompletedTask; };
        SharedProjectionAppliedDeliveryProof proof = await coordinator.ReadAppliedDeliveryProofAsync(scope, delivery);
        proof.IsApplied.ShouldBeTrue();
        proof.Generation.ShouldBe(model.Generation);
        proof.ReceiptIdentity.ShouldNotBeNullOrWhiteSpace();
        proof.ReceiptFingerprint.ShouldNotBeNullOrWhiteSpace();
        mutations.ShouldBe(0);
        store.Count.ShouldBe(keys);
        (await coordinator.ReadAsync<Index>(scope, IndexKey)).SelectedETag.ShouldBe(model.SelectedETag);
        (await coordinator.ReadAppliedDeliveryProofAsync(scope, delivery with { CanonicalPayload = "changed"u8.ToArray() }))
            .ReasonCode.ShouldBe("delivery_identity_conflict");
    }

    [Fact]
    public async Task AppliedProof_RequiresEveryDeclaredWriterEvenAfterOneOwnerWasFolded()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var scope = new SharedProjectionScope(StoreName, "tenant-a", "widget", "shared-proof", ["writer-a", "writer-b"]);
        SharedProjectionLease lease = await coordinator.RegisterWriterAsync(scope, "writer-a");
        var delivery = new SharedProjectionDelivery("owner", 1, "payload"u8.ToArray(), "1"u8.ToArray(), OwnerSequenceNumber: 1);
        _ = await coordinator.JournalAsync(scope, lease, delivery);
        _ = await coordinator.CatchUpAsync(scope, (_, _, _) => Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>(
            [ReadModelBatchOperation.Write(IndexKey, new Index(1), ReadModelBatchConcurrency.LastWrite)]));
        (await coordinator.ReadAppliedDeliveryProofAsync(scope, delivery)).IsApplied.ShouldBeFalse();
        _ = await coordinator.RegisterWriterAsync(scope, "writer-b");
        (await coordinator.ReadAppliedDeliveryProofAsync(scope, delivery)).IsApplied.ShouldBeTrue();
    }

    [Fact]
    public async Task GlobalEpochDeliverySeparatesIdenticalOwnerIdsAcrossTenants()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var handler = new FencedIndexHandler { GlobalScope = true };
        var services = new ServiceCollection();
        _ = services.AddSingleton<IReadModelStore>(store);
        _ = services.AddSingleton<IReadModelBatchStore>(store);
        _ = services.AddSingleton(coordinator);
        _ = services.AddScoped<IAsyncDomainProjectionHandler>(_ => handler);
        using ServiceProvider provider = services.BuildServiceProvider();
        foreach (string tenant in new[] { "tenant-a", "tenant-b" })
        {
            ProjectionDispatchResponse response = await DomainProjectionDispatcher.DispatchAsync(
                provider,
                new ProjectionDispatchRequest(new ProjectionRequest(tenant, "widget", "same-owner", [Event(1, 1)]),
                    [handler.ProjectionType], "delivery-" + tenant, CatalogFingerprint()),
                new ProjectionDispatchOptions(), IdentityOptions(), CancellationToken.None);
            response.Outcomes.Single().Status.ShouldBe(ProjectionDispatchStatus.Completed);
        }

        SharedProjectionScope scope = handler.CreateScope("tenant-a");
        scope.ComputeHash().ShouldBe(handler.CreateScope("tenant-b").ComputeHash());
        foreach (string tenant in new[] { "tenant-a", "tenant-b" })
        {
            string sourceKey = ((IAsyncDomainSharedProjectionEpochHandler)handler)
                .SourceStreamKey(tenant, "same-owner");
            sourceKey.ShouldBe(new AggregateIdentity(tenant, "widget", "same-owner").ActorId);
            (await coordinator.GetCheckpointAsync(scope, sourceKey))!.OwnerSequenceNumber.ShouldBe(1);
        }
        (await coordinator.ReadAsync<Index>(scope, IndexKey)).Value!.Count.ShouldBe(2);

        var identity = new DomainSharedProjectionRebuildIdentity("tenant-a", "widget",
            handler.ProjectionType, "global-tenant-a-rebuild", CatalogFingerprint());
        string capture = DomainSharedProjectionRebuildFingerprint.AppendInventory(
            DomainSharedProjectionRebuildFingerprint.EmptyInventory, 0,
            DomainSharedProjectionRebuildFingerprint.ComputeHistory("same-owner", false, [Event(1, 1)]));
        DomainSharedProjectionRebuildResponse begun = await RebuildAsync(provider,
            new DomainSharedProjectionRebuildRequest(DomainSharedProjectionRebuildProtocol.Version,
                DomainSharedProjectionRebuildAction.Begin, identity,
                CaptureInventoryFingerprint: capture,
                SourceHighWatermarks: new Dictionary<string, long> { ["same-owner"] = 1 }));
        begun.Phase.ShouldBe(DomainSharedProjectionRebuildPhase.Accumulating);
        DomainSharedProjectionRebuildResponse accumulated = await RebuildAsync(provider,
            new DomainSharedProjectionRebuildRequest(DomainSharedProjectionRebuildProtocol.Version,
                DomainSharedProjectionRebuildAction.Accumulate, identity,
                AggregateOrdinal: 0, AggregateId: "same-owner", Events: [Event(1, 1)]));
        accumulated.AcceptedAggregateCount.ShouldBe(1);
        _ = await RebuildAsync(provider, Lifecycle(identity, DomainSharedProjectionRebuildAction.Abort));
        (await coordinator.ReadAsync<Index>(scope, IndexKey)).Value!.Count.ShouldBe(2);
    }

    [Fact]
    public async Task NamedDeliveryDuringSharedRebuild_JournalsThenConvergesAfterAtomicPromotion()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var handler = new FencedIndexHandler();
        var services = new ServiceCollection();
        _ = services.AddSingleton<IReadModelStore>(store);
        _ = services.AddSingleton<IReadModelBatchStore>(store);
        _ = services.AddSingleton(coordinator);
        _ = services.AddScoped<IAsyncDomainProjectionHandler>(_ => handler);
        using ServiceProvider provider = services.BuildServiceProvider();

        ProjectionEventDto first = Event(1, 1);
        ProjectionEventDto second = Event(2, 2);
        DomainProjectionIdentityOptions identityOptions = IdentityOptions();
        ProjectionDispatchOptions options = new();
        string catalog = CatalogFingerprint();
        ProjectionDispatchResponse initial = await DomainProjectionDispatcher.DispatchAsync(
            provider,
            new ProjectionDispatchRequest(
                new ProjectionRequest("tenant-a", "widget", "aggregate-a", [first]),
                ["fenced-widget-index"],
                "delivery-1",
                catalog),
            options,
            identityOptions,
            CancellationToken.None);
        initial.Outcomes.Single().Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await coordinator.GetControlIndexTenantsAsync(StoreName, "widget-discovery"))
            .ShouldBe(["tenant-a"]);

        var identity = new DomainSharedProjectionRebuildIdentity(
            "tenant-a",
            "widget",
            "fenced-widget-index",
            "rebuild-1",
            catalog);
        string capture = DomainSharedProjectionRebuildFingerprint.AppendInventory(
            DomainSharedProjectionRebuildFingerprint.EmptyInventory,
            0,
            DomainSharedProjectionRebuildFingerprint.ComputeHistory("aggregate-a", false, [first]));
        DomainSharedProjectionRebuildResponse current = await RebuildAsync(
            provider,
            new DomainSharedProjectionRebuildRequest(
                DomainSharedProjectionRebuildProtocol.Version,
                DomainSharedProjectionRebuildAction.Begin,
                identity,
                CaptureInventoryFingerprint: capture,
                SourceHighWatermarks: new Dictionary<string, long> { ["aggregate-a"] = 1 }));
        current.Phase.ShouldBe(DomainSharedProjectionRebuildPhase.Accumulating);

        ProjectionDispatchResponse concurrent = await DomainProjectionDispatcher.DispatchAsync(
            provider,
            new ProjectionDispatchRequest(
                new ProjectionRequest("tenant-a", "widget", "aggregate-a", [second]),
                ["fenced-widget-index"],
                "delivery-2",
                catalog),
            options,
            identityOptions,
            CancellationToken.None);
        concurrent.Outcomes.Single().Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        (await coordinator.GetCheckpointAsync(handler.CreateScope("tenant-a"), "aggregate-a"))!
            .Position.ShouldBe(1);
        SharedProjectionScope scope = handler.CreateScope("tenant-a");
        SharedProjectionReadResult<Index> old = await coordinator.ReadAsync<Index>(scope, IndexKey);
        old.Generation.ShouldBe(0);
        old.Value!.Count.ShouldBe(1);
        old.IsStale.ShouldBeTrue();

        current = await RebuildAsync(provider, new DomainSharedProjectionRebuildRequest(
            DomainSharedProjectionRebuildProtocol.Version,
            DomainSharedProjectionRebuildAction.Accumulate,
            identity,
            AggregateOrdinal: 0,
            AggregateId: "aggregate-a",
            Events: [first]));
        current = await RebuildAsync(provider, new DomainSharedProjectionRebuildRequest(
            DomainSharedProjectionRebuildProtocol.Version,
            DomainSharedProjectionRebuildAction.Finalize,
            identity,
            ExpectedAggregateCount: current.AcceptedAggregateCount,
            ExpectedInventoryFingerprint: current.InventoryFingerprint));
        current.Phase.ShouldBe(DomainSharedProjectionRebuildPhase.Finalized);
        current = await RebuildAsync(provider, Lifecycle(identity, DomainSharedProjectionRebuildAction.Stage));
        current.Phase.ShouldBe(DomainSharedProjectionRebuildPhase.Prepared);
        (await coordinator.ReadAsync<Index>(scope, IndexKey)).Value!.Count.ShouldBe(1);

        current = await RebuildAsync(provider, Lifecycle(identity, DomainSharedProjectionRebuildAction.Commit));
        current.Phase.ShouldBe(DomainSharedProjectionRebuildPhase.Committed);
        current.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        SharedProjectionReadResult<Index> promoted = await coordinator.ReadAsync<Index>(scope, IndexKey);
        promoted.Generation.ShouldBe(1);
        promoted.IsStale.ShouldBeFalse();
        promoted.Value!.Count.ShouldBe(1);

        ProjectionDispatchResponse changedCaptured = await DomainProjectionDispatcher.DispatchAsync(
            provider,
            new ProjectionDispatchRequest(
                new ProjectionRequest("tenant-a", "widget", "aggregate-a",
                    [first with { MessageId = "changed-captured-message" }]),
                ["fenced-widget-index"],
                "changed-captured-delivery",
                catalog),
            options,
            identityOptions,
            CancellationToken.None);
        changedCaptured.Outcomes.Single().Status.ShouldBe(ProjectionDispatchStatus.Failed);
        changedCaptured.Outcomes.Single().ReasonCode.ShouldBe(
            ProjectionDispatchReasonCodes.DeliveryIdentityConflict);

        ProjectionDispatchResponse deliveredAfterPromotion = await DomainProjectionDispatcher.DispatchAsync(
            provider,
            new ProjectionDispatchRequest(
                new ProjectionRequest("tenant-a", "widget", "aggregate-a", [first, second]),
                ["fenced-widget-index"],
                "delivery-2",
                catalog),
            options,
            identityOptions,
            CancellationToken.None);
        deliveredAfterPromotion.Outcomes.Single().Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await coordinator.ReadAsync<Index>(scope, IndexKey)).Value!.Count.ShouldBe(2);
        (await coordinator.GetCheckpointAsync(scope, "aggregate-a"))!.Position.ShouldBe(2);

        DomainSharedProjectionRebuildResponse replay = await RebuildAsync(
            provider,
            Lifecycle(identity, DomainSharedProjectionRebuildAction.Commit));
        replay.Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        (await coordinator.ReadAsync<Index>(scope, IndexKey)).Value!.Count.ShouldBe(2);
    }

    [Fact]
    public async Task MultiEnvelopeDelivery_JournalsEveryPositionAndRejectsConflictingRedelivery()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var handler = new FencedIndexHandler();
        var services = new ServiceCollection();
        _ = services.AddSingleton<IReadModelStore>(store);
        _ = services.AddSingleton<IReadModelBatchStore>(store);
        _ = services.AddSingleton(coordinator);
        _ = services.AddScoped<IAsyncDomainProjectionHandler>(_ => handler);
        using ServiceProvider provider = services.BuildServiceProvider();
        ProjectionEventDto[] events = [Event(1, 1), Event(2, 2), Event(3, 3)];
        ProjectionDispatchRequest delivery = new(
            new ProjectionRequest("tenant-a", "widget", "aggregate-a", events),
            ["fenced-widget-index"],
            "delivery-1",
            CatalogFingerprint());

        ProjectionDispatchResponse first = await DomainProjectionDispatcher.DispatchAsync(
            provider, delivery, new ProjectionDispatchOptions(), IdentityOptions(), CancellationToken.None);
        first.Outcomes.Single().Status.ShouldBe(ProjectionDispatchStatus.Completed);
        SharedProjectionScope scope = handler.CreateScope("tenant-a");
        (await coordinator.ReadAsync<Index>(scope, IndexKey)).Value!.Count.ShouldBe(3);
        (await coordinator.GetCheckpointAsync(scope, "aggregate-a"))!.Position.ShouldBe(3);

        ProjectionDispatchResponse duplicate = await DomainProjectionDispatcher.DispatchAsync(
            provider, delivery, new ProjectionDispatchOptions(), IdentityOptions(), CancellationToken.None);
        duplicate.Outcomes.Single().Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        (await coordinator.ReadAsync<Index>(scope, IndexKey)).Value!.Count.ShouldBe(3);

        ProjectionEventDto changed = Event(2, 2) with { Payload = JsonSerializer.SerializeToUtf8Bytes(new { sequence = 999 }) };
        ProjectionDispatchResponse conflicting = await DomainProjectionDispatcher.DispatchAsync(
            provider,
            delivery with { Request = delivery.Request with { Events = [events[0], changed, events[2]] } },
            new ProjectionDispatchOptions(),
            IdentityOptions(),
            CancellationToken.None);
        conflicting.Outcomes.Single().Status.ShouldBe(ProjectionDispatchStatus.Failed);
        (await coordinator.ReadAsync<Index>(scope, IndexKey)).Value!.Count.ShouldBe(3);
    }

    [Fact]
    public async Task MultiEnvelopeDelivery_StopsAtFirstBackpressureWithoutJournalingLaterPositions()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var handler = new FencedIndexHandler();
        var services = new ServiceCollection();
        _ = services.AddSingleton<IReadModelStore>(store);
        _ = services.AddSingleton<IReadModelBatchStore>(store);
        _ = services.AddSingleton(coordinator);
        _ = services.AddScoped<IAsyncDomainProjectionHandler>(_ => handler);
        using ServiceProvider provider = services.BuildServiceProvider();
        SharedProjectionScope scope = handler.CreateScope("tenant-a");
        SharedProjectionLease lease = await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId);
        _ = await coordinator.BeginAsync(scope, "rebuild", "inventory", new Dictionary<string, long>());
        lease = await coordinator.RefreshLeaseAsync(scope, handler.OrdinaryWriterId);
        for (long position = 1; position <= 127; position++)
        {
            (await coordinator.JournalAsync(scope, lease, new SharedProjectionDelivery(
                "aggregate-a", position, [(byte)position], BitConverter.GetBytes(position))))
                .ShouldBe(SharedProjectionJournalResult.Journaled);
        }

        ProjectionDispatchResponse response = await DomainProjectionDispatcher.DispatchAsync(
            provider,
            new ProjectionDispatchRequest(
                new ProjectionRequest("tenant-a", "widget", "aggregate-a", [Event(128, 128), Event(129, 129), Event(130, 130)]),
                ["fenced-widget-index"],
                "delivery-130",
                CatalogFingerprint()),
            new ProjectionDispatchOptions(), IdentityOptions(), CancellationToken.None);

        response.Outcomes.Single().Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        (await coordinator.GetStatusAsync(scope)).PendingDeliveryCount.ShouldBe(127);
        (await coordinator.GetCheckpointAsync(scope, "aggregate-a"))!.Position.ShouldBe(127);
    }

    [Fact]
    public async Task MultiEnvelopeDelivery_RejectsSequenceGapBeforeAnyJournalWrite()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var handler = new FencedIndexHandler();
        var services = new ServiceCollection();
        _ = services.AddSingleton<IReadModelStore>(store);
        _ = services.AddSingleton<IReadModelBatchStore>(store);
        _ = services.AddSingleton(coordinator);
        _ = services.AddScoped<IAsyncDomainProjectionHandler>(_ => handler);
        using ServiceProvider provider = services.BuildServiceProvider();

        ProjectionDispatchResponse response = await DomainProjectionDispatcher.DispatchAsync(
            provider,
            new ProjectionDispatchRequest(
                new ProjectionRequest("tenant-a", "widget", "aggregate-a", [Event(1, 1), Event(3, 3)]),
                ["fenced-widget-index"],
                "delivery-3",
                CatalogFingerprint()),
            new ProjectionDispatchOptions(), IdentityOptions(), CancellationToken.None);

        response.Outcomes.Single().Status.ShouldBe(ProjectionDispatchStatus.Failed);
        (await store.GetAsync<object>(StoreName,
            "shared-epoch:" + handler.CreateScope("tenant-a").ComputeHash())).Value.ShouldBeNull();
    }

    [Fact]
    public async Task SeparateDeliveriesWaitForTheMissingFirstOwnerSequence()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var handler = new FencedIndexHandler();
        using ServiceProvider provider = new ServiceCollection()
            .AddSingleton<IReadModelStore>(store)
            .AddSingleton<IReadModelBatchStore>(store)
            .AddSingleton(coordinator)
            .AddScoped<IAsyncDomainProjectionHandler>(_ => handler)
            .BuildServiceProvider();

        async Task<ProjectionDispatchOutcome> Deliver(ProjectionEventDto item)
            => (await DomainProjectionDispatcher.DispatchAsync(provider,
                new ProjectionDispatchRequest(
                    new ProjectionRequest("tenant-a", "widget", "aggregate-a", [item]),
                    ["fenced-widget-index"], "delivery-" + item.SequenceNumber, CatalogFingerprint()),
                new ProjectionDispatchOptions(), IdentityOptions(), CancellationToken.None)).Outcomes.Single();

        ProjectionDispatchOutcome gap = await Deliver(Event(2, 2));
        gap.Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        gap.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryGap);
        (await coordinator.ReadAsync<Index>(handler.CreateScope("tenant-a"), IndexKey)).Value.ShouldBeNull();

        (await Deliver(Event(1, 1))).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await Deliver(Event(2, 2))).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await coordinator.ReadAsync<Index>(handler.CreateScope("tenant-a"), IndexKey)).Value.ShouldNotBeNull().Count.ShouldBe(2);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    public async Task RegistrationOrJournalingStoreReadOutageIsRetryable(int failureRead, bool daprFailure)
    {
        var inner = new InMemoryReadModelStore();
        var fault = new FaultingReadModelStore(inner) { FailEpochReadNumber = failureRead, DaprFailure = daprFailure };
        var coordinator = new SharedProjectionEpochCoordinator(fault, inner);
        var handler = new FencedIndexHandler();
        using ServiceProvider provider = new ServiceCollection()
            .AddSingleton<IReadModelStore>(fault)
            .AddSingleton<IReadModelBatchStore>(inner)
            .AddSingleton(coordinator)
            .AddScoped<IAsyncDomainProjectionHandler>(_ => handler)
            .BuildServiceProvider();

        ProjectionDispatchOutcome outcome = (await DomainProjectionDispatcher.DispatchAsync(provider,
            new ProjectionDispatchRequest(
                new ProjectionRequest("tenant-a", "widget", "aggregate-a", [Event(1, 1)]),
                ["fenced-widget-index"], "outage", CatalogFingerprint()),
            new ProjectionDispatchOptions(), IdentityOptions(), CancellationToken.None)).Outcomes.Single();

        outcome.Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        outcome.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryStateUnavailable);
        if (failureRead == 2)
        {
            (await coordinator.ReadAsync<Index>(handler.CreateScope("tenant-a"), IndexKey)).Value.ShouldBeNull();
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task StoreOwnedCancellationDuringRegistrationJournalingOrCatchUpIsRetryable(int failureRead)
    {
        var inner = new InMemoryReadModelStore();
        var fault = new FaultingReadModelStore(inner)
        {
            FailEpochReadNumber = failureRead,
            TaskCanceledFailure = true
        };
        var coordinator = new SharedProjectionEpochCoordinator(fault, inner);
        var handler = new FencedIndexHandler();
        using ServiceProvider provider = new ServiceCollection()
            .AddSingleton<IReadModelStore>(fault)
            .AddSingleton<IReadModelBatchStore>(inner)
            .AddSingleton(coordinator)
            .AddScoped<IAsyncDomainProjectionHandler>(_ => handler)
            .BuildServiceProvider();

        ProjectionDispatchOutcome outcome = (await DomainProjectionDispatcher.DispatchAsync(provider,
            new ProjectionDispatchRequest(
                new ProjectionRequest("tenant-a", "widget", "aggregate-a", [Event(1, 1)]),
                ["fenced-widget-index"], "store-timeout", CatalogFingerprint()),
            new ProjectionDispatchOptions(), IdentityOptions(), CancellationToken.None)).Outcomes.Single();

        outcome.Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        outcome.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryStateUnavailable);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task CallerCancellationDuringRegistrationOrJournalingPropagates(int failureRead)
    {
        var inner = new InMemoryReadModelStore();
        using var cancellation = new CancellationTokenSource();
        var fault = new FaultingReadModelStore(inner)
        {
            FailEpochReadNumber = failureRead,
            TaskCanceledFailure = true,
            CancelRequestOnFailure = cancellation
        };
        var coordinator = new SharedProjectionEpochCoordinator(fault, inner);
        var handler = new FencedIndexHandler();
        using ServiceProvider provider = new ServiceCollection()
            .AddSingleton<IReadModelStore>(fault)
            .AddSingleton<IReadModelBatchStore>(inner)
            .AddSingleton(coordinator)
            .AddScoped<IAsyncDomainProjectionHandler>(_ => handler)
            .BuildServiceProvider();

        await Should.ThrowAsync<OperationCanceledException>(() => DomainProjectionDispatcher.DispatchAsync(
            provider,
            new ProjectionDispatchRequest(new ProjectionRequest("tenant-a", "widget", "aggregate-a", [Event(1, 1)]),
                ["fenced-widget-index"], "cancelled", CatalogFingerprint()),
            new ProjectionDispatchOptions(), IdentityOptions(), cancellation.Token));
    }

    [Fact]
    public async Task LegacyOwnerCheckpointRequiresExplicitRebuildBeforeLaterSequence()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var handler = new FencedIndexHandler();
        SharedProjectionScope scope = handler.CreateScope("tenant-a");
        _ = await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId);
        string stateKey = "shared-epoch:" + scope.ComputeHash();
        ReadModelEntry<JsonObject> entry = await store.GetAsync<JsonObject>(StoreName, stateKey);
        JsonObject legacy = entry.Value.ShouldNotBeNull();
        legacy["consumerCheckpoints"] = new JsonObject
        {
            ["aggregate-a"] = JsonSerializer.SerializeToNode(
                new SharedProjectionCheckpoint(1, [1], 0), new JsonSerializerOptions(JsonSerializerDefaults.Web))
        };
        (await store.TrySaveAsync(StoreName, stateKey, legacy, entry.ETag!)).ShouldBeTrue();
        using ServiceProvider provider = new ServiceCollection()
            .AddSingleton<IReadModelStore>(store)
            .AddSingleton<IReadModelBatchStore>(store)
            .AddSingleton(coordinator)
            .AddScoped<IAsyncDomainProjectionHandler>(_ => handler)
            .BuildServiceProvider();

        ProjectionDispatchOutcome outcome = (await DomainProjectionDispatcher.DispatchAsync(provider,
            new ProjectionDispatchRequest(new ProjectionRequest("tenant-a", "widget", "aggregate-a", [Event(2, 2)]),
                ["fenced-widget-index"], "legacy-later", CatalogFingerprint()),
            new ProjectionDispatchOptions(), IdentityOptions(), CancellationToken.None)).Outcomes.Single();
        outcome.Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        outcome.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryRebuildRequired);
        (await coordinator.ReadAsync<Index>(scope, IndexKey)).Value.ShouldBeNull();
    }

    [Fact]
    public async Task FencedHandlerLocalCancellationIsIndeterminate()
    {
        var store = new InMemoryReadModelStore();
        var handler = new FencedIndexHandler { CancelFold = true };
        using ServiceProvider provider = new ServiceCollection()
            .AddSingleton<IReadModelStore>(store)
            .AddSingleton<IReadModelBatchStore>(store)
            .AddSingleton(new SharedProjectionEpochCoordinator(store, store))
            .AddScoped<IAsyncDomainProjectionHandler>(_ => handler)
            .BuildServiceProvider();

        ProjectionDispatchOutcome outcome = (await DomainProjectionDispatcher.DispatchAsync(provider,
            new ProjectionDispatchRequest(
                new ProjectionRequest("tenant-a", "widget", "aggregate-a", [Event(1, 1)]),
                ["fenced-widget-index"], "handler-cancellation", CatalogFingerprint()),
            new ProjectionDispatchOptions(), IdentityOptions(), CancellationToken.None)).Outcomes.Single();

        outcome.Status.ShouldBe(ProjectionDispatchStatus.Indeterminate);
        outcome.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.HandlerFailure);
    }

    [Fact]
    public async Task NamedFoldFailure_ReturnsRetryableThenParksThroughFencedRecovery()
    {
        var store = new InMemoryReadModelStore();
        var handler = new FencedIndexHandler { FailFold = true };
        ProjectionDispatchRequest delivery = new(
            new ProjectionRequest("tenant-a", "widget", "aggregate-a", [Event(1, 10)]),
            ["fenced-widget-index"],
            "delivery-1",
            CatalogFingerprint());
        SharedProjectionScope scope = handler.CreateScope("tenant-a");

        var firstCoordinator = new SharedProjectionEpochCoordinator(store, store);
        var firstServices = new ServiceCollection();
        _ = firstServices.AddSingleton<IReadModelStore>(store);
        _ = firstServices.AddSingleton<IReadModelBatchStore>(store);
        _ = firstServices.AddSingleton(firstCoordinator);
        _ = firstServices.AddScoped<IAsyncDomainProjectionHandler>(_ => handler);
        using (ServiceProvider firstProvider = firstServices.BuildServiceProvider())
        {
            ProjectionDispatchResponse first = await DomainProjectionDispatcher.DispatchAsync(
                firstProvider, delivery, new ProjectionDispatchOptions(), IdentityOptions(), CancellationToken.None);
            first.Outcomes.Single().Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        }

        (await firstCoordinator.GetStatusAsync(scope)).PendingDeliveryCount.ShouldBe(1);
        (await firstCoordinator.ReadGenerationAsync<Parking>(scope, 0, "widget:parking")).Value.ShouldBeNull();
        var restarted = new SharedProjectionEpochCoordinator(store, store);
        var restartedServices = new ServiceCollection();
        _ = restartedServices.AddSingleton<IReadModelStore>(store);
        _ = restartedServices.AddSingleton<IReadModelBatchStore>(store);
        _ = restartedServices.AddSingleton(restarted);
        _ = restartedServices.AddScoped<IAsyncDomainProjectionHandler>(_ => handler);
        using ServiceProvider restartedProvider = restartedServices.BuildServiceProvider();
        ProjectionDispatchResponse parked = await DomainProjectionDispatcher.DispatchAsync(
            restartedProvider, delivery, new ProjectionDispatchOptions(), IdentityOptions(), CancellationToken.None);
        parked.Outcomes.Single().Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        (await restarted.ReadAsync<Parking>(scope, "widget:parking")).Value.ShouldBe(new Parking(1, 2));
        (await restarted.GetStatusAsync(scope)).PendingDeliveryCount.ShouldBe(0);
        ProjectionDispatchResponse duplicate = await DomainProjectionDispatcher.DispatchAsync(
            restartedProvider, delivery, new ProjectionDispatchOptions(), IdentityOptions(), CancellationToken.None);
        duplicate.Outcomes.Single().Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        (await restarted.ReadAsync<Parking>(scope, "widget:parking")).Value.ShouldBe(new Parking(1, 2));
    }

    private static string CatalogFingerprint()
        => ProjectionRouteCatalogFingerprint.Compute(
            "widget-service",
            "v1",
            [new ProjectionDispatchRoute("widget", "fenced-widget-index")]);

    private static ProjectionEventDto Event(long sequence, long globalPosition)
        => new(
            "widget-updated",
            JsonSerializer.SerializeToUtf8Bytes(new { sequence }),
            "json",
            sequence,
            DateTimeOffset.UnixEpoch,
            "correlation-1",
            "message-" + sequence,
            "user-1",
            globalPosition);

    private static DomainProjectionIdentityOptions IdentityOptions()
        => new() { AppId = "widget-service", ServiceVersion = "v1" };

    private static DomainSharedProjectionRebuildRequest Lifecycle(
        DomainSharedProjectionRebuildIdentity identity,
        DomainSharedProjectionRebuildAction action)
        => new(DomainSharedProjectionRebuildProtocol.Version, action, identity);

    private static Task<DomainSharedProjectionRebuildResponse> RebuildAsync(
        IServiceProvider provider,
        DomainSharedProjectionRebuildRequest request)
        => DomainSharedProjectionRebuildDispatcher.DispatchAsync(
            provider,
            request,
            new ProjectionDispatchOptions(),
            IdentityOptions(),
            CancellationToken.None);

    private sealed record Index(int Count);

    private sealed record Parking(long FailedPosition, int FailureCount);

    private sealed class FaultingReadModelStore(InMemoryReadModelStore inner) : IReadModelStore
    {
        private int _epochReads;

        public int FailEpochReadNumber { get; init; }

        public bool DaprFailure { get; init; }

        public bool TaskCanceledFailure { get; init; }

        public CancellationTokenSource? CancelRequestOnFailure { get; init; }

        public Task<ReadModelEntry<TValue>> GetAsync<TValue>(
            string storeName, string key, CancellationToken cancellationToken = default) where TValue : class
        {
            if (key.StartsWith("shared-epoch:", StringComparison.Ordinal)
                && Interlocked.Increment(ref _epochReads) == FailEpochReadNumber)
            {
                if (DaprFailure)
                {
                    throw new Dapr.DaprException("Injected Dapr state-store outage.");
                }

                if (TaskCanceledFailure)
                {
                    CancelRequestOnFailure?.Cancel();
                    throw new TaskCanceledException("Injected read-model store timeout.");
                }

                throw new IOException("Injected read-model I/O outage.");
            }

            return inner.GetAsync<TValue>(storeName, key, cancellationToken);
        }

        public Task SaveAsync<TValue>(
            string storeName, string key, TValue value, CancellationToken cancellationToken = default) where TValue : class
            => inner.SaveAsync(storeName, key, value, cancellationToken);

        public Task<bool> TrySaveAsync<TValue>(
            string storeName, string key, TValue value, string etag, CancellationToken cancellationToken = default) where TValue : class
            => inner.TrySaveAsync(storeName, key, value, etag, cancellationToken);
    }

    private sealed class FencedIndexHandler : IAsyncDomainSharedProjectionEpochHandler
    {
        private static readonly JsonSerializerOptions s_webOptions = new(JsonSerializerDefaults.Web);

        public bool EpochEnabled => true;

        public bool GlobalScope { get; init; }
        public bool RequireOtherWriter { get; init; }

        public bool GlobalEpochScope => GlobalScope;

        public bool FailFold { get; init; }

        public bool CancelFold { get; init; }

        public string Domain => "widget";

        public string ProjectionType => "fenced-widget-index";

        public string RebuildStoreName => StoreName;

        public string OrdinaryWriterId => "ordinary-dispatch";

        public string? ControlIndexName => "widget-discovery";

        public SharedProjectionScope CreateScope(string tenantId)
            => new(StoreName, GlobalScope ? IAsyncDomainSharedProjectionEpochHandler.GlobalTenantSegment
                : tenantId, Domain, ProjectionType, RequireOtherWriter ? [OrdinaryWriterId, "other-writer"] : [OrdinaryWriterId]);

        public Task<DomainSharedProjectionRebuildCandidate> CreateEmptyCandidateAsync(
            DomainSharedProjectionRebuildIdentity identity,
            CancellationToken cancellationToken)
            => Task.FromResult(new DomainSharedProjectionRebuildCandidate(JsonSerializer.SerializeToUtf8Bytes(0)));

        public Task<DomainSharedProjectionRebuildCandidate> AccumulateAsync(
            DomainSharedProjectionRebuildIdentity identity,
            DomainSharedProjectionRebuildCandidate candidate,
            ProjectionRequest aggregateHistory,
            CancellationToken cancellationToken)
            => Task.FromResult(new DomainSharedProjectionRebuildCandidate(JsonSerializer.SerializeToUtf8Bytes(
                JsonSerializer.Deserialize<int>(candidate.State.Span) + aggregateHistory.Events.Length)));

        public Task<DomainProjectionRebuildPlan> FinalizeAsync(
            DomainSharedProjectionRebuildIdentity identity,
            DomainSharedProjectionRebuildCandidate candidate,
            CancellationToken cancellationToken)
            => Task.FromResult(new DomainProjectionRebuildPlan(
                StoreName,
                [ReadModelBatchOperation.Write(
                    IndexKey,
                    new Index(JsonSerializer.Deserialize<int>(candidate.State.Span)),
                    ReadModelBatchConcurrency.LastWrite)]));

        public async Task<IReadOnlyList<ReadModelBatchOperation>> FoldCatchUpAsync(
            SharedProjectionDelivery delivery,
            long generation,
            SharedProjectionEpochCoordinator coordinator,
            CancellationToken cancellationToken)
        {
            if (CancelFold)
            {
                throw new OperationCanceledException("Handler-local cancellation.");
            }

            ProjectionRequest request = JsonSerializer.Deserialize<ProjectionRequest>(delivery.CanonicalPayload, s_webOptions)!;
            if (FailFold)
            {
                long failedPosition = request.Events[0].SequenceNumber;
                throw new SharedProjectionFoldFailureException(
                    request.AggregateId,
                    failedPosition,
                    2,
                    count => [ReadModelBatchOperation.Write(
                        "widget:parking",
                        new Parking(failedPosition, count),
                        ReadModelBatchConcurrency.LastWrite)]);
            }

            ReadModelEntry<Index> prior = await coordinator.ReadGenerationAsync<Index>(
                CreateScope(request.TenantId),
                generation,
                IndexKey,
                cancellationToken);
            return [ReadModelBatchOperation.Write(
                IndexKey,
                new Index((prior.Value?.Count ?? 0) + 1),
                ReadModelBatchConcurrency.LastWrite)];
        }

        public Task<DomainProjectionHandlerResult> ProjectAsync(
            ProjectionRequest request,
            string dispatchId,
            CancellationToken cancellationToken)
            => throw new NotSupportedException("Fenced ordinary delivery must be journaled by the dispatcher.");
    }
}
