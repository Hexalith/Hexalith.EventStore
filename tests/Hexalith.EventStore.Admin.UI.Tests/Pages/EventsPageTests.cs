using System.Reflection;

using AngleSharp.Dom;

using Bunit;

using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;
using Hexalith.EventStore.Admin.UI.Components.Shared;
using Hexalith.EventStore.Admin.UI.Pages;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.UI.Tests.Pages;

/// <summary>
/// bUnit tests for the Events page.
/// </summary>
public class EventsPageTests : AdminUITestContext {
    private readonly AdminStreamApiClient _mockApiClient;

    public EventsPageTests() {
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
    public void EventsPage_ShowsLoadingSkeleton() {
        // Arrange — setup a slow-responding mock
        TaskCompletionSource<PagedResult<StreamSummary>> tcs = new();
        _ = _mockApiClient.GetRecentlyActiveStreamsAsync(
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(tcs.Task);
        _ = _mockApiClient.GetTenantsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TenantSummary>>([]));

        // Act
        IRenderedComponent<Events> cut = Render<Events>();

        // Assert — exactly three skeleton cards are shown while loading
        cut.WaitForAssertion(
            () => cut.FindComponents<SkeletonCard>().Count.ShouldBe(3),
            TimeSpan.FromSeconds(5));

        // Complete the task to clean up
        tcs.SetResult(new PagedResult<StreamSummary>([], 0, null));
    }

    [Fact]
    public void EventsPage_ShowsEmptyState_WhenNoStreams() {
        // Arrange
        SetupEmptyMocks();

        // Act
        IRenderedComponent<Events> cut = Render<Events>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("No events recorded yet"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("No events recorded yet");
        cut.Markup.ShouldContain("Events will appear here as commands are processed");
    }

    [Fact]
    public void EventsPage_RendersDataGridWithColumns() {
        // Arrange
        SetupMocksWithEvents(3);

        // Act
        IRenderedComponent<Events> cut = Render<Events>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("tenant-0"), TimeSpan.FromSeconds(5));

        // Assert
        string markup = cut.Markup;
        markup.ShouldContain("Event Type");
        markup.ShouldContain("Tenant");
        markup.ShouldContain("Domain");
        markup.ShouldContain("Aggregate ID");
        markup.ShouldContain("Correlation ID");
        markup.ShouldContain("Timestamp");
        markup.ShouldContain("tenant-0");
        markup.ShouldContain("counter");
        markup.ShouldContain("TestEvent");
    }

    [Fact]
    public void EventsPage_ServiceUnavailable_ShowsErrorState() {
        // Arrange
        _ = _mockApiClient.GetRecentlyActiveStreamsAsync(
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns<PagedResult<StreamSummary>>(_ =>
                throw new Hexalith.EventStore.Admin.UI.Services.Exceptions.ServiceUnavailableException());
        _ = _mockApiClient.GetTenantsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TenantSummary>>([]));

        // Act
        IRenderedComponent<Events> cut = Render<Events>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Unable to load events"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("admin backend may be unavailable");
    }

    [Fact]
    public void EventsPage_ForbiddenAccess_ShowsAccessDenied() {
        // Arrange
        _ = _mockApiClient.GetRecentlyActiveStreamsAsync(
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns<PagedResult<StreamSummary>>(_ =>
                throw new Hexalith.EventStore.Admin.UI.Services.Exceptions.ForbiddenAccessException("Forbidden"));
        _ = _mockApiClient.GetTenantsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TenantSummary>>([]));

        // Act
        IRenderedComponent<Events> cut = Render<Events>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Access Denied"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Access Denied");
    }

    [Fact]
    public async Task EventsPage_RowClick_NavigatesToStreamDetailWithSequence() {
        // Arrange
        SetupMocksWithEvents(1, "tenant a", "orders/sales", "agg?123");

        // Act
        IRenderedComponent<Events> cut = Render<Events>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("TestEvent"), TimeSpan.FromSeconds(5));

        // Click the rendered data row to verify grid wiring, not just the private handler
        IElement? row = cut.FindAll("tr")
            .FirstOrDefault(r => r.TextContent.Contains("tenant a", StringComparison.OrdinalIgnoreCase));
        _ = row.ShouldNotBeNull();
        await cut.InvokeAsync(() => row!.Click());

        NavManager.Uri.ShouldEndWith(
            "/streams/tenant%20a/orders%2Fsales/agg%3F123?detail=1");
    }

    [Fact]
    public async Task EventsPage_RefreshPreservesFiltersPageAndScrollState() {
        // Arrange
        _ = JSInterop.Setup<double>("hexalithAdmin.getScrollTop", _ => true).SetResult(240d);

        StreamSummary stream = new(
            "tenant-a",
            "counter",
            "agg-00000001",
            30,
            DateTimeOffset.UtcNow,
            30,
            false,
            StreamStatus.Active);

        _ = _mockApiClient.GetRecentlyActiveStreamsAsync(
            "tenant-a",
            null,
            50,
            Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(new PagedResult<StreamSummary>([stream], 1, null)),
                Task.FromResult(new PagedResult<StreamSummary>([stream], 1, null)));

        _ = _mockApiClient.GetStreamTimelineAsync(
            stream.TenantId,
            stream.Domain,
            stream.AggregateId,
            Arg.Any<long?>(),
            Arg.Any<long?>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(new PagedResult<TimelineEntry>(CreateTimelineEntries(30, "CreateEvent"), 30, null)),
                Task.FromResult(new PagedResult<TimelineEntry>(CreateTimelineEntries(30, "CreateEventRefreshed"), 30, null)));

        _ = _mockApiClient.GetTenantsAsync(Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult<IReadOnlyList<TenantSummary>>(
                [
                    new TenantSummary("tenant-a", "Tenant A", TenantStatusType.Active),
                ]),
                Task.FromResult<IReadOnlyList<TenantSummary>>(
                [
                    new TenantSummary("tenant-a", "Tenant A", TenantStatusType.Active),
                    new TenantSummary("tenant-b", "Tenant B", TenantStatusType.Active),
                ]));

        NavManager.NavigateTo("/events?page=2&tenant=tenant-a&eventType=Create");
        IRenderedComponent<Events> cut = Render<Events>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Page 2 of 2"), TimeSpan.FromSeconds(5));

        DashboardRefreshService refreshService = Services.GetRequiredService<DashboardRefreshService>();

        // Act
        await cut.InvokeAsync(() => {
            RaiseRefresh(refreshService, new DashboardData(null, null));
            return Task.CompletedTask;
        });

        // Assert
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("CreateEventRefreshed"), TimeSpan.FromSeconds(5));
        cut.Markup.ShouldContain("Page 2 of 2");
        NavManager.Uri.ShouldContain("page=2");
        NavManager.Uri.ShouldContain("tenant=tenant-a");
        NavManager.Uri.ShouldContain("eventType=Create");
        _ = JSInterop.VerifyInvoke("hexalithAdmin.getScrollTop");
        cut.WaitForAssertion(() => JSInterop.VerifyInvoke("hexalithAdmin.setScrollTop"), TimeSpan.FromSeconds(5));
        _ = _mockApiClient.Received(2).GetTenantsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void EventsPage_StatCards_ShowCorrectValues() {
        // Arrange — 3 streams, each with 2 events of different types
        List<StreamSummary> streams =
        [
            new("t1", "d1", "a1", 2, DateTimeOffset.UtcNow, 2, false, StreamStatus.Active),
            new("t2", "d2", "a2", 2, DateTimeOffset.UtcNow, 2, false, StreamStatus.Active),
            new("t1", "d1", "a3", 2, DateTimeOffset.UtcNow, 2, false, StreamStatus.Active),
        ];
        _ = _mockApiClient.GetRecentlyActiveStreamsAsync(
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PagedResult<StreamSummary>(streams, streams.Count, null)));

        foreach (StreamSummary s in streams) {
            List<TimelineEntry> timeline =
            [
                new(1, DateTimeOffset.UtcNow.AddMinutes(-2), TimelineEntryType.Event, "TypeA", "corr-1", null),
                new(2, DateTimeOffset.UtcNow.AddMinutes(-1), TimelineEntryType.Event, "TypeB", "corr-2", null),
            ];
            _ = _mockApiClient.GetStreamTimelineAsync(
                s.TenantId, s.Domain, s.AggregateId,
                Arg.Any<long?>(), Arg.Any<long?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(new PagedResult<TimelineEntry>(timeline, timeline.Count, null)));
        }

        _ = _mockApiClient.GetTenantsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TenantSummary>>([]));

        // Act
        IRenderedComponent<Events> cut = Render<Events>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Recent Events"), TimeSpan.FromSeconds(5));

        // Assert
        string markup = cut.Markup;
        // Recent Events: 3 streams * 2 events = 6
        markup.ShouldContain(">6<");
        // Unique Event Types: TypeA, TypeB = 2
        markup.ShouldContain(">2<");
        // Active Streams: 3 distinct stream identity combos
        markup.ShouldContain(">3<");
    }

    [Fact]
    public void EventsPage_ShowsFilteredEmptyState_WhenFiltersActive() {
        // Arrange
        SetupEmptyMocks();

        // Act
        NavManager.NavigateTo("/events?tenant=tenant-a");
        IRenderedComponent<Events> cut = Render<Events>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("No events found"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("No events found");
        cut.Markup.ShouldNotContain("No events recorded yet");
    }

    private void SetupEmptyMocks() {
        _ = _mockApiClient.GetRecentlyActiveStreamsAsync(
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PagedResult<StreamSummary>([], 0, null)));
        _ = _mockApiClient.GetTenantsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TenantSummary>>([]));
    }

    private void SetupMocksWithEvents(
        int streamCount,
        string? tenantPrefix = null,
        string domain = "counter",
        string? aggPrefix = null) {
        List<StreamSummary> streams = [];
        for (int i = 0; i < streamCount; i++) {
            string tenant = tenantPrefix ?? $"tenant-{i}";
            string agg = aggPrefix ?? $"agg-{i:D8}";
            streams.Add(new StreamSummary(
                tenant, domain, agg, 2, DateTimeOffset.UtcNow.AddMinutes(-i), 2, false, StreamStatus.Active));
        }

        _ = _mockApiClient.GetRecentlyActiveStreamsAsync(
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PagedResult<StreamSummary>(streams, streams.Count, null)));

        foreach (StreamSummary s in streams) {
            List<TimelineEntry> timeline =
            [
                new(1, DateTimeOffset.UtcNow.AddMinutes(-2), TimelineEntryType.Event, "TestEvent", "corr-001", null),
                new(2, DateTimeOffset.UtcNow.AddMinutes(-1), TimelineEntryType.Command, "TestCommand", "corr-001", null),
            ];
            _ = _mockApiClient.GetStreamTimelineAsync(
                s.TenantId, s.Domain, s.AggregateId,
                Arg.Any<long?>(), Arg.Any<long?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(new PagedResult<TimelineEntry>(timeline, timeline.Count, null)));
        }

        _ = _mockApiClient.GetTenantsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TenantSummary>>([]));
    }

    private static List<TimelineEntry> CreateTimelineEntries(int count, string eventTypeName) {
        List<TimelineEntry> timeline = [];
        for (int i = 0; i < count; i++) {
            timeline.Add(new TimelineEntry(
                i + 1,
                DateTimeOffset.UtcNow.AddMinutes(-count + i),
                TimelineEntryType.Event,
                eventTypeName,
                $"corr-{i + 1:D3}",
                null));
        }

        return timeline;
    }

    private Microsoft.AspNetCore.Components.NavigationManager NavManager =>
        Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();

    private static void RaiseRefresh(DashboardRefreshService refreshService, DashboardData data) {
        FieldInfo? eventField = typeof(DashboardRefreshService)
            .GetField("OnDataChanged", BindingFlags.Instance | BindingFlags.NonPublic);
        var handler = (Action<DashboardData>?)eventField?.GetValue(refreshService);
        handler?.Invoke(data);
    }
}
