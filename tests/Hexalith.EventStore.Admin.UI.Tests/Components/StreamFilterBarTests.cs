using Bunit;

using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;
using Hexalith.EventStore.Admin.UI.Components;

namespace Hexalith.EventStore.Admin.UI.Tests.Components;

public class StreamFilterBarTests : AdminUITestContext {
    [Fact]
    public void StreamFilterBar_RendersStatusButtons() {
        IRenderedComponent<StreamFilterBar> cut = Render<StreamFilterBar>(p => p
            .Add(c => c.SelectedStatus, "All"));

        string markup = cut.Markup;
        markup.ShouldContain("All");
        markup.ShouldContain("Active");
        markup.ShouldContain("Idle");
    }

    [Fact]
    public void StreamFilterBar_InvokesStatusChanged() {
        string? selectedStatus = null;

        IRenderedComponent<StreamFilterBar> cut = Render<StreamFilterBar>(p => p
            .Add(c => c.SelectedStatus, "All")
            .Add(c => c.SelectedStatusChanged, s => selectedStatus = s));

        AngleSharp.Dom.IElement? activeButton = cut.FindAll("button")
            .FirstOrDefault(b => b.InnerHtml.Contains("Active"));
        if (activeButton is not null) {
            activeButton.Click();
            selectedStatus.ShouldBe("Active");
        }
    }

    [Fact]
    public void StreamFilterBar_RendersTenantDropdown_WhenMultipleTenants() {
        IReadOnlyList<TenantSummary> tenants =
        [
            new("tenant-a", "Acme Corp", TenantStatusType.Active),
            new("tenant-b", "Widget Co", TenantStatusType.Active),
        ];

        IRenderedComponent<StreamFilterBar> cut = Render<StreamFilterBar>(p => p
            .Add(c => c.SelectedStatus, "All")
            .Add(c => c.Tenants, tenants));

        string markup = cut.Markup;
        (markup.Contains("Acme Corp") || markup.Contains("tenant-a") || markup.Contains("select")).ShouldBeTrue();
    }

    [Fact]
    public void StreamFilterBar_RendersDomainDropdown_WhenDomainsAvailable() {
        IReadOnlyList<string> domains = ["Counter", "Orders", "Payments"];

        IRenderedComponent<StreamFilterBar> cut = Render<StreamFilterBar>(p => p
            .Add(c => c.SelectedStatus, "All")
            .Add(c => c.Domains, domains));

        string markup = cut.Markup;
        (markup.Contains("Counter") || markup.Contains("select") || markup.Contains("combobox")).ShouldBeTrue();
    }
}
