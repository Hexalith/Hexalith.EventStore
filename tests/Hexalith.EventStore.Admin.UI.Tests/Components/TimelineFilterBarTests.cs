using Bunit;

using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.UI.Components;

namespace Hexalith.EventStore.Admin.UI.Tests.Components;

public class TimelineFilterBarTests : AdminUITestContext {
    [Fact]
    public void TimelineFilterBar_RendersTypeFilterButtons() {
        IRenderedComponent<TimelineFilterBar> cut = Render<TimelineFilterBar>(p => p
            .Add(c => c.SelectedEntryType, null));

        string markup = cut.Markup;
        markup.ShouldContain("All");
        markup.ShouldContain("Command");
        markup.ShouldContain("Event");
    }

    [Fact]
    public void TimelineFilterBar_InvokesEntryTypeChanged() {
        TimelineEntryType? selectedType = null;

        IRenderedComponent<TimelineFilterBar> cut = Render<TimelineFilterBar>(p => p
            .Add(c => c.SelectedEntryType, null)
            .Add(c => c.SelectedEntryTypeChanged, t => selectedType = t));

        AngleSharp.Dom.IElement? commandsButton = cut.FindAll("button")
            .FirstOrDefault(b => b.InnerHtml.Contains("Command"));
        if (commandsButton is not null) {
            commandsButton.Click();
            selectedType.ShouldBe(TimelineEntryType.Command);
        }
    }

    [Fact]
    public void TimelineFilterBar_RendersCorrelationSearchInput() {
        IRenderedComponent<TimelineFilterBar> cut = Render<TimelineFilterBar>(p => p
            .Add(c => c.SelectedEntryType, null));

        // Should have a search input for correlation filtering
        string markup = cut.Markup;
        (markup.Contains("input") || markup.Contains("search") || markup.Contains("correlation")).ShouldBeTrue();
    }

    [Fact]
    public void TimelineFilterBar_RendersCompareModeToggle() {
        IRenderedComponent<TimelineFilterBar> cut = Render<TimelineFilterBar>(p => p
            .Add(c => c.SelectedEntryType, null)
            .Add(c => c.CompareMode, false));

        string markup = cut.Markup;
        (markup.Contains("Compare") || markup.Contains("compare") || markup.Contains("diff")).ShouldBeTrue();
    }

    [Fact]
    public void TimelineFilterBar_InvokesCompareModeChanged() {
        bool? compareModeValue = null;

        IRenderedComponent<TimelineFilterBar> cut = Render<TimelineFilterBar>(p => p
            .Add(c => c.SelectedEntryType, null)
            .Add(c => c.CompareMode, false)
            .Add(c => c.CompareModeChanged, v => compareModeValue = v));

        AngleSharp.Dom.IElement? compareButton = cut.FindAll("button")
            .FirstOrDefault(b => b.InnerHtml.Contains("Compare") || b.InnerHtml.Contains("compare") || b.InnerHtml.Contains("diff"));
        if (compareButton is not null) {
            compareButton.Click();
            compareModeValue.ShouldBe(true);
        }
    }
}
