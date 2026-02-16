namespace Hexalith.EventStore.Server.Tests.HealthChecks;

using System.Net.Http;

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

    [Fact]
    public async Task CheckHealth_SidecarHealthy_ReturnsHealthy()
    {
        // Arrange
        var daprClient = Substitute.For<DaprClient>();
        daprClient.CheckHealthAsync(Arg.Any<CancellationToken>())
            .Returns(true);
        var healthCheck = new DaprSidecarHealthCheck(daprClient);

        // Act
        var result = await healthCheck.CheckHealthAsync(CreateContext());

        // Assert
        result.Status.ShouldBe(HealthStatus.Healthy);
        result.Description!.ShouldContain("responsive");
    }

    [Fact]
    public async Task CheckHealth_SidecarUnhealthy_ReturnsUnhealthy()
    {
        // Arrange
        var daprClient = Substitute.For<DaprClient>();
        daprClient.CheckHealthAsync(Arg.Any<CancellationToken>())
            .Returns(false);
        var healthCheck = new DaprSidecarHealthCheck(daprClient);

        // Act
        var result = await healthCheck.CheckHealthAsync(CreateContext());

        // Assert
        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description!.ShouldContain("not responsive");
    }

    [Fact]
    public async Task CheckHealth_SidecarUnreachable_ReturnsUnhealthy()
    {
        // Arrange
        var daprClient = Substitute.For<DaprClient>();
        daprClient.CheckHealthAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));
        var healthCheck = new DaprSidecarHealthCheck(daprClient);

        // Act
        var result = await healthCheck.CheckHealthAsync(CreateContext());

        // Assert
        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description!.ShouldContain("HttpRequestException");
    }

    [Fact]
    public async Task CheckHealth_DaprException_ReturnsUnhealthy()
    {
        // Arrange
        var daprClient = Substitute.For<DaprClient>();
        daprClient.CheckHealthAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new Dapr.DaprException("Sidecar unavailable"));
        var healthCheck = new DaprSidecarHealthCheck(daprClient);

        // Act
        var result = await healthCheck.CheckHealthAsync(CreateContext());

        // Assert
        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description!.ShouldContain("DaprException");
    }
}
