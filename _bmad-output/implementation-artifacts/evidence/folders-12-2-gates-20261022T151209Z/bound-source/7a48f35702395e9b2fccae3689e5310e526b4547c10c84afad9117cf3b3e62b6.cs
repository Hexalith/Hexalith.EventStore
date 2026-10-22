using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Streams;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.Streams;

public class BisectResultTests {
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance() {
        DateTimeOffset timestamp = new(2026, 3, 27, 10, 0, 0, TimeSpan.Zero);
        List<FieldChange> changes = [new("Count", "4", "5")];
        List<string> watchedFields = ["Count", "Status"];
        List<BisectStep> steps = [new(1, 50, "good", 0), new(2, 75, "bad", 1)];

        var result = new BisectResult(
            "tenant1", "orders", "order-1",
            0, 75, timestamp,
            "OrderUpdated", "corr-1", "user-1",
            changes, watchedFields, steps, 2, false);

        result.TenantId.ShouldBe("tenant1");
        result.Domain.ShouldBe("orders");
        result.AggregateId.ShouldBe("order-1");
        result.GoodSequence.ShouldBe(0);
        result.DivergentSequence.ShouldBe(75);
        result.DivergentEventType.ShouldBe("OrderUpdated");
        result.DivergentCorrelationId.ShouldBe("corr-1");
        result.DivergentUserId.ShouldBe("user-1");
        result.DivergentFieldChanges.Count.ShouldBe(1);
        result.WatchedFieldPaths.Count.ShouldBe(2);
        result.Steps.Count.ShouldBe(2);
        result.TotalSteps.ShouldBe(2);
        result.IsTruncated.ShouldBeFalse();
    }

    [Fact]
    public void Constructor_WithNullStrings_DefaultsToEmpty() {
        var result = new BisectResult(
            null!, null!, null!,
            0, 1, DateTimeOffset.MinValue,
            null!, null!, null!,
            null!, null!, null!, 0, false);

        result.TenantId.ShouldBe(string.Empty);
        result.Domain.ShouldBe(string.Empty);
        result.AggregateId.ShouldBe(string.Empty);
        result.DivergentEventType.ShouldBe(string.Empty);
        result.DivergentCorrelationId.ShouldBe(string.Empty);
        result.DivergentUserId.ShouldBe(string.Empty);
        result.DivergentFieldChanges.ShouldBeEmpty();
        result.WatchedFieldPaths.ShouldBeEmpty();
        result.Steps.ShouldBeEmpty();
    }

    [Fact]
    public void ToString_RedactsFieldValues() {
        List<FieldChange> changes = [new("Secret", "secret-old", "secret-new")];
        var result = new BisectResult(
            "tenant1", "orders", "order-1",
            0, 50, DateTimeOffset.UtcNow,
            "Evt", "corr", "user",
            changes, ["Secret"], [new(1, 25, "bad", 1)], 1, false);

        string str = result.ToString();

        str.ShouldContain("tenant1");
        str.ShouldContain("1 steps");
        str.ShouldNotContain("secret-old");
        str.ShouldNotContain("secret-new");
    }

    [Fact]
    public void SerializationRoundTrip_WithCollections_PreservesAll() {
        DateTimeOffset timestamp = new(2026, 3, 27, 10, 0, 0, TimeSpan.Zero);
        List<FieldChange> changes = [new("Count", "4", "5")];
        List<BisectStep> steps =
        [
            new(1, 512, "good", 0),
            new(2, 768, "bad", 1),
        ];

        var original = new BisectResult(
            "tenant1", "domain1", "agg-1",
            0, 768, timestamp,
            "EventType", "corr-1", "user-1",
            changes, ["Count"], steps, 2, true);

        string json = JsonSerializer.Serialize(original);
        BisectResult? deserialized = JsonSerializer.Deserialize<BisectResult>(json);

        _ = deserialized.ShouldNotBeNull();
        deserialized!.TenantId.ShouldBe("tenant1");
        deserialized.Domain.ShouldBe("domain1");
        deserialized.AggregateId.ShouldBe("agg-1");
        deserialized.GoodSequence.ShouldBe(0);
        deserialized.DivergentSequence.ShouldBe(768);
        deserialized.DivergentEventType.ShouldBe("EventType");
        deserialized.IsTruncated.ShouldBeTrue();
        deserialized.Steps.Count.ShouldBe(2);
        deserialized.Steps[0].Verdict.ShouldBe("good");
        deserialized.Steps[1].Verdict.ShouldBe("bad");
        deserialized.DivergentFieldChanges.Count.ShouldBe(1);
        deserialized.DivergentFieldChanges[0].FieldPath.ShouldBe("Count");
        deserialized.WatchedFieldPaths.Count.ShouldBe(1);
    }

    [Fact]
    public void EmptyDivergentFieldChanges_IndicatesNoDivergence() {
        var result = new BisectResult(
            "t1", "d1", "a1",
            0, 100, DateTimeOffset.MinValue,
            string.Empty, string.Empty, string.Empty,
            [], [], [], 0, false);

        result.DivergentFieldChanges.ShouldBeEmpty();
    }
}
