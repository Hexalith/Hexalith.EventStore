using System.Text.Json.Nodes;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

public partial class DaprReadModelBatchTests
{
    [Theory]
    [InlineData(ReadModelBatchStoreProfile.Resumable, 12807, true, 'x')]
    [InlineData(ReadModelBatchStoreProfile.TransactionQualified, 12807, true, 'x')]
    [InlineData(ReadModelBatchStoreProfile.Resumable, 16384, true, 'x')]
    [InlineData(ReadModelBatchStoreProfile.TransactionQualified, 16384, true, 'x')]
    [InlineData(ReadModelBatchStoreProfile.Resumable, 16385, false, 'x')]
    [InlineData(ReadModelBatchStoreProfile.TransactionQualified, 16385, false, 'x')]
    [InlineData(ReadModelBatchStoreProfile.Resumable, 16384, true, '~')]
    [InlineData(ReadModelBatchStoreProfile.TransactionQualified, 16384, true, '~')]
    [InlineData(ReadModelBatchStoreProfile.Resumable, 16385, false, '~')]
    [InlineData(ReadModelBatchStoreProfile.TransactionQualified, 16385, false, '~')]
    public async Task DeadlineOwnerProofQualifiesOnlyUnexpiredUnambiguousSelectedEvidence(ReadModelBatchStoreProfile profile, int logicalBytes, bool qualified, char padding)
    {
        using var client = new RecordingDaprClient();
        DateTimeOffset deadline = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
        var clock = new ProjectionDeadlineTimeProvider(deadline.AddTicks(-1));
        var options = new ReadModelBatchOptions { TimeProvider = clock };
        options.StoreProfiles[Store] = profile;
        var store = new DaprReadModelStore(client, Options.Create(options));
        ReadModelBatchScope scope = Scope();
        var owner = new ProjectionOwnerSelection(1, scope.TenantId, scope.Domain, scope.AggregateId,
            scope.ProjectionType, "owner", "generation", 1, scope.BatchId, scope.BatchId, "version-1");
        string key = scope.TenantId + ":" + scope.Domain + ":" + scope.AggregateId + ":detail";
        int overhead = ReadModelBatchCanonicalJson.Serialize(new { Count = 1, Padding = "", ProjectionOwner = owner, ProjectionVersion = owner.FreshnessVersion }).Length;
        ReadModelBatchOperation operation = ReadModelBatchOperation.WriteExpiring(key,
            new { Count = 1, Padding = new string(padding, logicalBytes - overhead), ProjectionOwner = owner, ProjectionVersion = owner.FreshnessVersion }, ReadModelBatchConcurrency.CreateOnly, expiresAtUtc: deadline);
        operation.CanonicalValue.Length.ShouldBe(logicalBytes);
        var batch = new ReadModelBatch(scope, [operation])
        { OwnerSelection = owner };
        (await store.ExecuteAsync(batch)).IsSuccess.ShouldBeTrue();
        int mutations = client.TrySaveByteOperations.Count;
        var request = new ProjectionOwnerProofRequest(scope.TenantId, scope.Domain, scope.AggregateId, scope.ProjectionType, "catalog");
        var binding = new ReadModelOwnerProofBinding(Store, "detail");
        ProjectionOwnerProofResult current = await store.ReadOwnerProofAsync(request, binding);
        if (!qualified)
        {
            current.Proof.ShouldBeNull();
            current.ReasonCode.ShouldBe("delivery_reconciliation_required");
            return;
        }
        current.ReadBodyBytes.ShouldBeLessThanOrEqualTo(32_768);
        current.ReadBodyBytes.ShouldBe(client.ByteStoreValue(key).Length + client.ByteStoreValue(scope.ComputeMarkerKey()).Length);
        current.Proof.ShouldNotBeNull().Selection.ShouldBe(owner);
        current.MetadataKeyReads.ShouldBe(2);
        byte[] receipt = client.ByteStoreValue(scope.ComputeMarkerKey());
        JsonObject invalidReceipt = JsonNode.Parse(receipt)!.AsObject();
        invalidReceipt["scratchCompensationKey"] = "foreign";
        client.SeedByteStore(scope.ComputeMarkerKey(), System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(invalidReceipt));
        (await store.ReadOwnerProofAsync(request, binding)).Proof.ShouldBeNull();
        client.SeedByteStore(scope.ComputeMarkerKey(), receipt);
        client.BeforeReadByteState = readKey => { if (readKey == scope.ComputeMarkerKey()) clock.UtcNow = deadline; };
        (await store.ReadOwnerProofAsync(request, binding)).Proof.ShouldBeNull();
        client.BeforeReadByteState = null;
        clock.UtcNow = deadline;
        (await store.ReadOwnerProofAsync(request, binding)).Proof.ShouldBeNull();
        client.TrySaveByteOperations.Count.ShouldBe(mutations);
        clock.UtcNow = deadline.AddTicks(-1);
        JsonObject raw = JsonNode.Parse(client.ByteStoreValue(key))!.AsObject();
        raw["expires"] = "invalid-deadline";
        client.SeedByteStore(key, System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(raw));
        (await store.ReadOwnerProofAsync(request, binding)).Proof.ShouldBeNull();
        client.TrySaveByteOperations.Count.ShouldBe(mutations);
    }
}
