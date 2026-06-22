using Bunit;

using Hexalith.EventStore.Admin.Abstractions.Models.Storage;
using Hexalith.EventStore.Admin.UI.Components;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.Admin.UI.Tests.Components;

/// <summary>
/// bUnit tests for the StorageTreemap component.
/// </summary>
public class StorageTreemapTests : AdminUITestContext {
    [Fact]
    public void StorageTreemap_RendersSvg_WithRoleAndAriaLabel() {
        // Arrange
        IReadOnlyList<StreamStorageInfo> data = CreateSampleData();

        // Act
        IRenderedComponent<StorageTreemap> cut = Render<StorageTreemap>(parameters => parameters
            .Add(p => p.Data, data));

        // Assert
        cut.Markup.ShouldContain("role=\"img\"");
        cut.Markup.ShouldContain("aria-label=\"Storage distribution");
    }

    [Fact]
    public void StorageTreemap_FiresOnAggregateTypeSelected_OnRectangleClick() {
        // Arrange
        string? selectedType = null;
        IReadOnlyList<StreamStorageInfo> data = CreateSampleData();

        // Act
        IRenderedComponent<StorageTreemap> cut = Render<StorageTreemap>(parameters => parameters
            .Add(p => p.Data, data)
            .Add(p => p.OnAggregateTypeSelected, EventCallback.Factory.Create<string>(this, val => selectedType = val)));

        // Find and click a treemap rectangle
        AngleSharp.Dom.IElement? rectElement = cut.Find("g.treemap-rect");
        _ = rectElement.ShouldNotBeNull();
        rectElement.Click();

        // Assert — should have been called with an aggregate type
        _ = selectedType.ShouldNotBeNull();
    }

    [Fact]
    public void StorageTreemap_InteractiveCells_HaveButtonSemanticsAndKeyboardAccess() {
        // Accessibility remediation (audit DV-1 / H-ES-4): the clickable treemap groups had no native
        // focus/role/keyboard affordance. They must now expose role="button", be focusable (tabindex=0),
        // carry an accessible name, and reflect selection state via aria-pressed.
        IReadOnlyList<StreamStorageInfo> data = CreateSampleData();

        IRenderedComponent<StorageTreemap> cut = Render<StorageTreemap>(parameters => parameters
            .Add(p => p.Data, data));

        AngleSharp.Dom.IElement cell = cut.Find("g.treemap-rect");
        cell.GetAttribute("role").ShouldBe("button");
        cell.GetAttribute("tabindex").ShouldBe("0");
        cell.GetAttribute("aria-label").ShouldNotBeNullOrWhiteSpace();
        cell.GetAttribute("aria-pressed").ShouldNotBeNull();
    }

    [Fact]
    public void StorageTreemap_InteractiveCell_FiresSelection_OnEnterKey() {
        // Keyboard activation parity: pressing Enter on a focusable treemap cell selects it.
        string? selectedType = null;
        IReadOnlyList<StreamStorageInfo> data = CreateSampleData();

        IRenderedComponent<StorageTreemap> cut = Render<StorageTreemap>(parameters => parameters
            .Add(p => p.Data, data)
            .Add(p => p.OnAggregateTypeSelected, EventCallback.Factory.Create<string>(this, val => selectedType = val)));

        cut.Find("g.treemap-rect").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Enter" });

        _ = selectedType.ShouldNotBeNull();
    }

    [Fact]
    public void StorageTreemap_RendersHiddenScreenReaderTable() {
        // Arrange
        IReadOnlyList<StreamStorageInfo> data = CreateSampleData();

        // Act
        IRenderedComponent<StorageTreemap> cut = Render<StorageTreemap>(parameters => parameters
            .Add(p => p.Data, data));

        // Assert
        cut.Markup.ShouldContain("class=\"sr-only\"");
        cut.Markup.ShouldContain("Storage distribution data");
        cut.Markup.ShouldContain("Order");
    }

    [Fact]
    public void StorageTreemap_BarChartFallback_WhenNotWideViewport() {
        // Arrange — set viewport to narrow
        ViewportService viewportService = Services.GetRequiredService<ViewportService>();
        viewportService.OnViewportWidthChanged(false);

        IReadOnlyList<StreamStorageInfo> data = CreateSampleData();

        // Act
        IRenderedComponent<StorageTreemap> cut = Render<StorageTreemap>(parameters => parameters
            .Add(p => p.Data, data));

        // Assert — bar chart mode shows "Bar Chart" button as accent
        cut.Markup.ShouldContain("Storage distribution bar chart");
    }

    [Fact]
    public void StorageTreemap_GroupsIntoOtherBucket_WhenOver500Types() {
        // Arrange — create 501 distinct aggregate types
        List<StreamStorageInfo> data = [];
        for (int i = 0; i < 501; i++) {
            data.Add(new StreamStorageInfo("t1", "Domain", $"agg-{i}", $"Type{i}", 100 + i, null, true, null));
        }

        // Act
        IRenderedComponent<StorageTreemap> cut = Render<StorageTreemap>(parameters => parameters
            .Add(p => p.Data, data));

        // Assert — should contain "Other" bucket
        cut.Markup.ShouldContain("Other");
    }

    private static IReadOnlyList<StreamStorageInfo> CreateSampleData() =>
    [
        new StreamStorageInfo("t1", "Sales", "agg-001", "Order", 5000, 1073741824, true, TimeSpan.FromHours(2)),
        new StreamStorageInfo("t1", "Sales", "agg-002", "Invoice", 3000, 536870912, false, null),
        new StreamStorageInfo("t1", "Inventory", "agg-003", "Product", 2000, 268435456, true, TimeSpan.FromDays(1)),
    ];
}
