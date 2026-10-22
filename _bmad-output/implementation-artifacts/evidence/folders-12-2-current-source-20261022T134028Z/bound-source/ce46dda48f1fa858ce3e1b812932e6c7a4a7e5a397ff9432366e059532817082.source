using Bunit;

using Hexalith.EventStore.Admin.Abstractions.Models.Storage;
using Hexalith.EventStore.Admin.UI.Pages;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.UI.Tests.Pages;

/// <summary>
/// bUnit tests for the Storage page.
/// </summary>
public class StoragePageTests : AdminUITestContext {
    private readonly AdminStorageApiClient _mockStorageApi;

    public StoragePageTests() {
        _mockStorageApi = Substitute.For<AdminStorageApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminStorageApiClient>.Instance);
        _ = Services.AddScoped(_ => _mockStorageApi);
    }

    // ===== Merge-blocking tests (5.1-5.10) =====

    [Fact]
    public void StoragePage_RendersStatCards_WithCorrectValues() {
        // Arrange
        SetupOverview(150000, 1073741824);
        SetupEmptyHotStreams();

        // Act
        IRenderedComponent<Storage> cut = Render<Storage>();
        string formatted = 150000L.ToString("N0");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain(formatted), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Total Events");
        cut.Markup.ShouldContain(formatted);
        // FormatBytes uses InvariantCulture (always "1.0 GB" format)
        cut.Markup.ShouldContain("1.0 GB");
        cut.Markup.ShouldContain("Tenants");
    }

    [Fact]
    public void StoragePage_ShowsSkeletonCards_DuringLoading() {
        // Arrange — never complete the task
        TaskCompletionSource<StorageOverview> tcs = new();
        _ = _mockStorageApi.GetStorageOverviewAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(tcs.Task);
        _ = _mockStorageApi.GetHotStreamsAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<StreamStorageInfo>>([]));

        // Act
        IRenderedComponent<Storage> cut = Render<Storage>();

        // Assert — skeleton cards present during loading
        cut.Markup.ShouldContain("aria-hidden=\"true\"");
    }

    [Fact]
    public void StoragePage_TenantBreakdownGrid_RendersAllTenants() {
        // Arrange
        SetupOverview(200000, 2147483648, [
            new TenantStorageInfo("tenant-alpha", 100000, 1073741824, 500.0),
            new TenantStorageInfo("tenant-beta", 100000, 1073741824, 1500.0),
        ]);
        SetupEmptyHotStreams();

        // Act
        IRenderedComponent<Storage> cut = Render<Storage>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("tenant-alpha"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("tenant-alpha");
        cut.Markup.ShouldContain("tenant-beta");
        string formatted = 100000L.ToString("N0");
        cut.Markup.ShouldContain(formatted);
    }

    [Fact]
    public void StoragePage_HotStreamsGrid_RendersStreams() {
        // Arrange
        SetupOverview(50000, null);
        SetupHotStreams([
            new StreamStorageInfo("t1", "Sales", "agg-12345678901234", "Order", 5000, null, true, TimeSpan.FromHours(2)),
            new StreamStorageInfo("t1", "Sales", "agg-99999999999999", "Invoice", 200, null, false, null),
        ]);

        // Act
        IRenderedComponent<Storage> cut = Render<Storage>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Order"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Order");
        cut.Markup.ShouldContain("Invoice");
        cut.Markup.ShouldContain("Sales");
        string formattedCount = 5000L.ToString("N0");
        cut.Markup.ShouldContain(formattedCount);
    }

    [Fact]
    public void StoragePage_ShowsIssueBanner_OnApiError() {
        // Arrange
        _ = _mockStorageApi.GetStorageOverviewAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<StorageOverview>(new Hexalith.EventStore.Admin.UI.Services.Exceptions.ServiceUnavailableException("test")));
        _ = _mockStorageApi.GetHotStreamsAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<StreamStorageInfo>>([]));

        // Act
        IRenderedComponent<Storage> cut = Render<Storage>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Unable to load storage data"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Unable to load storage data");
    }

    [Fact]
    public void StoragePage_ShowsNA_WhenTotalSizeBytesIsNull() {
        // Arrange — all sizes null → should show "Total Streams" instead of "Total Storage"
        SetupOverview(50000, null, [
            new TenantStorageInfo("t1", 50000, null, 100.0),
        ]);
        SetupEmptyHotStreams();

        // Act
        IRenderedComponent<Storage> cut = Render<Storage>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Total Streams"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Total Streams");
    }

    [Fact]
    public void StoragePage_UsesExactTotalStreamCount_WhenAvailable() {
        // Arrange
        SetupOverview(50000, null, [
            new TenantStorageInfo("t1", 50000, null, 100.0),
        ], totalStreamCount: 1234);
        SetupEmptyHotStreams();

        // Act
        IRenderedComponent<Storage> cut = Render<Storage>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Total Streams"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain(1234L.ToString("N0"));
        cut.Markup.ShouldContain("Total streams in the selected scope");
    }

    [Fact]
    public void StoragePage_HasH1Heading() {
        // Arrange
        SetupOverview(0, null);
        SetupEmptyHotStreams();

        // Act
        IRenderedComponent<Storage> cut = Render<Storage>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Storage"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("<h1");
        cut.Markup.ShouldContain("Storage");
    }

    [Fact]
    public void StoragePage_TreemapHasSvgWithRoleAndAriaLabel() {
        // Arrange
        SetupOverview(10000, null);
        SetupHotStreams([
            new StreamStorageInfo("t1", "Sales", "agg-1", "Order", 5000, null, true, TimeSpan.FromHours(1)),
            new StreamStorageInfo("t1", "Sales", "agg-2", "Invoice", 3000, null, false, null),
        ]);

        // Act
        IRenderedComponent<Storage> cut = Render<Storage>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("role=\"img\""), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("role=\"img\"");
        cut.Markup.ShouldContain("aria-label=\"Storage distribution");
    }

    [Fact]
    public void StoragePage_EmptyState_WhenNoTenants() {
        // Arrange
        SetupOverview(0, null, []);
        SetupEmptyHotStreams();

        // Act
        IRenderedComponent<Storage> cut = Render<Storage>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("No storage data available"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("No storage data available");
    }

    [Fact]
    public void StoragePage_SnapshotRiskBanner_ShowsCount() {
        // Arrange
        SetupOverview(50000, null);
        SetupHotStreams([
            new StreamStorageInfo("t1", "Sales", "agg-1", "Order", 5000, null, false, null),
            new StreamStorageInfo("t1", "Sales", "agg-2", "Invoice", 2000, null, false, null),
            new StreamStorageInfo("t1", "Sales", "agg-3", "Payment", 500, null, true, TimeSpan.FromHours(1)),
        ]);

        // Act
        IRenderedComponent<Storage> cut = Render<Storage>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("2 streams have"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("2 streams have");
        cut.Markup.ShouldContain("1,000 events without snapshots");
    }

    // ===== Recommended tests (5.11-5.22) =====

    [Fact]
    public void StoragePage_HotStreamsWarningIcon_ForStreamsWithoutSnapshots() {
        // Arrange
        SetupOverview(10000, null);
        SetupHotStreams([
            new StreamStorageInfo("t1", "Sales", "agg-1", "Order", 5000, null, false, null),
        ]);

        // Act
        IRenderedComponent<Storage> cut = Render<Storage>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Warning: no snapshot with over 1000 events"), TimeSpan.FromSeconds(5));

        // Assert — warning aria-label present (icon may render as entity or unicode)
        cut.Markup.ShouldContain("Warning: no snapshot with over 1000 events");
    }

    [Fact]
    public void StoragePage_GrowthBadgeColors_CorrectSeverity() {
        // Arrange
        SetupOverview(100000, null, [
            new TenantStorageInfo("low-growth", 30000, null, 500.0),
            new TenantStorageInfo("med-growth", 30000, null, 5000.0),
            new TenantStorageInfo("high-growth", 40000, null, 15000.0),
        ]);
        SetupEmptyHotStreams();

        // Act
        IRenderedComponent<Storage> cut = Render<Storage>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("low-growth"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Low");
        cut.Markup.ShouldContain("Medium");
        cut.Markup.ShouldContain("High");
    }

    [Fact]
    public void StoragePage_FormatBytes_CorrectOutputs() {
        // Arrange — verify via stat card rendering
        SetupOverview(1000, 1024, [
            new TenantStorageInfo("t1", 1000, 1024, null),
        ]);
        SetupEmptyHotStreams();

        // Act — FormatBytes uses InvariantCulture (always "1.0 KB" format)
        string expected = "1.0 KB";
        IRenderedComponent<Storage> cut = Render<Storage>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain(expected), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain(expected);
    }

    [Fact]
    public void StoragePage_HiddenScreenReaderTable_RenderedWithTreemap() {
        // Arrange
        SetupOverview(10000, null);
        SetupHotStreams([
            new StreamStorageInfo("t1", "Sales", "agg-1", "Order", 5000, null, true, TimeSpan.FromHours(1)),
        ]);

        // Act
        IRenderedComponent<Storage> cut = Render<Storage>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("sr-only"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("class=\"sr-only\"");
        cut.Markup.ShouldContain("Storage distribution data");
    }

    // ===== Helpers =====

    private void SetupOverview(long totalEvents, long? totalSize, IReadOnlyList<TenantStorageInfo>? tenants = null, long? totalStreamCount = null) {
        StorageOverview overview = new(totalEvents, totalSize, tenants ?? [new TenantStorageInfo("default", totalEvents, totalSize, 100.0)], totalStreamCount);
        _ = _mockStorageApi.GetStorageOverviewAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(overview));
    }

    private void SetupEmptyHotStreams() => _ = _mockStorageApi.GetHotStreamsAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<StreamStorageInfo>>([]));

    private void SetupHotStreams(IReadOnlyList<StreamStorageInfo> streams) => _ = _mockStorageApi.GetHotStreamsAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(streams));
}
