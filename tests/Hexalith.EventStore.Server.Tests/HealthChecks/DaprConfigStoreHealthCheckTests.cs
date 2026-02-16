namespace Hexalith.EventStore.Server.Tests.HealthChecks;

using Dapr.Client;

using Hexalith.EventStore.CommandApi.HealthChecks;

using Microsoft.Extensions.Diagnostics.HealthChecks;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

public class DaprConfigStoreHealthCheckTests
{
    private const string ConfigStoreName = "configstore";

    private static HealthCheckContext CreateContext(HealthStatus failureStatus = HealthStatus.Degraded)
    {
        var healthCheck = Substitute.For<IHealthCheck>();
        return new HealthCheckContext
        {
            Registration = new HealthCheckRegistration(
                "dapr-configstore", healthCheck, failureStatus, ["ready"]),
        };
    }

    private static DaprMetadata CreateMetadata(params DaprComponentsMetadata[] components)
    {
        return new DaprMetadata(
            id: "test-app",
            actors: [],
            extended: new Dictionary<string, string>(),
            components: components);
    }

    [Fact]
    public async Task CheckHealth_ConfigStoreComponentFound_ReturnsHealthy()
    {
        // Arrange
        var daprClient = Substitute.For<DaprClient>();
        var metadata = CreateMetadata(new DaprComponentsMetadata(ConfigStoreName, "configuration.redis", "v1", []));
        daprClient.GetMetadataAsync(Arg.Any<CancellationToken>())
            .Returns(metadata);
        var healthCheck = new DaprConfigStoreHealthCheck(daprClient, ConfigStoreName);

        // Act
        var result = await healthCheck.CheckHealthAsync(CreateContext());

        // Assert
        result.Status.ShouldBe(HealthStatus.Healthy);
        result.Description!.ShouldContain("accessible");
    }

    [Fact]
    public async Task CheckHealth_ConfigStoreComponentNotFound_ReturnsDegraded()
    {
        // Arrange
        var daprClient = Substitute.For<DaprClient>();
        var metadata = CreateMetadata(new DaprComponentsMetadata("other", "state.redis", "v1", []));
        daprClient.GetMetadataAsync(Arg.Any<CancellationToken>())
            .Returns(metadata);
        var healthCheck = new DaprConfigStoreHealthCheck(daprClient, ConfigStoreName);

        // Act
        var result = await healthCheck.CheckHealthAsync(CreateContext());

        // Assert
        result.Status.ShouldBe(HealthStatus.Degraded);
        result.Description!.ShouldContain("not found");
    }

    [Fact]
    public async Task CheckHealth_MetadataCallFails_ReturnsDegraded()
    {
        // Arrange
        var daprClient = Substitute.For<DaprClient>();
        daprClient.GetMetadataAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new Dapr.DaprException("Sidecar unavailable"));
        var healthCheck = new DaprConfigStoreHealthCheck(daprClient, ConfigStoreName);

        // Act
        var result = await healthCheck.CheckHealthAsync(CreateContext());

        // Assert
        result.Status.ShouldBe(HealthStatus.Degraded);
        result.Description!.ShouldContain("DaprException");
    }
}
