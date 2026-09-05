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
    /// First subject digest whose three receipts were superseded when the transitive predecessor
    /// handler became part of the subject.
    /// </summary>
    private const string SupersededSubjectSha256 =
        "bb58d691ee404cc958433e996204e3382721de3931ac64cf8f7a61de97c30709";

    /// <summary>Subject whose receipts are superseded by the trusted-verifier hardening.</summary>
    private const string TrustedVerifierSupersededSubjectSha256 =
        "dab64f5fbbf55783630ad75451d35d517d829e194fb618dc8b0526d39761d38d";

    /// <summary>
    /// Subject whose three receipts were superseded by the loop-6 batch: the producers became
    /// decision inputs, the acceptance-source envelopes became closed-schema, and a fourth
    /// tooling-composed-receipt limitation was bound into the subject.
    /// </summary>
    private const string BatchSupersededSubjectSha256 =
        "a8cc777ed04f1f0a7f7dffb7f24f7359f786e9114afe04fc69b1aa90cb8fdf7f";

    /// <summary>
    /// Subject the checked-in packet currently binds. It is drift-bound here and in docs/ci.md so a
    /// record that keeps naming a superseded subject cannot stay green.
    /// </summary>
    private const string CurrentSubjectSha256 =
        "86c59c79cf783d2a11ea967fdd4cca8281d01c626b80f9e6a6dc862fbb596274";

    /// <summary>Number of files in the frozen Story 3.14 packet.</summary>
    private const int FrozenStory314PacketFileCount = 66;

    /// <summary>
    /// SHA-256 of the frozen Story 3.14 packet's own <c>&lt;digest&gt;  &lt;relative path&gt;</c>
    /// manifest, in byte order. Pinning the manifest closes the whole tree rather than one file.
    /// </summary>
    private const string FrozenStory314PacketManifestSha256 =
        "2d13d833ad0cc3df54c11ff1e53bbf322928f777375a0a36fdbef843bf128f18";

    /// <summary>
    /// Pinned QEMU user-mode emulator image the <c>linux/arm64</c> Production smoke depends on. It
    /// is host state rather than a packet input, so it is documented as a prerequisite in both the
    /// capture script and docs/ci.md instead of being bound into the subject.
    /// </summary>
    private const string BinfmtEmulatorSha256 =
        "400a4873b838d1b89194d982c45e5fb3cda4593fbfd7e08a02e76b03b21166f0";

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
        "Every acceptance receipt is composed by repository tooling and posted with the rostered " +
            "role holder's credential, not typed by hand.",
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
        ("README.md", "ee4acf117309481a8f59ff21f1b862d69f5444bd0f91ca4ff146ce0922e1f488"),
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
        (BatchSupersededSubjectSha256 + "/eventstore-owner.json",
            "846b249857789b97afe6e8204a6136b075055b85d56e9d815bede2d152420370"),
        (BatchSupersededSubjectSha256 + "/release-owner.json",
            "8f98e5b8b3541d9959c0a64ac741fd03ab59eb9a23caa4db0eca39333df6983e"),
        (BatchSupersededSubjectSha256 + "/test-architect.json",
            "203e5f2a0749d6bd6da534c1a48d507edc1f71dadde90f2c86b36b6a1a50ded2"),
        (BatchSupersededSubjectSha256 + "/sources/eventstore-owner.json",
            "6e97eb2c564ae78a3ab875d7458ee3b7d53dc5cc03b8bce8f2a446351dc3edf0"),
        (BatchSupersededSubjectSha256 + "/sources/release-owner.json",
            "181f2001d93ee982b19758335cb2ba37d7bfd5b0f9e99c77990a40f697d6bb25"),
        (BatchSupersededSubjectSha256 + "/sources/test-architect.json",
            "c20fee033cfcb055ed2387d0c40109a7da33a0b97e73db98aef71dd975b9e40a"),
    ];

    /// <summary>
    /// The real dedicated Story 3.15 acceptance issue. Fixtures use it deliberately: a receipt has
    /// to resolve to this one thread, so a fixture anchored anywhere else would fail for the wrong
    /// reason and prove nothing about the field under test.
    /// </summary>
    private const int AcceptanceIssue = 352;

    /// <summary>
    /// Verifies the checked-in packet closes positive parity once three roster-bound receipts bind
    /// the current subject: verifier exit 0, selected index only the bound digest, and every
    /// non-authority flag remains false. <c>deployed_runtime_parity</c> and
    /// <c>selected_deployed_identity</c> are granted only at three of three; a synthesized
    /// zero-receipt copy still fails closed in
    /// <see cref="AssemblerReproducesTheSubjectAndPropagatesTheVerifierVerdict"/>.
    /// </summary>
    [Fact]
    public void CheckedInPacketClosesPositiveParityWhenThreeReceiptsBindTheCurrentSubject()
    {
        string root = FindRepositoryRoot();
        string packet = Path.Combine(root, EvidenceRelativePath);

        (int exitCode, string output, string error) = RunValidator(root, packet);
        exitCode.ShouldBe(0, error);
        output.ShouldContain("pass:");
        output.ShouldContain("subject=sha256:" + CurrentSubjectSha256);
        output.ShouldContain("selected=" + IndexDigest);

        JsonObject closure = LoadJson(Path.Combine(packet, "closure.json"));
        closure["subject"]!["sha256"]!.GetValue<string>().ShouldBe(CurrentSubjectSha256);
        closure["acceptances"]!["receipts"]!.AsArray().Count.ShouldBe(RequiredRoles.Length);
        closure["acceptances"]!["receipts"]!.AsArray()
            .Select(item => item!["role"]!.GetValue<string>())
            .ShouldBe(RequiredRoles);
        closure["deployment_authorized"]!.GetValue<bool>().ShouldBeFalse();
        closure["consumer_removal_authorized"]!.GetValue<bool>().ShouldBeFalse();
        closure["publication_authorized"]!.GetValue<bool>().ShouldBeFalse();
        closure["grants_mutation_authority"]!.GetValue<bool>().ShouldBeFalse();

        // The claim fields are present and, with three receipts, granted by the exit 0 above.
        closure["deployed_runtime_parity"]!.GetValue<string>().ShouldBe("available");
        closure["selected_deployed_identity"]!.GetValue<string>().ShouldBe(IndexDigest);

        closure["acceptances"]!["directory"]!.GetValue<string>()
            .ShouldBe("acceptances/" + CurrentSubjectSha256);
        Directory.Exists(Path.Combine(packet, "acceptances", CurrentSubjectSha256)).ShouldBeTrue();
        foreach (string role in RequiredRoles)
        {
            File.Exists(Path.Combine(packet, "acceptances", CurrentSubjectSha256, role + ".json"))
                .ShouldBeTrue(role);
            File.Exists(Path.Combine(packet, "acceptances", CurrentSubjectSha256, "sources", role + ".json"))
                .ShouldBeTrue(role);
        }
    }

    /// <summary>
    /// Verifies every superseded receipt and source is retained byte-for-byte outside the packet.
    /// Directory presence alone cannot notice a rewritten, truncated, or re-signed receipt, so each
    /// retained file's SHA-256 is pinned here.
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
    /// Verifies equal-valued booleans and floats cannot satisfy the handler's workflow and package
    /// integer contracts after the outer dispatch route has been selected.
    /// </summary>
    /// <param name="field">Integer field to mutate.</param>
    /// <param name="jsonValue">Equal-valued non-integer JSON representation.</param>
    /// <param name="expectedError">Focused fail-closed reason.</param>
    [Theory]
    [InlineData("workflow-run-attempt", "true", "workflow run attempt is invalid")]
    [InlineData("workflow-run-id", "32361958618.0", "workflow run identifier is invalid")]
    [InlineData("package-count", "14.0", "package manifest identity is invalid")]
    public void WorkflowAndPackageCountsRequireExactJsonIntegers(
        string field,
        string jsonValue,
        string expectedError)
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            string closurePath = Path.Combine(temporary, "closure.json");
            JsonObject closure = LoadJson(closurePath);
            JsonNode value = JsonNode.Parse(jsonValue).ShouldNotBeNull();
            switch (field)
            {
                case "workflow-run-attempt":
                    closure["lineage"]!["workflow"]!["run_attempt"] = value;
                    break;
                case "workflow-run-id":
                    closure["lineage"]!["workflow"]!["run_id"] = value;
                    break;
                case "package-count":
                    closure["packages"]!["count"] = value;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(field), field, "Unknown integer field.");
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
    /// Verifies every acceptance field is mandatory and receipt mutations fail closed.
    /// </summary>
    /// <param name="field">Receipt field to remove.</param>
    /// <param name="role">Rostered role whose receipt loses the field.</param>
    [Theory]
    [InlineData("accepted_at", "eventstore-owner")]
    [InlineData("accepted_limitations", "release-owner")]
    [InlineData("accepted_scope", "test-architect")]
    [InlineData("decision", "release-owner")]
    [InlineData("durable_source", "test-architect")]
    [InlineData("reviewer_identity", "release-owner")]
    [InlineData("role", "eventstore-owner")]
    [InlineData("schema", "test-architect")]
    [InlineData("subject_sha256", "release-owner")]
    public void EveryReceiptFieldIsRequired(string field, string role)
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            // Positive control: without it every case could pass because the fixture never
            // validated in the first place.
            RunValidator(root, temporary).ExitCode.ShouldBe(0);

            // Mutating index 0 every time left release-owner -- a structurally identical but
            // separately validated receipt -- and the distinct test-architect branch never
            // exercised by any negative case. Each case names the role whose receipt it removes.
            RewriteReceipt(temporary, ReceiptIndex(temporary, role), receipt => receipt.Remove(field));

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
    /// <param name="role">Rostered role whose receipt is mutated, where the mutation targets one.</param>
    [Theory]
    [InlineData("wrong-role", "acceptance receipt does not bind", "eventstore-owner")]
    [InlineData("wrong-role", "acceptance receipt does not bind", "release-owner")]
    [InlineData("wrong-role", "acceptance receipt does not bind", "test-architect")]
    [InlineData("subject-mismatch", "acceptance receipt does not bind", "release-owner")]
    [InlineData("stale", "acceptance predates the subject", "release-owner")]
    [InlineData("unverifiable", "retained file binding mismatch", "test-architect")]
    [InlineData("duplicate", "acceptance roles are missing, duplicated, or out of order", "eventstore-owner")]
    [InlineData("missing", "exactly three packet-bound receipts are required", "eventstore-owner")]
    public void InvalidAcceptanceNeverAuthorizesParity(string mutation, string expectedError, string role)
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            // Positive control, as above.
            RunValidator(root, temporary).ExitCode.ShouldBe(0);

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
                RewriteReceipt(temporary, ReceiptIndex(temporary, role), receipt =>
                {
                    switch (mutation)
                    {
                        case "wrong-role":
                            receipt["reviewer_identity"] =
                                role == "test-architect" ? "bmad:mallory" : "github:mallory";
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
    /// Verifies the roster authority and both owner-role receipts are three distinct GitHub
    /// comments. Consistently rebinding one snapshot to an already-used comment ID must not let a
    /// single comment stand in for two independent role records.
    /// </summary>
    /// <param name="commentId">Already-used roster or EventStore-owner comment identifier.</param>
    [Theory]
    [InlineData(5407975180L)]
    [InlineData(9000001L)]
    public void RosterAndOwnerAcceptanceCommentsRequireDistinctIdentities(long commentId)
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            int releaseOwner = ReceiptIndex(temporary, "release-owner");
            RewriteReceiptSource(temporary, releaseOwner, source =>
            {
                source["id"] = commentId;
                source["html_url"] = FormattableString.Invariant(
                    $"https://github.com/Hexalith/Hexalith.EventStore/issues/{AcceptanceIssue}#issuecomment-{commentId}");
                source["node_id"] = FormattableString.Invariant($"IC_kwDO{commentId}");
                source["url"] = FormattableString.Invariant(
                    $"https://api.github.com/repos/Hexalith/Hexalith.EventStore/issues/comments/{commentId}");
                source["reactions"]!["url"] = FormattableString.Invariant(
                    $"https://api.github.com/repos/Hexalith/Hexalith.EventStore/issues/comments/{commentId}/reactions");
            });

            ShouldFailClosed(
                RunValidator(root, temporary),
                "GitHub roster and acceptance comment identities must be distinct");
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
        string packetRoot = Path.Combine(
            root,
            "_bmad-output",
            "implementation-artifacts",
            "evidence",
            "story-3-14",
            SourceSha);
        string predecessor = Path.Combine(packetRoot, "release-identity.json");

        ComputeSha256(predecessor).ShouldBe(PredecessorSha256);

        // Hashing the identity file alone proves only that one of 66 retained files is unchanged,
        // while several cases in this suite reach into the rest of that packet. Close the whole
        // tree: the manifest lists every file's digest and relative path in byte order, so an added,
        // removed, renamed or rewritten file all change the pinned value.
        string[] files = Directory.EnumerateFiles(packetRoot, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(packetRoot, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();
        files.Length.ShouldBe(FrozenStory314PacketFileCount);
        string manifest = string.Concat(files.Select(relative => FormattableString.Invariant(
            $"{ComputeSha256(Path.Combine(packetRoot, relative.Replace('/', Path.DirectorySeparatorChar)))}  {relative}\n")));
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(manifest)))
            .ToLowerInvariant()
            .ShouldBe(FrozenStory314PacketManifestSha256);

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
    /// Verifies every executing module on the trust path is loaded from already verified source
    /// bytes rather than resolved by importlib. The previous form of this test pinned a
    /// post-import <c>__file__</c> comparison that could not fail -- the loader sets
    /// <c>__file__</c> from the same relative path such a check re-derives -- and matched its own
    /// pin table rather than the call sites. What actually holds the property is that each of the
    /// four pinned files is handed to <c>_load_verified_module</c> as pre-read bytes, and that no
    /// ordinary import statement or <c>importlib</c> resolution of those modules survives.
    /// </summary>
    [Fact]
    public void EveryTrustPathModuleExecutesOnlyPreVerifiedSourceBytes()
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

        // Every trust-path file is pinned, and _verify_import_path reads and hashes exactly the
        // pinned set before the first module executes.
        foreach (string relative in expected)
        {
            dispatcher.ShouldContain($"\"{relative}\"");
        }

        // The loader is the only execution route, and it compiles the bytes it was handed.
        dispatcher.ShouldContain("exec(compile(source,");

        // Read the call sites, not the pin table: `"<relative>"` also appears in
        // IMPORT_PATH_FILE_SHA256, so asserting its presence proved nothing about what is loaded.
        // Every call must hand the loader a source drawn from the verified `sources` mapping, and
        // the v1 call site names no literal at all -- it builds its key from the module name.
        MatchCollection calls = Regex.Matches(
            dispatcher,
            @"    \w+ = _load_verified_module\((?<args>.*?)\n    \)",
            RegexOptions.Singleline);
        calls.Count.ShouldBe(4);
        foreach (Match call in calls)
        {
            call.Groups["args"].Value.ShouldContain("sources[");
        }

        // Exactly one definition plus those four calls.
        Regex.Matches(dispatcher, @"_load_verified_module\(").Count.ShouldBe(5);
        dispatcher.ShouldContain("sources[module_name.replace(\".\", \"/\") + \".py\"]");

        // The dead post-import path comparison must not come back: it re-derived its expectation
        // from the same value the loader assigned, so it was true by construction.
        dispatcher.ShouldNotContain("_verify_imported_file");

        // No ordinary import of a trust-path module may remain: that would bypass the pinned bytes.
        foreach (string module in new[] { "deployed_runtime_parity_handlers", "release_evidence_handlers" })
        {
            Regex.IsMatch(dispatcher, $@"^\s*(?:import|from)\s+{Regex.Escape(module)}\b", RegexOptions.Multiline)
                .ShouldBeFalse(module);
        }

        // Comments may still explain why find_spec is avoided; only executable lines are checked.
        Regex.IsMatch(
            dispatcher,
            @"^(?!\s*#).*importlib\.(?:import_module|util\.find_spec)",
            RegexOptions.Multiline).ShouldBeFalse();
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
        // four: the current subject, the selected index, the frozen predecessor identity, and the
        // pinned QEMU emulator image the arm64 Production smoke depends on. The slice stops at the
        // next top-level heading -- running to end-of-file would fold every later section's digests
        // into the assertion -- and the token pattern is boundary-anchored so a longer hex run
        // cannot satisfy it through a 64-character window.
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
            new[]
            {
                subjectSha256,
                IndexDigest["sha256:".Length..],
                PredecessorSha256,
                BinfmtEmulatorSha256,
            }
                .Order(StringComparer.Ordinal)
                .ToArray());

        // The emulator digest is an environmental prerequisite, not a packet input, so nothing else
        // binds it. Keep the operator record and the capture script's documented precondition from
        // drifting apart.
        File.ReadAllText(Path.Combine(root, "tools", "capture-corrected-deployed-runtime-parity-smokes.py"))
            .ShouldContain(BinfmtEmulatorSha256);
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
    /// Verifies every retained smoke value predicate is mutation-proven independently of the
    /// type/window cases. Mutating one field at a time, regenerating the matching platform log, and
    /// re-attaching receipts must reach the outcome guard rather than an earlier binding failure.
    /// </summary>
    /// <param name="field">Platform field whose value predicate is breached.</param>
    [Theory]
    [InlineData("observed_runtime_platform")]
    [InlineData("http_status")]
    [InlineData("redirect_count")]
    [InlineData("cleanup")]
    [InlineData("readiness_result")]
    [InlineData("outcome")]
    [InlineData("child_digest")]
    public void SmokeValuePredicatesFailClosedWhenMutated(string field)
    {
        string root = FindRepositoryRoot();
        string temporary = CopyPacket(root);
        try
        {
            MutateSmokeResults(temporary, results =>
            {
                JsonObject platform = results["platforms"]!.AsArray()[0]!.AsObject();
                switch (field)
                {
                    case "observed_runtime_platform":
                        platform["observed_runtime_platform"] = "linux/arm64";
                        break;
                    case "http_status":
                        platform["http_status"] = 201;
                        break;
                    case "redirect_count":
                        platform["redirect_count"] = 1;
                        break;
                    case "cleanup":
                        platform["cleanup"] = "failure";
                        break;
                    case "readiness_result":
                        platform["readiness_result"] = "failure";
                        break;
                    case "outcome":
                        platform["outcome"] = "failure";
                        break;
                    case "child_digest":
                        platform["child_digest"] =
                            "sha256:ede853318267146a9888574f79e16ea1e51c1f363a35910fe883b5a9d7256f44";
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(field), field, "Unhandled smoke field.");
                }
            });
            AttachThreeAcceptedReceipts(temporary);

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
    /// Verifies the aggregate smoke summary itself uses the selected canonical UTF-8 encoding, as
    /// already required of both platform logs. Rebinding whitespace must not make two byte shapes
    /// represent the same authorizing evidence.
    /// </summary>
    [Fact]
    public void SmokeResultsRequireCanonicalUtf8Bytes()
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
            File.AppendAllText(resultsPath, " ");
            UpdateFileBinding(resultsBinding, resultsPath, updateDigest: false);
            WriteCanonical(closurePath, closure);

            ShouldFailClosed(
                RunValidator(root, temporary),
                "Production smoke results are not canonical UTF-8 JSON");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies Production-smoke windows cannot be retained from the future, even when their own
    /// durations, logs, inventory, subject and fresh receipts all agree.
    /// </summary>
    [Fact]
    public void ProductionSmokeWindowsInTheFutureFailClosed()
    {
        string root = FindRepositoryRoot();
        string temporary = CopyPacket(root);
        try
        {
            MutateSmokeResults(temporary, results =>
            {
                DateTimeOffset future = DateTimeOffset.UtcNow.AddYears(10);
                results["started_at"] = Utc(future);
                results["ended_at"] = Utc(future.AddMinutes(4));
                JsonArray platforms = results["platforms"]!.AsArray();
                platforms[0]!["started_at"] = Utc(future);
                platforms[0]!["ended_at"] = Utc(future.AddMinutes(2));
                platforms[1]!["started_at"] = Utc(future.AddMinutes(2));
                platforms[1]!["ended_at"] = Utc(future.AddMinutes(4));
            });
            AttachThreeAcceptedReceipts(temporary);

            ShouldFailClosed(
                RunValidator(root, temporary),
                "Production smoke window lies in the future");
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
    /// Verifies a receipt anchored anywhere except the dedicated Story 3.15 issue fails closed.
    /// A positive allowlist rejects past and future sibling threads without maintaining a denylist.
    /// </summary>
    /// <param name="issue">
    /// Foreign issue number to anchor the receipt source on: the two cross-lineage threads the
    /// superseded receipts were anchored on, the Story 3.13 thread, and an arbitrary fresh issue
    /// standing for every future sibling.
    /// </param>
    [Theory]
    [InlineData(324)]
    [InlineData(346)]
    [InlineData(351)]
    [InlineData(900001)]
    public void ReceiptSourceAnchoredOnForeignLineageIssueFailsClosed(int issue)
    {
        string root = FindRepositoryRoot();

        // Non-vacuity: the rejected thread must not be the one allowlisted thread, or the case
        // would be asserting nothing about lineage.
        issue.ShouldNotBe(AcceptanceIssue);
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
                            $"https://github.com/Hexalith/Hexalith.EventStore/issues/{AcceptanceIssue}#issuecomment-{commentId + 1}");
                        break;
                    case "issue-url-thread":
                        source["issue_url"] = FormattableString.Invariant(
                            $"https://api.github.com/repos/Hexalith/Hexalith.EventStore/issues/{AcceptanceIssue + 1}");
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
                "owner-role registry authority source role mapping is invalid");
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
                "owner-role registry authority source role mapping is invalid");
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
                "owner-role registry authority source body is invalid");
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
                "owner-role registry authority source body is invalid");
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
    /// Verifies imports performed by the four verified source modules cannot resolve a same-named
    /// repository file before the standard library. The shadow raises if imported, while the
    /// expected incomplete-receipt failure proves real handler validation still ran.
    /// </summary>
    [Fact]
    public void RepositoryLocalStandardLibraryShadowCannotExecute()
    {
        string root = FindRepositoryRoot();
        string packet = CreateIncompletePacket(root);
        string temporary = Path.Combine(Path.GetTempPath(), $"eventstore-story315-shadow-{Guid.NewGuid():N}");
        try
        {
            CopyDirectory(Path.Combine(root, "tools"), Path.Combine(temporary, "tools"));
            File.WriteAllText(
                Path.Combine(temporary, "tools", "zipfile.py"),
                "print('repository-zipfile-shadow-executed')\nraise RuntimeError('shadow loaded')\n");

            (int exitCode, string output, string error) = RunProcess(
                temporary,
                "python3",
                "tools/validate-corrected-deployed-runtime-parity.py",
                Path.Combine(packet, "closure.json"),
                "--packet-root",
                packet);

            exitCode.ShouldBe(1, error);
            error.ShouldContain("exactly three packet-bound receipts are required");
            output.ShouldNotContain("repository-zipfile-shadow-executed");
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
    /// Verifies each verifier replaces its initial process with an isolated, no-site interpreter
    /// before importing shadowable dependencies. A non-repository PYTHONPATH supplies both a
    /// <c>hashlib</c> shadow and a sitecustomize meta-path hook; neither may participate in the
    /// authoritative verifier process.
    /// </summary>
    /// <param name="verifier">Verifier entry point.</param>
    [Theory]
    [InlineData("parity")]
    [InlineData("predecessor")]
    public void VerifiersCrossAHermeticInterpreterBoundaryBeforeShadowableImports(string verifier)
    {
        string root = FindRepositoryRoot();
        string packet = CreateIncompletePacket(root);
        string hostile = Path.Combine(Path.GetTempPath(), $"eventstore-story315-pythonpath-{Guid.NewGuid():N}");
        Directory.CreateDirectory(hostile);
        try
        {
            File.WriteAllText(
                Path.Combine(hostile, "sitecustomize.py"),
                "import sys\n"
                + "class Hook:\n"
                + "    def find_spec(self, fullname, path=None, target=None):\n"
                + "        if fullname == 'hashlib': print('untrusted-import-hook-executed')\n"
                + "        return None\n"
                + "sys.meta_path.insert(0, Hook())\n");
            File.WriteAllText(
                Path.Combine(hostile, "hashlib.py"),
                "print('untrusted-pythonpath-shadow-executed')\n"
                + "raise RuntimeError('hostile hashlib shadow loaded')\n");

            string[] arguments;
            string expectedError;
            if (verifier == "parity")
            {
                arguments =
                [
                    "tools/validate-corrected-deployed-runtime-parity.py",
                    Path.Combine(packet, "closure.json"),
                    "--packet-root",
                    packet,
                ];
                expectedError = "exactly three packet-bound receipts are required";
            }
            else
            {
                string predecessor = Path.Combine(
                    root,
                    "_bmad-output",
                    "implementation-artifacts",
                    "evidence",
                    "story-3-14",
                    SourceSha);
                arguments =
                [
                    "tools/validate-corrective-release-evidence.py",
                    Path.Combine(predecessor, "release-identity.json"),
                    "--manifest",
                    "tools/release-packages.json",
                    "--packet-root",
                    predecessor,
                ];
                expectedError = "pass:";
            }

            (int exitCode, string output, string error) = RunProcessWithEnvironment(
                root,
                "python3",
                new Dictionary<string, string> { ["PYTHONPATH"] = hostile },
                arguments);

            if (verifier == "parity")
            {
                exitCode.ShouldBe(1, error);
                error.ShouldContain(expectedError);
            }
            else
            {
                exitCode.ShouldBe(0, error);
                output.ShouldContain(expectedError);
            }

            output.ShouldNotContain("untrusted-import-hook-executed");
            output.ShouldNotContain("untrusted-pythonpath-shadow-executed");
            error.ShouldNotContain("untrusted-import-hook-executed");
            error.ShouldNotContain("untrusted-pythonpath-shadow-executed");
            error.ShouldNotContain("Traceback");
        }
        finally
        {
            Directory.Delete(hostile, recursive: true);
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

            // Control 1: a cache file really was produced. Without this the test passes when
            // py_compile silently wrote nothing, which proves nothing about the loader.
            string cacheDirectory = Path.Combine(
                temporary,
                "tools",
                "deployed_runtime_parity_handlers",
                "__pycache__");
            Directory.Exists(cacheDirectory).ShouldBeTrue();
            string[] caches = Directory.GetFiles(cacheDirectory, "v1.*.pyc");
            caches.Length.ShouldBeGreaterThan(0);

            // Control 2: the stale cache genuinely wins under an ordinary import, so the marker
            // would execute if the verifier resolved this module through importlib. Only with this
            // control does the negative assertion below mean the source-only loader is doing the
            // work.
            (int controlExit, string controlOutput, string controlError) = RunProcess(
                Path.Combine(temporary, "tools"),
                "python3",
                "-c",
                "import deployed_runtime_parity_handlers.v1");
            controlExit.ShouldBe(0, controlError);
            controlOutput.ShouldContain("untrusted-bytecode-executed");

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
    /// Verifies each authentication clause on the roster comment actually rejects. Deleting the
    /// association clause, the account clause, or either thread equality previously left every
    /// closure case green, because the retained comment satisfies them all and no case constructed
    /// one that does not -- so the CONTRIBUTOR exception removed earlier could silently return.
    /// </summary>
    /// <param name="mutation">Authentication clause to violate.</param>
    [Theory]
    [InlineData("author-association")]
    [InlineData("user-login")]
    [InlineData("user-id")]
    [InlineData("comment-id")]
    [InlineData("html-url-thread")]
    [InlineData("issue-url-thread")]
    [InlineData("comment-url-id")]
    [InlineData("updated-after-created")]
    [InlineData("registry-created-at")]
    [InlineData("other-comment-on-same-issue")]
    [InlineData("github-app")]
    public void RegistryAuthoritySourceAuthenticationClausesAllFailClosed(string mutation)
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            // Positive control: the untouched copy must validate, so a broken harness cannot be
            // mistaken for the clause firing.
            RunValidator(root, temporary).ExitCode.ShouldBe(0);

            MutateRegistrySourceDocument(temporary, source =>
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
                    case "comment-id":
                        source["id"] = 5409999999L;
                        break;
                    case "html-url-thread":
                        source["html_url"] =
                            "https://github.com/Hexalith/Hexalith.EventStore/issues/351#issuecomment-5407975180";
                        break;
                    case "issue-url-thread":
                        source["issue_url"] =
                            "https://api.github.com/repos/Hexalith/Hexalith.EventStore/issues/351";
                        break;
                    case "comment-url-id":
                        source["url"] =
                            "https://api.github.com/repos/Hexalith/Hexalith.EventStore/issues/comments/5407975181";
                        break;
                    case "updated-after-created":
                        source["updated_at"] = "2026-08-25T09:59:45Z";
                        break;
                    case "registry-created-at":
                        // Isolates the clause binding the registry document to the timestamp of the
                        // comment that authenticates it. Nothing else in this theory touches it.
                        source["created_at"] = "2026-08-25T08:59:46Z";
                        source["updated_at"] = "2026-08-25T08:59:46Z";
                        break;
                    case "other-comment-on-same-issue":
                        // Every id-bearing field is rewritten consistently to a different comment
                        // on the allowlisted issue, so only REGISTRY_AUTHORITY_COMMENT_ID can
                        // reject it. The comment-id case alone also breaks the URL anchors, which
                        // let that constant be deleted with the theory still green.
                        source["id"] = 5407975181L;
                        source["html_url"] =
                            "https://github.com/Hexalith/Hexalith.EventStore/issues/352#issuecomment-5407975181";
                        source["url"] =
                            "https://api.github.com/repos/Hexalith/Hexalith.EventStore/issues/comments/5407975181";
                        source["reactions"]!["url"] =
                            "https://api.github.com/repos/Hexalith/Hexalith.EventStore/issues/comments/5407975181/reactions";
                        break;
                    case "github-app":
                        source["performed_via_github_app"] = new JsonObject { ["id"] = 1 };
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
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
    /// Verifies a legitimate nuspec that quotes a literal <c>&lt;!DOCTYPE</c> inside CDATA is
    /// accepted. The earlier fixture used the escaped entity form, which never matched the
    /// whole-document regex this scanner replaced, so it passed identically against the old code
    /// and pinned nothing. A CDATA-quoted literal is red under the old regex and green under the
    /// prolog scanner, which is what makes it a real control.
    /// </summary>
    [Fact]
    public void NuspecQuotingDoctypeInCdataIsAccepted()
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            RunValidator(root, temporary).ExitCode.ShouldBe(0);

            string closurePath = Path.Combine(temporary, "closure.json");
            JsonObject closure = LoadJson(closurePath);
            JsonObject nuget = closure["packages"]!["items"]![0]!["nuget_org"]!.AsObject();
            string packagePath = Path.Combine(temporary, nuget["file"]!.GetValue<string>());
            ReplaceNuspec(
                packagePath,
                "<package><metadata><id>Hexalith.EventStore.Contracts</id><version>3.96.2</version>"
                + "<description><![CDATA[<!DOCTYPE evil>]]></description>"
                + "<repository type=\"git\" url=\"https://github.com/Hexalith/Hexalith.EventStore\" "
                + $"commit=\"{SourceSha}\" /></metadata></package>");
            UpdateFileBinding(nuget, packagePath, updateDigest: false);
            WriteCanonical(closurePath, closure);
            RebindInventoryAndSubject(temporary);
            AttachThreeAcceptedReceipts(temporary);

            (int exitCode, _, string error) = RunValidator(root, temporary);
            exitCode.ShouldBe(0, error);
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies a DTD hidden behind a prolog comment that quotes a tag still fails closed. A regex
    /// looking for the document-element start would be truncated by that comment and never see the
    /// declaration behind it, which is why the prolog is consumed construct by construct.
    /// </summary>
    [Fact]
    public void NuspecDtdBehindAPrologCommentFailsClosed()
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            RunValidator(root, temporary).ExitCode.ShouldBe(0);

            string closurePath = Path.Combine(temporary, "closure.json");
            JsonObject closure = LoadJson(closurePath);
            JsonObject nuget = closure["packages"]!["items"]![0]!["nuget_org"]!.AsObject();
            string packagePath = Path.Combine(temporary, nuget["file"]!.GetValue<string>());
            ReplaceNuspec(
                packagePath,
                "<!-- <package> --><!DOCTYPE package [<!ENTITY x \"y\">]>"
                + "<package><metadata><id>Hexalith.EventStore.Contracts</id><version>3.96.2</version>"
                + "<repository type=\"git\" url=\"https://github.com/Hexalith/Hexalith.EventStore\" "
                + $"commit=\"{SourceSha}\" /></metadata></package>");
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
    /// Verifies a residual byte-order mark cannot skip the prolog scan. <c>utf-8-sig</c> strips
    /// exactly one BOM, so a doubled BOM survived as U+FEFF, the scan returned without inspecting
    /// anything, and expat then consumed the re-emitted BOM and parsed the DTD behind it --
    /// reproduced end to end, with the smuggled entity resolving into the package id. The
    /// single-BOM control proves the doubled case is not simply "any BOM is rejected".
    /// </summary>
    /// <param name="leadingBoms">Number of byte-order marks to prepend.</param>
    /// <param name="expectedError">Expected fail-closed reason.</param>
    [Theory]
    [InlineData(1, "package nuspec contains forbidden DTD or entity declarations")]
    [InlineData(2, "package nuspec is not strict UTF-8 XML")]
    [InlineData(3, "package nuspec is not strict UTF-8 XML")]
    public void ResidualByteOrderMarkCannotSkipThePrologScan(int leadingBoms, string expectedError)
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            RunValidator(root, temporary).ExitCode.ShouldBe(0);

            string closurePath = Path.Combine(temporary, "closure.json");
            JsonObject closure = LoadJson(closurePath);
            JsonObject nuget = closure["packages"]!["items"]![0]!["nuget_org"]!.AsObject();
            string packagePath = Path.Combine(temporary, nuget["file"]!.GetValue<string>());
            byte[] bom = [0xEF, 0xBB, 0xBF];
            List<byte> document = [];
            for (int index = 0; index < leadingBoms; index++)
            {
                document.AddRange(bom);
            }

            document.AddRange(Encoding.UTF8.GetBytes(
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>"
                + "<!DOCTYPE package [<!ENTITY smuggle \"Hexalith.Evil\">]>"
                + "<package><metadata><id>&smuggle;</id><version>3.96.2</version>"
                + "<repository type=\"git\" url=\"https://github.com/Hexalith/Hexalith.EventStore\" "
                + $"commit=\"{SourceSha}\" /></metadata></package>"));
            ReplaceNuspecBytes(packagePath, [.. document]);
            UpdateFileBinding(nuget, packagePath, updateDigest: false);
            WriteCanonical(closurePath, closure);

            ShouldFailClosed(RunValidator(root, temporary), expectedError);
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies both malformed-prolog branches fail closed. Neither was asserted anywhere, so an
    /// unterminated construct -- or a document with no element at all -- could have exited the scan
    /// silently, which is precisely how the byte-order-mark bypass worked.
    /// </summary>
    /// <param name="nuspec">Malformed prolog to substitute.</param>
    [Theory]
    [InlineData("<!-- never closed <package><metadata><id>x</id></metadata></package>")]
    [InlineData("<?xml version=\"1.0\" encoding=\"utf-8\"?>   ")]
    public void MalformedNuspecPrologFailsClosed(string nuspec)
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            RunValidator(root, temporary).ExitCode.ShouldBe(0);

            string closurePath = Path.Combine(temporary, "closure.json");
            JsonObject closure = LoadJson(closurePath);
            JsonObject nuget = closure["packages"]!["items"]![0]!["nuget_org"]!.AsObject();
            string packagePath = Path.Combine(temporary, nuget["file"]!.GetValue<string>());
            ReplaceNuspec(packagePath, nuspec);
            UpdateFileBinding(nuget, packagePath, updateDigest: false);
            WriteCanonical(closurePath, closure);

            ShouldFailClosed(RunValidator(root, temporary), "package nuspec prolog is malformed");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies an XML declaration containing a question mark inside a pseudo-attribute value no
    /// longer causes the encoding check to be silently skipped. The previous pattern matched only a
    /// declaration body free of <c>?</c>, so such a document bypassed the check entirely instead of
    /// failing closed.
    /// </summary>
    [Fact]
    public void NuspecDeclarationWithQuestionMarkFailsClosedInsteadOfSkippingTheEncodingCheck()
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            RunValidator(root, temporary).ExitCode.ShouldBe(0);

            string closurePath = Path.Combine(temporary, "closure.json");
            JsonObject closure = LoadJson(closurePath);
            JsonObject nuget = closure["packages"]!["items"]![0]!["nuget_org"]!.AsObject();
            string packagePath = Path.Combine(temporary, nuget["file"]!.GetValue<string>());
            ReplaceNuspec(
                packagePath,
                "<?xml version=\"1.0\" encoding=\"utf-16?\"?>" +
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
    /// Verifies every surface that restates the current subject digest is drift-bound. Only the two
    /// markdown records were covered, so the sprint tracker and this story's own spec could keep
    /// naming a superseded subject with the whole suite green.
    /// </summary>
    /// <param name="relativePath">Surface that restates the subject digest.</param>
    [Theory]
    [InlineData("_bmad-output/implementation-artifacts/sprint-status.yaml")]
    [InlineData("_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md")]
    public void SubjectRestatingSurfacesNameTheCurrentSubject(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        string root = FindRepositoryRoot();
        JsonObject closure = LoadJson(Path.Combine(root, EvidenceRelativePath, "closure.json"));
        string subjectSha256 = closure["subject"]!["sha256"]!.GetValue<string>();
        string text = File.ReadAllText(
            Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

        text.ShouldContain(subjectSha256);

        // A surface may narrate superseded subjects, but the current one must be the first subject
        // digest a reader meets on it.
        string[] subjects =
        [
            subjectSha256,
            BatchSupersededSubjectSha256,
            TrustedVerifierSupersededSubjectSha256,
            SupersededSubjectSha256,
        ];
        Regex.Matches(text, "(?<![0-9a-fA-F])[0-9a-f]{64}(?![0-9a-fA-F])")
            .Select(match => match.Value)
            .FirstOrDefault(value => subjects.Contains(value, StringComparer.Ordinal))
            .ShouldBe(subjectSha256, relativePath);
    }

    /// <summary>
    /// Verifies the proof packet's tool-digest table matches the digests the closure actually binds.
    /// A producer edit re-mints the subject, but nothing stopped that table from silently drifting.
    /// </summary>
    [Fact]
    public void ProofPacketToolDigestTableMatchesTheBoundDispatch()
    {
        string root = FindRepositoryRoot();
        JsonObject closure = LoadJson(Path.Combine(root, EvidenceRelativePath, "closure.json"));
        string proofPacket = File.ReadAllText(Path.Combine(
            root,
            "_bmad-output",
            "implementation-artifacts",
            "3-15-corrected-deployed-runtime-parity-closure-proof-packet.md"));

        string[] bindings =
        [
            "handler",
            "verifier",
            "predecessor_handler",
            "predecessor_package",
            "capture",
            "assembler",
        ];
        foreach (string name in bindings)
        {
            JsonObject binding = closure["dispatch"]![name]!.AsObject();
            string file = binding["file"]!.GetValue<string>();
            string sha256 = binding["sha256"]!.GetValue<string>();
            proofPacket.ShouldContain($"| `{file}` | `{sha256}` |");
        }

        // Non-vacuity: the table must not carry a stale row beside the current ones.
        Regex.Matches(proofPacket, @"\| `tools/[^`]+` \| `(?<digest>[0-9a-f]{64})` \|")
            .Select(match => match.Groups["digest"].Value)
            .Order(StringComparer.Ordinal)
            .ToArray()
            .ShouldBe(bindings
                .Select(name => closure["dispatch"]![name]!["sha256"]!.GetValue<string>())
                .Order(StringComparer.Ordinal)
                .ToArray());
    }

    /// <summary>
    /// Verifies the verifier's per-platform cleanup allowance equals the capture tool's own cleanup
    /// budget. The allowance is a verifier-side constant rather than a field in the frozen
    /// <c>smoke-results.json</c> -- retained evidence must not be rewritten to satisfy a later
    /// schema -- so nothing else keeps the two files in step.
    /// </summary>
    [Fact]
    public void CleanupAllowanceAgreesBetweenVerifierAndCaptureTool()
    {
        string root = FindRepositoryRoot();
        string handler = File.ReadAllText(
            Path.Combine(root, "tools", "deployed_runtime_parity_handlers", "v1.py"));
        string capture = File.ReadAllText(
            Path.Combine(root, "tools", "capture-corrected-deployed-runtime-parity-smokes.py"));

        Match allowance = Regex.Match(handler, @"^CLEANUP_ALLOWANCE_SECONDS = (\d+)$", RegexOptions.Multiline);
        Match budget = Regex.Match(capture, @"^CLEANUP_TIMEOUT_SECONDS = (\d+)$", RegexOptions.Multiline);
        Match verifierOverhead = Regex.Match(
            handler,
            @"^TIMESTAMP_TRANSITION_ALLOWANCE_SECONDS = (\d+)$",
            RegexOptions.Multiline);
        Match producerOverhead = Regex.Match(
            capture,
            @"^TIMESTAMP_TRANSITION_ALLOWANCE_SECONDS = (\d+)$",
            RegexOptions.Multiline);
        allowance.Success.ShouldBeTrue();
        budget.Success.ShouldBeTrue();
        verifierOverhead.Success.ShouldBeTrue();
        producerOverhead.Success.ShouldBeTrue();
        allowance.Groups[1].Value.ShouldBe(budget.Groups[1].Value);
        verifierOverhead.Groups[1].Value.ShouldBe(producerOverhead.Groups[1].Value);
    }

    /// <summary>
    /// Verifies a retained smoke record whose own window exceeds the platform budget plus the
    /// cleanup allowance fails closed, and likewise for the aggregate window. Both bounds were
    /// unasserted, and the platform bound was simultaneously too tight for records the capture tool
    /// legitimately produces.
    /// </summary>
    /// <param name="mutation">Which bound to breach.</param>
    /// <param name="expectedError">Expected fail-closed reason.</param>
    [Theory]
    [InlineData("platform-window", "Production smoke platform outcome is invalid")]
    [InlineData("aggregate-window", "Production smoke aggregate bound is invalid")]
    public void SmokeWindowsExceedingTheirBoundsFailClosed(string mutation, string expectedError)
    {
        string root = FindRepositoryRoot();
        string temporary = CopyPacket(root);
        try
        {
            MutateSmokeResults(temporary, results =>
            {
                DateTimeOffset start = DateTimeOffset.Parse(
                    results["started_at"]!.GetValue<string>(),
                    CultureInfo.InvariantCulture);
                if (mutation == "platform-window")
                {
                    // 216s > 180 + 30 + 5, while the aggregate stays inside its bound.
                    JsonObject platform = results["platforms"]!.AsArray()[0]!.AsObject();
                    DateTimeOffset platformStart = DateTimeOffset.Parse(
                        platform["started_at"]!.GetValue<string>(),
                        CultureInfo.InvariantCulture);
                    platform["ended_at"] = Utc(platformStart.AddSeconds(216));
                    results["ended_at"] = Utc(platformStart.AddSeconds(300));
                }
                else
                {
                    // 436s > 2 x (180 + 30 + 5) + 5, with platform windows untouched.
                    results["ended_at"] = Utc(start.AddSeconds(436));
                }
            });

            // The mutation re-mints the subject, so the receipts must be re-attached; otherwise the
            // run stops at the acceptance-address check and never reaches the bound under test.
            AttachThreeAcceptedReceipts(temporary);

            ShouldFailClosed(RunValidator(root, temporary), expectedError);
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies a retained smoke record whose windows sit inside the platform budget plus the
    /// cleanup allowance -- the shape the capture tool actually produces, because it stamps
    /// <c>started_at</c> before the platform deadline and <c>ended_at</c> after cleanup -- is
    /// accepted. Bounding by the platform budget alone made the producer able to emit records this
    /// verifier rejected.
    /// </summary>
    [Fact]
    public void SmokeWindowInsideThePlatformBudgetPlusCleanupAllowanceIsAccepted()
    {
        string root = FindRepositoryRoot();
        string temporary = CopyPacket(root);
        try
        {
            MutateSmokeResults(temporary, results =>
            {
                JsonObject platform = results["platforms"]!.AsArray()[0]!.AsObject();
                DateTimeOffset platformStart = DateTimeOffset.Parse(
                    platform["started_at"]!.GetValue<string>(),
                    CultureInfo.InvariantCulture);
                platform["ended_at"] = Utc(platformStart.AddSeconds(214));
                DateTimeOffset start = DateTimeOffset.Parse(
                    results["started_at"]!.GetValue<string>(),
                    CultureInfo.InvariantCulture);
                results["ended_at"] = Utc(start.AddSeconds(300));
            });
            AttachThreeAcceptedReceipts(temporary);

            (int exitCode, _, string error) = RunValidator(root, temporary);
            exitCode.ShouldBe(0, error);
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies a bytes <c>sys.path</c> entry produces the support-safe fail-closed line and the
    /// rerun trigger rather than a traceback, and -- crucially -- that such an entry is still
    /// recognised as repository-local. Catching <c>TypeError</c> alone silenced the crash by making
    /// the guard answer False, which let a bytes-origin repository module escape both displacement
    /// and the post-execution shadow check.
    /// </summary>
    /// <param name="script">Dispatcher under test.</param>
    [Theory]
    [InlineData("tools/validate-corrected-deployed-runtime-parity.py")]
    [InlineData("tools/validate-corrective-release-evidence.py")]
    public void BytesRepositoryPathsAreRecognisedRatherThanSilentlyDropped(string script)
    {
        ArgumentNullException.ThrowIfNull(script);
        string root = FindRepositoryRoot();

        // The guard must answer True for a bytes repository path; answering False is the bypass.
        (int probeExit, string probeOutput, string probeError) = RunProcess(
            root,
            "python3",
            "-c",
            "import importlib.util,sys;"
            + "s=importlib.util.spec_from_file_location('probe',sys.argv[1]);"
            + "m=importlib.util.module_from_spec(s);s.loader.exec_module(m);"
            + "r=str(m._repository_root());"
            + "print('str', m._is_repository_path(r));"
            + "print('bytes', m._is_repository_path(r.encode()));"
            + "print('outside', m._is_repository_path(b'/usr/lib/python3'))",
            Path.Combine(root, script.Replace('/', Path.DirectorySeparatorChar)));
        probeExit.ShouldBe(0, probeError);
        probeOutput.ShouldContain("str True");
        probeOutput.ShouldContain("bytes True");
        probeOutput.ShouldContain("outside False");

        // And an actual bytes sys.path entry must not produce a traceback.
        string packet = CreateIncompletePacket(root);
        try
        {
            (int exitCode, string output, string error) = RunProcess(
                root,
                "python3",
                "-c",
                "import runpy,sys;sys.path.insert(0, b'/tmp/bytes-path-entry');"
                + "sys.argv=[sys.argv[1],*sys.argv[2:]];"
                + "runpy.run_path(sys.argv[0],run_name='__main__')",
                Path.Combine(root, "tools", "validate-corrected-deployed-runtime-parity.py"),
                Path.Combine(packet, "closure.json"),
                "--packet-root",
                packet);

            exitCode.ShouldBe(1, error);
            error.ShouldNotContain("Traceback");
            error.ShouldContain("exactly three packet-bound receipts are required");
            error.ShouldContain("rerun: " + RerunTrigger);
            output.ShouldNotContain("pass:");
        }
        finally
        {
            Directory.Delete(packet, recursive: true);
        }
    }

    /// <summary>
    /// Verifies the roster-configuration guard actually fails when the rostered account is changed.
    /// Its previous form compared the identity table against strings interpolated from that same
    /// table, so rewriting <c>OWNER_GITHUB_ACCOUNT</c> to any other account left it green -- the
    /// exact defect its docstring claimed to fix. Reproduced here by mutating the handler in a
    /// copied tool tree and rebinding both the dispatcher pin and the closure's dispatch digest, so
    /// execution reaches the guard instead of stopping at the pin check.
    /// </summary>
    /// <param name="replacement">Replacement rostered account tuple.</param>
    [Theory]
    [InlineData("(\"mallory\", 999)")]
    [InlineData("(\"jpiquot\", 999)")]
    public void RosterConfigurationGuardRejectsAReRosteredAccount(string replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        string root = FindRepositoryRoot();
        string packet = CopyPacket(root);
        string temporary = Path.Combine(Path.GetTempPath(), $"eventstore-story315-roster-{Guid.NewGuid():N}");
        try
        {
            CopyDirectory(Path.Combine(root, "tools"), Path.Combine(temporary, "tools"));
            // With three receipts, validate_identity no longer short-circuits before the predecessor
            // packet is read from repository_root. Mirror that frozen handoff into the tools-only
            // temp tree so the control reaches real roster validation instead of a missing-file
            // Errno 2.
            const string PredecessorRelative =
                "_bmad-output/implementation-artifacts/evidence/story-3-14/" + SourceSha;
            CopyDirectory(
                Path.Combine(root, PredecessorRelative.Replace('/', Path.DirectorySeparatorChar)),
                Path.Combine(temporary, PredecessorRelative.Replace('/', Path.DirectorySeparatorChar)));

            // Control: the copied tree reaches real validation before anything is mutated.
            (int controlExit, string controlOutput, string controlError) = RunProcess(
                temporary,
                "python3",
                "tools/validate-corrected-deployed-runtime-parity.py",
                Path.Combine(packet, "closure.json"),
                "--packet-root",
                packet);
            controlExit.ShouldBe(0, controlError);
            controlOutput.ShouldContain("pass:");
            controlOutput.ShouldContain("subject=sha256:" + CurrentSubjectSha256);

            string handlerPath = Path.Combine(
                temporary, "tools", "deployed_runtime_parity_handlers", "v1.py");
            string handler = File.ReadAllText(handlerPath);
            // Anchored on the line start: RATIFIED_OWNER_GITHUB_ACCOUNT contains this text as a
            // substring, and rewriting both halves would make the guard compare two equal mutated
            // tuples -- exactly the green-by-construction shape this case exists to disprove.
            const string Original = "\nOWNER_GITHUB_ACCOUNT = (\"jpiquot\", 6775094)";
            handler.ShouldContain(Original);
            File.WriteAllText(
                handlerPath,
                handler.Replace(Original, "\nOWNER_GITHUB_ACCOUNT = " + replacement, StringComparison.Ordinal));

            // Rebind the dispatcher pin and the closure's dispatch digest so the run gets past the
            // pinned-source and route-selection checks and actually reaches the roster guard.
            string mutatedDigest = ComputeSha256(handlerPath);
            string dispatcherPath = Path.Combine(
                temporary, "tools", "validate-corrected-deployed-runtime-parity.py");
            string dispatcher = File.ReadAllText(dispatcherPath);
            string originalDigest = Regex.Match(
                dispatcher, "^V1_HANDLER_SHA256 = \"([0-9a-f]{64})\"", RegexOptions.Multiline).Groups[1].Value;
            originalDigest.ShouldNotBeNullOrEmpty();
            File.WriteAllText(
                dispatcherPath,
                dispatcher.Replace(originalDigest, mutatedDigest, StringComparison.Ordinal));

            string closurePath = Path.Combine(packet, "closure.json");
            JsonObject closure = LoadJson(closurePath);
            closure["dispatch"]!["handler"]!["sha256"] = mutatedDigest;
            closure["dispatch"]!["handler"]!["size"] = new FileInfo(handlerPath).Length;
            WriteCanonical(closurePath, closure);

            ShouldFailClosed(
                RunProcess(
                    temporary,
                    "python3",
                    "tools/validate-corrected-deployed-runtime-parity.py",
                    closurePath,
                    "--packet-root",
                    packet),
                "rostered owner identity configuration is inconsistent");
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
    /// Verifies a stray unreviewed field cannot persist inside a retained GitHub acceptance source.
    /// Rebinding the receipt and the closure around the enlarged source previously produced a full
    /// pass with the subject unchanged, so the rerun trigger never fired on content added to the
    /// packet's only external authentication artifact.
    /// </summary>
    /// <param name="scope">Envelope level the stray field is injected at.</param>
    [Theory]
    [InlineData("envelope")]
    [InlineData("user")]
    [InlineData("reactions")]
    public void StrayFieldInsideRetainedAcceptanceSourceFailsClosed(string scope)
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            RunValidator(root, temporary).ExitCode.ShouldBe(0);

            int index = ReceiptIndex(temporary, "eventstore-owner");
            RewriteReceiptSource(temporary, index, source =>
            {
                if (scope == "envelope")
                {
                    source["stray_unreviewed_field"] = "anything at all";
                }
                else
                {
                    source[scope]!.AsObject()["stray_unreviewed_field"] = "anything at all";
                }
            });

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
    /// Verifies a stray unreviewed field cannot persist inside the retained roster comment either.
    /// </summary>
    [Fact]
    public void StrayFieldInsideRetainedRegistryAuthoritySourceFailsClosed()
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            RunValidator(root, temporary).ExitCode.ShouldBe(0);
            MutateRegistrySourceDocument(
                temporary,
                source => source["stray_unreviewed_field"] = "anything at all");

            ShouldFailClosed(
                RunValidator(root, temporary),
                "owner-role registry authority source schema is invalid");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies fail-closed reasons the frozen block names by hand -- lineage, predecessor identity,
    /// the dispatcher route key, closure identity, package lineage, OCI identity and unsafe binding
    /// paths -- are each actually reachable and produce their own message. Every one of these was
    /// previously unasserted anywhere, so the branch behind it could have been deleted with the
    /// whole suite still green.
    /// </summary>
    /// <param name="mutation">Closure mutation to apply.</param>
    /// <param name="expectedError">Exact fail-closed reason the mutation must produce.</param>
    [Theory]
    [InlineData("workflow-run", "lineage does not reproduce the corrective release")]
    [InlineData("predecessor-digest", "predecessor identity is not the frozen Story 3.14 handoff")]
    [InlineData("handler-digest", "closure does not select a trusted live handler")]
    [InlineData("story-id", "closure identity is invalid")]
    [InlineData("package-version", "package mapping lineage is invalid")]
    [InlineData("oci-image", "OCI image identity is invalid")]
    [InlineData("oci-media-type", "OCI file binding is invalid")]
    [InlineData("unsafe-binding-path", "file binding path is unsafe")]
    public void NamedFailClosedReasonsAreEachReachable(string mutation, string expectedError)
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            RunValidator(root, temporary).ExitCode.ShouldBe(0);

            string closurePath = Path.Combine(temporary, "closure.json");
            JsonObject closure = LoadJson(closurePath);
            switch (mutation)
            {
                case "workflow-run":
                    closure["lineage"]!["workflow"]!["run_attempt"] = 2;
                    break;
                case "predecessor-digest":
                    closure["predecessor"]!["sha256"] = new string('0', 64);
                    break;
                case "handler-digest":
                    closure["dispatch"]!["handler"]!["sha256"] = new string('1', 64);
                    break;
                case "story-id":
                    closure["story_id"] = "3.16";
                    break;
                case "package-version":
                    closure["packages"]!["items"]!.AsArray()[0]!["version"] = "3.96.3";
                    break;
                case "oci-image":
                    closure["oci"]!["image"] = "registry.hexalith.com/other@" + IndexDigest;
                    break;
                case "oci-media-type":
                    closure["oci"]!["children"]!.AsArray()[0]!["config"]!["media_type"] =
                        "application/vnd.oci.image.manifest.v1+json";
                    break;
                case "unsafe-binding-path":
                    closure["technical_inventory"]!["file"] = "../technical-sha256.txt";
                    break;
                default:
                    // A typo'd InlineData must fail loudly rather than silently duplicating
                    // whichever case happened to sit behind default.
                    throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
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
    /// Verifies a retained NuGet.org package that is not a readable archive fails closed on the
    /// archive check rather than anywhere earlier, by correcting its binding first.
    /// </summary>
    [Fact]
    public void RetainedNuGetPackageThatIsNotAnArchiveFailsClosed()
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            RunValidator(root, temporary).ExitCode.ShouldBe(0);

            string closurePath = Path.Combine(temporary, "closure.json");
            JsonObject closure = LoadJson(closurePath);
            JsonObject nuget = closure["packages"]!["items"]!.AsArray()[0]!["nuget_org"]!.AsObject();
            string packagePath = Path.Combine(temporary, nuget["file"]!.GetValue<string>());
            File.WriteAllBytes(packagePath, "not a zip archive at all"u8.ToArray());
            nuget["sha256"] = ComputeSha256(packagePath);
            nuget["size"] = new FileInfo(packagePath).Length;
            WriteCanonical(closurePath, closure);

            ShouldFailClosed(
                RunValidator(root, temporary),
                "NuGet.org package is not a valid signed archive");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies the raw OCI index bytes are pinned by the selected index digest itself, which is
    /// what makes <c>raw OCI index shape is invalid</c> unreachable through packet mutation: any
    /// edit to <c>oci/index.raw</c> changes its SHA-256, and the binding, the descriptor digest and
    /// the selected identity all have to equal the one constant the handler pins. The shape guard
    /// is retained as a structural precondition for the strict three-way zip that follows it.
    /// </summary>
    [Fact]
    public void RawOciIndexBytesArePinnedByTheSelectedIndexDigest()
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            RunValidator(root, temporary).ExitCode.ShouldBe(0);

            string indexPath = Path.Combine(temporary, "oci", "index.raw");
            ComputeSha256(indexPath).ShouldBe(IndexDigest["sha256:".Length..]);

            byte[] original = File.ReadAllBytes(indexPath);
            File.WriteAllBytes(indexPath, [.. original, (byte)'\n']);

            ShouldFailClosed(RunValidator(root, temporary), "retained file binding mismatch: oci/index.raw");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies packet bindings reject FIFOs before reading them. Treating unsupported entries as
    /// "not a file" let a FIFO ride through inventory discovery and block indefinitely once a
    /// declared binding tried to read it.
    /// </summary>
    [Fact]
    public void PacketBindingsRequireRegularFiles()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            string package = Path.Combine(
                temporary,
                "packages",
                "Hexalith.EventStore.Contracts.3.96.2.nupkg");
            File.Delete(package);
            (int fifoExit, _, string fifoError) = RunProcess(temporary, "mkfifo", package);
            fifoExit.ShouldBe(0, fifoError);

            ShouldFailClosed(
                RunValidator(root, temporary),
                "retained packet entry is not a regular file");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies a binding cannot follow a symlink merely because its target is another regular file
    /// inside the packet. The lexical entry itself must be a retained regular file.
    /// </summary>
    [Fact]
    public void PacketBindingsRejectInternalSymlinkTargetsBeforeReading()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            string package = Path.Combine(
                temporary,
                "packages",
                "Hexalith.EventStore.Contracts.3.96.2.nupkg");
            string target = Path.Combine(temporary, "packages", "internal-target.nupkg");
            File.Move(package, target);
            File.CreateSymbolicLink(package, target);

            ShouldFailClosed(
                RunValidator(root, temporary),
                "packet-relative evidence path traverses a symbolic link");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies the assembler rejects a symlinked packet root before reading or rewriting any
    /// retained input, even when the link resolves to an otherwise valid packet.
    /// </summary>
    [Fact]
    public void AssemblerRejectsASymlinkedPacketRoot()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        string root = FindRepositoryRoot();
        string packet = CopyPacket(root);
        string link = Path.Combine(Path.GetTempPath(), $"eventstore-story315-packet-link-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateSymbolicLink(link, packet);

            (int exitCode, string output, string error) = RunProcess(
                root,
                "python3",
                "tools/assemble-corrected-deployed-runtime-parity.py",
                link);

            exitCode.ShouldBe(1, error);
            output.ShouldNotContain("subject=sha256:");
            error.ShouldContain("packet root is not a regular directory");
            error.ShouldContain("rerun: ");
            error.ShouldNotContain("Traceback");
        }
        finally
        {
            if (Directory.Exists(link))
            {
                Directory.Delete(link);
            }

            Directory.Delete(packet, recursive: true);
        }
    }

    /// <summary>
    /// Verifies an existing output entry cannot redirect the assembler's closure write outside the
    /// packet. The packet tree is rejected before the retained subject or any output is rewritten.
    /// </summary>
    [Fact]
    public void AssemblerRejectsASymlinkedOutputBeforeAnyWrite()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        string root = FindRepositoryRoot();
        string packet = CopyPacket(root);
        string closurePath = Path.Combine(packet, "closure.json");
        string outside = Path.Combine(
            Path.GetTempPath(),
            $"eventstore-story315-outside-closure-{Guid.NewGuid():N}.json");
        try
        {
            byte[] sentinel = Encoding.UTF8.GetBytes("outside-sentinel\n");
            File.WriteAllBytes(outside, sentinel);
            File.Delete(closurePath);
            File.CreateSymbolicLink(closurePath, outside);

            (int exitCode, string output, string error) = RunProcess(
                root,
                "python3",
                "tools/assemble-corrected-deployed-runtime-parity.py",
                packet);

            exitCode.ShouldBe(1, error);
            output.ShouldNotContain("subject=sha256:");
            error.ShouldContain("packet contains a symbolic link");
            error.ShouldContain("rerun: ");
            error.ShouldNotContain("Traceback");
            File.ReadAllBytes(outside).ShouldBe(sentinel);
        }
        finally
        {
            if (File.Exists(closurePath))
            {
                File.Delete(closurePath);
            }

            Directory.Delete(packet, recursive: true);
            if (File.Exists(outside))
            {
                File.Delete(outside);
            }
        }
    }

    /// <summary>
    /// Verifies the assembler validates a retained predecessor identity before indexing it. The
    /// copied tool tree makes the malformed predecessor a repository-owned input for this
    /// invocation without altering the immutable checked-in Story 3.14 packet.
    /// </summary>
    [Fact]
    public void AssemblerValidatesTheRetainedPredecessorBeforeIndexing()
    {
        string root = FindRepositoryRoot();
        string packet = CopyPacket(root);
        string repository = Path.Combine(
            Path.GetTempPath(),
            $"eventstore-story315-assembler-repository-{Guid.NewGuid():N}");
        try
        {
            CopyDirectory(Path.Combine(root, "tools"), Path.Combine(repository, "tools"));
            string predecessorRelative = Path.Combine(
                "_bmad-output",
                "implementation-artifacts",
                "evidence",
                "story-3-14",
                SourceSha,
                "release-identity.json");
            string predecessorPath = Path.Combine(repository, predecessorRelative);
            Directory.CreateDirectory(Path.GetDirectoryName(predecessorPath).ShouldNotBeNull());
            WriteCanonical(predecessorPath, new JsonObject());

            string closurePath = Path.Combine(packet, "closure.json");
            string originalClosure = ComputeSha256(closurePath);
            (int exitCode, string output, string error) = RunProcess(
                repository,
                "python3",
                "tools/assemble-corrected-deployed-runtime-parity.py",
                packet);

            exitCode.ShouldBe(1, error);
            output.ShouldNotContain("subject=sha256:");
            error.ShouldContain("corrective release identity field set drift");
            error.ShouldContain("rerun: ");
            error.ShouldNotContain("Traceback");
            ComputeSha256(closurePath).ShouldBe(originalClosure);
        }
        finally
        {
            Directory.Delete(packet, recursive: true);
            if (Directory.Exists(repository))
            {
                Directory.Delete(repository, recursive: true);
            }
        }
    }

    /// <summary>
    /// Verifies malformed retained registry and smoke documents fail with controlled assembler
    /// reasons before dictionary indexing can raise a traceback or emit a replacement closure.
    /// </summary>
    /// <param name="document">Retained document to corrupt.</param>
    /// <param name="expectedError">Focused assembler failure.</param>
    [Theory]
    [InlineData("registry", "retained owner-role authority source structure is invalid")]
    [InlineData("smokes", "retained Production smoke results structure is invalid")]
    public void AssemblerValidatesRetainedStructuresBeforeIndexing(
        string document,
        string expectedError)
    {
        string root = FindRepositoryRoot();
        string temporary = CopyPacket(root);
        try
        {
            string closurePath = Path.Combine(temporary, "closure.json");
            string originalClosure = ComputeSha256(closurePath);
            if (document == "registry")
            {
                WriteCanonical(
                    Path.Combine(temporary, "registry", "role-registry-source.json"),
                    new JsonObject());
            }
            else
            {
                JsonObject results = LoadJson(
                    Path.Combine(temporary, "smokes", "smoke-results.json"));
                results.Remove("platforms");
                WriteCanonical(
                    Path.Combine(temporary, "smokes", "smoke-results.json"),
                    results);
            }

            (int exitCode, string output, string error) = RunProcess(
                root,
                "python3",
                "tools/assemble-corrected-deployed-runtime-parity.py",
                temporary);

            exitCode.ShouldBe(1, error);
            output.ShouldNotContain("subject=sha256:");
            error.ShouldContain(expectedError);
            error.ShouldContain("rerun: ");
            error.ShouldNotContain("Traceback");
            ComputeSha256(closurePath).ShouldBe(originalClosure);
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies each invalid Production-smoke state the assembler claims to reject stops before
    /// closure emission: failed aggregate, wrong immutable-child coverage, and failed platform.
    /// </summary>
    /// <param name="mutation">Smoke state to invalidate.</param>
    /// <param name="expectedError">Focused assembler failure.</param>
    [Theory]
    [InlineData("aggregate", "retained Production smokes did not pass")]
    [InlineData("child-coverage", "retained Production smokes do not cover the selected children")]
    [InlineData("platform-outcome", "a retained Production smoke platform did not pass")]
    public void AssemblerRejectsInvalidSmokeStatesBeforeClosureEmission(
        string mutation,
        string expectedError)
    {
        string root = FindRepositoryRoot();
        string temporary = CopyPacket(root);
        try
        {
            string closurePath = Path.Combine(temporary, "closure.json");
            string originalClosure = ComputeSha256(closurePath);
            string resultsPath = Path.Combine(temporary, "smokes", "smoke-results.json");
            JsonObject results = LoadJson(resultsPath);
            switch (mutation)
            {
                case "aggregate":
                    results["result"] = "failure";
                    results["exit_code"] = 1;
                    break;
                case "child-coverage":
                    results["platforms"]![0]!["child_digest"] = "sha256:" + new string('0', 64);
                    break;
                case "platform-outcome":
                    results["platforms"]![0]!["outcome"] = "failure";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown smoke mutation.");
            }

            WriteCanonical(resultsPath, results);

            (int exitCode, string output, string error) = RunProcess(
                root,
                "python3",
                "tools/assemble-corrected-deployed-runtime-parity.py",
                temporary);

            exitCode.ShouldBe(1, error);
            output.ShouldNotContain("subject=sha256:");
            error.ShouldContain(expectedError);
            error.ShouldContain("rerun: ");
            error.ShouldNotContain("Traceback");
            ComputeSha256(closurePath).ShouldBe(originalClosure);
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies the packet assembler is executable and that its reported contract is real, not a
    /// prose claim. It had no caller anywhere in the repository, so replacing its receipt-count exit
    /// rule with an unconditional success left every test green -- the always-exit-zero shape of the
    /// same defect that already shipped once in this story.
    /// </summary>
    [Fact]
    public void AssemblerReproducesTheSubjectAndPropagatesTheVerifierVerdict()
    {
        string root = FindRepositoryRoot();
        string incomplete = CreateIncompletePacket(root);
        string accepted = CreateAcceptedPacket(root);
        try
        {
            // Zero receipts: the assembler must reproduce the checked-in subject byte-for-byte and
            // report the verifier's own non-zero verdict rather than a success-shaped line.
            (int ExitCode, string Output, string Error) result = RunProcess(
                root,
                "python3",
                "tools/assemble-corrected-deployed-runtime-parity.py",
                incomplete);

            // ShouldNotBe(0) is satisfied by a traceback, which is the failure shape this lane
            // keeps producing; require the same fail-closed contract as every other negative.
            ShouldFailClosed(result, "exactly three packet-bound receipts are required");
            string output = result.Output;
            output.ShouldContain("subject=sha256:" + CurrentSubjectSha256);
            output.ShouldContain("receipts=0");
            output.ShouldContain("verifier_exit=1");
            ComputeSha256(Path.Combine(incomplete, "subject.json")).ShouldBe(CurrentSubjectSha256);

            // Three receipts: the same subject, a passing verifier, and a zero exit.
            (int acceptedExit, string acceptedOutput, string acceptedError) = RunProcess(
                root,
                "python3",
                "tools/assemble-corrected-deployed-runtime-parity.py",
                accepted);
            acceptedExit.ShouldBe(0, acceptedError);
            acceptedOutput.ShouldContain("subject=sha256:" + CurrentSubjectSha256);
            acceptedOutput.ShouldContain("receipts=3");
            acceptedOutput.ShouldContain("verifier_exit=0");
        }
        finally
        {
            Directory.Delete(incomplete, recursive: true);
            Directory.Delete(accepted, recursive: true);
        }
    }

    /// <summary>
    /// Verifies the closure binds both packet producers. Neither the bounded smoke capture tool nor
    /// the assembler was bound anywhere, which is exactly why the smoke acceptance semantics could
    /// change -- from any 2xx to exactly 200 -- without invalidating a single receipt.
    /// </summary>
    [Theory]
    [InlineData("capture", "tools/capture-corrected-deployed-runtime-parity-smokes.py")]
    [InlineData("assembler", "tools/assemble-corrected-deployed-runtime-parity.py")]
    public void ClosureBindsBothPacketProducers(string bindingName, string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        string root = FindRepositoryRoot();
        JsonObject closure = LoadJson(Path.Combine(root, EvidenceRelativePath, "closure.json"));
        JsonObject binding = closure["dispatch"]![bindingName]!.AsObject();

        binding["file"]!.GetValue<string>().ShouldBe(relativePath);
        binding["sha256"]!.GetValue<string>().ShouldBe(
            ComputeSha256(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar))));
        binding["size"]!.GetValue<long>().ShouldBe(
            new FileInfo(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar))).Length);

        // The subject binds the whole dispatch block, so a producer edit re-mints.
        JsonObject subject = LoadJson(Path.Combine(root, EvidenceRelativePath, "subject.json"));
        subject["verifier"]![bindingName]!["sha256"]!.GetValue<string>()
            .ShouldBe(binding["sha256"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies a drifted producer binding fails closed instead of validating against stale bytes.
    /// </summary>
    /// <param name="bindingName">Producer binding to drift.</param>
    [Theory]
    [InlineData("capture")]
    [InlineData("assembler")]
    public void DriftedProducerBindingFailsClosed(string bindingName)
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            RunValidator(root, temporary).ExitCode.ShouldBe(0);

            string closurePath = Path.Combine(temporary, "closure.json");
            JsonObject closure = LoadJson(closurePath);
            closure["dispatch"]![bindingName]!["sha256"] = new string('2', 64);
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
    /// Verifies the two operator-facing Story 3.15 records state the current subject and the current
    /// verdict. Only docs/ci.md was drift-bound, so both records could be -- and once were -- left
    /// asserting a superseded subject and a passing verdict against a packet that fails closed.
    /// </summary>
    /// <param name="relativePath">Operator record to check.</param>
    [Theory]
    [InlineData("_bmad-output/implementation-artifacts/3-15-corrected-deployed-runtime-parity-closure.md")]
    [InlineData("_bmad-output/implementation-artifacts/3-15-corrected-deployed-runtime-parity-closure-proof-packet.md")]
    public void OperatorRecordsStateTheCurrentSubjectAndVerdict(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        string root = FindRepositoryRoot();
        JsonObject closure = LoadJson(Path.Combine(root, EvidenceRelativePath, "closure.json"));
        string subjectSha256 = closure["subject"]!["sha256"]!.GetValue<string>();
        int receipts = closure["acceptances"]!["receipts"]!.AsArray().Count;
        string record = File.ReadAllText(
            Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

        record.ShouldContain(subjectSha256);
        record.ShouldContain(IndexDigest["sha256:".Length..]);

        // Read the claim field itself rather than inferring the verdict only from the receipt
        // count: a record must explain the value an auditor will actually find in the JSON.
        closure["deployed_runtime_parity"]!.GetValue<string>().ShouldBe("available");
        record.ShouldContain("claim");

        // A record may narrate superseded subjects, but it must never present one as current: the
        // current subject has to be the first subject digest the reader meets.
        string[] subjects =
        [
            subjectSha256,
            BatchSupersededSubjectSha256,
            TrustedVerifierSupersededSubjectSha256,
            SupersededSubjectSha256,
        ];
        string? firstSubject = Regex.Matches(record, "(?<![0-9a-fA-F])[0-9a-f]{64}(?![0-9a-fA-F])")
            .Select(match => match.Value)
            .FirstOrDefault(value => subjects.Contains(value, StringComparer.Ordinal));
        firstSubject.ShouldBe(subjectSha256, relativePath);

        // The verdict itself must agree with the packet, not with a previous acceptance round.
        if (receipts == RequiredRoles.Length)
        {
            record.ShouldContain("parity is available");
        }
        else
        {
            record.ShouldContain("fails closed");
            record.ShouldContain(FormattableString.Invariant($"{receipts} of {RequiredRoles.Length}"));
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
        string acceptancesRoot = Path.Combine(temporary, "acceptances");
        if (Directory.Exists(acceptancesRoot))
        {
            Directory.Delete(acceptancesRoot, recursive: true);
        }

        string closurePath = Path.Combine(temporary, "closure.json");
        JsonObject closure = LoadJson(closurePath);
        closure["acceptances"]!["receipts"] = new JsonArray();
        WriteCanonical(closurePath, closure);
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

    /// <summary>
    /// Builds one retained GitHub issue-comment envelope in the exact closed shape the verifier
    /// accepts. The verifier close-lists the envelope, its user object and its reaction summary, so
    /// a fixture that emits only the fields the checks read would be rejected -- and, more
    /// importantly, would stop proving that a stray unreviewed field fails closed.
    /// </summary>
    /// <param name="commentId">Comment identifier to bind the id, URLs and anchor to.</param>
    /// <param name="body">Comment body.</param>
    /// <param name="timestamp">Value used for both created_at and updated_at.</param>
    /// <returns>The closed-shape comment document.</returns>
    private static JsonObject GitHubComment(long commentId, string body, string timestamp) => new()
    {
        ["author_association"] = "MEMBER",
        ["body"] = body,
        ["created_at"] = timestamp,
        ["html_url"] = FormattableString.Invariant(
            $"https://github.com/Hexalith/Hexalith.EventStore/issues/{AcceptanceIssue}#issuecomment-{commentId}"),
        ["id"] = commentId,
        ["issue_url"] = FormattableString.Invariant(
            $"https://api.github.com/repos/Hexalith/Hexalith.EventStore/issues/{AcceptanceIssue}"),
        ["minimized"] = null,
        ["node_id"] = FormattableString.Invariant($"IC_kwDO{commentId}"),
        ["performed_via_github_app"] = null,
        ["pin"] = null,
        ["reactions"] = new JsonObject
        {
            ["+1"] = 0,
            ["-1"] = 0,
            ["confused"] = 0,
            ["eyes"] = 0,
            ["heart"] = 0,
            ["hooray"] = 0,
            ["laugh"] = 0,
            ["rocket"] = 0,
            ["total_count"] = 0,
            ["url"] = FormattableString.Invariant(
                $"https://api.github.com/repos/Hexalith/Hexalith.EventStore/issues/comments/{commentId}/reactions"),
        },
        ["updated_at"] = timestamp,
        ["url"] = FormattableString.Invariant(
            $"https://api.github.com/repos/Hexalith/Hexalith.EventStore/issues/comments/{commentId}"),
        ["user"] = new JsonObject
        {
            ["avatar_url"] = "https://avatars.githubusercontent.com/u/6775094?v=4",
            ["events_url"] = "https://api.github.com/users/jpiquot/events{/privacy}",
            ["followers_url"] = "https://api.github.com/users/jpiquot/followers",
            ["following_url"] = "https://api.github.com/users/jpiquot/following{/other_user}",
            ["gists_url"] = "https://api.github.com/users/jpiquot/gists{/gist_id}",
            ["gravatar_id"] = string.Empty,
            ["html_url"] = "https://github.com/jpiquot",
            ["id"] = 6775094,
            ["login"] = "jpiquot",
            ["node_id"] = "MDQ6VXNlcjY3NzUwOTQ=",
            ["organizations_url"] = "https://api.github.com/users/jpiquot/orgs",
            ["received_events_url"] = "https://api.github.com/users/jpiquot/received_events",
            ["repos_url"] = "https://api.github.com/users/jpiquot/repos",
            ["site_admin"] = false,
            ["starred_url"] = "https://api.github.com/users/jpiquot/starred{/owner}{/repo}",
            ["subscriptions_url"] = "https://api.github.com/users/jpiquot/subscriptions",
            ["type"] = "User",
            ["url"] = "https://api.github.com/users/jpiquot",
            ["user_view_type"] = "public",
        },
    };

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
                sourceDocument = GitHubComment(
                    commentId,
                    Encoding.UTF8.GetString(CanonicalBytes(acceptance)).TrimEnd('\n'),
                    acceptedAt.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
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
    /// <summary>Formats one instant in the exact second-precision UTC shape the verifier requires.</summary>
    /// <param name="value">Instant to format.</param>
    /// <returns>The formatted timestamp.</returns>
    private static string Utc(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    /// <summary>
    /// Rewrites the retained Production smoke summary, regenerates each platform log from the
    /// mutated summary so the log-versus-summary equality still holds, then rebinds the log
    /// bindings, the results binding, the closed inventory and the canonical subject. Without the
    /// log regeneration a timing mutation would fail on the log comparison rather than on the bound
    /// under test.
    /// </summary>
    /// <param name="packet">Packet root to mutate.</param>
    /// <param name="mutate">Mutation applied to the smoke summary.</param>
    private static void MutateSmokeResults(string packet, Action<JsonObject> mutate)
    {
        string closurePath = Path.Combine(packet, "closure.json");
        JsonObject closure = LoadJson(closurePath);
        string resultsRelative = closure["production_smokes"]!["results"]!["file"]!.GetValue<string>();
        string resultsPath = Path.Combine(packet, resultsRelative);
        JsonObject results = LoadJson(resultsPath);

        mutate(results);

        foreach (JsonNode? platform in results["platforms"]!.AsArray())
        {
            JsonObject item = platform!.AsObject();
            JsonObject log = new()
            {
                ["attempts"] = item["attempts"]!.DeepClone(),
                ["child_digest"] = item["child_digest"]!.DeepClone(),
                ["cleanup"] = item["cleanup"]!.DeepClone(),
                ["ended_at"] = item["ended_at"]!.DeepClone(),
                ["exit_code"] = item["exit_code"]!.DeepClone(),
                ["health_path"] = "/alive",
                ["hosting_environment"] = "Production",
                ["http_status"] = item["http_status"]!.DeepClone(),
                ["observed_runtime_platform"] = item["observed_runtime_platform"]!.DeepClone(),
                ["outcome"] = item["outcome"]!.DeepClone(),
                ["platform"] = item["platform"]!.DeepClone(),
                ["readiness_result"] = item["readiness_result"]!.DeepClone(),
                ["redirect_count"] = item["redirect_count"]!.DeepClone(),
                ["schema"] = "hexalith.eventstore.production-smoke-log.v1",
                ["started_at"] = item["started_at"]!.DeepClone(),
            };
            string logRelative = item["log"]!["file"]!.GetValue<string>();
            string logPath = Path.Combine(packet, logRelative);
            WriteCanonical(logPath, log);
            item["log"]!["sha256"] = ComputeSha256(logPath);
            item["log"]!["size"] = new FileInfo(logPath).Length;
        }

        WriteCanonical(resultsPath, results);
        closure["production_smokes"]!["results"]!["sha256"] = ComputeSha256(resultsPath);
        closure["production_smokes"]!["results"]!["size"] = new FileInfo(resultsPath).Length;
        WriteCanonical(closurePath, closure);
        RebindInventoryAndSubject(packet);
    }

    private static void MutateRegistrySourceBody(string packet, Func<string, string> transform) =>
        MutateRegistrySourceDocument(
            packet,
            source => source["body"] = transform(source["body"]!.GetValue<string>()));

    /// <summary>
    /// Rewrites any field of the retained roster comment, then rebinds the registry document and
    /// the closure's registry binding. As with the body variant, the technical inventory and the
    /// canonical subject are deliberately left alone: registry validation runs before the inventory
    /// sweep, so a negative case fails on the registry itself.
    /// </summary>
    /// <param name="packet">Packet root to mutate.</param>
    /// <param name="mutate">Mutation applied to the retained comment document.</param>
    private static void MutateRegistrySourceDocument(string packet, Action<JsonObject> mutate)
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
        mutate(source);
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

        // The subject also hashes the canonical package-domain and OCI blocks, so a rebind that
        // touched retained package or OCI bytes has to re-derive those too -- otherwise the
        // positive control fails on the subject rather than on the property under test.
        subject["evidence"]!["package_domains_sha256"] =
            Convert.ToHexString(SHA256.HashData(CanonicalBytes(closure["packages"]!)))
                .ToLowerInvariant();
        subject["evidence"]!["oci_graph_sha256"] =
            Convert.ToHexString(SHA256.HashData(CanonicalBytes(closure["oci"]!)))
                .ToLowerInvariant();
        subject["evidence"]!["production_smokes_sha256"] =
            closure["production_smokes"]!["results"]!["sha256"]!.GetValue<string>();
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
        => RunProcessWithEnvironment(
            workingDirectory,
            fileName,
            new Dictionary<string, string>(),
            arguments);

    private static (int ExitCode, string Output, string Error) RunProcessWithEnvironment(
        string workingDirectory,
        string fileName,
        IReadOnlyDictionary<string, string> environment,
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

        foreach ((string key, string value) in environment)
        {
            startInfo.Environment[key] = value;
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

    /// <summary>
    /// Copies a tree, skipping compiled Python bytecode. Copying developer-local
    /// <c>__pycache__</c> into the very tree the stale-bytecode test controls would let a cache
    /// this test did not create decide the outcome.
    /// </summary>
    /// <param name="source">Directory to copy.</param>
    /// <param name="destination">Destination directory.</param>
    private static void CopyDirectory(string source, string destination)
    {
        static bool IsBytecode(string path) =>
            path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => segment == "__pycache__")
            || path.EndsWith(".pyc", StringComparison.Ordinal);

        Directory.CreateDirectory(destination);
        foreach (string directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(source, directory);
            if (!IsBytecode(relative))
            {
                Directory.CreateDirectory(Path.Combine(destination, relative));
            }
        }

        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(source, file);
            if (!IsBytecode(relative))
            {
                File.Copy(file, Path.Combine(destination, relative));
            }
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
