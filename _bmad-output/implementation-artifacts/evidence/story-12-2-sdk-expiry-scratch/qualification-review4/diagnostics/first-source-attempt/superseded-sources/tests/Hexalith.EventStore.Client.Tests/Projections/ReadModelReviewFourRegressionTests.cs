using System.Text.Json.Nodes;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

public sealed class ReadModelReviewFourRegressionTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ExpiredInstalledEnvelopeCannotCompleteUntilPhysicalDeletionSettles(bool retained, bool lostResponse)
    {
        using var client = new RecordingDaprClient();
        var deadline = DateTimeOffset.UtcNow.AddMinutes(1);
        var clock = new ProjectionDeadlineTimeProvider(deadline.AddTicks(-1));
        var options = new ReadModelBatchOptions { TimeProvider = clock };
        var store = new DaprReadModelStore(client, Options.Create(options));
        var batch = new ReadModelBatch(new("statestore", "tenant", "domain", "owner", "view", "expiry"),
            [ReadModelBatchOperation.WriteExpiring("private", new { value = 1 }, ReadModelBatchConcurrency.LastWrite, deadline),
                ReadModelBatchOperation.Write("checkpoint", new { position = 1 }, ReadModelBatchConcurrency.LastWrite)])
            { RetainManifestForRecovery = retained };
        var protocol = new ReadModelBatchProtocol(new DaprReadModelBatchStateAccessor(client, "statestore"), options, NullLogger.Instance);
        client.TryDeleteByteOverride = key => key == "private" ? lostResponse ? throw new IOException("ambiguous delete") : false : null;
        var cross = new DelegatingBatchFaultInjector((phase, _, _) =>
        {
            if (phase == ReadModelBatchPhase.AfterCommit) clock.UtcNow = deadline;
            return Task.CompletedTask;
        });
        ReadModelBatchResult result = await protocol.ExecuteAsync(batch, cross, CancellationToken.None);
        result.IsSuccess.ShouldBeFalse();
        ReadModelBatchEnvelope.IsEnvelope(client.ByteStoreValue("private")).ShouldBeTrue();
        (await store.GetAsync<ReadModelBatchMarker>("statestore", batch.Scope.ComputeMarkerKey())).Value!.Status.ShouldBe(ReadModelBatchMarkerStatus.Committed);
        client.TryDeleteByteOverride = null;
        (await store.ExecuteAsync(batch)).IsSuccess.ShouldBeTrue();
        client.ByteStoreContains("private").ShouldBeFalse();
        (await store.ExecuteAsync(batch)).IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    public async Task RetainedTerminalProofRecoversLateExitedInstallerForEveryParticipant(bool abort, int ordinal)
    {
        using var client = new RecordingDaprClient { PersistTypedState = true };
        var store = new DaprReadModelStore(client);
        var scope = new SharedProjectionScope("statestore", "tenant", "widget", "index", ["writer"]);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(scope, "writer");
        _ = await coordinator.BeginAsync(scope, "operation", "inventory", new Dictionary<string, long>());
        ReadModelEntry<SharedProjectionEpochState> epoch = await store.GetAsync<SharedProjectionEpochState>(scope.StoreName, scope.StateKey);
        var batch = new ReadModelBatch(new(scope.StoreName, scope.TenantId, scope.Domain, scope.Family, scope.Family, "retained"),
            [ReadModelBatchOperation.Write("private", new { value = 1 }, ReadModelBatchConcurrency.CreateOnly),
                ReadModelBatchOperation.Write(scope.StateKey, epoch.Value!, ReadModelBatchConcurrency.Match(epoch.ETag!))])
            { RetainManifestForRecovery = true };
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        string key = batch.Operations[ordinal].Key;
        byte[]? delayed = null;
        client.BeforeTrySaveByteState = (candidateKey, bytes) =>
        {
            if (candidateKey != key || delayed is not null || !ReadModelBatchEnvelope.IsEnvelope(bytes.Span)) return;
            delayed = bytes.ToArray();
            reached.TrySetResult();
            release.Task.GetAwaiter().GetResult();
            throw new IOException("original installer exited before physical response");
        };
        Task<ReadModelBatchStagingResult> original = Task.Run(() => store.StageAsync(batch));
        await reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        client.BeforeTrySaveByteState = null;
        if (abort)
        {
            (await store.StageAsync(batch)).Status.ShouldBe(ReadModelBatchStagingStatus.Prepared);
            (await store.AbortAsync(batch)).Status.ShouldBe(ReadModelBatchStagingStatus.Aborted);
        }
        else (await store.ExecuteAsync(batch)).IsSuccess.ShouldBeTrue();
        // The helper reclaims the original payload and receipt; the stopped worker can leave its exact envelope.
        await coordinator.ReclaimScratchReceiptAsync(scope, batch.Scope.BatchId);
        client.ByteStoreContains(batch.Scope.ComputeMarkerKey()).ShouldBeFalse();
        client.SeedByteStore(key, delayed!);
        release.TrySetResult();
        _ = await original;
        string proofKey = ReadModelBatchParticipantProof.Key(batch.Scope, ReadModelBatchFingerprint.Compute(batch), ordinal);
        byte[] proof = client.ByteStoreValue(proofKey);
        byte[] exactEnvelope = client.ByteStoreValue(key);
        JsonObject tampered = JsonNode.Parse(proof)!.AsObject();
        tampered["anchor"]!["previousDigest"] = new string('A', 64);
        client.SeedByteStore(proofKey, System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(tampered));
        (await new DaprReadModelStore(client).RecoverPendingAsync(batch.Scope, key)).IsResolved.ShouldBeFalse();
        client.ByteStoreValue(key).ShouldBe(exactEnvelope);
        client.SeedByteStore(proofKey, new byte[ReadModelBatchParticipantProof.MaxEncodedBytes + 1]);
        (await new DaprReadModelStore(client).RecoverPendingAsync(batch.Scope, key)).IsResolved.ShouldBeFalse();
        client.ByteStoreValue(key).ShouldBe(exactEnvelope);
        client.SeedByteStore(proofKey, proof);
        (await new DaprReadModelStore(client).RecoverPendingAsync(batch.Scope, key)).IsResolved.ShouldBeTrue();
        if (client.ByteStoreContains(key)) ReadModelBatchEnvelope.IsEnvelope(client.ByteStoreValue(key)).ShouldBeFalse();
        (await new DaprReadModelStore(client).RecoverPendingAsync(batch.Scope, key)).IsResolved.ShouldBeTrue();
    }

    [Fact]
    public async Task FakeAndDaprEtagProbeHideInclusiveExpiredAndPreparedAbsentValues()
    {
        var deadline = DateTimeOffset.UtcNow;
        var clock = new ProjectionDeadlineTimeProvider(deadline.AddTicks(-1));
        var options = new ReadModelBatchOptions { TimeProvider = clock };
        using var client = new RecordingDaprClient();
        var fake = new InMemoryReadModelStore();
        fake.BatchOptions.TimeProvider = clock;
        var dapr = new DaprReadModelStore(client, Options.Create(options));
        foreach (IReadModelBatchStore store in new IReadModelBatchStore[] { fake, dapr })
        {
            var batch = new ReadModelBatch(new("statestore", "tenant", "domain", "owner", "view", "probe"),
                [ReadModelBatchOperation.WriteExpiring("value", new { value = 1 }, ReadModelBatchConcurrency.LastWrite, deadline)]);
            (await store.ExecuteAsync(batch)).IsSuccess.ShouldBeTrue();
        }
        clock.UtcNow = deadline;
        (await fake.TryReadEtagAsync("statestore", "value")).ShouldBe((false, string.Empty));
        (await dapr.TryReadEtagAsync("statestore", "value")).ShouldBe((false, string.Empty));
        foreach (IReadModelBatchStagingStore store in new IReadModelBatchStagingStore[] { fake, dapr })
        {
            var batch = new ReadModelBatch(new("statestore", "tenant", "domain", "owner", "view", "prepared"),
                [ReadModelBatchOperation.Write("pending", new { value = 2 }, ReadModelBatchConcurrency.CreateOnly)]);
            (await store.StageAsync(batch)).Status.ShouldBe(ReadModelBatchStagingStatus.Prepared);
        }
        (await fake.TryReadEtagAsync("statestore", "pending")).ShouldBe((false, string.Empty));
        (await dapr.TryReadEtagAsync("statestore", "pending")).ShouldBe((false, string.Empty));
    }
}
