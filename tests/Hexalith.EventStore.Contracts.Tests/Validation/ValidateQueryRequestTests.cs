
using Hexalith.EventStore.Contracts.Validation;

namespace Hexalith.EventStore.Contracts.Tests.Validation;

public class ValidateQueryRequestTests {
    [Fact]
    public void Constructor_WithAllFields_CreatesInstance() {
        var request = new ValidateQueryRequest(
            Tenant: "acme",
            Domain: "orders",
            QueryType: "GetCurrentState",
            AggregateId: "order-123");

        request.Tenant.ShouldBe("acme");
        request.Domain.ShouldBe("orders");
        request.QueryType.ShouldBe("GetCurrentState");
        request.AggregateId.ShouldBe("order-123");
    }

    [Fact]
    public void Constructor_WithoutAggregateId_DefaultsToNull() {
        var request = new ValidateQueryRequest(
            Tenant: "acme",
            Domain: "orders",
            QueryType: "GetCurrentState");

        request.AggregateId.ShouldBeNull();
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual() {
        var request1 = new ValidateQueryRequest("acme", "orders", "GetCurrentState");
        var request2 = new ValidateQueryRequest("acme", "orders", "GetCurrentState");

        request2.ShouldBe(request1);
    }

    [Fact]
    public void RecordEquality_DifferentAggregateId_AreNotEqual() {
        var request1 = new ValidateQueryRequest("acme", "orders", "GetCurrentState", "order-1");
        var request2 = new ValidateQueryRequest("acme", "orders", "GetCurrentState", "order-2");

        request2.ShouldNotBe(request1);
    }
}
