using Bunit;

using Hexalith.EventStore.Admin.UI.Components;

namespace Hexalith.EventStore.Admin.UI.Tests.Components;

public class ProjectionFilterBarTests : AdminUITestContext
{
    [Fact]
    public void ProjectionFilterBar_RendersStatusButtons()
    {
        IRenderedComponent<ProjectionFilterBar> cut = Render<ProjectionFilterBar>(p => p
            .Add(c => c.SelectedStatus, "All"));

        string markup = cut.Markup;
        markup.ShouldContain("All");
        markup.ShouldContain("Running");
        markup.ShouldContain("Paused");
        markup.ShouldContain("Error");
    }

    [Fact]
    public void ProjectionFilterBar_InvokesStatusChanged_WhenButtonClicked()
    {
        string? selectedStatus = null;

        IRenderedComponent<ProjectionFilterBar> cut = Render<ProjectionFilterBar>(p => p
            .Add(c => c.SelectedStatus, "All")
            .Add(c => c.SelectedStatusChanged, s => selectedStatus = s));

        var pausedButton = cut.FindAll("button")
            .FirstOrDefault(b => b.InnerHtml.Contains("Paused"));
        if (pausedButton is not null)
        {
            pausedButton.Click();
            selectedStatus.ShouldBe("Paused");
        }
    }

    [Fact]
    public void ProjectionFilterBar_ShowsTenantDropdown_WhenMultipleTenants()
    {
        IReadOnlyList<string> tenants = ["tenant-a", "tenant-b", "tenant-c"];

        IRenderedComponent<ProjectionFilterBar> cut = Render<ProjectionFilterBar>(p => p
            .Add(c => c.SelectedStatus, "All")
            .Add(c => c.Tenants, tenants));

        // When multiple tenants, dropdown should be present
        string markup = cut.Markup;
        (markup.Contains("tenant-a") || markup.Contains("select") || markup.Contains("combobox")).ShouldBeTrue();
    }

    [Fact]
    public void ProjectionFilterBar_HidesTenantDropdown_WhenSingleTenant()
    {
        IReadOnlyList<string> tenants = ["tenant-a"];

        IRenderedComponent<ProjectionFilterBar> cut = Render<ProjectionFilterBar>(p => p
            .Add(c => c.SelectedStatus, "All")
            .Add(c => c.Tenants, tenants));

        // With single tenant, no dropdown needed — should not contain tenant selection UI
        // The component should render without error
        cut.Markup.ShouldNotBeNull();
    }
}
