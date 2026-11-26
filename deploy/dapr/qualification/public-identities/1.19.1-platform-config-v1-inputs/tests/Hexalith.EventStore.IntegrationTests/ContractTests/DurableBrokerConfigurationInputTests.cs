using Hexalith.EventStore.IntegrationTests.Fixtures;

using Shouldly;

namespace Hexalith.EventStore.IntegrationTests.ContractTests;

/// <summary>Verifies bounded configuration forwarding without starting live infrastructure.</summary>
public class DurableBrokerConfigurationInputTests
{
    /// <summary>Preserves the historical invocation and full broker matrix.</summary>
    [Fact]
    public void HistoricalInvocationRemainsUnchanged()
    {
        DurableBrokerQualificationFixture.QualificationArguments("deploy/dapr/qualification/runtime-lock.json", null, "output")
            .ShouldBe(["tools/qualify-dapr-brokers.py", "--profile", "deploy/dapr/qualification/runtime-lock.json", "--brokers", "rabbitmq,kafka", "--output", "output"]);
    }

    /// <summary>Forwards the separate input with the new explicit profile.</summary>
    [Fact]
    public void PlatformInvocationForwardsConfigurationAndFullMatrix()
    {
        string[] arguments = DurableBrokerQualificationFixture.QualificationArguments(
            "deploy/dapr/qualification/runtime-lock-1.19.1-platform-config-v1.json", "platform.json", "output");
        arguments.ShouldContain("rabbitmq,kafka");
        arguments[^2].ShouldBe("--platform-config");
        arguments[^1].ShouldBe("platform.json");
    }

    /// <summary>Rejects missing configuration and attempts to relabel historical captures.</summary>
    /// <param name="profile">The selected profile.</param>
    /// <param name="configuration">The optional configuration path.</param>
    [Theory]
    [InlineData("runtime-lock.json", "platform.json")]
    [InlineData("runtime-lock-1.19.1.json", "platform.json")]
    [InlineData("runtime-lock-1.19.1-platform-config-v1.json", null)]
    [InlineData("runtime-lock-1.19.1-platform-config-v1.json", " ")]
    public void ConfigurationRequiresExplicitPairedProfile(string profile, string? configuration)
    {
        Should.Throw<ArgumentException>(() => DurableBrokerQualificationFixture.QualificationArguments(profile, configuration, "output"));
    }
}
