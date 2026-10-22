using System.Text;
using Hexalith.EventStore.Client.Projections;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

/// <summary>
/// Retained batches re-seal participant proof and terminal fences during resume, compaction, abort and cleanup. On a
/// DAPR sidecar a rejected create-only write is retried by its resiliency policy and counts toward the state store
/// circuit breaker, so sealing existing identical evidence must read first and never issue a rejected write.
/// </summary>
public sealed class ReadModelBatchRetainedSealTests
{
    [Fact]
    public async Task RetainedBatchCompletionIssuesNoRejectedConditionalWrite()
    {
        using var client = new RecordingDaprClient { PersistTypedState = true };
        ReadModelBatchScope scope = Scope("completed");
        var accessor = Recording(client, scope);
        ReadModelBatch batch = new(scope,
            [ReadModelBatchOperation.Write("first", new { value = 1 }, ReadModelBatchConcurrency.CreateOnly),
                ReadModelBatchOperation.Write("last", new { value = 2 }, ReadModelBatchConcurrency.CreateOnly)])
        { RetainManifestForRecovery = true };

        ReadModelBatchResult result = await new ReadModelBatchProtocol(accessor, new(), NullLogger.Instance).ExecuteAsync(batch, null, default);

        result.Status.ShouldBe(ReadModelBatchStatus.Completed);
        accessor.RejectedMutations.ShouldBeEmpty();
        string fingerprint = ReadModelBatchFingerprint.Compute(batch);
        // Install seals each proof once; compaction and cleanup re-validate it without writing again.
        accessor.Writes.Count(write => write.Key == ReadModelBatchParticipantProof.Key(scope, fingerprint, 0)).ShouldBe(1);
        accessor.Writes.Count(write => write.Key == ReadModelBatchParticipantProof.Key(scope, fingerprint, 1)).ShouldBe(1);
        accessor.Writes.Count(write => write.Key == ReadModelBatchTerminalFence.Key(scope.ComputeScopeHash(), fingerprint)).ShouldBe(1);
        accessor.Writes.Count(write => write.Key == scope.ComputeMarkerKey() + ":retired").ShouldBe(1);
        var store = new DaprReadModelStore(client);
        (await store.GetAsync<System.Text.Json.Nodes.JsonObject>(scope.StoreName, "first")).Value!["value"]!.GetValue<int>().ShouldBe(1);
        (await store.GetAsync<System.Text.Json.Nodes.JsonObject>(scope.StoreName, "last")).Value!["value"]!.GetValue<int>().ShouldBe(2);
    }

    [Fact]
    public async Task RetainedBatchAbortIssuesNoRejectedConditionalWriteAndRestoresInstalledParticipants()
    {
        using var client = new RecordingDaprClient { PersistTypedState = true };
        ReadModelBatchScope scope = Scope("aborted");
        byte[] foreign = Encoding.UTF8.GetBytes("{\"value\":9}");
        client.SeedByteStore("last", foreign);
        var accessor = Recording(client, scope);
        ReadModelBatch batch = new(scope,
            [ReadModelBatchOperation.Write("first", new { value = 1 }, ReadModelBatchConcurrency.CreateOnly),
                ReadModelBatchOperation.Write("last", new { value = 2 }, ReadModelBatchConcurrency.Match("stale"))])
        { RetainManifestForRecovery = true };

        ReadModelBatchResult result = await new ReadModelBatchProtocol(accessor, new(), NullLogger.Instance).ExecuteAsync(batch, null, default);

        result.Status.ShouldBe(ReadModelBatchStatus.Conflict);
        accessor.RejectedMutations.ShouldBeEmpty();
        string fingerprint = ReadModelBatchFingerprint.Compute(batch);
        // The installed participant's proof is sealed once and only re-validated by compensation and cleanup.
        accessor.Writes.Count(write => write.Key == ReadModelBatchParticipantProof.Key(scope, fingerprint, 0)).ShouldBe(1);
        accessor.Writes.Count(write => write.Key == ReadModelBatchTerminalFence.Key(scope.ComputeScopeHash(), fingerprint)).ShouldBeLessThanOrEqualTo(1);
        client.ByteStoreContains("first").ShouldBeFalse();
        client.ByteStoreValue("last").ShouldBe(foreign);
    }

    [Fact]
    public async Task IdenticalParticipantProofReSealReturnsTrueWithoutWriting()
    {
        using var client = new RecordingDaprClient { PersistTypedState = true };
        ReadModelBatchScope scope = Scope("proof-identical");
        var accessor = Recording(client, scope);
        (string fingerprint, ReadModelBatchRecoveryAnchor anchor) = Participant(scope);

        (await ReadModelBatchParticipantProof.SealAsync(accessor, scope, fingerprint, anchor, ReadModelBatchConcurrency.CreateOnly, default)).ShouldBeTrue();
        (await ReadModelBatchParticipantProof.SealAsync(accessor, scope, fingerprint, anchor, ReadModelBatchConcurrency.CreateOnly, default)).ShouldBeTrue();

        accessor.Writes.Count.ShouldBe(1);
        accessor.RejectedMutations.ShouldBeEmpty();
    }

    [Fact]
    public async Task IdenticalTerminalFenceReSealReturnsTrueWithoutWriting()
    {
        using var client = new RecordingDaprClient { PersistTypedState = true };
        ReadModelBatchScope scope = Scope("fence-identical");
        var accessor = Recording(client, scope);
        ReadModelBatchTerminalFence fence = Fence(scope, ReadModelBatchMarkerStatus.Completed);

        (await fence.SealAsync(accessor, default)).ShouldBeTrue();
        (await fence.SealAsync(accessor, default)).ShouldBeTrue();

        // One fence and one scope index write; the identical re-seal issues neither.
        accessor.Writes.Count.ShouldBe(2);
        accessor.RejectedMutations.ShouldBeEmpty();
    }

    [Fact]
    public async Task DivergentExistingParticipantProofIsRefusedWithoutWriting()
    {
        using var client = new RecordingDaprClient { PersistTypedState = true };
        ReadModelBatchScope scope = Scope("proof-divergent");
        var accessor = Recording(client, scope);
        (string fingerprint, ReadModelBatchRecoveryAnchor anchor) = Participant(scope);
        byte[] divergent = Encoding.UTF8.GetBytes("{\"foreign\":true}");
        client.SeedByteStore(ReadModelBatchParticipantProof.Key(scope, fingerprint, anchor.Ordinal), divergent);

        (await ReadModelBatchParticipantProof.SealAsync(accessor, scope, fingerprint, anchor, ReadModelBatchConcurrency.CreateOnly, default)).ShouldBeFalse();

        accessor.Writes.ShouldBeEmpty();
        client.ByteStoreValue(ReadModelBatchParticipantProof.Key(scope, fingerprint, anchor.Ordinal)).ShouldBe(divergent);
    }

    [Fact]
    public async Task DivergentExistingTerminalFenceOrIndexIsRefusedWithoutWriting()
    {
        using var client = new RecordingDaprClient { PersistTypedState = true };
        ReadModelBatchScope scope = Scope("fence-divergent");
        var accessor = Recording(client, scope);
        (await Fence(scope, ReadModelBatchMarkerStatus.Aborted).SealAsync(accessor, default)).ShouldBeTrue();
        int sealedWrites = accessor.Writes.Count;

        // A different decision for the same retired fingerprint keeps today's refusal.
        (await Fence(scope, ReadModelBatchMarkerStatus.Completed).SealAsync(accessor, default)).ShouldBeFalse();
        accessor.Writes.Count.ShouldBe(sealedWrites);

        // A divergent scope index is refused after the identical fence is accepted without a write.
        using var indexClient = new RecordingDaprClient { PersistTypedState = true };
        var indexAccessor = Recording(indexClient, scope);
        ReadModelBatchTerminalFence fence = Fence(scope, ReadModelBatchMarkerStatus.Completed);
        (await fence.SealAsync(indexAccessor, default)).ShouldBeTrue();
        byte[] divergent = Encoding.UTF8.GetBytes("{\"foreign\":true}");
        indexClient.SeedByteStore(scope.ComputeMarkerKey() + ":retired", divergent);
        int indexWrites = indexAccessor.Writes.Count;
        (await fence.SealAsync(indexAccessor, default)).ShouldBeFalse();
        indexAccessor.Writes.Count.ShouldBe(indexWrites);
        indexClient.ByteStoreValue(scope.ComputeMarkerKey() + ":retired").ShouldBe(divergent);
    }

    [Fact]
    public async Task AbsentProofAndFenceAreCreatedOnce()
    {
        using var client = new RecordingDaprClient { PersistTypedState = true };
        ReadModelBatchScope scope = Scope("absent");
        var accessor = Recording(client, scope);
        (string fingerprint, ReadModelBatchRecoveryAnchor anchor) = Participant(scope);
        ReadModelBatchTerminalFence fence = Fence(scope, ReadModelBatchMarkerStatus.Completed);

        (await ReadModelBatchParticipantProof.SealAsync(accessor, scope, fingerprint, anchor, ReadModelBatchConcurrency.CreateOnly, default)).ShouldBeTrue();
        (await fence.SealAsync(accessor, default)).ShouldBeTrue();

        accessor.Writes.Select(static write => (write.Key, write.ExpectedETag, write.Accepted)).ShouldBe(
        [
            (ReadModelBatchParticipantProof.Key(scope, fingerprint, anchor.Ordinal), string.Empty, true),
            (ReadModelBatchTerminalFence.Key(scope.ComputeScopeHash(), fence.Fingerprint), string.Empty, true),
            (scope.ComputeMarkerKey() + ":retired", string.Empty, true),
        ]);
        (await ReadModelBatchParticipantProof.MatchesAnchorAsync(accessor, scope, fingerprint, anchor, default)).ShouldBeTrue();
        ReadModelBatchCanonicalJson.Serialize((await ReadModelBatchTerminalFence.ReadAsync(accessor, scope, fence.Fingerprint, default))!).ToArray()
            .ShouldBe(ReadModelBatchCanonicalJson.Serialize(fence).ToArray());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LostParticipantProofCreationRaceFallsBackToExactComparison(bool identical)
    {
        using var client = new RecordingDaprClient { PersistTypedState = true };
        ReadModelBatchScope scope = Scope("proof-race");
        var accessor = Recording(client, scope);
        (string fingerprint, ReadModelBatchRecoveryAnchor anchor) = Participant(scope);
        string key = ReadModelBatchParticipantProof.Key(scope, fingerprint, anchor.Ordinal);
        byte[] divergent = Encoding.UTF8.GetBytes("{\"foreign\":true}");
        // A concurrent sealer creates the key after the read but before this create.
        accessor.BeforeWrite = (written, bytes, _) =>
        {
            if (written == key) client.SeedByteStore(key, identical ? bytes : divergent);
            return Task.CompletedTask;
        };

        (await ReadModelBatchParticipantProof.SealAsync(accessor, scope, fingerprint, anchor, ReadModelBatchConcurrency.CreateOnly, default)).ShouldBe(identical);

        accessor.Writes.ShouldHaveSingleItem().Accepted.ShouldBeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LostTerminalFenceCreationRaceFallsBackToExactComparison(bool identical)
    {
        using var client = new RecordingDaprClient { PersistTypedState = true };
        ReadModelBatchScope scope = Scope("fence-race");
        var accessor = Recording(client, scope);
        ReadModelBatchTerminalFence fence = Fence(scope, ReadModelBatchMarkerStatus.Completed);
        ReadModelBatchTerminalFence divergent = Fence(scope, ReadModelBatchMarkerStatus.Aborted);
        string key = ReadModelBatchTerminalFence.Key(scope.ComputeScopeHash(), fence.Fingerprint);
        accessor.BeforeWrite = (written, bytes, _) =>
        {
            if (written == key) client.SeedByteStore(key, identical ? bytes : ReadModelBatchCanonicalJson.Serialize(divergent));
            return Task.CompletedTask;
        };

        (await fence.SealAsync(accessor, default)).ShouldBe(identical);

        accessor.Writes.First().Accepted.ShouldBeFalse();
        // The raced fence still seals its scope index only when the stored fence is identical.
        accessor.Writes.Count.ShouldBe(identical ? 2 : 1);
    }

    private static ReadModelBatchScope Scope(string batchId) => new("statestore", "tenant", "domain", "owner", "projection", batchId);

    private static ConditionalWriteRecordingBatchStateAccessor Recording(RecordingDaprClient client, ReadModelBatchScope scope)
        => new(new DaprReadModelBatchStateAccessor(client, scope.StoreName));

    private static (string Fingerprint, ReadModelBatchRecoveryAnchor Anchor) Participant(ReadModelBatchScope scope)
    {
        string fingerprint = Fingerprint(scope);
        var anchor = new ReadModelBatchRecoveryAnchor(scope, "first", 0, false,
            ReadModelBatchRecoveryAnchor.Digest(Encoding.UTF8.GetBytes("candidate")), null, null);
        return (fingerprint, anchor);
    }

    private static ReadModelBatchTerminalFence Fence(ReadModelBatchScope scope, ReadModelBatchMarkerStatus decision)
        => new(scope, Fingerprint(scope), [], 2, decision, 2);

    private static string Fingerprint(ReadModelBatchScope scope) => ReadModelBatchFingerprint.Compute(new ReadModelBatch(scope,
        [ReadModelBatchOperation.Write("first", new { value = 1 }, ReadModelBatchConcurrency.CreateOnly),
            ReadModelBatchOperation.Write("last", new { value = 2 }, ReadModelBatchConcurrency.CreateOnly)]) { RetainManifestForRecovery = true });
}
