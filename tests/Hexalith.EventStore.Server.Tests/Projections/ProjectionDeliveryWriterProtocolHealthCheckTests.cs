using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.Diagnostics.HealthChecks;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public class ProjectionDeliveryWriterProtocolHealthCheckTests {
    [Fact]
    public async Task MissingMarker_IsUnhealthy() {
        IProjectionDeliveryStateStore store = Substitute.For<IProjectionDeliveryStateStore>();
        _ = store.ReadWriterProtocolAsync(Arg.Any<CancellationToken>())
            .Returns((ProjectionDeliveryWriterProtocol?)null);
        var check = new ProjectionDeliveryWriterProtocolHealthCheck(store);

        HealthCheckResult result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.ShouldBe(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task ExactV2Marker_IsHealthy() {
        IProjectionDeliveryStateStore store = Substitute.For<IProjectionDeliveryStateStore>();
        _ = store.ReadWriterProtocolAsync(Arg.Any<CancellationToken>()).Returns(
            new ProjectionDeliveryWriterProtocol(1, 2, "commit", DateTimeOffset.UtcNow));
        var check = new ProjectionDeliveryWriterProtocolHealthCheck(store);

        HealthCheckResult result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.ShouldBe(HealthStatus.Healthy);
    }
}
