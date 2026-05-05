using System.Text.RegularExpressions;

using Shouldly;

namespace Hexalith.EventStore.Admin.UI.Tests.Governance;

// ATDD red-phase scaffolds for story:
//   _bmad-output/implementation-artifacts/post-epic-deferred-dw5-admin-ui-runtime-follow-ups.md
//
// Combined governance gate covering the file-system-observable acceptance criteria:
//   AC#1  — DW5 baselined; decision ledger / classification stub exists in evidence folder.
//   AC#10 — Runtime evidence artifacts durable: folder + index file with required columns.
//   AC#13 — Deferred-work disposition markers narrow & auditable.
//   AC#14 — Scope boundaries: story File List entries stay under allowed UI / test / evidence roots.
//   AC#15 — Bookkeeping: story Dev Agent Record, Change Log, Verification Status reflect dev work.
//
// These scaffolds are file-system structural — no Aspire, no DAPR, no browser. They live
// alongside the bUnit tests because the Admin.UI.Tests project is the natural home for
// DW5 governance gates and the infrastructure (xUnit + Shouldly) is already wired.
//
// Skip rationale: tests are marked [Fact(Skip = "...")] until the corresponding artefact
// is recorded by the dev. Removing Skip per AC unmarks the gate and keeps it green during
// regression so the next reviewer can rely on it.
public class Dw5GovernanceAtddTests {
    private const string _storyKey = "post-epic-deferred-dw5-admin-ui-runtime-follow-ups";
    private const string _storyMarker = "STORY:post-epic-deferred-dw5-admin-ui-runtime-follow-ups";

    private const string _ac1SkipReason =
        "ATDD red phase — DW5 AC#1 (decision ledger). Remove Skip after the evidence folder contains a "
        + "classification ledger naming each selected deferred bullet with one of: patch-now, evidence-now, "
        + "accepted-debt, obsolete-resolved, duplicate, not-DW5.";
    private const string _ac10FolderSkipReason =
        "ATDD red phase — DW5 AC#10 (evidence folder). Remove Skip after the evidence folder is created.";
    private const string _ac10IndexSkipReason =
        "ATDD red phase — DW5 AC#10 (evidence index file with required columns). Remove Skip after the index "
        + "file is written with sections for environment, app URLs, TypeCatalog nav checks, shortcut checks, "
        + "dialog checks, console errors, and deferred-work dispositions.";
    private const string _ac13SkipReason =
        "ATDD red phase — DW5 AC#13 (DW5 disposition marker on deferred-work.md). Remove Skip after at least "
        + "one bullet in deferred-work.md carries STORY:" + _storyKey + " or DW5 + RESOLVED / ACCEPTED-DEBT / "
        + "DUPLICATE / NO-ACTION.";
    private const string _ac14SkipReason =
        "ATDD red phase — DW5 AC#14 (scope boundaries on File List). Remove Skip after the story File List is "
        + "updated and every entry is rooted under an allowed prefix (UI src/tests/E2E, evidence folder, "
        + "story file itself, sprint-status, change log, predev process notes).";
    private const string _ac15SkipReason =
        "ATDD red phase — DW5 AC#15 (bookkeeping). Remove Skip after the story's Dev Agent Record Completion "
        + "Notes, Change Log row, and Verification Status reflect the dev work — not the template/initial state.";

    [Fact(Skip = _ac1SkipReason)]
    public void EvidenceFolder_DecisionLedger_ListsAllowedDispositionVocabulary() {
        // AC#1 — The decision ledger MUST exist (any of: ledger.md, decision-ledger.md, dispositions.md, or
        // index.md / README.md / evidence.md when those serve as the ledger) and MUST name every value in the
        // disposition vocabulary at least once so reviewers can audit the per-item classification.
        string folder = EvidenceFolder();
        Directory.Exists(folder).ShouldBeTrue(
            customMessage: $"DW5 AC#1 requires the evidence folder at: {folder}");

        string? ledgerPath = new[] {
                "decision-ledger.md", "ledger.md", "dispositions.md",
                "index.md", "README.md", "evidence.md",
            }
            .Select(name => Path.Combine(folder, name))
            .FirstOrDefault(File.Exists);

        ledgerPath.ShouldNotBeNull(
            customMessage: "DW5 AC#1 requires a decision ledger file in the evidence folder.");
        string content = File.ReadAllText(ledgerPath!);

        string[] vocabulary = [
            "patch-now",
            "evidence-now",
            "accepted-debt",
            "obsolete-resolved",
            "duplicate",
            "not-DW5",
        ];

        foreach (string term in vocabulary) {
            content.ShouldContain(term, Case.Insensitive,
                customMessage: $"DW5 AC#1 requires the decision ledger to name '{term}' from the disposition vocabulary.");
        }
    }

    [Fact(Skip = _ac10FolderSkipReason)]
    public void EvidenceFolder_ExistsUnderTestArtifacts() {
        // AC#10 — The evidence folder is the durable home for DW5 runtime artefacts.
        string folder = EvidenceFolder();
        Directory.Exists(folder).ShouldBeTrue(
            customMessage: $"DW5 AC#10 requires evidence folder at: {folder}");
    }

    [Fact(Skip = _ac10IndexSkipReason)]
    public void EvidenceIndex_ContainsAllRequiredSections() {
        // AC#10 — The evidence index MUST include the seven required sections from the
        // story (environment, app URLs, TypeCatalog nav checks, shortcut checks, dialog
        // checks, console errors, deferred-work dispositions). Section headings are
        // matched case-insensitively so dev can use either headings or table titles.
        string folder = EvidenceFolder();
        string? indexPath = new[] { "index.md", "README.md", "evidence.md" }
            .Select(name => Path.Combine(folder, name))
            .FirstOrDefault(File.Exists);

        indexPath.ShouldNotBeNull(
            customMessage: "DW5 AC#10 requires an evidence index file (index.md / README.md / evidence.md) in the evidence folder.");
        string content = File.ReadAllText(indexPath!);

        string[] requiredSections = [
            "Environment",
            "App URL",
            "TypeCatalog",
            "Shortcut",
            "Dialog",
            "Console",
            "Disposition",
        ];

        foreach (string section in requiredSections) {
            content.ShouldContain(section, Case.Insensitive,
                customMessage: $"DW5 AC#10 requires the evidence index to contain a '{section}' section/table.");
        }
    }

    [Fact(Skip = _ac13SkipReason)]
    public void DeferredWork_HasDw5DispositionMarker_OnAtLeastOneBullet() {
        // AC#13 — At least ONE bullet in deferred-work.md MUST carry a DW5 disposition
        // marker. Acceptable forms:
        //   STORY:post-epic-deferred-dw5-admin-ui-runtime-follow-ups
        //   DW5 + (RESOLVED | ACCEPTED-DEBT | DUPLICATE | NO-ACTION)
        string deferredPath = DeferredWorkPath();
        File.Exists(deferredPath).ShouldBeTrue(
            customMessage: $"deferred-work.md not found at: {deferredPath}");
        string content = File.ReadAllText(deferredPath);

        bool hasStoryMarker = content.Contains(_storyMarker, StringComparison.Ordinal);
        bool hasDw5Disposition = content.Contains("DW5", StringComparison.Ordinal)
            && (content.Contains("RESOLVED", StringComparison.Ordinal)
                || content.Contains("ACCEPTED-DEBT", StringComparison.Ordinal)
                || content.Contains("DUPLICATE", StringComparison.Ordinal)
                || content.Contains("NO-ACTION", StringComparison.Ordinal));

        (hasStoryMarker || hasDw5Disposition).ShouldBeTrue(
            customMessage: "DW5 AC#13 requires at least one bullet in deferred-work.md to carry a DW5 disposition "
                + "marker (STORY:" + _storyKey + " or DW5 + RESOLVED/ACCEPTED-DEBT/DUPLICATE/NO-ACTION).");
    }

    [Fact(Skip = _ac14SkipReason)]
    public void StoryFileList_EntriesStayUnderAllowedRoots() {
        // AC#14 — Every entry in the story's `## File List` section MUST live under an
        // allowed root. This pins the scope-boundary contract so reviewers can reject
        // additions that touch Admin API contracts, DAPR YAML, EventStore command/query
        // contracts, SignalR hub semantics, TypeCatalog server endpoints, MCP behavior,
        // evidence-schema validators, or deferred-work governance conventions.
        string storyPath = StoryPath();
        File.Exists(storyPath).ShouldBeTrue();
        IReadOnlyList<string> entries = ParseFileListEntries(File.ReadAllText(storyPath));
        entries.ShouldNotBeEmpty(
            customMessage: "DW5 AC#14: story File List should not be empty by handoff (dev must list touched files).");

        string[] allowedPrefixes = [
            "src/Hexalith.EventStore.Admin.UI/",
            "tests/Hexalith.EventStore.Admin.UI.Tests/",
            "tests/Hexalith.EventStore.Admin.UI.E2E/",
            "_bmad-output/test-artifacts/post-epic-deferred-dw5-admin-ui-runtime-follow-ups/",
            "_bmad-output/implementation-artifacts/post-epic-deferred-dw5-admin-ui-runtime-follow-ups.md",
            "_bmad-output/implementation-artifacts/sprint-status.yaml",
            "_bmad-output/implementation-artifacts/deferred-work.md",
            "_bmad-output/process-notes/",
            "CHANGELOG.md",
        ];

        List<string> violations = entries
            .Where(e => !allowedPrefixes.Any(p => e.Replace('\\', '/').StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        violations.ShouldBeEmpty(
            customMessage: "DW5 AC#14: story File List entries outside allowed roots: "
                + string.Join(", ", violations));
    }

    [Fact(Skip = _ac15SkipReason)]
    public void Story_BookkeepingSectionsReflectDevWork() {
        // AC#15 — At dev handoff the story MUST show evidence of dev work in:
        //   - Dev Agent Record / Completion Notes (a non-template Completion Note),
        //   - Change Log (at least one row added by the dev, dated past 2026-05-05), and
        //   - Verification Status (mentions "evidence" or "browser" — not just template text).
        string storyPath = StoryPath();
        string content = File.ReadAllText(storyPath);

        // Dev work signal: a Completion Note line referencing DW5 implementation work
        // (e.g. "Reproduced", "Fixed", "Recorded", "Captured", "Disposition") rather
        // than the initial "No implementation work has been performed for this story." line.
        bool devNotePresent = !content.Contains(
            "No implementation work has been performed for this story.",
            StringComparison.OrdinalIgnoreCase);
        devNotePresent.ShouldBeTrue(
            customMessage: "DW5 AC#15: Dev Agent Record Completion Notes still says 'No implementation work has been performed' — "
                + "expected at least one dev-recorded Completion Note before handoff.");

        // Change Log signal: at least one Change Log row beyond the three story-creation
        // entries (0.1, 0.2, 0.3 dated 2026-05-04 / 2026-05-05).
        Match[] changeLogRows = Regex.Matches(content, "^\\| \\d{4}-\\d{2}-\\d{2} \\| 0\\.[0-9.]+ \\|", RegexOptions.Multiline)
            .ToArray();
        changeLogRows.Length.ShouldBeGreaterThan(3,
            customMessage: "DW5 AC#15: expected at least one new Change Log row added during dev work (story shipped with 3 rows).");

        // Verification Status signal: mentions "evidence" or "browser" (case-insensitive).
        Regex verificationStatus = new("## Verification Status[\\s\\S]*?(evidence|browser)", RegexOptions.IgnoreCase);
        verificationStatus.IsMatch(content).ShouldBeTrue(
            customMessage: "DW5 AC#15: Verification Status section must reference recorded evidence or browser runs by handoff.");
    }

    private static string EvidenceFolder() => Path.Combine(
        Dw5TestPaths.RepoRoot(), "_bmad-output", "test-artifacts", _storyKey);

    private static string DeferredWorkPath() => Path.Combine(
        Dw5TestPaths.RepoRoot(), "_bmad-output", "implementation-artifacts", "deferred-work.md");

    private static string StoryPath() => Path.Combine(
        Dw5TestPaths.RepoRoot(), "_bmad-output", "implementation-artifacts", _storyKey + ".md");

    private static IReadOnlyList<string> ParseFileListEntries(string storyContent) {
        // The story File List section starts with "### File List" or "## File List" and
        // contains "- `path`" or "- path" bullets until the next heading.
        Match section = Regex.Match(storyContent, "^#{2,3} File List\\s*$([\\s\\S]*?)(?=^#{1,3} |\\z)", RegexOptions.Multiline);
        if (!section.Success) {
            return [];
        }

        List<string> entries = [];
        foreach (Match bullet in Regex.Matches(section.Groups[1].Value, "^-\\s+`?([^`\\s][^`\n]*?)`?\\s*$", RegexOptions.Multiline)) {
            entries.Add(bullet.Groups[1].Value.Trim());
        }

        return entries;
    }
}
