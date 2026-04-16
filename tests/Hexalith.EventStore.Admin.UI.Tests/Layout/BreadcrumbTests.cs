using Bunit;

using Hexalith.EventStore.Admin.UI.Layout;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.Admin.UI.Tests.Layout;

/// <summary>
/// bUnit tests for the Breadcrumb component.
/// Story 15-8: Deep Linking and Context-Aware Breadcrumbs.
/// </summary>
public class BreadcrumbTests : AdminUITestContext {
    // ===== Merge-blocking tests =====

    [Fact]
    public void Breadcrumb_RendersSemanticLabel_ForStreamsRoute() {
        // AC 1, 2: Breadcrumb shows "Streams" with capital S (semantic label)
        NavigationManager navMan = Services.GetRequiredService<NavigationManager>();
        navMan.NavigateTo("/streams");

        IRenderedComponent<Breadcrumb> cut = Render<Breadcrumb>();

        // Verify semantic label is present
        AngleSharp.Dom.IElement current = cut.Find("[aria-current='page']");
        current.TextContent.ShouldBe("Streams");
    }

    [Fact]
    public void Breadcrumb_RendersMultiWordLabel_ForTypeCatalog() {
        // AC 2: Multi-word label "Type Catalog" for /types
        NavigationManager navMan = Services.GetRequiredService<NavigationManager>();
        navMan.NavigateTo("/types");

        IRenderedComponent<Breadcrumb> cut = Render<Breadcrumb>();

        cut.Markup.ShouldContain("Type Catalog");
    }

    [Fact]
    public void Breadcrumb_RendersDynamicSegments_InMonospace() {
        // AC 1: Dynamic segments use monospace CSS class
        NavigationManager navMan = Services.GetRequiredService<NavigationManager>();
        navMan.NavigateTo("/streams/tenant-acme/banking/agg-7f3b");

        IRenderedComponent<Breadcrumb> cut = Render<Breadcrumb>();

        string markup = cut.Markup;
        markup.ShouldContain("Streams");
        markup.ShouldContain("tenant-acme");
        markup.ShouldContain("banking");
        markup.ShouldContain("agg-7f3b");

        // Dynamic segments should have monospace class
        AngleSharp.Dom.IElement currentSegment = cut.Find("[aria-current='page']");
        currentSegment.TextContent.ShouldBe("agg-7f3b");
        currentSegment.ClassList.ShouldContain("monospace");
    }

    [Fact]
    public void Breadcrumb_RendersDeadLettersPath_Correctly() {
        // AC 1, 2: /health/dead-letters → Home / Health / Dead Letters
        NavigationManager navMan = Services.GetRequiredService<NavigationManager>();
        navMan.NavigateTo("/health/dead-letters");

        IRenderedComponent<Breadcrumb> cut = Render<Breadcrumb>();

        string markup = cut.Markup;
        markup.ShouldContain("Home");
        markup.ShouldContain("Health");
        markup.ShouldContain("Dead Letters");
    }

    [Fact]
    public void Breadcrumb_RendersNavElement_WithAriaLabel() {
        // AC 12: Container is <nav> with aria-label="Breadcrumb"
        NavigationManager navMan = Services.GetRequiredService<NavigationManager>();
        navMan.NavigateTo("/streams");

        IRenderedComponent<Breadcrumb> cut = Render<Breadcrumb>();

        AngleSharp.Dom.IElement nav = cut.Find("nav");
        _ = nav.ShouldNotBeNull();
        nav.GetAttribute("aria-label").ShouldBe("Breadcrumb");
    }

    [Fact]
    public void Breadcrumb_FinalSegment_HasAriaCurrentAndIsNotLink() {
        // AC 12: Final segment has aria-current="page" and is not a link
        NavigationManager navMan = Services.GetRequiredService<NavigationManager>();
        navMan.NavigateTo("/streams");

        IRenderedComponent<Breadcrumb> cut = Render<Breadcrumb>();

        AngleSharp.Dom.IElement current = cut.Find("[aria-current='page']");
        _ = current.ShouldNotBeNull();
        current.TagName.ShouldBe("SPAN");
        current.TextContent.ShouldBe("Streams");
    }

    [Fact]
    public void Breadcrumb_CopyButton_HasAriaLabel() {
        // AC 4, 12: Copy button has proper aria-label
        NavigationManager navMan = Services.GetRequiredService<NavigationManager>();
        navMan.NavigateTo("/streams");

        IRenderedComponent<Breadcrumb> cut = Render<Breadcrumb>();

        AngleSharp.Dom.IElement copyBtn = cut.Find("[aria-label='Copy page URL to clipboard']");
        _ = copyBtn.ShouldNotBeNull();
        copyBtn.GetAttribute("title").ShouldBe("Copy link");
    }

    [Fact]
    public void Breadcrumb_IsNotRendered_OnHomePage() {
        // AC 1: No breadcrumb on home page
        IRenderedComponent<Breadcrumb> cut = Render<Breadcrumb>();

        cut.Markup.ShouldNotContain("admin-breadcrumb");
    }

    // ===== Recommended tests =====

    [Fact]
    public void Breadcrumb_Truncation_ShowsEllipsisOnNarrowViewport() {
        // AC 3: Narrow viewport with 5+ segments shows "..." button
        NavigationManager navMan = Services.GetRequiredService<NavigationManager>();
        navMan.NavigateTo("/streams/tenant-acme/banking/agg-7f3b");

        IRenderedComponent<Breadcrumb> cut = Render<Breadcrumb>();

        // Simulate viewport narrowing AFTER component has initialized
        ViewportService viewportService = Services.GetRequiredService<ViewportService>();
        viewportService.OnViewportWidthChanged(false);

        cut.WaitForAssertion(() => {
            AngleSharp.Dom.IElement truncBtn = cut.Find("[aria-label='Show full breadcrumb path']");
            _ = truncBtn.ShouldNotBeNull();
            truncBtn.TextContent.ShouldBe("...");
        });
    }

    [Fact]
    public void Breadcrumb_Truncation_DoesNotShowEllipsis_AtFourSegments() {
        // AC 3 boundary: exactly 4 segments (Home + 3) should not truncate
        NavigationManager navMan = Services.GetRequiredService<NavigationManager>();
        navMan.NavigateTo("/streams/tenant-acme/banking");

        IRenderedComponent<Breadcrumb> cut = Render<Breadcrumb>();

        ViewportService viewportService = Services.GetRequiredService<ViewportService>();
        viewportService.OnViewportWidthChanged(false);

        cut.WaitForAssertion(() => cut.FindAll("[aria-label='Show full breadcrumb path']").Count.ShouldBe(0));
    }

    [Fact]
    public void Breadcrumb_Truncation_ExpandsWhenClicked() {
        // AC 3: Clicking "..." shows all segments
        NavigationManager navMan = Services.GetRequiredService<NavigationManager>();
        navMan.NavigateTo("/streams/tenant-acme/banking/agg-7f3b");

        IRenderedComponent<Breadcrumb> cut = Render<Breadcrumb>();

        ViewportService viewportService = Services.GetRequiredService<ViewportService>();
        viewportService.OnViewportWidthChanged(false);

        cut.WaitForAssertion(() => cut.Find("[aria-label='Show full breadcrumb path']").ShouldNotBeNull());

        AngleSharp.Dom.IElement truncBtn = cut.Find("[aria-label='Show full breadcrumb path']");
        truncBtn.Click();

        // After click, all segments should be visible
        string markup = cut.Markup;
        markup.ShouldContain("Home");
        markup.ShouldContain("Streams");
        markup.ShouldContain("tenant-acme");
        markup.ShouldContain("banking");
        markup.ShouldContain("agg-7f3b");

        // No truncation button after expanding
        cut.FindAll("[aria-label='Show full breadcrumb path']").Count.ShouldBe(0);
    }

    [Fact]
    public void Breadcrumb_IntermediateSegments_AreClickableLinks() {
        // AC 1: Each segment except the last is a clickable link
        NavigationManager navMan = Services.GetRequiredService<NavigationManager>();
        navMan.NavigateTo("/streams/tenant-acme/banking/agg-7f3b");

        IRenderedComponent<Breadcrumb> cut = Render<Breadcrumb>();

        IReadOnlyList<AngleSharp.Dom.IElement> links = cut.FindAll("a");
        links.Count.ShouldBeGreaterThanOrEqualTo(4); // Home, Streams, tenant-acme, banking

        // Verify Home link
        links[0].GetAttribute("href").ShouldBe("/");
        links[0].TextContent.ShouldBe("Home");

        // Verify Streams link
        links[1].GetAttribute("href").ShouldBe("/streams");
        links[1].TextContent.ShouldBe("Streams");
    }

    [Fact]
    public void Breadcrumb_Separator_HasAriaHidden() {
        // AC 12: Separator "/" has aria-hidden="true"
        NavigationManager navMan = Services.GetRequiredService<NavigationManager>();
        navMan.NavigateTo("/streams");

        IRenderedComponent<Breadcrumb> cut = Render<Breadcrumb>();

        AngleSharp.Dom.IElement separator = cut.Find(".separator");
        separator.GetAttribute("aria-hidden").ShouldBe("true");
        separator.TextContent.ShouldBe("/");
    }

    [Fact]
    public void Breadcrumb_CopyButton_InvokesJSInterop() {
        // AC 4: Copy button calls JS interop
        NavigationManager navMan = Services.GetRequiredService<NavigationManager>();
        navMan.NavigateTo("/streams");

        IRenderedComponent<Breadcrumb> cut = Render<Breadcrumb>();

        AngleSharp.Dom.IElement copyBtn = cut.Find("[aria-label='Copy page URL to clipboard']");
        copyBtn.Click();

        // JSInterop is in Loose mode, so invocations are accepted without explicit setup
        _ = JSInterop.VerifyInvoke("hexalithAdmin.copyToClipboard");
    }

    [Fact]
    public void Breadcrumb_Truncation_ResetsOnNavigation() {
        // AC 3: Truncation state resets on navigation change
        NavigationManager navMan = Services.GetRequiredService<NavigationManager>();
        navMan.NavigateTo("/streams/tenant-acme/banking/agg-7f3b");

        IRenderedComponent<Breadcrumb> cut = Render<Breadcrumb>();

        ViewportService viewportService = Services.GetRequiredService<ViewportService>();
        viewportService.OnViewportWidthChanged(false);

        cut.WaitForAssertion(() => cut.Find("[aria-label='Show full breadcrumb path']").ShouldNotBeNull());

        // Expand
        AngleSharp.Dom.IElement truncBtn = cut.Find("[aria-label='Show full breadcrumb path']");
        truncBtn.Click();

        // Verify expanded (no truncation button)
        cut.FindAll("[aria-label='Show full breadcrumb path']").Count.ShouldBe(0);

        // Navigate to a new deep path
        navMan.NavigateTo("/streams/tenant-other/finance/agg-abc1");

        // After navigation, should be truncated again (not expanded)
        cut.WaitForAssertion(() => {
            AngleSharp.Dom.IElement newTruncBtn = cut.Find("[aria-label='Show full breadcrumb path']");
            _ = newTruncBtn.ShouldNotBeNull();
        });
    }

    [Fact]
    public void Breadcrumb_UnknownSegments_RenderVerbatimInMonospace() {
        // AC 2: Unknown segments (not in dictionary) render verbatim with monospace
        NavigationManager navMan = Services.GetRequiredService<NavigationManager>();
        navMan.NavigateTo("/streams/my-tenant-id");

        IRenderedComponent<Breadcrumb> cut = Render<Breadcrumb>();

        string markup = cut.Markup;
        markup.ShouldContain("my-tenant-id");
        markup.ShouldContain("Streams"); // Known segment

        // The last segment (my-tenant-id) should have monospace
        AngleSharp.Dom.IElement current = cut.Find("[aria-current='page']");
        current.ClassList.ShouldContain("monospace");
    }
}
