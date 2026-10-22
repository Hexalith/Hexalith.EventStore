using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Fakes;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

public sealed class SharedProjectionFoldFailureTests
{
    private static readonly SharedProjectionScope s_scope = new(
        "statestore", "tenant-a", "widget", "widget-index", ["ordinary-dispatch"]);

    [Fact]
    public async Task RetryCountSurvivesRestart_ThenPreparedParkingRecoversWithoutRefolding()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        SharedProjectionLease lease = await coordinator.RegisterWriterAsync(s_scope, "ordinary-dispatch");
        SharedProjectionDelivery delivery = Delivery(10);
        (await coordinator.JournalAsync(s_scope, lease, delivery)).ShouldBe(SharedProjectionJournalResult.Journaled);

        SharedProjectionRetryableFoldException first = await Should.ThrowAsync<SharedProjectionRetryableFoldException>(
            () => coordinator.CatchUpAsync(s_scope, FailAt(7, 3)));
        first.Failure.ShouldBe(new SharedProjectionFailureStatus("widget-a", 7, 1));
        (await coordinator.GetCheckpointAsync(s_scope, "widget-a"))!.Position.ShouldBe(10);
        (await coordinator.GetStatusAsync(s_scope)).PendingDeliveryCount.ShouldBe(1);
        (await coordinator.ReadGenerationAsync<Parking>(s_scope, 0, "parking")).Value.ShouldBeNull();

        var restarted = new SharedProjectionEpochCoordinator(store, store);
        SharedProjectionRetryableFoldException second = await Should.ThrowAsync<SharedProjectionRetryableFoldException>(
            () => restarted.CatchUpAsync(s_scope, FailAt(7, 3)));
        second.Failure.ShouldBe(new SharedProjectionFailureStatus("widget-a", 7, 2));

        bool faulted = false;
        store.BatchFaultHook = (phase, _, _) =>
        {
            if (phase == ReadModelBatchPhase.BeforeCommit && !faulted)
            {
                faulted = true;
                throw new InvalidOperationException("injected parking batch failure");
            }

            return Task.CompletedTask;
        };
        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => restarted.CatchUpAsync(s_scope, FailAt(7, 3)));
        ReadModelEntry<SharedProjectionEpochState> pending = await store
            .GetAsync<SharedProjectionEpochState>(s_scope.StoreName, s_scope.StateKey);
        pending.Value!.Journal.Single().Failure.ShouldBe(new SharedProjectionFailureStatus("widget-a", 7, 3));
        pending.Value.Journal.Single().Prepared.ShouldNotBeNull();
        (await restarted.ReadGenerationAsync<Parking>(s_scope, 0, "parking")).Value.ShouldBeNull();

        store.BatchFaultHook = null;
        var recovered = new SharedProjectionEpochCoordinator(store, store);
        int refolds = 0;
        (await recovered.CatchUpAsync(s_scope, (_, _, _) =>
        {
            refolds++;
            throw new InvalidOperationException("prepared parking must not be folded again");
        })).ShouldBe(1);
        refolds.ShouldBe(0);
        SharedProjectionReadResult<Parking> parked = await recovered.ReadAsync<Parking>(s_scope, "parking");
        parked.IsAvailable.ShouldBeTrue();
        parked.IsStale.ShouldBeFalse();
        parked.Value.ShouldBe(new Parking(7, 3, true));
        (await recovered.GetStatusAsync(s_scope)).PendingDeliveryCount.ShouldBe(0);
        ReadModelEntry<SharedProjectionDeliveryReceipt> receipt = await store
            .GetAsync<SharedProjectionDeliveryReceipt>(
                s_scope.StoreName, s_scope.ReceiptKey(0, "widget-a", 10));
        receipt.Value!.Parked.ShouldBeTrue();
        receipt.Value.Failure.ShouldBe(new SharedProjectionFailureStatus("widget-a", 7, 3));
        (await recovered.JournalAsync(s_scope, lease, delivery)).ShouldBe(SharedProjectionJournalResult.AlreadyJournaled);
        (await recovered.JournalAsync(s_scope, lease, delivery with { CanonicalPayload = [9] }))
            .ShouldBe(SharedProjectionJournalResult.IdentityConflict);
        (await recovered.ReadAsync<Parking>(s_scope, "parking")).Value.ShouldBe(new Parking(7, 3, true));
    }

    [Fact]
    public async Task DifferentFailedSourcePosition_ResetsConsecutiveCount()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        SharedProjectionLease lease = await coordinator.RegisterWriterAsync(s_scope, "ordinary-dispatch");
        _ = await coordinator.JournalAsync(s_scope, lease, Delivery(10));

        (await Should.ThrowAsync<SharedProjectionRetryableFoldException>(
            () => coordinator.CatchUpAsync(s_scope, FailAt(7, 2)))).Failure.FailureCount.ShouldBe(1);
        SharedProjectionRetryableFoldException changed = await Should.ThrowAsync<SharedProjectionRetryableFoldException>(
            () => coordinator.CatchUpAsync(s_scope, FailAt(8, 2)));
        changed.Failure.ShouldBe(new SharedProjectionFailureStatus("widget-a", 8, 1));

        var restarted = new SharedProjectionEpochCoordinator(store, store);
        (await restarted.CatchUpAsync(s_scope, FailAt(8, 2))).ShouldBe(1);
        (await restarted.ReadAsync<Parking>(s_scope, "parking")).Value.ShouldBe(new Parking(8, 2, true));
    }

    [Fact]
    public async Task SuccessfulRetry_ClearsPendingHeadAndRecordsAppliedDisposition()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        SharedProjectionLease lease = await coordinator.RegisterWriterAsync(s_scope, "ordinary-dispatch");
        _ = await coordinator.JournalAsync(s_scope, lease, Delivery(10));
        _ = await Should.ThrowAsync<SharedProjectionRetryableFoldException>(
            () => coordinator.CatchUpAsync(s_scope, FailAt(7, 3)));

        var restarted = new SharedProjectionEpochCoordinator(store, store);
        (await restarted.CatchUpAsync(s_scope, (_, _, _) => Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>(
            [ReadModelBatchOperation.Write("healthy", new Parking(7, 1, false), ReadModelBatchConcurrency.LastWrite)])))
            .ShouldBe(1);
        (await restarted.ReadAsync<Parking>(s_scope, "healthy")).Value.ShouldBe(new Parking(7, 1, false));
        (await restarted.ReadAsync<Parking>(s_scope, "parking")).Value.ShouldBeNull();
        ReadModelEntry<SharedProjectionDeliveryReceipt> receipt = await store
            .GetAsync<SharedProjectionDeliveryReceipt>(
                s_scope.StoreName, s_scope.ReceiptKey(0, "widget-a", 10));
        receipt.Value!.Parked.ShouldBeFalse();
        receipt.Value.Failure.ShouldBe(new SharedProjectionFailureStatus("widget-a", 7, 1));
    }

    [Fact]
    public async Task FailureForAnotherSourceStream_DoesNotChangeTheJournal()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        SharedProjectionLease lease = await coordinator.RegisterWriterAsync(s_scope, "ordinary-dispatch");
        _ = await coordinator.JournalAsync(s_scope, lease, Delivery(10));

        _ = await Should.ThrowAsync<InvalidOperationException>(() => coordinator.CatchUpAsync(
            s_scope,
            (_, _, _) => throw new SharedProjectionFoldFailureException(
                "another-widget", 7, 2, count => [ParkingWrite(7, count)])));
        ReadModelEntry<SharedProjectionEpochState> state = await store
            .GetAsync<SharedProjectionEpochState>(s_scope.StoreName, s_scope.StateKey);
        state.Value!.Journal.Single().Failure.ShouldBeNull();
        state.Value.Journal.Single().Prepared.ShouldBeNull();
        (await coordinator.ReadGenerationAsync<Parking>(s_scope, 0, "parking")).Value.ShouldBeNull();
    }

    private static SharedProjectionDelivery Delivery(long position)
        => new("widget-a", position, [1], [2]);

    private static Func<SharedProjectionDelivery, long, CancellationToken, Task<IReadOnlyList<ReadModelBatchOperation>>> FailAt(
        long failedPosition,
        int retryLimit)
        => (_, _, _) => throw new SharedProjectionFoldFailureException(
            "widget-a", failedPosition, retryLimit, count => [ParkingWrite(failedPosition, count)]);

    private static ReadModelBatchOperation ParkingWrite(long position, int count)
        => ReadModelBatchOperation.Write(
            "parking", new Parking(position, count, true), ReadModelBatchConcurrency.LastWrite);

    private sealed record Parking(long FailedPosition, int FailureCount, bool Parked);
}
