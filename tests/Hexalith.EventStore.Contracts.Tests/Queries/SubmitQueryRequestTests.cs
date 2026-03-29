
using System.Text.Json;

using Hexalith.EventStore.Contracts.Queries;

namespace Hexalith.EventStore.Contracts.Tests.Queries;

public class SubmitQueryRequestTests {
    [Fact]
    public void Constructor_WithAllFields_CreatesInstance() {
        JsonElement payload = JsonDocument.Parse("{\"page\":1}").RootElement;
        var request = new SubmitQueryRequest(
            Tenant: "acme",
            Domain: "orders",
            AggregateId: "order-123",
            QueryType: "GetCurrentState",
            Payload: payload,
            EntityId: "entity-1");

        request.Tenant.ShouldBe("acme");
        request.Domain.ShouldBe("orders");
        request.AggregateId.ShouldBe("order-123");
        request.QueryType.ShouldBe("GetCurrentState");
        _ = request.Payload.ShouldNotBeNull();
        request.Payload.Value.ValueKind.ShouldBe(JsonValueKind.Object);
        request.EntityId.ShouldBe("entity-1");
    }

    [Fact]
    public void Constructor_WithoutPayloadOrEntityId_DefaultsToNull() {
        var request = new SubmitQueryRequest(
            Tenant: "acme",
            Domain: "orders",
            AggregateId: "order-123",
            QueryType: "GetCurrentState");

        request.Payload.ShouldBeNull();
        request.EntityId.ShouldBeNull();
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual() {
        var request1 = new SubmitQueryRequest("acme", "orders", "order-123", "GetCurrentState");
        var request2 = new SubmitQueryRequest("acme", "orders", "order-123", "GetCurrentState");

        request2.ShouldBe(request1);
    }

    [Fact]
    public void RecordEquality_DifferentValues_AreNotEqual() {
        var request1 = new SubmitQueryRequest("acme", "orders", "order-123", "GetCurrentState");
        var request2 = new SubmitQueryRequest("acme", "orders", "order-456", "GetCurrentState");

        request2.ShouldNotBe(request1);
    }
}
