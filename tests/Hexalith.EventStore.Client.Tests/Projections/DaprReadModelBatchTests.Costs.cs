using Hexalith.EventStore.Client.Projections;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

public partial class DaprReadModelBatchTests
{
    [Fact]
    public async Task PreparedManifest_ReportsPlannedOperationsWithoutClaimingCommittedLogicalModelMutations()
    {
        using var client = new RecordingDaprClient();
        DaprReadModelStore store = CreateStore(client);
        ReadModelBatch batch = WriteBatch();
        using ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope();

        (await store.StageAsync(batch)).Status.ShouldBe(ReadModelBatchStagingStatus.Prepared);

        cost[ProjectionDeliveryCost.ManifestOperations].ShouldBe(batch.Operations.Count);
        cost[ProjectionDeliveryCost.ModelMutations].ShouldBe(0);
        cost[ProjectionDeliveryCost.MutationAttempts].ShouldBeGreaterThan(0); // private prepared writes remain charged
        (await store.CommitAsync(batch)).Status.ShouldBe(ReadModelBatchStagingStatus.Committed);
        cost[ProjectionDeliveryCost.ModelMutations].ShouldBe(batch.Operations.Count);
        (await store.CommitAsync(batch)).Status.ShouldBe(ReadModelBatchStagingStatus.Committed);
        cost[ProjectionDeliveryCost.ModelMutations].ShouldBe(batch.Operations.Count);
    }
}
