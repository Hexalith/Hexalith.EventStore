using Bunit;

using Hexalith.EventStore.Admin.UI.Layout;

namespace Hexalith.EventStore.Admin.UI.Tests.Layout;

public class NavMenuTests : AdminUITestContext
{
    [Fact]
    public void NavMenu_RendersWithoutException()
    {
        IRenderedComponent<NavMenu> cut = Render<NavMenu>(
            parameters => parameters
                .Add(p => p.Width, "220px")
                .Add(p => p.UserRole, AdminRole.Admin));

        cut.Markup.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void NavMenu_ContainsExpectedNavigationLinks()
    {
        IRenderedComponent<NavMenu> cut = Render<NavMenu>(
            parameters => parameters
                .Add(p => p.Width, "220px")
                .Add(p => p.UserRole, AdminRole.Admin));

        // Use WaitForAssertion since topology section loads async
        cut.WaitForAssertion(() =>
        {
            string markup = cut.Markup;

            // Core navigation
            markup.ShouldContain("Home");
            markup.ShouldContain("Commands");
            markup.ShouldContain("Events");
            markup.ShouldContain("Streams");
            markup.ShouldContain("Health");
            markup.ShouldContain("Tenants");

            // Services & infra
            markup.ShouldContain("Services");
            markup.ShouldContain("Projections");
            markup.ShouldContain("Types");
            markup.ShouldContain("DAPR");

            // DBA operations
            markup.ShouldContain("Storage");
            markup.ShouldContain("Snapshots");
            markup.ShouldContain("Backups");
            markup.ShouldContain("Compaction");
            markup.ShouldContain("Consistency");

            // Admin
            markup.ShouldContain("Settings");
        }, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void NavMenu_RendersV4StructuralElements()
    {
        IRenderedComponent<NavMenu> cut = Render<NavMenu>(
            parameters => parameters
                .Add(p => p.Width, "220px")
                .Add(p => p.UserRole, AdminRole.Admin));

        string markup = cut.Markup;

        // v4 Fluent UI renders as kebab-case web component tags
        // These are the primary migration regression markers:
        // v4: fluent-nav-menu  -> v5: fluent-nav
        // v4: fluent-nav-link  -> v5: fluent-nav-item
        // v4: fluent-nav-group -> v5: fluent-nav-category
        markup.ShouldContain("fluent-nav-menu");
        markup.ShouldContain("fluent-nav-link");
        markup.ShouldContain("fluent-nav-group");
    }

    [Fact]
    public void NavMenu_SettingsHiddenForNonAdminRole()
    {
        IRenderedComponent<NavMenu> cut = Render<NavMenu>(
            parameters => parameters
                .Add(p => p.Width, "220px")
                .Add(p => p.UserRole, AdminRole.ReadOnly));

        cut.WaitForAssertion(() =>
        {
            // NavMenu should render navigation but NOT the Settings link for ReadOnly role
            string markup = cut.Markup;
            markup.ShouldContain("Home");
            markup.ShouldNotContain("Settings");
        }, TimeSpan.FromSeconds(5));
    }
}
