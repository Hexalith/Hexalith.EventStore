using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
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
        RunGit(root, "rev-parse", "HEAD:" + predecessorPrefix.TrimEnd('/'))
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
                return;
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
    /// Verifies private IPv4/IPv6 addresses cannot be retained as support-safe evidence values.
    /// </summary>
    [Fact]
    public void SupportSafeValuesRejectPrivateIpv6Addresses()
    {
        ValueIsSupportSafe("fd00::1").ShouldBeFalse();
        ValueIsSupportSafe("fc00::abcd").ShouldBeFalse();
        ValueIsSupportSafe("fe80::1").ShouldBeFalse();
        ValueIsSupportSafe("fec0::1").ShouldBeFalse();
        ValueIsSupportSafe("https://[fd00::1]/status").ShouldBeFalse();
        ValueIsSupportSafe("2606:4700:4700::1111").ShouldBeTrue();
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
        }
        finally
        {
            Directory.Delete(cleanupRoot, recursive: true);
        }
    }

    /// <summary>
    /// Verifies actual evidence stays fail-closed even if its declared verdict is tampered to pass.
    /// </summary>
    [Fact]
    public void DerivedClosureRejectsActualIncompleteLineageAndDeclarativeTampering()
    {
        string root = FindRepositoryRoot();
        string evidence = Path.Combine(root, EvidenceRelativePath);
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
            ValidateEvidenceIntegrity(crosswalk, root, evidence, coreBytes).ShouldBeTrue();
            ValidatePackages(crosswalk, root, evidence, packageManifestSha256).ShouldBeTrue();
            ValidatePackageBytes(crosswalk["selected_candidates"]![0]!.AsObject(), evidence).ShouldBeTrue();
            ValidateRelease(crosswalk["selected_candidates"]![0]!.AsObject(), root, evidence).ShouldBeTrue();
            ValidateOciGraph(crosswalk, root, evidence).ShouldBeTrue();
            ValidateOciProvenance(crosswalk, evidence).ShouldBeTrue();
            ValidateRuntimeExecution(crosswalk, root, evidence).ShouldBeTrue();
            ValidateRuntimeEquivalence(crosswalk).ShouldBeTrue();
            ValidateDeploymentAuthority(crosswalk, root, evidence).ShouldBeTrue();
            LoadReviewerRoster(crosswalk, evidence).ShouldNotBeNull();
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
            Directory.Delete(cleanupRoot, recursive: true);
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
                releaseEvidence["deployment_action_at"]!.GetValue<string>());
            authorityRecord["expires_at"] = actionAt.AddSeconds(-1).ToString("O");
            byte[] invalidAuthorityBytes = JsonSerializer.SerializeToUtf8Bytes(authorityRecord);
            File.WriteAllBytes(authorityPath, invalidAuthorityBytes);
            crosswalk["selected_candidates"]![0]!["release_authority"]!["record_sha256"] =
                ComputeSha256(invalidAuthorityBytes);

            ValidateDeploymentAuthority(crosswalk, root, evidence).ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(cleanupRoot, recursive: true);
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
            Directory.Delete(cleanupRoot, recursive: true);
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
            Directory.Delete(cleanupRoot, recursive: true);
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
            Directory.Delete(cleanupRoot, recursive: true);
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
            File.CreateSymbolicLink(receiptPath, externalReceiptPath);

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
            Directory.Delete(cleanupRoot, recursive: true);
        }
    }

    /// <summary>
    /// Verifies each named cross-lineage splice is explicitly rejected.
    /// </summary>
    /// <param name="spliceId">The prohibited splice identifier.</param>
    [Theory]
    [InlineData("story-1-20-source-packages-plus-v3.77.2-release-index")]
    [InlineData("v3.77.2-source-packages-plus-story-1-20-proof-index")]
    public void ProhibitedCrossLineageSplicesFailClosed(string spliceId)
    {
        string root = FindRepositoryRoot();
        (string cleanupRoot, string evidence, JsonObject crosswalk, _, _, byte[] coreBytes,
            byte[] proofBytes, string packageManifestSha256) = CreatePassingFixture(root);
        try
        {
            JsonObject mutated = Clone(crosswalk);
            JsonObject candidate = mutated["selected_candidates"]![0]!.AsObject();
            if (spliceId == "story-1-20-source-packages-plus-v3.77.2-release-index")
            {
                candidate["release"]!["semantic_version"] = "3.77.2";
                candidate["release"]!["semantic_tag"] = "v3.77.2";
                candidate["release"]!["source_sha"] = "77a9a442c0e6d0408957888e10c3a9accd634c99";
                candidate["oci"]!["index_digest"] =
                    "sha256:db3ab41e187efc0de397fd1205660a0f685e2c94ecd8f4a8f1843ac567056bf6";
            }
            else
            {
                candidate["source"]!["sha"] = "77a9a442c0e6d0408957888e10c3a9accd634c99";
                candidate["packages"]!["version"] = "3.77.2";
            }

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
            Directory.Delete(cleanupRoot, recursive: true);
        }
    }

    /// <summary>
    /// Verifies the recovered package directory is an exact set and every declared archive remains byte-bound.
    /// </summary>
    /// <param name="mutation">The archive mutation to apply.</param>
    [Theory]
    [InlineData("extra-archive")]
    [InlineData("mutated-bytes")]
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
            Directory.Delete(cleanupRoot, recursive: true);
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
            Directory.Delete(cleanupRoot, recursive: true);
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
            Directory.Delete(cleanupRoot, recursive: true);
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
            Directory.Delete(cleanupRoot, recursive: true);
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
            Directory.Delete(cleanupRoot, recursive: true);
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
            Directory.Delete(cleanupRoot, recursive: true);
        }
    }

    /// <summary>
    /// Verifies the independent OCI validation report is also strict and content-bound.
    /// </summary>
    /// <param name="mutation">The validation-report mutation to apply.</param>
    [Theory]
    [InlineData("schema")]
    [InlineData("repository")]
    [InlineData("raw-index")]
    [InlineData("child-digest")]
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
                case "raw-index": report["raw_index_file"] = "tag-response.raw"; break;
                case "child-digest": report["children"]![0]!["manifest_digest"] =
                    "sha256:" + new string('0', 64); break;
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
            Directory.Delete(cleanupRoot, recursive: true);
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
            Directory.Delete(cleanupRoot, recursive: true);
        }
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
                            .GetValue<string>());
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
            else if (mutation == "global-duration")
            {
                DateTimeOffset started = DateTimeOffset.Parse(runtime["started_at"]!.GetValue<string>());
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
                        string afterExecution = DateTimeOffset.Parse(runtime["ended_at"]!.GetValue<string>())
                            .AddSeconds(1).ToString("O");
                        log["ended_at"] = afterExecution;
                        platform["ended_at"] = afterExecution;
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
            RefreshReviewBindings(crosswalk, evidence, proofBytes);
            ValidateRuntimeExecution(crosswalk, root, evidence).ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(cleanupRoot, recursive: true);
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
            Directory.Delete(cleanupRoot, recursive: true);
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
    public void ReviewerRosterRejectsExtraAndUnauthorizedMappings(string mutation)
    {
        string root = FindRepositoryRoot();
        (string cleanupRoot, string evidence, JsonObject crosswalk, _, _, _, byte[] proofBytes,
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
                default: roster["undeclared"] = true; break;
            }

            byte[] rosterBytes = JsonSerializer.SerializeToUtf8Bytes(roster);
            File.WriteAllBytes(Path.Combine(evidence, ReviewerRosterFile), rosterBytes);
            crosswalk["approval_contract"]!["reviewer_roster_sha256"] = ComputeSha256(rosterBytes);
            EvaluateWithFreshReview(
                crosswalk,
                root,
                evidence,
                proofBytes,
                packageManifestSha256).ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(cleanupRoot, recursive: true);
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
            File.CreateSymbolicLink(releasePath, externalPath);
            ValidateRelease(crosswalk["selected_candidates"]![0]!.AsObject(), root, evidence).ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(cleanupRoot, recursive: true);
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
                    evidenceCoreManifestBytes)
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

            string archiveRoot = ResolveWithin(evidenceRoot, verification["archive_root"]!.GetValue<string>());
            JsonObject[] items = packages["items"]!.AsArray().Select(item => item!.AsObject()).ToArray();
            string[] expectedArchives = items.Select(item => item["archive"]!.GetValue<string>())
                .Order(StringComparer.Ordinal).ToArray();
            string[] actualArchives = Directory.GetFiles(archiveRoot, "*.nupkg", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray()!;
            return items.Length == 14
                && actualArchives.SequenceEqual(expectedArchives, StringComparer.Ordinal)
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
                "index_digest",
                "children",
                "raw_index_file",
                "raw_index_sha256",
                "raw_graph_result",
                "response_metadata_result",
                "result",
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
                || validation["index_digest"]!.GetValue<string>() != indexDigest
                || validation["raw_index_file"]!.GetValue<string>() != "index.raw"
                || validation["raw_index_sha256"]!.GetValue<string>() != indexDigest[7..]
                || validation["raw_graph_result"]!.GetValue<string>() != "pass"
                || validation["response_metadata_result"]!.GetValue<string>() != "pass"
                || validation["result"]!.GetValue<string>() != "pass"
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
                || !ValidateConfigLabelSummaries(readback["config_labels"]!.AsObject(), sourceSha))
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
                if (validationChild["manifest_digest"]!.GetValue<string>() !=
                        child["manifest_digest"]!.GetValue<string>()
                    || validationChild["manifest_size"]!.GetValue<int>() !=
                        child["manifest_size"]!.GetValue<int>()
                    || validationChild["config_digest"]!.GetValue<string>() !=
                        child["config_digest"]!.GetValue<string>()
                    || validationChild["config_size"]!.GetValue<int>() !=
                        child["config_size"]!.GetValue<int>())
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
                    || config["architecture"]!.GetValue<string>() != platformParts[1])
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

    private static bool ValidateConfigLabelSummaries(JsonObject configLabels, string sourceSha) =>
        configLabels["verification_result"]!.GetValue<string>() == "pass"
        && configLabels["provenance_label_result"]!.GetValue<string>() == "pass"
        && configLabels["exact_source_match"]!.GetValue<bool>()
        && configLabels["approved_source_sha"]!.GetValue<string>() == sourceSha
        && configLabels["revision"]!.GetValue<string>() == sourceSha;

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
                JsonObject labels = JsonNode.Parse(ReadEvidenceFile(
                    evidenceRoot,
                    child["config_raw_file"]!.GetValue<string>()))!["config"]!["Labels"]!.AsObject();
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
                || retained["schema"]!.GetValue<string>() !=
                    "hexalith.eventstore.story-3-13-runtime-verification/v2"
                || !HasExactProperties(retained, retainedFields)
                || !DocumentIsSupportSafe(retained)
                || runtime["execution_result"]!.GetValue<string>() != "pass"
                || runtime["evidence_completeness"]!.GetValue<string>() != "pass"
                || runtime["exit_code"]!.GetValue<int>() != 0
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
            if (preflight["platform"]!.GetValue<string>() != "linux/arm64"
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
                JsonObject child = oci["children"]!.AsArray().Select(item => item!.AsObject()).Single(item =>
                    item["platform"]!.GetValue<string>() == platform["platform"]!.GetValue<string>());
                if (platform["child_digest"]!.GetValue<string>() != child["manifest_digest"]!.GetValue<string>()
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
            JsonObject roster = LoadReviewerRoster(crosswalk, evidenceRoot);
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

            JsonObject subject = JsonNode.Parse(subjectBytes)!.AsObject();
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

            string[] receiptPaths = Directory.GetFiles(receiptDirectory, "*.json", SearchOption.TopDirectoryOnly);
            string[] expectedNames = RequiredRoles.Select(role => role + ".json")
                .Order(StringComparer.Ordinal).ToArray();
            if (receiptPaths.Length != 3
                || !receiptPaths.Select(Path.GetFileName).Order(StringComparer.Ordinal)
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
                || receipts.Select(receipt => receipt["role"]!.GetValue<string>())
                    .Distinct(StringComparer.Ordinal).Count() != 3
                || !receipts.Select(receipt => receipt["role"]!.GetValue<string>())
                    .Order(StringComparer.Ordinal).SequenceEqual(RequiredRoles, StringComparer.Ordinal))
            {
                return false;
            }

            DateTimeOffset subjectCreated = DateTimeOffset.Parse(subject["created_at"]!.GetValue<string>());
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
                && identity["authority_record_sha256"] is null
                && identity["canonical_lineage_id"] is null
                && identity["index_digest"]!.GetValue<string>() == ExpectedIndexDigest
                && candidate["release"]!["semantic_version"] is null
                && candidate["release"]!["workflow_run"] is null
                && candidate["release"]!["source_sha"] is null
                && !candidate["release_authority"]!["deployment_authorized"]!.GetValue<bool>()
                && verdict["decision"]!.GetValue<string>() == "fail-closed"
                && !verdict["story_may_be_done"]!.GetValue<bool>()
                && !verdict["external_state_changed"]!.GetValue<bool>()
                && !verdict["predecessor_state_changed"]!.GetValue<bool>()
                && verdict["blockers"]!.AsArray().Count > 0
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
                && created >= runtimeEnded;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
            or NullReferenceException
            or ArgumentException
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

    private static JsonObject LoadReviewerRoster(JsonObject crosswalk, string evidenceRoot)
    {
        JsonObject approval = crosswalk["approval_contract"]!.AsObject();
        string path = approval["reviewer_roster_path"]!.GetValue<string>();
        byte[] bytes = ReadEvidenceFile(evidenceRoot, path);
        JsonObject roster = JsonNode.Parse(bytes)!.AsObject();
        JsonObject roles = roster["roles"]!.AsObject();
        string[] roleNames = roles.Select(role => role.Key).Order(StringComparer.Ordinal).ToArray();
        if (path != ReviewerRosterFile
            || approval["reviewer_roster_sha256"]!.GetValue<string>() != ComputeSha256(bytes)
            || roster["schema"]!.GetValue<string>() !=
                "hexalith.eventstore.story-3-13-reviewer-roster/v1"
            || roster["repository"]!.GetValue<string>() != ExpectedRepository
            || !HasExactProperties(roster, ["schema", "repository", "roles"])
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
        byte[] coreManifestBytes)
    {
        string predecessorPrefix =
            "_bmad-output/implementation-artifacts/evidence/story-1-20/" + ApprovedSourceSha + "/";
        string[] expectedPredecessorPaths = ExpectedPredecessorFiles
            .Select(path => predecessorPrefix + path)
            .ToArray();
        byte[] retainedCore = ReadEvidenceFile(evidenceRoot, "evidence-core-sha256.txt");
        byte[] predecessorManifest = ReadEvidenceFile(evidenceRoot, "predecessor-tree-sha256.txt");
        return retainedCore.SequenceEqual(coreManifestBytes)
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
        return preflight["log_sha256"]!.GetValue<string>() == ComputeSha256(bytes)
            && HasExactProperties(log, fields)
            && DocumentIsSupportSafe(log)
            && log["schema"]!.GetValue<string>() ==
                "hexalith.eventstore.story-3-13-runtime-preflight/v1"
            && log["platform"]!.GetValue<string>() == "linux/arm64"
            && log["child_digest"]!.GetValue<string>() == arm64["manifest_digest"]!.GetValue<string>()
            && preflight["child_digest"]!.GetValue<string>() == log["child_digest"]!.GetValue<string>()
            && log["observed_runtime_platform"]!.GetValue<string>() == "linux/arm64"
            && log["exit_code"]!.GetValue<int>() == 0
            && log["outcome"]!.GetValue<string>() == "pass"
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
        return platform["log_sha256"]!.GetValue<string>() == ComputeSha256(bytes)
            && HasExactProperties(log, fields)
            && DocumentIsSupportSafe(log)
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
            && TryParseExplicitOffset(log["started_at"]!.GetValue<string>(), out DateTimeOffset startedAt)
            && TryParseExplicitOffset(log["ended_at"]!.GetValue<string>(), out DateTimeOffset endedAt)
            && platform["started_at"]!.GetValue<string>() == log["started_at"]!.GetValue<string>()
            && platform["ended_at"]!.GetValue<string>() == log["ended_at"]!.GetValue<string>()
            && startedAt >= executionStarted
            && endedAt > startedAt
            && endedAt <= executionEnded
            && endedAt - startedAt <= TimeSpan.FromSeconds(contract["timeout_seconds"]!.GetValue<int>())
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
                    && !string.IsNullOrWhiteSpace(report["blocker"]!.GetValue<string>())
                    && !string.IsNullOrWhiteSpace(report["reopen_trigger"]!.GetValue<string>())
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
                && status is >= 100 and <= 599);
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

    private static bool HostIsPrivate(string host) =>
        IPAddress.TryParse(host.Trim('[', ']'), out IPAddress? address) && AddressIsPrivate(address);

    private static bool AddressIsPrivate(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            return AddressIsPrivate(address.MapToIPv4());
        }

        byte[] bytes = address.GetAddressBytes();
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
        return hasOffset && DateTimeOffset.TryParse(value, out result);
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

        WriteReviewerRoster(evidence);
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
            "This acceptance authorizes no package publication, registry mutation, deployment mutation, consumer migration, predecessor change, or Epic 1 change.",
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
                ["index_digest"] = indexDigest,
                ["children"] = new JsonArray(children.Select(item => (JsonNode)new JsonObject
                {
                    ["platform"] = item!["platform"]!.GetValue<string>(),
                    ["manifest_digest"] = item["manifest_digest"]!.GetValue<string>(),
                    ["manifest_size"] = item["manifest_size"]!.GetValue<int>(),
                    ["config_digest"] = item["config_digest"]!.GetValue<string>(),
                    ["config_size"] = item["config_size"]!.GetValue<int>(),
                }).ToArray()),
                ["raw_index_file"] = "index.raw",
                ["raw_index_sha256"] = indexHash,
                ["raw_graph_result"] = "pass",
                ["response_metadata_result"] = "pass",
                ["result"] = "pass",
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

    private static void WriteReviewerRoster(string evidence)
    {
        JsonObject roster = new()
        {
            ["schema"] = "hexalith.eventstore.story-3-13-reviewer-roster/v1",
            ["repository"] = "Hexalith/Hexalith.EventStore",
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
        string directory = Path.Combine(evidence, "acceptances", subjectHash);
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }

        Directory.CreateDirectory(directory);
        string sourcesDirectory = Path.Combine(directory, "sources");
        Directory.CreateDirectory(sourcesDirectory);
        DateTimeOffset acceptedAt = DateTimeOffset.Parse(subject["created_at"]!.GetValue<string>()).AddMinutes(1);
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

        process.Start();
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
    private static string ComputePinnedBuildsToolSha256(string repositoryRoot, string toolPath)
    {
        const string buildsPrefix = "references/Hexalith.Builds/";
        if (!toolPath.StartsWith(buildsPrefix, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Shared tool path is outside the Builds submodule.");
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
        process.StartInfo.ArgumentList.Add(ExpectedBuildsSha + ":" + toolPath[buildsPrefix.Length..]);
        process.Start();
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

        return false;
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
