using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Streams;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.Streams;

public class TraceMapProjectionTests {
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance() {
        var proj = new TraceMapProjection("CounterView", "processed", 42);

        proj.ProjectionName.ShouldBe("CounterView");
        proj.Status.ShouldBe("processed");
        proj.LastProcessedSequence.ShouldBe(42);
    }

    [Fact]
    public void Constructor_WithNullStrings_DefaultsToEmpty() {
        var proj = new TraceMapProjection(null!, null!, null);

        proj.ProjectionName.ShouldBe(string.Empty);
        proj.Status.ShouldBe(string.Empty);
        proj.LastProcessedSequence.ShouldBeNull();
    }

    [Fact]
    public void ToString_ContainsAllFields() {
        var proj = new TraceMapProjection("MyProj", "faulted", 10);

        string result = proj.ToString();

        result.ShouldContain("MyProj");
        result.ShouldContain("faulted");
        result.ShouldContain("10");
    }

    [Fact]
    public void SerializationRoundTrip_PreservesAll() {
        var original = new TraceMapProjection("Proj", "pending", 5);

        string json = JsonSerializer.Serialize(original);
        TraceMapProjection? deserialized = JsonSerializer.Deserialize<TraceMapProjection>(json);

        _ = deserialized.ShouldNotBeNull();
        deserialized!.ProjectionName.ShouldBe("Proj");
        deserialized.Status.ShouldBe("pending");
        deserialized.LastProcessedSequence.ShouldBe(5);
    }

    [Theory]
    [InlineData("processed")]
    [InlineData("pending")]
    [InlineData("faulted")]
    [InlineData("unknown")]
    public void Constructor_WithValidStatus_AcceptsValue(string status) {
        var proj = new TraceMapProjection("Test", status, null);

        proj.Status.ShouldBe(status);
    }
}
