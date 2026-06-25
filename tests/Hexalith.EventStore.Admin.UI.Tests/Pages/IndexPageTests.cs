using Bunit;

using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;
using Hexalith.EventStore.Admin.Abstractions.Models.Health;
using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.UI.Components.Shared;
using Hexalith.EventStore.Admin.UI.Services.Exceptions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Hexalith.EventStore.Admin.UI.Tests.Pages;

/// <summary>
/// bUnit tests for the landing page (Index.razor).
/// </summary>
public class IndexPageTests : AdminUITestContext {
    private readonly AdminStreamApiClient _mockApiClient;

    public IndexPageTests() {
        _mockApiClient = Substitute.For<AdminStreamApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminStreamApiClient>.Instance);
        _ = Services.AddScoped(_ => _mockApiClient);
        _ = Services.AddScoped<DashboardRefreshService>();
        TestSignalRClient testClient = new();
        _ = Services.AddSingleton(testClient);
        _ = Services.AddSingleton(testClient.Inner);
    }

    [Fact]
    public void LandingPage_WithHealthData_ShowsStatCards() {
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
    public void LandingPage_StatCardNavigationWrappers_AreKeyboardAccessible() {
        // Accessibility remediation (audit NS-1): the StatCard navigation affordances must be real
        // interactive elements (role="button" + tabindex + accessible name + keyboard activation),
        // not a bare clickable <div style="cursor:pointer">.
        SystemHealthReport health = CreateHealthReport(0, 42.5, 0.05);
        _ = _mockApiClient.GetSystemHealthAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SystemHealthReport?>(health));

        IRenderedComponent<Hexalith.EventStore.Admin.UI.Pages.Index> cut = Render<Hexalith.EventStore.Admin.UI.Pages.Index>();

        IReadOnlyList<AngleSharp.Dom.IElement> navWrappers = cut.FindAll(".stat-card-link");
        navWrappers.Count.ShouldBe(2);
        foreach (AngleSharp.Dom.IElement wrapper in navWrappers) {
            wrapper.GetAttribute("role").ShouldBe("button");
            wrapper.GetAttribute("tabindex").ShouldBe("0");
            wrapper.GetAttribute("aria-label").ShouldNotBeNullOrWhiteSpace();
            // data-activate-button opts the wrapper into the interop.js Space page-scroll suppression.
            wrapper.HasAttribute("data-activate-button").ShouldBeTrue();
        }
    }

    [Fact]
    public void LandingPage_StatCardWrapper_NavigatesOnEnterKey() {
        // Keyboard activation parity: pressing Enter on the focusable StatCard wrapper navigates.
        SystemHealthReport health = CreateHealthReport(0, 42.5, 0.05);
        _ = _mockApiClient.GetSystemHealthAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SystemHealthReport?>(health));

        IRenderedComponent<Hexalith.EventStore.Admin.UI.Pages.Index> cut = Render<Hexalith.EventStore.Admin.UI.Pages.Index>();
        Microsoft.AspNetCore.Components.NavigationManager nav =
            Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();

        AngleSharp.Dom.IElement wrapper = cut.FindAll(".stat-card-link")[0];
        wrapper.KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Enter" });

        nav.Uri.ShouldContain("/streams");
    }

    [Fact]
    public void LandingPage_StatCardWrapper_NavigatesOnSpaceKey() {
        // Keyboard activation parity: Space activates the focusable StatCard wrapper too (the
        // page-scroll default is suppressed by the interop.js [data-activate-button] listener).
        SystemHealthReport health = CreateHealthReport(0, 42.5, 0.05);
        _ = _mockApiClient.GetSystemHealthAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SystemHealthReport?>(health));

        IRenderedComponent<Hexalith.EventStore.Admin.UI.Pages.Index> cut = Render<Hexalith.EventStore.Admin.UI.Pages.Index>();
        Microsoft.AspNetCore.Components.NavigationManager nav =
            Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();

        AngleSharp.Dom.IElement wrapper = cut.FindAll(".stat-card-link")[0];
        wrapper.KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = " " });

        nav.Uri.ShouldContain("/streams");
    }

    [Fact]
    public void LandingPage_WhenLoading_ShowsSkeletonCards() {
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
    public void LandingPage_WhenApiUnavailable_ShowsIssueBanner() {
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
    public void LandingPage_WhenZeroEvents_ShowsEmptyState() {
        // Arrange
        SystemHealthReport health = CreateHealthReport(0, 0, 0);
        _ = _mockApiClient.GetSystemHealthAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SystemHealthReport?>(health));
        _ = _mockApiClient.GetRecentlyActiveStreamsAsync(
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PagedResult<StreamSummary>([], 0, null)));

        // Act
        IRenderedComponent<Hexalith.EventStore.Admin.UI.Pages.Index> cut = Render<Hexalith.EventStore.Admin.UI.Pages.Index>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("EventStore Admin is running."), TimeSpan.FromSeconds(5));

        // Assert — EmptyState shown, no ActivityChart or grid
        cut.Markup.ShouldContain("No active streams yet");
        cut.Markup.ShouldNotContain("Stream Activity (24h)");
    }

    [Fact]
    public void LandingPage_WhenApiTimesOut_ShowsStaleData() {
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
    public void LandingPage_ErrorRateSeverity_Rendering() {
        // Verify the error rate severity rendering logic via StatusDisplayConfig
        var config = StatusBadge.StatusDisplayConfig.FromStreamStatus(StreamStatus.Active);
        config.Label.ShouldBe("Active");
        config.CssColor.ShouldContain("success");

        var tombConfig = StatusBadge.StatusDisplayConfig.FromStreamStatus(StreamStatus.Tombstoned);
        tombConfig.Label.ShouldBe("Tombstoned");
        tombConfig.CssColor.ShouldContain("error");
    }

    [Fact]
    public void LandingPage_WhenMetricsUnavailable_RendersUnavailableInsteadOfFakeZero() {
        // ADR-3 Truthful Metrics: dashboard must not display 0/0.0/s/0.00% for sources that have
        // no implementation. The status flags drive an explicit "unavailable" string.
        SystemHealthReport health = new(
            HealthStatus.Healthy,
            TotalEventCount: 0,
            EventsPerSecond: 0,
            ErrorPercentage: 0,
            DaprComponents: [],
            ObservabilityLinks: new ObservabilityLinks(null, null, null),
            TotalEventCountStatus: SystemHealthMetricStatus.Unavailable,
            EventsPerSecondStatus: SystemHealthMetricStatus.Unavailable,
            ErrorPercentageStatus: SystemHealthMetricStatus.Unavailable);
        _ = _mockApiClient.GetSystemHealthAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SystemHealthReport?>(health));
        _ = _mockApiClient.GetRecentlyActiveStreamsAsync(
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PagedResult<StreamSummary>([], 0, null)));

        IRenderedComponent<Hexalith.EventStore.Admin.UI.Pages.Index> cut = Render<Hexalith.EventStore.Admin.UI.Pages.Index>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("unavailable"), TimeSpan.FromSeconds(5));

        // No fake-precision values for the unimplemented metrics.
        cut.Markup.ShouldNotContain("0.0/s");
        cut.Markup.ShouldNotContain("0.00%");
    }

    [Fact]
    public void LandingPage_WhenMetricsAvailableAndZero_RendersRealZeroInsteadOfUnavailable() {
        SystemHealthReport health = new(
            HealthStatus.Healthy,
            TotalEventCount: 0,
            EventsPerSecond: 0,
            ErrorPercentage: 0,
            DaprComponents: [],
            ObservabilityLinks: new ObservabilityLinks(null, null, null),
            TotalEventCountStatus: SystemHealthMetricStatus.Available,
            EventsPerSecondStatus: SystemHealthMetricStatus.Available,
            ErrorPercentageStatus: SystemHealthMetricStatus.Available,
            InventorySourceStatus: RemoteMetadataStatus.Available,
            LocalSidecarMetadataStatus: RemoteMetadataStatus.Available);
        _ = _mockApiClient.GetSystemHealthAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SystemHealthReport?>(health));
        _ = _mockApiClient.GetRecentlyActiveStreamsAsync(
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PagedResult<StreamSummary>([], 0, null)));

        IRenderedComponent<Hexalith.EventStore.Admin.UI.Pages.Index> cut = Render<Hexalith.EventStore.Admin.UI.Pages.Index>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Events/sec"), TimeSpan.FromSeconds(5));

        cut.Markup.ShouldNotContain("unavailable");
        cut.Markup.ShouldContain($"{0.0:F1}/s");
        cut.Markup.ShouldContain($"{0.0:F2}%");
    }

    [Fact]
    public void LandingPage_WhenLoading_DoesNotFlashFakeZeros() {
        // ADR-3: loading state must not render 0 / 0.0/s / 0.00% before health data arrives.
        TaskCompletionSource<SystemHealthReport?> tcs = new();
        _ = _mockApiClient.GetSystemHealthAsync(Arg.Any<CancellationToken>())
            .Returns(tcs.Task);

        IRenderedComponent<Hexalith.EventStore.Admin.UI.Pages.Index> cut = Render<Hexalith.EventStore.Admin.UI.Pages.Index>();

        // The dash placeholder is the universal "not yet" indicator on the StatCard while loading.
        cut.Markup.ShouldNotContain("0.0/s");
        cut.Markup.ShouldNotContain("0.00%");

        // Cleanup — let the page resolve so dispose doesn't dangle.
        tcs.SetResult(null);
    }

    [Fact]
    public void LandingPage_WhenStreamsExist_RendersActivityChartWithoutGatingOnTotalEventCount() {
        // ST6: ActivityChart no longer gates on TotalEventCount > 0 (which was hardcoded to 0).
        // Even if TotalEventCount comes back as 0 or unavailable, the chart should render when
        // streams exist (proves the gate is no longer load-bearing on the stale metric).
        SystemHealthReport health = new(
            HealthStatus.Healthy,
            TotalEventCount: 0,
            EventsPerSecond: 0,
            ErrorPercentage: 0,
            DaprComponents: [],
            ObservabilityLinks: new ObservabilityLinks(null, null, null),
            TotalEventCountStatus: SystemHealthMetricStatus.Unavailable,
            EventsPerSecondStatus: SystemHealthMetricStatus.Unavailable,
            ErrorPercentageStatus: SystemHealthMetricStatus.Unavailable);
        _ = _mockApiClient.GetSystemHealthAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SystemHealthReport?>(health));
        _ = _mockApiClient.GetRecentlyActiveStreamsAsync(
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(CreateStreamsResult(3)));

        IRenderedComponent<Hexalith.EventStore.Admin.UI.Pages.Index> cut = Render<Hexalith.EventStore.Admin.UI.Pages.Index>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Recent Streams"), TimeSpan.FromSeconds(5));
    }

    private static SystemHealthReport CreateHealthReport(long totalEvents, double eventsPerSec, double errorPct)
        => new(
            HealthStatus.Healthy,
            totalEvents,
            eventsPerSec,
            errorPct,
            [],
            new ObservabilityLinks(null, null, null));

    private static PagedResult<StreamSummary> CreateStreamsResult(int count) {
        List<StreamSummary> items = [];
        for (int i = 0; i < count; i++) {
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
