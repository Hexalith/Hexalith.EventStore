namespace Hexalith.EventStore.Admin.UI.E2E;

// ATDD red-phase scaffolds for story:
//   _bmad-output/implementation-artifacts/post-epic-deferred-dw5-admin-ui-runtime-follow-ups.md
//
// AC#5 — Ctrl+B sidebar toggle works on the Blazor renderer context. Repeated keypresses
//        MUST collapse and expand the sidebar without browser console errors, SignalR
//        circuit exceptions, or lost local-storage state.
// AC#6 — Storage key remains hexalith-sidebar-collapsed-{tier} for the current viewport
//        tier; saved state restores after refresh.
// AC#7 — Ctrl+K command palette opens, closes, and re-opens in the same browser session
//        after Ctrl+B activity (regression guard).
//
// These scaffolds prove the runtime contract that bUnit cannot: real keyboard events on
// a Blazor Server hydrated page, repeated toggles, refresh persistence, and same-session
// shortcut coexistence. They MUST stay skipped until the dev's browser evidence pass
// records the symptom (or a not-reproduced disposition) and the targeted fix lands.
[Trait("Category", "E2E")]
[Collection("Playwright")]
public class Dw5SidebarShortcutBrowserAtddTests
{
    private static readonly TimeSpan _shortcutTimeout = TimeSpan.FromSeconds(3);

    private readonly PlaywrightFixture _fixture;

    public Dw5SidebarShortcutBrowserAtddTests(PlaywrightFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task CtrlB_RepeatedToggle_NoConsoleErrors_SidebarWidthAlternates()
    {
        // AC#5 — Five Ctrl+B presses MUST alternate the sidebar collapsed/expanded
        // state and produce zero error-level console messages.
        (IBrowserContext context, IPage page) = await _fixture.CreatePageAsync();
        List<string> errors = [];
        page.Console += (_, msg) => {
            if (msg.Type == "error") {
                errors.Add(msg.Text);
            }
        };

        await using (context)
        {
            await page.GotoAsync("/");
            await page.WaitForSelectorAsync(".admin-sidebar nav[aria-label='Main navigation']");
            await WaitForShortcutRegistrationAsync(page);

            ILocator sidebar = page.Locator(".admin-sidebar:visible").First;
            string? initialClass = await sidebar.GetAttributeAsync("class");

            for (int i = 0; i < 5; i++)
            {
                await page.Keyboard.PressAsync("Control+b");
                await page.WaitForFunctionAsync(
                    "(args) => document.querySelector('.admin-sidebar')?.className !== args.previousClass",
                    new { previousClass = initialClass },
                    new PageWaitForFunctionOptions { Timeout = (float)_shortcutTimeout.TotalMilliseconds });

                initialClass = await sidebar.GetAttributeAsync("class");
            }

            errors.ShouldBeEmpty(
                customMessage: "DW5 AC#5: Ctrl+B produced console errors (likely SignalR circuit / renderer-context): "
                    + string.Join(" | ", errors));
        }
    }

    [Fact]
    public async Task CtrlB_StorageKey_MatchesViewportTier_AndPersistsAcrossRefresh()
    {
        // AC#6 — After Ctrl+B, hexalithAdmin.getLocalStorage(`hexalith-sidebar-collapsed-{tier}`)
        // MUST return "true" or "false" (not null) for the current viewport's tier, and a
        // page refresh MUST restore the same collapsed state.
        (IBrowserContext context, IPage page) = await _fixture.CreatePageAsync();
        await using (context)
        {
            await page.GotoAsync("/");
            await page.WaitForSelectorAsync(".admin-sidebar nav[aria-label='Main navigation']");
            await WaitForShortcutRegistrationAsync(page);

            // Compute expected storage key from current viewport width.
            int viewportWidth = await page.EvaluateAsync<int>("() => window.innerWidth");
            string tier = viewportWidth switch
            {
                >= 1920 => "optimal",
                >= 1280 => "standard",
                >= 960 => "compact",
                _ => "minimum",
            };
            string expectedKey = $"hexalith-sidebar-collapsed-{tier}";

            await page.Keyboard.PressAsync("Control+b");

            // Allow the JS interop to write to local storage.
            await page.WaitForFunctionAsync(
                "(args) => localStorage.getItem(args.key) !== null",
                new { key = expectedKey },
                new PageWaitForFunctionOptions { Timeout = (float)_shortcutTimeout.TotalMilliseconds });

            string? storedValue = await page.EvaluateAsync<string?>(
                "(args) => localStorage.getItem(args.key)",
                new { key = expectedKey });

            storedValue.ShouldNotBeNull(
                customMessage: $"DW5 AC#6: localStorage key '{expectedKey}' must be written after Ctrl+B for tier '{tier}'.");

            // Refresh and verify the saved state is restored to the same value AND that
            // the rendered sidebar reflects that value (a regression flipping the boolean
            // sense would still pass the storage roundtrip silently).
            await page.ReloadAsync();
            await page.WaitForSelectorAsync(".admin-sidebar nav[aria-label='Main navigation']");
            await WaitForShortcutRegistrationAsync(page);

            string? restoredValue = await page.EvaluateAsync<string?>(
                "(args) => localStorage.getItem(args.key)",
                new { key = expectedKey });
            restoredValue.ShouldBe(storedValue,
                customMessage: $"DW5 AC#6: localStorage key '{expectedKey}' must persist across refresh.");

            // Visible sidebar state must match the persisted boolean: "true" => collapsed.
            ILocator sidebar = page.Locator(".admin-sidebar:visible").First;
            string? sidebarClass = await sidebar.GetAttributeAsync("class") ?? string.Empty;
            bool actuallyCollapsed = sidebarClass.Contains("collapsed", StringComparison.Ordinal);
            bool expectedCollapsed = string.Equals(restoredValue, "true", StringComparison.OrdinalIgnoreCase);
            actuallyCollapsed.ShouldBe(expectedCollapsed,
                customMessage: $"DW5 AC#6: rendered sidebar collapsed state ({actuallyCollapsed}) must match persisted value ('{restoredValue}') after refresh — a regression that flips the boolean sense would otherwise pass the storage roundtrip silently.");
        }
    }

    [Fact]
    public async Task CtrlK_OpenCloseReopen_StillWorksAfterCtrlBActivity()
    {
        // AC#7 — After several Ctrl+B presses, Ctrl+K MUST still open the command
        // palette, Escape MUST close it, and a second Ctrl+K MUST re-open it. All
        // three transitions happen in the SAME browser session (not separate test runs).
        (IBrowserContext context, IPage page) = await _fixture.CreatePageAsync();
        await using (context)
        {
            await page.GotoAsync("/");
            await page.WaitForSelectorAsync(".admin-sidebar nav[aria-label='Main navigation']");
            await WaitForShortcutRegistrationAsync(page);

            // Exercise Ctrl+B first.
            await page.Keyboard.PressAsync("Control+b");
            await page.Keyboard.PressAsync("Control+b");

            // Open the command palette.
            await page.Keyboard.PressAsync("Control+k");
            ILocator paletteSearch = page.Locator("fluent-text-field:has(input[placeholder='Search commands, pages, actions...']), input[placeholder='Search commands, pages, actions...']").First;
            await paletteSearch.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = (float)_shortcutTimeout.TotalMilliseconds,
            });

            // Close it.
            await page.Keyboard.PressAsync("Escape");
            await paletteSearch.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Hidden,
                Timeout = (float)_shortcutTimeout.TotalMilliseconds,
            });

            // Re-open it (Story 21-13 fix this scaffold protects).
            await page.Keyboard.PressAsync("Control+k");
            await paletteSearch.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = (float)_shortcutTimeout.TotalMilliseconds,
            });
        }
    }

    private static async Task WaitForShortcutRegistrationAsync(IPage page)
    {
        await page.WaitForFunctionAsync(
            "() => window.hexalithAdmin?._shortcutHandlers?.size > 0",
            options: new PageWaitForFunctionOptions { Timeout = (float)_shortcutTimeout.TotalMilliseconds });
    }
}
