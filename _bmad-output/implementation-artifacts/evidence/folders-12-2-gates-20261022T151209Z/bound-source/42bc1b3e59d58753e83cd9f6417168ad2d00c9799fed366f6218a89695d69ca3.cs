using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Streams;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.Streams;

public class TraceMapEventTests {
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance() {
        DateTimeOffset ts = new(2026, 3, 27, 10, 0, 0, TimeSpan.Zero);

        var evt = new TraceMapEvent(5, "CounterIncremented", ts, "caus-123", false);

        evt.SequenceNumber.ShouldBe(5);
        evt.EventTypeName.ShouldBe("CounterIncremented");
        evt.Timestamp.ShouldBe(ts);
        evt.CausationId.ShouldBe("caus-123");
        evt.IsRejection.ShouldBeFalse();
    }

    [Fact]
    public void Constructor_WithNullStrings_DefaultsToEmpty() {
        var evt = new TraceMapEvent(1, null!, DateTimeOffset.MinValue, null, false);

        evt.EventTypeName.ShouldBe(string.Empty);
        evt.CausationId.ShouldBeNull();
    }

    [Fact]
    public void ToString_ContainsAllFields() {
        var evt = new TraceMapEvent(3, "OrderPlaced", DateTimeOffset.UtcNow, "c-1", false);

        string result = evt.ToString();

        result.ShouldContain("SequenceNumber = 3");
        result.ShouldContain("OrderPlaced");
        result.ShouldContain("c-1");
    }

    [Fact]
    public void SerializationRoundTrip_PreservesAll() {
        DateTimeOffset ts = new(2026, 3, 27, 10, 0, 0, TimeSpan.Zero);
        var original = new TraceMapEvent(10, "Evt", ts, "caus-1", true);

        string json = JsonSerializer.Serialize(original);
        TraceMapEvent? deserialized = JsonSerializer.Deserialize<TraceMapEvent>(json);

        _ = deserialized.ShouldNotBeNull();
        deserialized!.SequenceNumber.ShouldBe(10);
        deserialized.EventTypeName.ShouldBe("Evt");
        deserialized.CausationId.ShouldBe("caus-1");
        deserialized.IsRejection.ShouldBeTrue();
    }
}
