using System.Text.Json.Nodes;
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
    public async Task NamedAbortReconcilesLateOriginalChunkAfterTerminalCleanupFlagsAdvance(bool accepted)
    {
        var store = new InMemoryReadModelStore();
        var handler = new PartitionedHandler { FoldOrdinaryRows = true };
        SharedProjectionScope scope = handler.CreateScope("tenant-a");
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using ServiceProvider provider = Provider(store, store, coordinator, handler);
        _ = await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId);
        var identity = Identity("retired-owner");
        var owner = ("owner-a", Event([1]) with { GlobalPosition = 1 });
        _ = await BeginAsync(provider, identity, [owner]);
        string session = DomainSharedProjectionRebuildSessionKey.Compute(identity);
        string scratch = DomainSharedProjectionRebuildDispatcher.PartitionChunkKey(session, 0, 0);
        byte[]? original = null;
        store.BatchFaultHook = (phase, ordinal, _) =>
        {
            if (phase == ReadModelBatchPhase.AfterInstallOperation && ordinal == 0 && original is null
                && store.SnapshotRaw(StoreName, scratch) is { } bytes)
            {
                original = bytes;
                if (!accepted) throw new IOException("original owner stopped after installation");
            }
            return Task.CompletedTask;
        };
        (await AccumulateAsync(provider, identity, 0, owner)).Status.ShouldBe(accepted ? ProjectionDispatchStatus.Completed : ProjectionDispatchStatus.Indeterminate);
        store.BatchFaultHook = null;
        (await RebuildAsync(provider, Lifecycle(identity, DomainSharedProjectionRebuildAction.Abort))).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        var terminal = await store.GetAsync<DomainSharedProjectionRebuildSessionState>(StoreName, session);
        terminal.Value!.ScratchIndexesCleaned.ShouldBeTrue();
        terminal.Value.ScratchCleanedOwnerCount.ShouldBe(accepted ? 1 : 0);
        byte[] open = store.SnapshotRaw(StoreName, ("shared-epoch:" + scope.ComputeHash()))!;
        store.SeedRaw(StoreName, scratch, JsonNode.Parse(original!)!);
        coordinator = new SharedProjectionEpochCoordinator(store, store);
        using ServiceProvider replaced = Provider(store, store, coordinator, handler);
        (await RebuildAsync(replaced, Lifecycle(identity, DomainSharedProjectionRebuildAction.Abort))).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        store.SnapshotRaw(StoreName, scratch).ShouldBeNull();
        store.SnapshotRaw(StoreName, ("shared-epoch:" + scope.ComputeHash())).ShouldBe(open);
        (await RebuildAsync(replaced, Lifecycle(identity, DomainSharedProjectionRebuildAction.Abort))).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        ProjectionEventDto growth = Event([9]) with { GlobalPosition = 2, MessageId = "after-late-cleanup" };
        ProjectionDispatchResponse delivery = await DomainProjectionDispatcher.DispatchAsync(replaced,
            new ProjectionDispatchRequest(new ProjectionRequest("tenant-a", "widget", "owner-a", [growth]), [handler.ProjectionType],
                growth.MessageId, identity.CatalogFingerprint), new ProjectionDispatchOptions(),
            new DomainProjectionIdentityOptions { AppId = "widget-service", ServiceVersion = "v1" }, default);
        delivery.Outcomes.Single().Status.ShouldBe(ProjectionDispatchStatus.Completed);
        var fresh = Identity("fresh-after-retired-owner");
        string inventory = DomainSharedProjectionRebuildFingerprint.AppendInventory(DomainSharedProjectionRebuildFingerprint.EmptyInventory, 0,
            DomainSharedProjectionRebuildFingerprint.ComputeHistory("owner-a", false, [growth]));
        (await RebuildAsync(replaced, new(DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Begin,
            fresh, CaptureInventoryFingerprint: inventory, SourceHighWatermarks: new Dictionary<string, long> { ["owner-a"] = 2 })))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        var current = await AccumulateAsync(replaced, fresh, 0, ("owner-a", growth));
        (await FinishAsync(replaced, fresh, current)).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        (await coordinator.ReadAsync<PayloadRow>(scope, "row:owner-a")).Value.ShouldNotBeNull();
    }
}
