using System.Diagnostics;
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
        "runtime-verification.json",
        "smoke-linux-amd64.log",
        "smoke-linux-arm64.log",
        "smoke-preflight.log",
        "smoke-results.json",
        "tag-response.raw",
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
        JsonObject crosswalk = LoadCrosswalk(root);

        ValidatePackages(crosswalk, root).ShouldBeTrue();

        JsonObject mutated = Clone(crosswalk);
        mutated["selected_candidates"]![0]!["packages"]!["items"]![0]!["project"] = "src/wrong.csproj";
        ValidatePackages(mutated, root).ShouldBeFalse();
        mutated = Clone(crosswalk);
        mutated["selected_candidates"]![0]!["packages"]!["items"]![0]!["archive"] = "wrong.nupkg";
        ValidatePackages(mutated, root).ShouldBeFalse();
        mutated = Clone(crosswalk);
        mutated["selected_candidates"]![0]!["packages"]!["items"]![0]!["sha256"] = new string('0', 64);
        ValidatePackages(mutated, root).ShouldBeFalse();
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
            ["evidence-core-sha256.txt", "identity-crosswalk.json"]).ShouldBeTrue();
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
    /// Verifies the raw OCI graph and independent tag and digest response bodies.
    /// </summary>
    [Fact]
    public void RawOciGraphAndIndependentRegistryResponsesAreContentBound()
    {
        string root = FindRepositoryRoot();
        string evidence = Path.Combine(root, EvidenceRelativePath);
        JsonObject crosswalk = LoadCrosswalk(root);

        ValidateOciGraph(crosswalk, evidence).ShouldBeTrue();

        JsonObject mutated = Clone(crosswalk);
        mutated["selected_candidates"]![0]!["oci"]!["children"]![0]!["manifest_raw_file"] =
            "child-linux-arm64.manifest.raw";
        ValidateOciGraph(mutated, evidence).ShouldBeFalse();
        mutated = Clone(crosswalk);
        mutated["selected_candidates"]![0]!["oci"]!["index_raw_file"] = "../index.raw";
        ValidateOciGraph(mutated, evidence).ShouldBeFalse();
        mutated = Clone(crosswalk);
        mutated["selected_candidates"]![0]!["oci"]!["tag_response_raw_file"] = "/tmp/index.raw";
        ValidateOciGraph(mutated, evidence).ShouldBeFalse();
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
    /// Verifies both executions are bound to their child digests and logs, while Production equivalence remains open.
    /// </summary>
    [Fact]
    public void RuntimeExecutionsPassButProductionContractEquivalenceFails()
    {
        string root = FindRepositoryRoot();
        string evidence = Path.Combine(root, EvidenceRelativePath);
        JsonObject crosswalk = LoadCrosswalk(root);

        ValidateRuntimeExecution(crosswalk, evidence).ShouldBeTrue();
        ValidateRuntimeEquivalence(crosswalk).ShouldBeFalse();
        JsonObject runtime = crosswalk["selected_candidates"]![0]!["runtime"]!.AsObject();
        runtime["contract"]!["actual_hosting_environment"]!.GetValue<string>().ShouldBe("Development");
        runtime["contract"]!["required_hosting_environment"]!.GetValue<string>().ShouldBe("Production");

        JsonObject mutated = Clone(crosswalk);
        mutated["selected_candidates"]![0]!["runtime"]!["platforms"]![0]!["child_digest"] =
            "sha256:" + new string('0', 64);
        ValidateRuntimeExecution(mutated, evidence).ShouldBeFalse();
        mutated = Clone(crosswalk);
        mutated["selected_candidates"]![0]!["runtime"]!["platforms"]![0]!["log"] = "../smoke.log";
        ValidateRuntimeExecution(mutated, evidence).ShouldBeFalse();
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

        EvaluateClosure(crosswalk, crosswalkBytes, subjectBytes, [], root, evidence, coreBytes, proofBytes)
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
        EvaluateClosure(tampered, tamperedBytes, subjectBytes, [], root, evidence, coreBytes, proofBytes)
            .ShouldBeFalse();
    }

    /// <summary>
    /// Verifies a synthetic complete lineage passes only when every derived fact is present.
    /// </summary>
    [Fact]
    public void CompleteDerivedLineagePassesAndMissingChecksOrBlockersFail()
    {
        string root = FindRepositoryRoot();
        (string evidence, JsonObject crosswalk, byte[] crosswalkBytes, byte[] subjectBytes,
            JsonObject[] receipts, byte[] coreBytes, byte[] proofBytes) = CreatePassingFixture(root);
        try
        {
            EvaluateClosure(
                crosswalk,
                crosswalkBytes,
                subjectBytes,
                receipts,
                root,
                evidence,
                coreBytes,
                proofBytes).ShouldBeTrue();

            JsonObject mutated = Clone(crosswalk);
            mutated["verdict"]!["checks"]!.AsObject().Remove("oci_graph");
            EvaluateClosure(
                mutated,
                JsonSerializer.SerializeToUtf8Bytes(mutated),
                subjectBytes,
                receipts,
                root,
                evidence,
                coreBytes,
                proofBytes).ShouldBeFalse();

            mutated = Clone(crosswalk);
            mutated["selected_candidates"]![0]!["release"]!.AsObject().Remove("publisher_identity");
            EvaluateClosure(
                mutated,
                JsonSerializer.SerializeToUtf8Bytes(mutated),
                subjectBytes,
                receipts,
                root,
                evidence,
                coreBytes,
                proofBytes).ShouldBeFalse();

            mutated = Clone(crosswalk);
            mutated["verdict"]!["checks"]!["extra"] = "pass";
            EvaluateClosure(
                mutated,
                JsonSerializer.SerializeToUtf8Bytes(mutated),
                subjectBytes,
                receipts,
                root,
                evidence,
                coreBytes,
                proofBytes).ShouldBeFalse();

            mutated = Clone(crosswalk);
            mutated["verdict"]!["checks"]!["oci_graph"] = "";
            EvaluateClosure(
                mutated,
                JsonSerializer.SerializeToUtf8Bytes(mutated),
                subjectBytes,
                receipts,
                root,
                evidence,
                coreBytes,
                proofBytes).ShouldBeFalse();

            mutated = Clone(crosswalk);
            mutated["verdict"]!["blockers"] = new JsonArray(new JsonObject { ["id"] = "hidden" });
            EvaluateClosure(
                mutated,
                JsonSerializer.SerializeToUtf8Bytes(mutated),
                subjectBytes,
                receipts,
                root,
                evidence,
                coreBytes,
                proofBytes).ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(evidence, recursive: true);
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
        (string evidence, JsonObject crosswalk, byte[] crosswalkBytes, byte[] subjectBytes,
            JsonObject[] receipts, byte[] coreBytes, byte[] proofBytes) = CreatePassingFixture(root);
        try
        {
            receipts[0].Remove(missingField);
            EvaluateClosure(
                crosswalk,
                crosswalkBytes,
                subjectBytes,
                receipts,
                root,
                evidence,
                coreBytes,
                proofBytes).ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(evidence, recursive: true);
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
        (string evidence, JsonObject crosswalk, byte[] crosswalkBytes, byte[] subjectBytes,
            JsonObject[] receipts, byte[] coreBytes, byte[] proofBytes) = CreatePassingFixture(root);
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
                receipts,
                root,
                evidence,
                coreBytes,
                proofBytes).ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(evidence, recursive: true);
        }
    }

    /// <summary>
    /// Verifies stale receipt subjects and duplicate roles or reviewers fail closed.
    /// </summary>
    [Fact]
    public void ExternalAcceptancesMustBeUniqueAndAcceptOneUnchangedSubject()
    {
        string root = FindRepositoryRoot();
        (string evidence, JsonObject crosswalk, byte[] crosswalkBytes, byte[] subjectBytes,
            JsonObject[] receipts, byte[] coreBytes, byte[] proofBytes) = CreatePassingFixture(root);
        try
        {
            JsonObject[] stale = receipts.Select(Clone).ToArray();
            stale[0]["subject_sha256"] = new string('0', 64);
            EvaluateClosure(crosswalk, crosswalkBytes, subjectBytes, stale, root, evidence, coreBytes, proofBytes)
                .ShouldBeFalse();

            JsonObject[] duplicateRole = receipts.Select(Clone).ToArray();
            duplicateRole[1]["role"] = duplicateRole[0]["role"]!.GetValue<string>();
            EvaluateClosure(
                crosswalk,
                crosswalkBytes,
                subjectBytes,
                duplicateRole,
                root,
                evidence,
                coreBytes,
                proofBytes).ShouldBeFalse();

            JsonObject[] duplicateReviewer = receipts.Select(Clone).ToArray();
            duplicateReviewer[1]["reviewer_identity"] =
                duplicateReviewer[0]["reviewer_identity"]!.GetValue<string>();
            EvaluateClosure(
                crosswalk,
                crosswalkBytes,
                subjectBytes,
                duplicateReviewer,
                root,
                evidence,
                coreBytes,
                proofBytes).ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(evidence, recursive: true);
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
        JsonObject crosswalk = LoadCrosswalk(FindRepositoryRoot());
        JsonObject splice = crosswalk["prohibited_splices"]!.AsArray()
            .Select(item => item!.AsObject())
            .Single(item => item["splice_id"]!.GetValue<string>() == spliceId);

        splice["result"]!.GetValue<string>().ShouldBe("rejected");
        splice["source_package_lineage"]!.GetValue<string>()
            .ShouldNotBe(splice["release_index_lineage"]!.GetValue<string>());
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
        IReadOnlyCollection<JsonObject> receipts,
        string repositoryRoot,
        string evidenceRoot,
        byte[] evidenceCoreManifestBytes,
        byte[] proofPacketBytes)
    {
        try
        {
            JsonObject[] candidates = crosswalk["selected_candidates"]!.AsArray()
                .Select(node => node!.AsObject())
                .ToArray();
            if (crosswalk["schema"]!.GetValue<string>() !=
                    "hexalith.eventstore.story-3-13-identity-crosswalk/v2"
                || crosswalk["schema_version"]!.GetValue<int>() != 2
                || candidates.Length != 1
                || candidates[0]["source"]!["sha"]!.GetValue<string>() != ApprovedSourceSha
                || !ValidatePackages(crosswalk, repositoryRoot)
                || !ValidateRelease(candidates[0])
                || !ValidateOciGraph(crosswalk, evidenceRoot)
                || !ValidateOciProvenance(crosswalk, evidenceRoot)
                || !ValidateRuntimeExecution(crosswalk, evidenceRoot)
                || !ValidateRuntimeEquivalence(crosswalk)
                || !ValidateDeploymentAuthority(candidates[0]))
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
                receipts,
                evidenceCoreManifestBytes,
                proofPacketBytes);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
            or KeyNotFoundException
            or NullReferenceException
            or ArgumentOutOfRangeException
            or InvalidDataException
            or JsonException
            or IOException
            or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool ValidatePackages(JsonObject crosswalk, string root)
    {
        try
        {
            JsonObject packages = crosswalk["selected_candidates"]![0]!["packages"]!.AsObject();
            string version = packages["version"]!.GetValue<string>();
            JsonObject[] actualItems = packages["items"]!.AsArray().Select(item => item!.AsObject()).ToArray();
            JsonObject[] expectedItems = JsonNode.Parse(
                File.ReadAllText(Path.Combine(root, "tools", "release-packages.json")))!["packages"]!
                .AsArray().Select(item => item!.AsObject()).ToArray();
            Dictionary<string, string> retainedHashes = ParseChecksumManifest(
                File.ReadAllBytes(ResolveWithin(
                    root,
                    packages["hash_manifest_path"]!.GetValue<string>())));

            if (packages["release_manifest_sha256"]!.GetValue<string>() !=
                    ComputeSha256(Path.Combine(root, "tools", "release-packages.json"))
                || packages["hash_manifest_sha256"]!.GetValue<string>() !=
                    ComputeSha256(ResolveWithin(root, packages["hash_manifest_path"]!.GetValue<string>()))
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
        catch (Exception exception) when (exception is InvalidOperationException or JsonException or IOException)
        {
            return false;
        }
    }

    private static bool ValidateRelease(JsonObject candidate)
    {
        JsonObject packages = candidate["packages"]!.AsObject();
        JsonObject byteVerification = packages["byte_verification"]!.AsObject();
        JsonObject release = candidate["release"]!.AsObject();
        string[] requiredTextFields =
        [
            "semantic_version",
            "semantic_tag",
            "builds_execution_sha",
            "publisher_identity",
            "validator_identity",
            "source_sha",
        ];

        return byteVerification["result"]!.GetValue<string>() == "pass"
            && byteVerification["recovered_count"]!.GetValue<int>() == 14
            && packages["items"]!.AsArray().All(item =>
                item!["byte_verification"]!.GetValue<string>() == "pass")
            && requiredTextFields.All(field => !string.IsNullOrWhiteSpace(release[field]!.GetValue<string>()))
            && release["workflow_run"]!.GetValue<long>() > 0
            && release["workflow_attempt"]!.GetValue<int>() > 0
            && release["source_sha"]!.GetValue<string>() == ApprovedSourceSha
            && release["source_exact_match"]!.GetValue<bool>()
            && release["verification"]!["result"]!.GetValue<string>() == "pass";
    }

    private static bool ValidateDeploymentAuthority(JsonObject candidate) =>
        candidate["release_authority"]!["deployment_authorized"]!.GetValue<bool>()
        && candidate["release_authority"]!["authorized_source_sha"]!.GetValue<string>() == ApprovedSourceSha
        && candidate["release_authority"]!["verification"]!["result"]!.GetValue<string>() == "pass";

    private static bool ValidateOciGraph(JsonObject crosswalk, string evidenceRoot)
    {
        try
        {
            JsonObject oci = crosswalk["selected_candidates"]![0]!["oci"]!.AsObject();
            byte[] indexBytes = ReadEvidenceFile(evidenceRoot, oci["index_raw_file"]!.GetValue<string>());
            string indexDigest = "sha256:" + ComputeSha256(indexBytes);
            if (oci["index_digest"]!.GetValue<string>() != indexDigest
                || oci["index_raw_sha256"]!.GetValue<string>() != indexDigest[7..]
                || oci["index_size"]!.GetValue<int>() != indexBytes.Length
                || oci["index_media_type"]!.GetValue<string>() != OciIndexMediaType)
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
            string[] platforms = children.Select(child => child["platform"]!.GetValue<string>())
                .Order(StringComparer.Ordinal).ToArray();
            if (children.Length != 2
                || descriptors.Length != 2
                || !platforms.SequenceEqual(["linux/amd64", "linux/arm64"], StringComparer.Ordinal)
                || platforms.Distinct(StringComparer.Ordinal).Count() != 2)
            {
                return false;
            }

            foreach (JsonObject child in children)
            {
                string platform = child["platform"]!.GetValue<string>();
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
                && ResponseBindingMatches(oci, "tag", tagBytes, indexDigest)
                && ResponseBindingMatches(oci, "digest", digestBytes, indexDigest)
                && oci["verification"]!["result"]!.GetValue<string>() == "pass";
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
            or InvalidDataException
            or JsonException
            or IOException
            or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool ValidateOciProvenance(JsonObject crosswalk, string evidenceRoot)
    {
        try
        {
            JsonObject oci = crosswalk["selected_candidates"]![0]!["oci"]!.AsObject();
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

            foreach (JsonObject child in oci["children"]!.AsArray().Select(item => item!.AsObject()))
            {
                JsonObject labels = JsonNode.Parse(ReadEvidenceFile(
                    evidenceRoot,
                    child["config_raw_file"]!.GetValue<string>()))!["config"]!["Labels"]!.AsObject();
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
            }

            return true;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
            or InvalidDataException
            or JsonException
            or IOException
            or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool ValidateRuntimeExecution(JsonObject crosswalk, string evidenceRoot)
    {
        try
        {
            JsonObject candidate = crosswalk["selected_candidates"]![0]!.AsObject();
            JsonObject runtime = candidate["runtime"]!.AsObject();
            JsonObject oci = candidate["oci"]!.AsObject();
            JsonObject retained = JsonNode.Parse(ReadEvidenceFile(
                evidenceRoot,
                runtime["citation"]!.GetValue<string>()))!.AsObject();
            string[] boundProperties = ["execution_result", "contract_equivalence", "result", "exit_code"];
            if (boundProperties.Any(property =>
                    retained[property]!.ToJsonString() != runtime[property]!.ToJsonString())
                || runtime["execution_result"]!.GetValue<string>() != "pass"
                || runtime["exit_code"]!.GetValue<int>() != 0
                || !DateTimeOffset.TryParse(runtime["started_at"]!.GetValue<string>(), out DateTimeOffset started)
                || !DateTimeOffset.TryParse(runtime["ended_at"]!.GetValue<string>(), out DateTimeOffset ended)
                || ended <= started)
            {
                return false;
            }

            JsonObject contract = runtime["contract"]!.AsObject();
            if (contract["health_path"]!.GetValue<string>() != "/alive"
                || contract["http_expectation"]!.GetValue<string>() != "2xx-without-redirect"
                || contract["timeout_seconds"]!.GetValue<int>() <= 0
                || contract["poll_interval_seconds"]!.GetValue<int>() <= 0)
            {
                return false;
            }

            JsonObject preflight = runtime["preflight"]!.AsObject();
            if (preflight["platform"]!.GetValue<string>() != "linux/arm64"
                || preflight["outcome"]!.GetValue<string>() != "pass"
                || !FileBindingMatches(evidenceRoot, preflight, "log"))
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
                    || !FileBindingMatches(evidenceRoot, platform, "log"))
                {
                    return false;
                }
            }

            return true;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
            or InvalidDataException
            or JsonException
            or IOException
            or UnauthorizedAccessException)
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
        IReadOnlyCollection<JsonObject> receipts,
        byte[] coreBytes,
        byte[] proofBytes)
    {
        JsonObject approval = crosswalk["approval_contract"]!.AsObject();
        string[] roles = approval["required_roles"]!.AsArray()
            .Select(item => item!.GetValue<string>()).Order(StringComparer.Ordinal).ToArray();
        string[] fields = approval["required_receipt_fields"]!.AsArray()
            .Select(item => item!.GetValue<string>()).Order(StringComparer.Ordinal).ToArray();
        if (!approval["outside_hashed_evidence"]!.GetValue<bool>()
            || !approval["external_receipt_location"]!.GetValue<string>().Contains("{subject_sha256}", StringComparison.Ordinal)
            || !roles.SequenceEqual(RequiredRoles, StringComparer.Ordinal)
            || !fields.SequenceEqual(RequiredReceiptFields, StringComparer.Ordinal)
            || approval["receipt_count"]!.GetValue<int>() != 3
            || approval["verification"]!["result"]!.GetValue<string>() != "pass")
        {
            return false;
        }

        JsonObject subject = JsonNode.Parse(subjectBytes)!.AsObject();
        if (subject["schema"]!.GetValue<string>() != "hexalith.eventstore.story-3-13-review-subject/v2"
            || subject["proposed_decision"]!.GetValue<string>() != "pass"
            || !RawBindingMatches(subject, "identity_crosswalk", crosswalkBytes)
            || !RawBindingMatches(subject, "evidence_core_manifest", coreBytes)
            || !RawBindingMatches(subject, "proof_packet", proofBytes))
        {
            return false;
        }

        string subjectHash = ComputeSha256(subjectBytes);
        if (receipts.Count != 3
            || receipts.Any(receipt => !RequiredReceiptFields.All(receipt.ContainsKey))
            || receipts.Select(receipt => receipt["role"]!.GetValue<string>()).Distinct(StringComparer.Ordinal).Count() != 3
            || receipts.Select(receipt => receipt["reviewer_identity"]!.GetValue<string>())
                .Distinct(StringComparer.Ordinal).Count() != 3
            || !receipts.Select(receipt => receipt["role"]!.GetValue<string>())
                .Order(StringComparer.Ordinal).SequenceEqual(RequiredRoles, StringComparer.Ordinal))
        {
            return false;
        }

        return receipts.All(receipt => RequiredReceiptFields.All(receipt.ContainsKey)
            && RequiredReceiptFields.All(field => HasReceiptValue(receipt[field]))
            && receipt["decision"]!.GetValue<string>() == "accepted"
            && receipt["subject_sha256"]!.GetValue<string>() == subjectHash
            && DateTimeOffset.TryParse(receipt["accepted_at"]!.GetValue<string>(), out _)
            && Uri.TryCreate(receipt["durable_source"]!.GetValue<string>(), UriKind.Absolute, out Uri? uri)
            && uri.Scheme == Uri.UriSchemeHttps);
    }

    private static bool HasReceiptValue(JsonNode? node) => node switch
    {
        JsonArray array => array.Count > 0 && array.All(item =>
            item is JsonValue value && !string.IsNullOrWhiteSpace(value.GetValue<string>())),
        JsonValue value => !string.IsNullOrWhiteSpace(value.GetValue<string>()),
        _ => false,
    };

    private static bool RawBindingMatches(JsonObject subject, string name, byte[] bytes)
    {
        JsonObject binding = subject[name]!.AsObject();
        return !string.IsNullOrWhiteSpace(binding["path"]!.GetValue<string>())
            && binding["sha256"]!.GetValue<string>() == ComputeSha256(bytes);
    }

    private static bool ResponseBindingMatches(JsonObject oci, string name, byte[] bytes, string digest) =>
        oci[name + "_response_raw_sha256"]!.GetValue<string>() == ComputeSha256(bytes)
        && oci[name + "_response_size"]!.GetValue<int>() == bytes.Length
        && oci[name + "_response_content_type"]!.GetValue<string>() == OciIndexMediaType
        && oci[name + "_response_docker_content_digest"]!.GetValue<string>() == digest;

    private static bool BytesMatchDescriptor(byte[] bytes, JsonObject child, string prefix) =>
        child[prefix + "_raw_sha256"]!.GetValue<string>() == ComputeSha256(bytes)
        && child[prefix + "_digest"]!.GetValue<string>() == "sha256:" + ComputeSha256(bytes)
        && child[prefix + "_size"]!.GetValue<int>() == bytes.Length;

    private static bool FileBindingMatches(string root, JsonObject item, string property)
    {
        byte[] bytes = ReadEvidenceFile(root, item[property]!.GetValue<string>());
        return item[property + "_sha256"]!.GetValue<string>() == ComputeSha256(bytes);
    }

    private static (string Evidence, JsonObject Crosswalk, byte[] CrosswalkBytes, byte[] SubjectBytes,
        JsonObject[] Receipts, byte[] CoreBytes, byte[] ProofBytes) CreatePassingFixture(string root)
    {
        string evidence = Path.Combine(Path.GetTempPath(), "story-3-13-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(evidence);
        JsonObject crosswalk = LoadCrosswalk(root);
        JsonObject candidate = crosswalk["selected_candidates"]![0]!.AsObject();
        JsonObject packages = candidate["packages"]!.AsObject();
        packages["byte_verification"]!["result"] = "pass";
        packages["byte_verification"]!["recovered_count"] = 14;
        foreach (JsonNode? item in packages["items"]!.AsArray())
        {
            item!["byte_verification"] = "pass";
        }

        JsonObject release = candidate["release"]!.AsObject();
        release["semantic_version"] = "3.82.1";
        release["semantic_tag"] = "v3.82.1";
        release["workflow_run"] = 123456789L;
        release["workflow_attempt"] = 1;
        release["builds_execution_sha"] = "a53166539bf4441d5e33d04281b14c2d59e950c3";
        release["publisher_identity"] = "release-publisher";
        release["validator_identity"] = "test-architect";
        release["source_sha"] = ApprovedSourceSha;
        release["source_exact_match"] = true;
        release["verification"]!["result"] = "pass";
        candidate["release_authority"]!["deployment_authorized"] = true;
        candidate["release_authority"]!["verification"]!["result"] = "pass";

        PopulatePassingOciAndRuntime(candidate, evidence);
        JsonObject verdict = crosswalk["verdict"]!.AsObject();
        verdict["decision"] = "pass";
        verdict["story_may_be_done"] = true;
        verdict["blockers"] = new JsonArray();
        foreach (string check in ExpectedChecks)
        {
            verdict["checks"]![check] = "pass";
        }

        JsonObject approval = crosswalk["approval_contract"]!.AsObject();
        approval["receipt_count"] = 3;
        approval["verification"]!["result"] = "pass";
        byte[] crosswalkBytes = JsonSerializer.SerializeToUtf8Bytes(crosswalk);
        byte[] coreBytes = Encoding.UTF8.GetBytes("synthetic frozen core manifest\n");
        byte[] proofBytes = Encoding.UTF8.GetBytes("synthetic frozen human proof packet\n");
        JsonObject subject = new()
        {
            ["schema"] = "hexalith.eventstore.story-3-13-review-subject/v2",
            ["proposed_decision"] = "pass",
            ["identity_crosswalk"] = Binding("identity-crosswalk.json", crosswalkBytes),
            ["evidence_core_manifest"] = Binding("evidence-core-sha256.txt", coreBytes),
            ["proof_packet"] = Binding(ProofRelativePath, proofBytes),
        };
        byte[] subjectBytes = JsonSerializer.SerializeToUtf8Bytes(subject);
        string subjectHash = ComputeSha256(subjectBytes);
        JsonObject[] receipts = RequiredRoles.Select((role, index) => new JsonObject
        {
            ["role"] = role,
            ["reviewer_identity"] = "reviewer-" + index,
            ["accepted_at"] = "2026-08-04T12:00:00Z",
            ["durable_source"] = "https://example.invalid/reviews/" + index,
            ["accepted_scope"] = "Story 3.13 closure",
            ["accepted_limitations"] = new JsonArray("No external mutation"),
            ["decision"] = "accepted",
            ["subject_sha256"] = subjectHash,
        }).ToArray();
        return (evidence, crosswalk, crosswalkBytes, subjectBytes, receipts, coreBytes, proofBytes);
    }

    private static void PopulatePassingOciAndRuntime(JsonObject candidate, string evidence)
    {
        JsonObject oci = candidate["oci"]!.AsObject();
        JsonArray descriptors = [];
        JsonArray children = [];
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
                        ["org.opencontainers.image.url"] = "https://github.com/Hexalith/Hexalith.EventStore/releases/tag/v3.82.1",
                        ["org.opencontainers.image.documentation"] = "https://github.com/Hexalith/Hexalith.EventStore/blob/main/README.md",
                    },
                },
            };
            byte[] configBytes = JsonSerializer.SerializeToUtf8Bytes(config);
            string configDigest = "sha256:" + ComputeSha256(configBytes);
            File.WriteAllBytes(Path.Combine(evidence, stem + ".config.raw"), configBytes);
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
            File.WriteAllBytes(Path.Combine(evidence, stem + ".manifest.raw"), manifestBytes);
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
                ["manifest_raw_file"] = stem + ".manifest.raw",
                ["manifest_raw_sha256"] = manifestDigest[7..],
                ["config_digest"] = configDigest,
                ["config_size"] = configBytes.Length,
                ["config_media_type"] = OciConfigMediaType,
                ["config_raw_file"] = stem + ".config.raw",
                ["config_raw_sha256"] = configDigest[7..],
                ["config_platform"] = platform,
                ["verification"] = "pass",
            });
        }

        JsonObject index = new()
        {
            ["schemaVersion"] = 2,
            ["mediaType"] = OciIndexMediaType,
            ["manifests"] = descriptors,
        };
        byte[] indexBytes = JsonSerializer.SerializeToUtf8Bytes(index);
        string indexHash = ComputeSha256(indexBytes);
        foreach (string file in new[] { "index.raw", "tag-response.raw", "digest-response.raw" })
        {
            File.WriteAllBytes(Path.Combine(evidence, file), indexBytes);
        }

        oci["index_digest"] = "sha256:" + indexHash;
        oci["index_raw_sha256"] = indexHash;
        oci["index_size"] = indexBytes.Length;
        oci["index_media_type"] = OciIndexMediaType;
        oci["children"] = children;
        foreach (string name in new[] { "tag", "digest" })
        {
            oci[name + "_response_raw_sha256"] = indexHash;
            oci[name + "_response_size"] = indexBytes.Length;
            oci[name + "_response_content_type"] = OciIndexMediaType;
            oci[name + "_response_docker_content_digest"] = "sha256:" + indexHash;
        }

        JsonObject labels = oci["provenance_labels"]!.AsObject();
        labels["org.opencontainers.image.source"] = "https://github.com/Hexalith/Hexalith.EventStore";
        labels["org.opencontainers.image.url"] =
            "https://github.com/Hexalith/Hexalith.EventStore/releases/tag/v3.82.1";
        labels["org.opencontainers.image.documentation"] =
            "https://github.com/Hexalith/Hexalith.EventStore/blob/main/README.md";
        labels["verification"]!["result"] = "pass";

        foreach ((string file, string content) in new[]
        {
            ("smoke-preflight.log", "preflight pass\n"),
            ("smoke-linux-amd64.log", "amd64 pass and cleanup\n"),
            ("smoke-linux-arm64.log", "arm64 pass and cleanup\n"),
        })
        {
            File.WriteAllText(Path.Combine(evidence, file), content);
        }

        JsonObject runtime = candidate["runtime"]!.AsObject();
        runtime["execution_result"] = "pass";
        runtime["contract_equivalence"] = "pass";
        runtime["result"] = "pass";
        runtime["exit_code"] = 0;
        runtime["contract"]!["actual_hosting_environment"] = "Production";
        runtime["contract"]!["required_hosting_environment"] = "Production";
        runtime["verification"]!["result"] = "pass";
        runtime["preflight"]!["log_sha256"] = ComputeSha256(Path.Combine(evidence, "smoke-preflight.log"));
        foreach (JsonObject platform in runtime["platforms"]!.AsArray().Select(item => item!.AsObject()))
        {
            JsonObject child = children.Select(item => item!.AsObject()).Single(item =>
                item["platform"]!.GetValue<string>() == platform["platform"]!.GetValue<string>());
            platform["child_digest"] = child["manifest_digest"]!.GetValue<string>();
            platform["log_sha256"] = ComputeSha256(Path.Combine(evidence, platform["log"]!.GetValue<string>()));
        }

        File.WriteAllBytes(
            Path.Combine(evidence, runtime["citation"]!.GetValue<string>()),
            JsonSerializer.SerializeToUtf8Bytes(runtime));
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
            exception is InvalidDataException or IOException or UnauthorizedAccessException)
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

        return target;
    }

    private static JsonObject LoadCrosswalk(string root) => JsonNode.Parse(
        File.ReadAllBytes(Path.Combine(root, EvidenceRelativePath, "identity-crosswalk.json")))!.AsObject();

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
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        process.ExitCode.ShouldBe(0, error);
        return output.Trim();
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
