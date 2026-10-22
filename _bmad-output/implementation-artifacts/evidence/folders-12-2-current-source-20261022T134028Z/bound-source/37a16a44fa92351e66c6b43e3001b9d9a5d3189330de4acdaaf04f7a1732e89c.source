using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

public partial class DaprReadModelBatchTests
{
    [Theory]
    [InlineData(ReadModelBatchStoreProfile.Resumable, false)]
    [InlineData(ReadModelBatchStoreProfile.Resumable, true)]
    [InlineData(ReadModelBatchStoreProfile.TransactionQualified, false)]
    [InlineData(ReadModelBatchStoreProfile.TransactionQualified, true)]
    public async Task OriginalEventManifest_TerminalCleanupUncertaintyRetainsSameIdentityRecoveryWithoutModelWrites(
        ReadModelBatchStoreProfile profile, bool throws)
    {
        using var client = new RecordingDaprClient();
        DaprReadModelStore store = CreateStore(client, profile);
        ReadModelBatchScope scope = Scope();
        var selected = new ProjectionOwnerSelection(1, scope.TenantId, scope.Domain, scope.AggregateId, scope.ProjectionType,
            "owner-1", "generation-1", 2, scope.BatchId, scope.BatchId, "version-2");
        const string detail = "tenant-1:counter:agg-1:detail";
        const string index = "tenant-1:counter:agg-1:index";
        var batch = new ReadModelBatch(scope,
        [ReadModelBatchOperation.Write(detail, new { Count = 2, ProjectionVersion = selected.FreshnessVersion, ProjectionOwner = selected }, ReadModelBatchConcurrency.CreateOnly),
            ReadModelBatchOperation.Write(index, new { Value = new string('x', 32_768) }, ReadModelBatchConcurrency.CreateOnly)]) { OwnerSelection = selected };
        string key = ReadModelBatchKeys.ManifestKey(scope.ComputeScopeHash());
        client.TryDeleteByteOverride = target => target == key ? throws ? throw new IOException("Lost cleanup before deletion.") : false : null;
        (await store.ExecuteAsync(batch)).IsSuccess.ShouldBeFalse();
        client.ByteStoreValue(key).ShouldBe(ReadModelBatchFingerprint.BuildCanonicalManifest(batch));
        byte[] modelBefore = client.ByteStoreValue(detail), indexBefore = client.ByteStoreValue(index);
        byte[] receiptBefore = client.ByteStoreValue(scope.ComputeMarkerKey());
        var request = new ProjectionAppliedReceiptRequest(new(scope.TenantId, scope.Domain, scope.AggregateId, scope.ProjectionType, "catalog"),
            2, scope.BatchId, selected.OwnerIdentity, selected.OwnerGeneration);
        int deletes = client.TryDeleteByteOperations.Count;
        (await store.ReadAppliedReceiptAsync(request, new(Store, "detail"))).Proof.ShouldNotBeNull();
        client.TryDeleteByteOperations.Count.ShouldBe(deletes, "read-only receipt inspection cannot clean up");
        (await store.ReconcileAppliedReceiptAsync(request, new(Store, "detail"))).Proof.ShouldBeNull();
        client.ByteStoreContains(key).ShouldBeTrue();
        client.TryDeleteByteOverride = null!;
        using ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope();
        ProjectionOwnerProofResult recovered = await store.ReconcileAppliedReceiptAsync(request, new(Store, "detail"));
        recovered.Proof.ShouldNotBeNull().ReceiptFingerprint.ShouldBe(ReadModelBatchFingerprint.Compute(batch));
        client.ByteStoreContains(key).ShouldBeFalse();
        client.ByteStoreValue(detail).ShouldBe(modelBefore);
        client.ByteStoreValue(index).ShouldBe(indexBefore);
        client.ByteStoreValue(scope.ComputeMarkerKey()).ShouldBe(receiptBefore);
        cost[ProjectionDeliveryCost.ModelMutations].ShouldBe(0);
        cost[ProjectionDeliveryCost.BatchExecutions].ShouldBe(0);
    }

    [Fact]
    public async Task OriginalEventManifest_LostSuccessfulDeleteResponseIsProvenBySameIdentityReadback()
    {
        using var client = new RecordingDaprClient();
        DaprReadModelStore store = CreateStore(client);
        ReadModelBatchScope scope = Scope();
        var selected = new ProjectionOwnerSelection(1, scope.TenantId, scope.Domain, scope.AggregateId, scope.ProjectionType,
            "owner-1", "generation-1", 2, scope.BatchId, scope.BatchId, "version-2");
        var batch = new ReadModelBatch(scope,
        [ReadModelBatchOperation.Write("tenant-1:counter:agg-1:detail", new { ProjectionVersion = selected.FreshnessVersion,
            ProjectionOwner = selected, Value = new string('x', 32_768) }, ReadModelBatchConcurrency.CreateOnly)]) { OwnerSelection = selected };
        string key = ReadModelBatchKeys.ManifestKey(scope.ComputeScopeHash());
        client.AfterTryDeleteByteState = target => { if (target == key) throw new IOException("Lost successful cleanup response."); };
        (await store.ExecuteAsync(batch)).IsSuccess.ShouldBeTrue();
        client.ByteStoreContains(key).ShouldBeFalse();
        client.TryDeleteByteOperations.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OriginalEventManifest_ConflictingPrivateBytesOrForeignReferenceNeverResumeEffects(bool foreignReference)
    {
        using var client = new RecordingDaprClient();
        DaprReadModelStore store = CreateStore(client);
        ReadModelBatchScope scope = Scope();
        var selected = new ProjectionOwnerSelection(1, scope.TenantId, scope.Domain, scope.AggregateId, scope.ProjectionType,
            "owner-1", "generation-1", 2, scope.BatchId, scope.BatchId, "version-2");
        var batch = new ReadModelBatch(scope,
        [ReadModelBatchOperation.Write("tenant-1:counter:agg-1:detail",
            new { ProjectionVersion = selected.FreshnessVersion, ProjectionOwner = selected }, ReadModelBatchConcurrency.CreateOnly),
            ReadModelBatchOperation.Write("tenant-1:counter:agg-1:index", new { Value = new string('x', 32_768) }, ReadModelBatchConcurrency.CreateOnly)])
        { OwnerSelection = selected };
        (await store.StageAsync(batch)).Status.ShouldBe(ReadModelBatchStagingStatus.Prepared);
        System.Text.Json.Nodes.JsonNode marker = System.Text.Json.Nodes.JsonNode.Parse(client.ByteStoreValue(scope.ComputeMarkerKey()))!;
        string key = marker["manifestKey"]!.GetValue<string>();
        if (foreignReference)
        {
            marker["manifestKey"] = "another-scope-manifest";
            client.SeedByteStore(scope.ComputeMarkerKey(), JsonSerializer.SerializeToUtf8Bytes(marker));
        }
        else
        {
            byte[] changed = client.ByteStoreValue(key);
            changed[0] ^= 1;
            client.SeedByteStore(key, changed);
        }
        int writes = client.TrySaveByteOperations.Count;
        var request = new ProjectionAppliedReceiptRequest(new(scope.TenantId, scope.Domain, scope.AggregateId, scope.ProjectionType, "catalog"),
            2, scope.BatchId, selected.OwnerIdentity, selected.OwnerGeneration);
        ProjectionOwnerProofResult result = await store.ReconcileAppliedReceiptAsync(request, new(Store, "detail"));
        result.Proof.ShouldBeNull();
        result.ReasonCode.ShouldBe("delivery_reconciliation_required");
        result.MetadataKeyReads.ShouldBe(foreignReference ? 1 : 2);
        client.TrySaveByteOperations.Count.ShouldBe(writes);
        (await store.GetAsync<JsonDocument>(Store, "tenant-1:counter:agg-1:detail")).Value.ShouldBeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OriginalEventManifest_LargePreparedOrCommittedWorkUsesSeparateManifestBudgetAndRecovers(bool afterCommit)
    {
        using var client = new RecordingDaprClient();
        DaprReadModelStore store = CreateStore(client);
        ReadModelBatchScope scope = Scope();
        var selected = new ProjectionOwnerSelection(1, scope.TenantId, scope.Domain, scope.AggregateId, scope.ProjectionType,
            "owner-1", "generation-1", 2, scope.BatchId, scope.BatchId, "version-2");
        const string detail = "tenant-1:counter:agg-1:detail";
        const string index = "tenant-1:counter:agg-1:index";
        var batch = new ReadModelBatch(scope,
        [ReadModelBatchOperation.Write(detail, new { Count = 2, ProjectionVersion = selected.FreshnessVersion, ProjectionOwner = selected },
            ReadModelBatchConcurrency.CreateOnly),
            ReadModelBatchOperation.Write(index, new { Value = new string('x', 32_768) }, ReadModelBatchConcurrency.CreateOnly)])
        { OwnerSelection = selected };
        string fingerprint = ReadModelBatchFingerprint.Compute(batch);
        client.BeforeTrySaveByteState = (key, value) =>
        {
            if (key == index && ReadModelBatchEnvelope.IsEnvelope(value.Span) != afterCommit)
                throw new IOException("Interrupted original large batch.");
        };
        (await store.ExecuteAsync(batch)).IsSuccess.ShouldBeFalse();
        byte[] markerBytes = client.ByteStoreValue(scope.ComputeMarkerKey());
        markerBytes.Length.ShouldBeLessThanOrEqualTo(16_384);
        using JsonDocument marker = JsonDocument.Parse(markerBytes);
        marker.RootElement.TryGetProperty("manifest", out _).ShouldBeFalse();
        string manifestKey = marker.RootElement.GetProperty("manifestKey").GetString()!;
        byte[] immutableManifest = client.ByteStoreValue(manifestKey);
        immutableManifest.Length.ShouldBeGreaterThan(16_384);
        immutableManifest.ShouldBe(ReadModelBatchFingerprint.BuildCanonicalManifest(batch));
        ReadModelBatchFingerprint.ComputeFromManifest(immutableManifest).ShouldBe(fingerprint);
        client.BeforeTrySaveByteState = null!;

        var request = new ProjectionAppliedReceiptRequest(new(scope.TenantId, scope.Domain, scope.AggregateId, scope.ProjectionType, "catalog"),
            2, scope.BatchId, selected.OwnerIdentity, selected.OwnerGeneration);
        ProjectionOwnerProofResult resumed = await store.ReconcileAppliedReceiptAsync(request, new(Store, "detail"));
        resumed.Proof.ShouldNotBeNull().ReceiptFingerprint.ShouldBe(fingerprint);
        resumed.ReadBodyBytes.ShouldBeGreaterThan(immutableManifest.Length);
        (await store.ReadOwnerProofAsync(request.Scope, new(Store, "detail"))).Proof.ShouldBe(resumed.Proof);
        (await store.GetAsync<JsonDocument>(Store, index)).Value.ShouldNotBeNull().RootElement.GetProperty("value").GetString()!.Length.ShouldBe(32_768);
        (ReadOnlyMemory<byte> remaining, string etag) = await client.GetByteStateAndETagAsync(Store, manifestKey, null, null!, CancellationToken.None);
        remaining.IsEmpty.ShouldBeTrue();
        etag.ShouldBeNullOrEmpty();
    }

    [Fact]
    public async Task OriginalEventManifest_OneMiBCeilingCannotBeRaisedForGuardedOwners()
    {
        using var client = new RecordingDaprClient();
        var options = new ReadModelBatchOptions { MaxCanonicalManifestBytes = 2_097_152 };
        var store = new DaprReadModelStore(client, Microsoft.Extensions.Options.Options.Create(options));
        ReadModelBatchScope scope = Scope();
        var selected = new ProjectionOwnerSelection(1, scope.TenantId, scope.Domain, scope.AggregateId, scope.ProjectionType,
            "owner-1", "generation-1", 2, scope.BatchId, scope.BatchId, "version-2");
        var batch = new ReadModelBatch(scope,
        [ReadModelBatchOperation.Write("tenant-1:counter:agg-1:detail",
            new { Value = new string('x', 800_000), ProjectionVersion = selected.FreshnessVersion, ProjectionOwner = selected },
            ReadModelBatchConcurrency.CreateOnly)]) { OwnerSelection = selected };
        await Should.ThrowAsync<ArgumentException>(() => store.ExecuteAsync(batch));
        client.TrySaveByteOperations.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OriginalEventManifest_RecoversPreparedAndPartlyCompactedBatchesWithoutRecomputingIdentity(bool afterCommit)
    {
        using var client = new RecordingDaprClient();
        DaprReadModelStore store = CreateStore(client);
        ReadModelBatchScope scope = Scope();
        var selected = new ProjectionOwnerSelection(1, scope.TenantId, scope.Domain, scope.AggregateId, scope.ProjectionType,
            "owner-1", "generation-1", 2, scope.BatchId, scope.BatchId, "version-2");
        const string detailKey = "tenant-1:counter:agg-1:detail";
        const string indexKey = "tenant-1:counter:agg-1:index";
        var value = new { Count = 2, ProjectionVersion = selected.FreshnessVersion, ProjectionOwner = selected };
        var batch = new ReadModelBatch(scope,
        [ReadModelBatchOperation.Write(detailKey, value, ReadModelBatchConcurrency.CreateOnly),
            ReadModelBatchOperation.Write(indexKey, value, ReadModelBatchConcurrency.CreateOnly)]) { OwnerSelection = selected };
        string fingerprint = ReadModelBatchFingerprint.Compute(batch);
        client.BeforeTrySaveByteState = (key, bytes) =>
        {
            if (key == indexKey && ReadModelBatchEnvelope.IsEnvelope(bytes.Span) != afterCommit)
                throw new IOException("Interrupted original event batch.");
        };
        (await store.ExecuteAsync(batch)).IsSuccess.ShouldBeFalse();
        var request = new ProjectionAppliedReceiptRequest(new(scope.TenantId, scope.Domain, scope.AggregateId, scope.ProjectionType, "catalog"),
            2, scope.BatchId, selected.OwnerIdentity, selected.OwnerGeneration);
        int interruptedWrites = client.TrySaveByteOperations.Count;
        (await store.ReadOwnerProofAsync(request.Scope, new(Store, "detail"))).Proof.ShouldBeNull();
        (await store.ReadAppliedReceiptAsync(request, new(Store, "detail"))).Proof.ShouldBeNull();
        client.TrySaveByteOperations.Count.ShouldBe(interruptedWrites, "read-only proof must not reconcile the manifest");
        client.BeforeTrySaveByteState = null!;

        ProjectionOwnerProofResult recovered = await store.ReconcileAppliedReceiptAsync(request, new(Store, "detail"));
        recovered.Proof.ShouldNotBeNull().ReceiptFingerprint.ShouldBe(fingerprint);
        (await store.ReadOwnerProofAsync(request.Scope, new(Store, "detail"))).Proof.ShouldBe(recovered.Proof);
        JsonElement detail = (await store.GetAsync<JsonDocument>(Store, detailKey)).Value.ShouldNotBeNull().RootElement;
        JsonElement index = (await store.GetAsync<JsonDocument>(Store, indexKey)).Value.ShouldNotBeNull().RootElement;
        detail.GetProperty("count").GetInt32().ShouldBe(2);
        index.GetProperty("count").GetInt32().ShouldBe(2);
        using JsonDocument receipt = JsonDocument.Parse(client.ByteStoreValue(scope.ComputeMarkerKey()));
        receipt.RootElement.TryGetProperty("manifest", out _).ShouldBeFalse();
        receipt.RootElement.GetProperty("fp").GetString().ShouldBe(fingerprint);
        int writes = client.TrySaveByteOperations.Count;
        (await store.ReconcileAppliedReceiptAsync(request, new(Store, "detail"))).Proof.ShouldBe(recovered.Proof);
        client.TrySaveByteOperations.Count.ShouldBe(writes);
    }

    [Fact]
    public async Task OriginalEventManifest_WrongGenerationCannotResumeOrOverwritePartialWork()
    {
        using var client = new RecordingDaprClient();
        DaprReadModelStore store = CreateStore(client);
        ReadModelBatchScope scope = Scope();
        var selected = new ProjectionOwnerSelection(1, scope.TenantId, scope.Domain, scope.AggregateId, scope.ProjectionType,
            "owner-1", "generation-1", 2, scope.BatchId, scope.BatchId, "version-2");
        const string key = "tenant-1:counter:agg-1:detail";
        var batch = new ReadModelBatch(scope, [ReadModelBatchOperation.Write(key,
            new { Count = 2, ProjectionVersion = selected.FreshnessVersion, ProjectionOwner = selected }, ReadModelBatchConcurrency.CreateOnly)])
        { OwnerSelection = selected };
        client.BeforeTrySaveByteState = (currentKey, bytes) =>
        {
            if (currentKey == key && !ReadModelBatchEnvelope.IsEnvelope(bytes.Span)) throw new IOException("Interrupted compaction.");
        };
        (await store.ExecuteAsync(batch)).IsSuccess.ShouldBeFalse();
        client.BeforeTrySaveByteState = null!;
        int writes = client.TrySaveByteOperations.Count;
        var request = new ProjectionAppliedReceiptRequest(new(scope.TenantId, scope.Domain, scope.AggregateId, scope.ProjectionType, "catalog"),
            2, scope.BatchId, selected.OwnerIdentity, "different-generation");
        ProjectionOwnerProofResult result = await store.ReconcileAppliedReceiptAsync(request, new(Store, "detail"));
        result.Proof.ShouldBeNull();
        result.ReasonCode.ShouldBe("delivery_reconciliation_required");
        client.TrySaveByteOperations.Count.ShouldBe(writes);
    }
}
