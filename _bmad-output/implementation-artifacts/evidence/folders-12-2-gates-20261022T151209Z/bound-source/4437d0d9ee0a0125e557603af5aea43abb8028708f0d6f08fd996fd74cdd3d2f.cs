using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

public partial class DaprReadModelBatchTests
{
    [Theory]
    [InlineData("owner")]
    [InlineData("case_owner")]
    [InlineData("version")]
    [InlineData("case_version")]
    [InlineData("generation")]
    [InlineData("freshness")]
    [InlineData("case_freshness")]
    public async Task SelectedOwner_RejectsAmbiguousPersistedMetadataBeforeAuthorizingReceipts(string corruption)
    {
        using var client = new RecordingDaprClient();
        DaprReadModelStore store = CreateStore(client);
        ReadModelBatchScope scope = Scope();
        var owner = new ProjectionOwnerSelection(1, scope.TenantId, scope.Domain, scope.AggregateId, scope.ProjectionType,
            "owner-1", "generation-1", 2, scope.BatchId, scope.BatchId, "version-2");
        string json = JsonSerializer.Serialize(new { ProjectionVersion = owner.FreshnessVersion, ProjectionOwner = owner }, JsonSerializerOptions.Web);
        string invalid = corruption switch
        {
            "owner" => json.Insert(1, "\"projectionOwner\":" + JsonSerializer.Serialize(owner, JsonSerializerOptions.Web) + ","),
            "case_owner" => json.Insert(1, "\"ProjectionOwner\":" + JsonSerializer.Serialize(owner, JsonSerializerOptions.Web) + ","),
            "version" => json.Replace("\"projectionOwner\":{", "\"projectionOwner\":{\"version\":999,", StringComparison.Ordinal),
            "case_version" => json.Replace("\"projectionOwner\":{", "\"projectionOwner\":{\"Version\":999,", StringComparison.Ordinal),
            "generation" => json.Replace("\"projectionOwner\":{", "\"projectionOwner\":{\"OwnerGeneration\":\"other\",", StringComparison.Ordinal),
            "freshness" => json.Insert(1, "\"projectionVersion\":\"other\","),
            _ => json.Insert(1, "\"ProjectionVersion\":\"other\","),
        };
        byte[] rejected = Encoding.UTF8.GetBytes(invalid);
        client.SeedByteStore("tenant-1:counter:agg-1:detail", rejected);
        var request = new ProjectionOwnerProofRequest(scope.TenantId, scope.Domain, scope.AggregateId, scope.ProjectionType, "catalog");
        ProjectionOwnerProofResult proof = await store.ReadOwnerProofAsync(request, new(Store, "detail"));
        proof.Proof.ShouldBeNull();
        proof.ReasonCode.ShouldBe("delivery_reconciliation_required");
        proof.MetadataKeyReads.ShouldBe(1);
        client.TrySaveByteOperations.ShouldBeEmpty();
        client.ByteStoreValue("tenant-1:counter:agg-1:detail").ShouldBe(rejected);
        Should.Throw<ArgumentException>(() => ReadModelOwnerSelectionMetadata.Read(rejected));
    }
}
