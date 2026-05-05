using Bunit;

using Microsoft.JSInterop;

namespace Hexalith.EventStore.Admin.UI.Tests.Layout;

// ATDD red-phase scaffolds for story:
//   _bmad-output/implementation-artifacts/post-epic-deferred-dw5-admin-ui-runtime-follow-ups.md
//
// AC#5 — Ctrl+B sidebar toggle works on the Blazor renderer context. The MainLayout
//        OnToggleSidebarShortcut must dispatch state mutation + StateHasChanged on the
//        renderer synchronization context. The deferred-work hypothesis is that the
//        current `ConfigureAwait(false)` chain causes Ctrl+B to resume off-context.
// AC#6 — Persistence is viewport-tier-scoped. Storage key MUST be
//        `hexalith-sidebar-collapsed-{tier}` where tier is one of optimal/standard/
//        compact/minimum based on `hexalithAdmin.getViewportWidth`.
// AC#7 — Ctrl+K command palette must remain non-regressive when shortcut handling
//        changes. The deterministic part of this contract is that MainLayout still
//        exposes the JSInvokable OnCommandPaletteShortcut entry point.
//
// These scaffolds are bUnit-level. Browser-runtime parts (repeated keystrokes, circuit
// errors, refresh persistence, same-session Ctrl+K) live in
// tests/Hexalith.EventStore.Admin.UI.E2E/Dw5SidebarShortcutBrowserAtddTests.cs.
//
// Skip rationale: tests are marked [Fact(Skip = "...")] until DW5 delivers the renderer-
// context fix, the per-tier storage key proof, and the same-session Ctrl+K coexistence
// proof. Removing Skip per AC unmarks the gate and keeps it green during regression.
public class Dw5SidebarShortcutAtddTests : AdminUITestContext {
    private const string _ac5SkipReason =
        "ATDD red phase — DW5 AC#5 (Ctrl+B renderer-context). Remove Skip after MainLayout.OnToggleSidebarShortcut "
        + "keeps state mutation + StateHasChanged on the Blazor renderer context (e.g. removes ConfigureAwait(false) "
        + "before StateHasChanged or routes through InvokeAsync).";
    private const string _ac6SkipReason =
        "ATDD red phase — DW5 AC#6 (viewport-tier storage key). Remove Skip after MainLayout proves the storage key "
        + "stays as hexalith-sidebar-collapsed-{tier} for each viewport tier (optimal/standard/compact/minimum).";
    private const string _ac6CompactDefaultSkipReason =
        "ATDD red phase — DW5 AC#6 (compact-tier default collapse). Remove Skip after MainLayout proves compact-tier "
        + "viewport defaults to collapsed when no saved state exists.";
    private const string _ac7SkipReason =
        "ATDD red phase — DW5 AC#7 (Ctrl+K coexistence). Remove Skip after MainLayout still exposes the JSInvokable "
        + "OnCommandPaletteShortcut entry point alongside the new Ctrl+B fix.";

    [Fact(Skip = _ac5SkipReason)]
    public async Task OnToggleSidebarShortcut_FlipsCollapseStateAndDispatchesRerender() {
        // AC#5 — The shortcut MUST: (a) flip the _sidebarCollapsed boolean, (b) write
        // the new value to local storage via hexalithAdmin.setLocalStorage, and (c)
        // trigger a rerender on the Blazor synchronization context.
        // bUnit captures (a) and (b) deterministically; (c) is observable by asserting
        // the layout markup width transitions between "220px" and "48px" without throwing
        // InvalidOperationException("StateHasChanged was called outside of the renderer
        // synchronization context").
        IRenderedComponent<Hexalith.EventStore.Admin.UI.Layout.MainLayout> cut = Render<Hexalith.EventStore.Admin.UI.Layout.MainLayout>(
            parameters => parameters.Add(p => p.Body, builder => builder.AddContent(0, "Body")));

        cut.WaitForAssertion(
            () => cut.Markup.ShouldContain("220px"),
            TimeSpan.FromSeconds(5));

        await cut.InvokeAsync(() => cut.Instance.OnToggleSidebarShortcut());

        cut.WaitForAssertion(
            () => cut.Markup.ShouldContain("48px"),
            TimeSpan.FromSeconds(5));
    }

    [Fact(Skip = _ac5SkipReason)]
    public async Task OnToggleSidebarShortcut_RepeatedTogglesAlternateState() {
        // AC#5 — Repeated invocation must alternate state without losing dispatch on
        // the renderer context. Two consecutive toggles return to the original collapsed
        // state and produce the original markup width.
        IRenderedComponent<Hexalith.EventStore.Admin.UI.Layout.MainLayout> cut = Render<Hexalith.EventStore.Admin.UI.Layout.MainLayout>(
            parameters => parameters.Add(p => p.Body, builder => builder.AddContent(0, "Body")));

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("220px"), TimeSpan.FromSeconds(5));

        await cut.InvokeAsync(() => cut.Instance.OnToggleSidebarShortcut());
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("48px"), TimeSpan.FromSeconds(5));

        await cut.InvokeAsync(() => cut.Instance.OnToggleSidebarShortcut());
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("220px"), TimeSpan.FromSeconds(5));
    }

    [Theory(Skip = _ac6SkipReason)]
    [InlineData(2560, "hexalith-sidebar-collapsed-optimal")]
    [InlineData(1920, "hexalith-sidebar-collapsed-optimal")]
    [InlineData(1500, "hexalith-sidebar-collapsed-standard")]
    [InlineData(1280, "hexalith-sidebar-collapsed-standard")]
    [InlineData(1100, "hexalith-sidebar-collapsed-compact")]
    [InlineData(960, "hexalith-sidebar-collapsed-compact")]
    [InlineData(800, "hexalith-sidebar-collapsed-minimum")]
    public async Task OnToggleSidebarShortcut_PersistsUnderViewportTierKey(int viewportWidth, string expectedKey) {
        // AC#6 — Storage key MUST be hexalith-sidebar-collapsed-{tier} per viewport tier.
        // The bUnit JSInterop layer captures the setLocalStorage call so we can assert the
        // exact key the layout used. This pins the contract that all four tiers map to
        // distinct keys and that no future change collapses tiers into one global key.
        _ = JSInterop.Setup<int>("hexalithAdmin.getViewportWidth", _ => true).SetResult(viewportWidth);

        IRenderedComponent<Hexalith.EventStore.Admin.UI.Layout.MainLayout> cut = Render<Hexalith.EventStore.Admin.UI.Layout.MainLayout>(
            parameters => parameters.Add(p => p.Body, builder => builder.AddContent(0, "Body")));

        await cut.InvokeAsync(() => cut.Instance.OnToggleSidebarShortcut());

        JSRuntimeInvocation invocation = JSInterop.Invocations
            .Where(i => i.Identifier == "hexalithAdmin.setLocalStorage")
            .ShouldHaveSingleItem(
                customMessage: "DW5 AC#6 expects exactly one setLocalStorage call per shortcut invocation.");
        invocation.Arguments.ShouldNotBeNull();
        invocation.Arguments[0].ShouldBe(expectedKey,
            customMessage: $"DW5 AC#6 expects storage key '{expectedKey}' for viewport width {viewportWidth}px.");
    }

    [Fact(Skip = _ac6CompactDefaultSkipReason)]
    public void OnAfterRender_CompactViewport_DefaultsToCollapsed() {
        // AC#6 — When viewport tier is "compact" and no saved state exists, the layout
        // MUST default to collapsed. This is intentional behavior the story explicitly
        // protects against accidental removal during the renderer-context fix.
        _ = JSInterop.Setup<int>("hexalithAdmin.getViewportWidth", _ => true).SetResult(1100);
        _ = JSInterop.Setup<string?>("hexalithAdmin.getLocalStorage", _ => true).SetResult(null);

        IRenderedComponent<Hexalith.EventStore.Admin.UI.Layout.MainLayout> cut = Render<Hexalith.EventStore.Admin.UI.Layout.MainLayout>(
            parameters => parameters.Add(p => p.Body, builder => builder.AddContent(0, "Body")));

        cut.WaitForAssertion(
            () => cut.Markup.ShouldContain("48px",
                customMessage: "DW5 AC#6: compact viewport must collapse to 48px width when no saved state."),
            TimeSpan.FromSeconds(5));
    }

    [Fact(Skip = _ac7SkipReason)]
    public async Task OnCommandPaletteShortcut_RemainsInvokableAfterCtrlBFix() {
        // AC#7 — The Ctrl+K palette must continue to function alongside the renderer-
        // context fix. The deterministic guarantee is that MainLayout still exposes
        // OnCommandPaletteShortcut as a JSInvokable method that completes without
        // throwing. Browser-level open/close/re-open coverage lives in the E2E scaffold.
        IRenderedComponent<Hexalith.EventStore.Admin.UI.Layout.MainLayout> cut = Render<Hexalith.EventStore.Admin.UI.Layout.MainLayout>(
            parameters => parameters.Add(p => p.Body, builder => builder.AddContent(0, "Body")));

        await Should.NotThrowAsync(async () =>
            await cut.InvokeAsync(() => cut.Instance.OnCommandPaletteShortcut()));
    }
}
