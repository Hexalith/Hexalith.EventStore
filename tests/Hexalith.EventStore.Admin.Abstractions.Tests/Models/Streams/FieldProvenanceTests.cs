using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Streams;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.Streams;

public class FieldProvenanceTests {
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance() {
        var prov = new FieldProvenance(
            "Count", "5", "4", 10,
            DateTimeOffset.UtcNow, "CounterIncremented",
            "corr-1", "user-1");

        prov.FieldPath.ShouldBe("Count");
        prov.CurrentValue.ShouldBe("5");
        prov.PreviousValue.ShouldBe("4");
        prov.LastChangedAtSequence.ShouldBe(10);
        prov.LastChangedByEventType.ShouldBe("CounterIncremented");
        prov.LastChangedByCorrelationId.ShouldBe("corr-1");
        prov.LastChangedByUserId.ShouldBe("user-1");
    }

    [Fact]
    public void Constructor_WithNullStrings_DefaultsToEmpty() {
        var prov = new FieldProvenance(
            null!, null!, null!, 1,
            DateTimeOffset.UtcNow, null!, null!, null!);

        prov.FieldPath.ShouldBe(string.Empty);
        prov.CurrentValue.ShouldBe(string.Empty);
        prov.PreviousValue.ShouldBe(string.Empty);
        prov.LastChangedByEventType.ShouldBe(string.Empty);
        prov.LastChangedByCorrelationId.ShouldBe(string.Empty);
        prov.LastChangedByUserId.ShouldBe(string.Empty);
    }

    [Fact]
    public void ToString_RedactsCurrentAndPreviousValues() {
        var prov = new FieldProvenance(
            "Count", "secret-current", "secret-previous", 5,
            DateTimeOffset.UtcNow, "CounterIncremented", "corr-1", "user-1");

        string result = prov.ToString();

        result.ShouldContain("[REDACTED]");
        result.ShouldContain("Count");
        result.ShouldNotContain("secret-current");
        result.ShouldNotContain("secret-previous");
    }

    [Fact]
    public void SerializationRoundTrip_PreservesAllFields() {
        DateTimeOffset timestamp = new(2026, 3, 27, 10, 0, 0, TimeSpan.Zero);
        var original = new FieldProvenance(
            "Status.IsActive", "true", "false", 42,
            timestamp, "StatusUpdated", "corr-abc", "admin-user");

        string json = JsonSerializer.Serialize(original);
        FieldProvenance? deserialized = JsonSerializer.Deserialize<FieldProvenance>(json);

        _ = deserialized.ShouldNotBeNull();
        deserialized!.FieldPath.ShouldBe("Status.IsActive");
        deserialized.CurrentValue.ShouldBe("true");
        deserialized.PreviousValue.ShouldBe("false");
        deserialized.LastChangedAtSequence.ShouldBe(42);
        deserialized.LastChangedAtTimestamp.ShouldBe(timestamp);
        deserialized.LastChangedByEventType.ShouldBe("StatusUpdated");
        deserialized.LastChangedByCorrelationId.ShouldBe("corr-abc");
        deserialized.LastChangedByUserId.ShouldBe("admin-user");
    }
}
