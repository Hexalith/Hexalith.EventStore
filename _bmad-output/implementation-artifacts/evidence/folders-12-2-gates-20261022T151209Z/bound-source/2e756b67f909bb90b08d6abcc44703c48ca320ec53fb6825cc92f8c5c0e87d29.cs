
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

        request.Tenant.ShouldBe("acme");
        request.Domain.ShouldBe("orders");
        request.CommandType.ShouldBe("CreateOrder");
        request.AggregateId.ShouldBe("order-123");
    }

    [Fact]
    public void Constructor_WithoutAggregateId_DefaultsToNull() {
        var request = new ValidateCommandRequest(
            Tenant: "acme",
            Domain: "orders",
            CommandType: "CreateOrder");

        request.AggregateId.ShouldBeNull();
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual() {
        var request1 = new ValidateCommandRequest("acme", "orders", "CreateOrder");
        var request2 = new ValidateCommandRequest("acme", "orders", "CreateOrder");

        request2.ShouldBe(request1);
    }

    [Fact]
    public void RecordEquality_DifferentAggregateId_AreNotEqual() {
        var request1 = new ValidateCommandRequest("acme", "orders", "CreateOrder", "order-1");
        var request2 = new ValidateCommandRequest("acme", "orders", "CreateOrder", "order-2");

        request2.ShouldNotBe(request1);
    }
}
