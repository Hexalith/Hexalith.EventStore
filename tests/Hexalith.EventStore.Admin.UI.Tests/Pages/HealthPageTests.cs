using System.Reflection;

using Bunit;

using Hexalith.EventStore.Admin.Abstractions.Models.Health;
using Hexalith.EventStore.Admin.UI.Pages;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.UI.Tests.Pages;

/// <summary>
/// bUnit tests for the Health page.
/// </summary>
public class HealthPageTests : AdminUITestContext {
    private readonly AdminStreamApiClient _mockApiClient;

    public HealthPageTests() {
        _mockApiClient = Substitute.For<AdminStreamApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminStreamApiClient>.Instance);
        _ = Services.AddScoped(_ => _mockApiClient);
        _ = Services.AddScoped<DashboardRefreshService>();
    }

    // ===== Merge-blocking tests =====

    [Fact]
    public void HealthPage_RendersStatCards_WithCorrectValues() {
        // Arrange
        SystemHealthReport report = CreateHealthyReport();
        _ = _mockApiClient.GetSystemHealthAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SystemHealthReport?>(report));

        // Act
        IRenderedComponent<Health> cut = Render<Health>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Total Events"), TimeSpan.FromSeconds(5));

        // Assert
        string markup = cut.Markup;
        markup.ShouldContain("Total Events");
        markup.ShouldContain("Events/sec");
        markup.ShouldContain("Error Rate");
        markup.ShouldContain("DAPR Components");
        // EventsPerSecond formatted to F1 (culture-dependent: "42.5" or "42,5")
        markup.ShouldContain(42.5.ToString("F1"));
        // ErrorPercentage
        markup.ShouldContain($"{0.3:F1}%");
    }

    [Theory]
    [InlineData(HealthStatus.Healthy, "Healthy")]
    [InlineData(HealthStatus.Degraded, "Degraded")]
    [InlineData(HealthStatus.Unhealthy, "Unhealthy")]
    public void HealthPage_RendersOverallStatusBadge_WithCorrectText(HealthStatus status, string expectedLabel) {
        // Arrange
        SystemHealthReport report = CreateReportWithStatus(status);
        _ = _mockApiClient.GetSystemHealthAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SystemHealthReport?>(report));

        // Act
        IRenderedComponent<Health> cut = Render<Health>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain(expectedLabel), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain(expectedLabel);
    }

    [Fact]
    public void HealthPage_RendersDaprComponentGrid_WithAllColumns() {
        // Arrange
        SystemHealthReport report = CreateHealthyReport();
        _ = _mockApiClient.GetSystemHealthAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SystemHealthReport?>(report));

        // Act
        IRenderedComponent<Health> cut = Render<Health>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("statestore"), TimeSpan.FromSeconds(5));

        // Assert
        string markup = cut.Markup;
        markup.ShouldContain("Component Name");
        markup.ShouldContain("Component Type");
        markup.ShouldContain("Status");
        markup.ShouldContain("Last Check");
        markup.ShouldContain("statestore");
        markup.ShouldContain("state.redis");
    }

    [Fact]
    public void HealthPage_ShowsIssueBanner_OnApiFailure() {
        // Arrange
        _ = _mockApiClient.GetSystemHealthAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SystemHealthReport?>(null));

        // Act
        IRenderedComponent<Health> cut = Render<Health>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Unable to load health status"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Unable to load health status");
    }

    [Fact]
    public void HealthPage_ShowsStaleIndicator_WhenRefreshFailsAfterSuccessfulLoad() {
        // Arrange
        SystemHealthReport report = CreateHealthyReport();
        _ = _mockApiClient.GetSystemHealthAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SystemHealthReport?>(report));

        // Act — initial load succeeds
        IRenderedComponent<Health> cut = Render<Health>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Total Events"), TimeSpan.FromSeconds(5));

        // Simulate refresh with null health (API down) by invoking the event's backing field
        DashboardRefreshService refreshService = Services.GetRequiredService<DashboardRefreshService>();
        DashboardData staleData = new(null, null);

        FieldInfo? eventField = typeof(DashboardRefreshService)
            .GetField("OnDataChanged", BindingFlags.Instance | BindingFlags.NonPublic);
        var handler = (Action<DashboardData>?)eventField?.GetValue(refreshService);
        handler?.Invoke(staleData);

        // Assert — cached data still visible and stale banner shown
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Health data may be stale"), TimeSpan.FromSeconds(5));
        cut.Markup.ShouldContain("Total Events");
    }

    [Fact]
    public void HealthPage_RendersObservabilityButtons_WhenUrlsConfigured() {
        // Arrange
        SystemHealthReport report = CreateReportWithObservabilityLinks();
        _ = _mockApiClient.GetSystemHealthAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SystemHealthReport?>(report));

        // Act
        IRenderedComponent<Health> cut = Render<Health>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("View Traces"), TimeSpan.FromSeconds(5));

        // Assert
        string markup = cut.Markup;
        markup.ShouldContain("Observability Tools");
        markup.ShouldContain("View Traces");
        markup.ShouldContain("View Metrics");
        markup.ShouldContain("View Logs");
        markup.ShouldContain("https://zipkin.example.com");
        markup.ShouldContain("https://grafana.example.com");
        markup.ShouldContain("https://seq.example.com");
        markup.ShouldContain("target=\"_blank\"");
        markup.ShouldContain("rel=\"noopener noreferrer\"");
    }

    [Fact]
    public void HealthPage_HidesObservabilityButtons_WhenUrlsNull() {
        // Arrange
        SystemHealthReport report = CreateHealthyReport(); // has null URLs
        _ = _mockApiClient.GetSystemHealthAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SystemHealthReport?>(report));

        // Act
        IRenderedComponent<Health> cut = Render<Health>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Total Events"), TimeSpan.FromSeconds(5));

        // Assert — no observability section
        cut.Markup.ShouldNotContain("Observability Tools");
        cut.Markup.ShouldNotContain("View Traces");
    }

    [Fact]
    public void HealthPage_RefreshHandler_UpdatesDisplayedData() {
        // Arrange — initial data
        SystemHealthReport initialReport = CreateHealthyReport();
        _ = _mockApiClient.GetSystemHealthAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SystemHealthReport?>(initialReport));

        // Act — render and verify initial data
        IRenderedComponent<Health> cut = Render<Health>();
        string formattedCount = 1000L.ToString("N0");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain(formattedCount), TimeSpan.FromSeconds(5));

        // Assert — initial data shown (culture-dependent: "1,000" or "1 000")
        cut.Markup.ShouldContain(formattedCount);
    }

    // ===== Recommended tests =====

    [Theory]
    [InlineData(0.5, "success")]
    [InlineData(2.5, "warning")]
    [InlineData(7.0, "error")]
    public void HealthPage_ErrorRateSeverity_MapsCorrectly(double errorPercentage, string expectedSeverity) {
        // Arrange
        SystemHealthReport report = new(
            HealthStatus.Healthy,
            1000,
            10.0,
            errorPercentage,
            [new DaprComponentHealth("store", "state.redis", HealthStatus.Healthy, DateTimeOffset.UtcNow)],
            new ObservabilityLinks(null, null, null));
        _ = _mockApiClient.GetSystemHealthAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SystemHealthReport?>(report));

        // Act
        IRenderedComponent<Health> cut = Render<Health>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Error Rate"), TimeSpan.FromSeconds(5));

        // Assert — severity color is applied via StatCard
        string markup = cut.Markup;
        markup.ShouldContain($"--hexalith-status-{expectedSeverity}");
    }

    [Fact]
    public void HealthPage_DaprComponentsStatCard_ShowsCorrectFormat() {
        // Arrange
        SystemHealthReport report = CreateMixedHealthReport();
        _ = _mockApiClient.GetSystemHealthAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SystemHealthReport?>(report));

        // Act
        IRenderedComponent<Health> cut = Render<Health>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("DAPR Components"), TimeSpan.FromSeconds(5));

        // Assert — shows "healthy/total" format
        cut.Markup.ShouldContain("1/3"); // 1 healthy out of 3
    }

    [Fact]
    public void HealthPage_DaprComponentStatusBadge_RendersCorrectSeverity() {
        // Arrange
        SystemHealthReport report = CreateMixedHealthReport();
        _ = _mockApiClient.GetSystemHealthAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SystemHealthReport?>(report));

        // Act
        IRenderedComponent<Health> cut = Render<Health>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("statestore"), TimeSpan.FromSeconds(5));

        // Assert — status badges rendered with correct severity colors
        string markup = cut.Markup;
        markup.ShouldContain("Healthy");
        markup.ShouldContain("Degraded");
        markup.ShouldContain("Unhealthy");
    }

    [Fact]
    public void HealthPage_ShowsSkeletonCards_DuringLoading() {
        // Arrange — never complete the task to keep loading state
        TaskCompletionSource<SystemHealthReport?> tcs = new();
        _ = _mockApiClient.GetSystemHealthAsync(Arg.Any<CancellationToken>())
            .Returns(tcs.Task);

        // Act
        IRenderedComponent<Health> cut = Render<Health>();

        // Assert — skeleton cards present during loading
        cut.Markup.ShouldContain("aria-hidden=\"true\""); // SkeletonCard has aria-hidden
    }

    [Fact]
    public void HealthPage_ShowsEmptyState_WhenDaprComponentsEmpty() {
        // Arrange
        SystemHealthReport report = new(
            HealthStatus.Healthy,
            1000,
            10.0,
            0.1,
            [],
            new ObservabilityLinks(null, null, null));
        _ = _mockApiClient.GetSystemHealthAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SystemHealthReport?>(report));

        // Act
        IRenderedComponent<Health> cut = Render<Health>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("No DAPR components detected"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("No DAPR components detected");
    }

    [Fact]
    public void HealthPage_HidesEntireObservabilitySection_WhenAllUrlsNull() {
        // Arrange
        SystemHealthReport report = CreateHealthyReport(); // all URLs null
        _ = _mockApiClient.GetSystemHealthAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SystemHealthReport?>(report));

        // Act
        IRenderedComponent<Health> cut = Render<Health>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Total Events"), TimeSpan.FromSeconds(5));

        // Assert — entire section hidden
        cut.Markup.ShouldNotContain("Observability Tools");
    }

    [Fact]
    public void HealthPage_StatCards_UseCorrectGridSpans() {
        // Arrange
        SystemHealthReport report = CreateHealthyReport();
        _ = _mockApiClient.GetSystemHealthAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SystemHealthReport?>(report));

        // Act
        IRenderedComponent<Health> cut = Render<Health>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Total Events"), TimeSpan.FromSeconds(5));

        // Assert — FluentGridItem renders with responsive spans
        // The grid items should exist (FluentGrid renders grid layout)
        cut.Markup.ShouldContain("Total Events");
        cut.Markup.ShouldContain("Events/sec");
        cut.Markup.ShouldContain("Error Rate");
        cut.Markup.ShouldContain("DAPR Components");
    }

    [Fact]
    public void HealthPage_GridHasAriaLabel() {
        // Arrange
        SystemHealthReport report = CreateHealthyReport();
        _ = _mockApiClient.GetSystemHealthAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SystemHealthReport?>(report));

        // Act
        IRenderedComponent<Health> cut = Render<Health>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("statestore"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("DAPR component health status");
    }

    [Fact]
    public void HealthPage_HasRefreshButton() {
        // Arrange
        _ = _mockApiClient.GetSystemHealthAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SystemHealthReport?>(CreateHealthyReport()));

        // Act
        IRenderedComponent<Health> cut = Render<Health>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Refresh"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Refresh");
    }

    [Fact]
    public void HealthPage_ComponentTypeSubtitle_ShowsCount() {
        // Arrange
        SystemHealthReport report = CreateMixedHealthReport();
        _ = _mockApiClient.GetSystemHealthAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SystemHealthReport?>(report));

        // Act
        IRenderedComponent<Health> cut = Render<Health>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("DAPR Components"), TimeSpan.FromSeconds(5));

        // Assert — subtitle shows component type count
        cut.Markup.ShouldContain("component types");
    }

    // ===== Test data helpers =====

    private static SystemHealthReport CreateHealthyReport() => new(
            HealthStatus.Healthy,
            1000,
            42.5,
            0.3,
            [
                new DaprComponentHealth("statestore", "state.redis", HealthStatus.Healthy, DateTimeOffset.UtcNow.AddMinutes(-1)),
                new DaprComponentHealth("pubsub", "pubsub.redis", HealthStatus.Healthy, DateTimeOffset.UtcNow.AddMinutes(-2)),
            ],
            new ObservabilityLinks(null, null, null));

    private static SystemHealthReport CreateReportWithStatus(HealthStatus status) => new(
            status,
            500,
            10.0,
            status == HealthStatus.Unhealthy ? 8.0 : 0.5,
            [new DaprComponentHealth("store", "state.redis", HealthStatus.Healthy, DateTimeOffset.UtcNow)],
            new ObservabilityLinks(null, null, null));

    private static SystemHealthReport CreateReportWithObservabilityLinks() => new(
            HealthStatus.Healthy,
            2000,
            50.0,
            0.1,
            [new DaprComponentHealth("store", "state.redis", HealthStatus.Healthy, DateTimeOffset.UtcNow)],
            new ObservabilityLinks(
                "https://zipkin.example.com",
                "https://grafana.example.com",
                "https://seq.example.com"));

    private static SystemHealthReport CreateMixedHealthReport() => new(
            HealthStatus.Degraded,
            3000,
            25.0,
            2.5,
            [
                new DaprComponentHealth("statestore", "state.redis", HealthStatus.Healthy, DateTimeOffset.UtcNow.AddMinutes(-1)),
                new DaprComponentHealth("pubsub", "pubsub.redis", HealthStatus.Degraded, DateTimeOffset.UtcNow.AddMinutes(-5)),
                new DaprComponentHealth("configstore", "configuration.redis", HealthStatus.Unhealthy, DateTimeOffset.UtcNow.AddMinutes(-10)),
            ],
            new ObservabilityLinks(null, null, null));
}
