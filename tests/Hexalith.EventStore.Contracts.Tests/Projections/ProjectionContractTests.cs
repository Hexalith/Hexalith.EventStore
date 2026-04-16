
using System.Text.Json;

using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.Contracts.Tests.Projections;

public class ProjectionContractTests {
    [Fact]
    public void ProjectionEventDto_RoundTrips_Json() {
        var dto = new ProjectionEventDto(
            EventTypeName: "OrderCreated",
            Payload: [1, 2, 3, 4],
            SerializationFormat: "json",
            SequenceNumber: 42,
            Timestamp: new DateTimeOffset(2026, 3, 20, 10, 0, 0, TimeSpan.Zero),
            CorrelationId: "corr-001");

        string json = JsonSerializer.Serialize(dto);
        ProjectionEventDto? deserialized = JsonSerializer.Deserialize<ProjectionEventDto>(json);

        _ = deserialized.ShouldNotBeNull();
        deserialized.EventTypeName.ShouldBe("OrderCreated");
        deserialized.Payload.ShouldBe(new byte[] { 1, 2, 3, 4 });
        deserialized.SerializationFormat.ShouldBe("json");
        deserialized.SequenceNumber.ShouldBe(42);
        deserialized.Timestamp.ShouldBe(new DateTimeOffset(2026, 3, 20, 10, 0, 0, TimeSpan.Zero));
        deserialized.CorrelationId.ShouldBe("corr-001");
    }

    [Fact]
    public void ProjectionEventDto_EmptyPayload_RoundTripsJson() {
        var dto = new ProjectionEventDto(
            EventTypeName: "MarkerEvent",
            Payload: [],
            SerializationFormat: "json",
            SequenceNumber: 1,
            Timestamp: DateTimeOffset.UtcNow,
            CorrelationId: "corr-empty");

        string json = JsonSerializer.Serialize(dto);
        ProjectionEventDto? deserialized = JsonSerializer.Deserialize<ProjectionEventDto>(json);

        _ = deserialized.ShouldNotBeNull();
        deserialized.Payload.ShouldBeEmpty();
    }

    [Fact]
    public void ProjectionRequest_RoundTrips_Json() {
        ProjectionEventDto[] events = new[] {
            new ProjectionEventDto("OrderCreated", [1, 2], "json", 1, DateTimeOffset.UtcNow, "corr-001"),
            new ProjectionEventDto("OrderUpdated", [3, 4], "json", 2, DateTimeOffset.UtcNow, "corr-001"),
        };
        var request = new ProjectionRequest(
            TenantId: "acme",
            Domain: "orders",
            AggregateId: "order-123",
            Events: events);

        string json = JsonSerializer.Serialize(request);
        ProjectionRequest? deserialized = JsonSerializer.Deserialize<ProjectionRequest>(json);

        _ = deserialized.ShouldNotBeNull();
        deserialized.TenantId.ShouldBe("acme");
        deserialized.Domain.ShouldBe("orders");
        deserialized.AggregateId.ShouldBe("order-123");
        deserialized.Events.Length.ShouldBe(2);
        deserialized.Events[0].EventTypeName.ShouldBe("OrderCreated");
        deserialized.Events[1].EventTypeName.ShouldBe("OrderUpdated");
    }

    [Fact]
    public void ProjectionResponse_RoundTrips_Json() {
        using var stateObj = JsonDocument.Parse("""{"count": 42, "name": "test"}""");
        var response = new ProjectionResponse(
            ProjectionType: "counter-summary",
            State: stateObj.RootElement.Clone());

        string json = JsonSerializer.Serialize(response);
        ProjectionResponse? deserialized = JsonSerializer.Deserialize<ProjectionResponse>(json);

        _ = deserialized.ShouldNotBeNull();
        deserialized.ProjectionType.ShouldBe("counter-summary");
        deserialized.State.GetProperty("count").GetInt32().ShouldBe(42);
        deserialized.State.GetProperty("name").GetString().ShouldBe("test");
    }
}
