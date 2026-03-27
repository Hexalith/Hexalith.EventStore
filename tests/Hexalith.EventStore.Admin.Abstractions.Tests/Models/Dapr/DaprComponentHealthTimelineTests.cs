using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;
using Hexalith.EventStore.Admin.Abstractions.Models.Health;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.Dapr;

public class DaprComponentHealthTimelineTests
{
    [Fact]
    public void Empty_ReturnsTimelineWithNoData()
    {
        DaprComponentHealthTimeline empty = DaprComponentHealthTimeline.Empty;

        empty.Entries.ShouldBeEmpty();
        empty.HasData.ShouldBeFalse();
        empty.IsTruncated.ShouldBeFalse();
    }

    [Fact]
    public void Constructor_WithEntries_SetsHasDataTrue()
    {
        var entries = new List<DaprHealthHistoryEntry>
        {
            new("statestore", "state.redis", HealthStatus.Healthy, DateTimeOffset.UtcNow),
        };

        var timeline = new DaprComponentHealthTimeline(entries.AsReadOnly(), HasData: true);

        timeline.Entries.Count.ShouldBe(1);
        timeline.HasData.ShouldBeTrue();
        timeline.IsTruncated.ShouldBeFalse();
    }

    [Fact]
    public void Constructor_WithTruncated_SetsIsTruncatedTrue()
    {
        var entries = new List<DaprHealthHistoryEntry>
        {
            new("statestore", "state.redis", HealthStatus.Healthy, DateTimeOffset.UtcNow),
        };

        var timeline = new DaprComponentHealthTimeline(entries.AsReadOnly(), HasData: true, IsTruncated: true);

        timeline.IsTruncated.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_WithEmptyEntries_SetsHasDataFalse()
    {
        var timeline = new DaprComponentHealthTimeline([], HasData: false);

        timeline.Entries.ShouldBeEmpty();
        timeline.HasData.ShouldBeFalse();
    }
}
