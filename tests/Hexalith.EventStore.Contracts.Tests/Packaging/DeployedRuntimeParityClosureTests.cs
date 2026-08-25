using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hexalith.EventStore.Contracts.Tests.Packaging;

/// <summary>
/// Verifies the Story 3.13 deployed-runtime parity closure evidence contract.
/// </summary>
public sealed class DeployedRuntimeParityClosureTests
{
    private const string ApprovedSourceSha = "fa2d1c9910f8976553adb33dcdb1c9ff2ea75594";
    private const string ApprovedPackageVersion = "999.1.20-proof.fa2d1c9910f8";
    private const string SelectedSourceSha = "80d12ef5eee71a9fe3ea7be51171da4a71b69a28";
    private const string SelectedPackageVersion = "3.94.1";
    private const string SelectedIndexDigest =
        "sha256:ab8784c8c9c67229ee178e9d6dd809df9554b3cdafb43ffb7bfd38c792e2afcd";
    private const string SelectedEvidenceRelativePath =
        "_bmad-output/implementation-artifacts/evidence/story-3-13/" +
        SelectedSourceSha +
        "/ab8784c8c9c67229ee178e9d6dd809df9554b3cdafb43ffb7bfd38c792e2afcd";
    private const string ApprovedPackageManifestSha256 =
        "4271ddc76411780591423ab024b776cd34a2abccf1cc2dac03a245e141dbe0bc";
    private const string EvidenceRelativePath =
        "_bmad-output/implementation-artifacts/evidence/story-3-13/" +
        ApprovedSourceSha +
        "/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87";
    private const string ExpectedIndexDigest =
        "sha256:523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87";
    private const string ProofRelativePath =
        "_bmad-output/implementation-artifacts/3-13-deployed-runtime-parity-closure-proof-packet.md";
    private const string OciIndexMediaType = "application/vnd.oci.image.index.v1+json";
    private const string OciManifestMediaType = "application/vnd.oci.image.manifest.v1+json";
    private const string OciConfigMediaType = "application/vnd.oci.image.config.v1+json";
    private const string ReviewerRosterFile = "reviewer-roster.json";
    private const string ExpectedBaselineCommit = "1d6e9321acfc416768c1c78e9facf573c9c41f71";
    private const string ExpectedBaselineBuildsSha = "e69891f67578c2f0dec1cd7d7eea113430d31077";
    private const string ExpectedBuildsSha = "a53166539bf4441d5e33d04281b14c2d59e950c3";
    private const string ExpectedRepository = "Hexalith/Hexalith.EventStore";
    private const string ExpectedRegistry = "registry.hexalith.com";
    private const string ExpectedContainerRepository = "eventstore";
    private const string ExpectedSmokeToolPath =
        "references/Hexalith.Builds/Github/publish-containers/smoke_container_platforms.py";
    private const string ExpectedSmokeToolSha256 =
        "c7ec862fd79bf96be12670d53707e3c8a828e0161e58745e57b652a42243e8a9";
    private const string ExpectedOciValidatorPath =
        "references/Hexalith.Builds/Github/publish-containers/oci_registry_validator.py";
    private const string ExpectedOciValidatorSha256 =
        "e1547e31fbdb8a678c99a245510e718c1cb35f6b9ec51264aa7bc1cdae419509";
    private const string ReceiptDirectoryTemplate = "acceptances/{subject_sha256}";

    private const string SelectedReviewSubjectSha256 =
        "6cee8dad34c1233c6184404b409fb65d1a4dd0bccdd0d0ee54e8869120970a97";
    private const string DispositionRelativePath =
        "_bmad-output/implementation-artifacts/evidence/story-3-13/disposition/" +
        SelectedReviewSubjectSha256;
    private const string DispositionEnvelopeFile = "disposition-envelope.json";
    private const string DispositionManifestFile = "disposition-sha256.txt";
    private const string DispositionSchema =
        "hexalith.eventstore.story-3-13-disposition-envelope/v1";
    private const string RejectedDisposition = "rejected-non-authorizing";
    private const string UnavailableDeployedParity = "unavailable-for-v3.94.1";
    private const string SelectedReleaseTag = "v3.94.1";
    private const string DispositionReceiptTemplate = "acceptances/{envelope_sha256}";
    private const string DispositionReceiptSchema =
        "hexalith.eventstore.story-3-13-acceptance-receipt/v1";
    private const string DispositionSourceSchema =
        "hexalith.eventstore.story-3-13-acceptance-source/v2";
    private const string SelectedProofRelativePath =
        "_bmad-output/implementation-artifacts/" +
        "3-13-deployed-runtime-parity-closure-v3.94.1-proof-packet.md";
    private const string DispositionAuthorityRelativePath =
        "_bmad-output/planning-artifacts/sprint-change-proposal-2026-08-16.md";
    private const string Story120EvidenceRelativePath =
        "_bmad-output/implementation-artifacts/evidence/story-1-20/" + ApprovedSourceSha;
    private const string StoryRecordRelativePath =
        "_bmad-output/implementation-artifacts/3-13-deployed-runtime-parity-closure.md";
    private const string SpecRelativePath =
        "_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md";
    private const string SprintStatusRelativePath =
        "_bmad-output/implementation-artifacts/sprint-status.yaml";
    private const string CiDocumentationRelativePath = "docs/ci.md";
    private const string DispositionVerifierPath =
        "tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs";
    private const string RevisionLabel = "org.opencontainers.image.revision";
    private const string MalformedLabelValue = "https";

    private static readonly JsonSerializerOptions CanonicalDispositionJsonOptions =
        new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static readonly string[] DispositionEnvelopeFields =
    [
        "acceptance_contract",
        "assembled_at",
        "candidate",
        "candidate_disposition",
        "deployed_runtime_parity",
        "deployment_authorized",
        "governing_authority",
        "limitations",
        "referenced_evidence",
        "retained_blockers",
        "retained_checksum_manifests",
        "retained_identity",
        "retained_provenance_defects",
        "revalidation_trigger",
        "review_subject",
        "schema",
        "selected_deployed_identity",
        "story_id",
        "successor_boundary",
        "verification",
    ];

    private static readonly string[] MalformedProvenanceLabels =
    [
        "org.opencontainers.image.documentation",
        "org.opencontainers.image.source",
        "org.opencontainers.image.url",
    ];

    private static readonly string[] RetainedBlockerIds =
    [
        "deployment-authority-missing",
        "malformed-oci-provenance-labels",
        "story-3-13-acceptances-missing",
    ];

    // Concrete identity material owned by another lineage. Ancestry, tags, and labels are
    // insufficient evidence, so the disposition may never carry any of these Story 1.20,
    // v3.75.0/v3.77.1/v3.77.2, or Story 3.14 values.
    private static readonly string[] ForeignLineageTokens =
    [
        "3.75.0",
        "3.77.1",
        "3.77.2",
        "3.96.2",
        "4b1410852b11be3bcaebf8f2e6277c1d30ce13a19f48cf0df86ed93646d709c3",
        "523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87",
        "77a9a442c0e6d0408957888e10c3a9accd634c99",
        "999.1.20-proof",
        "db3ab41e187efc0de397fd1205660a0f685e2c94ecd8f4a8f1843ac567056bf6",
        "f343bb0153e9cdcb8b12ec10153813072f5ad38d",
        ApprovedSourceSha,
    ];

    private const string DispositionRevalidationTrigger =
        "re-verify every retained checksum manifest and the frozen review subject before any " +
        "re-declaration; never re-capture v3.94.1 evidence to make a manifest match";

    // The complete frozen identity field set of the retained 6cee8dad review subject.
    private static readonly string[] SubjectFrozenIdentityFields =
    [
        "authority_record_sha256",
        "canonical_lineage_id",
        "index_digest",
        "package_hash_manifest_sha256",
        "package_version",
        "release_version",
        "source_sha",
        "workflow_run",
    ];

    private static readonly string[] SubjectBoundIdentityFields =
    [
        "authority_record_sha256",
        "index_digest",
        "package_version",
        "release_version",
        "source_sha",
        "workflow_run",
    ];

    private static readonly (string File, int EntryCount, string Base)[] RetainedManifestDefinitions =
    [
        ("evidence-core-sha256.txt", 34, "evidence-root"),
        ("evidence-sha256.txt", 3, "evidence-root"),
        ("nuget-sha256.txt", 14, "evidence-root/packages"),
        ("predecessor-tree-sha256.txt", 40, "repository-root"),
    ];

    private static readonly string[] DispositionSpecificLimitations =
    [
        "This envelope disposes the immutable v3.94.1 candidate as rejected and non-authorizing; " +
            "it selects no deployed image and grants no deployment or consumer-migration authority.",
        "Positive FR36 deployed-runtime parity stays open and is owned by Story 3.15 after the " +
            "separately authorized Story 3.14 corrective release.",
        "The approved 2026-08-16 correct-course decision is planning authority only and is never " +
            "an acceptance receipt.",
    ];

    private static readonly string[] DispositionSupportingFiles =
    [
        SelectedProofRelativePath,
        DispositionAuthorityRelativePath,
    ];

    private static readonly JsonSerializerOptions IndentedDispositionJsonOptions =
        new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, WriteIndented = true };

    private static readonly string[] ExpectedChecks =
    [
        "content_bound_acceptances",
        "deployment_authority",
        "exact_source",
        "oci_graph",
        "oci_provenance_labels",
        "package_bytes",
        "package_inventory",
        "predecessor_integrity",
        "runtime_both_platforms",
        "semantic_release_provenance",
        "single_lineage",
        "source_release_exact_match",
    ];

    private static readonly string[] RequiredReceiptFields =
    [
        "accepted_at",
        "accepted_limitations",
        "accepted_scope",
        "decision",
        "durable_source",
        "reviewer_identity",
        "role",
        "schema",
        "subject_sha256",
    ];

    private static readonly string[] RequiredRoles =
    [
        "eventstore-owner",
        "release-owner",
        "test-architect",
    ];

    private static readonly string[] ExpectedCoreFiles =
    [
        "child-linux-amd64.config.raw",
        "child-linux-amd64.manifest.raw",
        "child-linux-arm64.config.raw",
        "child-linux-arm64.manifest.raw",
        "digest-response.raw",
        "index.raw",
        "oci-validation.json",
        "package-availability.json",
        "predecessor-tree-sha256.txt",
        "registry-readback.json",
        ReviewerRosterFile,
        "runtime-verification.json",
        "smoke-linux-amd64.log",
        "smoke-linux-arm64.log",
        "smoke-preflight.log",
        "smoke-results.json",
        "tag-response.raw",
    ];

    private static readonly string[] ExpectedOuterFiles =
    [
        "evidence-core-sha256.txt",
        "identity-crosswalk.json",
        "review-subject.json",
    ];

    private static readonly string[] ExpectedSupportSafeJsonReports =
    [
        "oci-validation.json",
        "package-availability.json",
        "registry-readback.json",
        ReviewerRosterFile,
        "runtime-verification.json",
        "smoke-results.json",
    ];

    private static readonly string[] ExpectedSupportSafeUriHosts =
    [
        "github.com",

        // A retained acceptance comment is the verbatim GitHub REST response, whose `url` and
        // `issue_url` are necessarily api.github.com. The allowlist stays fail-closed; this is the
        // one additional public host the acceptance contract can now legitimately cite.
        "api.github.com",
        ExpectedRegistry,
    ];

    private static readonly string[] ExpectedMutationLimitations =
    [
        "No package, release, registry, deployment, consumer, predecessor, Epic 1, or submodule mutation is authorized.",
        "The selected package hashes are retained evidence, but the original archive bytes are unavailable for independent rehashing.",
        "The retained smoke artifacts report Development execution while docs/ci.md requires Production; independently proven liveness and equivalent runtime smoke both remain open.",
        "The image source, URL, and documentation OCI labels are the malformed value https and supply no revision provenance.",
        "The retained OCI descriptor/body relationships pass, but complete response-metadata replay, semantic-release provenance, and an exact source revision mapping are missing.",
        "Child-manifest and config response metadata was not retained, so the live registry response checks are not reproducible from the packet.",
        "The smoke logs do not retain structured HTTP, observed-platform, per-platform timing, or exit-code facts and therefore cannot independently prove executed liveness.",
        "The current shared validator CLI cannot accept the non-SemVer quarantine tag; the immutable graph was validated with its unchanged validation functions.",
        "No Story 3.13 owner or Test Architect acceptance has been requested or inferred; future receipts stay outside hashed evidence.",
    ];

    private static readonly string[] ExpectedPredecessorFiles =
    [
        "ad11-preflight.json",
        "ad11-preflight.sha256",
        "approval-subject.json",
        "candidate-source-sha.txt",
        "capture-generated-image-index.targets",
        "container-inspect.txt",
        "container-manifest.json",
        "container-platforms.txt",
        "container-provenance.json",
        "critical-evidence-expected-files.txt",
        "critical-evidence-sha256.txt",
        "discovered-test-projects.txt",
        "dotnet-runtimes-current.txt",
        "dotnet-runtimes.txt",
        "effective-package-versions-debug.txt",
        "effective-package-versions-release.txt",
        "environment.txt",
        "eventstore-owner-proof-approval.github.json",
        "eventstore-owner-proof-approval.json",
        "expected-package-ids.txt",
        "generated-image-index.digest.txt",
        "generated-image-index.json",
        "latest-successful-gate-completed-at.txt",
        "mandatory-test-projects.txt",
        "nuget-sha256.txt",
        "package-files.txt",
        "package-version.txt",
        "raw-evidence-bundle.json",
        "raw-evidence-immutability-proof.json",
        "release-owner-final-disposition.github.json",
        "release-owner-final-disposition.json",
        "release-owner-publication-authority.checked-at.txt",
        "release-owner-publication-authority.github.json",
        "release-owner-publication-authority.json",
        "source-state-after-publication.txt",
        "source-state-after.txt",
        "source-state-before-publication.txt",
        "source-state-before.txt",
        "story-1-16-followup-review.github.json",
        "story-1-16-followup-review.json",
    ];

    /// <summary>
    /// Verifies the owner-approved v3.94.1 selected packet exists beside the historical fail-closed tree.
    /// </summary>
    [Fact]
    public void SelectedV3941PacketIsPresentAndDoesNotSpliceHistoricalProofBytes()
    {
        string root = FindRepositoryRoot();
        string selectedEvidence = Path.Combine(root, SelectedEvidenceRelativePath);
        Directory.Exists(selectedEvidence).ShouldBeTrue();
        File.Exists(Path.Combine(root, EvidenceRelativePath, "identity-crosswalk.json")).ShouldBeTrue();
        JsonObject selected = JsonNode.Parse(
            File.ReadAllBytes(Path.Combine(selectedEvidence, "identity-crosswalk.json")))!.AsObject();
        selected["approved_identity"]!["source_sha"]!.GetValue<string>().ShouldBe(SelectedSourceSha);
        selected["approved_identity"]!["package_version"]!.GetValue<string>().ShouldBe(SelectedPackageVersion);
        selected["selected_candidates"]![0]!["oci"]!["index_digest"]!.GetValue<string>()
            .ShouldBe(SelectedIndexDigest);
        selected["selected_candidates"]![0]!["packages"]!["byte_verification"]!["recovered_count"]!
            .GetValue<int>().ShouldBe(14);
        selected["verdict"]!["story_may_be_done"]!.GetValue<bool>().ShouldBeFalse();
        selected["approval_contract"]!["receipt_count"]!.GetValue<int>().ShouldBe(0);
        ValidatePackageBytes(selected["selected_candidates"]![0]!.AsObject(), selectedEvidence).ShouldBeTrue();
        string[] spliceIds = selected["prohibited_splices"]!.AsArray()
            .Select(item => item!["splice_id"]!.GetValue<string>()).ToArray();
        spliceIds.ShouldContain("story-1-20-source-packages-plus-v3.94.1-release-index");
        spliceIds.ShouldContain("v3.94.1-source-packages-plus-story-1-20-proof-index");
    }

    /// <summary>
    /// Verifies .gitignore last patterns re-include evidence packages archives and still exclude generic nupkgs.
    /// </summary>
    [Fact]
    public void GitignoreLastPatternsReincludeEvidencePackageArchivesAndExcludeGenericNupkgs()
    {
        string root = FindRepositoryRoot();
        string[] patterns = ReadSignificantGitignorePatterns(root);
        patterns.Length.ShouldBeGreaterThanOrEqualTo(4);
        patterns[^2].ShouldBe("!_bmad-output/**/evidence/**/packages/");
        patterns[^1].ShouldBe("!_bmad-output/**/evidence/**/packages/*.nupkg");
        patterns.ShouldContain("*.nupkg");
        patterns.ShouldContain("!_bmad-output/**/evidence/**/[Ll]ogs/");
        patterns.ShouldContain("!_bmad-output/**/evidence/**/*.log");
        patterns.ShouldNotContain(pattern =>
            pattern.StartsWith('!') && pattern.Contains("/logs/**", StringComparison.OrdinalIgnoreCase));

        string evidenceArchive = Path.Combine(
            SelectedEvidenceRelativePath,
            "packages",
            "Hexalith.EventStore.Contracts.3.94.1.nupkg")
            .Replace('\\', '/');
        IsIgnoredByGit(root, evidenceArchive).ShouldBeFalse();
        IsIgnoredByGit(root, "Hexalith.EventStore.Contracts.3.94.1.nupkg").ShouldBeTrue();
        IsIgnoredByGit(root, "packages/Hexalith.EventStore.Contracts.3.94.1.nupkg").ShouldBeTrue();
        IsIgnoredByGit(root, "logs/Hexalith.EventStore.Contracts.3.94.1.nupkg").ShouldBeTrue();
        IsIgnoredByGit(
            root,
            "_bmad-output/implementation-artifacts/evidence/story-3-13/logs/build.nupkg")
            .ShouldBeTrue();

        string selectedEvidence = Path.Combine(root, SelectedEvidenceRelativePath);
        string[] requiredArchives = JsonNode.Parse(
                File.ReadAllBytes(Path.Combine(selectedEvidence, "identity-crosswalk.json")))!
            .AsObject()["selected_candidates"]![0]!["packages"]!["items"]!.AsArray()
            .Select(item => item!["archive"]!.GetValue<string>())
            .Order(StringComparer.Ordinal)
            .ToArray();
        requiredArchives.Length.ShouldBe(14);

        string[] manifestArchives = File.ReadAllLines(Path.Combine(selectedEvidence, "evidence-core-sha256.txt"))
            .Select(static line => line.Trim())
            .Select(static line =>
            {
                const string marker = "  packages/";
                int markerIndex = line.IndexOf(marker, StringComparison.Ordinal);
                return markerIndex < 0 ? null : line[(markerIndex + "  ".Length)..];
            })
            .Where(static relative => relative is not null && relative.EndsWith(".nupkg", StringComparison.Ordinal))
            .Select(static relative => Path.GetFileName(relative)!)
            .Order(StringComparer.Ordinal)
            .ToArray();
        manifestArchives.ShouldBe(requiredArchives);

        string packagesRelative = (SelectedEvidenceRelativePath + "/packages").Replace('\\', '/');
        string[] trackedArchives = RunGit(root, "ls-files", "--", packagesRelative)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(static path => Path.GetFileName(path.Replace('\\', '/')))
            .Where(static name => !string.IsNullOrEmpty(name))
            .Order(StringComparer.Ordinal)
            .ToArray();
        trackedArchives.ShouldBe(requiredArchives);
    }

    /// <summary>
    /// Verifies exact package identity tuples and every retained archive hash against frozen inputs.
    /// </summary>
    [Fact]
    public void PackageIdentityTuplesAndRetainedHashesAreExact()
    {
        string root = FindRepositoryRoot();
        string evidence = Path.Combine(root, EvidenceRelativePath);
        JsonObject crosswalk = LoadCrosswalk(root);

        ValidatePackages(crosswalk, root, evidence, ApprovedPackageManifestSha256).ShouldBeTrue();

        JsonObject mutated = Clone(crosswalk);
        mutated["selected_candidates"]![0]!["packages"]!["items"]![0]!["project"] = "src/wrong.csproj";
        ValidatePackages(mutated, root, evidence, ApprovedPackageManifestSha256).ShouldBeFalse();
        mutated = Clone(crosswalk);
        mutated["selected_candidates"]![0]!["packages"]!["items"]![0]!["archive"] = "wrong.nupkg";
        ValidatePackages(mutated, root, evidence, ApprovedPackageManifestSha256).ShouldBeFalse();
        mutated = Clone(crosswalk);
        mutated["selected_candidates"]![0]!["packages"]!["items"]![0]!["sha256"] = new string('0', 64);
        ValidatePackages(mutated, root, evidence, ApprovedPackageManifestSha256).ShouldBeFalse();
        mutated = Clone(crosswalk);
        mutated["approved_identity"]!["package_hash_manifest_sha256"] = new string('0', 64);
        ValidatePackages(mutated, root, evidence, ApprovedPackageManifestSha256).ShouldBeFalse();
    }

    /// <summary>
    /// Verifies exact sorted unique evidence sets and immutable predecessor fingerprints.
    /// </summary>
    [Fact]
    public void EvidenceAndPredecessorManifestsAreExactAndReproducible()
    {
        string root = FindRepositoryRoot();
        string evidence = Path.Combine(root, EvidenceRelativePath);
        string predecessorPrefix =
            "_bmad-output/implementation-artifacts/evidence/story-1-20/" + ApprovedSourceSha + "/";
        string[] expectedPredecessorPaths = ExpectedPredecessorFiles
            .Select(path => predecessorPrefix + path)
            .ToArray();

        VerifyChecksumManifest(
            File.ReadAllBytes(Path.Combine(evidence, "evidence-core-sha256.txt")),
            evidence,
            ExpectedCoreFiles).ShouldBeTrue();
        VerifyChecksumManifest(
            File.ReadAllBytes(Path.Combine(evidence, "evidence-sha256.txt")),
            evidence,
            ExpectedOuterFiles).ShouldBeTrue();
        VerifyChecksumManifest(
            File.ReadAllBytes(Path.Combine(evidence, "predecessor-tree-sha256.txt")),
            root,
            expectedPredecessorPaths).ShouldBeTrue();
        byte[] unsortedCore = Encoding.UTF8.GetBytes(string.Join(
            '\n',
            File.ReadAllLines(Path.Combine(evidence, "evidence-core-sha256.txt")).Reverse()) + "\n");
        VerifyChecksumManifest(unsortedCore, evidence, ExpectedCoreFiles).ShouldBeFalse();

        ComputeSha256(Path.Combine(evidence, "predecessor-tree-sha256.txt"))
            .ShouldBe("d76d44291bccce0dbea384d2bf8c0258c6ba847dc4bdfa5150d881f4f5eae092");
        RunGit(root, "rev-parse", ExpectedBaselineCommit + ":" + predecessorPrefix.TrimEnd('/'))
            .ShouldBe("fcd0c25c9cf6bb0554e208d529f1ef09c223725a");
        ComputeSha256(Path.Combine(
            root,
            "_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-and-runtime-pin.md"))
            .ShouldBe("0feee912874154a3885fbe69ac68419c89b209b8c9c5b9291833604881f34fa5");
        ComputeSha256(Path.Combine(
            root,
            "_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md"))
            .ShouldBe("cb1ccde9d5cc5ca6cb52cbeab30fb9cd59bd89771e14f4b489e20bd5e3d46743");
        ComputeSha256(Path.Combine(
            root,
            "_bmad-output/implementation-artifacts/3-12-multi-platform-eventstore-container-publishing-correction.md"))
            .ShouldBe("2bfc9ff991c9aeeaf11fd9c1926a17bb44ca290f99bd75b05df68a6edaf3e09c");
    }

    /// <summary>
    /// Verifies malformed, incomplete, duplicate, and unsafe checksum manifests fail closed.
    /// </summary>
    /// <param name="manifest">The malformed manifest content.</param>
    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa  index.raw\n" +
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa  index.raw\n")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa  ../index.raw\n")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa  /tmp/index.raw\n")]
    public void MalformedChecksumManifestsFailClosed(string manifest)
    {
        string evidence = Path.Combine(FindRepositoryRoot(), EvidenceRelativePath);
        VerifyChecksumManifest(Encoding.UTF8.GetBytes(manifest), evidence, ["index.raw"]).ShouldBeFalse();
    }

    /// <summary>
    /// Verifies a well-formed checksum entry whose digest does not match the file bytes fails closed.
    /// </summary>
    [Fact]
    public void ChecksumManifestRejectsMismatchedHashForExistingFile()
    {
        string evidence = Path.Combine(FindRepositoryRoot(), EvidenceRelativePath);
        string mismatched = new string('a', 64) + "  index.raw\n";
        VerifyChecksumManifest(Encoding.UTF8.GetBytes(mismatched), evidence, ["index.raw"]).ShouldBeFalse();
    }

    /// <summary>
    /// Verifies evidence paths cannot escape their root through a symbolic link.
    /// </summary>
    [Fact]
    public void EvidencePathsRejectSymlinkEscapes()
    {
        string fixtureRoot = Path.Combine(Path.GetTempPath(), "story-3-13-link-" + Guid.NewGuid().ToString("N"));
        string evidence = Path.Combine(fixtureRoot, "evidence");
        string external = Path.Combine(fixtureRoot, "outside.txt");
        string link = Path.Combine(evidence, "linked.txt");
        Directory.CreateDirectory(evidence);
        File.WriteAllText(external, "outside evidence root");
        try
        {
            try
            {
                File.CreateSymbolicLink(link, external);
            }
            catch (Exception exception) when (
                exception is PlatformNotSupportedException or UnauthorizedAccessException or IOException)
            {
                Assert.Skip("Symbolic links are unavailable in this environment: " + exception.GetType().Name);
            }

            Should.Throw<InvalidDataException>(() => ResolveWithin(evidence, "linked.txt"));
        }
        finally
        {
            Directory.Delete(fixtureRoot, recursive: true);
        }
    }

    /// <summary>
    /// Verifies missing child and config response metadata keeps the retained OCI graph fail-closed.
    /// </summary>
    [Fact]
    public void MissingObjectResponseMetadataFailsOciGraphClosed()
    {
        string root = FindRepositoryRoot();
        string evidence = Path.Combine(root, EvidenceRelativePath);
        JsonObject crosswalk = LoadCrosswalk(root);

        ValidateOciGraph(crosswalk, root, evidence).ShouldBeFalse();

        JsonObject mutated = Clone(crosswalk);
        mutated["selected_candidates"]![0]!["oci"]!["children"]![0]!["manifest_raw_file"] =
            "child-linux-arm64.manifest.raw";
        ValidateOciGraph(mutated, root, evidence).ShouldBeFalse();
        mutated = Clone(crosswalk);
        mutated["selected_candidates"]![0]!["oci"]!["index_raw_file"] = "../index.raw";
        ValidateOciGraph(mutated, root, evidence).ShouldBeFalse();
        mutated = Clone(crosswalk);
        mutated["selected_candidates"]![0]!["oci"]!["tag_response_raw_file"] = "/tmp/index.raw";
        ValidateOciGraph(mutated, root, evidence).ShouldBeFalse();
    }

    /// <summary>
    /// Verifies the retained malformed provenance labels are an explicit closure blocker.
    /// </summary>
    [Fact]
    public void MalformedOciProvenanceLabelsFailClosed()
    {
        string root = FindRepositoryRoot();
        JsonObject crosswalk = LoadCrosswalk(root);
        JsonObject provenance = crosswalk["selected_candidates"]![0]!["oci"]!["provenance_labels"]!.AsObject();

        provenance["org.opencontainers.image.source"]!.GetValue<string>().ShouldBe("https");
        provenance["org.opencontainers.image.url"]!.GetValue<string>().ShouldBe("https");
        provenance["org.opencontainers.image.documentation"]!.GetValue<string>().ShouldBe("https");
        provenance["verification"]!["result"]!.GetValue<string>().ShouldBe("fail");
        ValidateOciProvenance(crosswalk, Path.Combine(root, EvidenceRelativePath)).ShouldBeFalse();
    }

    /// <summary>
    /// Verifies incomplete runtime records cannot prove executed liveness or Production equivalence.
    /// </summary>
    [Fact]
    public void IncompleteRuntimeEvidenceFailsClosed()
    {
        string root = FindRepositoryRoot();
        string evidence = Path.Combine(root, EvidenceRelativePath);
        JsonObject crosswalk = LoadCrosswalk(root);

        ValidateRuntimeExecution(crosswalk, root, evidence).ShouldBeFalse();
        ValidateRuntimeEquivalence(crosswalk).ShouldBeFalse();
        JsonObject runtime = crosswalk["selected_candidates"]![0]!["runtime"]!.AsObject();
        runtime["contract"]!["actual_hosting_environment"]!.GetValue<string>().ShouldBe("Development");
        runtime["contract"]!["required_hosting_environment"]!.GetValue<string>().ShouldBe("Production");
        DateTimeOffset started = DateTimeOffset.Parse(
            runtime["started_at"]!.GetValue<string>(),
            CultureInfo.InvariantCulture);
        DateTimeOffset ended = DateTimeOffset.Parse(
            runtime["ended_at"]!.GetValue<string>(),
            CultureInfo.InvariantCulture);
        JsonObject contract = runtime["contract"]!.AsObject();
        foreach (JsonObject platform in runtime["platforms"]!.AsArray().Select(item => item!.AsObject()))
        {
            bool structuredLogAccepted;
            try
            {
                structuredLogAccepted = ValidateRuntimeLog(evidence, platform, contract, started, ended);
            }
            catch (Exception exception) when (
                exception is JsonException
                or InvalidOperationException
                or NullReferenceException
                or ArgumentException
                or FormatException
                or OverflowException
                or InvalidDataException
                or IOException)
            {
                structuredLogAccepted = false;
            }

            structuredLogAccepted.ShouldBeFalse(
                "Retained fail-closed smoke logs must not satisfy ValidateRuntimeLog.");
        }

        JsonObject oci = crosswalk["selected_candidates"]![0]!["oci"]!.AsObject();
        JsonObject preflight = runtime["preflight"]!.AsObject();
        bool structuredPreflightAccepted;
        try
        {
            structuredPreflightAccepted = ValidatePreflightLog(evidence, preflight, oci, started, ended);
        }
        catch (Exception exception) when (
            exception is JsonException
            or InvalidOperationException
            or NullReferenceException
            or ArgumentException
            or FormatException
            or OverflowException
            or InvalidDataException
            or IOException)
        {
            structuredPreflightAccepted = false;
        }

        structuredPreflightAccepted.ShouldBeFalse(
            "Retained fail-closed smoke-preflight.log must not satisfy ValidatePreflightLog.");

        JsonObject mutated = Clone(crosswalk);
        mutated["selected_candidates"]![0]!["runtime"]!["platforms"]![0]!["child_digest"] =
            "sha256:" + new string('0', 64);
        ValidateRuntimeExecution(mutated, root, evidence).ShouldBeFalse();
        mutated = Clone(crosswalk);
        mutated["selected_candidates"]![0]!["runtime"]!["platforms"]![0]!["log"] = "../smoke.log";
        ValidateRuntimeExecution(mutated, root, evidence).ShouldBeFalse();
    }

    /// <summary>
    /// Verifies nested credential-shaped fields cannot be retained as support-safe runtime evidence.
    /// </summary>
    [Fact]
    public void SupportSafeRuntimeRecordsRejectSensitiveFieldNames()
    {
        LogIsSupportSafe(JsonSerializer.SerializeToUtf8Bytes(new JsonObject
        {
            ["result"] = "pass",
            ["access_token"] = "redacted-but-forbidden",
        })).ShouldBeFalse();
        LogIsSupportSafe(JsonSerializer.SerializeToUtf8Bytes(new JsonObject
        {
            ["result"] = "pass",
            ["nested"] = new JsonObject { ["client-secret"] = "redacted-but-forbidden" },
        })).ShouldBeFalse();
        LogIsSupportSafe(JsonSerializer.SerializeToUtf8Bytes(new JsonObject
        {
            ["result"] = "pass",
            ["private_key"] = "redacted-but-forbidden",
        })).ShouldBeFalse();
        LogIsSupportSafe(JsonSerializer.SerializeToUtf8Bytes(new JsonObject
        {
            ["result"] = "pass",
            ["private-key"] = "redacted-but-forbidden",
        })).ShouldBeFalse();
        LogIsSupportSafe(JsonSerializer.SerializeToUtf8Bytes(new JsonObject
        {
            ["result"] = "pass",
            ["privatekey"] = "redacted-but-forbidden",
        })).ShouldBeFalse();
    }

    /// <summary>
    /// Verifies private addresses and unapproved URI hosts cannot be retained as support-safe evidence values.
    /// </summary>
    [Fact]
    public void SupportSafeValuesRejectPrivateIpv6Addresses()
    {
        ValueIsSupportSafe("fd00::1").ShouldBeFalse();
        ValueIsSupportSafe("fc00::abcd").ShouldBeFalse();
        ValueIsSupportSafe("fe80::1").ShouldBeFalse();
        ValueIsSupportSafe("::192.168.1.10").ShouldBeFalse();
        ValueIsSupportSafe("::ffff:10.0.0.1").ShouldBeFalse();
        ValueIsSupportSafe("fec0::1").ShouldBeFalse();
        ValueIsSupportSafe("https://[fd00::1]/status").ShouldBeFalse();
        ValueIsSupportSafe("2606:4700:4700::1111").ShouldBeTrue();
        ValueIsSupportSafe("0.0.0.0").ShouldBeFalse();
        ValueIsSupportSafe("::").ShouldBeFalse();
        ValueIsSupportSafe("http://0.0.0.0/alive").ShouldBeFalse();
        ValueIsSupportSafe("https://[::]/status").ShouldBeFalse();
        ValueIsSupportSafe("http://nas.local/alive").ShouldBeFalse();
        ValueIsSupportSafe("https://svc.internal/status").ShouldBeFalse();
        ValueIsSupportSafe("https://fileserver.corp/path").ShouldBeFalse();
        ValueIsSupportSafe("http://printer.lan/").ShouldBeFalse();
        ValueIsSupportSafe("https://apparently-public.example/status").ShouldBeFalse();
        ValueIsSupportSafe("https://8.8.8.8/status").ShouldBeFalse();
        ValueIsSupportSafe("https://github.com/Hexalith/Hexalith.EventStore").ShouldBeTrue();
        ValueIsSupportSafe("https://registry.hexalith.com/eventstore").ShouldBeTrue();
        ValueIsSupportSafe("10.0.0.1").ShouldBeFalse();
        ValueIsSupportSafe("100.64.0.1").ShouldBeFalse();
        ValueIsSupportSafe("100.127.255.254").ShouldBeFalse();
        ValueIsSupportSafe("100.63.255.255").ShouldBeTrue();
        ValueIsSupportSafe("100.128.0.1").ShouldBeTrue();
        ValueIsSupportSafe("8.8.8.8").ShouldBeTrue();
        ValueIsSupportSafe("-----BEGIN EC PRIVATE KEY-----\nabc\n-----END EC PRIVATE KEY-----")
            .ShouldBeFalse();
        ValueIsSupportSafe("-----BEGIN OPENSSH PRIVATE KEY-----\nabc\n-----END OPENSSH PRIVATE KEY-----")
            .ShouldBeFalse();
        ValueIsSupportSafe(
                "-----BEGIN ENCRYPTED PRIVATE KEY-----\nabc\n-----END ENCRYPTED PRIVATE KEY-----")
            .ShouldBeFalse();
        ValueIsSupportSafe("-----BEGIN DSA PRIVATE KEY-----\nabc\n-----END DSA PRIVATE KEY-----")
            .ShouldBeFalse();
    }

    /// <summary>
    /// Verifies re-running the smoke against an unchanged artifact preserves the canonical lineage.
    /// </summary>
    [Fact]
    public void CanonicalLineageIgnoresExecutionOnlyRuntimeFacts()
    {
        string root = FindRepositoryRoot();
        (string cleanupRoot, _, JsonObject crosswalk, _, _, _, _, _) = CreatePassingFixture(root);
        try
        {
            JsonObject candidate = crosswalk["selected_candidates"]![0]!.AsObject();
            string before = ComputeLineageMaterialSha256(candidate);
            JsonObject runtime = candidate["runtime"]!.AsObject();
            runtime["started_at"] = "2030-01-01T00:00:00.0000000+00:00";
            runtime["ended_at"] = "2030-01-01T00:02:00.0000000+00:00";
            runtime["platforms"]![0]!["attempts"] = 47;
            runtime["platforms"]![0]!["started_at"] = "2030-01-01T00:00:20.0000000+00:00";
            runtime["platforms"]![0]!["log_sha256"] = new string('9', 64);
            runtime["preflight"]!["log_sha256"] = new string('8', 64);
            runtime["smoke_results"]!["sha256"] = new string('7', 64);
            ComputeLineageMaterialSha256(candidate).ShouldBe(before);

            runtime["platforms"]![0]!["child_digest"] = "sha256:" + new string('0', 64);
            ComputeLineageMaterialSha256(candidate).ShouldNotBe(before);

            string afterDigest = ComputeLineageMaterialSha256(candidate);
            runtime["platforms"]![0]!["outcome"] = "fail";
            ComputeLineageMaterialSha256(candidate).ShouldNotBe(afterDigest);

            string afterOutcome = ComputeLineageMaterialSha256(candidate);
            runtime["platforms"]![0]!["readiness_result"] = "fail";
            ComputeLineageMaterialSha256(candidate).ShouldNotBe(afterOutcome);
        }
        finally
        {
            DeleteTemporaryDirectory(cleanupRoot);
        }
    }

    /// <summary>
    /// Verifies actual evidence stays fail-closed even if its declared verdict is tampered to pass.
    /// </summary>
    [Fact]
    public void DerivedClosureRejectsActualIncompleteLineageAndDeclarativeTampering()
    {
        string root = FindRepositoryRoot();
        string liveEvidence = Path.Combine(root, EvidenceRelativePath);
        string cleanupRoot = Path.Combine(Path.GetTempPath(), "story-3-13-derived-" + Guid.NewGuid().ToString("N"));
        string evidence = Path.Combine(cleanupRoot, ApprovedSourceSha, ExpectedIndexDigest[7..]);
        CopyDirectory(liveEvidence, evidence);
        try
        {
            JsonObject crosswalk = LoadCrosswalk(root);
            byte[] crosswalkBytes = File.ReadAllBytes(Path.Combine(evidence, "identity-crosswalk.json"));
            byte[] subjectBytes = File.ReadAllBytes(Path.Combine(evidence, "review-subject.json"));
            byte[] coreBytes = File.ReadAllBytes(Path.Combine(evidence, "evidence-core-sha256.txt"));
            byte[] proofBytes = File.ReadAllBytes(Path.Combine(root, ProofRelativePath));
            byte[] outerBytes = File.ReadAllBytes(Path.Combine(evidence, "evidence-sha256.txt"));

            ValidateActualFailClosedSubject(
                crosswalk,
                crosswalkBytes,
                subjectBytes,
                coreBytes,
                proofBytes,
                outerBytes,
                evidence).ShouldBeTrue();

            string smokePath = Path.Combine(evidence, "smoke-results.json");
            JsonObject dishonestSmoke = JsonNode.Parse(File.ReadAllBytes(smokePath))!.AsObject();
            dishonestSmoke["result"] = "pass";
            File.WriteAllBytes(smokePath, JsonSerializer.SerializeToUtf8Bytes(dishonestSmoke));
            ValidateActualFailClosedSubject(
                crosswalk,
                crosswalkBytes,
                subjectBytes,
                coreBytes,
                proofBytes,
                outerBytes,
                evidence).ShouldBeFalse();
            // Restore honest smoke so later mutations are independent.
            File.WriteAllBytes(
                smokePath,
                File.ReadAllBytes(Path.Combine(liveEvidence, "smoke-results.json")));

            string packageAvailabilityPath = Path.Combine(evidence, "package-availability.json");
            JsonObject recoveredPackages = new()
            {
                ["schema"] = "hexalith.eventstore.story-3-13-package-availability/v2",
                ["checked_at"] = "2026-08-04T11:17:05Z",
                ["package_version"] = ApprovedPackageVersion,
                ["expected_count"] = 14,
                ["recovered_count"] = 14,
                ["archive_root"] = "packages",
                ["result"] = "pass",
            };
            File.WriteAllBytes(
                packageAvailabilityPath,
                JsonSerializer.SerializeToUtf8Bytes(recoveredPackages));
            ValidateActualFailClosedSubject(
                crosswalk,
                crosswalkBytes,
                subjectBytes,
                coreBytes,
                proofBytes,
                outerBytes,
                evidence).ShouldBeFalse();
            File.WriteAllBytes(
                packageAvailabilityPath,
                File.ReadAllBytes(Path.Combine(liveEvidence, "package-availability.json")));

            string runtimeVerificationPath = Path.Combine(evidence, "runtime-verification.json");
            JsonObject driftedCitation = JsonNode.Parse(File.ReadAllBytes(runtimeVerificationPath))!.AsObject();
            driftedCitation["contract"]!["actual_hosting_environment"] = "Production";
            File.WriteAllBytes(
                runtimeVerificationPath,
                JsonSerializer.SerializeToUtf8Bytes(driftedCitation));
            ValidateActualFailClosedSubject(
                crosswalk,
                crosswalkBytes,
                subjectBytes,
                coreBytes,
                proofBytes,
                outerBytes,
                evidence).ShouldBeFalse();
            File.WriteAllBytes(
                runtimeVerificationPath,
                File.ReadAllBytes(Path.Combine(liveEvidence, "runtime-verification.json")));

            string registryPath = Path.Combine(evidence, "registry-readback.json");
            JsonObject dishonestRegistry = JsonNode.Parse(File.ReadAllBytes(registryPath))!.AsObject();
            dishonestRegistry["shared_validator"]!["cli_candidate_consequence"] =
                "Weakened SemVer validation accepted the quarantine tag.";
            File.WriteAllBytes(registryPath, JsonSerializer.SerializeToUtf8Bytes(dishonestRegistry));
            ValidateActualFailClosedSubject(
                crosswalk,
                crosswalkBytes,
                subjectBytes,
                coreBytes,
                proofBytes,
                outerBytes,
                evidence).ShouldBeFalse();
            File.WriteAllBytes(
                registryPath,
                File.ReadAllBytes(Path.Combine(liveEvidence, "registry-readback.json")));

            EvaluateClosure(crosswalk, crosswalkBytes, subjectBytes, root, evidence, coreBytes, proofBytes)
                .ShouldBeFalse();

            JsonObject tampered = Clone(crosswalk);
            JsonObject verdict = tampered["verdict"]!.AsObject();
            verdict["decision"] = "pass";
            verdict["story_may_be_done"] = true;
            verdict["blockers"] = new JsonArray();
            foreach ((string key, _) in verdict["checks"]!.AsObject())
            {
                verdict["checks"]![key] = "pass";
            }

            byte[] tamperedBytes = JsonSerializer.SerializeToUtf8Bytes(tampered);
            EvaluateClosure(tampered, tamperedBytes, subjectBytes, root, evidence, coreBytes, proofBytes)
                .ShouldBeFalse();

            JsonObject tamperedSubject = JsonNode.Parse(subjectBytes)!.AsObject();
            tamperedSubject["proposed_decision"] = "pass";
            ValidateActualFailClosedSubject(
                crosswalk,
                crosswalkBytes,
                JsonSerializer.SerializeToUtf8Bytes(tamperedSubject),
                coreBytes,
                proofBytes,
                outerBytes,
                evidence).ShouldBeFalse();

            JsonObject dishonestChecks = Clone(crosswalk["verdict"]!["checks"]!.AsObject());
            dishonestChecks["runtime_both_platforms"] = "pass";
            ValidateFailClosedVerdictChecks(dishonestChecks).ShouldBeFalse();
            dishonestChecks = Clone(crosswalk["verdict"]!["checks"]!.AsObject());
            dishonestChecks["oci_graph"] = "pass";
            ValidateFailClosedVerdictChecks(dishonestChecks).ShouldBeFalse();
            dishonestChecks = Clone(crosswalk["verdict"]!["checks"]!.AsObject());
            dishonestChecks["deployment_authority"] = "pass";
            ValidateFailClosedVerdictChecks(dishonestChecks).ShouldBeFalse();

            byte[] tamperedOuter = Encoding.UTF8.GetBytes(
                Encoding.UTF8.GetString(outerBytes).Replace(
                    ComputeSha256(subjectBytes),
                    new string('0', 64),
                    StringComparison.Ordinal));
            ValidateActualFailClosedSubject(
                crosswalk,
                crosswalkBytes,
                subjectBytes,
                coreBytes,
                proofBytes,
                tamperedOuter,
                evidence).ShouldBeFalse();
        }
        finally
        {
            if (Directory.Exists(cleanupRoot))
            {
                DeleteTemporaryDirectory(cleanupRoot);
            }
        }
    }

    /// <summary>
    /// Verifies a synthetic complete lineage passes only when every derived fact is present.
    /// </summary>
    [Fact]
    public void CompleteDerivedLineagePassesAndMissingChecksOrBlockersFail()
    {
        string root = FindRepositoryRoot();
        (string cleanupRoot, string evidence, JsonObject crosswalk, byte[] crosswalkBytes, byte[] subjectBytes,
            byte[] coreBytes, byte[] proofBytes, string packageManifestSha256) = CreatePassingFixture(root);
        try
        {
            ValidateBaselineAndPredecessors(crosswalk, root, evidence, packageManifestSha256).ShouldBeTrue();
            ValidateEvidenceIntegrity(crosswalk, root, evidence, coreBytes, subjectBytes).ShouldBeTrue();
            ValidatePackages(crosswalk, root, evidence, packageManifestSha256).ShouldBeTrue();
            ValidatePackageBytes(crosswalk["selected_candidates"]![0]!.AsObject(), evidence).ShouldBeTrue();
            ValidateRelease(crosswalk["selected_candidates"]![0]!.AsObject(), root, evidence).ShouldBeTrue();
            ValidateOciGraph(crosswalk, root, evidence).ShouldBeTrue();
            ValidateOciProvenance(crosswalk, evidence).ShouldBeTrue();
            ValidateRuntimeExecution(crosswalk, root, evidence).ShouldBeTrue();
            ValidateRuntimeEquivalence(crosswalk).ShouldBeTrue();
            ValidateDeploymentAuthority(crosswalk, root, evidence).ShouldBeTrue();
            LoadReviewerRoster(
                crosswalk,
                evidence,
                DateTimeOffset.Parse(
                    JsonNode.Parse(subjectBytes)!["created_at"]!.GetValue<string>(),
                    CultureInfo.InvariantCulture)).ShouldNotBeNull();
            ValidateReviewSubject(
                crosswalk,
                JsonNode.Parse(subjectBytes)!.AsObject(),
                crosswalkBytes,
                coreBytes,
                proofBytes,
                evidence).ShouldBeTrue();
            ValidateAcceptances(
                crosswalk,
                crosswalkBytes,
                subjectBytes,
                root,
                evidence,
                coreBytes,
                proofBytes).ShouldBeTrue();
            EvaluateClosure(
                crosswalk,
                crosswalkBytes,
                subjectBytes,
                root,
                evidence,
                coreBytes,
                proofBytes,
                packageManifestSha256).ShouldBeTrue();

            JsonObject mutated = Clone(crosswalk);
            mutated["verdict"]!["checks"]!.AsObject().Remove("oci_graph");
            EvaluateClosure(
                mutated,
                JsonSerializer.SerializeToUtf8Bytes(mutated),
                subjectBytes,
                root,
                evidence,
                coreBytes,
                proofBytes,
                packageManifestSha256).ShouldBeFalse();

            mutated = Clone(crosswalk);
            mutated["selected_candidates"]![0]!["release"]!.AsObject().Remove("publisher_identity");
            EvaluateClosure(
                mutated,
                JsonSerializer.SerializeToUtf8Bytes(mutated),
                subjectBytes,
                root,
                evidence,
                coreBytes,
                proofBytes,
                packageManifestSha256).ShouldBeFalse();

            mutated = Clone(crosswalk);
            mutated["verdict"]!["checks"]!["extra"] = "pass";
            EvaluateClosure(
                mutated,
                JsonSerializer.SerializeToUtf8Bytes(mutated),
                subjectBytes,
                root,
                evidence,
                coreBytes,
                proofBytes,
                packageManifestSha256).ShouldBeFalse();

            mutated = Clone(crosswalk);
            mutated["verdict"]!["checks"]!["oci_graph"] = "";
            EvaluateClosure(
                mutated,
                JsonSerializer.SerializeToUtf8Bytes(mutated),
                subjectBytes,
                root,
                evidence,
                coreBytes,
                proofBytes,
                packageManifestSha256).ShouldBeFalse();

            mutated = Clone(crosswalk);
            mutated["verdict"]!["blockers"] = new JsonArray(new JsonObject { ["id"] = "hidden" });
            EvaluateClosure(
                mutated,
                JsonSerializer.SerializeToUtf8Bytes(mutated),
                subjectBytes,
                root,
                evidence,
                coreBytes,
                proofBytes,
                packageManifestSha256).ShouldBeFalse();
        }
        finally
        {
            DeleteTemporaryDirectory(cleanupRoot);
        }
    }

    /// <summary>
    /// Verifies deployment authority was valid at the retained deployment action time.
    /// </summary>
    [Fact]
    public void DeploymentAuthorityMustCoverRecordedActionTime()
    {
        string root = FindRepositoryRoot();
        (string cleanupRoot, string evidence, JsonObject crosswalk, _, _, _, _, _) = CreatePassingFixture(root);
        try
        {
            string authorityPath = Path.Combine(evidence, "deployment-authority.json");
            JsonObject authorityRecord = JsonNode.Parse(File.ReadAllBytes(authorityPath))!.AsObject();
            JsonObject releaseEvidence = JsonNode.Parse(
                File.ReadAllBytes(Path.Combine(evidence, "release-provenance.json")))!.AsObject();
            DateTimeOffset actionAt = DateTimeOffset.Parse(
                releaseEvidence["deployment_action_at"]!.GetValue<string>(),
                CultureInfo.InvariantCulture);
            authorityRecord["expires_at"] = actionAt.AddSeconds(-1).ToString("O");
            byte[] invalidAuthorityBytes = JsonSerializer.SerializeToUtf8Bytes(authorityRecord);
            File.WriteAllBytes(authorityPath, invalidAuthorityBytes);
            crosswalk["selected_candidates"]![0]!["release_authority"]!["record_sha256"] =
                ComputeSha256(invalidAuthorityBytes);

            ValidateDeploymentAuthority(crosswalk, root, evidence).ShouldBeFalse();
        }
        finally
        {
            DeleteTemporaryDirectory(cleanupRoot);
        }
    }

    /// <summary>
    /// Verifies every external receipt field is mandatory.
    /// </summary>
    /// <param name="missingField">The field removed from one receipt.</param>
    [Theory]
    [MemberData(nameof(ReceiptFieldNames))]
    public void ExternalAcceptanceReceiptsRequireEveryField(string missingField)
    {
        string root = FindRepositoryRoot();
        (string cleanupRoot, string evidence, JsonObject crosswalk, byte[] crosswalkBytes, byte[] subjectBytes,
            byte[] coreBytes, byte[] proofBytes, string packageManifestSha256) = CreatePassingFixture(root);
        try
        {
            RemoveReceiptField(evidence, subjectBytes, RequiredRoles[0], missingField);
            EvaluateClosure(
                crosswalk,
                crosswalkBytes,
                subjectBytes,
                root,
                evidence,
                coreBytes,
                proofBytes,
                packageManifestSha256).ShouldBeFalse();
        }
        finally
        {
            DeleteTemporaryDirectory(cleanupRoot);
        }
    }

    /// <summary>
    /// Verifies the review subject binds raw crosswalk, core-manifest, and proof bytes.
    /// </summary>
    /// <param name="binding">The binding whose raw bytes are mutated.</param>
    [Theory]
    [InlineData("crosswalk")]
    [InlineData("core")]
    [InlineData("proof")]
    public void ReviewSubjectRejectsEveryStaleRawBinding(string binding)
    {
        string root = FindRepositoryRoot();
        (string cleanupRoot, string evidence, JsonObject crosswalk, byte[] crosswalkBytes, byte[] subjectBytes,
            byte[] coreBytes, byte[] proofBytes, string packageManifestSha256) = CreatePassingFixture(root);
        try
        {
            if (binding == "crosswalk")
            {
                crosswalkBytes = [.. crosswalkBytes, (byte)' '];
            }
            else if (binding == "core")
            {
                coreBytes = [.. coreBytes, (byte)' '];
            }
            else
            {
                proofBytes = [.. proofBytes, (byte)' '];
            }

            EvaluateClosure(
                crosswalk,
                crosswalkBytes,
                subjectBytes,
                root,
                evidence,
                coreBytes,
                proofBytes,
                packageManifestSha256).ShouldBeFalse();
        }
        finally
        {
            DeleteTemporaryDirectory(cleanupRoot);
        }
    }

    /// <summary>
    /// Verifies stale receipt subjects and duplicate roles or reviewers fail closed.
    /// </summary>
    [Fact]
    public void ExternalAcceptancesMustBeUniqueAndAcceptOneUnchangedSubject()
    {
        string root = FindRepositoryRoot();
        (string cleanupRoot, string evidence, JsonObject crosswalk, byte[] crosswalkBytes, byte[] subjectBytes,
            byte[] coreBytes, byte[] proofBytes, string packageManifestSha256) = CreatePassingFixture(root);
        try
        {
            MutateReceipt(evidence, subjectBytes, RequiredRoles[0], receipt =>
                receipt["subject_sha256"] = new string('0', 64));
            EvaluateClosure(crosswalk, crosswalkBytes, subjectBytes, root, evidence, coreBytes, proofBytes,
                packageManifestSha256)
                .ShouldBeFalse();

            CreateAcceptanceReceipts(evidence, subjectBytes);
            MutateReceipt(evidence, subjectBytes, RequiredRoles[1], receipt =>
                receipt["role"] = RequiredRoles[0]);
            EvaluateClosure(
                crosswalk,
                crosswalkBytes,
                subjectBytes,
                root,
                evidence,
                coreBytes,
                proofBytes,
                packageManifestSha256).ShouldBeFalse();

            CreateAcceptanceReceipts(evidence, subjectBytes);
            MutateReceipt(evidence, subjectBytes, RequiredRoles[2], receipt =>
                receipt["reviewer_identity"] = "github:unauthorized");
            EvaluateClosure(
                crosswalk,
                crosswalkBytes,
                subjectBytes,
                root,
                evidence,
                coreBytes,
                proofBytes,
                packageManifestSha256).ShouldBeFalse();
        }
        finally
        {
            DeleteTemporaryDirectory(cleanupRoot);
        }
    }

    /// <summary>
    /// Verifies the acceptance tree is an exact set and rejects top-level or nested sidecars.
    /// </summary>
    /// <param name="location">The directory in which the undeclared sidecar is created.</param>
    [Theory]
    [InlineData("top-level")]
    [InlineData("sources")]
    public void ExternalAcceptancesRejectUndeclaredSidecars(string location)
    {
        string root = FindRepositoryRoot();
        (string cleanupRoot, string evidence, JsonObject crosswalk, byte[] crosswalkBytes, byte[] subjectBytes,
            byte[] coreBytes, byte[] proofBytes, string packageManifestSha256) = CreatePassingFixture(root);
        try
        {
            string directory = Path.Combine(evidence, "acceptances", ComputeSha256(subjectBytes));
            string sidecarRoot = location == "sources" ? Path.Combine(directory, "sources") : directory;
            File.WriteAllText(Path.Combine(sidecarRoot, "undeclared.txt"), "not bound");

            EvaluateClosure(
                crosswalk,
                crosswalkBytes,
                subjectBytes,
                root,
                evidence,
                coreBytes,
                proofBytes,
                packageManifestSha256).ShouldBeFalse();
        }
        finally
        {
            DeleteTemporaryDirectory(cleanupRoot);
        }
    }

    /// <summary>
    /// Verifies acceptance receipt files cannot escape their subject-hash directory through a symbolic link.
    /// </summary>
    [Fact]
    public void ExternalAcceptanceReceiptPathsRejectSymlinkEscapes()
    {
        string root = FindRepositoryRoot();
        (string cleanupRoot, string evidence, JsonObject crosswalk, byte[] crosswalkBytes, byte[] subjectBytes,
            byte[] coreBytes, byte[] proofBytes, string packageManifestSha256) = CreatePassingFixture(root);
        try
        {
            string subjectHash = ComputeSha256(subjectBytes);
            string receiptPath = Path.Combine(
                evidence,
                "acceptances",
                subjectHash,
                RequiredRoles[0] + ".json");
            string externalReceiptPath = Path.Combine(cleanupRoot, "external-receipt.json");
            File.Move(receiptPath, externalReceiptPath);
            try
            {
                File.CreateSymbolicLink(receiptPath, externalReceiptPath);
            }
            catch (Exception exception) when (
                exception is PlatformNotSupportedException or UnauthorizedAccessException or IOException)
            {
                Assert.Skip("Symbolic links are unavailable in this environment: " + exception.GetType().Name);
            }

            EvaluateClosure(
                crosswalk,
                crosswalkBytes,
                subjectBytes,
                root,
                evidence,
                coreBytes,
                proofBytes,
                packageManifestSha256).ShouldBeFalse();
        }
        finally
        {
            DeleteTemporaryDirectory(cleanupRoot);
        }
    }

    /// <summary>
    /// Verifies each named cross-lineage splice is explicitly rejected.
    /// </summary>
    /// <param name="spliceId">The prohibited splice identifier.</param>
    [Theory]
    [InlineData("story-1-20-source-packages-plus-v3.77.2-release-index")]
    [InlineData("v3.77.2-source-packages-plus-story-1-20-proof-index")]
    [InlineData("story-1-20-source-packages-plus-v3.94.1-release-index")]
    [InlineData("v3.94.1-source-packages-plus-story-1-20-proof-index")]
    public void ProhibitedCrossLineageSplicesFailClosed(string spliceId)
    {
        string root = FindRepositoryRoot();
        (string cleanupRoot, string evidence, JsonObject crosswalk, _, _, byte[] coreBytes,
            byte[] proofBytes, string packageManifestSha256) = CreatePassingFixture(root);
        try
        {
            JsonObject mutated = Clone(crosswalk);
            JsonObject candidate = mutated["selected_candidates"]![0]!.AsObject();
            const string CorrectiveReleaseSource = "77a9a442c0e6d0408957888e10c3a9accd634c99";
            const string CorrectiveIndexDigest =
                "sha256:db3ab41e187efc0de397fd1205660a0f685e2c94ecd8f4a8f1843ac567056bf6";
            const string SelectedReleaseSource = "80d12ef5eee71a9fe3ea7be51171da4a71b69a28";
            const string SelectedIndexDigest =
                "sha256:ab8784c8c9c67229ee178e9d6dd809df9554b3cdafb43ffb7bfd38c792e2afcd";
            ArgumentException.ThrowIfNullOrWhiteSpace(spliceId);
            if (spliceId is "story-1-20-source-packages-plus-v3.77.2-release-index"
                or "story-1-20-source-packages-plus-v3.94.1-release-index")
            {
                bool useSelectedRelease = spliceId.Contains("v3.94.1", StringComparison.Ordinal);
                string foreignSource = useSelectedRelease ? SelectedReleaseSource : CorrectiveReleaseSource;
                string foreignIndex = useSelectedRelease ? SelectedIndexDigest : CorrectiveIndexDigest;
                string foreignVersion = useSelectedRelease ? "3.94.1" : "3.77.2";
                candidate["source"]!["sha"]!.GetValue<string>().ShouldBe(ApprovedSourceSha);
                candidate["packages"]!["version"]!.GetValue<string>().ShouldBe(ApprovedPackageVersion);
                candidate["release"]!["semantic_version"] = foreignVersion;
                candidate["release"]!["semantic_tag"] = "v" + foreignVersion;
                candidate["release"]!["source_sha"] = foreignSource;
                candidate["release"]!["source_exact_match"] = false;
                candidate["oci"]!["index_digest"] = foreignIndex;
                candidate["oci"]!["immutable_reference"] =
                    ExpectedRegistry + "/" + ExpectedContainerRepository + "@" + foreignIndex;
                candidate["source"]!["sha"]!.GetValue<string>()
                    .ShouldNotBe(candidate["release"]!["source_sha"]!.GetValue<string>());
            }
            else
            {
                bool useSelectedSource = spliceId.Contains("v3.94.1", StringComparison.Ordinal);
                string foreignSource = useSelectedSource ? SelectedReleaseSource : CorrectiveReleaseSource;
                string foreignVersion = useSelectedSource ? "3.94.1" : "3.77.2";
                string retainedIndex = candidate["oci"]!["index_digest"]!.GetValue<string>();
                retainedIndex.ShouldBe(
                    crosswalk["selected_candidates"]![0]!["oci"]!["index_digest"]!.GetValue<string>());
                candidate["source"]!["sha"] = foreignSource;
                candidate["packages"]!["version"] = foreignVersion;
                foreach (JsonObject item in candidate["packages"]!["items"]!.AsArray()
                    .Select(item => item!.AsObject()))
                {
                    item["version"] = foreignVersion;
                }

                candidate["source"]!["sha"]!.GetValue<string>().ShouldNotBe(ApprovedSourceSha);
                candidate["oci"]!["index_digest"]!.GetValue<string>().ShouldBe(retainedIndex);
            }

            ValidateRelease(candidate, root, evidence).ShouldBeFalse();
            byte[] mutatedBytes = JsonSerializer.SerializeToUtf8Bytes(mutated);
            byte[] subjectBytes = JsonSerializer.SerializeToUtf8Bytes(CreatePassingReviewSubject(
                mutated,
                mutatedBytes,
                coreBytes,
                proofBytes,
                DateTimeOffset.UtcNow.AddMinutes(-4)));
            CreateAcceptanceReceipts(evidence, subjectBytes);
            EvaluateClosure(
                mutated,
                mutatedBytes,
                subjectBytes,
                root,
                evidence,
                coreBytes,
                proofBytes,
                packageManifestSha256).ShouldBeFalse();
        }
        finally
        {
            DeleteTemporaryDirectory(cleanupRoot);
        }
    }

    /// <summary>
    /// Verifies the recovered package directory is an exact set and every declared archive remains byte-bound.
    /// </summary>
    /// <param name="mutation">The archive mutation to apply.</param>
    [Theory]
    [InlineData("extra-archive")]
    [InlineData("mutated-bytes")]
    [InlineData("sidecar-file")]
    [InlineData("nested-archive")]
    public void PackageArchiveDirectoryRejectsExtraOrMutatedNupkg(string mutation)
    {
        string root = FindRepositoryRoot();
        (string cleanupRoot, string evidence, JsonObject crosswalk, _, _, _, byte[] proofBytes,
            string packageManifestSha256) = CreatePassingFixture(root);
        try
        {
            string archiveRoot = Path.Combine(evidence, "packages");
            if (mutation == "extra-archive")
            {
                File.WriteAllText(Path.Combine(archiveRoot, "undeclared.999.1.20-proof.fa2d1c9910f8.nupkg"), "x");
            }
            else if (mutation == "sidecar-file")
            {
                File.WriteAllText(Path.Combine(archiveRoot, "README.txt"), "sidecar");
            }
            else if (mutation == "nested-archive")
            {
                string nested = Path.Combine(archiveRoot, "nested");
                Directory.CreateDirectory(nested);
                File.WriteAllText(Path.Combine(nested, "smuggled.nupkg"), "nested-payload");
            }
            else
            {
                string archive = crosswalk["selected_candidates"]![0]!["packages"]!["items"]![0]!["archive"]!
                    .GetValue<string>();
                File.AppendAllText(Path.Combine(archiveRoot, archive), "tampered");
            }

            ValidatePackageBytes(crosswalk["selected_candidates"]![0]!.AsObject(), evidence).ShouldBeFalse();
            EvaluateWithFreshReview(
                crosswalk,
                root,
                evidence,
                proofBytes,
                packageManifestSha256).ShouldBeFalse();
        }
        finally
        {
            DeleteTemporaryDirectory(cleanupRoot);
        }
    }

    /// <summary>
    /// Verifies a mutable tag-only OCI identity cannot authorize deployment without an index digest.
    /// </summary>
    [Fact]
    public void MutableTagOnlyIdentityFailsClosed()
    {
        string root = FindRepositoryRoot();
        (string cleanupRoot, string evidence, JsonObject crosswalk, _, _, _, byte[] proofBytes,
            string packageManifestSha256) = CreatePassingFixture(root);
        try
        {
            ValidateOciGraph(crosswalk, root, evidence).ShouldBeTrue();
            EvaluateWithFreshReview(
                crosswalk,
                root,
                evidence,
                proofBytes,
                packageManifestSha256).ShouldBeTrue();

            JsonObject oci = crosswalk["selected_candidates"]![0]!["oci"]!.AsObject();
            oci["immutable_reference"] =
                ExpectedRegistry + "/" + ExpectedContainerRepository + ":v" + ApprovedPackageVersion;
            ValidateOciGraph(crosswalk, root, evidence).ShouldBeFalse();
            EvaluateWithFreshReview(
                crosswalk,
                root,
                evidence,
                proofBytes,
                packageManifestSha256).ShouldBeFalse();
        }
        finally
        {
            DeleteTemporaryDirectory(cleanupRoot);
        }
    }

    /// <summary>
    /// Verifies the OCI platform set rejects extras, unknowns, duplicates, and nested-index bindings.
    /// </summary>
    /// <param name="mutation">The platform-set mutation.</param>
    [Theory]
    [InlineData("third-child")]
    [InlineData("unknown-platform")]
    [InlineData("duplicate-platform")]
    [InlineData("nested-index")]
    public void OciGraphRejectsPlatformSetMutations(string mutation)
    {
        string root = FindRepositoryRoot();
        (string cleanupRoot, string evidence, JsonObject crosswalk, _, _, _, byte[] proofBytes,
            string packageManifestSha256) = CreatePassingFixture(root);
        try
        {
            ValidateOciGraph(crosswalk, root, evidence).ShouldBeTrue();
            JsonObject oci = crosswalk["selected_candidates"]![0]!["oci"]!.AsObject();
            JsonArray children = oci["children"]!.AsArray();
            JsonObject first = children[0]!.AsObject();
            switch (mutation)
            {
                case "third-child":
                    children.Add(Clone(first));
                    children[^1]!["platform"] = "linux/ppc64le";
                    break;
                case "unknown-platform":
                    first["platform"] = "unknown/unknown";
                    break;
                case "duplicate-platform":
                    children[1]!["platform"] = first["platform"]!.GetValue<string>();
                    break;
                default:
                    evidence = RebindIndex(crosswalk, evidence, index =>
                        index["annotations"] = new JsonObject
                        {
                            ["org.hexalith.story-3-13-control"] = "rebound",
                        });
                    ValidateOciGraph(crosswalk, root, evidence).ShouldBeTrue();
                    evidence = RebindIndex(crosswalk, evidence, index =>
                        index["manifests"]![0]!["mediaType"] = OciIndexMediaType);
                    break;
            }

            ValidateOciGraph(crosswalk, root, evidence).ShouldBeFalse();
            EvaluateWithFreshReview(
                crosswalk,
                root,
                evidence,
                proofBytes,
                packageManifestSha256).ShouldBeFalse();
        }
        finally
        {
            DeleteTemporaryDirectory(cleanupRoot);
        }
    }

    /// <summary>
    /// Verifies config os/architecture must equal the parent platform descriptor.
    /// </summary>
    [Fact]
    public void OciGraphRejectsConfigArchitectureMismatch()
    {
        string root = FindRepositoryRoot();
        (string cleanupRoot, string evidence, JsonObject crosswalk, _, _, _, byte[] proofBytes,
            string packageManifestSha256) = CreatePassingFixture(root);
        try
        {
            string rebound = RebindAmd64ConfigArchitecture(
                crosswalk,
                evidence,
                "amd64",
                reformatBytes: true);
            ValidateOciGraph(crosswalk, root, rebound).ShouldBeTrue();
            string mutated = RebindAmd64ConfigArchitecture(crosswalk, rebound, "ppc64le");
            ValidateOciGraph(crosswalk, root, mutated).ShouldBeFalse();
            EvaluateWithFreshReview(
                crosswalk,
                root,
                mutated,
                proofBytes,
                packageManifestSha256).ShouldBeFalse();
        }
        finally
        {
            DeleteTemporaryDirectory(cleanupRoot);
        }
    }

    /// <summary>
    /// Verifies ValidateRuntimeLog rejects a zero poll interval even when other fields stay valid.
    /// </summary>
    [Fact]
    public void RuntimeLogRejectsZeroPollInterval()
    {
        string root = FindRepositoryRoot();
        (string cleanupRoot, string evidence, JsonObject crosswalk, _, _, _, _, _) = CreatePassingFixture(root);
        try
        {
            JsonObject runtime = crosswalk["selected_candidates"]![0]!["runtime"]!.AsObject();
            JsonObject contract = Clone(runtime["contract"]!.AsObject());
            JsonObject platform = runtime["platforms"]![0]!.AsObject();
            DateTimeOffset started = DateTimeOffset.Parse(
                runtime["started_at"]!.GetValue<string>(),
                CultureInfo.InvariantCulture);
            DateTimeOffset ended = DateTimeOffset.Parse(
                runtime["ended_at"]!.GetValue<string>(),
                CultureInfo.InvariantCulture);
            ValidateRuntimeLog(evidence, platform, contract, started, ended).ShouldBeTrue();

            contract["poll_interval_seconds"] = 0;
            ValidateRuntimeLog(evidence, platform, contract, started, ended).ShouldBeFalse();
        }
        finally
        {
            DeleteTemporaryDirectory(cleanupRoot);
        }
    }

    /// <summary>
    /// Verifies an archive-root trailing separator resolves to the same exact package directory.
    /// </summary>
    [Fact]
    public void PackageArchiveRootAcceptsTrailingSeparator()
    {
        string root = FindRepositoryRoot();
        (string cleanupRoot, string evidence, JsonObject crosswalk, _, _, _, _, _) = CreatePassingFixture(root);
        try
        {
            JsonObject candidate = crosswalk["selected_candidates"]![0]!.AsObject();
            candidate["packages"]!["byte_verification"]!["archive_root"] = "packages/";
            ValidatePackageBytes(candidate, evidence).ShouldBeTrue();
        }
        finally
        {
            DeleteTemporaryDirectory(cleanupRoot);
        }
    }

    /// <summary>
    /// Verifies inaccessible retained evidence bytes keep the OCI graph fail-closed.
    /// </summary>
    [Fact]
    public void InaccessibleRetainedEvidenceFailsClosed()
    {
        string root = FindRepositoryRoot();
        (string cleanupRoot, string evidence, JsonObject crosswalk, _, _, _, byte[] proofBytes,
            string packageManifestSha256) = CreatePassingFixture(root);
        try
        {
            (byte[] crosswalkBytes, byte[] subjectBytes, byte[] coreBytes) =
                RefreshReviewBindings(crosswalk, evidence, proofBytes);
            File.Delete(Path.Combine(evidence, "index.raw"));
            ValidateOciGraph(crosswalk, root, evidence).ShouldBeFalse();
            EvaluateClosure(
                crosswalk,
                crosswalkBytes,
                subjectBytes,
                root,
                evidence,
                coreBytes,
                proofBytes,
                packageManifestSha256).ShouldBeFalse();
        }
        finally
        {
            DeleteTemporaryDirectory(cleanupRoot);
        }
    }

    /// <summary>
    /// Verifies an unlisted file dropped into the content-addressed evidence directory fails closed.
    /// Every other gate reads manifest entries rather than the directory, so without the directory
    /// enumeration an unbound artifact would ride inside the packet undetected.
    /// </summary>
    /// <param name="strayFileName">The unlisted file planted in the evidence directory.</param>
    [Theory]
    [InlineData("stray-evidence.json")]
    [InlineData("smoke-linux-amd64.log.bak")]
    [InlineData(".hidden-note")]
    [InlineData("nested/stray-evidence.json")]
    [InlineData("acceptances/other-subject/unbound.json")]
    public void UnlistedEvidenceDirectoryFileFailsClosed(string strayFileName)
    {
        string root = FindRepositoryRoot();
        (string cleanupRoot, string evidence, JsonObject crosswalk, byte[] crosswalkBytes,
            byte[] subjectBytes, byte[] coreBytes, byte[] proofBytes,
            string packageManifestSha256) = CreatePassingFixture(root);
        try
        {
            EvaluateClosure(
                crosswalk,
                crosswalkBytes,
                subjectBytes,
                root,
                evidence,
                coreBytes,
                proofBytes,
                packageManifestSha256).ShouldBeTrue();

            string strayPath = Path.Combine(evidence, strayFileName);
            Directory.CreateDirectory(Path.GetDirectoryName(strayPath)!);
            File.WriteAllText(strayPath, "unbound artifact");

            EvaluateClosure(
                crosswalk,
                crosswalkBytes,
                subjectBytes,
                root,
                evidence,
                coreBytes,
                proofBytes,
                packageManifestSha256).ShouldBeFalse();
        }
        finally
        {
            DeleteTemporaryDirectory(cleanupRoot);
        }
    }

    /// <summary>
    /// Verifies environment, product, and evidence failure classes each block runtime closure.
    /// </summary>
    /// <param name="site">Where the classified failure is recorded.</param>
    /// <param name="failureClass">The failure class under test.</param>
    [Theory]
    [InlineData("preflight", "environment")]
    [InlineData("platform", "product")]
    [InlineData("platform", "evidence")]
    public void ClassifiedRuntimeFailuresEachBlockEqually(string site, string failureClass)
    {
        string root = FindRepositoryRoot();
        (string cleanupRoot, string evidence, JsonObject crosswalk, _, _, _, byte[] proofBytes,
            string packageManifestSha256) = CreatePassingFixture(root);
        try
        {
            JsonObject runtime = crosswalk["selected_candidates"]![0]!["runtime"]!.AsObject();
            if (site == "preflight")
            {
                JsonObject preflight = runtime["preflight"]!.AsObject();
                preflight["outcome"] = "fail";
                preflight["failure_class"] = failureClass;
                JsonObject log = JsonNode.Parse(ReadEvidenceFile(evidence, preflight["log"]!.GetValue<string>()))!
                    .AsObject();
                log["outcome"] = "fail";
                log["failure_class"] = failureClass;
                byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(log);
                File.WriteAllBytes(Path.Combine(evidence, preflight["log"]!.GetValue<string>()), bytes);
                preflight["log_sha256"] = ComputeSha256(bytes);
            }
            else
            {
                JsonObject platform = runtime["platforms"]![0]!.AsObject();
                platform["outcome"] = "fail";
                platform["failure_class"] = failureClass;
                platform["readiness_result"] = "fail";
                JsonObject log = JsonNode.Parse(ReadEvidenceFile(evidence, platform["log"]!.GetValue<string>()))!
                    .AsObject();
                log["readiness_result"] = "fail";
                log["failure_class"] = failureClass;
                byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(log);
                File.WriteAllBytes(Path.Combine(evidence, platform["log"]!.GetValue<string>()), bytes);
                platform["log_sha256"] = ComputeSha256(bytes);
            }

            ValidateRuntimeExecution(crosswalk, root, evidence).ShouldBeFalse();
            EvaluateWithFreshReview(
                crosswalk,
                root,
                evidence,
                proofBytes,
                packageManifestSha256).ShouldBeFalse();
        }
        finally
        {
            DeleteTemporaryDirectory(cleanupRoot);
        }
    }

    /// <summary>
    /// Verifies an unclassified runtime failure is rejected rather than silently mapped to pass.
    /// </summary>
    [Fact]
    public void UnclassifiedRuntimeFailureIsRejected()
    {
        string root = FindRepositoryRoot();
        (string cleanupRoot, string evidence, JsonObject crosswalk, _, _, _, byte[] proofBytes,
            string packageManifestSha256) = CreatePassingFixture(root);
        try
        {
            JsonObject platform = crosswalk["selected_candidates"]![0]!["runtime"]!["platforms"]![0]!.AsObject();
            platform["outcome"] = "fail";
            platform.Remove("failure_class");
            ValidateRuntimeExecution(crosswalk, root, evidence).ShouldBeFalse();
            EvaluateWithFreshReview(
                crosswalk,
                root,
                evidence,
                proofBytes,
                packageManifestSha256).ShouldBeFalse();
        }
        finally
        {
            DeleteTemporaryDirectory(cleanupRoot);
        }
    }

    /// <summary>
    /// Verifies failure classification cannot disagree between the crosswalk node and retained log.
    /// </summary>
    [Fact]
    public void RuntimeFailureClassificationMustMatchRetainedLog()
    {
        JsonObject node = new() { ["outcome"] = "fail", ["failure_class"] = "environment" };
        JsonObject log = new() { ["failure_class"] = "environment" };
        RuntimeFailureClassificationMatchesLog(node, log).ShouldBeTrue();

        log["failure_class"] = "product";
        RuntimeFailureClassificationMatchesLog(node, log).ShouldBeFalse();
        log.Remove("failure_class");
        RuntimeFailureClassificationMatchesLog(node, log).ShouldBeFalse();

        node = new JsonObject { ["outcome"] = "pass" };
        RuntimeFailureClassificationMatchesLog(node, new JsonObject()).ShouldBeTrue();
        RuntimeFailureClassificationMatchesLog(
            node,
            new JsonObject { ["failure_class"] = "evidence" }).ShouldBeFalse();
        RuntimeFailureClassificationMatchesLog(
            new JsonObject { ["outcome"] = "pass", ["failure_class"] = null },
            new JsonObject { ["failure_class"] = null }).ShouldBeFalse();
    }

    /// <summary>
    /// Verifies retained-evidence copies reject symbolic links before following their targets.
    /// </summary>
    [Fact]
    public void EvidenceCopyRejectsSymbolicLinks()
    {
        string cleanupRoot = Path.Combine(Path.GetTempPath(), "story-3-13-copy-" + Guid.NewGuid().ToString("N"));
        string source = Path.Combine(cleanupRoot, "source");
        string destination = Path.Combine(cleanupRoot, "destination");
        Directory.CreateDirectory(source);
        string target = Path.Combine(cleanupRoot, "outside.txt");
        File.WriteAllText(target, "outside");
        try
        {
            try
            {
                File.CreateSymbolicLink(Path.Combine(source, "linked.txt"), target);
            }
            catch (Exception exception) when (
                exception is PlatformNotSupportedException or UnauthorizedAccessException or IOException)
            {
                Assert.Skip("Symbolic links are unavailable in this environment: " + exception.GetType().Name);
            }

            Should.Throw<InvalidDataException>(() => CopyDirectory(source, destination));
        }
        finally
        {
            DeleteTemporaryDirectory(cleanupRoot);
        }
    }

    /// <summary>
    /// Verifies fail-closed NuGet.org availability rejects missing shape and non-404 statuses.
    /// </summary>
    /// <param name="mutation">The nuget_org mutation to apply.</param>
    [Theory]
    [InlineData("null-nuget")]
    [InlineData("missing-package")]
    [InlineData("status-200")]
    [InlineData("empty-statuses")]
    [InlineData("absolute-root")]
    [InlineData("dotdot-root")]
    [InlineData("missing-durable-source")]
    [InlineData("durable-source-pass")]
    public void PackageAvailabilityRejectsNugetOrgMutations(string mutation)
    {
        string root = FindRepositoryRoot();
        string evidence = Path.Combine(root, EvidenceRelativePath);
        JsonObject crosswalk = LoadCrosswalk(root);
        string temp = Path.Combine(Path.GetTempPath(), "story-3-13-nuget-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            JsonObject report = JsonNode.Parse(
                File.ReadAllBytes(Path.Combine(evidence, "package-availability.json")))!.AsObject();
            switch (mutation)
            {
                case "null-nuget":
                    report["nuget_org"] = null;
                    break;
                case "missing-package":
                    report["nuget_org"]!["http_status_by_package"]!.AsObject()
                        .Remove("Hexalith.EventStore.Contracts");
                    break;
                case "absolute-root":
                    report["local_search_roots"] = new JsonArray("/tmp", "relative-ok");
                    break;
                case "dotdot-root":
                    report["local_search_roots"] = new JsonArray("../escape", "relative-ok");
                    break;
                case "missing-durable-source":
                    report["durable_source_queries"]!.AsArray().RemoveAt(0);
                    break;
                case "durable-source-pass":
                    report["durable_source_queries"]![0]!["result"] = "pass";
                    break;
                case "status-200":
                    foreach (KeyValuePair<string, JsonNode?> property in
                        report["nuget_org"]!["http_status_by_package"]!.AsObject().ToArray())
                    {
                        report["nuget_org"]!["http_status_by_package"]![property.Key] = 200;
                    }

                    break;
                default:
                    report["nuget_org"]!["http_status_by_package"] = new JsonObject();
                    break;
            }

            File.WriteAllBytes(
                Path.Combine(temp, "package-availability.json"),
                JsonSerializer.SerializeToUtf8Bytes(report));
            ValidatePackageAvailability(crosswalk, temp).ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    /// <summary>
    /// Verifies OCI graph evaluation rejects a child whose declared verification is not pass.
    /// </summary>
    [Fact]
    public void OciGraphRejectsChildVerificationFailure()
    {
        string root = FindRepositoryRoot();
        (string cleanupRoot, string evidence, JsonObject crosswalk, _, _, _, byte[] proofBytes,
            string packageManifestSha256) = CreatePassingFixture(root);
        try
        {
            JsonObject mutated = Clone(crosswalk);
            mutated["selected_candidates"]![0]!["oci"]!["children"]![0]!["verification"] = "fail";
            ValidateOciGraph(mutated, root, evidence).ShouldBeFalse();
            EvaluateWithFreshReview(
                mutated,
                root,
                evidence,
                proofBytes,
                packageManifestSha256).ShouldBeFalse();
        }
        finally
        {
            DeleteTemporaryDirectory(cleanupRoot);
        }
    }

    /// <summary>
    /// Verifies the package-availability report has an exact schema and support-safe content.
    /// </summary>
    /// <param name="mutation">The report mutation to apply.</param>
    [Theory]
    [InlineData("schema")]
    [InlineData("unsafe-value")]
    [InlineData("unsafe-field")]
    public void PackageAvailabilityReportRejectsSchemaAndUnsafeContent(string mutation)
    {
        string root = FindRepositoryRoot();
        (string cleanupRoot, string evidence, JsonObject crosswalk, _, _, _, byte[] proofBytes,
            string packageManifestSha256) = CreatePassingFixture(root);
        try
        {
            JsonObject report = JsonNode.Parse(ReadEvidenceFile(evidence, "package-availability.json"))!
                .AsObject();
            switch (mutation)
            {
                case "schema": report["schema"] = "package-availability/v1"; break;
                case "unsafe-value": report["archive_root"] = "http://127.0.0.1/packages"; break;
                default: report["authorization"] = "redacted"; break;
            }

            File.WriteAllBytes(
                Path.Combine(evidence, "package-availability.json"),
                JsonSerializer.SerializeToUtf8Bytes(report));
            if (mutation != "schema")
            {
                JsonEvidenceIsSupportSafe(evidence, "package-availability.json").ShouldBeFalse();
            }

            EvaluateWithFreshReview(
                crosswalk,
                root,
                evidence,
                proofBytes,
                packageManifestSha256).ShouldBeFalse();
        }
        finally
        {
            DeleteTemporaryDirectory(cleanupRoot);
        }
    }

    /// <summary>
    /// Verifies baseline commits, gitlinks, predecessor blobs, and tree manifests are object-bound.
    /// </summary>
    /// <param name="mutation">The declaration mutation to apply.</param>
    [Theory]
    [InlineData("baseline-commit")]
    [InlineData("builds-gitlink")]
    [InlineData("predecessor-blob")]
    [InlineData("predecessor-tree")]
    [InlineData("tree-manifest")]
    public void BaselineAndPredecessorDeclarationsRejectMutation(string mutation)
    {
        string root = FindRepositoryRoot();
        (string cleanupRoot, string evidence, JsonObject crosswalk, _, _, _, byte[] proofBytes,
            string packageManifestSha256) = CreatePassingFixture(root);
        try
        {
            switch (mutation)
            {
                case "baseline-commit":
                    crosswalk["baseline"]!["eventstore_head"] = new string('0', 40);
                    break;
                case "builds-gitlink":
                    crosswalk["baseline"]!["builds_gitlink_sha"] = new string('0', 40);
                    break;
                case "predecessor-blob":
                    crosswalk["predecessor_inputs"]!["story_1_20_record"]!["git_blob"] = new string('0', 40);
                    break;
                case "predecessor-tree":
                    crosswalk["predecessor_inputs"]!["story_1_20_evidence_tree"]!["git_tree"] =
                        new string('0', 40);
                    break;
                default:
                    crosswalk["predecessor_inputs"]!["story_1_20_evidence_tree"]!["full_tree_manifest_sha256"] =
                        new string('0', 64);
                    break;
            }

            EvaluateWithFreshReview(
                crosswalk,
                root,
                evidence,
                proofBytes,
                packageManifestSha256).ShouldBeFalse();
        }
        finally
        {
            DeleteTemporaryDirectory(cleanupRoot);
        }
    }

    /// <summary>
    /// Verifies every authoritative release field and the canonical lineage digest are independently enforced.
    /// </summary>
    /// <param name="mutation">The release mutation to apply.</param>
    [Theory]
    [InlineData("schema")]
    [InlineData("head-sha")]
    [InlineData("tag-ref")]
    [InlineData("workflow-url")]
    [InlineData("builds-sha")]
    [InlineData("publisher")]
    [InlineData("authority-hash")]
    [InlineData("lineage")]
    [InlineData("extra-field")]
    public void ReleaseProvenanceAndCanonicalLineageRejectMutation(string mutation)
    {
        string root = FindRepositoryRoot();
        (string cleanupRoot, string evidence, JsonObject crosswalk, _, _, _, byte[] proofBytes,
            string packageManifestSha256) = CreatePassingFixture(root);
        try
        {
            JsonObject candidate = crosswalk["selected_candidates"]![0]!.AsObject();
            JsonObject retained = JsonNode.Parse(ReadEvidenceFile(evidence, "release-provenance.json"))!.AsObject();
            switch (mutation)
            {
                case "schema": retained["schema"] = "release-provenance/v1"; break;
                case "head-sha": retained["head_sha"] = new string('0', 40); break;
                case "tag-ref": retained["tag_ref"] = "refs/heads/main"; break;
                case "workflow-url": retained["workflow_run_url"] = "https://github.com/example/run/1"; break;
                case "builds-sha": retained["builds_execution_sha"] = new string('0', 40); break;
                case "publisher": retained["publisher_identity"] = "github:unknown"; break;
                case "authority-hash": retained["authority_record_sha256"] = new string('0', 64); break;
                case "lineage": candidate["lineage_id"] = "sha256:" + new string('0', 64); break;
                default: retained["undeclared"] = true; break;
            }

            if (mutation != "lineage")
            {
                byte[] retainedBytes = JsonSerializer.SerializeToUtf8Bytes(retained);
                File.WriteAllBytes(Path.Combine(evidence, "release-provenance.json"), retainedBytes);
                candidate["release"]!["evidence_sha256"] = ComputeSha256(retainedBytes);
            }

            ValidateRelease(candidate, root, evidence).ShouldBeFalse();
            EvaluateWithFreshReview(
                crosswalk,
                root,
                evidence,
                proofBytes,
                packageManifestSha256).ShouldBeFalse();
        }
        finally
        {
            DeleteTemporaryDirectory(cleanupRoot);
        }
    }

    /// <summary>
    /// Verifies deployment authority schema, owner, source, time interval, and scope are independently enforced.
    /// </summary>
    /// <param name="mutation">The authority mutation to apply.</param>
    [Theory]
    [InlineData("expiry-boundary")]
    [InlineData("missing-offset")]
    [InlineData("wrong-owner")]
    [InlineData("source-traversal")]
    [InlineData("source-hash")]
    [InlineData("scope-splice")]
    [InlineData("extra-field")]
    public void DeploymentAuthorityRejectsBoundaryOwnerSourceAndScopeMutations(string mutation)
    {
        string root = FindRepositoryRoot();
        (string cleanupRoot, string evidence, JsonObject crosswalk, _, _, _, byte[] proofBytes,
            string packageManifestSha256) = CreatePassingFixture(root);
        try
        {
            JsonObject candidate = crosswalk["selected_candidates"]![0]!.AsObject();
            JsonObject record = JsonNode.Parse(ReadEvidenceFile(evidence, "deployment-authority.json"))!.AsObject();
            JsonObject release = JsonNode.Parse(ReadEvidenceFile(evidence, "release-provenance.json"))!.AsObject();
            switch (mutation)
            {
                case "expiry-boundary":
                    record["expires_at"] = release["deployment_action_at"]!.GetValue<string>();
                    break;
                case "missing-offset":
                    record["authorized_at"] = "2026-08-04T12:00:00";
                    break;
                case "wrong-owner":
                    record["owner"] = "github:unknown";
                    break;
                case "source-traversal":
                    record["durable_source"]!["path"] = "../deployment-authority-source.json";
                    break;
                case "source-hash":
                    record["durable_source"]!["sha256"] = new string('0', 64);
                    break;
                case "scope-splice":
                    record["scope"]!["index_digest"] = "sha256:" + new string('0', 64);
                    break;
                default:
                    record["undeclared"] = true;
                    break;
            }

            byte[] recordBytes = JsonSerializer.SerializeToUtf8Bytes(record);
            File.WriteAllBytes(Path.Combine(evidence, "deployment-authority.json"), recordBytes);
            string recordHash = ComputeSha256(recordBytes);
            candidate["release_authority"]!["record_sha256"] = recordHash;
            candidate["lineage_id"] = ComputeCanonicalLineage(candidate, recordHash);
            candidate["release_authority"]!["canonical_lineage_id"] = candidate["lineage_id"]!.GetValue<string>();
            RefreshReviewBindings(crosswalk, evidence, proofBytes);

            ValidateDeploymentAuthority(crosswalk, root, evidence).ShouldBeFalse();
        }
        finally
        {
            DeleteTemporaryDirectory(cleanupRoot);
        }
    }

    /// <summary>
    /// Verifies retained registry reports bind exact endpoints, statuses, raw files, and schemas.
    /// </summary>
    /// <param name="mutation">The report mutation to apply.</param>
    [Theory]
    [InlineData("schema")]
    [InlineData("repository")]
    [InlineData("discovery-tag")]
    [InlineData("tag-url")]
    [InlineData("tag-status")]
    [InlineData("tag-raw-file")]
    [InlineData("digest-url")]
    [InlineData("digest-status")]
    [InlineData("object-url")]
    [InlineData("object-status")]
    [InlineData("object-raw-file")]
    [InlineData("object-reference")]
    [InlineData("object-content-type")]
    [InlineData("object-digest-header")]
    [InlineData("object-content-length")]
    [InlineData("object-raw-sha")]
    [InlineData("cli-candidate-compatibility")]
    [InlineData("config-labels-summary")]
    [InlineData("config-labels-revision")]
    [InlineData("config-labels-approved-source-sha")]
    [InlineData("config-labels-version")]
    [InlineData("tag-digest-identical-flag")]
    [InlineData("extra-field")]
    public void OciRegistryReportsRejectEndpointStatusAndRawBindingMutations(string mutation)
    {
        string root = FindRepositoryRoot();
        (string cleanupRoot, string evidence, JsonObject crosswalk, _, _, _, byte[] proofBytes,
            string packageManifestSha256) = CreatePassingFixture(root);
        try
        {
            JsonObject report = JsonNode.Parse(ReadEvidenceFile(evidence, "registry-readback.json"))!.AsObject();
            switch (mutation)
            {
                case "schema": report["schema"] = "registry-readback/v1"; break;
                case "repository": report["repository"] = "registry.example.invalid/other"; break;
                case "discovery-tag": report["discovery_tag"] = "latest"; break;
                case "tag-url": report["tag_response"]!["request_url"] = "https://example.invalid"; break;
                case "tag-status": report["tag_response"]!["http_status"] = 302; break;
                case "tag-raw-file": report["tag_response"]!["raw_file"] = "index.raw"; break;
                case "digest-url": report["digest_response"]!["request_url"] = "https://example.invalid"; break;
                case "digest-status": report["digest_response"]!["http_status"] = 404; break;
                case "object-url": report["objects"]![0]!["request_url"] = "https://example.invalid"; break;
                case "object-status": report["objects"]![0]!["http_status"] = 404; break;
                case "object-raw-file": report["objects"]![0]!["raw_file"] = "index.raw"; break;
                case "object-reference": report["objects"]![0]!["reference"] = "latest"; break;
                case "object-content-type": report["objects"]![0]!["content_type"] = "application/json"; break;
                case "object-digest-header":
                    report["objects"]![0]!["docker_content_digest"] = "sha256:" + new string('0', 64);
                    break;
                case "object-content-length": report["objects"]![0]!["content_length"] = 0; break;
                case "object-raw-sha": report["objects"]![0]!["raw_sha256"] = new string('0', 64); break;
                case "cli-candidate-compatibility":
                    report["shared_validator"]!["cli_candidate_compatibility"] = "unavailable";
                    break;
                case "config-labels-summary":
                    report["config_labels"]!["verification_result"] = "fail";
                    report["config_labels"]!["provenance_label_result"] = "fail";
                    report["config_labels"]!["exact_source_match"] = false;
                    break;
                case "config-labels-revision":
                    report["config_labels"]!["revision"] = new string('0', 40);
                    break;
                case "config-labels-approved-source-sha":
                    report["config_labels"]!["approved_source_sha"] = new string('0', 40);
                    break;
                case "config-labels-version":
                    report["config_labels"]!["version"] = "0.0.0-mutated";
                    break;
                case "tag-digest-identical-flag":
                    report["tag_and_digest_bytes_identical"] = false;
                    break;
                default: report["undeclared"] = true; break;
            }

            File.WriteAllBytes(
                Path.Combine(evidence, "registry-readback.json"),
                JsonSerializer.SerializeToUtf8Bytes(report));
            ValidateOciGraph(crosswalk, root, evidence).ShouldBeFalse();
            EvaluateWithFreshReview(
                crosswalk,
                root,
                evidence,
                proofBytes,
                packageManifestSha256).ShouldBeFalse();
        }
        finally
        {
            DeleteTemporaryDirectory(cleanupRoot);
        }
    }

    /// <summary>
    /// Verifies the independent OCI validation report is also strict and content-bound.
    /// </summary>
    /// <param name="mutation">The validation-report mutation to apply.</param>
    [Theory]
    [InlineData("schema")]
    [InlineData("repository")]
    [InlineData("immutable-reference")]
    [InlineData("raw-index")]
    [InlineData("child-digest")]
    [InlineData("verification")]
    [InlineData("extra-field")]
    public void OciValidationReportRejectsSchemaRootAndDescriptorMutations(string mutation)
    {
        string root = FindRepositoryRoot();
        (string cleanupRoot, string evidence, JsonObject crosswalk, _, _, _, byte[] proofBytes,
            string packageManifestSha256) = CreatePassingFixture(root);
        try
        {
            JsonObject report = JsonNode.Parse(ReadEvidenceFile(evidence, "oci-validation.json"))!.AsObject();
            switch (mutation)
            {
                case "schema": report["schema"] = "oci-validation/v1"; break;
                case "repository": report["repository"] = "registry.example.invalid/other"; break;
                case "immutable-reference":
                    report["immutable_reference"] = ExpectedRegistry + "/" +
                        ExpectedContainerRepository + ":latest";
                    break;
                case "raw-index": report["raw_index_file"] = "tag-response.raw"; break;
                case "child-digest": report["children"]![0]!["manifest_digest"] =
                    "sha256:" + new string('0', 64); break;
                case "verification": report["verification"]!["result"] = "fail"; break;
                default: report["undeclared"] = true; break;
            }

            File.WriteAllBytes(
                Path.Combine(evidence, "oci-validation.json"),
                JsonSerializer.SerializeToUtf8Bytes(report));
            ValidateOciGraph(crosswalk, root, evidence).ShouldBeFalse();
            EvaluateWithFreshReview(
                crosswalk,
                root,
                evidence,
                proofBytes,
                packageManifestSha256).ShouldBeFalse();
        }
        finally
        {
            DeleteTemporaryDirectory(cleanupRoot);
        }
    }

    /// <summary>
    /// Verifies all five exact OCI provenance labels remain source- and release-bound.
    /// </summary>
    /// <param name="label">The label to mutate.</param>
    [Theory]
    [InlineData("org.opencontainers.image.revision")]
    [InlineData("org.opencontainers.image.version")]
    [InlineData("org.opencontainers.image.source")]
    [InlineData("org.opencontainers.image.url")]
    [InlineData("org.opencontainers.image.documentation")]
    public void OciProvenanceRejectsEveryExactLabelMutation(string label)
    {
        ArgumentNullException.ThrowIfNull(label);
        string root = FindRepositoryRoot();
        (string cleanupRoot, string evidence, JsonObject crosswalk, _, _, _, byte[] proofBytes,
            string packageManifestSha256) = CreatePassingFixture(root);
        try
        {
            crosswalk["selected_candidates"]![0]!["oci"]!["provenance_labels"]![label] =
                label.EndsWith("revision", StringComparison.Ordinal) ? new string('0', 40) : "https://example.invalid";
            ValidateOciProvenance(crosswalk, evidence).ShouldBeFalse();
            EvaluateWithFreshReview(
                crosswalk,
                root,
                evidence,
                proofBytes,
                packageManifestSha256).ShouldBeFalse();
        }
        finally
        {
            DeleteTemporaryDirectory(cleanupRoot);
        }
    }

    /// <summary>
    /// Verifies retained config-raw Labels stay bound even when the provenance summary is unchanged.
    /// </summary>
    [Fact]
    public void OciProvenanceRejectsConfigRawLabelMutationWhileSummaryStaysCorrect()
    {
        string root = FindRepositoryRoot();
        (string cleanupRoot, string evidence, JsonObject crosswalk, _, _, _, byte[] proofBytes,
            string packageManifestSha256) = CreatePassingFixture(root);
        try
        {
            string configPath = Path.Combine(evidence, "child-linux-amd64.config.raw");
            JsonObject config = JsonNode.Parse(File.ReadAllBytes(configPath))!.AsObject();
            config["config"]!["Labels"]!["org.opencontainers.image.revision"] = new string('0', 40);
            File.WriteAllBytes(configPath, JsonSerializer.SerializeToUtf8Bytes(config));

            ValidateOciProvenance(crosswalk, evidence).ShouldBeFalse();
            EvaluateWithFreshReview(
                crosswalk,
                root,
                evidence,
                proofBytes,
                packageManifestSha256).ShouldBeFalse();
        }
        finally
        {
            DeleteTemporaryDirectory(cleanupRoot);
        }
    }

    /// <summary>
    /// Verifies retained raw OCI configs reject credential-shaped values even when labels remain valid.
    /// </summary>
    [Fact]
    public void OciProvenanceRejectsSensitiveConfigRawValues()
    {
        string root = FindRepositoryRoot();
        (string cleanupRoot, string evidence, JsonObject crosswalk, _, _, _, _, _) = CreatePassingFixture(root);
        try
        {
            ValidateOciGraph(crosswalk, root, evidence).ShouldBeTrue();
            ValidateOciProvenance(crosswalk, evidence).ShouldBeTrue();

            string configPath = Path.Combine(evidence, "child-linux-amd64.config.raw");
            JsonObject config = JsonNode.Parse(File.ReadAllBytes(configPath))!.AsObject();
            config["config"]!["Env"] = new JsonArray("Authorization: Bearer retained-credential");
            File.WriteAllBytes(configPath, JsonSerializer.SerializeToUtf8Bytes(config));

            string rebound = RebindAmd64ConfigArchitecture(crosswalk, evidence, "amd64");
            ValidateOciGraph(crosswalk, root, rebound).ShouldBeFalse();
            ValidateOciProvenance(crosswalk, rebound).ShouldBeFalse();
        }
        finally
        {
            DeleteTemporaryDirectory(cleanupRoot);
        }
    }

    /// <summary>
    /// Verifies shared Builds tool pins are rehashed from the pinned git object, not JSON alone.
    /// </summary>
    [Fact]
    public void SharedBuildsToolIdentityIsRehashedFromPinnedGitBytes()
    {
        string root = FindRepositoryRoot();
        ComputePinnedBuildsToolSha256(root, ExpectedSmokeToolPath).ShouldBe(ExpectedSmokeToolSha256);
        ComputePinnedBuildsToolSha256(root, ExpectedOciValidatorPath).ShouldBe(ExpectedOciValidatorSha256);
        ComputePinnedBuildsToolSha256(
                root,
                "references/Hexalith.Builds/Github/publish-containers/publication_preflight.py")
            .ShouldNotBe(ExpectedSmokeToolSha256);
        ComputePinnedBuildsToolSha256(
                root,
                "references/Hexalith.Builds/Github/publish-containers/publication_preflight.py")
            .ShouldNotBe(ExpectedOciValidatorSha256);

        // A constant path→hash map that ignores the pinned Builds revision would still return
        // ExpectedSmokeToolSha256 here; requiring InvalidDataException forces a real git lookup.
        Should.Throw<InvalidDataException>(() => ComputePinnedBuildsToolSha256(
            root,
            ExpectedSmokeToolPath,
            new string('0', 40)));
    }

    /// <summary>
    /// Verifies runtime logs bind digest, observed platform, HTTP result, timing, cadence, and cleanup.
    /// </summary>
    /// <param name="mutation">The runtime mutation to apply.</param>
    [Theory]
    [InlineData("child-digest")]
    [InlineData("observed-platform")]
    [InlineData("http-status")]
    [InlineData("redirect")]
    [InlineData("health-path")]
    [InlineData("cadence")]
    [InlineData("timestamp-bound")]
    [InlineData("cleanup")]
    [InlineData("preflight-digest")]
    [InlineData("preflight-order")]
    [InlineData("tool-sha")]
    [InlineData("command-image")]
    [InlineData("command-not-pinned")]
    [InlineData("minimal-configuration")]
    [InlineData("global-duration")]
    [InlineData("smoke-result")]
    [InlineData("evidence-completeness")]
    [InlineData("exit-code-citation")]
    [InlineData("exit-code-verification-result")]
    [InlineData("smoke-exit-code-verification-drift")]
    [InlineData("preflight-failure-class")]
    [InlineData("platform-failure-class")]
    [InlineData("platform-unknown-failure-class")]
    [InlineData("platform-null-failure-class")]
    [InlineData("log-failure-class")]
    [InlineData("actual-hosting-environment")]
    [InlineData("required-hosting-environment")]
    [InlineData("citation")]
    [InlineData("cleanup-check")]
    [InlineData("preflight-log-name")]
    [InlineData("platform-log-name")]
    [InlineData("oversized-log")]
    [InlineData("preflight-extra-field")]
    [InlineData("platform-extra-field")]
    public void RuntimeEvidenceRejectsExecutionAndBoundMutations(string mutation)
    {
        string root = FindRepositoryRoot();
        (string cleanupRoot, string evidence, JsonObject crosswalk, _, _, _, byte[] proofBytes, _) =
            CreatePassingFixture(root);
        try
        {
            JsonObject runtime = crosswalk["selected_candidates"]![0]!["runtime"]!.AsObject();
            if (mutation is "preflight-digest" or "preflight-order")
            {
                JsonObject preflight = runtime["preflight"]!.AsObject();
                JsonObject log = JsonNode.Parse(ReadEvidenceFile(evidence, preflight["log"]!.GetValue<string>()))!
                    .AsObject();
                if (mutation == "preflight-digest")
                {
                    log["child_digest"] = "sha256:" + new string('0', 64);
                    preflight["child_digest"] = log["child_digest"]!.GetValue<string>();
                }
                else
                {
                    DateTimeOffset arm64Started = DateTimeOffset.Parse(
                        runtime["platforms"]!.AsArray().Select(item => item!.AsObject()).Single(item =>
                            item["platform"]!.GetValue<string>() == "linux/arm64")["started_at"]!
                            .GetValue<string>(),
                        CultureInfo.InvariantCulture);
                    string afterArm64Started = arm64Started.AddSeconds(1).ToString("O");
                    log["ended_at"] = afterArm64Started;
                    preflight["ended_at"] = afterArm64Started;
                }

                byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(log);
                File.WriteAllBytes(Path.Combine(evidence, preflight["log"]!.GetValue<string>()), bytes);
                preflight["log_sha256"] = ComputeSha256(bytes);
            }
            else if (mutation == "tool-sha")
            {
                runtime["tool"]!["sha256"] = new string('0', 64);
            }
            else if (mutation == "command-image")
            {
                runtime["command"]!["arguments"]![2] = ExpectedRegistry + "/" +
                    ExpectedContainerRepository + ":latest";
            }
            else if (mutation == "command-not-pinned")
            {
                runtime["command"]!["digest_pinned"] = false;
            }
            else if (mutation == "minimal-configuration")
            {
                runtime["contract"]!["minimal_configuration"]!["environment_variables"]!
                    ["ASPNETCORE_ENVIRONMENT"] = "Development";
            }
            else if (mutation == "actual-hosting-environment")
            {
                runtime["contract"]!["actual_hosting_environment"] = "Development";
                JsonObject platform = runtime["platforms"]![0]!.AsObject();
                JsonObject log = JsonNode.Parse(ReadEvidenceFile(evidence, platform["log"]!.GetValue<string>()))!
                    .AsObject();
                log["hosting_environment"] = "Development";
                byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(log);
                File.WriteAllBytes(Path.Combine(evidence, platform["log"]!.GetValue<string>()), bytes);
                platform["log_sha256"] = ComputeSha256(bytes);
            }
            else if (mutation == "required-hosting-environment")
            {
                runtime["contract"]!["required_hosting_environment"] = "Development";
            }
            else if (mutation == "citation")
            {
                string alias = "runtime-verification.alias.json";
                File.Copy(
                    Path.Combine(evidence, runtime["citation"]!.GetValue<string>()),
                    Path.Combine(evidence, alias),
                    overwrite: true);
                runtime["citation"] = alias;
            }
            else if (mutation == "cleanup-check")
            {
                runtime["cleanup_check"] = "   ";
            }
            else if (mutation == "preflight-log-name")
            {
                JsonObject preflight = runtime["preflight"]!.AsObject();
                string alias = "preflight-alias.log";
                File.Copy(
                    Path.Combine(evidence, preflight["log"]!.GetValue<string>()),
                    Path.Combine(evidence, alias),
                    overwrite: true);
                preflight["log"] = alias;
            }
            else if (mutation == "platform-log-name")
            {
                JsonObject platform = runtime["platforms"]![0]!.AsObject();
                string alias = "platform-alias.log";
                File.Copy(
                    Path.Combine(evidence, platform["log"]!.GetValue<string>()),
                    Path.Combine(evidence, alias),
                    overwrite: true);
                platform["log"] = alias;
            }
            else if (mutation == "oversized-log")
            {
                JsonObject platform = runtime["platforms"]![0]!.AsObject();
                JsonObject log = JsonNode.Parse(ReadEvidenceFile(evidence, platform["log"]!.GetValue<string>()))!
                    .AsObject();
                log["hosting_environment"] = "Production" + new string('x', 20_000);
                byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(log);
                File.WriteAllBytes(Path.Combine(evidence, platform["log"]!.GetValue<string>()), bytes);
                platform["log_sha256"] = ComputeSha256(bytes);
            }
            else if (mutation == "preflight-extra-field")
            {
                runtime["preflight"]!["undeclared"] = true;
            }
            else if (mutation == "platform-extra-field")
            {
                runtime["platforms"]![0]!["undeclared"] = true;
            }
            else if (mutation == "global-duration")
            {
                DateTimeOffset started = DateTimeOffset.Parse(
                    runtime["started_at"]!.GetValue<string>(),
                    CultureInfo.InvariantCulture);
                runtime["ended_at"] = started.AddSeconds(421).ToString("O");
            }
            else if (mutation == "smoke-result")
            {
                JsonObject smoke = JsonNode.Parse(ReadEvidenceFile(evidence, "smoke-results.json"))!.AsObject();
                smoke["result"] = "fail";
                File.WriteAllBytes(
                    Path.Combine(evidence, "smoke-results.json"),
                    JsonSerializer.SerializeToUtf8Bytes(smoke));
            }
            else if (mutation == "evidence-completeness")
            {
                runtime["evidence_completeness"] = "fail";
            }
            else if (mutation == "exit-code-citation")
            {
                runtime["exit_code_verification"]!["citation"] = "retained-smoke-output";
            }
            else if (mutation == "exit-code-verification-result")
            {
                runtime["exit_code_verification"]!["result"] = "fail";
            }
            else if (mutation == "smoke-exit-code-verification-drift")
            {
                // Applied after PersistRuntimeBindings so only the retained smoke record drifts.
            }
            else if (mutation == "preflight-failure-class")
            {
                // A node claiming outcome "pass" must carry no failure class. This is the only
                // branch RuntimeFailureClassificationIsValid decides on its own: every "fail"
                // outcome is already rejected by the sibling outcome checks.
                runtime["preflight"]!.AsObject()["failure_class"] = "environment";
            }
            else if (mutation == "platform-failure-class")
            {
                runtime["platforms"]![0]!.AsObject()["failure_class"] = "product";
            }
            else if (mutation == "platform-unknown-failure-class")
            {
                runtime["platforms"]![0]!.AsObject()["failure_class"] = "unknown";
            }
            else if (mutation == "platform-null-failure-class")
            {
                runtime["platforms"]![0]!.AsObject()["failure_class"] = null;
            }
            else
            {
                JsonObject platform = runtime["platforms"]![0]!.AsObject();
                JsonObject log = JsonNode.Parse(ReadEvidenceFile(evidence, platform["log"]!.GetValue<string>()))!
                    .AsObject();
                switch (mutation)
                {
                    case "child-digest":
                        log["child_digest"] = "sha256:" + new string('0', 64);
                        platform["child_digest"] = log["child_digest"]!.GetValue<string>();
                        break;
                    case "observed-platform": log["observed_runtime_platform"] = "linux/arm64"; break;
                    case "http-status": log["http_status"] = 302; break;
                    case "redirect": log["redirect_count"] = 1; break;
                    case "health-path": log["health_path"] = "/"; break;
                    case "cadence":
                        log["attempts"] = 99;
                        platform["attempts"] = 99;
                        break;
                    case "timestamp-bound":
                        string afterExecution = DateTimeOffset.Parse(
                                runtime["ended_at"]!.GetValue<string>(),
                                CultureInfo.InvariantCulture)
                            .AddSeconds(1).ToString("O");
                        log["ended_at"] = afterExecution;
                        platform["ended_at"] = afterExecution;
                        break;
                    case "log-failure-class":
                        log["failure_class"] = "evidence";
                        break;
                    default:
                        log["cleanup"] = "fail";
                        platform["cleanup"] = "fail";
                        break;
                }

                byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(log);
                File.WriteAllBytes(Path.Combine(evidence, platform["log"]!.GetValue<string>()), bytes);
                platform["log_sha256"] = ComputeSha256(bytes);
            }

            PersistRuntimeBindings(runtime, evidence);
            if (mutation == "smoke-exit-code-verification-drift")
            {
                JsonObject smoke = JsonNode.Parse(ReadEvidenceFile(evidence, "smoke-results.json"))!.AsObject();
                smoke["exit_code_verification"]!["citation"] = "retained-smoke-output";
                byte[] smokeBytes = JsonSerializer.SerializeToUtf8Bytes(smoke);
                File.WriteAllBytes(Path.Combine(evidence, "smoke-results.json"), smokeBytes);
                runtime["smoke_results"]!["sha256"] = ComputeSha256(smokeBytes);
                JsonObject retained = Clone(runtime);
                retained.Remove("citation");
                File.WriteAllBytes(
                    Path.Combine(evidence, runtime["citation"]!.GetValue<string>()),
                    JsonSerializer.SerializeToUtf8Bytes(retained));
            }

            RefreshReviewBindings(crosswalk, evidence, proofBytes);
            ValidateRuntimeExecution(crosswalk, root, evidence).ShouldBeFalse();
            if (mutation is "actual-hosting-environment" or "required-hosting-environment")
            {
                ValidateRuntimeEquivalence(crosswalk).ShouldBeFalse();
            }
        }
        finally
        {
            DeleteTemporaryDirectory(cleanupRoot);
        }
    }

    /// <summary>
    /// Verifies receipt schema, decision, limitations, source path, and retained source bytes are strict.
    /// </summary>
    /// <param name="mutation">The receipt mutation to apply.</param>
    [Theory]
    [InlineData("extra-field")]
    [InlineData("revoked")]
    [InlineData("empty-limitations")]
    [InlineData("source-traversal")]
    [InlineData("source-tamper")]
    public void AcceptanceReceiptsRejectSchemaDecisionAndSourceMutations(string mutation)
    {
        string root = FindRepositoryRoot();
        (string cleanupRoot, string evidence, JsonObject crosswalk, byte[] crosswalkBytes, byte[] subjectBytes,
            byte[] coreBytes, byte[] proofBytes, string packageManifestSha256) = CreatePassingFixture(root);
        try
        {
            string role = RequiredRoles[0];
            if (mutation == "source-tamper")
            {
                string source = Path.Combine(
                    evidence,
                    "acceptances",
                    ComputeSha256(subjectBytes),
                    "sources",
                    role + ".json");
                File.AppendAllText(source, " ");
            }
            else
            {
                MutateReceipt(evidence, subjectBytes, role, receipt =>
                {
                    switch (mutation)
                    {
                        case "extra-field": receipt["undeclared"] = true; break;
                        case "revoked": receipt["decision"] = "revoked"; break;
                        case "empty-limitations": receipt["accepted_limitations"] = new JsonArray(); break;
                        default: receipt["durable_source"]!["path"] = "../outside.json"; break;
                    }
                });
            }

            EvaluateClosure(
                crosswalk,
                crosswalkBytes,
                subjectBytes,
                root,
                evidence,
                coreBytes,
                proofBytes,
                packageManifestSha256).ShouldBeFalse();
        }
        finally
        {
            DeleteTemporaryDirectory(cleanupRoot);
        }
    }

    /// <summary>
    /// Verifies each acceptance receipt filename remains bound to the role field it contains.
    /// </summary>
    [Fact]
    public void AcceptanceReceiptsRejectRoleFilenameMismatch()
    {
        string root = FindRepositoryRoot();
        (string cleanupRoot, string evidence, JsonObject crosswalk, byte[] crosswalkBytes, byte[] subjectBytes,
            byte[] coreBytes, byte[] proofBytes, string packageManifestSha256) = CreatePassingFixture(root);
        try
        {
            string directory = Path.Combine(evidence, "acceptances", ComputeSha256(subjectBytes));
            string ownerPath = Path.Combine(directory, "eventstore-owner.json");
            string releasePath = Path.Combine(directory, "release-owner.json");
            JsonObject owner = JsonNode.Parse(File.ReadAllBytes(ownerPath))!.AsObject();
            JsonObject release = JsonNode.Parse(File.ReadAllBytes(releasePath))!.AsObject();
            string ownerRole = owner["role"]!.GetValue<string>();
            owner["role"] = release["role"]!.GetValue<string>();
            release["role"] = ownerRole;
            File.WriteAllBytes(ownerPath, JsonSerializer.SerializeToUtf8Bytes(owner));
            File.WriteAllBytes(releasePath, JsonSerializer.SerializeToUtf8Bytes(release));

            EvaluateClosure(
                crosswalk,
                crosswalkBytes,
                subjectBytes,
                root,
                evidence,
                coreBytes,
                proofBytes,
                packageManifestSha256).ShouldBeFalse();
        }
        finally
        {
            DeleteTemporaryDirectory(cleanupRoot);
        }
    }

    /// <summary>
    /// Verifies the repository roster has an exact schema and exact governing role mappings.
    /// </summary>
    /// <param name="mutation">The roster mutation to apply.</param>
    [Theory]
    [InlineData("extra-role")]
    [InlineData("wrong-owner")]
    [InlineData("wrong-test-architect")]
    [InlineData("extra-field")]
    [InlineData("created-after-subject")]
    [InlineData("missing-authority-source")]
    [InlineData("authority-source-kind")]
    [InlineData("authority-source-url")]
    [InlineData("authority-source-date")]
    public void ReviewerRosterRejectsExtraAndUnauthorizedMappings(string mutation)
    {
        string root = FindRepositoryRoot();
        (string cleanupRoot, string evidence, JsonObject crosswalk, _, byte[] subjectBytes, _, byte[] proofBytes,
            string packageManifestSha256) = CreatePassingFixture(root);
        try
        {
            JsonObject roster = JsonNode.Parse(ReadEvidenceFile(evidence, ReviewerRosterFile))!.AsObject();
            switch (mutation)
            {
                case "extra-role": roster["roles"]!["observer"] = new JsonArray("github:anyone"); break;
                case "wrong-owner": roster["roles"]!["release-owner"] = new JsonArray("github:unknown"); break;
                case "wrong-test-architect":
                    roster["roles"]!["test-architect"] = new JsonArray("github:jpiquot"); break;
                case "created-after-subject":
                    roster["created_at"] = DateTimeOffset.UtcNow.AddDays(1).ToString("O");
                    break;
                case "missing-authority-source":
                    roster.Remove("authority_source");
                    break;
                case "authority-source-kind":
                    roster["authority_source"]!["kind"] = "repository-commit";
                    break;
                case "authority-source-url":
                    roster["authority_source"]!["url"] =
                        "https://github.com/Hexalith/Hexalith.EventStore/commit/" + ApprovedSourceSha;
                    break;
                case "authority-source-date":
                    roster["authority_source"]!["decision_date"] = "2999-01-01";
                    break;
                default: roster["undeclared"] = true; break;
            }

            byte[] rosterBytes = JsonSerializer.SerializeToUtf8Bytes(roster);
            File.WriteAllBytes(Path.Combine(evidence, ReviewerRosterFile), rosterBytes);
            crosswalk["approval_contract"]!["reviewer_roster_sha256"] = ComputeSha256(rosterBytes);
            if (mutation == "missing-authority-source")
            {
                Should.Throw<InvalidDataException>(() => LoadReviewerRoster(
                    crosswalk,
                    evidence,
                    DateTimeOffset.Parse(
                        JsonNode.Parse(subjectBytes)!["created_at"]!.GetValue<string>(),
                        CultureInfo.InvariantCulture)));
            }

            EvaluateWithFreshReview(
                crosswalk,
                root,
                evidence,
                proofBytes,
                packageManifestSha256).ShouldBeFalse();
        }
        finally
        {
            DeleteTemporaryDirectory(cleanupRoot);
        }
    }

    /// <summary>
    /// Verifies all control-document paths reject traversal, absolute paths, and symlink escapes.
    /// </summary>
    [Fact]
    public void ControlDocumentPathsRejectMalformedAndSymlinkEscapes()
    {
        string root = FindRepositoryRoot();
        (string cleanupRoot, string evidence, JsonObject crosswalk, _, _, _, _, _) = CreatePassingFixture(root);
        try
        {
            JsonObject mutated = Clone(crosswalk);
            mutated["selected_candidates"]![0]!["release"]!["evidence_path"] = "../release-provenance.json";
            ValidateRelease(mutated["selected_candidates"]![0]!.AsObject(), root, evidence).ShouldBeFalse();

            mutated = Clone(crosswalk);
            mutated["selected_candidates"]![0]!["release_authority"]!["record_path"] =
                Path.Combine(cleanupRoot, "authority.json");
            ValidateDeploymentAuthority(mutated, root, evidence).ShouldBeFalse();

            mutated = Clone(crosswalk);
            mutated["approval_contract"]!["reviewer_roster_path"] = "../reviewer-roster.json";
            Should.Throw<InvalidDataException>(() => LoadReviewerRoster(mutated, evidence));

            string releasePath = Path.Combine(evidence, "release-provenance.json");
            string externalPath = Path.Combine(cleanupRoot, "release-provenance.json");
            File.Move(releasePath, externalPath);
            try
            {
                File.CreateSymbolicLink(releasePath, externalPath);
            }
            catch (Exception exception) when (
                exception is PlatformNotSupportedException or UnauthorizedAccessException or IOException)
            {
                Assert.Skip("Symbolic links are unavailable in this environment: " + exception.GetType().Name);
            }

            ValidateRelease(crosswalk["selected_candidates"]![0]!.AsObject(), root, evidence).ShouldBeFalse();
        }
        finally
        {
            DeleteTemporaryDirectory(cleanupRoot);
        }
    }

    /// <summary>
    /// Verifies credential-, diagnostic-, JWT-, and private-endpoint-shaped values are not support-safe.
    /// </summary>
    /// <param name="value">The unsafe retained value.</param>
    [Theory]
    [InlineData("Bearer abcdef")]
    [InlineData("eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.signature")]
    [InlineData("http://127.0.0.1:8080/alive")]
    [InlineData("System.Exception: boom at System.RuntimeMethod")]
    [InlineData("https://user:password@example.com/path")]
    public void SupportSafeRecordsRejectSensitiveValues(string value) =>
        LogIsSupportSafe(JsonSerializer.SerializeToUtf8Bytes(new JsonObject { ["message"] = value }))
            .ShouldBeFalse();

    /// <summary>
    /// Verifies the retained v3.94.1 disposition envelope verifies while acceptance reports 0 of 3.
    /// </summary>
    [Fact]
    public void DispositionEnvelopeVerifiesAndReportsThreeOfThreeAcceptances()
    {
        string root = FindRepositoryRoot();
        string disposition = Path.Combine(root, DispositionRelativePath);
        string evidence = Path.Combine(root, SelectedEvidenceRelativePath);

        Directory.Exists(disposition).ShouldBeTrue();
        Path.GetFullPath(disposition).StartsWith(
            Path.GetFullPath(evidence) + Path.DirectorySeparatorChar,
            StringComparison.Ordinal).ShouldBeFalse();
        Path.GetFullPath(disposition).StartsWith(
            Path.GetFullPath(Path.Combine(root, EvidenceRelativePath)) + Path.DirectorySeparatorChar,
            StringComparison.Ordinal).ShouldBeFalse();

        (bool verified, string rejection, int receipts, string acceptanceRejection) =
            EvaluateDisposition(root, disposition, evidence);
        verified.ShouldBeTrue(rejection);
        rejection.ShouldBeEmpty();
        // Three role-bound acceptances were collected on 2026-08-24 against this envelope: the
        // eventstore-owner and release-owner receipts are backed by GitHub-minted issue comments
        // 5395155800 and 5395155988 on issue 351, and the test-architect receipt by a bmad record.
        receipts.ShouldBe(3, acceptanceRejection);
        acceptanceRejection.ShouldBeEmpty();
        DispositionStoryMayBeDone(root, disposition, evidence).ShouldBeTrue();

        JsonObject envelope = LoadDispositionEnvelope(disposition);
        envelope["candidate"]!.GetValue<string>().ShouldBe(SelectedReleaseTag);
        envelope["candidate_disposition"]!.GetValue<string>().ShouldBe(RejectedDisposition);
        envelope["deployed_runtime_parity"]!.GetValue<string>().ShouldBe(UnavailableDeployedParity);
        envelope["selected_deployed_identity"].ShouldBeNull();
        envelope["deployment_authorized"]!.GetValue<bool>().ShouldBeFalse();
        envelope["review_subject"]!["sha256"]!.GetValue<string>().ShouldBe(SelectedReviewSubjectSha256);
        envelope["retained_blockers"]!.AsArray()
            .Select(item => item!["id"]!.GetValue<string>())
            .Order(StringComparer.Ordinal)
            .ShouldBe(RetainedBlockerIds);
    }

    /// <summary>
    /// Verifies the envelope repeats exactly the hashes and identity scalars the frozen subject records.
    /// </summary>
    [Fact]
    public void DispositionEnvelopeRepeatsTheFrozenSubjectRecordedHashesAndScalars()
    {
        string root = FindRepositoryRoot();
        string evidence = Path.Combine(root, SelectedEvidenceRelativePath);
        JsonObject envelope = LoadDispositionEnvelope(Path.Combine(root, DispositionRelativePath));
        JsonObject subject = JsonNode.Parse(ReadEvidenceFile(evidence, "review-subject.json"))!.AsObject();
        JsonObject referenced = envelope["referenced_evidence"]!.AsObject();
        JsonObject identity = envelope["retained_identity"]!.AsObject();

        // The three digests the frozen 6cee8dad... subject itself records. Asserting them here is what
        // makes that pin enforceable: a re-declared envelope over edited retained bytes cannot pass.
        subject["identity_crosswalk"]!["sha256"]!.GetValue<string>()
            .ShouldBe("ba4e909ea4fd93d0357ccdab1af579e04d4dfd134260cdd6d2db1eea9f28efcc");
        subject["evidence_core_manifest"]!["sha256"]!.GetValue<string>()
            .ShouldBe("00136b5336836bb782673c944e7cd98c274f104ff7fec9919d5b27946f538fd5");
        subject["proof_packet"]!["sha256"]!.GetValue<string>()
            .ShouldBe("684e5ced0ff0f7dcaa7b942467e035f79219cf804a35a842d800cb4c6dce0e1d");

        referenced["identity_crosswalk"]!["sha256"]!.GetValue<string>()
            .ShouldBe(subject["identity_crosswalk"]!["sha256"]!.GetValue<string>());
        referenced["evidence_core_manifest"]!["sha256"]!.GetValue<string>()
            .ShouldBe(subject["evidence_core_manifest"]!["sha256"]!.GetValue<string>());
        referenced["proof_packet"]!["sha256"]!.GetValue<string>()
            .ShouldBe(subject["proof_packet"]!["sha256"]!.GetValue<string>());
        referenced["proof_packet"]!["file"]!.GetValue<string>()
            .ShouldBe(subject["proof_packet"]!["path"]!.GetValue<string>());

        subject["identity"]!.AsObject().Select(property => property.Key)
            .Order(StringComparer.Ordinal)
            .ShouldBe(SubjectFrozenIdentityFields);
        foreach (string field in SubjectBoundIdentityFields)
        {
            identity[field]!.ToJsonString().ShouldBe(
                subject["identity"]![field]!.ToJsonString(),
                field);
        }

        ComputeSha256(Path.Combine(root, SelectedProofRelativePath))
            .ShouldBe(subject["proof_packet"]!["sha256"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies a disposition directory not named after the frozen review-subject digest fails closed.
    /// </summary>
    [Fact]
    public void MisnamedDispositionDirectoryFailsClosed()
    {
        string root = FindRepositoryRoot();
        (string cleanupRoot, string disposition) = CopyDisposition(root);
        try
        {
            // Positive control: prove the copy verifies before it is moved, so a subtly broken
            // CopyDisposition/CopyDirectory cannot leave this test green while proving nothing.
            EvaluateDisposition(
                root,
                disposition,
                Path.Combine(root, SelectedEvidenceRelativePath)).Verified.ShouldBeTrue();

            string misnamedDisposition = Path.Combine(cleanupRoot, "not-" + SelectedReviewSubjectSha256);
            CopyDirectory(disposition, misnamedDisposition);
            (bool verified, string rejection, _, _) = EvaluateDisposition(
                root,
                misnamedDisposition,
                Path.Combine(root, SelectedEvidenceRelativePath));
            verified.ShouldBeFalse();
            ShouldRejectWith(rejection, "disposition.directory");
        }
        finally
        {
            DeleteTemporaryDirectory(cleanupRoot);
        }
    }

    /// <summary>
    /// Verifies a disposition copied into the selected frozen evidence tree fails closed.
    /// </summary>
    [Fact]
    public void DispositionInsideFrozenEvidenceTreeFailsClosed()
    {
        string root = FindRepositoryRoot();
        (string cleanupRoot, string copiedRoot, string disposition, string evidence) =
            CopyDispositionWithEvidence(root);
        try
        {
            string nestedDisposition = Path.Combine(evidence, "nested", SelectedReviewSubjectSha256);
            CopyDirectory(disposition, nestedDisposition);
            (bool verified, string rejection, _, _) = EvaluateDisposition(
                copiedRoot,
                nestedDisposition,
                evidence);
            verified.ShouldBeFalse();
            ShouldRejectWith(rejection, "disposition.location");
        }
        finally
        {
            DeleteTemporaryDirectory(cleanupRoot);
        }
    }

    /// <summary>
    /// Verifies a disposition copied into the historical (non-selected) frozen evidence tree fails
    /// closed. The location guard has two disjuncts -- selected tree and historical tree -- and this
    /// covers the second, which <see cref="DispositionInsideFrozenEvidenceTreeFailsClosed"/> does not.
    /// </summary>
    [Fact]
    public void DispositionInsideHistoricalEvidenceTreeFailsClosed()
    {
        string root = FindRepositoryRoot();
        (string cleanupRoot, string disposition) = CopyDisposition(root);
        try
        {
            // Positive control: the copy must verify at its original location before being nested
            // inside the historical tree, otherwise the rejection below proves nothing.
            EvaluateDisposition(
                root,
                disposition,
                Path.Combine(root, SelectedEvidenceRelativePath)).Verified.ShouldBeTrue();

            string historicalEvidenceRoot = Path.Combine(cleanupRoot, EvidenceRelativePath);
            string nestedDisposition = Path.Combine(
                historicalEvidenceRoot,
                "nested",
                SelectedReviewSubjectSha256);
            CopyDirectory(disposition, nestedDisposition);
            (bool verified, string rejection, _, _) = EvaluateDisposition(
                cleanupRoot,
                nestedDisposition,
                Path.Combine(root, SelectedEvidenceRelativePath));
            verified.ShouldBeFalse();
            ShouldRejectWith(rejection, "disposition.location");
        }
        finally
        {
            DeleteTemporaryDirectory(cleanupRoot);
        }
    }

    /// <summary>
    /// Verifies the selected v3.94.1 tree is a closed inventory that admits no planted artifact.
    /// </summary>
    [Fact]
    public void SelectedEvidenceTreeInventoryIsClosed()
    {
        string root = FindRepositoryRoot();
        string evidence = Path.Combine(root, SelectedEvidenceRelativePath);

        // Positive control. EvidenceDirectoryHasNoUnlistedFiles is reachable only through
        // EvaluateClosure, which short-circuits on ApprovedSourceSha, so the selected tree would
        // otherwise never be inventory-checked at all.
        RejectSelectedEvidenceInventory(evidence).ShouldBeNull();

        string[] actual = Directory.GetFiles(evidence, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(evidence, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();
        actual.Length.ShouldBe(38);
        actual.Count(name => name.StartsWith("packages/", StringComparison.Ordinal)).ShouldBe(14);
        Directory.GetDirectories(evidence, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(evidence, path).Replace('\\', '/'))
            .ShouldBe(["packages"]);
    }

    /// <summary>
    /// Verifies any file planted inside the frozen selected tree fails the closed inventory.
    /// </summary>
    /// <param name="mutation">The planted-artifact identifier.</param>
    [Theory]
    [InlineData("stray-root-file", "selected_evidence.file_inventory")]
    [InlineData("forged-receipt", "selected_evidence.directory_inventory")]
    [InlineData("stray-package", "selected_evidence.file_inventory")]
    [InlineData("stray-subdirectory", "selected_evidence.directory_inventory")]
    [InlineData("empty-subdirectory", "selected_evidence.directory_inventory")]
    public void PlantedFileInsideSelectedEvidenceTreeFailsClosed(
        string mutation,
        string expectedReason)
    {
        string root = FindRepositoryRoot();
        (string cleanupRoot, string copiedRoot, string disposition, string evidence) =
            CopyDispositionWithEvidence(root);
        try
        {
            RejectSelectedEvidenceInventory(evidence).ShouldBeNull();
            EvaluateDisposition(copiedRoot, disposition, evidence).Verified.ShouldBeTrue();
            switch (mutation)
            {
                case "stray-root-file":
                    File.WriteAllText(Path.Combine(evidence, "extra-note.txt"), "planted\n");
                    break;
                case "forged-receipt":
                    {
                        string forged = Path.Combine(
                            evidence,
                            "acceptances",
                            SelectedReviewSubjectSha256);
                        Directory.CreateDirectory(forged);
                        File.WriteAllText(Path.Combine(forged, "eventstore-owner.json"), "{}");
                    }

                    break;
                case "stray-package":
                    File.WriteAllText(
                        Path.Combine(evidence, "packages", "Hexalith.EventStore.Extra.3.94.1.nupkg"),
                        "planted\n");
                    break;
                case "stray-subdirectory":
                    Directory.CreateDirectory(Path.Combine(evidence, "raw"));
                    File.WriteAllText(Path.Combine(evidence, "raw", "index.raw"), "planted\n");
                    break;
                case "empty-subdirectory":
                    // A planted directory that holds no file is visible only to the directory clause.
                    Directory.CreateDirectory(Path.Combine(evidence, "acceptances"));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown mutation.");
            }

            // Both manifests exercised below still verify; only the closed inventory can see this.
            RetainedManifestStillVerifies(evidence, "evidence-sha256.txt", evidence, 3).ShouldBeTrue();
            RetainedManifestStillVerifies(evidence, "evidence-core-sha256.txt", evidence, 34).ShouldBeTrue();
            RejectSelectedEvidenceInventory(evidence).ShouldNotBeNull();
            (bool verified, string rejection, _, _) =
                EvaluateDisposition(copiedRoot, disposition, evidence);
            verified.ShouldBeFalse();
            ShouldRejectWith(rejection, expectedReason);
        }
        finally
        {
            DeleteTemporaryDirectory(cleanupRoot);
        }
    }

    /// <summary>
    /// Verifies every retained checksum entry of both content-addressed Story 3.13 trees still passes.
    /// </summary>
    [Fact]
    public void RetainedDispositionEvidenceTreesStillVerifyEveryChecksumEntry()
    {
        string root = FindRepositoryRoot();
        string selected = Path.Combine(root, SelectedEvidenceRelativePath);
        string historical = Path.Combine(root, EvidenceRelativePath);
        string packages = Path.Combine(selected, "packages");

        // The selected tree carries four manifests totalling 91 entries; the historical tree carries
        // three (it has no nuget-sha256.txt) totalling 60. 151 is the both-trees figure.
        RetainedManifestStillVerifies(selected, "evidence-sha256.txt", selected, 3).ShouldBeTrue();
        RetainedManifestStillVerifies(selected, "evidence-core-sha256.txt", selected, 34).ShouldBeTrue();
        RetainedManifestStillVerifies(selected, "nuget-sha256.txt", packages, 14).ShouldBeTrue();
        RetainedManifestStillVerifies(selected, "predecessor-tree-sha256.txt", root, 40).ShouldBeTrue();
        RetainedManifestStillVerifies(historical, "evidence-sha256.txt", historical, 3).ShouldBeTrue();
        RetainedManifestStillVerifies(historical, "evidence-core-sha256.txt", historical, 17).ShouldBeTrue();
        RetainedManifestStillVerifies(historical, "predecessor-tree-sha256.txt", root, 40).ShouldBeTrue();
        File.Exists(Path.Combine(historical, "nuget-sha256.txt")).ShouldBeFalse();
        ComputeSha256(Path.Combine(selected, "review-subject.json")).ShouldBe(SelectedReviewSubjectSha256);
    }

    /// <summary>
    /// Verifies the complete rejected disposition plus three role-bound receipts becomes story-completable.
    /// </summary>
    [Fact]
    public void CompleteDispositionWithThreeRoleBoundReceiptsIsStoryCompletable()
    {
        string root = FindRepositoryRoot();
        string evidence = Path.Combine(root, SelectedEvidenceRelativePath);
        (string cleanupRoot, string disposition) = CopyDisposition(root);
        try
        {
            DispositionStoryMayBeDone(root, disposition, evidence).ShouldBeFalse();
            CreateDispositionReceipts(disposition);
            (bool verified, string rejection, int receipts, string acceptanceRejection) =
                EvaluateDisposition(root, disposition, evidence);
            verified.ShouldBeTrue(rejection);
            receipts.ShouldBe(3, acceptanceRejection);
            acceptanceRejection.ShouldBeEmpty();
            DispositionStoryMayBeDone(root, disposition, evidence).ShouldBeTrue();

            JsonObject envelope = LoadDispositionEnvelope(disposition);
            envelope["selected_deployed_identity"].ShouldBeNull();
            envelope["successor_boundary"]!["closes_fr36_deployed_parity"]!.GetValue<bool>().ShouldBeFalse();
            envelope["successor_boundary"]!["depends_on_corrective_release"]!.GetValue<bool>().ShouldBeFalse();
            envelope["successor_boundary"]!["positive_deployed_runtime_parity_owner"]!
                .GetValue<string>().ShouldBe("3.15");
        }
        finally
        {
            DeleteTemporaryDirectory(cleanupRoot);
        }
    }

    /// <summary>
    /// Verifies any pass-shaped or authorizing disposition claim is rejected with its exact diagnostic.
    /// </summary>
    /// <param name="mutation">The pass-shaped mutation identifier.</param>
    /// <param name="expectedReason">The exact rejection reason code the gate must emit.</param>
    [Theory]
    [InlineData("candidate-disposition-pass", "envelope.candidate_disposition")]
    [InlineData("parity-pass", "envelope.deployed_runtime_parity")]
    [InlineData("selected-identity", "envelope.selected_deployed_identity")]
    [InlineData("deployment-authorized", "envelope.deployment_authorized")]
    [InlineData("verification-pass", "envelope.verification.result")]
    [InlineData("closes-fr36", "envelope.successor_boundary.closes_fr36_deployed_parity")]
    [InlineData("authorizes-deployment", "envelope.successor_boundary.authorizes_deployment")]
    [InlineData("depends-on-corrective-release", "envelope.successor_boundary.depends_on_corrective_release")]
    public void PassShapedDispositionFailsClosed(string mutation, string expectedReason)
    {
        string root = FindRepositoryRoot();
        string evidence = Path.Combine(root, SelectedEvidenceRelativePath);
        (string cleanupRoot, string disposition) = CopyDisposition(root);
        try
        {
            EvaluateDisposition(root, disposition, evidence).Verified.ShouldBeTrue();
            JsonObject envelope = LoadDispositionEnvelope(disposition);
            switch (mutation)
            {
                case "candidate-disposition-pass":
                    envelope["candidate_disposition"] = "pass";
                    break;
                case "parity-pass":
                    envelope["deployed_runtime_parity"] = "pass";
                    break;
                case "selected-identity":
                    envelope["selected_deployed_identity"] = SelectedIndexDigest;
                    break;
                case "deployment-authorized":
                    envelope["deployment_authorized"] = true;
                    break;
                case "verification-pass":
                    envelope["verification"]!["result"] = "pass";
                    break;
                case "closes-fr36":
                    envelope["successor_boundary"]!["closes_fr36_deployed_parity"] = true;
                    break;
                case "authorizes-deployment":
                    envelope["successor_boundary"]!["authorizes_deployment"] = true;
                    break;
                case "depends-on-corrective-release":
                    envelope["successor_boundary"]!["depends_on_corrective_release"] = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown mutation.");
            }

            WriteDispositionEnvelope(disposition, envelope);
            (bool verified, string rejection, _, _) = EvaluateDisposition(root, disposition, evidence);
            verified.ShouldBeFalse();
            ShouldRejectWith(rejection, expectedReason);
        }
        finally
        {
            DeleteTemporaryDirectory(cleanupRoot);
        }
    }

    /// <summary>
    /// Verifies an omitted, normalized, or synthesized v3.94.1 fact is rejected with its exact diagnostic.
    /// </summary>
    /// <param name="mutation">The omission or normalization identifier.</param>
    /// <param name="expectedReason">The exact rejection reason code the gate must emit.</param>
    [Theory]
    [InlineData("normalized-source-label", "envelope.retained_provenance_defects.malformed_labels")]
    [InlineData("dropped-malformed-label", "envelope.retained_provenance_defects.malformed_labels")]
    [InlineData("duplicate-malformed-label", "envelope.retained_provenance_defects.malformed_labels")]
    [InlineData("extra-malformed-label", "envelope.retained_provenance_defects.malformed_labels")]
    [InlineData("extra-absent-label", "envelope.retained_provenance_defects.absent_labels")]
    [InlineData("synthesized-revision-label", "envelope.retained_provenance_defects.observed_config_revision")]
    [InlineData("dropped-absent-label", "envelope.retained_provenance_defects.absent_labels")]
    [InlineData("duplicate-absent-label", "envelope.retained_provenance_defects.absent_labels")]
    [InlineData("dropped-blocker", "envelope.retained_blockers")]
    [InlineData("reworded-blocker", "envelope.retained_blockers")]
    [InlineData("dropped-retained-limitation", "envelope.limitations")]
    [InlineData("reworded-retained-limitation", "envelope.limitations")]
    [InlineData("extra-retained-limitation", "envelope.limitations")]
    public void OmittedOrNormalizedDispositionFactFailsClosed(string mutation, string expectedReason)
    {
        string root = FindRepositoryRoot();
        string evidence = Path.Combine(root, SelectedEvidenceRelativePath);
        (string cleanupRoot, string disposition) = CopyDisposition(root);
        try
        {
            EvaluateDisposition(root, disposition, evidence).Verified.ShouldBeTrue();
            JsonObject envelope = LoadDispositionEnvelope(disposition);
            JsonObject defects = envelope["retained_provenance_defects"]!.AsObject();
            switch (mutation)
            {
                case "normalized-source-label":
                    defects["malformed_labels"]!.AsArray()
                        .First(item => item!["label"]!.GetValue<string>() ==
                            "org.opencontainers.image.source")!
                        ["retained_value"] = "https://github.com/" + ExpectedRepository;
                    break;
                case "dropped-malformed-label":
                    defects["malformed_labels"]!.AsArray().RemoveAt(0);
                    break;
                case "duplicate-malformed-label":
                    {
                        JsonArray malformedLabels = defects["malformed_labels"]!.AsArray();
                        malformedLabels[malformedLabels.Count - 1] = malformedLabels[0]!.DeepClone();
                    }

                    break;
                case "extra-malformed-label":
                    // Only the exact-cardinality guard can reject a fabricated extra defect row.
                    defects["malformed_labels"]!.AsArray().Add(new JsonObject
                    {
                        ["platform"] = "linux/amd64",
                        ["config_file"] = "child-linux-amd64.config.raw",
                        ["label"] = "org.opencontainers.image.vendor",
                        ["retained_value"] = MalformedLabelValue,
                    });
                    break;
                case "extra-absent-label":
                    defects["absent_labels"]!.AsArray().Add(new JsonObject
                    {
                        ["platform"] = "linux/riscv64",
                        ["config_file"] = "child-linux-riscv64.config.raw",
                        ["label"] = RevisionLabel,
                    });
                    break;
                case "synthesized-revision-label":
                    defects["observed_config_revision"] = SelectedSourceSha;
                    break;
                case "dropped-absent-label":
                    defects["absent_labels"]!.AsArray().RemoveAt(0);
                    break;
                case "duplicate-absent-label":
                    {
                        JsonArray absentLabels = defects["absent_labels"]!.AsArray();
                        absentLabels[absentLabels.Count - 1] = absentLabels[0]!.DeepClone();
                    }

                    break;
                case "dropped-blocker":
                    envelope["retained_blockers"]!.AsArray().RemoveAt(0);
                    break;
                case "reworded-blocker":
                    envelope["retained_blockers"]!.AsArray()[0]!["consequence"] =
                        "The provenance labels are acceptable for this candidate.";
                    break;
                case "dropped-retained-limitation":
                    envelope["limitations"]!.AsArray().RemoveAt(2);
                    break;
                case "reworded-retained-limitation":
                    envelope["limitations"]!.AsArray()[2] =
                        "OCI provenance labels are valid absolute URLs for this candidate.";
                    break;
                case "extra-retained-limitation":
                    envelope["limitations"]!.AsArray().Add("A fabricated limitation was appended.");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown mutation.");
            }

            WriteDispositionEnvelope(disposition, envelope);
            (bool verified, string rejection, _, _) = EvaluateDisposition(root, disposition, evidence);
            verified.ShouldBeFalse();
            ShouldRejectWith(rejection, expectedReason);
        }
        finally
        {
            DeleteTemporaryDirectory(cleanupRoot);
        }
    }

    /// <summary>
    /// Verifies a genuine internal fault reaches the catch-all diagnostic. This is deliberately a
    /// read failure rather than malformed content: a malformed envelope is diagnosed as
    /// <c>disposition.canonical_bytes</c>, so without this case no test exercises
    /// <c>internal.exception</c> and the whole catch-all could be deleted with the suite green.
    /// </summary>
    [Fact]
    public void UnreadableDispositionEnvelopeReportsAnInternalDiagnostic()
    {
        string root = FindRepositoryRoot();
        string evidence = Path.Combine(root, SelectedEvidenceRelativePath);
        (string cleanupRoot, string disposition) = CopyDisposition(root);
        try
        {
            EvaluateDisposition(root, disposition, evidence).Verified.ShouldBeTrue();

            // Replacing the envelope file with a directory of the same name makes the read fail with
            // an I/O-class exception on every supported platform, without depending on file-mode
            // enforcement that a privileged test runner would bypass.
            string envelopePath = Path.Combine(disposition, DispositionEnvelopeFile);
            File.Delete(envelopePath);
            Directory.CreateDirectory(envelopePath);

            (bool verified, string rejection, _, _) = EvaluateDisposition(root, disposition, evidence);
            verified.ShouldBeFalse();
            ShouldRejectWith(rejection, "internal.exception");
        }
        finally
        {
            DeleteTemporaryDirectory(cleanupRoot);
        }
    }

    /// <summary>
    /// Verifies a duplicated or empty declared checksum-manifest row rejects the envelope. Without
    /// this guard a duplicate "file" value falls through to <c>SingleOrDefault</c> in the per-row
    /// loop, which throws <see cref="InvalidOperationException"/> and is swallowed as
    /// <c>internal.exception</c> instead of this field-specific diagnostic.
    /// </summary>
    /// <param name="mutation">The manifest-declaration defect identifier.</param>
    [Theory]
    [InlineData("duplicate-manifest-file")]
    [InlineData("empty-manifest-file")]
    public void DuplicateOrEmptyChecksumManifestDeclarationFailsClosed(string mutation)
    {
        string root = FindRepositoryRoot();
        string evidence = Path.Combine(root, SelectedEvidenceRelativePath);
        (string cleanupRoot, string disposition) = CopyDisposition(root);
        try
        {
            EvaluateDisposition(root, disposition, evidence).Verified.ShouldBeTrue();
            JsonObject envelope = LoadDispositionEnvelope(disposition);
            JsonArray manifests = envelope["retained_checksum_manifests"]!.AsArray();
            switch (mutation)
            {
                case "duplicate-manifest-file":
                    // Collapse the last row's "file" onto the first row's, keeping the array length
                    // unchanged but leaving two distinct declared manifests sharing one file name.
                    manifests[^1]!["file"] = manifests[0]!["file"]!.GetValue<string>();
                    break;
                case "empty-manifest-file":
                    manifests[0]!["file"] = string.Empty;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown mutation.");
            }

            WriteDispositionEnvelope(disposition, envelope);
            (bool verified, string rejection, _, _) = EvaluateDisposition(root, disposition, evidence);
            verified.ShouldBeFalse();
            ShouldRejectWith(rejection, "envelope.retained_checksum_manifests");
        }
        finally
        {
            DeleteTemporaryDirectory(cleanupRoot);
        }
    }

    /// <summary>
    /// Verifies any retained checksum drift rejects the envelope and records a revalidation trigger.
    /// </summary>
    /// <param name="mutation">The drifting retained artifact identifier.</param>
    /// <param name="expectedReason">The exact rejection reason code the gate must emit.</param>
    [Theory]
    [InlineData("core-evidence-file", "retained_checksum_manifest.evidence-core-sha256.txt")]
    [InlineData("package-archive", "retained_checksum_manifest.evidence-core-sha256.txt")]
    [InlineData("predecessor-file", "retained_checksum_manifest.predecessor-tree-sha256.txt")]
    [InlineData("recaptured-core-manifest", "envelope.referenced_evidence.evidence_core_manifest")]
    [InlineData("recaptured-crosswalk", "envelope.referenced_evidence.identity_crosswalk")]
    public void FrozenChainDriftFailsClosed(string mutation, string expectedReason)
    {
        string root = FindRepositoryRoot();
        (string cleanupRoot, string copiedRoot, string disposition, string evidence) =
            CopyDispositionWithEvidence(root);
        try
        {
            EvaluateDisposition(copiedRoot, disposition, evidence).Verified.ShouldBeTrue();
            switch (mutation)
            {
                case "core-evidence-file":
                    File.AppendAllText(Path.Combine(evidence, "smoke-linux-amd64.log"), "drift\n");
                    break;
                case "package-archive":
                    File.AppendAllBytes(
                        Directory.GetFiles(Path.Combine(evidence, "packages"), "*.nupkg")
                            .Order(StringComparer.Ordinal).First(),
                        [0x00]);
                    break;
                case "predecessor-file":
                    File.AppendAllText(
                        Path.Combine(copiedRoot, Story120EvidenceRelativePath, "environment.txt"),
                        "drift\n");
                    break;
                case "recaptured-core-manifest":
                    File.AppendAllText(Path.Combine(evidence, "smoke-linux-arm64.log"), "drift\n");
                    RewriteRetainedCoreManifest(evidence);
                    break;
                case "recaptured-crosswalk":
                    RewriteRetainedCrosswalkVerdict(evidence);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown mutation.");
            }

            (bool verified, string rejection, _, _) =
                EvaluateDisposition(copiedRoot, disposition, evidence);
            verified.ShouldBeFalse();
            ShouldRejectWith(rejection, expectedReason);
            rejection.ShouldContain("revalidation:");
            LoadDispositionEnvelope(disposition)["revalidation_trigger"]!.GetValue<string>()
                .ShouldContain("Never re-capture");
        }
        finally
        {
            DeleteTemporaryDirectory(cleanupRoot);
        }
    }

    /// <summary>
    /// Verifies re-declaring the envelope over drifted retained bytes still fails the frozen subject pin.
    /// </summary>
    /// <param name="mutation">The drift-and-re-declare identifier.</param>
    /// <param name="expectedReason">The exact rejection reason code the gate must emit.</param>
    [Theory]
    [InlineData("proof-packet-drift-redeclared", "subject.proof_packet.sha256")]
    [InlineData("core-manifest-drift-redeclared", "subject.evidence_core_manifest.sha256")]
    [InlineData("crosswalk-drift-redeclared", "subject.identity_crosswalk.sha256")]
    [InlineData("identity-scalar-only", "subject.retained_identity.workflow_run")]
    [InlineData("package-version-scalar-only", "subject.retained_identity.package_version")]
    public void EnvelopeReDeclaredOverDriftedRetainedBytesFailsClosed(string mutation, string expectedReason)
    {
        string root = FindRepositoryRoot();
        (string cleanupRoot, string copiedRoot, string disposition, string evidence) =
            CopyDispositionWithEvidence(root);
        try
        {
            EvaluateDisposition(copiedRoot, disposition, evidence).Verified.ShouldBeTrue();
            JsonObject envelope = LoadDispositionEnvelope(disposition);
            switch (mutation)
            {
                case "proof-packet-drift-redeclared":
                    {
                        // The proof packet belongs to no checksum manifest, so only the subject's
                        // recorded 684e5ced... digest can reject this.
                        string packet = Path.Combine(copiedRoot, SelectedProofRelativePath);
                        File.AppendAllText(packet, "\nre-declared\n");
                        ReDeclareBinding(envelope["referenced_evidence"]!["proof_packet"]!.AsObject(), packet);
                    }

                    break;
                case "core-manifest-drift-redeclared":
                    {
                        File.AppendAllText(Path.Combine(evidence, "smoke-preflight.log"), "drift\n");
                        RewriteRetainedCoreManifest(evidence);
                        RewriteRetainedOuterManifest(evidence);
                        ReDeclareBinding(
                            envelope["referenced_evidence"]!["evidence_core_manifest"]!.AsObject(),
                            Path.Combine(evidence, "evidence-core-sha256.txt"));
                    }

                    break;
                case "crosswalk-drift-redeclared":
                    {
                        RewriteRetainedCrosswalkVerdict(evidence);
                        RewriteRetainedOuterManifest(evidence);
                        ReDeclareBinding(
                            envelope["referenced_evidence"]!["identity_crosswalk"]!.AsObject(),
                            Path.Combine(evidence, "identity-crosswalk.json"));
                        envelope["retained_blockers"]!.AsArray().RemoveAt(0);
                    }

                    break;
                case "identity-scalar-only":
                    envelope["retained_identity"]!["workflow_run"] = 31781920405L;
                    break;
                case "package-version-scalar-only":
                    envelope["retained_identity"]!["package_version"] = "3.94.2";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown mutation.");
            }

            WriteDispositionEnvelope(disposition, envelope);
            (bool verified, string rejection, _, _) =
                EvaluateDisposition(copiedRoot, disposition, evidence);
            verified.ShouldBeFalse();
            ShouldRejectWith(rejection, expectedReason);
        }
        finally
        {
            DeleteTemporaryDirectory(cleanupRoot);
        }
    }

    /// <summary>
    /// Verifies an incomplete or unauthorized acceptance set leaves Story 3.13 non-done.
    /// </summary>
    /// <param name="mutation">The acceptance defect identifier.</param>
    /// <param name="expectedCount">The exact number of receipts that must still validate.</param>
    /// <param name="expectedReason">The exact acceptance rejection reason code.</param>
    [Theory]
    [InlineData("two-receipts", 0, "acceptance.receipt_set")]
    [InlineData("stale-subject-digest", 2, "acceptance.receipt.subject_sha256")]
    [InlineData("self-declared-role", 2, "acceptance.roster.reviewer_identity")]
    [InlineData("unauthorized-reviewer-identity", 2, "acceptance.roster.reviewer_identity")]
    [InlineData("source-identity-mismatch", 2, "acceptance.source.reviewer_identity")]
    [InlineData("planning-approval-as-receipt", 2, "acceptance.receipt.durable_source")]
    [InlineData("planning-artifact-source-kind", 2, "acceptance.receipt.durable_source")]
    [InlineData("role-field-mismatch", 2, "acceptance.receipt.role_filename")]
    [InlineData("rejected-decision", 2, "acceptance.receipt.decision")]
    [InlineData("wrong-accepted-scope", 2, "acceptance.receipt.accepted_scope")]
    [InlineData("receipt-schema-mismatch", 2, "acceptance.receipt.schema")]
    [InlineData("backdated-receipt", 2, "acceptance.receipt.accepted_at")]
    [InlineData("future-receipt", 2, "acceptance.receipt.accepted_at")]
    [InlineData("accepted-limitations-mismatch", 2, "acceptance.receipt.accepted_limitations")]
    [InlineData("source-schema-mismatch", 2, "acceptance.source.record")]
    [InlineData("source-subject-digest", 2, "acceptance.source.subject_sha256")]
    [InlineData("source-decision-mismatch", 2, "acceptance.source.decision")]
    [InlineData("malformed-receipt-json", 2, "acceptance.receipt.json")]
    [InlineData("malformed-source-json", 2, "acceptance.source.record")]
    [InlineData("synthetic-comment-anchor", 2, "acceptance.source.comment_anchor")]
    [InlineData("mismatched-comment-anchor", 2, "acceptance.source.comment_anchor")]
    [InlineData("unbound-source-bytes", 2, "acceptance.receipt.durable_source")]
    [InlineData("edited-comment", 2, "acceptance.source.comment_edited")]
    [InlineData("foreign-comment-author", 2, "acceptance.source.comment_author")]
    [InlineData("comment-body-decision", 2, "acceptance.source.comment_body")]
    [InlineData("comment-shape-drift", 2, "acceptance.source.comment")]
    public void IncompleteDispositionAcceptanceKeepsStoryNonDone(
        string mutation,
        int expectedCount,
        string expectedReason)
    {
        string root = FindRepositoryRoot();
        string evidence = Path.Combine(root, SelectedEvidenceRelativePath);
        (string cleanupRoot, string disposition) = CopyDisposition(root);
        try
        {
            CreateDispositionReceipts(disposition);
            DispositionStoryMayBeDone(root, disposition, evidence).ShouldBeTrue();

            byte[] envelopeBytes = ReadEvidenceFile(disposition, DispositionEnvelopeFile);
            DateTimeOffset validationTime = ParseVerifiedExplicitOffset(
                JsonNode.Parse(envelopeBytes)!["assembled_at"]!.GetValue<string>()).AddMinutes(2);
            string receiptDirectory = Path.Combine(
                disposition,
                "acceptances",
                ComputeSha256(envelopeBytes));
            switch (mutation)
            {
                case "two-receipts":
                    File.Delete(Path.Combine(receiptDirectory, "release-owner.json"));
                    break;
                case "stale-subject-digest":
                    // A realistic hex digest, not a degenerate constant: an all-zero value is
                    // rejected by the support-safety scan and would pass for the wrong reason.
                    MutateDispositionReceiptAndSourceField(
                        receiptDirectory,
                        "eventstore-owner",
                        "subject_sha256",
                        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef");
                    break;
                case "self-declared-role":
                    MutateDispositionReceipt(receiptDirectory, "test-architect", receipt =>
                        receipt["reviewer_identity"] = "bmad:self-declared");
                    break;
                case "unauthorized-reviewer-identity":
                    // Receipt and durable source agree on an identity the packet-bound roster does
                    // not authorize, so only the roster check can reject it.
                    MutateDispositionReceiptAndSource(
                        receiptDirectory,
                        "test-architect",
                        "bmad:not-the-test-architect");
                    break;
                case "source-identity-mismatch":
                    // The receipt names a rostered identity; its durable source names a different
                    // rostered identity, so only the receipt-to-source cross-check can reject it.
                    MutateDispositionSourceRecord(receiptDirectory, "eventstore-owner", source =>
                        source["reviewer_identity"] = "bmad:murat");
                    break;
                case "planning-approval-as-receipt":
                    MutateDispositionReceipt(receiptDirectory, "release-owner", receipt =>
                        receipt["durable_source"] = new JsonObject
                        {
                            ["kind"] = "planning-artifact",
                            ["path"] = DispositionAuthorityRelativePath,
                            ["sha256"] = new string('1', 64),
                        });
                    break;
                case "planning-artifact-source-kind":
                    // Path and digest stay correct, so only the durable-source kind clause can
                    // reject a receipt that reclassifies its source as a planning artifact.
                    MutateDispositionReceipt(receiptDirectory, "release-owner", receipt =>
                        receipt["durable_source"]!["kind"] = "planning-artifact");
                    break;
                case "role-field-mismatch":
                    MutateDispositionReceipt(receiptDirectory, "release-owner", receipt =>
                        receipt["role"] = "eventstore-owner");
                    break;
                case "rejected-decision":
                    MutateDispositionReceipt(receiptDirectory, "eventstore-owner", receipt =>
                        receipt["decision"] = "rejected");
                    break;
                case "wrong-accepted-scope":
                    MutateDispositionReceipt(receiptDirectory, "test-architect", receipt =>
                        receipt["accepted_scope"] = "Story 3.13 deployed-runtime parity closure");
                    break;
                case "receipt-schema-mismatch":
                    MutateDispositionReceipt(receiptDirectory, "eventstore-owner", receipt =>
                        receipt["schema"] = "wrong-schema");
                    break;
                case "backdated-receipt":
                    MutateDispositionReceiptAndSourceTimestamps(
                        receiptDirectory,
                        "eventstore-owner",
                        "2026-01-01T00:00:00+00:00");
                    break;
                case "future-receipt":
                    MutateDispositionReceiptAndSourceTimestamps(
                        receiptDirectory,
                        "eventstore-owner",
                        validationTime.AddDays(1).ToString("O", CultureInfo.InvariantCulture));
                    break;
                case "accepted-limitations-mismatch":
                    MutateDispositionReceipt(receiptDirectory, "eventstore-owner", receipt =>
                        receipt["accepted_limitations"]!.AsArray().RemoveAt(0));
                    break;
                case "source-schema-mismatch":
                    MutateDispositionSourceRecord(receiptDirectory, "eventstore-owner", source =>
                        source["schema"] = "wrong-schema");
                    break;
                case "source-subject-digest":
                    MutateDispositionSourceRecord(receiptDirectory, "eventstore-owner", source =>
                        source["subject_sha256"] = new string('3', 64));
                    break;
                case "synthetic-comment-anchor":
                    // The exact URL shape the previous contract demanded. GitHub never mints it, so
                    // it must now fail closed rather than be the only accepted form.
                    MutateDispositionSourceRecord(receiptDirectory, "eventstore-owner", source =>
                        source["comment"]!["html_url"] =
                            "https://github.com/" + ExpectedRepository + "/commit/" +
                            SelectedSourceSha + "#story-3-13-disposition-anchor-eventstore-owner");
                    break;

                case "mismatched-comment-anchor":
                    // A well-formed GitHub anchor that resolves to a different comment than the one
                    // the record retains. The URL parses, so only the agreement between the anchored
                    // id and comment.id can reject it.
                    MutateDispositionSourceRecord(receiptDirectory, "eventstore-owner", source =>
                        source["comment"]!["html_url"] =
                            "https://github.com/" + ExpectedRepository + "/issues/324#issuecomment-" +
                            (source["comment"]!["id"]!.GetValue<long>() + 1)
                                .ToString(CultureInfo.InvariantCulture));
                    break;

                case "unbound-source-bytes":
                    {
                        // Re-emitting the retained source record with indentation leaves every value
                        // the gate compares identical while changing the bytes, so only the receipt's
                        // durable_source.sha256 binding can see that it no longer covers its source.
                        string sourcePath = Path.Combine(
                            receiptDirectory,
                            "sources",
                            "eventstore-owner.json");
                        File.WriteAllBytes(
                            sourcePath,
                            JsonSerializer.SerializeToUtf8Bytes(
                                JsonNode.Parse(File.ReadAllBytes(sourcePath))!.AsObject(),
                                IndentedDispositionJsonOptions));
                    }

                    break;

                case "edited-comment":
                    // GitHub stamps updated_at forward when a comment is edited after posting, so a
                    // retained comment whose two timestamps disagree is no longer the text the
                    // reviewer published at the acceptance instant.
                    MutateDispositionSourceRecord(receiptDirectory, "eventstore-owner", source =>
                        source["comment"]!["updated_at"] = "2026-08-24T13:00:00Z");
                    break;

                case "foreign-comment-author":
                    MutateDispositionSourceRecord(receiptDirectory, "eventstore-owner", source =>
                        source["comment"]!["user"]!["login"] = "not-the-owner");
                    break;

                case "comment-body-decision":
                    MutateDispositionSourceRecord(receiptDirectory, "eventstore-owner", source =>
                    {
                        JsonObject body = JsonNode.Parse(
                            source["comment"]!["body"]!.GetValue<string>())!.AsObject();
                        body["decision"] = "rejected";
                        source["comment"]!["body"] = body.ToJsonString();
                    });
                    break;

                case "comment-shape-drift":
                    MutateDispositionSourceRecord(receiptDirectory, "eventstore-owner", source =>
                        source["comment"]!.AsObject().Remove("author_association"));
                    break;

                case "source-decision-mismatch":
                    MutateDispositionSourceRecord(receiptDirectory, "eventstore-owner", source =>
                        source["decision"] = "rejected");
                    break;
                case "malformed-receipt-json":
                    File.WriteAllText(
                        Path.Combine(receiptDirectory, "eventstore-owner.json"),
                        "{ not json");
                    break;
                case "malformed-source-json":
                    {
                        byte[] malformedSource = Encoding.UTF8.GetBytes("{ not json");
                        File.WriteAllBytes(
                            Path.Combine(receiptDirectory, "sources", "eventstore-owner.json"),
                            malformedSource);
                        MutateDispositionReceipt(receiptDirectory, "eventstore-owner", receipt =>
                            receipt["durable_source"]!["sha256"] = ComputeSha256(malformedSource));
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown mutation.");
            }

            RebindDispositionManifest(disposition);
            (bool verified, string rejection, int receipts, string acceptanceRejection) =
                EvaluateDisposition(root, disposition, evidence, validationTime);
            verified.ShouldBeTrue(rejection);
            receipts.ShouldBe(expectedCount);
            ShouldRejectWith(acceptanceRejection, expectedReason);
            DispositionStoryMayBeDone(root, disposition, evidence, validationTime).ShouldBeFalse();
        }
        finally
        {
            DeleteTemporaryDirectory(cleanupRoot);
        }
    }

    /// <summary>
    /// Verifies mixing Story 1.20, v3.77.2, or Story 3.14 identity material fails closed.
    /// </summary>
    /// <param name="mutation">The spliced lineage identifier.</param>
    [Theory]
    [InlineData("story-1-20-source")]
    [InlineData("story-1-20-index")]
    [InlineData("story-1-20-crosswalk")]
    [InlineData("v3-77-2-release-tag")]
    [InlineData("story-3-14-source")]
    [InlineData("story-3-14-index")]
    [InlineData("foreign-material-in-authority-section")]
    [InlineData("foreign-material-in-defect-method")]
    [InlineData("foreign-material-in-limitations")]
    [InlineData("foreign-material-in-verification")]
    [InlineData("foreign-material-in-successor-boundary")]
    [InlineData("foreign-material-in-acceptance-contract")]
    [InlineData("foreign-material-in-retained-blockers")]
    [InlineData("foreign-material-in-revalidation-trigger")]
    public void CrossLineageSpliceFailsClosed(string mutation)
    {
        string root = FindRepositoryRoot();
        string evidence = Path.Combine(root, SelectedEvidenceRelativePath);
        (string cleanupRoot, string disposition) = CopyDisposition(root);
        try
        {
            EvaluateDisposition(root, disposition, evidence).Verified.ShouldBeTrue();
            JsonObject envelope = LoadDispositionEnvelope(disposition);
            JsonObject identity = envelope["retained_identity"]!.AsObject();
            switch (mutation)
            {
                case "story-1-20-source":
                    identity["source_sha"] = ApprovedSourceSha;
                    break;
                case "story-1-20-index":
                    identity["index_digest"] = ExpectedIndexDigest;
                    break;
                case "story-1-20-crosswalk":
                    {
                        byte[] historical = ReadEvidenceFile(
                            root,
                            EvidenceRelativePath + "/identity-crosswalk.json");
                        envelope["referenced_evidence"]!["identity_crosswalk"] = new JsonObject
                        {
                            ["file"] = EvidenceRelativePath + "/identity-crosswalk.json",
                            ["size"] = historical.Length,
                            ["sha256"] = ComputeSha256(historical),
                        };
                    }

                    break;
                case "v3-77-2-release-tag":
                    identity["release_tag"] = "v3.77.2";
                    identity["release_version"] = "3.77.2";
                    break;
                case "story-3-14-source":
                    identity["source_sha"] = "f343bb0153e9cdcb8b12ec10153813072f5ad38d";
                    break;
                case "story-3-14-index":
                    identity["index_digest"] =
                        "sha256:4b1410852b11be3bcaebf8f2e6277c1d30ce13a19f48cf0df86ed93646d709c3";
                    break;
                case "foreign-material-in-authority-section":
                    // Free text inside an identity-bearing section is checked only by the foreign
                    // lineage token scan, so this case makes that scan the deciding guard.
                    envelope["governing_authority"]!["section"] =
                        "4.4 Story 3.13 implementation boundary, carried over from release 3.77.2";
                    break;
                case "foreign-material-in-defect-method":
                    envelope["retained_provenance_defects"]!["verification"]!["method"] =
                        "re-parse both retained raw config objects and compare them with release 3.96.2";
                    break;
                case "foreign-material-in-limitations":
                    envelope["limitations"]!.AsArray().Add("splice retained evidence from v3.77.2");
                    break;
                case "foreign-material-in-verification":
                    envelope["verification"]!["method"] = "compare against release 3.96.2";
                    break;
                case "foreign-material-in-successor-boundary":
                    envelope["successor_boundary"]!["positive_deployed_runtime_parity_owner"] =
                        "Story 3.15 using release 3.96.2";
                    break;
                case "foreign-material-in-acceptance-contract":
                    envelope["acceptance_contract"]!["receipt_schema"] =
                        DispositionReceiptSchema + "/3.96.2";
                    break;
                case "foreign-material-in-retained-blockers":
                    envelope["retained_blockers"]!.AsArray()[0]!["consequence"] =
                        "deployment stays blocked until v3.77.2 is selected";
                    break;
                case "foreign-material-in-revalidation-trigger":
                    envelope["revalidation_trigger"] = "revalidate against 3.96.2";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown mutation.");
            }

            WriteDispositionEnvelope(disposition, envelope);
            (bool verified, string rejection, _, _) = EvaluateDisposition(root, disposition, evidence);
            verified.ShouldBeFalse();
            ShouldRejectWith(rejection, "envelope.foreign_lineage");
        }
        finally
        {
            DeleteTemporaryDirectory(cleanupRoot);
        }
    }

    /// <summary>
    /// Verifies the disposition directory stays a closed, canonical, hash-bound inventory.
    /// </summary>
    /// <param name="mutation">The closure defect identifier.</param>
    /// <param name="expectedReason">The exact rejection reason code the gate must emit.</param>
    [Theory]
    [InlineData("resealed-stray-file", "disposition.manifest")]
    [InlineData("resealed-stray-acceptance-file", "disposition.manifest")]
    [InlineData("resealed-empty-acceptance-directory", "disposition.manifest")]
    [InlineData("role-filename-mismatch", "disposition.manifest")]
    [InlineData("undeclared-sidecar", "disposition.manifest")]
    [InlineData("stale-envelope-directory", "disposition.manifest")]
    [InlineData("missing-entry", "disposition.manifest")]
    [InlineData("mismatched-hash", "disposition.manifest")]
    [InlineData("non-canonical-envelope", "disposition.canonical_bytes")]
    [InlineData("indented-envelope", "disposition.canonical_bytes")]
    public void DispositionDirectoryClosureFailsClosed(string mutation, string expectedReason)
    {
        string root = FindRepositoryRoot();
        string evidence = Path.Combine(root, SelectedEvidenceRelativePath);
        (string cleanupRoot, string disposition) = CopyDisposition(root);
        try
        {
            EvaluateDisposition(root, disposition, evidence).Verified.ShouldBeTrue();
            string manifestPath = Path.Combine(disposition, DispositionManifestFile);
            switch (mutation)
            {
                case "resealed-stray-file":
                    File.WriteAllText(Path.Combine(disposition, "notes.txt"), "unlisted\n");
                    RebindDispositionManifest(disposition);
                    break;
                case "resealed-stray-acceptance-file":
                    {
                        CreateDispositionReceipts(disposition);
                        string envelopeHash = ComputeSha256(ReadEvidenceFile(
                            disposition,
                            DispositionEnvelopeFile));
                        File.WriteAllText(
                            Path.Combine(
                                disposition,
                                "acceptances",
                                envelopeHash,
                                "sources",
                                "notes.txt"),
                            "unlisted\n");
                        RebindDispositionManifest(disposition);
                    }

                    break;
                case "resealed-empty-acceptance-directory":
                    {
                        // A planted directory holding no file leaves both the file inventory and the
                        // resealed manifest intact, so only the closed-directory clause can see it.
                        CreateDispositionReceipts(disposition);
                        string envelopeHash = ComputeSha256(ReadEvidenceFile(
                            disposition,
                            DispositionEnvelopeFile));
                        Directory.CreateDirectory(Path.Combine(
                            disposition,
                            "acceptances",
                            envelopeHash,
                            "sources",
                            "pending"));
                        RebindDispositionManifest(disposition);
                    }

                    break;
                case "role-filename-mismatch":
                    {
                        CreateDispositionReceipts(disposition);
                        string envelopeHash = ComputeSha256(ReadEvidenceFile(
                            disposition,
                            DispositionEnvelopeFile));
                        string receiptDirectory = Path.Combine(
                            disposition,
                            "acceptances",
                            envelopeHash);
                        File.Move(
                            Path.Combine(receiptDirectory, "release-owner.json"),
                            Path.Combine(receiptDirectory, "release-owner-2.json"));
                        RebindDispositionManifest(disposition);
                    }

                    break;
                case "undeclared-sidecar":
                    {
                        CreateDispositionReceipts(disposition);
                        string envelopeHash = ComputeSha256(ReadEvidenceFile(
                            disposition,
                            DispositionEnvelopeFile));
                        File.WriteAllText(
                            Path.Combine(
                                disposition,
                                "acceptances",
                                envelopeHash,
                                "extra-approval.json"),
                            "{}");
                        RebindDispositionManifest(disposition);
                    }

                    break;
                case "stale-envelope-directory":
                    {
                        CreateDispositionReceipts(disposition);
                        string envelopeHash = ComputeSha256(ReadEvidenceFile(
                            disposition,
                            DispositionEnvelopeFile));
                        Directory.Move(
                            Path.Combine(disposition, "acceptances", envelopeHash),
                            Path.Combine(disposition, "acceptances", new string('2', 64)));
                        RebindDispositionManifest(disposition);
                    }

                    break;
                case "missing-entry":
                    File.WriteAllText(manifestPath, string.Empty);
                    break;
                case "mismatched-hash":
                    File.WriteAllText(
                        manifestPath,
                        new string('0', 64) + "  " + DispositionEnvelopeFile + "\n");
                    break;
                case "non-canonical-envelope":
                    File.WriteAllBytes(
                        Path.Combine(disposition, DispositionEnvelopeFile),
                        JsonSerializer.SerializeToUtf8Bytes(LoadDispositionEnvelope(disposition)));
                    RebindDispositionManifest(disposition);
                    break;
                case "indented-envelope":
                    File.WriteAllBytes(
                        Path.Combine(disposition, DispositionEnvelopeFile),
                        JsonSerializer.SerializeToUtf8Bytes(
                            CanonicalizeJson(LoadDispositionEnvelope(disposition)),
                            IndentedDispositionJsonOptions));
                    RebindDispositionManifest(disposition);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown mutation.");
            }

            (bool verified, string rejection, _, _) = EvaluateDisposition(root, disposition, evidence);
            verified.ShouldBeFalse();
            ShouldRejectWith(rejection, expectedReason);
        }
        finally
        {
            DeleteTemporaryDirectory(cleanupRoot);
        }
    }

    /// <summary>
    /// Verifies malformed or non-object envelopes surface as canonical-envelope defects.
    /// </summary>
    /// <param name="content">The invalid envelope content.</param>
    [Theory]
    [InlineData("{ not json")]
    [InlineData("[]")]
    public void InvalidDispositionEnvelopeReportsCanonicalBytesDiagnostic(string content)
    {
        string root = FindRepositoryRoot();
        string evidence = Path.Combine(root, SelectedEvidenceRelativePath);
        (string cleanupRoot, string disposition) = CopyDisposition(root);
        try
        {
            EvaluateDisposition(root, disposition, evidence).Verified.ShouldBeTrue();
            File.WriteAllText(Path.Combine(disposition, DispositionEnvelopeFile), content);
            RebindDispositionManifest(disposition);
            (bool verified, string rejection, _, _) = EvaluateDisposition(root, disposition, evidence);
            verified.ShouldBeFalse();
            ShouldRejectWith(rejection, "disposition.canonical_bytes");
        }
        finally
        {
            DeleteTemporaryDirectory(cleanupRoot);
        }
    }

    /// <summary>
    /// Verifies the Story 3.13 lifecycle surfaces record the rejected, non-authorizing disposition.
    /// </summary>
    [Fact]
    public void Story313LifecycleSurfacesRecordTheRejectedDisposition()
    {
        string root = FindRepositoryRoot();

        // Every lifecycle assertion below is anchored to the line that owns the value, never to a
        // whole-file substring: these documents also narrate their own lifecycle history, and a
        // substring scan reads that prose instead of the state it claims to pin.
        string sprint = ReadNormalizedText(root, SprintStatusRelativePath);
        SingleLineValue(sprint, "  3-13-v3-94-1-deployed-runtime-evidence-disposition:")
            .ShouldBe("done");

        string story = ReadNormalizedText(root, StoryRecordRelativePath);
        story.ShouldContain("# Story 3.13: v3.94.1 Deployed Runtime Evidence Disposition");
        story.ShouldContain(RejectedDisposition);
        SingleLineValue(story, "Status:").ShouldBe("done");

        string ci = ReadNormalizedText(root, CiDocumentationRelativePath);
        ci.ShouldContain(RejectedDisposition);
        ci.ShouldContain("Story 3.15 owns positive deployed-runtime parity");
        ci.ShouldContain("Story 3.14 owns the corrective release");
        // The operator-facing surface must carry a verifiable digest, not an elided one.
        ci.ShouldContain(SelectedReviewSubjectSha256);

        // The spec frontmatter is a fourth lifecycle surface distinct from the story record; it must
        // not silently drift to the closed lifecycle token while acceptance sits at 0/3 receipts.
        // Anchored to the parsed YAML frontmatter block on purpose: a whole-file substring scan reads
        // the review ledger's own prose, which quotes both lifecycle tokens verbatim when recording
        // findings about them. That made the negative half fail on correct content and the positive
        // half pass off a prose line, so neither observed the frontmatter it claimed to pin.
        string spec = ReadNormalizedText(root, SpecRelativePath);
        FrontmatterValue(spec, "status").ShouldBe("'done'");
    }

    /// <summary>
    /// Reads the value of the one line that starts with a prefix, failing when it is not unique.
    /// </summary>
    /// <param name="text">The normalized document text.</param>
    /// <param name="prefix">The line prefix that owns the value.</param>
    /// <returns>The trimmed remainder of that line.</returns>
    private static string SingleLineValue(string text, string prefix)
    {
        string[] matches = text.Split('\n')
            .Where(line => line.StartsWith(prefix, StringComparison.Ordinal))
            .ToArray();
        matches.Length.ShouldBe(1, "expected exactly one line starting with " + prefix);
        return matches[0][prefix.Length..].Trim();
    }

    /// <summary>
    /// Reads one key from a document's leading YAML frontmatter block.
    /// </summary>
    /// <param name="text">The normalized document text.</param>
    /// <param name="key">The frontmatter key to read.</param>
    /// <returns>The trimmed value, or an empty string when the block or key is absent.</returns>
    private static string FrontmatterValue(string text, string key)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (!text.StartsWith("---\n", StringComparison.Ordinal))
        {
            return string.Empty;
        }

        int end = text.IndexOf("\n---", 3, StringComparison.Ordinal);
        if (end < 0)
        {
            return string.Empty;
        }

        string prefix = key + ":";
        foreach (string line in text[4..end].Split('\n'))
        {
            if (line.StartsWith(prefix, StringComparison.Ordinal))
            {
                return line[prefix.Length..].Trim();
            }
        }

        return string.Empty;
    }

    private static string ReadNormalizedText(string root, string relativePath) =>
        Encoding.UTF8.GetString(ReadEvidenceFile(root, relativePath))
            .Replace("\r\n", "\n", StringComparison.Ordinal);

    private static void ShouldRejectWith(string actualReason, string expectedCode)
    {
        // Comparing the exact code makes an unexpected rejection -- including a fixture fault that
        // reached the catch as internal.exception -- fail the case instead of passing it.
        DispositionReasonCode(actualReason).ShouldBe(expectedCode, actualReason);
        ValueIsSupportSafe(actualReason).ShouldBeTrue(actualReason);

        // Every rejection has to hand the operator a next action, so a reason that names a defect
        // without a remediation or a revalidation trigger is itself a defect.
        (actualReason.Contains("; remediation: ", StringComparison.Ordinal)
            || actualReason.Contains("; revalidation: ", StringComparison.Ordinal))
            .ShouldBeTrue(actualReason);
    }

    // The disposition envelope lives outside both content-addressed evidence trees because the frozen
    // crosswalk pins receipt_count to 0, so adding files inside would force a crosswalk edit that
    // invalidates the very subject the envelope cites. Every rejection returns a support-safe reason
    // naming the offending field plus a remediation or revalidation trigger, so a negative test can
    // assert the exact cause instead of a bare false.
    private static (bool Verified, string Rejection, int AcceptedReceipts, string AcceptanceRejection)
        EvaluateDisposition(
            string repositoryRoot,
            string dispositionRoot,
            string selectedEvidenceRoot,
            DateTimeOffset? validationTime = null)
    {
        try
        {
            DateTimeOffset observedAt = validationTime ?? DateTimeOffset.UtcNow;
            if (Path.GetFileName(dispositionRoot.TrimEnd(Path.DirectorySeparatorChar)) !=
                SelectedReviewSubjectSha256)
            {
                return (false, DispositionReason(
                    "disposition.directory",
                    "the disposition directory is not addressed by the frozen review subject digest",
                    "place the envelope under evidence/story-3-13/disposition/<review-subject-sha256>"), 0, string.Empty);
            }

            if (PathIsWithin(dispositionRoot, selectedEvidenceRoot)
                || PathIsWithin(dispositionRoot, Path.Combine(repositoryRoot, EvidenceRelativePath)))
            {
                return (false, DispositionReason(
                    "disposition.location",
                    "the disposition is inside a frozen content-addressed evidence tree",
                    "place the disposition outside both frozen evidence trees"), 0, string.Empty);
            }

            byte[] envelopeBytes = ReadEvidenceFile(dispositionRoot, DispositionEnvelopeFile);
            JsonObject? envelope;
            try
            {
                envelope = JsonNode.Parse(envelopeBytes) as JsonObject;
            }
            catch (JsonException)
            {
                envelope = null;
            }

            if (envelope is null)
            {
                return (false, DispositionReason(
                    "disposition.canonical_bytes",
                    "the disposition envelope is not a JSON object",
                    "re-emit the envelope with the platform codec canonical_bytes form"), 0, string.Empty);
            }

            if (!envelopeBytes.SequenceEqual(CanonicalDispositionBytes(envelope)))
            {
                return (false, DispositionReason(
                    "disposition.canonical_bytes",
                    "the envelope bytes are not the canonical sorted-key compact form with a trailing newline",
                    "re-emit the envelope with the platform codec canonical_bytes form"), 0, string.Empty);
            }

            string? manifestRejection = RejectDispositionManifest(
                dispositionRoot,
                ComputeSha256(envelopeBytes));
            if (manifestRejection is not null)
            {
                return (false, manifestRejection, 0, string.Empty);
            }

            string? envelopeRejection = RejectDispositionEnvelope(
                envelope,
                repositoryRoot,
                selectedEvidenceRoot,
                observedAt);
            if (envelopeRejection is not null)
            {
                return (false, envelopeRejection, 0, string.Empty);
            }

            (int accepted, string acceptanceRejection) = CountDispositionReceipts(
                envelope,
                envelopeBytes,
                repositoryRoot,
                dispositionRoot,
                selectedEvidenceRoot,
                observedAt);
            return (true, string.Empty, accepted, acceptanceRejection);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
            or NullReferenceException
            or ArgumentException
            or OverflowException
            or FormatException
            or InvalidDataException
            or JsonException
            or IOException
            or NotSupportedException
            or UnauthorizedAccessException)
        {
            return (false, DispositionReason(
                "internal.exception",
                "the disposition gate could not complete: " + exception.GetType().Name,
                "repair the evidence fixture and re-run the disposition gate"), 0, string.Empty);
        }
    }

    private static bool DispositionStoryMayBeDone(
        string repositoryRoot,
        string dispositionRoot,
        string selectedEvidenceRoot,
        DateTimeOffset? validationTime = null)
    {
        (bool verified, _, int receipts, _) =
            EvaluateDisposition(repositoryRoot, dispositionRoot, selectedEvidenceRoot, validationTime);
        return verified && receipts == 3;
    }

    private static string DispositionReason(string code, string detail, string remediation) =>
        code + ": " + detail + "; remediation: " + remediation;

    private static string DispositionDriftReason(string code, string detail) =>
        code + ": " + detail + "; revalidation: " + DispositionRevalidationTrigger;

    private static string DispositionReasonCode(string reason)
    {
        int separator = reason.IndexOf(':', StringComparison.Ordinal);
        return separator < 0 ? reason : reason[..separator];
    }

    private static string? RejectDispositionEnvelope(
        JsonObject envelope,
        string repositoryRoot,
        string selectedEvidenceRoot,
        DateTimeOffset validationTime)
    {
        if (!HasExactProperties(envelope, DispositionEnvelopeFields))
        {
            return DispositionReason(
                "envelope.schema",
                "the envelope does not carry exactly the required top-level fields",
                "emit every required field and no additional field");
        }

        if (!DocumentIsSupportSafe(envelope))
        {
            return DispositionReason(
                "envelope.support_safety",
                "the envelope carries a field name or value that is not support-safe",
                "remove the sensitive field name or value before re-emitting the envelope");
        }

        string?[] shapeChecks =
        [
            RejectUnless(
                envelope["schema"]!.GetValue<string>() == DispositionSchema,
                "envelope.schema",
                "the envelope schema identifier is not the Story 3.13 disposition schema",
                "restore the disposition envelope schema identifier"),
            RejectUnless(
                envelope["story_id"]!.GetValue<string>() == "3.13",
                "envelope.story_id",
                "the envelope does not identify Story 3.13",
                "restore story_id to 3.13"),
            RejectUnless(
                envelope["candidate"]!.GetValue<string>() == SelectedReleaseTag,
                "envelope.candidate",
                "the disposed candidate is not the retained v3.94.1 release",
                "restore the retained v3.94.1 candidate"),
            RejectUnless(
                envelope["candidate_disposition"]!.GetValue<string>() == RejectedDisposition,
                "envelope.candidate_disposition",
                "the candidate disposition is not the retained rejected-non-authorizing value",
                "restore candidate_disposition to rejected-non-authorizing; a pass outcome is never valid for v3.94.1"),
            RejectUnless(
                envelope["deployed_runtime_parity"]!.GetValue<string>() == UnavailableDeployedParity,
                "envelope.deployed_runtime_parity",
                "deployed runtime parity is not recorded as unavailable for v3.94.1",
                "restore deployed_runtime_parity to unavailable-for-v3.94.1; Story 3.15 owns positive parity"),
            RejectUnless(
                envelope["selected_deployed_identity"] is null,
                "envelope.selected_deployed_identity",
                "the envelope selects a deployed identity, which a rejected candidate may never do",
                "restore selected_deployed_identity to null"),
            RejectUnless(
                !envelope["deployment_authorized"]!.GetValue<bool>(),
                "envelope.deployment_authorized",
                "the envelope authorizes deployment, which the retained authority record forbids",
                "restore deployment_authorized to false"),
            RejectUnless(
                !string.IsNullOrWhiteSpace(envelope["revalidation_trigger"]!.GetValue<string>()),
                "envelope.revalidation_trigger",
                "the envelope records no revalidation trigger",
                "record the revalidation trigger for retained checksum drift"),
        ];
        string? shapeRejection = shapeChecks.FirstOrDefault(reason => reason is not null);
        if (shapeRejection is not null)
        {
            return shapeRejection;
        }

        string? foreignRejection = RejectForeignLineage(envelope);
        if (foreignRejection is not null)
        {
            return foreignRejection;
        }

        string? bindingRejection = RejectDispositionBindings(envelope, repositoryRoot);
        if (bindingRejection is not null)
        {
            return bindingRejection;
        }

        JsonObject referenced = envelope["referenced_evidence"]!.AsObject();
        byte[] crosswalkBytes = ReadEvidenceFile(
            repositoryRoot,
            referenced["identity_crosswalk"]!["file"]!.GetValue<string>());
        byte[] subjectBytes = ReadEvidenceFile(
            repositoryRoot,
            envelope["review_subject"]!["file"]!.GetValue<string>());
        JsonObject crosswalk = JsonNode.Parse(crosswalkBytes)!.AsObject();
        JsonObject subject = JsonNode.Parse(subjectBytes)!.AsObject();

        return RejectSubjectBinding(envelope, subject, crosswalk)
            ?? RejectDispositionIdentity(envelope, crosswalk, selectedEvidenceRoot)
            ?? RejectDispositionDefects(envelope, selectedEvidenceRoot)
            ?? RejectDispositionRetainedRecords(
                envelope,
                crosswalk,
                subject,
                selectedEvidenceRoot,
                repositoryRoot)
            ?? RejectSelectedEvidenceInventory(selectedEvidenceRoot)
            ?? RejectDispositionContracts(envelope, crosswalk, repositoryRoot)
            ?? RejectDispositionChronology(envelope, subject, validationTime);
    }

    private static string? RejectUnless(bool condition, string code, string detail, string remediation) =>
        condition ? null : DispositionReason(code, detail, remediation);

    private static string? RejectDispositionBindings(JsonObject envelope, string repositoryRoot)
    {
        JsonObject referenced = envelope["referenced_evidence"]!.AsObject();
        if (!ExactFileBindingMatches(
                envelope["review_subject"],
                repositoryRoot,
                SelectedEvidenceRelativePath + "/review-subject.json")
            || envelope["review_subject"]!["sha256"]!.GetValue<string>() != SelectedReviewSubjectSha256)
        {
            return DispositionDriftReason(
                "envelope.review_subject",
                "the declared review-subject binding does not reproduce the retained 6cee8dad subject bytes");
        }

        if (!HasExactProperties(
                referenced,
                ["evidence_core_manifest", "identity_crosswalk", "proof_packet", "reviewer_roster"]))
        {
            return DispositionReason(
                "envelope.referenced_evidence",
                "the envelope does not reference exactly the four required retained artifacts",
                "reference the crosswalk, evidence-core manifest, reviewer roster, and v3.94.1 proof packet");
        }

        string[] names = ["identity_crosswalk", "evidence_core_manifest", "reviewer_roster", "proof_packet"];
        string[] paths =
        [
            SelectedEvidenceRelativePath + "/identity-crosswalk.json",
            SelectedEvidenceRelativePath + "/evidence-core-sha256.txt",
            SelectedEvidenceRelativePath + "/reviewer-roster.json",
            SelectedProofRelativePath,
        ];
        for (int index = 0; index < names.Length; index++)
        {
            if (!ExactFileBindingMatches(referenced[names[index]], repositoryRoot, paths[index]))
            {
                return DispositionDriftReason(
                    "envelope.referenced_evidence." + names[index],
                    "the declared binding does not reproduce the retained bytes on disk");
            }
        }

        JsonObject authority = envelope["governing_authority"]!.AsObject();
        if (!HasExactProperties(
                authority,
                ["approved_by", "approved_on", "file", "kind", "section", "sha256", "size"])
            || authority["kind"]!.GetValue<string>() != "approved-sprint-change-proposal"
            || authority["approved_on"]!.GetValue<string>() != "2026-08-16"
            || string.IsNullOrWhiteSpace(authority["approved_by"]!.GetValue<string>())
            || string.IsNullOrWhiteSpace(authority["section"]!.GetValue<string>())
            || !FileContentBindingMatches(authority, repositoryRoot, DispositionAuthorityRelativePath))
        {
            return DispositionReason(
                "envelope.governing_authority",
                "the governing authority binding is incomplete or does not reproduce the approved 2026-08-16 proposal",
                "bind the approved correct-course proposal by file, size, and sha256; planning approval is never a receipt");
        }

        return null;
    }

    // The frozen 6cee8dad subject records its own digests and identity scalars. Comparing the envelope
    // against those recorded values -- not only against files on disk -- is what makes the subject pin
    // enforceable, because a re-declared envelope over edited retained bytes still contradicts them.
    private static string? RejectSubjectBinding(
        JsonObject envelope,
        JsonObject subject,
        JsonObject crosswalk)
    {
        JsonObject referenced = envelope["referenced_evidence"]!.AsObject();
        JsonObject identity = envelope["retained_identity"]!.AsObject();
        // The subject's own field set is not re-checked here: the envelope's review_subject binding
        // already pins the subject bytes exactly, so any shape change is rejected before this point.
        // SubjectFrozenIdentityFields is asserted by the focused test instead of by a dead guard.
        JsonObject subjectIdentity = subject["identity"]!.AsObject();
        string[] names = ["identity_crosswalk", "evidence_core_manifest", "proof_packet"];
        string[] envelopePaths =
        [
            SelectedEvidenceRelativePath + "/identity-crosswalk.json",
            SelectedEvidenceRelativePath + "/evidence-core-sha256.txt",
            SelectedProofRelativePath,
        ];
        for (int index = 0; index < names.Length; index++)
        {
            string name = names[index];
            if (subject[name] is not JsonObject recorded
                || recorded["sha256"]!.GetValue<string>() !=
                    referenced[name]!["sha256"]!.GetValue<string>())
            {
                return DispositionDriftReason(
                    "subject." + name + ".sha256",
                    "the envelope declares a digest the frozen review subject does not record for this artifact");
            }

            if (referenced[name]!["file"]!.GetValue<string>() != envelopePaths[index])
            {
                return DispositionDriftReason(
                    "subject." + name + ".path",
                    "the envelope references a different file than the frozen review subject pins");
            }
        }

        if (subject["proof_packet"]!["path"]!.GetValue<string>() != SelectedProofRelativePath)
        {
            return DispositionDriftReason(
                "subject.proof_packet.path",
                "the frozen review subject pins a different v3.94.1 proof-packet path");
        }

        foreach (string field in SubjectBoundIdentityFields)
        {
            if (!JsonNode.DeepEquals(identity[field], subjectIdentity[field]))
            {
                return DispositionDriftReason(
                    "subject.retained_identity." + field,
                    "the envelope declares an identity value the frozen review subject does not record");
            }
        }

        // The subject is the authority; these pin the subject itself so the comparison above is not
        // merely self-consistent.
        JsonObject release = crosswalk["selected_candidates"]![0]!["release"]!.AsObject();
        string?[] coherence =
        [
            RejectUnless(
                subjectIdentity["source_sha"]!.GetValue<string>() == SelectedSourceSha,
                "subject.crosswalk_coherence.source_sha",
                "the retained review subject does not record the selected v3.94.1 source",
                "revalidate the retained review subject before re-declaring the envelope"),
            RejectUnless(
                subjectIdentity["index_digest"]!.GetValue<string>() == SelectedIndexDigest,
                "subject.crosswalk_coherence.index_digest",
                "the retained review subject does not record the selected immutable index digest",
                "revalidate the retained review subject before re-declaring the envelope"),
            RejectUnless(
                subjectIdentity["package_version"]!.GetValue<string>() == SelectedPackageVersion,
                "subject.crosswalk_coherence.package_version",
                "the retained review subject does not record the selected package version",
                "revalidate the retained review subject before re-declaring the envelope"),
            RejectUnless(
                subjectIdentity["release_version"]!.GetValue<string>() ==
                    release["semantic_version"]!.GetValue<string>(),
                "subject.crosswalk_coherence.release_version",
                "the retained review subject and identity crosswalk disagree on the release version",
                "revalidate the retained review subject before re-declaring the envelope"),
            RejectUnless(
                subjectIdentity["workflow_run"]!.GetValue<long>() ==
                    release["workflow_run"]!.GetValue<long>(),
                "subject.crosswalk_coherence.workflow_run",
                "the retained review subject and identity crosswalk disagree on the release workflow run",
                "revalidate the retained review subject before re-declaring the envelope"),
            RejectUnless(
                subjectIdentity["authority_record_sha256"]!.GetValue<string>() ==
                    crosswalk["selected_candidates"]![0]!["release_authority"]!["record_sha256"]!
                        .GetValue<string>(),
                "subject.crosswalk_coherence.authority_record_sha256",
                "the retained review subject and identity crosswalk disagree on the release authority record",
                "revalidate the retained review subject before re-declaring the envelope"),
            RejectUnless(
                subjectIdentity["package_hash_manifest_sha256"]!.GetValue<string>() ==
                    crosswalk["approved_identity"]!["package_hash_manifest_sha256"]!.GetValue<string>(),
                "subject.crosswalk_coherence.package_hash_manifest_sha256",
                "the retained review subject and identity crosswalk disagree on the package hash manifest",
                "revalidate the retained review subject before re-declaring the envelope"),
            RejectUnless(
                subjectIdentity["canonical_lineage_id"] is null,
                "subject.crosswalk_coherence.canonical_lineage_id",
                "the retained review subject asserts a canonical lineage the rejected candidate cannot have",
                "revalidate the retained review subject before re-declaring the envelope"),
        ];
        return coherence.FirstOrDefault(reason => reason is not null);
    }

    private static string? RejectDispositionIdentity(
        JsonObject envelope,
        JsonObject crosswalk,
        string selectedEvidenceRoot)
    {
        JsonObject candidate = crosswalk["selected_candidates"]![0]!.AsObject();
        JsonObject release = candidate["release"]!.AsObject();
        JsonObject identity = envelope["retained_identity"]!.AsObject();
        if (!HasExactProperties(
            identity,
            [
                "authority_record_sha256",
                "builds_execution_sha",
                "container_repository",
                "evidence_root",
                "index_digest",
                "package_version",
                "registry",
                "release_tag",
                "release_version",
                "source_sha",
                "workflow_attempt",
                "workflow_run",
            ]))
        {
            return DispositionReason(
                "envelope.retained_identity",
                "the retained identity does not carry exactly the required scalars",
                "declare every retained identity scalar and no additional scalar");
        }

        JsonObject deploymentAuthority = JsonNode.Parse(
            ReadEvidenceFile(selectedEvidenceRoot, "deployment-authority.json"))!.AsObject();
        string?[] checks =
        [
            RejectUnless(
                identity["evidence_root"]!.GetValue<string>() == SelectedEvidenceRelativePath,
                "envelope.retained_identity.evidence_root",
                "the retained evidence root is not the selected v3.94.1 content-addressed tree",
                "restore the selected evidence root path"),
            RejectUnless(
                candidate["source"]!["sha"]!.GetValue<string>() == SelectedSourceSha,
                "envelope.retained_identity.source_sha",
                "the identity crosswalk does not describe the selected v3.94.1 source",
                "bind the envelope to the selected v3.94.1 crosswalk"),
            RejectUnless(
                identity["release_tag"]!.GetValue<string>() == SelectedReleaseTag
                && identity["release_tag"]!.GetValue<string>() ==
                    release["semantic_tag"]!.GetValue<string>(),
                "envelope.retained_identity.release_tag",
                "the declared release tag is not the retained v3.94.1 tag",
                "restore the retained v3.94.1 release tag"),
            RejectUnless(
                identity["workflow_attempt"]!.GetValue<int>() ==
                    release["workflow_attempt"]!.GetValue<int>(),
                "envelope.retained_identity.workflow_attempt",
                "the declared workflow attempt does not match the retained release provenance",
                "restore the retained workflow attempt"),
            RejectUnless(
                identity["builds_execution_sha"]!.GetValue<string>() ==
                    release["builds_execution_sha"]!.GetValue<string>(),
                "envelope.retained_identity.builds_execution_sha",
                "the declared Builds execution identity does not match the retained release provenance",
                "restore the retained Builds execution identity"),
            RejectUnless(
                identity["registry"]!.GetValue<string>() == ExpectedRegistry,
                "envelope.retained_identity.registry",
                "the declared registry is not the EventStore release registry",
                "restore the retained registry"),
            RejectUnless(
                identity["container_repository"]!.GetValue<string>() == ExpectedContainerRepository,
                "envelope.retained_identity.container_repository",
                "the declared container repository is not the EventStore image repository",
                "restore the retained container repository"),
            RejectUnless(
                identity["index_digest"]!.GetValue<string>() ==
                    candidate["oci"]!["index_digest"]!.GetValue<string>(),
                "envelope.retained_identity.index_digest",
                "the declared index digest does not match the retained OCI graph",
                "restore the retained immutable index digest"),
            RejectUnless(
                !candidate["release_authority"]!["deployment_authorized"]!.GetValue<bool>(),
                "envelope.deployment_authority",
                "the retained crosswalk authority no longer withholds deployment authorization",
                "restore the retained deployment_authorized false state"),
            RejectUnless(
                !deploymentAuthority["deployment_authorized"]!.GetValue<bool>(),
                "envelope.deployment_authority",
                "the retained deployment authority record no longer withholds deployment authorization",
                "restore the retained deployment_authorized false state"),
        ];
        return checks.FirstOrDefault(reason => reason is not null);
    }

    // The malformed labels and the absent revision label are re-derived from the retained raw config
    // objects, so an omitted, normalized, or synthesized provenance fact cannot pass declaratively.
    private static string? RejectDispositionDefects(
        JsonObject envelope,
        string selectedEvidenceRoot)
    {
        JsonObject defects = envelope["retained_provenance_defects"]!.AsObject();
        if (!HasExactProperties(
                defects,
                ["absent_labels", "malformed_labels", "observed_config_revision", "verification"]))
        {
            return DispositionReason(
                "envelope.retained_provenance_defects",
                "the retained provenance defect record does not carry exactly its required fields",
                "record the malformed labels, the absent revision label, and the verification method");
        }

        if (defects["observed_config_revision"] is not null)
        {
            return DispositionReason(
                "envelope.retained_provenance_defects.observed_config_revision",
                "a revision label was synthesized for a candidate whose configs carry none",
                "restore observed_config_revision to null; the absent revision label is a retained failure");
        }

        if (!HasExactProperties(defects["verification"]!.AsObject(), ["method", "result"])
            || defects["verification"]!["result"]!.GetValue<string>() != "reproduced"
            || string.IsNullOrWhiteSpace(defects["verification"]!["method"]!.GetValue<string>()))
        {
            return DispositionReason(
                "envelope.retained_provenance_defects.verification",
                "the defect verification record does not state a reproduced re-derivation method",
                "re-derive both raw config label sets and record the method and reproduced result");
        }

        JsonObject[] malformed = defects["malformed_labels"]!.AsArray()
            .Select(item => item!.AsObject()).ToArray();
        JsonObject[] absent = defects["absent_labels"]!.AsArray()
            .Select(item => item!.AsObject()).ToArray();
        (string Platform, string ConfigFile)[] platformDefinitions = JsonNode.Parse(
                ReadEvidenceFile(selectedEvidenceRoot, "index.raw"))!["manifests"]!.AsArray()
            .Select(item => item!["platform"]!.AsObject())
            .Select(platform =>
            {
                string operatingSystem = platform["os"]!.GetValue<string>();
                string architecture = platform["architecture"]!.GetValue<string>();
                return (
                    operatingSystem + "/" + architecture,
                    "child-" + operatingSystem + "-" + architecture + ".config.raw");
            })
            .OrderBy(item => item.Item1, StringComparer.Ordinal)
            .ToArray();
        if (malformed.Length != platformDefinitions.Length * MalformedProvenanceLabels.Length
            || malformed.GroupBy(
                    item => item["platform"]!.GetValue<string>() + "\n" +
                        item["label"]!.GetValue<string>(),
                    StringComparer.Ordinal)
                .Any(group => group.Count() != 1))
        {
            return DispositionReason(
                "envelope.retained_provenance_defects.malformed_labels",
                "the malformed-label record does not carry exactly one row per platform and required label",
                "record all six retained malformed label values verbatim and no fabricated row");
        }

        if (absent.Length != platformDefinitions.Length
            || absent.GroupBy(
                    item => item["platform"]!.GetValue<string>(),
                    StringComparer.Ordinal)
                .Any(group => group.Count() != 1))
        {
            return DispositionReason(
                "envelope.retained_provenance_defects.absent_labels",
                "the absent-label record does not carry exactly one row per platform",
                "record the absent revision label for both retained platform configs and no fabricated row");
        }

        foreach ((string platform, string configFile) in platformDefinitions)
        {
            JsonObject labels = JsonNode.Parse(ReadEvidenceFile(selectedEvidenceRoot, configFile))!
                .AsObject()["config"]!["Labels"]!.AsObject();
            JsonObject? absentEntry = absent.SingleOrDefault(item =>
                item["platform"]!.GetValue<string>() == platform);
            if (labels.ContainsKey(RevisionLabel)
                || absentEntry is null
                || !HasExactProperties(absentEntry, ["config_file", "label", "platform"])
                || absentEntry["config_file"]!.GetValue<string>() != configFile
                || absentEntry["label"]!.GetValue<string>() != RevisionLabel)
            {
                return DispositionReason(
                    "envelope.retained_provenance_defects.absent_labels",
                    "the absent revision label for a retained platform config is missing or misdescribed",
                    "record the absent revision label re-derived from the retained raw config object");
            }

            foreach (string label in MalformedProvenanceLabels)
            {
                JsonObject? entry = malformed.SingleOrDefault(item =>
                    item["platform"]!.GetValue<string>() == platform
                    && item["label"]!.GetValue<string>() == label);
                if (entry is null
                    || !HasExactProperties(entry, ["config_file", "label", "platform", "retained_value"])
                    || entry["config_file"]!.GetValue<string>() != configFile
                    || labels[label] is not JsonValue retained
                    || retained.GetValue<string>() != MalformedLabelValue
                    || entry["retained_value"]!.GetValue<string>() != retained.GetValue<string>())
                {
                    return DispositionReason(
                        "envelope.retained_provenance_defects.malformed_labels",
                        "a retained malformed label value was omitted, normalized, or does not match its raw config object",
                        "record each malformed label exactly as the retained raw config object holds it");
                }
            }
        }

        return null;
    }

    private static string? RejectDispositionRetainedRecords(
        JsonObject envelope,
        JsonObject crosswalk,
        JsonObject subject,
        string selectedEvidenceRoot,
        string repositoryRoot)
    {
        JsonObject verdict = crosswalk["verdict"]!.AsObject();
        string[] blockerIds = envelope["retained_blockers"]!.AsArray()
            .Select(item => item!["id"]!.GetValue<string>()).Order(StringComparer.Ordinal).ToArray();
        if (!JsonNode.DeepEquals(envelope["retained_blockers"], verdict["blockers"])
            || !blockerIds.SequenceEqual(RetainedBlockerIds, StringComparer.Ordinal))
        {
            return DispositionReason(
                "envelope.retained_blockers",
                "the retained blockers were dropped, reworded, or no longer match the frozen crosswalk verdict",
                "carry all three retained blockers verbatim from the frozen crosswalk verdict");
        }

        if (verdict["decision"]!.GetValue<string>() != "fail-closed"
            || verdict["story_may_be_done"]!.GetValue<bool>()
            || subject["proposed_decision"]!.GetValue<string>() != "fail-closed"
            || subject["required_acceptances"]!.AsArray().Count != 3
            || subject["required_acceptances"]!.AsArray().Any(item =>
                item!["status"]!.GetValue<string>() != "missing"))
        {
            return DispositionDriftReason(
                "envelope.retained_verdict",
                "the retained crosswalk verdict or review subject no longer records the fail-closed decision");
        }

        string[] subjectLimitations = subject["limitations"]!.AsArray()
            .Select(item => item!.GetValue<string>()).ToArray();
        string[] limitations = envelope["limitations"]!.AsArray()
            .Select(item => item!.GetValue<string>()).ToArray();
        string[] expectedLimitations = subjectLimitations
            .Concat(DispositionSpecificLimitations)
            .ToArray();
        if (!limitations.SequenceEqual(expectedLimitations, StringComparer.Ordinal))
        {
            return DispositionReason(
                "envelope.limitations",
                "the retained or disposition-specific limitations were dropped, changed, reordered, or extended",
                "carry exactly the retained limitations followed by the three approved disposition limitations");
        }

        JsonObject[] declaredManifests = envelope["retained_checksum_manifests"]!.AsArray()
            .Select(item => item!.AsObject()).ToArray();
        if (declaredManifests.Length != RetainedManifestDefinitions.Length
            || declaredManifests
                .GroupBy(item => item["file"]?.GetValue<string>() ?? string.Empty, StringComparer.Ordinal)
                .Any(group => group.Key.Length == 0 || group.Count() != 1))
        {
            return DispositionReason(
                "envelope.retained_checksum_manifests",
                "the envelope does not declare exactly one row for each retained checksum manifest",
                "declare the outer, core, package, and predecessor checksum manifests");
        }

        foreach ((string manifestFile, int entryCount, string manifestBase) in RetainedManifestDefinitions)
        {
            JsonObject? declared = declaredManifests.SingleOrDefault(item =>
                item["file"]!.GetValue<string>() == manifestFile);
            string basePath = manifestBase switch
            {
                "evidence-root" => selectedEvidenceRoot,
                "evidence-root/packages" => ResolveWithin(selectedEvidenceRoot, "packages"),
                _ => repositoryRoot,
            };
            if (declared is null
                || !HasExactProperties(declared, ["base", "entries", "file"])
                || declared["base"]!.GetValue<string>() != manifestBase
                || declared["entries"]!.GetValue<int>() != entryCount)
            {
                return DispositionReason(
                    "retained_checksum_manifest." + manifestFile,
                    "the declared checksum manifest inventory does not match the retained manifest",
                    "declare the retained manifest file, base directory, and entry count");
            }

            if (!RetainedManifestStillVerifies(
                selectedEvidenceRoot,
                manifestFile,
                basePath,
                entryCount))
            {
                return DispositionDriftReason(
                    "retained_checksum_manifest." + manifestFile,
                    "a retained checksum entry no longer matches its file");
            }
        }

        return null;
    }

    // EvidenceDirectoryHasNoUnlistedFiles is reachable only through EvaluateClosure, which
    // short-circuits on ApprovedSourceSha, so the selected 80d12ef5 tree is never inventory-checked
    // there. Without this, a planted file -- including a forged receipt -- survives with every
    // retained checksum entry still verifying.
    private static string? RejectSelectedEvidenceInventory(string selectedEvidenceRoot)
    {
        HashSet<string> listed = new(StringComparer.Ordinal) { "evidence-sha256.txt" };
        foreach (string manifest in new[] { "evidence-sha256.txt", "evidence-core-sha256.txt" })
        {
            foreach (string entry in ParseChecksumManifest(
                ReadEvidenceFile(selectedEvidenceRoot, manifest)).Keys)
            {
                listed.Add(entry.Replace('\\', '/'));
            }
        }

        string[] actualFiles = Directory.GetFiles(selectedEvidenceRoot, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(selectedEvidenceRoot, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] actualDirectories = Directory
            .GetDirectories(selectedEvidenceRoot, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(selectedEvidenceRoot, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (!actualDirectories.SequenceEqual(["packages"], StringComparer.Ordinal))
        {
            return DispositionReason(
                "selected_evidence.directory_inventory",
                "the selected evidence tree contains a directory outside its closed inventory",
                "remove the planted directory; the retained packet admits only the packages directory");
        }

        if (!actualFiles.All(listed.Contains) || actualFiles.Length != listed.Count)
        {
            return DispositionReason(
                "selected_evidence.file_inventory",
                "the selected evidence tree contains a file that no retained checksum manifest lists",
                "remove the planted file; receipts and new artifacts belong outside the hashed evidence tree");
        }

        return null;
    }

    private static string? RejectDispositionContracts(
        JsonObject envelope,
        JsonObject crosswalk,
        string repositoryRoot)
    {
        JsonObject boundary = envelope["successor_boundary"]!.AsObject();
        JsonObject acceptance = envelope["acceptance_contract"]!.AsObject();
        JsonObject verification = envelope["verification"]!.AsObject();
        if (!HasExactProperties(
            boundary,
            [
                "authorizes_consumer_migration",
                "authorizes_deployment",
                "authorizes_parties_8_6_or_g5",
                "closes_fr36_deployed_parity",
                "corrective_release_owner",
                "depends_on_corrective_release",
                "positive_deployed_runtime_parity_owner",
                "reopens_story_1_20_or_3_12",
            ]))
        {
            return DispositionReason(
                "envelope.successor_boundary",
                "the successor boundary does not carry exactly its required fields",
                "declare the successor owners and every authorization boundary flag");
        }

        string[] boundaryFlags =
        [
            "authorizes_consumer_migration",
            "authorizes_deployment",
            "authorizes_parties_8_6_or_g5",
            "closes_fr36_deployed_parity",
            "reopens_story_1_20_or_3_12",
        ];
        foreach (string flag in boundaryFlags)
        {
            if (boundary[flag]!.GetValue<bool>())
            {
                return DispositionReason(
                    "envelope.successor_boundary." + flag,
                    "the disposition claims an authorization or closure a rejected candidate cannot grant",
                    "restore the boundary flag to false; Story 3.15 owns positive parity and deployment stays unauthorized");
            }
        }

        byte[] rosterBytes = ReadEvidenceFile(
            repositoryRoot,
            envelope["referenced_evidence"]!["reviewer_roster"]!["file"]!.GetValue<string>());
        string[] contractRoles = acceptance["required_roles"]!.AsArray()
            .Select(item => item!.GetValue<string>()).Order(StringComparer.Ordinal).ToArray();
        string[] contractFields = acceptance["required_receipt_fields"]!.AsArray()
            .Select(item => item!.GetValue<string>()).Order(StringComparer.Ordinal).ToArray();
        string?[] checks =
        [
            RejectUnless(
                !boundary["depends_on_corrective_release"]!.GetValue<bool>(),
                "envelope.successor_boundary.depends_on_corrective_release",
                "the rejected candidate claims the corrective release is still a dependency",
                "restore depends_on_corrective_release to false; Story 3.14 already owns the corrective release"),
            RejectUnless(
                boundary["positive_deployed_runtime_parity_owner"]!.GetValue<string>() == "3.15",
                "envelope.successor_boundary.positive_deployed_runtime_parity_owner",
                "positive deployed-runtime parity is not assigned to Story 3.15",
                "assign positive deployed-runtime parity to Story 3.15"),
            RejectUnless(
                boundary["corrective_release_owner"]!.GetValue<string>() == "3.14",
                "envelope.successor_boundary.corrective_release_owner",
                "the corrective release is not assigned to Story 3.14",
                "assign the corrective release to Story 3.14"),
            RejectUnless(
                HasExactProperties(
                    acceptance,
                    [
                        "outside_hashed_evidence",
                        "planning_approval_is_a_receipt",
                        "receipt_location",
                        "receipt_schema",
                        "required_receipt_fields",
                        "required_roles",
                        "reviewer_roster_sha256",
                        "self_declared_role_is_a_receipt",
                        "source_schema",
                    ])
                && contractRoles.SequenceEqual(RequiredRoles, StringComparer.Ordinal)
                && contractFields.SequenceEqual(RequiredReceiptFields, StringComparer.Ordinal)
                && acceptance["receipt_location"]!.GetValue<string>() == DispositionReceiptTemplate
                && acceptance["outside_hashed_evidence"]!.GetValue<bool>()
                && acceptance["receipt_schema"]!.GetValue<string>() == DispositionReceiptSchema
                && acceptance["source_schema"]!.GetValue<string>() == DispositionSourceSchema,
                "envelope.acceptance_contract",
                "the acceptance contract does not require three role-bound receipts outside the hashed evidence",
                "declare the three roles, the frozen receipt fields, and the envelope-addressed receipt location"),
            RejectUnless(
                acceptance["reviewer_roster_sha256"]!.GetValue<string>() == ComputeSha256(rosterBytes)
                && acceptance["reviewer_roster_sha256"]!.GetValue<string>() ==
                    crosswalk["approval_contract"]!["reviewer_roster_sha256"]!.GetValue<string>(),
                "envelope.acceptance_contract.reviewer_roster_sha256",
                "the declared reviewer roster digest does not match the retained roster",
                "bind the packet-owned reviewer roster by its retained digest"),
            RejectUnless(
                !acceptance["planning_approval_is_a_receipt"]!.GetValue<bool>()
                && !acceptance["self_declared_role_is_a_receipt"]!.GetValue<bool>(),
                "envelope.acceptance_contract.receipt_authority",
                "the acceptance contract admits planning approval or a self-declared role as a receipt",
                "declare that planning approval and self-declared roles are never receipts"),
            RejectUnless(
                HasExactProperties(
                    verification,
                    [
                        "external_state_changed",
                        "method",
                        "result",
                        "retained_evidence_changed",
                        "verifier",
                    ])
                && verification["verifier"]!.GetValue<string>() == DispositionVerifierPath
                && !string.IsNullOrWhiteSpace(verification["method"]!.GetValue<string>()),
                "envelope.verification",
                "the verification record does not name its method and platform-owned verifier",
                "record the re-derivation method and the focused verifier that enforces it"),
            RejectUnless(
                verification["result"]!.GetValue<string>() == "verified",
                "envelope.verification.result",
                "the verification result is not the disposition verified state",
                "restore the verification result to verified; a pass verdict is never valid for v3.94.1"),
            RejectUnless(
                !verification["external_state_changed"]!.GetValue<bool>()
                && !verification["retained_evidence_changed"]!.GetValue<bool>(),
                "envelope.verification.mutation",
                "the verification record admits an external or retained-evidence mutation",
                "restore both mutation flags to false; the disposition changes no retained byte"),
        ];
        return checks.FirstOrDefault(reason => reason is not null);
    }

    private static string? RejectDispositionChronology(
        JsonObject envelope,
        JsonObject subject,
        DateTimeOffset validationTime)
    {
        if (!TryParseExplicitOffset(
                envelope["assembled_at"]!.GetValue<string>(),
                out DateTimeOffset assembledAt)
            || !TryParseExplicitOffset(
                subject["created_at"]!.GetValue<string>(),
                out DateTimeOffset subjectCreated)
            || assembledAt < subjectCreated
            || assembledAt > validationTime.AddMinutes(5))
        {
            return DispositionReason(
                "envelope.assembled_at",
                "the envelope assembly time is malformed, precedes the frozen review subject, or lies in the future",
                "record an explicit-offset assembly time at or after the frozen review subject creation time");
        }

        return null;
    }

    private static string? RejectForeignLineage(JsonObject envelope)
    {
        List<string> values = [];
        CollectDispositionStrings(envelope, values);

        return values.Any(value => ForeignLineageTokens.Any(token =>
            value.Contains(token, StringComparison.OrdinalIgnoreCase)))
            ? DispositionReason(
                "envelope.foreign_lineage",
                "an identity-bearing section carries concrete identity material owned by another release lineage",
                "remove the Story 1.20, v3.77.x, or Story 3.14 material; ancestry, tags, and labels are insufficient evidence")
            : null;
    }

    private static void CollectDispositionStrings(JsonNode node, List<string> values)
    {
        switch (node)
        {
            case JsonObject value:
                foreach (KeyValuePair<string, JsonNode?> property in value)
                {
                    if (property.Value is not null)
                    {
                        CollectDispositionStrings(property.Value, values);
                    }
                }

                break;
            case JsonArray value:
                foreach (JsonNode? item in value)
                {
                    if (item is not null)
                    {
                        CollectDispositionStrings(item, values);
                    }
                }

                break;
            case JsonValue value when value.TryGetValue<string>(out string? text):
                values.Add(text);
                break;
            default:
                break;
        }
    }

    private static (int Count, string Rejection) CountDispositionReceipts(
        JsonObject envelope,
        byte[] envelopeBytes,
        string repositoryRoot,
        string dispositionRoot,
        string selectedEvidenceRoot,
        DateTimeOffset validationTime)
    {
        string acceptancesRoot = Path.Combine(dispositionRoot, "acceptances");
        string envelopeHash = ComputeSha256(envelopeBytes);
        string missingDirectory = DispositionReason(
            "acceptance.receipt_directory",
            "no receipt directory addressed by the current envelope digest exists",
            "collect three role-bound receipts under acceptances/<envelope-sha256>; any envelope byte change invalidates them");
        if (!Directory.Exists(acceptancesRoot))
        {
            return (0, missingDirectory);
        }

        string[] acceptanceEntries = Directory.EnumerateFileSystemEntries(acceptancesRoot)
            .Select(Path.GetFileName).Where(name => name is not null).Cast<string>()
            .Order(StringComparer.Ordinal).ToArray();
        if (acceptanceEntries.Length != 1 || acceptanceEntries[0] != envelopeHash)
        {
            return (0, missingDirectory);
        }

        string receiptDirectory = ResolveWithin(
            dispositionRoot,
            envelope["acceptance_contract"]!["receipt_location"]!.GetValue<string>()
                .Replace("{envelope_sha256}", envelopeHash, StringComparison.Ordinal));
        string sourcesDirectory = ResolveWithin(receiptDirectory, "sources");
        string[] expectedNames = RequiredRoles.Select(role => role + ".json")
            .Order(StringComparer.Ordinal).ToArray();
        string[] expectedTopLevel = expectedNames.Append("sources").Order(StringComparer.Ordinal).ToArray();
        if (!Directory.EnumerateFileSystemEntries(receiptDirectory)
                .Select(Path.GetFileName).Where(name => name is not null).Cast<string>()
                .Order(StringComparer.Ordinal)
                .SequenceEqual(expectedTopLevel, StringComparer.Ordinal)
            || !Directory.Exists(sourcesDirectory)
            || Directory.EnumerateDirectories(sourcesDirectory).Any()
            || !Directory.GetFiles(sourcesDirectory, "*", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName).Where(name => name is not null).Cast<string>()
                .Order(StringComparer.Ordinal)
                .SequenceEqual(expectedNames, StringComparer.Ordinal))
        {
            return (0, DispositionReason(
                "acceptance.receipt_set",
                "the receipt directory does not contain exactly one receipt and one durable source per required role",
                "provide exactly three role-named receipts and their three durable sources, with no additional entry"));
        }

        byte[] crosswalkBytes = ReadEvidenceFile(
            repositoryRoot,
            envelope["referenced_evidence"]!["identity_crosswalk"]!["file"]!.GetValue<string>());
        JsonObject crosswalk = JsonNode.Parse(crosswalkBytes)!.AsObject();
        JsonObject subject = JsonNode.Parse(ReadEvidenceFile(
            repositoryRoot,
            envelope["review_subject"]!["file"]!.GetValue<string>()))!.AsObject();
        DateTimeOffset subjectCreated = ParseVerifiedExplicitOffset(
            subject["created_at"]!.GetValue<string>());
        DateTimeOffset assembledAt = ParseVerifiedExplicitOffset(
            envelope["assembled_at"]!.GetValue<string>());

        JsonObject roster = LoadReviewerRoster(crosswalk, selectedEvidenceRoot, subjectCreated);
        string[] envelopeLimitations = envelope["limitations"]!.AsArray()
            .Select(item => item!.GetValue<string>()).ToArray();
        string expectedScope =
            "Story 3.13 v3.94.1 rejected-non-authorizing evidence disposition for " + envelopeHash;
        List<string> acceptedRoles = [];
        string firstRejection = string.Empty;
        foreach (string name in expectedNames)
        {
            string? rejection = RejectDispositionReceipt(
                name,
                receiptDirectory,
                roster,
                envelopeLimitations,
                expectedScope,
                envelopeHash,
                assembledAt,
                validationTime);
            if (rejection is null)
            {
                acceptedRoles.Add(name);
            }
            else if (firstRejection.Length == 0)
            {
                firstRejection = rejection;
            }
        }

        return (acceptedRoles.Count, firstRejection);
    }

    private static string? RejectDispositionReceipt(
        string name,
        string receiptDirectory,
        JsonObject roster,
        string[] envelopeLimitations,
        string expectedScope,
        string envelopeHash,
        DateTimeOffset assembledAt,
        DateTimeOffset validationTime)
    {
        JsonObject? receipt;
        try
        {
            receipt = JsonNode.Parse(ReadEvidenceFile(receiptDirectory, name)) as JsonObject;
        }
        catch (JsonException)
        {
            receipt = null;
        }

        if (receipt is null)
        {
            // Distinct from the field-shape failure below: this path is reached only when the receipt
            // bytes do not parse as a JSON object at all. Sharing one code with the schema check left
            // no test able to discriminate the two rules.
            return DispositionReason(
                "acceptance.receipt.json",
                "a receipt is not parseable as a JSON object",
                "re-issue the receipt as well-formed JSON in the frozen Story 3.13 receipt schema");
        }

        if (!HasExactProperties(receipt, RequiredReceiptFields)
            || !DocumentIsSupportSafe(receipt)
            || receipt["schema"]!.GetValue<string>() != DispositionReceiptSchema
            || RequiredReceiptFields.Where(field => field != "durable_source")
                .Any(field => !HasReceiptValue(receipt[field])))
        {
            return DispositionReason(
                "acceptance.receipt.schema",
                "a receipt does not carry exactly the frozen receipt fields with support-safe values",
                "re-issue the receipt against the frozen Story 3.13 acceptance-receipt schema");
        }

        string role = receipt["role"]!.GetValue<string>();
        string reviewer = receipt["reviewer_identity"]!.GetValue<string>();
        if (name != role + ".json" || !RequiredRoles.Contains(role, StringComparer.Ordinal))
        {
            return DispositionReason(
                "acceptance.receipt.role_filename",
                "a receipt filename does not bind the role it declares",
                "name each receipt after the required role it carries");
        }

        if (roster["roles"]![role] is not JsonArray authorized
            || !authorized.Select(item => item!.GetValue<string>())
                .Contains(reviewer, StringComparer.Ordinal))
        {
            return DispositionReason(
                "acceptance.roster.reviewer_identity",
                "a receipt names a reviewer the packet-bound owner-role registry does not authorize",
                "collect the receipt from the rostered owner; a self-declared role is never a receipt");
        }

        if (receipt["decision"]!.GetValue<string>() != "accepted")
        {
            return DispositionReason(
                "acceptance.receipt.decision",
                "a receipt does not record an accepted decision",
                "collect an explicit acceptance of the rejected-non-authorizing disposition");
        }

        if (receipt["subject_sha256"]!.GetValue<string>() != SelectedReviewSubjectSha256)
        {
            return DispositionDriftReason(
                "acceptance.receipt.subject_sha256",
                "a receipt binds a review-subject digest other than the retained frozen subject");
        }

        if (receipt["accepted_scope"]!.GetValue<string>() != expectedScope)
        {
            return DispositionReason(
                "acceptance.receipt.accepted_scope",
                "a receipt does not accept the rejected-non-authorizing disposition for the current envelope",
                "accept the exact disposition scope bound to the current envelope digest");
        }

        if (!receipt["accepted_limitations"]!.AsArray()
                .Select(item => item!.GetValue<string>())
                .SequenceEqual(envelopeLimitations, StringComparer.Ordinal))
        {
            return DispositionReason(
                "acceptance.receipt.accepted_limitations",
                "a receipt does not accept the exact retained limitations the envelope carries",
                "accept every retained limitation verbatim");
        }

        JsonObject durableSource = receipt["durable_source"]!.AsObject();

        // A GitHub-identified reviewer must be evidenced by a GitHub-minted comment; only the
        // tooling-attested test-architect role may rest on a bmad record. The previous contract
        // accepted a generic "retained-immutable-external-record" for every role, which no real
        // GitHub artifact could ever satisfy and which therefore only a fixture could produce.
        string expectedSourceKind = reviewer.StartsWith("github:", StringComparison.Ordinal)
            ? "github-issue-comment"
            : "bmad-test-architect-record";
        if (!HasExactProperties(durableSource, ["kind", "path", "sha256"])
            || durableSource["kind"]!.GetValue<string>() != expectedSourceKind
            || durableSource["path"]!.GetValue<string>() != "sources/" + role + ".json")
        {
            return DispositionReason(
                "acceptance.receipt.durable_source",
                "a receipt cites a durable source of the wrong kind for its rostered reviewer identity",
                "cite the retained durable source; a planning artifact is never a receipt source");
        }

        byte[] sourceBytes = ReadEvidenceFile(receiptDirectory, durableSource["path"]!.GetValue<string>());

        // The receipt binds the exact bytes of the record it cites. Without this the retained source
        // could be re-emitted or replaced after the fact and the receipt would still read as valid.
        if (durableSource["sha256"]!.GetValue<string>() != ComputeSha256(sourceBytes))
        {
            return DispositionReason(
                "acceptance.receipt.durable_source",
                "a receipt does not bind the retained bytes of the durable source record it cites",
                "re-declare durable_source.sha256 over the retained source record bytes");
        }

        JsonObject? sourceRecord;
        try
        {
            sourceRecord = JsonNode.Parse(sourceBytes) as JsonObject;
        }
        catch (JsonException)
        {
            sourceRecord = null;
        }

        if (sourceRecord is null)
        {
            return DispositionReason(
                "acceptance.source.record",
                "a durable source record is not a JSON object in the frozen source schema",
                "re-issue the durable source record bound to the same role, envelope, and repository");
        }

        string[] expectedSourceProperties = expectedSourceKind == "github-issue-comment"
            ?
            [
                "accepted_limitations",
                "accepted_scope",
                "captured_at",
                "comment",
                "decision",
                "repository",
                "reviewer_identity",
                "role",
                "schema",
                "subject_sha256",
            ]
            :
            [
                "accepted_limitations",
                "accepted_scope",
                "captured_at",
                "decision",
                "repository",
                "reviewer_identity",
                "role",
                "schema",
                "subject_sha256",
            ];
        if (!HasExactProperties(sourceRecord, expectedSourceProperties)
            || !DocumentIsSupportSafe(sourceRecord)
            || sourceRecord["schema"]!.GetValue<string>() != DispositionSourceSchema
            || sourceRecord["repository"]!.GetValue<string>() != ExpectedRepository
            || sourceRecord["role"]!.GetValue<string>() != role)
        {
            return DispositionReason(
                "acceptance.source.record",
                "a durable source record does not reproduce its receipt binding",
                "re-issue the durable source record bound to the same role, envelope, and repository");
        }

        if (sourceRecord["reviewer_identity"]!.GetValue<string>() != reviewer)
        {
            return DispositionReason(
                "acceptance.source.reviewer_identity",
                "a receipt and its durable source record name different reviewers",
                "issue the durable source record for the same rostered reviewer the receipt names");
        }

        if (sourceRecord["subject_sha256"]!.GetValue<string>() != SelectedReviewSubjectSha256)
        {
            return DispositionDriftReason(
                "acceptance.source.subject_sha256",
                "a durable source record binds a review-subject digest other than the retained frozen subject");
        }

        if (sourceRecord["decision"]!.GetValue<string>() != "accepted"
            || sourceRecord["accepted_scope"]!.GetValue<string>() != expectedScope
            || !JsonNode.DeepEquals(sourceRecord["accepted_limitations"], receipt["accepted_limitations"])
            || sourceRecord["captured_at"]!.GetValue<string>() !=
                receipt["accepted_at"]!.GetValue<string>())
        {
            return DispositionReason(
                "acceptance.source.decision",
                "a durable source record does not reproduce the receipt decision, scope, limitations, or timestamp",
                "re-issue the durable source record from the same acceptance event");
        }

        if (expectedSourceKind == "github-issue-comment")
        {
            JsonObject comment = sourceRecord["comment"]!.AsObject();
            if (!HasExactProperties(
                    comment,
                    [
                        "author_association",
                        "body",
                        "created_at",
                        "html_url",
                        "id",
                        "issue_url",
                        "updated_at",
                        "url",
                        "user",
                    ]))
            {
                return DispositionReason(
                    "acceptance.source.comment",
                    "a retained acceptance comment is not the verbatim GitHub issue-comment record",
                    "retain the unmodified GitHub API comment object for the acceptance");
            }

            // The anchor must be one GitHub actually minted. A hand-authored fragment resolves to a
            // page but proves nothing about authorship, which is exactly what the previous contract
            // required and why no genuine acceptance could satisfy it.
            if (!TryParseIssueCommentAnchor(
                    comment["html_url"]!.GetValue<string>(),
                    out long anchoredCommentId)
                || anchoredCommentId != comment["id"]!.GetValue<long>())
            {
                return DispositionReason(
                    "acceptance.source.comment_anchor",
                    "a retained acceptance comment does not carry a GitHub-minted issue-comment anchor",
                    "cite the #issuecomment-<id> URL GitHub minted for the acceptance comment");
            }

            if (comment["user"]!["login"]!.GetValue<string>() != reviewer["github:".Length..])
            {
                return DispositionReason(
                    "acceptance.source.comment_author",
                    "a retained acceptance comment was not authored by the rostered reviewer",
                    "collect the acceptance comment from the rostered reviewer's own account");
            }

            // GitHub stamps updated_at forward on every edit, so requiring the field to be present
            // proves nothing on its own: an acceptance rewritten after posting is only visible as a
            // disagreement between the two timestamps.
            if (comment["updated_at"]!.GetValue<string>()
                != comment["created_at"]!.GetValue<string>())
            {
                return DispositionReason(
                    "acceptance.source.comment_edited",
                    "a retained acceptance comment was edited after it was posted",
                    "retain an unedited acceptance comment; re-post the acceptance instead of editing it");
            }

            JsonObject? commentAcceptance;
            try
            {
                commentAcceptance = JsonNode.Parse(comment["body"]!.GetValue<string>()) as JsonObject;
            }
            catch (JsonException)
            {
                commentAcceptance = null;
            }

            // The acceptance the reviewer actually published must be the acceptance the receipt
            // claims -- otherwise the retained comment is decoration around an unrelated assertion.
            if (commentAcceptance is null
                || !HasReceiptValue(commentAcceptance["decision"])
                || commentAcceptance["decision"]!.GetValue<string>() != "accepted"
                || commentAcceptance["role"]!.GetValue<string>() != role
                || commentAcceptance["reviewer_identity"]!.GetValue<string>() != reviewer
                || commentAcceptance["subject_sha256"]!.GetValue<string>() != SelectedReviewSubjectSha256
                || commentAcceptance["accepted_scope"]!.GetValue<string>() != expectedScope
                || !JsonNode.DeepEquals(
                    commentAcceptance["accepted_limitations"],
                    receipt["accepted_limitations"])
                || comment["created_at"]!.GetValue<string>()
                    != sourceRecord["captured_at"]!.GetValue<string>())
            {
                return DispositionReason(
                    "acceptance.source.comment_body",
                    "a retained acceptance comment body does not carry the acceptance the receipt claims",
                    "post the acceptance JSON as the comment body and retain the comment verbatim");
            }
        }

        if (!TryParseExplicitOffset(
                receipt["accepted_at"]!.GetValue<string>(),
                out DateTimeOffset acceptedAt)
            || acceptedAt < assembledAt
            || acceptedAt > validationTime.AddMinutes(5))
        {
            return DispositionReason(
                "acceptance.receipt.accepted_at",
                "a receipt timestamp precedes the envelope it accepts or lies in the future",
                "record an explicit-offset acceptance time at or after the envelope assembly time");
        }

        return null;
    }

    /// <summary>
    /// Parses the comment id out of a GitHub-minted issue-comment anchor for this repository.
    /// </summary>
    /// <param name="url">The candidate <c>html_url</c> value.</param>
    /// <param name="commentId">The anchored comment id when parsing succeeds.</param>
    /// <returns><see langword="true"/> when the URL is a GitHub-minted issue-comment anchor.</returns>
    private static bool TryParseIssueCommentAnchor(string url, out long commentId)
    {
        commentId = 0;
        string prefix = "https://github.com/" + ExpectedRepository + "/issues/";
        const string fragment = "#issuecomment-";
        if (!url.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        int fragmentIndex = url.IndexOf(fragment, StringComparison.Ordinal);
        if (fragmentIndex <= prefix.Length)
        {
            return false;
        }

        string issueNumber = url[prefix.Length..fragmentIndex];
        string id = url[(fragmentIndex + fragment.Length)..];
        return issueNumber.All(char.IsAsciiDigit)
            && id.Length > 0
            && id.All(char.IsAsciiDigit)
            && long.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out commentId);
    }

    private static string? RejectDispositionManifest(string dispositionRoot, string envelopeHash)
    {
        string manifestRejection = DispositionReason(
            "disposition.manifest",
            "the disposition checksum manifest does not close recursively over the directory",
            "regenerate disposition-sha256.txt over every file in the disposition directory");

        // An empty or malformed manifest is a manifest defect, not an internal fault, so it must
        // surface under its own diagnostic rather than as internal.exception.
        Dictionary<string, string> entries;
        try
        {
            entries = ParseChecksumManifest(ReadEvidenceFile(dispositionRoot, DispositionManifestFile));
        }
        catch (InvalidDataException)
        {
            return manifestRejection;
        }

        HashSet<string> allowedFiles = new(StringComparer.Ordinal) { DispositionEnvelopeFile };
        foreach (string role in RequiredRoles)
        {
            allowedFiles.Add("acceptances/" + envelopeHash + "/" + role + ".json");
            allowedFiles.Add("acceptances/" + envelopeHash + "/sources/" + role + ".json");
        }

        HashSet<string> allowedDirectories = new(StringComparer.Ordinal)
        {
            "acceptances",
            "acceptances/" + envelopeHash,
            "acceptances/" + envelopeHash + "/sources",
        };
        string[] actual = DispositionFilesUnder(dispositionRoot);
        string[] actualDirectories = Directory.GetDirectories(
                dispositionRoot,
                "*",
                SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(dispositionRoot, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();
        return actual.All(allowedFiles.Contains)
            && actualDirectories.All(allowedDirectories.Contains)
            && actual.SequenceEqual(entries.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal)
            && entries.All(entry => ComputeSha256(ResolveWithin(dispositionRoot, entry.Key)) == entry.Value)
            ? null
            : manifestRejection;
    }

    private static string[] DispositionFilesUnder(string dispositionRoot) =>
        Directory.GetFiles(dispositionRoot, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(dispositionRoot, path).Replace('\\', '/'))
            .Where(relative => relative != DispositionManifestFile)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static bool RetainedManifestStillVerifies(
        string evidenceRoot,
        string manifestFile,
        string basePath,
        int expectedEntries)
    {
        try
        {
            Dictionary<string, string> entries = ParseChecksumManifest(
                ReadEvidenceFile(evidenceRoot, manifestFile));
            return entries.Count == expectedEntries
                && entries.All(entry => ComputeSha256(ResolveWithin(basePath, entry.Key)) == entry.Value);
        }
        catch (Exception exception) when (
            exception is ArgumentException
            or InvalidDataException
            or IOException
            or NotSupportedException
            or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static byte[] CanonicalDispositionBytes(JsonNode node)
    {
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(
            CanonicalizeJson(node),
            CanonicalDispositionJsonOptions);
        byte[] canonical = new byte[json.Length + 1];
        json.CopyTo(canonical, 0);
        canonical[^1] = (byte)'\n';
        return canonical;
    }

    private static bool FileContentBindingMatches(
        JsonObject binding,
        string repositoryRoot,
        string expectedRelativePath)
    {
        if (binding["file"]!.GetValue<string>() != expectedRelativePath)
        {
            return false;
        }

        byte[] bytes = ReadEvidenceFile(repositoryRoot, expectedRelativePath);
        return binding["size"]!.GetValue<int>() == bytes.Length
            && binding["sha256"]!.GetValue<string>() == ComputeSha256(bytes);
    }

    private static bool ExactFileBindingMatches(
        JsonNode? node,
        string repositoryRoot,
        string expectedRelativePath) =>
        node is JsonObject binding
        && HasExactProperties(binding, ["file", "sha256", "size"])
        && FileContentBindingMatches(binding, repositoryRoot, expectedRelativePath);

    private static JsonObject LoadDispositionEnvelope(string dispositionRoot) =>
        JsonNode.Parse(ReadEvidenceFile(dispositionRoot, DispositionEnvelopeFile))!.AsObject();

    private static (string CleanupRoot, string Disposition) CopyDisposition(string repositoryRoot)
    {
        string cleanupRoot = Path.Combine(
            Path.GetTempPath(),
            "story-3-13-disposition-" + Guid.NewGuid().ToString("N"));
        string disposition = Path.Combine(cleanupRoot, SelectedReviewSubjectSha256);
        try
        {
            CopyDirectory(Path.Combine(repositoryRoot, DispositionRelativePath), disposition);

            // Collected receipts are addressed by envelope digest, so any test that mutates the
            // envelope would orphan them and trip the closed-inventory check before the clause it
            // means to prove. Copies start receipt-free; CreateDispositionReceipts adds them back.
            string acceptances = Path.Combine(disposition, "acceptances");
            if (Directory.Exists(acceptances))
            {
                Directory.Delete(acceptances, recursive: true);
                RebindDispositionManifest(disposition);
            }

            return (cleanupRoot, disposition);
        }
        catch
        {
            DeleteTemporaryDirectory(cleanupRoot);
            throw;
        }
    }

    private static (string CleanupRoot, string Root, string Disposition, string Evidence)
        CopyDispositionWithEvidence(string repositoryRoot)
    {
        string cleanupRoot = Path.Combine(
            Path.GetTempPath(),
            "story-3-13-frozen-" + Guid.NewGuid().ToString("N"));
        string copiedRoot = Path.Combine(cleanupRoot, "repository");
        try
        {
            CopyDirectory(
                Path.Combine(repositoryRoot, DispositionRelativePath),
                Path.Combine(copiedRoot, DispositionRelativePath));
            CopyDirectory(
                Path.Combine(repositoryRoot, SelectedEvidenceRelativePath),
                Path.Combine(copiedRoot, SelectedEvidenceRelativePath));
            CopyDirectory(
                Path.Combine(repositoryRoot, Story120EvidenceRelativePath),
                Path.Combine(copiedRoot, Story120EvidenceRelativePath));
            string copiedDisposition = Path.Combine(copiedRoot, DispositionRelativePath);
            string copiedAcceptances = Path.Combine(copiedDisposition, "acceptances");
            if (Directory.Exists(copiedAcceptances))
            {
                Directory.Delete(copiedAcceptances, recursive: true);
                RebindDispositionManifest(copiedDisposition);
            }

            foreach (string relative in DispositionSupportingFiles)
            {
                string destination = Path.Combine(copiedRoot, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(Path.Combine(repositoryRoot, relative), destination, overwrite: true);
            }

            return (
                cleanupRoot,
                copiedRoot,
                Path.Combine(copiedRoot, DispositionRelativePath),
                Path.Combine(copiedRoot, SelectedEvidenceRelativePath));
        }
        catch
        {
            DeleteTemporaryDirectory(cleanupRoot);
            throw;
        }
    }

    private static void WriteDispositionEnvelope(string dispositionRoot, JsonObject envelope)
    {
        File.WriteAllBytes(
            Path.Combine(dispositionRoot, DispositionEnvelopeFile),
            CanonicalDispositionBytes(envelope));
        RebindDispositionManifest(dispositionRoot);
    }

    private static void RebindDispositionManifest(string dispositionRoot)
    {
        StringBuilder builder = new();
        foreach (string relative in DispositionFilesUnder(dispositionRoot))
        {
            builder.Append(ComputeSha256(Path.Combine(
                    dispositionRoot,
                    relative.Replace('/', Path.DirectorySeparatorChar))))
                .Append("  ")
                .Append(relative)
                .Append('\n');
        }

        File.WriteAllText(Path.Combine(dispositionRoot, DispositionManifestFile), builder.ToString());
    }

    private static void ReDeclareBinding(JsonObject binding, string absolutePath)
    {
        byte[] bytes = File.ReadAllBytes(absolutePath);
        binding["size"] = bytes.Length;
        binding["sha256"] = ComputeSha256(bytes);
    }

    private static void CreateDispositionReceipts(string dispositionRoot)
    {
        byte[] envelopeBytes = ReadEvidenceFile(dispositionRoot, DispositionEnvelopeFile);
        JsonObject envelope = JsonNode.Parse(envelopeBytes)!.AsObject();
        string envelopeHash = ComputeSha256(envelopeBytes);
        string acceptancesRoot = Path.Combine(dispositionRoot, "acceptances");
        if (Directory.Exists(acceptancesRoot))
        {
            Directory.Delete(acceptancesRoot, recursive: true);
        }

        string receiptDirectory = Path.Combine(acceptancesRoot, envelopeHash);
        string sourcesDirectory = Path.Combine(receiptDirectory, "sources");
        Directory.CreateDirectory(sourcesDirectory);
        DateTimeOffset acceptedAt = ParseVerifiedExplicitOffset(
                envelope["assembled_at"]!.GetValue<string>())
            .AddMinutes(1)
            .ToUniversalTime();
        string acceptedScope =
            "Story 3.13 v3.94.1 rejected-non-authorizing evidence disposition for " + envelopeHash;
        foreach (string role in RequiredRoles)
        {
            string reviewer = role == "test-architect" ? "bmad:murat" : "github:jpiquot";
            bool githubSourced = reviewer.StartsWith("github:", StringComparison.Ordinal);
            string capturedAt = acceptedAt.ToString("O", CultureInfo.InvariantCulture);
            JsonObject source = new()
            {
                ["schema"] = DispositionSourceSchema,
                ["repository"] = ExpectedRepository,
                ["captured_at"] = capturedAt,
                ["role"] = role,
                ["reviewer_identity"] = reviewer,
                ["subject_sha256"] = SelectedReviewSubjectSha256,
                ["decision"] = "accepted",
                ["accepted_scope"] = acceptedScope,
                ["accepted_limitations"] = envelope["limitations"]!.DeepClone(),
            };

            if (githubSourced)
            {
                // Shaped exactly like a `gh api .../issues/comments/<id>` response so the fixture
                // exercises the same parse path a genuinely collected acceptance would.
                long commentId = role == "eventstore-owner" ? 5290564373L : 5290564374L;
                string login = reviewer["github:".Length..];
                JsonObject body = new()
                {
                    ["accepted_limitations"] = envelope["limitations"]!.DeepClone(),
                    ["accepted_scope"] = acceptedScope,
                    ["decision"] = "accepted",
                    ["reviewer_identity"] = reviewer,
                    ["role"] = role,
                    ["schema"] = DispositionReceiptSchema,
                    ["subject_sha256"] = SelectedReviewSubjectSha256,
                };
                string id = commentId.ToString(CultureInfo.InvariantCulture);
                source["comment"] = new JsonObject
                {
                    ["author_association"] = "MEMBER",
                    ["body"] = body.ToJsonString(),
                    ["created_at"] = capturedAt,
                    ["html_url"] = "https://github.com/" + ExpectedRepository +
                        "/issues/324#issuecomment-" + id,
                    ["id"] = commentId,
                    ["issue_url"] = "https://api.github.com/repos/" + ExpectedRepository +
                        "/issues/324",
                    ["updated_at"] = capturedAt,
                    ["url"] = "https://api.github.com/repos/" + ExpectedRepository +
                        "/issues/comments/" + id,
                    ["user"] = new JsonObject { ["login"] = login },
                };
            }

            byte[] sourceBytes = JsonSerializer.SerializeToUtf8Bytes(source);
            File.WriteAllBytes(Path.Combine(sourcesDirectory, role + ".json"), sourceBytes);
            JsonObject receipt = new()
            {
                ["schema"] = DispositionReceiptSchema,
                ["role"] = role,
                ["reviewer_identity"] = reviewer,
                ["accepted_at"] = capturedAt,
                ["durable_source"] = new JsonObject
                {
                    ["kind"] = githubSourced
                        ? "github-issue-comment"
                        : "bmad-test-architect-record",
                    ["path"] = "sources/" + role + ".json",
                    ["sha256"] = ComputeSha256(sourceBytes),
                },
                ["accepted_scope"] = acceptedScope,
                ["accepted_limitations"] = envelope["limitations"]!.DeepClone(),
                ["decision"] = "accepted",
                ["subject_sha256"] = SelectedReviewSubjectSha256,
            };
            File.WriteAllBytes(
                Path.Combine(receiptDirectory, role + ".json"),
                JsonSerializer.SerializeToUtf8Bytes(receipt));
        }

        RebindDispositionManifest(dispositionRoot);
    }

    private static void MutateDispositionReceiptAndSourceField(
        string receiptDirectory,
        string role,
        string field,
        string value)
    {
        byte[] sourceBytes = WriteDispositionSourceRecord(receiptDirectory, role, source =>
            source[field] = value);
        MutateDispositionReceipt(receiptDirectory, role, receipt =>
        {
            receipt[field] = value;
            receipt["durable_source"]!["sha256"] = ComputeSha256(sourceBytes);
        });
    }

    private static void MutateDispositionReceiptAndSource(
        string receiptDirectory,
        string role,
        string reviewerIdentity)
    {
        byte[] sourceBytes = WriteDispositionSourceRecord(receiptDirectory, role, source =>
            source["reviewer_identity"] = reviewerIdentity);
        MutateDispositionReceipt(receiptDirectory, role, receipt =>
        {
            receipt["reviewer_identity"] = reviewerIdentity;
            receipt["durable_source"]!["sha256"] = ComputeSha256(sourceBytes);
        });
    }

    private static void MutateDispositionReceiptAndSourceTimestamps(
        string receiptDirectory,
        string role,
        string timestamp)
    {
        byte[] sourceBytes = WriteDispositionSourceRecord(receiptDirectory, role, source =>
        {
            source["captured_at"] = timestamp;

            // The retained comment carries the same acceptance instant in both of its timestamps, so
            // a timestamp mutation must move them together. Leaving either behind would make these
            // cases fail on the comment-body cross-check or the edited-comment clause instead of the
            // accepted_at range they exist to prove.
            if (source["comment"] is JsonObject comment)
            {
                comment["created_at"] = timestamp;
                comment["updated_at"] = timestamp;
            }
        });
        MutateDispositionReceipt(receiptDirectory, role, receipt =>
        {
            receipt["accepted_at"] = timestamp;
            receipt["durable_source"]!["sha256"] = ComputeSha256(sourceBytes);
        });
    }

    private static void MutateDispositionSourceRecord(
        string receiptDirectory,
        string role,
        Action<JsonObject> mutate)
    {
        byte[] sourceBytes = WriteDispositionSourceRecord(receiptDirectory, role, mutate);
        MutateDispositionReceipt(receiptDirectory, role, receipt =>
            receipt["durable_source"]!["sha256"] = ComputeSha256(sourceBytes));
    }

    private static byte[] WriteDispositionSourceRecord(
        string receiptDirectory,
        string role,
        Action<JsonObject> mutate)
    {
        string sourcePath = Path.Combine(receiptDirectory, "sources", role + ".json");
        JsonObject source = JsonNode.Parse(File.ReadAllBytes(sourcePath))!.AsObject();
        mutate(source);
        byte[] sourceBytes = JsonSerializer.SerializeToUtf8Bytes(source);
        File.WriteAllBytes(sourcePath, sourceBytes);
        return sourceBytes;
    }

    private static void MutateDispositionReceipt(
        string receiptDirectory,
        string role,
        Action<JsonObject> mutate)
    {
        string path = Path.Combine(receiptDirectory, role + ".json");
        JsonObject receipt = JsonNode.Parse(File.ReadAllBytes(path))!.AsObject();
        mutate(receipt);
        File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(receipt));
    }

    // Re-capturing a retained manifest so a drifted file matches must still fail, because the
    // envelope and the frozen review subject both bind the manifest bytes themselves.
    private static void RewriteRetainedCoreManifest(string evidenceRoot)
    {
        RewriteRetainedManifest(evidenceRoot, "evidence-core-sha256.txt");
    }

    private static void RewriteRetainedOuterManifest(string evidenceRoot)
    {
        RewriteRetainedManifest(evidenceRoot, "evidence-sha256.txt");
    }

    private static void RewriteRetainedManifest(string evidenceRoot, string manifestFile)
    {
        Dictionary<string, string> entries = ParseChecksumManifest(
            ReadEvidenceFile(evidenceRoot, manifestFile));
        StringBuilder builder = new();
        foreach (string relative in entries.Keys.Order(StringComparer.Ordinal))
        {
            builder.Append(ComputeSha256(ResolveWithin(evidenceRoot, relative)))
                .Append("  ")
                .Append(relative)
                .Append('\n');
        }

        File.WriteAllText(Path.Combine(evidenceRoot, manifestFile), builder.ToString());
    }

    private static void RewriteRetainedCrosswalkVerdict(string evidenceRoot)
    {
        JsonObject crosswalk = JsonNode.Parse(
            ReadEvidenceFile(evidenceRoot, "identity-crosswalk.json"))!.AsObject();
        crosswalk["verdict"]!["blockers"]!.AsArray().RemoveAt(0);
        File.WriteAllBytes(
            Path.Combine(evidenceRoot, "identity-crosswalk.json"),
            JsonSerializer.SerializeToUtf8Bytes(crosswalk));
    }

    /// <summary>
    /// Gets receipt field names for negative tests.
    /// </summary>
    public static IEnumerable<object[]> ReceiptFieldNames =>
        RequiredReceiptFields.Select(receiptField => new object[] { receiptField });

    private static bool EvaluateClosure(
        JsonObject crosswalk,
        byte[] crosswalkBytes,
        byte[] reviewSubjectBytes,
        string repositoryRoot,
        string evidenceRoot,
        byte[] evidenceCoreManifestBytes,
        byte[] proofPacketBytes,
        string expectedPackageManifestSha256 = ApprovedPackageManifestSha256)
    {
        try
        {
            JsonObject[] candidates = crosswalk["selected_candidates"]!.AsArray()
                .Select(node => node!.AsObject())
                .ToArray();
            JsonObject? parsedCrosswalk = JsonNode.Parse(crosswalkBytes)?.AsObject();
            if (crosswalk["schema"]!.GetValue<string>() !=
                    "hexalith.eventstore.story-3-13-identity-crosswalk/v2"
                || crosswalk["schema_version"]!.GetValue<int>() != 2
                || parsedCrosswalk is null
                || !JsonNode.DeepEquals(parsedCrosswalk, crosswalk)
                || candidates.Length != 1
                || candidates[0]["source"]!["sha"]!.GetValue<string>() != ApprovedSourceSha
                || !ValidateBaselineAndPredecessors(
                    crosswalk,
                    repositoryRoot,
                    evidenceRoot,
                    expectedPackageManifestSha256)
                || !ValidateEvidenceIntegrity(
                    crosswalk,
                    repositoryRoot,
                    evidenceRoot,
                    evidenceCoreManifestBytes,
                    reviewSubjectBytes)
                || !ValidatePackages(
                    crosswalk,
                    repositoryRoot,
                    evidenceRoot,
                    expectedPackageManifestSha256)
                || !ValidatePackageBytes(candidates[0], evidenceRoot)
                || !ValidateRelease(candidates[0], repositoryRoot, evidenceRoot)
                || !ValidateOciGraph(crosswalk, repositoryRoot, evidenceRoot)
                || !ValidateOciProvenance(crosswalk, evidenceRoot)
                || !ValidateRuntimeExecution(crosswalk, repositoryRoot, evidenceRoot)
                || !ValidateRuntimeEquivalence(crosswalk)
                || !ValidateDeploymentAuthority(crosswalk, repositoryRoot, evidenceRoot))
            {
                return false;
            }

            JsonObject verdict = crosswalk["verdict"]!.AsObject();
            JsonObject checks = verdict["checks"]!.AsObject();
            string[] checkNames = checks.Select(check => check.Key).Order(StringComparer.Ordinal).ToArray();
            if (!checkNames.SequenceEqual(ExpectedChecks, StringComparer.Ordinal)
                || checks.Any(check => check.Value?.GetValue<string>() != "pass")
                || verdict["decision"]!.GetValue<string>() != "pass"
                || verdict["story_may_be_done"]!.GetValue<bool>() != true
                || verdict["external_state_changed"]!.GetValue<bool>()
                || verdict["predecessor_state_changed"]!.GetValue<bool>()
                || verdict["blockers"]!.AsArray().Count != 0)
            {
                return false;
            }

            return ValidateAcceptances(
                crosswalk,
                crosswalkBytes,
                reviewSubjectBytes,
                repositoryRoot,
                evidenceRoot,
                evidenceCoreManifestBytes,
                proofPacketBytes);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
            or KeyNotFoundException
            or NullReferenceException
            or ArgumentOutOfRangeException
            or ArgumentException
            or OverflowException
            or FormatException
            or InvalidDataException
            or JsonException
            or IOException
            or NotSupportedException
            or UnauthorizedAccessException
            or TimeoutException)
        {
            return false;
        }
    }

    private static bool ValidatePackages(
        JsonObject crosswalk,
        string repositoryRoot,
        string evidenceRoot,
        string expectedPackageManifestSha256)
    {
        try
        {
            JsonObject approvedIdentity = crosswalk["approved_identity"]!.AsObject();
            JsonObject packages = crosswalk["selected_candidates"]![0]!["packages"]!.AsObject();
            string version = packages["version"]!.GetValue<string>();
            JsonObject[] actualItems = packages["items"]!.AsArray().Select(item => item!.AsObject()).ToArray();
            JsonObject[] expectedItems = JsonNode.Parse(
                ReadEvidenceFile(repositoryRoot, "tools/release-packages.json"))!["packages"]!
                .AsArray().Select(item => item!.AsObject()).ToArray();
            string hashManifestPath = packages["hash_manifest_path"]!.GetValue<string>();
            string hashManifestScope = packages["hash_manifest_scope"]!.GetValue<string>();
            byte[] hashManifestBytes = ReadScopedFile(
                repositoryRoot,
                evidenceRoot,
                hashManifestScope,
                hashManifestPath);
            Dictionary<string, string> retainedHashes = ParseChecksumManifest(
                hashManifestBytes);

            if (approvedIdentity["source_sha"]!.GetValue<string>() != ApprovedSourceSha
                || approvedIdentity["package_version"]!.GetValue<string>() != version
                || approvedIdentity["package_hash_manifest_sha256"]!.GetValue<string>() !=
                    expectedPackageManifestSha256
                || packages["release_manifest_path"]!.GetValue<string>() != "tools/release-packages.json"
                || packages["release_manifest_sha256"]!.GetValue<string>() !=
                    ComputeSha256(Path.Combine(repositoryRoot, "tools", "release-packages.json"))
                || packages["hash_manifest_sha256"]!.GetValue<string>() !=
                    ComputeSha256(hashManifestBytes)
                || packages["hash_manifest_sha256"]!.GetValue<string>() != expectedPackageManifestSha256
                || (expectedPackageManifestSha256 == ApprovedPackageManifestSha256
                    && (hashManifestScope != "repository"
                        || hashManifestPath !=
                        "_bmad-output/implementation-artifacts/evidence/story-1-20/" + ApprovedSourceSha +
                        "/nuget-sha256.txt"
                        || version != ApprovedPackageVersion))
                || packages["expected_count"]!.GetValue<int>() != 14
                || actualItems.Length != 14
                || retainedHashes.Count != 14)
            {
                return false;
            }

            string[] expectedTuples = expectedItems
                .Select(item => item["id"]!.GetValue<string>() + "|" + item["project"]!.GetValue<string>())
                .Order(StringComparer.Ordinal)
                .ToArray();
            string[] actualTuples = actualItems
                .Select(item => item["id"]!.GetValue<string>() + "|" + item["project"]!.GetValue<string>())
                .Order(StringComparer.Ordinal)
                .ToArray();
            return actualTuples.SequenceEqual(expectedTuples, StringComparer.Ordinal)
                && actualTuples.Distinct(StringComparer.Ordinal).Count() == 14
                && actualItems.All(item =>
                {
                    string id = item["id"]!.GetValue<string>();
                    string archive = id + "." + version + ".nupkg";
                    return item["archive"]!.GetValue<string>() == archive
                        && retainedHashes.TryGetValue(archive, out string? hash)
                        && item["sha256"]!.GetValue<string>() == hash;
                });
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
            or NullReferenceException
            or ArgumentException
            or OverflowException
            or InvalidDataException
            or JsonException
            or IOException
            or NotSupportedException
            or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool ValidatePackageBytes(JsonObject candidate, string evidenceRoot)
    {
        try
        {
            JsonObject packages = candidate["packages"]!.AsObject();
            JsonObject verification = packages["byte_verification"]!.AsObject();
            if (verification["result"]!.GetValue<string>() != "pass"
                || verification["recovered_count"]!.GetValue<int>() != 14)
            {
                return false;
            }

            string archiveRoot = Path.TrimEndingDirectorySeparator(
                ResolveWithin(evidenceRoot, verification["archive_root"]!.GetValue<string>()));
            JsonObject[] items = packages["items"]!.AsArray().Select(item => item!.AsObject()).ToArray();
            string[] expectedArchives = items.Select(item => item["archive"]!.GetValue<string>())
                .Order(StringComparer.Ordinal).ToArray();
            string[] actualTopLevelFiles = Directory.GetFiles(archiveRoot, "*", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .Where(name => name is not null)
                .Cast<string>()
                .Order(StringComparer.Ordinal)
                .ToArray();
            // Nested files or subdirectories can smuggle undeclared package payloads past a
            // top-level-only exact-set comparison.
            if (Directory.EnumerateDirectories(archiveRoot).Any()
                || Directory.GetFiles(archiveRoot, "*", SearchOption.AllDirectories).Any(path =>
                    !string.Equals(
                        Path.GetDirectoryName(path),
                        archiveRoot,
                        StringComparison.Ordinal)))
            {
                return false;
            }

            return items.Length == 14
                && actualTopLevelFiles.SequenceEqual(expectedArchives, StringComparer.Ordinal)
                && items.All(item =>
            {
                string archive = item["archive"]!.GetValue<string>();
                string archivePath = ResolveWithin(archiveRoot, archive);
                return item["byte_verification"]!.GetValue<string>() == "pass"
                    && File.Exists(archivePath)
                    && ComputeSha256(archivePath) == item["sha256"]!.GetValue<string>();
            });
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
            or NullReferenceException
            or ArgumentException
            or OverflowException
            or InvalidDataException
            or JsonException
            or IOException
            or NotSupportedException
            or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool ValidateRelease(JsonObject candidate, string repositoryRoot, string evidenceRoot)
    {
        try
        {
            JsonObject packages = candidate["packages"]!.AsObject();
            JsonObject release = candidate["release"]!.AsObject();
            JsonObject oci = candidate["oci"]!.AsObject();
            JsonObject authority = candidate["release_authority"]!.AsObject();
            byte[] evidenceBytes = ReadScopedFile(
                repositoryRoot,
                evidenceRoot,
                release["evidence_scope"]!.GetValue<string>(),
                release["evidence_path"]!.GetValue<string>());
            JsonObject retained = JsonNode.Parse(evidenceBytes)!.AsObject();
            string version = packages["version"]!.GetValue<string>();
            string tag = "v" + version;
            string indexDigest = oci["index_digest"]!.GetValue<string>();
            string authorityHash = authority["record_sha256"]!.GetValue<string>();
            string lineage = ComputeCanonicalLineage(candidate, authorityHash);
            string[] boundFields =
            [
                "semantic_version",
                "semantic_tag",
                "repository",
                "workflow_identity",
                "workflow_name",
                "workflow_path",
                "workflow_run_url",
                "builds_execution_sha",
                "publisher_identity",
                "validator_identity",
                "source_sha",
                "workflow_run",
                "workflow_attempt",
            ];

            string[] retainedFields =
            [
                "schema",
                "repository",
                "workflow_identity",
                "workflow_name",
                "workflow_path",
                "workflow_run_url",
                "workflow_run",
                "workflow_attempt",
                "conclusion",
                "event",
                "head_sha",
                "source_sha",
                "tag_ref",
                "tag_source_sha",
                "semantic_version",
                "semantic_tag",
                "builds_execution_sha",
                "publisher_identity",
                "validator_identity",
                "package_version",
                "package_hash_manifest_sha256",
                "registry",
                "container_repository",
                "index_digest",
                "authority_record_sha256",
                "deployment_action_at",
                "result",
            ];

            long run = release["workflow_run"]!.GetValue<long>();
            int attempt = release["workflow_attempt"]!.GetValue<int>();
            string expectedRunUrl = "https://github.com/" + ExpectedRepository + "/actions/runs/" + run +
                "/attempts/" + attempt;

            return release["verification"]!["result"]!.GetValue<string>() == "pass"
                && release["evidence_sha256"]!.GetValue<string>() == ComputeSha256(evidenceBytes)
                && release["evidence_scope"]!.GetValue<string>() == "evidence"
                && retained["schema"]!.GetValue<string>() ==
                    "hexalith.eventstore.story-3-13-release-provenance/v2"
                && HasExactProperties(retained, retainedFields)
                && DocumentIsSupportSafe(retained)
                && boundFields.All(field => retained[field]!.ToJsonString() == release[field]!.ToJsonString())
                && release["repository"]!.GetValue<string>() == ExpectedRepository
                && release["workflow_identity"]!.GetValue<string>() == "github-actions:release.yml"
                && release["workflow_name"]!.GetValue<string>() == "Release"
                && release["workflow_path"]!.GetValue<string>() == ".github/workflows/release.yml"
                && release["workflow_run_url"]!.GetValue<string>() == expectedRunUrl
                && release["semantic_version"]!.GetValue<string>() == version
                && release["semantic_tag"]!.GetValue<string>() == tag
                && run > 0
                && attempt > 0
                && IsFullSha(release["builds_execution_sha"]!.GetValue<string>())
                && release["builds_execution_sha"]!.GetValue<string>() == ExpectedBuildsSha
                && release["publisher_identity"]!.GetValue<string>() == "github-actions:semantic-release"
                && release["validator_identity"]!.GetValue<string>() == "hexalith-builds:" + ExpectedBuildsSha
                && release["source_sha"]!.GetValue<string>() == ApprovedSourceSha
                && IsFullSha(release["source_sha"]!.GetValue<string>())
                && release["source_exact_match"]!.GetValue<bool>()
                && retained["conclusion"]!.GetValue<string>() == "success"
                && retained["event"]!.GetValue<string>() == "workflow_dispatch"
                && retained["head_sha"]!.GetValue<string>() == ApprovedSourceSha
                && retained["source_sha"]!.GetValue<string>() == ApprovedSourceSha
                && retained["tag_ref"]!.GetValue<string>() == "refs/tags/" + tag
                && retained["tag_source_sha"]!.GetValue<string>() == ApprovedSourceSha
                && retained["package_version"]!.GetValue<string>() == version
                && retained["package_hash_manifest_sha256"]!.GetValue<string>() ==
                    packages["hash_manifest_sha256"]!.GetValue<string>()
                && retained["builds_execution_sha"]!.GetValue<string>() == ExpectedBuildsSha
                && retained["authority_record_sha256"]!.GetValue<string>() == authorityHash
                && retained["index_digest"]!.GetValue<string>() == indexDigest
                && retained["registry"]!.GetValue<string>() == ExpectedRegistry
                && retained["container_repository"]!.GetValue<string>() == ExpectedContainerRepository
                && retained["result"]!.GetValue<string>() == "pass"
                && candidate["lineage_id"]!.GetValue<string>() == lineage;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
            or NullReferenceException
            or ArgumentException
            or OverflowException
            or InvalidDataException
            or JsonException
            or IOException
            or NotSupportedException
            or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool ValidateDeploymentAuthority(
        JsonObject crosswalk,
        string repositoryRoot,
        string evidenceRoot)
    {
        try
        {
            JsonObject candidate = crosswalk["selected_candidates"]![0]!.AsObject();
            JsonObject authority = candidate["release_authority"]!.AsObject();
            JsonObject release = candidate["release"]!.AsObject();
            JsonObject packages = candidate["packages"]!.AsObject();
            JsonObject oci = candidate["oci"]!.AsObject();
            JsonObject releaseEvidence = JsonNode.Parse(ReadScopedFile(
                repositoryRoot,
                evidenceRoot,
                release["evidence_scope"]!.GetValue<string>(),
                release["evidence_path"]!.GetValue<string>()))!.AsObject();
            byte[] recordBytes = ReadScopedFile(
                repositoryRoot,
                evidenceRoot,
                authority["record_scope"]!.GetValue<string>(),
                authority["record_path"]!.GetValue<string>());
            JsonObject retained = JsonNode.Parse(recordBytes)!.AsObject();
            JsonObject scope = retained["scope"]!.AsObject();
            JsonObject roster = LoadReviewerRoster(crosswalk, evidenceRoot);
            string owner = retained["owner"]!.GetValue<string>();
            string[] authorizedReleaseOwners = roster["roles"]!["release-owner"]!.AsArray()
                .Select(item => item!.GetValue<string>()).ToArray();
            JsonObject durableSource = retained["durable_source"]!.AsObject();
            byte[] sourceBytes = ReadScopedFile(
                repositoryRoot,
                evidenceRoot,
                durableSource["scope"]!.GetValue<string>(),
                durableSource["path"]!.GetValue<string>());
            JsonObject sourceRecord = JsonNode.Parse(sourceBytes)!.AsObject();
            string[] recordFields =
            [
                "schema",
                "repository",
                "action",
                "owner",
                "authorized_at",
                "expires_at",
                "rationale",
                "deployment_authorized",
                "scope",
                "lineage_material_sha256",
                "durable_source",
            ];
            string[] scopeFields =
            [
                "source_sha",
                "package_manifest_sha256",
                "package_version",
                "semantic_tag",
                "registry",
                "container_repository",
                "index_digest",
                "platforms",
                "workflow_run",
                "workflow_attempt",
                "builds_execution_sha",
                "publisher_identity",
                "validator_identity",
            ];
            string[] sourceFields =
            [
                "schema",
                "repository",
                "source_url",
                "captured_at",
                "owner",
                "action",
                "decision",
                "scope",
            ];
            string sourceUrl = "https://github.com/" + ExpectedRepository + "/commit/" + ApprovedSourceSha +
                "#story-3-13-deployment-authority";

            return authority["deployment_authorized"]!.GetValue<bool>()
                && authority["verification"]!["result"]!.GetValue<string>() == "pass"
                && authority["record_sha256"]!.GetValue<string>() == ComputeSha256(recordBytes)
                && authority["record_scope"]!.GetValue<string>() == "evidence"
                && retained["schema"]!.GetValue<string>() ==
                    "hexalith.eventstore.story-3-13-deployment-authority/v2"
                && retained["repository"]!.GetValue<string>() == ExpectedRepository
                && HasExactProperties(retained, recordFields)
                && HasExactProperties(scope, scopeFields)
                && DocumentIsSupportSafe(retained)
                && retained["action"]!.GetValue<string>() == "deployed-runtime-identity-acceptance"
                && retained["deployment_authorized"]!.GetValue<bool>()
                && authorizedReleaseOwners.Contains(owner, StringComparer.Ordinal)
                && owner == "github:jpiquot"
                && authority["owner"]!.GetValue<string>() == owner
                && authority["authorized_source_sha"]!.GetValue<string>() == ApprovedSourceSha
                && authority["canonical_lineage_id"]!.GetValue<string>() ==
                    candidate["lineage_id"]!.GetValue<string>()
                && retained["lineage_material_sha256"]!.GetValue<string>() ==
                    ComputeLineageMaterialSha256(candidate)
                && scope["source_sha"]!.GetValue<string>() == ApprovedSourceSha
                && scope["package_manifest_sha256"]!.GetValue<string>() ==
                    packages["hash_manifest_sha256"]!.GetValue<string>()
                && scope["package_version"]!.GetValue<string>() == packages["version"]!.GetValue<string>()
                && scope["semantic_tag"]!.GetValue<string>() == release["semantic_tag"]!.GetValue<string>()
                && scope["registry"]!.GetValue<string>() == ExpectedRegistry
                && scope["container_repository"]!.GetValue<string>() == ExpectedContainerRepository
                && scope["index_digest"]!.GetValue<string>() == oci["index_digest"]!.GetValue<string>()
                && scope["platforms"]!.AsArray().Select(item => item!.GetValue<string>())
                    .Order(StringComparer.Ordinal)
                    .SequenceEqual(["linux/amd64", "linux/arm64"], StringComparer.Ordinal)
                && scope["workflow_run"]!.GetValue<long>() == release["workflow_run"]!.GetValue<long>()
                && scope["workflow_attempt"]!.GetValue<int>() == release["workflow_attempt"]!.GetValue<int>()
                && scope["builds_execution_sha"]!.GetValue<string>() == ExpectedBuildsSha
                && scope["publisher_identity"]!.GetValue<string>() ==
                    release["publisher_identity"]!.GetValue<string>()
                && scope["validator_identity"]!.GetValue<string>() ==
                    release["validator_identity"]!.GetValue<string>()
                && TryParseExplicitOffset(
                    retained["authorized_at"]!.GetValue<string>(),
                    out DateTimeOffset authorizedAt)
                && TryParseExplicitOffset(retained["expires_at"]!.GetValue<string>(), out DateTimeOffset expiresAt)
                && TryParseExplicitOffset(
                    releaseEvidence["deployment_action_at"]!.GetValue<string>(),
                    out DateTimeOffset deploymentActionAt)
                && expiresAt > authorizedAt
                && authorizedAt <= deploymentActionAt
                && deploymentActionAt < expiresAt
                && !string.IsNullOrWhiteSpace(retained["rationale"]!.GetValue<string>())
                && HasExactProperties(durableSource, ["kind", "scope", "path", "sha256"])
                && durableSource["kind"]!.GetValue<string>() == "retained-immutable-external-record"
                && durableSource["scope"]!.GetValue<string>() == "evidence"
                && durableSource["path"]!.GetValue<string>() == "deployment-authority-source.json"
                && durableSource["sha256"]!.GetValue<string>() == ComputeSha256(sourceBytes)
                && sourceRecord["schema"]!.GetValue<string>() ==
                    "hexalith.eventstore.story-3-13-deployment-authority-source/v1"
                && HasExactProperties(sourceRecord, sourceFields)
                && DocumentIsSupportSafe(sourceRecord)
                && sourceRecord["repository"]!.GetValue<string>() == ExpectedRepository
                && sourceRecord["source_url"]!.GetValue<string>() == sourceUrl
                && sourceRecord["owner"]!.GetValue<string>() == owner
                && sourceRecord["action"]!.GetValue<string>() == retained["action"]!.GetValue<string>()
                && sourceRecord["decision"]!.GetValue<string>() == "authorized"
                && JsonNode.DeepEquals(sourceRecord["scope"], scope)
                && TryParseExplicitOffset(
                    sourceRecord["captured_at"]!.GetValue<string>(),
                    out DateTimeOffset capturedAt)
                && capturedAt >= authorizedAt
                && capturedAt <= deploymentActionAt;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
            or NullReferenceException
            or ArgumentException
            or OverflowException
            or FormatException
            or InvalidDataException
            or JsonException
            or IOException
            or NotSupportedException
            or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool ValidateSharedValidatorIdentity(JsonObject sharedValidator, string repositoryRoot)
    {
        string[] fields =
        [
            "path",
            "sha256",
            "builds_gitlink_sha",
            "verification_result",
            "cli_candidate_compatibility",
            "cli_candidate_consequence",
        ];
        string path = sharedValidator["path"]!.GetValue<string>();
        return HasExactProperties(sharedValidator, fields)
            && path == ExpectedOciValidatorPath
            && sharedValidator["builds_gitlink_sha"]!.GetValue<string>() == ExpectedBuildsSha
            && sharedValidator["verification_result"]!.GetValue<string>() == "pass"
            && sharedValidator["cli_candidate_compatibility"]!.GetValue<string>() == "pass"
            && sharedValidator["cli_candidate_consequence"]!.GetValue<string>() ==
                "The semantic release tag is accepted by the pinned validator."
            && sharedValidator["sha256"]!.GetValue<string>() == ExpectedOciValidatorSha256
            && ComputePinnedBuildsToolSha256(repositoryRoot, path) == ExpectedOciValidatorSha256;
    }

    private static bool ValidateOciGraph(JsonObject crosswalk, string repositoryRoot, string evidenceRoot)
    {
        try
        {
            JsonObject oci = crosswalk["selected_candidates"]![0]!["oci"]!.AsObject();
            byte[] indexBytes = ReadEvidenceFile(evidenceRoot, oci["index_raw_file"]!.GetValue<string>());
            string indexDigest = "sha256:" + ComputeSha256(indexBytes);
            string registry = oci["registry"]!.GetValue<string>();
            string repository = oci["repository"]!.GetValue<string>();
            string sourceSha = crosswalk["selected_candidates"]![0]!["source"]!["sha"]!.GetValue<string>();
            string releaseVersion = crosswalk["selected_candidates"]![0]!["release"]!["semantic_version"]!
                .GetValue<string>();
            string? sourceDirectory = Directory.GetParent(evidenceRoot)?.Name;
            JsonObject readback = JsonNode.Parse(ReadEvidenceFile(evidenceRoot, "registry-readback.json"))!.AsObject();
            JsonObject validation = JsonNode.Parse(ReadEvidenceFile(evidenceRoot, "oci-validation.json"))!.AsObject();
            JsonObject[] objectResponses = readback["objects"]!.AsArray()
                .Select(item => item!.AsObject()).ToArray();
            string semanticTag = crosswalk["selected_candidates"]![0]!["release"]!["semantic_tag"]!
                .GetValue<string>();
            string discoveryTag = readback["discovery_tag"]!.GetValue<string>();
            string[] readbackFields =
            [
                "schema",
                "checked_at",
                "repository",
                "discovery_tag",
                "immutable_index_digest",
                "tag_response",
                "digest_response",
                "objects",
                "object_response_metadata_result",
                "tag_and_digest_bytes_identical",
                "shared_validator",
                "config_labels",
                "oci_graph_result",
                "result",
                "scope",
            ];
            string[] validationFields =
            [
                "schema",
                "checked_at",
                "repository",
                "immutable_reference",
                "index_digest",
                "index_size",
                "media_type",
                "platforms",
                "children",
                "raw_index_file",
                "raw_index_sha256",
                "raw_graph_result",
                "response_metadata_result",
                "result",
                "verification",
            ];
            if (registry != ExpectedRegistry
                || repository != ExpectedContainerRepository
                || Path.GetFileName(evidenceRoot) != indexDigest[7..]
                || sourceDirectory != sourceSha
                || objectResponses.Length != 4
                || readback["schema"]!.GetValue<string>() !=
                    "hexalith.eventstore.story-3-13-registry-readback/v2"
                || validation["schema"]!.GetValue<string>() !=
                    "hexalith.eventstore.story-3-13-oci-validation/v2"
                || !HasExactProperties(readback, readbackFields)
                || !HasExactProperties(validation, validationFields)
                || !DocumentIsSupportSafe(readback)
                || !DocumentIsSupportSafe(validation)
                || !TryParseExplicitOffset(readback["checked_at"]!.GetValue<string>(), out _)
                || !TryParseExplicitOffset(validation["checked_at"]!.GetValue<string>(), out _)
                || discoveryTag != semanticTag
                || oci["immutable_reference"]!.GetValue<string>() !=
                    registry + "/" + repository + "@" + indexDigest
                || readback["repository"]!.GetValue<string>() != registry + "/" + repository
                || readback["immutable_index_digest"]!.GetValue<string>() != indexDigest
                || validation["repository"]!.GetValue<string>() != registry + "/" + repository
                || validation["immutable_reference"]!.GetValue<string>() !=
                    registry + "/" + repository + "@" + indexDigest
                || validation["index_digest"]!.GetValue<string>() != indexDigest
                || validation["index_size"]!.GetValue<int>() != indexBytes.Length
                || validation["media_type"]!.GetValue<string>() != OciIndexMediaType
                || !validation["platforms"]!.AsArray().Select(item => item!.GetValue<string>())
                    .SequenceEqual(["linux/amd64", "linux/arm64"], StringComparer.Ordinal)
                || oci["index_raw_file"]!.GetValue<string>() != "index.raw"
                || validation["raw_index_file"]!.GetValue<string>() != "index.raw"
                || validation["raw_index_sha256"]!.GetValue<string>() != indexDigest[7..]
                || validation["raw_graph_result"]!.GetValue<string>() != "pass"
                || validation["response_metadata_result"]!.GetValue<string>() != "pass"
                || validation["result"]!.GetValue<string>() != "pass"
                || !HasExactProperties(validation["verification"]!.AsObject(), ["method", "result", "reason"])
                || validation["verification"]!["method"]!.GetValue<string>() !=
                    "retained-raw-oci-graph-and-response-metadata"
                || validation["verification"]!["result"]!.GetValue<string>() != "pass"
                || string.IsNullOrWhiteSpace(validation["verification"]!["reason"]!.GetValue<string>())
                || readback["object_response_metadata_result"]!.GetValue<string>() != "pass"
                || readback["oci_graph_result"]!.GetValue<string>() != "pass"
                || readback["result"]!.GetValue<string>() != "pass"
                || oci["index_digest"]!.GetValue<string>() != indexDigest
                || oci["index_raw_sha256"]!.GetValue<string>() != indexDigest[7..]
                || oci["index_size"]!.GetValue<int>() != indexBytes.Length
                || oci["index_media_type"]!.GetValue<string>() != OciIndexMediaType
                || !ValidateSharedValidatorIdentity(
                    readback["shared_validator"]!.AsObject(),
                    repositoryRoot)
                || !readback["tag_and_digest_bytes_identical"]!.GetValue<bool>()
                || !ValidateConfigLabelSummaries(
                    readback["config_labels"]!.AsObject(),
                    sourceSha,
                    releaseVersion))
            {
                return false;
            }

            JsonObject index = JsonNode.Parse(indexBytes)!.AsObject();
            if (index["schemaVersion"]!.GetValue<int>() != 2
                || index["mediaType"]!.GetValue<string>() != OciIndexMediaType)
            {
                return false;
            }

            JsonObject[] descriptors = index["manifests"]!.AsArray().Select(item => item!.AsObject()).ToArray();
            JsonObject[] children = oci["children"]!.AsArray().Select(item => item!.AsObject()).ToArray();
            JsonObject[] validationChildren = validation["children"]!.AsArray()
                .Select(item => item!.AsObject()).ToArray();
            string[] platforms = children.Select(child => child["platform"]!.GetValue<string>())
                .Order(StringComparer.Ordinal).ToArray();
            if (children.Length != 2
                || descriptors.Length != 2
                || validationChildren.Length != 2
                || !platforms.SequenceEqual(["linux/amd64", "linux/arm64"], StringComparer.Ordinal)
                || platforms.Distinct(StringComparer.Ordinal).Count() != 2)
            {
                return false;
            }

            foreach (JsonObject child in children)
            {
                string platform = child["platform"]!.GetValue<string>();
                string expectedStem = "child-" + platform.Replace('/', '-');
                if (child["manifest_raw_file"]!.GetValue<string>() != expectedStem + ".manifest.raw"
                    || child["config_raw_file"]!.GetValue<string>() != expectedStem + ".config.raw")
                {
                    return false;
                }

                string[] platformParts = platform.Split('/');
                JsonObject descriptor = descriptors.Single(item =>
                    item["platform"]!["os"]!.GetValue<string>() == platformParts[0]
                    && item["platform"]!["architecture"]!.GetValue<string>() == platformParts[1]);
                if (descriptor["platform"]!.AsObject().ContainsKey("variant")
                    || descriptor["mediaType"]!.GetValue<string>() != OciManifestMediaType
                    || descriptor["digest"]!.GetValue<string>() != child["manifest_digest"]!.GetValue<string>()
                    || descriptor["size"]!.GetValue<int>() != child["manifest_size"]!.GetValue<int>())
                {
                    return false;
                }

                byte[] manifestBytes = ReadEvidenceFile(
                    evidenceRoot,
                    child["manifest_raw_file"]!.GetValue<string>());
                byte[] configBytes = ReadEvidenceFile(evidenceRoot, child["config_raw_file"]!.GetValue<string>());
                if (!BytesMatchDescriptor(manifestBytes, child, "manifest")
                    || !BytesMatchDescriptor(configBytes, child, "config")
                    || child["manifest_media_type"]!.GetValue<string>() != OciManifestMediaType
                    || child["config_media_type"]!.GetValue<string>() != OciConfigMediaType
                    || child["config_platform"]!.GetValue<string>() != platform)
                {
                    return false;
                }

                JsonObject manifestResponse = objectResponses.Single(response =>
                    response["kind"]!.GetValue<string>() == "child-manifest"
                    && response["digest"]!.GetValue<string>() == child["manifest_digest"]!.GetValue<string>());
                JsonObject configResponse = objectResponses.Single(response =>
                    response["kind"]!.GetValue<string>() == "config"
                    && response["digest"]!.GetValue<string>() == child["config_digest"]!.GetValue<string>());
                if (!ObjectResponseBindingMatches(
                        manifestResponse,
                        manifestBytes,
                        child["manifest_digest"]!.GetValue<string>(),
                        OciManifestMediaType,
                        child["manifest_raw_file"]!.GetValue<string>())
                    || !ObjectResponseBindingMatches(
                        configResponse,
                        configBytes,
                        child["config_digest"]!.GetValue<string>(),
                        OciConfigMediaType,
                        child["config_raw_file"]!.GetValue<string>()))
                {
                    return false;
                }

                JsonObject validationChild = validationChildren.Single(item =>
                    item["platform"]!.GetValue<string>() == platform);
                if (!HasExactProperties(
                        validationChild,
                        [
                            "platform",
                            "manifest_digest",
                            "manifest_size",
                            "media_type",
                            "manifest_raw_file",
                            "manifest_raw_sha256",
                            "config_digest",
                            "config_size",
                            "config_media_type",
                            "config_raw_file",
                            "config_raw_sha256",
                        ])
                    || validationChild["manifest_digest"]!.GetValue<string>() !=
                        child["manifest_digest"]!.GetValue<string>()
                    || validationChild["manifest_size"]!.GetValue<int>() !=
                        child["manifest_size"]!.GetValue<int>()
                    || validationChild["media_type"]!.GetValue<string>() != OciManifestMediaType
                    || validationChild["manifest_raw_file"]!.GetValue<string>() !=
                        child["manifest_raw_file"]!.GetValue<string>()
                    || validationChild["manifest_raw_sha256"]!.GetValue<string>() !=
                        child["manifest_raw_sha256"]!.GetValue<string>()
                    || validationChild["config_digest"]!.GetValue<string>() !=
                        child["config_digest"]!.GetValue<string>()
                    || validationChild["config_size"]!.GetValue<int>() !=
                        child["config_size"]!.GetValue<int>()
                    || validationChild["config_media_type"]!.GetValue<string>() != OciConfigMediaType
                    || validationChild["config_raw_file"]!.GetValue<string>() !=
                        child["config_raw_file"]!.GetValue<string>()
                    || validationChild["config_raw_sha256"]!.GetValue<string>() !=
                        child["config_raw_sha256"]!.GetValue<string>()
                    || child["verification"]!.GetValue<string>() != "pass")
                {
                    return false;
                }

                JsonObject manifest = JsonNode.Parse(manifestBytes)!.AsObject();
                JsonObject configDescriptor = manifest["config"]!.AsObject();
                JsonObject config = JsonNode.Parse(configBytes)!.AsObject();
                if (manifest["schemaVersion"]!.GetValue<int>() != 2
                    || manifest["mediaType"]!.GetValue<string>() != OciManifestMediaType
                    || configDescriptor["mediaType"]!.GetValue<string>() != OciConfigMediaType
                    || configDescriptor["digest"]!.GetValue<string>() != child["config_digest"]!.GetValue<string>()
                    || configDescriptor["size"]!.GetValue<int>() != child["config_size"]!.GetValue<int>()
                    || config["os"]!.GetValue<string>() != platformParts[0]
                    || config["architecture"]!.GetValue<string>() != platformParts[1]
                    || !DocumentIsSupportSafe(config))
                {
                    return false;
                }
            }

            byte[] tagBytes = ReadEvidenceFile(evidenceRoot, oci["tag_response_raw_file"]!.GetValue<string>());
            byte[] digestBytes = ReadEvidenceFile(evidenceRoot, oci["digest_response_raw_file"]!.GetValue<string>());
            return oci["tag_and_digest_bytes_identical"]!.GetValue<bool>()
                && tagBytes.SequenceEqual(digestBytes)
                && tagBytes.SequenceEqual(indexBytes)
                && ResponseBindingMatches(
                    oci,
                    readback["tag_response"]!.AsObject(),
                    "tag",
                    tagBytes,
                    indexDigest,
                    discoveryTag)
                && ResponseBindingMatches(
                    oci,
                    readback["digest_response"]!.AsObject(),
                    "digest",
                    digestBytes,
                    indexDigest,
                    indexDigest)
                && oci["verification"]!["result"]!.GetValue<string>() == "pass";
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
            or NullReferenceException
            or ArgumentException
            or OverflowException
            or InvalidDataException
            or JsonException
            or IOException
            or NotSupportedException
            or UnauthorizedAccessException
            or TimeoutException)
        {
            return false;
        }
    }

    private static bool ValidateConfigLabelSummaries(
        JsonObject configLabels,
        string sourceSha,
        string releaseVersion) =>
        configLabels["verification_result"]!.GetValue<string>() == "pass"
        && configLabels["provenance_label_result"]!.GetValue<string>() == "pass"
        && configLabels["exact_source_match"]!.GetValue<bool>()
        && configLabels["approved_source_sha"]!.GetValue<string>() == sourceSha
        && configLabels["revision"]!.GetValue<string>() == sourceSha
        && configLabels["version"]!.GetValue<string>() == releaseVersion;

    private static bool ValidateOciProvenance(JsonObject crosswalk, string evidenceRoot)
    {
        try
        {
            JsonObject oci = crosswalk["selected_candidates"]![0]!["oci"]!.AsObject();
            JsonObject candidate = crosswalk["selected_candidates"]![0]!.AsObject();
            JsonObject expected = oci["provenance_labels"]!.AsObject();
            string[] labelNames =
            [
                "org.opencontainers.image.source",
                "org.opencontainers.image.url",
                "org.opencontainers.image.documentation",
            ];
            if (expected["verification"]!["result"]!.GetValue<string>() != "pass")
            {
                return false;
            }

            string approvedRevision = candidate["source"]!["sha"]!.GetValue<string>();
            string releaseVersion = candidate["release"]!["semantic_version"]!.GetValue<string>();
            string releaseTag = candidate["release"]!["semantic_tag"]!.GetValue<string>();
            string expectedSource = "https://github.com/" + ExpectedRepository;
            string expectedReleaseUrl = expectedSource + "/releases/tag/" + releaseTag;
            string expectedDocumentationUrl = expectedSource + "/blob/" + approvedRevision + "/README.md";
            if (expected["org.opencontainers.image.revision"]!.GetValue<string>() != approvedRevision
                || expected["org.opencontainers.image.version"]!.GetValue<string>() != releaseVersion
                || expected["org.opencontainers.image.source"]!.GetValue<string>() != expectedSource
                || expected["org.opencontainers.image.url"]!.GetValue<string>() != expectedReleaseUrl
                || expected["org.opencontainers.image.documentation"]!.GetValue<string>() !=
                    expectedDocumentationUrl)
            {
                return false;
            }

            foreach (JsonObject child in oci["children"]!.AsArray().Select(item => item!.AsObject()))
            {
                JsonObject config = JsonNode.Parse(ReadEvidenceFile(
                    evidenceRoot,
                    child["config_raw_file"]!.GetValue<string>()))!.AsObject();
                if (!DocumentIsSupportSafe(config))
                {
                    return false;
                }

                JsonObject labels = config["config"]!["Labels"]!.AsObject();
                if (labels["org.opencontainers.image.revision"]!.GetValue<string>() != approvedRevision
                    || labels["org.opencontainers.image.version"]!.GetValue<string>() != releaseVersion)
                {
                    return false;
                }

                foreach (string labelName in labelNames)
                {
                    string value = expected[labelName]!.GetValue<string>();
                    if (labels[labelName]!.GetValue<string>() != value
                        || !Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
                        || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
                        || string.IsNullOrWhiteSpace(uri.Host))
                    {
                        return false;
                    }
                }

                if (labels["org.opencontainers.image.source"]!.GetValue<string>() != expectedSource
                    || labels["org.opencontainers.image.url"]!.GetValue<string>() != expectedReleaseUrl
                    || labels["org.opencontainers.image.documentation"]!.GetValue<string>() !=
                        expectedDocumentationUrl)
                {
                    return false;
                }
            }

            return true;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
            or NullReferenceException
            or ArgumentException
            or OverflowException
            or InvalidDataException
            or JsonException
            or IOException
            or NotSupportedException
            or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool ValidateRuntimeExecution(
        JsonObject crosswalk,
        string repositoryRoot,
        string evidenceRoot)
    {
        try
        {
            JsonObject candidate = crosswalk["selected_candidates"]![0]!.AsObject();
            JsonObject runtime = candidate["runtime"]!.AsObject();
            JsonObject oci = candidate["oci"]!.AsObject();
            JsonObject retained = JsonNode.Parse(ReadEvidenceFile(
                evidenceRoot,
                runtime["citation"]!.GetValue<string>()))!.AsObject();
            JsonObject expectedRetained = Clone(runtime);
            expectedRetained.Remove("citation");
            string[] retainedFields =
            [
                "schema",
                "execution_result",
                "contract_equivalence",
                "result",
                "started_at",
                "ended_at",
                "exit_code",
                "exit_code_verification",
                "contract",
                "tool",
                "command",
                "preflight",
                "platforms",
                "smoke_results",
                "cleanup_check",
                "evidence_completeness",
                "verification",
            ];
            if (!JsonNode.DeepEquals(retained, expectedRetained)
                || runtime["citation"]!.GetValue<string>() != "runtime-verification.json"
                || retained["schema"]!.GetValue<string>() !=
                    "hexalith.eventstore.story-3-13-runtime-verification/v2"
                || !HasExactProperties(retained, retainedFields)
                || !DocumentIsSupportSafe(retained)
                || runtime["execution_result"]!.GetValue<string>() != "pass"
                || runtime["evidence_completeness"]!.GetValue<string>() != "pass"
                || string.IsNullOrWhiteSpace(runtime["cleanup_check"]!.GetValue<string>())
                || runtime["exit_code"]!.GetValue<int>() != 0
                || !HasExactProperties(
                    runtime["exit_code_verification"]!.AsObject(),
                    ["citation", "result", "reason"])
                || runtime["exit_code_verification"]!["citation"]!.GetValue<string>() !=
                    "bounded-smoke-process-result"
                || runtime["exit_code_verification"]!["result"]!.GetValue<string>() != "pass"
                || string.IsNullOrWhiteSpace(
                    runtime["exit_code_verification"]!["reason"]!.GetValue<string>())
                || !TryParseExplicitOffset(runtime["started_at"]!.GetValue<string>(), out DateTimeOffset started)
                || !TryParseExplicitOffset(runtime["ended_at"]!.GetValue<string>(), out DateTimeOffset ended)
                || ended <= started
                || ended - started > TimeSpan.FromSeconds(420))
            {
                return false;
            }

            JsonObject contract = runtime["contract"]!.AsObject();
            if (contract["health_path"]!.GetValue<string>() != "/alive"
                || contract["http_expectation"]!.GetValue<string>() != "2xx-without-redirect"
                || contract["timeout_seconds"]!.GetValue<int>() != 180
                || contract["poll_interval_seconds"]!.GetValue<int>() != 2
                || contract["actual_hosting_environment"]!.GetValue<string>() != "Production"
                || contract["required_hosting_environment"]!.GetValue<string>() != "Production"
                || !ValidateMinimalConfiguration(contract["minimal_configuration"]!.AsObject()))
            {
                return false;
            }

            JsonObject tool = runtime["tool"]!.AsObject();
            if (tool["path"]!.GetValue<string>() != ExpectedSmokeToolPath
                || tool["sha256"]!.GetValue<string>() != ExpectedSmokeToolSha256
                || tool["builds_gitlink_sha"]!.GetValue<string>() != ExpectedBuildsSha
                || tool["identity"]!.GetValue<string>() != "hexalith-builds:" + ExpectedBuildsSha
                || ComputePinnedBuildsToolSha256(repositoryRoot, ExpectedSmokeToolPath) !=
                    ExpectedSmokeToolSha256)
            {
                return false;
            }

            JsonObject command = runtime["command"]!.AsObject();
            string[] expectedArguments =
            [
                ExpectedSmokeToolPath,
                "--image",
                oci["immutable_reference"]!.GetValue<string>(),
                "--timeout-seconds",
                "180",
                "--poll-interval-seconds",
                "2",
                "--hosting-environment",
                "Production",
            ];
            if (command["executable"]!.GetValue<string>() != "python3"
                || !command["digest_pinned"]!.GetValue<bool>()
                || !command["arguments"]!.AsArray().Select(item => item!.GetValue<string>())
                    .SequenceEqual(expectedArguments, StringComparer.Ordinal))
            {
                return false;
            }

            JsonObject preflight = runtime["preflight"]!.AsObject();
            string[] preflightFields =
            [
                "platform",
                "outcome",
                "log",
                "log_sha256",
                "child_digest",
                "started_at",
                "ended_at",
            ];
            if (preflight.ContainsKey("failure_class"))
            {
                preflightFields = [.. preflightFields, "failure_class"];
            }

            if (!HasExactProperties(preflight, preflightFields)
                || !RuntimeFailureClassificationIsValid(preflight)
                || preflight["platform"]!.GetValue<string>() != "linux/arm64"
                || preflight["outcome"]!.GetValue<string>() != "pass"
                || !ValidatePreflightLog(evidenceRoot, preflight, oci, started, ended))
            {
                return false;
            }

            JsonObject[] platforms = runtime["platforms"]!.AsArray().Select(item => item!.AsObject()).ToArray();
            string[] names = platforms.Select(item => item["platform"]!.GetValue<string>())
                .Order(StringComparer.Ordinal).ToArray();
            if (!names.SequenceEqual(["linux/amd64", "linux/arm64"], StringComparer.Ordinal)
                || names.Distinct(StringComparer.Ordinal).Count() != 2)
            {
                return false;
            }

            foreach (JsonObject platform in platforms)
            {
                string[] platformFields =
                [
                    "platform",
                    "child_digest",
                    "observed_runtime_platform",
                    "attempts",
                    "outcome",
                    "cleanup",
                    "log",
                    "log_sha256",
                    "exit_code",
                    "readiness_result",
                    "started_at",
                    "ended_at",
                ];
                if (platform.ContainsKey("failure_class"))
                {
                    platformFields = [.. platformFields, "failure_class"];
                }

                JsonObject child = oci["children"]!.AsArray().Select(item => item!.AsObject()).Single(item =>
                    item["platform"]!.GetValue<string>() == platform["platform"]!.GetValue<string>());
                if (!HasExactProperties(platform, platformFields)
                    || !RuntimeFailureClassificationIsValid(platform)
                    || platform["child_digest"]!.GetValue<string>() != child["manifest_digest"]!.GetValue<string>()
                    || platform["attempts"]!.GetValue<int>() <= 0
                    || platform["outcome"]!.GetValue<string>() != "pass"
                    || platform["cleanup"]!.GetValue<string>() != "pass"
                    || !ValidateRuntimeLog(evidenceRoot, platform, contract, started, ended))
                {
                    return false;
                }
            }

            JsonObject arm64 = platforms.Single(item => item["platform"]!.GetValue<string>() == "linux/arm64");
            if (!TryParseExplicitOffset(preflight["ended_at"]!.GetValue<string>(), out DateTimeOffset preflightEnded)
                || !TryParseExplicitOffset(arm64["started_at"]!.GetValue<string>(), out DateTimeOffset arm64Started)
                || preflightEnded > arm64Started)
            {
                return false;
            }

            JsonObject smokeBinding = runtime["smoke_results"]!.AsObject();
            byte[] smokeBytes = ReadEvidenceFile(evidenceRoot, smokeBinding["path"]!.GetValue<string>());
            JsonObject smokeResults = JsonNode.Parse(smokeBytes)!.AsObject();
            string[] smokeFields =
            [
                "schema",
                "image_repository",
                "tool",
                "command",
                "started_at",
                "ended_at",
                "exit_code",
                "exit_code_verification",
                "platforms",
                "result",
            ];
            if (smokeBinding["path"]!.GetValue<string>() != "smoke-results.json"
                || smokeBinding["sha256"]!.GetValue<string>() != ComputeSha256(smokeBytes)
                || smokeResults["schema"]!.GetValue<string>() !=
                    "hexalith.eventstore.story-3-13-smoke-results/v2"
                || !HasExactProperties(smokeResults, smokeFields)
                || !DocumentIsSupportSafe(smokeResults)
                || smokeResults["image_repository"]!.GetValue<string>() !=
                    ExpectedRegistry + "/" + ExpectedContainerRepository
                || !JsonNode.DeepEquals(smokeResults["tool"], tool)
                || !JsonNode.DeepEquals(smokeResults["command"], command)
                || smokeResults["started_at"]!.GetValue<string>() != runtime["started_at"]!.GetValue<string>()
                || smokeResults["ended_at"]!.GetValue<string>() != runtime["ended_at"]!.GetValue<string>()
                || smokeResults["exit_code"]!.GetValue<int>() != 0
                || !JsonNode.DeepEquals(
                    smokeResults["exit_code_verification"],
                    runtime["exit_code_verification"])
                || !JsonNode.DeepEquals(smokeResults["platforms"], runtime["platforms"])
                || smokeResults["result"]!.GetValue<string>() != "pass")
            {
                return false;
            }

            return true;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
            or NullReferenceException
            or ArgumentException
            or OverflowException
            or InvalidDataException
            or JsonException
            or IOException
            or NotSupportedException
            or UnauthorizedAccessException
            or TimeoutException)
        {
            return false;
        }
    }

    private static bool ValidateRuntimeEquivalence(JsonObject crosswalk)
    {
        JsonObject runtime = crosswalk["selected_candidates"]![0]!["runtime"]!.AsObject();
        JsonObject contract = runtime["contract"]!.AsObject();
        return runtime["contract_equivalence"]!.GetValue<string>() == "pass"
            && runtime["result"]!.GetValue<string>() == "pass"
            && runtime["verification"]!["result"]!.GetValue<string>() == "pass"
            && contract["actual_hosting_environment"]!.GetValue<string>() == "Production"
            && contract["required_hosting_environment"]!.GetValue<string>() == "Production";
    }

    private static bool ValidateAcceptances(
        JsonObject crosswalk,
        byte[] crosswalkBytes,
        byte[] subjectBytes,
        string repositoryRoot,
        string evidenceRoot,
        byte[] coreBytes,
        byte[] proofBytes)
    {
        try
        {
            JsonObject approval = crosswalk["approval_contract"]!.AsObject();
            string[] roles = approval["required_roles"]!.AsArray()
                .Select(item => item!.GetValue<string>()).Order(StringComparer.Ordinal).ToArray();
            string[] fields = approval["required_receipt_fields"]!.AsArray()
                .Select(item => item!.GetValue<string>()).Order(StringComparer.Ordinal).ToArray();
            JsonObject subject = JsonNode.Parse(subjectBytes)!.AsObject();
            if (!TryParseExplicitOffset(subject["created_at"]!.GetValue<string>(), out DateTimeOffset subjectCreated))
            {
                return false;
            }

            JsonObject roster = LoadReviewerRoster(crosswalk, evidenceRoot, subjectCreated);
            if (!approval["outside_hashed_evidence"]!.GetValue<bool>()
                || approval["external_receipt_location"]!.GetValue<string>() != ReceiptDirectoryTemplate
                || !roles.SequenceEqual(RequiredRoles, StringComparer.Ordinal)
                || !fields.SequenceEqual(RequiredReceiptFields, StringComparer.Ordinal)
                || approval["receipt_count"]!.GetValue<int>() != 3
                || approval["verification"]!["result"]!.GetValue<string>() != "pass"
                || roster["schema"]!.GetValue<string>() !=
                    "hexalith.eventstore.story-3-13-reviewer-roster/v1"
                || roster["repository"]!.GetValue<string>() != "Hexalith/Hexalith.EventStore")
            {
                return false;
            }

            if (!ValidateReviewSubject(
                    crosswalk,
                    subject,
                    crosswalkBytes,
                    coreBytes,
                    proofBytes,
                    evidenceRoot))
            {
                return false;
            }

            string subjectHash = ComputeSha256(subjectBytes);
            string receiptLocation = approval["external_receipt_location"]!.GetValue<string>()
                .Replace("{subject_sha256}", subjectHash, StringComparison.Ordinal);
            string receiptDirectory = ResolveWithin(evidenceRoot, receiptLocation);
            if (!Directory.Exists(receiptDirectory))
            {
                return false;
            }

            string sourcesDirectory = ResolveWithin(receiptDirectory, "sources");
            string[] receiptPaths = Directory.GetFiles(receiptDirectory, "*.json", SearchOption.TopDirectoryOnly);
            string[] expectedNames = RequiredRoles.Select(role => role + ".json")
                .Order(StringComparer.Ordinal).ToArray();
            string[] actualTopLevelEntries = Directory.EnumerateFileSystemEntries(receiptDirectory)
                .Select(Path.GetFileName).Where(name => name is not null).Cast<string>()
                .Order(StringComparer.Ordinal).ToArray();
            string[] expectedTopLevelEntries = expectedNames.Append("sources")
                .Order(StringComparer.Ordinal).ToArray();
            if (receiptPaths.Length != 3
                || !receiptPaths.Select(Path.GetFileName).Order(StringComparer.Ordinal)
                    .SequenceEqual(expectedNames, StringComparer.Ordinal)
                || !actualTopLevelEntries.SequenceEqual(expectedTopLevelEntries, StringComparer.Ordinal)
                || !Directory.Exists(sourcesDirectory)
                || Directory.EnumerateDirectories(sourcesDirectory).Any()
                || !Directory.GetFiles(sourcesDirectory, "*", SearchOption.TopDirectoryOnly)
                    .Select(Path.GetFileName).Order(StringComparer.Ordinal)
                    .SequenceEqual(expectedNames, StringComparer.Ordinal))
            {
                return false;
            }

            JsonObject[] receipts = receiptPaths.Select(path => JsonNode.Parse(ReadEvidenceFile(
                    receiptDirectory,
                    Path.GetFileName(path)))!.AsObject())
                .ToArray();
            if (receipts.Any(receipt => !HasExactProperties(receipt, RequiredReceiptFields)
                    || receipt["schema"]!.GetValue<string>() !=
                        "hexalith.eventstore.story-3-13-acceptance-receipt/v1"
                    || !DocumentIsSupportSafe(receipt))
                || receiptPaths.Zip(receipts).Any(pair =>
                    Path.GetFileName(pair.First) != pair.Second["role"]!.GetValue<string>() + ".json")
                || receipts.Select(receipt => receipt["role"]!.GetValue<string>())
                    .Distinct(StringComparer.Ordinal).Count() != 3
                || !receipts.Select(receipt => receipt["role"]!.GetValue<string>())
                    .Order(StringComparer.Ordinal).SequenceEqual(RequiredRoles, StringComparer.Ordinal))
            {
                return false;
            }

            string[] subjectLimitations = subject["limitations"]!.AsArray()
                .Select(item => item!.GetValue<string>()).ToArray();
            string expectedScope = "Story 3.13 deployed-runtime parity closure for " + subjectHash;
            return receipts.All(receipt =>
            {
                string role = receipt["role"]!.GetValue<string>();
                string identity = receipt["reviewer_identity"]!.GetValue<string>();
                string[] acceptedLimitations = receipt["accepted_limitations"]!.AsArray()
                    .Select(item => item!.GetValue<string>()).ToArray();
                JsonObject durableSource = receipt["durable_source"]!.AsObject();
                byte[] sourceBytes = ReadEvidenceFile(
                    receiptDirectory,
                    durableSource["path"]!.GetValue<string>());
                JsonObject sourceRecord = JsonNode.Parse(sourceBytes)!.AsObject();
                string expectedSourceUrl = "https://github.com/" + ExpectedRepository + "/commit/" +
                    ApprovedSourceSha + "#story-3-13-" + subjectHash + "-" + role;
                string[] sourceFields =
                [
                    "schema",
                    "repository",
                    "source_url",
                    "captured_at",
                    "role",
                    "reviewer_identity",
                    "subject_sha256",
                    "decision",
                    "accepted_scope",
                    "accepted_limitations",
                ];
                return RequiredReceiptFields.Where(field => field != "durable_source")
                        .All(field => HasReceiptValue(receipt[field]))
                    && roster["roles"]![role]!.AsArray()
                        .Select(item => item!.GetValue<string>()).Contains(identity, StringComparer.Ordinal)
                    && receipt["decision"]!.GetValue<string>() == "accepted"
                    && receipt["subject_sha256"]!.GetValue<string>() == subjectHash
                    && receipt["accepted_scope"]!.GetValue<string>() == expectedScope
                    && acceptedLimitations.Length > 0
                    && LimitationsContainMutationProhibitions(acceptedLimitations)
                    && acceptedLimitations.SequenceEqual(subjectLimitations, StringComparer.Ordinal)
                    && HasExactProperties(
                        durableSource,
                        ["kind", "path", "sha256"])
                    && durableSource["kind"]!.GetValue<string>() == "retained-immutable-external-record"
                    && durableSource["path"]!.GetValue<string>() == "sources/" + role + ".json"
                    && durableSource["sha256"]!.GetValue<string>() == ComputeSha256(sourceBytes)
                    && sourceRecord["schema"]!.GetValue<string>() ==
                        "hexalith.eventstore.story-3-13-acceptance-source/v1"
                    && HasExactProperties(sourceRecord, sourceFields)
                    && DocumentIsSupportSafe(sourceRecord)
                    && sourceRecord["repository"]!.GetValue<string>() == ExpectedRepository
                    && sourceRecord["source_url"]!.GetValue<string>() == expectedSourceUrl
                    && sourceRecord["role"]!.GetValue<string>() == role
                    && sourceRecord["reviewer_identity"]!.GetValue<string>() == identity
                    && sourceRecord["subject_sha256"]!.GetValue<string>() == subjectHash
                    && sourceRecord["decision"]!.GetValue<string>() == "accepted"
                    && sourceRecord["accepted_scope"]!.GetValue<string>() == expectedScope
                    && JsonNode.DeepEquals(sourceRecord["accepted_limitations"], receipt["accepted_limitations"])
                    && TryParseExplicitOffset(
                        receipt["accepted_at"]!.GetValue<string>(),
                        out DateTimeOffset acceptedAt)
                    && sourceRecord["captured_at"]!.GetValue<string>() ==
                        receipt["accepted_at"]!.GetValue<string>()
                    && acceptedAt >= subjectCreated
                    && acceptedAt <= DateTimeOffset.UtcNow.AddMinutes(5);
            });
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
            or NullReferenceException
            or ArgumentException
            or OverflowException
            or FormatException
            or InvalidDataException
            or JsonException
            or IOException
            or NotSupportedException
            or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool HasReceiptValue(JsonNode? node) => node switch
    {
        JsonArray array => array.Count > 0 && array.All(item =>
            item is JsonValue value && !string.IsNullOrWhiteSpace(value.GetValue<string>())),
        JsonValue value => !string.IsNullOrWhiteSpace(value.GetValue<string>()),
        _ => false,
    };

    private static bool ValidateReviewSubject(
        JsonObject crosswalk,
        JsonObject subject,
        byte[] crosswalkBytes,
        byte[] coreBytes,
        byte[] proofBytes,
        string evidenceRoot)
    {
        JsonObject candidate = crosswalk["selected_candidates"]![0]!.AsObject();
        JsonObject packages = candidate["packages"]!.AsObject();
        JsonObject release = candidate["release"]!.AsObject();
        JsonObject oci = candidate["oci"]!.AsObject();
        JsonObject identity = subject["identity"]!.AsObject();
        string[] subjectRoles = subject["required_acceptances"]!.AsArray()
            .Select(item => item!["role"]!.GetValue<string>()).Order(StringComparer.Ordinal).ToArray();
        string[] subjectLimitations = subject["limitations"]!.AsArray()
            .Select(item => item!.GetValue<string>()).ToArray();
        string[] crosswalkLimitations = crosswalk["limitations"]!.AsArray()
            .Select(item => item!.GetValue<string>()).ToArray();
        JsonObject registryReadback = JsonNode.Parse(ReadEvidenceFile(evidenceRoot, "registry-readback.json"))!
            .AsObject();
        JsonObject runtime = candidate["runtime"]!.AsObject();
        string[] subjectFields =
        [
            "schema",
            "created_at",
            "proposed_decision",
            "identity_crosswalk",
            "evidence_core_manifest",
            "proof_packet",
            "identity",
            "passing_evidence",
            "blockers",
            "limitations",
            "required_acceptances",
        ];
        string[] identityFields =
        [
            "source_sha",
            "package_version",
            "package_hash_manifest_sha256",
            "release_version",
            "workflow_run",
            "authority_record_sha256",
            "index_digest",
            "canonical_lineage_id",
        ];

        return subject["schema"]!.GetValue<string>() == "hexalith.eventstore.story-3-13-review-subject/v2"
            && HasExactProperties(subject, subjectFields)
            && HasExactProperties(identity, identityFields)
            && DocumentIsSupportSafe(subject)
            && subject["proposed_decision"]!.GetValue<string>() == "pass"
            && TryParseExplicitOffset(subject["created_at"]!.GetValue<string>(), out DateTimeOffset createdAt)
            && TryParseExplicitOffset(crosswalk["assembled_at"]!.GetValue<string>(), out DateTimeOffset assembledAt)
            && TryParseExplicitOffset(
                registryReadback["checked_at"]!.GetValue<string>(),
                out DateTimeOffset registryCheckedAt)
            && TryParseExplicitOffset(runtime["ended_at"]!.GetValue<string>(), out DateTimeOffset runtimeEndedAt)
            && createdAt >= assembledAt
            && createdAt >= registryCheckedAt
            && createdAt >= runtimeEndedAt
            && createdAt <= DateTimeOffset.UtcNow.AddMinutes(5)
            && RawBindingMatches(subject, "identity_crosswalk", "identity-crosswalk.json", crosswalkBytes)
            && RawBindingMatches(subject, "evidence_core_manifest", "evidence-core-sha256.txt", coreBytes)
            && RawBindingMatches(subject, "proof_packet", ProofRelativePath, proofBytes)
            && identity["source_sha"]!.GetValue<string>() == ApprovedSourceSha
            && identity["package_version"]!.GetValue<string>() == packages["version"]!.GetValue<string>()
            && identity["package_hash_manifest_sha256"]!.GetValue<string>() ==
                packages["hash_manifest_sha256"]!.GetValue<string>()
            && identity["release_version"]!.GetValue<string>() == release["semantic_version"]!.GetValue<string>()
            && identity["workflow_run"]!.GetValue<long>() == release["workflow_run"]!.GetValue<long>()
            && identity["authority_record_sha256"]!.GetValue<string>() ==
                candidate["release_authority"]!["record_sha256"]!.GetValue<string>()
            && identity["index_digest"]!.GetValue<string>() == oci["index_digest"]!.GetValue<string>()
            && identity["canonical_lineage_id"]!.GetValue<string>() ==
                candidate["lineage_id"]!.GetValue<string>()
            && subject["passing_evidence"]!.AsArray().Count > 0
            && subject["blockers"]!.AsArray().Count == 0
            && subjectLimitations.Length > 0
            && LimitationsContainMutationProhibitions(subjectLimitations)
            && subjectLimitations.SequenceEqual(crosswalkLimitations, StringComparer.Ordinal)
            && subjectRoles.SequenceEqual(RequiredRoles, StringComparer.Ordinal)
            && subject["required_acceptances"]!.AsArray().All(item =>
                item!["status"]!.GetValue<string>() == "required");
    }

    private static bool ValidateActualFailClosedSubject(
        JsonObject crosswalk,
        byte[] crosswalkBytes,
        byte[] subjectBytes,
        byte[] coreBytes,
        byte[] proofBytes,
        byte[] outerManifestBytes,
        string evidenceRoot)
    {
        try
        {
            JsonObject subject = JsonNode.Parse(subjectBytes)!.AsObject();
            JsonObject parsedCrosswalk = JsonNode.Parse(crosswalkBytes)!.AsObject();
            JsonObject candidate = crosswalk["selected_candidates"]![0]!.AsObject();
            JsonObject identity = subject["identity"]!.AsObject();
            JsonObject verdict = crosswalk["verdict"]!.AsObject();
            JsonObject approval = crosswalk["approval_contract"]!.AsObject();
            JsonObject registry = JsonNode.Parse(ReadEvidenceFile(evidenceRoot, "registry-readback.json"))!
                .AsObject();
            JsonObject ociValidation = JsonNode.Parse(ReadEvidenceFile(evidenceRoot, "oci-validation.json"))!
                .AsObject();
            JsonObject runtime = candidate["runtime"]!.AsObject();
            string[] subjectFields =
            [
                "schema",
                "created_at",
                "proposed_decision",
                "identity_crosswalk",
                "evidence_core_manifest",
                "proof_packet",
                "identity",
                "passing_evidence",
                "blockers",
                "limitations",
                "required_acceptances",
            ];
            string[] identityFields =
            [
                "source_sha",
                "package_version",
                "package_hash_manifest_sha256",
                "release_version",
                "workflow_run",
                "authority_record_sha256",
                "index_digest",
                "canonical_lineage_id",
            ];
            Dictionary<string, string> outer = ParseChecksumManifest(outerManifestBytes);
            string[] outerPaths = outer.Keys.ToArray();
            string[] limitations = subject["limitations"]!.AsArray()
                .Select(item => item!.GetValue<string>()).ToArray();
            JsonObject[] required = subject["required_acceptances"]!.AsArray()
                .Select(item => item!.AsObject()).ToArray();

            return JsonNode.DeepEquals(parsedCrosswalk, crosswalk)
                && subject["schema"]!.GetValue<string>() ==
                    "hexalith.eventstore.story-3-13-review-subject/v2"
                && HasExactProperties(subject, subjectFields)
                && HasExactProperties(identity, identityFields)
                && DocumentIsSupportSafe(subject)
                && ExpectedSupportSafeJsonReports.All(path =>
                    JsonEvidenceIsSupportSafe(evidenceRoot, path))
                && ValidatePackageAvailability(crosswalk, evidenceRoot)
                && JsonNode.Parse(ReadEvidenceFile(evidenceRoot, "package-availability.json"))!
                    ["schema"]!.GetValue<string>() ==
                    "hexalith.eventstore.story-3-13-package-availability/v1"
                && JsonNode.Parse(ReadEvidenceFile(evidenceRoot, "package-availability.json"))!
                    ["result"]!.GetValue<string>() == "fail"
                && subject["proposed_decision"]!.GetValue<string>() == "fail-closed"
                && RawBindingMatches(subject, "identity_crosswalk", "identity-crosswalk.json", crosswalkBytes)
                && RawBindingMatches(subject, "evidence_core_manifest", "evidence-core-sha256.txt", coreBytes)
                && RawBindingMatches(subject, "proof_packet", ProofRelativePath, proofBytes)
                && outerPaths.SequenceEqual(ExpectedOuterFiles, StringComparer.Ordinal)
                && outer["evidence-core-sha256.txt"] == ComputeSha256(coreBytes)
                && outer["identity-crosswalk.json"] == ComputeSha256(crosswalkBytes)
                && outer["review-subject.json"] == ComputeSha256(subjectBytes)
                && identity["source_sha"]!.GetValue<string>() == ApprovedSourceSha
                && identity["package_version"]!.GetValue<string>() == ApprovedPackageVersion
                && identity["package_hash_manifest_sha256"]!.GetValue<string>() ==
                    ApprovedPackageManifestSha256
                && identity["release_version"] is null
                && identity["workflow_run"] is null
                && identity["authority_record_sha256"]!.GetValue<string>() ==
                    candidate["release_authority"]!["record_sha256"]!.GetValue<string>()
                && identity["canonical_lineage_id"] is null
                && identity["index_digest"]!.GetValue<string>() == ExpectedIndexDigest
                && candidate["release"]!["semantic_version"] is null
                && candidate["release"]!["workflow_run"] is null
                && candidate["release"]!["source_sha"] is null
                && !candidate["release_authority"]!["deployment_authorized"]!.GetValue<bool>()
                && runtime["citation"]!.GetValue<string>() == "runtime-verification.json"
                && runtime["execution_result"]!.GetValue<string>() == "unverified"
                && runtime["result"]!.GetValue<string>() == "fail"
                && runtime["exit_code"] is null
                && HasExactProperties(
                    runtime["exit_code_verification"]!.AsObject(),
                    ["citation", "result", "reason"])
                && runtime["exit_code_verification"]!["result"]!.GetValue<string>() == "fail"
                && runtime["preflight"]!["outcome"]!.GetValue<string>() == "unverified"
                && runtime["platforms"]!.AsArray().All(item =>
                    item!["outcome"]!.GetValue<string>() == "unverified")
                && runtime["contract_equivalence"]!.GetValue<string>() == "fail"
                && runtime["contract"]!["actual_hosting_environment"]!.GetValue<string>() == "Development"
                && runtime["contract"]!["required_hosting_environment"]!.GetValue<string>() == "Production"
                && registry["object_response_metadata_result"]!.GetValue<string>() == "missing"
                && registry["result"]!.GetValue<string>() == "fail"
                && registry["oci_graph_result"]!.GetValue<string>() == "fail"
                && registry["shared_validator"]!["cli_candidate_compatibility"]!.GetValue<string>() ==
                    "unavailable"
                && registry["shared_validator"]!["verification_result"]!.GetValue<string>() == "pass"
                && registry["shared_validator"]!["cli_candidate_consequence"]!.GetValue<string>() ==
                    "The CLI accepts SemVer tags only; the unchanged validation functions were applied to the immutable proof graph without weakening the contract."
                && ociValidation["schema"]!.GetValue<string>() ==
                    "hexalith.eventstore.story-3-13-oci-validation/v2"
                && HasExactProperties(
                    ociValidation,
                    [
                        "schema",
                        "checked_at",
                        "repository",
                        "immutable_reference",
                        "index_digest",
                        "index_size",
                        "media_type",
                        "platforms",
                        "children",
                        "raw_index_file",
                        "raw_index_sha256",
                        "raw_graph_result",
                        "response_metadata_result",
                        "result",
                        "verification",
                    ])
                && ociValidation["immutable_reference"]!.GetValue<string>() ==
                    registry["repository"]!.GetValue<string>() + "@" + ExpectedIndexDigest
                && ociValidation["response_metadata_result"]!.GetValue<string>() == "missing"
                && ociValidation["result"]!.GetValue<string>() == "fail"
                && ociValidation["verification"]!["result"]!.GetValue<string>() == "fail"
                && JsonNode.Parse(ReadEvidenceFile(evidenceRoot, "smoke-results.json"))!
                    ["result"]!.GetValue<string>() == "fail"
                && JsonNode.Parse(ReadEvidenceFile(evidenceRoot, "smoke-results.json"))!
                    ["exit_code"] is null
                && JsonNode.Parse(ReadEvidenceFile(evidenceRoot, "smoke-results.json"))!
                    ["platforms"]!.AsArray().All(item =>
                        item!["outcome"]!.GetValue<string>() == "unverified"
                        && item["cleanup"]!.GetValue<string>() == "unverified")
                && JsonNode.Parse(ReadEvidenceFile(evidenceRoot, "runtime-verification.json"))!
                    ["execution_result"]!.GetValue<string>() == "unverified"
                && JsonNode.Parse(ReadEvidenceFile(evidenceRoot, "runtime-verification.json"))!
                    ["result"]!.GetValue<string>() == "fail"
                && JsonNode.Parse(ReadEvidenceFile(evidenceRoot, "runtime-verification.json"))!
                    ["contract_equivalence"]!.GetValue<string>() == "fail"
                && JsonNode.Parse(ReadEvidenceFile(evidenceRoot, "runtime-verification.json"))!
                    ["evidence_completeness"]!.GetValue<string>() == "fail"
                && runtime["platforms"]!.AsArray().All(item =>
                    item!["attempts"] is null
                    && item["cleanup"]!.GetValue<string>() == "unverified")
                && JsonNode.Parse(ReadEvidenceFile(evidenceRoot, "runtime-verification.json"))!
                    ["contract"]!["actual_hosting_environment"]!.GetValue<string>() ==
                    runtime["contract"]!["actual_hosting_environment"]!.GetValue<string>()
                && JsonNode.Parse(ReadEvidenceFile(evidenceRoot, "runtime-verification.json"))!
                    ["contract"]!["required_hosting_environment"]!.GetValue<string>() ==
                    runtime["contract"]!["required_hosting_environment"]!.GetValue<string>()
                && verdict["decision"]!.GetValue<string>() == "fail-closed"
                && !verdict["story_may_be_done"]!.GetValue<bool>()
                && !verdict["external_state_changed"]!.GetValue<bool>()
                && !verdict["predecessor_state_changed"]!.GetValue<bool>()
                && verdict["blockers"]!.AsArray().Count > 0
                && ValidateFailClosedVerdictChecks(verdict["checks"]!.AsObject())
                && subject["blockers"]!.AsArray().Count > 0
                && subject["passing_evidence"]!.AsArray().Count > 0
                && limitations.SequenceEqual(
                    crosswalk["limitations"]!.AsArray().Select(item => item!.GetValue<string>()),
                    StringComparer.Ordinal)
                && limitations.SequenceEqual(ExpectedMutationLimitations, StringComparer.Ordinal)
                && LimitationsContainMutationProhibitions(limitations)
                && required.Length == 3
                && required.Select(item => item["role"]!.GetValue<string>()).Order(StringComparer.Ordinal)
                    .SequenceEqual(RequiredRoles, StringComparer.Ordinal)
                && required.All(item => HasExactProperties(item, ["role", "status"])
                    && item["status"]!.GetValue<string>() == "missing")
                && approval["external_receipt_location"]!.GetValue<string>() == ReceiptDirectoryTemplate
                && approval["receipt_count"]!.GetValue<int>() == 0
                && approval["verification"]!["result"]!.GetValue<string>() == "missing"
                && TryParseExplicitOffset(subject["created_at"]!.GetValue<string>(), out DateTimeOffset created)
                && TryParseExplicitOffset(crosswalk["assembled_at"]!.GetValue<string>(), out DateTimeOffset assembled)
                && TryParseExplicitOffset(
                    registry["checked_at"]!.GetValue<string>(),
                    out DateTimeOffset registryChecked)
                && TryParseExplicitOffset(runtime["ended_at"]!.GetValue<string>(), out DateTimeOffset runtimeEnded)
                && created >= assembled
                && created >= registryChecked
                && created >= runtimeEnded
                && created <= DateTimeOffset.UtcNow.AddMinutes(5);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
            or NullReferenceException
            or ArgumentException
            or OverflowException
            or FormatException
            or InvalidDataException
            or JsonException
            or IOException
            or NotSupportedException
            or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static JsonObject LoadReviewerRoster(
        JsonObject crosswalk,
        string evidenceRoot,
        DateTimeOffset? subjectCreatedAt = null)
    {
        JsonObject approval = crosswalk["approval_contract"]!.AsObject();
        string path = approval["reviewer_roster_path"]!.GetValue<string>();
        byte[] bytes = ReadEvidenceFile(evidenceRoot, path);
        if (JsonNode.Parse(bytes) is not JsonObject roster
            || roster["roles"] is not JsonObject roles
            || roster["authority_source"] is not JsonObject authoritySource)
        {
            throw new InvalidDataException("Reviewer roster is missing a required object.");
        }

        string[] roleNames = roles.Select(role => role.Key).Order(StringComparer.Ordinal).ToArray();
        DateTimeOffset assembledAt = default;
        DateTimeOffset rosterCreatedAt = default;
        bool temporalBindingIsValid =
            TryParseExplicitOffset(crosswalk["assembled_at"]!.GetValue<string>(), out assembledAt)
            && TryParseExplicitOffset(roster["created_at"]!.GetValue<string>(), out rosterCreatedAt)
            && rosterCreatedAt >= assembledAt
            && (subjectCreatedAt is null || rosterCreatedAt <= subjectCreatedAt.Value);
        bool authoritySourceIsValid =
            HasExactProperties(authoritySource, ["kind", "url", "decision_date", "ratification"])
            && authoritySource["kind"]?.GetValue<string>() == "github-issue-comment"
            && authoritySource["url"] is JsonValue authorityUrlValue
            && Uri.TryCreate(authorityUrlValue.GetValue<string>(), UriKind.Absolute, out Uri? authorityUri)
            && authorityUri.Scheme == Uri.UriSchemeHttps
            && authorityUri.Host == "github.com"
            && authorityUri.AbsolutePath.StartsWith(
                "/Hexalith/Hexalith.EventStore/issues/",
                StringComparison.Ordinal)
            && authorityUri.Fragment.StartsWith("#issuecomment-", StringComparison.Ordinal)
            && authoritySource["decision_date"] is JsonValue decisionDateValue
            && DateOnly.TryParseExact(
                decisionDateValue.GetValue<string>(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateOnly decisionDate)
            && temporalBindingIsValid
            && decisionDate <= DateOnly.FromDateTime(rosterCreatedAt.UtcDateTime)
            && authoritySource["ratification"] is JsonValue ratificationValue
            && !string.IsNullOrWhiteSpace(ratificationValue.GetValue<string>());
        if (path != ReviewerRosterFile
            || approval["reviewer_roster_sha256"]!.GetValue<string>() != ComputeSha256(bytes)
            || roster["schema"]!.GetValue<string>() !=
                "hexalith.eventstore.story-3-13-reviewer-roster/v1"
            || roster["repository"]!.GetValue<string>() != ExpectedRepository
            || !HasExactProperties(roster, ["schema", "repository", "created_at", "authority_source", "roles"])
            || !authoritySourceIsValid
            || !DocumentIsSupportSafe(roster)
            || !roleNames.SequenceEqual(RequiredRoles, StringComparer.Ordinal)
            || roles.Any(role => role.Value!.AsArray().Count == 0
                || role.Value.AsArray().Any(identity =>
                    identity is not JsonValue value
                    || string.IsNullOrWhiteSpace(value.GetValue<string>()))
                || role.Value.AsArray().Select(identity => identity!.GetValue<string>())
                    .Distinct(StringComparer.Ordinal).Count() != role.Value.AsArray().Count))
        {
            throw new InvalidDataException("Reviewer roster is incomplete or does not match its binding.");
        }

        if (!roles["eventstore-owner"]!.AsArray().Select(item => item!.GetValue<string>())
                .SequenceEqual(["github:jpiquot"], StringComparer.Ordinal)
            || !roles["release-owner"]!.AsArray().Select(item => item!.GetValue<string>())
                .SequenceEqual(["github:jpiquot"], StringComparer.Ordinal)
            || !roles["test-architect"]!.AsArray().Select(item => item!.GetValue<string>())
                .SequenceEqual(["bmad:murat"], StringComparer.Ordinal))
        {
            throw new InvalidDataException("Reviewer roster does not match the governing Story 3.13 roles.");
        }

        return roster;
    }

    private static bool ValidateBaselineAndPredecessors(
        JsonObject crosswalk,
        string repositoryRoot,
        string evidenceRoot,
        string expectedPackageManifestSha256 = ApprovedPackageManifestSha256)
    {
        try
        {
            JsonObject baseline = crosswalk["baseline"]!.AsObject();
            JsonObject predecessorInputs = crosswalk["predecessor_inputs"]!.AsObject();
            JsonObject approvedIdentity = crosswalk["approved_identity"]!.AsObject();
            if (!HasExactProperties(baseline, ["eventstore_head", "builds_gitlink_sha", "verification"])
                || baseline["eventstore_head"]!.GetValue<string>() != ExpectedBaselineCommit
                || baseline["builds_gitlink_sha"]!.GetValue<string>() != ExpectedBaselineBuildsSha
                || baseline["verification"]!["method"]!.GetValue<string>() !=
                    "git rev-parse HEAD and git ls-files --stage references/Hexalith.Builds"
                || baseline["verification"]!["result"]!.GetValue<string>() != "pass"
                || RunGit(repositoryRoot, "cat-file", "-t", ExpectedBaselineCommit) != "commit"
                || RunGit(repositoryRoot, "rev-parse", ExpectedBaselineCommit + ":references/Hexalith.Builds") !=
                    ExpectedBaselineBuildsSha
                || RunGit(repositoryRoot, "cat-file", "-t", ApprovedSourceSha) != "commit")
            {
                return false;
            }

            (string Key, string ExpectedPath, string ExpectedSha256)[] blobs =
            [
                (
                    "story_1_20_record",
                    "_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-and-runtime-pin.md",
                    "0feee912874154a3885fbe69ac68419c89b209b8c9c5b9291833604881f34fa5"),
                (
                    "story_1_20_proof_packet",
                    "_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md",
                    "cb1ccde9d5cc5ca6cb52cbeab30fb9cd59bd89771e14f4b489e20bd5e3d46743"),
                (
                    "story_3_12_record",
                    "_bmad-output/implementation-artifacts/3-12-multi-platform-eventstore-container-publishing-correction.md",
                    "2bfc9ff991c9aeeaf11fd9c1926a17bb44ca290f99bd75b05df68a6edaf3e09c"),
            ];
            foreach ((string key, string expectedPath, string expectedSha256) in blobs)
            {
                JsonObject declared = predecessorInputs[key]!.AsObject();
                byte[] bytes = ReadEvidenceFile(repositoryRoot, declared["path"]!.GetValue<string>());
                if (!HasExactProperties(declared, ["path", "git_blob", "sha256", "verification"])
                    || declared["path"]!.GetValue<string>() != expectedPath
                    || declared["verification"]!.GetValue<string>() != "pass"
                    || declared["sha256"]!.GetValue<string>() != expectedSha256
                    || ComputeSha256(bytes) != expectedSha256
                    || declared["git_blob"]!.GetValue<string>() !=
                        RunGit(repositoryRoot, "rev-parse", ExpectedBaselineCommit + ":" + expectedPath))
                {
                    return false;
                }
            }

            JsonObject tree = predecessorInputs["story_1_20_evidence_tree"]!.AsObject();
            string treePath = tree["path"]!.GetValue<string>().TrimEnd('/');
            byte[] fullManifest = ReadEvidenceFile(
                evidenceRoot,
                tree["full_tree_manifest"]!.GetValue<string>());
            byte[] criticalManifest = ReadEvidenceFile(repositoryRoot, treePath + "/critical-evidence-sha256.txt");
            string[] gitPaths = RunGit(
                repositoryRoot,
                "ls-tree",
                "-r",
                "--name-only",
                ExpectedBaselineCommit,
                "--",
                treePath).Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (!HasExactProperties(tree,
                [
                    "path",
                    "git_tree",
                    "file_count",
                    "full_tree_manifest",
                    "critical_manifest_sha256",
                    "critical_manifest_entry_count",
                    "verification",
                    "full_tree_manifest_sha256",
                    "expected_entry_count",
                ])
                || tree["path"]!.GetValue<string>() != treePath + "/"
                || tree["git_tree"]!.GetValue<string>() !=
                    RunGit(repositoryRoot, "rev-parse", ExpectedBaselineCommit + ":" + treePath)
                || tree["verification"]!.GetValue<string>() != "pass"
                || tree["file_count"]!.GetValue<int>() != 40
                || tree["expected_entry_count"]!.GetValue<int>() != 40
                || gitPaths.Length != 40
                || tree["full_tree_manifest"]!.GetValue<string>() != "predecessor-tree-sha256.txt"
                || tree["full_tree_manifest_sha256"]!.GetValue<string>() != ComputeSha256(fullManifest)
                || tree["critical_manifest_sha256"]!.GetValue<string>() != ComputeSha256(criticalManifest)
                || tree["critical_manifest_entry_count"]!.GetValue<int>() !=
                    ParseChecksumManifest(criticalManifest).Count
                || tree["critical_manifest_entry_count"]!.GetValue<int>() != 33)
            {
                return false;
            }

            string approvedSourcePath = approvedIdentity["source"]!.GetValue<string>();
            bool approved = approvedSourcePath == treePath + "/approval-subject.json"
                && ReadEvidenceFile(repositoryRoot, approvedSourcePath).Length > 0
                && approvedIdentity["source_sha"]!.GetValue<string>() == ApprovedSourceSha
                && approvedIdentity["package_version"]!.GetValue<string>() == ApprovedPackageVersion
                && approvedIdentity["package_hash_manifest_sha256"]!.GetValue<string>() ==
                    expectedPackageManifestSha256
                && approvedIdentity["verification"]!["result"]!.GetValue<string>() == "pass";
            if (!approved)
            {
                return false;
            }

            return approved;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
            or NullReferenceException
            or ArgumentException
            or OverflowException
            or InvalidDataException
            or JsonException
            or IOException
            or NotSupportedException
            or UnauthorizedAccessException
            or TimeoutException)
        {
            return false;
        }
    }

    private static bool ValidateEvidenceIntegrity(
        JsonObject crosswalk,
        string repositoryRoot,
        string evidenceRoot,
        byte[] coreManifestBytes,
        byte[] reviewSubjectBytes)
    {
        string predecessorPrefix =
            "_bmad-output/implementation-artifacts/evidence/story-1-20/" + ApprovedSourceSha + "/";
        string[] expectedPredecessorPaths = ExpectedPredecessorFiles
            .Select(path => predecessorPrefix + path)
            .ToArray();
        byte[] retainedCore = ReadEvidenceFile(evidenceRoot, "evidence-core-sha256.txt");
        byte[] predecessorManifest = ReadEvidenceFile(evidenceRoot, "predecessor-tree-sha256.txt");
        return retainedCore.SequenceEqual(coreManifestBytes)
            && EvidenceDirectoryHasNoUnlistedFiles(evidenceRoot, crosswalk, reviewSubjectBytes)
            && VerifyChecksumManifest(
                coreManifestBytes,
                evidenceRoot,
                ExpectedCoreFilesFor(crosswalk))
            && ExpectedSupportSafeJsonReports.All(path =>
                JsonEvidenceIsSupportSafe(evidenceRoot, path))
            && ValidatePackageAvailability(crosswalk, evidenceRoot)
            && ComputeSha256(predecessorManifest) ==
                "d76d44291bccce0dbea384d2bf8c0258c6ba847dc4bdfa5150d881f4f5eae092"
            && VerifyChecksumManifest(predecessorManifest, repositoryRoot, expectedPredecessorPaths);
    }

    /// <summary>
    /// Rejects any recursively present file or directory that is not part of the core packet or the
    /// exact receipt tree bound to the current review subject.
    /// </summary>
    private static bool EvidenceDirectoryHasNoUnlistedFiles(
        string evidenceRoot,
        JsonObject crosswalk,
        byte[] reviewSubjectBytes)
    {
        HashSet<string> listed = new(StringComparer.Ordinal) { "evidence-sha256.txt" };
        foreach (string path in ExpectedCoreFilesFor(crosswalk).Concat(ExpectedOuterFiles))
        {
            listed.Add(path.Replace('\\', '/'));
        }

        if (crosswalk["approval_contract"]!["receipt_count"]!.GetValue<int>() == 3)
        {
            string receiptRoot = "acceptances/" + ComputeSha256(reviewSubjectBytes);
            foreach (string role in RequiredRoles)
            {
                listed.Add(receiptRoot + "/" + role + ".json");
                listed.Add(receiptRoot + "/sources/" + role + ".json");
            }
        }

        HashSet<string> listedDirectories = new(StringComparer.Ordinal);
        foreach (string path in listed)
        {
            string? parent = Path.GetDirectoryName(path.Replace('/', Path.DirectorySeparatorChar));
            while (!string.IsNullOrEmpty(parent))
            {
                listedDirectories.Add(parent.Replace('\\', '/'));
                parent = Path.GetDirectoryName(parent);
            }
        }

        string[] actualFiles = Directory.GetFiles(evidenceRoot, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(evidenceRoot, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] actualDirectories = Directory.GetDirectories(evidenceRoot, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(evidenceRoot, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();
        return actualFiles.All(listed.Contains)
            && actualDirectories.All(listedDirectories.Contains);
    }

    private static string[] ExpectedCoreFilesFor(JsonObject crosswalk)
    {
        HashSet<string> paths = new(ExpectedCoreFiles, StringComparer.Ordinal);
        JsonObject candidate = crosswalk["selected_candidates"]![0]!.AsObject();
        JsonObject packages = candidate["packages"]!.AsObject();
        JsonObject release = candidate["release"]!.AsObject();
        JsonObject authority = candidate["release_authority"]!.AsObject();
        if (packages["hash_manifest_scope"]!.GetValue<string>() == "evidence")
        {
            paths.Add(packages["hash_manifest_path"]!.GetValue<string>());
        }

        if (packages["byte_verification"]!["result"]!.GetValue<string>() == "pass")
        {
            string archiveRoot = packages["byte_verification"]!["archive_root"]!.GetValue<string>().TrimEnd('/');
            foreach (JsonObject item in packages["items"]!.AsArray().Select(item => item!.AsObject()))
            {
                paths.Add(archiveRoot + "/" + item["archive"]!.GetValue<string>());
            }
        }

        if (release["verification"]!["result"]!.GetValue<string>() == "pass"
            && release["evidence_scope"]!.GetValue<string>() == "evidence")
        {
            paths.Add(release["evidence_path"]!.GetValue<string>());
        }

        if (authority["record_scope"]!.GetValue<string>() == "evidence")
        {
            paths.Add(authority["record_path"]!.GetValue<string>());
            if (authority["verification"]!["result"]!.GetValue<string>() == "pass")
            {
                paths.Add("deployment-authority-source.json");
            }
        }

        return paths.Order(StringComparer.Ordinal).ToArray();
    }

    private static byte[] ReadScopedFile(
        string repositoryRoot,
        string evidenceRoot,
        string scope,
        string relativePath) => scope switch
        {
            "repository" => File.ReadAllBytes(ResolveWithin(repositoryRoot, relativePath)),
            "evidence" => File.ReadAllBytes(ResolveWithin(evidenceRoot, relativePath)),
            _ => throw new InvalidDataException("Evidence scope must be repository or evidence."),
        };

    private static bool RawBindingMatches(JsonObject subject, string name, string expectedPath, byte[] bytes)
    {
        JsonObject binding = subject[name]!.AsObject();
        return binding["path"]!.GetValue<string>() == expectedPath
            && binding["sha256"]!.GetValue<string>() == ComputeSha256(bytes);
    }

    private static bool ResponseBindingMatches(
        JsonObject oci,
        JsonObject retained,
        string name,
        byte[] bytes,
        string digest,
        string reference) =>
        oci[name + "_response_raw_sha256"]!.GetValue<string>() == ComputeSha256(bytes)
        && oci[name + "_response_raw_file"]!.GetValue<string>() == name + "-response.raw"
        && oci[name + "_response_size"]!.GetValue<int>() == bytes.Length
        && oci[name + "_response_content_type"]!.GetValue<string>() == OciIndexMediaType
        && oci[name + "_response_docker_content_digest"]!.GetValue<string>() == digest
        && retained["raw_file"]!.GetValue<string>() == name + "-response.raw"
        && retained["reference"]!.GetValue<string>() == reference
        && retained["request_url"]!.GetValue<string>() ==
            "https://" + ExpectedRegistry + "/v2/" + ExpectedContainerRepository + "/manifests/" + reference
        && retained["http_status"]!.GetValue<int>() == 200
        && retained["raw_sha256"]!.GetValue<string>() == ComputeSha256(bytes)
        && retained["content_length"]!.GetValue<int>() == bytes.Length
        && retained["content_type"]!.GetValue<string>() == OciIndexMediaType
        && retained["docker_content_digest"]!.GetValue<string>() == digest;

    private static bool ObjectResponseBindingMatches(
        JsonObject response,
        byte[] bytes,
        string digest,
        string contentType,
        string rawFile) =>
        HasExactProperties(response,
        [
            "kind",
            "digest",
            "reference",
            "request_url",
            "http_status",
            "content_type",
            "docker_content_digest",
            "content_length",
            "raw_file",
            "raw_sha256",
        ])
        && DocumentIsSupportSafe(response)
        && response["digest"]!.GetValue<string>() == digest
        && response["reference"]!.GetValue<string>() == digest
        && response["request_url"]!.GetValue<string>() ==
            "https://" + ExpectedRegistry + "/v2/" + ExpectedContainerRepository + "/" +
            (response["kind"]!.GetValue<string>() == "config" ? "blobs/" : "manifests/") + digest
        && response["http_status"]!.GetValue<int>() == 200
        && response["docker_content_digest"]!.GetValue<string>() == digest
        && response["content_type"]!.GetValue<string>() == contentType
        && response["content_length"]!.GetValue<int>() == bytes.Length
        && response["raw_file"]!.GetValue<string>() == rawFile
        && response["raw_sha256"]!.GetValue<string>() == ComputeSha256(bytes);

    private static bool BytesMatchDescriptor(byte[] bytes, JsonObject child, string prefix) =>
        child[prefix + "_raw_sha256"]!.GetValue<string>() == ComputeSha256(bytes)
        && child[prefix + "_digest"]!.GetValue<string>() == "sha256:" + ComputeSha256(bytes)
        && child[prefix + "_size"]!.GetValue<int>() == bytes.Length;

    private static bool FileBindingMatches(string root, JsonObject item, string property)
    {
        byte[] bytes = ReadEvidenceFile(root, item[property]!.GetValue<string>());
        return item[property + "_sha256"]!.GetValue<string>() == ComputeSha256(bytes);
    }

    private static bool ValidatePreflightLog(
        string evidenceRoot,
        JsonObject preflight,
        JsonObject oci,
        DateTimeOffset executionStarted,
        DateTimeOffset executionEnded)
    {
        byte[] bytes = ReadEvidenceFile(evidenceRoot, preflight["log"]!.GetValue<string>());
        JsonObject log = JsonNode.Parse(bytes)!.AsObject();
        JsonObject arm64 = oci["children"]!.AsArray().Select(item => item!.AsObject()).Single(item =>
            item["platform"]!.GetValue<string>() == "linux/arm64");
        string[] fields =
        [
            "schema",
            "platform",
            "child_digest",
            "observed_runtime_platform",
            "started_at",
            "ended_at",
            "exit_code",
            "outcome",
        ];
        if (log.ContainsKey("failure_class"))
        {
            fields = [.. fields, "failure_class"];
        }

        return preflight["log"]!.GetValue<string>() == "smoke-preflight.log"
            && preflight["log_sha256"]!.GetValue<string>() == ComputeSha256(bytes)
            && HasExactProperties(log, fields)
            && LogIsSupportSafe(bytes)
            && log["schema"]!.GetValue<string>() ==
                "hexalith.eventstore.story-3-13-runtime-preflight/v1"
            && log["platform"]!.GetValue<string>() == "linux/arm64"
            && log["child_digest"]!.GetValue<string>() == arm64["manifest_digest"]!.GetValue<string>()
            && preflight["child_digest"]!.GetValue<string>() == log["child_digest"]!.GetValue<string>()
            && log["observed_runtime_platform"]!.GetValue<string>() == "linux/arm64"
            && log["exit_code"]!.GetValue<int>() == 0
            && log["outcome"]!.GetValue<string>() == "pass"
            && RuntimeFailureClassificationMatchesLog(preflight, log)
            && TryParseExplicitOffset(log["started_at"]!.GetValue<string>(), out DateTimeOffset startedAt)
            && TryParseExplicitOffset(log["ended_at"]!.GetValue<string>(), out DateTimeOffset endedAt)
            && preflight["started_at"]!.GetValue<string>() == log["started_at"]!.GetValue<string>()
            && preflight["ended_at"]!.GetValue<string>() == log["ended_at"]!.GetValue<string>()
            && startedAt >= executionStarted
            && endedAt > startedAt
            && endedAt - startedAt <= TimeSpan.FromSeconds(60)
            && endedAt <= executionEnded;
    }

    private static bool ValidateRuntimeLog(
        string evidenceRoot,
        JsonObject platform,
        JsonObject contract,
        DateTimeOffset executionStarted,
        DateTimeOffset executionEnded)
    {
        byte[] bytes = ReadEvidenceFile(evidenceRoot, platform["log"]!.GetValue<string>());
        JsonObject log = JsonNode.Parse(bytes)!.AsObject();
        string platformName = platform["platform"]!.GetValue<string>();
        string[] fields =
        [
            "schema",
            "platform",
            "observed_runtime_platform",
            "child_digest",
            "health_path",
            "hosting_environment",
            "http_status",
            "redirect_count",
            "attempts",
            "started_at",
            "ended_at",
            "exit_code",
            "readiness_result",
            "cleanup",
        ];
        if (log.ContainsKey("failure_class"))
        {
            fields = [.. fields, "failure_class"];
        }

        return platform["log"]!.GetValue<string>() ==
                "smoke-" + platformName.Replace('/', '-') + ".log"
            && platform["log_sha256"]!.GetValue<string>() == ComputeSha256(bytes)
            && HasExactProperties(log, fields)
            && LogIsSupportSafe(bytes)
            && log["schema"]!.GetValue<string>() ==
                "hexalith.eventstore.story-3-13-runtime-execution/v1"
            && log["platform"]!.GetValue<string>() == platformName
            && log["observed_runtime_platform"]!.GetValue<string>() == platformName
            && platform["observed_runtime_platform"]!.GetValue<string>() == platformName
            && log["child_digest"]!.GetValue<string>() == platform["child_digest"]!.GetValue<string>()
            && log["health_path"]!.GetValue<string>() == contract["health_path"]!.GetValue<string>()
            && log["hosting_environment"]!.GetValue<string>() ==
                contract["actual_hosting_environment"]!.GetValue<string>()
            && log["http_status"]!.GetValue<int>() is >= 200 and <= 299
            && log["redirect_count"]!.GetValue<int>() == 0
            && log["attempts"]!.GetValue<int>() == platform["attempts"]!.GetValue<int>()
            && log["exit_code"]!.GetValue<int>() == 0
            && platform["exit_code"]!.GetValue<int>() == 0
            && log["readiness_result"]!.GetValue<string>() == "pass"
            && platform["readiness_result"]!.GetValue<string>() == "pass"
            && log["cleanup"]!.GetValue<string>() == "pass"
            && RuntimeFailureClassificationMatchesLog(platform, log)
            && TryParseExplicitOffset(log["started_at"]!.GetValue<string>(), out DateTimeOffset startedAt)
            && TryParseExplicitOffset(log["ended_at"]!.GetValue<string>(), out DateTimeOffset endedAt)
            && platform["started_at"]!.GetValue<string>() == log["started_at"]!.GetValue<string>()
            && platform["ended_at"]!.GetValue<string>() == log["ended_at"]!.GetValue<string>()
            && startedAt >= executionStarted
            && endedAt > startedAt
            && endedAt <= executionEnded
            && endedAt - startedAt <= TimeSpan.FromSeconds(contract["timeout_seconds"]!.GetValue<int>())
            && contract["poll_interval_seconds"]!.GetValue<int>() > 0
            && platform["attempts"]!.GetValue<int>() <=
                Math.Ceiling((endedAt - startedAt).TotalSeconds /
                    contract["poll_interval_seconds"]!.GetValue<int>()) + 1;
    }

    private static bool LogIsSupportSafe(byte[] bytes)
    {
        if (bytes.Length == 0 || bytes.Length > 16_384)
        {
            return false;
        }

        JsonNode? document;
        try
        {
            document = JsonNode.Parse(bytes);
        }
        catch (JsonException)
        {
            return false;
        }

        return document is not null
            && DocumentIsSupportSafe(document);
    }

    private static bool JsonEvidenceIsSupportSafe(string evidenceRoot, string path)
    {
        try
        {
            byte[] bytes = ReadEvidenceFile(evidenceRoot, path);
            return bytes.Length is > 0 and <= 1_048_576
                && JsonNode.Parse(bytes) is JsonNode document
                && DocumentIsSupportSafe(document);
        }
        catch (Exception exception) when (
            exception is ArgumentException
            or InvalidDataException
            or JsonException
            or IOException
            or NotSupportedException
            or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool ValidatePackageAvailability(JsonObject crosswalk, string evidenceRoot)
    {
        try
        {
            JsonObject report = JsonNode.Parse(ReadEvidenceFile(evidenceRoot, "package-availability.json"))!
                .AsObject();
            string schema = report["schema"]!.GetValue<string>();
            string version = crosswalk["selected_candidates"]![0]!["packages"]!["version"]!
                .GetValue<string>();
            if (!DocumentIsSupportSafe(report)
                || !TryParseExplicitOffset(report["checked_at"]!.GetValue<string>(), out _)
                || report["package_version"]!.GetValue<string>() != version
                || report["expected_count"]!.GetValue<int>() != 14)
            {
                return false;
            }

            if (schema == "hexalith.eventstore.story-3-13-package-availability/v1")
            {
                string[] fields =
                [
                    "schema",
                    "checked_at",
                    "package_version",
                    "expected_count",
                    "recovered_count",
                    "local_search_roots",
                    "local_matches",
                    "nuget_org",
                    "durable_source_queries",
                    "rebuild_attempted",
                    "result",
                    "blocker",
                    "reopen_trigger",
                ];
                return HasExactProperties(report, fields)
                    && report["recovered_count"]!.GetValue<int>() == 0
                    && !report["rebuild_attempted"]!.GetValue<bool>()
                    && report["result"]!.GetValue<string>() == "fail"
                    && report["local_matches"]!.AsArray().Count == 0
                    && report["local_search_roots"]!.AsArray().All(rootNode =>
                        rootNode is JsonValue rootValue
                        && rootValue.TryGetValue(out string? searchRoot)
                        && !string.IsNullOrWhiteSpace(searchRoot)
                        && !Path.IsPathRooted(searchRoot)
                        && !searchRoot.Contains("..", StringComparison.Ordinal))
                    && !string.IsNullOrWhiteSpace(report["blocker"]!.GetValue<string>())
                    && !string.IsNullOrWhiteSpace(report["reopen_trigger"]!.GetValue<string>())
                    && ValidateDurablePackageSourceQueries(report["durable_source_queries"])
                    && ValidateNugetOrgAvailability(
                        report["nuget_org"],
                        crosswalk["selected_candidates"]![0]!["packages"]!["items"]!.AsArray()
                            .Select(item => item!["id"]!.GetValue<string>())
                            .ToArray());
            }

            return schema == "hexalith.eventstore.story-3-13-package-availability/v2"
                && HasExactProperties(report,
                [
                    "schema",
                    "checked_at",
                    "package_version",
                    "expected_count",
                    "recovered_count",
                    "archive_root",
                    "result",
                ])
                && report["recovered_count"]!.GetValue<int>() == 14
                && report["archive_root"]!.GetValue<string>() == "packages"
                && report["result"]!.GetValue<string>() == "pass";
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
            or NullReferenceException
            or ArgumentException
            or OverflowException
            or InvalidDataException
            or JsonException
            or IOException
            or NotSupportedException
            or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool ValidateNugetOrgAvailability(JsonNode? nugetOrg, string[] expectedPackageIds)
    {
        if (nugetOrg is not JsonObject nuget
            || !HasExactProperties(nuget, ["http_status_by_package"])
            || nuget["http_status_by_package"] is not JsonObject statuses)
        {
            return false;
        }

        string[] expected = expectedPackageIds.Order(StringComparer.Ordinal).ToArray();
        string[] actual = statuses.Select(property => property.Key).Order(StringComparer.Ordinal).ToArray();
        return actual.SequenceEqual(expected, StringComparer.Ordinal)
            && statuses.All(property =>
                property.Value is JsonValue value
                && value.TryGetValue(out int status)
                && status == 404);
    }

    private static bool ValidateDurablePackageSourceQueries(JsonNode? queriesNode)
    {
        if (queriesNode is not JsonArray queries)
        {
            return false;
        }

        (string Source, string Method, string ExpectedResult, string Result)[] expected =
        [
            ("nuget.org-flat-container", "exact-package-version-http-get", "404-per-package", "not-found"),
            ("github-packages-org-nuget-inventory", "authenticated-org-package-inventory-with-read-packages",
                "zero-matching-packages", "not-found"),
            ("hexalith-internal-feed", "configured-nuget-source-inventory", "source-not-configured",
                "unavailable"),
        ];
        JsonObject[] actual = queries.Select(item => item!.AsObject()).ToArray();
        return actual.Length == expected.Length
            && actual.All(query => HasExactProperties(
                query,
                ["source", "method", "expected_result", "observed_count", "result"]))
            && expected.All(item => actual.Count(query =>
                query["source"]!.GetValue<string>() == item.Source
                && query["method"]!.GetValue<string>() == item.Method
                && query["expected_result"]!.GetValue<string>() == item.ExpectedResult
                && query["observed_count"]!.GetValue<int>() == 0
                && query["result"]!.GetValue<string>() == item.Result) == 1);
    }

    private static bool ValidateFailClosedVerdictChecks(JsonObject checks)
    {
        string[] checkNames = checks.Select(check => check.Key).Order(StringComparer.Ordinal).ToArray();
        return checkNames.SequenceEqual(ExpectedChecks, StringComparer.Ordinal)
            && checks["predecessor_integrity"]!.GetValue<string>() == "pass"
            && checks["exact_source"]!.GetValue<string>() == "pass"
            && checks["package_inventory"]!.GetValue<string>() == "pass"
            && checks["package_bytes"]!.GetValue<string>() == "fail"
            && checks["semantic_release_provenance"]!.GetValue<string>() == "fail"
            && checks["source_release_exact_match"]!.GetValue<string>() == "fail"
            && checks["oci_graph"]!.GetValue<string>() == "fail"
            && checks["oci_provenance_labels"]!.GetValue<string>() == "fail"
            && checks["runtime_both_platforms"]!.GetValue<string>() == "fail"
            && checks["deployment_authority"]!.GetValue<string>() == "fail"
            && checks["content_bound_acceptances"]!.GetValue<string>() == "missing"
            && checks["single_lineage"]!.GetValue<string>() == "fail";
    }

    private static bool DocumentIsSupportSafe(JsonNode node) => node switch
    {
        JsonObject value => value.All(property =>
            FieldNameIsSupportSafe(property.Key)
            && (property.Value is null || DocumentIsSupportSafe(property.Value))),
        JsonArray value => value.All(item => item is null || DocumentIsSupportSafe(item)),
        JsonValue value when value.TryGetValue<string>(out string? text) => ValueIsSupportSafe(text),
        _ => true,
    };

    private static bool FieldNameIsSupportSafe(string name)
    {
        string normalized = name.Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();
        string[] sensitiveFragments =
        [
            "apikey",
            "authorization",
            "connectionstring",
            "cookie",
            "credential",
            "diagnosticexcerpt",
            "password",
            "privatekey",
            "secret",
            "token",
        ];
        return sensitiveFragments.All(fragment => !normalized.Contains(fragment, StringComparison.Ordinal));
    }

    private static bool ValueIsSupportSafe(string value)
    {
        string[] forbidden =
        [
            "authorization:",
            "bearer ",
            "basic ",
            "password=",
            "pwd=",
            "accountkey=",
            "client_secret",
            "-----begin private key-----",
            "-----begin rsa private key-----",
            "-----begin ec private key-----",
            "-----begin openssh private key-----",
            "-----begin encrypted private key-----",
            "-----begin dsa private key-----",
            "raw payload",
            "raw_payload",
            "diagnostic excerpt",
            "stack trace",
            "system.exception",
            " at system.",
            "localhost",
        ];
        if (forbidden.Any(item => value.Contains(item, StringComparison.OrdinalIgnoreCase))
            || LooksLikeJwt(value))
        {
            return false;
        }

        // A bare IPv6 literal also parses as an absolute URI whose scheme is its first group, so the
        // address form is resolved first to keep such values from bypassing the private-range check.
        if (IPAddress.TryParse(value, out IPAddress? address))
        {
            return !AddressIsPrivate(address);
        }

        return !Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
            || (string.IsNullOrEmpty(uri.UserInfo) && !HostIsPrivate(uri.Host));
    }

    private static bool LooksLikeJwt(string value)
    {
        string[] parts = value.Split('.');
        return parts.Length == 3
            && parts.All(part => part.Length >= 8
                && part.All(character => char.IsLetterOrDigit(character) || character is '-' or '_'));
    }

    private static bool HostIsPrivate(string host)
    {
        string normalized = host.Trim().Trim('[', ']');
        if (string.IsNullOrEmpty(normalized))
        {
            // Opaque absolute values such as schema ids parse as URIs with no host.
            return false;
        }

        if (normalized.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(".internal", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(".corp", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(".lan", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // DNS resolution is mutable and environment-dependent. Fail closed on every absolute-URI
        // host except the exact public services that this evidence contract is allowed to cite.
        return !ExpectedSupportSafeUriHosts.Contains(normalized, StringComparer.OrdinalIgnoreCase);
    }

    private static bool AddressIsPrivate(IPAddress address)
    {
        if (IPAddress.IsLoopback(address)
            || address.Equals(IPAddress.Any)
            || address.Equals(IPAddress.IPv6Any))
        {
            return true;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            return AddressIsPrivate(address.MapToIPv4());
        }

        byte[] bytes = address.GetAddressBytes();
        if (bytes.Length == 16
            && bytes.AsSpan(0, 12).SequenceEqual(new byte[12]))
        {
            return AddressIsPrivate(new IPAddress(bytes.AsSpan(12, 4).ToArray()));
        }

        return bytes.Length switch
        {
            4 => bytes[0] == 10
                || bytes[0] == 127
                || (bytes[0] == 169 && bytes[1] == 254)
                || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
                || (bytes[0] == 192 && bytes[1] == 168)
                || (bytes[0] == 100 && bytes[1] is >= 64 and <= 127),
            16 => (bytes[0] & 0xfe) == 0xfc
                || (bytes[0] == 0xfe && (bytes[1] & 0xc0) == 0x80)
                || (bytes[0] == 0xfe && (bytes[1] & 0xc0) == 0xc0),
            _ => false,
        };
    }

    private static bool HasExactProperties(JsonObject value, IEnumerable<string> expected) =>
        value.Select(property => property.Key).Order(StringComparer.Ordinal)
            .SequenceEqual(expected.Order(StringComparer.Ordinal), StringComparer.Ordinal);

    private static bool IsFullSha(string value) =>
        value.Length == 40 && value.All(character => Uri.IsHexDigit(character));

    private static bool TryParseExplicitOffset(string value, out DateTimeOffset result)
    {
        result = default;
        bool hasOffset = value.EndsWith('Z')
            || (value.Length >= 6
                && value[^6] is '+' or '-'
                && value[^3] == ':');
        return hasOffset && DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out result);
    }

    private static DateTimeOffset ParseVerifiedExplicitOffset(string value) =>
        TryParseExplicitOffset(value, out DateTimeOffset result)
            ? result
            : throw new InvalidDataException("A previously verified explicit-offset timestamp became invalid.");

    private static bool PathIsWithin(string path, string parentPath)
    {
        string canonicalPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        string canonicalParent = Path.GetFullPath(parentPath).TrimEnd(Path.DirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        return canonicalPath.StartsWith(canonicalParent, StringComparison.Ordinal);
    }

    private static bool LimitationsContainMutationProhibitions(IEnumerable<string> limitations)
    {
        string text = string.Join('\n', limitations).ToLowerInvariant();
        string[] required =
        [
            "package",
            "registry",
            "deployment",
            "consumer",
            "predecessor",
            "epic 1",
            "submodule",
        ];
        return required.All(text.Contains);
    }

    private static bool ValidateMinimalConfiguration(JsonObject configuration) =>
        HasExactProperties(
            configuration,
            ["container_user", "container_port", "environment_variables", "command_contract"])
        && configuration["container_user"]!.GetValue<string>() == "app"
        && configuration["container_port"]!.GetValue<int>() == 8080
        && configuration["command_contract"]!.GetValue<string>() ==
            "bounded-digest-pinned-alive-v1"
        && HasExactProperties(configuration["environment_variables"]!.AsObject(), ["ASPNETCORE_ENVIRONMENT"])
        && configuration["environment_variables"]!["ASPNETCORE_ENVIRONMENT"]!.GetValue<string>() ==
            "Production";

    private static string ComputeLineageMaterialSha256(JsonObject candidate)
    {
        JsonObject release = candidate["release"]!.DeepClone().AsObject();
        release.Remove("evidence_sha256");
        JsonObject material = new()
        {
            ["source"] = candidate["source"]!.DeepClone(),
            ["packages"] = candidate["packages"]!.DeepClone(),
            ["release"] = release,
            ["oci"] = candidate["oci"]!.DeepClone(),
            ["runtime"] = StripExecutionOnlyRuntimeFields(candidate["runtime"]!.DeepClone().AsObject()),
        };
        return ComputeSha256(JsonSerializer.SerializeToUtf8Bytes(CanonicalizeJson(material)));
    }

    // Re-verifying an unchanged artifact must not invalidate a granted authority, so per-execution
    // values are excluded and only identity-affecting runtime facts determine the lineage.
    private static JsonObject StripExecutionOnlyRuntimeFields(JsonObject runtime)
    {
        string[] executionOnly = ["started_at", "ended_at", "attempts", "log_sha256"];
        foreach (string field in executionOnly)
        {
            runtime.Remove(field);
        }

        if (runtime["preflight"] is JsonObject preflight)
        {
            foreach (string field in executionOnly)
            {
                preflight.Remove(field);
            }
        }

        if (runtime["platforms"] is JsonArray platforms)
        {
            foreach (JsonObject platform in platforms.OfType<JsonObject>())
            {
                foreach (string field in executionOnly)
                {
                    platform.Remove(field);
                }
            }
        }

        (runtime["smoke_results"] as JsonObject)?.Remove("sha256");
        return runtime;
    }

    private static string ComputeCanonicalLineage(JsonObject candidate, string authorityRecordSha256) =>
        "sha256:" + ComputeSha256(Encoding.UTF8.GetBytes(
            ComputeLineageMaterialSha256(candidate) + "\n" + authorityRecordSha256 + "\n"));

    private static JsonNode CanonicalizeJson(JsonNode node) => node switch
    {
        JsonObject value => new JsonObject(value.OrderBy(property => property.Key, StringComparer.Ordinal)
            .Select(property => new KeyValuePair<string, JsonNode?>(
                property.Key,
                property.Value is null ? null : CanonicalizeJson(property.Value)))),
        JsonArray value => new JsonArray(value.Select(item =>
            item is null ? null : CanonicalizeJson(item)).ToArray()),
        _ => node.DeepClone(),
    };

    private static (string CleanupRoot, string Evidence, JsonObject Crosswalk, byte[] CrosswalkBytes,
        byte[] SubjectBytes, byte[] CoreBytes, byte[] ProofBytes, string PackageManifestSha256)
        CreatePassingFixture(string root)
    {
        string cleanupRoot = Path.Combine(Path.GetTempPath(), "story-3-13-" + Guid.NewGuid().ToString("N"));
        string staging = Path.Combine(cleanupRoot, "staging");
        Directory.CreateDirectory(staging);
        JsonObject crosswalk = LoadCrosswalk(root);
        JsonObject candidate = crosswalk["selected_candidates"]![0]!.AsObject();
        JsonObject packages = candidate["packages"]!.AsObject();
        string packageDirectory = Path.Combine(staging, "packages");
        Directory.CreateDirectory(packageDirectory);
        List<string> packageHashes = [];
        foreach (JsonObject item in packages["items"]!.AsArray().Select(item => item!.AsObject()))
        {
            string archive = item["id"]!.GetValue<string>() + "." + ApprovedPackageVersion + ".nupkg";
            byte[] archiveBytes = Encoding.UTF8.GetBytes(
                item["id"]!.GetValue<string>() + "|" + ApprovedPackageVersion + "|synthetic-package-bytes\n");
            string archiveHash = ComputeSha256(archiveBytes);
            File.WriteAllBytes(Path.Combine(packageDirectory, archive), archiveBytes);
            item["archive"] = archive;
            item["sha256"] = archiveHash;
            item["byte_verification"] = "pass";
            packageHashes.Add(archiveHash + "  " + archive);
        }

        byte[] packageManifestBytes = Encoding.UTF8.GetBytes(
            string.Join('\n', packageHashes.Order(StringComparer.Ordinal)) + "\n");
        File.WriteAllBytes(Path.Combine(staging, "nuget-sha256.txt"), packageManifestBytes);
        string packageManifestSha256 = ComputeSha256(packageManifestBytes);
        packages["hash_manifest_scope"] = "evidence";
        packages["hash_manifest_path"] = "nuget-sha256.txt";
        packages["hash_manifest_sha256"] = packageManifestSha256;
        packages["version"] = ApprovedPackageVersion;
        packages["byte_verification"]!["result"] = "pass";
        packages["byte_verification"]!["recovered_count"] = 14;
        packages["byte_verification"]!["archive_root"] = "packages";
        crosswalk["approved_identity"]!["package_version"] = ApprovedPackageVersion;
        crosswalk["approved_identity"]!["package_hash_manifest_sha256"] = packageManifestSha256;

        string indexHash = PopulatePassingOciAndRuntime(candidate, staging);
        string evidence = Path.Combine(cleanupRoot, ApprovedSourceSha, indexHash);
        Directory.CreateDirectory(Path.GetDirectoryName(evidence)!);
        Directory.Move(staging, evidence);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        crosswalk["assembled_at"] = now.AddMinutes(-5).ToString("O");
        JsonObject release = candidate["release"]!.AsObject();
        release["semantic_version"] = ApprovedPackageVersion;
        release["semantic_tag"] = "v" + ApprovedPackageVersion;
        release["repository"] = ExpectedRepository;
        release["workflow_identity"] = "github-actions:release.yml";
        release["workflow_name"] = "Release";
        release["workflow_path"] = ".github/workflows/release.yml";
        release["workflow_run"] = 123456789L;
        release["workflow_attempt"] = 1;
        release["workflow_run_url"] =
            "https://github.com/" + ExpectedRepository + "/actions/runs/123456789/attempts/1";
        release["builds_execution_sha"] = ExpectedBuildsSha;
        release["publisher_identity"] = "github-actions:semantic-release";
        release["validator_identity"] = "hexalith-builds:" + ExpectedBuildsSha;
        release["source_sha"] = ApprovedSourceSha;
        release["source_exact_match"] = true;
        release["evidence_scope"] = "evidence";
        release["evidence_path"] = "release-provenance.json";
        release["verification"]!["result"] = "pass";

        WriteReviewerRoster(evidence, now.AddMinutes(-4.5));
        JsonObject approval = crosswalk["approval_contract"]!.AsObject();
        approval["reviewer_roster_path"] = ReviewerRosterFile;
        approval["reviewer_roster_sha256"] = ComputeSha256(Path.Combine(evidence, ReviewerRosterFile));
        approval["external_receipt_location"] = ReceiptDirectoryTemplate;
        approval["required_receipt_fields"] = new JsonArray(RequiredReceiptFields
            .Select(field => (JsonNode)field).ToArray());
        approval["receipt_count"] = 3;
        approval["verification"]!["result"] = "pass";

        JsonObject authorityScope = CreatePassingAuthorityScope(candidate);
        JsonObject authoritySource = CreatePassingAuthoritySource(authorityScope, now);
        byte[] authoritySourceBytes = JsonSerializer.SerializeToUtf8Bytes(authoritySource);
        File.WriteAllBytes(Path.Combine(evidence, "deployment-authority-source.json"), authoritySourceBytes);
        JsonObject authorityRecord = CreatePassingAuthority(
            candidate,
            now,
            authorityScope,
            ComputeSha256(authoritySourceBytes));
        byte[] authorityBytes = JsonSerializer.SerializeToUtf8Bytes(authorityRecord);
        File.WriteAllBytes(Path.Combine(evidence, "deployment-authority.json"), authorityBytes);
        JsonObject authority = candidate["release_authority"]!.AsObject();
        authority["record_scope"] = "evidence";
        authority["record_path"] = "deployment-authority.json";
        authority["record_sha256"] = ComputeSha256(authorityBytes);
        authority["owner"] = "github:jpiquot";
        authority["authorized_source_sha"] = ApprovedSourceSha;
        authority["deployment_authorized"] = true;
        authority["verification"]!["result"] = "pass";
        candidate["lineage_id"] = ComputeCanonicalLineage(candidate, ComputeSha256(authorityBytes));
        authority["canonical_lineage_id"] = candidate["lineage_id"]!.GetValue<string>();

        JsonObject releaseEvidence = CreatePassingReleaseEvidence(
            candidate,
            packageManifestSha256,
            ComputeSha256(authorityBytes),
            now.AddMinutes(-11));
        byte[] releaseEvidenceBytes = JsonSerializer.SerializeToUtf8Bytes(releaseEvidence);
        File.WriteAllBytes(Path.Combine(evidence, "release-provenance.json"), releaseEvidenceBytes);
        release["evidence_sha256"] = ComputeSha256(releaseEvidenceBytes);

        string predecessorManifestSource = Path.Combine(root, EvidenceRelativePath, "predecessor-tree-sha256.txt");
        File.Copy(predecessorManifestSource, Path.Combine(evidence, "predecessor-tree-sha256.txt"));
        File.WriteAllBytes(
            Path.Combine(evidence, "package-availability.json"),
            JsonSerializer.SerializeToUtf8Bytes(new JsonObject
            {
                ["schema"] = "hexalith.eventstore.story-3-13-package-availability/v2",
                ["checked_at"] = now.AddMinutes(-6).ToString("O"),
                ["package_version"] = ApprovedPackageVersion,
                ["expected_count"] = 14,
                ["recovered_count"] = 14,
                ["archive_root"] = "packages",
                ["result"] = "pass",
            }));

        crosswalk["limitations"] = new JsonArray(
            "This acceptance authorizes no package publication, registry mutation, deployment mutation, consumer migration, predecessor change, Epic 1 change, or submodule mutation.",
            "Every acceptance is bound to one unchanged review-subject SHA-256.");
        JsonObject verdict = crosswalk["verdict"]!.AsObject();
        verdict["decision"] = "pass";
        verdict["story_may_be_done"] = true;
        verdict["blockers"] = new JsonArray();
        foreach (string check in ExpectedChecks)
        {
            verdict["checks"]![check] = "pass";
        }

        byte[] coreBytes = WriteCoreManifest(crosswalk, evidence);
        byte[] crosswalkBytes = JsonSerializer.SerializeToUtf8Bytes(crosswalk);
        byte[] proofBytes = Encoding.UTF8.GetBytes("synthetic frozen human proof packet\n");
        JsonObject subject = CreatePassingReviewSubject(
            crosswalk,
            crosswalkBytes,
            coreBytes,
            proofBytes,
            now.AddMinutes(-4));
        byte[] subjectBytes = JsonSerializer.SerializeToUtf8Bytes(subject);
        CreateAcceptanceReceipts(evidence, subjectBytes);
        return (
            cleanupRoot,
            evidence,
            crosswalk,
            crosswalkBytes,
            subjectBytes,
            coreBytes,
            proofBytes,
            packageManifestSha256);
    }

    private static string PopulatePassingOciAndRuntime(JsonObject candidate, string evidence)
    {
        JsonObject oci = candidate["oci"]!.AsObject();
        JsonArray descriptors = [];
        JsonArray children = [];
        JsonArray objectResponses = [];
        foreach ((string platform, string architecture) in new[]
        {
            ("linux/amd64", "amd64"),
            ("linux/arm64", "arm64"),
        })
        {
            string stem = "child-" + platform.Replace('/', '-');
            JsonObject config = new()
            {
                ["architecture"] = architecture,
                ["os"] = "linux",
                ["config"] = new JsonObject
                {
                    ["Labels"] = new JsonObject
                    {
                        ["org.opencontainers.image.source"] = "https://github.com/Hexalith/Hexalith.EventStore",
                        ["org.opencontainers.image.url"] =
                            "https://github.com/Hexalith/Hexalith.EventStore/releases/tag/v" + ApprovedPackageVersion,
                        ["org.opencontainers.image.documentation"] =
                            "https://github.com/Hexalith/Hexalith.EventStore/blob/" + ApprovedSourceSha + "/README.md",
                        ["org.opencontainers.image.revision"] = ApprovedSourceSha,
                        ["org.opencontainers.image.version"] = ApprovedPackageVersion,
                    },
                },
            };
            byte[] configBytes = JsonSerializer.SerializeToUtf8Bytes(config);
            string configDigest = "sha256:" + ComputeSha256(configBytes);
            string configFile = stem + ".config.raw";
            File.WriteAllBytes(Path.Combine(evidence, configFile), configBytes);
            JsonObject manifest = new()
            {
                ["schemaVersion"] = 2,
                ["mediaType"] = OciManifestMediaType,
                ["config"] = new JsonObject
                {
                    ["mediaType"] = OciConfigMediaType,
                    ["digest"] = configDigest,
                    ["size"] = configBytes.Length,
                },
                ["layers"] = new JsonArray(),
            };
            byte[] manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifest);
            string manifestDigest = "sha256:" + ComputeSha256(manifestBytes);
            string manifestFile = stem + ".manifest.raw";
            File.WriteAllBytes(Path.Combine(evidence, manifestFile), manifestBytes);
            descriptors.Add(new JsonObject
            {
                ["mediaType"] = OciManifestMediaType,
                ["digest"] = manifestDigest,
                ["size"] = manifestBytes.Length,
                ["platform"] = new JsonObject { ["os"] = "linux", ["architecture"] = architecture },
            });
            children.Add(new JsonObject
            {
                ["platform"] = platform,
                ["manifest_digest"] = manifestDigest,
                ["manifest_size"] = manifestBytes.Length,
                ["manifest_media_type"] = OciManifestMediaType,
                ["manifest_raw_file"] = manifestFile,
                ["manifest_raw_sha256"] = manifestDigest[7..],
                ["config_digest"] = configDigest,
                ["config_size"] = configBytes.Length,
                ["config_media_type"] = OciConfigMediaType,
                ["config_raw_file"] = configFile,
                ["config_raw_sha256"] = configDigest[7..],
                ["config_platform"] = platform,
                ["verification"] = "pass",
            });
            objectResponses.Add(ObjectResponse("child-manifest", manifestDigest, OciManifestMediaType, manifestFile, manifestBytes));
            objectResponses.Add(ObjectResponse("config", configDigest, OciConfigMediaType, configFile, configBytes));
        }

        JsonObject index = new()
        {
            ["schemaVersion"] = 2,
            ["mediaType"] = OciIndexMediaType,
            ["manifests"] = descriptors,
        };
        byte[] indexBytes = JsonSerializer.SerializeToUtf8Bytes(index);
        string indexHash = ComputeSha256(indexBytes);
        string indexDigest = "sha256:" + indexHash;
        foreach (string file in new[] { "index.raw", "tag-response.raw", "digest-response.raw" })
        {
            File.WriteAllBytes(Path.Combine(evidence, file), indexBytes);
        }

        oci["registry"] = "registry.hexalith.com";
        oci["repository"] = "eventstore";
        oci["immutable_reference"] = "registry.hexalith.com/eventstore@" + indexDigest;
        oci["index_digest"] = indexDigest;
        oci["index_raw_sha256"] = indexHash;
        oci["index_size"] = indexBytes.Length;
        oci["index_media_type"] = OciIndexMediaType;
        oci["children"] = children;
        foreach (string name in new[] { "tag", "digest" })
        {
            oci[name + "_response_raw_file"] = name + "-response.raw";
            oci[name + "_response_raw_sha256"] = indexHash;
            oci[name + "_response_size"] = indexBytes.Length;
            oci[name + "_response_content_type"] = OciIndexMediaType;
            oci[name + "_response_docker_content_digest"] = indexDigest;
        }

        JsonObject labels = oci["provenance_labels"]!.AsObject();
        labels["org.opencontainers.image.source"] = "https://github.com/Hexalith/Hexalith.EventStore";
        labels["org.opencontainers.image.url"] =
            "https://github.com/Hexalith/Hexalith.EventStore/releases/tag/v" + ApprovedPackageVersion;
        labels["org.opencontainers.image.documentation"] =
            "https://github.com/Hexalith/Hexalith.EventStore/blob/" + ApprovedSourceSha + "/README.md";
        labels["org.opencontainers.image.revision"] = ApprovedSourceSha;
        labels["org.opencontainers.image.version"] = ApprovedPackageVersion;
        labels["verification"]!["result"] = "pass";
        oci["verification"]!["result"] = "pass";

        DateTimeOffset now = DateTimeOffset.UtcNow;
        JsonObject registryReadback = new()
        {
            ["schema"] = "hexalith.eventstore.story-3-13-registry-readback/v2",
            ["checked_at"] = now.AddMinutes(-7).ToString("O"),
            ["repository"] = ExpectedRegistry + "/" + ExpectedContainerRepository,
            ["discovery_tag"] = "v" + ApprovedPackageVersion,
            ["immutable_index_digest"] = indexDigest,
            ["tag_response"] = IndexResponse("tag", "v" + ApprovedPackageVersion, indexDigest, indexBytes),
            ["digest_response"] = IndexResponse("digest", indexDigest, indexDigest, indexBytes),
            ["objects"] = objectResponses,
            ["object_response_metadata_result"] = "pass",
            ["tag_and_digest_bytes_identical"] = true,
            ["shared_validator"] = new JsonObject
            {
                ["path"] = "references/Hexalith.Builds/Github/publish-containers/oci_registry_validator.py",
                ["sha256"] = "e1547e31fbdb8a678c99a245510e718c1cb35f6b9ec51264aa7bc1cdae419509",
                ["builds_gitlink_sha"] = ExpectedBuildsSha,
                ["verification_result"] = "pass",
                ["cli_candidate_compatibility"] = "pass",
                ["cli_candidate_consequence"] = "The semantic release tag is accepted by the pinned validator.",
            },
            ["config_labels"] = new JsonObject
            {
                ["version"] = ApprovedPackageVersion,
                ["revision"] = ApprovedSourceSha,
                ["approved_source_sha"] = ApprovedSourceSha,
                ["exact_source_match"] = true,
                ["verification_result"] = "pass",
                ["provenance_label_result"] = "pass",
            },
            ["oci_graph_result"] = "pass",
            ["result"] = "pass",
            ["scope"] = "The exact semantic tag and immutable digest resolve to one verified two-platform graph.",
        };
        File.WriteAllBytes(
            Path.Combine(evidence, "registry-readback.json"),
            JsonSerializer.SerializeToUtf8Bytes(registryReadback));
        File.WriteAllBytes(
            Path.Combine(evidence, "oci-validation.json"),
            JsonSerializer.SerializeToUtf8Bytes(new JsonObject
            {
                ["schema"] = "hexalith.eventstore.story-3-13-oci-validation/v2",
                ["checked_at"] = now.AddMinutes(-7).ToString("O"),
                ["repository"] = ExpectedRegistry + "/" + ExpectedContainerRepository,
                ["immutable_reference"] = ExpectedRegistry + "/" + ExpectedContainerRepository + "@" +
                    indexDigest,
                ["index_digest"] = indexDigest,
                ["index_size"] = indexBytes.Length,
                ["media_type"] = OciIndexMediaType,
                ["platforms"] = new JsonArray("linux/amd64", "linux/arm64"),
                ["children"] = new JsonArray(children.Select(item => (JsonNode)new JsonObject
                {
                    ["platform"] = item!["platform"]!.GetValue<string>(),
                    ["manifest_digest"] = item["manifest_digest"]!.GetValue<string>(),
                    ["manifest_size"] = item["manifest_size"]!.GetValue<int>(),
                    ["media_type"] = OciManifestMediaType,
                    ["manifest_raw_file"] = item["manifest_raw_file"]!.GetValue<string>(),
                    ["manifest_raw_sha256"] = item["manifest_raw_sha256"]!.GetValue<string>(),
                    ["config_digest"] = item["config_digest"]!.GetValue<string>(),
                    ["config_size"] = item["config_size"]!.GetValue<int>(),
                    ["config_media_type"] = OciConfigMediaType,
                    ["config_raw_file"] = item["config_raw_file"]!.GetValue<string>(),
                    ["config_raw_sha256"] = item["config_raw_sha256"]!.GetValue<string>(),
                }).ToArray()),
                ["raw_index_file"] = "index.raw",
                ["raw_index_sha256"] = indexHash,
                ["raw_graph_result"] = "pass",
                ["response_metadata_result"] = "pass",
                ["result"] = "pass",
                ["verification"] = new JsonObject
                {
                    ["method"] = "retained-raw-oci-graph-and-response-metadata",
                    ["result"] = "pass",
                    ["reason"] = "Every retained raw descriptor and response-metadata binding was verified.",
                },
            }));

        PopulatePassingRuntime(candidate, evidence, children, now);
        return indexHash;
    }

    private static void PopulatePassingRuntime(
        JsonObject candidate,
        string evidence,
        JsonArray children,
        DateTimeOffset now)
    {
        DateTimeOffset startedAt = now.AddMinutes(-10);
        DateTimeOffset endedAt = now.AddMinutes(-8);
        JsonObject runtime = candidate["runtime"]!.AsObject();
        runtime["schema"] = "hexalith.eventstore.story-3-13-runtime-verification/v2";
        runtime["execution_result"] = "pass";
        runtime["contract_equivalence"] = "pass";
        runtime["result"] = "pass";
        runtime["started_at"] = startedAt.ToString("O");
        runtime["ended_at"] = endedAt.ToString("O");
        runtime["exit_code"] = 0;
        runtime["exit_code_verification"] = new JsonObject
        {
            ["citation"] = "bounded-smoke-process-result",
            ["result"] = "pass",
            ["reason"] = "The bounded smoke command returned process exit code zero.",
        };
        runtime["contract"]!["actual_hosting_environment"] = "Production";
        runtime["contract"]!["required_hosting_environment"] = "Production";
        runtime["contract"]!["timeout_seconds"] = 180;
        runtime["contract"]!["poll_interval_seconds"] = 2;
        runtime["contract"]!["minimal_configuration"] = new JsonObject
        {
            ["container_user"] = "app",
            ["container_port"] = 8080,
            ["environment_variables"] = new JsonObject { ["ASPNETCORE_ENVIRONMENT"] = "Production" },
            ["command_contract"] = "bounded-digest-pinned-alive-v1",
        };
        runtime["contract"]!.AsObject().Remove("equivalence_blocker");
        runtime["tool"] = new JsonObject
        {
            ["path"] = ExpectedSmokeToolPath,
            ["sha256"] = ExpectedSmokeToolSha256,
            ["builds_gitlink_sha"] = ExpectedBuildsSha,
            ["identity"] = "hexalith-builds:" + ExpectedBuildsSha,
        };
        runtime["command"] = new JsonObject
        {
            ["executable"] = "python3",
            ["arguments"] = new JsonArray(
                ExpectedSmokeToolPath,
                "--image",
                candidate["oci"]!["immutable_reference"]!.GetValue<string>(),
                "--timeout-seconds",
                "180",
                "--poll-interval-seconds",
                "2",
                "--hosting-environment",
                "Production"),
            ["digest_pinned"] = true,
        };
        runtime["cleanup_check"] = "Every bounded synthetic platform container was removed.";
        runtime["evidence_completeness"] = "pass";
        runtime["verification"]!["result"] = "pass";
        runtime["verification"]!.AsObject().Remove("reason");
        runtime.AsObject().Remove("blocker");

        JsonObject arm64Child = children.Select(item => item!.AsObject()).Single(item =>
            item["platform"]!.GetValue<string>() == "linux/arm64");
        DateTimeOffset preflightStarted = startedAt;
        DateTimeOffset preflightEnded = startedAt.AddSeconds(10);
        JsonObject preflightLog = new()
        {
            ["schema"] = "hexalith.eventstore.story-3-13-runtime-preflight/v1",
            ["platform"] = "linux/arm64",
            ["child_digest"] = arm64Child["manifest_digest"]!.GetValue<string>(),
            ["observed_runtime_platform"] = "linux/arm64",
            ["started_at"] = preflightStarted.ToString("O"),
            ["ended_at"] = preflightEnded.ToString("O"),
            ["exit_code"] = 0,
            ["outcome"] = "pass",
        };
        byte[] preflightBytes = JsonSerializer.SerializeToUtf8Bytes(preflightLog);
        File.WriteAllBytes(Path.Combine(evidence, "smoke-preflight.log"), preflightBytes);
        runtime["preflight"]!["child_digest"] = arm64Child["manifest_digest"]!.GetValue<string>();
        runtime["preflight"]!["outcome"] = "pass";
        runtime["preflight"]!["started_at"] = preflightStarted.ToString("O");
        runtime["preflight"]!["ended_at"] = preflightEnded.ToString("O");
        runtime["preflight"]!["log_sha256"] = ComputeSha256(preflightBytes);

        int index = 0;
        foreach (JsonObject platform in runtime["platforms"]!.AsArray().Select(item => item!.AsObject()))
        {
            JsonObject child = children.Select(item => item!.AsObject()).Single(item =>
                item["platform"]!.GetValue<string>() == platform["platform"]!.GetValue<string>());
            string platformName = platform["platform"]!.GetValue<string>();
            DateTimeOffset platformStarted = startedAt.AddSeconds(20 + (index * 30));
            DateTimeOffset platformEnded = platformStarted.AddSeconds(20);
            platform["child_digest"] = child["manifest_digest"]!.GetValue<string>();
            platform["observed_runtime_platform"] = platformName;
            platform["attempts"] = 2;
            platform["outcome"] = "pass";
            platform["cleanup"] = "pass";
            platform["exit_code"] = 0;
            platform["readiness_result"] = "pass";
            platform["started_at"] = platformStarted.ToString("O");
            platform["ended_at"] = platformEnded.ToString("O");
            JsonObject log = new()
            {
                ["schema"] = "hexalith.eventstore.story-3-13-runtime-execution/v1",
                ["platform"] = platformName,
                ["observed_runtime_platform"] = platformName,
                ["child_digest"] = child["manifest_digest"]!.GetValue<string>(),
                ["health_path"] = "/alive",
                ["hosting_environment"] = "Production",
                ["http_status"] = 200,
                ["redirect_count"] = 0,
                ["attempts"] = 2,
                ["started_at"] = platformStarted.ToString("O"),
                ["ended_at"] = platformEnded.ToString("O"),
                ["exit_code"] = 0,
                ["readiness_result"] = "pass",
                ["cleanup"] = "pass",
            };
            byte[] logBytes = JsonSerializer.SerializeToUtf8Bytes(log);
            File.WriteAllBytes(Path.Combine(evidence, platform["log"]!.GetValue<string>()), logBytes);
            platform["log_sha256"] = ComputeSha256(logBytes);
            index++;
        }

        JsonObject smokeResults = new()
        {
            ["schema"] = "hexalith.eventstore.story-3-13-smoke-results/v2",
            ["image_repository"] = ExpectedRegistry + "/" + ExpectedContainerRepository,
            ["tool"] = runtime["tool"]!.DeepClone(),
            ["command"] = runtime["command"]!.DeepClone(),
            ["started_at"] = startedAt.ToString("O"),
            ["ended_at"] = endedAt.ToString("O"),
            ["exit_code"] = 0,
            ["exit_code_verification"] = runtime["exit_code_verification"]!.DeepClone(),
            ["platforms"] = runtime["platforms"]!.DeepClone(),
            ["result"] = "pass",
        };
        byte[] smokeResultsBytes = JsonSerializer.SerializeToUtf8Bytes(smokeResults);
        File.WriteAllBytes(Path.Combine(evidence, "smoke-results.json"), smokeResultsBytes);
        runtime["smoke_results"] = new JsonObject
        {
            ["path"] = "smoke-results.json",
            ["sha256"] = ComputeSha256(smokeResultsBytes),
        };
        JsonObject retained = Clone(runtime);
        retained.Remove("citation");
        File.WriteAllBytes(
            Path.Combine(evidence, runtime["citation"]!.GetValue<string>()),
            JsonSerializer.SerializeToUtf8Bytes(retained));
    }

    private static JsonObject ObjectResponse(
        string kind,
        string digest,
        string contentType,
        string rawFile,
        byte[] bytes) => new()
        {
            ["kind"] = kind,
            ["digest"] = digest,
            ["reference"] = digest,
            ["request_url"] = "https://" + ExpectedRegistry + "/v2/" + ExpectedContainerRepository + "/" +
                (kind == "config" ? "blobs/" : "manifests/") + digest,
            ["http_status"] = 200,
            ["content_type"] = contentType,
            ["docker_content_digest"] = digest,
            ["content_length"] = bytes.Length,
            ["raw_file"] = rawFile,
            ["raw_sha256"] = ComputeSha256(bytes),
        };

    private static JsonObject IndexResponse(string name, string reference, string digest, byte[] bytes) => new()
    {
        ["raw_file"] = name + "-response.raw",
        ["reference"] = reference,
        ["request_url"] = "https://" + ExpectedRegistry + "/v2/" + ExpectedContainerRepository +
            "/manifests/" + reference,
        ["http_status"] = 200,
        ["content_type"] = OciIndexMediaType,
        ["docker_content_digest"] = digest,
        ["content_length"] = bytes.Length,
        ["raw_sha256"] = ComputeSha256(bytes),
    };

    private static void WriteReviewerRoster(string evidence, DateTimeOffset createdAt)
    {
        JsonObject roster = new()
        {
            ["schema"] = "hexalith.eventstore.story-3-13-reviewer-roster/v1",
            ["repository"] = "Hexalith/Hexalith.EventStore",
            ["created_at"] = createdAt.ToString("O"),
            ["authority_source"] = new JsonObject
            {
                ["kind"] = "github-issue-comment",
                ["url"] = "https://github.com/Hexalith/Hexalith.EventStore/issues/313" +
                    "#issuecomment-313",
                ["decision_date"] = createdAt.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["ratification"] =
                    "Synthetic external decision binding the three governing Story 3.13 reviewer roles.",
            },
            ["roles"] = new JsonObject
            {
                ["eventstore-owner"] = new JsonArray("github:jpiquot"),
                ["release-owner"] = new JsonArray("github:jpiquot"),
                ["test-architect"] = new JsonArray("bmad:murat"),
            },
        };
        File.WriteAllBytes(
            Path.Combine(evidence, ReviewerRosterFile),
            JsonSerializer.SerializeToUtf8Bytes(roster));
    }

    private static JsonObject CreatePassingAuthorityScope(JsonObject candidate) => new()
    {
        ["source_sha"] = ApprovedSourceSha,
        ["package_manifest_sha256"] = candidate["packages"]!["hash_manifest_sha256"]!.GetValue<string>(),
        ["package_version"] = ApprovedPackageVersion,
        ["semantic_tag"] = "v" + ApprovedPackageVersion,
        ["registry"] = ExpectedRegistry,
        ["container_repository"] = ExpectedContainerRepository,
        ["index_digest"] = candidate["oci"]!["index_digest"]!.GetValue<string>(),
        ["platforms"] = new JsonArray("linux/amd64", "linux/arm64"),
        ["workflow_run"] = candidate["release"]!["workflow_run"]!.GetValue<long>(),
        ["workflow_attempt"] = candidate["release"]!["workflow_attempt"]!.GetValue<int>(),
        ["builds_execution_sha"] = ExpectedBuildsSha,
        ["publisher_identity"] = "github-actions:semantic-release",
        ["validator_identity"] = "hexalith-builds:" + ExpectedBuildsSha,
    };

    private static JsonObject CreatePassingAuthoritySource(JsonObject scope, DateTimeOffset now) => new()
    {
        ["schema"] = "hexalith.eventstore.story-3-13-deployment-authority-source/v1",
        ["repository"] = ExpectedRepository,
        ["source_url"] = "https://github.com/" + ExpectedRepository + "/commit/" + ApprovedSourceSha +
            "#story-3-13-deployment-authority",
        ["captured_at"] = now.AddMinutes(-11).AddSeconds(-30).ToString("O"),
        ["owner"] = "github:jpiquot",
        ["action"] = "deployed-runtime-identity-acceptance",
        ["decision"] = "authorized",
        ["scope"] = scope.DeepClone(),
    };

    private static JsonObject CreatePassingAuthority(
        JsonObject candidate,
        DateTimeOffset now,
        JsonObject scope,
        string authoritySourceSha256) => new()
    {
        ["schema"] = "hexalith.eventstore.story-3-13-deployment-authority/v2",
        ["repository"] = ExpectedRepository,
        ["action"] = "deployed-runtime-identity-acceptance",
        ["owner"] = "github:jpiquot",
        ["authorized_at"] = now.AddMinutes(-12).ToString("O"),
        ["expires_at"] = now.AddDays(30).ToString("O"),
        ["rationale"] = "Synthetic authorization for the complete test lineage only.",
        ["deployment_authorized"] = true,
        ["scope"] = scope.DeepClone(),
        ["lineage_material_sha256"] = ComputeLineageMaterialSha256(candidate),
        ["durable_source"] = new JsonObject
        {
            ["kind"] = "retained-immutable-external-record",
            ["scope"] = "evidence",
            ["path"] = "deployment-authority-source.json",
            ["sha256"] = authoritySourceSha256,
        },
    };

    private static JsonObject CreatePassingReleaseEvidence(
        JsonObject candidate,
        string packageManifestSha256,
        string authorityRecordSha256,
        DateTimeOffset deploymentActionAt)
    {
        JsonObject release = candidate["release"]!.AsObject();
        return new JsonObject
        {
            ["schema"] = "hexalith.eventstore.story-3-13-release-provenance/v2",
            ["repository"] = ExpectedRepository,
            ["workflow_identity"] = release["workflow_identity"]!.GetValue<string>(),
            ["workflow_name"] = release["workflow_name"]!.GetValue<string>(),
            ["workflow_path"] = release["workflow_path"]!.GetValue<string>(),
            ["workflow_run_url"] = release["workflow_run_url"]!.GetValue<string>(),
            ["workflow_run"] = release["workflow_run"]!.GetValue<long>(),
            ["workflow_attempt"] = release["workflow_attempt"]!.GetValue<int>(),
            ["conclusion"] = "success",
            ["event"] = "workflow_dispatch",
            ["head_sha"] = ApprovedSourceSha,
            ["source_sha"] = ApprovedSourceSha,
            ["tag_ref"] = "refs/tags/v" + ApprovedPackageVersion,
            ["tag_source_sha"] = ApprovedSourceSha,
            ["semantic_version"] = ApprovedPackageVersion,
            ["semantic_tag"] = "v" + ApprovedPackageVersion,
            ["builds_execution_sha"] = ExpectedBuildsSha,
            ["publisher_identity"] = "github-actions:semantic-release",
            ["validator_identity"] = "hexalith-builds:" + ExpectedBuildsSha,
            ["package_version"] = ApprovedPackageVersion,
            ["package_hash_manifest_sha256"] = packageManifestSha256,
            ["registry"] = ExpectedRegistry,
            ["container_repository"] = ExpectedContainerRepository,
            ["index_digest"] = candidate["oci"]!["index_digest"]!.GetValue<string>(),
            ["authority_record_sha256"] = authorityRecordSha256,
            ["deployment_action_at"] = deploymentActionAt.ToString("O"),
            ["result"] = "pass",
        };
    }

    private static byte[] WriteCoreManifest(JsonObject crosswalk, string evidence)
    {
        string[] paths = ExpectedCoreFilesFor(crosswalk);
        string text = string.Join(
            '\n',
            paths.Select(path => ComputeSha256(ResolveWithin(evidence, path)) + "  " + path)) + "\n";
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        File.WriteAllBytes(Path.Combine(evidence, "evidence-core-sha256.txt"), bytes);
        return bytes;
    }

    private static (byte[] CrosswalkBytes, byte[] SubjectBytes, byte[] CoreBytes) RefreshReviewBindings(
        JsonObject crosswalk,
        string evidence,
        byte[] proofBytes)
    {
        byte[] coreBytes = WriteCoreManifest(crosswalk, evidence);
        byte[] crosswalkBytes = JsonSerializer.SerializeToUtf8Bytes(crosswalk);
        JsonObject subject = CreatePassingReviewSubject(
            crosswalk,
            crosswalkBytes,
            coreBytes,
            proofBytes,
            DateTimeOffset.UtcNow);
        byte[] subjectBytes = JsonSerializer.SerializeToUtf8Bytes(subject);
        CreateAcceptanceReceipts(evidence, subjectBytes);
        return (crosswalkBytes, subjectBytes, coreBytes);
    }

    private static bool EvaluateWithFreshReview(
        JsonObject crosswalk,
        string repositoryRoot,
        string evidence,
        byte[] proofBytes,
        string packageManifestSha256)
    {
        (byte[] crosswalkBytes, byte[] subjectBytes, byte[] coreBytes) =
            RefreshReviewBindings(crosswalk, evidence, proofBytes);
        return EvaluateClosure(
            crosswalk,
            crosswalkBytes,
            subjectBytes,
            repositoryRoot,
            evidence,
            coreBytes,
            proofBytes,
            packageManifestSha256);
    }

    private static void PersistRuntimeBindings(JsonObject runtime, string evidence)
    {
        JsonObject smoke = JsonNode.Parse(ReadEvidenceFile(evidence, "smoke-results.json"))!.AsObject();
        smoke["tool"] = runtime["tool"]!.DeepClone();
        smoke["command"] = runtime["command"]!.DeepClone();
        smoke["started_at"] = runtime["started_at"]!.DeepClone();
        smoke["ended_at"] = runtime["ended_at"]!.DeepClone();
        smoke["exit_code"] = runtime["exit_code"]!.DeepClone();
        smoke["exit_code_verification"] = runtime["exit_code_verification"]!.DeepClone();
        smoke["platforms"] = runtime["platforms"]!.DeepClone();
        byte[] smokeBytes = JsonSerializer.SerializeToUtf8Bytes(smoke);
        File.WriteAllBytes(Path.Combine(evidence, "smoke-results.json"), smokeBytes);
        runtime["smoke_results"]!["sha256"] = ComputeSha256(smokeBytes);

        JsonObject retained = Clone(runtime);
        retained.Remove("citation");
        File.WriteAllBytes(
            Path.Combine(evidence, runtime["citation"]!.GetValue<string>()),
            JsonSerializer.SerializeToUtf8Bytes(retained));
    }

    private static JsonObject CreatePassingReviewSubject(
        JsonObject crosswalk,
        byte[] crosswalkBytes,
        byte[] coreBytes,
        byte[] proofBytes,
        DateTimeOffset createdAt)
    {
        JsonObject candidate = crosswalk["selected_candidates"]![0]!.AsObject();
        return new JsonObject
        {
            ["schema"] = "hexalith.eventstore.story-3-13-review-subject/v2",
            ["created_at"] = createdAt.ToString("O"),
            ["proposed_decision"] = "pass",
            ["identity_crosswalk"] = Binding("identity-crosswalk.json", crosswalkBytes),
            ["evidence_core_manifest"] = Binding("evidence-core-sha256.txt", coreBytes),
            ["proof_packet"] = Binding(ProofRelativePath, proofBytes),
            ["identity"] = new JsonObject
            {
                ["source_sha"] = ApprovedSourceSha,
                ["package_version"] = candidate["packages"]!["version"]!.GetValue<string>(),
                ["package_hash_manifest_sha256"] =
                    candidate["packages"]!["hash_manifest_sha256"]!.GetValue<string>(),
                ["release_version"] = candidate["release"]!["semantic_version"]!.GetValue<string>(),
                ["workflow_run"] = candidate["release"]!["workflow_run"]!.GetValue<long>(),
                ["authority_record_sha256"] =
                    candidate["release_authority"]!["record_sha256"]!.GetValue<string>(),
                ["index_digest"] = candidate["oci"]!["index_digest"]!.GetValue<string>(),
                ["canonical_lineage_id"] = candidate["lineage_id"]!.GetValue<string>(),
            },
            ["passing_evidence"] = new JsonArray("Every independently retained lineage check passed."),
            ["blockers"] = new JsonArray(),
            ["limitations"] = crosswalk["limitations"]!.DeepClone(),
            ["required_acceptances"] = new JsonArray(RequiredRoles.Select(role => (JsonNode)new JsonObject
            {
                ["role"] = role,
                ["status"] = "required",
            }).ToArray()),
        };
    }

    private static void CreateAcceptanceReceipts(string evidence, byte[] subjectBytes)
    {
        JsonObject subject = JsonNode.Parse(subjectBytes)!.AsObject();
        string subjectHash = ComputeSha256(subjectBytes);
        string acceptancesRoot = Path.Combine(evidence, "acceptances");
        if (Directory.Exists(acceptancesRoot))
        {
            Directory.Delete(acceptancesRoot, recursive: true);
        }

        string directory = Path.Combine(acceptancesRoot, subjectHash);
        Directory.CreateDirectory(directory);
        string sourcesDirectory = Path.Combine(directory, "sources");
        Directory.CreateDirectory(sourcesDirectory);
        DateTimeOffset acceptedAt = DateTimeOffset.Parse(
            subject["created_at"]!.GetValue<string>(),
            CultureInfo.InvariantCulture).AddMinutes(1);
        foreach (string role in RequiredRoles)
        {
            string identity = role == "test-architect" ? "bmad:murat" : "github:jpiquot";
            string acceptedScope = "Story 3.13 deployed-runtime parity closure for " + subjectHash;
            JsonObject source = new()
            {
                ["schema"] = "hexalith.eventstore.story-3-13-acceptance-source/v1",
                ["repository"] = ExpectedRepository,
                ["source_url"] = "https://github.com/" + ExpectedRepository + "/commit/" +
                    ApprovedSourceSha + "#story-3-13-" + subjectHash + "-" + role,
                ["captured_at"] = acceptedAt.ToString("O"),
                ["role"] = role,
                ["reviewer_identity"] = identity,
                ["subject_sha256"] = subjectHash,
                ["decision"] = "accepted",
                ["accepted_scope"] = acceptedScope,
                ["accepted_limitations"] = subject["limitations"]!.DeepClone(),
            };
            byte[] sourceBytes = JsonSerializer.SerializeToUtf8Bytes(source);
            File.WriteAllBytes(Path.Combine(sourcesDirectory, role + ".json"), sourceBytes);
            JsonObject receipt = new()
            {
                ["schema"] = "hexalith.eventstore.story-3-13-acceptance-receipt/v1",
                ["role"] = role,
                ["reviewer_identity"] = identity,
                ["accepted_at"] = acceptedAt.ToString("O"),
                ["durable_source"] = new JsonObject
                {
                    ["kind"] = "retained-immutable-external-record",
                    ["path"] = "sources/" + role + ".json",
                    ["sha256"] = ComputeSha256(sourceBytes),
                },
                ["accepted_scope"] = acceptedScope,
                ["accepted_limitations"] = subject["limitations"]!.DeepClone(),
                ["decision"] = "accepted",
                ["subject_sha256"] = subjectHash,
            };
            File.WriteAllBytes(
                Path.Combine(directory, role + ".json"),
                JsonSerializer.SerializeToUtf8Bytes(receipt));
        }
    }

    private static void RemoveReceiptField(
        string evidence,
        byte[] subjectBytes,
        string role,
        string field) => MutateReceipt(evidence, subjectBytes, role, receipt => receipt.Remove(field));

    private static void MutateReceipt(
        string evidence,
        byte[] subjectBytes,
        string role,
        Action<JsonObject> mutate)
    {
        string subjectHash = ComputeSha256(subjectBytes);
        string path = Path.Combine(evidence, "acceptances", subjectHash, role + ".json");
        JsonObject receipt = JsonNode.Parse(File.ReadAllBytes(path))!.AsObject();
        mutate(receipt);
        File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(receipt));
    }

    private static JsonObject Binding(string path, byte[] bytes) => new()
    {
        ["path"] = path,
        ["sha256"] = ComputeSha256(bytes),
    };

    private static bool VerifyChecksumManifest(byte[] manifestBytes, string basePath, string[] expectedPaths)
    {
        try
        {
            Dictionary<string, string> entries = ParseChecksumManifest(manifestBytes);
            string[] actualPaths = entries.Keys.ToArray();
            string[] expected = expectedPaths.Order(StringComparer.Ordinal).ToArray();
            return actualPaths.SequenceEqual(expected, StringComparer.Ordinal)
                && entries.All(entry => ComputeSha256(ResolveWithin(basePath, entry.Key)) == entry.Value);
        }
        catch (Exception exception) when (
            exception is ArgumentException
            or InvalidDataException
            or IOException
            or NotSupportedException
            or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static Dictionary<string, string> ParseChecksumManifest(byte[] manifestBytes)
    {
        string text = Encoding.UTF8.GetString(manifestBytes);
        string[] lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0)
        {
            throw new InvalidDataException("Checksum manifest is empty.");
        }

        Dictionary<string, string> entries = new(StringComparer.Ordinal);
        foreach (string rawLine in lines)
        {
            string line = rawLine.TrimEnd('\r');
            string[] parts = line.Split("  ", 2, StringSplitOptions.None);
            if (parts.Length != 2
                || parts[0].Length != 64
                || !parts[0].All(character => Uri.IsHexDigit(character))
                || string.IsNullOrWhiteSpace(parts[1])
                || !entries.TryAdd(parts[1], parts[0].ToLowerInvariant()))
            {
                throw new InvalidDataException("Malformed or duplicate checksum entry.");
            }
        }

        return entries;
    }

    private static byte[] ReadEvidenceFile(string root, string relativePath) =>
        File.ReadAllBytes(ResolveWithin(root, relativePath));

    private static string ResolveWithin(string root, string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            throw new InvalidDataException("Absolute evidence path is forbidden.");
        }

        string canonicalRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string target = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!target.StartsWith(canonicalRoot, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Evidence path escapes its root.");
        }

        string current = canonicalRoot.TrimEnd(Path.DirectorySeparatorChar);
        if (File.Exists(current) || Directory.Exists(current))
        {
            RejectReparsePoint(current);
        }

        foreach (string segment in Path.GetRelativePath(current, target)
            .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (File.Exists(current) || Directory.Exists(current))
            {
                RejectReparsePoint(current);
            }
        }

        return target;
    }

    private static void RejectReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("Evidence paths cannot contain symbolic links or reparse points.");
        }
    }

    private static JsonObject LoadCrosswalk(string root)
    {
        string evidence = ResolveWithin(root, EvidenceRelativePath);
        return JsonNode.Parse(ReadEvidenceFile(evidence, "identity-crosswalk.json"))!.AsObject();
    }

    private static JsonObject Clone(JsonObject value) => value.DeepClone().AsObject();

    private static string ComputeSha256(string path) => ComputeSha256(File.ReadAllBytes(path));

    private static string ComputeSha256(byte[] bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static string RunGit(string root, params string[] arguments)
    {
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo("git")
            {
                WorkingDirectory = root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            },
        };
        foreach (string argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        try
        {
            process.Start();
        }
        catch (Win32Exception exception)
        {
            throw new InvalidDataException("Git object verification failed to start git.", exception);
        }

        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        if (!WaitForProcessExit(process, TimeSpan.FromSeconds(30)))
        {
            throw new TimeoutException(
                "Git object verification timed out after 30 seconds: " + string.Join(' ', arguments));
        }

        string output = outputTask.GetAwaiter().GetResult();
        string error = errorTask.GetAwaiter().GetResult();
        if (process.ExitCode != 0)
        {
            throw new InvalidDataException("Git object verification failed: " + error.Trim());
        }

        return output.Trim();
    }

    // The pinned Builds revision is historical, so the tool bytes are read from the submodule
    // object store rather than the live worktree, which tracks a different gitlink.
    private static string ComputePinnedBuildsToolSha256(string repositoryRoot, string toolPath) =>
        ComputePinnedBuildsToolSha256(repositoryRoot, toolPath, ExpectedBuildsSha);

    private static string ComputePinnedBuildsToolSha256(
        string repositoryRoot,
        string toolPath,
        string buildsSha)
    {
        const string buildsPrefix = "references/Hexalith.Builds/";
        if (!toolPath.StartsWith(buildsPrefix, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Shared tool path is outside the Builds submodule.");
        }

        if (string.IsNullOrWhiteSpace(buildsSha) || buildsSha.Length != 40)
        {
            throw new InvalidDataException("Shared Builds tool verification requires a 40-character Builds SHA.");
        }

        using Process process = new()
        {
            StartInfo = new ProcessStartInfo("git")
            {
                WorkingDirectory = Path.Combine(repositoryRoot, "references", "Hexalith.Builds"),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            },
        };
        process.StartInfo.ArgumentList.Add("show");
        process.StartInfo.ArgumentList.Add(buildsSha + ":" + toolPath[buildsPrefix.Length..]);
        try
        {
            process.Start();
        }
        catch (Win32Exception exception)
        {
            throw new InvalidDataException(
                "Shared Builds tool verification failed to start git: " + toolPath,
                exception);
        }

        using MemoryStream buffer = new();
        Task copyTask = process.StandardOutput.BaseStream.CopyToAsync(buffer);
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        if (!WaitForProcessExit(process, TimeSpan.FromSeconds(30)))
        {
            throw new TimeoutException(
                "Shared Builds tool verification timed out after 30 seconds: " + toolPath);
        }

        copyTask.GetAwaiter().GetResult();
        string error = errorTask.GetAwaiter().GetResult();
        return process.ExitCode == 0
            ? ComputeSha256(buffer.ToArray())
            : throw new InvalidDataException("Shared Builds tool verification failed: " + error.Trim());
    }

    private static bool WaitForProcessExit(Process process, TimeSpan timeout)
    {
        if (process.WaitForExit((int)timeout.TotalMilliseconds))
        {
            return true;
        }

        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // The process exited between WaitForExit and Kill.
        }

        try
        {
            _ = process.WaitForExit(5_000);
        }
        catch (SystemException)
        {
            // Best-effort cleanup after a forced kill.
        }

        // Never treat a timed-out (and possibly truncated) process as success. Callers must not
        // hash or parse stdout after a kill/timeout window.
        return false;
    }

    private static bool RuntimeFailureClassificationIsValid(JsonObject node)
    {
        string outcome = node["outcome"]!.GetValue<string>();
        bool hasClass = node.TryGetPropertyValue("failure_class", out JsonNode? classNode);
        if (outcome == "pass")
        {
            return !hasClass;
        }

        return hasClass
            && classNode is JsonValue classValue
            && classValue.GetValue<string>() is "environment" or "product" or "evidence";
    }

    private static bool RuntimeFailureClassificationMatchesLog(JsonObject node, JsonObject log)
    {
        bool nodeHasClass = node.TryGetPropertyValue("failure_class", out JsonNode? nodeClass);
        bool logHasClass = log.TryGetPropertyValue("failure_class", out JsonNode? logClass);
        return nodeHasClass == logHasClass
            && (!nodeHasClass
                || (nodeClass is JsonValue nodeValue
                    && logClass is JsonValue logValue
                    && nodeValue.GetValue<string>() == logValue.GetValue<string>()));
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        Stack<string> directories = new();
        directories.Push(source);
        while (directories.Count > 0)
        {
            string current = directories.Pop();
            foreach (string entry in Directory.EnumerateFileSystemEntries(current))
            {
                FileAttributes attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidDataException("Evidence copies cannot traverse symbolic links or reparse points.");
                }

                string target = Path.Combine(destination, Path.GetRelativePath(source, entry));
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    Directory.CreateDirectory(target);
                    directories.Push(entry);
                }
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Copy(entry, target, overwrite: true);
                }
            }
        }
    }

    private static void DeleteTemporaryDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
            // Cleanup must not mask the contract-test assertion that owns the temporary tree.
        }
        catch (UnauthorizedAccessException)
        {
            // Cleanup is best effort on platforms that briefly retain handles after process exit.
        }
    }

    private static string RebindAmd64ConfigArchitecture(
        JsonObject crosswalk,
        string evidence,
        string architecture,
        bool reformatBytes = false)
    {
        JsonObject oci = crosswalk["selected_candidates"]![0]!["oci"]!.AsObject();
        JsonObject child = oci["children"]!.AsArray().Select(item => item!.AsObject()).Single(item =>
            item["platform"]!.GetValue<string>() == "linux/amd64");
        string configFile = child["config_raw_file"]!.GetValue<string>();
        string manifestFile = child["manifest_raw_file"]!.GetValue<string>();
        JsonObject config = JsonNode.Parse(File.ReadAllBytes(Path.Combine(evidence, configFile)))!.AsObject();
        config["architecture"] = architecture;
        byte[] configBytes = JsonSerializer.SerializeToUtf8Bytes(
            config,
            new JsonSerializerOptions { WriteIndented = reformatBytes });
        string configDigest = "sha256:" + ComputeSha256(configBytes);
        File.WriteAllBytes(Path.Combine(evidence, configFile), configBytes);

        JsonObject manifest = new()
        {
            ["schemaVersion"] = 2,
            ["mediaType"] = OciManifestMediaType,
            ["config"] = new JsonObject
            {
                ["mediaType"] = OciConfigMediaType,
                ["digest"] = configDigest,
                ["size"] = configBytes.Length,
            },
            ["layers"] = new JsonArray(),
        };
        byte[] manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifest);
        string manifestDigest = "sha256:" + ComputeSha256(manifestBytes);
        File.WriteAllBytes(Path.Combine(evidence, manifestFile), manifestBytes);

        child["config_digest"] = configDigest;
        child["config_size"] = configBytes.Length;
        child["config_raw_sha256"] = configDigest[7..];
        child["manifest_digest"] = manifestDigest;
        child["manifest_size"] = manifestBytes.Length;
        child["manifest_raw_sha256"] = manifestDigest[7..];

        JsonObject index = JsonNode.Parse(File.ReadAllBytes(Path.Combine(evidence, "index.raw")))!.AsObject();
        JsonObject descriptor = index["manifests"]!.AsArray().Select(item => item!.AsObject()).Single(item =>
            item["platform"]!["architecture"]!.GetValue<string>() == "amd64");
        descriptor["digest"] = manifestDigest;
        descriptor["size"] = manifestBytes.Length;
        byte[] indexBytes = JsonSerializer.SerializeToUtf8Bytes(index);
        string indexHash = ComputeSha256(indexBytes);
        string indexDigest = "sha256:" + indexHash;
        foreach (string file in new[] { "index.raw", "tag-response.raw", "digest-response.raw" })
        {
            File.WriteAllBytes(Path.Combine(evidence, file), indexBytes);
        }

        oci["index_digest"] = indexDigest;
        oci["index_raw_sha256"] = indexHash;
        oci["index_size"] = indexBytes.Length;
        oci["immutable_reference"] = ExpectedRegistry + "/" + ExpectedContainerRepository + "@" + indexDigest;
        foreach (string name in new[] { "tag", "digest" })
        {
            oci[name + "_response_raw_sha256"] = indexHash;
            oci[name + "_response_size"] = indexBytes.Length;
            oci[name + "_response_docker_content_digest"] = indexDigest;
        }

        JsonObject readback = JsonNode.Parse(ReadEvidenceFile(evidence, "registry-readback.json"))!.AsObject();
        readback["immutable_index_digest"] = indexDigest;
        readback["tag_response"] = IndexResponse(
            "tag",
            "v" + ApprovedPackageVersion,
            indexDigest,
            indexBytes);
        readback["digest_response"] = IndexResponse("digest", indexDigest, indexDigest, indexBytes);
        JsonArray objects = readback["objects"]!.AsArray();
        for (int indexObject = 0; indexObject < objects.Count; indexObject++)
        {
            JsonObject response = objects[indexObject]!.AsObject();
            if (response["raw_file"]!.GetValue<string>() == manifestFile)
            {
                objects[indexObject] = ObjectResponse(
                    "child-manifest",
                    manifestDigest,
                    OciManifestMediaType,
                    manifestFile,
                    manifestBytes);
            }
            else if (response["raw_file"]!.GetValue<string>() == configFile)
            {
                objects[indexObject] = ObjectResponse(
                    "config",
                    configDigest,
                    OciConfigMediaType,
                    configFile,
                    configBytes);
            }
        }

        File.WriteAllBytes(
            Path.Combine(evidence, "registry-readback.json"),
            JsonSerializer.SerializeToUtf8Bytes(readback));

        JsonObject validation = JsonNode.Parse(ReadEvidenceFile(evidence, "oci-validation.json"))!.AsObject();
        validation["immutable_reference"] =
            ExpectedRegistry + "/" + ExpectedContainerRepository + "@" + indexDigest;
        validation["index_digest"] = indexDigest;
        validation["index_size"] = indexBytes.Length;
        validation["raw_index_sha256"] = indexHash;
        foreach (JsonObject validationChild in validation["children"]!.AsArray().Select(item => item!.AsObject()))
        {
            if (validationChild["platform"]!.GetValue<string>() != "linux/amd64")
            {
                continue;
            }

            validationChild["manifest_digest"] = manifestDigest;
            validationChild["manifest_size"] = manifestBytes.Length;
            validationChild["manifest_raw_sha256"] = manifestDigest[7..];
            validationChild["config_digest"] = configDigest;
            validationChild["config_size"] = configBytes.Length;
            validationChild["config_raw_sha256"] = configDigest[7..];
        }

        File.WriteAllBytes(
            Path.Combine(evidence, "oci-validation.json"),
            JsonSerializer.SerializeToUtf8Bytes(validation));

        foreach (JsonObject platform in crosswalk["selected_candidates"]![0]!["runtime"]!["platforms"]!
            .AsArray().Select(item => item!.AsObject()))
        {
            if (platform["platform"]!.GetValue<string>() == "linux/amd64")
            {
                platform["child_digest"] = manifestDigest;
                JsonObject log = JsonNode.Parse(ReadEvidenceFile(evidence, platform["log"]!.GetValue<string>()))!
                    .AsObject();
                log["child_digest"] = manifestDigest;
                byte[] logBytes = JsonSerializer.SerializeToUtf8Bytes(log);
                File.WriteAllBytes(Path.Combine(evidence, platform["log"]!.GetValue<string>()), logBytes);
                platform["log_sha256"] = ComputeSha256(logBytes);
            }
        }

        string parent = Path.GetDirectoryName(evidence)!;
        string rebound = Path.Combine(parent, indexHash);
        Directory.Move(evidence, rebound);
        return rebound;
    }

    private static string RebindIndex(JsonObject crosswalk, string evidence, Action<JsonObject> mutate)
    {
        JsonObject oci = crosswalk["selected_candidates"]![0]!["oci"]!.AsObject();
        JsonObject index = JsonNode.Parse(File.ReadAllBytes(Path.Combine(evidence, "index.raw")))!.AsObject();
        mutate(index);
        byte[] indexBytes = JsonSerializer.SerializeToUtf8Bytes(index);
        string indexHash = ComputeSha256(indexBytes);
        string indexDigest = "sha256:" + indexHash;
        foreach (string file in new[] { "index.raw", "tag-response.raw", "digest-response.raw" })
        {
            File.WriteAllBytes(Path.Combine(evidence, file), indexBytes);
        }

        oci["index_digest"] = indexDigest;
        oci["index_raw_sha256"] = indexHash;
        oci["index_size"] = indexBytes.Length;
        oci["immutable_reference"] = ExpectedRegistry + "/" + ExpectedContainerRepository + "@" + indexDigest;
        foreach (string name in new[] { "tag", "digest" })
        {
            oci[name + "_response_raw_sha256"] = indexHash;
            oci[name + "_response_size"] = indexBytes.Length;
            oci[name + "_response_docker_content_digest"] = indexDigest;
        }

        JsonObject readback = JsonNode.Parse(ReadEvidenceFile(evidence, "registry-readback.json"))!.AsObject();
        readback["immutable_index_digest"] = indexDigest;
        readback["tag_response"] = IndexResponse("tag", "v" + ApprovedPackageVersion, indexDigest, indexBytes);
        readback["digest_response"] = IndexResponse("digest", indexDigest, indexDigest, indexBytes);
        File.WriteAllBytes(
            Path.Combine(evidence, "registry-readback.json"),
            JsonSerializer.SerializeToUtf8Bytes(readback));

        JsonObject validation = JsonNode.Parse(ReadEvidenceFile(evidence, "oci-validation.json"))!.AsObject();
        validation["immutable_reference"] = oci["immutable_reference"]!.DeepClone();
        validation["index_digest"] = indexDigest;
        validation["index_size"] = indexBytes.Length;
        validation["raw_index_sha256"] = indexHash;
        File.WriteAllBytes(
            Path.Combine(evidence, "oci-validation.json"),
            JsonSerializer.SerializeToUtf8Bytes(validation));

        string parent = Path.GetDirectoryName(evidence)!;
        string rebound = Path.Combine(parent, indexHash);
        Directory.Move(evidence, rebound);
        return rebound;
    }

    private static string[] ReadSignificantGitignorePatterns(string root)
    {
        return File.ReadAllLines(Path.Combine(root, ".gitignore"))
            .Select(static line => line.Trim())
            .Where(static line => line.Length > 0 && !line.StartsWith("#", StringComparison.Ordinal))
            .ToArray();
    }

    private static bool IsIgnoredByGit(string root, string relativePath)
    {
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo("git")
            {
                WorkingDirectory = root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            },
        };
        process.StartInfo.ArgumentList.Add("check-ignore");
        process.StartInfo.ArgumentList.Add("--no-index");
        process.StartInfo.ArgumentList.Add("-v");
        process.StartInfo.ArgumentList.Add("--");
        process.StartInfo.ArgumentList.Add(relativePath);

        try
        {
            process.Start();
        }
        catch (Win32Exception exception)
        {
            throw new InvalidDataException("Git ignore verification failed to start git.", exception);
        }

        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        if (!WaitForProcessExit(process, TimeSpan.FromSeconds(30)))
        {
            throw new TimeoutException(
                "Git ignore verification timed out after 30 seconds: check-ignore --no-index -v -- "
                + relativePath);
        }

        string output = outputTask.GetAwaiter().GetResult();
        string error = errorTask.GetAwaiter().GetResult();
        if (process.ExitCode == 1)
        {
            return false;
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidDataException("Git ignore verification failed: " + error.Trim());
        }

        if (string.IsNullOrWhiteSpace(output))
        {
            throw new InvalidDataException(
                "Git check-ignore reported ignored without a verbose matching line for "
                + relativePath
                + ".");
        }

        string patternField = output.Split('\t', 2)[0];
        int lastColon = patternField.LastIndexOf(':');
        string pattern = lastColon >= 0 ? patternField[(lastColon + 1)..] : patternField;
        return !pattern.StartsWith('!');
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Hexalith.EventStore.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Hexalith.EventStore repository root.");
    }
}
