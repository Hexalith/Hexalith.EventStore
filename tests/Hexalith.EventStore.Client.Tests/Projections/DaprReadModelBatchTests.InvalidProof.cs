using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

public partial class DaprReadModelBatchTests
{
    [Theory]
    [InlineData("owner", "duplicate")]
    [InlineData("owner", "nested_duplicate")]
    [InlineData("owner", "case_duplicate")]
    [InlineData("owner", "case_nested_duplicate")]
    [InlineData("owner", "fingerprint")]
    [InlineData("owner", "padding_bits")]
    [InlineData("applied", "duplicate")]
    [InlineData("applied", "nested_duplicate")]
    [InlineData("applied", "case_duplicate")]
    [InlineData("applied", "case_nested_duplicate")]
    [InlineData("applied", "fingerprint")]
    [InlineData("applied", "padding_bits")]
    [InlineData("reconcile", "duplicate")]
    [InlineData("reconcile", "nested_duplicate")]
    [InlineData("reconcile", "case_duplicate")]
    [InlineData("reconcile", "case_nested_duplicate")]
    [InlineData("reconcile", "fingerprint")]
    [InlineData("reconcile", "padding_bits")]
    public async Task ReceiptProof_RejectsMalformedOrDuplicateEvidenceWithinItsExistingReadsWithoutMutation(string operation, string corruption)
    {
        using var client = new RecordingDaprClient();
        DaprReadModelStore store = CreateStore(client);
        ReadModelBatchScope scope = Scope();
        var selection = new ProjectionOwnerSelection(1, scope.TenantId, scope.Domain, scope.AggregateId,
            scope.ProjectionType, "owner-1", "generation-1", 2, scope.BatchId, scope.BatchId, "version-2");
        var batch = new ReadModelBatch(scope, [ReadModelBatchOperation.Write("tenant-1:counter:agg-1:detail",
            new { Count = 2, ProjectionVersion = selection.FreshnessVersion, ProjectionOwner = selection },
            ReadModelBatchConcurrency.LastWrite)]) { OwnerSelection = selection };
        (await store.ExecuteAsync(batch)).IsSuccess.ShouldBeTrue();
        string fingerprint = ReadModelBatchFingerprint.Compute(batch);
        string original = Encoding.UTF8.GetString(client.ByteStoreValue(scope.ComputeMarkerKey()));
        string corrupted = corruption switch
        {
            "duplicate" => original.Insert(1, "\"fp\":" + JsonSerializer.Serialize(fingerprint) + ","),
            "nested_duplicate" => original.Replace("\"owner\":{", "\"owner\":{\"version\":999,", StringComparison.Ordinal),
            "case_duplicate" => original.Insert(1, "\"FP\":" + JsonSerializer.Serialize(fingerprint) + ","),
            "case_nested_duplicate" => original.Replace("\"owner\":{", "\"owner\":{\"Version\":999,", StringComparison.Ordinal),
            "padding_bits" => original.Replace(fingerprint, "v1:" + new string('A', 42) + "B", StringComparison.Ordinal),
            _ => original.Replace(fingerprint, "unsupported-fingerprint", StringComparison.Ordinal),
        };
        corrupted.ShouldNotBe(original);
        byte[] rejected = Encoding.UTF8.GetBytes(corrupted);
        client.SeedByteStore(scope.ComputeMarkerKey(), rejected);
        int mutations = client.TrySaveByteOperations.Count;
        var request = new ProjectionOwnerProofRequest(scope.TenantId, scope.Domain, scope.AggregateId, scope.ProjectionType, "catalog");
        var applied = new ProjectionAppliedReceiptRequest(request, 2, scope.BatchId, selection.OwnerIdentity, selection.OwnerGeneration);

        ProjectionOwnerProofResult result = operation switch
        {
            "owner" => await store.ReadOwnerProofAsync(request, new(Store, "detail")),
            "applied" => await store.ReadAppliedReceiptAsync(applied, new(Store, "detail")),
            _ => await store.ReconcileAppliedReceiptAsync(applied, new(Store, "detail")),
        };

        result.Proof.ShouldBeNull();
        result.ReasonCode.ShouldBe("delivery_reconciliation_required");
        result.MetadataKeyReads.ShouldBe(operation == "owner" ? 2 : 1);
        result.ReadBodyBytes.ShouldBeGreaterThanOrEqualTo(rejected.Length);
        client.TrySaveByteOperations.Count.ShouldBe(mutations);
        client.ByteStoreValue(scope.ComputeMarkerKey()).ShouldBe(rejected);
    }
}
