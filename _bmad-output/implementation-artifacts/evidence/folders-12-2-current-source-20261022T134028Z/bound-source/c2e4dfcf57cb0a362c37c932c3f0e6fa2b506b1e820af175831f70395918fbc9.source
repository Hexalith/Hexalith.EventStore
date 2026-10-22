
using Hexalith.EventStore.HealthChecks;

using Microsoft.Extensions.Diagnostics.HealthChecks;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.HealthChecks;

public class DaprActorPlacementHealthCheckTests {
    private static HealthCheckContext CreateContext(HealthStatus failureStatus = HealthStatus.Unhealthy) {
        IHealthCheck healthCheck = Substitute.For<IHealthCheck>();
        return new HealthCheckContext {
            Registration = new HealthCheckRegistration(
                "dapr-actor-placement", healthCheck, failureStatus, ["ready"]),
        };
    }

    [Fact]
    public async Task CheckHealth_HostReady_ReturnsHealthy() {
        // Arrange
        IDaprActorPlacementProbe probe = Substitute.For<IDaprActorPlacementProbe>();
        _ = probe.CheckAsync(Arg.Any<CancellationToken>())
            .Returns(new DaprActorPlacementStatus(true, true, "placement: connected", "RUNNING"));
        var healthCheck = new DaprActorPlacementHealthCheck(probe);

        // Act
        HealthCheckResult result = await healthCheck.CheckHealthAsync(CreateContext());

        // Assert
        result.Status.ShouldBe(HealthStatus.Healthy);
        result.Description!.ShouldContain("placement: connected");
    }

    [Fact]
    public async Task CheckHealth_HostNotReady_ReturnsUnhealthy() {
        // Arrange -- sidecar responds but the actor host has not joined placement
        IDaprActorPlacementProbe probe = Substitute.For<IDaprActorPlacementProbe>();
        _ = probe.CheckAsync(Arg.Any<CancellationToken>())
            .Returns(new DaprActorPlacementStatus(true, false, "placement: disconnected", "RUNNING"));
        var healthCheck = new DaprActorPlacementHealthCheck(probe);

        // Act
        HealthCheckResult result = await healthCheck.CheckHealthAsync(CreateContext());

        // Assert
        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description!.ShouldContain("placement service");
        result.Description!.ShouldContain("hang");
    }

    [Fact]
    public async Task CheckHealth_ProbeThrows_ReturnsUnhealthy() {
        // Arrange
        IDaprActorPlacementProbe probe = Substitute.For<IDaprActorPlacementProbe>();
        _ = probe.CheckAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Sidecar unavailable"));
        var healthCheck = new DaprActorPlacementHealthCheck(probe);

        // Act
        HealthCheckResult result = await healthCheck.CheckHealthAsync(CreateContext());

        // Assert
        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description!.ShouldContain(nameof(HttpRequestException));
    }

    [Fact]
    public async Task CheckHealth_PropagatesCancellationToken() {
        // Arrange
        using var cts = new CancellationTokenSource();
        IDaprActorPlacementProbe probe = Substitute.For<IDaprActorPlacementProbe>();
        _ = probe.CheckAsync(Arg.Any<CancellationToken>())
            .Returns(new DaprActorPlacementStatus(true, true, "placement: connected", "RUNNING"));
        var healthCheck = new DaprActorPlacementHealthCheck(probe);

        // Act
        _ = await healthCheck.CheckHealthAsync(CreateContext(), cts.Token);

        // Assert
        _ = await probe.Received(1).CheckAsync(cts.Token);
    }

    [Fact]
    public void Constructor_NullProbe_ThrowsArgumentNullException()
        => Should.Throw<ArgumentNullException>(() => new DaprActorPlacementHealthCheck(null!));
}
