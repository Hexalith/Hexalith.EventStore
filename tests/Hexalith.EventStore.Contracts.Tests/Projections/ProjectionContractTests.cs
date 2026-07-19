
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
            CorrelationId: "corr-001",
            MessageId: "01JZC5P8Z6M8AJ6W0KVRJHW5QX",
            UserId: "user-001",
            GlobalPosition: 104);

        string json = JsonSerializer.Serialize(dto);
        ProjectionEventDto? deserialized = JsonSerializer.Deserialize<ProjectionEventDto>(json);

        _ = deserialized.ShouldNotBeNull();
        deserialized.EventTypeName.ShouldBe("OrderCreated");
        deserialized.Payload.ShouldBe(new byte[] { 1, 2, 3, 4 });
        deserialized.SerializationFormat.ShouldBe("json");
        deserialized.SequenceNumber.ShouldBe(42);
        deserialized.Timestamp.ShouldBe(new DateTimeOffset(2026, 3, 20, 10, 0, 0, TimeSpan.Zero));
        deserialized.CorrelationId.ShouldBe("corr-001");
        deserialized.MessageId.ShouldBe("01JZC5P8Z6M8AJ6W0KVRJHW5QX");
        deserialized.UserId.ShouldBe("user-001");
        deserialized.GlobalPosition.ShouldBe(104);
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
        deserialized.GlobalPosition.ShouldBe(0);
    }

    [Fact]
    public void ProjectionEventDto_LegacyJsonWithoutGlobalPosition_UsesUnknownDefault() {
        const string json = """
            {
              "EventTypeName": "OrderCreated",
              "Payload": "AQI=",
              "SerializationFormat": "json",
              "SequenceNumber": 1,
              "Timestamp": "2026-07-19T12:00:00+00:00",
              "CorrelationId": "corr-001"
            }
            """;

        ProjectionEventDto? deserialized = JsonSerializer.Deserialize<ProjectionEventDto>(json);

        _ = deserialized.ShouldNotBeNull();
        deserialized.GlobalPosition.ShouldBe(0);
    }

    [Fact]
    public void ProjectionEventDto_NegativeGlobalPosition_IsRejected() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ProjectionEventDto(
            "OrderCreated",
            [1, 2],
            "json",
            1,
            DateTimeOffset.UnixEpoch,
            "corr-001",
            GlobalPosition: -1));

    [Fact]
    public void ProjectionEventDto_PublicCompatibility_PreservesLegacyConstructorAndDeconstruct() {
        Type[] legacyParameters = [
            typeof(string),
            typeof(byte[]),
            typeof(string),
            typeof(long),
            typeof(DateTimeOffset),
            typeof(string),
            typeof(string),
            typeof(string),
        ];
        typeof(ProjectionEventDto).GetConstructor(legacyParameters).ShouldNotBeNull();

        var value = new ProjectionEventDto(
            "OrderCreated",
            [1],
            "json",
            1,
            DateTimeOffset.UnixEpoch,
            "corr-1",
            "message-1",
            "user-1");
        (string eventType, _, _, long sequence, _, _, string? messageId, _) = value;

        eventType.ShouldBe("OrderCreated");
        sequence.ShouldBe(1);
        messageId.ShouldBe("message-1");
        value.GlobalPosition.ShouldBe(0);
    }

    [Fact]
    public void ProjectionEventDto_WithExpression_RejectsNegativeGlobalPosition() {
        var value = new ProjectionEventDto(
            "OrderCreated",
            [1],
            "json",
            1,
            DateTimeOffset.UnixEpoch,
            "corr-1");

        _ = Should.Throw<ArgumentOutOfRangeException>(() => value with { GlobalPosition = -1 });
    }

    [Fact]
    public void ProjectionEventDto_ValueEquality_IncludesPriorFieldsAndGlobalPosition() {
        var baseline = new ProjectionEventDto(
            "OrderCreated",
            [1],
            "json",
            1,
            DateTimeOffset.UnixEpoch,
            "corr-1",
            "message-1",
            "user-1",
            104);
        ProjectionEventDto equal = baseline with { };

        equal.ShouldBe(baseline);
        equal.GetHashCode().ShouldBe(baseline.GetHashCode());
        (baseline with { EventTypeName = "OrderUpdated" }).ShouldNotBe(baseline);
        ProjectionEventDto differentPosition = baseline with { GlobalPosition = 109 };
        differentPosition.ShouldNotBe(baseline);
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
