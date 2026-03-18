
using Dapr.Client;

using Hexalith.EventStore.CommandApi.HealthChecks;

using Microsoft.Extensions.Diagnostics.HealthChecks;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.HealthChecks;

public class DaprStateStoreHealthCheckTests {
    private const string StoreName = "statestore";

    private static HealthCheckContext CreateContext(HealthStatus failureStatus = HealthStatus.Unhealthy) {
        IHealthCheck healthCheck = Substitute.For<IHealthCheck>();
        return new HealthCheckContext {
            Registration = new HealthCheckRegistration(
                "dapr-statestore", healthCheck, failureStatus, ["ready"]),
        };
    }

    [Fact]
    public async Task CheckHealth_StateStoreAccessible_ReturnsHealthy() {
        // Arrange
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetStateAsync<string>(StoreName, "__health_check__", cancellationToken: Arg.Any<CancellationToken>())
            .Returns((string)null!);
        var healthCheck = new DaprStateStoreHealthCheck(daprClient, StoreName);

        // Act
        HealthCheckResult result = await healthCheck.CheckHealthAsync(CreateContext());

        // Assert
        result.Status.ShouldBe(HealthStatus.Healthy);
        result.Description!.ShouldContain("accessible");
    }

    [Fact]
    public async Task CheckHealth_StateStoreUnavailable_ReturnsUnhealthy() {
        // Arrange
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetStateAsync<string>(StoreName, "__health_check__", cancellationToken: Arg.Any<CancellationToken>())
            .ThrowsAsync(new Dapr.DaprException("State store unavailable"));
        var healthCheck = new DaprStateStoreHealthCheck(daprClient, StoreName);

        // Act
        HealthCheckResult result = await healthCheck.CheckHealthAsync(CreateContext());

        // Assert
        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description!.ShouldContain("not accessible");
    }

    [Fact]
    public async Task CheckHealth_StateStoreReturnsValue_ReturnsHealthy() {
        // Arrange -- edge case: sentinel key exists and has a value
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetStateAsync<string>(StoreName, "__health_check__", cancellationToken: Arg.Any<CancellationToken>())
            .Returns("some-value");
        var healthCheck = new DaprStateStoreHealthCheck(daprClient, StoreName);

        // Act
        HealthCheckResult result = await healthCheck.CheckHealthAsync(CreateContext());

        // Assert
        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealth_NeverWritesToStateStore() {
        // Arrange
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetStateAsync<string>(StoreName, "__health_check__", cancellationToken: Arg.Any<CancellationToken>())
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

    [Fact]
    public async Task CheckHealth_PropagatesCancellationToken() {
        // Arrange
        using var cts = new CancellationTokenSource();
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetStateAsync<string>(StoreName, "__health_check__", cancellationToken: Arg.Any<CancellationToken>())
            .Returns((string)null!);
        var healthCheck = new DaprStateStoreHealthCheck(daprClient, StoreName);

        // Act
        _ = await healthCheck.CheckHealthAsync(CreateContext(), cts.Token);

        // Assert
        await daprClient.Received(1).GetStateAsync<string>(StoreName, "__health_check__", cancellationToken: cts.Token);
    }

    [Fact]
    public void Constructor_NullDaprClient_ThrowsArgumentNullException() {
        Should.Throw<ArgumentNullException>(() => new DaprStateStoreHealthCheck(null!, StoreName));
    }

    [Fact]
    public void Constructor_NullStoreName_ThrowsArgumentNullException() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        Should.Throw<ArgumentNullException>(() => new DaprStateStoreHealthCheck(daprClient, null!));
    }
}
