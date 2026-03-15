
using Hexalith.EventStore.Contracts.Validation;

namespace Hexalith.EventStore.Contracts.Tests.Validation;

public class ValidateCommandRequestTests {
    [Fact]
    public void Constructor_WithAllFields_CreatesInstance() {
        var request = new ValidateCommandRequest(
            Tenant: "acme",
            Domain: "orders",
            CommandType: "CreateOrder",
            AggregateId: "order-123");

        Assert.Equal("acme", request.Tenant);
        Assert.Equal("orders", request.Domain);
        Assert.Equal("CreateOrder", request.CommandType);
        Assert.Equal("order-123", request.AggregateId);
    }

    [Fact]
    public void Constructor_WithoutAggregateId_DefaultsToNull() {
        var request = new ValidateCommandRequest(
            Tenant: "acme",
            Domain: "orders",
            CommandType: "CreateOrder");

        Assert.Null(request.AggregateId);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual() {
        var request1 = new ValidateCommandRequest("acme", "orders", "CreateOrder");
        var request2 = new ValidateCommandRequest("acme", "orders", "CreateOrder");

        Assert.Equal(request1, request2);
    }

    [Fact]
    public void RecordEquality_DifferentAggregateId_AreNotEqual() {
        var request1 = new ValidateCommandRequest("acme", "orders", "CreateOrder", "order-1");
        var request2 = new ValidateCommandRequest("acme", "orders", "CreateOrder", "order-2");

        Assert.NotEqual(request1, request2);
    }
}
