using Bunit;

using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;
using Hexalith.EventStore.Admin.Abstractions.Models.Health;
using Hexalith.EventStore.Admin.UI.Pages;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.UI.Tests.Pages;

/// <summary>
/// bUnit tests for the DaprHealthHistory page.
/// </summary>
public class DaprHealthHistoryPageTests : AdminUITestContext {
    private readonly AdminHealthHistoryApiClient _mockClient;

    public DaprHealthHistoryPageTests() {
        _mockClient = Substitute.For<AdminHealthHistoryApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminHealthHistoryApiClient>.Instance);
        _ = Services.AddScoped(_ => _mockClient);
    }

    [Fact]
    public void HealthHistoryPage_RendersTitle() {
        // Arrange
        SetupSuccessfulResponse();

        // Act
        IRenderedComponent<DaprHealthHistory> cut = Render<DaprHealthHistory>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("DAPR Health History"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("DAPR Health History");
    }

    [Fact]
    public void HealthHistoryPage_RendersBackLink() {
        // Arrange
        SetupSuccessfulResponse();

        // Act
        IRenderedComponent<DaprHealthHistory> cut = Render<DaprHealthHistory>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("DAPR Infrastructure"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("DAPR Infrastructure");
    }

    [Fact]
    public void HealthHistoryPage_RendersStatCards_WithData() {
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
    public void HealthHistoryPage_RendersHeatmap_WhenDataAvailable() {
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
    public void HealthHistoryPage_RendersEmptyState_WhenNoData() {
        // Round 10 P2: DaprComponentHealthTimeline.HistoryStatus now defaults to Unavailable
        // (truth-contract alignment: new status fields default to non-green semantics). The
        // "empty success" path requires HistoryStatus = Available + HasData = false explicitly.
        // The HistoryStorageUnavailable banner branch is covered by the sibling test
        // HealthHistoryPage_RendersUnavailableBanner_WhenHistoryStorageUnavailable.
        _ = _mockClient.GetHealthHistoryAsync(
            Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DaprComponentHealthTimeline?>(
                new DaprComponentHealthTimeline(
                    [],
                    HasData: false,
                    HistoryStatus: SystemHealthMetricStatus.Available)));

        // Act
        IRenderedComponent<DaprHealthHistory> cut = Render<DaprHealthHistory>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("No health history available yet"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("No health history available yet");
    }

    [Fact]
    public void HealthHistoryPage_RendersRealZeroInventoryCopy_WhenHistorySourceReportsNoComponentRows() {
        _ = _mockClient.GetHealthHistoryAsync(
            Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DaprComponentHealthTimeline?>(
                new DaprComponentHealthTimeline(
                    [],
                    HasData: false,
                    HistoryStatus: SystemHealthMetricStatus.Available,
                    StatusMessage: "Health history source was read successfully; the remote DAPR inventory reported no component rows for this sample.")));

        IRenderedComponent<DaprHealthHistory> cut = Render<DaprHealthHistory>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("No component rows recorded"), TimeSpan.FromSeconds(5));

        cut.Markup.ShouldContain("remote DAPR inventory reported no component rows");
        cut.Markup.ShouldNotContain("No health history available yet");
    }

    [Fact]
    public void HealthHistoryPage_RendersUnavailableBanner_WhenHistoryStorageUnavailable() {
        _ = _mockClient.GetHealthHistoryAsync(
            Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DaprComponentHealthTimeline?>(
                new DaprComponentHealthTimeline(
                    [],
                    HasData: false,
                    HistoryStatus: SystemHealthMetricStatus.Unavailable,
                    StatusMessage: "Health history storage is unavailable.")));

        IRenderedComponent<DaprHealthHistory> cut = Render<DaprHealthHistory>();
        cut.WaitForAssertion(
            () => cut.Markup.ShouldContain("Health history storage unavailable"),
            TimeSpan.FromSeconds(5));

        cut.Markup.ShouldContain("Health history storage unavailable");
        cut.Markup.ShouldNotContain("No health history available yet");
    }

    [Fact]
    public void HealthHistoryPage_RendersSourceStatusBanner_WhenRemoteMetadataUnavailableSampleExists() {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var timeline = new DaprComponentHealthTimeline(
            [
                new DaprHealthHistoryEntry(
                    "remote-eventstore-metadata",
                    "metadata.dapr",
                    HealthStatus.Unhealthy,
                    now,
                    SourceStatus: RemoteMetadataStatus.Unreachable),
            ],
            HasData: true);
        _ = _mockClient.GetHealthHistoryAsync(
            Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DaprComponentHealthTimeline?>(timeline));

        IRenderedComponent<DaprHealthHistory> cut = Render<DaprHealthHistory>();
        cut.WaitForAssertion(
            () => cut.Markup.ShouldContain("DAPR inventory source unavailable"),
            TimeSpan.FromSeconds(5));

        cut.Markup.ShouldContain("remote EventStore metadata as unreachable");
        cut.Markup.ShouldContain("remote-eventstore-metadata");
    }

    [Fact]
    public void HealthHistoryPage_RendersTruncationWarning_WhenIsTruncated() {
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
    public void HealthHistoryPage_RendersTimeRangeButtons() {
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
    public void HealthHistoryPage_RendersTransitionLog_WithData() {
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
    public void HealthHistoryPage_RendersIssueBanner_WhenApiUnavailable() {
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
    public void HealthHistoryPage_RendersRefreshButton() {
        // Arrange
        SetupSuccessfulResponse();

        // Act
        IRenderedComponent<DaprHealthHistory> cut = Render<DaprHealthHistory>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Refresh"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Refresh");
    }

    [Fact]
    public void DaprHealthHistoryPage_ShowsPubSubRow_WhenRemoteMetadataAvailable() {
        // ST5 — round-3 patch F21. The collector test covers the persistence write; this page-
        // level regression asserts the heatmap/grid actually renders the pub/sub row sourced
        // from remote EventStore metadata, not just the locally-scoped state-store entries.
        // Without this guard the canonical inventory unification could regress into "history
        // page silently omits pub/sub rows that /dapr/components shows" — exactly the AC4
        // contradiction the round-1 ST5 task targeted.
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DaprComponentHealthTimeline timeline = new(
            [
                new DaprHealthHistoryEntry("statestore", "state.redis", HealthStatus.Healthy, now.AddMinutes(-15)),
                new DaprHealthHistoryEntry("pubsub", "pubsub.redis", HealthStatus.Healthy, now.AddMinutes(-15)),
                new DaprHealthHistoryEntry("pubsub", "pubsub.redis", HealthStatus.Healthy, now.AddMinutes(-5)),
            ],
            HasData: true);
        _ = _mockClient.GetHealthHistoryAsync(
            Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DaprComponentHealthTimeline?>(timeline));

        IRenderedComponent<DaprHealthHistory> cut = Render<DaprHealthHistory>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("statestore"), TimeSpan.FromSeconds(5));

        string markup = cut.Markup;
        // The heatmap groups by component name; pub/sub must surface as its own row, not be
        // collapsed into the state-store row.
        markup.ShouldContain("pubsub");
        markup.ShouldContain("statestore");
    }

    [Fact]
    public void DaprHealthHistoryPage_CellTitle_DoesNotRenderRawRazorExpression() {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DaprComponentHealthTimeline timeline = new(
            [
                new DaprHealthHistoryEntry("statestore", "state.redis", HealthStatus.Healthy, now.AddMinutes(-15)),
            ],
            HasData: true);
        _ = _mockClient.GetHealthHistoryAsync(
            Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DaprComponentHealthTimeline?>(timeline));

        IRenderedComponent<DaprHealthHistory> cut = Render<DaprHealthHistory>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("statestore"), TimeSpan.FromSeconds(5));

        cut.Markup.ShouldContain("statestore |");
        cut.Markup.ShouldNotContain("@slot.End.ToString");
    }

    [Fact]
    public void HealthHistoryPage_Heatmap_IsRovingTabindexGrid_SingleTabStop() {
        // a11y remediation (roving-tabindex ARIA grid): the heatmap is a single Tab stop — exactly one
        // interactive cell carries tabindex=0; the rest are -1 and reached via the arrow keys.
        SetupSuccessfulResponse();

        IRenderedComponent<DaprHealthHistory> cut = Render<DaprHealthHistory>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Timeline Heatmap"), TimeSpan.FromSeconds(5));

        AngleSharp.Dom.IElement grid = cut.Find(".health-heatmap");
        grid.GetAttribute("role").ShouldBe("grid");
        grid.HasAttribute("data-roving-grid").ShouldBeTrue();

        IReadOnlyList<AngleSharp.Dom.IElement> cells = cut.FindAll(".health-heatmap [data-row]");
        cells.Count.ShouldBeGreaterThan(1);
        cells.Count(c => c.GetAttribute("tabindex") == "0").ShouldBe(1);
    }

    [Fact]
    public void HealthHistoryPage_Heatmap_ArrowRight_MovesTheSingleTabStop() {
        // Roving tabindex: ArrowRight moves the single tab stop from (row 0, col 0) to (row 0, col 1).
        SetupSuccessfulResponse();

        IRenderedComponent<DaprHealthHistory> cut = Render<DaprHealthHistory>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Timeline Heatmap"), TimeSpan.FromSeconds(5));

        AngleSharp.Dom.IElement active = cut.Find(".health-heatmap [data-row=\"0\"][data-col=\"0\"]");
        active.GetAttribute("tabindex").ShouldBe("0");

        active.KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "ArrowRight" });

        cut.Find(".health-heatmap [data-row=\"0\"][data-col=\"1\"]").GetAttribute("tabindex").ShouldBe("0");
        cut.Find(".health-heatmap [data-row=\"0\"][data-col=\"0\"]").GetAttribute("tabindex").ShouldBe("-1");
    }

    [Fact]
    public void HealthHistoryPage_HeatmapDataCell_FiltersTransitionLog_OnEnterKey() {
        // a11y remediation: the heatmap data cells (previously mouse-only) are keyboard-activatable —
        // pressing Enter on a gridcell filters the transition log to that component+slot.
        SetupSuccessfulResponse();

        IRenderedComponent<DaprHealthHistory> cut = Render<DaprHealthHistory>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Timeline Heatmap"), TimeSpan.FromSeconds(5));

        AngleSharp.Dom.IElement cell = cut.Find(".health-heatmap [role=\"gridcell\"][data-row=\"0\"][data-col=\"1\"]");
        cell.KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Enter" });

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Filtered:"), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void HealthHistoryPage_HeatmapRowHeader_SelectsComponent_OnSpaceKey() {
        // a11y remediation: the heatmap row header selects the component on Space (opens the drill-down
        // panel); the page-scroll default is suppressed by the interop.js [data-activate-button] listener.
        SetupSuccessfulResponse();

        IRenderedComponent<DaprHealthHistory> cut = Render<DaprHealthHistory>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Timeline Heatmap"), TimeSpan.FromSeconds(5));

        AngleSharp.Dom.IElement rowHeader = cut.Find(".health-heatmap [role=\"rowheader\"][data-row=\"0\"]");
        rowHeader.KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = " " });

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("View Component"), TimeSpan.FromSeconds(5));
    }

    // ===== Helper methods =====

    private void SetupSuccessfulResponse() {
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
