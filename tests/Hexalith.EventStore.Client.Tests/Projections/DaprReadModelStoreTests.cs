using Dapr.Client;

using Hexalith.EventStore.Client.Projections;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

public class DaprReadModelStoreTests {
    private const string StoreName = "statestore";

    [Fact]
    public async Task GetManyAsync_UsesOneBoundedBulkCallAndReturnsMissingKeysInRequestOrder() {
        var daprClient = new RecordingDaprClient();
        daprClient.SeedByteStore("read-model:1", "{\"value\":42}"u8.ToArray());
        daprClient.SeedByteStore("read-model:3", "{\"value\":84}"u8.ToArray());
        var store = new DaprReadModelStore(daprClient);

        IReadOnlyList<ReadModelBulkEntry<DaprReadModelStoreTestModel>> entries = await store.GetManyAsync<DaprReadModelStoreTestModel>(
            StoreName,
            ["read-model:1", "read-model:2", "read-model:3"],
            parallelism: 7);

        entries.Select(static item => item.Key).ShouldBe(["read-model:1", "read-model:2", "read-model:3"]);
        entries[0].Value!.Value.ShouldBe(42);
        entries[1].Value.ShouldBeNull();
        entries[2].Value!.Value.ShouldBe(84);
        daprClient.BulkStateCallCount.ShouldBe(1);
        daprClient.BulkStateParallelism.ShouldBe(7);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TrySaveAsync_UsesFirstWriteConcurrencyPassesETagAndReturnsDaprResult(bool daprResult) {
        var daprClient = new RecordingDaprClient { TrySaveResult = daprResult };
        var store = new DaprReadModelStore(daprClient);
        var value = new DaprReadModelStoreTestModel { Value = 42 };

        bool saved = await store.TrySaveAsync(StoreName, "read-model:1", value, "etag-1");

        saved.ShouldBe(daprResult);
        daprClient.StoreName.ShouldBe(StoreName);
        daprClient.Key.ShouldBe("read-model:1");
        daprClient.Value.ShouldBe(value);
        daprClient.ETag.ShouldBe("etag-1");
        StateOptions options = daprClient.StateOptions.ShouldNotBeNull();
        options.Concurrency.ShouldBe(ConcurrencyMode.FirstWrite);
    }

    [Fact]
    public async Task FailedCreateOnlyWritesReadBackExistingReceiptsWithoutChangingETagConventions() {
        var client = new RecordingDaprClient { PersistTypedState = true };
        var store = new DaprReadModelStore(client);
        var receipt = new DaprReadModelStoreTestModel { Value = 42 };

        (await store.TrySaveAsync(StoreName, "receipt", receipt, string.Empty)).ShouldBeTrue();
        client.ETag.ShouldBe(string.Empty);
        (await store.TrySaveAsync(StoreName, "receipt", new DaprReadModelStoreTestModel { Value = 99 }, string.Empty)).ShouldBeFalse();
        (await store.GetAsync<DaprReadModelStoreTestModel>(StoreName, "receipt")).Value!.Value.ShouldBe(42);
        (await store.TrySaveWithTimeToLiveAsync(StoreName, "receipt", receipt, string.Empty, TimeSpan.FromMinutes(1))).ShouldBeFalse();
        client.ETag.ShouldBe(string.Empty);
        (await store.TrySaveWithTimeToLiveAsync(StoreName, "expiring", receipt, string.Empty, TimeSpan.FromMinutes(1))).ShouldBeTrue();
        ReadModelEntry<DaprReadModelStoreTestModel> current = await store.GetAsync<DaprReadModelStoreTestModel>(StoreName, "expiring");
        (await store.TrySaveWithTimeToLiveAsync(StoreName, "expiring", new DaprReadModelStoreTestModel { Value = 99 },
            current.ETag!, TimeSpan.FromMinutes(1))).ShouldBeTrue();
        client.ETag.ShouldBe(current.ETag);
        (await store.GetAsync<DaprReadModelStoreTestModel>(StoreName, "expiring")).Value!.Value.ShouldBe(99);

        var accessor = new DaprReadModelBatchStateAccessor(client, StoreName);
        (await accessor.TryWriteAsync("raw-receipt", "{}"u8.ToArray(), string.Empty, CancellationToken.None)).ShouldBeTrue();
        client.TrySaveByteOperations[^1].ETag.ShouldBe(string.Empty);
        (await accessor.TryWriteAsync("raw-receipt", "{}"u8.ToArray(), string.Empty, CancellationToken.None)).ShouldBeFalse();
        RawStateEntry raw = await accessor.ReadAsync("raw-receipt", CancellationToken.None);
        (await accessor.TryWriteAsync("raw-receipt", "{\"value\":99}"u8.ToArray(), raw.ETag, CancellationToken.None)).ShouldBeTrue();
        client.TrySaveByteOperations[^1].ETag.ShouldBe(raw.ETag);
    }

    [Fact]
    public async Task ReconstructedCatchUpDequeuesAppliedReceiptAfterCloseoutCancellation() {
        var client = new RecordingDaprClient { PersistTypedState = true };
        var store = new DaprReadModelStore(client);
        var scope = new SharedProjectionScope(StoreName, "tenant-1", "counter", "family", ["writer"]);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        SharedProjectionLease lease = await coordinator.RegisterWriterAsync(scope, "writer");
        var delivery = new SharedProjectionDelivery("counter-1", 11, "{}"u8.ToArray(), "11"u8.ToArray(), OwnerSequenceNumber: 5);
        client.SeedByteStore(scope.CheckpointKey(delivery.SourceStream), System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(
            new SharedProjectionCheckpoint(10, "10"u8.ToArray(), 4), client.JsonSerializerOptions));
        (await coordinator.JournalAsync(scope, lease, delivery)).ShouldBe(SharedProjectionJournalResult.Journaled);
        using var cancellation = new CancellationTokenSource();
        client.BeforeTrySaveByteState = (key, _) => {
            if (key.StartsWith("shared-receipt:", StringComparison.Ordinal)) cancellation.Cancel();
        };
        await Should.ThrowAsync<OperationCanceledException>(() => coordinator.CatchUpAsync(scope,
            (_, _, _) => Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>(
                [ReadModelBatchOperation.Write("membership", new DaprReadModelStoreTestModel { Value = 42 }, ReadModelBatchConcurrency.LastWrite)]),
            cancellation.Token));
        (await coordinator.GetStatusAsync(scope)).PendingDeliveryCount.ShouldBe(1);
        (await coordinator.GetCheckpointAsync(scope, delivery.SourceStream))!.Position.ShouldBe(11);
        string receiptKey = scope.ReceiptKey(0, delivery.SourceStream, delivery.EnvelopePosition);
        byte[] receipt = client.ByteStoreValue(receiptKey).ShouldNotBeNull().ToArray();

        client.BeforeTrySaveByteState = null!;
        var restarted = new SharedProjectionEpochCoordinator(store, store);
        (await restarted.CatchUpAsync(scope, (_, _, _) => throw new InvalidOperationException("Prepared evidence must not refold."))).ShouldBe(1);
        (await restarted.GetStatusAsync(scope)).PendingDeliveryCount.ShouldBe(0);
        client.ByteStoreValue(receiptKey).ShouldBe(receipt);
        (await restarted.ReadAsync<DaprReadModelStoreTestModel>(scope, "membership")).Value!.Value.ShouldBe(42);
    }

    [Theory]
    [InlineData("missing", false)]
    [InlineData("read-failed", false)]
    [InlineData("caller-cancelled", false)]
    [InlineData("update", false)]
    [InlineData("missing", true)]
    [InlineData("read-failed", true)]
    [InlineData("caller-cancelled", true)]
    [InlineData("update", true)]
    public async Task FailedCreatePreservesFailureWithoutExistingKeyProof(string failure, bool raw) {
        var client = new RecordingDaprClient { PersistTypedState = true };
        var store = new DaprReadModelStore(client);
        var original = new Dapr.DaprException("Ambiguous write failure.");
        using var cancellation = new CancellationTokenSource();
        int reads = 0;
        client.BeforeTrySaveByteState = (_, _) => {
            if (failure == "caller-cancelled") cancellation.Cancel();
            throw original;
        };
        client.BeforeReadByteState = _ => {
            reads++;
            if (failure == "read-failed") throw new IOException("Read is unavailable.");
        };
        string etag = failure == "update" ? "existing-etag" : string.Empty;
        Task<bool> WriteAsync() => raw
            ? new DaprReadModelBatchStateAccessor(client, StoreName).TryWriteAsync("receipt", "{}"u8.ToArray(), etag, cancellation.Token)
            : store.TrySaveAsync(StoreName, "receipt", new DaprReadModelStoreTestModel { Value = 42 }, etag, cancellation.Token);

        (await Should.ThrowAsync<Dapr.DaprException>(WriteAsync)).ShouldBeSameAs(original);
        reads.ShouldBe(failure is "update" or "caller-cancelled" ? 0 : 1);
    }

    [Fact]
    public async Task CancellationDuringFailedCreateReadBackRemainsCancellation() {
        var client = new RecordingDaprClient { PersistTypedState = true };
        client.SeedByteStore("receipt", "{}"u8.ToArray());
        var store = new DaprReadModelStore(client);
        using var cancellation = new CancellationTokenSource();
        client.BeforeReadByteState = _ => cancellation.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(() => store.TrySaveAsync(StoreName, "receipt",
            new DaprReadModelStoreTestModel { Value = 42 }, string.Empty, cancellation.Token));
        cancellation.IsCancellationRequested.ShouldBeTrue();
    }

    [Fact]
    public async Task TrySaveWithTimeToLiveAsync_RoundsUpAndPassesDaprTtlMetadata() {
        var daprClient = new RecordingDaprClient { TrySaveResult = true };
        var store = new DaprReadModelStore(daprClient);

        bool saved = await store.TrySaveWithTimeToLiveAsync(
            StoreName,
            "dispatch-ledger:1",
            new DaprReadModelStoreTestModel { Value = 42 },
            string.Empty,
            TimeSpan.FromMilliseconds(1500));

        saved.ShouldBeTrue();
        daprClient.Metadata["ttlInSeconds"].ShouldBe("2");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TryEraseAsync_UsesFirstWriteConcurrencyPassesETagAndReturnsDaprResult(bool daprResult) {
        var daprClient = new RecordingDaprClient { TryDeleteResult = daprResult };
        var store = new DaprReadModelStore(daprClient);

        bool erased = await store.TryEraseAsync(StoreName, "read-model:1", "etag-1");

        erased.ShouldBe(daprResult);
        daprClient.StoreName.ShouldBe(StoreName);
        daprClient.Key.ShouldBe("read-model:1");
        daprClient.ETag.ShouldBe("etag-1");
        StateOptions options = daprClient.StateOptions.ShouldNotBeNull();
        options.Concurrency.ShouldBe(ConcurrencyMode.FirstWrite);
    }

    [Fact]
    public async Task TryEraseAsync_Cancelled_ThrowsOperationCanceledException() {
        var daprClient = new RecordingDaprClient { TryDeleteResult = true };
        var store = new DaprReadModelStore(daprClient);
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            () => store.TryEraseAsync(StoreName, "read-model:1", "etag-1", source.Token));
    }

    [Fact]
    public async Task TryReadEtagAsync_PresentValue_ReturnsPresentTrueWithEtagUsableForErase() {
        var daprClient = new RecordingDaprClient();
        daprClient.SeedByteStore("read-model:1", "{\"value\":42}"u8.ToArray());
        var store = new DaprReadModelStore(daprClient);

        (bool present, string etag) = await store.TryReadEtagAsync(StoreName, "read-model:1");

        present.ShouldBeTrue();
        etag.ShouldNotBeNullOrEmpty();

        // The ETag mirrors the store's visibility read, so it drives a first-write-wins erase.
        bool erased = await store.TryEraseAsync(StoreName, "read-model:1", etag);
        erased.ShouldBeTrue();
        daprClient.ByteStoreContains("read-model:1").ShouldBeFalse();
    }

    [Fact]
    public async Task TryReadEtagAsync_AbsentValue_ReturnsPresentFalseWithEmptyEtag() {
        var daprClient = new RecordingDaprClient();
        var store = new DaprReadModelStore(daprClient);

        (bool present, string etag) = await store.TryReadEtagAsync(StoreName, "missing");

        present.ShouldBeFalse();
        etag.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task TryReadEtagAsync_Cancelled_ThrowsOperationCanceledException() {
        var daprClient = new RecordingDaprClient();
        var store = new DaprReadModelStore(daprClient);
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            () => store.TryReadEtagAsync(StoreName, "read-model:1", source.Token));
    }
}
