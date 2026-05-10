using Bunit;

using Hexalith.EventStore.Admin.UI.Components.Shared;

namespace Hexalith.EventStore.Admin.UI.Tests.Components.Shared;

public class StatCardTests : AdminUITestContext {
    [Fact]
    public void StatCard_RendersLabelAndValue() {
        IRenderedComponent<StatCard> cut = Render<StatCard>(
            parameters => parameters
                .Add(p => p.Label, "Total Events")
                .Add(p => p.Value, "1,234"));

        string markup = cut.Markup;
        markup.ShouldContain("Total Events");
        markup.ShouldContain("1,234");
    }

    [Theory]
    [InlineData("success", "color: var(--hexalith-status-success)")]
    [InlineData("accent", "color: var(--hexalith-status-inflight)")]
    [InlineData("warning", "color: var(--hexalith-status-warning)")]
    [InlineData("error", "color: var(--hexalith-status-error)")]
    [InlineData("neutral", "color: var(--colorNeutralForeground1)")]
    public void StatCard_AppliesSeverityBasedInlineColorStyle(string severity, string expectedStyle) {
        IRenderedComponent<StatCard> cut = Render<StatCard>(
            parameters => parameters
                .Add(p => p.Label, "Metric")
                .Add(p => p.Value, "42")
                .Add(p => p.Severity, severity));

        string markup = cut.Markup;
        markup.ShouldContain(expectedStyle);

        // Migration regression marker: v4 renders <fluent-card> web component tag
        markup.ShouldContain("fluent-card");
    }

    [Fact]
    public void StatCard_ShowsSkeletonWhenLoading() {
        IRenderedComponent<StatCard> cut = Render<StatCard>(
            parameters => parameters
                .Add(p => p.Label, "Loading Metric")
                .Add(p => p.Value, "99")
                .Add(p => p.IsLoading, true));

        string markup = cut.Markup;

        // SkeletonCard renders FluentSkeleton components instead of value
        markup.ShouldContain("fluent-skeleton");

        // Value should not be visible when loading (SkeletonCard replaces the value/label spans)
        markup.ShouldNotContain(">99<");
    }

    [Fact]
    public void StatCard_RendersAccessibilityLiveRegion() {
        IRenderedComponent<StatCard> cut = Render<StatCard>(
            parameters => parameters
                .Add(p => p.Label, "Active Streams")
                .Add(p => p.Value, "56"));

        string markup = cut.Markup;
        markup.ShouldContain("aria-live=\"polite\"");
    }

    [Fact]
    public void StatCard_ClearsStaleLiveAnnouncement_WhenValueChanges() {
        IRenderedComponent<StatCard> cut = Render<StatCard>(
            parameters => parameters
                .Add(p => p.Label, "Total Checks")
                .Add(p => p.Value, "0"));

        typeof(StatCard)
            .GetField("_liveAnnouncement", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(cut.Instance, "Total Checks: 0");

        cut.Render(parameters => parameters
            .Add(p => p.Label, "Total Checks")
            .Add(p => p.Value, "1"));

        cut.Markup.ShouldNotContain("Total Checks: 0");
        cut.Markup.ShouldContain("aria-label=\"Total Checks: 1\"");
    }
}
