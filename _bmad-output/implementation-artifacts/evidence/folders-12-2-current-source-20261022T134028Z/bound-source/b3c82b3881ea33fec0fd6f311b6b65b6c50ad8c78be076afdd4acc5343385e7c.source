using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Streams;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.Streams;

public class EventStepFrameTests {
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance() {
        DateTimeOffset timestamp = new(2026, 3, 27, 10, 0, 0, TimeSpan.Zero);
        List<FieldChange> changes = [new("Count", "4", "5")];

        var frame = new EventStepFrame(
            "tenant1", "orders", "order-1",
            5, "OrderUpdated", timestamp,
            "corr-1", "cause-1", "user-1",
            "{\"Count\":5}", "{\"Count\":5,\"Status\":\"Active\"}",
            changes, 10);

        frame.TenantId.ShouldBe("tenant1");
        frame.Domain.ShouldBe("orders");
        frame.AggregateId.ShouldBe("order-1");
        frame.SequenceNumber.ShouldBe(5);
        frame.EventTypeName.ShouldBe("OrderUpdated");
        frame.Timestamp.ShouldBe(timestamp);
        frame.CorrelationId.ShouldBe("corr-1");
        frame.CausationId.ShouldBe("cause-1");
        frame.UserId.ShouldBe("user-1");
        frame.EventPayloadJson.ShouldBe("{\"Count\":5}");
        frame.StateJson.ShouldBe("{\"Count\":5,\"Status\":\"Active\"}");
        frame.FieldChanges.Count.ShouldBe(1);
        frame.TotalEvents.ShouldBe(10);
    }

    [Fact]
    public void Constructor_WithNullStrings_DefaultsToEmpty() {
        var frame = new EventStepFrame(
            null!, null!, null!,
            1, null!, DateTimeOffset.MinValue,
            null!, null!, null!,
            null!, null!, null!, 1);

        frame.TenantId.ShouldBe(string.Empty);
        frame.Domain.ShouldBe(string.Empty);
        frame.AggregateId.ShouldBe(string.Empty);
        frame.EventTypeName.ShouldBe(string.Empty);
        frame.CorrelationId.ShouldBe(string.Empty);
        frame.CausationId.ShouldBe(string.Empty);
        frame.UserId.ShouldBe(string.Empty);
        frame.EventPayloadJson.ShouldBe(string.Empty);
        frame.StateJson.ShouldBe(string.Empty);
        frame.FieldChanges.ShouldBeEmpty();
    }

    [Fact]
    public void HasPrevious_AtSequenceOne_ReturnsFalse() {
        var frame = new EventStepFrame(
            "t", "d", "a", 1, "Evt", DateTimeOffset.UtcNow,
            "c", "cs", "u", "{}", "{}", [], 10);

        frame.HasPrevious.ShouldBeFalse();
    }

    [Fact]
    public void HasPrevious_AtSequenceGreaterThanOne_ReturnsTrue() {
        var frame = new EventStepFrame(
            "t", "d", "a", 5, "Evt", DateTimeOffset.UtcNow,
            "c", "cs", "u", "{}", "{}", [], 10);

        frame.HasPrevious.ShouldBeTrue();
    }

    [Fact]
    public void HasNext_AtLastEvent_ReturnsFalse() {
        var frame = new EventStepFrame(
            "t", "d", "a", 10, "Evt", DateTimeOffset.UtcNow,
            "c", "cs", "u", "{}", "{}", [], 10);

        frame.HasNext.ShouldBeFalse();
    }

    [Fact]
    public void HasNext_BeforeLastEvent_ReturnsTrue() {
        var frame = new EventStepFrame(
            "t", "d", "a", 5, "Evt", DateTimeOffset.UtcNow,
            "c", "cs", "u", "{}", "{}", [], 10);

        frame.HasNext.ShouldBeTrue();
    }

    [Fact]
    public void HasPrevious_HasNext_SingleEventStream_BothFalse() {
        var frame = new EventStepFrame(
            "t", "d", "a", 1, "Evt", DateTimeOffset.UtcNow,
            "c", "cs", "u", "{}", "{}", [], 1);

        frame.HasPrevious.ShouldBeFalse();
        frame.HasNext.ShouldBeFalse();
    }

    [Fact]
    public void ToString_RedactsPayloadAndStateAndFieldValues() {
        List<FieldChange> changes = [new("Secret", "secret-old", "secret-new")];
        var frame = new EventStepFrame(
            "tenant1", "orders", "order-1",
            5, "OrderUpdated", DateTimeOffset.UtcNow,
            "corr-1", "cause-1", "user-1",
            "{\"Password\":\"abc\"}", "{\"Secret\":\"xyz\"}",
            changes, 10);

        string str = frame.ToString();

        str.ShouldContain("tenant1");
        str.ShouldContain("OrderUpdated");
        str.ShouldContain("[REDACTED]");
        str.ShouldContain("1 changes");
        str.ShouldNotContain("Password");
        str.ShouldNotContain("abc");
        str.ShouldNotContain("xyz");
        str.ShouldNotContain("secret-old");
        str.ShouldNotContain("secret-new");
    }

    [Fact]
    public void SerializationRoundTrip_PreservesAllProperties() {
        DateTimeOffset timestamp = new(2026, 3, 27, 10, 0, 0, TimeSpan.Zero);
        List<FieldChange> changes = [new("Count", "4", "5"), new("Status", "\"Draft\"", "\"Active\"")];

        var original = new EventStepFrame(
            "tenant1", "domain1", "agg-1",
            3, "StatusChanged", timestamp,
            "corr-1", "cause-1", "user-1",
            "{\"Status\":\"Active\"}", "{\"Count\":5,\"Status\":\"Active\"}",
            changes, 7);

        string json = JsonSerializer.Serialize(original);
        EventStepFrame? deserialized = JsonSerializer.Deserialize<EventStepFrame>(json);

        _ = deserialized.ShouldNotBeNull();
        deserialized!.TenantId.ShouldBe("tenant1");
        deserialized.Domain.ShouldBe("domain1");
        deserialized.AggregateId.ShouldBe("agg-1");
        deserialized.SequenceNumber.ShouldBe(3);
        deserialized.EventTypeName.ShouldBe("StatusChanged");
        deserialized.Timestamp.ShouldBe(timestamp);
        deserialized.CorrelationId.ShouldBe("corr-1");
        deserialized.CausationId.ShouldBe("cause-1");
        deserialized.UserId.ShouldBe("user-1");
        deserialized.EventPayloadJson.ShouldBe("{\"Status\":\"Active\"}");
        deserialized.StateJson.ShouldBe("{\"Count\":5,\"Status\":\"Active\"}");
        deserialized.FieldChanges.Count.ShouldBe(2);
        deserialized.FieldChanges[0].FieldPath.ShouldBe("Count");
        deserialized.FieldChanges[1].FieldPath.ShouldBe("Status");
        deserialized.TotalEvents.ShouldBe(7);
        deserialized.HasPrevious.ShouldBeTrue();
        deserialized.HasNext.ShouldBeTrue();
    }
}
