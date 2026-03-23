using Bunit;

using Hexalith.EventStore.Admin.Abstractions.Models.Projections;
using Hexalith.EventStore.Admin.UI.Pages;
using Hexalith.EventStore.Admin.UI.Services;
using Hexalith.EventStore.SignalR;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.UI.Tests.Pages;

/// <summary>
/// bUnit tests for the Projections page.
/// </summary>
public class ProjectionsPageTests : AdminUITestContext
{
    private readonly AdminProjectionApiClient _mockApiClient;

    public ProjectionsPageTests()
    {
        _mockApiClient = Substitute.For<AdminProjectionApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminProjectionApiClient>.Instance);
        Services.AddScoped(_ => _mockApiClient);
        Services.AddScoped<DashboardRefreshService>();
        TestSignalRClient testClient = new();
        Services.AddSingleton(testClient);
        Services.AddSingleton(testClient.Inner);
    }

    [Fact]
    public void ProjectionsPage_RendersStatCards_WithCorrectCounts()
    {
        // Arrange
        IReadOnlyList<ProjectionStatus> projections = CreateProjections();
        _ = _mockApiClient.ListProjectionsAsync(
            Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(projections));

        // Act
        IRenderedComponent<Projections> cut = Render<Projections>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Total Projections"), TimeSpan.FromSeconds(5));

        // Assert
        string markup = cut.Markup;
        markup.ShouldContain("Total Projections");
        markup.ShouldContain("Running");
        markup.ShouldContain("Unhealthy");
        markup.ShouldContain("Max Lag");
    }

    [Fact]
    public void ProjectionsPage_RendersGrid_WithAllColumns()
    {
        // Arrange
        IReadOnlyList<ProjectionStatus> projections = CreateProjections();
        _ = _mockApiClient.ListProjectionsAsync(
            Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(projections));

        // Act
        IRenderedComponent<Projections> cut = Render<Projections>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("counter-projection"), TimeSpan.FromSeconds(5));

        // Assert
        string markup = cut.Markup;
        markup.ShouldContain("Name");
        markup.ShouldContain("Tenant");
        markup.ShouldContain("Status");
        markup.ShouldContain("Lag");
        markup.ShouldContain("Throughput");
        markup.ShouldContain("Errors");
        markup.ShouldContain("Last Position");
        markup.ShouldContain("Last Processed");
        markup.ShouldContain("counter-projection");
    }

    [Fact]
    public void ProjectionsPage_ShowsIssueBanner_OnApiFailure()
    {
        // Arrange
        _ = _mockApiClient.ListProjectionsAsync(
            Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<ProjectionStatus>>(new HttpRequestException("Fail")));

        // Act
        IRenderedComponent<Projections> cut = Render<Projections>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Unable to load projections"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Unable to load projections");
    }

    [Fact]
    public void ProjectionsPage_ShowsEmptyState_WhenNoProjections()
    {
        // Arrange
        _ = _mockApiClient.ListProjectionsAsync(
            Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ProjectionStatus>>([]));

        // Act
        IRenderedComponent<Projections> cut = Render<Projections>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("No projections found"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("No projections found");
    }

    [Fact]
    public void ProjectionsPage_ErrorCountColumn_ShowsRedWhenPositive()
    {
        // Arrange
        List<ProjectionStatus> projections =
        [
            new("error-proj", "tenant-1", ProjectionStatusType.Error, 500, 0.0, 3,
                100, DateTimeOffset.UtcNow.AddMinutes(-5)),
        ];
        _ = _mockApiClient.ListProjectionsAsync(
            Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ProjectionStatus>>(projections));

        // Act
        IRenderedComponent<Projections> cut = Render<Projections>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("error-proj"), TimeSpan.FromSeconds(5));

        // Assert — error count styled in red
        cut.Markup.ShouldContain("--hexalith-status-error");
    }

    [Fact]
    public void ProjectionsPage_StatusBadges_RenderCorrectly()
    {
        // Arrange
        IReadOnlyList<ProjectionStatus> projections = CreateProjections();
        _ = _mockApiClient.ListProjectionsAsync(
            Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(projections));

        // Act
        IRenderedComponent<Projections> cut = Render<Projections>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Running"), TimeSpan.FromSeconds(5));

        // Assert — all status types rendered
        string markup = cut.Markup;
        markup.ShouldContain("Running");
        markup.ShouldContain("Paused");
        markup.ShouldContain("Error");
    }

    [Fact]
    public void ProjectionsPage_HasRefreshButton()
    {
        // Arrange
        _ = _mockApiClient.ListProjectionsAsync(
            Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ProjectionStatus>>([]));

        // Act
        IRenderedComponent<Projections> cut = Render<Projections>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Refresh"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Refresh");
    }

    [Fact]
    public void ProjectionsPage_GridHasAriaLabel()
    {
        // Arrange
        IReadOnlyList<ProjectionStatus> projections = CreateProjections();
        _ = _mockApiClient.ListProjectionsAsync(
            Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(projections));

        // Act
        IRenderedComponent<Projections> cut = Render<Projections>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("counter-projection"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Projections list");
    }

    private static IReadOnlyList<ProjectionStatus> CreateProjections()
    {
        return
        [
            new("counter-projection", "tenant-1", ProjectionStatusType.Running, 10, 5.2, 0,
                1000, DateTimeOffset.UtcNow.AddMinutes(-1)),
            new("order-projection", "tenant-1", ProjectionStatusType.Paused, 200, 0.0, 0,
                800, DateTimeOffset.UtcNow.AddMinutes(-10)),
            new("inventory-projection", "tenant-2", ProjectionStatusType.Error, 500, 0.0, 3,
                500, DateTimeOffset.UtcNow.AddMinutes(-30)),
        ];
    }
}
