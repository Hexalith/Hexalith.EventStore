
using System.Text.Json;

using Hexalith.EventStore.Contracts.Queries;

namespace Hexalith.EventStore.Contracts.Tests.Queries;

public class SubmitQueryRequestTests
{
    [Fact]
    public void Constructor_WithAllFields_CreatesInstance()
    {
        JsonElement payload = JsonDocument.Parse("{\"page\":1}").RootElement;
        var request = new SubmitQueryRequest(
            Tenant: "acme",
            Domain: "orders",
            AggregateId: "order-123",
            QueryType: "GetCurrentState",
            Payload: payload);

        Assert.Equal("acme", request.Tenant);
        Assert.Equal("orders", request.Domain);
        Assert.Equal("order-123", request.AggregateId);
        Assert.Equal("GetCurrentState", request.QueryType);
        Assert.NotNull(request.Payload);
        Assert.Equal(JsonValueKind.Object, request.Payload.Value.ValueKind);
    }

    [Fact]
    public void Constructor_WithoutPayload_DefaultsToNull()
    {
        var request = new SubmitQueryRequest(
            Tenant: "acme",
            Domain: "orders",
            AggregateId: "order-123",
            QueryType: "GetCurrentState");

        Assert.Null(request.Payload);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        var request1 = new SubmitQueryRequest("acme", "orders", "order-123", "GetCurrentState");
        var request2 = new SubmitQueryRequest("acme", "orders", "order-123", "GetCurrentState");

        Assert.Equal(request1, request2);
    }

    [Fact]
    public void RecordEquality_DifferentValues_AreNotEqual()
    {
        var request1 = new SubmitQueryRequest("acme", "orders", "order-123", "GetCurrentState");
        var request2 = new SubmitQueryRequest("acme", "orders", "order-456", "GetCurrentState");

        Assert.NotEqual(request1, request2);
    }
}
