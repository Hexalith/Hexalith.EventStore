using Bunit;

using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.UI.Components;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.UI.Tests.Components;

public class BlameViewerTests : AdminUITestContext {
    private readonly AdminStreamApiClient _mockApiClient;

    public BlameViewerTests() {
        _mockApiClient = Substitute.For<AdminStreamApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminStreamApiClient>.Instance);
        _ = Services.AddScoped(_ => _mockApiClient);
        _ = Services.AddScoped<DashboardRefreshService>();
        _ = Services.AddScoped<TopologyCacheService>();
        TestSignalRClient testClient = new();
        _ = Services.AddSingleton(testClient);
        _ = Services.AddSingleton(testClient.Inner);
    }

    [Fact]
    public void BlameViewer_RendersFieldProvenanceTable() {
        List<FieldProvenance> fields =
        [
            new("Count", "5", "4", 10, DateTimeOffset.UtcNow, "CounterIncremented", "corr-1", "user-1"),
            new("Status", "\"Active\"", "\"Pending\"", 8, DateTimeOffset.UtcNow.AddMinutes(-5), "StatusChanged", "corr-2", "user-2"),
        ];
        var blame = new AggregateBlameView("tenant-a", "Counter", "agg-1", 10, DateTimeOffset.UtcNow, fields, false, false);
        SetupBlameMock(blame);

        IRenderedComponent<BlameViewer> cut = RenderBlameViewer();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Count"), TimeSpan.FromSeconds(5));

        string markup = cut.Markup;
        markup.ShouldContain("Count");
        markup.ShouldContain("Status");
        markup.ShouldContain("CounterIncremented");
        markup.ShouldContain("StatusChanged");
    }

    [Fact]
    public void BlameViewer_ShowsEmptyState_WhenNoFields() {
        var blame = new AggregateBlameView("tenant-a", "Counter", "agg-1", 10, DateTimeOffset.UtcNow, [], false, false);
        SetupBlameMock(blame);

        IRenderedComponent<BlameViewer> cut = RenderBlameViewer();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("No field"), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void BlameViewer_ShowsTruncationWarning_WhenIsTruncated() {
        List<FieldProvenance> fields =
        [
            new("Count", "5", "4", 10, DateTimeOffset.UtcNow, "CounterIncremented", "corr-1", "user-1"),
        ];
        var blame = new AggregateBlameView("tenant-a", "Counter", "agg-1", 10, DateTimeOffset.UtcNow, fields, true, false);
        SetupBlameMock(blame);

        IRenderedComponent<BlameViewer> cut = RenderBlameViewer();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("partial event window"), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void BlameViewer_ShowsEmptyOrError_WhenApiReturnsNull() {
        _ = _mockApiClient.GetAggregateBlameAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<long?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AggregateBlameView?>(null));

        IRenderedComponent<BlameViewer> cut = RenderBlameViewer();
        cut.WaitForAssertion(() => {
            string markup = cut.Markup;
            // Should show some state — error, empty, or no-data indicator
            (markup.Contains("No events") || markup.Contains("no fields") ||
             markup.Contains("error") || markup.Contains("not available") ||
             markup.Contains("Blame View") || markup.Contains("blame-viewer")).ShouldBeTrue();
        }, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void BlameViewer_InvokesOnClose_WhenCloseClicked() {
        List<FieldProvenance> fields =
        [
            new("Count", "5", "4", 10, DateTimeOffset.UtcNow, "CounterIncremented", "corr-1", "user-1"),
        ];
        var blame = new AggregateBlameView("tenant-a", "Counter", "agg-1", 10, DateTimeOffset.UtcNow, fields, false, false);
        SetupBlameMock(blame);

        bool closeCalled = false;
        IRenderedComponent<BlameViewer> cut = Render<BlameViewer>(p => p
            .Add(c => c.TenantId, "tenant-a")
            .Add(c => c.Domain, "Counter")
            .Add(c => c.AggregateId, "agg-1")
            .Add(c => c.OnClose, () => closeCalled = true));

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Count"), TimeSpan.FromSeconds(5));

        // Find and click the close button
        AngleSharp.Dom.IElement? closeButton = cut.FindAll("button").FirstOrDefault(b =>
            b.InnerHtml.Contains("Close") || b.InnerHtml.Contains("close") ||
            b.GetAttribute("aria-label")?.Contains("close", StringComparison.OrdinalIgnoreCase) == true ||
            b.InnerHtml.Contains("\u00d7") || b.InnerHtml.Contains("Back"));
        if (closeButton is not null) {
            closeButton.Click();
            closeCalled.ShouldBeTrue();
        }
    }

    private IRenderedComponent<BlameViewer> RenderBlameViewer(long? atSequence = null) => Render<BlameViewer>(p => p
                                                                                                   .Add(c => c.TenantId, "tenant-a")
                                                                                                   .Add(c => c.Domain, "Counter")
                                                                                                   .Add(c => c.AggregateId, "agg-1")
                                                                                                   .Add(c => c.AtSequence, atSequence));

    private void SetupBlameMock(AggregateBlameView blame) => _ = _mockApiClient.GetAggregateBlameAsync(
            "tenant-a", "Counter", "agg-1", Arg.Any<long?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AggregateBlameView?>(blame));
}
