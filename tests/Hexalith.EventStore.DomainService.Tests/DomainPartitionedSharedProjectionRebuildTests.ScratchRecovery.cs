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
    [InlineData(ReadModelBatchPhase.AfterInstallOperation, 0, false, false)]
    [InlineData(ReadModelBatchPhase.AfterInstallOperation, 1, false, false)]
    [InlineData(ReadModelBatchPhase.BeforeCommit, -1, false, false)]
    [InlineData(ReadModelBatchPhase.AfterCommit, -1, false, false)]
    [InlineData(ReadModelBatchPhase.BeforeCompaction, 0, false, false)]
    [InlineData(ReadModelBatchPhase.RecoveryManifestReclaimed, 1, false, false)]
    [InlineData(ReadModelBatchPhase.AfterInstallOperation, 0, true, false)]
    [InlineData(ReadModelBatchPhase.BeforeCommit, -1, true, false)]
    [InlineData(ReadModelBatchPhase.AfterInstallOperation, 0, false, true)]
    [InlineData(ReadModelBatchPhase.BeforeCommit, -1, true, true)]
    public async Task InterruptedAccumulateRetryOrDirectAbortPreservesSelectionAndPermitsFreshRebuild(
        ReadModelBatchPhase failure, int failureOrdinal, bool retryFirst, bool global)
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
        _ = await BeginAsync(first, original, history);
        DomainSharedProjectionRebuildResponse current = await AccumulateAsync(first, original, 0, history[0]);
        (await FinishAsync(first, original, current)).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        string selected = (await coordinator.ReadAsync<PayloadRow>(scope, "row:owner-a")).Value!.Value;
        long selectedGeneration = (await coordinator.GetStatusAsync(scope)).ActiveGeneration;

        DomainSharedProjectionRebuildIdentity interrupted = Identity("interrupted-scratch");
        (string Owner, ProjectionEventDto Event)[] replacement = [history[0], ("owner-b", Event([2]) with { GlobalPosition = 1 })];
        (await BeginAsync(first, interrupted, replacement)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        store.BatchFaultHook = (phase, ordinal, _) => phase == failure && ordinal == failureOrdinal
            ? throw new InvalidOperationException("scratch process stop") : Task.CompletedTask;
        (await AccumulateAsync(first, interrupted, 0, replacement[0])).Status.ShouldBe(ProjectionDispatchStatus.Indeterminate);
        store.BatchFaultHook = null;
        coordinator = new SharedProjectionEpochCoordinator(store, store);
        using ServiceProvider restarted = Provider(store, store, coordinator, handler);
        (await coordinator.ReadAsync<PayloadRow>(scope, "row:owner-a")).Value!.Value.ShouldBe(selected);
        (await coordinator.ReadAsync<PayloadRow>(scope, "row:owner-b")).Value.ShouldBeNull();
        if (retryFirst)
        {
            (await AccumulateAsync(restarted, interrupted, 0, replacement[0])).Status.ShouldBe(ProjectionDispatchStatus.Completed);
            (await AccumulateAsync(restarted, interrupted, 0, replacement[0])).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        }

        // Owner-sequenced named deliveries retain their retry work while the Building fence is active.
        foreach (string owner in new[] { "owner-a", "owner-b" })
        {
            ProjectionEventDto next = Event([9]) with { GlobalPosition = 2, SequenceNumber = owner == "owner-a" ? 2 : 1, MessageId = "next-" + owner };
            ProjectionDispatchResponse delivery = await DomainProjectionDispatcher.DispatchAsync(restarted,
                new ProjectionDispatchRequest(new ProjectionRequest("tenant-a", "widget", owner, [next]),
                    [handler.ProjectionType], "growth-" + owner, interrupted.CatalogFingerprint), new ProjectionDispatchOptions(),
                new DomainProjectionIdentityOptions { AppId = "widget-service", ServiceVersion = "v1" }, CancellationToken.None);
            delivery.Outcomes.Single().Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        }
        _ = await RebuildAsync(restarted, Lifecycle(interrupted with { OperationId = "foreign" }, DomainSharedProjectionRebuildAction.Abort));
        (await coordinator.GetStatusAsync(scope)).OperationId.ShouldBe(interrupted.OperationId);
        (await coordinator.GetStatusAsync(scope)).IsBuilding.ShouldBeTrue();
        DomainSharedProjectionRebuildResponse aborted = await RebuildAsync(restarted, Lifecycle(interrupted, DomainSharedProjectionRebuildAction.Abort));
        aborted.Phase.ShouldBe(DomainSharedProjectionRebuildPhase.Aborted, aborted.ToString());
        aborted.Status.ShouldBe(ProjectionDispatchStatus.Completed, aborted.ToString());
        (await RebuildAsync(restarted, Lifecycle(interrupted, DomainSharedProjectionRebuildAction.Abort))).Phase.ShouldBe(DomainSharedProjectionRebuildPhase.Aborted);
        SharedProjectionEpochStatus status = await coordinator.GetStatusAsync(scope);
        status.IsBuilding.ShouldBeFalse();
        status.IsAborting.ShouldBeFalse();
        status.ActiveGeneration.ShouldBe(selectedGeneration);
        status.PendingDeliveryCount.ShouldBe(0);
        (await coordinator.ReadAsync<PayloadRow>(scope, "row:owner-a")).Value!.Value.ShouldBe(selected);
        (await coordinator.ReadAsync<PayloadRow>(scope, "row:owner-b")).Value.ShouldBeNull();
        foreach (string owner in new[] { "owner-a", "owner-b" })
        {
            ProjectionEventDto next = Event([9]) with { GlobalPosition = 2, SequenceNumber = owner == "owner-a" ? 2 : 1, MessageId = "next-" + owner };
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
