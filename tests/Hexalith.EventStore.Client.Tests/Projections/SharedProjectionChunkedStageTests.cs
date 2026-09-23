using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Fakes;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

public sealed class SharedProjectionChunkedStageTests
{
    private static readonly SharedProjectionScope s_scope = new(
        "statestore",
        "tenant-a",
        "widget",
        "widget-index",
        ["ordinary-dispatch"]);

    [Fact]
    public async Task TenThousandAggregateManifestPromotesOneGenerationAndSealsChunks()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(s_scope, "ordinary-dispatch");
        _ = await coordinator.BeginAsync(s_scope, "large-stage", "inventory-10000", new Dictionary<string, long>());
        ReadModelBatchOperation[] manifest = [.. Enumerable.Range(0, 10_000).Select(index =>
            ReadModelBatchOperation.Write("item-" + index.ToString("D5"), new Counter(index), ReadModelBatchConcurrency.LastWrite))];

        _ = await coordinator.StageAsync(s_scope, "large-stage", manifest);
        SharedProjectionEpochState staged = store.Snapshot<SharedProjectionEpochState>(s_scope.StoreName, s_scope.StateKey)!;
        staged.StageChunks.ShouldNotBeNull();
        staged.StageMutations.ShouldBeNull();
        JsonSerializer.SerializeToUtf8Bytes(staged).Length.ShouldBeLessThan(64 * 1024);
        (await coordinator.ReadAsync<Counter>(s_scope, "item-09999")).Value.ShouldBeNull();

        SharedProjectionChunkReference reference = staged.StageChunks!;
        await coordinator.CommitAsync(s_scope, "large-stage");
        var restarted = new SharedProjectionEpochCoordinator(store, store);
        SharedProjectionReadResult<Counter> first = await restarted.ReadAsync<Counter>(s_scope, "item-00000");
        SharedProjectionReadResult<Counter> last = await restarted.ReadAsync<Counter>(s_scope, "item-09999");
        first.Generation.ShouldBe(1);
        first.Value!.Value.ShouldBe(0);
        last.Generation.ShouldBe(1);
        last.Value!.Value.ShouldBe(9999);
        store.Snapshot<SharedProjectionEpochState>(s_scope.StoreName, s_scope.StateKey)!.CleanupChunks.ShouldBeNull();
        _ = await Should.ThrowAsync<InvalidOperationException>(() => new SharedProjectionChunkStore(store)
            .ReadAsync(s_scope, reference, CancellationToken.None));
    }

    [Fact]
    public async Task InterruptedChunkedStageAbortsAndCannotBeRecreatedByDelayedWriter()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(s_scope, "ordinary-dispatch");
        _ = await coordinator.BeginAsync(s_scope, "aborted-large-stage", "inventory", new Dictionary<string, long>());
        ReadModelBatchOperation[] manifest = [.. Enumerable.Range(0, 300).Select(index =>
            ReadModelBatchOperation.Write("item-" + index.ToString("D5"), new Counter(index), ReadModelBatchConcurrency.LastWrite))];
        bool failed = false;
        store.BatchFaultHook = (phase, _, _) =>
        {
            if (phase == ReadModelBatchPhase.BeforeCommit && !failed)
            {
                failed = true;
                throw new InvalidOperationException("stage interrupted");
            }

            return Task.CompletedTask;
        };
        _ = await Should.ThrowAsync<InvalidOperationException>(() => coordinator.StageAsync(s_scope, "aborted-large-stage", manifest));
        SharedProjectionChunkReference reference = store
            .Snapshot<SharedProjectionEpochState>(s_scope.StoreName, s_scope.StateKey)!.StageChunks!;

        store.BatchFaultHook = null;
        var restarted = new SharedProjectionEpochCoordinator(store, store);
        await restarted.AbortAsync(
            s_scope,
            "aborted-large-stage",
            (_, _, _) => Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>([]));
        (await restarted.ReadGenerationAsync<Counter>(s_scope, 1, "item-00000")).Value.ShouldBeNull();
        (await restarted.ReadGenerationAsync<Counter>(s_scope, 1, "item-00299")).Value.ShouldBeNull();
        (await restarted.ReadAsync<Counter>(s_scope, "item-00000")).Generation.ShouldBe(0);
        _ = await Should.ThrowAsync<InvalidOperationException>(() => new SharedProjectionChunkStore(store)
            .WriteAsync(s_scope, reference, JsonSerializer.SerializeToUtf8Bytes(
                manifest.Select(SharedProjectionMutation.FromOperation).ToArray()), CancellationToken.None));
    }

    private sealed record Counter(int Value);
}
