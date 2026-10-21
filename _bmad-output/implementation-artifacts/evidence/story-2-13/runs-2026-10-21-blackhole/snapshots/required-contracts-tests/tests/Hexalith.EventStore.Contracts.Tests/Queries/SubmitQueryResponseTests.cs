
using System.Text.Json;

using Hexalith.EventStore.Contracts.Queries;

namespace Hexalith.EventStore.Contracts.Tests.Queries;

public class SubmitQueryResponseTests {
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance() {
        JsonElement payload = JsonDocument.Parse("{\"count\":42}").RootElement;
        var response = new SubmitQueryResponse(
            CorrelationId: "corr-123",
            Payload: payload);

        response.CorrelationId.ShouldBe("corr-123");
        response.Payload.ValueKind.ShouldBe(JsonValueKind.Object);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual() {
        JsonElement payload = JsonDocument.Parse("42").RootElement;
        var response1 = new SubmitQueryResponse("corr-1", payload);
        var response2 = new SubmitQueryResponse("corr-1", payload);

        response2.ShouldBe(response1);
    }

    [Fact]
    public void JsonRoundTrip_CamelCaseWireContract_PreservesFailureEnvelope() {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
        JsonElement payload = JsonDocument.Parse("{\"reason\":\"denied\"}").RootElement;
        var response = new SubmitQueryResponse(
            "corr-401",
            payload,
            Success: false,
            ErrorMessage: "Forbidden");

        string json = JsonSerializer.Serialize(response, options);
        SubmitQueryResponse? roundTripped = JsonSerializer.Deserialize<SubmitQueryResponse>(json, options);

        json.ShouldContain("\"correlationId\":\"corr-401\"");
        json.ShouldContain("\"success\":false");
        _ = roundTripped.ShouldNotBeNull();
        roundTripped.CorrelationId.ShouldBe("corr-401");
        roundTripped.Success.ShouldBeFalse();
        roundTripped.ErrorMessage.ShouldBe("Forbidden");
        roundTripped.Payload.GetProperty("reason").GetString().ShouldBe("denied");
    }

    [Fact]
    public void JsonRoundTrip_WithMetadata_PreservesCacheFreshnessAndPagingFields() {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
        JsonElement payload = JsonDocument.Parse("{\"items\":[]}").RootElement;
        var servedAt = new DateTimeOffset(2026, 5, 14, 9, 30, 0, TimeSpan.Zero);
        var response = new SubmitQueryResponse(
            "corr-200",
            payload,
            Metadata: new QueryResponseMetadata(
                ETag: "etag-1",
                IsNotModified: false,
                IsStale: null,
                IsDegraded: true,
                ProjectionVersion: "projection-v1",
                ServedAt: servedAt,
                Paging: new QueryPagingMetadata(PageSize: 25, Offset: 50, NextCursor: null, TotalCount: null, HasMore: false),
                WarningCodes: [QueryWarningCodes.DegradedSearch]) {
                Provenance = QueryResponseProvenance.ProjectionBacked,
                Lifecycle = ProjectionLifecycleState.Degraded,
            });

        string json = JsonSerializer.Serialize(response, options);
        SubmitQueryResponse? roundTripped = JsonSerializer.Deserialize<SubmitQueryResponse>(json, options);

        json.ShouldContain("\"metadata\"");
        json.ShouldContain("\"etag\":\"etag-1\"");
        json.ShouldContain("\"isDegraded\":true");
        json.ShouldContain("\"pageSize\":25");
        json.ShouldContain("\"hasMore\":false");
        _ = roundTripped.ShouldNotBeNull();
        _ = roundTripped.Metadata.ShouldNotBeNull();
        roundTripped.Metadata.ETag.ShouldBe("etag-1");
        roundTripped.Metadata.Provenance.ShouldBe(QueryResponseProvenance.ProjectionBacked);
        roundTripped.Metadata.Lifecycle.ShouldBe(ProjectionLifecycleState.Degraded);
        roundTripped.Metadata.IsDegraded.ShouldBe(true);
        roundTripped.Metadata.ServedAt.ShouldBe(servedAt);
        _ = roundTripped.Metadata.Paging.ShouldNotBeNull();
        roundTripped.Metadata.Paging.Offset.ShouldBe(50);
        roundTripped.Metadata.Paging.HasMore.ShouldBe(false);
        _ = roundTripped.Metadata.WarningCodes.ShouldNotBeNull();
        roundTripped.Metadata.WarningCodes.ShouldContain(QueryWarningCodes.DegradedSearch);
    }

    [Theory]
    [InlineData(ProjectionLifecycleState.Rebuilding)]
    [InlineData(ProjectionLifecycleState.Degraded)]
    [InlineData(ProjectionLifecycleState.Unavailable)]
    [InlineData(ProjectionLifecycleState.LocalOnly)]
    public void JsonRoundTrip_OperationalLifecycle_PreservesExactValue(
        ProjectionLifecycleState lifecycle) {
        var response = new SubmitQueryResponse(
            "corr-operational",
            JsonSerializer.SerializeToElement(new { value = 42 }),
            Metadata: new QueryResponseMetadata {
                Provenance = QueryResponseProvenance.ProjectionBacked,
                Lifecycle = lifecycle,
            });
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web);

        string json = JsonSerializer.Serialize(response, options);
        SubmitQueryResponse restored = JsonSerializer
            .Deserialize<SubmitQueryResponse>(json, options)
            .ShouldNotBeNull();

        restored.Metadata.ShouldNotBeNull().Lifecycle.ShouldBe(lifecycle);
    }
}
