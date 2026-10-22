using Shouldly;

namespace Hexalith.EventStore.DeferredWorkGovernance.Tests;

/// <summary>
/// DW6 ATDD red-phase scaffolds for AC #1, #2, #3, #5, and #8.
/// These tests pin the human-facing governance convention in deferred-work.md.
/// </summary>
public class Dw6GovernanceVocabularyAtddTests {
    private const string _baseSkip = "ATDD red phase -- DW6 deferred-work governance checker and story artifacts are not implemented. Remove Skip when implementing.";

    [Fact(Skip = _baseSkip)]
    public void DeferredWork_GovernanceSection_DefinesCanonicalDispositions() {
        string content = Dw6TestPaths.ReadRepoFile(Dw6TestPaths.DeferredWorkPath);

        content.ShouldContain("## Deferred-Work Governance", customMessage:
            "DW6 AC #1 requires a top-level governance section in deferred-work.md.");

        foreach (string disposition in Dw6RuleVocabulary.CanonicalDispositions) {
            content.ShouldContain(disposition, customMessage:
                $"Governance section must define canonical disposition '{disposition}'.");
        }
    }

    [Fact(Skip = _baseSkip)]
    public async Task OpenAndStoryDispositions_RequireOwnerAndNextReviewDate() {
        IDw6GovernanceCheckerInvoker checker = Dw6GovernanceCheckerInvokerFactory.Create();
        Dw6GovernanceReport report = await checker.CheckAsync(["--fixture", "missing-open-metadata"]);

        report.ExitCode.ShouldNotBe(0, "OPEN/STORY items missing owner or next-review-date must fail closed.");
        report.EmittedRuleIds.ShouldContain("dw6-open-missing-owner");
        report.EmittedRuleIds.ShouldContain("dw6-open-missing-next-review-date");
    }

    [Fact(Skip = _baseSkip)]
    public void NewDeferralGuidance_RequiresGroupingHint() {
        string content = Dw6TestPaths.ReadRepoFile(Dw6TestPaths.DeferredWorkPath);

        content.ShouldContain("grouping", customMessage:
            "Future deferrals must include grouping guidance such as story key, post-epic bucket, epic, component, or needs-triage.");
        content.ShouldContain("needs-triage", customMessage:
            "Governance guidance must name needs-triage as the fallback grouping.");
    }

    [Fact(Skip = _baseSkip)]
    public void LegacyCompatibility_DocumentsRecognizedMixedForms() {
        string content = Dw6TestPaths.ReadRepoFile(Dw6TestPaths.DeferredWorkPath);

        foreach (string legacyForm in Dw6RuleVocabulary.AcceptedLegacyForms) {
            content.ShouldContain(legacyForm, customMessage:
                $"DW6 must either recognize or document legacy form '{legacyForm}' as legacy-advisory.");
        }
    }

    [Fact(Skip = _baseSkip)]
    public async Task LegacyCompatibility_CheckerClassifiesMixedMarkerFixture() {
        IDw6GovernanceCheckerInvoker checker = Dw6GovernanceCheckerInvokerFactory.Create();
        Dw6GovernanceReport report = await checker.CheckAsync(["--fixture", "legacy-mixed-marker"]);

        // Counts prove that legacy bare-form STORY:..., RESOLVED-IN-..., and DW1-disposition:accepted-debt
        // are actually recognized by the checker, not just documented in prose.
        report.Counts["RESOLVED"].ShouldBeGreaterThan(0,
            "RESOLVED-IN-VALIDATOR legacy form must classify as RESOLVED via the checker, not only via prose mention.");
        report.Counts["ACCEPTED-DEBT"].ShouldBeGreaterThan(0,
            "DW1 disposition: accepted-debt legacy form must classify as ACCEPTED-DEBT via the checker.");

        // Multi-marker entries must surface secondary markers as compatibility context, proving
        // the deterministic-precedence rule is implemented and not just documented.
        report.EmittedRuleIds.ShouldContain("dw6-secondary-disposition-advisory",
            "Mixed-marker legacy entries must produce secondary-disposition advisory diagnostics for traceability.");
    }

    [Fact(Skip = _baseSkip)]
    public void ReviewerAndRetrospectiveHandoffGuidance_IsDocumentedAndLinkedFromStory() {
        string deferredWork = Dw6TestPaths.ReadRepoFile(Dw6TestPaths.DeferredWorkPath);
        string story = Dw6TestPaths.ReadRepoFile(Dw6TestPaths.StoryPath);

        deferredWork.ShouldContain("code review", customMessage:
            "Reviewer guidance must explain how to append a new deferred-work item.");
        deferredWork.ShouldContain("retrospective", customMessage:
            "Retrospective guidance must summarize OPEN count and promoted stories.");
        story.ShouldContain(Dw6TestPaths.DeferredWorkPath, customMessage:
            "The DW6 story must link the handoff guidance location.");
    }
}
