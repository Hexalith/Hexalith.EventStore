using Bunit;

using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;
using Hexalith.EventStore.Admin.Abstractions.Models.Health;
using Hexalith.EventStore.Admin.UI.Pages;
using Hexalith.EventStore.Admin.UI.Services;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.UI.Tests.Pages;

/// <summary>
/// bUnit tests for the DaprHealthHistory page.
/// </summary>
public class DaprHealthHistoryPageTests : AdminUITestContext
{
    private readonly AdminHealthHistoryApiClient _mockClient;

    public DaprHealthHistoryPageTests()
    {
        _mockClient = Substitute.For<AdminHealthHistoryApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminHealthHistoryApiClient>.Instance);
        Services.AddScoped(_ => _mockClient);
    }

    [Fact]
    public void HealthHistoryPage_RendersTitle()
    {
        // Arrange
        SetupSuccessfulResponse();

        // Act
        IRenderedComponent<DaprHealthHistory> cut = Render<DaprHealthHistory>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("DAPR Health History"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("DAPR Health History");
    }

    [Fact]
    public void HealthHistoryPage_RendersBackLink()
    {
        // Arrange
        SetupSuccessfulResponse();

        // Act
        IRenderedComponent<DaprHealthHistory> cut = Render<DaprHealthHistory>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("DAPR Infrastructure"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("DAPR Infrastructure");
    }

    [Fact]
    public void HealthHistoryPage_RendersStatCards_WithData()
    {
        // Arrange
        SetupSuccessfulResponse();

        // Act
        IRenderedComponent<DaprHealthHistory> cut = Render<DaprHealthHistory>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Total Components"), TimeSpan.FromSeconds(5));

        // Assert
        string markup = cut.Markup;
        markup.ShouldContain("Total Components");
        markup.ShouldContain("Currently Healthy");
        markup.ShouldContain("Status Changes");
        markup.ShouldContain("Uptime %");
    }

    [Fact]
    public void HealthHistoryPage_RendersHeatmap_WhenDataAvailable()
    {
        // Arrange
        SetupSuccessfulResponse();

        // Act
        IRenderedComponent<DaprHealthHistory> cut = Render<DaprHealthHistory>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Timeline Heatmap"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Timeline Heatmap");
        cut.Markup.ShouldContain("statestore");
    }

    [Fact]
    public void HealthHistoryPage_RendersEmptyState_WhenNoData()
    {
        // Arrange
        _ = _mockClient.GetHealthHistoryAsync(
            Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DaprComponentHealthTimeline?>(
                new DaprComponentHealthTimeline([], HasData: false)));

        // Act
        IRenderedComponent<DaprHealthHistory> cut = Render<DaprHealthHistory>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("No health history available yet"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("No health history available yet");
    }

    [Fact]
    public void HealthHistoryPage_RendersTruncationWarning_WhenIsTruncated()
    {
        // Arrange
        var timeline = new DaprComponentHealthTimeline(
            [new DaprHealthHistoryEntry("statestore", "state.redis", HealthStatus.Healthy, DateTimeOffset.UtcNow)],
            HasData: true,
            IsTruncated: true);
        _ = _mockClient.GetHealthHistoryAsync(
            Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DaprComponentHealthTimeline?>(timeline));

        // Act
        IRenderedComponent<DaprHealthHistory> cut = Render<DaprHealthHistory>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Results truncated"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Results truncated");
    }

    [Fact]
    public void HealthHistoryPage_RendersTimeRangeButtons()
    {
        // Arrange
        SetupSuccessfulResponse();

        // Act
        IRenderedComponent<DaprHealthHistory> cut = Render<DaprHealthHistory>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("1 Hour"), TimeSpan.FromSeconds(5));

        // Assert
        string markup = cut.Markup;
        markup.ShouldContain("1 Hour");
        markup.ShouldContain("6 Hours");
        markup.ShouldContain("24 Hours");
        markup.ShouldContain("3 Days");
        markup.ShouldContain("7 Days");
    }

    [Fact]
    public void HealthHistoryPage_RendersTransitionLog_WithData()
    {
        // Arrange
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var timeline = new DaprComponentHealthTimeline(
            [
                new DaprHealthHistoryEntry("statestore", "state.redis", HealthStatus.Healthy, now.AddMinutes(-30)),
                new DaprHealthHistoryEntry("statestore", "state.redis", HealthStatus.Degraded, now.AddMinutes(-15)),
            ],
            HasData: true);
        _ = _mockClient.GetHealthHistoryAsync(
            Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DaprComponentHealthTimeline?>(timeline));

        // Act
        IRenderedComponent<DaprHealthHistory> cut = Render<DaprHealthHistory>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Status Transitions"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Status Transitions");
    }

    [Fact]
    public void HealthHistoryPage_RendersIssueBanner_WhenApiUnavailable()
    {
        // Arrange
        _ = _mockClient.GetHealthHistoryAsync(
            Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns<DaprComponentHealthTimeline?>(_ => throw new InvalidOperationException("API down"));

        // Act
        IRenderedComponent<DaprHealthHistory> cut = Render<DaprHealthHistory>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Unable to retrieve health history"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Unable to retrieve health history");
    }

    [Fact]
    public void HealthHistoryPage_RendersRefreshButton()
    {
        // Arrange
        SetupSuccessfulResponse();

        // Act
        IRenderedComponent<DaprHealthHistory> cut = Render<DaprHealthHistory>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Refresh"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Refresh");
    }

    // ===== Helper methods =====

    private void SetupSuccessfulResponse()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var timeline = new DaprComponentHealthTimeline(
            [
                new DaprHealthHistoryEntry("statestore", "state.redis", HealthStatus.Healthy, now.AddMinutes(-30)),
                new DaprHealthHistoryEntry("pubsub", "pubsub.redis", HealthStatus.Healthy, now.AddMinutes(-30)),
            ],
            HasData: true);
        _ = _mockClient.GetHealthHistoryAsync(
            Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DaprComponentHealthTimeline?>(timeline));
    }
}
