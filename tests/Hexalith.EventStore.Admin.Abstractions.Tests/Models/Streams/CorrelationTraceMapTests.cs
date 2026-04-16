using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Streams;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.Streams;

public class CorrelationTraceMapTests {
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance() {
        DateTimeOffset received = new(2026, 3, 27, 10, 0, 0, TimeSpan.Zero);
        DateTimeOffset completed = received.AddMilliseconds(250);
        var events = new List<TraceMapEvent>
        {
            new(1, "CounterIncremented", received.AddMilliseconds(100), "caus-1", false),
        };
        var projections = new List<TraceMapProjection>
        {
            new("CounterProjection", "processed", 1),
        };

        var trace = new CorrelationTraceMap(
            "corr-123", "tenant1", "counter", "counter-1",
            "IncrementCounter", "Completed", "user-1",
            received, completed, 250,
            events, projections,
            null, null, "https://trace.example.com?correlationId=corr-123",
            42, false, null);

        trace.CorrelationId.ShouldBe("corr-123");
        trace.TenantId.ShouldBe("tenant1");
        trace.Domain.ShouldBe("counter");
        trace.AggregateId.ShouldBe("counter-1");
        trace.CommandType.ShouldBe("IncrementCounter");
        trace.CommandStatus.ShouldBe("Completed");
        trace.UserId.ShouldBe("user-1");
        trace.CommandReceivedAt.ShouldBe(received);
        trace.CommandCompletedAt.ShouldBe(completed);
        trace.DurationMs.ShouldBe(250);
        trace.ProducedEvents.Count.ShouldBe(1);
        trace.AffectedProjections.Count.ShouldBe(1);
        trace.RejectionEventType.ShouldBeNull();
        trace.ErrorMessage.ShouldBeNull();
        trace.ExternalTraceUrl.ShouldBe("https://trace.example.com?correlationId=corr-123");
        trace.TotalStreamEvents.ShouldBe(42);
        trace.ScanCapped.ShouldBeFalse();
        trace.ScanCapMessage.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WithNullStrings_DefaultsToEmpty() {
        var trace = new CorrelationTraceMap(
            null!, null!, null!, null!,
            null!, null!, null,
            null, null, null,
            null!, null!,
            null, null, null,
            0, false, null);

        trace.CorrelationId.ShouldBe(string.Empty);
        trace.TenantId.ShouldBe(string.Empty);
        trace.Domain.ShouldBe(string.Empty);
        trace.AggregateId.ShouldBe(string.Empty);
        trace.CommandType.ShouldBe(string.Empty);
        trace.CommandStatus.ShouldBe(string.Empty);
        trace.ProducedEvents.ShouldBeEmpty();
        trace.AffectedProjections.ShouldBeEmpty();
    }

    [Fact]
    public void ToString_OmitsEventPayloads() {
        var trace = new CorrelationTraceMap(
            "corr-1", "t1", "d1", "a1",
            "Cmd", "Completed", "user-1",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 100,
            [new TraceMapEvent(1, "Evt", DateTimeOffset.UtcNow, null, false)],
            [],
            null, null, null,
            10, false, null);

        string result = trace.ToString();

        result.ShouldContain("corr-1");
        result.ShouldContain("1 events");
        result.ShouldContain("0 projections");
        result.ShouldContain("TotalStreamEvents = 10");
    }

    [Fact]
    public void SerializationRoundTrip_PreservesAllProperties() {
        DateTimeOffset ts = new(2026, 3, 27, 10, 0, 0, TimeSpan.Zero);
        var original = new CorrelationTraceMap(
            "corr-1", "t1", "d1", "a1",
            "Cmd", "Rejected", "user-1",
            ts, ts.AddMilliseconds(50), 50,
            [new TraceMapEvent(1, "CommandRejected", ts, "caus-1", true)],
            [new TraceMapProjection("Proj1", "pending", 0)],
            "CommandRejected", "Invalid input", null,
            100, true, "Scan was capped.");

        string json = JsonSerializer.Serialize(original);
        CorrelationTraceMap? deserialized = JsonSerializer.Deserialize<CorrelationTraceMap>(json);

        _ = deserialized.ShouldNotBeNull();
        deserialized!.CorrelationId.ShouldBe("corr-1");
        deserialized.CommandStatus.ShouldBe("Rejected");
        deserialized.RejectionEventType.ShouldBe("CommandRejected");
        deserialized.ErrorMessage.ShouldBe("Invalid input");
        deserialized.ScanCapped.ShouldBeTrue();
        deserialized.ScanCapMessage.ShouldBe("Scan was capped.");
        deserialized.ProducedEvents.Count.ShouldBe(1);
        deserialized.ProducedEvents[0].IsRejection.ShouldBeTrue();
        deserialized.AffectedProjections.Count.ShouldBe(1);
        deserialized.AffectedProjections[0].Status.ShouldBe("pending");
    }

    [Fact]
    public void UnknownStatus_WithEmptyEvents_ReturnsValidInstance() {
        var trace = new CorrelationTraceMap(
            "expired-corr", "t1", string.Empty, string.Empty,
            string.Empty, "Unknown", null,
            null, null, null,
            [], [],
            null, "Command status not found.", null,
            0, false, null);

        trace.CommandStatus.ShouldBe("Unknown");
        trace.ProducedEvents.ShouldBeEmpty();
        trace.AffectedProjections.ShouldBeEmpty();
        trace.ErrorMessage.ShouldBe("Command status not found.");
    }
}
