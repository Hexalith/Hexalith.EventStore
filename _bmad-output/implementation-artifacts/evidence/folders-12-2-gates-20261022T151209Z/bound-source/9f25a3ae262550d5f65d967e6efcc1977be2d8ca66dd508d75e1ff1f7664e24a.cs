using System.Text.RegularExpressions;

using Shouldly;

namespace Hexalith.EventStore.Admin.UI.Tests.Governance;

/// <summary>
/// Governance guard for the project-wide "every UI page/component uses FrontComposer or Fluent v5 only"
/// rule (FrontComposer architecture.md §4.1). In Fluent UI Blazor v5 the design system only styles its own
/// custom elements (<c>&lt;fluent-button&gt;</c>, …); a raw <c>&lt;button&gt;</c> / <c>&lt;input&gt;</c> /
/// <c>&lt;select&gt;</c> / <c>&lt;textarea&gt;</c> is never upgraded and falls back to unstyled browser
/// rendering, which also drops the accessibility affordances Fluent components provide. This test fails the
/// build if a raw interactive HTML control is reintroduced into <c>Hexalith.EventStore.Admin.UI</c>, mirroring
/// the Tenants.UI guard (<c>Hexalith.Tenants.UI.Tests.DomainUiFluentConformanceTests</c>). Raw <c>&lt;a&gt;</c>
/// navigation links are permitted.
/// </summary>
public class AdminUiFluentConformanceTests {
    // Matches an opening tag for a raw interactive HTML control. The trailing character class anchors on a
    // real tag boundary (whitespace, self-close, or '>') so attributes like `inputmode=` and Fluent
    // components like <FluentButton> / <FluentTextInput> (capitalised) are not matched. Case-sensitive on
    // purpose — Razor component tags are PascalCase, raw HTML controls are lowercase.
    private static readonly Regex RawInteractiveControl = new(
        "<(button|input|select|textarea)(\\s|/|>)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    [Fact]
    [Trait("Category", "Governance")]
    public void AdminUi_components_use_fluent_v5_only_except_documented_carveouts() {
        string adminUiRoot = Path.Combine(Dw5TestPaths.RepoRoot(), "src", "Hexalith.EventStore.Admin.UI");
        Directory.Exists(adminUiRoot).ShouldBeTrue($"Admin.UI source root not found: {adminUiRoot}");

        // Documented carve-outs (FrontComposer architecture.md §4.1): custom-styled, fully-accessible
        // interactive elements where a FluentButton would cause visual regression —
        //   - ActivityChart.razor: clickable bar-chart bars (each a raw <button> wrapping a height-scaled
        //     <div>); FluentButton destroys the bar geometry. The bars live in a labelled role="group"
        //     (not role="img") so assistive technology exposes them as buttons, each carries an
        //     aria-label and a data-testid, and an sr-only data table is the non-visual text alternative
        //     (audit C3 fix).
        //   - Streams.razor: an inline monospace click-to-copy aggregate-ID grid cell; FluentButton breaks the cell.
        // Both carry aria-label/data-testid, so they are not the unstyled-control defect this rule targets.
        string[] carveOuts = ["ActivityChart.razor", "Streams.razor"];

        EnumerationOptions options = new() {
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.Hidden,
            IgnoreInaccessible = true,
        };

        string[] razorFiles = Directory
            .EnumerateFiles(adminUiRoot, "*.razor", options)
            .Where(f => !IsBuildOutput(f))
            .ToArray();

        // Guard against a broken path silently passing the scan.
        razorFiles.ShouldNotBeEmpty($"no .razor files found under {adminUiRoot}");

        List<string> offenders = [];
        foreach (string file in razorFiles) {
            if (carveOuts.Contains(Path.GetFileName(file), StringComparer.Ordinal)) {
                continue;
            }

            MatchCollection matches = RawInteractiveControl.Matches(File.ReadAllText(file));
            if (matches.Count > 0) {
                string tags = string.Join(
                    ", ",
                    matches.Select(match => match.Groups[1].Value).Distinct(StringComparer.Ordinal));
                offenders.Add($"{Path.GetFileName(file)} ({tags})");
            }
        }

        offenders.ShouldBeEmpty(
            "Admin.UI .razor components must use Fluent v5 components only (no raw <button>/<input>/<select>/"
            + "<textarea>; raw <a> nav links allowed). Carve-outs are allowlisted (ActivityChart, Streams). "
            + $"Raw interactive controls found in: {string.Join("; ", offenders)}");
    }

    private static bool IsBuildOutput(string file) {
        string normalized = file.Replace('\\', '/');
        return normalized.Contains("/bin/", StringComparison.Ordinal)
            || normalized.Contains("/obj/", StringComparison.Ordinal);
    }
}
