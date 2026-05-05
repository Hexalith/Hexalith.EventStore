using Shouldly;

namespace Hexalith.EventStore.Admin.Server.Tests.Evidence;

// ATDD red-phase scaffolds for story:
//   _bmad-output/implementation-artifacts/post-epic-deferred-dw2-admin-dapr-mcp-live-evidence.md
//
// Locks the structural completeness of the DW2 evidence package so reviewers can verify the
// promised artefacts without launching Aspire:
//   AC#11 Evidence artifacts durable & reviewable — folder + index file with required tables
//         (runtime baseline, Admin DAPR, Epic 20 debugging, MCP, latency, blockers, dispositions,
//         how-to-rerun) and a per-surface RemoteMetadataStatus matrix.
//   AC#12 Deferred-work dispositions updated narrowly — touched bullets in
//         `_bmad-output/implementation-artifacts/deferred-work.md` carry a DW2 marker
//         (STORY:post-epic-deferred-dw2-admin-dapr-mcp-live-evidence, RESOLVED, ACCEPTED-DEBT,
//          DUPLICATE, or NO-ACTION).
//
// These scaffolds are file-system structural — no Aspire, no DAPR, no Docker. They live in the
// Tier 1 Admin.Server.Tests project because Admin.Server is the primary consumer of the evidence
// surface; an alternative home would be a doc-validation project, which doesn't exist yet.
//
// Skip rationale: tests are marked [Fact(Skip = "...")] until the DW2 evidence has been recorded.
// Removing Skip per AC means the dev has captured the corresponding artefact and the gate will
// stay green during regression.
public class Dw2EvidenceIndexAtddTests
{
    private const string SkipReasonAc11Folder = "ATDD red phase — DW2 AC#11 (evidence folder). Remove Skip after `_bmad-output/test-artifacts/post-epic-deferred-dw2-admin-dapr-mcp-live-evidence/` is created during the smoke run.";
    private const string SkipReasonAc11Index = "ATDD red phase — DW2 AC#11 (evidence index tables). Remove Skip after the evidence index file is written with all required tables.";
    private const string SkipReasonAc11Matrix = "ATDD red phase — DW2 AC#11 (per-surface RemoteMetadataStatus matrix). Remove Skip after the index records sidecar/actors/pubsub each as their own row.";
    private const string SkipReasonAc11Canonical = "ATDD red phase — DW2 AC#11 (canonical seeded-stream identifier block). Remove Skip after the canonical block is recorded once and reused across Admin API, Admin UI, MCP, and CommandAPI artefacts.";
    private const string SkipReasonAc12 = "ATDD red phase — DW2 AC#12 (deferred-work disposition markers). Remove Skip after the relevant bullets in deferred-work.md carry a DW2 disposition marker.";

    private static string RepoRoot()
    {
        // tests/Hexalith.EventStore.Admin.Server.Tests/bin/<config>/<tfm>/ → repo root
        string testDir = Path.GetDirectoryName(typeof(Dw2EvidenceIndexAtddTests).Assembly.Location)!;
        return Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", "..", ".."));
    }

    private static string EvidenceFolder() => Path.Combine(
        RepoRoot(), "_bmad-output", "test-artifacts", "post-epic-deferred-dw2-admin-dapr-mcp-live-evidence");

    private static string DeferredWorkPath() => Path.Combine(
        RepoRoot(), "_bmad-output", "implementation-artifacts", "deferred-work.md");

    [Fact(Skip = SkipReasonAc11Folder)]
    public void EvidenceFolder_ExistsUnderTestArtifacts()
    {
        // AC#11 — The evidence folder is the durable home for DW2 artefacts. Story Task 0.2
        // explicitly names this path; the scaffold gate fails until the folder is created.
        string folder = EvidenceFolder();
        Directory.Exists(folder).ShouldBeTrue(
            customMessage: $"DW2 AC#11 requires evidence folder at: {folder}");
    }

    [Fact(Skip = SkipReasonAc11Index)]
    public void EvidenceIndex_ContainsAllRequiredTables()
    {
        // AC#11 — Evidence index MUST include eight tables. Each table header MUST be present so
        // reviewers can pair the smoke output with the index row. Acceptable index file names:
        // index.md, README.md, evidence.md (story Task 0.3 — markdown index file).
        string folder = EvidenceFolder();
        string? indexPath = new[] { "index.md", "README.md", "evidence.md" }
            .Select(name => Path.Combine(folder, name))
            .FirstOrDefault(File.Exists);

        indexPath.ShouldNotBeNull(
            customMessage: "DW2 AC#11 requires an evidence index file (index.md / README.md / evidence.md) in the evidence folder.");
        string content = File.ReadAllText(indexPath!);

        string[] requiredHeadings = [
            "Runtime baseline",
            "Admin DAPR",
            "Epic 20",
            "MCP",
            "Latency",
            "Blockers",
            "Dispositions",
            "How to rerun",
        ];

        foreach (string heading in requiredHeadings) {
            content.ShouldContain(heading, Case.Insensitive,
                customMessage: $"DW2 AC#11 requires the evidence index to contain a '{heading}' table/section.");
        }
    }

    [Fact(Skip = SkipReasonAc11Matrix)]
    public void EvidenceIndex_RemoteMetadataStatusMatrix_HasRowsPerSurface()
    {
        // AC#11 — RemoteMetadataStatus MUST be recorded per surface. The matrix MUST contain the
        // strings "sidecar", "actors", and "pubsub" (case-insensitive) so a reviewer can confirm
        // each surface has its own row, not a single global degraded flag.
        string folder = EvidenceFolder();
        string? indexPath = new[] { "index.md", "README.md", "evidence.md" }
            .Select(name => Path.Combine(folder, name))
            .FirstOrDefault(File.Exists);
        indexPath.ShouldNotBeNull();
        string content = File.ReadAllText(indexPath!);

        content.ShouldContain("sidecar", Case.Insensitive);
        content.ShouldContain("actors", Case.Insensitive);
        content.ShouldContain("pubsub", Case.Insensitive);
        content.ShouldContain("RemoteMetadataStatus", Case.Insensitive);
    }

    [Fact(Skip = SkipReasonAc11Canonical)]
    public void EvidenceIndex_RecordsCanonicalSeededStreamIdentifierBlock()
    {
        // AC#11 — Cross-Surface Consistency Rules require ONE canonical identifier block reused
        // across Admin API, Admin UI, MCP, and CommandAPI artefacts. The scaffold gate verifies
        // the canonical labels appear exactly once in the index file (label presence — not value).
        string folder = EvidenceFolder();
        string? indexPath = new[] { "index.md", "README.md", "evidence.md" }
            .Select(name => Path.Combine(folder, name))
            .FirstOrDefault(File.Exists);
        indexPath.ShouldNotBeNull();
        string content = File.ReadAllText(indexPath!);

        content.ShouldContain("TenantId", Case.Insensitive);
        content.ShouldContain("Domain", Case.Insensitive);
        content.ShouldContain("AggregateId", Case.Insensitive);
        content.ShouldContain("CorrelationId", Case.Insensitive);
        content.ShouldContain("EventCount", Case.Insensitive);
    }

    [Fact(Skip = SkipReasonAc12)]
    public void DeferredWork_HasDw2DispositionMarker_OnAtLeastOneBullet()
    {
        // AC#12 — At least ONE bullet in deferred-work.md MUST carry a DW2 disposition marker.
        // Acceptable markers:
        //   STORY:post-epic-deferred-dw2-admin-dapr-mcp-live-evidence
        //   RESOLVED, ACCEPTED-DEBT, DUPLICATE, NO-ACTION (when paired with a DW2 reference)
        // The narrow scope guard (story AC#12) requires only DW2-relevant bullets to be touched.
        string deferredPath = DeferredWorkPath();
        File.Exists(deferredPath).ShouldBeTrue(
            customMessage: $"deferred-work.md not found at: {deferredPath}");
        string content = File.ReadAllText(deferredPath);

        bool hasDw2Marker = content.Contains("STORY:post-epic-deferred-dw2-admin-dapr-mcp-live-evidence", StringComparison.Ordinal)
            || (content.Contains("DW2", StringComparison.Ordinal)
                && (content.Contains("RESOLVED", StringComparison.Ordinal)
                    || content.Contains("ACCEPTED-DEBT", StringComparison.Ordinal)
                    || content.Contains("DUPLICATE", StringComparison.Ordinal)
                    || content.Contains("NO-ACTION", StringComparison.Ordinal)));

        hasDw2Marker.ShouldBeTrue(
            customMessage: "DW2 AC#12 requires at least one bullet in deferred-work.md to carry a DW2 disposition marker (STORY:post-epic-deferred-dw2-... or DW2 + RESOLVED/ACCEPTED-DEBT/DUPLICATE/NO-ACTION).");
    }
}
