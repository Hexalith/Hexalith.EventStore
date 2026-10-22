using Shouldly;

namespace Hexalith.EventStore.DeferredWorkGovernance.Tests;

/// <summary>
/// DW6 ATDD red-phase scaffolds for AC #5, #6, #7, #10, and #11.
/// These tests guard the curated ledger sweep and story scope boundaries.
/// </summary>
public class Dw6LedgerSweepAtddTests {
    private const string _baseSkip = "ATDD red phase -- DW6 deferred-work governance checker and story artifacts are not implemented. Remove Skip when implementing.";

    [Fact(Skip = _baseSkip)]
    public void CuratedSweep_PreservesRawHistoricalBulletTextAgainstSnapshot() {
        string repoRoot = Dw6TestPaths.LocateRepoRoot();
        string snapshotPath = Path.Combine(repoRoot, Dw6TestPaths.SnapshotPath);
        File.Exists(snapshotPath).ShouldBeTrue(
            $"Take a story-start snapshot at '{Dw6TestPaths.SnapshotPath}' before editing deferred-work.md.");

        IReadOnlyList<string> before = Dw6TestPaths.ExtractMarkdownBullets(File.ReadAllText(snapshotPath));
        IReadOnlyList<string> after = Dw6TestPaths.ExtractMarkdownBullets(
            Dw6TestPaths.ReadRepoFile(Dw6TestPaths.DeferredWorkPath));

        after.Count.ShouldBeGreaterThanOrEqualTo(before.Count,
            "DW6 may add metadata beside legacy bullets but must not delete historical review text.");

        foreach (string oldBullet in before) {
            after.Any(newBullet => newBullet.Contains(oldBullet.Trim(), StringComparison.Ordinal))
                .ShouldBeTrue($"Historical bullet was not preserved: {Dw6TestPaths.Truncate(oldBullet, 180)}");
        }
    }

    [Fact(Skip = _baseSkip)]
    public void TouchedHeadingGroups_HaveOneLineRationaleInDevAgentRecord() {
        string story = Dw6TestPaths.ReadRepoFile(Dw6TestPaths.StoryPath);

        story.ShouldContain("### Debug Log References");
        story.ShouldContain("### Completion Notes List");
        story.ShouldContain("Touched deferred-work groups", customMessage:
            "Every ledger group touched by the sweep must have a one-line rationale in the Dev Agent Record.");
    }

    [Fact(Skip = _baseSkip)]
    public async Task Checker_DoesNotTurnEveryLegacyBulletIntoABlockingFailure() {
        Dw6GovernanceReport report = await Dw6GovernanceCheckerInvokerFactory.Create()
            .CheckAsync(["--legacy-advisory", Dw6TestPaths.DeferredWorkPath]);

        report.Diagnostics.Count(d => d.Rule == "dw6-unclassified-live-bullet")
            .ShouldBeLessThan(Dw6TestPaths.ExtractMarkdownBullets(
                Dw6TestPaths.ReadRepoFile(Dw6TestPaths.DeferredWorkPath)).Count,
                "Legacy-advisory mode must avoid failing every historical bullet before the curated sweep is complete.");
    }

    [Fact(Skip = _baseSkip)]
    public void ScopeBoundaries_DoNotClaimProductRuntimeOrGeneratedAuditFiles() {
        string story = Dw6TestPaths.ReadRepoFile(Dw6TestPaths.StoryPath);

        story.ShouldNotContain("src/Hexalith.EventStore/", customMessage:
            "DW6 must not implement product/runtime fixes.");
        story.ShouldNotContain("edited generated preflight JSON", customMessage:
            "DW6 must not claim generated preflight JSON audit files were edited.");
        story.ShouldContain("Do not initialize or update nested submodules", customMessage:
            "The nested-submodule boundary must remain explicit.");
    }
}
