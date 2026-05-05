using Shouldly;

namespace Hexalith.EventStore.DeferredWorkGovernance.Tests;

/// <summary>
/// DW6 ATDD red-phase scaffolds for AC #4, #7, and #9.
/// These tests define the checker/report contract without choosing whether
/// the implementation is PowerShell, Bash, Python, or .NET.
/// </summary>
public class Dw6CheckerReportAtddTests {
    private const string _skip = "ATDD red phase - DW6 checker contract. Remove Skip when implementing the matching AC.";

    [Fact(Skip = _skip + " AC#4.")]
    public async Task Checker_ReportIncludesAllCanonicalCountBuckets() {
        Dw6GovernanceReport report = await Dw6GovernanceCheckerInvokerFactory.Create()
            .CheckAsync([Dw6TestPaths.DeferredWorkPath]);

        foreach (string bucket in Dw6RuleVocabulary.CountBuckets) {
            report.Counts.Keys.ShouldContain(bucket, customMessage:
                $"Checker output must include the '{bucket}' count bucket even when zero.");
        }
    }

    [Fact(Skip = _skip + " AC#4.")]
    public async Task Checker_OutputIsStableSortedAndConcise() {
        IDw6GovernanceCheckerInvoker checker = Dw6GovernanceCheckerInvokerFactory.Create();
        Dw6GovernanceReport first = await checker.CheckAsync([Dw6TestPaths.DeferredWorkPath]);
        Dw6GovernanceReport second = await checker.CheckAsync([Dw6TestPaths.DeferredWorkPath]);

        first.Counts.ShouldBe(second.Counts, customMessage: "Counts must be stable across repeated runs.");
        first.Diagnostics.ShouldBe(second.Diagnostics, customMessage: "Diagnostics must be stable across repeated runs.");
        first.Diagnostics.ShouldBe(
            first.Diagnostics.OrderBy(d => d, Dw6GovernanceDiagnosticComparer.Instance).ToList(),
            customMessage: "Diagnostics must be sorted by file, heading, rule, disposition, and line.");
    }

    [Fact(Skip = _skip + " AC#7.")]
    public async Task Checker_UnclassifiedLiveBullets_ReportHeadingExcerptAndLocator() {
        Dw6GovernanceReport report = await Dw6GovernanceCheckerInvokerFactory.Create()
            .CheckAsync(["--fixture", "unclassified-live-bullet"]);

        report.ExitCode.ShouldNotBe(0, "Unclassified live bullets must be visible and fail closed.");
        Dw6GovernanceDiagnostic diagnostic = report.Diagnostics
            .Where(d => d.Rule == "dw6-unclassified-live-bullet")
            .ToList()
            .ShouldHaveSingleItem();

        diagnostic.Heading.ShouldNotBeNullOrWhiteSpace();
        diagnostic.Excerpt.ShouldNotBeNullOrWhiteSpace();
        diagnostic.Line.ShouldNotBeNull("A line number or stable locator is required when implementation can provide one.");
    }

    [Fact(Skip = _skip + " AC#9.")]
    public async Task Checker_HelpOutputDocumentsBlockingVersusAdvisoryBehavior() {
        Dw6GovernanceReport report = await Dw6GovernanceCheckerInvokerFactory.Create()
            .CheckAsync(["--help-json"]);

        string helpText = string.Join('\n', report.Diagnostics.Select(d => d.Hint));
        helpText.ShouldContain("blocking", customMessage:
            "Help output must explain which cases fail closed.");
        helpText.ShouldContain("advisory", customMessage:
            "Help output must explain whether legacy sections are advisory.");
    }

    [Fact(Skip = _skip + " AC#9.")]
    public void DocsValidationWiring_IsConsistentOrExplicitlyDeferred() {
        string ps1 = Dw6TestPaths.ReadRepoFile("scripts/validate-docs.ps1");
        string sh = Dw6TestPaths.ReadRepoFile("scripts/validate-docs.sh");
        string workflow = Dw6TestPaths.ReadRepoFile(".github/workflows/docs-validation.yml");

        bool ps1Wired = ps1.Contains("deferred-work", StringComparison.OrdinalIgnoreCase)
            || ps1.Contains("DW6-CI-DEFERRED:", StringComparison.Ordinal);
        bool shWired = sh.Contains("deferred-work", StringComparison.OrdinalIgnoreCase)
            || sh.Contains("DW6-CI-DEFERRED:", StringComparison.Ordinal);
        bool ciWired = workflow.Contains("deferred-work", StringComparison.OrdinalIgnoreCase)
            || workflow.Contains("DW6-CI-DEFERRED:", StringComparison.Ordinal);

        ps1Wired.ShouldBeTrue("PowerShell docs validation must invoke the checker or document a DW6-CI-DEFERRED reason.");
        shWired.ShouldBeTrue("Bash docs validation must invoke the checker or document a DW6-CI-DEFERRED reason.");
        ciWired.ShouldBeTrue("CI docs validation must invoke the checker or document a DW6-CI-DEFERRED reason.");
    }
}
