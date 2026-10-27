using Shouldly;

namespace Hexalith.EventStore.Testing.Integration.Tests;

/// <summary>Preserves caller control-plane selections while retaining harness endpoint defaults.</summary>
public sealed class AspireTopologyFixtureArgumentsTests
{
    [Fact]
    public void MissingEndpointsUseProbedDefaults()
    {
        AspireTopologyFixtureBase<object>.WithControlPlaneDefaults(["--EnableKeycloak=false"], 6050, 6060)
            .ShouldBe(["--EnableKeycloak=false", "--Dapr:PlacementHostAddress=localhost:6050", "--Dapr:SchedulerHostAddress=localhost:6060"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("remote:50005")]
    public void ExplicitEndpointValuesIncludingEmptyAreRetained(string value)
    {
        string[] arguments = [$"--Dapr:PlacementHostAddress={value}", "--dapr:schedulerhostaddress", value];
        AspireTopologyFixtureBase<object>.WithControlPlaneDefaults(arguments, 6050, 6060).ShouldBe(arguments);
    }

    [Fact]
    public void OneExplicitEndpointDoesNotSuppressTheOtherDefault()
    {
        AspireTopologyFixtureBase<object>.WithControlPlaneDefaults(["--Dapr:PlacementHostAddress="], 6050, 6060)
            .ShouldBe(["--Dapr:PlacementHostAddress=", "--Dapr:SchedulerHostAddress=localhost:6060"]);
    }
}
