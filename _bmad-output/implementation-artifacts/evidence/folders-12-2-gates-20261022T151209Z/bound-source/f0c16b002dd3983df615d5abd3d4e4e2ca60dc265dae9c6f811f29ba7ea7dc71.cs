using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Fakes;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

public sealed class SharedProjectionExpiryAndScratchTests
{
    private static SharedProjectionScope Scope(bool copyOnWrite = false) => new("statestore", "tenant-a", "widget", "shared-index", ["writer"])
    { CopyOnWrite = copyOnWrite };

    private static JsonObject Value(int count) => new() { ["count"] = count };

    [Fact]
    public async Task DelayedScratchCommitCannotRegressAnAbortCompletedByAnotherCoordinator()
    {
        var store = new InMemoryReadModelStore();
        SharedProjectionScope scope = Scope();
        var first = new SharedProjectionEpochCoordinator(store, store);
        _ = await first.RegisterWriterAsync(scope, "writer");
        _ = await first.BeginAsync(scope, "race", "inventory", new Dictionary<string, long>());
        SharedProjectionLease lease = await first.RefreshLeaseAsync(scope, "writer");
        (await first.JournalAsync(scope, lease, new SharedProjectionDelivery("admitted", 1, [1], [1])))
            .ShouldBe(SharedProjectionJournalResult.Journaled);
        var paused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int interceptions = 0;
        store.BatchFaultHook = async (phase, _, token) =>
        {
            if (phase == ReadModelBatchPhase.BeforeCommit && Interlocked.Increment(ref interceptions) == 1)
            {
                paused.SetResult();
                await release.Task.WaitAsync(token);
            }
        };
        Task<bool> delayed = first.TryWriteScratchAsync(scope, "race", "private", Value(1), "scratch",
            ReadModelBatchConcurrency.CreateOnly);
        await paused.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var aborter = new SharedProjectionEpochCoordinator(store, store);
        await aborter.AbortAsync(scope, "race", (_, _, _) => Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>([
            ReadModelBatchOperation.Write("index", Value(1), ReadModelBatchConcurrency.LastWrite)]));
        byte[] openedFence = store.SnapshotRaw(scope.StoreName, scope.StateKey)!;
        release.SetResult();
        _ = await delayed.WaitAsync(TimeSpan.FromSeconds(10));
        store.BatchFaultHook = null;
        store.SnapshotRaw(scope.StoreName, scope.StateKey).ShouldBe(openedFence);
        SharedProjectionEpochStatus status = await aborter.GetStatusAsync(scope);
        status.IsBuilding.ShouldBeFalse(); status.IsAborting.ShouldBeFalse(); status.PendingDeliveryCount.ShouldBe(0);
        (await aborter.GetCheckpointAsync(scope, "admitted"))!.Position.ShouldBe(1);
        (await aborter.ReadAsync<JsonObject>(scope, "index")).Value!["count"]!.GetValue<int>().ShouldBe(1);
    }

    [Theory]
    [InlineData(ReadModelBatchPhase.AfterInstallOperation, 0)]
    [InlineData(ReadModelBatchPhase.AfterInstallOperation, 1)]
    [InlineData(ReadModelBatchPhase.BeforeCommit, -1)]
    [InlineData(ReadModelBatchPhase.AfterCommit, -1)]
    [InlineData(ReadModelBatchPhase.BeforeCompaction, 0)]
    [InlineData(ReadModelBatchPhase.BeforeCompaction, 1)]
    [InlineData(ReadModelBatchPhase.RecoveryReceiptWritten, 1)]
    [InlineData(ReadModelBatchPhase.RecoveryManifestReclaimed, 1)]
    public async Task InterruptedScratchCanRetryThenAbortWithoutLosingAdmittedOwners(ReadModelBatchPhase failure, int failedOrdinal)
    {
        var store = new InMemoryReadModelStore();
        SharedProjectionScope scope = Scope();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(scope, "writer");
        store.SeedRaw(scope.StoreName, scope.PhysicalKey(0, "index"), Value(5));
        _ = await coordinator.BeginAsync(scope, "rebuild", "inventory", new Dictionary<string, long>());
        SharedProjectionLease lease = await coordinator.RefreshLeaseAsync(scope, "writer");
        foreach (string owner in new[] { "owner-a", "owner-b" })
            (await coordinator.JournalAsync(scope, lease, new SharedProjectionDelivery(owner, 1, [1], [1])))
                .ShouldBe(SharedProjectionJournalResult.Journaled);
        store.BatchFaultHook = (phase, ordinal, _) => phase == failure && ordinal == failedOrdinal
            ? throw new InvalidOperationException("simulated process replacement") : Task.CompletedTask;
        await Should.ThrowAsync<InvalidOperationException>(() => coordinator.TryWriteScratchAsync(scope, "rebuild", "private-scratch",
            Value(10), "scratch", ReadModelBatchConcurrency.CreateOnly));
        (await coordinator.ReadAsync<JsonObject>(scope, "index")).Value!["count"]!.GetValue<int>().ShouldBe(5);
        store.BatchFaultHook = null;
        coordinator = new SharedProjectionEpochCoordinator(store, store);
        // Recovery reloads the saved original policy rather than generating a new fence fingerprint.
        _ = await coordinator.TryWriteScratchAsync(scope, "rebuild", "private-scratch", Value(10), "scratch",
            ReadModelBatchConcurrency.CreateOnly);
        (await store.GetAsync<JsonObject>(scope.StoreName, "private-scratch")).Value!["count"]!.GetValue<int>().ShouldBe(10);
        await Should.ThrowAsync<InvalidOperationException>(() => coordinator.AbortAsync(scope, "foreign", Fold));
        await coordinator.AbortAsync(scope, "rebuild", Fold);
        await coordinator.AbortAsync(scope, "rebuild", Fold);
        SharedProjectionEpochState state = (await store.GetAsync<SharedProjectionEpochState>(scope.StoreName, scope.StateKey)).Value!;
        state.Phase.ShouldBe(SharedProjectionEpochPhase.Open);
        state.ActiveGeneration.ShouldBe(0);
        state.Journal.ShouldBeEmpty();
        (await coordinator.ReadAsync<JsonObject>(scope, "index")).Value!["count"]!.GetValue<int>().ShouldBe(7);
        foreach (string owner in new[] { "owner-a", "owner-b" })
            (await coordinator.GetCheckpointAsync(scope, owner))!.Position.ShouldBe(1);
        lease = await coordinator.RefreshLeaseAsync(scope, "writer");
        (await coordinator.JournalAsync(scope, lease, new SharedProjectionDelivery("owner-a", 2, [2], [2])))
            .ShouldBe(SharedProjectionJournalResult.Journaled);
        _ = await coordinator.CatchUpAsync(scope, Fold);
        (await coordinator.ReadAsync<JsonObject>(scope, "index")).Value!["count"]!.GetValue<int>().ShouldBe(8);
        _ = await coordinator.BeginAsync(scope, "fresh", "fresh-inventory", new Dictionary<string, long>());
        _ = await coordinator.StageAsync(scope, "fresh", [ReadModelBatchOperation.Write("index", Value(20), ReadModelBatchConcurrency.LastWrite)]);
        await coordinator.CommitAsync(scope, "fresh");
        (await coordinator.ReadAsync<JsonObject>(scope, "index")).Value!["count"]!.GetValue<int>().ShouldBe(20);
        (await coordinator.TryWriteScratchAsync(scope, "rebuild", "stale", Value(99), "stale", ReadModelBatchConcurrency.CreateOnly))
            .ShouldBeFalse();

        async Task<IReadOnlyList<ReadModelBatchOperation>> Fold(SharedProjectionDelivery _, long generation, CancellationToken token)
        {
            int count = (await coordinator.ReadGenerationAsync<JsonObject>(scope, generation, "index", token)).Value!["count"]!.GetValue<int>();
            return [ReadModelBatchOperation.Write("index", Value(count + 1), ReadModelBatchConcurrency.LastWrite)];
        }
    }

    [Fact]
    public async Task LegacyPreparedScratchCanCompensateOriginalEnvelopesBeforeAbort()
    {
        var store = new InMemoryReadModelStore();
        SharedProjectionScope scope = Scope();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(scope, "writer");
        _ = await coordinator.BeginAsync(scope, "legacy", "inventory", new Dictionary<string, long>());
        var fence = await store.GetAsync<SharedProjectionEpochState>(scope.StoreName, scope.StateKey);
        var legacy = new ReadModelBatch(new(scope.StoreName, scope.TenantId, scope.Domain, scope.Family, scope.Family, "legacy-scratch"),
        [ReadModelBatchOperation.Write("private-scratch", Value(1), ReadModelBatchConcurrency.CreateOnly),
            ReadModelBatchOperation.Write(scope.StateKey, fence.Value!, ReadModelBatchConcurrency.Match(fence.ETag!))]);
        store.BatchFaultHook = (phase, _, _) => phase == ReadModelBatchPhase.BeforeCommit
            ? throw new InvalidOperationException("legacy process stop") : Task.CompletedTask;
        await Should.ThrowAsync<InvalidOperationException>(() => store.ExecuteAsync(legacy));
        ReadModelBatchMarker marker = store.Snapshot<ReadModelBatchMarker>(scope.StoreName, legacy.Scope.ComputeMarkerKey())!;
        marker.OriginalManifest.ShouldBeNull();
        store.BatchFaultHook = null;
        coordinator = new SharedProjectionEpochCoordinator(store, store);
        await coordinator.AbortAsync(scope, "legacy", (_, _, _) => Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>([]));
        (await store.GetAsync<SharedProjectionEpochState>(scope.StoreName, scope.StateKey)).Value!.Phase.ShouldBe(SharedProjectionEpochPhase.Open);
        store.SnapshotRaw(scope.StoreName, "private-scratch").ShouldBeNull();
        store.Snapshot<ReadModelBatchMarker>(scope.StoreName, legacy.Scope.ComputeMarkerKey())!.Status.ShouldBe(ReadModelBatchMarkerStatus.Aborted);
    }

    [Fact]
    public async Task LegacyCommittedScratchWithoutManifestIsNotRolledBackOrGuessed()
    {
        var store = new InMemoryReadModelStore();
        SharedProjectionScope scope = Scope();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(scope, "writer");
        _ = await coordinator.BeginAsync(scope, "legacy", "inventory", new Dictionary<string, long>());
        var fence = await store.GetAsync<SharedProjectionEpochState>(scope.StoreName, scope.StateKey);
        var legacy = new ReadModelBatch(new(scope.StoreName, scope.TenantId, scope.Domain, scope.Family, scope.Family, "legacy-committed"),
        [ReadModelBatchOperation.Write("private-scratch", Value(1), ReadModelBatchConcurrency.CreateOnly),
            ReadModelBatchOperation.Write(scope.StateKey, fence.Value!, ReadModelBatchConcurrency.Match(fence.ETag!))]);
        store.BatchFaultHook = (phase, _, _) => phase == ReadModelBatchPhase.AfterCommit
            ? throw new InvalidOperationException("legacy process stop") : Task.CompletedTask;
        await Should.ThrowAsync<InvalidOperationException>(() => store.ExecuteAsync(legacy));
        store.BatchFaultHook = null;
        byte[] originalFence = store.SnapshotRaw(scope.StoreName, scope.StateKey)!;
        await Should.ThrowAsync<SharedProjectionWriteUnavailableException>(() => coordinator.AbortAsync(scope, "legacy",
            (_, _, _) => Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>([])));
        store.SnapshotRaw(scope.StoreName, scope.StateKey).ShouldBe(originalFence);
        store.Snapshot<ReadModelBatchMarker>(scope.StoreName, legacy.Scope.ComputeMarkerKey())!.Status.ShouldBe(ReadModelBatchMarkerStatus.Committed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StreamedSelectedAndInheritedGenerationsKeepOriginalDeadlines(bool copyOnWrite)
    {
        DateTimeOffset deadline = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
        var clock = new ProjectionDeadlineTimeProvider(deadline.AddHours(-1));
        var store = new InMemoryReadModelStore();
        store.BatchOptions.TimeProvider = clock;
        var coordinator = new SharedProjectionEpochCoordinator(store, store, clock);
        SharedProjectionScope scope = Scope(copyOnWrite);
        _ = await coordinator.RegisterWriterAsync(scope, "writer");
        _ = await coordinator.BeginAsync(scope, "first", "inventory", new Dictionary<string, long>());
        _ = await coordinator.StageStreamingAsync(scope, "first", Writes());
        await coordinator.CommitAsync(scope, "first");
        (await coordinator.ReadAsync<JsonObject>(scope, "expiring")).Value.ShouldNotBeNull();
        if (copyOnWrite)
        {
            _ = await coordinator.BeginAsync(scope, "inherit", "inventory-2", new Dictionary<string, long>());
            _ = await coordinator.StageStreamingAsync(scope, "inherit", Unrelated());
            await coordinator.CommitAsync(scope, "inherit");
        }
        clock.UtcNow = deadline.AddTicks(-1);
        (await coordinator.ReadAsync<JsonObject>(scope, "expiring")).Value.ShouldNotBeNull();
        clock.UtcNow = deadline;
        SharedProjectionReadResult<JsonObject> expired = await coordinator.ReadAsync<JsonObject>(scope, "expiring");
        expired.IsAvailable.ShouldBeTrue();
        expired.Value.ShouldBeNull();
        (await coordinator.ReadManyAsync<JsonObject>(scope, ["expiring", "checkpoint"])).Value!["checkpoint"].ShouldNotBeNull();

        async IAsyncEnumerable<ReadModelBatchOperation> Writes()
        {
            yield return ReadModelBatchOperation.WriteExpiring("expiring", Value(1), ReadModelBatchConcurrency.LastWrite, expiresAtUtc: deadline);
            yield return ReadModelBatchOperation.Write("checkpoint", Value(1), ReadModelBatchConcurrency.LastWrite);
            await Task.Yield();
        }
        static async IAsyncEnumerable<ReadModelBatchOperation> Unrelated()
        {
            yield return ReadModelBatchOperation.Write("unrelated", Value(2), ReadModelBatchConcurrency.LastWrite);
            await Task.Yield();
        }
    }
    [Fact]
    public async Task AbandonedSameKeyReplacementRetainsAnchorDeadlineAndTruthfulAbsence()
    {
        DateTimeOffset deadline = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
        var clock = new ProjectionDeadlineTimeProvider(deadline.AddMinutes(-1));
        var store = new InMemoryReadModelStore();
        store.BatchOptions.TimeProvider = clock;
        SharedProjectionScope scope = Scope(copyOnWrite: true);
        var coordinator = new SharedProjectionEpochCoordinator(store, store, clock);
        _ = await coordinator.RegisterWriterAsync(scope, "writer");
        var original = new ReadModelBatch(new(scope.StoreName, scope.TenantId, scope.Domain, "owner", scope.Family, "anchor"),
            [ReadModelBatchOperation.WriteExpiring(scope.PhysicalKey(0, "index"), Value(1), ReadModelBatchConcurrency.CreateOnly, deadline)]);
        (await store.ExecuteAsync(original)).IsSuccess.ShouldBeTrue();
        _ = await coordinator.BeginAsync(scope, "abandoned", "capture", new Dictionary<string, long>());
        _ = await coordinator.StageStreamingAsync(scope, "abandoned", Replacement());
        await coordinator.AbortAsync(scope, "abandoned", (_, _, _) => Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>([]));
        clock.UtcNow = deadline.AddTicks(-1);
        (await coordinator.ReadAsync<JsonObject>(scope, "index")).Value!["count"]!.GetValue<int>().ShouldBe(1);
        clock.UtcNow = deadline;
        (await coordinator.ReadAsync<JsonObject>(scope, "index")).Value.ShouldBeNull();
        static async IAsyncEnumerable<ReadModelBatchOperation> Replacement()
        {
            yield return ReadModelBatchOperation.Write("index", Value(99), ReadModelBatchConcurrency.LastWrite);
            await Task.Yield();
        }
    }

    [Fact]
    public async Task InheritedReadCrossingDeadlineDuringPhysicalAwaitReturnsAbsence()
    {
        using var client = new RecordingDaprClient { PersistTypedState = true };
        DateTimeOffset deadline = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
        var clock = new ProjectionDeadlineTimeProvider(deadline.AddTicks(-1));
        var store = new DaprReadModelStore(client, Microsoft.Extensions.Options.Options.Create(new ReadModelBatchOptions { TimeProvider = clock }));
        SharedProjectionScope scope = Scope(copyOnWrite: true);
        client.SeedByteStore(scope.CopyOnWritePointerKey("index"), JsonSerializer.SerializeToUtf8Bytes(
            new SharedProjectionCopyOnWritePointer(1, false, 0, true) { LatestExpiresAtUtc = deadline }));
        client.SeedByteStore(scope.CopyOnWriteCommitKey(1), JsonSerializer.SerializeToUtf8Bytes(new SharedProjectionCopyOnWriteCommit(1)));
        client.SeedByteStore(scope.PhysicalKey(1, "index"), ReadModelBatchExpiringValue.Encode(JsonSerializer.SerializeToUtf8Bytes(Value(1)), deadline));
        var coordinator = new SharedProjectionEpochCoordinator(store, store, clock);
        (await coordinator.ReadGenerationAsync<JsonObject>(scope, 2, "index")).Value.ShouldNotBeNull();
        client.BeforeReadByteState = key => { if (key == scope.PhysicalKey(1, "index")) clock.UtcNow = deadline; };
        (await coordinator.ReadGenerationAsync<JsonObject>(scope, 2, "index")).Value.ShouldBeNull();
        clock.UtcNow = deadline.AddTicks(-1);
        client.BeforeReadByteState = null;
        client.SeedByteStore(scope.PhysicalKey(1, "index"), "null"u8.ToArray());
        await Should.ThrowAsync<SharedProjectionReadUnavailableException>(() => coordinator.ReadGenerationAsync<JsonObject>(scope, 2, "index"));
    }

}
