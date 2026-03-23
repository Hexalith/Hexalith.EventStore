using Bunit;

using Microsoft.AspNetCore.Components;

namespace Hexalith.EventStore.Admin.UI.Tests.Layout;

/// <summary>
/// Test 9.3: MainLayout renders FluentLayout + FluentHeader + FluentNavMenu (AC: 2).
/// Merge-blocking test.
/// </summary>
public class MainLayoutTests : AdminUITestContext {
    [Fact]
    public void MainLayout_RendersFluentLayout() {
        IRenderedComponent<Hexalith.EventStore.Admin.UI.Layout.MainLayout> cut = Render<Hexalith.EventStore.Admin.UI.Layout.MainLayout>(
            parameters => parameters.Add(p => p.Body, (RenderFragment)(builder => builder.AddContent(0, "Test Content"))));

        string markup = cut.Markup;
        markup.ShouldContain("Hexalith EventStore Admin");
        markup.ShouldContain("Test Content");
    }

    [Fact]
    public void MainLayout_RendersSkipToMainContentLink() {
        IRenderedComponent<Hexalith.EventStore.Admin.UI.Layout.MainLayout> cut = Render<Hexalith.EventStore.Admin.UI.Layout.MainLayout>(
            parameters => parameters.Add(p => p.Body, (RenderFragment)(builder => builder.AddContent(0, "Content"))));

        string markup = cut.Markup;
        markup.ShouldContain("Skip to main content");
        markup.ShouldContain("skip-to-main");
    }

    [Fact]
    public void MainLayout_RendersNavMenu() {
        IRenderedComponent<Hexalith.EventStore.Admin.UI.Layout.MainLayout> cut = Render<Hexalith.EventStore.Admin.UI.Layout.MainLayout>(
            parameters => parameters.Add(p => p.Body, (RenderFragment)(builder => builder.AddContent(0, "Content"))));

        string markup = cut.Markup;
        markup.ShouldContain("Home");
        markup.ShouldContain("Commands");
        markup.ShouldContain("Events");
        markup.ShouldContain("Health");
        markup.ShouldContain("Services");
        markup.ShouldContain("Tenants");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Settings"));
    }

    [Fact]
    public void MainLayout_RendersMainContentArea() {
        IRenderedComponent<Hexalith.EventStore.Admin.UI.Layout.MainLayout> cut = Render<Hexalith.EventStore.Admin.UI.Layout.MainLayout>(
            parameters => parameters.Add(p => p.Body, (RenderFragment)(builder => builder.AddContent(0, "Content"))));

        cut.Find("#main-content").ShouldNotBeNull();
    }

    [Fact]
    public void MainLayout_DoesNotRenderBreadcrumbOnHomePage() {
        IRenderedComponent<Hexalith.EventStore.Admin.UI.Layout.MainLayout> cut = Render<Hexalith.EventStore.Admin.UI.Layout.MainLayout>(
            parameters => parameters.Add(p => p.Body, (RenderFragment)(builder => builder.AddContent(0, "Content"))));

        cut.Markup.ShouldNotContain("admin-breadcrumb");
    }
}
