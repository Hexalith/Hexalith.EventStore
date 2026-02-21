namespace Hexalith.EventStore.Server.Tests.HealthChecks;

using Dapr.Client;

using Hexalith.EventStore.CommandApi.HealthChecks;

using Microsoft.Extensions.Diagnostics.HealthChecks;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

public class DaprSidecarHealthCheckTests
{
    private static HealthCheckContext CreateContext(HealthStatus failureStatus = HealthStatus.Unhealthy)
    {
        var healthCheck = Substitute.For<IHealthCheck>();
        return new HealthCheckContext
        {
            Registration = new HealthCheckRegistration(
                "dapr-sidecar", healthCheck, failureStatus, ["ready"]),
        };
    }

    private static DaprMetadata CreateMetadata()
    {
        return new DaprMetadata(
            id: "commandapi",
            actors: [],
            extended: new Dictionary<string, string>(),
            components: []);
    }

    [Fact]
    public async Task CheckHealth_SidecarHealthy_ReturnsHealthy()
    {
        // Arrange
        var daprClient = Substitute.For<DaprClient>();
        daprClient.GetMetadataAsync(Arg.Any<CancellationToken>())
            .Returns(CreateMetadata());
        var healthCheck = new DaprSidecarHealthCheck(daprClient);

        // Act
        var result = await healthCheck.CheckHealthAsync(CreateContext());

        // Assert
        result.Status.ShouldBe(HealthStatus.Healthy);
        result.Description!.ShouldContain("responsive");
    }

    [Fact]
    public async Task CheckHealth_SidecarUnreachable_ReturnsUnhealthy()
    {
        // Arrange
        var daprClient = Substitute.For<DaprClient>();
        daprClient.GetMetadataAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new Grpc.Core.RpcException(new Grpc.Core.Status(Grpc.Core.StatusCode.Unavailable, "Connection refused")));
        var healthCheck = new DaprSidecarHealthCheck(daprClient);

        // Act
        var result = await healthCheck.CheckHealthAsync(CreateContext());

        // Assert
        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description!.ShouldContain("RpcException");
    }

    [Fact]
    public async Task CheckHealth_DaprException_ReturnsUnhealthy()
    {
        // Arrange
        var daprClient = Substitute.For<DaprClient>();
        daprClient.GetMetadataAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new Dapr.DaprException("Sidecar unavailable"));
        var healthCheck = new DaprSidecarHealthCheck(daprClient);

        // Act
        var result = await healthCheck.CheckHealthAsync(CreateContext());

        // Assert
        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description!.ShouldContain("DaprException");
    }
}
