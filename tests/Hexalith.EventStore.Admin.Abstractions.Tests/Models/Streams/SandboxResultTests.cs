using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Streams;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.Streams;

public class SandboxResultTests
{
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance()
    {
        List<SandboxEvent> events =
        [
            new(0, "CounterIncremented", "{\"Amount\":5}", false),
            new(1, "CounterIncremented", "{\"Amount\":3}", false),
        ];
        List<FieldChange> changes = [new("Count", "0", "8")];

        var result = new SandboxResult(
            "tenant1",
            "counters",
            "counter-1",
            5,
            "IncrementCounter",
            "accepted",
            events,
            "{\"Count\":8}",
            changes,
            null,
            42);

        result.TenantId.ShouldBe("tenant1");
        result.Domain.ShouldBe("counters");
        result.AggregateId.ShouldBe("counter-1");
        result.AtSequence.ShouldBe(5);
        result.CommandType.ShouldBe("IncrementCounter");
        result.Outcome.ShouldBe("accepted");
        result.ProducedEvents.Count.ShouldBe(2);
        result.ResultingStateJson.ShouldBe("{\"Count\":8}");
        result.StateChanges.Count.ShouldBe(1);
        result.ErrorMessage.ShouldBeNull();
        result.ExecutionTimeMs.ShouldBe(42);
    }

    [Fact]
    public void Constructor_WithNullStrings_DefaultsToEmpty()
    {
        var result = new SandboxResult(
            null!,
            null!,
            null!,
            0,
            null!,
            null!,
            null!,
            null!,
            null!,
            null,
            0);

        result.TenantId.ShouldBe(string.Empty);
        result.Domain.ShouldBe(string.Empty);
        result.AggregateId.ShouldBe(string.Empty);
        result.CommandType.ShouldBe(string.Empty);
        result.Outcome.ShouldBe(string.Empty);
        result.ProducedEvents.ShouldBeEmpty();
        result.ResultingStateJson.ShouldBe(string.Empty);
        result.StateChanges.ShouldBeEmpty();
        result.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public void ToString_RedactsResultingStateJson()
    {
        List<SandboxEvent> events = [new(0, "CounterIncremented", "{\"Amount\":5}", false)];
        List<FieldChange> changes = [new("Count", "0", "5")];

        var result = new SandboxResult(
            "tenant1",
            "counters",
            "counter-1",
            3,
            "IncrementCounter",
            "accepted",
            events,
            "{\"Secret\":\"highly-confidential-state\"}",
            changes,
            null,
            10);

        string str = result.ToString();

        str.ShouldContain("[REDACTED]");
        str.ShouldNotContain("highly-confidential-state");
        str.ShouldNotContain("Secret");
    }

    [Fact]
    public void ToString_ContainsOutcomeAndCounts()
    {
        List<SandboxEvent> events =
        [
            new(0, "CounterIncremented", "{}", false),
            new(1, "CounterIncremented", "{}", false),
        ];
        List<FieldChange> changes = [new("Count", "0", "10"), new("Status", "\"idle\"", "\"active\"")];

        var result = new SandboxResult(
            "tenant1",
            "counters",
            "counter-1",
            5,
            "IncrementCounter",
            "accepted",
            events,
            "{}",
            changes,
            null,
            25);

        string str = result.ToString();

        str.ShouldContain("accepted");
        str.ShouldContain("2 events");
        str.ShouldContain("2 changes");
        str.ShouldContain("tenant1");
        str.ShouldContain("IncrementCounter");
    }

    [Fact]
    public void SerializationRoundTrip_PreservesAllProperties()
    {
        List<SandboxEvent> events =
        [
            new(0, "CounterIncremented", "{\"Amount\":5}", false),
            new(1, "CounterRejected", "{\"Reason\":\"Limit\"}", true),
        ];
        List<FieldChange> changes = [new("Count", "0", "5")];

        var original = new SandboxResult(
            "tenant1",
            "counters",
            "counter-1",
            3,
            "IncrementCounter",
            "accepted",
            events,
            "{\"Count\":5}",
            changes,
            "Some error",
            99);

        string json = JsonSerializer.Serialize(original);
        SandboxResult? deserialized = JsonSerializer.Deserialize<SandboxResult>(json);

        deserialized.ShouldNotBeNull();
        deserialized!.TenantId.ShouldBe("tenant1");
        deserialized.Domain.ShouldBe("counters");
        deserialized.AggregateId.ShouldBe("counter-1");
        deserialized.AtSequence.ShouldBe(3);
        deserialized.CommandType.ShouldBe("IncrementCounter");
        deserialized.Outcome.ShouldBe("accepted");
        deserialized.ProducedEvents.Count.ShouldBe(2);
        deserialized.ProducedEvents[0].EventTypeName.ShouldBe("CounterIncremented");
        deserialized.ProducedEvents[1].EventTypeName.ShouldBe("CounterRejected");
        deserialized.ProducedEvents[1].IsRejection.ShouldBeTrue();
        deserialized.ResultingStateJson.ShouldBe("{\"Count\":5}");
        deserialized.StateChanges.Count.ShouldBe(1);
        deserialized.StateChanges[0].FieldPath.ShouldBe("Count");
        deserialized.ErrorMessage.ShouldBe("Some error");
        deserialized.ExecutionTimeMs.ShouldBe(99);
    }
}
