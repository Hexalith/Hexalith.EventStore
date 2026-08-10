using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

namespace Hexalith.EventStore.Contracts.Tests.Packaging;

/// <summary>
/// Verifies the FrontComposer Story 11.24 EventStore runtime-identity successor gate.
/// </summary>
public sealed class FrontComposerRuntimeIdentitySuccessorTests
{
    private const string BuildsCatalogSha = "a8a50859fa2f27f511a9470dfe1e3ae54d0ebc1a";
    private const string BuildsReleaseExecutionSha = "f75daebd4c522c081a6f62e274cf25e07971de69";
    private const string ConsumerScope = "Hexalith.FrontComposer Story 11.24";
    private const string DecisionRelativePath =
        "_bmad-output/implementation-artifacts/frontcomposer-11-24-runtime-identity-successor.md";
    private const string EvidenceRelativePath =
        "_bmad-output/implementation-artifacts/evidence/frontcomposer-story-11-24/" + SourceSha;
    private const string HistoricalBuildsGitlinkSha = "824d7ef100455423aabbcd399c8364074000b2e0";
    private const string ReleaseInventorySha = "6b0b70b856839d4117bcd969f6a2de0093c477c109cb79f3f2882b1f05effcae";
    private const string SourceSha = "bb94d93e9b84132cff83a38fba84f25455820d31";
    private const string SubjectSha = "9d074dfd0758a8934f122aab18659627dff1cf5d4c3e548b222cc0d79a881065";
    private const string Version = "3.91.1";

    private static readonly string[] RequiredReceiptFields =
    [
        "schema",
        "subject_sha256",
        "subject_frozen_at",
        "actor",
        "role",
        "decision",
        "source_sha",
        "version",
        "consumer_scope",
        "accepted_at",
        "durable_source",
        "statement",
    ];

    private static readonly string[] RequiredRoles =
    [
        "eventstore-owner",
        "release-owner",
    ];

    [Fact]
    public void EvidenceBundleExactTupleAndSignedFeedHashesPass()
    {
        string root = FindRepositoryRoot();
        JsonObject subject = ReadObject(Path.Combine(root, EvidenceRelativePath, "review-subject.json"));

        ComputeSha256(Path.Combine(root, EvidenceRelativePath, "review-subject.json")).ShouldBe(SubjectSha);
        ValidateSubject(subject).ShouldBeTrue();
        ValidateBoundEvidence(root, subject).ShouldBeTrue();
        ValidatePackageManifest(root).ShouldBeTrue();
        ValidateRestoreReceipt(root).ShouldBeTrue();
        ValidateProvenance(root).ShouldBeTrue();
        ValidateRoster(root).ShouldBeTrue();
    }

    [Fact]
    public void CompleteSuccessorWithTwoValidReceiptsAuthorizesOnlyBoundScope()
    {
        string root = FindRepositoryRoot();
        JsonObject subject = ReadObject(Path.Combine(root, EvidenceRelativePath, "review-subject.json"));
        JsonObject roster = ReadObject(Path.Combine(root, EvidenceRelativePath, "reviewer-roster.json"));
        JsonObject[] receipts =
        [
            CreateValidReceipt("eventstore-owner", 101),
            CreateValidReceipt("release-owner", 102),
        ];

        ValidateAuthorization(subject, roster, receipts, "available", true).ShouldBeTrue();
        JsonObject driftedScope = receipts[1].DeepClone().AsObject();
        driftedScope["consumer_scope"] = "Hexalith.Tenants Story 2.12";
        ValidateAuthorization(subject, roster, [receipts[0], driftedScope], "available", true)
            .ShouldBeFalse();
    }

    [Fact]
    public void ApprovalCheckpointWithoutReceiptsRemainsUnavailable()
    {
        string root = FindRepositoryRoot();
        JsonObject subject = ReadObject(Path.Combine(root, EvidenceRelativePath, "review-subject.json"));
        JsonObject roster = ReadObject(Path.Combine(root, EvidenceRelativePath, "reviewer-roster.json"));

        ValidateAuthorization(subject, roster, [], "unavailable", false).ShouldBeTrue();
        ValidateAuthorization(subject, roster, [], "available", true).ShouldBeFalse();
    }

    [Fact]
    public void DurableDecisionMatchesCapturedReceiptState()
    {
        string root = FindRepositoryRoot();
        JsonObject subject = ReadObject(Path.Combine(root, EvidenceRelativePath, "review-subject.json"));
        JsonObject roster = ReadObject(Path.Combine(root, EvidenceRelativePath, "reviewer-roster.json"));
        Dictionary<string, string> decision = ReadFrontmatter(Path.Combine(root, DecisionRelativePath));
        JsonObject[] receipts = LoadCurrentReceipts(root);

        bool receiptGatePassed = ValidateReceiptGate(subject, roster, receipts);
        decision["final_decision"].ShouldBe(receiptGatePassed ? "available" : "unavailable");
        bool.Parse(decision["authorize_consumer_migration"]).ShouldBe(receiptGatePassed);
    }

    [Theory]
    [InlineData("missing-eventstore-owner")]
    [InlineData("missing-release-owner")]
    [InlineData("late")]
    [InlineData("wrong-role")]
    [InlineData("wrong-actor")]
    [InlineData("missing-field")]
    [InlineData("duplicate-source")]
    public void ReceiptFailuresRemainNonAuthorizing(string mutation)
    {
        string root = FindRepositoryRoot();
        JsonObject subject = ReadObject(Path.Combine(root, EvidenceRelativePath, "review-subject.json"));
        JsonObject roster = ReadObject(Path.Combine(root, EvidenceRelativePath, "reviewer-roster.json"));
        List<JsonObject> receipts =
        [
            CreateValidReceipt("eventstore-owner", 101),
            CreateValidReceipt("release-owner", 102),
        ];

        switch (mutation)
        {
            case "missing-eventstore-owner":
                receipts.RemoveAt(0);
                break;
            case "missing-release-owner":
                receipts.RemoveAt(1);
                break;
            case "late":
                receipts[0]["accepted_at"] = "2026-08-10T07:06:11Z";
                break;
            case "wrong-role":
                receipts[0]["role"] = "test-architect";
                break;
            case "wrong-actor":
                receipts[0]["actor"] = "github:workflow-bot";
                break;
            case "missing-field":
                receipts[0].Remove("subject_sha256");
                break;
            case "duplicate-source":
                receipts[1]["durable_source"] = receipts[0]["durable_source"]!.GetValue<string>();
                break;
        }

        ValidateAuthorization(subject, roster, receipts, "available", true).ShouldBeFalse();
        ValidateAuthorization(subject, roster, receipts, "unavailable", false).ShouldBeTrue();
    }

    [Theory]
    [InlineData("source")]
    [InlineData("version")]
    [InlineData("package-hash")]
    [InlineData("catalog")]
    [InlineData("release-execution")]
    [InlineData("historical-gitlink")]
    [InlineData("subject")]
    public void IdentityAndEvidenceDriftFailClosed(string mutation)
    {
        string root = FindRepositoryRoot();
        JsonObject subject = ReadObject(Path.Combine(root, EvidenceRelativePath, "review-subject.json"));

        switch (mutation)
        {
            case "source":
                subject["candidate"]!["source_sha"] = new string('a', 40);
                ValidateSubject(subject).ShouldBeFalse();
                break;
            case "version":
                subject["candidate"]!["version"] = "3.91.2";
                ValidateSubject(subject).ShouldBeFalse();
                break;
            case "package-hash":
                JsonObject packages = ReadObject(Path.Combine(root, EvidenceRelativePath, "package-manifest.json"));
                packages["packages"]![0]!["sha256"] = new string('a', 64);
                ValidatePackageManifest(root, packages).ShouldBeFalse();
                break;
            case "catalog":
                subject["builds_identities"]!["catalog_exposure_sha"] = new string('a', 40);
                ValidateSubject(subject).ShouldBeFalse();
                break;
            case "release-execution":
                subject["builds_identities"]!["release_execution_sha"] = new string('a', 40);
                ValidateSubject(subject).ShouldBeFalse();
                break;
            case "historical-gitlink":
                subject["builds_identities"]!["historical_source_gitlink_sha"] = new string('a', 40);
                ValidateSubject(subject).ShouldBeFalse();
                break;
            case "subject":
                byte[] drifted = [.. File.ReadAllBytes(Path.Combine(root, EvidenceRelativePath, "review-subject.json")), (byte)'\n'];
                ComputeSha256(drifted).ShouldNotBe(SubjectSha);
                break;
        }
    }

    [Theory]
    [InlineData("retired-proof")]
    [InlineData("ancestry")]
    [InlineData("current-main")]
    [InlineData("release-success")]
    [InlineData("tenants-waiver")]
    public void ProhibitedSubstitutesGrantNoAuthority(string substitute)
    {
        string root = FindRepositoryRoot();
        JsonObject subject = ReadObject(Path.Combine(root, EvidenceRelativePath, "review-subject.json"));

        switch (substitute)
        {
            case "retired-proof":
                subject["candidate"]!["source_sha"] = "fa2d1c9910f8976553adb33dcdb1c9ff2ea75594";
                subject["candidate"]!["version"] = "999.1.20-proof.fa2d1c9910f8";
                break;
            case "ancestry":
                subject["candidate"]!["source_sha"] = "77a9a44200000000000000000000000000000000";
                break;
            case "current-main":
                subject["candidate"]!["source_sha"] = new string('f', 40);
                break;
            case "release-success":
                subject["bound_evidence"]!.AsArray().RemoveAt(0);
                break;
            case "tenants-waiver":
                subject["candidate"]!["consumer_scope"] = "Hexalith.Tenants Story 2.12";
                break;
        }

        ValidateSubject(subject).ShouldBeFalse();
    }

    private static string ComputeSha256(string path) => ComputeSha256(File.ReadAllBytes(path));

    private static string ComputeSha256(byte[] bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static JsonObject CreateValidReceipt(string role, int commentId)
    {
        string statement = role == "eventstore-owner"
            ? "I accept this exact EventStore source and signed NuGet.org package identity for Hexalith.FrontComposer Story 11.24 only."
            : "I authorize this exact EventStore source and signed NuGet.org package identity for migration by Hexalith.FrontComposer Story 11.24 only.";

        return new JsonObject
        {
            ["schema"] = "hexalith.eventstore.frontcomposer-runtime-acceptance.v1",
            ["subject_sha256"] = SubjectSha,
            ["subject_frozen_at"] = "2026-08-10T07:06:11Z",
            ["actor"] = "github:jpiquot",
            ["role"] = role,
            ["decision"] = "accepted",
            ["source_sha"] = SourceSha,
            ["version"] = Version,
            ["consumer_scope"] = ConsumerScope,
            ["accepted_at"] = "2026-08-10T08:00:00Z",
            ["durable_source"] = $"https://github.com/Hexalith/Hexalith.EventStore/issues/1#issuecomment-{commentId}",
            ["statement"] = statement,
        };
    }

    private static string FindRepositoryRoot()
    {
        foreach (string startPath in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }.Distinct())
        {
            DirectoryInfo? directory = new(startPath);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Hexalith.EventStore.slnx")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the Hexalith.EventStore repository root.");
    }

    private static bool HasExactProperties(JsonObject value, IEnumerable<string> expected) =>
        value.Select(property => property.Key)
            .Order(StringComparer.Ordinal)
            .SequenceEqual(expected.Order(StringComparer.Ordinal), StringComparer.Ordinal);

    private static JsonObject[] LoadCurrentReceipts(string root)
    {
        string receiptDirectory = Path.Combine(root, EvidenceRelativePath, "acceptances", SubjectSha);
        if (!Directory.Exists(receiptDirectory))
        {
            return [];
        }

        return Directory.GetFiles(receiptDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .Order(StringComparer.Ordinal)
            .Select(ReadObject)
            .ToArray();
    }

    private static JsonObject ReadObject(string path) =>
        JsonNode.Parse(File.ReadAllText(path))!.AsObject();

    private static Dictionary<string, string> ReadFrontmatter(string path)
    {
        string[] lines = File.ReadAllLines(path);
        lines[0].ShouldBe("---");
        Dictionary<string, string> values = new(StringComparer.Ordinal);
        for (int index = 1; index < lines.Length && lines[index] != "---"; index++)
        {
            int separator = lines[index].IndexOf(':', StringComparison.Ordinal);
            if (separator > 0)
            {
                values[lines[index][..separator]] = lines[index][(separator + 1)..]
                    .Trim()
                    .Trim('\'');
            }
        }

        return values;
    }

    private static bool ValidateAuthorization(
        JsonObject subject,
        JsonObject roster,
        IReadOnlyCollection<JsonObject> receipts,
        string finalDecision,
        bool authorizeConsumerMigration)
    {
        bool receiptGatePassed = ValidateReceiptGate(subject, roster, receipts);
        return receiptGatePassed
            ? finalDecision == "available" && authorizeConsumerMigration
            : finalDecision == "unavailable" && !authorizeConsumerMigration;
    }

    private static bool ValidateBoundEvidence(string root, JsonObject subject)
    {
        JsonArray boundEvidence = subject["bound_evidence"]!.AsArray();
        if (boundEvidence.Count != 5)
        {
            return false;
        }

        foreach (JsonNode? node in boundEvidence)
        {
            JsonObject entry = node!.AsObject();
            string relativePath = entry["path"]!.GetValue<string>();
            string path = Path.GetFullPath(Path.Combine(root, EvidenceRelativePath, relativePath));
            string evidenceRoot = Path.GetFullPath(Path.Combine(root, EvidenceRelativePath))
                + Path.DirectorySeparatorChar;
            if (!path.StartsWith(evidenceRoot, StringComparison.Ordinal)
                || !File.Exists(path)
                || ComputeSha256(path) != entry["sha256"]!.GetValue<string>())
            {
                return false;
            }
        }

        return true;
    }

    private static bool ValidatePackageManifest(string root, JsonObject? manifest = null)
    {
        manifest ??= ReadObject(Path.Combine(root, EvidenceRelativePath, "package-manifest.json"));
        JsonObject releaseInventory = ReadObject(Path.Combine(root, "tools/release-packages.json"));
        JsonArray expectedPackages = releaseInventory["packages"]!.AsArray();
        JsonArray packages = manifest["packages"]!.AsArray();
        string[] hashLines = File.ReadAllLines(Path.Combine(root, EvidenceRelativePath, "nuget-sha256.txt"));

        if (manifest["source_sha"]!.GetValue<string>() != SourceSha
            || manifest["version"]!.GetValue<string>() != Version
            || manifest["inventory"]!["sha256"]!.GetValue<string>() != ReleaseInventorySha
            || manifest["inventory"]!["package_count"]!.GetValue<int>() != 14
            || manifest["repository_signature"]!["verification"]!.GetValue<string>()
                != "passed for all 14 archives via dotnet nuget verify --all"
            || packages.Count != 14
            || hashLines.Length != 14)
        {
            return false;
        }

        HashSet<string> ids = new(StringComparer.Ordinal);
        HashSet<string> hashes = new(StringComparer.Ordinal);
        for (int index = 0; index < packages.Count; index++)
        {
            JsonObject package = packages[index]!.AsObject();
            JsonObject expected = expectedPackages[index]!.AsObject();
            string id = package["id"]!.GetValue<string>();
            string archive = package["archive"]!.GetValue<string>();
            string hash = package["sha256"]!.GetValue<string>();
            string[] hashParts = hashLines[index].Split("  ", StringSplitOptions.None);
            if (!ids.Add(id)
                || !hashes.Add(hash)
                || id != expected["id"]!.GetValue<string>()
                || package["project"]!.GetValue<string>() != expected["project"]!.GetValue<string>()
                || package["embedded_repository_commit"]!.GetValue<string>() != SourceSha
                || package["size"]!.GetValue<long>() <= 0
                || hash.Length != 64
                || hashParts.Length != 2
                || hashParts[0] != hash
                || hashParts[1] != archive)
            {
                return false;
            }
        }

        return packages.Count(package => package!["consumer_kind"]!.GetValue<string>() == "library") == 13
            && packages.Count(package => package!["consumer_kind"]!.GetValue<string>() == "dotnet-tool") == 1;
    }

    private static bool ValidateProvenance(string root)
    {
        JsonObject provenance = ReadObject(Path.Combine(root, EvidenceRelativePath, "release-catalog-provenance.json"));
        return provenance["candidate"]!["source_sha"]!.GetValue<string>() == SourceSha
            && provenance["candidate"]!["version"]!.GetValue<string>() == Version
            && provenance["candidate"]!["historical_builds_gitlink_sha"]!.GetValue<string>() == HistoricalBuildsGitlinkSha
            && provenance["exact_source_ci"]!["run_id"]!.GetValue<long>() == 30984920450
            && provenance["exact_source_ci"]!["conclusion"]!.GetValue<string>() == "success"
            && provenance["exact_source_release"]!["run_id"]!.GetValue<long>() == 30990565147
            && provenance["exact_source_release"]!["run_attempt"]!.GetValue<int>() == 1
            && provenance["exact_source_release"]!["head_sha"]!.GetValue<string>() == SourceSha
            && provenance["exact_source_release"]!["builds_execution_sha"]!.GetValue<string>() == BuildsReleaseExecutionSha
            && provenance["builds_catalog_exposure"]!["commit_sha"]!.GetValue<string>() == BuildsCatalogSha
            && provenance["builds_catalog_exposure"]!["exposed_version"]!.GetValue<string>() == Version
            && provenance["builds_catalog_exposure"]!["cataloged_package_count"]!.GetValue<int>() == 13
            && provenance["builds_catalog_exposure"]!["manifest_only_package"]!.GetValue<string>() == "Hexalith.EventStore.Admin.Cli"
            && provenance["distinct_builds_runner_schema_candidate"]!["version"]!.GetValue<string>() == "3.88.0";
    }

    private static bool ValidateReceipt(
        JsonObject receipt,
        string expectedRole,
        JsonObject subject,
        JsonObject roster)
    {
        if (!HasExactProperties(receipt, RequiredReceiptFields)
            || receipt["schema"]!.GetValue<string>() != "hexalith.eventstore.frontcomposer-runtime-acceptance.v1"
            || receipt["subject_sha256"]!.GetValue<string>() != SubjectSha
            || receipt["subject_frozen_at"]!.GetValue<string>() != subject["frozen_at"]!.GetValue<string>()
            || receipt["role"]!.GetValue<string>() != expectedRole
            || receipt["decision"]!.GetValue<string>() != "accepted"
            || receipt["source_sha"]!.GetValue<string>() != SourceSha
            || receipt["version"]!.GetValue<string>() != Version
            || receipt["consumer_scope"]!.GetValue<string>() != ConsumerScope)
        {
            return false;
        }

        string actor = receipt["actor"]!.GetValue<string>();
        if (!roster["roles"]![expectedRole]!.AsArray()
                .Any(node => node!.GetValue<string>() == actor))
        {
            return false;
        }

        if (!DateTimeOffset.TryParse(
                subject["frozen_at"]!.GetValue<string>(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out DateTimeOffset frozenAt)
            || !DateTimeOffset.TryParse(
                receipt["accepted_at"]!.GetValue<string>(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out DateTimeOffset acceptedAt)
            || acceptedAt <= frozenAt)
        {
            return false;
        }

        string durableSource = receipt["durable_source"]!.GetValue<string>();
        if (!Uri.TryCreate(durableSource, UriKind.Absolute, out Uri? receiptUri)
            || receiptUri.Scheme != Uri.UriSchemeHttps
            || receiptUri.Host != "github.com"
            || !receiptUri.AbsolutePath.StartsWith(
                "/Hexalith/Hexalith.EventStore/issues/",
                StringComparison.Ordinal)
            || !receiptUri.Fragment.StartsWith("#issuecomment-", StringComparison.Ordinal))
        {
            return false;
        }

        string expectedStatement = expectedRole == "eventstore-owner"
            ? "I accept this exact EventStore source and signed NuGet.org package identity for Hexalith.FrontComposer Story 11.24 only."
            : "I authorize this exact EventStore source and signed NuGet.org package identity for migration by Hexalith.FrontComposer Story 11.24 only.";
        return receipt["statement"]!.GetValue<string>() == expectedStatement;
    }

    private static bool ValidateReceiptGate(
        JsonObject subject,
        JsonObject roster,
        IReadOnlyCollection<JsonObject> receipts)
    {
        if (!ValidateSubject(subject)
            || !ValidateRoster(roster)
            || receipts.Count != RequiredRoles.Length)
        {
            return false;
        }

        string[] durableSources = receipts
            .Select(receipt => receipt["durable_source"]?.GetValue<string>() ?? string.Empty)
            .ToArray();
        if (durableSources.Distinct(StringComparer.Ordinal).Count() != RequiredRoles.Length)
        {
            return false;
        }

        return RequiredRoles.All(role =>
        {
            JsonObject[] roleReceipts = receipts
                .Where(receipt => receipt["role"]?.GetValue<string>() == role)
                .ToArray();
            return roleReceipts.Length == 1 && ValidateReceipt(roleReceipts[0], role, subject, roster);
        });
    }

    private static bool ValidateRestoreReceipt(string root)
    {
        JsonObject receipt = ReadObject(Path.Combine(root, EvidenceRelativePath, "restore-receipt.json"));
        return receipt["source_sha"]!.GetValue<string>() == SourceSha
            && receipt["version"]!.GetValue<string>() == Version
            && receipt["retrieval"]!["fresh_download"]!.GetValue<bool>()
            && receipt["retrieval"]!["archive_count"]!.GetValue<int>() == 14
            && receipt["signature_verification"]!["verified_count"]!.GetValue<int>() == 14
            && receipt["signature_verification"]!["result"]!.GetValue<string>() == "passed"
            && receipt["inventory_validation"]!["validated_count"]!.GetValue<int>() == 14
            && receipt["inventory_validation"]!["result"]!.GetValue<string>() == "passed"
            && receipt["consumer_validation"]!["fresh_per_consumer_package_cache"]!.GetValue<bool>()
            && !receipt["consumer_validation"]!["project_edges_allowed"]!.GetValue<bool>()
            && receipt["consumer_validation"]!["library_consumers_passed"]!.GetValue<int>() == 13
            && receipt["consumer_validation"]!["tool_consumers_passed"]!.GetValue<int>() == 1
            && receipt["consumer_validation"]!["failed"]!.GetValue<int>() == 0
            && receipt["consumer_validation"]!["skipped"]!.GetValue<int>() == 0
            && receipt["consumer_validation"]!["result"]!.GetValue<string>() == "passed";
    }

    private static bool ValidateRoster(string root) =>
        ValidateRoster(ReadObject(Path.Combine(root, EvidenceRelativePath, "reviewer-roster.json")));

    private static bool ValidateRoster(JsonObject roster) =>
        roster["consumer_scope"]!.GetValue<string>() == ConsumerScope
        && RequiredRoles.All(role =>
            roster["roles"]![role]!.AsArray()
                .Select(node => node!.GetValue<string>())
                .SequenceEqual(["github:jpiquot"], StringComparer.Ordinal));

    private static bool ValidateSubject(JsonObject subject)
    {
        if (!HasExactProperties(
                subject,
                ["schema", "subject_id", "frozen_at", "candidate", "builds_identities", "bound_evidence", "approval_gate", "final_record_contract", "limitations"]))
        {
            return false;
        }

        JsonObject candidate = subject["candidate"]!.AsObject();
        JsonObject builds = subject["builds_identities"]!.AsObject();
        JsonObject approval = subject["approval_gate"]!.AsObject();
        return subject["schema"]!.GetValue<string>() == "hexalith.eventstore.frontcomposer-runtime-review-subject.v1"
            && candidate["source_sha"]!.GetValue<string>() == SourceSha
            && candidate["tag"]!.GetValue<string>() == "v3.91.1"
            && candidate["version"]!.GetValue<string>() == Version
            && candidate["consumer_scope"]!.GetValue<string>() == ConsumerScope
            && candidate["package_count"]!.GetValue<int>() == 14
            && subject["bound_evidence"]!.AsArray().Count == 5
            && builds["catalog_exposure_sha"]!.GetValue<string>() == BuildsCatalogSha
            && builds["release_execution_sha"]!.GetValue<string>() == BuildsReleaseExecutionSha
            && builds["historical_source_gitlink_sha"]!.GetValue<string>() == HistoricalBuildsGitlinkSha
            && builds["runner_schema_candidate_version"]!.GetValue<string>() == "3.88.0"
            && approval["required_roles"]!.AsArray()
                .Select(node => node!.GetValue<string>())
                .SequenceEqual(RequiredRoles, StringComparer.Ordinal)
            && approval["required_receipt_fields"]!.AsArray()
                .Select(node => node!.GetValue<string>())
                .SequenceEqual(RequiredReceiptFields, StringComparer.Ordinal)
            && approval["authorized_actor"]!.GetValue<string>() == "github:jpiquot"
            && approval["required_decision"]!.GetValue<string>() == "accepted";
    }
}
