using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Testing.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

public sealed partial class DomainPartitionedSharedProjectionRebuildTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SupportedTransactionalScratchConflictSettlesBeforeNamedRetryOrAbort(bool retry)
    {
        var store = new InMemoryReadModelStore();
        store.BatchOptions.StoreProfiles[StoreName] = ReadModelBatchStoreProfile.TransactionQualified;
        var handler = new PartitionedHandler { FoldOrdinaryRows = true };
        SharedProjectionScope scope = handler.CreateScope("tenant-a");
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using ServiceProvider original = Provider(store, store, coordinator, handler);
        _ = await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId);
        var identity = Identity("transaction-conflict");
        (string Owner, ProjectionEventDto Event) owner = ("owner-a", Event([1]) with { GlobalPosition = 1 });
        (await BeginAsync(original, identity, [owner])).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        var racing = new OccupiedScratchBatchStore(store);
        using ServiceProvider interrupted = Provider(store, racing, new SharedProjectionEpochCoordinator(store, racing), handler);
        (await AccumulateAsync(interrupted, identity, 0, owner)).Status.ShouldNotBe(ProjectionDispatchStatus.Completed);
        ReadModelBatch batch = racing.OriginalBatch.ShouldNotBeNull();
        (await store.RecoverPendingAsync(batch.Scope, batch.Scope.ComputeMarkerKey())).IsResolved.ShouldBeTrue();
        using (System.Text.Json.JsonDocument terminal = System.Text.Json.JsonDocument.Parse(store.SnapshotRaw(StoreName, batch.Scope.ComputeMarkerKey())!))
            terminal.RootElement.GetProperty("st").GetInt32().ShouldBe(3);
        coordinator = new SharedProjectionEpochCoordinator(store, store);
        using ServiceProvider replaced = Provider(store, store, coordinator, handler);
        if (retry)
        {
            (await AccumulateAsync(replaced, identity, 0, owner)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
            (await AccumulateAsync(replaced, identity, 0, owner)).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        }
        (await RebuildAsync(replaced, Lifecycle(identity, DomainSharedProjectionRebuildAction.Abort))).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await coordinator.GetStatusAsync(scope)).IsBuilding.ShouldBeFalse();
        store.SnapshotRaw(StoreName, batch.Operations[0].Key).ShouldBeNull();
        ProjectionEventDto growth = Event([9]) with { GlobalPosition = 2, MessageId = "growth" };
        ProjectionDispatchResponse delivered = await DomainProjectionDispatcher.DispatchAsync(replaced,
            new ProjectionDispatchRequest(new ProjectionRequest("tenant-a", "widget", "owner-a", [growth]),
                [handler.ProjectionType], growth.MessageId, identity.CatalogFingerprint), new ProjectionDispatchOptions(),
            new DomainProjectionIdentityOptions { AppId = "widget-service", ServiceVersion = "v1" }, CancellationToken.None);
        delivered.Outcomes.Single().Status.ShouldBe(ProjectionDispatchStatus.Completed);
        var fresh = Identity("fresh-after-transaction");
        string inventory = DomainSharedProjectionRebuildFingerprint.AppendInventory(DomainSharedProjectionRebuildFingerprint.EmptyInventory,
            0, DomainSharedProjectionRebuildFingerprint.ComputeHistory("owner-a", false, [growth]));
        (await RebuildAsync(replaced, new(DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Begin,
            fresh, CaptureInventoryFingerprint: inventory, SourceHighWatermarks: new Dictionary<string, long> { ["owner-a"] = 2 })))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        var accepted = await AccumulateAsync(replaced, fresh, 0, ("owner-a", growth));
        accepted.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await FinishAsync(replaced, fresh, accepted)).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
    }
}
