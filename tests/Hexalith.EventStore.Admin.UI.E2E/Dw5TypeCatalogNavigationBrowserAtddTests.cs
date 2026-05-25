namespace Hexalith.EventStore.Admin.UI.E2E;

// ATDD red-phase scaffolds for story:
//   _bmad-output/implementation-artifacts/post-epic-deferred-dw5-admin-ui-runtime-follow-ups.md
//
// AC#2 — TypeCatalog navigation blocking is reproduced or closed with explicit evidence.
//        From `/types?tab=*`, clicking at least three sidebar nav links MUST change the
//        URL AND the rendered page within a bounded time without needing repeated clicks.
//        Story rule: route-only changes are insufficient (visible page must transition);
//        DOM-only changes are insufficient (URL must transition).
//
// These scaffolds exercise the full browser path (Blazor Server hydration + Fluent web-
// components + sidebar nav) and capture both URL and visible-page transitions in the same
// assertion block. They MUST stay skipped until the dev's browser evidence pass either:
//   (a) reproduces the block and the targeted fix lands, OR
//   (b) records a not-reproduced disposition with the matrix from Task 1.7 of the story.
//
// Each test asserts both URL and visible-page change. Bounded time = 3 seconds (matches
// the existing BrowserSmokeTests budget) per single sidebar click.
[Trait("Category", "E2E")]
[Collection("Playwright")]
public class Dw5TypeCatalogNavigationBrowserAtddTests
{
    private static readonly TimeSpan _navTimeout = TimeSpan.FromSeconds(3);

    private static readonly string[] _sidebarTargets = ["/commands", "/events", "/streams"];

    private readonly PlaywrightFixture _fixture;

    public Dw5TypeCatalogNavigationBrowserAtddTests(PlaywrightFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task SidebarNav_From_Types_TransitionsUrlAndVisiblePage()
    {
        await AssertSidebarNavFromTypeCatalog("/types");
    }

    [Fact]
    public async Task SidebarNav_From_TypesTabCommands_TransitionsUrlAndVisiblePage()
    {
        await AssertSidebarNavFromTypeCatalog("/types?tab=commands");
    }

    [Fact]
    public async Task SidebarNav_From_TypesTabAggregates_TransitionsUrlAndVisiblePage()
    {
        await AssertSidebarNavFromTypeCatalog("/types?tab=aggregates");
    }

    [Fact]
    public async Task SidebarNav_From_Types_ProducesNoConsoleErrors()
    {
        // AC#2/#5 — During the /types navigation pass, the browser console MUST be free
        // of error-level messages. A console error during nav is a render-loop signal
        // and indicates the targeted fix has not landed yet.
        (IBrowserContext context, IPage page) = await _fixture.CreatePageAsync();
        List<string> errors = [];
        page.Console += (_, msg) => {
            if (msg.Type == "error") {
                errors.Add(msg.Text);
            }
        };

        await using (context)
        {
            await page.GotoAsync("/types");
            await page.WaitForSelectorAsync(".admin-sidebar nav[aria-label='Main navigation']");

            foreach (string target in _sidebarTargets)
            {
                await page.Locator($".admin-sidebar nav a[href='{target}']").ClickAsync();
                await page.WaitForURLAsync($"**{target}", new PageWaitForURLOptions { Timeout = (float)_navTimeout.TotalMilliseconds });
            }

            errors.ShouldBeEmpty(
                customMessage: "DW5 AC#2/#5: console errors observed during /types sidebar navigation: "
                    + string.Join(" | ", errors));
        }
    }

    private async Task AssertSidebarNavFromTypeCatalog(string startingUrl)
    {
        // AC#2 — From the given /types URL, click each sidebar target and assert BOTH
        // a URL transition AND a visible page transition within the bounded timeout.
        // A URL-only change is insufficient if the old TypeCatalog content remains
        // visible; a DOM-only change is insufficient if the URL stays on /types.
        (IBrowserContext context, IPage page) = await _fixture.CreatePageAsync();
        await using (context)
        {
            await page.GotoAsync(startingUrl);
            await page.WaitForSelectorAsync(".admin-sidebar nav[aria-label='Main navigation']");
            await page.WaitForSelectorAsync("h1:has-text('Type Catalog')");

            foreach (string target in _sidebarTargets)
            {
                await page.Locator($".admin-sidebar nav a[href='{target}']").ClickAsync();

                // URL transition.
                await page.WaitForURLAsync(
                    $"**{target}",
                    new PageWaitForURLOptions { Timeout = (float)_navTimeout.TotalMilliseconds });
                page.Url.ShouldContain(target,
                    customMessage: $"DW5 AC#2: URL did not transition to {target} from {startingUrl}.");

                // Visible page transition — the Type Catalog H1 MUST disappear.
                ILocator typeCatalogH1 = page.Locator("h1:has-text('Type Catalog')");
                await typeCatalogH1.WaitForAsync(new LocatorWaitForOptions
                {
                    State = WaitForSelectorState.Detached,
                    Timeout = (float)_navTimeout.TotalMilliseconds,
                });

                // Re-arm for next iteration: navigate back to the starting URL.
                await page.GotoAsync(startingUrl);
                await page.WaitForSelectorAsync("h1:has-text('Type Catalog')");
            }
        }
    }
}
