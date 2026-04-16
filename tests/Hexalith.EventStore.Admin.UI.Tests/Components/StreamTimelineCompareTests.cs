using Bunit;

using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;
using Hexalith.EventStore.Admin.Abstractions.Models.TypeCatalog;
using Hexalith.EventStore.Admin.UI.Components;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.UI.Tests.Components;

/// <summary>
/// bUnit tests for StreamTimelineGrid compare mode.
/// </summary>
public class StreamTimelineCompareTests : AdminUITestContext {
    private readonly AdminStreamApiClient _mockApiClient;

    public StreamTimelineCompareTests() {
        _mockApiClient = Substitute.For<AdminStreamApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminStreamApiClient>.Instance);
        _ = Services.AddScoped(_ => _mockApiClient);
        _ = Services.AddScoped<DashboardRefreshService>();
        _ = Services.AddScoped<TopologyCacheService>();
        TestSignalRClient testClient = new();
        _ = Services.AddSingleton(testClient);
        _ = Services.AddSingleton(testClient.Inner);

        _ = _mockApiClient.GetTenantsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TenantSummary>>([]));
        _ = _mockApiClient.GetAggregateTypesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<AggregateTypeInfo>>([]));
    }

    [Fact]
    public void CompareMode_ShowsCheckboxes_WhenEnabled() {
        // Arrange
        List<TimelineEntry> entries = CreateEntries(3);

        // Act
        IRenderedComponent<StreamTimelineGrid> cut = Render<StreamTimelineGrid>(p => p
            .Add(c => c.Entries, entries)
            .Add(c => c.TotalCount, 3)
            .Add(c => c.CompareMode, true));

        // Assert
        string markup = cut.Markup;
        markup.ShouldContain("fluent-checkbox");
        markup.ShouldContain("Select two events to compare");
    }

    [Fact]
    public void CompareMode_HidesCheckboxes_WhenDisabled() {
        // Arrange
        List<TimelineEntry> entries = CreateEntries(3);

        // Act
        IRenderedComponent<StreamTimelineGrid> cut = Render<StreamTimelineGrid>(p => p
            .Add(c => c.Entries, entries)
            .Add(c => c.TotalCount, 3)
            .Add(c => c.CompareMode, false));

        // Assert
        cut.Markup.ShouldNotContain("fluent-checkbox");
    }

    [Fact]
    public void CompareMode_ViewDiffButton_NotShownWithLessThanTwoSelections() {
        // Arrange
        List<TimelineEntry> entries = CreateEntries(3);

        // Act
        IRenderedComponent<StreamTimelineGrid> cut = Render<StreamTimelineGrid>(p => p
            .Add(c => c.Entries, entries)
            .Add(c => c.TotalCount, 3)
            .Add(c => c.CompareMode, true));

        // Assert — View Diff button should not be present with 0 selections
        cut.Markup.ShouldNotContain("View Diff");
    }

    [Fact]
    public void CompareMode_ShowsProgressiveBannerText() {
        // Arrange
        List<TimelineEntry> entries = CreateEntries(3);

        // Act
        IRenderedComponent<StreamTimelineGrid> cut = Render<StreamTimelineGrid>(p => p
            .Add(c => c.Entries, entries)
            .Add(c => c.TotalCount, 3)
            .Add(c => c.CompareMode, true));

        // Assert — initial state: "Select two events"
        cut.Markup.ShouldContain("Select two events to compare");
        cut.Markup.ShouldContain("Selected: 0/2");
    }

    [Fact]
    public void EventDetailPanel_ShowsInspectStateButton() {
        // Arrange
        EventDetail detail = new(
            "test-tenant", "counter", "agg-001", 42, "CounterIncremented",
            DateTimeOffset.UtcNow, "corr-abc", null, null, """{"count": 1}""");
        _ = _mockApiClient.GetEventDetailAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), 42, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<EventDetail?>(detail));
        _ = _mockApiClient.GetAggregateStateAtPositionAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), 42, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AggregateStateSnapshot?>(null));

        // Act
        IRenderedComponent<EventDetailPanel> cut = Render<EventDetailPanel>(p => p
            .Add(c => c.TenantId, "test-tenant")
            .Add(c => c.Domain, "counter")
            .Add(c => c.AggregateId, "agg-001")
            .Add(c => c.SequenceNumber, 42L));

        // Assert
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Inspect State"), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void EventDetailPanel_ShowsDiffWithPrevious_WhenSequenceGe1() {
        // Arrange
        EventDetail detail = new(
            "test-tenant", "counter", "agg-001", 5, "CounterIncremented",
            DateTimeOffset.UtcNow, "corr-abc", null, null, """{"count": 5}""");
        _ = _mockApiClient.GetEventDetailAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), 5, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<EventDetail?>(detail));
        _ = _mockApiClient.GetAggregateStateAtPositionAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), 5, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AggregateStateSnapshot?>(null));

        // Act
        IRenderedComponent<EventDetailPanel> cut = Render<EventDetailPanel>(p => p
            .Add(c => c.TenantId, "test-tenant")
            .Add(c => c.Domain, "counter")
            .Add(c => c.AggregateId, "agg-001")
            .Add(c => c.SequenceNumber, 5L));

        // Assert
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Diff with Previous"), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void EventDetailPanel_HidesDiffWithPrevious_WhenSequenceIs0() {
        // Arrange
        EventDetail detail = new(
            "test-tenant", "counter", "agg-001", 0, "CounterCreated",
            DateTimeOffset.UtcNow, "corr-abc", null, null, """{"count": 0}""");
        _ = _mockApiClient.GetEventDetailAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), 0, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<EventDetail?>(detail));
        _ = _mockApiClient.GetAggregateStateAtPositionAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), 0, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AggregateStateSnapshot?>(null));

        // Act
        IRenderedComponent<EventDetailPanel> cut = Render<EventDetailPanel>(p => p
            .Add(c => c.TenantId, "test-tenant")
            .Add(c => c.Domain, "counter")
            .Add(c => c.AggregateId, "agg-001")
            .Add(c => c.SequenceNumber, 0L));

        // Assert
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("CounterCreated"), TimeSpan.FromSeconds(5));
        cut.Markup.ShouldNotContain("Diff with Previous");
    }

    private static List<TimelineEntry> CreateEntries(int count) {
        List<TimelineEntry> entries = [];
        for (int i = 1; i <= count; i++) {
            entries.Add(new TimelineEntry(
                i,
                DateTimeOffset.UtcNow.AddMinutes(-i),
                TimelineEntryType.Event,
                $"Event{i}",
                $"corr-{i:D8}",
                $"user-{i}"));
        }

        return entries;
    }
}
