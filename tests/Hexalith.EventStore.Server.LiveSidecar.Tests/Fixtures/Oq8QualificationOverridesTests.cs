using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Proves invalid overrides fail before the fixture can touch shared infrastructure.</summary>
public sealed class Oq8QualificationOverridesTests
{
    /// <summary>Preserves the existing CI execution profile.</summary>
    [Fact]
    public void AbsentOverridesPreserveReleaseProfile()
    {
        Oq8QualificationOverrides result = Oq8QualificationOverrides.Read(static _ => null);
        result.Configuration.ShouldBe("Release");
        result.DaprdPath.ShouldBeNull();
    }

    /// <summary>Requires the entire isolation group.</summary>
    [Fact]
    public void PartialOverridesFailClosed()
        => Should.Throw<InvalidOperationException>(() => Oq8QualificationOverrides.Read(
            static name => name.EndsWith("CONFIGURATION", StringComparison.Ordinal) ? "Debug" : null));

    /// <summary>Accepts a complete loopback profile and rejects unsafe substitutions.</summary>
    /// <param name="field">The substituted variable suffix.</param>
    /// <param name="value">The unsafe value.</param>
    [Theory]
    [InlineData("NAMESPACE", "default")]
    [InlineData("NAMESPACE", "g6-oq8-unsafe.namespace")]
    [InlineData("CONFIGURATION", "debug")]
    [InlineData("DAPRD_PATH", "daprd")]
    [InlineData("PLACEMENT_CONTAINER", "dapr_placement")]
    [InlineData("SCHEDULER_CONTAINER", "g6-oq8-placement")]
    [InlineData("REDIS_ENDPOINT", "192.0.2.1:6379")]
    [InlineData("REDIS_ENDPOINT", "localhost:6379")]
    [InlineData("REDIS_ENDPOINT", "127.0.0.1:0")]
    public void UnsafeOverridesFailClosed(string field, string value)
    {
        Dictionary<string, string> inputs = Inputs();
        inputs["HEXALITH_OQ8_" + field] = value;
        Should.Throw<InvalidOperationException>(() => Oq8QualificationOverrides.Read(name => inputs[name]));
    }

    /// <summary>Accepts an existing executable and distinct isolated resources.</summary>
    [Fact]
    public void CompleteOverridesSelectDebugAndIsolatedResources()
    {
        Dictionary<string, string> inputs = Inputs();
        Oq8QualificationOverrides result = Oq8QualificationOverrides.Read(name => inputs[name]);
        result.Configuration.ShouldBe("Debug");
        result.RedisEndpoint.ShouldBe("127.0.0.1:16379");
        result.PlacementContainer.ShouldBe("g6-oq8-placement");
        result.Namespace.ShouldBe("g6-oq8-test-discovery");
    }

    private static Dictionary<string, string> Inputs() => new()
    {
        ["HEXALITH_OQ8_DAPRD_PATH"] = typeof(Oq8QualificationOverridesTests).Assembly.Location,
        ["HEXALITH_OQ8_CONFIGURATION"] = "Debug",
        ["HEXALITH_OQ8_PLACEMENT_CONTAINER"] = "g6-oq8-placement",
        ["HEXALITH_OQ8_SCHEDULER_CONTAINER"] = "g6-oq8-scheduler",
        ["HEXALITH_OQ8_NAMESPACE"] = "g6-oq8-test-discovery",
        ["HEXALITH_OQ8_REDIS_ENDPOINT"] = "127.0.0.1:16379",
    };
}
