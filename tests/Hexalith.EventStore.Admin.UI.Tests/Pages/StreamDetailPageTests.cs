using Bunit;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;
using Hexalith.EventStore.Admin.Abstractions.Models.TypeCatalog;
using Hexalith.EventStore.Admin.UI.Components;
using Hexalith.EventStore.Admin.UI.Pages;
using Hexalith.EventStore.Admin.UI.Services;
using Hexalith.EventStore.SignalR;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.UI.Tests.Pages;

/// <summary>
/// bUnit tests for the StreamDetail page.
/// </summary>
public class StreamDetailPageTests : AdminUITestContext
{
    private readonly AdminStreamApiClient _mockApiClient;

    public StreamDetailPageTests()
    {
        _mockApiClient = Substitute.For<AdminStreamApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminStreamApiClient>.Instance);
        Services.AddScoped(_ => _mockApiClient);
        Services.AddScoped<DashboardRefreshService>();
        Services.AddScoped<TopologyCacheService>();
        TestSignalRClient testClient = new();
        Services.AddSingleton(testClient);
        Services.AddSingleton(testClient.Inner);

        // Default mock: empty tenants and types for TopologyCacheService
        _ = _mockApiClient.GetTenantsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TenantSummary>>([]));
        _ = _mockApiClient.GetAggregateTypesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<AggregateTypeInfo>>([]));
    }

    [Fact]
    public void StreamDetail_RendersHeader_WithMonospaceStreamIdentity()
    {
        // Arrange
        SetupTimeline(CreateTimelineResult(5));

        // Act
        IRenderedComponent<StreamDetail> cut = RenderStreamDetail();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("test-tenant"), TimeSpan.FromSeconds(5));

        // Assert
        string markup = cut.Markup;
        markup.ShouldContain("test-tenant");
        markup.ShouldContain("counter");
        markup.ShouldContain("agg-001");
        markup.ShouldContain("monospace");
    }

    [Fact]
    public void StreamDetail_RendersStatCards_WithStreamSummary()
    {
        // Arrange
        SetupTimeline(CreateTimelineResult(5));

        // Act
        IRenderedComponent<StreamDetail> cut = RenderStreamDetail();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Event Count"), TimeSpan.FromSeconds(5));

        // Assert
        string markup = cut.Markup;
        markup.ShouldContain("Event Count");
        markup.ShouldContain("Last Activity");
        markup.ShouldContain("Stream Status");
        markup.ShouldContain("Has Snapshot");
    }

    [Fact]
    public void StreamDetail_RendersTimelineGrid_WithColumns()
    {
        // Arrange
        SetupTimeline(CreateTimelineResult(3));

        // Act
        IRenderedComponent<StreamDetail> cut = RenderStreamDetail();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Seq"), TimeSpan.FromSeconds(5));

        // Assert
        string markup = cut.Markup;
        markup.ShouldContain("Seq");
        markup.ShouldContain("Time");
        markup.ShouldContain("Type");
        markup.ShouldContain("Type Name");
        markup.ShouldContain("Correlation");
        markup.ShouldContain("User");
    }

    [Fact]
    public void StreamDetail_TimelineEntryTypeBadge_MapsCorrectly()
    {
        // Arrange
        List<TimelineEntry> entries =
        [
            new(1, DateTimeOffset.UtcNow, TimelineEntryType.Command, "PlaceOrder", "corr-001", "user1"),
            new(2, DateTimeOffset.UtcNow, TimelineEntryType.Event, "OrderPlaced", "corr-001", "user1"),
            new(3, DateTimeOffset.UtcNow, TimelineEntryType.Query, "GetOrder", "corr-002", null),
        ];
        SetupTimeline(new PagedResult<TimelineEntry>(entries, 3, null));

        // Act
        IRenderedComponent<StreamDetail> cut = RenderStreamDetail();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Command"), TimeSpan.FromSeconds(5));

        // Assert
        string markup = cut.Markup;
        markup.ShouldContain("Command");
        markup.ShouldContain("Event");
        markup.ShouldContain("Query");
    }

    [Fact]
    public void StreamDetail_ShowsEmptyState_OnApiFailure()
    {
        // Arrange
        _ = _mockApiClient.GetStreamTimelineAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<long?>(), Arg.Any<long?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<PagedResult<TimelineEntry>>(new HttpRequestException("Connection refused")));

        // Act
        IRenderedComponent<StreamDetail> cut = RenderStreamDetail();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Unable to load timeline"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Unable to load timeline");
    }

    [Fact]
    public void StreamDetail_EventDetailPanel_RendersEventMetadata()
    {
        // Arrange
        List<TimelineEntry> entries =
        [
            new(42, DateTimeOffset.UtcNow, TimelineEntryType.Event, "CounterIncremented", "corr-abc", "user1"),
        ];
        SetupTimeline(new PagedResult<TimelineEntry>(entries, 1, null));

        EventDetail detail = new(
            "test-tenant", "counter", "agg-001", 42, "CounterIncremented",
            DateTimeOffset.UtcNow, "corr-abc", "cause-xyz", "user1",
            """{"count": 1}""");
        _ = _mockApiClient.GetEventDetailAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), 42, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<EventDetail?>(detail));
        _ = _mockApiClient.GetAggregateStateAtPositionAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), 42, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AggregateStateSnapshot?>(null));

        // Act — render the EventDetailPanel directly (SupplyParameterFromQuery not testable via bUnit Render)
        IRenderedComponent<EventDetailPanel> cut = Render<EventDetailPanel>(parameters => parameters
            .Add(p => p.TenantId, "test-tenant")
            .Add(p => p.Domain, "counter")
            .Add(p => p.AggregateId, "agg-001")
            .Add(p => p.SequenceNumber, 42L));
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("CounterIncremented"), TimeSpan.FromSeconds(5));

        // Assert
        string markup = cut.Markup;
        markup.ShouldContain("CounterIncremented");
        markup.ShouldContain("corr-abc");
    }

    [Fact]
    public void StreamDetail_InlineStatePreview_ShowsUnavailable_OnNull()
    {
        // Arrange
        EventDetail detail = new(
            "test-tenant", "counter", "agg-001", 42, "CounterIncremented",
            DateTimeOffset.UtcNow, "corr-abc", null, null, "{}");
        _ = _mockApiClient.GetEventDetailAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), 42, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<EventDetail?>(detail));
        _ = _mockApiClient.GetAggregateStateAtPositionAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), 42, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AggregateStateSnapshot?>(null));

        // Act — render EventDetailPanel directly to test state preview
        IRenderedComponent<EventDetailPanel> cut = Render<EventDetailPanel>(parameters => parameters
            .Add(p => p.TenantId, "test-tenant")
            .Add(p => p.Domain, "counter")
            .Add(p => p.AggregateId, "agg-001")
            .Add(p => p.SequenceNumber, 42L));
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("State reconstruction not available"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("State reconstruction not available at this position");
    }

    [Fact]
    public void StreamDetail_ShowsLoadingSkeleton_Initially()
    {
        // Arrange — make the API call hang
        TaskCompletionSource<PagedResult<TimelineEntry>> tcs = new();
        _ = _mockApiClient.GetStreamTimelineAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<long?>(), Arg.Any<long?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(tcs.Task);

        // Act
        IRenderedComponent<StreamDetail> cut = RenderStreamDetail();

        // Assert — skeleton should be visible while loading
        cut.Markup.ShouldContain("Loading stream...");
    }

    private IRenderedComponent<StreamDetail> RenderStreamDetail()
    {
        return Render<StreamDetail>(parameters => parameters
            .Add(p => p.TenantId, "test-tenant")
            .Add(p => p.Domain, "counter")
            .Add(p => p.AggregateId, "agg-001"));
    }

    private void SetupTimeline(PagedResult<TimelineEntry> result)
    {
        _ = _mockApiClient.GetStreamTimelineAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<long?>(), Arg.Any<long?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(result));
    }

    private static PagedResult<TimelineEntry> CreateTimelineResult(int count)
    {
        List<TimelineEntry> entries = [];
        for (int i = 1; i <= count; i++)
        {
            entries.Add(new TimelineEntry(
                i,
                DateTimeOffset.UtcNow.AddMinutes(-i),
                i % 2 == 0 ? TimelineEntryType.Command : TimelineEntryType.Event,
                $"SomeType{i}",
                $"corr-{i:D8}",
                i % 3 == 0 ? null : $"user-{i}"));
        }

        return new PagedResult<TimelineEntry>(entries, count, null);
    }
}
