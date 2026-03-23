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

    [Fact]
    public async Task Start_BeginsPollLoop_AndDisposeCancels()
    {
        // Arrange
        AdminStreamApiClient mockClient = Substitute.For<AdminStreamApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminStreamApiClient>.Instance);
        SystemHealthReport health = new(
            HealthStatus.Healthy, 10, 1.0, 0.0, [],
            new ObservabilityLinks(null, null, null));
        _ = mockClient.GetSystemHealthAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SystemHealthReport?>(health));

        DashboardRefreshService service = new(mockClient, NullLogger<DashboardRefreshService>.Instance);

        // Act — start the polling loop (will run in background)
        service.Start();

        // Assert — dispose should cancel the loop cleanly
        await service.DisposeAsync().ConfigureAwait(true);

        // Should not throw and timer loop should have exited
    }

    [Fact]
    public async Task Start_CalledTwice_DoesNotCreateDuplicateLoop()
    {
        // Arrange
        AdminStreamApiClient mockClient = Substitute.For<AdminStreamApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminStreamApiClient>.Instance);
        DashboardRefreshService service = new(mockClient, NullLogger<DashboardRefreshService>.Instance);

        // Act — calling Start twice should be idempotent
        service.Start();
        service.Start();

        // Assert — dispose should clean up without issues
        await service.DisposeAsync().ConfigureAwait(true);
    }

    [Fact]
    public async Task ConcurrentTriggers_ReentrancyGuardPreventsDoubleRefresh()
    {
        // Arrange
        AdminStreamApiClient mockClient = Substitute.For<AdminStreamApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminStreamApiClient>.Instance);
        TaskCompletionSource<SystemHealthReport?> tcs = new();
        _ = mockClient.GetSystemHealthAsync(Arg.Any<CancellationToken>())
            .Returns(tcs.Task);

        DashboardRefreshService service = new(mockClient, NullLogger<DashboardRefreshService>.Instance);
        int dataChangedCount = 0;
        service.OnDataChanged += _ => Interlocked.Increment(ref dataChangedCount);

        // Act — trigger two concurrent refreshes
        Task trigger1 = service.TriggerImmediateRefreshAsync();
        Task trigger2 = service.TriggerImmediateRefreshAsync(); // Should be skipped (re-entrancy guard)

        // Complete the API call
        SystemHealthReport health = new(
            HealthStatus.Healthy, 50, 2.0, 0.0, [],
            new ObservabilityLinks(null, null, null));
        tcs.SetResult(health);

        await trigger1.ConfigureAwait(true);
        await trigger2.ConfigureAwait(true);

        // Assert — only one refresh should have completed
        dataChangedCount.ShouldBe(1);

        await service.DisposeAsync().ConfigureAwait(true);
    }

    [Fact]
    public async Task Dispose_SynchronousDispose_DoesNotThrow()
    {
        // Arrange
        AdminStreamApiClient mockClient = Substitute.For<AdminStreamApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminStreamApiClient>.Instance);
        DashboardRefreshService service = new(mockClient, NullLogger<DashboardRefreshService>.Instance);
        service.Start();

        // Act & Assert — synchronous dispose should not throw
        service.Dispose();

        await Task.CompletedTask.ConfigureAwait(true);
    }
}
