using System.Security.Cryptography;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Fakes;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

public sealed class SharedProjectionStreamedStageTests
{
    private static readonly SharedProjectionScope s_scope = new("statestore", "tenant-stream", "widget",
        "streamed-index", ["ordinary-dispatch"]);

    [Fact]
    public async Task TenThousandHighEntropyRowsBeyond64MiBPromoteOnlyAfterSealedStream()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        ReadModelBatchOperation sample = ReadModelBatchOperation.Write("item-00000",
            new LargeRow(new byte[6_000]), ReadModelBatchConcurrency.LastWrite);
        ((long)sample.CanonicalValue.Length * 10_000).ShouldBeGreaterThan(64L * 1024 * 1024);
        _ = await coordinator.RegisterWriterAsync(s_scope, "ordinary-dispatch");
        _ = await coordinator.BeginAsync(s_scope, "large-stream", "inventory-10000", new Dictionary<string, long>());
        string fingerprint = await coordinator.StageStreamingAsync(s_scope, "large-stream", Rows(10_000, 6_000));
        fingerprint.ShouldStartWith("stream:");
        SharedProjectionEpochState staged = store.Snapshot<SharedProjectionEpochState>(s_scope.StoreName, s_scope.StateKey)!;
        staged.StageStreaming.ShouldBeTrue();
        staged.StageCompletedPageCount.ShouldBeGreaterThan(1);
        staged.StageCompletedPageCount.ShouldBe(staged.StageSealedPageCount);
        (await coordinator.ReadAsync<LargeRow>(s_scope, "item-00000")).Value.ShouldBeNull();
        string operationDigest = Convert.ToHexString(SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes("large-stream")));
        var pageSamples = new List<string>(staged.StageSealedPageCount);
        var metadataKeys = new List<(string Page, string Marker)>(staged.StageSealedPageCount);
        for (int ordinal = 0; ordinal < staged.StageSealedPageCount; ordinal++)
        {
            string pageKey = s_scope.StateKey + ":stage-page:" + staged.Epoch + ":" + operationDigest + ":" + ordinal;
            SharedProjectionStreamPage page = (await store.GetAsync<SharedProjectionStreamPage>(
                s_scope.StoreName, pageKey)).Value.ShouldNotBeNull();
            page.Ordinal.ShouldBe(ordinal);
            pageSamples.Add(page.Keys[0]);
            string marker = s_scope.StateKey + ":stage-key:" + staged.Epoch + ":"
                + Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(
                    "large-stream\0" + page.Keys[0])));
            metadataKeys.Add((pageKey, marker));
        }

        await coordinator.CommitAsync(s_scope, "large-stream");
        (await coordinator.ReadAsync<LargeRow>(s_scope, "item-00000")).Value!.Payload.Length.ShouldBe(6_000);
        (await coordinator.ReadAsync<LargeRow>(s_scope, "item-09999")).Value!.Payload.Length.ShouldBe(6_000);
        foreach (string key in pageSamples)
        {
            (await coordinator.ReadAsync<LargeRow>(s_scope, key)).Value.ShouldNotBeNull().Payload.Length.ShouldBe(6_000);
        }
        _ = await coordinator.BeginAsync(s_scope, "reconcile-large-stream", "inventory-reconcile",
            new Dictionary<string, long>());
        foreach ((string page, string marker) in metadataKeys)
        {
            (await store.GetAsync<SharedProjectionStreamPage>(s_scope.StoreName, page)).Value.ShouldBeNull();
            (await store.GetAsync<SharedProjectionStreamKey>(s_scope.StoreName, marker)).Value.ShouldBeNull();
        }
        for (int ordinal = 0; ordinal < staged.StageSealedPageCount; ordinal++)
        {
            string batchId = "stage-metadata:" + staged.Epoch + ":" + operationDigest + ":" + ordinal;
            string markerKey = new ReadModelBatchScope(s_scope.StoreName, s_scope.TenantId,
                s_scope.Domain, s_scope.Family, s_scope.Family, batchId).ComputeMarkerKey();
            (await store.GetAsync<object>(s_scope.StoreName, markerKey)).Value.ShouldBeNull();
        }
    }

    [Fact]
    public async Task RejectedPageReservationNeverWritesGenerationAndRetryCanSeal()
    {
        var inner = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(inner, inner);
        _ = await coordinator.RegisterWriterAsync(s_scope, "ordinary-dispatch");
        _ = await coordinator.BeginAsync(s_scope, "retry-stream", "inventory-retry", new Dictionary<string, long>());
        bool rejectMetadata = true;
        inner.BatchFaultHook = (phase, _, _) =>
        {
            if (rejectMetadata && phase == ReadModelBatchPhase.BeforeDispatch)
            {
                rejectMetadata = false;
                throw new InvalidOperationException("Injected metadata dispatch failure.");
            }

            return Task.CompletedTask;
        };
        _ = await Should.ThrowAsync<InvalidOperationException>(() => coordinator.StageStreamingAsync(
            s_scope, "retry-stream", Rows(2, 100)));
        (await coordinator.ReadGenerationAsync<LargeRow>(s_scope, 1, "item-00000")).Value.ShouldBeNull();
        _ = await Should.ThrowAsync<InvalidOperationException>(() => coordinator.CommitAsync(s_scope, "retry-stream"));

        inner.BatchFaultHook = null;
        _ = await coordinator.StageStreamingAsync(s_scope, "retry-stream", Rows(2, 100));
        await coordinator.CommitAsync(s_scope, "retry-stream");
        (await coordinator.ReadAsync<LargeRow>(s_scope, "item-00001")).Value!.Payload.Length.ShouldBe(100);
    }

    [Fact]
    public async Task DuplicateAcrossPagesCannotSealAndAbortDeletesPrivateGeneration()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(s_scope, "ordinary-dispatch");
        _ = await coordinator.BeginAsync(s_scope, "duplicate-stream", "inventory", new Dictionary<string, long>());
        _ = await Should.ThrowAsync<InvalidOperationException>(() => coordinator.StageStreamingAsync(
            s_scope, "duplicate-stream", DuplicateRows()));
        _ = await Should.ThrowAsync<InvalidOperationException>(() => coordinator.CommitAsync(s_scope, "duplicate-stream"));
        await coordinator.AbortAsync(s_scope, "duplicate-stream",
            (_, _, _) => Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>([]));
        (await coordinator.ReadGenerationAsync<LargeRow>(s_scope, 1, "item-00000")).Value.ShouldBeNull();
        (await coordinator.ReadAsync<LargeRow>(s_scope, "item-00000")).Generation.ShouldBe(0);
    }

    [Fact]
    public async Task DuplicateWithinOnePageDoesNotPinAbortCleanup()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(s_scope, "ordinary-dispatch");
        _ = await coordinator.BeginAsync(s_scope, "duplicate-one-page", "inventory", new Dictionary<string, long>());
        _ = await Should.ThrowAsync<ArgumentException>(() => coordinator.StageStreamingAsync(
            s_scope, "duplicate-one-page", DuplicateWithinPageRows()));

        await coordinator.AbortAsync(s_scope, "duplicate-one-page",
            (_, _, _) => Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>([]));
        (await coordinator.ReadGenerationAsync<LargeRow>(s_scope, 1, "item-00000")).Value.ShouldBeNull();
        _ = await coordinator.BeginAsync(s_scope, "after-duplicate", "next-inventory", new Dictionary<string, long>());
    }

    [Fact]
    public async Task InterruptedSecondPageReplaysFromDurablePageAndPromotesCompleteGeneration()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(s_scope, "ordinary-dispatch");
        _ = await coordinator.BeginAsync(s_scope, "interrupted-stream", "inventory", new Dictionary<string, long>());
        int commits = 0;
        store.BatchFaultHook = (phase, _, _) =>
        {
            if (phase == ReadModelBatchPhase.BeforeCommit && ++commits == 2)
                throw new InvalidOperationException("Injected second-page crash.");
            return Task.CompletedTask;
        };
        _ = await Should.ThrowAsync<InvalidOperationException>(() => coordinator.StageStreamingAsync(
            s_scope, "interrupted-stream", Rows(40, 100)));
        (await coordinator.ReadAsync<LargeRow>(s_scope, "item-00000")).Value.ShouldBeNull();
        _ = await Should.ThrowAsync<InvalidOperationException>(() => coordinator.CommitAsync(s_scope, "interrupted-stream"));

        store.BatchFaultHook = null;
        var restarted = new SharedProjectionEpochCoordinator(store, store);
        _ = await restarted.StageStreamingAsync(s_scope, "interrupted-stream", Rows(40, 100));
        await restarted.CommitAsync(s_scope, "interrupted-stream");
        (await restarted.ReadAsync<LargeRow>(s_scope, "item-00000")).Value!.Payload.Length.ShouldBe(100);
        (await restarted.ReadAsync<LargeRow>(s_scope, "item-00039")).Value!.Payload.Length.ShouldBe(100);
    }

    [Fact]
    public async Task CommittedStreamMetadataCleanupRecoversAfterCrashAndRepeatedRebuilds()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(s_scope, "ordinary-dispatch");
        string? previousPageKey = null;
        string? previousMarkerKey = null;
        foreach (string operation in new[] { "first-cleanup", "second-cleanup" })
        {
            _ = await coordinator.BeginAsync(s_scope, operation, "inventory-" + operation,
                new Dictionary<string, long>());
            if (previousPageKey is not null)
            {
                (await store.GetAsync<SharedProjectionStreamPage>(s_scope.StoreName,
                    previousPageKey)).Value.ShouldBeNull();
                (await store.GetAsync<SharedProjectionStreamKey>(s_scope.StoreName,
                    previousMarkerKey!)).Value.ShouldBeNull();
            }
            _ = await coordinator.StageStreamingAsync(s_scope, operation, Rows(70, 100));
            SharedProjectionEpochState staged = store.Snapshot<SharedProjectionEpochState>(
                s_scope.StoreName, s_scope.StateKey)!;
            string pageKey = s_scope.StateKey + ":stage-page:" + staged.Epoch + ":"
                + Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(operation))) + ":0";
            SharedProjectionStreamPage page = (await store.GetAsync<SharedProjectionStreamPage>(
                s_scope.StoreName, pageKey)).Value.ShouldNotBeNull();
            string markerKey = s_scope.StateKey + ":stage-key:" + staged.Epoch + ":"
                + Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(
                    operation + "\0" + page.Keys[0])));

            if (operation == "first-cleanup")
            {
                bool failed = false;
                var faultObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                store.BatchFaultHook = (phase, _, _) =>
                {
                    if (!failed && phase == ReadModelBatchPhase.BeforeCommit)
                    {
                        failed = true;
                        faultObserved.TrySetResult();
                        throw new IOException("Injected metadata cleanup crash.");
                    }

                    return Task.CompletedTask;
                };
                await coordinator.CommitAsync(s_scope, operation);
                await faultObserved.Task.WaitAsync(TimeSpan.FromSeconds(10));
                (await coordinator.ReadAsync<LargeRow>(s_scope, page.Keys[0])).Value.ShouldNotBeNull();
                store.BatchFaultHook = null;
                coordinator = new SharedProjectionEpochCoordinator(store, store);
                previousPageKey = pageKey;
                previousMarkerKey = markerKey;
                continue;
            }

            await coordinator.CommitAsync(s_scope, operation);
            _ = await coordinator.BeginAsync(s_scope, "after-second-cleanup", "inventory-final",
                new Dictionary<string, long>());
            (await store.GetAsync<SharedProjectionStreamPage>(s_scope.StoreName, pageKey)).Value.ShouldBeNull();
            (await store.GetAsync<SharedProjectionStreamKey>(s_scope.StoreName, markerKey)).Value.ShouldBeNull();
            string stageReceiptKey = new ReadModelBatchScope(s_scope.StoreName, s_scope.TenantId,
                s_scope.Domain, s_scope.Family, s_scope.Family,
                $"stage:{staged.Epoch}:{operation}:0").ComputeMarkerKey();
            (await store.GetAsync<ReadModelBatchMarker>(s_scope.StoreName, stageReceiptKey)).Value.ShouldBeNull();
        }
    }

    [Fact]
    public async Task AbortedStreamReclaimsTerminalMutationReceipts()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(s_scope, "ordinary-dispatch");
        const string operation = "abort-receipts";
        _ = await coordinator.BeginAsync(s_scope, operation, "inventory-abort-receipts",
            new Dictionary<string, long>());
        _ = await coordinator.StageStreamingAsync(s_scope, operation, Rows(2, 100));
        SharedProjectionEpochState staged = store.Snapshot<SharedProjectionEpochState>(
            s_scope.StoreName, s_scope.StateKey)!;
        await coordinator.AbortAsync(s_scope, operation,
            (_, _, _) => Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>([]));
        foreach (string prefix in new[] { "stage", "abort" })
        {
            string receiptKey = new ReadModelBatchScope(s_scope.StoreName, s_scope.TenantId,
                s_scope.Domain, s_scope.Family, s_scope.Family,
                $"{prefix}:{staged.Epoch}:{operation}:0").ComputeMarkerKey();
            (await store.GetAsync<ReadModelBatchMarker>(s_scope.StoreName, receiptKey)).Value.ShouldBeNull();
        }
    }

    [Fact]
    public async Task MetadataWrittenAfterAbortIsRecoveredBeforeNextEpoch()
    {
        var inner = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(inner, inner);
        _ = await coordinator.RegisterWriterAsync(s_scope, "ordinary-dispatch");
        _ = await coordinator.BeginAsync(s_scope, "racing-stage", "inventory-racing",
            new Dictionary<string, long>());
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int pauses = 0;
        inner.BatchFaultHook = async (phase, _, token) =>
        {
            if (phase == ReadModelBatchPhase.BeforeDispatch && Interlocked.Increment(ref pauses) == 1)
            {
                reached.TrySetResult();
                await release.Task.WaitAsync(token);
            }
        };
        Task<string> stage = coordinator.StageStreamingAsync(s_scope, "racing-stage", Rows(2, 100));
        await reached.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await coordinator.AbortAsync(s_scope, "racing-stage",
            (_, _, _) => Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>([]));
        release.TrySetResult();
        _ = await Should.ThrowAsync<InvalidOperationException>(() => stage);
        string pageKey = s_scope.StateKey + ":stage-page:1:"
            + Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("racing-stage"))) + ":0";
        (await inner.GetAsync<SharedProjectionStreamPage>(s_scope.StoreName, pageKey)).Value.ShouldBeNull();

        _ = await coordinator.BeginAsync(s_scope, "after-race", "inventory-after-race",
            new Dictionary<string, long>());
        (await inner.GetAsync<SharedProjectionStreamPage>(s_scope.StoreName, pageKey)).Value.ShouldBeNull();
    }

    [Fact]
    public async Task NearLimitOperationIdCanStageAndCleanStreamMetadata()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        string operation = new('o', 500);
        _ = await coordinator.RegisterWriterAsync(s_scope, "ordinary-dispatch");
        _ = await coordinator.BeginAsync(s_scope, operation, "inventory-long-id",
            new Dictionary<string, long>());
        _ = await coordinator.StageStreamingAsync(s_scope, operation, Rows(2, 100));
        await coordinator.CommitAsync(s_scope, operation);
        (await coordinator.ReadAsync<LargeRow>(s_scope, "item-00000")).Value.ShouldNotBeNull();
        _ = await coordinator.BeginAsync(s_scope, "long-id-cleanup-recovery", "inventory-recovery",
            new Dictionary<string, long>());
        SharedProjectionEpochState state = store.Snapshot<SharedProjectionEpochState>(s_scope.StoreName, s_scope.StateKey)!;
        state.CleanupStreamPageCount.ShouldBe(0);
    }

    private static async IAsyncEnumerable<ReadModelBatchOperation> DuplicateRows()
    {
        await foreach (ReadModelBatchOperation row in Rows(32, 100)) yield return row;
        yield return ReadModelBatchOperation.Write("item-00000", new LargeRow(new byte[100]),
            ReadModelBatchConcurrency.LastWrite);
    }

    private static async IAsyncEnumerable<ReadModelBatchOperation> DuplicateWithinPageRows()
    {
        await foreach (ReadModelBatchOperation row in Rows(1, 100)) yield return row;
        yield return ReadModelBatchOperation.Write("item-00000", new LargeRow(new byte[100]),
            ReadModelBatchConcurrency.LastWrite);
    }

    private static async IAsyncEnumerable<ReadModelBatchOperation> Rows(int count, int bytes)
    {
        await Task.CompletedTask;
        for (int index = 0; index < count; index++)
        {
            byte[] payload = new byte[bytes];
            // Stable high-entropy bytes let a crashed stage replay the exact page identities.
            for (int offset = 0; offset < bytes; offset += 32)
            {
                byte[] block = SHA256.HashData(BitConverter.GetBytes(((long)index << 32) | (uint)offset));
                block.AsSpan(0, Math.Min(32, bytes - offset)).CopyTo(payload.AsSpan(offset));
            }

            yield return ReadModelBatchOperation.Write("item-" + index.ToString("D5"),
                new LargeRow(payload), ReadModelBatchConcurrency.LastWrite);
        }
    }

    private sealed record LargeRow(byte[] Payload);

}
