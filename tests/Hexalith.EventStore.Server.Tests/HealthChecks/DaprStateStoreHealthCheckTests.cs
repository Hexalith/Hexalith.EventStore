namespace Hexalith.EventStore.Server.Tests.HealthChecks;

using Dapr.Client;

using Hexalith.EventStore.CommandApi.HealthChecks;

using Microsoft.Extensions.Diagnostics.HealthChecks;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

public class DaprStateStoreHealthCheckTests {
    private const string StoreName = "statestore";

    private static HealthCheckContext CreateContext(HealthStatus failureStatus = HealthStatus.Unhealthy) {
        var healthCheck = Substitute.For<IHealthCheck>();
        return new HealthCheckContext {
            Registration = new HealthCheckRegistration(
                "dapr-statestore", healthCheck, failureStatus, ["ready"]),
        };
    }

    [Fact]
    public async Task CheckHealth_StateStoreAccessible_ReturnsHealthy() {
        // Arrange
        var daprClient = Substitute.For<DaprClient>();
        daprClient.GetStateAsync<string>(StoreName, "__health_check__", cancellationToken: Arg.Any<CancellationToken>())
            .Returns((string)null!);
        var healthCheck = new DaprStateStoreHealthCheck(daprClient, StoreName);

        // Act
        var result = await healthCheck.CheckHealthAsync(CreateContext());

        // Assert
        result.Status.ShouldBe(HealthStatus.Healthy);
        result.Description!.ShouldContain("accessible");
    }

    [Fact]
    public async Task CheckHealth_StateStoreUnavailable_ReturnsUnhealthy() {
        // Arrange
        var daprClient = Substitute.For<DaprClient>();
        daprClient.GetStateAsync<string>(StoreName, "__health_check__", cancellationToken: Arg.Any<CancellationToken>())
            .ThrowsAsync(new Dapr.DaprException("State store unavailable"));
        var healthCheck = new DaprStateStoreHealthCheck(daprClient, StoreName);

        // Act
        var result = await healthCheck.CheckHealthAsync(CreateContext());

        // Assert
        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description!.ShouldContain("not accessible");
    }

    [Fact]
    public async Task CheckHealth_StateStoreReturnsValue_ReturnsHealthy() {
        // Arrange -- edge case: sentinel key exists and has a value
        var daprClient = Substitute.For<DaprClient>();
        daprClient.GetStateAsync<string>(StoreName, "__health_check__", cancellationToken: Arg.Any<CancellationToken>())
            .Returns("some-value");
        var healthCheck = new DaprStateStoreHealthCheck(daprClient, StoreName);

        // Act
        var result = await healthCheck.CheckHealthAsync(CreateContext());

        // Assert
        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealth_NeverWritesToStateStore() {
        // Arrange
        var daprClient = Substitute.For<DaprClient>();
        daprClient.GetStateAsync<string>(StoreName, "__health_check__", cancellationToken: Arg.Any<CancellationToken>())
            .Returns((string)null!);
        var healthCheck = new DaprStateStoreHealthCheck(daprClient, StoreName);

        // Act
        _ = await healthCheck.CheckHealthAsync(CreateContext());

        // Assert -- verify no write operations were called
        await daprClient.DidNotReceive().SaveStateAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>());
        await daprClient.DidNotReceive().DeleteStateAsync(
            Arg.Any<string>(), Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>());
    }
}
