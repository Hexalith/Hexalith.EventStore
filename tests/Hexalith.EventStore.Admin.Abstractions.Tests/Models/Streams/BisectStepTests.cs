using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Streams;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.Streams;

public class BisectStepTests {
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance() {
        var step = new BisectStep(1, 50, "good", 0);

        step.StepNumber.ShouldBe(1);
        step.TestedSequence.ShouldBe(50);
        step.Verdict.ShouldBe("good");
        step.DivergentFieldCount.ShouldBe(0);
    }

    [Fact]
    public void Constructor_WithBadVerdict_CreatesInstance() {
        var step = new BisectStep(3, 75, "bad", 5);

        step.StepNumber.ShouldBe(3);
        step.TestedSequence.ShouldBe(75);
        step.Verdict.ShouldBe("bad");
        step.DivergentFieldCount.ShouldBe(5);
    }

    [Fact]
    public void Constructor_WithNullVerdict_DefaultsToEmpty() {
        var step = new BisectStep(1, 50, null!, 0);

        step.Verdict.ShouldBe(string.Empty);
    }

    [Fact]
    public void ToString_ReturnsExpectedFormat() {
        var step = new BisectStep(2, 100, "bad", 3);

        string result = step.ToString();

        result.ShouldBe("Step 2: seq 100 = bad");
    }

    [Fact]
    public void SerializationRoundTrip_PreservesAllFields() {
        var original = new BisectStep(5, 512, "good", 0);

        string json = JsonSerializer.Serialize(original);
        BisectStep? deserialized = JsonSerializer.Deserialize<BisectStep>(json);

        _ = deserialized.ShouldNotBeNull();
        deserialized!.StepNumber.ShouldBe(5);
        deserialized.TestedSequence.ShouldBe(512);
        deserialized.Verdict.ShouldBe("good");
        deserialized.DivergentFieldCount.ShouldBe(0);
    }
}
