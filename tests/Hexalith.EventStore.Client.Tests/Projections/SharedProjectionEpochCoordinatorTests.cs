using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Fakes;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

public sealed class SharedProjectionEpochCoordinatorTests {
    private static readonly SharedProjectionScope s_scope = new(
        "statestore",
        "tenant-a",
        "widget",
        "widget-index",
        ["ordinary-dispatch"]);

    [Fact]
    public async Task ConcurrentBeginAndOrdinaryDelivery_ConvergeWithoutLostWrite() {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        SharedProjectionLease lease = await coordinator.RegisterWriterAsync(s_scope, "ordinary-dispatch");
        (await coordinator.JournalAsync(s_scope, lease, Delivery(1, 1))).ShouldBe(SharedProjectionJournalResult.Journaled);
        (await coordinator.CatchUpAsync(s_scope, Fold(coordinator))).ShouldBe(1);

        using var barrier = new Barrier(2);
        int arrivals = 0;
        store.ConcurrentWriteBeforeTrySave = () => {
            if (Interlocked.Increment(ref arrivals) <= 2) {
                barrier.SignalAndWait(TimeSpan.FromSeconds(5)).ShouldBeTrue();
            }
        };
        Task<long> begin = Task.Run(() => coordinator.BeginAsync(
            s_scope,
            "rebuild-race",
            "inventory-1",
            new Dictionary<string, long> { ["widget-a"] = 1 }));
        Task<SharedProjectionJournalResult> ordinary = Task.Run(() => coordinator.JournalAsync(s_scope, lease, Delivery(2, 1)));
        try {
            _ = await begin;
        }
        catch (InvalidOperationException) {
            (await ordinary).ShouldBe(SharedProjectionJournalResult.Journaled);
            (await coordinator.CatchUpAsync(s_scope, Fold(coordinator))).ShouldBe(1);
            _ = await coordinator.BeginAsync(s_scope, "rebuild-race", "inventory-2", new Dictionary<string, long> { ["widget-a"] = 2 });
        }

        SharedProjectionJournalResult ordinaryResult = await ordinary;
        arrivals.ShouldBeGreaterThanOrEqualTo(2);
        store.ConcurrentWriteBeforeTrySave = null;
        if (ordinaryResult == SharedProjectionJournalResult.StaleLease) {
            SharedProjectionLease refreshed = await coordinator.RefreshLeaseAsync(s_scope, "ordinary-dispatch");
            (await coordinator.JournalAsync(s_scope, refreshed, Delivery(2, 1))).ShouldBe(SharedProjectionJournalResult.Journaled);
        }

        SharedProjectionReadResult<Counter> beforeStage = await coordinator.ReadAsync<Counter>(s_scope, "index");
        beforeStage.IsAvailable.ShouldBeTrue();
        await coordinator.StageAsync(s_scope, "rebuild-race", [Write(beforeStage.Value!.Value)]);
        SharedProjectionReadResult<Counter> beforeCommit = await coordinator.ReadAsync<Counter>(s_scope, "index");
        beforeCommit.Generation.ShouldBe(0);
        beforeCommit.Value!.Value.ShouldBe(beforeStage.Value.Value);

        await coordinator.CommitAsync(s_scope, "rebuild-race");
        _ = await coordinator.CatchUpAsync(s_scope, Fold(coordinator));
        SharedProjectionReadResult<Counter> final = await coordinator.ReadAsync<Counter>(s_scope, "index");
        final.IsAvailable.ShouldBeTrue();
        final.IsStale.ShouldBeFalse();
        final.Generation.ShouldBe(1);
        final.Value!.Value.ShouldBe(2);
    }

    [Fact]
    public async Task CommittedSelector_HidesIncompleteCatchUpAndRestartReplaysPreparedManifest() {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        SharedProjectionLease lease = await coordinator.RegisterWriterAsync(s_scope, "ordinary-dispatch");
        (await coordinator.JournalAsync(s_scope, lease, Delivery(1, 1))).ShouldBe(SharedProjectionJournalResult.Journaled);
        _ = await coordinator.CatchUpAsync(s_scope, Fold(coordinator));
        _ = await coordinator.BeginAsync(s_scope, "rebuild-restart", "inventory-1", new Dictionary<string, long> { ["widget-a"] = 1 });
        lease = await coordinator.RefreshLeaseAsync(s_scope, "ordinary-dispatch");
        (await coordinator.JournalAsync(s_scope, lease, Delivery(2, 2))).ShouldBe(SharedProjectionJournalResult.Journaled);
        SharedProjectionCheckpoint checkpoint = (await coordinator.GetCheckpointAsync(s_scope, "widget-a"))!;
        checkpoint.Position.ShouldBe(2);
        checkpoint.Token.ShouldBe(JsonSerializer.SerializeToUtf8Bytes(2L));
        await coordinator.StageAsync(s_scope, "rebuild-restart", [Write(1)]);
        (await coordinator.ReadAsync<Counter>(s_scope, "index")).Value!.Value.ShouldBe(1);
        await coordinator.CommitAsync(s_scope, "rebuild-restart");

        SharedProjectionReadResult<Counter> midCommit = await coordinator.ReadAsync<Counter>(s_scope, "index");
        midCommit.Generation.ShouldBe(1);
        midCommit.IsAvailable.ShouldBeFalse();
        midCommit.IsStale.ShouldBeTrue();

        bool faulted = false;
        store.BatchFaultHook = (phase, _, _) => {
            if (phase == ReadModelBatchPhase.BeforeCommit && !faulted) {
                faulted = true;
                throw new InvalidOperationException("injected catch-up failure");
            }

            return Task.CompletedTask;
        };
        _ = await Should.ThrowAsync<InvalidOperationException>(() => coordinator.CatchUpAsync(s_scope, Fold(coordinator)));
        (await coordinator.ReadAsync<Counter>(s_scope, "index")).IsAvailable.ShouldBeFalse();

        store.BatchFaultHook = null;
        var restarted = new SharedProjectionEpochCoordinator(store, store);
        (await restarted.GetCheckpointAsync(s_scope, "widget-a"))!.Position.ShouldBe(2);
        (await restarted.CatchUpAsync(s_scope, Fold(restarted))).ShouldBe(1);
        SharedProjectionReadResult<Counter> recovered = await restarted.ReadAsync<Counter>(s_scope, "index");
        recovered.IsAvailable.ShouldBeTrue();
        recovered.IsStale.ShouldBeFalse();
        recovered.Value!.Value.ShouldBe(3);
        (await restarted.JournalAsync(s_scope, lease, Delivery(2, 2))).ShouldBe(SharedProjectionJournalResult.AlreadyJournaled);
        (await restarted.JournalAsync(s_scope, lease, Delivery(2, 9))).ShouldBe(SharedProjectionJournalResult.IdentityConflict);
    }

    [Fact]
    public async Task Abort_DrainsAcceptedDeliveryIntoOldGenerationAndRemovesStaging() {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        SharedProjectionLease lease = await coordinator.RegisterWriterAsync(s_scope, "ordinary-dispatch");
        (await coordinator.JournalAsync(s_scope, lease, Delivery(1, 1))).ShouldBe(SharedProjectionJournalResult.Journaled);
        _ = await coordinator.CatchUpAsync(s_scope, Fold(coordinator));
        _ = await coordinator.BeginAsync(s_scope, "rebuild-abort", "inventory-1", new Dictionary<string, long> { ["widget-a"] = 1 });
        lease = await coordinator.RefreshLeaseAsync(s_scope, "ordinary-dispatch");
        (await coordinator.JournalAsync(s_scope, lease, Delivery(2, 2))).ShouldBe(SharedProjectionJournalResult.Journaled);
        await coordinator.StageAsync(s_scope, "rebuild-abort", [Write(99)]);

        await coordinator.AbortAsync(s_scope, "rebuild-abort", Fold(coordinator));
        SharedProjectionReadResult<Counter> result = await coordinator.ReadAsync<Counter>(s_scope, "index");
        result.Generation.ShouldBe(0);
        result.IsAvailable.ShouldBeTrue();
        result.IsStale.ShouldBeFalse();
        result.Value!.Value.ShouldBe(3);
        (await coordinator.ReadGenerationAsync<Counter>(s_scope, 1, "index")).Value.ShouldBeNull();
        (await coordinator.JournalAsync(s_scope, lease, Delivery(2, 2))).ShouldBe(SharedProjectionJournalResult.AlreadyJournaled);
    }

    [Fact]
    public async Task CapturedPositionIsNotAcknowledgedBeforePromotionAndCanReplayAfterAbort()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(s_scope, "ordinary-dispatch");
        _ = await coordinator.BeginAsync(
            s_scope,
            "capture-abort",
            "inventory-1",
            new Dictionary<string, long> { ["widget-a"] = 1 });
        SharedProjectionLease lease = await coordinator.RefreshLeaseAsync(s_scope, "ordinary-dispatch");

        (await coordinator.JournalAsync(s_scope, lease, Delivery(1, 1)))
            .ShouldBe(SharedProjectionJournalResult.Backpressure);
        await coordinator.StageAsync(s_scope, "capture-abort", [Write(1)]);
        await coordinator.AbortAsync(s_scope, "capture-abort", Fold(coordinator));

        (await coordinator.JournalAsync(s_scope, lease, Delivery(1, 1)))
            .ShouldBe(SharedProjectionJournalResult.Journaled);
        (await coordinator.CatchUpAsync(s_scope, Fold(coordinator))).ShouldBe(1);
        SharedProjectionReadResult<Counter> result = await coordinator.ReadAsync<Counter>(s_scope, "index");
        result.Generation.ShouldBe(0);
        result.Value!.Value.ShouldBe(1);
    }

    [Fact]
    public async Task Begin_RequiresEveryConfiguredWriterAndRejectsStaleLease() {
        var store = new InMemoryReadModelStore();
        var scope = new SharedProjectionScope("statestore", "tenant-a", "widget", "widget-index", ["ordinary", "fan-out"]);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        SharedProjectionLease ordinary = await coordinator.RegisterWriterAsync(scope, "ordinary");
        _ = await Should.ThrowAsync<InvalidOperationException>(() => coordinator.BeginAsync(scope, "rebuild", "inventory", new Dictionary<string, long>()));
        _ = await coordinator.RegisterWriterAsync(scope, "fan-out");
        _ = await coordinator.BeginAsync(scope, "rebuild", "inventory", new Dictionary<string, long>());
        (await coordinator.JournalAsync(scope, ordinary, Delivery(1, 1))).ShouldBe(SharedProjectionJournalResult.StaleLease);
        SharedProjectionLease refreshed = await coordinator.RefreshLeaseAsync(scope, "ordinary");
        (await coordinator.JournalAsync(scope, refreshed, Delivery(1, 1))).ShouldBe(SharedProjectionJournalResult.Journaled);
        SharedProjectionScope changed = scope with { RequiredWriters = ["ordinary"] };
        _ = await Should.ThrowAsync<InvalidOperationException>(() => coordinator.RefreshLeaseAsync(changed, "ordinary"));
    }

    [Fact]
    public async Task MultiKeyRead_PinsOneCommittedGenerationAcrossPromotion() {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(s_scope, "ordinary-dispatch");
        _ = await coordinator.BeginAsync(s_scope, "first", "inventory-0", new Dictionary<string, long>());
        await coordinator.StageAsync(s_scope, "first", [
            ReadModelBatchOperation.Write("left", new Counter(1), ReadModelBatchConcurrency.LastWrite),
            ReadModelBatchOperation.Write("right", new Counter(1), ReadModelBatchConcurrency.LastWrite),
        ]);
        await coordinator.CommitAsync(s_scope, "first");
        SharedProjectionReadResult<IReadOnlyDictionary<string, Counter?>> old = await coordinator.ReadManyAsync<Counter>(
            s_scope,
            ["left", "right"]);
        old.Generation.ShouldBe(1);
        old.Value!["left"]!.Value.ShouldBe(1);
        old.Value["right"]!.Value.ShouldBe(1);

        _ = await coordinator.BeginAsync(s_scope, "second", "inventory-1", new Dictionary<string, long>());
        await coordinator.StageAsync(s_scope, "second", [
            ReadModelBatchOperation.Write("left", new Counter(2), ReadModelBatchConcurrency.LastWrite),
            ReadModelBatchOperation.Write("right", new Counter(2), ReadModelBatchConcurrency.LastWrite),
        ]);
        SharedProjectionReadResult<IReadOnlyDictionary<string, Counter?>> stillOld = await coordinator.ReadManyAsync<Counter>(
            s_scope,
            ["left", "right"]);
        stillOld.Generation.ShouldBe(1);
        stillOld.Value!["left"]!.Value.ShouldBe(1);
        stillOld.Value["right"]!.Value.ShouldBe(1);

        await coordinator.CommitAsync(s_scope, "second");
        SharedProjectionReadResult<IReadOnlyDictionary<string, Counter?>> promoted = await coordinator.ReadManyAsync<Counter>(
            s_scope,
            ["left", "right"]);
        promoted.Generation.ShouldBe(2);
        promoted.Value!["left"]!.Value.ShouldBe(2);
        promoted.Value["right"]!.Value.ShouldBe(2);
    }

    [Fact]
    public async Task Journal_DifferentPayloadCheckpointSplitConflicts() {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        SharedProjectionLease lease = await coordinator.RegisterWriterAsync(s_scope, "ordinary-dispatch");
        var first = new SharedProjectionDelivery("widget-a", 1, [1, 2], [3]);
        var second = new SharedProjectionDelivery("widget-a", 1, [1], [2, 3]);

        (await coordinator.JournalAsync(s_scope, lease, first)).ShouldBe(SharedProjectionJournalResult.Journaled);
        (await coordinator.JournalAsync(s_scope, lease, second)).ShouldBe(SharedProjectionJournalResult.IdentityConflict);
    }

    [Fact]
    public async Task BeginRetry_MustMatchFingerprintAndEveryCapturedWatermark() {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(s_scope, "ordinary-dispatch");
        var capture = new Dictionary<string, long> { ["widget-a"] = 3, ["widget-b"] = 7 };

        (await coordinator.BeginAsync(s_scope, "same-op", "inventory-a", capture)).ShouldBe(1);
        (await coordinator.BeginAsync(s_scope, "same-op", "inventory-a", new Dictionary<string, long> {
            ["widget-b"] = 7,
            ["widget-a"] = 3,
        })).ShouldBe(1);
        _ = await Should.ThrowAsync<InvalidOperationException>(() => coordinator.BeginAsync(
            s_scope,
            "same-op",
            "inventory-b",
            capture));
        _ = await Should.ThrowAsync<InvalidOperationException>(() => coordinator.BeginAsync(
            s_scope,
            "same-op",
            "inventory-a",
            new Dictionary<string, long> { ["widget-a"] = 3, ["widget-b"] = 8 }));

        await coordinator.StageAsync(s_scope, "same-op", [Write(1)]);
        await coordinator.CommitAsync(s_scope, "same-op");
        (await coordinator.BeginAsync(s_scope, "same-op", "inventory-a", capture)).ShouldBe(1);
        _ = await Should.ThrowAsync<InvalidOperationException>(() => coordinator.BeginAsync(
            s_scope,
            "same-op",
            "inventory-a",
            new Dictionary<string, long> { ["widget-a"] = 3 }));
    }

    [Fact]
    public async Task AbortAfterInterruptedStage_ReconcilesAndDeletesReservedGeneration() {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(s_scope, "ordinary-dispatch");
        _ = await coordinator.BeginAsync(s_scope, "interrupted-stage", "inventory-0", new Dictionary<string, long>());

        bool faulted = false;
        store.BatchFaultHook = (phase, _, _) => {
            if (phase == ReadModelBatchPhase.BeforeCommit && !faulted) {
                faulted = true;
                throw new InvalidOperationException("stage interrupted");
            }

            return Task.CompletedTask;
        };
        _ = await Should.ThrowAsync<InvalidOperationException>(() => coordinator.StageAsync(
            s_scope,
            "interrupted-stage",
            [Write(99)]));

        store.BatchFaultHook = null;
        var restarted = new SharedProjectionEpochCoordinator(store, store);
        await restarted.AbortAsync(s_scope, "interrupted-stage", Fold(restarted));
        (await restarted.ReadGenerationAsync<Counter>(s_scope, 1, "index")).Value.ShouldBeNull();
        (await restarted.ReadAsync<Counter>(s_scope, "index")).Generation.ShouldBe(0);
    }

    private static SharedProjectionDelivery Delivery(long position, int delta)
        => new("widget-a", position, JsonSerializer.SerializeToUtf8Bytes(delta), JsonSerializer.SerializeToUtf8Bytes(position));

    private static Func<SharedProjectionDelivery, long, CancellationToken, Task<IReadOnlyList<ReadModelBatchOperation>>> Fold(
        SharedProjectionEpochCoordinator coordinator)
        => async (delivery, generation, cancellationToken) => {
            ReadModelEntry<Counter> current = await coordinator.ReadGenerationAsync<Counter>(
                s_scope,
                generation,
                "index",
                cancellationToken);
            int delta = JsonSerializer.Deserialize<int>(delivery.CanonicalPayload);
            return [Write((current.Value?.Value ?? 0) + delta)];
        };

    private static ReadModelBatchOperation Write(int value)
        => ReadModelBatchOperation.Write("index", new Counter(value), ReadModelBatchConcurrency.LastWrite);

    private sealed record Counter(int Value);
}
