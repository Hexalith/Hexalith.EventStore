
using Dapr.Client;

using Hexalith.EventStore.CommandApi.HealthChecks;

using Microsoft.Extensions.Diagnostics.HealthChecks;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.HealthChecks;

public class DaprConfigStoreHealthCheckTests {
    private const string ConfigStoreName = "configstore";

    private static HealthCheckContext CreateContext(HealthStatus failureStatus = HealthStatus.Degraded) {
        IHealthCheck healthCheck = Substitute.For<IHealthCheck>();
        return new HealthCheckContext {
            Registration = new HealthCheckRegistration(
                "dapr-configstore", healthCheck, failureStatus, ["ready"]),
        };
    }

    private static DaprMetadata CreateMetadata(params DaprComponentsMetadata[] components) => new(
            id: "test-app",
            actors: [],
            extended: new Dictionary<string, string>(),
            components: components);

    [Fact]
    public async Task CheckHealth_ConfigStoreComponentFound_ReturnsHealthy() {
        // Arrange
        DaprClient daprClient = Substitute.For<DaprClient>();
        DaprMetadata metadata = CreateMetadata(new DaprComponentsMetadata(ConfigStoreName, "configuration.redis", "v1", []));
        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>())
            .Returns(metadata);
        var healthCheck = new DaprConfigStoreHealthCheck(daprClient, ConfigStoreName);

        // Act
        HealthCheckResult result = await healthCheck.CheckHealthAsync(CreateContext());

        // Assert
        result.Status.ShouldBe(HealthStatus.Healthy);
        result.Description!.ShouldContain("accessible");
    }

    [Fact]
    public async Task CheckHealth_ConfigStoreComponentNotFound_ReturnsDegraded() {
        // Arrange
        DaprClient daprClient = Substitute.For<DaprClient>();
        DaprMetadata metadata = CreateMetadata(new DaprComponentsMetadata("other", "state.redis", "v1", []));
        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>())
            .Returns(metadata);
        var healthCheck = new DaprConfigStoreHealthCheck(daprClient, ConfigStoreName);

        // Act
        HealthCheckResult result = await healthCheck.CheckHealthAsync(CreateContext());

        // Assert
        result.Status.ShouldBe(HealthStatus.Degraded);
        result.Description!.ShouldContain("not found");
    }

    [Fact]
    public async Task CheckHealth_MetadataCallFails_ReturnsDegraded() {
        // Arrange
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new Dapr.DaprException("Sidecar unavailable"));
        var healthCheck = new DaprConfigStoreHealthCheck(daprClient, ConfigStoreName);

        // Act
        HealthCheckResult result = await healthCheck.CheckHealthAsync(CreateContext());

        // Assert
        result.Status.ShouldBe(HealthStatus.Degraded);
        result.Description!.ShouldContain("DaprException");
    }

    [Fact]
    public async Task CheckHealth_WrongComponentType_ReturnsDegraded() {
        // Arrange -- component name matches but type is not configuration.*
        DaprClient daprClient = Substitute.For<DaprClient>();
        DaprMetadata metadata = CreateMetadata(new DaprComponentsMetadata(ConfigStoreName, "state.redis", "v1", []));
        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>())
            .Returns(metadata);
        var healthCheck = new DaprConfigStoreHealthCheck(daprClient, ConfigStoreName);

        // Act
        HealthCheckResult result = await healthCheck.CheckHealthAsync(CreateContext());

        // Assert
        result.Status.ShouldBe(HealthStatus.Degraded);
        result.Description!.ShouldContain("not found");
    }

    [Fact]
    public async Task CheckHealth_PropagatesCancellationToken() {
        // Arrange
        using var cts = new CancellationTokenSource();
        DaprClient daprClient = Substitute.For<DaprClient>();
        DaprMetadata metadata = CreateMetadata(new DaprComponentsMetadata(ConfigStoreName, "configuration.redis", "v1", []));
        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(metadata);
        var healthCheck = new DaprConfigStoreHealthCheck(daprClient, ConfigStoreName);

        // Act
        _ = await healthCheck.CheckHealthAsync(CreateContext(), cts.Token);

        // Assert
        _ = await daprClient.Received(1).GetMetadataAsync(cts.Token);
    }

    [Fact]
    public void Constructor_NullDaprClient_ThrowsArgumentNullException() => Should.Throw<ArgumentNullException>(() => new DaprConfigStoreHealthCheck(null!, ConfigStoreName));

    [Fact]
    public void Constructor_NullConfigStoreName_ThrowsArgumentNullException() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = Should.Throw<ArgumentNullException>(() => new DaprConfigStoreHealthCheck(daprClient, null!));
    }
}
