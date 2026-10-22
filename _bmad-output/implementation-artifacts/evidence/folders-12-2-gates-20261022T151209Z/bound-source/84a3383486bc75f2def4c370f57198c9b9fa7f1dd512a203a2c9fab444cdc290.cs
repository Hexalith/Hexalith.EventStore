using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Fakes;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

public sealed class SharedProjectionChunkedDeliveryTests
{
    private static readonly SharedProjectionScope s_scope = new(
        "statestore",
        "tenant-a",
        "widget",
        "widget-index",
        ["ordinary-dispatch"]);

    [Fact]
    public async Task LargeDeliverySurvivesRestartAndDrainsToPersistedProjection()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        SharedProjectionLease lease = await coordinator.RegisterWriterAsync(s_scope, "ordinary-dispatch");
        byte[] body = Enumerable.Range(0, 2 * 1024 * 1024).Select(index => (byte)(index % 251)).ToArray();
        var delivery = new SharedProjectionDelivery("widget-a", 1, body, JsonSerializer.SerializeToUtf8Bytes(1L));

        (await coordinator.JournalAsync(s_scope, lease, delivery)).ShouldBe(SharedProjectionJournalResult.Journaled);
        SharedProjectionEpochState journaled = store.Snapshot<SharedProjectionEpochState>(s_scope.StoreName, s_scope.StateKey)!;
        journaled.Journal[0].PayloadReady.ShouldBeTrue();
        journaled.Journal[0].CanonicalPayload.ShouldBeEmpty();
        journaled.Journal[0].PayloadChunks.ShouldNotBeNull();
        JsonSerializer.SerializeToUtf8Bytes(journaled).Length.ShouldBeLessThan(64 * 1024);
        SharedProjectionChunkReference reference = journaled.Journal[0].PayloadChunks!;

        var restarted = new SharedProjectionEpochCoordinator(store, store);
        (await restarted.CatchUpAsync(s_scope, (actual, _, _) =>
        {
            actual.CanonicalPayload.ShouldBe(body);
            return Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>(
                [ReadModelBatchOperation.Write("index", new Counter(1), ReadModelBatchConcurrency.LastWrite)]);
        })).ShouldBe(1);
        (await restarted.ReadAsync<Counter>(s_scope, "index")).Value!.Value.ShouldBe(1);
        (await restarted.GetCheckpointAsync(s_scope, "widget-a"))!.Position.ShouldBe(1);
        (await restarted.JournalAsync(s_scope, lease, delivery)).ShouldBe(SharedProjectionJournalResult.AlreadyJournaled);
        (await restarted.JournalAsync(s_scope, lease, delivery with { CanonicalPayload = [1, 2, 3] }))
            .ShouldBe(SharedProjectionJournalResult.IdentityConflict);
        _ = await Should.ThrowAsync<InvalidOperationException>(() => new SharedProjectionChunkStore(store)
            .ReadAsync(s_scope, reference, CancellationToken.None));
    }

    [Fact]
    public async Task CrashAfterReservationRequiresRedeliveryBeforeCheckpointAndCatchUp()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        SharedProjectionLease lease = await coordinator.RegisterWriterAsync(s_scope, "ordinary-dispatch");
        var delivery = new SharedProjectionDelivery("widget-a", 1, new byte[100_000], [1]);
        int writes = 0;
        store.ConcurrentWriteBeforeTrySave = () =>
        {
            if (Interlocked.Increment(ref writes) == 2)
            {
                throw new InvalidOperationException("crash before chunk write");
            }
        };
        _ = await Should.ThrowAsync<InvalidOperationException>(() => coordinator.JournalAsync(s_scope, lease, delivery));
        store.ConcurrentWriteBeforeTrySave = null;

        SharedProjectionEpochState pending = store.Snapshot<SharedProjectionEpochState>(s_scope.StoreName, s_scope.StateKey)!;
        pending.Journal.Length.ShouldBe(1);
        pending.Journal[0].PayloadReady.ShouldBeFalse();
        (await coordinator.GetCheckpointAsync(s_scope, "widget-a")).ShouldBeNull();
        _ = await Should.ThrowAsync<InvalidOperationException>(() => coordinator.CatchUpAsync(
            s_scope,
            (_, _, _) => Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>([])));

        var restarted = new SharedProjectionEpochCoordinator(store, store);
        (await restarted.JournalAsync(s_scope, lease, delivery)).ShouldBe(SharedProjectionJournalResult.Journaled);
        (await restarted.GetCheckpointAsync(s_scope, "widget-a"))!.Position.ShouldBe(1);
        (await restarted.CatchUpAsync(s_scope, (_, _, _) => Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>(
            [ReadModelBatchOperation.Write("index", new Counter(1), ReadModelBatchConcurrency.LastWrite)]))).ShouldBe(1);
        (await restarted.ReadAsync<Counter>(s_scope, "index")).Value!.Value.ShouldBe(1);
    }

    [Fact]
    public async Task TenThousandPositionHistoryKeepsEpochDocumentBounded()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        SharedProjectionLease lease = await coordinator.RegisterWriterAsync(s_scope, "ordinary-dispatch");
        for (int start = 1; start <= 10_000; start += 64)
        {
            int end = Math.Min(10_000, start + 63);
            for (int position = start; position <= end; position++)
            {
                var delivery = new SharedProjectionDelivery("widget-a", position, [1], BitConverter.GetBytes(position));
                (await coordinator.JournalAsync(s_scope, lease, delivery)).ShouldBe(SharedProjectionJournalResult.Journaled);
            }

            (await coordinator.CatchUpAsync(
                s_scope,
                (_, _, _) => Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>([])))
                .ShouldBe(end - start + 1);
        }

        var restarted = new SharedProjectionEpochCoordinator(store, store);
        (await restarted.GetCheckpointAsync(s_scope, "widget-a"))!.Position.ShouldBe(10_000);
        SharedProjectionEpochState final = store.Snapshot<SharedProjectionEpochState>(s_scope.StoreName, s_scope.StateKey)!;
        final.Journal.ShouldBeEmpty();
        final.ConsumerCheckpoints.ShouldBeEmpty();
        JsonSerializer.SerializeToUtf8Bytes(final).Length.ShouldBeLessThan(64 * 1024);
        (await restarted.JournalAsync(s_scope, lease, new SharedProjectionDelivery(
            "widget-a", 1, [1], BitConverter.GetBytes(1)))).ShouldBe(SharedProjectionJournalResult.AlreadyJournaled);
    }

    private sealed record Counter(int Value);
}
