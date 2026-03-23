using Bunit;

using Hexalith.EventStore.Admin.UI.Components;
using Hexalith.EventStore.Admin.UI.Models;

namespace Hexalith.EventStore.Admin.UI.Tests.Components;

/// <summary>
/// bUnit tests for the ActivityChart component.
/// </summary>
public class ActivityChartTests : AdminUITestContext
{
    [Fact]
    public void ActivityChart_WithBuckets_RendersCorrectNumberOfBars()
    {
        // Arrange
        DateTimeOffset now = DateTimeOffset.UtcNow;
        List<ActivityBucket> buckets = [];
        for (int i = 0; i < 24; i++)
        {
            buckets.Add(new ActivityBucket(
                now.AddHours(-24 + i),
                now.AddHours(-23 + i),
                i * 2));
        }

        // Act
        IRenderedComponent<ActivityChart> cut = Render<ActivityChart>(
            p => p.Add(c => c.Buckets, buckets));

        // Assert
        cut.FindAll(".activity-chart-bar-wrapper").Count.ShouldBe(24);
    }

    [Fact]
    public void ActivityChart_WithBuckets_HasAriaLabels()
    {
        // Arrange
        DateTimeOffset now = new(2026, 3, 23, 14, 0, 0, TimeSpan.Zero);
        List<ActivityBucket> buckets =
        [
            new(now, now.AddHours(1), 5),
        ];

        // Act
        IRenderedComponent<ActivityChart> cut = Render<ActivityChart>(
            p => p.Add(c => c.Buckets, buckets));

        // Assert
        string markup = cut.Markup;
        markup.ShouldContain("aria-label=\"14:00-15:00: 5 active streams\"");
    }

    [Fact]
    public void ActivityChart_WithBuckets_HasTitleTooltips()
    {
        // Arrange
        DateTimeOffset now = new(2026, 3, 23, 10, 0, 0, TimeSpan.Zero);
        List<ActivityBucket> buckets =
        [
            new(now, now.AddHours(1), 3),
        ];

        // Act
        IRenderedComponent<ActivityChart> cut = Render<ActivityChart>(
            p => p.Add(c => c.Buckets, buckets));

        // Assert
        cut.Markup.ShouldContain("title=\"10:00-11:00: 3 streams\"");
    }

    [Fact]
    public void ActivityChart_AllZeroBuckets_ShowsEmptyState()
    {
        // Arrange
        DateTimeOffset now = DateTimeOffset.UtcNow;
        List<ActivityBucket> buckets = [];
        for (int i = 0; i < 24; i++)
        {
            buckets.Add(new ActivityBucket(now.AddHours(-24 + i), now.AddHours(-23 + i), 0));
        }

        // Act
        IRenderedComponent<ActivityChart> cut = Render<ActivityChart>(
            p => p.Add(c => c.Buckets, buckets));

        // Assert
        cut.Markup.ShouldContain("No stream activity in the last 24 hours");
        cut.FindAll(".activity-chart-bar-wrapper").Count.ShouldBe(0);
    }

    [Fact]
    public void ActivityChart_Loading_ShowsSkeletonBars()
    {
        // Act
        IRenderedComponent<ActivityChart> cut = Render<ActivityChart>(
            p => p.Add(c => c.IsLoading, true));

        // Assert
        cut.FindAll(".activity-chart-skeleton-bar").Count.ShouldBeGreaterThan(0);
    }
}
