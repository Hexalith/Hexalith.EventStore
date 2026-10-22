namespace Hexalith.EventStore.Admin.UI.E2E;

// ATDD red-phase scaffolds for story:
//   _bmad-output/implementation-artifacts/post-epic-deferred-dw5-admin-ui-runtime-follow-ups.md
//
// AC#8 — CommandSandbox dialog accessibility evidence completed or honestly deferred.
//        Live DOM evidence MUST show whether aria-label="Event payload" lands on the
//        actual rendered Fluent dialog element when the dialog is opened with a real
//        stream/command context.
// AC#9 — EventDebugger dialog accessibility evidence completed or honestly deferred.
//        Same live DOM evidence requirement for the EventDebugger event-payload dialog.
//
// Story rule: do NOT claim full accessibility verification from markup-only or
// screenshot-only evidence. These scaffolds capture the live-DOM tier ONLY (where the
// aria-label landed on the rendered fluent element). Assistive-technology pass evidence
// remains a separate, manual capture in the evidence index and is not asserted here.
//
// Skip rationale: tests are marked [Fact(Skip = "...")] because they require a stream/
// command context that the cold-start E2E fixture does not seed. If the dev cannot seed
// the dialog in the available environment, they record a deferred-with-target-and-reason
// disposition for the AC and leave Skip in place.
[Trait("Category", "E2E")]
[Collection("Playwright")]
public class Dw5DialogAccessibilityBrowserAtddTests
{
    private const string _ac8SkipReason =
        "ATDD red phase — DW5 AC#8 (CommandSandbox dialog live DOM aria-label). Remove Skip after the dev "
        + "seeds a stream/command context that opens the CommandSandbox event-payload dialog and captures "
        + "the rendered Fluent element's aria-label. If seeding is unavailable, record the blocker in the "
        + "evidence index and leave Skip in place per story rule.";
    private const string _ac9SkipReason =
        "ATDD red phase — DW5 AC#9 (EventDebugger dialog live DOM aria-label). Remove Skip after the dev "
        + "seeds a stream that opens the EventDebugger event-payload dialog and captures the rendered "
        + "Fluent element's aria-label. If seeding is unavailable, record the blocker in the evidence "
        + "index and leave Skip in place per story rule.";

    private static readonly TimeSpan _dialogTimeout = TimeSpan.FromSeconds(5);

    private readonly PlaywrightFixture _fixture;

    public Dw5DialogAccessibilityBrowserAtddTests(PlaywrightFixture fixture) => _fixture = fixture;

    [Fact(Skip = _ac8SkipReason)]
    public async Task CommandSandbox_PayloadDialog_RenderedElementCarriesAriaLabel()
    {
        // AC#8 — Open the CommandSandbox event-payload dialog (requires a stream + accepted
        // sandbox result with at least one event). Then locate the rendered fluent-dialog
        // element with aria-label="Event payload" and assert it is present and visible.
        (IBrowserContext context, IPage page) = await _fixture.CreatePageAsync();
        await using (context)
        {
            await NavigateToCommandSandbox(page);

            // The fluent-dialog DOM element name is `fluent-dialog` after Fluent UI
            // Blazor v5 renders its web-component shell. The aria-label set on the
            // Razor element MUST land on this element (or on its inner dialog node).
            ILocator dialog = page.Locator("fluent-dialog[aria-label='Event payload'], [role='dialog'][aria-label='Event payload']").First;

            await dialog.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = (float)_dialogTimeout.TotalMilliseconds,
            });

            string? ariaLabel = await dialog.GetAttributeAsync("aria-label");
            ariaLabel.ShouldBe("Event payload",
                customMessage: "DW5 AC#8: rendered Fluent dialog must carry aria-label=\"Event payload\".");
        }
    }

    [Fact(Skip = _ac9SkipReason)]
    public async Task EventDebugger_PayloadDialog_RenderedElementCarriesAriaLabel()
    {
        // AC#9 — Same live-DOM aria-label contract for the EventDebugger payload dialog.
        (IBrowserContext context, IPage page) = await _fixture.CreatePageAsync();
        await using (context)
        {
            await NavigateToEventDebugger(page);

            ILocator dialog = page.Locator("fluent-dialog[aria-label='Event payload'], [role='dialog'][aria-label='Event payload']").First;

            await dialog.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = (float)_dialogTimeout.TotalMilliseconds,
            });

            string? ariaLabel = await dialog.GetAttributeAsync("aria-label");
            ariaLabel.ShouldBe("Event payload",
                customMessage: "DW5 AC#9: rendered Fluent dialog must carry aria-label=\"Event payload\".");
        }
    }

    private static async Task NavigateToCommandSandbox(IPage page)
    {
        // Placeholder navigation path. The dev's evidence pass MUST replace this with
        // the concrete Aspire-seeded path that opens the CommandSandbox dialog (likely:
        // /streams → select a stream → open Sandbox → run a command that produces an
        // event → click "View payload"). This scaffold pins the DOM contract; the
        // navigation steps live with the seeded environment.
        await page.GotoAsync("/streams");
        await page.WaitForSelectorAsync("main[role='main']");

        // The dev removes this throw and replaces it with the seeded interaction sequence
        // that opens the dialog. Until then the test remains Skip-guarded above.
        throw new InvalidOperationException(
            "DW5 AC#8 scaffold: replace this throw with the seeded interaction that opens "
            + "the CommandSandbox event-payload dialog before removing the Skip attribute.");
    }

    private static async Task NavigateToEventDebugger(IPage page)
    {
        // Same placeholder pattern for EventDebugger. Dev's evidence pass replaces this
        // with the concrete seeded interaction (likely: /streams → select a stream →
        // open Event Debugger → step to an event → click "View payload").
        await page.GotoAsync("/streams");
        await page.WaitForSelectorAsync("main[role='main']");

        throw new InvalidOperationException(
            "DW5 AC#9 scaffold: replace this throw with the seeded interaction that opens "
            + "the EventDebugger event-payload dialog before removing the Skip attribute.");
    }
}
