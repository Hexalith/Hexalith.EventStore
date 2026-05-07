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
    [Fact]
    public async Task OnToggleSidebarShortcut_FlipsCollapseStateAndDispatchesRerender() {
        // AC#5 — The shortcut MUST: (a) flip the _sidebarCollapsed boolean, (b) write
        // the new value to local storage via hexalithAdmin.setLocalStorage, and (c)
        // trigger a rerender on the Blazor synchronization context.
        // bUnit captures (a) and (b) deterministically; (c) is observable by asserting
        // the layout markup width transitions between "220px" and "140px" without throwing
        // InvalidOperationException("StateHasChanged was called outside of the renderer
        // synchronization context").
        IRenderedComponent<Hexalith.EventStore.Admin.UI.Layout.MainLayout> cut = Render<Hexalith.EventStore.Admin.UI.Layout.MainLayout>(
            parameters => parameters.Add(p => p.Body, builder => builder.AddContent(0, "Body")));

        cut.WaitForAssertion(
            () => cut.Markup.ShouldContain("220px"),
            TimeSpan.FromSeconds(5));

        await cut.InvokeAsync(() => cut.Instance.OnToggleSidebarShortcut());

        cut.WaitForAssertion(
            () => cut.Markup.ShouldContain("140px"),
            TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task OnToggleSidebarShortcut_RepeatedTogglesAlternateState() {
        // AC#5 — Repeated invocation must alternate state without losing dispatch on
        // the renderer context. Two consecutive toggles return to the original collapsed
        // state and produce the original markup width. We also verify two distinct
        // setLocalStorage calls were made with alternating values, so a regression that
        // only persists on the first toggle would be caught.
        IRenderedComponent<Hexalith.EventStore.Admin.UI.Layout.MainLayout> cut = Render<Hexalith.EventStore.Admin.UI.Layout.MainLayout>(
            parameters => parameters.Add(p => p.Body, builder => builder.AddContent(0, "Body")));

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("220px"), TimeSpan.FromSeconds(5));
        int setCallsBefore = JSInterop.Invocations.Count(i => i.Identifier == "hexalithAdmin.setLocalStorage");

        await cut.InvokeAsync(() => cut.Instance.OnToggleSidebarShortcut());
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("140px"), TimeSpan.FromSeconds(5));

        await cut.InvokeAsync(() => cut.Instance.OnToggleSidebarShortcut());
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("220px"), TimeSpan.FromSeconds(5));

        string[] writes = JSInterop.Invocations
            .Where(i => i.Identifier == "hexalithAdmin.setLocalStorage")
            .Skip(setCallsBefore)
            .Select(i => i.Arguments[1]?.ToString() ?? string.Empty)
            .ToArray();
        writes.Length.ShouldBe(2,
            customMessage: "DW5 AC#5: each Ctrl+B invocation must persist the new state via setLocalStorage.");
        writes[0].ShouldNotBe(writes[1],
            customMessage: "DW5 AC#5: repeated toggles must alternate the persisted value (regression where every toggle writes the same value).");
    }

    [Theory]
    // optimal tier
    [InlineData(2560, "hexalith-sidebar-collapsed-optimal")]
    [InlineData(1920, "hexalith-sidebar-collapsed-optimal")]
    // optimal/standard boundary
    [InlineData(1919, "hexalith-sidebar-collapsed-standard")]
    // standard tier
    [InlineData(1500, "hexalith-sidebar-collapsed-standard")]
    [InlineData(1280, "hexalith-sidebar-collapsed-standard")]
    // standard/compact boundary
    [InlineData(1279, "hexalith-sidebar-collapsed-compact")]
    // compact tier
    [InlineData(1100, "hexalith-sidebar-collapsed-compact")]
    [InlineData(960, "hexalith-sidebar-collapsed-compact")]
    // compact/minimum boundary
    [InlineData(959, "hexalith-sidebar-collapsed-minimum")]
    // minimum tier
    [InlineData(800, "hexalith-sidebar-collapsed-minimum")]
    public async Task OnToggleSidebarShortcut_PersistsUnderViewportTierKey(int viewportWidth, string expectedKey) {
        // AC#6 — Storage key MUST be hexalith-sidebar-collapsed-{tier} per viewport tier.
        // The bUnit JSInterop layer captures the setLocalStorage call so we can assert the
        // exact key AND the persisted value the layout used. This pins the contract that
        // all four tiers map to distinct keys, that boundaries land on the lower tier, and
        // that the value flips on each invocation (a regression that always writes the
        // same value would otherwise pass with key-only assertions).
        _ = JSInterop.Setup<int>("hexalithAdmin.getViewportWidth", _ => true).SetResult(viewportWidth);

        IRenderedComponent<Hexalith.EventStore.Admin.UI.Layout.MainLayout> cut = Render<Hexalith.EventStore.Admin.UI.Layout.MainLayout>(
            parameters => parameters.Add(p => p.Body, builder => builder.AddContent(0, "Body")));

        // Capture initial collapsed state for the tier (compact/minimum default to collapsed
        // when no saved state exists; optimal/standard default to expanded).
        bool initialCollapsed = cut.Markup.Contains("140px", StringComparison.Ordinal);
        string expectedValue = (!initialCollapsed).ToString().ToLowerInvariant();

        int setCallsBefore = JSInterop.Invocations.Count(i => i.Identifier == "hexalithAdmin.setLocalStorage");
        await cut.InvokeAsync(() => cut.Instance.OnToggleSidebarShortcut());

        JSRuntimeInvocation invocation = JSInterop.Invocations
            .Where(i => i.Identifier == "hexalithAdmin.setLocalStorage")
            .Skip(setCallsBefore)
            .ShouldHaveSingleItem(
                customMessage: "DW5 AC#6 expects exactly one setLocalStorage call per shortcut invocation.");
        invocation.Arguments.ShouldNotBeNull();
        invocation.Arguments[0].ShouldBe(expectedKey,
            customMessage: $"DW5 AC#6 expects storage key '{expectedKey}' for viewport width {viewportWidth}px.");
        invocation.Arguments[1]?.ToString().ShouldBe(expectedValue,
            customMessage: $"DW5 AC#6 expects persisted value '{expectedValue}' (toggle of initial collapsed={initialCollapsed}) for viewport width {viewportWidth}px.");
    }

    [Theory]
    [InlineData(1100, "compact")]
    [InlineData(800, "minimum")]
    public void OnAfterRender_NarrowViewport_DefaultsToCollapsed(int viewportWidth, string tier) {
        // AC#6 — When viewport tier is "compact" or "minimum" and no saved state exists,
        // the layout MUST default to collapsed. Both narrow tiers share this behavior:
        // a viewport narrower than `compact` (i.e. `minimum`) is even less able to host
        // the full sidebar, so default-collapse applies symmetrically.
        _ = JSInterop.Setup<int>("hexalithAdmin.getViewportWidth", _ => true).SetResult(viewportWidth);
        _ = JSInterop.Setup<string?>("hexalithAdmin.getLocalStorage", _ => true).SetResult(null);

        IRenderedComponent<Hexalith.EventStore.Admin.UI.Layout.MainLayout> cut = Render<Hexalith.EventStore.Admin.UI.Layout.MainLayout>(
            parameters => parameters.Add(p => p.Body, builder => builder.AddContent(0, "Body")));

        cut.WaitForAssertion(
            () => cut.Markup.ShouldContain("140px",
                customMessage: $"DW5 AC#6: {tier} viewport ({viewportWidth}px) must collapse to 140px width when no saved state."),
            TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task OnCommandPaletteShortcut_OpensPaletteWithForceFlag() {
        // AC#7 — The Ctrl+K palette must continue to function alongside the renderer-
        // context fix. The OnCommandPaletteShortcut JSInvokable must invoke
        // CommandPalette.OpenAsync(force: true). Verify this by inspecting the rendered
        // CommandPalette's _isOpen field after the shortcut completes — a regression
        // that silently drops the call would leave _isOpen=false.
        IRenderedComponent<Hexalith.EventStore.Admin.UI.Layout.MainLayout> cut = Render<Hexalith.EventStore.Admin.UI.Layout.MainLayout>(
            parameters => parameters.Add(p => p.Body, builder => builder.AddContent(0, "Body")));

        IRenderedComponent<Hexalith.EventStore.Admin.UI.Components.CommandPalette> palette =
            cut.FindComponent<Hexalith.EventStore.Admin.UI.Components.CommandPalette>();

        await Should.NotThrowAsync(async () =>
            await cut.InvokeAsync(() => cut.Instance.OnCommandPaletteShortcut()));

        System.Reflection.FieldInfo? isOpenField = typeof(Hexalith.EventStore.Admin.UI.Components.CommandPalette)
            .GetField("_isOpen", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        isOpenField.ShouldNotBeNull(
            customMessage: "DW5 AC#7: CommandPalette._isOpen field renamed/removed — test must be updated alongside the production refactor.");

        ((bool)isOpenField!.GetValue(palette.Instance)!).ShouldBeTrue(
            customMessage: "DW5 AC#7: OnCommandPaletteShortcut must call CommandPalette.OpenAsync(force: true), which sets _isOpen=true.");
    }
}
