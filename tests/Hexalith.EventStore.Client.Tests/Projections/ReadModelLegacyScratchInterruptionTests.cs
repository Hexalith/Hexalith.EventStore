using System.Text.Json.Nodes;
using Hexalith.EventStore.Client.Projections;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

public sealed class ReadModelLegacyScratchInterruptionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompensatorRetainsDiscoverableFenceUntilPrivateScratchSettlementAndCleansEvidence(bool casRace)
    {
        using var client = new RecordingDaprClient { PersistTypedState = true };
        var store = new DaprReadModelStore(client);
        var scope = new SharedProjectionScope("statestore", "tenant", "widget", "index", ["writer"]);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(scope, "writer");
        _ = await coordinator.BeginAsync(scope, "legacy", "inventory", new Dictionary<string, long>());
        ReadModelEntry<SharedProjectionEpochState> state = await store.GetAsync<SharedProjectionEpochState>(scope.StoreName, scope.StateKey);
        var batch = new ReadModelBatch(new(scope.StoreName, scope.TenantId, scope.Domain, scope.Family, scope.Family, "legacy-scratch"),
            [ReadModelBatchOperation.Write("private", new JsonObject { ["count"] = 1 }, ReadModelBatchConcurrency.CreateOnly),
                ReadModelBatchOperation.Write(scope.StateKey, state.Value!, ReadModelBatchConcurrency.Match(state.ETag!))]);
        var protocol = new ReadModelBatchProtocol(new DaprReadModelBatchStateAccessor(client, scope.StoreName), new ReadModelBatchOptions(), NullLogger.Instance);
        await Should.ThrowAsync<InvalidOperationException>(() => protocol.ExecuteAsync(batch,
            new DelegatingBatchFaultInjector((phase, _, _) => phase == ReadModelBatchPhase.BeforeCommit ? throw new InvalidOperationException("stop") : Task.CompletedTask), CancellationToken.None));
        client.TryDeleteByteOverride = key => key == "private" ? casRace ? false : throw new IOException("compensator stopped") : null;
        ReadModelPendingBatchRecoveryResult first = await store.RecoverPendingAsync(batch.Scope, "private");
        first.IsResolved.ShouldBeFalse();
        ReadModelBatchEnvelope.IsEnvelope(client.ByteStoreValue(scope.StateKey)).ShouldBeTrue();
        client.TryDeleteByteOverride = null;
        (await store.RecoverPendingAsync(batch.Scope, "private")).IsResolved.ShouldBeTrue();
        (await store.RecoverPendingAsync(batch.Scope, "private")).IsResolved.ShouldBeTrue();
        client.ByteStoreContains("private").ShouldBeFalse();
        (await store.GetAsync<ReadModelBatchMarker>(scope.StoreName, batch.Scope.ComputeMarkerKey())).Value!.Status.ShouldBe(ReadModelBatchMarkerStatus.Aborted);
        await new SharedProjectionEpochCoordinator(store, store).AbortAsync(scope, "legacy", (_, _, _) => Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>([]));
        (await store.GetAsync<SharedProjectionEpochState>(scope.StoreName, scope.StateKey)).Value!.Phase.ShouldBe(SharedProjectionEpochPhase.Open);
        byte[] opened = client.ByteStoreValue(scope.StateKey);
        await new SharedProjectionEpochCoordinator(store, store).ReclaimScratchReceiptAsync(scope, batch.Scope.BatchId);
        client.ByteStoreContains(ReadModelBatchKeys.ScratchCompensationKey(batch.Scope.ComputeScopeHash())).ShouldBeFalse();
        client.ByteStoreContains(batch.Scope.ComputeMarkerKey()).ShouldBeFalse();
        client.ByteStoreValue(scope.StateKey).ShouldBe(opened);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FinalFenceRestoreInterruptionAllowsReplacementAbortAndPreservesItsNewerFence(bool lostResponse)
    {
        using var client = new RecordingDaprClient { PersistTypedState = true };
        var store = new DaprReadModelStore(client);
        var scope = new SharedProjectionScope("statestore", "tenant", "widget", "index", ["writer"]);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(scope, "writer");
        _ = await coordinator.BeginAsync(scope, "legacy", "inventory", new Dictionary<string, long>());
        ReadModelEntry<SharedProjectionEpochState> state = await store.GetAsync<SharedProjectionEpochState>(scope.StoreName, scope.StateKey);
        var batch = new ReadModelBatch(new(scope.StoreName, scope.TenantId, scope.Domain, scope.Family, scope.Family, "legacy-scratch"),
            [ReadModelBatchOperation.Write("private", new JsonObject { ["count"] = 1 }, ReadModelBatchConcurrency.CreateOnly),
                ReadModelBatchOperation.Write(scope.StateKey, state.Value!, ReadModelBatchConcurrency.Match(state.ETag!))]);
        var protocol = new ReadModelBatchProtocol(new DaprReadModelBatchStateAccessor(client, scope.StoreName), new ReadModelBatchOptions(), NullLogger.Instance);
        await Should.ThrowAsync<InvalidOperationException>(() => protocol.ExecuteAsync(batch,
            new DelegatingBatchFaultInjector((phase, _, _) => phase == ReadModelBatchPhase.BeforeCommit ? throw new InvalidOperationException("stop") : Task.CompletedTask), CancellationToken.None));
        Action<string, ReadOnlyMemory<byte>> stop = (key, bytes) =>
        {
            if (key == scope.StateKey && !ReadModelBatchEnvelope.IsEnvelope(bytes.Span)) throw new IOException("final fence restore interrupted");
        };
        if (lostResponse) client.AfterTrySaveByteState = stop; else client.BeforeTrySaveByteState = stop;
        (await store.RecoverPendingAsync(batch.Scope, scope.StateKey)).IsResolved.ShouldBeFalse();
        client.ByteStoreContains("private").ShouldBeFalse("the last fence cannot disappear while private compensation remains");
        client.BeforeTrySaveByteState = null; client.AfterTrySaveByteState = null;
        await new SharedProjectionEpochCoordinator(store, store).AbortAsync(scope, "legacy", (_, _, _) => Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>([]));
        byte[] opened = client.ByteStoreValue(scope.StateKey);
        (await store.RecoverPendingAsync(batch.Scope, scope.StateKey)).IsResolved.ShouldBeTrue();
        await new SharedProjectionEpochCoordinator(store, store).ReclaimScratchReceiptAsync(scope, batch.Scope.BatchId);
        client.ByteStoreValue(scope.StateKey).ShouldBe(opened);
        client.ByteStoreContains(ReadModelBatchKeys.ScratchCompensationKey(batch.Scope.ComputeScopeHash())).ShouldBeFalse();
    }

}
