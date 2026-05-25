namespace Hexalith.EventStore.Admin.UI.E2E;

/// <summary>
/// Browser-based E2E smoke tests for Admin.UI.
/// Validates that the Blazor Server app renders correctly in a real browser
/// and that critical navigation paths work.
/// </summary>
[Trait("Category", "E2E")]
[Collection("Playwright")]
public class BrowserSmokeTests
{
    private readonly PlaywrightFixture _fixture;

    public BrowserSmokeTests(PlaywrightFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Dashboard_RendersShellAndTitle()
    {
        (IBrowserContext context, IPage page) = await _fixture.CreatePageAsync();
        await using (context)
        {
            await page.GotoAsync("/");

            // Wait for Blazor Server to hydrate the shell
            await page.WaitForSelectorAsync("main[role='main']");

            string title = await page.TitleAsync();
            title.ShouldContain("Hexalith EventStore Admin");
        }
    }

    [Fact]
    public async Task Dashboard_HasAccessibleNavigation()
    {
        (IBrowserContext context, IPage page) = await _fixture.CreatePageAsync();
        await using (context)
        {
            await page.GotoAsync("/");
            await page.WaitForSelectorAsync(".admin-sidebar nav[aria-label='Main navigation']");

            // Skip-to-main link is present for keyboard users
            ILocator skipLink = page.Locator("a.skip-to-main");
            (await skipLink.CountAsync()).ShouldBeGreaterThan(0);

            // Navigation landmark contains expected links
            ILocator nav = page.Locator(".admin-sidebar nav[aria-label='Main navigation']");
            string navText = await nav.InnerTextAsync();
            navText.ShouldContain("Home");
            navText.ShouldContain("Commands");
            navText.ShouldContain("Events");
            navText.ShouldContain("Streams");
            navText.ShouldContain("Health");
        }
    }

    [Fact]
    public async Task Navigation_CommandsPageLoads()
    {
        (IBrowserContext context, IPage page) = await _fixture.CreatePageAsync();
        await using (context)
        {
            await page.GotoAsync("/");
            await page.WaitForSelectorAsync(".admin-sidebar nav[aria-label='Main navigation']");

            // Click the Commands nav link
            await page.Locator(".admin-sidebar nav a[href='/commands']").ClickAsync();

            // Wait for the (event-driven, Blazor Server) URL commit before asserting —
            // a synchronous Url check here races the navigation.
            await page.WaitForURLAsync("**/commands", new PageWaitForURLOptions { Timeout = 3000 });
            page.Url.ShouldContain("/commands");
        }
    }

    [Fact]
    public async Task Dashboard_HydratesMainLandmark()
    {
        (IBrowserContext context, IPage page) = await _fixture.CreatePageAsync();
        await using (context)
        {
            await page.GotoAsync("/");

            // Functional: the shell hydrates and the main landmark appears (WaitForSelector
            // throws on timeout). Blazor Server hydration time varies under CI load, so the
            // "within 3s" perf budget is owned by perf-lab.yml, not asserted here.
            IElementHandle? main = await page.WaitForSelectorAsync("main[role='main']");
            main.ShouldNotBeNull();
        }
    }

    [Fact]
    public async Task Dashboard_StatCardsRender()
    {
        (IBrowserContext context, IPage page) = await _fixture.CreatePageAsync();
        await using (context)
        {
            await page.GotoAsync("/");
            await page.WaitForSelectorAsync("main[role='main']");

            // Stat cards should render (either with data or loading placeholders)
            ILocator statCards = page.Locator(".stat-card-grid");
            (await statCards.CountAsync()).ShouldBeGreaterThan(0);
        }
    }
}
