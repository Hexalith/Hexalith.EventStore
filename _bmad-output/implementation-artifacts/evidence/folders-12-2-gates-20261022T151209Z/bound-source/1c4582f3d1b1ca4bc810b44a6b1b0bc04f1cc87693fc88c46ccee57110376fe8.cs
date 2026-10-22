using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

public partial class DaprReadModelBatchTests
{
    private static ReadModelBatch CompletionBatch(string receipt = ReadModelRebuildCompletion.ReceiptProjectionName)
    {
        ReadModelBatchScope scope = Scope() with { ProjectionType = "rebuild" };
        var selected = new ProjectionOwnerSelection(1, scope.TenantId, scope.Domain, scope.AggregateId,
            "detail", "owner-1", "generation-1", 771, "event-771", scope.BatchId, "freshness-771")
            { ReceiptProjectionName = receipt };
        return new(scope,
        [
            ReadModelBatchOperation.Write("tenant-1:counter:agg-1:detail", new
            { ProjectionVersion = selected.FreshnessVersion, ProjectionOwner = selected }, ReadModelBatchConcurrency.CreateOnly),
            ReadModelBatchOperation.Write("rich-output", new { Messages = new string('x', 20_000) }, ReadModelBatchConcurrency.CreateOnly),
        ]) { OwnerSelections = [selected] };
    }

    private static Task<ProjectionOwnerProofResult> CompletionOwnerAsync(DaprReadModelStore store, ReadModelBatch batch)
        => store.ReadOwnerProofAsync(new(batch.Scope.TenantId, batch.Scope.Domain, batch.Scope.AggregateId,
            "detail", "catalog"), new(Store, "detail"));

    [Theory]
    [InlineData(ReadModelBatchStoreProfile.Resumable)]
    [InlineData(ReadModelBatchStoreProfile.TransactionQualified)]
    public async Task ContentFreeCompletion_ActualSdkCompletionPublishesOnlyBoundedMetadata(ReadModelBatchStoreProfile profile)
    {
        using var client = new RecordingDaprClient();
        var store = CreateStore(client, profile);
        var batch = CompletionBatch();
        (await store.ExecuteAsync(batch)).IsSuccess.ShouldBeTrue();
        string key = ReadModelBatchKeys.CompletionKey(batch.Scope.ComputeScopeHash());
        byte[] completion = client.ByteStoreValue(key);
        var proof = ReadModelBatchCompletionProof.Read(completion).ShouldNotBeNull();
        proof.Fingerprint.ShouldBe(ReadModelBatchFingerprint.Compute(batch));
        proof.Owners.ShouldBe(batch.OwnerSelections!);
        Encoding.UTF8.GetString(completion).ShouldNotContain("messages");
        client.ByteStoreContains(ReadModelBatchKeys.ManifestKey(batch.Scope.ComputeScopeHash())).ShouldBeFalse();
        int writes = client.TrySaveByteOperations.Count;
        client.BeforeReadByteState = read => (read == "tenant-1:counter:agg-1:detail" || read == key).ShouldBeTrue(read);
        var result = await CompletionOwnerAsync(store, batch);
        result.Proof.ShouldNotBeNull().ReceiptFingerprint.ShouldBe(proof.Fingerprint);
        result.MetadataKeyReads.ShouldBe(2);
        client.TrySaveByteOperations.Count.ShouldBe(writes);
    }

    [Theory]
    [InlineData(ReadModelBatchPhase.MarkerPrepared, -1)]
    [InlineData(ReadModelBatchPhase.AfterInstallOperation, 0)]
    [InlineData(ReadModelBatchPhase.BeforeCommit, -1)]
    [InlineData(ReadModelBatchPhase.AfterCommit, -1)]
    [InlineData(ReadModelBatchPhase.AfterCompaction, 0)]
    [InlineData(ReadModelBatchPhase.AfterCompaction, 1)]
    [InlineData(ReadModelBatchPhase.BeforeReceipt, -1)]
    [InlineData(ReadModelBatchPhase.BeforeCompletionProof, -1)]
    [InlineData(ReadModelBatchPhase.AfterCompletionProof, -1)]
    public async Task ContentFreeCompletion_EveryInterruptionRefusesWithoutRichReadsAndOriginalRetryConverges(ReadModelBatchPhase phase, int ordinal)
    {
        using var client = new RecordingDaprClient();
        var batch = CompletionBatch();
        var protocol = new ReadModelBatchProtocol(new DaprReadModelBatchStateAccessor(client, Store), new(), NullLogger.Instance);
        var fault = new NativeOwnerRecoveryFault(phase, ordinal);
        await Should.ThrowAsync<InvalidOperationException>(() => protocol.ExecuteAsync(batch, fault, default));
        fault.Fired.ShouldBeTrue();
        string completionKey = ReadModelBatchKeys.CompletionKey(batch.Scope.ComputeScopeHash());
        bool completed = phase == ReadModelBatchPhase.AfterCompletionProof;
        client.ByteStoreContains(completionKey).ShouldBe(completed);
        byte[]? originalReceipt = client.ByteStoreValue(batch.Scope.ComputeMarkerKey());
        string? originalTime = JsonNode.Parse(originalReceipt!)!["time"]?.GetValue<string>();
        int writes = client.TrySaveByteOperations.Count;
        client.BeforeReadByteState = key => (key == "tenant-1:counter:agg-1:detail" || key == completionKey).ShouldBeTrue(key);
        var store = CreateStore(client);
        ((await CompletionOwnerAsync(store, batch)).Proof is not null).ShouldBe(completed);
        // A pending content-free selection must refuse visibility without loading its rich marker.
        if (client.ByteStoreValue("tenant-1:counter:agg-1:detail") is { } selectedBytes && ReadModelBatchEnvelope.IsEnvelope(selectedBytes))
            await Should.ThrowAsync<InvalidOperationException>(() => store.GetCompactedAsync<JsonDocument>(Store, "tenant-1:counter:agg-1:detail"));
        else _ = await store.GetCompactedAsync<JsonDocument>(Store, "tenant-1:counter:agg-1:detail");
        client.TrySaveByteOperations.Count.ShouldBe(writes);
        client.BeforeReadByteState = null;
        (await CreateStore(client).ExecuteAsync(batch)).IsSuccess.ShouldBeTrue();
        byte[] proofBytes = client.ByteStoreValue(completionKey);
        var recovered = ReadModelBatchCompletionProof.Read(proofBytes).ShouldNotBeNull();
        recovered.Fingerprint.ShouldBe(ReadModelBatchFingerprint.Compute(batch));
        if (originalTime is not null) recovered.TerminalTimeUtc.ShouldBe(originalTime);
        (await CreateStore(client).ExecuteAsync(batch)).IsSuccess.ShouldBeTrue();
        client.ByteStoreValue(completionKey).ShouldBe(proofBytes);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("malformed")]
    [InlineData("scope")]
    [InlineData("batch")]
    [InlineData("fingerprint")]
    [InlineData("owner")]
    [InlineData("unknown-rich-field")]
    [InlineData("unknown-nested-rich-field")]
    [InlineData("duplicate")]
    public async Task ContentFreeCompletion_ChangedEvidenceNeverFallsBackToTheRichReceipt(string change)
    {
        using var client = new RecordingDaprClient();
        var store = CreateStore(client);
        var batch = CompletionBatch();
        (await store.ExecuteAsync(batch)).IsSuccess.ShouldBeTrue();
        string key = ReadModelBatchKeys.CompletionKey(batch.Scope.ComputeScopeHash());
        JsonObject proof = JsonNode.Parse(client.ByteStoreValue(key))!.AsObject();
        if (change == "missing")
            await store.TryEraseAsync(Store, key, (await store.GetAsync<JsonDocument>(Store, key)).ETag!);
        else
        {
            if (change == "scope") proof["scopeHash"] = "different-scope";
            if (change == "batch") proof["batchId"] = "different-batch";
            if (change == "fingerprint") proof["fingerprint"] = "v1:" + new string('A', 43);
            if (change == "owner") proof["owners"]![0]!["ownerGeneration"] = "different-owner";
            if (change == "unknown-rich-field") proof["messages"] = "forbidden";
            if (change == "unknown-nested-rich-field") proof["owners"]![0]!["messages"] = "forbidden";
            string text = change == "malformed" ? "{" : proof.ToJsonString();
            if (change == "duplicate") text = text.Insert(1, "\"version\":1,");
            client.SeedByteStore(key, Encoding.UTF8.GetBytes(text));
        }
        int writes = client.TrySaveByteOperations.Count;
        client.BeforeReadByteState = read => (read == "tenant-1:counter:agg-1:detail" || read == key).ShouldBeTrue(read);
        (await CompletionOwnerAsync(store, batch)).Proof.ShouldBeNull();
        client.TrySaveByteOperations.Count.ShouldBe(writes);
    }

    [Fact]
    public async Task ContentFreeCompletion_LegacyRetryDoesNotAcquireNewAuthorityAndOldReaderCannotReachRichMarker()
    {
        using var client = new RecordingDaprClient();
        var store = CreateStore(client);
        var old = CompletionBatch("rebuild");
        (await store.ExecuteAsync(old)).IsSuccess.ShouldBeTrue();
        (await store.ExecuteAsync(old)).IsSuccess.ShouldBeTrue();
        client.ByteStoreContains(ReadModelBatchKeys.CompletionKey(old.Scope.ComputeScopeHash())).ShouldBeFalse();
        using var optedInBackend = new RecordingDaprClient();
        var optedInStore = CreateStore(optedInBackend);
        var current = CompletionBatch();
        (await optedInStore.ExecuteAsync(current)).IsSuccess.ShouldBeTrue();
        var actual = (await optedInStore.GetCompactedAsync<JsonDocument>(Store, "tenant-1:counter:agg-1:detail")).Value!;
        var selected = ReadModelOwnerSelectionMetadata.Read(System.Text.Encoding.UTF8.GetBytes(actual.RootElement.GetRawText())).ShouldNotBeNull();
        optedInBackend.ByteStoreContains(ReadModelBatchKeys.CompletionKey(current.Scope.ComputeScopeHash())).ShouldBeTrue();
        optedInBackend.ByteStoreContains(current.Scope.ComputeMarkerKey()).ShouldBeTrue();
        // Execute the old lookup algorithm with the actual newly persisted discriminator. It hashes a
        // different scope, reads only that missing key and has no fallback to the real rebuild marker.
        var oldScope = new ReadModelBatchScope(Store, selected.TenantId, selected.Domain, selected.AggregateId,
            selected.ReceiptProjectionName!, selected.BatchId);
        string oldReaderKey = oldScope.ComputeMarkerKey();
        oldReaderKey.ShouldNotBe(current.Scope.ComputeMarkerKey());
        int reads = 0, writes = optedInBackend.TrySaveByteOperations.Count;
        optedInBackend.BeforeReadByteState = key => { key.ShouldBe(oldReaderKey); reads++; };
        var missing = await new DaprReadModelBatchStateAccessor(optedInBackend, Store).ReadAsync(oldReaderKey, default);
        missing.Exists.ShouldBeFalse();
        reads.ShouldBe(1);
        optedInBackend.TrySaveByteOperations.Count.ShouldBe(writes);
    }

    [Fact]
    public async Task ContentFreeCompletion_MisqualifiedPartialTransactionCannotPublishProof()
    {
        using var client = new RecordingDaprClient { TransactionOperationApplyFilter = operation => operation.Key != "rich-output" };
        var store = CreateStore(client, ReadModelBatchStoreProfile.TransactionQualified);
        var batch = CompletionBatch();
        (await store.ExecuteAsync(batch)).IsSuccess.ShouldBeFalse();
        client.ByteStoreContains(batch.Scope.ComputeMarkerKey()).ShouldBeTrue();
        client.ByteStoreContains(ReadModelBatchKeys.CompletionKey(batch.Scope.ComputeScopeHash())).ShouldBeFalse();
        (await CompletionOwnerAsync(store, batch)).Proof.ShouldBeNull();
        (await store.ExecuteAsync(batch)).IsSuccess.ShouldBeFalse();
    }
}
