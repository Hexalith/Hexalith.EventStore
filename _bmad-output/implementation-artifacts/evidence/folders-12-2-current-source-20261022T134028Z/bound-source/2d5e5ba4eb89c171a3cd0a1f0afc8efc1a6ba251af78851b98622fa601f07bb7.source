using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

public partial class DaprReadModelBatchTests
{
    [Theory]
    [InlineData(ReadModelBatchStoreProfile.Resumable)]
    [InlineData(ReadModelBatchStoreProfile.TransactionQualified)]
    public async Task CoordinatedReplayOwners_ReadSameExistingRebuildReceiptForEachSelectedRoute(ReadModelBatchStoreProfile profile)
    {
        using var client = new RecordingDaprClient();
        DaprReadModelStore store = CreateStore(client, profile);
        ReadModelBatchScope scope = Scope() with { ProjectionType = "rebuild" };
        ProjectionOwnerSelection[] selections = [.. new[] { "detail", "index" }.Select(route =>
            new ProjectionOwnerSelection(1, scope.TenantId, scope.Domain, scope.AggregateId, route,
                "owner-1", "generation-1", 771, "source-event-771", scope.BatchId, "freshness-771")
            { ReceiptProjectionName = "rebuild" })];
        var batch = new ReadModelBatch(scope, selections.Select(selection => ReadModelBatchOperation.Write(
            "tenant-1:counter:agg-1:" + selection.ProjectionName,
            new { Count = 771, ProjectionVersion = selection.FreshnessVersion, ProjectionOwner = selection },
            ReadModelBatchConcurrency.CreateOnly))) { OwnerSelections = selections };
        string fingerprint = ReadModelBatchFingerprint.Compute(batch);
        (await store.ExecuteAsync(batch)).IsSuccess.ShouldBeTrue();
        int writes = client.TrySaveByteOperations.Count;

        foreach (ProjectionOwnerSelection selected in selections)
        {
            ProjectionOwnerProofResult proof = await store.ReadOwnerProofAsync(new(scope.TenantId, scope.Domain,
                scope.AggregateId, selected.ProjectionName, "catalog"), new(Store, selected.ProjectionName));
            proof.Proof.ShouldNotBeNull().Selection.ShouldBe(selected);
            proof.Proof.ReceiptIdentity.ShouldBe(scope.ComputeScopeHash());
            proof.Proof.ReceiptFingerprint.ShouldBe(fingerprint);
            proof.MetadataKeyReads.ShouldBe(2);
        }

        client.TrySaveByteOperations.Count.ShouldBe(writes);
        using JsonDocument receipt = JsonDocument.Parse(client.ByteStoreValue(scope.ComputeMarkerKey()));
        receipt.RootElement.GetProperty("owners").GetArrayLength().ShouldBe(2);
        receipt.RootElement.TryGetProperty("owner", out _).ShouldBeFalse();
        receipt.RootElement.TryGetProperty("manifest", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task CoordinatedReplayOwners_StagedCandidatesCannotAuthorizeCompletionBeforeCommit()
    {
        using var client = new RecordingDaprClient();
        DaprReadModelStore store = CreateStore(client);
        ReadModelBatchScope scope = Scope() with { ProjectionType = "rebuild" };
        var selection = new ProjectionOwnerSelection(1, scope.TenantId, scope.Domain, scope.AggregateId,
            "detail", "owner-1", "generation-1", 257, "event-257", scope.BatchId, "freshness-257")
        { ReceiptProjectionName = "rebuild" };
        var batch = new ReadModelBatch(scope, [ReadModelBatchOperation.Write("tenant-1:counter:agg-1:detail",
            new { Count = 257, ProjectionVersion = selection.FreshnessVersion, ProjectionOwner = selection },
            ReadModelBatchConcurrency.CreateOnly)]) { OwnerSelections = [selection] };
        (await store.StageAsync(batch)).Status.ShouldBe(ReadModelBatchStagingStatus.Prepared);
        var request = new ProjectionOwnerProofRequest(scope.TenantId, scope.Domain, scope.AggregateId, "detail", "catalog");
        int writes = client.TrySaveByteOperations.Count;
        (await store.ReadOwnerProofAsync(request, new(Store, "detail"))).Proof.ShouldBeNull();
        client.TrySaveByteOperations.Count.ShouldBe(writes);
        (await store.CommitAsync(batch)).Status.ShouldBe(ReadModelBatchStagingStatus.Committed);
        (await store.ReadOwnerProofAsync(request, new(Store, "detail"))).Proof.ShouldNotBeNull().Selection.ShouldBe(selection);
    }

    [Fact]
    public async Task CoordinatedReplayOwners_ConflictingOrInventedRouteEvidenceFailsBeforeStateAccess()
    {
        using var client = new RecordingDaprClient();
        DaprReadModelStore store = CreateStore(client);
        ReadModelBatchScope scope = Scope() with { ProjectionType = "rebuild" };
        var selection = new ProjectionOwnerSelection(1, scope.TenantId, scope.Domain, scope.AggregateId,
            "detail", "owner-1", "generation-1", 257, "event-257", scope.BatchId, "freshness-257")
        { ReceiptProjectionName = "rebuild" };
        ReadModelBatchOperation operation = ReadModelBatchOperation.Write("tenant-1:counter:agg-1:detail",
            new { ProjectionVersion = selection.FreshnessVersion, ProjectionOwner = selection }, ReadModelBatchConcurrency.CreateOnly);
        var forged = new ReadModelBatch(scope, [operation]) { OwnerSelections = [selection with { ProjectionName = "index" }] };
        await Should.ThrowAsync<ArgumentException>(() => store.ExecuteAsync(forged));
        client.TrySaveByteOperations.ShouldBeEmpty();
        var ambiguous = new ReadModelBatch(scope, [operation]) { OwnerSelection = selection, OwnerSelections = [selection] };
        await Should.ThrowAsync<ArgumentException>(() => store.ExecuteAsync(ambiguous));
        client.TrySaveByteOperations.ShouldBeEmpty();
    }
}
