
using Hexalith.EventStore.Contracts.Validation;

namespace Hexalith.EventStore.Contracts.Tests.Validation;

public class ValidateQueryRequestTests
{
    [Fact]
    public void Constructor_WithAllFields_CreatesInstance()
    {
        var request = new ValidateQueryRequest(
            Tenant: "acme",
            Domain: "orders",
            QueryType: "GetCurrentState",
            AggregateId: "order-123");

        Assert.Equal("acme", request.Tenant);
        Assert.Equal("orders", request.Domain);
        Assert.Equal("GetCurrentState", request.QueryType);
        Assert.Equal("order-123", request.AggregateId);
    }

    [Fact]
    public void Constructor_WithoutAggregateId_DefaultsToNull()
    {
        var request = new ValidateQueryRequest(
            Tenant: "acme",
            Domain: "orders",
            QueryType: "GetCurrentState");

        Assert.Null(request.AggregateId);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        var request1 = new ValidateQueryRequest("acme", "orders", "GetCurrentState");
        var request2 = new ValidateQueryRequest("acme", "orders", "GetCurrentState");

        Assert.Equal(request1, request2);
    }

    [Fact]
    public void RecordEquality_DifferentAggregateId_AreNotEqual()
    {
        var request1 = new ValidateQueryRequest("acme", "orders", "GetCurrentState", "order-1");
        var request2 = new ValidateQueryRequest("acme", "orders", "GetCurrentState", "order-2");

        Assert.NotEqual(request1, request2);
    }
}
