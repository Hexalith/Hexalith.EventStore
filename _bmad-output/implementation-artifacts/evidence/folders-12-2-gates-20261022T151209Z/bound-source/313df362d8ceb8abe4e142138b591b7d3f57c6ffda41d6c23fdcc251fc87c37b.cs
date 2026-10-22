using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Streams;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.Streams;

public class AggregateBlameViewTests {
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance() {
        var fields = new List<FieldProvenance>
        {
            new("Count", "5", "4", 10, DateTimeOffset.UtcNow, "CounterIncremented", "corr-1", "user-1"),
        };

        var view = new AggregateBlameView(
            "tenant1", "orders", "order-1",
            10, DateTimeOffset.UtcNow, fields,
            false, false);

        view.TenantId.ShouldBe("tenant1");
        view.Domain.ShouldBe("orders");
        view.AggregateId.ShouldBe("order-1");
        view.AtSequence.ShouldBe(10);
        view.Fields.Count.ShouldBe(1);
        view.IsTruncated.ShouldBeFalse();
        view.IsFieldsTruncated.ShouldBeFalse();
    }

    [Fact]
    public void Constructor_WithNullStrings_DefaultsToEmpty() {
        var view = new AggregateBlameView(
            null!, null!, null!,
            0, DateTimeOffset.MinValue, null!,
            false, false);

        view.TenantId.ShouldBe(string.Empty);
        view.Domain.ShouldBe(string.Empty);
        view.AggregateId.ShouldBe(string.Empty);
        view.Fields.ShouldBeEmpty();
    }

    [Fact]
    public void ToString_RedactsFieldValues() {
        var fields = new List<FieldProvenance>
        {
            new("Secret", "secret-value", "old-secret", 1, DateTimeOffset.UtcNow, "Evt", "c", "u"),
        };

        var view = new AggregateBlameView(
            "tenant1", "orders", "order-1",
            5, DateTimeOffset.UtcNow, fields,
            false, false);

        string result = view.ToString();

        result.ShouldContain("tenant1");
        result.ShouldContain("1 fields");
        result.ShouldNotContain("secret-value");
        result.ShouldNotContain("old-secret");
    }

    [Fact]
    public void SerializationRoundTrip_WithFieldProvenanceList_PreservesAll() {
        DateTimeOffset timestamp = new(2026, 3, 27, 10, 0, 0, TimeSpan.Zero);
        var fields = new List<FieldProvenance>
        {
            new("Count", "5", "4", 10, timestamp, "CounterIncremented", "corr-1", "user-1"),
            new("Status", "\"active\"", "\"idle\"", 8, timestamp, "StatusChanged", "corr-2", "user-2"),
        };

        var original = new AggregateBlameView(
            "tenant1", "domain1", "agg-1",
            10, timestamp, fields, true, false);

        string json = JsonSerializer.Serialize(original);
        AggregateBlameView? deserialized = JsonSerializer.Deserialize<AggregateBlameView>(json);

        _ = deserialized.ShouldNotBeNull();
        deserialized!.TenantId.ShouldBe("tenant1");
        deserialized.Domain.ShouldBe("domain1");
        deserialized.AggregateId.ShouldBe("agg-1");
        deserialized.AtSequence.ShouldBe(10);
        deserialized.IsTruncated.ShouldBeTrue();
        deserialized.IsFieldsTruncated.ShouldBeFalse();
        deserialized.Fields.Count.ShouldBe(2);
        deserialized.Fields[0].FieldPath.ShouldBe("Count");
        deserialized.Fields[1].FieldPath.ShouldBe("Status");
    }

    [Fact]
    public void EmptyFields_WhenNoEvents_ReturnsEmptyList() {
        var view = new AggregateBlameView(
            "t1", "d1", "a1", 0,
            DateTimeOffset.MinValue, [], false, false);

        view.Fields.ShouldBeEmpty();
    }
}
