using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Fakes;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

public sealed class SharedProjectionChunkedCaptureTests
{
    private static readonly SharedProjectionScope s_scope = new(
        "statestore",
        "tenant-a",
        "widget",
        "widget-index",
        ["ordinary-dispatch"]);

    [Fact]
    public async Task TenThousandHighWatermarksRemainBoundedAndSelectCommittedGeneration()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(s_scope, "ordinary-dispatch");
        Dictionary<string, long> capture = Enumerable.Range(0, 10_000)
            .ToDictionary(index => "widget-" + index.ToString("D5"), _ => 7L, StringComparer.Ordinal);

        (await coordinator.BeginAsync(s_scope, "large-capture", "inventory", capture)).ShouldBe(1);
        SharedProjectionEpochState building = store.Snapshot<SharedProjectionEpochState>(s_scope.StoreName, s_scope.StateKey)!;
        building.CaptureChunks.ShouldNotBeNull();
        building.CaptureHighWatermarks.ShouldBeEmpty();
        JsonSerializer.SerializeToUtf8Bytes(building).Length.ShouldBeLessThan(64 * 1024);
        SharedProjectionLease lease = await coordinator.RefreshLeaseAsync(s_scope, "ordinary-dispatch");
        var captured = new SharedProjectionDelivery("widget-09999", 7, [7], [7]);
        (await coordinator.JournalAsync(s_scope, lease, captured)).ShouldBe(SharedProjectionJournalResult.Backpressure);

        await coordinator.StageAsync(s_scope, "large-capture", [ReadModelBatchOperation.Write(
            "index", new Counter(7), ReadModelBatchConcurrency.LastWrite)]);
        await coordinator.CommitAsync(s_scope, "large-capture");
        var restarted = new SharedProjectionEpochCoordinator(store, store);
        (await restarted.JournalAsync(s_scope, lease, captured)).ShouldBe(SharedProjectionJournalResult.AlreadyCaptured);
        (await restarted.ReadAsync<Counter>(s_scope, "index")).Value!.Value.ShouldBe(7);
    }

    [Fact]
    public async Task CapturingCrashCanAbortWithoutAcknowledgingOrRetainingChunks()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(s_scope, "ordinary-dispatch");
        Dictionary<string, long> capture = Enumerable.Range(0, 1_500)
            .ToDictionary(index => "widget-" + index.ToString("D5"), _ => 1L, StringComparer.Ordinal);
        int writes = 0;
        store.ConcurrentWriteBeforeTrySave = () =>
        {
            if (Interlocked.Increment(ref writes) == 2)
            {
                throw new InvalidOperationException("crash before capture chunk write");
            }
        };
        _ = await Should.ThrowAsync<InvalidOperationException>(() => coordinator.BeginAsync(s_scope, "crashed-capture", "inventory", capture));
        store.ConcurrentWriteBeforeTrySave = null;
        SharedProjectionEpochState reserved = store.Snapshot<SharedProjectionEpochState>(s_scope.StoreName, s_scope.StateKey)!;
        reserved.Phase.ShouldBe(SharedProjectionEpochPhase.Capturing);
        SharedProjectionChunkReference reference = reserved.CaptureChunks!;

        var restarted = new SharedProjectionEpochCoordinator(store, store);
        SharedProjectionLease lease = await restarted.RefreshLeaseAsync(s_scope, "ordinary-dispatch");
        var delivery = new SharedProjectionDelivery("widget-01499", 1, [1], [1]);
        (await restarted.JournalAsync(s_scope, lease, delivery)).ShouldBe(SharedProjectionJournalResult.Backpressure);
        await restarted.AbortAsync(
            s_scope,
            "crashed-capture",
            (_, _, _) => Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>([]));
        (await restarted.JournalAsync(s_scope, lease, delivery)).ShouldBe(SharedProjectionJournalResult.Journaled);
        (await restarted.CatchUpAsync(s_scope, (_, _, _) => Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>(
            [ReadModelBatchOperation.Write("index", new Counter(1), ReadModelBatchConcurrency.LastWrite)]))).ShouldBe(1);
        (await restarted.ReadAsync<Counter>(s_scope, "index")).Value!.Value.ShouldBe(1);
        _ = await Should.ThrowAsync<InvalidOperationException>(() => new SharedProjectionChunkStore(store)
            .ReadAsync(s_scope, reference, CancellationToken.None));
    }

    private sealed record Counter(int Value);
}
