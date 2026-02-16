namespace Hexalith.EventStore.Server.Tests.HealthChecks;

using Dapr.Client;

using Hexalith.EventStore.CommandApi.HealthChecks;

using Microsoft.Extensions.Diagnostics.HealthChecks;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

public class DaprPubSubHealthCheckTests
{
    private const string PubSubName = "pubsub";

    private static HealthCheckContext CreateContext(HealthStatus failureStatus = HealthStatus.Degraded)
    {
        var healthCheck = Substitute.For<IHealthCheck>();
        return new HealthCheckContext
        {
            Registration = new HealthCheckRegistration(
                "dapr-pubsub", healthCheck, failureStatus, ["ready"]),
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
    public async Task CheckHealth_PubSubComponentFound_ReturnsHealthy()
    {
        // Arrange
        var daprClient = Substitute.For<DaprClient>();
        var metadata = CreateMetadata(new DaprComponentsMetadata(PubSubName, "pubsub.redis", "v1", []));
        daprClient.GetMetadataAsync(Arg.Any<CancellationToken>())
            .Returns(metadata);
        var healthCheck = new DaprPubSubHealthCheck(daprClient, PubSubName);

        // Act
        var result = await healthCheck.CheckHealthAsync(CreateContext());

        // Assert
        result.Status.ShouldBe(HealthStatus.Healthy);
        result.Description!.ShouldContain("available");
        result.Description!.ShouldContain("pubsub.redis");
    }

    [Fact]
    public async Task CheckHealth_PubSubComponentNotFound_ReturnsDegraded()
    {
        // Arrange
        var daprClient = Substitute.For<DaprClient>();
        var metadata = CreateMetadata(new DaprComponentsMetadata("other", "state.redis", "v1", []));
        daprClient.GetMetadataAsync(Arg.Any<CancellationToken>())
            .Returns(metadata);
        var healthCheck = new DaprPubSubHealthCheck(daprClient, PubSubName);

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
        var healthCheck = new DaprPubSubHealthCheck(daprClient, PubSubName);

        // Act
        var result = await healthCheck.CheckHealthAsync(CreateContext());

        // Assert
        result.Status.ShouldBe(HealthStatus.Degraded);
        result.Description!.ShouldContain("DaprException");
    }

    [Fact]
    public async Task CheckHealth_WrongComponentType_ReturnsDegraded()
    {
        // Arrange -- component name matches but type is not pubsub.*
        var daprClient = Substitute.For<DaprClient>();
        var metadata = CreateMetadata(new DaprComponentsMetadata(PubSubName, "state.redis", "v1", []));
        daprClient.GetMetadataAsync(Arg.Any<CancellationToken>())
            .Returns(metadata);
        var healthCheck = new DaprPubSubHealthCheck(daprClient, PubSubName);

        // Act
        var result = await healthCheck.CheckHealthAsync(CreateContext());

        // Assert
        result.Status.ShouldBe(HealthStatus.Degraded);
        result.Description!.ShouldContain("not found");
    }
}
