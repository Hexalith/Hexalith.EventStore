using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.Admin.UI.Tests.Components;

/// <summary>
/// Focused regression tests for command palette open, fuzzy filtering, and navigation behavior.
/// </summary>
public class CommandPaletteTests : AdminUITestContext {
    [Fact]
    public void CommandPalette_Open_ShowsCatalogAndRequestsFocus() {
        IRenderedComponent<Hexalith.EventStore.Admin.UI.Components.CommandPalette> cut = Render<Hexalith.EventStore.Admin.UI.Components.CommandPalette>();

        cut.InvokeAsync(() => cut.Instance.Open());

        cut.WaitForAssertion(() => {
            cut.Markup.ShouldContain("Health Dashboard");
            cut.Markup.ShouldContain("Manage Tenants");
        });

        JSInterop.VerifyInvoke("hexalithAdmin.focusCommandPaletteSearch");
    }

    [Fact]
    public void CommandPaletteCatalog_Filter_FindsFuzzyTenantMatch() {
        IReadOnlyList<Hexalith.EventStore.Admin.UI.Components.CommandPaletteItem> results = Hexalith.EventStore.Admin.UI.Components.CommandPaletteCatalog.Filter("mng tnts");

        results.ShouldNotBeEmpty();
        results.First().Label.ShouldBe("Manage Tenants");
    }

    [Fact]
    public void CommandPalette_ClickingResult_NavigatesToSelectedRoute() {
        IRenderedComponent<Hexalith.EventStore.Admin.UI.Components.CommandPalette> cut = Render<Hexalith.EventStore.Admin.UI.Components.CommandPalette>();
        NavigationManager navigationManager = Services.GetRequiredService<NavigationManager>();

        cut.InvokeAsync(() => cut.Instance.Open());

        cut.WaitForAssertion(() => cut.FindAll("fluent-button").Count.ShouldBeGreaterThan(0));

        var commandButton = cut.FindAll("fluent-button").First(button => button.TextContent.Contains("Commands", StringComparison.Ordinal));
        commandButton.Click();

        navigationManager.Uri.ShouldEndWith("/commands");
    }
}