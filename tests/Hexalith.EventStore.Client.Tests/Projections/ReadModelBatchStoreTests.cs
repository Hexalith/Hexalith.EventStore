using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Fakes;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

/// <summary>
/// Deterministic coordinated-batch protocol tests over the in-memory fake, which runs the exact same
/// protocol engine as the DAPR adapter. Assertions inspect persisted detail/index/marker/envelope state,
/// not call counts.
/// </summary>
public class ReadModelBatchStoreTests {
    private const string Store = "statestore";

    public sealed record Detail(int Version);

    public sealed record IndexEntry(int Count);

    private static ReadModelBatchScope Scope(string batchId = "01J0BATCH0000000000000000") =>
        new(Store, "tenant-1", "counter", "agg-1", "counterView", batchId);

    private static ReadModelBatch WriteBatch(string batchId = "01J0BATCH0000000000000000") =>
        new(
            Scope(batchId),
            [
                ReadModelBatchOperation.Write("detail:agg-1", new Detail(2), ReadModelBatchConcurrency.LastWrite),
                ReadModelBatchOperation.Write("index:counterView", new IndexEntry(2), ReadModelBatchConcurrency.LastWrite),
            ]);

    // ----- Validation (before any state access) -----

    [Fact]
    public void ReadModelBatch_EmptyManifest_Throws() =>
        Should.Throw<ArgumentException>(() => new ReadModelBatch(Scope(), []));

    [Fact]
    public void ReadModelBatch_DuplicateLogicalKey_Throws() =>
        Should.Throw<ArgumentException>(() => new ReadModelBatch(
            Scope(),
            [
                ReadModelBatchOperation.Write("k", new Detail(1), ReadModelBatchConcurrency.LastWrite),
                ReadModelBatchOperation.Write("k", new Detail(2), ReadModelBatchConcurrency.LastWrite),
            ]));

    [Fact]
    public void WriteOperation_WithIdempotentAbsentConcurrency_Throws() =>
        Should.Throw<ArgumentException>(() =>
            ReadModelBatchOperation.Write("k", new Detail(1), ReadModelBatchConcurrency.IdempotentAbsent));

    [Fact]
    public void DeleteOperation_WithUnconditionalConcurrency_Throws() =>
        Should.Throw<ArgumentException>(() =>
            ReadModelBatchOperation.Delete("k", ReadModelBatchConcurrency.LastWrite));

    [Fact]
    public void DeleteOperation_WithCreateOnlyConcurrency_Throws() =>
        Should.Throw<ArgumentException>(() =>
            ReadModelBatchOperation.Delete("k", ReadModelBatchConcurrency.CreateOnly));

    [Fact]
    public async Task ExecuteAsync_ExceedingOperationLimit_ThrowsBeforeStateAccess() {
        var store = new InMemoryReadModelStore();
        store.BatchOptions.MaxOperations = 1;

        _ = await Should.ThrowAsync<ArgumentException>(() => store.ExecuteAsync(WriteBatch()));
        store.Count.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_KeyExceedingByteLimit_ThrowsBeforeStateAccess() {
        var store = new InMemoryReadModelStore();
        store.BatchOptions.MaxKeyByteLength = 4;

        _ = await Should.ThrowAsync<ArgumentException>(() => store.ExecuteAsync(WriteBatch()));
        store.Count.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_ManifestExceedingByteLimit_ThrowsBeforeStateAccess() {
        var store = new InMemoryReadModelStore();
        store.BatchOptions.MaxCanonicalManifestBytes = 8;

        _ = await Should.ThrowAsync<ArgumentException>(() => store.ExecuteAsync(WriteBatch()));
        store.Count.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_CancelledBeforeDispatch_ThrowsWithoutStateAccess() {
        var store = new InMemoryReadModelStore();
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(() => store.ExecuteAsync(WriteBatch(), source.Token));
        store.Count.ShouldBe(0);
    }

    // ----- Success and idempotency -----

    [Fact]
    public async Task ExecuteAsync_MultiOperationSuccess_PersistsEveryOperationAndCompactsEnvelopes() {
        var store = new InMemoryReadModelStore();

        ReadModelBatchResult result = await store.ExecuteAsync(WriteBatch());

        result.Status.ShouldBe(ReadModelBatchStatus.Completed);
        (await store.GetAsync<Detail>(Store, "detail:agg-1")).Value!.Version.ShouldBe(2);
        (await store.GetAsync<IndexEntry>(Store, "index:counterView")).Value!.Count.ShouldBe(2);
        store.HasPendingEnvelope(Store, "detail:agg-1").ShouldBeFalse();
        store.Snapshot<Detail>(Store, "detail:agg-1")!.Version.ShouldBe(2);
    }

    [Fact]
    public async Task ExecuteAsync_CompletedRetry_SameFingerprint_ReturnsAlreadyCompletedWithoutReapplying() {
        var store = new InMemoryReadModelStore();
        _ = await store.ExecuteAsync(WriteBatch());

        // Simulate a later out-of-band change; a genuine reapplication would clobber it.
        await store.SaveAsync(Store, "detail:agg-1", new Detail(99));

        ReadModelBatchResult retry = await store.ExecuteAsync(WriteBatch());

        retry.Status.ShouldBe(ReadModelBatchStatus.AlreadyCompleted);
        store.Snapshot<Detail>(Store, "detail:agg-1")!.Version.ShouldBe(99);
    }

    [Fact]
    public async Task ExecuteAsync_IdentityReuse_DifferentFingerprint_ReturnsIdentityConflict() {
        var store = new InMemoryReadModelStore();
        _ = await store.ExecuteAsync(WriteBatch());

        ReadModelBatch different = new(
            Scope(),
            [ReadModelBatchOperation.Write("detail:agg-1", new Detail(3), ReadModelBatchConcurrency.LastWrite)]);
        ReadModelBatchResult result = await store.ExecuteAsync(different);

        result.Status.ShouldBe(ReadModelBatchStatus.Conflict);
        result.ConflictKind.ShouldBe(ReadModelBatchConflictKind.Identity);
        store.Snapshot<Detail>(Store, "detail:agg-1")!.Version.ShouldBe(2);
    }

    // ----- Optimistic concurrency -----

    [Fact]
    public async Task ExecuteAsync_ExpectedETagWrite_Match_Succeeds() {
        var store = new InMemoryReadModelStore();
        await store.SaveAsync(Store, "detail:agg-1", new Detail(1));
        string etag = (await store.GetAsync<Detail>(Store, "detail:agg-1")).ETag!;

        ReadModelBatch batch = new(
            Scope(),
            [ReadModelBatchOperation.Write("detail:agg-1", new Detail(2), ReadModelBatchConcurrency.Match(etag))]);
        ReadModelBatchResult result = await store.ExecuteAsync(batch);

        result.Status.ShouldBe(ReadModelBatchStatus.Completed);
        store.Snapshot<Detail>(Store, "detail:agg-1")!.Version.ShouldBe(2);
    }

    [Fact]
    public async Task ExecuteAsync_ExpectedETagWrite_StaleEtag_ConflictsAndPreservesValue() {
        var store = new InMemoryReadModelStore();
        await store.SaveAsync(Store, "detail:agg-1", new Detail(1));

        ReadModelBatch batch = new(
            Scope(),
            [ReadModelBatchOperation.Write("detail:agg-1", new Detail(2), ReadModelBatchConcurrency.Match("stale"))]);
        ReadModelBatchResult result = await store.ExecuteAsync(batch);

        result.Status.ShouldBe(ReadModelBatchStatus.Conflict);
        result.ConflictKind.ShouldBe(ReadModelBatchConflictKind.Optimistic);
        store.Snapshot<Detail>(Store, "detail:agg-1")!.Version.ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteAsync_PreCommitConflict_CompensatesRestoringPreviousView() {
        var store = new InMemoryReadModelStore();
        await store.SaveAsync(Store, "index:counterView", new IndexEntry(1));

        // op0 installs a detail envelope, op1 (create-only on an existing key) conflicts -> compensate op0.
        ReadModelBatch batch = new(
            Scope(),
            [
                ReadModelBatchOperation.Write("detail:agg-1", new Detail(2), ReadModelBatchConcurrency.LastWrite),
                ReadModelBatchOperation.Write("index:counterView", new IndexEntry(2), ReadModelBatchConcurrency.CreateOnly),
            ]);
        ReadModelBatchResult result = await store.ExecuteAsync(batch);

        result.Status.ShouldBe(ReadModelBatchStatus.Conflict);
        result.ConflictKind.ShouldBe(ReadModelBatchConflictKind.Optimistic);
        (await store.GetAsync<Detail>(Store, "detail:agg-1")).Value.ShouldBeNull();
        store.HasPendingEnvelope(Store, "detail:agg-1").ShouldBeFalse();
        store.Snapshot<IndexEntry>(Store, "index:counterView")!.Count.ShouldBe(1);
    }

    // ----- Resumable visibility, interruption, and convergence -----

    [Fact]
    public async Task ExecuteAsync_InterruptedBeforeCommit_KeepsPreviousViewThenConvergesOnRetry() {
        var store = new InMemoryReadModelStore();
        await store.SaveAsync(Store, "detail:agg-1", new Detail(1));
        await store.SaveAsync(Store, "index:counterView", new IndexEntry(1));

        store.BatchFaultHook = (phase, _, _) =>
            phase == ReadModelBatchPhase.BeforeCommit
                ? throw new TimeoutException("interrupted before commit")
                : Task.CompletedTask;

        ReadModelBatchResult interrupted = await store.ExecuteAsync(WriteBatch());

        interrupted.Status.ShouldBe(ReadModelBatchStatus.Incomplete);
        // Readers still observe the previous complete pair while the marker is prepared.
        (await store.GetAsync<Detail>(Store, "detail:agg-1")).Value!.Version.ShouldBe(1);
        (await store.GetAsync<IndexEntry>(Store, "index:counterView")).Value!.Count.ShouldBe(1);
        store.HasPendingEnvelope(Store, "detail:agg-1").ShouldBeTrue();

        // Same-identity retry converges.
        store.BatchFaultHook = null;
        ReadModelBatchResult retry = await store.ExecuteAsync(WriteBatch());

        retry.Status.ShouldBe(ReadModelBatchStatus.Completed);
        (await store.GetAsync<Detail>(Store, "detail:agg-1")).Value!.Version.ShouldBe(2);
        store.HasPendingEnvelope(Store, "detail:agg-1").ShouldBeFalse();
        store.Snapshot<Detail>(Store, "detail:agg-1")!.Version.ShouldBe(2);
    }

    [Fact]
    public async Task ExecuteAsync_AmbiguousAfterCommit_ReconcilesToCompleted() {
        var store = new InMemoryReadModelStore();
        await store.SaveAsync(Store, "detail:agg-1", new Detail(1));
        await store.SaveAsync(Store, "index:counterView", new IndexEntry(1));

        // A transient crash before the receipt is written: throw once, then let reconciliation finish.
        bool thrown = false;
        store.BatchFaultHook = (phase, _, _) => {
            if (phase == ReadModelBatchPhase.BeforeReceipt && !thrown) {
                thrown = true;
                throw new TimeoutException("interrupted before receipt");
            }

            return Task.CompletedTask;
        };

        ReadModelBatchResult result = await store.ExecuteAsync(WriteBatch());

        result.Status.ShouldBe(ReadModelBatchStatus.Completed);
        (await store.GetAsync<Detail>(Store, "detail:agg-1")).Value!.Version.ShouldBe(2);
    }

    [Fact]
    public async Task ExecuteAsync_PostDispatchCancellation_ReconcilesWithoutRollback() {
        var store = new InMemoryReadModelStore();
        await store.SaveAsync(Store, "detail:agg-1", new Detail(1));
        await store.SaveAsync(Store, "index:counterView", new IndexEntry(1));

        using var source = new CancellationTokenSource();
        store.BatchFaultHook = (phase, _, ct) => {
            if (phase == ReadModelBatchPhase.MarkerPrepared) {
                source.Cancel();
                ct.ThrowIfCancellationRequested();
            }

            return Task.CompletedTask;
        };

        ReadModelBatchResult result = await store.ExecuteAsync(WriteBatch(), source.Token);

        // Cancellation is never a rollback: the prepared marker is retained and reconciliation reports a
        // proven (incomplete) outcome, not success.
        result.IsSuccess.ShouldBeFalse();
        result.Status.ShouldBe(ReadModelBatchStatus.Incomplete);

        store.BatchFaultHook = null;
        ReadModelBatchResult retry = await store.ExecuteAsync(WriteBatch());
        retry.Status.ShouldBe(ReadModelBatchStatus.Completed);
        (await store.GetAsync<Detail>(Store, "detail:agg-1")).Value!.Version.ShouldBe(2);
    }

    // ----- Transaction-qualified profile -----

    [Fact]
    public async Task ExecuteAsync_TransactionQualifiedProfile_WritesAndDeletesWithoutEnvelopes() {
        var store = new InMemoryReadModelStore();
        store.BatchOptions.StoreProfiles[Store] = ReadModelBatchStoreProfile.TransactionQualified;
        await store.SaveAsync(Store, "index:counterView", new IndexEntry(1));

        ReadModelBatch batch = new(
            Scope(),
            [
                ReadModelBatchOperation.Write("detail:agg-1", new Detail(2), ReadModelBatchConcurrency.LastWrite),
                ReadModelBatchOperation.Delete("index:counterView", ReadModelBatchConcurrency.IdempotentAbsent),
            ]);
        ReadModelBatchResult result = await store.ExecuteAsync(batch);

        result.Status.ShouldBe(ReadModelBatchStatus.Completed);
        // Transaction path persists raw canonical values (never an envelope).
        store.HasPendingEnvelope(Store, "detail:agg-1").ShouldBeFalse();
        store.Snapshot<Detail>(Store, "detail:agg-1")!.Version.ShouldBe(2);
        (await store.GetAsync<IndexEntry>(Store, "index:counterView")).Value.ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_DeleteOperation_RemovesValueAfterCommit() {
        var store = new InMemoryReadModelStore();
        string etag;
        await store.SaveAsync(Store, "detail:agg-1", new Detail(1));
        etag = (await store.GetAsync<Detail>(Store, "detail:agg-1")).ETag!;

        ReadModelBatch batch = new(
            Scope(),
            [ReadModelBatchOperation.Delete("detail:agg-1", ReadModelBatchConcurrency.Match(etag))]);
        ReadModelBatchResult result = await store.ExecuteAsync(batch);

        result.Status.ShouldBe(ReadModelBatchStatus.Completed);
        (await store.GetAsync<Detail>(Store, "detail:agg-1")).Value.ShouldBeNull();
    }
}
