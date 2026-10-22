using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Client.Projections;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

public partial class DaprReadModelBatchTests
{
    [Theory]
    [InlineData(ReadModelBatchPhase.BeforeReceipt, false)]
    [InlineData(ReadModelBatchPhase.RecoveryReceiptWritten, false)]
    [InlineData(ReadModelBatchPhase.RecoveryManifestReclaimed, false)]
    [InlineData(ReadModelBatchPhase.RecoveryAnchorCompacted, false)]
    [InlineData(ReadModelBatchPhase.RecoveryReceiptWritten, true)]
    [InlineData(ReadModelBatchPhase.RecoveryManifestReclaimed, true)]
    [InlineData(ReadModelBatchPhase.RecoveryAnchorCompacted, true)]
    public async Task TerminalRecoveryAnchorSurvivesEveryCleanupBoundary(ReadModelBatchPhase phase, bool compensate)
    {
        using var client = new RecordingDaprClient();
        var store = CreateStore(client);
        var value = new { Name = new string('x', 20_000) };
        ReadModelBatch batch = new(Scope(), compensate
            ? [ReadModelBatchOperation.Write("detail", value, ReadModelBatchConcurrency.CreateOnly), ReadModelBatchOperation.Write("index", new IndexEntry(1), ReadModelBatchConcurrency.CreateOnly)]
            : [ReadModelBatchOperation.Write("index", new IndexEntry(1), ReadModelBatchConcurrency.CreateOnly), ReadModelBatchOperation.Write("detail", value, ReadModelBatchConcurrency.CreateOnly)])
            { RetainManifestForRecovery = true };
        if (compensate) client.SeedByteStore("index", JsonSerializer.SerializeToUtf8Bytes(new IndexEntry(9)));
        var protocol = new ReadModelBatchProtocol(new DaprReadModelBatchStateAccessor(client, Store), new(), NullLogger.Instance);
        (await protocol.ExecuteAsync(batch, new PendingBatchRecoveryStopAtPhase(phase), TestContext.Current.CancellationToken)).IsSuccess.ShouldBeFalse();
        if (phase != ReadModelBatchPhase.RecoveryAnchorCompacted)
            ReadModelBatchEnvelope.FromBytes(client.ByteStoreValue("detail")).ShouldNotBeNull();
        // A later aggregate changes the already ordinary shared index. Recovery never rolls it back.
        client.SeedByteStore("index", JsonSerializer.SerializeToUtf8Bytes(new IndexEntry(10)));
        byte[] newer = client.ByteStoreValue("index");
        var restarted = CreateStore(client);
        (await restarted.RecoverPendingAsync(Scope() with { BatchId = "unknown" }, "detail")).IsResolved.ShouldBeTrue();
        client.ByteStoreValue("index").ShouldBe(newer);
        client.ByteStoreContains(ReadModelBatchKeys.ManifestKey(Scope().ComputeScopeHash())).ShouldBeFalse();
        ReadModelBatchMarker marker = ReadModelBatchMarker.ReadProof(client.ByteStoreValue(Scope().ComputeMarkerKey()))!;
        marker.OriginalManifest.ShouldBeNull(); marker.OriginalManifestKey.ShouldBeNull(); marker.Operations.ShouldBeNull();
        marker.Status.ShouldBe(compensate ? ReadModelBatchMarkerStatus.Aborted : ReadModelBatchMarkerStatus.Completed);
        client.ByteStoreValue(Scope().ComputeMarkerKey()).Length.ShouldBeLessThan(16_384);
        if (compensate) client.ByteStoreContains("detail").ShouldBeFalse();
        else
        {
            ReadModelBatchEnvelope.FromBytes(client.ByteStoreValue("detail")).ShouldBeNull();
            (await restarted.ExecuteAsync(batch)).Status.ShouldBe(ReadModelBatchStatus.AlreadyCompleted);
        }
    }

    [Theory]
    [InlineData("fabricated-previous", false)] [InlineData("fabricated-previous", true)]
    [InlineData("candidate", false)] [InlineData("delete", false)] [InlineData("ordinal", false)] [InlineData("missing", false)] [InlineData("previous", false)]
    [InlineData("candidate", true)] [InlineData("delete", true)] [InlineData("ordinal", true)] [InlineData("missing", true)] [InlineData("previous", true)]
    public async Task RecoveryValidatesNonDiscoveryEnvelopeBeforeAnyMutation(string fault, bool committed)
    {
        using var client = new RecordingDaprClient(); var store = CreateStore(client);
        var batch = new ReadModelBatch(Scope(), [ReadModelBatchOperation.Write("detail", new Detail(1), ReadModelBatchConcurrency.CreateOnly),
            ReadModelBatchOperation.Write("index", new IndexEntry(1), ReadModelBatchConcurrency.CreateOnly)]) { RetainManifestForRecovery = true };
        (await store.StageAsync(batch)).Status.ShouldBe(ReadModelBatchStagingStatus.Prepared);
        JsonNode envelope = JsonNode.Parse(client.ByteStoreValue("index"))!;
        if (fault == "candidate") envelope["cand"] = Convert.ToBase64String("{}"u8.ToArray());
        if (fault == "delete") { envelope["del"] = true; envelope["cand"] = null; }
        if (fault == "ordinal") envelope["ord"] = 0;
        if (fault == "missing") envelope.AsObject().Remove("del");
        if (fault == "previous") envelope["prev"] = "invalid-base64";
        if (fault == "fabricated-previous") envelope["prev"] = Convert.ToBase64String("{}"u8.ToArray());
        client.SeedByteStore("index", JsonSerializer.SerializeToUtf8Bytes(envelope));
        if (committed)
        {
            JsonNode marker = JsonNode.Parse(client.ByteStoreValue(Scope().ComputeMarkerKey()))!; marker["st"] = 1;
            client.SeedByteStore(Scope().ComputeMarkerKey(), JsonSerializer.SerializeToUtf8Bytes(marker));
        }
        int writes = client.TrySaveByteOperations.Count, deletes = client.TryDeleteByteOperations.Count;
        (await store.RecoverPendingAsync(Scope(), "detail")).IsResolved.ShouldBeFalse();
        client.TrySaveByteOperations.Count.ShouldBe(writes); client.TryDeleteByteOperations.Count.ShouldBe(deletes);
    }

    [Fact]
    public async Task OrdinaryDiscriminatorStringsAndNestedMetadataRemainNormalValues()
    {
        using var client = new RecordingDaprClient(); var store = CreateStore(client);
        var previous = new { Name = "$hxrmb", Metadata = new Dictionary<string, string> { ["$hxrmb"] = "ordinary" } };
        client.SeedByteStore("detail", JsonSerializer.SerializeToUtf8Bytes(previous));
        (await store.RecoverPendingAsync(Scope(), "detail")).IsResolved.ShouldBeTrue();
        string etag = (await new DaprReadModelBatchStateAccessor(client, Store).ReadAsync("detail", TestContext.Current.CancellationToken)).ETag;
        var batch = new ReadModelBatch(Scope(), [ReadModelBatchOperation.Write("detail", previous, ReadModelBatchConcurrency.Match(etag))]) { RetainManifestForRecovery = true };
        (await store.StageAsync(batch)).Status.ShouldBe(ReadModelBatchStagingStatus.Prepared);
        (await store.RecoverPendingAsync(Scope(), "detail")).IsResolved.ShouldBeTrue();
        JsonDocument.Parse(client.ByteStoreValue("detail")).RootElement.GetProperty("name").GetString().ShouldBe("$hxrmb");
        (await store.ExecuteAsync(batch)).Status.ShouldBe(ReadModelBatchStatus.AlreadyCompleted);
    }

    [Theory]
    [InlineData(10)] [InlineData(20_000)]
    public async Task RepeatedCompensatedConflictsReclaimInlineAndExternalManifests(int size)
    {
        using var client = new RecordingDaprClient(); var store = CreateStore(client);
        client.SeedByteStore("index", "{}"u8.ToArray());
        for (int attempt = 0; attempt < 3; attempt++)
        {
            ReadModelBatchScope scope = Scope() with { BatchId = "conflict-" + attempt };
            var batch = new ReadModelBatch(scope, [ReadModelBatchOperation.Write("detail", new { Name = new string('x', size) }, ReadModelBatchConcurrency.CreateOnly),
                ReadModelBatchOperation.Write("index", new IndexEntry(1), ReadModelBatchConcurrency.CreateOnly)]) { RetainManifestForRecovery = true };
            (await store.ExecuteAsync(batch)).ConflictKind.ShouldBe(ReadModelBatchConflictKind.Optimistic);
            ReadModelBatchMarker marker = ReadModelBatchMarker.ReadProof(client.ByteStoreValue(scope.ComputeMarkerKey()))!;
            marker.Status.ShouldBe(ReadModelBatchMarkerStatus.Aborted); marker.OriginalManifest.ShouldBeNull(); marker.OriginalManifestKey.ShouldBeNull();
            client.ByteStoreContains(ReadModelBatchKeys.ManifestKey(scope.ComputeScopeHash())).ShouldBeFalse();
            client.ByteStoreContains("detail").ShouldBeFalse();
        }
    }
    [Theory]
    [InlineData("scope")]
    [InlineData("key")]
    [InlineData("candidate")]
    [InlineData("previous")]
    [InlineData("missing-kind")]
    [InlineData("retained-payload")]
    public async Task TerminalAnchorProofCorruptionIsRefusedWithoutMutation(string fault)
    {
        using var client = new RecordingDaprClient();
        var batch = new ReadModelBatch(Scope(), [ReadModelBatchOperation.Write("detail", new { Name = new string('x', 20_000) }, ReadModelBatchConcurrency.CreateOnly)])
            { RetainManifestForRecovery = true };
        var protocol = new ReadModelBatchProtocol(new DaprReadModelBatchStateAccessor(client, Store), new(), NullLogger.Instance);
        (await protocol.ExecuteAsync(batch, new PendingBatchRecoveryStopAtPhase(ReadModelBatchPhase.RecoveryReceiptWritten), TestContext.Current.CancellationToken)).IsSuccess.ShouldBeFalse();
        JsonNode marker = JsonNode.Parse(client.ByteStoreValue(Scope().ComputeMarkerKey()))!;
        JsonNode anchor = marker["recoveryAnchor"]!;
        if (fault == "retained-payload") marker["manifest"] = Convert.ToBase64String("{}"u8.ToArray());
        if (fault == "scope") anchor["scope"]!["aggregateId"] = "other";
        if (fault == "key") anchor["key"] = "other";
        if (fault == "candidate") anchor["candidateDigest"] = new string('A', 64);
        if (fault == "previous") anchor["previousDigest"] = new string('A', 64);
        if (fault == "missing-kind") anchor.AsObject().Remove("isDelete");
        client.SeedByteStore(Scope().ComputeMarkerKey(), JsonSerializer.SerializeToUtf8Bytes(marker));
        int writes = client.TrySaveByteOperations.Count, deletes = client.TryDeleteByteOperations.Count;
        (await CreateStore(client).RecoverPendingAsync(Scope(), "detail")).IsResolved.ShouldBeFalse();
        client.TrySaveByteOperations.Count.ShouldBe(writes); client.TryDeleteByteOperations.Count.ShouldBe(deletes);
    }

}
