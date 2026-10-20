using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Client.Projections;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

public sealed class ReadModelReviewThreeRecoveryTests
{
    private static JsonObject Value(int count) => new() { ["count"] = count };

    [Theory]
    [InlineData("before-install")]
    [InlineData("after-read")]
    [InlineData("before-write")]
    [InlineData("after-write-stop")]
    [InlineData("commit-cas")]
    public async Task RetiredLegacyInstallerCannotRecreatePrivateStateOrRegressCompletedAbort(string cut)
    {
        using var client = new RecordingDaprClient { PersistTypedState = true };
        var store = new DaprReadModelStore(client);
        var scope = new SharedProjectionScope("statestore", "tenant", "widget", "family", ["writer"]);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(scope, "writer");
        _ = await coordinator.BeginAsync(scope, "operation", "inventory", new Dictionary<string, long>());
        ReadModelEntry<SharedProjectionEpochState> state = await store.GetAsync<SharedProjectionEpochState>(scope.StoreName, scope.StateKey);
        var batch = new ReadModelBatch(new(scope.StoreName, scope.TenantId, scope.Domain, scope.Family, scope.Family, "scratch"),
            [ReadModelBatchOperation.Write("private", Value(1), ReadModelBatchConcurrency.CreateOnly),
            ReadModelBatchOperation.Write(scope.StateKey, state.Value!, ReadModelBatchConcurrency.Match(state.ETag!))]);
        var paused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var accessor = new ReviewThreeBatchAccessor(new DaprReadModelBatchStateAccessor(client, scope.StoreName));
        int pausedCount = 0;
        async Task Pause(CancellationToken token)
        {
            if (Interlocked.Increment(ref pausedCount) != 1) return;
            paused.TrySetResult(); await release.Task.WaitAsync(token);
        }
        if (cut == "after-read") accessor.AfterRead = (key, _, token) => key == "private" ? Pause(token) : Task.CompletedTask;
        if (cut is "before-write" or "after-write-stop") accessor.BeforeWrite = (key, bytes, token) => key == "private" && ReadModelBatchEnvelope.IsEnvelope(bytes.Span) ? Pause(token) : Task.CompletedTask;
        if (cut == "commit-cas") accessor.BeforeWrite = (key, bytes, token) => key == batch.Scope.ComputeMarkerKey()
            && ReadModelBatchMarker.ReadProof(bytes)?.Status == ReadModelBatchMarkerStatus.Committed ? Pause(token) : Task.CompletedTask;
        var old = new ReadModelBatchProtocol(accessor, new ReadModelBatchOptions(), NullLogger.Instance);
        Task<ReadModelBatchResult> delayed = old.ExecuteAsync(batch, new DelegatingBatchFaultInjector((phase, ordinal, token) =>
            cut == "before-install" && phase == ReadModelBatchPhase.BeforeInstallOperation && ordinal == 0 ? Pause(token) : Task.CompletedTask), default);
        await paused.Task.WaitAsync(TimeSpan.FromSeconds(10));
        if (cut != "commit-cas")
        {
            var worker = new ReadModelBatchProtocol(new DaprReadModelBatchStateAccessor(client, scope.StoreName), new ReadModelBatchOptions(), NullLogger.Instance);
            await Should.ThrowAsync<InvalidOperationException>(() => worker.ExecuteAsync(batch, new DelegatingBatchFaultInjector((phase, _, _) =>
                phase == ReadModelBatchPhase.BeforeCommit ? throw new InvalidOperationException("owner exited") : Task.CompletedTask), default));
        }
        (await store.RecoverPendingAsync(batch.Scope, "private")).IsResolved.ShouldBeTrue();
        await coordinator.ReclaimScratchReceiptAsync(scope, "scratch");
        // The same reserved private identity may now use the retained protocol, with a new exact
        // fingerprint. Complete it before Abort so the old failed-CAS reread sees a replacement receipt.
        if (cut == "commit-cas")
        {
            ReadModelEntry<SharedProjectionEpochState> restored = await store.GetAsync<SharedProjectionEpochState>(scope.StoreName, scope.StateKey);
            var replacement = new ReadModelBatch(batch.Scope, [ReadModelBatchOperation.Write("private", Value(2), ReadModelBatchConcurrency.CreateOnly),
                ReadModelBatchOperation.Write(scope.StateKey, restored.Value!, ReadModelBatchConcurrency.Match(restored.ETag!))]) { RetainManifestForRecovery = true };
            (await store.ExecuteAsync(replacement)).IsSuccess.ShouldBeTrue();
            ReadModelEntry<JsonObject> scratch = await store.GetAsync<JsonObject>(scope.StoreName, "private");
            (await store.TryEraseAsync(scope.StoreName, "private", scratch.ETag!)).ShouldBeTrue();
        }
        await coordinator.AbortAsync(scope, "operation", (_, _, _) => Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>([]));
        byte[] opened = client.ByteStoreValue(scope.StateKey);
        if (cut == "after-write-stop") client.AfterTrySaveByteState = (key, bytes) =>
        { if (key == "private" && ReadModelBatchEnvelope.IsEnvelope(bytes.Span)) throw new InvalidOperationException("installer exited after persistence"); };
        release.TrySetResult();
        if (cut == "after-write-stop")
        {
            await Should.ThrowAsync<InvalidOperationException>(() => delayed.WaitAsync(TimeSpan.FromSeconds(10)));
            client.ByteStoreContains("private").ShouldBeTrue();
            client.AfterTrySaveByteState = null;
        }
        else (await delayed.WaitAsync(TimeSpan.FromSeconds(10))).IsSuccess.ShouldBeFalse();
        (await store.RecoverPendingAsync(batch.Scope with { BatchId = "replacement-discovery" }, "private")).IsResolved.ShouldBeTrue();
        client.ByteStoreValue(scope.StateKey).ShouldBe(opened); client.ByteStoreContains("private").ShouldBeFalse();
        (await store.RecoverPendingAsync(batch.Scope, "private")).IsResolved.ShouldBeTrue();
        (await coordinator.GetStatusAsync(scope)).IsBuilding.ShouldBeFalse();
        _ = await coordinator.BeginAsync(scope, "fresh", "fresh-inventory", new Dictionary<string, long>());
        await coordinator.AbortAsync(scope, "fresh", (_, _, _) => Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>([]));
    }

    [Theory]
    [InlineData("exited")]
    [InlineData("marker")]
    [InlineData("manifest")]
    public async Task ExactExternalManifestWithoutMarkerIsRetiredBeforeConditionalPayloadCleanup(string cut)
    {
        using var client = new RecordingDaprClient { PersistTypedState = true };
        var store = new DaprReadModelStore(client);
        var scope = new SharedProjectionScope("statestore", "tenant", "widget", "family", ["writer"]);
        var batch = new ReadModelBatch(new(scope.StoreName, scope.TenantId, scope.Domain, scope.Family, scope.Family, "external"),
            [ReadModelBatchOperation.Write("private", new JsonObject { ["payload"] = new string('x', 20_000) }, ReadModelBatchConcurrency.CreateOnly)]) { RetainManifestForRecovery = true };
        byte[] manifest = ReadModelBatchFingerprint.BuildCanonicalManifest(batch);
        string manifestKey = ReadModelBatchKeys.ManifestKey(batch.Scope.ComputeScopeHash());
        var paused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var accessor = new ReviewThreeBatchAccessor(new DaprReadModelBatchStateAccessor(client, scope.StoreName));
        accessor.BeforeWrite = async (key, _, token) =>
        {
            if (key != (cut == "manifest" ? manifestKey : batch.Scope.ComputeMarkerKey())) return;
            accessor.BeforeWrite = null; paused.TrySetResult(); await release.Task.WaitAsync(token);
        };
        Task<ReadModelBatchResult>? delayed = cut != "exited"
            ? new ReadModelBatchProtocol(accessor, new ReadModelBatchOptions(), NullLogger.Instance).ExecuteAsync(batch, null, default) : null;
        if (delayed is not null) await paused.Task.WaitAsync(TimeSpan.FromSeconds(10));
        if (cut is "exited" or "manifest") client.SeedByteStore(manifestKey, manifest);
        client.ByteStoreContains(manifestKey).ShouldBeTrue(); client.ByteStoreContains(batch.Scope.ComputeMarkerKey()).ShouldBeFalse();
        await new SharedProjectionEpochCoordinator(store, store).ReclaimScratchReceiptAsync(scope, batch.Scope.BatchId);
        client.ByteStoreContains(manifestKey).ShouldBeFalse(); client.ByteStoreContains(batch.Scope.ComputeMarkerKey()).ShouldBeFalse();
        if (delayed is not null)
        {
            release.TrySetResult(); (await delayed.WaitAsync(TimeSpan.FromSeconds(10))).IsSuccess.ShouldBeFalse();
            await new SharedProjectionEpochCoordinator(new DaprReadModelStore(client), new DaprReadModelStore(client))
                .ReclaimScratchReceiptAsync(scope, batch.Scope.BatchId);
        }
        client.ByteStoreContains("private").ShouldBeFalse(); client.ByteStoreContains(manifestKey).ShouldBeFalse();
        (await store.RecoverPendingAsync(batch.Scope, batch.Scope.ComputeMarkerKey())).IsResolved.ShouldBeTrue();
    }
    [Theory]
    [InlineData("scope")]
    [InlineData("malformed")]
    [InlineData("oversized")]
    [InlineData("own-participant")]
    [InlineData("foreign-participant")]
    public async Task MarkerlessCleanupValidatesOriginalBoundedScopeAndLeavesForeignParticipantsExact(string evidence)
    {
        using var client = new RecordingDaprClient();
        var scope = new ReadModelBatchScope("statestore", "tenant", "widget", "family", "family", "external");
        var batch = new ReadModelBatch(scope, [ReadModelBatchOperation.Write("private", Value(1), ReadModelBatchConcurrency.CreateOnly)])
            { RetainManifestForRecovery = true };
        byte[] manifest = evidence switch
        {
            "scope" => ReadModelBatchFingerprint.BuildCanonicalManifest(new ReadModelBatch(scope with { TenantId = "foreign" }, batch.Operations) { RetainManifestForRecovery = true }),
            "malformed" => "{}"u8.ToArray(),
            "oversized" => new byte[ReadModelBatchOptions.DefaultMaxCanonicalManifestBytes + 1],
            _ => ReadModelBatchFingerprint.BuildCanonicalManifest(batch),
        };
        string manifestKey = ReadModelBatchKeys.ManifestKey(scope.ComputeScopeHash());
        client.SeedByteStore(manifestKey, manifest);
        byte[]? participant = null;
        if (evidence is "own-participant" or "foreign-participant")
        {
            ReadModelBatchScope owner = evidence == "own-participant" ? scope : scope with { BatchId = "foreign" };
            participant = new ReadModelBatchEnvelope { ScopeHash = owner.ComputeScopeHash(), Fingerprint = ReadModelBatchFingerprint.Compute(batch),
                Ordinal = 0, CandidateBase64 = Convert.ToBase64String(batch.Operations[0].CanonicalValue.Span) }.ToBytes().ToArray();
            client.SeedByteStore("private", participant);
        }
        var store = new DaprReadModelStore(client);
        (await store.RecoverPendingAsync(scope, scope.ComputeMarkerKey())).IsResolved.ShouldBe(evidence == "foreign-participant");
        if (evidence == "foreign-participant") client.ByteStoreContains(manifestKey).ShouldBeFalse();
        else
        {
            client.ByteStoreValue(manifestKey).ShouldBe(manifest);
            client.ByteStoreContains(scope.ComputeMarkerKey()).ShouldBeFalse();
        }
        client.ByteStoreValue("private").ShouldBe(participant);
    }

    [Theory]
    [InlineData("marker-response")]
    [InlineData("fence-write")]
    [InlineData("manifest-response")]
    public async Task MarkerlessCleanupResumesItsOwnLostResponsesAndRetainsTheOriginalRetirement(string cut)
    {
        using var client = new RecordingDaprClient { PersistTypedState = true };
        var scope = new SharedProjectionScope("statestore", "tenant", "widget", "family", ["writer"]);
        var batch = new ReadModelBatch(new(scope.StoreName, scope.TenantId, scope.Domain, scope.Family, scope.Family, "external"),
            [ReadModelBatchOperation.Write("private", Value(1), ReadModelBatchConcurrency.CreateOnly)]) { RetainManifestForRecovery = true };
        string manifestKey = ReadModelBatchKeys.ManifestKey(batch.Scope.ComputeScopeHash());
        client.SeedByteStore(manifestKey, ReadModelBatchFingerprint.BuildCanonicalManifest(batch));
        client.AfterTrySaveByteState = (key, _) =>
        { if (cut == "marker-response" && key == batch.Scope.ComputeMarkerKey()) throw new IOException("lost marker response"); };
        client.BeforeTrySaveByteState = (key, _) =>
        { if (cut == "fence-write" && key.Contains(":retired:", StringComparison.Ordinal)) throw new IOException("fence unavailable"); };
        client.AfterTryDeleteByteState = key =>
        { if (cut == "manifest-response" && key == manifestKey) throw new IOException("lost manifest deletion response"); };
        var store = new DaprReadModelStore(client);
        ReadModelPendingBatchRecoveryResult initial = await store.RecoverPendingAsync(batch.Scope, batch.Scope.ComputeMarkerKey());
        initial.IsResolved.ShouldBe(cut == "manifest-response");
        client.AfterTrySaveByteState = null; client.BeforeTrySaveByteState = null; client.AfterTryDeleteByteState = null;
        store = new DaprReadModelStore(client);
        await new SharedProjectionEpochCoordinator(store, store).ReclaimScratchReceiptAsync(scope, batch.Scope.BatchId);
        await new SharedProjectionEpochCoordinator(store, store).ReclaimScratchReceiptAsync(scope, batch.Scope.BatchId);
        client.ByteStoreContains(manifestKey).ShouldBeFalse(); client.ByteStoreContains(batch.Scope.ComputeMarkerKey()).ShouldBeFalse();
        client.ByteStoreContains(ReadModelBatchTerminalFence.Key(batch.Scope.ComputeScopeHash(), ReadModelBatchFingerprint.Compute(batch))).ShouldBeTrue();
    }

}
