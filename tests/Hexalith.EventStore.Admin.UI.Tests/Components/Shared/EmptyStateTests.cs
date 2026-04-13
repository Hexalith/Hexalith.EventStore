using Bunit;

using Hexalith.EventStore.Admin.UI.Components.Shared;

namespace Hexalith.EventStore.Admin.UI.Tests.Components.Shared;

public class EmptyStateTests : AdminUITestContext
{
    [Fact]
    public void EmptyState_RendersTitleAndDescription()
    {
        IRenderedComponent<EmptyState> cut = Render<EmptyState>(
            parameters => parameters
                .Add(p => p.Title, "No Streams Found")
                .Add(p => p.Description, "There are no event streams matching your query."));

        string markup = cut.Markup;
        markup.ShouldContain("No Streams Found");
        markup.ShouldContain("There are no event streams matching your query.");
    }

    [Fact]
    public void EmptyState_RendersFluentAnchorWithAccentAppearance()
    {
        // Migration regression marker: FluentAnchor with Appearance.Accent
        // v4: <fluent-anchor appearance="accent"> -> v5: FluentAnchorButton with ButtonAppearance.Primary
        IRenderedComponent<EmptyState> cut = Render<EmptyState>(
            parameters => parameters
                .Add(p => p.Title, "No Data")
                .Add(p => p.Description, "Start by creating your first stream.")
                .Add(p => p.ActionLabel, "Create Stream")
                .Add(p => p.ActionHref, "/streams/new"));

        string markup = cut.Markup;
        markup.ShouldContain("fluent-anchor");
        markup.ShouldContain("Create Stream");
        markup.ShouldContain("appearance=\"accent\"");
        markup.ShouldContain("/streams/new");
    }

    [Fact]
    public void EmptyState_HidesActionLinkWhenNotProvided()
    {
        IRenderedComponent<EmptyState> cut = Render<EmptyState>(
            parameters => parameters
                .Add(p => p.Title, "Empty")
                .Add(p => p.Description, "Nothing here."));

        string markup = cut.Markup;
        markup.ShouldNotContain("fluent-anchor");
    }
}
