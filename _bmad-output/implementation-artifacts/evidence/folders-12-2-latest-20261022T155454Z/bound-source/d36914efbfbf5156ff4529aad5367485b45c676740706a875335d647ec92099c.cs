using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

public partial class DaprReadModelBatchTests
{
    [Theory]
    [InlineData("missing_version")]
    [InlineData("unsupported_version")]
    [InlineData("missing_time")]
    [InlineData("invalid_time")]
    [InlineData("no_offset")]
    [InlineData("wrong_offset")]
    [InlineData("invalid_day")]
    public async Task ReceiptProof_MalformedTerminalControlsCannotAuthorizeOwnerOrAppliedProofOrMutate(string corruption)
    {
        using var client = new RecordingDaprClient();
        DaprReadModelStore store = CreateStore(client);
        ReadModelBatchScope scope = Scope();
        var selected = new ProjectionOwnerSelection(1, scope.TenantId, scope.Domain, scope.AggregateId, scope.ProjectionType,
            "owner-1", "generation-1", 2, scope.BatchId, scope.BatchId, "version-2");
        var batch = new ReadModelBatch(scope, [ReadModelBatchOperation.Write("tenant-1:counter:agg-1:detail", new
        { Count = 2, ProjectionVersion = selected.FreshnessVersion, ProjectionOwner = selected }, ReadModelBatchConcurrency.CreateOnly)])
        { OwnerSelection = selected };
        (await store.ExecuteAsync(batch)).IsSuccess.ShouldBeTrue();
        var request = new ProjectionAppliedReceiptRequest(new(scope.TenantId, scope.Domain, scope.AggregateId, scope.ProjectionType, "catalog"),
            2, scope.BatchId, selected.OwnerIdentity, selected.OwnerGeneration);
        (await store.ReadOwnerProofAsync(request.Scope, new(Store, "detail"))).Proof.ShouldNotBeNull();
        (await store.ReadAppliedReceiptAsync(request, new(Store, "detail"))).Proof.ShouldNotBeNull();
        byte[] original = client.ByteStoreValue(scope.ComputeMarkerKey());
        var marker = JsonNode.Parse(original)!.AsObject();
        if (corruption == "missing_version") marker.Remove("v");
        else if (corruption == "unsupported_version") marker["v"] = 2;
        else if (corruption == "missing_time") marker.Remove("time");
        else marker["time"] = corruption switch
        {
            "invalid_time" => "not-a-timestamp",
            "no_offset" => "2026-10-16T12:00:00",
            "wrong_offset" => "2026-10-16T12:00:00+01:00",
            _ => "2026-02-30T12:00:00Z",
        };
        byte[] malformed = JsonSerializer.SerializeToUtf8Bytes(marker);
        client.SeedByteStore(scope.ComputeMarkerKey(), malformed);
        int writes = client.TrySaveByteOperations.Count, deletes = client.TryDeleteByteOperations.Count;
        ProjectionOwnerProofResult owner = await store.ReadOwnerProofAsync(request.Scope, new(Store, "detail"));
        ProjectionOwnerProofResult applied = await store.ReadAppliedReceiptAsync(request, new(Store, "detail"));
        owner.Proof.ShouldBeNull();
        owner.ReasonCode.ShouldBe("delivery_reconciliation_required");
        applied.Proof.ShouldBeNull();
        applied.ReasonCode.ShouldBe("delivery_reconciliation_required");
        client.TrySaveByteOperations.Count.ShouldBe(writes);
        client.TryDeleteByteOperations.Count.ShouldBe(deletes);
        client.ByteStoreValue(scope.ComputeMarkerKey()).ShouldBe(malformed);
        client.SeedByteStore(scope.ComputeMarkerKey(), original);
        (await store.ReadOwnerProofAsync(request.Scope, new(Store, "detail"))).Proof.ShouldNotBeNull();
        (await store.ReadAppliedReceiptAsync(request, new(Store, "detail"))).Proof.ShouldNotBeNull();
        client.ByteStoreValue(scope.ComputeMarkerKey()).ShouldBe(original);
        client.TrySaveByteOperations.Count.ShouldBe(writes);
    }

    [Theory]
    [InlineData("2026-10-16T12:00:00Z")]
    [InlineData("2026-10-16T12:00:00.1Z")]
    [InlineData("2026-10-16T12:00:00.1234567Z")]
    [InlineData("2026-10-16T12:00:00+00:00")]
    [InlineData("2026-10-16T12:00:00.1234567+00:00")]
    public async Task ReceiptProof_ExplicitVersionAndLegitimateUtcTerminalRepresentationsPreserveWriterBytes(string terminal)
    {
        using var client = new RecordingDaprClient();
        DaprReadModelStore store = CreateStore(client);
        ReadModelBatchScope scope = Scope();
        var selected = new ProjectionOwnerSelection(1, scope.TenantId, scope.Domain, scope.AggregateId, scope.ProjectionType,
            "owner-1", "generation-1", 2, scope.BatchId, scope.BatchId, "version-2");
        var batch = new ReadModelBatch(scope, [ReadModelBatchOperation.Write("tenant-1:counter:agg-1:detail", new
        { Count = 2, ProjectionVersion = selected.FreshnessVersion, ProjectionOwner = selected }, ReadModelBatchConcurrency.CreateOnly)])
        { OwnerSelection = selected };
        (await store.ExecuteAsync(batch)).IsSuccess.ShouldBeTrue();
        byte[] original = client.ByteStoreValue(scope.ComputeMarkerKey());
        ReadModelBatchMarker.ReadProof(original).ShouldNotBeNull();
        var marker = JsonNode.Parse(original)!.AsObject();
        marker["time"] = terminal;
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(marker);
        client.SeedByteStore(scope.ComputeMarkerKey(), bytes);
        int writes = client.TrySaveByteOperations.Count;
        var request = new ProjectionAppliedReceiptRequest(new(scope.TenantId, scope.Domain, scope.AggregateId, scope.ProjectionType, "catalog"),
            2, scope.BatchId, selected.OwnerIdentity, selected.OwnerGeneration);
        (await store.ReadOwnerProofAsync(request.Scope, new(Store, "detail"))).Proof.ShouldNotBeNull();
        (await store.ReadAppliedReceiptAsync(request, new(Store, "detail"))).Proof.ShouldNotBeNull();
        client.TrySaveByteOperations.Count.ShouldBe(writes);
        client.ByteStoreValue(scope.ComputeMarkerKey()).ShouldBe(bytes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ReceiptProof_NonterminalRecoveryDoesNotRequireTerminalTime(int status)
    {
        var marker = new ReadModelBatchMarker { Status = (ReadModelBatchMarkerStatus)status, Operations = [], OriginalManifest = [1, 2, 3] };
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(marker, JsonSerializerOptions.Web);
        ReadModelBatchMarker read = ReadModelBatchMarker.ReadProof(bytes).ShouldNotBeNull();
        read.Status.ShouldBe((ReadModelBatchMarkerStatus)status);
        read.OriginalManifest.ShouldBe(marker.OriginalManifest);
        read.TerminalTimeUtc.ShouldBeNull();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReceiptProof_StrictProofParsingPreservesLegacyGuardOffBatchExecution(bool missingVersion)
    {
        using var client = new RecordingDaprClient();
        DaprReadModelStore store = CreateStore(client);
        ReadModelBatchScope scope = Scope();
        var batch = new ReadModelBatch(scope, [ReadModelBatchOperation.Write("detail", new Detail(1), ReadModelBatchConcurrency.CreateOnly)]);
        (await store.ExecuteAsync(batch)).IsSuccess.ShouldBeTrue();
        var marker = JsonNode.Parse(client.ByteStoreValue(scope.ComputeMarkerKey()))!.AsObject();
        if (missingVersion) marker.Remove("v");
        else marker["time"] = "legacy-nonblank-time";
        byte[] legacy = JsonSerializer.SerializeToUtf8Bytes(marker);
        client.SeedByteStore(scope.ComputeMarkerKey(), legacy);
        ReadModelBatchMarker.ReadProof(legacy).ShouldBeNull();
        int writes = client.TrySaveByteOperations.Count;
        (await store.ExecuteAsync(batch)).Status.ShouldBe(ReadModelBatchStatus.AlreadyCompleted);
        client.ByteStoreValue(scope.ComputeMarkerKey()).ShouldBe(legacy);
        client.TrySaveByteOperations.Count.ShouldBe(writes);
    }
}
