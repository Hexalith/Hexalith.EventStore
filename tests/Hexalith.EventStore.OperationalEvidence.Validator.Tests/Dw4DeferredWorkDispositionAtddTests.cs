using Shouldly;

namespace Hexalith.EventStore.OperationalEvidence.Validator.Tests;

/// <summary>
/// DW4 ATDD red-phase scaffolds for AC #12 — deferred-work dispositions are
/// updated narrowly. DW4-relevant bullets in
/// <c>_bmad-output/implementation-artifacts/deferred-work.md</c> must carry
/// one of the documented disposition markers; unrelated DW1/DW2/DW3/DW5/DW6/
/// SignalR-policy/Admin-UI/release-governance entries must be unchanged from
/// a snapshot taken at story start.
/// </summary>
public class Dw4DeferredWorkDispositionAtddTests {
    private const string _baseSkip = "ATDD red phase — DW4 ";
    private const string _deferredWorkPath = "_bmad-output/implementation-artifacts/deferred-work.md";
    private const string _snapshotPath = "_bmad-output/test-artifacts/operational-evidence-validator/deferred-work-snapshot.md";

    private static readonly string[] _dw4RelevantSearchTerms = [
        "query operational evidence validator",
        "query-operational-evidence/v1 validator",
        "signalr operational evidence validator",
        "signalr-operational-evidence/v1 validator",
        "evidence schema validator",
        "evidence-template validator",
    ];

    [Fact(Skip = _baseSkip + "AC#12 — DW4-relevant deferred-work bullets must carry a disposition marker. Remove Skip when implementing.")]
    public void DeferredWork_Dw4RelevantBullets_CarryDispositionMarker() {
        string repoRoot = LocateRepoRoot();
        string deferredWorkContent = File.ReadAllText(Path.Combine(repoRoot, _deferredWorkPath));

        IEnumerable<string> bullets = ExtractBullets(deferredWorkContent)
            .Where(b => _dw4RelevantSearchTerms.Any(term =>
                b.Contains(term, StringComparison.OrdinalIgnoreCase)));

        IList<string> bulletsList = bullets.ToList();
        bulletsList.ShouldNotBeEmpty(
            "Could not locate any DW4-relevant bullets in deferred-work.md. " +
            "Either the search terms in this test are stale, or DW4 has nothing to dispose " +
            "(in which case the story may be a no-op — confirm and revise this assertion).");

        foreach (string bullet in bulletsList) {
            bool hasMarker = Dw4RuleVocabulary.DispositionMarkers.Any(marker =>
                bullet.Contains(marker, StringComparison.Ordinal));
            hasMarker.ShouldBeTrue(
                $"DW4-relevant bullet must carry one of " +
                $"[{string.Join(", ", Dw4RuleVocabulary.DispositionMarkers)}]. " +
                $"Bullet text: <{Truncate(bullet, 200)}>");
        }
    }

    [Fact(Skip = _baseSkip + "AC#12 — unrelated bullets must be unchanged from story-start snapshot. Remove Skip when implementing.")]
    public void DeferredWork_UnrelatedBullets_AreUnchangedFromSnapshot() {
        string repoRoot = LocateRepoRoot();
        string snapshotFullPath = Path.Combine(repoRoot, _snapshotPath);

        File.Exists(snapshotFullPath).ShouldBeTrue(
            $"Story-start snapshot at '{_snapshotPath}' is missing. " +
            "Dev must capture it at story start so this test can detect drift in unrelated bullets.");

        string snapshotContent = File.ReadAllText(snapshotFullPath);
        string currentContent = File.ReadAllText(Path.Combine(repoRoot, _deferredWorkPath));

        IList<string> snapshotBullets = ExtractBullets(snapshotContent)
            .Where(b => !_dw4RelevantSearchTerms.Any(t => b.Contains(t, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        IList<string> currentUnrelatedBullets = ExtractBullets(currentContent)
            .Where(b => !_dw4RelevantSearchTerms.Any(t => b.Contains(t, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        currentUnrelatedBullets.Count.ShouldBe(snapshotBullets.Count,
            "Unrelated bullet count must not change. AC #12 forbids touching DW1/DW2/DW3/DW5/DW6/" +
            "SignalR-policy/Admin-UI/release-governance entries while resolving DW4.");

        for (int i = 0; i < snapshotBullets.Count; i++) {
            currentUnrelatedBullets[i].ShouldBe(snapshotBullets[i],
                $"Unrelated bullet at index {i} drifted from snapshot. AC #12 forbids touching " +
                "non-DW4 deferred-work entries.");
        }
    }

    private static IEnumerable<string> ExtractBullets(string content) {
        // Naive bullet extraction: lines starting with '-' or '*' after optional whitespace.
        // The validator (story deliverable) replaces this with a proper parser; for test
        // purposes this is sufficient because deferred-work.md uses plain markdown bullets.
        foreach (string rawLine in content.Split('\n')) {
            string line = rawLine.TrimEnd('\r');
            string trimmed = line.TrimStart();
            if (trimmed.StartsWith("- ", StringComparison.Ordinal)
                || trimmed.StartsWith("* ", StringComparison.Ordinal)) {
                yield return line;
            }
        }
    }

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : string.Concat(s.AsSpan(0, max), "…");

    private static string LocateRepoRoot() {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null) {
            if (File.Exists(Path.Combine(dir, "Hexalith.EventStore.slnx"))) {
                return dir;
            }

            DirectoryInfo? parent = Directory.GetParent(dir);
            if (parent is null) {
                break;
            }

            dir = parent.FullName;
        }

        throw new InvalidOperationException("Repo root (Hexalith.EventStore.slnx) not found.");
    }
}
