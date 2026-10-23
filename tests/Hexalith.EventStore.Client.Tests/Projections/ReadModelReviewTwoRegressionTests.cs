using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

public sealed class ReadModelReviewTwoRegressionTests
{
    private static readonly DateTimeOffset s_deadline = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
    private static JsonObject Value(int count) => new() { ["count"] = count };
    private static ReadModelBatchScope Scope(string batch) => new("statestore", "tenant", "widget", "owner", "detail", batch);

    [Fact]
    public void MixedV2ManifestAndFingerprintMatchFixedCanonicalGolden()
    {
        const string canonical = """{"v":2,"scope":{"store":"statestore","tenant":"tenant","domain":"widget","aggregate":"owner","projection":"detail","batch":"golden-v2"},"recoverable":true,"ops":[{"ord":0,"key":"absolute","kind":"w","type":"System.Text.Json.Nodes.JsonObject","cmode":"ExpectedETag","etag":"e1","expires":639280944000000000,"val":"eyJjb3VudCI6MX0="},{"ord":1,"key":"relative","kind":"w","type":"System.Text.Json.Nodes.JsonObject","cmode":"Unconditional","etag":"","ttl":2,"val":"eyJjb3VudCI6Mn0="},{"ord":2,"key":"durable","kind":"w","type":"System.Text.Json.Nodes.JsonObject","cmode":"ExpectedETag","etag":"","val":"eyJjb3VudCI6M30="},{"ord":3,"key":"removed","kind":"d","type":"","cmode":"IdempotentAbsent","etag":"","val":""}]}""";
        const string fingerprint = "v2:syqDwX1gQzy3O7nNCw41kEhsSSpP-MWEolB_vqazW6M";
        var batch = new ReadModelBatch(Scope("golden-v2"),
        [ReadModelBatchOperation.WriteExpiring("absolute", Value(1), ReadModelBatchConcurrency.Match("e1"), s_deadline),
            ReadModelBatchOperation.Write("relative", Value(2), ReadModelBatchConcurrency.LastWrite, TimeSpan.FromMilliseconds(1200)),
            ReadModelBatchOperation.Write("durable", Value(3), ReadModelBatchConcurrency.CreateOnly),
            ReadModelBatchOperation.Delete("removed", ReadModelBatchConcurrency.IdempotentAbsent)]) { RetainManifestForRecovery = true };
        ReadModelBatchFingerprint.BuildCanonicalManifest(batch).ShouldBe(Encoding.UTF8.GetBytes(canonical));
        ReadModelBatchFingerprint.Compute(batch).ShouldBe(fingerprint);
        ReadModelBatch decoded = ReadModelBatchManifestCodec.Decode(Encoding.UTF8.GetBytes(canonical), batch.Scope,
            null, null, fingerprint, retainManifestForRecovery: true);
        ReadModelBatchFingerprint.BuildCanonicalManifest(decoded).ShouldBe(Encoding.UTF8.GetBytes(canonical));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public async Task OmittedExpiredWritesDoNotTurnInstalledPrefixCompensationIntoIndeterminate(bool retained, bool interior, bool crosses)
    {
        var clock = new ProjectionDeadlineTimeProvider(crosses ? s_deadline.AddTicks(-1) : s_deadline);
        var store = new InMemoryReadModelStore();
        store.BatchOptions.TimeProvider = clock;
        store.SeedRaw("statestore", "occupied", Value(9));
        var operations = new List<ReadModelBatchOperation>();
        if (interior) operations.Add(ReadModelBatchOperation.Write("prefix", Value(1), ReadModelBatchConcurrency.CreateOnly));
        operations.Add(ReadModelBatchOperation.WriteExpiring("expired", Value(2), ReadModelBatchConcurrency.CreateOnly, s_deadline));
        operations.Add(ReadModelBatchOperation.Write("occupied", Value(3), ReadModelBatchConcurrency.CreateOnly));
        var batch = new ReadModelBatch(Scope("skipped"), operations) { RetainManifestForRecovery = retained };
        if (crosses)
            store.BatchFaultHook = (phase, ordinal, _) =>
            {
                if (phase == ReadModelBatchPhase.BeforeInstallOperation && ordinal == operations.Count - 1) clock.UtcNow = s_deadline;
                return Task.CompletedTask;
            };
        ReadModelBatchResult result = await store.ExecuteAsync(batch);
        result.ConflictKind.ShouldBe(ReadModelBatchConflictKind.Optimistic, result.ToString());
        result.Status.ShouldBe(ReadModelBatchStatus.Conflict);
        store.BatchFaultHook = null;
        store.SnapshotRaw("statestore", "prefix").ShouldBeNull();
        store.SnapshotRaw("statestore", "expired").ShouldBeNull();
        (await store.GetAsync<JsonObject>("statestore", "occupied")).Value!["count"]!.GetValue<int>().ShouldBe(9);
        if (retained)
        {
            (await store.RecoverPendingAsync(batch.Scope, batch.Scope.ComputeMarkerKey())).IsResolved.ShouldBeTrue();
            (await store.RecoverPendingAsync(batch.Scope, batch.Scope.ComputeMarkerKey())).IsResolved.ShouldBeTrue();
        }
        (await store.ExecuteAsync(batch)).ConflictKind.ShouldBe(ReadModelBatchConflictKind.Optimistic);
        store.SnapshotRaw("statestore", "prefix").ShouldBeNull();
    }

    [Theory]
    [InlineData(ReadModelBatchStoreProfile.Resumable)]
    [InlineData(ReadModelBatchStoreProfile.TransactionQualified)]
    public async Task ExpiredCreateOnlyPredecessorReadToInstallRacePreservesExactNewerBytes(ReadModelBatchStoreProfile profile)
    {
        using var client = new RecordingDaprClient { EnforceTransactionConcurrency = true };
        var clock = new ProjectionDeadlineTimeProvider(s_deadline);
        var options = new ReadModelBatchOptions { TimeProvider = clock };
        options.StoreProfiles["statestore"] = profile;
        byte[] newer = "{\"count\":99,\"exact\":\"newer\"}"u8.ToArray();
        client.SeedByteStore("model", ReadModelBatchExpiringValue.Encode(JsonSerializer.SerializeToUtf8Bytes(Value(1)), s_deadline));
        bool raced = false;
        if (profile == ReadModelBatchStoreProfile.Resumable)
            client.BeforeTrySaveByteState = (key, bytes) =>
            {
                if (!raced && key == "model" && ReadModelBatchEnvelope.IsEnvelope(bytes.Span))
                { raced = true; client.SeedByteStore(key, newer); }
            };
        else client.BeforeStateTransaction = _ => { if (!raced) { raced = true; client.SeedByteStore("model", newer); } };
        var batch = new ReadModelBatch(Scope("cas-race"), [ReadModelBatchOperation.WriteExpiring("model", Value(2),
            ReadModelBatchConcurrency.CreateOnly, s_deadline.AddMinutes(1))]) { RetainManifestForRecovery = true };
        var store = new DaprReadModelStore(client, Options.Create(options));
        (await store.ExecuteAsync(batch)).IsSuccess.ShouldBeFalse();
        raced.ShouldBeTrue();
        client.ByteStoreValue("model").ShouldBe(newer);
        client.BeforeTrySaveByteState = null; client.BeforeStateTransaction = null;
        (await store.ExecuteAsync(batch)).IsSuccess.ShouldBeFalse();
        client.ByteStoreValue("model").ShouldBe(newer);
        client.ExecuteStateTransactionCallCount.ShouldBe(profile == ReadModelBatchStoreProfile.TransactionQualified ? 1 : 0);
        ReadModelBatchMarker marker = ReadModelBatchMarker.ReadProof(client.ByteStoreValue(batch.Scope.ComputeMarkerKey()))!;
        marker.Fingerprint.ShouldBe(ReadModelBatchFingerprint.Compute(batch));
        marker.Status.ShouldBe(ReadModelBatchMarkerStatus.Aborted);
        (await store.RecoverPendingAsync(batch.Scope, batch.Scope.ComputeMarkerKey())).IsResolved.ShouldBeTrue();
    }

    [Fact]
    public async Task NonRetainedPreExpiryCompactionRetryPreservesExactNewerBytes()
    {
        var store = new InMemoryReadModelStore();
        store.BatchOptions.TimeProvider = new ProjectionDeadlineTimeProvider(s_deadline.AddMinutes(-1));
        var batch = new ReadModelBatch(Scope("pre-expiry"), [ReadModelBatchOperation.WriteExpiring("model", Value(1),
            ReadModelBatchConcurrency.LastWrite, s_deadline)]);
        store.BatchFaultHook = (phase, _, _) => phase == ReadModelBatchPhase.BeforeCompaction
            ? throw new InvalidOperationException("stop after commit") : Task.CompletedTask;
        await Should.ThrowAsync<InvalidOperationException>(() => store.ExecuteAsync(batch));
        store.BatchFaultHook = null;
        store.SeedRaw("statestore", "model", Value(99));
        byte[] newer = store.SnapshotRaw("statestore", "model")!;
        // Both compaction and terminal retries recognize the historical decision while
        // preserving the exact superseding ordinary row.
        (await store.ExecuteAsync(batch)).Status.ShouldBe(ReadModelBatchStatus.Completed);
        (await store.ExecuteAsync(batch)).Status.ShouldBe(ReadModelBatchStatus.AlreadyCompleted);
        store.SnapshotRaw("statestore", "model").ShouldBe(newer);
    }

    [Theory]
    [InlineData("active")]
    [InlineData("phase")]
    [InlineData("writers")]
    [InlineData("checkpoints")]
    [InlineData("stream-bounds")]
    [InlineData("operation")]
    public async Task InvalidLegacyEpochCannotCreateProofOrMutateAnyOriginalBytes(string invalid)
    {
        using var client = new RecordingDaprClient { PersistTypedState = true };
        var store = new DaprReadModelStore(client);
        var scope = new SharedProjectionScope("statestore", "tenant", "widget", "index", ["writer"]);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(scope, "writer");
        _ = await coordinator.BeginAsync(scope, "legacy", "inventory", new Dictionary<string, long>());
        var entry = await store.GetAsync<SharedProjectionEpochState>(scope.StoreName, scope.StateKey);
        SharedProjectionEpochState malformed = invalid switch
        {
            "active" => entry.Value! with { ActiveGeneration = -1 },
            "phase" => entry.Value! with { Phase = (SharedProjectionEpochPhase)99 },
            "writers" => entry.Value! with { RequiredWriters = ["writer", "writer"] },
            "checkpoints" => entry.Value! with { ConsumerCheckpoints = new() { ["owner"] = new(-1, []) } },
            "stream-bounds" => entry.Value! with { StageReservedPageCount = -1 },
            _ => entry.Value! with { OperationId = "" },
        };
        await store.SaveAsync(scope.StoreName, scope.StateKey, malformed);
        entry = await store.GetAsync<SharedProjectionEpochState>(scope.StoreName, scope.StateKey);
        var batch = new ReadModelBatch(new(scope.StoreName, scope.TenantId, scope.Domain, scope.Family, scope.Family, "legacy-scratch"),
            [ReadModelBatchOperation.Write("private", Value(1), ReadModelBatchConcurrency.CreateOnly),
                ReadModelBatchOperation.Write(scope.StateKey, malformed, ReadModelBatchConcurrency.Match(entry.ETag!))]);
        var protocol = new ReadModelBatchProtocol(new DaprReadModelBatchStateAccessor(client, scope.StoreName), new(), NullLogger.Instance);
        await Should.ThrowAsync<InvalidOperationException>(() => protocol.ExecuteAsync(batch,
            new DelegatingBatchFaultInjector((phase, _, _) => phase == ReadModelBatchPhase.BeforeCommit ? throw new InvalidOperationException("stop") : Task.CompletedTask), CancellationToken.None));
        byte[] scratch = client.ByteStoreValue("private"), fence = client.ByteStoreValue(scope.StateKey), marker = client.ByteStoreValue(batch.Scope.ComputeMarkerKey());
        (await store.RecoverPendingAsync(batch.Scope, "private")).IsResolved.ShouldBeFalse();
        client.ByteStoreValue("private").ShouldBe(scratch); client.ByteStoreValue(scope.StateKey).ShouldBe(fence);
        client.ByteStoreValue(batch.Scope.ComputeMarkerKey()).ShouldBe(marker);
        client.ByteStoreContains(ReadModelBatchKeys.ScratchCompensationKey(batch.Scope.ComputeScopeHash())).ShouldBeFalse();
    }

    [Fact]
    public async Task ValidatedLegacyCompensationAllowsRepeatedSameActionRetryWithoutChangingOldFingerprint()
    {
        var store = new InMemoryReadModelStore();
        var scope = new SharedProjectionScope("statestore", "tenant", "widget", "index", ["writer"]);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(scope, "writer");
        _ = await coordinator.BeginAsync(scope, "legacy", "inventory", new Dictionary<string, long>());
        var entry = await store.GetAsync<SharedProjectionEpochState>(scope.StoreName, scope.StateKey);
        var old = new ReadModelBatch(new(scope.StoreName, scope.TenantId, scope.Domain, scope.Family, scope.Family, "scratch"),
            [ReadModelBatchOperation.Write("private", Value(1), ReadModelBatchConcurrency.CreateOnly),
                ReadModelBatchOperation.Write(scope.StateKey, entry.Value!, ReadModelBatchConcurrency.Match(entry.ETag!))]);
        string fingerprint = ReadModelBatchFingerprint.Compute(old);
        store.BatchFaultHook = (phase, _, _) => phase == ReadModelBatchPhase.BeforeCommit ? throw new InvalidOperationException("legacy stop") : Task.CompletedTask;
        await Should.ThrowAsync<InvalidOperationException>(() => store.ExecuteAsync(old));
        store.BatchFaultHook = null;
        (await store.RecoverPendingAsync(old.Scope, "private")).IsResolved.ShouldBeTrue();
        store.Snapshot<ReadModelBatchMarker>(scope.StoreName, old.Scope.ComputeMarkerKey())!.Fingerprint.ShouldBe(fingerprint);
        byte[] immutableAborted = store.SnapshotRaw(scope.StoreName, old.Scope.ComputeMarkerKey())!;
        coordinator = new SharedProjectionEpochCoordinator(store, store);
        (await coordinator.TryWriteScratchAsync(scope, "legacy", "private", Value(1), "scratch", ReadModelBatchConcurrency.CreateOnly)).ShouldBeTrue();
        (await coordinator.TryWriteScratchAsync(scope, "legacy", "private", Value(1), "scratch", ReadModelBatchConcurrency.CreateOnly)).ShouldBeFalse();
        (await coordinator.TryWriteScratchAsync(scope, "legacy", "private", Value(1), "scratch", ReadModelBatchConcurrency.CreateOnly)).ShouldBeFalse();
        store.SnapshotRaw(scope.StoreName, old.Scope.ComputeMarkerKey()).ShouldBe(immutableAborted);
        ReadModelBatchScope retainedScope = old.Scope with { BatchId = SharedProjectionEpochCoordinator.RetainedLegacyScratchBatchId(old.Scope) };
        store.Snapshot<ReadModelBatchMarker>(scope.StoreName, retainedScope.ComputeMarkerKey())!.Status.ShouldBe(ReadModelBatchMarkerStatus.Completed);
        (await store.GetAsync<JsonObject>(scope.StoreName, "private")).Value.ShouldNotBeNull();
        await coordinator.AbortAsync(scope, "legacy", (_, _, _) => Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>([]));
        (await coordinator.GetStatusAsync(scope)).IsBuilding.ShouldBeFalse();
    }
    [Fact]
    public async Task DelayedLegacyWorkerCannotCommitTheFreshRetainedFingerprintOrRegressOpenedFence()
    {
        var store = new InMemoryReadModelStore();
        var scope = new SharedProjectionScope("statestore", "tenant", "widget", "legacy-overlap", ["writer"]);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(scope, "writer");
        _ = await coordinator.BeginAsync(scope, "legacy", "inventory", new Dictionary<string, long>());
        var entry = await store.GetAsync<SharedProjectionEpochState>(scope.StoreName, scope.StateKey);
        var old = new ReadModelBatch(new(scope.StoreName, scope.TenantId, scope.Domain, scope.Family, scope.Family, "scratch"),
            [ReadModelBatchOperation.Write("private", Value(1), ReadModelBatchConcurrency.CreateOnly),
                ReadModelBatchOperation.Write(scope.StateKey, entry.Value!, ReadModelBatchConcurrency.Match(entry.ETag!))]);
        var paused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int cuts = 0;
        store.BatchFaultHook = async (phase, _, token) =>
        {
            if (phase == ReadModelBatchPhase.BeforeCommit && Interlocked.Increment(ref cuts) == 1)
            { paused.SetResult(); await release.Task.WaitAsync(token); }
        };
        Task<ReadModelBatchResult> delayed = store.ExecuteAsync(old);
        await paused.Task.WaitAsync(TimeSpan.FromSeconds(10));
        store.BatchFaultHook = null;
        coordinator = new SharedProjectionEpochCoordinator(store, store);
        (await coordinator.TryWriteScratchAsync(scope, "legacy", "private", Value(1), "scratch", ReadModelBatchConcurrency.CreateOnly)).ShouldBeFalse();
        (await coordinator.TryWriteScratchAsync(scope, "legacy", "private", Value(1), "scratch", ReadModelBatchConcurrency.CreateOnly)).ShouldBeTrue();
        await coordinator.AbortAsync(scope, "legacy", (_, _, _) => Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>([]));
        byte[] opened = store.SnapshotRaw(scope.StoreName, scope.StateKey)!;
        byte[] retainedMarker = store.SnapshotRaw(scope.StoreName, old.Scope.ComputeMarkerKey())!;
        ReadModelBatchScope childScope = old.Scope with { BatchId = SharedProjectionEpochCoordinator.RetainedLegacyScratchBatchId(old.Scope) };
        byte[] childMarker = store.SnapshotRaw(scope.StoreName, childScope.ComputeMarkerKey())!;
        release.SetResult();
        (await delayed.WaitAsync(TimeSpan.FromSeconds(10))).ConflictKind.ShouldBe(ReadModelBatchConflictKind.Optimistic);
        store.SnapshotRaw(scope.StoreName, scope.StateKey).ShouldBe(opened);
        store.SnapshotRaw(scope.StoreName, old.Scope.ComputeMarkerKey()).ShouldBe(retainedMarker);
        store.SnapshotRaw(scope.StoreName, childScope.ComputeMarkerKey()).ShouldBe(childMarker);
    }

}
