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
    private const string PredecessorPacketTreeSha256 =
        "2d13d833ad0cc3df54c11ff1e53bbf322928f777375a0a36fdbef843bf128f18";
    private const string IndexDigest =
        "sha256:4b1410852b11be3bcaebf8f2e6277c1d30ce13a19f48cf0df86ed93646d709c3";
    private const string EvidenceRelativePath =
        "_bmad-output/implementation-artifacts/evidence/story-3-15/" + SourceSha;

    /// <summary>
    /// First subject digest whose three receipts were superseded when the transitive predecessor
    /// handler became part of the subject.
    /// </summary>
    private const string SupersededSubjectSha256 =
        "bb58d691ee404cc958433e996204e3382721de3931ac64cf8f7a61de97c30709";

    /// <summary>Subject whose receipts are superseded by the trusted-verifier hardening.</summary>
    private const string TrustedVerifierSupersededSubjectSha256 =
        "dab64f5fbbf55783630ad75451d35d517d829e194fb618dc8b0526d39761d38d";

    /// <summary>Subject superseded by the loop-6 producer and limitation bindings.</summary>
    private const string Loop6SupersededSubjectSha256 =
        "a8cc777ed04f1f0a7f7dffb7f24f7359f786e9114afe04fc69b1aa90cb8fdf7f";

    /// <summary>Previously accepted subject superseded by the Step-04 bound-code hardening.</summary>
    private const string Step04SupersededSubjectSha256 =
        "c22d35b617fdecf06168071faf442621501c016b629a3674800f50489e2bf22f";

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
        "The owner acceptance comments were tooling-composed and posted with the authenticated owner's write credential.",
    ];

    private const string RerunTrigger =
        "Rebuild the complete subject and reject all prior receipts after any predecessor, package, OCI, " +
        "Production-smoke, inventory, registry, verifier, decision, or receipt-source policy change.";

    /// <summary>
    /// Exact SHA-256 of every retained superseded artefact, keyed by its path under
    /// <see cref="SupersededRelativePath"/>. Pinning the bytes -- rather than only the directory's
    /// existence -- is what makes the retention auditable: a silently rewritten or emptied receipt
    /// would otherwise still satisfy a presence-only assertion.
    /// </summary>
    private static readonly (string RelativePath, string Sha256)[] SupersededArtefacts =
    [
        ("README.md", "24ec7a65ba9f071bbcab9493805c4afef7db6e6338c80dce8848e37da5a1bf28"),
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
        (TrustedVerifierSupersededSubjectSha256 + "/eventstore-owner.json",
            "a5789ee4b1c33a8a74529aff1ba6797afca2cf583df1391bd57db9bf9b964eca"),
        (TrustedVerifierSupersededSubjectSha256 + "/release-owner.json",
            "4b5f5a7b9ba10704d88e66bafd9815475e108a5b135a8cdece3c4cbfa45094fb"),
        (TrustedVerifierSupersededSubjectSha256 + "/test-architect.json",
            "5897e2e07dd22b0600e8bd4933a6a30d8707708f821dc354cb1e59fe6b4844a4"),
        (TrustedVerifierSupersededSubjectSha256 + "/sources/eventstore-owner.json",
            "33ac0a3300e93f8b7561a46bd3977f25a9441d7c5a05281ed8a9d613942d78bc"),
        (TrustedVerifierSupersededSubjectSha256 + "/sources/release-owner.json",
            "9d21bf3b4fec487e9effdd1ed6bd1f819d4919208744fabec7abef67fb7ca9bc"),
        (TrustedVerifierSupersededSubjectSha256 + "/sources/test-architect.json",
            "cadf2f5c21d1d91bf2a3c407098adc89bd71ab5d195a9f4da34a3220b7cee5d7"),
        (Loop6SupersededSubjectSha256 + "/eventstore-owner.json",
            "846b249857789b97afe6e8204a6136b075055b85d56e9d815bede2d152420370"),
        (Loop6SupersededSubjectSha256 + "/release-owner.json",
            "8f98e5b8b3541d9959c0a64ac741fd03ab59eb9a23caa4db0eca39333df6983e"),
        (Loop6SupersededSubjectSha256 + "/test-architect.json",
            "203e5f2a0749d6bd6da534c1a48d507edc1f71dadde90f2c86b36b6a1a50ded2"),
        (Loop6SupersededSubjectSha256 + "/sources/eventstore-owner.json",
            "6e97eb2c564ae78a3ab875d7458ee3b7d53dc5cc03b8bce8f2a446351dc3edf0"),
        (Loop6SupersededSubjectSha256 + "/sources/release-owner.json",
            "181f2001d93ee982b19758335cb2ba37d7bfd5b0f9e99c77990a40f697d6bb25"),
        (Loop6SupersededSubjectSha256 + "/sources/test-architect.json",
            "c20fee033cfcb055ed2387d0c40109a7da33a0b97e73db98aef71dd975b9e40a"),
        (Step04SupersededSubjectSha256 + "/eventstore-owner.json",
            "c01add7cabfc5ea6bc48f50c0209b6c89cf228da3909b8ba70ff6f42887fd8a4"),
        (Step04SupersededSubjectSha256 + "/release-owner.json",
            "7d9bb1e0c079a34117a63d3ee114c7efd416c3758a500ad7ea235f6710b8e3c7"),
        (Step04SupersededSubjectSha256 + "/test-architect.json",
            "3316e9f576ec755d486a15c962eab50052cccfdd0bc3e86bd851232617f974c5"),
        (Step04SupersededSubjectSha256 + "/sources/eventstore-owner.json",
            "cddd8bebc823dbee401e5c6b8776b376d3276afb24b85b2bc5bb5142ee3f7a57"),
        (Step04SupersededSubjectSha256 + "/sources/release-owner.json",
            "ba8a7374e203b2d915b3ae7195063e2dc19ec139a53057ee2b5cafb5ca055299"),
        (Step04SupersededSubjectSha256 + "/sources/test-architect.json",
            "ec4497455f25bdf9e1137fb24c152f9241e44105bd97f4777852faaeb4a32fa9"),
    ];

    /// <summary>
    /// Dedicated Story 3.15 issue used by both retained receipts and acceptance fixtures.
    /// </summary>
    private const int Story315AcceptanceIssue = 352;

    /// <summary>
    /// Verifies the checked-in packet passes only after all three newly authorized exact-subject
    /// receipts are retained beneath the unchanged subject.
    /// </summary>
    [Fact]
    public void CheckedInPacketPassesWithThreeReceiptsBoundToTheCurrentSubject()
    {
        string root = FindRepositoryRoot();
        string packet = Path.Combine(root, EvidenceRelativePath);

        (int exitCode, string output, string error) = RunValidator(root, packet);
        exitCode.ShouldBe(0, error);
        output.ShouldContain("selected=" + IndexDigest);

        JsonObject closure = LoadJson(Path.Combine(packet, "closure.json"));
        string subject = closure["subject"]!["sha256"]!.GetValue<string>();
        closure["acceptances"]!["directory"]!.GetValue<string>().ShouldBe("acceptances/" + subject);
        closure["acceptances"]!["receipts"]!.AsArray().Count.ShouldBe(3);
        closure["deployed_runtime_parity"]!.GetValue<string>().ShouldBe("available");
        closure["selected_deployed_identity"]!.GetValue<string>().ShouldBe(IndexDigest);
        closure["deployment_authorized"]!.GetValue<bool>().ShouldBeFalse();
        closure["consumer_removal_authorized"]!.GetValue<bool>().ShouldBeFalse();
        closure["publication_authorized"]!.GetValue<bool>().ShouldBeFalse();
        closure["grants_mutation_authority"]!.GetValue<bool>().ShouldBeFalse();

        Directory.Exists(Path.Combine(packet, "acceptances", SupersededSubjectSha256))
            .ShouldBeFalse();
        Directory.Exists(Path.Combine(packet, "acceptances", TrustedVerifierSupersededSubjectSha256))
            .ShouldBeFalse();
        Directory.Exists(Path.Combine(packet, "acceptances", Loop6SupersededSubjectSha256))
            .ShouldBeFalse();
        Directory.Exists(Path.Combine(packet, "acceptances", Step04SupersededSubjectSha256))
            .ShouldBeFalse();
        Directory.Exists(Path.Combine(packet, "acceptances", subject)).ShouldBeTrue();
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
    public void ThreeRosterBoundRolesClosePositiveParityOnOneUnchangedSubject()
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
    [InlineData("noncanonical-closure", "closure bytes are not the selected codec's canonical UTF-8 form")]
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
    /// Mutation-proves fail-closed reasons that define the closure identity, lineage, route,
    /// package provenance, OCI binding, and safe-path boundaries.
    /// </summary>
    /// <param name="mutation">Mutation to apply.</param>
    /// <param name="expectedError">Exact fail-closed reason that must be reached.</param>
    [Theory]
    [InlineData("lineage", "lineage does not reproduce the corrective release")]
    [InlineData("predecessor", "predecessor identity is not the frozen Story 3.14 handoff")]
    [InlineData("handler-route", "closure does not select a trusted live handler")]
    [InlineData("oci-image", "OCI image identity is invalid")]
    [InlineData("oci-binding", "OCI file binding is invalid")]
    [InlineData("unsafe-binding", "file binding path is unsafe")]
    [InlineData("closure-identity", "closure identity is invalid")]
    [InlineData("package-lineage", "package mapping lineage is invalid")]
    [InlineData("invalid-nupkg", "NuGet.org package is not a valid signed archive")]
    public void CriticalFailClosedReasonsAreMutationProved(string mutation, string expectedError)
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            string closurePath = Path.Combine(temporary, "closure.json");
            JsonObject closure = LoadJson(closurePath);
            switch (mutation)
            {
                case "lineage":
                    closure["lineage"]!["version"] = "3.94.1";
                    break;
                case "predecessor":
                    closure["predecessor"]!["sha256"] = new string('0', 64);
                    break;
                case "handler-route":
                    closure["dispatch"]!["handler"]!["sha256"] = new string('0', 64);
                    break;
                case "oci-image":
                    closure["oci"]!["image"] = "registry.hexalith.com/eventstore@sha256:" + new string('0', 64);
                    break;
                case "oci-binding":
                    closure["oci"]!["index"]!["media_type"] = "application/octet-stream";
                    break;
                case "unsafe-binding":
                    closure["packages"]!["items"]![0]!["github_release_asset"]!["file"] = "../outside.nupkg";
                    break;
                case "closure-identity":
                    closure["story_id"] = "3.13";
                    break;
                case "package-lineage":
                    closure["packages"]!["items"]![0]!["repository_commit"] = new string('0', 40);
                    break;
                case "invalid-nupkg":
                    JsonObject binding = closure["packages"]!["items"]![0]!["nuget_org"]!.AsObject();
                    string packagePath = Path.Combine(temporary, binding["file"]!.GetValue<string>());
                    File.WriteAllText(packagePath, "not a NuGet archive");
                    UpdateFileBinding(binding, packagePath, updateDigest: false);
                    break;
            }

            WriteCanonical(closurePath, closure);
            ShouldFailClosed(RunValidator(root, temporary), expectedError);
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies malformed independently retained OCI index JSON reaches the raw-shape guard after
    /// its local file binding is corrected. The outer identity intentionally pins the real index
    /// digest, so this focused handler call isolates the deeper semantic branch.
    /// </summary>
    [Fact]
    public void ReboundMalformedRawOciIndexFailsItsSemanticShapeGuard()
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            string closurePath = Path.Combine(temporary, "closure.json");
            JsonObject closure = LoadJson(closurePath);
            JsonObject indexBinding = closure["oci"]!["index"]!.AsObject();
            string indexPath = Path.Combine(temporary, indexBinding["file"]!.GetValue<string>());
            File.WriteAllText(indexPath, "{\"schemaVersion\":1}\n");
            UpdateFileBinding(indexBinding, indexPath, updateDigest: true);
            WriteCanonical(closurePath, closure);

            const string Script =
                "import pathlib,sys;sys.path.insert(0,sys.argv[1]);" +
                "from deployed_runtime_parity_handlers import v1;" +
                "d=v1.load_json_bytes(pathlib.Path(sys.argv[2]).read_bytes());" +
                "p=v1.predecessor_handler.load_json_bytes(pathlib.Path(sys.argv[3]).read_bytes());" +
                "v1._validate_oci(d,p,pathlib.Path(sys.argv[4]),pathlib.Path(sys.argv[5]))";
            var result = RunProcess(
                root,
                "python3",
                "-c",
                Script,
                Path.Combine(root, "tools"),
                closurePath,
                Path.Combine(root, "_bmad-output", "implementation-artifacts", "evidence", "story-3-14", SourceSha, "release-identity.json"),
                temporary,
                root);

            result.ExitCode.ShouldNotBe(0);
            result.Error.ShouldContain("raw OCI index shape is invalid");
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
    [InlineData("assembler")]
    [InlineData("verifier")]
    [InlineData("predecessor_handler")]
    [InlineData("predecessor_package")]
    [InlineData("smoke_capture")]
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
    /// Verifies JSON values that compare equal to integer one cannot select the v1 handler unless
    /// their JSON type is exactly integer. Python otherwise treats <c>true == 1 == 1.0</c>.
    /// </summary>
    /// <param name="jsonValue">Non-integer JSON value equal to one.</param>
    [Theory]
    [InlineData("true")]
    [InlineData("1.0")]
    public void DispatchVersionRequiresAnExactJsonInteger(string jsonValue)
    {
        string root = FindRepositoryRoot();
        string temporary = CopyPacket(root);
        try
        {
            string closurePath = Path.Combine(temporary, "closure.json");
            JsonObject closure = LoadJson(closurePath);
            closure["dispatch"]!["version"] = JsonNode.Parse(jsonValue);
            WriteCanonical(closurePath, closure);

            (int exitCode, string output, string error) = RunValidator(root, temporary);

            exitCode.ShouldBe(1, error);
            output.ShouldNotContain("pass:");
            error.ShouldContain("closure dispatch metadata is invalid");
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
    /// Verifies the second owner receipt is independently load-bearing rather than covered only by
    /// the eventstore-owner mutation cases at receipt index zero.
    /// </summary>
    [Fact]
    public void ReleaseOwnerReceiptMutationFailsClosed()
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            RewriteReceipt(
                temporary,
                ReceiptIndex(temporary, "release-owner"),
                receipt => receipt["decision"] = "rejected");

            ShouldFailClosed(
                RunValidator(root, temporary),
                "acceptance receipt does not bind the unchanged subject and role");
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
        string predecessorRoot = Path.Combine(
            root,
            "_bmad-output",
            "implementation-artifacts",
            "evidence",
            "story-3-14",
            SourceSha);
        string predecessor = Path.Combine(predecessorRoot, "release-identity.json");

        ComputeTreeBindingSha256(predecessorRoot).ShouldBe(PredecessorPacketTreeSha256);
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
        ComputeTreeBindingSha256(predecessorRoot).ShouldBe(PredecessorPacketTreeSha256);
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
    /// Verifies an external evidence argument cannot drive validation while --packet-root supplies
    /// a different packet, even when the external closure bytes are themselves canonical and the
    /// packet is otherwise fully accepted.
    /// </summary>
    [Fact]
    public void PacketRootRequiresItsOwnClosureAsTheEvidencePath()
    {
        string root = FindRepositoryRoot();
        string packet = CreateAcceptedPacket(root);
        string external = Path.Combine(
            Path.GetTempPath(),
            $"eventstore-story315-external-closure-{Guid.NewGuid():N}.json");
        try
        {
            File.Copy(Path.Combine(packet, "closure.json"), external);

            ShouldFailClosed(
                RunProcess(
                    root,
                    "python3",
                    "tools/validate-corrected-deployed-runtime-parity.py",
                    external,
                    "--packet-root",
                    packet),
                "evidence path is not the packet root's closure.json");
        }
        finally
        {
            File.Delete(external);
            Directory.Delete(packet, recursive: true);
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
    /// Verifies both dispatchers execute already-verified source bytes directly and do not retain
    /// the tautological origin check that compared a module's assigned __file__ to its own input.
    /// </summary>
    [Fact]
    public void DispatchersUseVerifiedSourceWithoutTautologicalOriginChecks()
    {
        string root = FindRepositoryRoot();
        string[] dispatchers =
        [
            Path.Combine(root, "tools", "validate-corrected-deployed-runtime-parity.py"),
            Path.Combine(root, "tools", "validate-corrective-release-evidence.py"),
        ];
        foreach (string path in dispatchers)
        {
            string dispatcher = File.ReadAllText(path);
            dispatcher.ShouldContain("exec(compile(source");
            (dispatcher.Contains("_verify_pinned_source", StringComparison.Ordinal)
                || dispatcher.Contains("_verify_import_path", StringComparison.Ordinal)).ShouldBeTrue();
            dispatcher.ShouldNotContain("def _verify_imported_file");
        }
    }

    /// <summary>
    /// Executes the assembler as a real caller and proves it propagates the pinned verifier's
    /// accepted and incomplete verdicts instead of emitting a success-shaped line unconditionally.
    /// </summary>
    /// <param name="accepted">Whether three complete synthetic receipts are attached.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AssemblerRunsThePinnedVerifierAndPropagatesItsVerdict(bool accepted)
    {
        string root = FindRepositoryRoot();
        string temporary = accepted ? CreateAcceptedPacket(root) : CreateIncompletePacket(root);
        try
        {
            var result = RunProcess(
                root,
                "python3",
                "tools/assemble-corrected-deployed-runtime-parity.py",
                temporary);

            result.ExitCode.ShouldBe(accepted ? 0 : 1, result.Error);
            result.Output.ShouldContain(accepted ? "receipts=3 verifier_exit=0" : "receipts=0 verifier_exit=1");
            result.Output.ShouldContain("subject=sha256:");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
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
    /// Verifies both operator-facing Story 3.15 records carry the current subject and the actual
    /// receipt/verifier verdict, preventing a superseded accepted or fail-closed state from
    /// surviving beside a different checked-in packet.
    /// </summary>
    [Fact]
    public void OperatorRecordsDescribeTheCurrentSubjectAndVerifierVerdict()
    {
        string root = FindRepositoryRoot();
        string packet = Path.Combine(root, EvidenceRelativePath);
        JsonObject closure = LoadJson(Path.Combine(packet, "closure.json"));
        string subject = closure["subject"]!["sha256"]!.GetValue<string>();
        int receipts = closure["acceptances"]!["receipts"]!.AsArray().Count;
        var verifier = RunValidator(root, packet);
        string verdict = verifier.ExitCode == 0
            ? $"Verifier result: `pass` with exactly {receipts} of 3 roster-bound role receipts."
            : $"Verifier result: `fail closed` with exactly {receipts} of 3 roster-bound role receipts; no identity is selected.";

        string[] records =
        [
            Path.Combine(root, "_bmad-output", "implementation-artifacts", "3-15-corrected-deployed-runtime-parity-closure.md"),
            Path.Combine(root, "_bmad-output", "implementation-artifacts", "3-15-corrected-deployed-runtime-parity-closure-proof-packet.md"),
        ];
        foreach (string path in records)
        {
            string record = File.ReadAllText(path);
            record.ShouldContain($"Current subject: `{subject}`");
            record.ShouldContain(verdict);
        }
    }

    /// <summary>
    /// Verifies the machine-readable gate is bound to the current canonical subject, carries the
    /// same receipt state as the packet, and cannot cite the explicitly superseded trace matrix.
    /// </summary>
    [Fact]
    public void GateDecisionBindsCurrentSubjectWithoutSupersededTraceLink()
    {
        string root = FindRepositoryRoot();
        JsonObject closure = LoadJson(Path.Combine(root, EvidenceRelativePath, "closure.json"));
        string subject = closure["subject"]!["sha256"]!.GetValue<string>();
        int receipts = closure["acceptances"]!["receipts"]!.AsArray().Count;
        JsonObject gate = LoadJson(Path.Combine(root, "_bmad-output", "test-artifacts", "gate-decision.json"));

        gate["source_sha"]!.GetValue<string>().ShouldBe(subject);
        gate["links"]!["trace_report_path"]!.GetValue<string>().ShouldBeEmpty();
        gate["rationale"]!.GetValue<string>().ShouldContain(subject);
        gate["collection_status"]!.GetValue<string>().ShouldBe(
            receipts == 3 ? "COLLECTED" : "NOT_COLLECTED");
        gate["gate_status"]!.GetValue<string>().ShouldBe(receipts == 3 ? "PASS" : "BLOCKED");
        gate["overall_status"]!.GetValue<string>().ShouldBe(receipts == 3 ? "MET" : "NOT_MET");
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
    /// Verifies the verifier rejects an oversized retained-file declaration before attempting to
    /// allocate or read that declared amount. The actual package remains the small valid packet
    /// file, making the positive control independent of large test-data allocation.
    /// </summary>
    [Fact]
    public void OversizedRetainedFileBindingFailsBeforeRead()
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            string closurePath = Path.Combine(temporary, "closure.json");
            JsonObject closure = LoadJson(closurePath);
            closure["packages"]!["items"]![0]!["nuget_org"]!["size"] = (16 * 1024 * 1024) + 1;
            WriteCanonical(closurePath, closure);

            ShouldFailClosed(
                RunValidator(root, temporary),
                "retained file exceeds the support-safe size limit");
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
    /// Verifies UTF-16 bytes cannot hide a DTD/entity declaration from the pre-parse guard.
    /// Only strict UTF-8 XML is admitted to ElementTree.
    /// </summary>
    [Fact]
    public void Utf16PackageNuspecCannotBypassEntityRejection()
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            string closurePath = Path.Combine(temporary, "closure.json");
            JsonObject closure = LoadJson(closurePath);
            JsonObject nuget = closure["packages"]!["items"]![0]!["nuget_org"]!.AsObject();
            string packagePath = Path.Combine(temporary, nuget["file"]!.GetValue<string>());
            string xml =
                "<?xml version=\"1.0\" encoding=\"utf-16\"?>" +
                "<!DOCTYPE package [<!ENTITY packageId \"Hexalith.EventStore.Contracts\">]>" +
                "<package><metadata><id>&packageId;</id><version>3.96.2</version>" +
                "<repository type=\"git\" url=\"https://github.com/Hexalith/Hexalith.EventStore\" " +
                $"commit=\"{SourceSha}\" /></metadata></package>";
            ReplaceNuspecBytes(
                packagePath,
                [.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes(xml)]);
            UpdateFileBinding(nuget, packagePath, updateDigest: false);
            WriteCanonical(closurePath, closure);

            ShouldFailClosed(
                RunValidator(root, temporary),
                "package nuspec is not strict UTF-8 XML");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies explanatory DTD text inside CDATA is not confused with an executable declaration.
    /// </summary>
    [Fact]
    public void PackageNuspecMayMentionDoctypeInsideCdataDescription()
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
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                "<package><metadata><id>Hexalith.EventStore.Contracts</id><version>3.96.2</version>" +
                "<description><![CDATA[The literal <!DOCTYPE text is documentation, not markup.]]></description>" +
                "<repository type=\"git\" url=\"https://github.com/Hexalith/Hexalith.EventStore\" " +
                $"commit=\"{SourceSha}\" /></metadata></package>");
            UpdateFileBinding(nuget, packagePath, updateDigest: false);
            WriteCanonical(closurePath, closure);
            RebindInventoryAndSubject(temporary);
            AttachThreeAcceptedReceipts(temporary);

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
    /// Verifies a highly compressible but support-safe nuspec still reaches identity parsing and
    /// can close parity after all downstream bindings are re-minted.
    /// </summary>
    [Fact]
    public void SupportSafeCompressedNuspecStillValidates()
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            string closurePath = Path.Combine(temporary, "closure.json");
            JsonObject closure = LoadJson(closurePath);
            JsonObject item = closure["packages"]!["items"]![0]!.AsObject();
            JsonObject nuget = item["nuget_org"]!.AsObject();
            string packagePath = Path.Combine(temporary, nuget["file"]!.GetValue<string>());
            string packageId = item["id"]!.GetValue<string>();
            ReplaceNuspec(
                packagePath,
                "<package><metadata><id>" + packageId + "</id><version>3.96.2</version>" +
                "<description>" + new string('x', 900 * 1024) + "</description>" +
                "<repository type=\"git\" url=\"https://github.com/Hexalith/Hexalith.EventStore\" " +
                $"commit=\"{SourceSha}\" /></metadata></package>");
            UpdateFileBinding(nuget, packagePath, updateDigest: false);
            WriteCanonical(closurePath, closure);
            RebindInventoryAndSubject(temporary);
            AttachThreeAcceptedReceipts(temporary);

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
    /// Verifies a small compressed archive cannot expand an oversized nuspec into memory before
    /// the trusted handler checks its ZipInfo uncompressed size.
    /// </summary>
    [Fact]
    public void OversizedCompressedNuspecFailsBeforeExpansion()
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            string closurePath = Path.Combine(temporary, "closure.json");
            JsonObject closure = LoadJson(closurePath);
            JsonObject item = closure["packages"]!["items"]![0]!.AsObject();
            JsonObject nuget = item["nuget_org"]!.AsObject();
            string packagePath = Path.Combine(temporary, nuget["file"]!.GetValue<string>());
            string packageId = item["id"]!.GetValue<string>();
            ReplaceNuspec(
                packagePath,
                "<package><metadata><id>" + packageId + "</id><version>3.96.2</version>" +
                "<description>" + new string('x', (1024 * 1024) + 1) + "</description>" +
                "<repository type=\"git\" url=\"https://github.com/Hexalith/Hexalith.EventStore\" " +
                $"commit=\"{SourceSha}\" /></metadata></package>");
            UpdateFileBinding(nuget, packagePath, updateDigest: false);
            WriteCanonical(closurePath, closure);

            ShouldFailClosed(
                RunValidator(root, temporary),
                "package nuspec exceeds the support-safe uncompressed size limit");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies a malformed declaration containing a question mark cannot make a non-UTF-8
    /// encoding token invisible to the declaration gate.
    /// </summary>
    [Fact]
    public void MalformedXmlDeclarationCannotSkipTheEncodingCheck()
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
                "<?xml version=\"1.0\" ? encoding=\"utf-16\"?>" +
                "<package><metadata><id>Hexalith.EventStore.Contracts</id><version>3.96.2</version>" +
                "<repository type=\"git\" url=\"https://github.com/Hexalith/Hexalith.EventStore\" " +
                $"commit=\"{SourceSha}\" /></metadata></package>");
            UpdateFileBinding(nuget, packagePath, updateDigest: false);
            WriteCanonical(closurePath, closure);

            ShouldFailClosed(RunValidator(root, temporary), "package nuspec is not strict UTF-8 XML");
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
    /// Verifies every numeric smoke fact is a JSON integer, not a boolean or equal-valued float.
    /// Python equality alone would otherwise accept values such as <c>false == 0 == 0.0</c>.
    /// </summary>
    /// <param name="scope">Aggregate result or first platform record.</param>
    /// <param name="field">Numeric field to mutate.</param>
    /// <param name="jsonValue">Equal-valued non-integer JSON representation.</param>
    [Theory]
    [InlineData("aggregate", "timeout_seconds", "180.0")]
    [InlineData("aggregate", "exit_code", "false")]
    [InlineData("platform", "attempts", "1.0")]
    [InlineData("platform", "http_status", "200.0")]
    [InlineData("platform", "redirect_count", "false")]
    [InlineData("platform", "exit_code", "0.0")]
    public void SmokeNumericFactsRequireExactJsonIntegers(
        string scope,
        string field,
        string jsonValue)
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            string closurePath = Path.Combine(temporary, "closure.json");
            JsonObject closure = LoadJson(closurePath);
            JsonObject resultsBinding = closure["production_smokes"]!["results"]!.AsObject();
            string resultsPath = Path.Combine(
                temporary,
                resultsBinding["file"]!.GetValue<string>());
            JsonObject results = LoadJson(resultsPath);
            JsonNode value = JsonNode.Parse(jsonValue).ShouldNotBeNull();
            if (scope == "aggregate")
            {
                results[field] = value;
            }
            else
            {
                JsonObject platform = results["platforms"]![0]!.AsObject();
                platform[field] = value.DeepClone();
                JsonObject logBinding = platform["log"]!.AsObject();
                string logPath = Path.Combine(
                    temporary,
                    logBinding["file"]!.GetValue<string>());
                JsonObject log = LoadJson(logPath);
                log[field] = value;
                WriteCanonical(logPath, log);
                UpdateFileBinding(logBinding, logPath, updateDigest: false);
            }

            WriteCanonical(resultsPath, results);
            UpdateFileBinding(resultsBinding, resultsPath, updateDigest: false);
            WriteCanonical(closurePath, closure);

            ShouldFailClosed(
                RunValidator(root, temporary),
                scope == "aggregate"
                    ? "bounded Production smoke outcome is invalid"
                    : "Production smoke platform outcome is invalid");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies a platform result exceeding the declared 180-second capture budget fails closed,
    /// even while it remains inside the separate 360-second two-platform aggregate window.
    /// </summary>
    [Fact]
    public void ProductionSmokePlatformDurationMustFitItsDeclaredBudget()
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            string closurePath = Path.Combine(temporary, "closure.json");
            JsonObject closure = LoadJson(closurePath);
            JsonObject resultsBinding = closure["production_smokes"]!["results"]!.AsObject();
            string resultsPath = Path.Combine(temporary, resultsBinding["file"]!.GetValue<string>());
            JsonObject results = LoadJson(resultsPath);
            JsonObject platform = results["platforms"]![0]!.AsObject();
            DateTimeOffset start = DateTimeOffset.Parse(
                platform["started_at"]!.GetValue<string>(),
                CultureInfo.InvariantCulture);
            platform["ended_at"] = start.AddSeconds(181).ToString(
                "yyyy-MM-dd'T'HH:mm:ss'Z'",
                CultureInfo.InvariantCulture);
            results["ended_at"] = start.AddSeconds(300).ToString(
                "yyyy-MM-dd'T'HH:mm:ss'Z'",
                CultureInfo.InvariantCulture);
            WriteCanonical(resultsPath, results);
            UpdateFileBinding(resultsBinding, resultsPath, updateDigest: false);
            WriteCanonical(closurePath, closure);

            ShouldFailClosed(
                RunValidator(root, temporary),
                "Production smoke platform outcome is invalid");
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
            string subjectBefore = ComputeSha256(Path.Combine(temporary, "subject.json"));
            RewriteReceiptSource(temporary, 0, source => source["author_association"] = "NONE");

            // An individual retained source is deliberately outside the subject to avoid a hash
            // cycle. Its replacement invalidates the bound receipt and therefore the 3/3 verdict;
            // only a receipt-source policy change re-mints the subject itself.
            ComputeSha256(Path.Combine(temporary, "subject.json")).ShouldBe(subjectBefore);

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
    /// Verifies the authenticated GitHub envelope is closed-schema, so unreviewed fields cannot be
    /// retained outside the subject while leaving all identity checks and receipts valid.
    /// </summary>
    [Fact]
    public void ReceiptGitHubSourceWithUnknownFieldFailsClosed()
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            RewriteReceiptSource(temporary, 0, source => source["unreviewed"] = "content");

            ShouldFailClosed(
                RunValidator(root, temporary),
                "GitHub acceptance source schema is invalid");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies both parts of the rostered GitHub account tuple are load-bearing on owner receipts.
    /// </summary>
    /// <param name="field">GitHub user field to mutate.</param>
    [Theory]
    [InlineData("login")]
    [InlineData("id")]
    public void ReceiptGitHubSourceMustMatchTheRosteredOwnerAccount(string field)
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            RewriteReceiptSource(temporary, 1, source =>
            {
                source["user"]![field] = field == "login" ? "mallory" : 1;
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
    /// Verifies a receipt anchored anywhere except the dedicated Story 3.15 issue fails closed.
    /// A positive allowlist rejects past and future sibling threads without maintaining a denylist.
    /// </summary>
    /// <param name="issue">Foreign-lineage issue number to anchor the receipt source on.</param>
    [Theory]
    [InlineData(324)]
    [InlineData(346)]
    [InlineData(351)]
    [InlineData(900001)]
    public void ReceiptSourceAnchoredOnForeignLineageIssueFailsClosed(int issue)
    {
        string root = FindRepositoryRoot();
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
                            $"https://github.com/Hexalith/Hexalith.EventStore/issues/{Story315AcceptanceIssue}#issuecomment-{commentId + 1}");
                        break;
                    case "issue-url-thread":
                        source["issue_url"] = FormattableString.Invariant(
                            $"https://api.github.com/repos/Hexalith/Hexalith.EventStore/issues/{Story315AcceptanceIssue + 1}");
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
    /// Verifies every authenticated registry-source clause is load-bearing, including the account
    /// tuple, author association, and both dedicated-issue URLs.
    /// </summary>
    /// <param name="mutation">Registry authority source mutation.</param>
    [Theory]
    [InlineData("author-association")]
    [InlineData("user-login")]
    [InlineData("user-id")]
    [InlineData("html-url")]
    [InlineData("issue-url")]
    public void RegistryAuthoritySourceMustBeAuthenticatedToTheDedicatedIssue(string mutation)
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            MutateRegistrySource(temporary, source =>
            {
                switch (mutation)
                {
                    case "author-association":
                        source["author_association"] = "CONTRIBUTOR";
                        break;
                    case "user-login":
                        source["user"]!["login"] = "mallory";
                        break;
                    case "user-id":
                        source["user"]!["id"] = 1;
                        break;
                    case "html-url":
                        source["html_url"] =
                            "https://github.com/Hexalith/Hexalith.EventStore/issues/351#issuecomment-5407975180";
                        break;
                    case "issue-url":
                        source["issue_url"] =
                            "https://api.github.com/repos/Hexalith/Hexalith.EventStore/issues/351";
                        break;
                }
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
                    "migration, or Story 3.15 done status.";
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
        string packet = CreateIncompletePacket(root);
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

            Directory.Delete(packet, recursive: true);
        }
    }

    /// <summary>
    /// Verifies neither the dispatcher's own early imports nor imports performed by the four
    /// verified source modules can resolve a same-named repository file before the standard
    /// library. Each shadow raises if imported, while the expected incomplete-receipt failure
    /// proves real handler validation still ran.
    /// </summary>
    /// <param name="module">Repository-local standard-library shadow to plant.</param>
    /// <param name="marker">Unique output marker emitted only if the shadow executes.</param>
    [Theory]
    [InlineData("json.py", "repository-json-shadow-executed")]
    [InlineData("zipfile.py", "repository-zipfile-shadow-executed")]
    public void RepositoryLocalStandardLibraryShadowCannotExecute(string module, string marker)
    {
        string root = FindRepositoryRoot();
        string packet = CreateIncompletePacket(root);
        string temporary = Path.Combine(Path.GetTempPath(), $"eventstore-story315-shadow-{Guid.NewGuid():N}");
        try
        {
            CopyDirectory(Path.Combine(root, "tools"), Path.Combine(temporary, "tools"));
            File.WriteAllText(
                Path.Combine(temporary, "tools", module),
                $"print('{marker}')\nraise RuntimeError('shadow loaded')\n");

            (int exitCode, string output, string error) = RunProcess(
                temporary,
                "python3",
                "tools/validate-corrected-deployed-runtime-parity.py",
                Path.Combine(packet, "closure.json"),
                "--packet-root",
                packet);

            exitCode.ShouldBe(1, error);
            error.ShouldContain("exactly three packet-bound receipts are required");
            output.ShouldNotContain(marker);
            output.ShouldNotContain("pass:");
        }
        finally
        {
            if (Directory.Exists(temporary))
            {
                Directory.Delete(temporary, recursive: true);
            }

            Directory.Delete(packet, recursive: true);
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
        string packet = CreateIncompletePacket(root);
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

            var cachePath = RunProcess(
                temporary,
                "python3",
                "-c",
                "import importlib.util,sys;print(importlib.util.cache_from_source(sys.argv[1]))",
                handlerPath);
            cachePath.ExitCode.ShouldBe(0, cachePath.Error);
            File.Exists(cachePath.Output.Trim()).ShouldBeTrue();
            var ordinaryImport = RunProcess(
                temporary,
                "python3",
                "-c",
                "import sys;sys.path.insert(0,sys.argv[1]);import deployed_runtime_parity_handlers.v1",
                Path.Combine(temporary, "tools"));
            ordinaryImport.ExitCode.ShouldBe(0, ordinaryImport.Error);
            ordinaryImport.Output.ShouldContain("untrusted-bytecode-executed");

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

            Directory.Delete(packet, recursive: true);
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
        => ReplaceNuspecBytes(zipPath, Encoding.UTF8.GetBytes(content));

    private static void ReplaceNuspecBytes(string zipPath, byte[] content)
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
        stream.Write(content);
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

    private static string CreateIncompletePacket(string root)
    {
        string temporary = CopyPacket(root);
        string closurePath = Path.Combine(temporary, "closure.json");
        JsonObject closure = LoadJson(closurePath);
        closure["acceptances"]!["receipts"] = new JsonArray();
        WriteCanonical(closurePath, closure);
        string acceptancesRoot = Path.Combine(temporary, "acceptances");
        if (Directory.Exists(acceptancesRoot))
        {
            Directory.Delete(acceptancesRoot, recursive: true);
        }

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
        string acceptancesRoot = Path.Combine(temporary, "acceptances");
        if (Directory.Exists(acceptancesRoot))
        {
            Directory.Delete(acceptancesRoot, recursive: true);
        }

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
                        $"https://github.com/Hexalith/Hexalith.EventStore/issues/{Story315AcceptanceIssue}#issuecomment-{commentId}"),
                    ["id"] = commentId,
                    ["issue_url"] = FormattableString.Invariant(
                        $"https://api.github.com/repos/Hexalith/Hexalith.EventStore/issues/{Story315AcceptanceIssue}"),
                    ["minimized"] = false,
                    ["node_id"] = "fixture-comment-node-" + commentId.ToString(CultureInfo.InvariantCulture),
                    ["performed_via_github_app"] = null,
                    ["pin"] = null,
                    ["reactions"] = new JsonObject(),
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
        => MutateRegistrySource(
            packet,
            source => source["body"] = transform(source["body"]!.GetValue<string>()));

    /// <summary>
    /// Rewrites the retained roster source and corrects the source, registry, and closure bindings.
    /// </summary>
    /// <param name="packet">Packet root to mutate.</param>
    /// <param name="transform">Source-document mutation to apply.</param>
    private static void MutateRegistrySource(string packet, Action<JsonObject> transform)
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
        transform(source);
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
        subject["evidence"]!["package_domains_sha256"] =
            ComputeCanonicalSha256(closure["packages"].ShouldNotBeNull());
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

    private static string ComputeCanonicalSha256(JsonNode value) =>
        Convert.ToHexString(SHA256.HashData(CanonicalBytes(value))).ToLowerInvariant();

    private static string ComputeTreeBindingSha256(string root)
    {
        string bindings = string.Concat(Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => new
            {
                Path = path,
                Relative = Path.GetRelativePath(root, path).Replace('\\', '/'),
            })
            .OrderBy(item => item.Relative, StringComparer.Ordinal)
            .Select(item => $"{ComputeSha256(item.Path)}  {item.Relative}\n"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(bindings))).ToLowerInvariant();
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(source, path).Split(Path.DirectorySeparatorChar)
                .Contains("__pycache__", StringComparer.Ordinal)))
        {
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        }

        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)
            .Where(path => !path.EndsWith(".pyc", StringComparison.OrdinalIgnoreCase)
                && !Path.GetRelativePath(source, path).Split(Path.DirectorySeparatorChar)
                    .Contains("__pycache__", StringComparer.Ordinal)))
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
