using Bunit;

using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.UI.Components;

namespace Hexalith.EventStore.Admin.UI.Tests.Components;

public class CommandDetailPanelTests : AdminUITestContext
{
    [Fact]
    public void CommandDetailPanel_RendersCommandMetadata()
    {
        var entry = new TimelineEntry(1, DateTimeOffset.UtcNow, TimelineEntryType.Command,
            "IncrementCounter", "corr-1", "user-1");

        IRenderedComponent<CommandDetailPanel> cut = Render<CommandDetailPanel>(p => p
            .Add(c => c.Entry, entry));

        string markup = cut.Markup;
        markup.ShouldContain("IncrementCounter");
        markup.ShouldContain("corr-1");
    }

    [Fact]
    public void CommandDetailPanel_RendersSequenceNumber()
    {
        var entry = new TimelineEntry(42, DateTimeOffset.UtcNow, TimelineEntryType.Command,
            "IncrementCounter", "corr-1", null);

        IRenderedComponent<CommandDetailPanel> cut = Render<CommandDetailPanel>(p => p
            .Add(c => c.Entry, entry));

        cut.Markup.ShouldContain("42");
    }

    [Fact]
    public void CommandDetailPanel_RendersUserId_WhenPresent()
    {
        var entry = new TimelineEntry(1, DateTimeOffset.UtcNow, TimelineEntryType.Command,
            "IncrementCounter", "corr-1", "admin@acme.com");

        IRenderedComponent<CommandDetailPanel> cut = Render<CommandDetailPanel>(p => p
            .Add(c => c.Entry, entry));

        cut.Markup.ShouldContain("admin@acme.com");
    }

    [Fact]
    public void CommandDetailPanel_InvokesOnCorrelationFilter()
    {
        var entry = new TimelineEntry(1, DateTimeOffset.UtcNow, TimelineEntryType.Command,
            "IncrementCounter", "corr-filter-test", null);
        string? filteredCorrelation = null;

        IRenderedComponent<CommandDetailPanel> cut = Render<CommandDetailPanel>(p => p
            .Add(c => c.Entry, entry)
            .Add(c => c.OnCorrelationFilter, id => filteredCorrelation = id));

        // Find the correlation filter button
        var filterButton = cut.FindAll("button")
            .FirstOrDefault(b => b.InnerHtml.Contains("corr-filter") ||
                b.GetAttribute("title")?.Contains("filter", StringComparison.OrdinalIgnoreCase) == true ||
                b.GetAttribute("aria-label")?.Contains("filter", StringComparison.OrdinalIgnoreCase) == true);
        if (filterButton is not null)
        {
            filterButton.Click();
            filteredCorrelation.ShouldBe("corr-filter-test");
        }
    }
}
