using Bunit;

using Hexalith.EventStore.Admin.UI.Components;
using Hexalith.EventStore.Admin.UI.Models;

using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.Admin.UI.Tests.Components;

/// <summary>
/// bUnit tests for the ActivityChart component.
/// </summary>
public class ActivityChartTests : AdminUITestContext {
    [Fact]
    public void ActivityChart_WithBuckets_RendersCorrectNumberOfBars() {
        // Arrange
        DateTimeOffset now = DateTimeOffset.UtcNow;
        List<ActivityBucket> buckets = [];
        for (int i = 0; i < 24; i++) {
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
    public void ActivityChart_WithBuckets_HasAriaLabels() {
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
    public void ActivityChart_WithBuckets_HasTitleTooltips() {
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
    public void ActivityChart_InteractiveBars_AreExposedAsButtonsInLabelledGroup_NotImageRole() {
        // Accessibility remediation (audit C3 / H-ES-3): the interactive bars must not be nested inside
        // a role="img" container (which would collapse the chart into a single image and hide the
        // buttons from assistive technology). They live in a labelled role="group" of real buttons; the
        // sr-only table remains the non-visual data alternative.
        DateTimeOffset now = new(2026, 3, 23, 14, 0, 0, TimeSpan.Zero);
        List<ActivityBucket> buckets = [new(now, now.AddHours(1), 5)];

        IRenderedComponent<ActivityChart> cut = Render<ActivityChart>(
            p => p.Add(c => c.Buckets, buckets));

        AngleSharp.Dom.IElement bars = cut.Find(".activity-chart-bars");
        bars.GetAttribute("role").ShouldBe("group");
        bars.GetAttribute("aria-label").ShouldNotBeNullOrWhiteSpace();
        cut.Markup.ShouldNotContain("role=\"img\"");

        // Each bar is a real <button> (keyboard-focusable / activatable natively) carrying a
        // data-testid — backing the AdminUiFluentConformanceTests carve-out (aria-label + data-testid).
        cut.FindAll(".activity-chart-bar-wrapper").Count.ShouldBe(1);
        AngleSharp.Dom.IElement bar = cut.Find(".activity-chart-bar-wrapper");
        bar.NodeName.ShouldBe("BUTTON");
        bar.GetAttribute("data-testid").ShouldBe("activity-chart-bar");
        // The sr-only data table text alternative is still present.
        cut.Markup.ShouldContain("Stream activity data for the last 24 hours");
    }

    [Fact]
    public void ActivityChart_BarClick_NavigatesToStreamsForHour() {
        DateTimeOffset now = new(2026, 3, 23, 14, 0, 0, TimeSpan.Zero);
        List<ActivityBucket> buckets = [new(now, now.AddHours(1), 5)];

        IRenderedComponent<ActivityChart> cut = Render<ActivityChart>(
            p => p.Add(c => c.Buckets, buckets));
        Microsoft.AspNetCore.Components.NavigationManager nav =
            Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();

        cut.Find(".activity-chart-bar-wrapper").Click();

        nav.Uri.ShouldContain("/streams?start=");
    }

    [Fact]
    public void ActivityChart_AllZeroBuckets_ShowsEmptyState() {
        // Arrange
        DateTimeOffset now = DateTimeOffset.UtcNow;
        List<ActivityBucket> buckets = [];
        for (int i = 0; i < 24; i++) {
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
    public void ActivityChart_Loading_ShowsSkeletonBars() {
        // Act
        IRenderedComponent<ActivityChart> cut = Render<ActivityChart>(
            p => p.Add(c => c.IsLoading, true));

        // Assert
        cut.FindAll(".activity-chart-skeleton-bar").Count.ShouldBeGreaterThan(0);
    }
}
