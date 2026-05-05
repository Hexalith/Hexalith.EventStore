using Shouldly;

namespace Hexalith.EventStore.DeferredWorkGovernance.Tests;

/// <summary>
/// DW6 ATDD red-phase scaffolds for AC #10 and #12.
/// These tests pin evidence capture and story bookkeeping expectations.
/// </summary>
public class Dw6BookkeepingAtddTests {
    private const string _skip = "ATDD red phase - DW6 bookkeeping. Remove Skip when implementing the matching AC.";

    [Fact(Skip = _skip + " AC#10.")]
    public void DevAgentRecord_IncludesBeforeAfterCountsAndCheckerOutput() {
        string story = Dw6TestPaths.ReadRepoFile(Dw6TestPaths.StoryPath);

        story.ShouldContain("before unresolved count", Case.Insensitive, customMessage:
            "Dev Agent Record must include the baseline unresolved count.");
        story.ShouldContain("after unresolved count", Case.Insensitive, customMessage:
            "Dev Agent Record must include the final unresolved count.");
        story.ShouldContain("checker output", Case.Insensitive, customMessage:
            "Dev Agent Record must paste or summarize the checker/checklist output.");
    }

    [Fact(Skip = _skip + " AC#10.")]
    public void DevAgentRecord_ListsFilesTouchedAndRemainingUnclassifiedSections() {
        string story = Dw6TestPaths.ReadRepoFile(Dw6TestPaths.StoryPath);

        story.ShouldContain("### File List");
        story.ShouldContain(Dw6TestPaths.DeferredWorkPath);
        story.ShouldContain("intentionally unclassified", Case.Insensitive, customMessage:
            "Any remaining unclassified legacy sections must be listed with a follow-up path.");
    }

    [Fact(Skip = _skip + " AC#12.")]
    public void SprintStatus_MovesToReviewOnlyAfterEvidenceIsRecorded() {
        string sprintStatus = Dw6TestPaths.ReadRepoFile(Dw6TestPaths.SprintStatusPath);
        string story = Dw6TestPaths.ReadRepoFile(Dw6TestPaths.StoryPath);

        bool evidenceRecorded = story.Contains("checker output", StringComparison.OrdinalIgnoreCase)
            && story.Contains("before unresolved count", StringComparison.OrdinalIgnoreCase)
            && story.Contains("after unresolved count", StringComparison.OrdinalIgnoreCase);

        if (sprintStatus.Contains($"{Dw6RuleVocabulary.StoryKey}: review", StringComparison.Ordinal)) {
            evidenceRecorded.ShouldBeTrue(
                "The sprint-status row may move to review only after governance convention, checker output, ledger sweep, and validation evidence are recorded.");
        }
    }

    [Fact(Skip = _skip + " workflow handoff.")]
    public void Story_LinksAtddArtifactsForDevStoryHandoff() {
        string story = Dw6TestPaths.ReadRepoFile(Dw6TestPaths.StoryPath);

        story.ShouldContain(Dw6TestPaths.ChecklistPath, customMessage:
            "Story Dev Notes should link the DW6 ATDD checklist for downstream dev-story work.");
        story.ShouldContain("Hexalith.EventStore.DeferredWorkGovernance.Tests", customMessage:
            "Story Dev Notes should identify the red-phase test project.");
    }
}
