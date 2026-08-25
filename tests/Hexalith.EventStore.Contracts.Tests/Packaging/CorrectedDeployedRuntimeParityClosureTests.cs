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
    ];

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

        (int exitCode, _, string error) = RunValidator(root, packet);

        exitCode.ShouldNotBe(0);
        error.ShouldContain("exactly three packet-bound receipts are required");
        JsonObject closure = LoadJson(Path.Combine(packet, "closure.json"));
        closure["acceptances"]!["receipts"]!.AsArray().Count.ShouldBe(0);
        closure["selected_deployed_identity"]!.GetValue<string>().ShouldBe(IndexDigest);
        closure["deployment_authorized"]!.GetValue<bool>().ShouldBeFalse();
        closure["consumer_removal_authorized"]!.GetValue<bool>().ShouldBeFalse();
        closure["publication_authorized"]!.GetValue<bool>().ShouldBeFalse();
        closure["grants_mutation_authority"]!.GetValue<bool>().ShouldBeFalse();

        // The superseded receipts must stay outside the packet: reachable for audit, never bound.
        string superseded = Path.Combine(
            root, "_bmad-output", "implementation-artifacts", "evidence", "story-3-15",
            "superseded-acceptances");
        Directory.Exists(superseded).ShouldBeTrue();
        Directory.Exists(Path.Combine(packet, "acceptances", "bb58d691ee404cc958433e996204e3382721de3931ac64cf8f7a61de97c30709"))
            .ShouldBeFalse();
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
    /// Verifies mutable package, OCI, and smoke evidence fails closed even when a full receipt set exists.
    /// </summary>
    /// <param name="relativePath">Retained evidence file to mutate.</param>
    /// <param name="expectedError">Expected fail-closed reason.</param>
    [Theory]
    [InlineData("packages/Hexalith.EventStore.Contracts.3.96.2.nupkg", "retained file binding mismatch")]
    [InlineData("oci/index.raw", "retained file binding mismatch")]
    [InlineData("oci/child-linux-amd64.manifest.raw", "retained file binding mismatch")]
    [InlineData("oci/child-linux-arm64.config.raw", "retained file binding mismatch")]
    [InlineData("smokes/smoke-linux-amd64.log", "retained file binding mismatch")]
    [InlineData("smokes/smoke-linux-arm64.log", "retained file binding mismatch")]
    [InlineData("technical-sha256.txt", "retained file binding mismatch")]
    [InlineData("registry/owner-role-registry.json", "retained file binding mismatch")]
    [InlineData("registry/role-registry-source.json", "retained file binding mismatch")]
    [InlineData("subject.json", "retained file binding mismatch")]
    public void MutableOrMixedEvidenceNeverSelectsIdentity(string relativePath, string expectedError)
    {
        string root = FindRepositoryRoot();
        string temporary = CreateAcceptedPacket(root);
        try
        {
            string path = Path.Combine(temporary, relativePath);
            File.WriteAllBytes(path, [.. File.ReadAllBytes(path), (byte)' ']);

            (int exitCode, _, string error) = RunValidator(root, temporary);

            exitCode.ShouldNotBe(0);
            error.ShouldContain(expectedError);
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

            (int exitCode, _, string error) = RunValidator(root, temporary);

            exitCode.ShouldNotBe(0);
            error.ShouldContain(expectedError);
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
            JsonObject closure = LoadJson(Path.Combine(temporary, "closure.json"));
            string receiptRelative = closure["acceptances"]!["receipts"]![0]!["file"]!.GetValue<string>();
            string receiptPath = Path.Combine(temporary, receiptRelative);
            JsonObject receipt = LoadJson(receiptPath);
            receipt.Remove(field);
            WriteCanonical(receiptPath, receipt);
            UpdateReceiptBinding(closure, 0, receiptPath);
            WriteCanonical(Path.Combine(temporary, "closure.json"), closure);

            (int exitCode, _, string error) = RunValidator(root, temporary);

            exitCode.ShouldNotBe(0);
            error.ShouldContain("acceptance receipt schema is invalid");
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
                string receiptPath = Path.Combine(temporary, bindings[0]!["file"]!.GetValue<string>());
                JsonObject receipt = LoadJson(receiptPath);
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

                WriteCanonical(receiptPath, receipt);
                UpdateReceiptBinding(closure, 0, receiptPath);
                WriteCanonical(closurePath, closure);
            }

            (int exitCode, _, string error) = RunValidator(root, temporary);

            exitCode.ShouldNotBe(0);
            error.ShouldContain(expectedError);
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

            (int exitCode, _, string error) = RunValidator(root, temporary);

            exitCode.ShouldNotBe(0);
            error.ShouldContain("closure outcome or non-authority flags are invalid");
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
    /// Verifies the dispatch table's pinned handler digest matches the live handler file, so a
    /// drifted literal fails closed instead of silently accepting a stale or wrong handler.
    /// </summary>
    [Fact]
    public void DispatchTableHandlerDigestMatchesLiveHandlerFile()
    {
        string root = FindRepositoryRoot();
        string handlerPath = Path.Combine(root, "tools", "deployed_runtime_parity_handlers", "v1.py");
        string dispatcherText = File.ReadAllText(
            Path.Combine(root, "tools", "validate-corrected-deployed-runtime-parity.py"));
        Match match = Regex.Match(dispatcherText, "V1_HANDLER_SHA256 = \"([0-9a-f]{64})\"");

        match.Success.ShouldBeTrue();
        match.Groups[1].Value.ShouldBe(ComputeSha256(handlerPath));
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
        // require the set of 64-hex tokens in the Story 3.15 section to be exactly the expected two.
        int section = ci.IndexOf("### Story 3.15", StringComparison.Ordinal);
        section.ShouldBeGreaterThan(-1);
        string[] digests = Regex.Matches(ci[section..], "[0-9a-f]{64}")
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

            (int exitCode, _, string error) = RunValidator(root, temporary);

            exitCode.ShouldNotBe(0);
            error.ShouldContain("packet contains files outside the closed technical inventory");
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

            (int exitCode, _, string error) = RunValidator(root, temporary);

            exitCode.ShouldNotBe(0);
            error.ShouldContain("NuGet.org package signature or nuspec identity is invalid");
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

            (int exitCode, _, string error) = RunValidator(root, temporary);

            exitCode.ShouldNotBe(0);
            error.ShouldContain("Production smoke log does not reproduce its result");
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
            string closurePath = Path.Combine(temporary, "closure.json");
            JsonObject closure = LoadJson(closurePath);
            string receiptRelative = closure["acceptances"]!["receipts"]![0]!["file"]!.GetValue<string>();
            string receiptPath = Path.Combine(temporary, receiptRelative);
            JsonObject receipt = LoadJson(receiptPath);
            string sourceRelative = receipt["durable_source"]!["file"]!.GetValue<string>();
            string sourcePath = Path.Combine(temporary, sourceRelative);
            JsonObject source = LoadJson(sourcePath);
            source["author_association"] = "NONE";
            WriteCanonical(sourcePath, source);
            receipt["durable_source"]!["sha256"] = ComputeSha256(sourcePath);
            receipt["durable_source"]!["size"] = new FileInfo(sourcePath).Length;
            WriteCanonical(receiptPath, receipt);
            UpdateReceiptBinding(closure, 0, receiptPath);
            WriteCanonical(closurePath, closure);

            (int exitCode, _, string error) = RunValidator(root, temporary);

            exitCode.ShouldNotBe(0);
            error.ShouldContain("GitHub acceptance source is not authenticated to the rostered owner");
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
            string closurePath = Path.Combine(temporary, "closure.json");
            JsonObject closure = LoadJson(closurePath);
            string registryRelative = closure["owner_role_registry"]!["file"]!.GetValue<string>();
            string registryPath = Path.Combine(temporary, registryRelative);
            JsonObject registry = LoadJson(registryPath);
            string sourceRelative = registry["authority_source"]!["file"]!.GetValue<string>();
            string sourcePath = Path.Combine(temporary, sourceRelative);
            JsonObject source = LoadJson(sourcePath);
            source["body"] = source["body"]!.GetValue<string>() + "\n- eventstore-owner: github:mallory";
            WriteCanonical(sourcePath, source);
            registry["authority_source"]!["sha256"] = ComputeSha256(sourcePath);
            registry["authority_source"]!["size"] = new FileInfo(sourcePath).Length;
            WriteCanonical(registryPath, registry);
            closure["owner_role_registry"]!["sha256"] = ComputeSha256(registryPath);
            closure["owner_role_registry"]!["size"] = new FileInfo(registryPath).Length;
            WriteCanonical(closurePath, closure);

            (int exitCode, _, string error) = RunValidator(root, temporary);

            exitCode.ShouldNotBe(0);
            error.ShouldContain("owner-role registry authority source is invalid");
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
            string closurePath = Path.Combine(temporary, "closure.json");
            JsonObject closure = LoadJson(closurePath);
            JsonArray bindings = closure["acceptances"]!["receipts"]!.AsArray();
            int testArchitectIndex = Enumerable.Range(0, bindings.Count)
                .First(index => bindings[index]!["role"]!.GetValue<string>() == "test-architect");
            string receiptPath = Path.Combine(temporary, bindings[testArchitectIndex]!["file"]!.GetValue<string>());
            JsonObject receipt = LoadJson(receiptPath);
            string sourcePath = Path.Combine(temporary, receipt["durable_source"]!["file"]!.GetValue<string>());
            JsonObject source = LoadJson(sourcePath);
            source["test_architect"] = "bmad:mallory";
            WriteCanonical(sourcePath, source);
            receipt["durable_source"]!["sha256"] = ComputeSha256(sourcePath);
            receipt["durable_source"]!["size"] = new FileInfo(sourcePath).Length;
            WriteCanonical(receiptPath, receipt);
            UpdateReceiptBinding(closure, testArchitectIndex, receiptPath);
            WriteCanonical(closurePath, closure);

            (int exitCode, _, string error) = RunValidator(root, temporary);

            exitCode.ShouldNotBe(0);
            error.ShouldContain("Test Architect acceptance source is invalid");
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
            string closurePath = Path.Combine(temporary, "closure.json");
            JsonObject closure = LoadJson(closurePath);
            string receiptRelative = closure["acceptances"]!["receipts"]![0]!["file"]!.GetValue<string>();
            string receiptPath = Path.Combine(temporary, receiptRelative);
            JsonObject receipt = LoadJson(receiptPath);
            receipt["durable_source"]!.AsObject().Remove(field);
            WriteCanonical(receiptPath, receipt);
            UpdateReceiptBinding(closure, 0, receiptPath);
            WriteCanonical(closurePath, closure);

            (int exitCode, _, string error) = RunValidator(root, temporary);

            exitCode.ShouldNotBe(0);
            error.ShouldContain("receipt source binding is invalid");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
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

    private static string CreateAcceptedPacket(string root)
    {
        string source = Path.Combine(root, EvidenceRelativePath);
        string temporary = Path.Combine(Path.GetTempPath(), $"eventstore-story315-{Guid.NewGuid():N}");
        CopyDirectory(source, temporary);
        JsonObject closure = LoadJson(Path.Combine(temporary, "closure.json"));
        string subjectHash = closure["subject"]!["sha256"]!.GetValue<string>();
        JsonObject subject = LoadJson(Path.Combine(temporary, "subject.json"));
        DateTimeOffset acceptedAt = DateTimeOffset.Parse(
            subject["created_at"]!.GetValue<string>(),
            CultureInfo.InvariantCulture);
        string acceptanceDirectory = closure["acceptances"]!["directory"]!.GetValue<string>();
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
                sourceDocument = new()
                {
                    ["author_association"] = "MEMBER",
                    ["body"] = Encoding.UTF8.GetString(CanonicalBytes(acceptance)).TrimEnd('\n'),
                    ["created_at"] = acceptedAt.ToString(
                        "yyyy-MM-dd'T'HH:mm:ss'Z'",
                        CultureInfo.InvariantCulture),
                    ["html_url"] = "https://github.com/Hexalith/Hexalith.EventStore/issues/346#issuecomment-1",
                    ["id"] = 1,
                    ["issue_url"] = "https://api.github.com/repos/Hexalith/Hexalith.EventStore/issues/346",
                    ["performed_via_github_app"] = null,
                    ["updated_at"] = acceptedAt.ToString(
                        "yyyy-MM-dd'T'HH:mm:ss'Z'",
                        CultureInfo.InvariantCulture),
                    ["url"] = "https://api.github.com/repos/Hexalith/Hexalith.EventStore/issues/comments/1",
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
        return temporary;
    }

    private static void UpdateReceiptBinding(JsonObject closure, int index, string receiptPath)
    {
        JsonNode binding = closure["acceptances"]!["receipts"]![index].ShouldNotBeNull();
        binding["sha256"] = ComputeSha256(receiptPath);
        binding["size"] = new FileInfo(receiptPath).Length;
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
            (int controlExit, _, string controlError) = RunProcess(
                temporary,
                "python3",
                "tools/validate-corrected-deployed-runtime-parity.py",
                Path.Combine(packet, "closure.json"),
                "--packet-root",
                packet);
            controlExit.ShouldNotBe(0);
            controlError.ShouldContain("exactly three packet-bound receipts are required");

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

            exitCode.ShouldNotBe(0);
            error.ShouldContain("trusted live handler source does not match its pinned SHA-256");
            output.ShouldNotContain("untrusted-import-executed");
        }
        finally
        {
            if (Directory.Exists(temporary))
            {
                Directory.Delete(temporary, recursive: true);
            }
        }
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
