using System.Security.Cryptography;
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
    public async Task CapturedIdentityPrefixExtendsAfterOrdinaryGrowthAndPreservesReplay() {
        var store = new InMemoryReadModelStore();
        SharedProjectionEpochCoordinator coordinator = await PrepareSequenceSeedAsync(store, "prefix", 1);
        byte[] identities = CapturedIdentities(8);
        await coordinator.SeedCapturedOwnerSequenceAsync(s_scope, "prefix", "widget-a", 1,
            capturedEventDigests: identities[..32]);
        await coordinator.CompleteOwnerSequenceHandoffAsync(s_scope, "prefix");
        _ = await coordinator.CatchUpAsync(s_scope, Fold(coordinator));
        SharedProjectionLease lease = await coordinator.RefreshLeaseAsync(s_scope, "ordinary-dispatch");
        for (int position = 2; position <= 8; position++) {
            (await coordinator.JournalAsync(s_scope, lease, Delivery(position, 1) with { OwnerSequenceNumber = position }))
                .ShouldBe(SharedProjectionJournalResult.Journaled);
            (await coordinator.CatchUpAsync(s_scope, Fold(coordinator))).ShouldBe(1);
        }

        SharedProjectionCheckpoint checkpoint = (await coordinator.GetCheckpointAsync(s_scope, "widget-a"))!;
        checkpoint.OwnerSequenceNumber.ShouldBe(8);
        checkpoint.CapturedEventDigests!.Length.ShouldBe(32);
        (await coordinator.ReadAsync<Counter>(s_scope, "index")).Value!.Value.ShouldBe(8);

        _ = await coordinator.BeginAsync(s_scope, "complete", "complete-inventory",
            new Dictionary<string, long> { ["widget-a"] = 8 });
        _ = await coordinator.StageAsync(s_scope, "complete", [Write(8)]);
        await coordinator.CommitAsync(s_scope, "complete", requireOwnerSequenceHandoff: true);
        await coordinator.SeedCapturedOwnerSequenceAsync(s_scope, "complete", "widget-a", 8,
            capturedEventDigests: identities);
        await coordinator.SeedCapturedOwnerSequenceAsync(s_scope, "complete", "widget-a", 8,
            capturedEventDigests: identities);
        await coordinator.CompleteOwnerSequenceHandoffAsync(s_scope, "complete");
        _ = await coordinator.CatchUpAsync(s_scope, Fold(coordinator));
        (await coordinator.GetCheckpointAsync(s_scope, "widget-a"))!.CapturedEventDigests!.ShouldBe(identities);

        lease = await coordinator.RefreshLeaseAsync(s_scope, "ordinary-dispatch");
        for (int position = 1; position <= 8; position++) {
            (await coordinator.JournalAsync(s_scope, lease, Delivery(position, 1) with { OwnerSequenceNumber = position }))
                .ShouldBe(SharedProjectionJournalResult.AlreadyCaptured);
        }

        (await coordinator.JournalAsync(s_scope, lease, Delivery(1, 2) with { OwnerSequenceNumber = 1 }))
            .ShouldBe(SharedProjectionJournalResult.IdentityConflict);
        (await coordinator.JournalAsync(s_scope, lease, Delivery(9, 1) with { OwnerSequenceNumber = 9 }))
            .ShouldBe(SharedProjectionJournalResult.Journaled);
        (await coordinator.CatchUpAsync(s_scope, Fold(coordinator))).ShouldBe(1);
        (await coordinator.ReadAsync<Counter>(s_scope, "index")).Value!.Value.ShouldBe(9);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(8, false)]
    [InlineData(1, true)]
    public async Task SequenceSeedRejectsConflictingOrMissingKnownPrefix(long priorSequence, bool omitIdentities) {
        var store = new InMemoryReadModelStore();
        SharedProjectionEpochCoordinator coordinator = await PrepareSequenceSeedAsync(store, "conflicting-prefix", 8);
        byte[] identities = CapturedIdentities(8);
        var existing = new SharedProjectionCheckpoint(priorSequence, [1], priorSequence, identities[..32]);
        store.SeedRaw(s_scope.StoreName, s_scope.CheckpointKey("widget-a"), existing);
        ReadModelEntry<SharedProjectionCheckpoint> before = await store.GetAsync<SharedProjectionCheckpoint>(
            s_scope.StoreName, s_scope.CheckpointKey("widget-a"));
        identities[0] ^= 1;

        _ = await Should.ThrowAsync<InvalidOperationException>(() => coordinator.SeedCapturedOwnerSequenceAsync(
            s_scope, "conflicting-prefix", "widget-a", 8, capturedEventDigests: omitIdentities ? null : identities));
        ReadModelEntry<SharedProjectionCheckpoint> after = await store.GetAsync<SharedProjectionCheckpoint>(
            s_scope.StoreName, s_scope.CheckpointKey("widget-a"));
        after.ETag.ShouldBe(before.ETag);
        JsonSerializer.Serialize(after.Value).ShouldBe(JsonSerializer.Serialize(before.Value));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(31, 1)]
    [InlineData(33, 8)]
    [InlineData(64, 1)]
    [InlineData(288, 8)]
    public async Task SequenceSeedRejectsMalformedRetainedIdentityVectors(int byteCount, long priorSequence) {
        var store = new InMemoryReadModelStore();
        SharedProjectionEpochCoordinator coordinator = await PrepareSequenceSeedAsync(store, "malformed-prefix", 8);
        store.SeedRaw(s_scope.StoreName, s_scope.CheckpointKey("widget-a"),
            new SharedProjectionCheckpoint(8, [8], priorSequence, new byte[byteCount]));
        ReadModelEntry<SharedProjectionCheckpoint> before = await store.GetAsync<SharedProjectionCheckpoint>(
            s_scope.StoreName, s_scope.CheckpointKey("widget-a"));

        _ = await Should.ThrowAsync<InvalidOperationException>(() => coordinator.SeedCapturedOwnerSequenceAsync(
            s_scope, "malformed-prefix", "widget-a", 8, capturedEventDigests: CapturedIdentities(8)));
        (await store.GetAsync<SharedProjectionCheckpoint>(s_scope.StoreName, s_scope.CheckpointKey("widget-a")))
            .ETag.ShouldBe(before.ETag);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    [InlineData(224)]
    [InlineData(255)]
    [InlineData(288)]
    public async Task SequenceSeedRequiresANewCompleteIdentityVector(int byteCount) {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await Should.ThrowAsync<ArgumentException>(() => coordinator.SeedCapturedOwnerSequenceAsync(
            s_scope, "incomplete", "widget-a", 8, capturedEventDigests: new byte[byteCount]));
        store.Count.ShouldBe(0);
    }

    [Fact]
    public async Task SequenceSeedPrefixExtensionPreservesANewerConcurrentCheckpoint() {
        var store = new InMemoryReadModelStore();
        SharedProjectionEpochCoordinator coordinator = await PrepareSequenceSeedAsync(store, "extension-race", 8);
        byte[] identities = CapturedIdentities(8);
        store.SeedRaw(s_scope.StoreName, s_scope.CheckpointKey("widget-a"),
            new SharedProjectionCheckpoint(8, [8], 8, identities[..32]));
        _ = await Should.ThrowAsync<InvalidOperationException>(() => coordinator.SeedCapturedOwnerSequenceAsync(
            s_scope, "stale-operation", "widget-a", 8, capturedEventDigests: identities));
        bool raced = false;
        var newer = new SharedProjectionCheckpoint(9, [9], 9, identities[..32]);
        store.BatchFaultHook = (phase, _, _) => {
            if (phase == ReadModelBatchPhase.MarkerPrepared && !raced) {
                raced = true;
                store.SeedRaw(s_scope.StoreName, s_scope.CheckpointKey("widget-a"), newer);
            }
            return Task.CompletedTask;
        };

        _ = await Should.ThrowAsync<InvalidOperationException>(() => coordinator.SeedCapturedOwnerSequenceAsync(
            s_scope, "extension-race", "widget-a", 8, capturedEventDigests: identities));
        raced.ShouldBeTrue();
        JsonSerializer.Serialize((await coordinator.GetCheckpointAsync(s_scope, "widget-a")))
            .ShouldBe(JsonSerializer.Serialize(newer));
    }

    [Fact]
    public async Task CapturedPositionRequiresTheCapturedEventIdentity() {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(s_scope, "ordinary-dispatch");
        _ = await coordinator.BeginAsync(s_scope, "captured-identity", "inventory",
            new Dictionary<string, long> { ["widget-a"] = 1 });
        await coordinator.StageAsync(s_scope, "captured-identity", [Write(1)]);
        await coordinator.CommitAsync(s_scope, "captured-identity", requireOwnerSequenceHandoff: true);
        byte[] capturedIdentity = SharedProjectionCapturedEventIdentity.Compute("widget-a", 1, 1, [7]);
        await coordinator.SeedCapturedOwnerSequenceAsync(s_scope, "captured-identity", "widget-a", 1,
            capturedEventDigests: capturedIdentity);
        await coordinator.CompleteOwnerSequenceHandoffAsync(s_scope, "captured-identity");
        SharedProjectionLease lease = await coordinator.RefreshLeaseAsync(s_scope, "ordinary-dispatch");

        (await coordinator.JournalAsync(s_scope, lease,
            new SharedProjectionDelivery("widget-a", 1, [7], [1], OwnerSequenceNumber: 1)))
            .ShouldBe(SharedProjectionJournalResult.AlreadyCaptured);
        (await coordinator.JournalAsync(s_scope, lease,
            new SharedProjectionDelivery("widget-a", 1, [8], [1], OwnerSequenceNumber: 1)))
            .ShouldBe(SharedProjectionJournalResult.IdentityConflict);

        (await coordinator.JournalAsync(s_scope, lease,
            new SharedProjectionDelivery("widget-a", 2, [9], [2], OwnerSequenceNumber: 2)))
            .ShouldBe(SharedProjectionJournalResult.Journaled);
        (await coordinator.GetCheckpointAsync(s_scope, "widget-a"))!.CapturedEventDigests!
            .ShouldBe(capturedIdentity);
    }

    [Fact]
    public async Task CapturedOwnerRejectsAnEarlierEventBeyondItsSealedWatermark() {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(s_scope, "ordinary-dispatch");
        _ = await coordinator.BeginAsync(s_scope, "capture-every-position", "inventory",
            new Dictionary<string, long> { ["widget-a"] = 3 });

        (await coordinator.MatchesCapturedOwnerPositionAsync(s_scope, "capture-every-position",
            "widget-a", [1, 3])).ShouldBeTrue();
        (await coordinator.MatchesCapturedOwnerPositionAsync(s_scope, "capture-every-position",
            "widget-a", [4, 3])).ShouldBeFalse();
        (await coordinator.MatchesCapturedOwnerPositionAsync(s_scope, "capture-every-position",
            "widget-a", [2, 1, 3])).ShouldBeFalse();
        (await coordinator.MatchesCapturedOwnerPositionAsync(s_scope, "capture-every-position",
            "widget-a", [1, 1, 3])).ShouldBeFalse();
        (await coordinator.MatchesCapturedOwnerPositionAsync(s_scope, "capture-every-position",
            "widget-a", [1, 2])).ShouldBeFalse();
    }

    [Fact]
    public async Task SequenceSeedRejectsAheadCheckpointEvenWhenItsSequenceAlreadyMatches() {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(s_scope, "ordinary-dispatch");
        _ = await coordinator.BeginAsync(s_scope, "seed-ahead", "inventory",
            new Dictionary<string, long> { ["widget-a"] = 3 });
        _ = await coordinator.StageAsync(s_scope, "seed-ahead", [Write(1)]);
        await coordinator.CommitAsync(s_scope, "seed-ahead", requireOwnerSequenceHandoff: true);
        (await store.TrySaveAsync(s_scope.StoreName, s_scope.CheckpointKey("widget-a"),
            new SharedProjectionCheckpoint(4, [4], 1), string.Empty)).ShouldBeTrue();

        _ = await Should.ThrowAsync<InvalidOperationException>(() => coordinator
            .SeedCapturedOwnerSequenceAsync(s_scope, "seed-ahead", "widget-a", 1));
        (await coordinator.GetCheckpointAsync(s_scope, "widget-a"))!.Position.ShouldBe(4);
    }

    [Fact]
    public async Task SequenceSeedRejectsBehindCheckpointWhenItsSequenceAlreadyMatches() {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(s_scope, "ordinary-dispatch");
        _ = await coordinator.BeginAsync(s_scope, "seed-behind", "inventory",
            new Dictionary<string, long> { ["widget-a"] = 3 });
        _ = await coordinator.StageAsync(s_scope, "seed-behind", [Write(1)]);
        await coordinator.CommitAsync(s_scope, "seed-behind", requireOwnerSequenceHandoff: true);
        (await store.TrySaveAsync(s_scope.StoreName, s_scope.CheckpointKey("widget-a"),
            new SharedProjectionCheckpoint(2, [2], 1), string.Empty)).ShouldBeTrue();

        _ = await Should.ThrowAsync<InvalidOperationException>(() => coordinator
            .SeedCapturedOwnerSequenceAsync(s_scope, "seed-behind", "widget-a", 1));
        (await coordinator.GetCheckpointAsync(s_scope, "widget-a"))!.Position.ShouldBe(2);
    }

    [Fact]
    public async Task LegacyCheckpointRequiresCapturedRebuildBeforeSequenceDeliveryResumes() {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(s_scope, "ordinary-dispatch");
        (await store.TrySaveAsync(s_scope.StoreName, s_scope.CheckpointKey("widget-a"),
            new SharedProjectionCheckpoint(1, [1]), string.Empty)).ShouldBeTrue();
        SharedProjectionLease lease = await coordinator.RefreshLeaseAsync(s_scope, "ordinary-dispatch");
        var next = new SharedProjectionDelivery("widget-a", 2, [2], [2], OwnerSequenceNumber: 2);

        (await coordinator.JournalAsync(s_scope, lease, next))
            .ShouldBe(SharedProjectionJournalResult.RebuildRequired);
        _ = await coordinator.BeginAsync(s_scope, "legacy-upgrade", "captured-inventory",
            new Dictionary<string, long> { ["widget-a"] = 1 });
        _ = await coordinator.StageAsync(s_scope, "legacy-upgrade",
            [ReadModelBatchOperation.Write("index", new Counter(1), ReadModelBatchConcurrency.LastWrite)]);
        await coordinator.CommitAsync(s_scope, "legacy-upgrade", requireOwnerSequenceHandoff: true);
        await coordinator.SeedCapturedOwnerSequenceAsync(s_scope, "legacy-upgrade", "widget-a", 1);
        await coordinator.CompleteOwnerSequenceHandoffAsync(s_scope, "legacy-upgrade");
        lease = await coordinator.RefreshLeaseAsync(s_scope, "ordinary-dispatch");
        (await coordinator.JournalAsync(s_scope, lease, next)).ShouldBe(SharedProjectionJournalResult.Journaled);
    }

    [Fact]
    public async Task LegacyConsumerCheckpointWithoutOwnerIndexRequiresRebuild() {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(s_scope, "ordinary-dispatch");
        ReadModelEntry<SharedProjectionEpochState> entry = await store.GetAsync<SharedProjectionEpochState>(
            s_scope.StoreName, s_scope.StateKey);
        SharedProjectionEpochState state = entry.Value.ShouldNotBeNull();
        (await store.TrySaveAsync(s_scope.StoreName, s_scope.StateKey, state with {
            ConsumerCheckpoints = new Dictionary<string, SharedProjectionCheckpoint> {
                ["widget-a"] = new(1, [1])
            }
        }, entry.ETag!)).ShouldBeTrue();
        SharedProjectionLease lease = await coordinator.RefreshLeaseAsync(s_scope, "ordinary-dispatch");

        (await coordinator.JournalAsync(s_scope, lease,
            new SharedProjectionDelivery("widget-a", 2, [2], [2], OwnerSequenceNumber: 2)))
            .ShouldBe(SharedProjectionJournalResult.RebuildRequired);
    }

    [Fact]
    public async Task LargePreparedCatchUpSurvivesBatchFailureAndCleansReservedChunks() {
        var store = new InMemoryReadModelStore();
        var faultStore = new ChunkWriteFaultStore(store);
        var coordinator = new SharedProjectionEpochCoordinator(faultStore, store);
        SharedProjectionLease lease = await coordinator.RegisterWriterAsync(s_scope, "ordinary-dispatch");
        _ = await coordinator.JournalAsync(s_scope, lease, Delivery(1, 1));
        string expected = new('x', 100_000);
        static Task<IReadOnlyList<ReadModelBatchOperation>> FoldLarge(
            SharedProjectionDelivery _, long __, CancellationToken ___)
            => Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>([
                ReadModelBatchOperation.Write("large", new LargeRow(new string('x', 100_000)), ReadModelBatchConcurrency.LastWrite)]);
        faultStore.FailNextChunkWrite = true;
        _ = await Should.ThrowAsync<SharedProjectionWriteUnavailableException>(() => coordinator.CatchUpAsync(s_scope, FoldLarge));
        SharedProjectionEpochState reserved = (await store.GetAsync<SharedProjectionEpochState>(s_scope.StoreName, s_scope.StateKey))
            .Value.ShouldNotBeNull();
        reserved.Journal.Single().PreparedChunks.ShouldNotBeNull();
        reserved.Journal.Single().PreparedReady.ShouldBeFalse();
        bool faulted = false;
        store.BatchFaultHook = (phase, _, _) => {
            if (phase == ReadModelBatchPhase.BeforeCommit && !faulted) {
                faulted = true;
                throw new IOException("injected selected-generation write failure");
            }

            return Task.CompletedTask;
        };
        _ = await Should.ThrowAsync<SharedProjectionWriteUnavailableException>(() => coordinator.CatchUpAsync(s_scope, FoldLarge));
        SharedProjectionEpochState pending = (await store.GetAsync<SharedProjectionEpochState>(s_scope.StoreName, s_scope.StateKey))
            .Value.ShouldNotBeNull();
        SharedProjectionChunkReference reference = pending.Journal.Single().PreparedChunks.ShouldNotBeNull();
        pending.Journal.Single().PreparedReady.ShouldBeTrue();
        (await coordinator.ReadAsync<LargeRow>(s_scope, "large")).Value.ShouldBeNull();

        store.BatchFaultHook = null;
        var restarted = new SharedProjectionEpochCoordinator(faultStore, store);
        (await restarted.CatchUpAsync(s_scope, FoldLarge)).ShouldBe(1);
        (await restarted.ReadAsync<LargeRow>(s_scope, "large")).Value.ShouldNotBeNull().Value.ShouldBe(expected);
        (await store.GetAsync<SharedProjectionChunk>(s_scope.StoreName,
            SharedProjectionChunkStore.Key(s_scope, reference, 0))).Value.ShouldNotBeNull().Tombstone.ShouldBeTrue();
    }

    [Fact]
    public async Task StoreOwnedCancellationDuringPreparedChunkReadAndWriteIsRetryable() {
        var store = new InMemoryReadModelStore();
        var faultStore = new ChunkWriteFaultStore(store);
        var coordinator = new SharedProjectionEpochCoordinator(faultStore, store);
        SharedProjectionLease lease = await coordinator.RegisterWriterAsync(s_scope, "ordinary-dispatch");
        _ = await coordinator.JournalAsync(s_scope, lease, Delivery(1, 1));
        static Task<IReadOnlyList<ReadModelBatchOperation>> FoldLarge(
            SharedProjectionDelivery _, long __, CancellationToken ___)
            => Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>([
                ReadModelBatchOperation.Write("large", new LargeRow(new string('x', 100_000)),
                    ReadModelBatchConcurrency.LastWrite)]);
        bool failBatch = true;
        store.BatchFaultHook = (phase, _, _) => {
            if (phase == ReadModelBatchPhase.BeforeCommit && failBatch) {
                failBatch = false;
                throw new IOException("injected selected-generation write failure");
            }

            return Task.CompletedTask;
        };
        await Should.ThrowAsync<SharedProjectionWriteUnavailableException>(() =>
            coordinator.CatchUpAsync(s_scope, FoldLarge));
        store.BatchFaultHook = null;

        faultStore.FailNextChunkReadWithCancellation = true;
        await Should.ThrowAsync<SharedProjectionReadUnavailableException>(() =>
            coordinator.CatchUpAsync(s_scope, FoldLarge));
        (await coordinator.CatchUpAsync(s_scope, FoldLarge)).ShouldBe(1);

        _ = await coordinator.JournalAsync(s_scope, lease, Delivery(2, 2));
        faultStore.FailNextChunkWriteWithCancellation = true;
        await Should.ThrowAsync<SharedProjectionWriteUnavailableException>(() =>
            coordinator.CatchUpAsync(s_scope, FoldLarge));
        (await coordinator.CatchUpAsync(s_scope, FoldLarge)).ShouldBe(1);
        (await coordinator.ReadAsync<LargeRow>(s_scope, "large")).Value.ShouldNotBeNull();
    }

    private sealed record LargeRow(string Value);

    private sealed class ChunkWriteFaultStore(InMemoryReadModelStore inner) : IReadModelStore {
        public bool FailNextChunkWrite { get; set; }

        public bool FailNextChunkReadWithCancellation { get; set; }

        public bool FailNextChunkWriteWithCancellation { get; set; }

        public Task<ReadModelEntry<TValue>> GetAsync<TValue>(
            string storeName, string key, CancellationToken cancellationToken = default) where TValue : class
        {
            if (FailNextChunkReadWithCancellation && key.StartsWith("shared-chunk:", StringComparison.Ordinal)) {
                FailNextChunkReadWithCancellation = false;
                throw new TaskCanceledException("injected chunk read timeout");
            }

            return inner.GetAsync<TValue>(storeName, key, cancellationToken);
        }

        public Task SaveAsync<TValue>(
            string storeName, string key, TValue value, CancellationToken cancellationToken = default) where TValue : class
            => inner.SaveAsync(storeName, key, value, cancellationToken);

        public Task<bool> TrySaveAsync<TValue>(
            string storeName, string key, TValue value, string etag, CancellationToken cancellationToken = default) where TValue : class {
            if (FailNextChunkWriteWithCancellation && key.StartsWith("shared-chunk:", StringComparison.Ordinal)) {
                FailNextChunkWriteWithCancellation = false;
                throw new TaskCanceledException("injected chunk write timeout");
            }

            if (FailNextChunkWrite && key.StartsWith("shared-chunk:", StringComparison.Ordinal)) {
                FailNextChunkWrite = false;
                throw new IOException("injected chunk write failure");
            }

            return inner.TrySaveAsync(storeName, key, value, etag, cancellationToken);
        }
    }

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
    public async Task SingleKeyReadRetriesAfterPromotionDuringPhysicalRead() {
        var inner = new InMemoryReadModelStore();
        var intercepted = new SelectedReadInterleavingStore(inner);
        var coordinator = new SharedProjectionEpochCoordinator(intercepted, inner);
        _ = await coordinator.RegisterWriterAsync(s_scope, "ordinary-dispatch");
        _ = await coordinator.BeginAsync(s_scope, "first-selected", "inventory-0", new Dictionary<string, long>());
        await coordinator.StageAsync(s_scope, "first-selected", [Write(1)]);
        await coordinator.CommitAsync(s_scope, "first-selected");
        _ = await coordinator.BeginAsync(s_scope, "second-selected", "inventory-1", new Dictionary<string, long>());
        await coordinator.StageAsync(s_scope, "second-selected", [Write(2)]);
        intercepted.AfterPhysicalRead = () => coordinator.CommitAsync(s_scope, "second-selected");

        SharedProjectionReadResult<Counter> selected = await coordinator.ReadAsync<Counter>(s_scope, "index");

        selected.Generation.ShouldBe(2);
        selected.Value.ShouldNotBeNull().Value.ShouldBe(2);
        selected.IsStale.ShouldBeFalse();
    }

    [Fact]
    public async Task SingleKeyReadClassifiesPostReadStateOutageAsUnavailable() {
        var inner = new InMemoryReadModelStore();
        var intercepted = new SelectedReadInterleavingStore(inner);
        var coordinator = new SharedProjectionEpochCoordinator(intercepted, inner);
        _ = await coordinator.RegisterWriterAsync(s_scope, "ordinary-dispatch");
        _ = await coordinator.BeginAsync(s_scope, "selected-outage", "inventory-0", new Dictionary<string, long>());
        await coordinator.StageAsync(s_scope, "selected-outage", [Write(1)]);
        await coordinator.CommitAsync(s_scope, "selected-outage");
        await coordinator.WaitForPromotedCleanupAsync(s_scope);
        intercepted.FailStateAfterPhysicalRead = true;

        _ = await Should.ThrowAsync<SharedProjectionReadUnavailableException>(() =>
            coordinator.ReadAsync<Counter>(s_scope, "index"));
    }

    [Fact]
    public async Task SingleKeyReadClassifiesDaprPostReadStateOutageAsUnavailable() {
        var inner = new InMemoryReadModelStore();
        var intercepted = new SelectedReadInterleavingStore(inner);
        var coordinator = new SharedProjectionEpochCoordinator(intercepted, inner);
        _ = await coordinator.RegisterWriterAsync(s_scope, "ordinary-dispatch");
        _ = await coordinator.BeginAsync(s_scope, "selected-dapr-outage", "inventory-0",
            new Dictionary<string, long>());
        await coordinator.StageAsync(s_scope, "selected-dapr-outage", [Write(1)]);
        await coordinator.CommitAsync(s_scope, "selected-dapr-outage");
        await coordinator.WaitForPromotedCleanupAsync(s_scope);
        intercepted.FailDaprStateAfterPhysicalRead = true;

        _ = await Should.ThrowAsync<SharedProjectionReadUnavailableException>(() =>
            coordinator.ReadAsync<Counter>(s_scope, "index"));
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

    private static async Task<SharedProjectionEpochCoordinator> PrepareSequenceSeedAsync(
        InMemoryReadModelStore store, string operationId, int position) {
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(s_scope, "ordinary-dispatch");
        _ = await coordinator.BeginAsync(s_scope, operationId, "inventory",
            new Dictionary<string, long> { ["widget-a"] = position });
        _ = await coordinator.StageAsync(s_scope, operationId, [Write(position)]);
        await coordinator.CommitAsync(s_scope, operationId, requireOwnerSequenceHandoff: true);
        return coordinator;
    }

    private static byte[] CapturedIdentities(int count)
        => Enumerable.Range(1, count).SelectMany(position => SharedProjectionCapturedEventIdentity.Compute(
            "widget-a", position, position, JsonSerializer.SerializeToUtf8Bytes(1))).ToArray();

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

    private sealed class SelectedReadInterleavingStore(InMemoryReadModelStore inner) : IReadModelStore {
        public Func<Task>? AfterPhysicalRead { get; set; }

        public bool FailStateAfterPhysicalRead { get; set; }

        public bool FailDaprStateAfterPhysicalRead { get; set; }

        private bool _failStateRead;

        private bool _failDaprStateRead;

        public async Task<ReadModelEntry<TValue>> GetAsync<TValue>(
            string storeName, string key, CancellationToken cancellationToken = default) where TValue : class {
            if (_failStateRead && key == s_scope.StateKey) {
                _failStateRead = false;
                throw new HttpRequestException("injected post-read state outage");
            }
            if (_failDaprStateRead && key == s_scope.StateKey) {
                _failDaprStateRead = false;
                throw new Dapr.DaprException("injected Dapr post-read state outage");
            }

            ReadModelEntry<TValue> result = await inner.GetAsync<TValue>(storeName, key, cancellationToken);
            if (key.StartsWith("shared-generation:", StringComparison.Ordinal)) {
                if (FailStateAfterPhysicalRead) {
                    FailStateAfterPhysicalRead = false;
                    _failStateRead = true;
                }
                if (FailDaprStateAfterPhysicalRead) {
                    FailDaprStateAfterPhysicalRead = false;
                    _failDaprStateRead = true;
                }

                Func<Task>? action = AfterPhysicalRead;
                AfterPhysicalRead = null;
                if (action is not null) await action();
            }

            return result;
        }

        public Task SaveAsync<TValue>(string storeName, string key, TValue value,
            CancellationToken cancellationToken = default) where TValue : class
            => inner.SaveAsync(storeName, key, value, cancellationToken);

        public Task<bool> TrySaveAsync<TValue>(string storeName, string key, TValue value, string etag,
            CancellationToken cancellationToken = default) where TValue : class
            => inner.TrySaveAsync(storeName, key, value, etag, cancellationToken);
    }
}
