using System.Security.Cryptography;
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
    public async Task ProductionLegacyScratchCompensationConvergesForSameActionRetryOrDirectAbort(bool retry)
    {
        var store = new InMemoryReadModelStore();
        var handler = new PartitionedHandler { FoldOrdinaryRows = true };
        SharedProjectionScope scope = handler.CreateScope("tenant-a");
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using ServiceProvider original = Provider(store, store, coordinator, handler);
        _ = await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId);
        var identity = Identity("legacy-pending");
        (string Owner, ProjectionEventDto Event) owner = ("owner-a", Event([1]) with { GlobalPosition = 1 });
        _ = await BeginAsync(original, identity, [owner]);
        var legacy = new LegacyScratchBatchStore(store);
        using ServiceProvider interrupted = Provider(store, legacy, new SharedProjectionEpochCoordinator(store, legacy), handler);
        store.BatchFaultHook = (phase, _, _) => phase == ReadModelBatchPhase.BeforeCommit
            ? throw new InvalidOperationException("historical owner stopped") : Task.CompletedTask;
        (await AccumulateAsync(interrupted, identity, 0, owner)).Status.ShouldBe(ProjectionDispatchStatus.Indeterminate);
        store.BatchFaultHook = null;
        coordinator = new SharedProjectionEpochCoordinator(store, store);
        using ServiceProvider replaced = Provider(store, store, coordinator, handler);
        if (retry)
        {
            (await AccumulateAsync(replaced, identity, 0, owner)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
            (await AccumulateAsync(replaced, identity, 0, owner)).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        }
        (await RebuildAsync(replaced, Lifecycle(identity, DomainSharedProjectionRebuildAction.Abort))).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await RebuildAsync(replaced, Lifecycle(identity, DomainSharedProjectionRebuildAction.Abort))).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        (await coordinator.GetStatusAsync(scope)).IsBuilding.ShouldBeFalse();
        (await coordinator.GetStatusAsync(scope)).IsAborting.ShouldBeFalse();
        store.SnapshotRaw(StoreName, DomainSharedProjectionRebuildDispatcher.PartitionChunkKey(
            DomainSharedProjectionRebuildSessionKey.Compute(identity), 0, 0)).ShouldBeNull();
        ProjectionEventDto growth = Event([9]) with { GlobalPosition = 2, MessageId = "growth" };
        ProjectionDispatchResponse delivered = await DomainProjectionDispatcher.DispatchAsync(replaced,
            new ProjectionDispatchRequest(new ProjectionRequest("tenant-a", "widget", "owner-a", [growth]),
                [handler.ProjectionType], growth.MessageId, identity.CatalogFingerprint), new ProjectionDispatchOptions(),
            new DomainProjectionIdentityOptions { AppId = "widget-service", ServiceVersion = "v1" }, CancellationToken.None);
        delivered.Outcomes.Single().Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await coordinator.ReadAsync<PayloadRow>(scope, "row:owner-a")).Value!.Value.ShouldBe(Convert.ToHexString(SHA256.HashData([9])));
        var fresh = Identity("fresh-after-legacy");
        string inventory = DomainSharedProjectionRebuildFingerprint.AppendInventory(DomainSharedProjectionRebuildFingerprint.EmptyInventory,
            0, DomainSharedProjectionRebuildFingerprint.ComputeHistory("owner-a", false, [growth]));
        (await RebuildAsync(replaced, new(DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Begin,
            fresh, CaptureInventoryFingerprint: inventory, SourceHighWatermarks: new Dictionary<string, long> { ["owner-a"] = 2 })))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        var accepted = await AccumulateAsync(replaced, fresh, 0, ("owner-a", growth));
        accepted.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await FinishAsync(replaced, fresh, accepted)).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
    }

    public static IEnumerable<object[]> ScratchRecoveryCases()
    {
        (ReadModelBatchPhase Phase, int Ordinal)[] cuts =
        [
            (ReadModelBatchPhase.MarkerPrepared, -1),
            (ReadModelBatchPhase.BeforeInstallOperation, 0),
            (ReadModelBatchPhase.AfterInstallOperation, 0),
            (ReadModelBatchPhase.BeforeInstallOperation, 1),
            (ReadModelBatchPhase.AfterInstallOperation, 1),
            (ReadModelBatchPhase.BeforeCommit, -1),
            (ReadModelBatchPhase.AfterCommit, -1),
            (ReadModelBatchPhase.BeforeCompaction, 0),
            (ReadModelBatchPhase.AfterCompaction, 0),
            (ReadModelBatchPhase.BeforeCompaction, 1),
            (ReadModelBatchPhase.RecoveryReceiptWritten, 1),
            (ReadModelBatchPhase.RecoveryManifestReclaimed, 1),
            (ReadModelBatchPhase.RecoveryAnchorCompacted, 1),
        ];
        foreach (var cut in cuts)
            foreach (bool populated in new[] { false, true })
                foreach (bool retry in new[] { false, true })
                    yield return [cut.Phase, cut.Ordinal, retry, false, populated, false];
        foreach (var cut in cuts.Where(cut => cut.Phase is not (ReadModelBatchPhase.RecoveryReceiptWritten
            or ReadModelBatchPhase.RecoveryManifestReclaimed or ReadModelBatchPhase.RecoveryAnchorCompacted) && cut.Ordinal <= 0))
            yield return [cut.Phase, cut.Ordinal, false, false, true, true];
        yield return [ReadModelBatchPhase.BeforeCommit, -1, true, true, true, false];
    }

    [Theory]
    [MemberData(nameof(ScratchRecoveryCases))]
    public async Task InterruptedAccumulateRetryOrDirectAbortPreservesSelectionAndPermitsFreshRebuild(
        ReadModelBatchPhase failure, int failureOrdinal, bool retryFirst, bool global, bool populated, bool cleanupFailure)
    {
        var store = new InMemoryReadModelStore();
        PartitionedHandler handler = global ? new GlobalPartitionedHandler() : new PartitionedHandler();
        handler.FoldOrdinaryRows = true;
        SharedProjectionScope scope = handler.CreateScope("tenant-a");
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using ServiceProvider first = Provider(store, store, coordinator, handler);
        _ = await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId);
        DomainSharedProjectionRebuildIdentity original = Identity("old-selected");
        (string Owner, ProjectionEventDto Event)[] history = [("owner-a", Event([1]) with { GlobalPosition = 1 })];
        DomainSharedProjectionRebuildResponse current;
        string? selected = null;
        if (populated)
        {
            _ = await BeginAsync(first, original, history);
            current = await AccumulateAsync(first, original, 0, history[0]);
            (await FinishAsync(first, original, current)).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
            selected = (await coordinator.ReadAsync<PayloadRow>(scope, "row:owner-a")).Value!.Value;
        }
        long selectedGeneration = (await coordinator.GetStatusAsync(scope)).ActiveGeneration;

        DomainSharedProjectionRebuildIdentity interrupted = Identity("interrupted-scratch");
        (string Owner, ProjectionEventDto Event)[] replacement = [history[0], ("owner-b", Event([2]) with { GlobalPosition = 1 })];
        (await BeginAsync(first, interrupted, replacement)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        if (!cleanupFailure)
            store.BatchFaultHook = (phase, ordinal, _) => phase == failure && ordinal == failureOrdinal
                ? throw new InvalidOperationException("scratch process stop") : Task.CompletedTask;
        (await AccumulateAsync(first, interrupted, 0, replacement[0])).Status.ShouldBe(
            cleanupFailure ? ProjectionDispatchStatus.Completed : ProjectionDispatchStatus.Indeterminate);
        store.BatchFaultHook = null;
        coordinator = new SharedProjectionEpochCoordinator(store, store);
        using ServiceProvider restarted = Provider(store, store, coordinator, handler);
        (await coordinator.ReadAsync<PayloadRow>(scope, "row:owner-a")).Value?.Value.ShouldBe(selected);
        (await coordinator.ReadAsync<PayloadRow>(scope, "row:owner-b")).Value.ShouldBeNull();
        if (retryFirst)
        {
            (await AccumulateAsync(restarted, interrupted, 0, replacement[0])).Status.ShouldBe(ProjectionDispatchStatus.Completed);
            (await AccumulateAsync(restarted, interrupted, 0, replacement[0])).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        }

        // Owner-sequenced named deliveries retain their retry work while the Building fence is active.
        foreach (string owner in new[] { "owner-a", "owner-b" })
        {
            ProjectionEventDto next = Event([9]) with { GlobalPosition = 2, SequenceNumber = populated && owner == "owner-a" ? 2 : 1, MessageId = "next-" + owner };
            ProjectionDispatchResponse delivery = await DomainProjectionDispatcher.DispatchAsync(restarted,
                new ProjectionDispatchRequest(new ProjectionRequest("tenant-a", "widget", owner, [next]),
                    [handler.ProjectionType], "growth-" + owner, interrupted.CatalogFingerprint), new ProjectionDispatchOptions(),
                new DomainProjectionIdentityOptions { AppId = "widget-service", ServiceVersion = "v1" }, CancellationToken.None);
            delivery.Outcomes.Single().Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        }
        _ = await RebuildAsync(restarted, Lifecycle(interrupted with { OperationId = "foreign" }, DomainSharedProjectionRebuildAction.Abort));
        (await coordinator.GetStatusAsync(scope)).OperationId.ShouldBe(interrupted.OperationId);
        (await coordinator.GetStatusAsync(scope)).IsBuilding.ShouldBeTrue();
        if (cleanupFailure)
        {
            store.BatchFaultHook = (phase, ordinal, _) => phase == failure && ordinal == failureOrdinal
                ? throw new InvalidOperationException("cleanup process stop") : Task.CompletedTask;
            (await RebuildAsync(restarted, Lifecycle(interrupted, DomainSharedProjectionRebuildAction.Abort)))
                .Status.ShouldBe(ProjectionDispatchStatus.Indeterminate);
            store.BatchFaultHook = null;
            coordinator = new SharedProjectionEpochCoordinator(store, store);
        }
        using ServiceProvider recoveryProvider = Provider(store, store, coordinator, handler);
        DomainSharedProjectionRebuildResponse aborted = await RebuildAsync(recoveryProvider, Lifecycle(interrupted, DomainSharedProjectionRebuildAction.Abort));
        aborted.Phase.ShouldBe(DomainSharedProjectionRebuildPhase.Aborted, aborted.ToString());
        aborted.Status.ShouldBe(cleanupFailure ? ProjectionDispatchStatus.AlreadyCompleted : ProjectionDispatchStatus.Completed, aborted.ToString());
        (await RebuildAsync(restarted, Lifecycle(interrupted, DomainSharedProjectionRebuildAction.Abort))).Phase.ShouldBe(DomainSharedProjectionRebuildPhase.Aborted);
        SharedProjectionEpochStatus status = await coordinator.GetStatusAsync(scope);
        status.IsBuilding.ShouldBeFalse();
        status.IsAborting.ShouldBeFalse();
        status.ActiveGeneration.ShouldBe(selectedGeneration);
        status.PendingDeliveryCount.ShouldBe(0);
        (await coordinator.ReadAsync<PayloadRow>(scope, "row:owner-a")).Value?.Value.ShouldBe(selected);
        (await coordinator.ReadAsync<PayloadRow>(scope, "row:owner-b")).Value.ShouldBeNull();
        foreach (string owner in new[] { "owner-a", "owner-b" })
        {
            ProjectionEventDto next = Event([9]) with { GlobalPosition = 2, SequenceNumber = populated && owner == "owner-a" ? 2 : 1, MessageId = "next-" + owner };
            ProjectionDispatchResponse growth = await DomainProjectionDispatcher.DispatchAsync(restarted,
                new ProjectionDispatchRequest(new ProjectionRequest("tenant-a", "widget", owner, [next]),
                    [handler.ProjectionType], "growth-" + owner, interrupted.CatalogFingerprint), new ProjectionDispatchOptions(),
                new DomainProjectionIdentityOptions { AppId = "widget-service", ServiceVersion = "v1" }, CancellationToken.None);
            growth.Outcomes.Single().Status.ShouldBe(ProjectionDispatchStatus.Completed, growth.ToString());
            (await coordinator.ReadAsync<PayloadRow>(scope, "row:" + owner)).Value!.Value.ShouldBe(Convert.ToHexString(SHA256.HashData([9])));
            (await coordinator.GetCheckpointAsync(scope, ((IAsyncDomainSharedProjectionEpochHandler)handler).SourceStreamKey("tenant-a", owner)))!.Position.ShouldBe(2);
        }
        string sessionKey = DomainSharedProjectionRebuildSessionKey.Compute(interrupted);
        store.SnapshotRaw(StoreName, DomainSharedProjectionRebuildDispatcher.PartitionChunkKey(sessionKey, 0, 0)).ShouldBeNull();
        DomainSharedProjectionRebuildIdentity fresh = Identity("fresh-after-abort");
        ProjectionEventDto captured = Event([9]) with { GlobalPosition = 2, MessageId = "next-owner-b" };
        string inventory = DomainSharedProjectionRebuildFingerprint.AppendInventory(DomainSharedProjectionRebuildFingerprint.EmptyInventory, 0,
            DomainSharedProjectionRebuildFingerprint.ComputeHistory("owner-b", false, [captured]));
        (await RebuildAsync(restarted, new(DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Begin, fresh,
            CaptureInventoryFingerprint: inventory, SourceHighWatermarks: new Dictionary<string, long> { ["owner-b"] = 2 })))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        current = await AccumulateAsync(restarted, fresh, 0, ("owner-b", captured));
        (await FinishAsync(restarted, fresh, current)).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
    }
}
