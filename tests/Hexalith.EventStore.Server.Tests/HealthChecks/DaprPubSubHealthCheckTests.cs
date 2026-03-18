
using Dapr.Client;

using Hexalith.EventStore.CommandApi.HealthChecks;

using Microsoft.Extensions.Diagnostics.HealthChecks;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.HealthChecks;

public class DaprPubSubHealthCheckTests {
    private const string PubSubName = "pubsub";

    private static HealthCheckContext CreateContext(HealthStatus failureStatus = HealthStatus.Degraded) {
        IHealthCheck healthCheck = Substitute.For<IHealthCheck>();
        return new HealthCheckContext {
            Registration = new HealthCheckRegistration(
                "dapr-pubsub", healthCheck, failureStatus, ["ready"]),
        };
    }

    private static DaprMetadata CreateMetadata(params DaprComponentsMetadata[] components) => new(
            id: "test-app",
            actors: [],
            extended: new Dictionary<string, string>(),
            components: components);

    [Fact]
    public async Task CheckHealth_PubSubComponentFound_ReturnsHealthy() {
        // Arrange
        DaprClient daprClient = Substitute.For<DaprClient>();
        DaprMetadata metadata = CreateMetadata(new DaprComponentsMetadata(PubSubName, "pubsub.redis", "v1", []));
        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>())
            .Returns(metadata);
        var healthCheck = new DaprPubSubHealthCheck(daprClient, PubSubName);

        // Act
        HealthCheckResult result = await healthCheck.CheckHealthAsync(CreateContext());

        // Assert
        result.Status.ShouldBe(HealthStatus.Healthy);
        result.Description!.ShouldContain("available");
        result.Description!.ShouldContain("pubsub.redis");
    }

    [Fact]
    public async Task CheckHealth_PubSubComponentNotFound_ReturnsDegraded() {
        // Arrange
        DaprClient daprClient = Substitute.For<DaprClient>();
        DaprMetadata metadata = CreateMetadata(new DaprComponentsMetadata("other", "state.redis", "v1", []));
        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>())
            .Returns(metadata);
        var healthCheck = new DaprPubSubHealthCheck(daprClient, PubSubName);

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
        var healthCheck = new DaprPubSubHealthCheck(daprClient, PubSubName);

        // Act
        HealthCheckResult result = await healthCheck.CheckHealthAsync(CreateContext());

        // Assert
        result.Status.ShouldBe(HealthStatus.Degraded);
        result.Description!.ShouldContain("DaprException");
    }

    [Fact]
    public async Task CheckHealth_WrongComponentType_ReturnsDegraded() {
        // Arrange -- component name matches but type is not pubsub.*
        DaprClient daprClient = Substitute.For<DaprClient>();
        DaprMetadata metadata = CreateMetadata(new DaprComponentsMetadata(PubSubName, "state.redis", "v1", []));
        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>())
            .Returns(metadata);
        var healthCheck = new DaprPubSubHealthCheck(daprClient, PubSubName);

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
        DaprMetadata metadata = CreateMetadata(new DaprComponentsMetadata(PubSubName, "pubsub.redis", "v1", []));
        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(metadata);
        var healthCheck = new DaprPubSubHealthCheck(daprClient, PubSubName);

        // Act
        _ = await healthCheck.CheckHealthAsync(CreateContext(), cts.Token);

        // Assert
        await daprClient.Received(1).GetMetadataAsync(cts.Token);
    }

    [Fact]
    public void Constructor_NullDaprClient_ThrowsArgumentNullException() {
        Should.Throw<ArgumentNullException>(() => new DaprPubSubHealthCheck(null!, PubSubName));
    }

    [Fact]
    public void Constructor_NullPubSubName_ThrowsArgumentNullException() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        Should.Throw<ArgumentNullException>(() => new DaprPubSubHealthCheck(daprClient, null!));
    }
}
