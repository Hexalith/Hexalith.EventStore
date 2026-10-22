using System.Text.Json;
using System.Text.Json.Nodes;
using Dapr.Client;
using Hexalith.EventStore.Client.Projections;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

public sealed partial class ProjectionExpiryAndScratchRecoveryLiveTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    public async Task RetiredLegacyWorkerPreservesOpenEpochAfterDelayedPhysicalInstallOrCommitCas(bool commitCas, bool erasedByEarlierSdk, bool replacementPrepared)
    {
        string tenant = "legacy-delayed-" + Guid.NewGuid().ToString("N");
        using DaprClient original = Client();
        var store = new DaprReadModelStore(original);
        var scope = new SharedProjectionScope("statestore", tenant, "widget", "family", ["writer"]);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(scope, "writer");
        _ = await coordinator.BeginAsync(scope, "operation", "inventory", new Dictionary<string, long>());
        ReadModelEntry<SharedProjectionEpochState> state = await store.GetAsync<SharedProjectionEpochState>(scope.StoreName, scope.StateKey);
        string scratch = tenant + ":private";
        var batch = new ReadModelBatch(new(scope.StoreName, tenant, scope.Domain, scope.Family, scope.Family, "legacy"),
            [ReadModelBatchOperation.Write(scratch, Value(1), ReadModelBatchConcurrency.CreateOnly),
                ReadModelBatchOperation.Write(scope.StateKey, state.Value!, ReadModelBatchConcurrency.Match(state.ETag!))]);
        var paused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int stopped = 0;
        var delayedAccessor = new DelayedScratchLiveAccessor(new DaprReadModelBatchStateAccessor(original, scope.StoreName), async (key, bytes, token) =>
        {
            bool target = commitCas ? key == batch.Scope.ComputeMarkerKey() && ReadModelBatchMarker.ReadProof(bytes)?.Status == ReadModelBatchMarkerStatus.Committed
                : key == scratch && ReadModelBatchEnvelope.IsEnvelope(bytes.Span);
            if (target && Interlocked.Increment(ref stopped) == 1)
            { paused.TrySetResult(); await release.Task.WaitAsync(token); }
        });
        Task<ReadModelBatchResult> old = new ReadModelBatchProtocol(delayedAccessor, new ReadModelBatchOptions(), NullLogger.Instance)
            .ExecuteAsync(batch, null, TestContext.Current.CancellationToken);
        await paused.Task.WaitAsync(TimeSpan.FromSeconds(15));
        string oldPreparedETag = (await new DaprReadModelBatchStateAccessor(original, scope.StoreName).ReadAsync(batch.Scope.ComputeMarkerKey(), default)).ETag;
        using DaprClient replacement = Client();
        store = new DaprReadModelStore(replacement);
        coordinator = new SharedProjectionEpochCoordinator(store, store);
        if (!commitCas)
        {
            await Should.ThrowAsync<InvalidOperationException>(() => new ReadModelBatchProtocol(
                new DaprReadModelBatchStateAccessor(replacement, scope.StoreName), new ReadModelBatchOptions(), NullLogger.Instance)
                .ExecuteAsync(batch, new RetainedBatchLiveFaultInjector(ReadModelBatchPhase.BeforeCommit, -1), default));
        }
        (await store.RecoverPendingAsync(batch.Scope, scratch)).IsResolved.ShouldBeTrue();
        await coordinator.ReclaimScratchReceiptAsync(scope, batch.Scope.BatchId);
        ReadModelEntry<ReadModelBatchMarker> retired = await store.GetAsync<ReadModelBatchMarker>(scope.StoreName, batch.Scope.ComputeMarkerKey());
        retired.Value!.Status.ShouldBe(ReadModelBatchMarkerStatus.Aborted);
        byte[] heldLegacyMarker = (byte[])(await _redis.GetDatabase().HashGetAsync(Key(batch.Scope.ComputeMarkerKey()), "data"))!;
        if (erasedByEarlierSdk)
            (await store.TryEraseAsync(scope.StoreName, batch.Scope.ComputeMarkerKey(), retired.ETag!)).ShouldBeTrue();
        ReadModelBatchScope? childScope = null;
        byte[]? newerMarker = null, newerScratch = null;
        if (replacementPrepared)
        {
            ReadModelEntry<SharedProjectionEpochState> current = await store.GetAsync<SharedProjectionEpochState>(scope.StoreName, scope.StateKey);
            childScope = batch.Scope with { BatchId = SharedProjectionEpochCoordinator.RetainedLegacyScratchBatchId(batch.Scope) };
            var child = new ReadModelBatch(childScope, [ReadModelBatchOperation.Write(scratch, Value(2), ReadModelBatchConcurrency.CreateOnly),
                ReadModelBatchOperation.Write(scope.StateKey, current.Value!, ReadModelBatchConcurrency.Match(current.ETag!))]) { RetainManifestForRecovery = true };
            await Should.ThrowAsync<InvalidOperationException>(() => new ReadModelBatchProtocol(new DaprReadModelBatchStateAccessor(replacement, scope.StoreName),
                new(), NullLogger.Instance).ExecuteAsync(child, new RetainedBatchLiveFaultInjector(ReadModelBatchPhase.BeforeCommit, -1), default));
            RawStateEntry prepared = await new DaprReadModelBatchStateAccessor(replacement, scope.StoreName).ReadAsync(childScope.ComputeMarkerKey(), default);
            prepared.ETag.ShouldBe(oldPreparedETag, "Redis reuses initial versions; separate SDK identity must prevent marker ABA");
            _ = await coordinator.ReconcileScratchBatchAsync(scope, batch.Scope.BatchId, default);
            newerMarker = (byte[])(await _redis.GetDatabase().HashGetAsync(Key(childScope.ComputeMarkerKey()), "data"))!;
            newerScratch = (byte[])(await _redis.GetDatabase().HashGetAsync(Key(scratch), "data"))!;
        }
        await coordinator.AbortAsync(scope, "operation", (_, _, _) => Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>([]));
        byte[] opened = (byte[])(await _redis.GetDatabase().HashGetAsync(Key(scope.StateKey), "data"))!;
        release.TrySetResult();
        (await old.WaitAsync(TimeSpan.FromSeconds(15))).IsSuccess.ShouldBeFalse();
        ((byte[])(await _redis.GetDatabase().HashGetAsync(Key(scope.StateKey), "data"))!).ShouldBe(opened);
        if (!erasedByEarlierSdk)
            ((byte[])(await _redis.GetDatabase().HashGetAsync(Key(batch.Scope.ComputeMarkerKey()), "data"))!).ShouldBe(heldLegacyMarker);
        else ReadModelBatchMarker.ReadProof((byte[])(await _redis.GetDatabase().HashGetAsync(Key(batch.Scope.ComputeMarkerKey()), "data"))!)!.Status.ShouldBe(ReadModelBatchMarkerStatus.Aborted);
        if (replacementPrepared)
        {
            ((byte[])(await _redis.GetDatabase().HashGetAsync(Key(childScope!.ComputeMarkerKey()), "data"))!).ShouldBe(newerMarker!);
            ((byte[])(await _redis.GetDatabase().HashGetAsync(Key(scratch), "data"))!).ShouldBe(newerScratch!);
            ReadModelEntry<JsonObject> row = await store.GetAsync<JsonObject>(scope.StoreName, scratch);
            (await store.TryEraseAsync(scope.StoreName, scratch, row.ETag!)).ShouldBeTrue();
            await coordinator.ReclaimScratchReceiptAsync(scope, batch.Scope.BatchId);
        }
        (await _redis.GetDatabase().KeyExistsAsync(Key(scratch))).ShouldBeFalse();
        (await store.RecoverPendingAsync(batch.Scope, scratch)).IsResolved.ShouldBeTrue();
        (await coordinator.GetStatusAsync(scope)).IsBuilding.ShouldBeFalse();
        _ = await coordinator.BeginAsync(scope, "fresh", "fresh-inventory", new Dictionary<string, long>());
        await coordinator.AbortAsync(scope, "fresh", (_, _, _) => Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>([]));
        TestContext.Current.TestOutputHelper!.WriteLine($"review3-legacy;commit-cas={commitCas};earlier-erasure={erasedByEarlierSdk};replacement-initial-version-reused={replacementPrepared};newer-marker-and-row-preserved={replacementPrepared};Open-bytes-preserved=true;private-absent=true;fresh-rebuild=true");
    }

    [Fact]
    public async Task ExternalManifestBeforeMarkerCrashRetiresOriginalPreparerAndReclaimsPhysicalPayload()
    {
        string tenant = "external-markerless-" + Guid.NewGuid().ToString("N");
        using DaprClient original = Client();
        var scope = new SharedProjectionScope("statestore", tenant, "widget", "family", ["writer"]);
        string scratch = tenant + ":private";
        var batch = new ReadModelBatch(new(scope.StoreName, tenant, scope.Domain, scope.Family, scope.Family, "external"),
            [ReadModelBatchOperation.Write(scratch, new JsonObject { ["payload"] = new string('x', 20_000) }, ReadModelBatchConcurrency.CreateOnly)]) { RetainManifestForRecovery = true };
        string manifest = ReadModelBatchKeys.ManifestKey(batch.Scope.ComputeScopeHash());
        var paused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int stopped = 0;
        var accessor = new DelayedScratchLiveAccessor(new DaprReadModelBatchStateAccessor(original, scope.StoreName), async (key, _, token) =>
        {
            if (key == batch.Scope.ComputeMarkerKey() && Interlocked.Increment(ref stopped) == 1)
            { paused.TrySetResult(); await release.Task.WaitAsync(token); }
        });
        Task<ReadModelBatchResult> old = new ReadModelBatchProtocol(accessor, new ReadModelBatchOptions(), NullLogger.Instance)
            .ExecuteAsync(batch, null, TestContext.Current.CancellationToken);
        await paused.Task.WaitAsync(TimeSpan.FromSeconds(15));
        (await _redis.GetDatabase().KeyExistsAsync(Key(manifest))).ShouldBeTrue();
        (await _redis.GetDatabase().KeyExistsAsync(Key(batch.Scope.ComputeMarkerKey()))).ShouldBeFalse();
        using DaprClient replacement = Client();
        var store = new DaprReadModelStore(replacement);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        await coordinator.ReclaimScratchReceiptAsync(scope, batch.Scope.BatchId);
        (await _redis.GetDatabase().KeyExistsAsync(Key(manifest))).ShouldBeFalse();
        release.TrySetResult(); (await old.WaitAsync(TimeSpan.FromSeconds(15))).IsSuccess.ShouldBeFalse();
        await coordinator.ReclaimScratchReceiptAsync(scope, batch.Scope.BatchId);
        await coordinator.ReclaimScratchReceiptAsync(scope, batch.Scope.BatchId);
        (await _redis.GetDatabase().KeyExistsAsync(Key(manifest))).ShouldBeFalse();
        (await _redis.GetDatabase().KeyExistsAsync(Key(scratch))).ShouldBeFalse();
        (await _redis.GetDatabase().KeyExistsAsync(Key(batch.Scope.ComputeMarkerKey()))).ShouldBeFalse();
        (await _redis.GetDatabase().KeyExistsAsync(Key(ReadModelBatchTerminalFence.Key(batch.Scope.ComputeScopeHash(), ReadModelBatchFingerprint.Compute(batch))))).ShouldBeTrue();
        TestContext.Current.TestOutputHelper!.WriteLine("review3-external;raw-manifest-before-marker=true;replaced-client=true;physical-private-and-manifest-absent=true;minimal-terminal-fence-retained=true");
    }
}
