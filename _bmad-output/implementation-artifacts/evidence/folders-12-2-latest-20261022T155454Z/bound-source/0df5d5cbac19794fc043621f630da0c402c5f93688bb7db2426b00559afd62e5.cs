using System.Text.RegularExpressions;

namespace Hexalith.EventStore.Admin.UI.Tests.Components;

// ATDD red-phase scaffolds for story:
//   _bmad-output/implementation-artifacts/post-epic-deferred-dw5-admin-ui-runtime-follow-ups.md
//
// AC#12 — Fluent UI Blazor v5 component behavior must be respected. The repository is
//         pinned to Microsoft.FluentUI.AspNetCore.Components 5.0.0-rc.2-26098.1. DW5
//         changes MUST NOT reintroduce removed v4 APIs. Specifically:
//           - No `Typo` or `Typography` parameter on FluentText
//           - FluentDialogBody is retained inside event payload dialogs
//           - FluentTabs continues to use ActiveTabId / ActiveTabIdChanged
//
// These invariants are best enforced as static markup-source assertions across the UI
// project: they run quickly, do not require a render context, and produce a clear
// signal if a later refactor reintroduces a removed v4 API.
public class Dw5FluentV5InvariantsAtddTests {
    [Fact]
    public void AdminUI_FluentText_DoesNotUseRemovedV4TypoOrTypographyParameter() {
        // AC#12 — Scan every .razor file under Admin.UI for any FluentText element that
        // carries the removed v4 Typo/Typography parameter. v5 uses As=TextTag.* with
        // Size=TextSize.* and Weight=TextWeight.*.
        string adminUiRoot = Path.Combine(Dw5TestPaths.RepoRoot(), "src", "Hexalith.EventStore.Admin.UI");
        Directory.Exists(adminUiRoot).ShouldBeTrue();

        Regex forbidden = new(
            "<FluentText\\b[^>]*\\b(Typo|Typography)\\s*=",
            RegexOptions.Singleline);

        List<string> violations = [];
        foreach (string razor in Directory.EnumerateFiles(adminUiRoot, "*.razor", SearchOption.AllDirectories)) {
            string content = File.ReadAllText(razor);
            if (forbidden.IsMatch(content)) {
                violations.Add(razor);
            }
        }

        violations.ShouldBeEmpty(
            customMessage: "DW5 AC#12: removed v4 Typo/Typography parameter found on FluentText in: "
                + string.Join(", ", violations));
    }

    [Fact]
    public void AdminUI_PayloadDialogs_RetainFluentDialogBody() {
        // AC#12 — Both event-payload dialogs MUST wrap content in FluentDialogBody.
        // Removing FluentDialogBody would break v5 dialog template slots
        // (TitleTemplate / ChildContent / ActionTemplate).
        string commandSandbox = ReadRazor("Components/CommandSandbox.razor");
        string eventDebugger = ReadRazor("Components/EventDebugger.razor");

        commandSandbox.ShouldContain("<FluentDialogBody>",
            customMessage: "DW5 AC#12: CommandSandbox payload dialog must retain <FluentDialogBody>.");
        eventDebugger.ShouldContain("<FluentDialogBody>",
            customMessage: "DW5 AC#12: EventDebugger payload dialog must retain <FluentDialogBody>.");
    }

    [Fact]
    public void TypeCatalog_FluentTabs_BindsActiveTabIdAndActiveTabIdChanged() {
        // AC#12 — TypeCatalog FluentTabs MUST keep the v5 binding pattern:
        //   <FluentTabs ActiveTabId="..." ActiveTabIdChanged="...">.
        // Reverting to the v4 SelectedId/SelectedIdChanged pair would silently break
        // the URL-driven tab state DW5 explicitly protects.
        string typeCatalog = ReadRazor("Pages/TypeCatalog.razor");

        Regex bindingPattern = new(
            "<FluentTabs\\b[^>]*ActiveTabId=[^>]*ActiveTabIdChanged=",
            RegexOptions.Singleline);
        bindingPattern.IsMatch(typeCatalog).ShouldBeTrue(
            customMessage: "DW5 AC#12: TypeCatalog FluentTabs must bind both ActiveTabId and ActiveTabIdChanged.");
    }

    private static string ReadRazor(string relativePath) {
        string repoRoot = Dw5TestPaths.RepoRoot();
        string fullPath = Path.Combine(repoRoot, "src", "Hexalith.EventStore.Admin.UI", relativePath);
        File.Exists(fullPath).ShouldBeTrue(
            customMessage: $"DW5 Fluent v5 invariant scaffold expected source file at: {fullPath}");
        return File.ReadAllText(fullPath);
    }
}
