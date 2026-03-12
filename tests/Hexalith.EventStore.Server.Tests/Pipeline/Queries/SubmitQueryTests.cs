
using System.Text.Json;

using Hexalith.EventStore.Server.Pipeline.Queries;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Pipeline.Queries;

public class SubmitQueryTests {
    [Fact]
    public void Constructor_ValidFields_SetsAllProperties() {
        byte[] payload = [0x01, 0x02];
        var sut = new SubmitQuery(
            Tenant: "test-tenant",
            Domain: "orders",
            AggregateId: "order-1",
            QueryType: "GetOrderStatus",
            Payload: payload,
            CorrelationId: "corr-1",
            UserId: "user-1");

        sut.Tenant.ShouldBe("test-tenant");
        sut.Domain.ShouldBe("orders");
        sut.AggregateId.ShouldBe("order-1");
        sut.QueryType.ShouldBe("GetOrderStatus");
        sut.Payload.ShouldBe(payload);
        sut.CorrelationId.ShouldBe("corr-1");
        sut.UserId.ShouldBe("user-1");
    }

    [Fact]
    public void Constructor_EmptyPayload_IsValid() {
        var sut = new SubmitQuery("t", "d", "a", "q", [], "c", "u");
        sut.Payload.ShouldBeEmpty();
    }
}

public class SubmitQueryResultTests {
    [Fact]
    public void Constructor_SetsProperties() {
        JsonElement payload = JsonDocument.Parse("{\"count\":42}").RootElement;
        var sut = new SubmitQueryResult("corr-1", payload);

        sut.CorrelationId.ShouldBe("corr-1");
        sut.Payload.GetProperty("count").GetInt32().ShouldBe(42);
    }
}
