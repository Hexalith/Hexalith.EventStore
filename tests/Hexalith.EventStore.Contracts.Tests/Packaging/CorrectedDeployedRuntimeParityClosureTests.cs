using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Hexalith.EventStore.Contracts.Tests.Packaging;

/// <summary>
/// Verifies the Story 3.15 corrected deployed-runtime parity closure contract.
/// </summary>
public sealed class CorrectedDeployedRuntimeParityClosureTests
{
    private const string SourceSha = "f343bb0153e9cdcb8b12ec10153813072f5ad38d";
    private const string PredecessorSha256 =
        "4d1a0c336397e971bf10001095d5e427dd03c499ee428a3121a913926da8c4a9";
    private const string IndexDigest =
        "sha256:4b1410852b11be3bcaebf8f2e6277c1d30ce13a19f48cf0df86ed93646d709c3";
    private const string EvidenceRelativePath =
        "_bmad-output/implementation-artifacts/evidence/story-3-15/" + SourceSha;

    /// <summary>
    /// Subject digest the three superseded receipts were collected for. Binding the transitively
    /// imported predecessor handler re-minted the subject, so those receipts authorize nothing and
    /// must never reappear inside the packet.
    /// </summary>
    private const string SupersededSubjectSha256 =
        "bb58d691ee404cc958433e996204e3382721de3931ac64cf8f7a61de97c30709";

    private const string SupersededRelativePath =
        "_bmad-output/implementation-artifacts/evidence/story-3-15/superseded-acceptances";

    private static readonly string[] RequiredRoles =
    [
        "eventstore-owner",
        "release-owner",
        "test-architect",
    ];

    private static readonly string[] Limitations =
    [
        "This packet supplies immutable deployed-runtime parity evidence only.",
        "It authorizes no deployment, publication, registry mutation, consumer removal, or predecessor change.",
        "The Test Architect acceptance is a self-attested BMAD record without independent external authentication.",
    ];

    private const string RerunTrigger =
        "Rebuild the complete subject and reject all prior receipts after any predecessor, package, OCI, " +
        "Production-smoke, inventory, registry, verifier, decision, or receipt-source change.";

    /// <summary>
    /// Exact SHA-256 of every retained superseded artefact, keyed by its path under
    /// <see cref="SupersededRelativePath"/>. Pinning the bytes -- rather than only the directory's
    /// existence -- is what makes the retention auditable: a silently rewritten or emptied receipt
    /// would otherwise still satisfy a presence-only assertion.
    /// </summary>
    private static readonly (string RelativePath, string Sha256)[] SupersededArtefacts =
    [
        ("README.md", "bd75c168326b15e26cc9e3e658bd07d9afd9058f799327fcd7c21f4af0164de9"),
        (SupersededSubjectSha256 + "/eventstore-owner.json",
            "ad8cc4fb62e5d1b843f42716235a8cce415ab612359b77fd0006c7dbea6ecfbf"),
        (SupersededSubjectSha256 + "/release-owner.json",
            "43506002503164af97d52c76aaea67c48120e64b52882839921226f600b6f7c5"),
        (SupersededSubjectSha256 + "/test-architect.json",
            "88bb28c155623845c78630e8198b4921893436c1e3efa147a7da48720666163a"),
        (SupersededSubjectSha256 + "/sources/eventstore-owner.json",
            "535b5e1066d18f68f0964d06b84a42210bbee4c955e8b678fe93794d485f3ac8"),
        (SupersededSubjectSha256 + "/sources/release-owner.json",
            "d10ecec2e169a62987fa0ab603072f44d4f4b95bb590ffe9fb9340b44bb494c3"),
        (SupersededSubjectSha256 + "/sources/test-architect.json",
            "d3c145d63449fa96f666281f7065a92c9a2a88f2646cf93d7a2f2666d6a00a1a"),
    ];

    /// <summary>
    /// Issue number used by synthesized fixtures. It is deliberately not 324 (Story 1.20) or 346
    /// (Story 3.14): the verifier rejects those two threads by number because reusing them is the
    /// cross-lineage splice this story family guards against, and the superseded bb58d691 receipts
    /// were themselves anchored on 346. Real receipts must be collected on a dedicated Story 3.15
    /// issue.
    /// </summary>
    private const int SyntheticAcceptanceIssue = 900001;

    /// <summary>Story 1.20 and Story 3.14 acceptance threads, rejected as foreign lineage.</summary>
    private static readonly int[] ForeignLineageIssues = [324, 346];

    /// <summary>
    /// Verifies the checked-in packet fails closed while no receipt binds the current subject.
    /// Binding the transitively imported predecessor handler changed the canonical subject, so the
    /// rerun trigger rejected the three receipts collected for the superseded subject; they are
    /// retained under evidence/story-3-15/superseded-acceptances/ and authorize nothing here.
    /// The positive path is proved on a synthesized packet by
    /// <see cref="ThreeAuthenticatedRolesClosePositiveParityOnOneUnchangedSubject"/>.
    /// </summary>
    [Fact]
    public void CheckedInPacketFailsClosedUntilThreeReceiptsBindTheCurrentSubject()
    {
        string root = FindRepositoryRoot();
        string packet = Path.Combine(root, EvidenceRelativePath);

        ShouldFailClosed(RunValidator(root, packet), "exactly three packet-bound receipts are required");

        JsonObject closure = LoadJson(Path.Combine(packet, "closure.json"));
        closure["acceptances"]!["receipts"]!.AsArray().Count.ShouldBe(0);
        closure["selected_deployed_identity"]!.GetValue<string>().ShouldBe(IndexDigest);
        closure["deployment_authorized"]!.GetValue<bool>().ShouldBeFalse();
        closure["consumer_removal_authorized"]!.GetValue<bool>().ShouldBeFalse();
        closure["publication_authorized"]!.GetValue<bool>().ShouldBeFalse();
        closure["grants_mutation_authority"]!.GetValue<bool>().ShouldBeFalse();

        Directory.Exists(Path.Combine(packet, "acceptances", SupersededSubjectSha256))
            .ShouldBeFalse();
    }

    /// <summary>
    /// Verifies the three superseded receipts and their sources are retained byte-for-byte outside
    /// the packet. Directory presence alone cannot notice a rewritten, truncated, or re-signed
    /// receipt, so each retained file's SHA-256 is pinned here.
    /// </summary>
    [Fact]
    public void SupersededReceiptsAreRetainedByteForByteOutsideThePacket()
    {
        string root = FindRepositoryRoot();
        string superseded = Path.Combine(root, SupersededRelativePath);

        Directory.Exists(superseded).ShouldBeTrue();
        foreach ((string relativePath, string sha256) in SupersededArtefacts)
        {
            string path = Path.Combine(superseded, relativePath);
            File.Exists(path).ShouldBeTrue(relativePath);
            ComputeSha256(path).ShouldBe(sha256, relativePath);
        }

        // The retained tree holds exactly these artefacts: an extra receipt smuggled in beside them
        // would otherwise pass unhashed.
        Directory.EnumerateFiles(superseded, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(superseded, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ShouldBe(SupersededArtefacts.Select(artefact => artefact.RelativePath)
                .Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// Verifies three exact rostered roles can accept one unchanged subject and select only the index.
    /// </summary>
    [Fact]
    public void ThreeAuthenticatedRolesClosePositiveParityOnOneUnchangedSubject()
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            (int exitCode, string output, string error) = RunValidator(root, temporary);

            exitCode.ShouldBe(0, error);
            output.ShouldContain("selected=" + IndexDigest);
            JsonObject closure = LoadJson(Path.Combine(temporary, "closure.json"));
            closure["deployed_runtime_parity"]!.GetValue<string>().ShouldBe("available");
            closure["selected_deployed_identity"]!.GetValue<string>().ShouldBe(IndexDigest);
            closure["deployment_authorized"]!.GetValue<bool>().ShouldBeFalse();
            closure["consumer_removal_authorized"]!.GetValue<bool>().ShouldBeFalse();
            closure["publication_authorized"]!.GetValue<bool>().ShouldBeFalse();
            closure["grants_mutation_authority"]!.GetValue<bool>().ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies both package byte domains are independently retained and repository-signed NuGet bytes differ.
    /// </summary>
    [Fact]
    public void PackageDomainsBindAllFourteenManifestPackagesWithoutConflation()
    {
        string root = FindRepositoryRoot();
        string packet = Path.Combine(root, EvidenceRelativePath);
        JsonObject closure = LoadJson(Path.Combine(packet, "closure.json"));
        JsonArray items = closure["packages"]!["items"]!.AsArray();

        items.Count.ShouldBe(14);
        items.Select(item => item!["id"]!.GetValue<string>()).Distinct(StringComparer.Ordinal).Count().ShouldBe(14);
        items.All(item =>
            item!["nuget_org"]!["repository_signature_entry_present"]!.GetValue<bool>()
            && item["nuget_org"]!["sha256"]!.GetValue<string>()
                != item["github_release_asset"]!["sha256"]!.GetValue<string>()).ShouldBeTrue();
        foreach (JsonNode? item in items)
        {
            JsonNode package = item.ShouldNotBeNull();
            string relative = package["nuget_org"]!["file"]!.GetValue<string>();
            ComputeSha256(Path.Combine(packet, relative)).ShouldBe(
                package["nuget_org"]!["sha256"]!.GetValue<string>());
        }
    }

    /// <summary>
    /// Verifies replacing independently downloaded NuGet bytes with the predecessor's GitHub
    /// release-asset bytes reaches the explicit byte-domain conflation guard after rebinding.
    /// </summary>
    [Fact]
    public void ReboundGitHubReleaseAssetBytesCannotMasqueradeAsNuGetBytes()
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            string closurePath = Path.Combine(temporary, "closure.json");
            JsonObject closure = LoadJson(closurePath);
            JsonObject item = closure["packages"]!["items"]![0]!.AsObject();
            JsonObject nuget = item["nuget_org"]!.AsObject();
            string destination = Path.Combine(temporary, nuget["file"]!.GetValue<string>());
            string predecessor = Path.Combine(
                root,
                "_bmad-output",
                "implementation-artifacts",
                "evidence",
                "story-3-14",
                SourceSha,
                item["github_release_asset"]!["file"]!.GetValue<string>());
            File.Copy(predecessor, destination, overwrite: true);
            UpdateFileBinding(nuget, destination, updateDigest: false);
            WriteCanonical(closurePath, closure);

            ShouldFailClosed(
                RunValidator(root, temporary),
                "NuGet-signed and GitHub release-asset byte domains were conflated");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies raw OCI graph bytes and both bounded Production smoke records form one immutable lineage.
    /// </summary>
    [Fact]
    public void RawOciGraphAndBothProductionSmokesReproduceSelectedIndex()
    {
        string root = FindRepositoryRoot();
        string packet = Path.Combine(root, EvidenceRelativePath);
        JsonObject closure = LoadJson(Path.Combine(packet, "closure.json"));
        JsonObject indexBinding = closure["oci"]!["index"]!.AsObject();
        ComputeSha256(Path.Combine(packet, indexBinding["file"]!.GetValue<string>())).ShouldBe(
            IndexDigest["sha256:".Length..]);

        JsonObject results = LoadJson(Path.Combine(
            packet,
            closure["production_smokes"]!["results"]!["file"]!.GetValue<string>()));
        results["environment"]!.GetValue<string>().ShouldBe("Production");
        results["result"]!.GetValue<string>().ShouldBe("pass");
        results["platforms"]!.AsArray().Select(item => item!["platform"]!.GetValue<string>())
            .ShouldBe(["linux/amd64", "linux/arm64"]);
        results["platforms"]!.AsArray().ShouldAllBe(item =>
            item!["http_status"]!.GetValue<int>() == 200
            && item["redirect_count"]!.GetValue<int>() == 0
            && item["cleanup"]!.GetValue<string>() == "pass"
            && item["outcome"]!.GetValue<string>() == "pass");
    }

    /// <summary>
    /// Verifies mutable package, OCI, and smoke evidence fails closed even when a full receipt set
    /// exists, and that the fail-closed reason names the exact file whose binding broke. A message
    /// that only states the generic prefix cannot distinguish which retained edge was recomputed,
    /// so every case pins "retained file binding mismatch: &lt;file&gt;".
    /// </summary>
    /// <param name="relativePath">Retained evidence file to mutate.</param>
    [Theory]
    [InlineData("packages/Hexalith.EventStore.Contracts.3.96.2.nupkg")]
    [InlineData("oci/index.raw")]
    [InlineData("oci/child-linux-amd64.manifest.raw")]
    [InlineData("oci/child-linux-amd64.config.raw")]
    [InlineData("oci/child-linux-arm64.manifest.raw")]
    [InlineData("oci/child-linux-arm64.config.raw")]
    [InlineData("smokes/smoke-results.json")]
    [InlineData("smokes/smoke-linux-amd64.log")]
    [InlineData("smokes/smoke-linux-arm64.log")]
    [InlineData("technical-sha256.txt")]
    [InlineData("registry/owner-role-registry.json")]
    [InlineData("registry/role-registry-source.json")]
    [InlineData("subject.json")]
    public void MutableOrMixedEvidenceNeverSelectsIdentity(string relativePath)
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            string path = Path.Combine(temporary, relativePath);
            File.WriteAllBytes(path, [.. File.ReadAllBytes(path), (byte)' ']);

            ShouldFailClosed(
                RunValidator(root, temporary),
                "retained file binding mismatch: " + relativePath);
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies independently retained OCI bytes still have to reproduce the frozen predecessor
    /// after their local SHA-256, size, and OCI digest binding have all been corrected.
    /// </summary>
    /// <param name="relativePath">Raw OCI object to mutate and rebind.</param>
    /// <param name="expectedError">Expected semantic mismatch after the binding passes.</param>
    [Theory]
    [InlineData("oci/child-linux-amd64.manifest.raw", "independent raw OCI manifest does not match the predecessor")]
    [InlineData("oci/child-linux-arm64.config.raw", "independent raw OCI config does not match the predecessor")]
    public void ReboundOciBytesStillHaveToReproduceThePredecessor(
        string relativePath,
        string expectedError)
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            string closurePath = Path.Combine(temporary, "closure.json");
            JsonObject closure = LoadJson(closurePath);
            string path = Path.Combine(temporary, relativePath);
            File.WriteAllBytes(path, [.. File.ReadAllBytes(path), (byte)' ']);
            UpdateFileBinding(
                FindFileBinding(closure, relativePath),
                path,
                updateDigest: true);
            WriteCanonical(closurePath, closure);

            ShouldFailClosed(RunValidator(root, temporary), expectedError);
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies a rebound smoke summary cannot declare a failing run while the closure still claims
    /// positive parity.
    /// </summary>
    [Fact]
    public void ReboundFailingSmokeSummaryCannotAuthorizeParity()
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            string closurePath = Path.Combine(temporary, "closure.json");
            JsonObject closure = LoadJson(closurePath);
            JsonObject binding = closure["production_smokes"]!["results"]!.AsObject();
            string resultsPath = Path.Combine(temporary, binding["file"]!.GetValue<string>());
            JsonObject results = LoadJson(resultsPath);
            results["result"] = "fail";
            WriteCanonical(resultsPath, results);
            UpdateFileBinding(binding, resultsPath, updateDigest: false);
            WriteCanonical(closurePath, closure);

            ShouldFailClosed(RunValidator(root, temporary), "bounded Production smoke outcome is invalid");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies a rebound registry still has to reproduce the exact configured role roster.
    /// </summary>
    [Fact]
    public void ReboundRegistryWithChangedRoleIdentityFailsClosed()
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            string closurePath = Path.Combine(temporary, "closure.json");
            JsonObject closure = LoadJson(closurePath);
            JsonObject binding = closure["owner_role_registry"]!.AsObject();
            string registryPath = Path.Combine(temporary, binding["file"]!.GetValue<string>());
            JsonObject registry = LoadJson(registryPath);
            registry["roles"]!["eventstore-owner"] = new JsonArray((JsonNode)"github:mallory");
            WriteCanonical(registryPath, registry);
            UpdateFileBinding(binding, registryPath, updateDigest: false);
            WriteCanonical(closurePath, closure);

            ShouldFailClosed(RunValidator(root, temporary), "owner-role registry is invalid");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies a rebound checksum inventory still has to be derived from every exact retained file.
    /// </summary>
    [Fact]
    public void ReboundHandWrittenInventoryCannotReplaceTheDerivedInventory()
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            string closurePath = Path.Combine(temporary, "closure.json");
            JsonObject closure = LoadJson(closurePath);
            JsonObject binding = closure["technical_inventory"]!.AsObject();
            string inventoryPath = Path.Combine(temporary, binding["file"]!.GetValue<string>());
            File.AppendAllText(inventoryPath, $"{new string('0', 64)}  packages/not-retained.bin\n");
            UpdateFileBinding(binding, inventoryPath, updateDigest: false);
            WriteCanonical(closurePath, closure);

            ShouldFailClosed(
                RunValidator(root, temporary),
                "technical inventory is not closed over the exact retained files");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies noncanonical identity bytes and a declared package-domain splice fail closed.
    /// </summary>
    /// <param name="mutation">Identity mutation case.</param>
    /// <param name="expectedError">Expected fail-closed reason.</param>
    [Theory]
    [InlineData("noncanonical-closure", "canonical UTF-8 form")]
    [InlineData("package-domain-splice", "GitHub release-asset package domain changed")]
    public void IdentityAndPackageDomainMutationsFailClosed(string mutation, string expectedError)
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            string closurePath = Path.Combine(temporary, "closure.json");
            if (mutation == "noncanonical-closure")
            {
                string closureText = File.ReadAllText(closurePath);
                int separator = closureText.IndexOf(':', StringComparison.Ordinal);
                File.WriteAllText(
                    closurePath,
                    closureText.Insert(separator + 1, " "));
            }
            else
            {
                JsonObject closure = LoadJson(closurePath);
                closure["packages"]!["items"]![0]!["github_release_asset"]!["sha256"] = new string('0', 64);
                WriteCanonical(closurePath, closure);
            }

            ShouldFailClosed(RunValidator(root, temporary), expectedError);
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies every transitive live-file binding in dispatch is load-bearing, not only the handler
    /// digest the outer dispatcher uses as its route key.
    /// </summary>
    /// <param name="bindingName">Non-route dispatch binding to invalidate.</param>
    [Theory]
    [InlineData("verifier")]
    [InlineData("predecessor_handler")]
    [InlineData("predecessor_package")]
    public void EveryDispatchLiveFileBindingMustSelectTrustedBytes(string bindingName)
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            string closurePath = Path.Combine(temporary, "closure.json");
            JsonObject closure = LoadJson(closurePath);
            closure["dispatch"]![bindingName]!["sha256"] = new string('0', 64);
            WriteCanonical(closurePath, closure);

            ShouldFailClosed(
                RunValidator(root, temporary),
                "dispatch identity does not select the trusted live verifier");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies the complete expected-subject equality is load-bearing after the changed subject is
    /// rebound and receives a fresh, structurally valid receipt set.
    /// </summary>
    [Fact]
    public void ReboundSubjectWithChangedDecisionInputFailsClosed()
    {
        string root = FindRepositoryRoot();
        string temporary = CopyPacket(root);
        try
        {
            string closurePath = Path.Combine(temporary, "closure.json");
            JsonObject closure = LoadJson(closurePath);
            string subjectPath = Path.Combine(temporary, "subject.json");
            JsonObject subject = LoadJson(subjectPath);
            subject["decision"]!["deployment_authorized"] = true;
            WriteCanonical(subjectPath, subject);
            UpdateFileBinding(closure["subject"]!.AsObject(), subjectPath, updateDigest: false);
            string subjectHash = closure["subject"]!["sha256"]!.GetValue<string>();
            closure["acceptances"]!["directory"] = "acceptances/" + subjectHash;
            WriteCanonical(closurePath, closure);
            AttachThreeAcceptedReceipts(temporary);

            ShouldFailClosed(
                RunValidator(root, temporary),
                "canonical subject does not bind every decision input");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies an unhashable handler digest is classified as invalid dispatch metadata without a
    /// traceback or handler import.
    /// </summary>
    [Fact]
    public void UnhashableDispatchDigestFailsClosedWithSupportSafeReason()
    {
        string root = FindRepositoryRoot();
        string temporary = CopyPacket(root);
        try
        {
            string closurePath = Path.Combine(temporary, "closure.json");
            JsonObject closure = LoadJson(closurePath);
            closure["dispatch"]!["handler"]!["sha256"] = new JsonArray();
            WriteCanonical(closurePath, closure);

            (int exitCode, string output, string error) = RunValidator(root, temporary);

            exitCode.ShouldBe(1, error);
            output.ShouldNotContain("pass:");
            error.ShouldContain("closure dispatch metadata is invalid");
            error.ShouldContain("rerun: " + RerunTrigger);
            error.ShouldNotContain("Traceback");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies every acceptance field is mandatory and receipt mutations fail closed.
    /// </summary>
    /// <param name="field">Receipt field to remove.</param>
    [Theory]
    [InlineData("accepted_at")]
    [InlineData("accepted_limitations")]
    [InlineData("accepted_scope")]
    [InlineData("decision")]
    [InlineData("durable_source")]
    [InlineData("reviewer_identity")]
    [InlineData("role")]
    [InlineData("schema")]
    [InlineData("subject_sha256")]
    public void EveryReceiptFieldIsRequired(string field)
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            RewriteReceipt(temporary, 0, receipt => receipt.Remove(field));

            ShouldFailClosed(RunValidator(root, temporary), "acceptance receipt schema is invalid");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies wrong-role, subject-mismatched, stale, duplicate, and unverifiable receipts fail closed.
    /// </summary>
    /// <param name="mutation">Receipt mutation case.</param>
    /// <param name="expectedError">Expected fail-closed reason.</param>
    [Theory]
    [InlineData("wrong-role", "acceptance receipt does not bind")]
    [InlineData("subject-mismatch", "acceptance receipt does not bind")]
    [InlineData("stale", "acceptance predates the subject")]
    [InlineData("unverifiable", "retained file binding mismatch")]
    [InlineData("duplicate", "acceptance roles are missing, duplicated, or out of order")]
    [InlineData("missing", "exactly three packet-bound receipts are required")]
    public void InvalidAcceptanceNeverAuthorizesParity(string mutation, string expectedError)
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            string closurePath = Path.Combine(temporary, "closure.json");
            JsonObject closure = LoadJson(closurePath);
            JsonArray bindings = closure["acceptances"]!["receipts"]!.AsArray();
            if (mutation == "duplicate")
            {
                bindings[1]!["role"] = "eventstore-owner";
                WriteCanonical(closurePath, closure);
            }
            else if (mutation == "missing")
            {
                bindings.RemoveAt(2);
                WriteCanonical(closurePath, closure);
            }
            else
            {
                RewriteReceipt(temporary, 0, receipt =>
                {
                    switch (mutation)
                    {
                        case "wrong-role":
                            receipt["reviewer_identity"] = "github:mallory";
                            break;
                        case "subject-mismatch":
                            receipt["subject_sha256"] = new string('0', 64);
                            break;
                        case "stale":
                            receipt["accepted_at"] = "2020-01-01T00:00:00Z";
                            break;
                        case "unverifiable":
                            receipt["durable_source"]!["sha256"] = new string('0', 64);
                            break;
                    }
                });
            }

            ShouldFailClosed(RunValidator(root, temporary), expectedError);
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies an acceptance dated after the verifying host's clock fails closed, so a receipt
    /// cannot be pre-signed for a subject nobody has reviewed yet.
    /// </summary>
    [Fact]
    public void AcceptanceTimestampInTheFutureFailsClosed()
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            RewriteReceipt(temporary, 0, receipt => receipt["accepted_at"] = "2099-01-01T00:00:00Z");

            ShouldFailClosed(RunValidator(root, temporary), "acceptance timestamp lies in the future");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies a malformed timestamp fails closed with the verifier's own message instead of
    /// escaping as an unhandled traceback. A date-only value parses through fromisoformat but drops
    /// the offset, and a minute-precision value parses to a valid aware datetime, so neither shape
    /// is caught by parsing alone.
    /// </summary>
    /// <param name="timestamp">Malformed acceptance timestamp.</param>
    [Theory]
    [InlineData("2026-08-25Z")]
    [InlineData("2026-08-25T00:00Z")]
    public void MalformedAcceptanceTimestampFailsClosedWithoutCrashing(string timestamp)
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            RewriteReceipt(temporary, 0, receipt => receipt["accepted_at"] = timestamp);

            (int exitCode, string output, string error) = RunValidator(root, temporary);

            exitCode.ShouldBe(1, error);
            output.ShouldNotContain("pass:");
            error.ShouldContain("acceptance timestamp is invalid");
            error.ShouldNotContain("Traceback");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies any subject or non-authority decision change invalidates the closure and all old receipts.
    /// </summary>
    /// <param name="field">Closure field to mutate.</param>
    [Theory]
    [InlineData("deployment_authorized")]
    [InlineData("consumer_removal_authorized")]
    [InlineData("publication_authorized")]
    [InlineData("grants_mutation_authority")]
    public void DownstreamAuthorityFlagsRemainFalse(string field)
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            string closurePath = Path.Combine(temporary, "closure.json");
            JsonObject closure = LoadJson(closurePath);
            closure[field] = true;
            WriteCanonical(closurePath, closure);

            ShouldFailClosed(
                RunValidator(root, temporary),
                "closure outcome or non-authority flags are invalid");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies validation and mutation tests never change the frozen Story 3.14 packet.
    /// </summary>
    [Fact]
    public void FrozenStory314PacketRemainsByteForByteUnchanged()
    {
        string root = FindRepositoryRoot();
        string predecessor = Path.Combine(
            root,
            "_bmad-output",
            "implementation-artifacts",
            "evidence",
            "story-3-14",
            SourceSha,
            "release-identity.json");

        ComputeSha256(predecessor).ShouldBe(PredecessorSha256);
        (int exitCode, string output, string error) = RunProcess(
            root,
            "python3",
            "tools/validate-corrective-release-evidence.py",
            predecessor,
            "--manifest",
            "tools/release-packages.json",
            "--packet-root",
            Path.GetDirectoryName(predecessor).ShouldNotBeNull());
        exitCode.ShouldBe(0, error);
        output.ShouldContain(PredecessorSha256);
        ComputeSha256(predecessor).ShouldBe(PredecessorSha256);
    }

    /// <summary>
    /// Verifies the corrected closure validator exposes the same explicit manifest override as its
    /// predecessor validator and actually validates the supplied bytes.
    /// </summary>
    [Fact]
    public void ManifestOverrideIsLoadBearing()
    {
        string root = FindRepositoryRoot();
        string packet = Path.Combine(root, EvidenceRelativePath);
        string temporary = Path.Combine(Path.GetTempPath(), $"eventstore-story315-manifest-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(temporary, "{\"packages\":[]}\n");

            ShouldFailClosed(
                RunProcess(
                    root,
                    "python3",
                    "tools/validate-corrected-deployed-runtime-parity.py",
                    Path.Combine(packet, "closure.json"),
                    "--manifest",
                    temporary,
                    "--packet-root",
                    packet),
                "release package manifest must contain exactly 14 packages");
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    /// <summary>
    /// Verifies every entry in the dispatcher's import-path pin table matches the live file it
    /// names, so a drifted literal fails closed instead of silently accepting a stale or wrong
    /// module. Checking only the v1 handler would leave the package initializers and the v3
    /// predecessor handler -- which perform most of the closure's actual validation -- unchecked.
    /// </summary>
    [Fact]
    public void DispatchTableHandlerDigestMatchesLiveHandlerFile()
    {
        string root = FindRepositoryRoot();
        string dispatcherText = File.ReadAllText(
            Path.Combine(root, "tools", "validate-corrected-deployed-runtime-parity.py"));

        MatchCollection assignments = Regex.Matches(
            dispatcherText,
            "^V1_HANDLER_SHA256 = \"([0-9a-f]{64})\"",
            RegexOptions.Multiline);
        assignments.Count.ShouldBe(1);
        string v1Digest = assignments[0].Groups[1].Value;
        v1Digest.ShouldBe(ComputeSha256(
            Path.Combine(root, "tools", "deployed_runtime_parity_handlers", "v1.py")));

        int start = dispatcherText.IndexOf("IMPORT_PATH_FILE_SHA256 = {", StringComparison.Ordinal);
        start.ShouldBeGreaterThan(-1);
        int end = dispatcherText.IndexOf("\n}", start, StringComparison.Ordinal);
        end.ShouldBeGreaterThan(start);
        string table = dispatcherText[start..end];

        MatchCollection entries = Regex.Matches(
            table,
            "\"([A-Za-z0-9_/.]+\\.py)\":\\s*(?:\"([0-9a-f]{64})\"|(V1_HANDLER_SHA256))");

        // Non-vacuity: the parser must account for every pinned module in the table, not just the
        // ones whose formatting the pattern happens to fit.
        entries.Count.ShouldBe(Regex.Matches(table, "\\.py\":").Count);
        entries.Count.ShouldBeGreaterThanOrEqualTo(4);
        foreach (Match entry in entries)
        {
            string relative = entry.Groups[1].Value;
            string pinned = entry.Groups[2].Success ? entry.Groups[2].Value : v1Digest;
            string live = Path.Combine(
                root,
                "tools",
                relative.Replace('/', Path.DirectorySeparatorChar));
            File.Exists(live).ShouldBeTrue(relative);
            pinned.ShouldBe(ComputeSha256(live), relative);
        }
    }

    /// <summary>
    /// Verifies provenance checks name every executing module on the source-only import path,
    /// including both package initializers and the transitive predecessor handler.
    /// </summary>
    [Fact]
    public void ImportedModuleProvenanceCoversTheCompleteVerifiedPath()
    {
        string root = FindRepositoryRoot();
        string dispatcher = File.ReadAllText(
            Path.Combine(root, "tools", "validate-corrected-deployed-runtime-parity.py"));

        string[] expected =
        [
            "deployed_runtime_parity_handlers/__init__.py",
            "deployed_runtime_parity_handlers/v1.py",
            "release_evidence_handlers/__init__.py",
            "release_evidence_handlers/v3.py",
        ];
        foreach (string relative in expected)
        {
            dispatcher.ShouldContain($"_verify_imported_file(");
            dispatcher.ShouldContain($"\"{relative}\"");
        }

        Regex.Matches(dispatcher, "_verify_imported_file\\(").Count.ShouldBe(5);
    }

    /// <summary>
    /// Verifies docs/ci.md states the actual current acceptance-ready subject and selected identity
    /// digests, rather than a value that can silently drift from the checked-in evidence.
    /// </summary>
    [Fact]
    public void CiDocDescribesTheCurrentSubjectAndSelectedIdentityDigests()
    {
        string root = FindRepositoryRoot();
        JsonObject closure = LoadJson(Path.Combine(root, EvidenceRelativePath, "closure.json"));
        string subjectSha256 = closure["subject"]!["sha256"]!.GetValue<string>();
        string ci = File.ReadAllText(Path.Combine(root, "docs", "ci.md"));

        ci.ShouldContain(subjectSha256);
        ci.ShouldContain(IndexDigest["sha256:".Length..]);

        // Presence alone cannot notice a superseded digest left behind beside the current one, so
        // require the set of 64-hex tokens in the Story 3.15 section to be exactly the expected
        // three: the current subject, the selected index, and the frozen predecessor identity. The
        // slice stops at the next top-level heading -- running to end-of-file would fold every
        // later section's digests into the assertion -- and the token pattern is boundary-anchored
        // so a longer hex run cannot satisfy it through a 64-character window.
        int section = ci.IndexOf("### Story 3.15", StringComparison.Ordinal);
        section.ShouldBeGreaterThan(-1);
        int sectionEnd = ci.IndexOf("\n## ", section, StringComparison.Ordinal);
        sectionEnd.ShouldBeGreaterThan(section);
        string[] digests = Regex.Matches(
                ci[section..sectionEnd],
                "(?<![0-9a-fA-F])[0-9a-f]{64}(?![0-9a-fA-F])")
            .Select(match => match.Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        digests.ShouldBe(
            new[] { subjectSha256, IndexDigest["sha256:".Length..], PredecessorSha256 }
                .Order(StringComparer.Ordinal)
                .ToArray());
    }

    /// <summary>
    /// Verifies a stray file added anywhere in the retained packet (outside the acceptances
    /// directory) fails closed, even when every declared binding still verifies.
    /// </summary>
    [Fact]
    public void StrayUnlistedFileInPacketFailsClosed()
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            File.WriteAllBytes(Path.Combine(temporary, "packages", "stray-not-listed.bin"), [1, 2, 3]);

            ShouldFailClosed(
                RunValidator(root, temporary),
                "packet contains files outside the closed technical inventory");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies file, directory, and dangling symbolic links cannot evade the packet inventory walk
    /// or the separately close-listed acceptance tree.
    /// </summary>
    /// <param name="shape">Symbolic-link shape to plant.</param>
    [Theory]
    [InlineData("file")]
    [InlineData("directory")]
    [InlineData("dangling-acceptance")]
    public void SymbolicLinksCannotEvadeClosedInventory(string shape)
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            JsonObject closure = LoadJson(Path.Combine(temporary, "closure.json"));
            if (shape == "file")
            {
                File.CreateSymbolicLink(
                    Path.Combine(temporary, "stray-link"),
                    Path.Combine(temporary, "subject.json"));
            }
            else if (shape == "directory")
            {
                Directory.CreateSymbolicLink(
                    Path.Combine(temporary, "linked-packages"),
                    Path.Combine(temporary, "packages"));
            }
            else
            {
                string acceptanceDirectory = closure["acceptances"]!["directory"]!.GetValue<string>();
                File.CreateSymbolicLink(
                    Path.Combine(temporary, acceptanceDirectory, "dangling.json"),
                    Path.Combine(temporary, "does-not-exist"));
            }

            ShouldFailClosed(
                RunValidator(root, temporary),
                "packet contains a symbolic link outside the closed inventory");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies an acceptance tree for any subject other than the bound one fails closed. The bound
    /// subject's directory is close-listed by the receipt check and therefore skipped by the
    /// inventory sweep; without a dedicated rejection a superseded or planted receipt tree would
    /// ride along entirely unhashed.
    /// </summary>
    [Fact]
    public void ForeignAcceptanceTreeOutsideTheBoundSubjectFailsClosed()
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            string foreign = Path.Combine(temporary, "acceptances", SupersededSubjectSha256);
            Directory.CreateDirectory(foreign);
            File.WriteAllBytes(Path.Combine(foreign, "eventstore-owner.json"), "{}\n"u8.ToArray());

            ShouldFailClosed(
                RunValidator(root, temporary),
                "packet retains an acceptance tree outside the bound subject");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies the bound subject's acceptance directory is closed over exactly three receipts and
    /// three sources. That directory is exempt from the inventory sweep, so an extra file placed in
    /// it -- or in its sources/ subdirectory -- is only ever caught here.
    /// </summary>
    /// <param name="subdirectory">Directory under the acceptance tree to plant the stray file in.</param>
    [Theory]
    [InlineData("")]
    [InlineData("sources")]
    public void StrayFileInsideTheBoundAcceptanceDirectoryFailsClosed(string subdirectory)
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            JsonObject closure = LoadJson(Path.Combine(temporary, "closure.json"));
            string acceptanceDirectory = closure["acceptances"]!["directory"]!.GetValue<string>();
            File.WriteAllBytes(
                Path.Combine(temporary, acceptanceDirectory, subdirectory, "stray.json"),
                "{}\n"u8.ToArray());

            ShouldFailClosed(
                RunValidator(root, temporary),
                "acceptance directory is not closed over exactly three receipts and sources");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies a NuGet.org package with a duplicated signature entry fails closed on the signature
    /// count check itself, even when its retained-file hash binding is correctly recomputed and its
    /// bytes remain clearly distinct from the GitHub release-asset domain.
    /// </summary>
    [Fact]
    public void PackageWithDuplicatedSignatureEntryFailsClosedOnSignatureCheck()
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            string closurePath = Path.Combine(temporary, "closure.json");
            JsonObject closure = LoadJson(closurePath);
            string relative = closure["packages"]!["items"]![0]!["nuget_org"]!["file"]!.GetValue<string>();
            string packagePath = Path.Combine(temporary, relative);
            DuplicateZipEntry(packagePath, ".signature.p7s");
            closure["packages"]!["items"]![0]!["nuget_org"]!["sha256"] = ComputeSha256(packagePath);
            closure["packages"]!["items"]![0]!["nuget_org"]!["size"] = new FileInfo(packagePath).Length;
            WriteCanonical(closurePath, closure);

            ShouldFailClosed(
                RunValidator(root, temporary),
                "NuGet.org package signature or nuspec identity is invalid");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies packet-supplied nuspec XML cannot declare or expand internal entities, even after
    /// the mutated package's retained binding is corrected.
    /// </summary>
    [Fact]
    public void PackageNuspecWithEntityDeclarationFailsClosedBeforeExpansion()
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            string closurePath = Path.Combine(temporary, "closure.json");
            JsonObject closure = LoadJson(closurePath);
            JsonObject nuget = closure["packages"]!["items"]![0]!["nuget_org"]!.AsObject();
            string packagePath = Path.Combine(temporary, nuget["file"]!.GetValue<string>());
            ReplaceNuspec(
                packagePath,
                "<!DOCTYPE package [<!ENTITY packageId \"Hexalith.EventStore.Contracts\">]>" +
                "<package><metadata><id>&packageId;</id><version>3.96.2</version>" +
                "<repository type=\"git\" url=\"https://github.com/Hexalith/Hexalith.EventStore\" " +
                $"commit=\"{SourceSha}\" /></metadata></package>");
            UpdateFileBinding(nuget, packagePath, updateDigest: false);
            WriteCanonical(closurePath, closure);

            ShouldFailClosed(
                RunValidator(root, temporary),
                "package nuspec contains forbidden DTD or entity declarations");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies a Production smoke log whose content disagrees with its own result summary fails
    /// closed on the log-reproduction check, even when every hash binding along the way is correct.
    /// </summary>
    [Fact]
    public void SmokeLogDisagreeingWithItsOwnResultSummaryFailsClosed()
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            string closurePath = Path.Combine(temporary, "closure.json");
            JsonObject closure = LoadJson(closurePath);
            string resultsRelative =
                closure["production_smokes"]!["results"]!["file"]!.GetValue<string>();
            string resultsPath = Path.Combine(temporary, resultsRelative);
            JsonObject results = LoadJson(resultsPath);
            JsonObject platform = (JsonObject)results["platforms"]!.AsArray()[0]!;
            string logRelative = platform["log"]!["file"]!.GetValue<string>();
            string logPath = Path.Combine(temporary, logRelative);
            JsonObject log = LoadJson(logPath);
            log["http_status"] = 599;
            WriteCanonical(logPath, log);
            platform["log"]!["sha256"] = ComputeSha256(logPath);
            platform["log"]!["size"] = new FileInfo(logPath).Length;
            WriteCanonical(resultsPath, results);
            closure["production_smokes"]!["results"]!["sha256"] = ComputeSha256(resultsPath);
            closure["production_smokes"]!["results"]!["size"] = new FileInfo(resultsPath).Length;
            WriteCanonical(closurePath, closure);

            ShouldFailClosed(
                RunValidator(root, temporary),
                "Production smoke log does not reproduce its result");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies a GitHub receipt source whose author association is not a rostered level fails
    /// closed, even when the receipt's durable-source binding is correctly recomputed for it.
    /// </summary>
    [Fact]
    public void ReceiptGitHubSourceWithUnrosteredAuthorAssociationFailsClosed()
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            RewriteReceiptSource(temporary, 0, source => source["author_association"] = "NONE");

            ShouldFailClosed(
                RunValidator(root, temporary),
                "GitHub acceptance source is not authenticated to the rostered owner");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies a receipt anchored on the Story 1.20 or Story 3.14 acceptance thread fails closed.
    /// Those threads are rejected by number: reusing one is the cross-lineage splice this story
    /// family exists to prevent, and the superseded bb58d691 receipts were collected on issue 346.
    /// </summary>
    /// <param name="issue">Foreign-lineage issue number to anchor the receipt source on.</param>
    [Theory]
    [InlineData(324)]
    [InlineData(346)]
    public void ReceiptSourceAnchoredOnForeignLineageIssueFailsClosed(int issue)
    {
        string root = FindRepositoryRoot();
        ForeignLineageIssues.ShouldContain(issue);
        string temporary = CreateAcceptedPacket(root);
        try
        {
            RewriteReceiptSource(temporary, 0, source =>
            {
                long commentId = source["id"]!.GetValue<long>();
                source["html_url"] = FormattableString.Invariant(
                    $"https://github.com/Hexalith/Hexalith.EventStore/issues/{issue}#issuecomment-{commentId}");
                source["issue_url"] = FormattableString.Invariant(
                    $"https://api.github.com/repos/Hexalith/Hexalith.EventStore/issues/{issue}");
            });

            ShouldFailClosed(
                RunValidator(root, temporary),
                "GitHub acceptance source is not authenticated to the rostered owner");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies a retained comment whose id, anchor, comment URL, and issue URL do not all resolve
    /// to one comment on one thread fails closed. Prefix-matching each field independently let a
    /// receipt cite an id from one thread, an anchor from another, and an issue URL from a third.
    /// </summary>
    /// <param name="mutation">Which of the four cross-referenced fields to desynchronize.</param>
    [Theory]
    [InlineData("anchor-comment-id")]
    [InlineData("issue-url-thread")]
    [InlineData("comment-url-id")]
    public void ReceiptSourceIdentityMustResolveToOneComment(string mutation)
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            RewriteReceiptSource(temporary, 0, source =>
            {
                long commentId = source["id"]!.GetValue<long>();
                switch (mutation)
                {
                    case "anchor-comment-id":
                        source["html_url"] = FormattableString.Invariant(
                            $"https://github.com/Hexalith/Hexalith.EventStore/issues/{SyntheticAcceptanceIssue}#issuecomment-{commentId + 1}");
                        break;
                    case "issue-url-thread":
                        source["issue_url"] = FormattableString.Invariant(
                            $"https://api.github.com/repos/Hexalith/Hexalith.EventStore/issues/{SyntheticAcceptanceIssue + 1}");
                        break;
                    case "comment-url-id":
                        source["url"] = FormattableString.Invariant(
                            $"https://api.github.com/repos/Hexalith/Hexalith.EventStore/issues/comments/{commentId + 1}");
                        break;
                }
            });

            ShouldFailClosed(
                RunValidator(root, temporary),
                "GitHub acceptance source is not authenticated to the rostered owner");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies each rostered role is bound to exactly one acceptance source kind, in both
    /// directions. Without that binding an owner receipt could present a self-attested bmad record
    /// and skip GitHub authentication entirely, and a Test Architect receipt could claim a GitHub
    /// comment identity the roster never granted it.
    /// </summary>
    /// <param name="role">Rostered role whose receipt is mutated.</param>
    /// <param name="kind">Source kind to declare for that role.</param>
    [Theory]
    [InlineData("eventstore-owner", "bmad-test-architect-record")]
    [InlineData("release-owner", "bmad-test-architect-record")]
    [InlineData("test-architect", "github-issue-comment")]
    public void ReceiptSourceKindMustMatchTheRosteredRole(string role, string kind)
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            RewriteReceipt(
                temporary,
                ReceiptIndex(temporary, role),
                receipt => receipt["durable_source"]!["kind"] = kind);

            ShouldFailClosed(
                RunValidator(root, temporary),
                "acceptance source kind does not match the rostered role");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies a source kind outside the two rostered values fails at the single load-bearing
    /// role-to-kind allowlist instead of relying on an unreachable downstream branch.
    /// </summary>
    /// <param name="kind">Non-allowlisted source kind to declare.</param>
    [Theory]
    [InlineData("email")]
    [InlineData("signal-message")]
    [InlineData("")]
    public void ReceiptSourceKindOutsideTheAllowlistFailsClosed(string kind)
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            RewriteReceipt(temporary, 0, receipt => receipt["durable_source"]!["kind"] = kind);

            ShouldFailClosed(
                RunValidator(root, temporary),
                "acceptance source kind does not match the rostered role");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies the owner-role registry authority source is rejected when its role-mapping lines
    /// are extended with a contradicting or extra role assignment, closing what was previously a
    /// substring-only containment check.
    /// </summary>
    [Fact]
    public void RegistryAuthoritySourceWithContradictingRoleLineFailsClosed()
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            MutateRegistrySourceBody(
                temporary,
                body => body + "\n- eventstore-owner: github:mallory");

            ShouldFailClosed(
                RunValidator(root, temporary),
                "owner-role registry authority source is invalid");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies a contradicting role line placed *before* the genuine mapping is rejected too.
    /// The mapping was built by feeding regex findall() straight into dict(), which is last-wins:
    /// an appended contradiction lost to the genuine line and an appended one won, so only the
    /// prepended shape proves the repeated-key rejection rather than the ordering accident.
    /// </summary>
    [Fact]
    public void RegistryAuthoritySourceWithPrependedContradictingRoleLineFailsClosed()
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            MutateRegistrySourceBody(
                temporary,
                body => "- eventstore-owner: github:mallory\n" + body);

            ShouldFailClosed(
                RunValidator(root, temporary),
                "owner-role registry authority source is invalid");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies the reused roster comment must itself disclaim deployment authority. The comment is
    /// reused across stories because it records a role-holder fact, so removing the disclaiming
    /// sentence is exactly how that reuse would silently widen into a release authorization.
    /// </summary>
    [Fact]
    public void RegistryAuthoritySourceWithoutDeploymentDisclaimerFailsClosed()
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            MutateRegistrySourceBody(temporary, body =>
            {
                int disclaimer = body.IndexOf("It authorizes no", StringComparison.Ordinal);
                disclaimer.ShouldBeGreaterThan(-1);
                return body[..disclaimer];
            });

            ShouldFailClosed(
                RunValidator(root, temporary),
                "owner-role registry authority source is invalid");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies disclaimer-like sentences that actually authorize deployment cannot satisfy the
    /// exact retained non-authority statement.
    /// </summary>
    /// <param name="replacement">Contradictory sentence replacing the genuine disclaimer.</param>
    [Theory]
    [InlineData("It authorizes no obstacle to deployment.")]
    [InlineData("It authorizes no changes, and authorizes deployment of any image.")]
    public void RegistryAuthoritySourceWithContradictoryDisclaimerFailsClosed(string replacement)
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            MutateRegistrySourceBody(temporary, body =>
            {
                const string GenuineDisclaimer =
                    "This comment is the durable external authority_source for reviewer-roster.json. " +
                    "It authorizes no package recovery, release, registry mutation, deployment, consumer " +
                    "migration, or Story 3.13 done status.";
                body.ShouldContain(GenuineDisclaimer);
                return body.Replace(GenuineDisclaimer, replacement, StringComparison.Ordinal);
            });

            ShouldFailClosed(
                RunValidator(root, temporary),
                "owner-role registry authority source is invalid");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Positive control for the registry authority source: GitHub returns comment bodies with CRLF
    /// line endings, which defeat the line-anchored role-mapping pattern and the one-sentence
    /// disclaimer match. A genuine roster comment must still be accepted, so the tightened checks
    /// cannot be shown green by rejecting everything.
    /// </summary>
    [Fact]
    public void RegistryAuthoritySourceWithCrlfBodyIsAccepted()
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacketWithRegistrySourceBody(
            root,
            body => body.Replace("\n", "\r\n", StringComparison.Ordinal));
        try
        {
            JsonObject registry = LoadJson(Path.Combine(temporary, "registry", "owner-role-registry.json"));
            string sourcePath = Path.Combine(
                temporary,
                registry["authority_source"]!["file"]!.GetValue<string>());
            LoadJson(sourcePath)["body"]!.GetValue<string>().ShouldContain("\r\n");

            (int exitCode, string output, string error) = RunValidator(root, temporary);

            exitCode.ShouldBe(0, error);
            output.ShouldContain("selected=" + IndexDigest);
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies the Test Architect receipt's structurally distinct source branch is itself exercised
    /// by a negative case, not only ever reached by the all-green positive-closure fixture.
    /// </summary>
    [Fact]
    public void TestArchitectReceiptSourceMismatchFailsClosed()
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            RewriteReceiptSource(
                temporary,
                ReceiptIndex(temporary, "test-architect"),
                source => source["test_architect"] = "bmad:mallory");

            ShouldFailClosed(
                RunValidator(root, temporary),
                "Test Architect acceptance source is invalid");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies removing a single nested field from a receipt's durable_source binding fails closed,
    /// not only removal of a top-level receipt field.
    /// </summary>
    /// <param name="field">Nested durable-source field to remove.</param>
    [Theory]
    [InlineData("file")]
    [InlineData("kind")]
    [InlineData("sha256")]
    [InlineData("size")]
    public void ReceiptDurableSourceMissingNestedFieldFailsClosed(string field)
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            RewriteReceipt(temporary, 0, receipt => receipt["durable_source"]!.AsObject().Remove(field));

            ShouldFailClosed(RunValidator(root, temporary), "receipt source binding is invalid");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies unreviewed bytes anywhere on the verifier's import path fail closed before they
    /// execute. Each case tampers one file in a copied tool tree; the injected marker must never
    /// reach stdout. The untampered control proves the copied tree can otherwise reach validation,
    /// so a broken harness cannot masquerade as the guard firing.
    /// </summary>
    /// <param name="tamperedFile">Import-path file to append unreviewed code to.</param>
    [Theory]
    [InlineData("deployed_runtime_parity_handlers/__init__.py")]
    [InlineData("deployed_runtime_parity_handlers/v1.py")]
    [InlineData("release_evidence_handlers/__init__.py")]
    [InlineData("release_evidence_handlers/v3.py")]
    public void TamperedImportPathBytesNeverExecute(string tamperedFile)
    {
        string root = FindRepositoryRoot();
        string packet = Path.Combine(root, EvidenceRelativePath);
        string temporary = Path.Combine(Path.GetTempPath(), $"eventstore-story315-import-{Guid.NewGuid():N}");
        try
        {
            CopyDirectory(Path.Combine(root, "tools"), Path.Combine(temporary, "tools"));

            // Control: the copied tree must reach real validation before anything is tampered.
            ShouldFailClosed(
                RunProcess(
                    temporary,
                    "python3",
                    "tools/validate-corrected-deployed-runtime-parity.py",
                    Path.Combine(packet, "closure.json"),
                    "--packet-root",
                    packet),
                "exactly three packet-bound receipts are required");

            File.AppendAllText(
                Path.Combine(temporary, "tools", tamperedFile),
                "\nprint('untrusted-import-executed')\n");

            (int exitCode, string output, string error) = RunProcess(
                temporary,
                "python3",
                "tools/validate-corrected-deployed-runtime-parity.py",
                Path.Combine(packet, "closure.json"),
                "--packet-root",
                packet);

            exitCode.ShouldBe(1, error);
            error.ShouldContain("trusted live handler source does not match its pinned SHA-256");
            output.ShouldNotContain("untrusted-import-executed");
            output.ShouldNotContain("pass:");
        }
        finally
        {
            if (Directory.Exists(temporary))
            {
                Directory.Delete(temporary, recursive: true);
            }
        }
    }

    /// <summary>
    /// Verifies timestamp-valid stale bytecode cannot execute in place of the pinned source bytes.
    /// The cached module prints a marker, then the trusted source is restored with the same length
    /// and timestamp so ordinary importlib considers the malicious pyc valid.
    /// </summary>
    [Fact]
    public void StaleBytecodeCannotStandInForVerifiedSource()
    {
        string root = FindRepositoryRoot();
        string packet = Path.Combine(root, EvidenceRelativePath);
        string temporary = Path.Combine(Path.GetTempPath(), $"eventstore-story315-pyc-{Guid.NewGuid():N}");
        try
        {
            CopyDirectory(Path.Combine(root, "tools"), Path.Combine(temporary, "tools"));
            string handlerPath = Path.Combine(
                temporary,
                "tools",
                "deployed_runtime_parity_handlers",
                "v1.py");
            byte[] trusted = File.ReadAllBytes(handlerPath);
            string trustedText = Encoding.UTF8.GetString(trusted);
            const string ReplaceableLine =
                "# subject must bind them too, or a v3 change would leave the subject and every receipt valid while";
            int lineStart = trustedText.IndexOf(ReplaceableLine, StringComparison.Ordinal);
            lineStart.ShouldBeGreaterThan(0);
            string maliciousLine = "print('untrusted-bytecode-executed')".PadRight(ReplaceableLine.Length);
            maliciousLine.Length.ShouldBe(ReplaceableLine.Length);
            byte[] malicious = [.. trusted];
            Encoding.ASCII.GetBytes(maliciousLine).CopyTo(malicious, lineStart);
            DateTime timestampCandidate = DateTime.UtcNow.AddMinutes(-1);
            DateTime cacheTimestamp = new(
                timestampCandidate.Ticks - (timestampCandidate.Ticks % TimeSpan.TicksPerSecond),
                DateTimeKind.Utc);
            File.WriteAllBytes(handlerPath, malicious);
            File.SetLastWriteTimeUtc(handlerPath, cacheTimestamp);
            (int compileExit, _, string compileError) = RunProcess(
                temporary,
                "python3",
                "-m",
                "py_compile",
                handlerPath);
            compileExit.ShouldBe(0, compileError);
            File.WriteAllBytes(handlerPath, trusted);
            File.SetLastWriteTimeUtc(handlerPath, cacheTimestamp);

            (int exitCode, string output, string error) = RunProcess(
                temporary,
                "python3",
                "tools/validate-corrected-deployed-runtime-parity.py",
                Path.Combine(packet, "closure.json"),
                "--packet-root",
                packet);

            exitCode.ShouldBe(1, error);
            error.ShouldContain("exactly three packet-bound receipts are required");
            error.ShouldContain("rerun: " + RerunTrigger);
            output.ShouldNotContain("untrusted-bytecode-executed");
            output.ShouldNotContain("pass:");
        }
        finally
        {
            if (Directory.Exists(temporary))
            {
                Directory.Delete(temporary, recursive: true);
            }
        }
    }

    /// <summary>
    /// Asserts one validator run failed closed: exit code exactly 1, the expected reason on stderr,
    /// and no pass line on stdout. A fail-open -- exit 0 with a printed pass line while a guard
    /// silently skipped -- is the specific regression this lane suffered, so a nonzero-exit
    /// assertion alone is not enough.
    /// </summary>
    /// <param name="result">Validator process result.</param>
    /// <param name="expectedError">Expected fail-closed reason on stderr.</param>
    private static void ShouldFailClosed(
        (int ExitCode, string Output, string Error) result,
        string expectedError)
    {
        result.ExitCode.ShouldBe(1, result.Error);
        result.Error.ShouldContain(expectedError);
        result.Error.ShouldContain("rerun: " + RerunTrigger);
        result.Output.ShouldNotContain("pass:");
    }

    private static void DuplicateZipEntry(string zipPath, string entryName)
    {
        using ZipArchive archive = ZipFile.Open(zipPath, ZipArchiveMode.Update);
        ZipArchiveEntry original = archive.GetEntry(entryName).ShouldNotBeNull();
        using Stream originalStream = original.Open();
        using MemoryStream buffer = new();
        originalStream.CopyTo(buffer);
        ZipArchiveEntry duplicate = archive.CreateEntry(entryName);
        using Stream duplicateStream = duplicate.Open();
        buffer.Position = 0;
        buffer.CopyTo(duplicateStream);
    }

    private static void ReplaceNuspec(string zipPath, string content)
    {
        using ZipArchive archive = ZipFile.Open(zipPath, ZipArchiveMode.Update);
        ZipArchiveEntry[] nuspecs = archive.Entries
            .Where(entry => entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal))
            .ToArray();
        nuspecs.Length.ShouldBe(1);
        string name = nuspecs[0].FullName;
        nuspecs[0].Delete();
        ZipArchiveEntry replacement = archive.CreateEntry(name);
        using Stream stream = replacement.Open();
        stream.Write(Encoding.UTF8.GetBytes(content));
    }

    private static JsonObject FindFileBinding(JsonNode node, string relativePath)
    {
        if (node is JsonObject candidate
            && candidate["file"]?.GetValue<string>() == relativePath
            && candidate.ContainsKey("sha256")
            && candidate.ContainsKey("size"))
        {
            return candidate;
        }

        IEnumerable<JsonNode?> children = node switch
        {
            JsonObject item => item.Select(property => property.Value),
            JsonArray items => items,
            _ => [],
        };
        foreach (JsonNode? child in children)
        {
            if (child is null)
            {
                continue;
            }

            try
            {
                return FindFileBinding(child, relativePath);
            }
            catch (InvalidOperationException)
            {
            }
        }

        throw new InvalidOperationException($"File binding '{relativePath}' was not found.");
    }

    private static void UpdateFileBinding(JsonObject binding, string path, bool updateDigest)
    {
        string sha256 = ComputeSha256(path);
        binding["sha256"] = sha256;
        binding["size"] = new FileInfo(path).Length;
        if (updateDigest)
        {
            binding["digest"] = "sha256:" + sha256;
        }
    }

    private static string CopyPacket(string root)
    {
        string source = Path.Combine(root, EvidenceRelativePath);
        string temporary = Path.Combine(Path.GetTempPath(), $"eventstore-story315-{Guid.NewGuid():N}");
        CopyDirectory(source, temporary);
        return temporary;
    }

    private static string CreateAcceptedPacket(string root)
    {
        string temporary = CopyPacket(root);
        AttachThreeAcceptedReceipts(temporary);
        return temporary;
    }

    private static string CreateAcceptedPacketWithRegistrySourceBody(
        string root,
        Func<string, string> transform)
    {
        string temporary = CopyPacket(root);
        MutateRegistrySourceBody(temporary, transform);
        RebindInventoryAndSubject(temporary);
        AttachThreeAcceptedReceipts(temporary);
        return temporary;
    }

    private static void AttachThreeAcceptedReceipts(string temporary)
    {
        JsonObject closure = LoadJson(Path.Combine(temporary, "closure.json"));
        string subjectHash = closure["subject"]!["sha256"]!.GetValue<string>();
        JsonObject subject = LoadJson(Path.Combine(temporary, "subject.json"));
        DateTimeOffset acceptedAt = DateTimeOffset.Parse(
            subject["created_at"]!.GetValue<string>(),
            CultureInfo.InvariantCulture);
        string acceptanceDirectory = "acceptances/" + subjectHash;
        closure["acceptances"]!["directory"] = acceptanceDirectory;
        string sourcesDirectory = Path.Combine(temporary, acceptanceDirectory, "sources");
        Directory.CreateDirectory(sourcesDirectory);
        JsonArray bindings = [];
        foreach (string role in RequiredRoles)
        {
            string identity = role == "test-architect" ? "bmad:murat" : "github:jpiquot";
            JsonObject acceptance = new()
            {
                ["accepted_at"] = acceptedAt.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
                ["accepted_limitations"] = new JsonArray(Limitations.Select(value => (JsonNode)value).ToArray()),
                ["accepted_scope"] = "Story 3.15 corrected deployed-runtime parity for " + subjectHash,
                ["decision"] = "accepted",
                ["reviewer_identity"] = identity,
                ["role"] = role,
                ["schema"] = "hexalith.eventstore.deployed-runtime-parity-acceptance.v1",
                ["subject_sha256"] = subjectHash,
            };
            JsonObject sourceDocument;
            string sourceKind;
            if (role == "test-architect")
            {
                sourceKind = "bmad-test-architect-record";
                sourceDocument = new()
                {
                    ["acceptance"] = acceptance.DeepClone(),
                    ["repository"] = "Hexalith/Hexalith.EventStore",
                    ["schema"] = "hexalith.eventstore.test-architect-acceptance-source.v1",
                    ["test_architect"] = "bmad:murat",
                };
            }
            else
            {
                sourceKind = "github-issue-comment";

                // Each rostered owner accepts in a distinct comment; sharing one id across roles
                // would let a single comment stand in for two of the three required acceptances.
                long commentId = role == "eventstore-owner" ? 9000001L : 9000002L;
                sourceDocument = new()
                {
                    ["author_association"] = "MEMBER",
                    ["body"] = Encoding.UTF8.GetString(CanonicalBytes(acceptance)).TrimEnd('\n'),
                    ["created_at"] = acceptedAt.ToString(
                        "yyyy-MM-dd'T'HH:mm:ss'Z'",
                        CultureInfo.InvariantCulture),
                    ["html_url"] = FormattableString.Invariant(
                        $"https://github.com/Hexalith/Hexalith.EventStore/issues/{SyntheticAcceptanceIssue}#issuecomment-{commentId}"),
                    ["id"] = commentId,
                    ["issue_url"] = FormattableString.Invariant(
                        $"https://api.github.com/repos/Hexalith/Hexalith.EventStore/issues/{SyntheticAcceptanceIssue}"),
                    ["performed_via_github_app"] = null,
                    ["updated_at"] = acceptedAt.ToString(
                        "yyyy-MM-dd'T'HH:mm:ss'Z'",
                        CultureInfo.InvariantCulture),
                    ["url"] = FormattableString.Invariant(
                        $"https://api.github.com/repos/Hexalith/Hexalith.EventStore/issues/comments/{commentId}"),
                    ["user"] = new JsonObject { ["id"] = 6775094, ["login"] = "jpiquot" },
                };
            }

            string sourcePath = Path.Combine(sourcesDirectory, role + ".json");
            WriteCanonical(sourcePath, sourceDocument);
            JsonObject receipt = (JsonObject)acceptance.DeepClone();
            receipt["durable_source"] = new JsonObject
            {
                ["file"] = Path.GetRelativePath(temporary, sourcePath).Replace('\\', '/'),
                ["kind"] = sourceKind,
                ["sha256"] = ComputeSha256(sourcePath),
                ["size"] = new FileInfo(sourcePath).Length,
            };
            string receiptPath = Path.Combine(temporary, acceptanceDirectory, role + ".json");
            WriteCanonical(receiptPath, receipt);
            bindings.Add(new JsonObject
            {
                ["file"] = Path.GetRelativePath(temporary, receiptPath).Replace('\\', '/'),
                ["role"] = role,
                ["sha256"] = ComputeSha256(receiptPath),
                ["size"] = new FileInfo(receiptPath).Length,
            });
        }

        closure["acceptances"]!["receipts"] = bindings;
        WriteCanonical(Path.Combine(temporary, "closure.json"), closure);
    }

    /// <summary>
    /// Rewrites the retained roster comment's body, then rebinds the registry document and the
    /// closure's registry binding. The technical inventory and the canonical subject are left alone
    /// on purpose: registry validation runs before the inventory sweep, so a negative case fails on
    /// the registry itself.
    /// </summary>
    /// <param name="packet">Packet root to mutate.</param>
    /// <param name="transform">Body rewrite to apply.</param>
    private static void MutateRegistrySourceBody(string packet, Func<string, string> transform)
    {
        string closurePath = Path.Combine(packet, "closure.json");
        JsonObject closure = LoadJson(closurePath);
        string registryPath = Path.Combine(
            packet,
            closure["owner_role_registry"]!["file"]!.GetValue<string>());
        JsonObject registry = LoadJson(registryPath);
        string sourcePath = Path.Combine(
            packet,
            registry["authority_source"]!["file"]!.GetValue<string>());
        JsonObject source = LoadJson(sourcePath);
        source["body"] = transform(source["body"]!.GetValue<string>());
        WriteCanonical(sourcePath, source);
        registry["authority_source"]!["sha256"] = ComputeSha256(sourcePath);
        registry["authority_source"]!["size"] = new FileInfo(sourcePath).Length;
        WriteCanonical(registryPath, registry);
        closure["owner_role_registry"]!["sha256"] = ComputeSha256(registryPath);
        closure["owner_role_registry"]!["size"] = new FileInfo(registryPath).Length;
        WriteCanonical(closurePath, closure);
    }

    /// <summary>
    /// Recomputes the closed technical inventory and re-mints the canonical subject after a
    /// retained file changed, so a positive-control packet reproduces the verifier's own derivation
    /// instead of asserting against a hand-written digest.
    /// </summary>
    /// <param name="packet">Packet root to rebind.</param>
    private static void RebindInventoryAndSubject(string packet)
    {
        string closurePath = Path.Combine(packet, "closure.json");
        JsonObject closure = LoadJson(closurePath);
        JsonObject registry = LoadJson(Path.Combine(
            packet,
            closure["owner_role_registry"]!["file"]!.GetValue<string>()));
        JsonObject results = LoadJson(Path.Combine(
            packet,
            closure["production_smokes"]!["results"]!["file"]!.GetValue<string>()));

        List<string> inventory = [];
        foreach (JsonNode? item in closure["packages"]!["items"]!.AsArray())
        {
            inventory.Add(item!["nuget_org"]!["file"]!.GetValue<string>());
        }

        foreach (JsonNode? child in closure["oci"]!["children"]!.AsArray())
        {
            inventory.Add(child!["manifest"]!["file"]!.GetValue<string>());
            inventory.Add(child["config"]!["file"]!.GetValue<string>());
        }

        foreach (JsonNode? platform in results["platforms"]!.AsArray())
        {
            inventory.Add(platform!["log"]!["file"]!.GetValue<string>());
        }

        inventory.Add(closure["oci"]!["index"]!["file"]!.GetValue<string>());
        inventory.Add(closure["production_smokes"]!["results"]!["file"]!.GetValue<string>());
        inventory.Add(closure["owner_role_registry"]!["file"]!.GetValue<string>());
        inventory.Add(registry["authority_source"]!["file"]!.GetValue<string>());

        string inventoryText = string.Concat(inventory
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Select(relative => FormattableString.Invariant(
                $"{ComputeSha256(Path.Combine(packet, relative))}  {relative}\n")));
        string inventoryPath = Path.Combine(
            packet,
            closure["technical_inventory"]!["file"]!.GetValue<string>());
        File.WriteAllBytes(inventoryPath, Encoding.UTF8.GetBytes(inventoryText));
        closure["technical_inventory"]!["sha256"] = ComputeSha256(inventoryPath);
        closure["technical_inventory"]!["size"] = new FileInfo(inventoryPath).Length;

        string subjectPath = Path.Combine(packet, closure["subject"]!["file"]!.GetValue<string>());
        JsonObject subject = LoadJson(subjectPath);
        subject["authority"]!["owner_role_registry_sha256"] =
            closure["owner_role_registry"]!["sha256"]!.GetValue<string>();
        subject["evidence"]!["technical_inventory_sha256"] =
            closure["technical_inventory"]!["sha256"]!.GetValue<string>();
        WriteCanonical(subjectPath, subject);
        closure["subject"]!["sha256"] = ComputeSha256(subjectPath);
        closure["subject"]!["size"] = new FileInfo(subjectPath).Length;
        WriteCanonical(closurePath, closure);
    }

    private static int ReceiptIndex(string packet, string role)
    {
        JsonArray bindings = LoadJson(Path.Combine(packet, "closure.json"))["acceptances"]!["receipts"]!
            .AsArray();
        return Enumerable.Range(0, bindings.Count)
            .First(index => bindings[index]!["role"]!.GetValue<string>() == role);
    }

    private static void RewriteReceipt(string packet, int index, Action<JsonObject> mutate)
    {
        string closurePath = Path.Combine(packet, "closure.json");
        JsonObject closure = LoadJson(closurePath);
        string receiptPath = Path.Combine(
            packet,
            closure["acceptances"]!["receipts"]![index]!["file"]!.GetValue<string>());
        JsonObject receipt = LoadJson(receiptPath);
        mutate(receipt);
        WriteCanonical(receiptPath, receipt);
        UpdateReceiptBinding(closure, index, receiptPath);
        WriteCanonical(closurePath, closure);
    }

    private static void RewriteReceiptSource(string packet, int index, Action<JsonObject> mutate)
    {
        string closurePath = Path.Combine(packet, "closure.json");
        JsonObject closure = LoadJson(closurePath);
        string receiptPath = Path.Combine(
            packet,
            closure["acceptances"]!["receipts"]![index]!["file"]!.GetValue<string>());
        JsonObject receipt = LoadJson(receiptPath);
        string sourcePath = Path.Combine(
            packet,
            receipt["durable_source"]!["file"]!.GetValue<string>());
        JsonObject source = LoadJson(sourcePath);
        mutate(source);
        WriteCanonical(sourcePath, source);
        receipt["durable_source"]!["sha256"] = ComputeSha256(sourcePath);
        receipt["durable_source"]!["size"] = new FileInfo(sourcePath).Length;
        WriteCanonical(receiptPath, receipt);
        UpdateReceiptBinding(closure, index, receiptPath);
        WriteCanonical(closurePath, closure);
    }

    private static void UpdateReceiptBinding(JsonObject closure, int index, string receiptPath)
    {
        JsonNode binding = closure["acceptances"]!["receipts"]![index].ShouldNotBeNull();
        binding["sha256"] = ComputeSha256(receiptPath);
        binding["size"] = new FileInfo(receiptPath).Length;
    }

    private static (int ExitCode, string Output, string Error) RunValidator(string root, string packet) =>
        RunProcess(
            root,
            "python3",
            "tools/validate-corrected-deployed-runtime-parity.py",
            Path.Combine(packet, "closure.json"),
            "--packet-root",
            packet);

    private static (int ExitCode, string Output, string Error) RunProcess(
        string workingDirectory,
        string fileName,
        params string[] arguments)
    {
        ProcessStartInfo startInfo = new(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo).ShouldNotBeNull();
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(TimeSpan.FromMinutes(2)))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException(
                $"'{fileName} {string.Join(' ', arguments)}' did not exit within the 2-minute test budget.");
        }

        return (process.ExitCode, outputTask.GetAwaiter().GetResult(), errorTask.GetAwaiter().GetResult());
    }

    private static JsonObject LoadJson(string path) => JsonNode.Parse(File.ReadAllBytes(path))!.AsObject();

    private static void WriteCanonical(string path, JsonNode value) =>
        File.WriteAllBytes(path, CanonicalBytes(value));

    private static byte[] CanonicalBytes(JsonNode value)
    {
        using JsonDocument document = JsonDocument.Parse(value.ToJsonString());
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(
            stream,
            new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, Indented = false }))
        {
            WriteCanonicalElement(writer, document.RootElement);
        }

        stream.WriteByte((byte)'\n');
        return stream.ToArray();
    }

    private static void WriteCanonicalElement(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (JsonProperty property in element.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonicalElement(writer, property.Value);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (JsonElement item in element.EnumerateArray())
                {
                    WriteCanonicalElement(writer, item);
                }

                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }

    private static string ComputeSha256(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        }

        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            File.Copy(file, Path.Combine(destination, Path.GetRelativePath(source, file)));
        }
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Hexalith.EventStore.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
