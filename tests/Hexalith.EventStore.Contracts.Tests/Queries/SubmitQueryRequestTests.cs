
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

    [Fact]
    public void JsonRoundTrip_UsesCamelCaseGatewayContract_ForAllPublicFields() {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
        JsonElement payload = JsonSerializer.SerializeToElement(new { page = 1 }, options);
        var request = new SubmitQueryRequest(
            Tenant: "tenant-a",
            Domain: "party",
            AggregateId: "party-1",
            QueryType: "GetParty",
            ProjectionType: "party-summary",
            Payload: payload,
            EntityId: "party-entity-1",
            ProjectionActorType: "PartyProjectionActor");

        string json = JsonSerializer.Serialize(request, options);
        SubmitQueryRequest? roundTripped = JsonSerializer.Deserialize<SubmitQueryRequest>(json, options);

        json.ShouldContain("\"tenant\":\"tenant-a\"");
        json.ShouldContain("\"aggregateId\":\"party-1\"");
        json.ShouldContain("\"queryType\":\"GetParty\"");
        json.ShouldContain("\"projectionType\":\"party-summary\"");
        json.ShouldContain("\"entityId\":\"party-entity-1\"");
        json.ShouldContain("\"projectionActorType\":\"PartyProjectionActor\"");
        roundTripped.ShouldNotBeNull();
        roundTripped.Payload.ShouldNotBeNull();
        roundTripped.Payload.Value.GetProperty("page").GetInt32().ShouldBe(1);
        roundTripped.ProjectionActorType.ShouldBe("PartyProjectionActor");
    }
}
