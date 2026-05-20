using Bunit;

using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.UI.Pages;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.UI.Tests.Pages;

/// <summary>
/// ST8/ST9: bUnit tests proving the aggregate-id copy affordance on /streams copies without
/// triggering the parent row's navigation handler. Covers the click-target separation contract
/// (ADR-4) and accessible feedback.
/// </summary>
public class StreamsPageCopyIsolationTests : AdminUITestContext {
    private readonly AdminStreamApiClient _mockApiClient;

    public StreamsPageCopyIsolationTests() {
        _mockApiClient = Substitute.For<AdminStreamApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminStreamApiClient>.Instance);
        _ = Services.AddScoped(_ => _mockApiClient);
        _ = Services.AddScoped<DashboardRefreshService>();
        TestSignalRClient testClient = new();
        _ = Services.AddSingleton(testClient);
        _ = Services.AddSingleton(testClient.Inner);

        // Default: one stream so the grid renders.
        StreamSummary item = new(
            TenantId: "tenant-a",
            Domain: "counter",
            AggregateId: "counter-12345678",
            LastEventSequence: 1,
            LastActivityUtc: DateTimeOffset.UtcNow,
            EventCount: 18,
            HasSnapshot: false,
            StreamStatus: StreamStatus.Active);
        _ = _mockApiClient.GetRecentlyActiveStreamsAsync(
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PagedResult<StreamSummary>([item], 1, null)));
    }

    [Fact]
    public void AggregateIdCopy_IsRenderedAsAccessibleButton_NotPlainSpan() {
        IRenderedComponent<Streams> cut = Render<Streams>();
        cut.WaitForAssertion(
            () => cut.FindAll("[data-testid='aggregate-id-copy']").Count.ShouldBe(1),
            TimeSpan.FromSeconds(5));

        AngleSharp.Dom.IElement button = cut.Find("[data-testid='aggregate-id-copy']");

        button.TagName.ShouldBe("BUTTON");
        button.GetAttribute("aria-label").ShouldBe("Copy aggregate ID counter-12345678 to clipboard");
        button.GetAttribute("title").ShouldBe("counter-12345678 (click to copy)");
    }

    [Fact]
    public void AggregateIdCopy_Click_DoesNotNavigate() {
        Microsoft.AspNetCore.Components.NavigationManager nav =
            Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
        string urlBefore = nav.Uri;

        IRenderedComponent<Streams> cut = Render<Streams>();
        cut.WaitForAssertion(
            () => cut.FindAll("[data-testid='aggregate-id-copy']").Count.ShouldBe(1),
            TimeSpan.FromSeconds(5));

        cut.Find("[data-testid='aggregate-id-copy']").Click();

        nav.Uri.ShouldBe(urlBefore);
    }

    [Fact]
    public void AggregateIdCopy_Click_InvokesClipboardWriteText_AndAnnouncesAccessibleStatus() {
        IRenderedComponent<Streams> cut = Render<Streams>();
        cut.WaitForAssertion(
            () => cut.FindAll("[data-testid='aggregate-id-copy']").Count.ShouldBe(1),
            TimeSpan.FromSeconds(5));

        cut.Find("[data-testid='aggregate-id-copy']").Click();

        // Clipboard call goes through JSInterop.
        _ = JSInterop.VerifyInvoke("navigator.clipboard.writeText");

        // Accessible status region announces the copied value via aria-live region.
        cut.WaitForAssertion(
            () => cut.Find("[data-testid='copy-status']").TextContent
                .ShouldContain("Copied aggregate ID counter-12345678"),
            TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Row_Click_OutsideCopyButton_StillNavigatesToStreamDetail() {
        Microsoft.AspNetCore.Components.NavigationManager nav =
            Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
        string urlBefore = nav.Uri;

        IRenderedComponent<Streams> cut = Render<Streams>();
        cut.WaitForAssertion(
            () => cut.FindAll("[role='row']").Count.ShouldBeGreaterThan(1),
            TimeSpan.FromSeconds(5));

        // Find a non-copy cell (Tenant column) and click it — clicking should bubble to the
        // FluentDataGrid OnRowClick handler that calls NavigateTo("/streams/...").
        AngleSharp.Dom.IElement[] rowCells = cut.FindAll("[role='row']")
            .Skip(1) // skip header row
            .First()
            .QuerySelectorAll("[role='cell'], [role='gridcell']")
            .ToArray();
        rowCells.Length.ShouldBeGreaterThan(0);

        // Click the Tenant cell (index 1: status/tenant/domain/aggid/events/activity/snapshot).
        rowCells[1].Click();

        // The page should have triggered navigation (URL changes to the stream detail).
        cut.WaitForAssertion(
            () => nav.Uri.ShouldNotBe(urlBefore),
            TimeSpan.FromSeconds(5));
        nav.Uri.ShouldContain("/streams/tenant-a/counter/counter-12345678");
    }
}
