using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Checks isolation selectors fail before a diagnostic can target unrelated infrastructure.</summary>
public sealed class DaprTestInfrastructureContainersTests
{
    /// <summary>Preserves ordinary local-development container names when no selector is supplied.</summary>
    [Fact]
    public void MissingSelectorPreservesLocalDefaults()
        => DaprTestInfrastructureContainers.Resolve("dapr_redis", _ => null).ShouldBe("dapr_redis");

    /// <summary>Uses the complete immutable ID supplied by the fixture owner.</summary>
    [Fact]
    public void ExactContainerIdentityOverridesConventionalName()
    {
        string identity = new('a', 64);
        DaprTestInfrastructureContainers.Resolve("dapr_placement", variable =>
            variable == "EVENTSTORE_TEST_PLACEMENT_CONTAINER" ? identity : null).ShouldBe(identity);
    }

    /// <summary>Rejects names, truncated IDs and malformed explicit selectors.</summary>
    /// <param name="selector">The invalid supplied selector.</param>
    [Theory]
    [InlineData("")]
    [InlineData("dapr_redis")]
    [InlineData("0123456789ab")]
    [InlineData("--all")]
    public void InvalidExplicitIdentityFailsClosed(string selector)
        => Should.Throw<InvalidOperationException>(() =>
            DaprTestInfrastructureContainers.Resolve("dapr_redis", _ => selector));
}
