using System.Text.Json;

using Hexalith.EventStore.Contracts.Queries;

namespace Hexalith.EventStore.Contracts.Tests.Queries;

public class QueryResultLifecycleCarrierTests {
    [Theory]
    [InlineData(ProjectionLifecycleState.Rebuilding)]
    [InlineData(ProjectionLifecycleState.Degraded)]
    [InlineData(ProjectionLifecycleState.Unavailable)]
    [InlineData(ProjectionLifecycleState.LocalOnly)]
    public void SystemTextJsonRoundTrip_OperationalLifecycle_PreservesExactValue(
        ProjectionLifecycleState lifecycle) {
        QueryResult original = QueryResult.FromPayload(
            JsonSerializer.SerializeToElement(new { value = 42 }),
            "counter",
            new QueryResponseMetadata {
                Provenance = QueryResponseProvenance.ProjectionBacked,
                Lifecycle = lifecycle,
            });

        string json = JsonSerializer.Serialize(original);
        QueryResult restored = JsonSerializer.Deserialize<QueryResult>(json).ShouldNotBeNull();

        restored.Metadata.ShouldNotBeNull().Lifecycle.ShouldBe(lifecycle);
    }
}
