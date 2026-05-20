using System.Text.RegularExpressions;

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

    [Fact]
    public void EvidenceFolder_DecisionLedger_ListsAllowedDispositionVocabulary() {
        // AC#1 — The decision ledger MUST exist (any of: ledger.md, decision-ledger.md, dispositions.md, or
        // index.md / README.md / evidence.md when those serve as the ledger) and MUST name every value in the
        // disposition vocabulary at least once so reviewers can audit the per-item classification.
        string folder = EvidenceFolder();
        Directory.Exists(folder).ShouldBeTrue(
            customMessage: $"DW5 AC#1 requires the evidence folder at: {folder}");

        string? ledgerPath = new[] {
                "decision-ledger.md", "ledger.md", "dispositions.md",
                "evidence-index.md", "index.md", "README.md", "evidence.md",
            }
            .Select(name => Path.Combine(folder, name))
            .FirstOrDefault(File.Exists);

        _ = ledgerPath.ShouldNotBeNull(
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

    [Fact]
    public void EvidenceFolder_ExistsUnderTestArtifacts() {
        // AC#10 — The evidence folder is the durable home for DW5 runtime artefacts.
        string folder = EvidenceFolder();
        Directory.Exists(folder).ShouldBeTrue(
            customMessage: $"DW5 AC#10 requires evidence folder at: {folder}");
    }

    [Fact]
    public void EvidenceIndex_ContainsAllRequiredSections() {
        // AC#10 — The evidence index MUST include the seven required sections from the
        // story (environment, app URLs, TypeCatalog nav checks, shortcut checks, dialog
        // checks, console errors, deferred-work dispositions). Sections must appear as
        // markdown headings or as table-header rows — narrative substring mentions do
        // not satisfy the gate, otherwise a skeleton with seven words pasted in passes.
        string folder = EvidenceFolder();
        string? indexPath = new[] { "evidence-index.md", "index.md", "README.md", "evidence.md" }
            .Select(name => Path.Combine(folder, name))
            .FirstOrDefault(File.Exists);

        _ = indexPath.ShouldNotBeNull(
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
            // Match either a markdown heading line (^#{1,6}\s+...section...) or a table-
            // header pipe row whose cell starts with the section keyword. Both forms are
            // structurally meaningful; loose narrative mentions are rejected.
            string heading = $"^#{{1,6}}\\s+.*{Regex.Escape(section)}.*$";
            string tableHeader = $"^\\|\\s*{Regex.Escape(section)}[^|]*\\|";
            bool found = Regex.IsMatch(content, heading, RegexOptions.IgnoreCase | RegexOptions.Multiline)
                || Regex.IsMatch(content, tableHeader, RegexOptions.IgnoreCase | RegexOptions.Multiline);
            found.ShouldBeTrue(
                customMessage: $"DW5 AC#10 requires the evidence index to contain a '{section}' section heading or table-header column (narrative mention is not enough).");
        }
    }

    [Fact]
    public void DeferredWork_HasDw5DispositionMarker_OnAtLeastOneBullet() {
        // AC#13 — At least ONE *bullet* in deferred-work.md MUST carry a DW5 disposition
        // marker. The previous file-wide Contains pair (DW5 && RESOLVED) passed trivially
        // because both words appear in unrelated narrative sections. This tightens the
        // gate to require either:
        //   - a bullet line containing STORY:post-epic-deferred-dw5-... ; or
        //   - a bullet line containing DW5 alongside one of the disposition tokens.
        string deferredPath = DeferredWorkPath();
        File.Exists(deferredPath).ShouldBeTrue(
            customMessage: $"deferred-work.md not found at: {deferredPath}");
        string content = File.ReadAllText(deferredPath);

        string[] dispositionTokens = ["RESOLVED", "ACCEPTED-DEBT", "DUPLICATE", "NO-ACTION", "NOT-REPRODUCED-WITH-EVIDENCE"];

        bool hasMarkedBullet = content
            .Split('\n')
            .Where(line => line.TrimStart().StartsWith("- ", StringComparison.Ordinal))
            .Any(line =>
                line.Contains(_storyMarker, StringComparison.Ordinal)
                || (line.Contains("DW5", StringComparison.Ordinal)
                    && dispositionTokens.Any(t => line.Contains(t, StringComparison.Ordinal))));

        hasMarkedBullet.ShouldBeTrue(
            customMessage: "DW5 AC#13 requires at least one *bullet* in deferred-work.md to carry a DW5 disposition "
                + "marker (STORY:" + _storyKey + " or DW5 + RESOLVED/ACCEPTED-DEBT/DUPLICATE/NO-ACTION/NOT-REPRODUCED-WITH-EVIDENCE).");
    }

    [Fact]
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

        var violations = entries
            .Where(e => !allowedPrefixes.Any(p => e.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        violations.ShouldBeEmpty(
            customMessage: "DW5 AC#14: story File List entries outside allowed roots: "
                + string.Join(", ", violations));
    }

    [Fact]
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
        // entries. Match any semver-shaped version (\d+\.[0-9.]+) so a 1.x major bump
        // does not break this gate for an unrelated reason.
        Match[] changeLogRows = Regex.Matches(content, "^\\| \\d{4}-\\d{2}-\\d{2} \\| \\d+\\.[0-9.]+ \\|", RegexOptions.Multiline)
            .ToArray();
        changeLogRows.Length.ShouldBeGreaterThan(3,
            customMessage: "DW5 AC#15: expected at least one new Change Log row added during dev work (story shipped with 3 rows).");

        // Verification Status signal: must contain at least one structured evidence row
        // (bullet or table row) referencing test/build/browser results — narrative
        // mention of the word "evidence" alone is insufficient.
        Match verificationSection = Regex.Match(
            content,
            "^## Verification Status\\s*$([\\s\\S]*?)(?=^## |\\z)",
            RegexOptions.Multiline);
        verificationSection.Success.ShouldBeTrue(
            customMessage: "DW5 AC#15: Verification Status section is missing.");
        string verificationBody = verificationSection.Groups[1].Value;
        bool hasStructuredRow = Regex.IsMatch(
            verificationBody,
            "^[-|].*(test|build|evidence|browser|aspire|playwright|bUnit|E2E)",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);
        hasStructuredRow.ShouldBeTrue(
            customMessage: "DW5 AC#15: Verification Status must include at least one bullet or table row referencing test/build/browser/evidence run results.");
    }

    private static string EvidenceFolder() => Path.Combine(
        Dw5TestPaths.RepoRoot(), "_bmad-output", "test-artifacts", _storyKey);

    private static string DeferredWorkPath() => Path.Combine(
        Dw5TestPaths.RepoRoot(), "_bmad-output", "implementation-artifacts", "deferred-work.md");

    private static string StoryPath() => Path.Combine(
        Dw5TestPaths.RepoRoot(), "_bmad-output", "implementation-artifacts", _storyKey + ".md");

    private static IReadOnlyList<string> ParseFileListEntries(string storyContent) {
        // The story File List section starts with "### File List" or "## File List".
        // Allow trailing whitespace on the heading line and accept bullets in either
        // backticked or plain form, with optional trailing notes (e.g. "(added)" or
        // " - new component"). The entry is the first whitespace-delimited token after
        // the bullet marker, stripped of backticks and Windows separators normalized.
        Match section = Regex.Match(storyContent, "^#{2,3} File List\\s*$([\\s\\S]*?)(?=^#{1,3} |\\z)", RegexOptions.Multiline);
        if (!section.Success) {
            return [];
        }

        List<string> entries = [];
        foreach (Match bullet in Regex.Matches(section.Groups[1].Value, "^-\\s+`?([^`\\s]+)`?", RegexOptions.Multiline)) {
            string raw = bullet.Groups[1].Value.Trim().Trim('`');
            entries.Add(raw.Replace('\\', '/'));
        }

        return entries;
    }
}
