using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

public partial class DaprReadModelBatchTests
{
    [Fact]
    public async Task OtherBatchPendingEnvelopeSurvivesNonRetainedRecovery()
    {
        using var client = new RecordingDaprClient();
        var accessor = new DaprReadModelBatchStateAccessor(client, Store);
        var original = new ReadModelBatch(Scope(), [ReadModelBatchOperation.Write("detail", new Detail(1), ReadModelBatchConcurrency.CreateOnly)]);
        var protocol = new ReadModelBatchProtocol(accessor, new(), NullLogger.Instance);
        (await protocol.ExecuteAsync(original, new PendingBatchRecoveryStopAtPhase(ReadModelBatchPhase.BeforeReceipt), TestContext.Current.CancellationToken)).IsSuccess.ShouldBeFalse();
        ReadModelBatchMarker.ReadProof(client.ByteStoreValue(original.Scope.ComputeMarkerKey()))!.Status.ShouldBe(ReadModelBatchMarkerStatus.Committed);
        string etag = (await accessor.ReadAsync("detail", TestContext.Current.CancellationToken)).ETag;
        var competing = new ReadModelBatch(Scope() with { BatchId = "competing-dispatch" },
            [ReadModelBatchOperation.Write("detail", new Detail(2), ReadModelBatchConcurrency.Match(etag))]);
        (await new ReadModelBatchProtocol(accessor, new(), NullLogger.Instance).ExecuteAsync(competing,
            new PendingBatchRecoveryStopAtPhase(ReadModelBatchPhase.AfterInstallOperation), TestContext.Current.CancellationToken)).IsSuccess.ShouldBeFalse();
        byte[] pending = client.ByteStoreValue("detail");
        ReadModelBatchEnvelope.IsEnvelope(pending).ShouldBeTrue();

        (await CreateStore(client).ExecuteAsync(original)).IsSuccess.ShouldBeFalse();
        client.ByteStoreValue("detail").ShouldBe(pending);
    }

    [Fact]
    public async Task GuardedCommittedBatchCannotAuthorizeSupersedingOrdinaryState()
    {
        using var client = new RecordingDaprClient();
        var accessor = new DaprReadModelBatchStateAccessor(client, Store);
        ReadModelBatchScope scope = Scope();
        var owner = new ProjectionOwnerSelection(1, scope.TenantId, scope.Domain, scope.AggregateId,
            scope.ProjectionType, "owner-1", "generation-1", 2, scope.BatchId, scope.BatchId, "version-2");
        const string key = "tenant-1:counter:agg-1:counterView";
        var original = new ReadModelBatch(scope, [ReadModelBatchOperation.Write(key,
            new { Count = 2, ProjectionVersion = owner.FreshnessVersion, ProjectionOwner = owner }, ReadModelBatchConcurrency.CreateOnly)]) { OwnerSelection = owner };
        var protocol = new ReadModelBatchProtocol(accessor, new(), NullLogger.Instance);
        (await protocol.ExecuteAsync(original, new PendingBatchRecoveryStopAtPhase(ReadModelBatchPhase.BeforeReceipt), TestContext.Current.CancellationToken)).IsSuccess.ShouldBeFalse();
        ReadModelBatchMarker.ReadProof(client.ByteStoreValue(scope.ComputeMarkerKey()))!.Status.ShouldBe(ReadModelBatchMarkerStatus.Committed);
        byte[] newer = "{\"erased\":true,\"version\":99}"u8.ToArray();
        client.SeedByteStore(key, newer);

        (await CreateStore(client).ExecuteAsync(original)).IsSuccess.ShouldBeFalse();
        client.ByteStoreValue(key).ShouldBe(newer);
    }

    [Theory]
    [InlineData(ReadModelBatchPhase.AfterCommit, false, false)]
    [InlineData(ReadModelBatchPhase.AfterCommit, false, true)]
    [InlineData(ReadModelBatchPhase.AfterCommit, true, false)]
    [InlineData(ReadModelBatchPhase.AfterCommit, true, true)]
    [InlineData(ReadModelBatchPhase.BeforeCompaction, false, false)]
    [InlineData(ReadModelBatchPhase.BeforeCompaction, false, true)]
    [InlineData(ReadModelBatchPhase.BeforeReceipt, false, false)]
    [InlineData(ReadModelBatchPhase.BeforeReceipt, false, true)]
    public async Task NonRetainedCommittedBatchNeverReclaimsSupersedingOrdinaryState(
        ReadModelBatchPhase phase, bool batchDelete, bool newerDelete)
    {
        using var client = new RecordingDaprClient();
        client.SeedByteStore("detail", "{\"version\":0}"u8.ToArray());
        var accessor = new DaprReadModelBatchStateAccessor(client, Store);
        string etag = (await accessor.ReadAsync("detail", TestContext.Current.CancellationToken)).ETag;
        ReadModelBatch batch = new(Scope(), [batchDelete
            ? ReadModelBatchOperation.Delete("detail", ReadModelBatchConcurrency.Match(etag))
            : ReadModelBatchOperation.Write("detail", new Detail(1), ReadModelBatchConcurrency.Match(etag))]);
        var protocol = new ReadModelBatchProtocol(accessor, new(), NullLogger.Instance);
        (await protocol.ExecuteAsync(batch, new PendingBatchRecoveryStopAtPhase(phase), TestContext.Current.CancellationToken)).IsSuccess.ShouldBeFalse();
        byte[] newer = "{\"erased\":true,\"version\":99}"u8.ToArray();
        if (newerDelete)
        {
            var current = await accessor.ReadAsync("detail", TestContext.Current.CancellationToken);
            if (current.Exists) (await accessor.TryDeleteAsync("detail", current.ETag, TestContext.Current.CancellationToken)).ShouldBeTrue();
        }
        else client.SeedByteStore("detail", newer);
        byte[] expected = client.ByteStoreValue("detail");
        (await CreateStore(client).ExecuteAsync(batch)).IsSuccess.ShouldBeTrue();
        (await CreateStore(client).ExecuteAsync(batch)).Status.ShouldBe(ReadModelBatchStatus.AlreadyCompleted);
        (await CreateStore(client).StageAsync(batch)).Status.ShouldBe(ReadModelBatchStagingStatus.Committed);
        (await CreateStore(client).CommitAsync(batch)).Status.ShouldBe(ReadModelBatchStagingStatus.Committed);
        client.ByteStoreValue("detail").ShouldBe(expected);
        ReadModelBatchMarker receipt = ReadModelBatchMarker.ReadProof(client.ByteStoreValue(Scope().ComputeMarkerKey()))!;
        receipt.Status.ShouldBe(ReadModelBatchMarkerStatus.Completed);
        receipt.OriginalManifest.ShouldBeNull(); receipt.OriginalManifestKey.ShouldBeNull();
        client.ByteStoreContains(ReadModelBatchKeys.ManifestKey(Scope().ComputeScopeHash())).ShouldBeFalse();
    }

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
    [InlineData(ReadModelBatchPhase.RecoveryReceiptWritten, 10)]
    [InlineData(ReadModelBatchPhase.RecoveryManifestReclaimed, 10)]
    [InlineData(ReadModelBatchPhase.RecoveryReceiptWritten, 20_000)]
    [InlineData(ReadModelBatchPhase.RecoveryManifestReclaimed, 20_000)]
    public async Task DirectExecuteRetryFinishesOwnedRetainedAnchorBeforeTerminalVerification(ReadModelBatchPhase interruption, int size)
    {
        using var client = new RecordingDaprClient();
        ReadModelBatch batch = new(Scope(), [ReadModelBatchOperation.Write("index", new IndexEntry(1), ReadModelBatchConcurrency.CreateOnly),
            ReadModelBatchOperation.Write("detail", new { Name = new string('x', size) }, ReadModelBatchConcurrency.CreateOnly)]) { RetainManifestForRecovery = true };
        var protocol = new ReadModelBatchProtocol(new DaprReadModelBatchStateAccessor(client, Store), new(), NullLogger.Instance);
        (await protocol.ExecuteAsync(batch, new PendingBatchRecoveryStopAtPhase(interruption), TestContext.Current.CancellationToken)).IsSuccess.ShouldBeFalse();
        ReadModelBatchMarker.ReadProof(client.ByteStoreValue(batch.Scope.ComputeMarkerKey()))!.Status.ShouldBe(ReadModelBatchMarkerStatus.Completed);
        ReadModelBatchEnvelope.FromBytes(client.ByteStoreValue("detail")).ShouldNotBeNull();
        client.SeedByteStore("index", "{\"count\":99}"u8.ToArray());
        byte[] superseding = client.ByteStoreValue("index");
        (await CreateStore(client).ExecuteAsync(batch)).Status.ShouldBe(ReadModelBatchStatus.AlreadyCompleted);
        client.ByteStoreValue("index").ShouldBe(superseding);
        ReadModelBatchEnvelope.FromBytes(client.ByteStoreValue("detail")).ShouldBeNull();
        JsonDocument.Parse(client.ByteStoreValue("detail")).RootElement.GetProperty("name").GetString()!.Length.ShouldBe(size);
        client.ByteStoreContains(ReadModelBatchKeys.ManifestKey(batch.Scope.ComputeScopeHash())).ShouldBeFalse();
        (await CreateStore(client).ExecuteAsync(batch)).Status.ShouldBe(ReadModelBatchStatus.AlreadyCompleted);
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
