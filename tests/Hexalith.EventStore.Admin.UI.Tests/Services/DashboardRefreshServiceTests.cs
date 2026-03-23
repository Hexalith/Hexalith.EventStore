using Hexalith.EventStore.Admin.Abstractions.Models.Health;
using Hexalith.EventStore.Admin.UI.Services;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.UI.Tests.Services;

/// <summary>
/// Unit tests for the DashboardRefreshService.
/// </summary>
public class DashboardRefreshServiceTests
{
    [Fact]
    public async Task TriggerImmediateRefresh_FiresOnDataChanged()
    {
        // Arrange
        AdminStreamApiClient mockClient = Substitute.For<AdminStreamApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminStreamApiClient>.Instance);
        SystemHealthReport health = new(
            HealthStatus.Healthy, 100, 5.0, 0.1, [],
            new ObservabilityLinks(null, null, null));
        _ = mockClient.GetSystemHealthAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SystemHealthReport?>(health));

        DashboardRefreshService service = new(mockClient, NullLogger<DashboardRefreshService>.Instance);
        DashboardData? receivedData = null;
        service.OnDataChanged += data => receivedData = data;

        // Act
        await service.TriggerImmediateRefreshAsync().ConfigureAwait(true);

        // Assert
        receivedData.ShouldNotBeNull();
        receivedData.Health.ShouldNotBeNull();
        receivedData.Health.TotalEventCount.ShouldBe(100);

        await service.DisposeAsync().ConfigureAwait(true);
    }

    [Fact]
    public async Task TriggerImmediateRefresh_OnError_FiresOnErrorEvent()
    {
        // Arrange
        AdminStreamApiClient mockClient = Substitute.For<AdminStreamApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminStreamApiClient>.Instance);
        _ = mockClient.GetSystemHealthAsync(Arg.Any<CancellationToken>())
            .Returns<SystemHealthReport?>(_ => throw new HttpRequestException("timeout"));

        DashboardRefreshService service = new(mockClient, NullLogger<DashboardRefreshService>.Instance);
        Exception? receivedError = null;
        service.OnError += ex => receivedError = ex;

        // Act
        await service.TriggerImmediateRefreshAsync().ConfigureAwait(true);

        // Assert
        receivedError.ShouldNotBeNull();
        receivedError.ShouldBeOfType<HttpRequestException>();

        await service.DisposeAsync().ConfigureAwait(true);
    }

    [Fact]
    public async Task Dispose_CleansUpWithoutError()
    {
        // Arrange
        AdminStreamApiClient mockClient = Substitute.For<AdminStreamApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminStreamApiClient>.Instance);
        DashboardRefreshService service = new(mockClient, NullLogger<DashboardRefreshService>.Instance);

        // Act & Assert — should not throw
        await service.DisposeAsync().ConfigureAwait(true);
    }
}
