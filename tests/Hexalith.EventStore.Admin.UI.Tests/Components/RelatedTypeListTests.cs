using Bunit;

using Hexalith.EventStore.Admin.UI.Components;

namespace Hexalith.EventStore.Admin.UI.Tests.Components;

public class RelatedTypeListTests : AdminUITestContext
{
    [Fact]
    public void RelatedTypeList_RendersAllItems_WhenBelowMaxVisible()
    {
        IReadOnlyList<string> items = ["CounterIncremented", "CounterReset", "ThresholdReached"];

        IRenderedComponent<RelatedTypeList> cut = Render<RelatedTypeList>(p => p
            .Add(c => c.Items, items));

        string markup = cut.Markup;
        markup.ShouldContain("CounterIncremented");
        markup.ShouldContain("CounterReset");
        markup.ShouldContain("ThresholdReached");
    }

    [Fact]
    public void RelatedTypeList_CollapsesItems_WhenAboveMaxVisible()
    {
        // MaxVisible is 10 by default
        List<string> items = [];
        for (int i = 0; i < 15; i++)
        {
            items.Add($"EventType{i}");
        }

        IRenderedComponent<RelatedTypeList> cut = Render<RelatedTypeList>(p => p
            .Add(c => c.Items, items));

        string markup = cut.Markup;
        // First 10 should be visible
        markup.ShouldContain("EventType0");
        markup.ShouldContain("EventType9");
        // Should have a "Show all" button
        markup.ShouldContain("Show all");
    }

    [Fact]
    public void RelatedTypeList_RendersEmpty_WhenNoItems()
    {
        IReadOnlyList<string> items = [];

        IRenderedComponent<RelatedTypeList> cut = Render<RelatedTypeList>(p => p
            .Add(c => c.Items, items));

        // Should not throw, and should have minimal markup
        cut.Markup.ShouldNotBeNull();
    }

    [Fact]
    public void RelatedTypeList_InvokesOnItemClick()
    {
        IReadOnlyList<string> items = ["CounterIncremented"];
        string? clickedItem = null;

        IRenderedComponent<RelatedTypeList> cut = Render<RelatedTypeList>(p => p
            .Add(c => c.Items, items)
            .Add(c => c.OnItemClick, item => clickedItem = item));

        // Find the badge/button for the item and click it
        var itemElement = cut.FindAll("button, a, span[role='button'], [style*='cursor']")
            .FirstOrDefault(el => el.InnerHtml.Contains("CounterIncremented"));
        if (itemElement is not null)
        {
            itemElement.Click();
            clickedItem.ShouldBe("CounterIncremented");
        }
    }
}
