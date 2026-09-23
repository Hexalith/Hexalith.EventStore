using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

public sealed class DomainFencedSharedProjectionIntegrationTests
{
    private const string StoreName = "statestore";
    private const string IndexKey = "widget:shared-index";

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
                ["widget-index"],
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
            "widget-index",
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
                new ProjectionRequest("tenant-a", "widget", "aggregate-a", [first, second]),
                ["widget-index"],
                "delivery-2",
                catalog),
            options,
            identityOptions,
            CancellationToken.None);
        concurrent.Outcomes.Single().Status.ShouldBe(ProjectionDispatchStatus.Completed);
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
        promoted.Value!.Count.ShouldBe(2);
        (await coordinator.GetCheckpointAsync(scope, "aggregate-a"))!.Position.ShouldBe(2);

        DomainSharedProjectionRebuildResponse replay = await RebuildAsync(
            provider,
            Lifecycle(identity, DomainSharedProjectionRebuildAction.Commit));
        replay.Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        (await coordinator.ReadAsync<Index>(scope, IndexKey)).Value!.Count.ShouldBe(2);
    }

    [Fact]
    public async Task NamedFoldFailure_ReturnsRetryableThenParksThroughFencedRecovery()
    {
        var store = new InMemoryReadModelStore();
        var handler = new FencedIndexHandler { FailFold = true };
        ProjectionDispatchRequest delivery = new(
            new ProjectionRequest("tenant-a", "widget", "aggregate-a", [Event(7, 10)]),
            ["widget-index"],
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
        (await restarted.ReadAsync<Parking>(scope, "widget:parking")).Value.ShouldBe(new Parking(7, 2));
        (await restarted.GetStatusAsync(scope)).PendingDeliveryCount.ShouldBe(0);
        ProjectionDispatchResponse duplicate = await DomainProjectionDispatcher.DispatchAsync(
            restartedProvider, delivery, new ProjectionDispatchOptions(), IdentityOptions(), CancellationToken.None);
        duplicate.Outcomes.Single().Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        (await restarted.ReadAsync<Parking>(scope, "widget:parking")).Value.ShouldBe(new Parking(7, 2));
    }

    private static string CatalogFingerprint()
        => ProjectionRouteCatalogFingerprint.Compute(
            "widget-service",
            "v1",
            [new ProjectionDispatchRoute("widget", "widget-index")]);

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

    private sealed class FencedIndexHandler : IAsyncDomainSharedProjectionEpochHandler
    {
        private static readonly JsonSerializerOptions s_webOptions = new(JsonSerializerDefaults.Web);

        public bool EpochEnabled => true;

        public bool FailFold { get; init; }

        public string Domain => "widget";

        public string ProjectionType => "widget-index";

        public string RebuildStoreName => StoreName;

        public string OrdinaryWriterId => "ordinary-dispatch";

        public string? ControlIndexName => "widget-discovery";

        public SharedProjectionScope CreateScope(string tenantId)
            => new(StoreName, tenantId, Domain, ProjectionType, [OrdinaryWriterId]);

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
