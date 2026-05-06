using System.Text.RegularExpressions;

using Shouldly;

namespace Hexalith.EventStore.Admin.UI.Tests.Components;

// ATDD red-phase scaffolds for story:
//   _bmad-output/implementation-artifacts/post-epic-deferred-dw5-admin-ui-runtime-follow-ups.md
//
// AC#8 — CommandSandbox event-payload dialog must carry aria-label="Event payload" on
//        the rendered FluentDialog wrapper around FluentDialogBody. bUnit tests can
//        prove the markup CONTRACT (the .razor source still wires the attribute on
//        the dialog element). Live DOM evidence (where the attribute lands on the
//        rendered Fluent web component) is browser-only and lives in the E2E scaffold.
// AC#9 — Same contract for EventDebugger event-payload dialog.
//
// These scaffolds are markup-source assertions: they read the .razor file and confirm
// the dialog element retains aria-label="Event payload" wrapped around FluentDialogBody.
// This guards against accidental removal during the renderer-context fix or any later
// refactor. The dialog is a closed Modal at initial render, so DOM-based bUnit assertions
// are not the right level for this contract — source-based assertions are deterministic
// and reviewer-auditable.
public class Dw5DialogAccessibilityAtddTests {
    [Fact]
    public void CommandSandbox_PayloadDialog_HasFluentDialogWithAriaLabel() {
        // AC#8 — The CommandSandbox event payload dialog MUST be wrapped in a
        // FluentDialog element with aria-label="Event payload" (case-sensitive).
        string razor = ReadRazor("Components/CommandSandbox.razor");

        // Match: <FluentDialog ... aria-label="Event payload" ... > followed (anywhere) by <FluentDialogBody>
        Regex pattern = new(
            "<FluentDialog\\b[^>]*aria-label=\"Event payload\"[^>]*>\\s*<FluentDialogBody\\b",
            RegexOptions.Singleline);
        pattern.IsMatch(razor).ShouldBeTrue(
            customMessage: "DW5 AC#8: CommandSandbox.razor must contain a <FluentDialog ... aria-label=\"Event payload\"> "
                + "wrapping <FluentDialogBody>.");
    }

    [Fact]
    public void EventDebugger_PayloadDialog_HasFluentDialogWithAriaLabel() {
        // AC#9 — Same contract for EventDebugger.
        string razor = ReadRazor("Components/EventDebugger.razor");

        Regex pattern = new(
            "<FluentDialog\\b[^>]*aria-label=\"Event payload\"[^>]*>\\s*<FluentDialogBody\\b",
            RegexOptions.Singleline);
        pattern.IsMatch(razor).ShouldBeTrue(
            customMessage: "DW5 AC#9: EventDebugger.razor must contain a <FluentDialog ... aria-label=\"Event payload\"> "
                + "wrapping <FluentDialogBody>.");
    }

    [Fact]
    public void CommandSandbox_PayloadDialog_IsModal() {
        // AC#8 — Dialog MUST be Modal="true". A non-modal dialog escapes focus and
        // breaks the keyboard / assistive-technology contract this AC commits to.
        string razor = ReadRazor("Components/CommandSandbox.razor");

        Regex pattern = new(
            "<FluentDialog\\b[^>]*Modal=\"true\"[^>]*aria-label=\"Event payload\"|"
            + "<FluentDialog\\b[^>]*aria-label=\"Event payload\"[^>]*Modal=\"true\"",
            RegexOptions.Singleline);
        pattern.IsMatch(razor).ShouldBeTrue(
            customMessage: "DW5 AC#8: CommandSandbox payload dialog must declare Modal=\"true\".");
    }

    [Fact]
    public void EventDebugger_PayloadDialog_IsModal() {
        // AC#9 — Same Modal=true contract.
        string razor = ReadRazor("Components/EventDebugger.razor");

        Regex pattern = new(
            "<FluentDialog\\b[^>]*Modal=\"true\"[^>]*aria-label=\"Event payload\"|"
            + "<FluentDialog\\b[^>]*aria-label=\"Event payload\"[^>]*Modal=\"true\"",
            RegexOptions.Singleline);
        pattern.IsMatch(razor).ShouldBeTrue(
            customMessage: "DW5 AC#9: EventDebugger payload dialog must declare Modal=\"true\".");
    }

    private static string ReadRazor(string relativePath) {
        string repoRoot = Dw5TestPaths.RepoRoot();
        string fullPath = Path.Combine(repoRoot, "src", "Hexalith.EventStore.Admin.UI", relativePath);
        File.Exists(fullPath).ShouldBeTrue(
            customMessage: $"DW5 dialog accessibility scaffold expected source file at: {fullPath}");
        return File.ReadAllText(fullPath);
    }
}
