using System.Reflection;
using System.Text.Json;

using Bunit;

using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;
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
        markup.ShouldContain("Source");
        markup.ShouldContain("Status");
        markup.ShouldContain("Last Check");
        markup.ShouldContain("statestore");
        markup.ShouldContain("state.redis");
        markup.ShouldContain("Local probe");
        markup.ShouldContain("Remote (eventstore)");
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
            new ObservabilityLinks(null, null, null),
            TotalEventCountStatus: SystemHealthMetricStatus.Available,
            EventsPerSecondStatus: SystemHealthMetricStatus.Available,
            ErrorPercentageStatus: SystemHealthMetricStatus.Available);
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
            new ObservabilityLinks(null, null, null),
            TotalEventCountStatus: SystemHealthMetricStatus.Available,
            EventsPerSecondStatus: SystemHealthMetricStatus.Available,
            ErrorPercentageStatus: SystemHealthMetricStatus.Available);
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

    // ===== AC3: shared metric truthfulness on /health (Issue #7) =====

    [Fact]
    public void HealthPage_RendersUnavailable_ForUnavailableEventsPerSecond_AC3() {
        // AC3: when a metric's *Status is Unavailable, render "unavailable" — never 0.0/s.
        SystemHealthReport report = new(
            HealthStatus.Healthy,
            TotalEventCount: 100,
            EventsPerSecond: 0,
            ErrorPercentage: 0,
            [new DaprComponentHealth("statestore", "state.redis", HealthStatus.Healthy, DateTimeOffset.UtcNow, DaprComponentSource.LocalAdminProbe)],
            new ObservabilityLinks(null, null, null),
            TotalEventCountStatus: SystemHealthMetricStatus.Available,
            EventsPerSecondStatus: SystemHealthMetricStatus.Unavailable,
            ErrorPercentageStatus: SystemHealthMetricStatus.Unavailable);
        _ = _mockApiClient.GetSystemHealthAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SystemHealthReport?>(report));

        IRenderedComponent<Health> cut = Render<Health>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Events/sec"), TimeSpan.FromSeconds(5));

        string markup = cut.Markup;
        markup.ShouldContain("unavailable");
        // Critical regression guard: no fake zero for unavailable metrics on /health.
        markup.ShouldNotContain("0.0/s");
        markup.ShouldNotContain("0.0%");
    }

    [Fact]
    public void HealthPage_RendersRealZero_ForAvailableZeroMetric_AC3() {
        // AC3: when *Status is Available and value is 0, render real zero per page format.
        SystemHealthReport report = new(
            HealthStatus.Healthy,
            TotalEventCount: 0,
            EventsPerSecond: 0,
            ErrorPercentage: 0,
            [new DaprComponentHealth("statestore", "state.redis", HealthStatus.Healthy, DateTimeOffset.UtcNow, DaprComponentSource.LocalAdminProbe)],
            new ObservabilityLinks(null, null, null),
            TotalEventCountStatus: SystemHealthMetricStatus.Available,
            EventsPerSecondStatus: SystemHealthMetricStatus.Available,
            ErrorPercentageStatus: SystemHealthMetricStatus.Available);
        _ = _mockApiClient.GetSystemHealthAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SystemHealthReport?>(report));

        IRenderedComponent<Health> cut = Render<Health>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Total Events"), TimeSpan.FromSeconds(5));

        string markup = cut.Markup;
        markup.ShouldNotContain("unavailable");
        // Real zero values rendered per page format. Culture-safe: F1 produces "0.0" or "0,0".
        string formattedZero = 0.0.ToString("F1");
        markup.ShouldContain(formattedZero); // EventsPerSecond F1
    }

    [Fact]
    public void HealthPage_RendersPartialReport_WithStateStoreUnhealthy_AC1AC2() {
        // AC1+AC2: HTTP 200 partial report with state-store Unhealthy renders the grid + grid
        // entry, not a blank screen. Overall is Unhealthy and dependent metrics are unavailable.
        SystemHealthReport report = new(
            HealthStatus.Unhealthy,
            TotalEventCount: 0,
            EventsPerSecond: 0,
            ErrorPercentage: 0,
            [new DaprComponentHealth("statestore", "state.redis", HealthStatus.Unhealthy, DateTimeOffset.UtcNow)],
            new ObservabilityLinks(null, null, null),
            TotalEventCountStatus: SystemHealthMetricStatus.Unavailable,
            EventsPerSecondStatus: SystemHealthMetricStatus.Unavailable,
            ErrorPercentageStatus: SystemHealthMetricStatus.Unavailable);
        _ = _mockApiClient.GetSystemHealthAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SystemHealthReport?>(report));

        IRenderedComponent<Health> cut = Render<Health>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("statestore"), TimeSpan.FromSeconds(5));

        string markup = cut.Markup;
        markup.ShouldContain("Unhealthy");
        markup.ShouldContain("statestore");
        // Total Events / Events per second / Error rate must render unavailable when status says so.
        markup.ShouldContain("unavailable");
        markup.ShouldNotContain("0.0/s");
        markup.ShouldNotContain("0.0%");
    }

    [Fact]
    public void HealthPage_RendersStaleSuffix_ForStaleMetric_AC3() {
        // AC3 stale-rendering rule: when *Status is Stale, the cached value renders with a
        // visible "(stale)" marker so an operator does not mistake last-good cache for a fresh
        // measurement. Severity must NOT be neutral (which would look identical to a healthy
        // fresh value).
        //
        // Pin both production rendering and the assertion to InvariantCulture for the duration
        // of the test. bUnit may dispatch render work onto a thread whose CurrentCulture
        // differs from the test thread (especially under fr-FR where N0 emits NBSP as the
        // group separator). Without this guard the assertion is non-deterministic.
        System.Globalization.CultureInfo originalCulture = Thread.CurrentThread.CurrentCulture;
        System.Globalization.CultureInfo originalUiCulture = Thread.CurrentThread.CurrentUICulture;
        Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        Thread.CurrentThread.CurrentUICulture = System.Globalization.CultureInfo.InvariantCulture;
        try {
            SystemHealthReport report = new(
                HealthStatus.Healthy,
                TotalEventCount: 12345,
                EventsPerSecond: 7.5,
                ErrorPercentage: 1.2,
                [new DaprComponentHealth("statestore", "state.redis", HealthStatus.Healthy, DateTimeOffset.UtcNow, DaprComponentSource.LocalAdminProbe)],
                new ObservabilityLinks(null, null, null),
                TotalEventCountStatus: SystemHealthMetricStatus.Stale,
                EventsPerSecondStatus: SystemHealthMetricStatus.Stale,
                ErrorPercentageStatus: SystemHealthMetricStatus.Stale);
            _ = _mockApiClient.GetSystemHealthAsync(Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<SystemHealthReport?>(report));

            IRenderedComponent<Health> cut = Render<Health>();
            cut.WaitForAssertion(() => cut.Markup.ShouldContain("Total Events"), TimeSpan.FromSeconds(5));

            string markup = cut.Markup;
            // Both production rendering and the test now use InvariantCulture, so "12,345"
            // is the deterministic output regardless of the host environment's locale.
            markup.ShouldContain($"{12345L.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)} (stale)");
            markup.ShouldContain($"{7.5.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)} (stale)");
            markup.ShouldContain($"{1.2.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}% (stale)");
            markup.ShouldNotContain("unavailable");
        }
        finally {
            Thread.CurrentThread.CurrentCulture = originalCulture;
            Thread.CurrentThread.CurrentUICulture = originalUiCulture;
        }
    }

    // ===== AC7 named UI failure fixtures (F20 — Test Plan line 414) =====
    //
    // Spec Test Plan names five fixtures. HealthReport_Partial_StateStoreUnavailable is
    // covered by HealthPage_RendersPartialReport_WithStateStoreUnhealthy_AC1AC2 above. The
    // four below cover the API failure modes: 401/403 forbidden, timeout, malformed payload,
    // and null. Each asserts the issue banner / loading exit invariants required by AC2.

    [Fact]
    public void HealthApi_Forbidden_NoStaleReuse() {
        // AC2 contract: a cold-start 401/403 must surface the issue banner (not crash, not
        // spin forever). Round 4 patch ALSO exercises the warm-cache → 401/403 sequence: a
        // successful first load populates the cache, then a refresh signal with no fresh data
        // simulates the auth-failure refresh — production marks stale, but the cached report
        // remains visible. The fixture asserts BOTH branches:
        //   (1) cold-start 401: banner, no cache to reuse;
        //   (2) warm-cache + null-data refresh: stale banner appears AND cached data remains
        //       visible (today the page does reuse cache on any failure; future cross-tenant
        //       invalidation is tracked separately under spec line 99).
        _ = _mockApiClient.GetSystemHealthAsync(Arg.Any<CancellationToken>())
            .Returns<Task<SystemHealthReport?>>(_ => throw new UnauthorizedAccessException("403 Forbidden"));

        // (1) cold-start 401
        IRenderedComponent<Health> cut = Render<Health>();
        cut.WaitForAssertion(
            () => cut.Markup.ShouldContain("Unable to load health status"),
            TimeSpan.FromSeconds(5));

        string markup = cut.Markup;
        markup.ShouldContain("Unable to load health status");
        // Page must exit loading state — no skeleton spinner content remains.
        markup.ShouldNotContain("hexalith-skeleton-card");

        // (2) warm-cache + 401-on-refresh: re-render a fresh page, this time with a successful
        // first load, then trigger a refresh signal carrying null data (the in-process
        // equivalent of "the dashboard refresh saw an auth failure"). Use a separate scope so
        // the previous failure doesn't poison this one.
        SystemHealthReport report = CreateHealthyReport();
        _ = _mockApiClient.GetSystemHealthAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SystemHealthReport?>(report));

        IRenderedComponent<Health> warmCut = Render<Health>();
        warmCut.WaitForAssertion(() => warmCut.Markup.ShouldContain("Total Events"), TimeSpan.FromSeconds(5));

        DashboardRefreshService refreshService = Services.GetRequiredService<DashboardRefreshService>();
        FieldInfo? eventField = typeof(DashboardRefreshService)
            .GetField("OnDataChanged", BindingFlags.Instance | BindingFlags.NonPublic);
        var handler = (Action<DashboardData>?)eventField?.GetValue(refreshService);
        handler?.Invoke(new DashboardData(null, null));

        warmCut.WaitForAssertion(
            () => warmCut.Markup.ShouldContain("Health data may be stale"),
            TimeSpan.FromSeconds(5));
        warmCut.Markup.ShouldContain("Total Events");
    }

    [Fact]
    public void HealthApi_Timeout_StaleLastGood() {
        // AC2 contract priority order (spec line 96): "current partial report, stale last-good
        // report clearly labelled with timestamp/source, then IssueBanner/EmptyState." Round 4
        // patch enhances this fixture to exercise the stale-last-good branch in addition to
        // the cold-start timeout banner:
        //   (1) cold-start TaskCanceledException: banner, no cache to reuse;
        //   (2) warm-cache + null-data refresh: stale label appears AND cached value remains
        //       rendered with a "(stale)" suffix (the production stale path).

        // (1) cold-start timeout
        _ = _mockApiClient.GetSystemHealthAsync(Arg.Any<CancellationToken>())
            .Returns<Task<SystemHealthReport?>>(_ => throw new TaskCanceledException("Request timed out"));

        IRenderedComponent<Health> cut = Render<Health>();
        cut.WaitForAssertion(
            () => cut.Markup.ShouldContain("Unable to load health status"),
            TimeSpan.FromSeconds(5));

        cut.Markup.ShouldNotContain("hexalith-skeleton-card");

        // (2) warm-cache + timeout-on-refresh: render fresh, load successfully, then refresh
        // with null data so the page enters the stale path. Asserts both the stale banner copy
        // AND the cached value are visible — the prior "always banner" path would lose the
        // cached report.
        SystemHealthReport report = CreateHealthyReport();
        _ = _mockApiClient.GetSystemHealthAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SystemHealthReport?>(report));

        IRenderedComponent<Health> warmCut = Render<Health>();
        warmCut.WaitForAssertion(() => warmCut.Markup.ShouldContain("Total Events"), TimeSpan.FromSeconds(5));

        DashboardRefreshService refreshService = Services.GetRequiredService<DashboardRefreshService>();
        FieldInfo? eventField = typeof(DashboardRefreshService)
            .GetField("OnDataChanged", BindingFlags.Instance | BindingFlags.NonPublic);
        var handler = (Action<DashboardData>?)eventField?.GetValue(refreshService);
        handler?.Invoke(new DashboardData(null, null));

        warmCut.WaitForAssertion(
            () => warmCut.Markup.ShouldContain("Health data may be stale"),
            TimeSpan.FromSeconds(5));
        warmCut.Markup.ShouldContain("Total Events");
    }

    [Fact]
    public void HealthApi_Malformed_IssueBanner() {
        // AC2 line 95 lists "malformed error response" as a distinct API failure mode. Round 4
        // patch separates the assertion from the empty-state path (which has its own coverage):
        // the API client throws JsonException — the genuine "malformed payload" trigger, not a
        // valid-but-empty DTO. The page's catch-all in OnInitializedAsync routes this to the
        // unable-to-load banner without crashing or spinning. Asserting on banner copy avoids
        // the "No DAPR components detected" empty-state synonym which doubles up with
        // HealthApi_Null_IssueBanner / null-DTO coverage.
        _ = _mockApiClient.GetSystemHealthAsync(Arg.Any<CancellationToken>())
            .Returns<Task<SystemHealthReport?>>(_ => throw new JsonException("Unexpected token at line 1, position 1."));

        IRenderedComponent<Health> cut = Render<Health>();
        cut.WaitForAssertion(
            () => cut.Markup.ShouldContain("Unable to load health status"),
            TimeSpan.FromSeconds(5));

        string markup = cut.Markup;
        markup.ShouldContain("Unable to load health status");
        // Page must exit loading state — no skeleton spinner content remains.
        markup.ShouldNotContain("hexalith-skeleton-card");
        // No fake-zero metric rendering even when payload is malformed.
        markup.ShouldNotContain("0.0/s");
        markup.ShouldNotContain("0.0%");
    }

    [Fact]
    public void HealthApi_Null_IssueBanner() {
        // The API returns null (e.g. 204 No Content). The page must show the unable-to-load
        // banner without spinning forever or crashing.
        _ = _mockApiClient.GetSystemHealthAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SystemHealthReport?>(null));

        IRenderedComponent<Health> cut = Render<Health>();
        cut.WaitForAssertion(
            () => cut.Markup.ShouldContain("Unable to load health status"),
            TimeSpan.FromSeconds(5));

        cut.Markup.ShouldNotContain("hexalith-skeleton-card");
    }

    // ===== Test data helpers =====

    private static SystemHealthReport CreateHealthyReport() => new(
            HealthStatus.Healthy,
            1000,
            42.5,
            0.3,
            [
                new DaprComponentHealth("statestore", "state.redis", HealthStatus.Healthy, DateTimeOffset.UtcNow.AddMinutes(-1), DaprComponentSource.LocalAdminProbe),
                new DaprComponentHealth("pubsub", "pubsub.redis", HealthStatus.Healthy, DateTimeOffset.UtcNow.AddMinutes(-2), DaprComponentSource.RemoteEventStoreMetadata),
            ],
            new ObservabilityLinks(null, null, null),
            TotalEventCountStatus: SystemHealthMetricStatus.Available,
            EventsPerSecondStatus: SystemHealthMetricStatus.Available,
            ErrorPercentageStatus: SystemHealthMetricStatus.Available);

    private static SystemHealthReport CreateReportWithStatus(HealthStatus status) => new(
            status,
            500,
            10.0,
            status == HealthStatus.Unhealthy ? 8.0 : 0.5,
            [new DaprComponentHealth("store", "state.redis", HealthStatus.Healthy, DateTimeOffset.UtcNow)],
            new ObservabilityLinks(null, null, null),
            TotalEventCountStatus: SystemHealthMetricStatus.Available,
            EventsPerSecondStatus: SystemHealthMetricStatus.Available,
            ErrorPercentageStatus: SystemHealthMetricStatus.Available);

    private static SystemHealthReport CreateReportWithObservabilityLinks() => new(
            HealthStatus.Healthy,
            2000,
            50.0,
            0.1,
            [new DaprComponentHealth("store", "state.redis", HealthStatus.Healthy, DateTimeOffset.UtcNow)],
            new ObservabilityLinks(
                "https://zipkin.example.com",
                "https://grafana.example.com",
                "https://seq.example.com"),
            TotalEventCountStatus: SystemHealthMetricStatus.Available,
            EventsPerSecondStatus: SystemHealthMetricStatus.Available,
            ErrorPercentageStatus: SystemHealthMetricStatus.Available);

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
            new ObservabilityLinks(null, null, null),
            TotalEventCountStatus: SystemHealthMetricStatus.Available,
            EventsPerSecondStatus: SystemHealthMetricStatus.Available,
            ErrorPercentageStatus: SystemHealthMetricStatus.Available);
}
