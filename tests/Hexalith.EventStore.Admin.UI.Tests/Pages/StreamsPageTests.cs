using Bunit;

using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;
using Hexalith.EventStore.Admin.UI.Pages;
using Hexalith.EventStore.Admin.UI.Services;
using Hexalith.EventStore.SignalR;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.UI.Tests.Pages;

/// <summary>
/// bUnit tests for the Streams page.
/// </summary>
public class StreamsPageTests : AdminUITestContext
{
    private readonly AdminStreamApiClient _mockApiClient;

    public StreamsPageTests()
    {
        _mockApiClient = Substitute.For<AdminStreamApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminStreamApiClient>.Instance);
        Services.AddScoped(_ => _mockApiClient);
        Services.AddScoped<DashboardRefreshService>();
        TestSignalRClient testClient = new();
        Services.AddSingleton(testClient);
        Services.AddSingleton(testClient.Inner);
    }

    [Fact]
    public void StreamsPage_RendersDataGridWithColumns()
    {
        // Arrange
        PagedResult<StreamSummary> streams = CreateStreamsResult(3);
        _ = _mockApiClient.GetRecentlyActiveStreamsAsync(
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(streams));
        _ = _mockApiClient.GetTenantsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TenantSummary>>([]));

        // Act
        IRenderedComponent<Streams> cut = Render<Streams>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("tenant-0"), TimeSpan.FromSeconds(5));

        // Assert
        string markup = cut.Markup;
        markup.ShouldContain("Status");
        markup.ShouldContain("Tenant");
        markup.ShouldContain("Aggregate ID");
        markup.ShouldContain("Events");
        markup.ShouldContain("Last Activity");
        markup.ShouldContain("tenant-0");
    }

    [Fact]
    public void StreamsPage_StatusBadge_MapsStreamStatusCorrectly()
    {
        // Arrange
        List<StreamSummary> items =
        [
            new("t1", "d1", "agg-001", 1, DateTimeOffset.UtcNow, 10, false, StreamStatus.Active),
            new("t2", "d2", "agg-002", 2, DateTimeOffset.UtcNow, 20, false, StreamStatus.Idle),
            new("t3", "d3", "agg-003", 3, DateTimeOffset.UtcNow, 30, false, StreamStatus.Tombstoned),
        ];
        _ = _mockApiClient.GetRecentlyActiveStreamsAsync(
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PagedResult<StreamSummary>(items, 3, null)));
        _ = _mockApiClient.GetTenantsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TenantSummary>>([]));

        // Act
        IRenderedComponent<Streams> cut = Render<Streams>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Tombstoned"), TimeSpan.FromSeconds(5));

        // Assert — status labels present
        string markup = cut.Markup;
        markup.ShouldContain("Active");
        markup.ShouldContain("Idle");
        markup.ShouldContain("Tombstoned");
    }

    [Fact]
    public void StreamsPage_ShowsEmptyState_WhenNoStreams()
    {
        // Arrange
        _ = _mockApiClient.GetRecentlyActiveStreamsAsync(
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PagedResult<StreamSummary>([], 0, null)));
        _ = _mockApiClient.GetTenantsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TenantSummary>>([]));

        // Act
        IRenderedComponent<Streams> cut = Render<Streams>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("No streams found"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("No streams found");
    }

    [Fact]
    public void StreamsPage_ShowsPagination_WhenMoreThan25Streams()
    {
        // Arrange
        PagedResult<StreamSummary> streams = CreateStreamsResult(30);
        _ = _mockApiClient.GetRecentlyActiveStreamsAsync(
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(streams));
        _ = _mockApiClient.GetTenantsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TenantSummary>>([]));

        // Act
        IRenderedComponent<Streams> cut = Render<Streams>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Page 1 of 2"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Page 1 of 2");
        cut.Markup.ShouldContain("30 total");
    }

    [Fact]
    public void StreamsPage_AggregateId_IsTruncated()
    {
        // Arrange
        List<StreamSummary> items =
        [
            new("t1", "d1", "abcdefghijklmnop", 1, DateTimeOffset.UtcNow, 10, false, StreamStatus.Active),
        ];
        _ = _mockApiClient.GetRecentlyActiveStreamsAsync(
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PagedResult<StreamSummary>(items, 1, null)));
        _ = _mockApiClient.GetTenantsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TenantSummary>>([]));

        // Act
        IRenderedComponent<Streams> cut = Render<Streams>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("abcdefgh"), TimeSpan.FromSeconds(5));

        // Assert — truncated to 8 chars + ellipsis
        cut.Markup.ShouldContain("abcdefgh\u2026");
        // Full ID in tooltip
        cut.Markup.ShouldContain("title=\"abcdefghijklmnop\"");
    }

    private static PagedResult<StreamSummary> CreateStreamsResult(int count)
    {
        List<StreamSummary> items = [];
        for (int i = 0; i < count; i++)
        {
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
