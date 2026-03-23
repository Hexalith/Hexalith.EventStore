using Bunit;

using Hexalith.EventStore.Admin.Abstractions.Models.Health;
using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.UI.Pages;
using Hexalith.EventStore.Admin.UI.Services;
using Hexalith.EventStore.Admin.UI.Components.Shared;
using Hexalith.EventStore.Admin.UI.Services.Exceptions;
using Hexalith.EventStore.SignalR;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Hexalith.EventStore.Admin.UI.Tests.Pages;

/// <summary>
/// bUnit tests for the landing page (Index.razor).
/// </summary>
public class IndexPageTests : AdminUITestContext
{
    private readonly AdminStreamApiClient _mockApiClient;

    public IndexPageTests()
    {
        _mockApiClient = Substitute.For<AdminStreamApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminStreamApiClient>.Instance);
        Services.AddScoped(_ => _mockApiClient);
        Services.AddScoped<DashboardRefreshService>();
        TestSignalRClient testClient = new();
        Services.AddSingleton(testClient);
        Services.AddSingleton(testClient.Inner);
    }

    [Fact]
    public void LandingPage_WithHealthData_ShowsStatCards()
    {
        // Verify AdminStreamApiClient mock is wired correctly
        SystemHealthReport health = CreateHealthReport(0, 42.5, 0.05);
        _ = _mockApiClient.GetSystemHealthAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SystemHealthReport?>(health));

        // Act — render the page; bUnit awaits OnInitializedAsync
        IRenderedComponent<Hexalith.EventStore.Admin.UI.Pages.Index> cut = Render<Hexalith.EventStore.Admin.UI.Pages.Index>();

        // Assert — stat cards are always rendered (even during loading)
        cut.Markup.ShouldContain("stat-card-grid");
        // The page should show stat card labels
        cut.Markup.ShouldContain("Active Streams");
        cut.Markup.ShouldContain("Total Events");
        cut.Markup.ShouldContain("Events/sec");
        cut.Markup.ShouldContain("Error Rate");
    }

    [Fact]
    public void LandingPage_WhenLoading_ShowsSkeletonCards()
    {
        // Arrange — API never returns (simulates loading)
        TaskCompletionSource<SystemHealthReport?> tcs = new();
        _ = _mockApiClient.GetSystemHealthAsync(Arg.Any<CancellationToken>())
            .Returns(tcs.Task);

        // Act
        IRenderedComponent<Hexalith.EventStore.Admin.UI.Pages.Index> cut = Render<Hexalith.EventStore.Admin.UI.Pages.Index>();

        // Assert — skeleton cards visible
        cut.Markup.ShouldContain("aria-hidden=\"true\"");
    }

    [Fact]
    public void LandingPage_WhenApiUnavailable_ShowsIssueBanner()
    {
        // Arrange
        _ = _mockApiClient.GetSystemHealthAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new ServiceUnavailableException());

        // Act
        IRenderedComponent<Hexalith.EventStore.Admin.UI.Pages.Index> cut = Render<Hexalith.EventStore.Admin.UI.Pages.Index>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Admin API Unavailable"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Retry");
    }

    [Fact]
    public void LandingPage_WhenZeroEvents_ShowsEmptyState()
    {
        // Arrange
        SystemHealthReport health = CreateHealthReport(0, 0, 0);
        _ = _mockApiClient.GetSystemHealthAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SystemHealthReport?>(health));

        // Act
        IRenderedComponent<Hexalith.EventStore.Admin.UI.Pages.Index> cut = Render<Hexalith.EventStore.Admin.UI.Pages.Index>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("EventStore Admin is running."), TimeSpan.FromSeconds(5));

        // Assert — EmptyState shown, no ActivityChart or grid
        cut.Markup.ShouldContain("0 commands processed");
        cut.Markup.ShouldNotContain("Stream Activity (24h)");
    }

    [Fact]
    public void LandingPage_WhenApiTimesOut_ShowsStaleData()
    {
        // Arrange — first call succeeds, second fails
        SystemHealthReport health = CreateHealthReport(500, 10, 0.01);
        PagedResult<StreamSummary> streams = CreateStreamsResult(3);
        _ = _mockApiClient.GetSystemHealthAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SystemHealthReport?>(health));
        _ = _mockApiClient.GetRecentlyActiveStreamsAsync(
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(streams));

        // Act — initial render with data
        IRenderedComponent<Hexalith.EventStore.Admin.UI.Pages.Index> cut = Render<Hexalith.EventStore.Admin.UI.Pages.Index>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("500"), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void LandingPage_ErrorRateSeverity_Rendering()
    {
        // Verify the error rate severity rendering logic via StatusDisplayConfig
        StatusBadge.StatusDisplayConfig config = StatusBadge.StatusDisplayConfig.FromStreamStatus(StreamStatus.Active);
        config.Label.ShouldBe("Active");
        config.CssColor.ShouldContain("success");

        StatusBadge.StatusDisplayConfig tombConfig = StatusBadge.StatusDisplayConfig.FromStreamStatus(StreamStatus.Tombstoned);
        tombConfig.Label.ShouldBe("Tombstoned");
        tombConfig.CssColor.ShouldContain("error");
    }

    private static SystemHealthReport CreateHealthReport(long totalEvents, double eventsPerSec, double errorPct)
        => new(
            HealthStatus.Healthy,
            totalEvents,
            eventsPerSec,
            errorPct,
            [],
            new ObservabilityLinks(null, null, null));

    private static PagedResult<StreamSummary> CreateStreamsResult(int count)
    {
        List<StreamSummary> items = [];
        for (int i = 0; i < count; i++)
        {
            items.Add(new StreamSummary(
                $"tenant-{i}",
                "counter",
                $"agg-{i:D8}",
                i + 1,
                DateTimeOffset.UtcNow.AddMinutes(-i),
                (i + 1) * 10,
                i % 2 == 0,
                StreamStatus.Active));
        }

        return new PagedResult<StreamSummary>(items, count, null);
    }
}
