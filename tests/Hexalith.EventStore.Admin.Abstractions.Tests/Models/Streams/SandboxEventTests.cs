using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Streams;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.Streams;

public class SandboxEventTests
{
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance()
    {
        var sandboxEvent = new SandboxEvent(
            0,
            "CounterIncremented",
            "{\"Amount\":5}",
            false);

        sandboxEvent.Index.ShouldBe(0);
        sandboxEvent.EventTypeName.ShouldBe("CounterIncremented");
        sandboxEvent.PayloadJson.ShouldBe("{\"Amount\":5}");
        sandboxEvent.IsRejection.ShouldBeFalse();
    }

    [Fact]
    public void Constructor_WithNullStrings_DefaultsToEmpty()
    {
        var sandboxEvent = new SandboxEvent(
            1,
            null!,
            null!,
            true);

        sandboxEvent.EventTypeName.ShouldBe(string.Empty);
        sandboxEvent.PayloadJson.ShouldBe(string.Empty);
        sandboxEvent.Index.ShouldBe(1);
        sandboxEvent.IsRejection.ShouldBeTrue();
    }

    [Fact]
    public void ToString_RedactsPayloadJson()
    {
        var sandboxEvent = new SandboxEvent(
            0,
            "CounterIncremented",
            "{\"Secret\":\"top-secret-payload\"}",
            false);

        string result = sandboxEvent.ToString();

        result.ShouldContain("[REDACTED]");
        result.ShouldContain("CounterIncremented");
        result.ShouldNotContain("top-secret-payload");
        result.ShouldNotContain("Secret");
    }

    [Fact]
    public void SerializationRoundTrip_PreservesAllProperties()
    {
        var original = new SandboxEvent(
            2,
            "OrderRejected",
            "{\"Reason\":\"Insufficient funds\"}",
            true);

        string json = JsonSerializer.Serialize(original);
        SandboxEvent? deserialized = JsonSerializer.Deserialize<SandboxEvent>(json);

        deserialized.ShouldNotBeNull();
        deserialized!.Index.ShouldBe(2);
        deserialized.EventTypeName.ShouldBe("OrderRejected");
        deserialized.PayloadJson.ShouldBe("{\"Reason\":\"Insufficient funds\"}");
        deserialized.IsRejection.ShouldBeTrue();
    }
}
