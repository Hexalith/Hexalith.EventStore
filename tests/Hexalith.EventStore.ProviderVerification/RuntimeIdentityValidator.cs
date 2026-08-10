using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Controllers;

namespace Hexalith.EventStore.ProviderVerification;

internal static class RuntimeIdentityValidator
{
    private static readonly string[] RequiredApprovalRoles = ["eventstore-owner", "release-owner"];
    private static readonly string[] RequiredReceiptFields =
    [
        "schema", "subject_sha256", "subject_frozen_at", "actor", "role", "decision",
        "source_sha", "version", "consumer_scope", "accepted_at", "durable_source", "statement",
    ];

    public static IdentityEvidence Validate(
        string identityRecordPath,
        string evidenceDirectory,
        string repositoryRoot,
        ICollection<InputHash> hashes)
    {
        Dictionary<string, string> decision = ReadFrontmatter(identityRecordPath);
        string expectedSource = RequireHash(decision, "source_sha", 40);
        string expectedVersion = Required(decision, "version");
        string subjectHash = RequireHash(decision, "subject_sha256", 64);
        DateTimeOffset recordedAt = RequireExplicitTimestamp(
            Required(decision, "recorded_at"),
            "identity.decision.timestamp-invalid");
        if (Required(decision, "schema") != "hexalith.eventstore.frontcomposer-runtime-decision.v1")
        {
            throw new ProviderVerificationInputException("identity.decision.schema-invalid");
        }

        string subjectPath = Path.Combine(evidenceDirectory, "review-subject.json");
        string packageManifestPath = Path.Combine(evidenceDirectory, "package-manifest.json");
        string provenancePath = Path.Combine(evidenceDirectory, "release-catalog-provenance.json");
        string rosterPath = Path.Combine(evidenceDirectory, "reviewer-roster.json");
        foreach (string path in new[] { subjectPath, packageManifestPath, provenancePath, rosterPath })
        {
            if (!SafePath.TryResolveExistingFile(path, 2 * 1024 * 1024, out _, out string code))
            {
                throw new ProviderVerificationInputException(code);
            }
        }

        if (VerificationInputLoader.ComputeSha256(subjectPath) != subjectHash)
        {
            throw new ProviderVerificationInputException("identity.subject.hash-mismatch");
        }

        using JsonDocument subjectDocument = JsonInput.Read(subjectPath, 512 * 1024);
        JsonElement subject = subjectDocument.RootElement;
        JsonInput.RequireExactProperties(
            subject,
            "schema", "subject_id", "frozen_at", "candidate", "builds_identities", "bound_evidence",
            "approval_gate", "final_record_contract", "limitations");
        JsonElement candidate = subject.GetProperty("candidate");
        string subjectSource = RequiredHash(candidate, "source_sha", 40);
        string subjectVersion = JsonInput.RequiredString(candidate, "version");
        string subjectTag = JsonInput.RequiredString(candidate, "tag");
        string consumerScope = JsonInput.RequiredString(candidate, "consumer_scope");
        int subjectPackageCount = JsonInput.RequiredInt32(candidate, "package_count");
        string subjectPackageHashDomain = JsonInput.RequiredString(candidate, "package_hash_domain", 512);
        string subjectFrozenAt = JsonInput.RequiredString(subject, "frozen_at");
        DateTimeOffset subjectFrozenAtValue = RequireExplicitTimestamp(
            subjectFrozenAt,
            "identity.subject.timestamp-invalid");
        JsonElement buildsIdentities = JsonInput.RequiredObject(subject, "builds_identities");
        string expectedBuilds = RequiredHash(
            buildsIdentities,
            "catalog_exposure_sha",
            40);
        string expectedBuildsReleaseExecution = RequiredHash(buildsIdentities, "release_execution_sha", 40);
        string expectedHistoricalBuildsGitlink = RequiredHash(buildsIdentities, "historical_source_gitlink_sha", 40);
        if (JsonInput.RequiredString(subject, "schema") != "hexalith.eventstore.frontcomposer-runtime-review-subject.v1"
            || subjectSource != expectedSource
            || subjectVersion != expectedVersion
            || subjectTag != Required(decision, "tag")
            || consumerScope != Required(decision, "consumer_scope"))
        {
            throw new ProviderVerificationInputException("identity.subject.tuple-mismatch");
        }

        if (recordedAt < subjectFrozenAtValue)
        {
            throw new ProviderVerificationInputException("identity.decision.order-invalid");
        }

        foreach (JsonElement item in subject.GetProperty("bound_evidence").EnumerateArray())
        {
            JsonInput.RequireExactProperties(item, "path", "sha256");
            string name = JsonInput.RequiredString(item, "path");
            if (!string.Equals(Path.GetFileName(name), name, StringComparison.Ordinal))
            {
                throw new ProviderVerificationInputException("identity.evidence.path-invalid");
            }

            string evidencePath = Path.Combine(evidenceDirectory, name);
            string expectedHash = RequiredHash(item, "sha256", 64);
            if (!SafePath.TryResolveExistingFile(evidencePath, 2 * 1024 * 1024, out evidencePath, out string code)
                || VerificationInputLoader.ComputeSha256(evidencePath) != expectedHash)
            {
                throw new ProviderVerificationInputException(code.Length == 0 ? "identity.evidence.hash-mismatch" : code);
            }

            hashes.Add(new InputHash("identity-evidence", name, expectedHash));
        }

        using JsonDocument packageDocument = JsonInput.Read(packageManifestPath, 2 * 1024 * 1024);
        JsonElement packageManifest = packageDocument.RootElement;
        string releaseInventoryHash = ValidatePackageManifest(
            packageManifest,
            subjectSource,
            subjectTag,
            subjectVersion,
            subjectPackageCount,
            subjectPackageHashDomain);
        using JsonDocument provenanceDocument = JsonInput.Read(provenancePath, 2 * 1024 * 1024);
        ValidateProvenance(
            provenanceDocument.RootElement,
            subjectSource,
            subjectTag,
            subjectVersion,
            releaseInventoryHash,
            expectedBuilds,
            expectedBuildsReleaseExecution,
            expectedHistoricalBuildsGitlink,
            subjectPackageCount);
        string evidenceManifestHash = VerificationInputLoader.ComputeSha256(packageManifestPath);
        string observedSource = RunGit(repositoryRoot, "rev-parse", "HEAD");
        string observedVersion = ReadRuntimeVersion();
        bool providerWorktreeClean = IsProviderWorktreeClean(repositoryRoot);
        (string observedBuilds, bool buildsWorktreeClean) = FindBuildsIdentity(repositoryRoot);
        string inventoryPath = Path.Combine(repositoryRoot, "tools", "release-packages.json");
        string observedInventoryHash = File.Exists(inventoryPath)
            ? VerificationInputLoader.ComputeSha256(inventoryPath)
            : string.Empty;

        (int approvalCount, bool receiptGatePassed) = ValidateApprovalReceipts(
            evidenceDirectory,
            subjectHash,
            subjectFrozenAt,
            subjectFrozenAtValue,
            expectedSource,
            expectedVersion,
            consumerScope,
            rosterPath,
            hashes);
        bool decisionAuthorizes = string.Equals(Required(decision, "final_decision"), "available", StringComparison.Ordinal)
            && string.Equals(Required(decision, "authorize_consumer_migration"), "true", StringComparison.Ordinal);
        bool approvalAuthorized = decisionAuthorizes && receiptGatePassed;
        bool runtimeMatches = IsRuntimeMatch(
            expectedSource,
            observedSource,
            expectedVersion,
            observedVersion,
            expectedBuilds,
            observedBuilds,
            releaseInventoryHash,
            observedInventoryHash,
            providerWorktreeClean,
            buildsWorktreeClean);
        var reasons = new List<string>();
        if (!approvalAuthorized)
        {
            reasons.Add("identity.approval.unavailable");
        }

        if (expectedSource != observedSource)
        {
            reasons.Add("identity.source.mismatch");
        }

        if (!VersionMatches(expectedVersion, observedVersion, observedSource))
        {
            reasons.Add("identity.version.mismatch");
        }

        if (expectedBuilds != observedBuilds || !buildsWorktreeClean)
        {
            reasons.Add("identity.builds.mismatch");
        }

        if (!providerWorktreeClean)
        {
            reasons.Add("identity.runtime-worktree.dirty");
        }

        if (!buildsWorktreeClean)
        {
            reasons.Add("identity.builds-worktree.dirty");
        }

        if (releaseInventoryHash != observedInventoryHash)
        {
            reasons.Add("identity.release-inventory.mismatch");
        }

        return new IdentityEvidence(
            expectedSource,
            observedSource,
            expectedVersion,
            observedVersion,
            expectedBuilds,
            observedBuilds,
            releaseInventoryHash,
            observedInventoryHash,
            evidenceManifestHash,
            VerificationInputLoader.ComputeSha256(identityRecordPath),
            subjectHash,
            approvalCount,
            approvalAuthorized,
            runtimeMatches,
            reasons);
    }

    internal static string ValidatePackageManifest(
        JsonElement packageManifest,
        string expectedSource,
        string expectedTag,
        string expectedVersion,
        int expectedPackageCount,
        string expectedHashDomain)
    {
        JsonInput.RequireExactProperties(
            packageManifest,
            "schema", "captured_at", "source_sha", "tag", "version", "hash_algorithm", "hash_domain",
            "repository_signature", "inventory", "packages");
        if (JsonInput.RequiredString(packageManifest, "schema")
                != "hexalith.eventstore.frontcomposer-runtime-packages.v1"
            || RequiredHash(packageManifest, "source_sha", 40) != expectedSource
            || JsonInput.RequiredString(packageManifest, "tag") != expectedTag
            || JsonInput.RequiredString(packageManifest, "version") != expectedVersion
            || JsonInput.RequiredString(packageManifest, "hash_algorithm") != "SHA-256"
            || JsonInput.RequiredString(packageManifest, "hash_domain", 512) != expectedHashDomain)
        {
            throw new ProviderVerificationInputException("identity.package.tuple-mismatch");
        }

        _ = RequireExplicitTimestamp(
            JsonInput.RequiredString(packageManifest, "captured_at"),
            "identity.package.timestamp-invalid");
        JsonElement inventory = JsonInput.RequiredObject(packageManifest, "inventory");
        JsonInput.RequireExactProperties(
            inventory,
            "path", "sha256", "package_count", "library_count", "tool_count");
        string inventoryHash = RequiredHash(inventory, "sha256", 64);
        int packageCount = JsonInput.RequiredInt32(inventory, "package_count");
        int libraryCount = JsonInput.RequiredInt32(inventory, "library_count");
        int toolCount = JsonInput.RequiredInt32(inventory, "tool_count");
        JsonElement packages = JsonInput.RequiredArray(packageManifest, "packages");
        if (JsonInput.RequiredString(inventory, "path") != "tools/release-packages.json"
            || expectedPackageCount <= 0
            || packageCount != expectedPackageCount
            || packages.GetArrayLength() != expectedPackageCount
            || libraryCount < 0
            || toolCount < 0
            || libraryCount + toolCount != expectedPackageCount)
        {
            throw new ProviderVerificationInputException("identity.package.count-mismatch");
        }

        var packageIds = new HashSet<string>(StringComparer.Ordinal);
        int observedLibraries = 0;
        int observedTools = 0;
        foreach (JsonElement package in packages.EnumerateArray())
        {
            JsonInput.RequireExactProperties(
                package,
                "id", "project", "archive", "nuget_url", "size", "sha256",
                "embedded_repository_commit", "consumer_kind");
            string id = JsonInput.RequiredString(package, "id");
            string project = JsonInput.RequiredString(package, "project", 512);
            string archive = JsonInput.RequiredString(package, "archive", 512);
            string consumerKind = JsonInput.RequiredString(package, "consumer_kind");
            int size = JsonInput.RequiredInt32(package, "size");
            if (!packageIds.Add(id)
                || !project.StartsWith("src/", StringComparison.Ordinal)
                || project.Contains("..", StringComparison.Ordinal)
                || archive != $"{id}.{expectedVersion}.nupkg"
                || size <= 0
                || RequiredHash(package, "sha256", 64).Length != 64
                || RequiredHash(package, "embedded_repository_commit", 40) != expectedSource)
            {
                throw new ProviderVerificationInputException("identity.package.entry-invalid");
            }

            switch (consumerKind)
            {
                case "library":
                    observedLibraries++;
                    break;
                case "dotnet-tool":
                    observedTools++;
                    break;
                default:
                    throw new ProviderVerificationInputException("identity.package.entry-invalid");
            }
        }

        if (observedLibraries != libraryCount || observedTools != toolCount)
        {
            throw new ProviderVerificationInputException("identity.package.count-mismatch");
        }

        return inventoryHash;
    }

    internal static void ValidateProvenance(
        JsonElement provenance,
        string expectedSource,
        string expectedTag,
        string expectedVersion,
        string expectedInventoryHash,
        string expectedBuilds,
        string expectedBuildsReleaseExecution,
        string expectedHistoricalBuildsGitlink,
        int expectedPackageCount)
    {
        JsonInput.RequireExactProperties(
            provenance,
            "schema", "captured_at", "candidate", "exact_source_ci", "exact_source_release",
            "builds_catalog_exposure", "distinct_builds_runner_schema_candidate", "rejected_bases");
        if (JsonInput.RequiredString(provenance, "schema")
            != "hexalith.eventstore.frontcomposer-runtime-provenance.v1")
        {
            throw new ProviderVerificationInputException("identity.provenance.schema-invalid");
        }

        _ = RequireExplicitTimestamp(
            JsonInput.RequiredString(provenance, "captured_at"),
            "identity.provenance.timestamp-invalid");
        JsonElement candidate = JsonInput.RequiredObject(provenance, "candidate");
        JsonInput.RequireExactProperties(
            candidate,
            "source_sha", "tag", "version", "release_inventory_sha256", "historical_builds_gitlink_sha");
        JsonElement sourceCi = JsonInput.RequiredObject(provenance, "exact_source_ci");
        JsonElement sourceRelease = JsonInput.RequiredObject(provenance, "exact_source_release");
        JsonElement catalog = JsonInput.RequiredObject(provenance, "builds_catalog_exposure");
        if (RequiredHash(candidate, "source_sha", 40) != expectedSource
            || JsonInput.RequiredString(candidate, "tag") != expectedTag
            || JsonInput.RequiredString(candidate, "version") != expectedVersion
            || RequiredHash(candidate, "release_inventory_sha256", 64) != expectedInventoryHash
            || RequiredHash(candidate, "historical_builds_gitlink_sha", 40) != expectedHistoricalBuildsGitlink
            || JsonInput.RequiredString(sourceCi, "repository") != "Hexalith/Hexalith.EventStore"
            || RequiredHash(sourceCi, "head_sha", 40) != expectedSource
            || JsonInput.RequiredString(sourceCi, "conclusion") != "success"
            || JsonInput.RequiredString(sourceRelease, "repository") != "Hexalith/Hexalith.EventStore"
            || RequiredHash(sourceRelease, "head_sha", 40) != expectedSource
            || JsonInput.RequiredString(sourceRelease, "conclusion") != "success"
            || JsonInput.RequiredString(sourceRelease, "release_tag") != expectedTag
            || RequiredHash(sourceRelease, "builds_execution_sha", 40) != expectedBuildsReleaseExecution
            || JsonInput.RequiredString(catalog, "repository") != "Hexalith/Hexalith.Builds"
            || RequiredHash(catalog, "commit_sha", 40) != expectedBuilds
            || JsonInput.RequiredString(catalog, "exposed_version") != expectedVersion
            || JsonInput.RequiredInt32(catalog, "cataloged_package_count") != expectedPackageCount - 1
            || JsonInput.RequiredString(catalog, "manifest_only_package") != "Hexalith.EventStore.Admin.Cli")
        {
            throw new ProviderVerificationInputException("identity.provenance.tuple-mismatch");
        }

        JsonElement catalogedPackages = JsonInput.RequiredArray(catalog, "cataloged_packages");
        string[] packageIds = catalogedPackages.EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() ?? string.Empty : string.Empty)
            .ToArray();
        if (packageIds.Length != expectedPackageCount - 1
            || packageIds.Any(string.IsNullOrWhiteSpace)
            || packageIds.Distinct(StringComparer.Ordinal).Count() != packageIds.Length)
        {
            throw new ProviderVerificationInputException("identity.provenance.catalog-invalid");
        }
    }

    private static Dictionary<string, string> ReadFrontmatter(string path)
    {
        string[] lines = File.ReadAllLines(path);
        if (lines.Length < 3 || lines[0] != "---")
        {
            throw new ProviderVerificationInputException("identity.decision.malformed");
        }

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        int index = 1;
        for (; index < lines.Length && lines[index] != "---"; index++)
        {
            int separator = lines[index].IndexOf(':', StringComparison.Ordinal);
            if (separator <= 0
                || !result.TryAdd(
                    lines[index][..separator].Trim(),
                    lines[index][(separator + 1)..].Trim().Trim('\'')))
            {
                throw new ProviderVerificationInputException("identity.decision.malformed");
            }
        }

        string[] required =
        [
            "schema", "recorded_at", "subject_sha256", "source_sha", "tag", "version",
            "consumer_scope", "final_decision", "authorize_consumer_migration",
        ];
        if (index == lines.Length || !result.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(required))
        {
            throw new ProviderVerificationInputException("identity.decision.extra-or-missing-field");
        }

        return result;
    }

    private static string Required(IReadOnlyDictionary<string, string> values, string key)
        => values.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ProviderVerificationInputException("identity.decision.value-invalid");

    private static string RequireHash(IReadOnlyDictionary<string, string> values, string key, int length)
    {
        string value = Required(values, key);
        return IsLowercaseHash(value, length)
            ? value
            : throw new ProviderVerificationInputException("identity.decision.hash-invalid");
    }

    private static string RequiredHash(JsonElement element, string propertyName, int length)
    {
        string value = JsonInput.RequiredString(element, propertyName);
        return IsLowercaseHash(value, length)
            ? value
            : throw new ProviderVerificationInputException("identity.evidence.hash-invalid");
    }

    private static bool IsLowercaseHash(string value, int length)
        => value.Length == length
            && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static (int Count, bool Valid) ValidateApprovalReceipts(
        string evidenceDirectory,
        string subjectHash,
        string subjectFrozenAt,
        DateTimeOffset subjectFrozenAtValue,
        string expectedSource,
        string expectedVersion,
        string consumerScope,
        string rosterPath,
        ICollection<InputHash> hashes)
    {
        string directory = Path.Combine(evidenceDirectory, "acceptances", subjectHash);
        if (!Directory.Exists(directory))
        {
            return (0, false);
        }

        if (!SafePath.TryResolveExistingDirectory(directory, out directory, out _))
        {
            return (0, false);
        }

        string[] files = Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly);
        if (files.Length != RequiredApprovalRoles.Length)
        {
            return (files.Length, false);
        }

        using JsonDocument rosterDocument = JsonInput.Read(rosterPath, 512 * 1024);
        JsonElement roster = rosterDocument.RootElement;
        JsonInput.RequireExactProperties(roster, "schema", "frozen_at", "repository", "consumer_scope", "roles", "receipt_policy");
        if (JsonInput.RequiredString(roster, "schema") != "hexalith.eventstore.frontcomposer-runtime-reviewer-roster.v1"
            || JsonInput.RequiredString(roster, "consumer_scope") != consumerScope
            || !TryParseExplicitTimestamp(
                JsonInput.RequiredString(roster, "frozen_at"),
                out DateTimeOffset rosterFrozenAt)
            || rosterFrozenAt > subjectFrozenAtValue)
        {
            return (files.Length, false);
        }

        var validRoles = new HashSet<string>(StringComparer.Ordinal);
        var durableSources = new HashSet<string>(StringComparer.Ordinal);
        foreach (string file in files.Order(StringComparer.Ordinal))
        {
            if (!SafePath.TryResolveExistingFile(file, 128 * 1024, out string receiptPath, out _))
            {
                return (files.Length, false);
            }

            hashes.Add(new InputHash("identity-approval", Path.GetFileName(receiptPath), VerificationInputLoader.ComputeSha256(receiptPath)));
            using JsonDocument receiptDocument = JsonInput.Read(receiptPath, 128 * 1024);
            JsonElement receipt = receiptDocument.RootElement;
            JsonInput.RequireExactProperties(receipt, RequiredReceiptFields);
            string role = JsonInput.RequiredString(receipt, "role");
            string actor = JsonInput.RequiredString(receipt, "actor");
            string durableSource = JsonInput.RequiredString(receipt, "durable_source", 1024);
            if (!RequiredApprovalRoles.Contains(role, StringComparer.Ordinal)
                || !validRoles.Add(role)
                || !durableSources.Add(durableSource)
                || !ReceiptTupleMatches(
                    receipt,
                    role,
                    subjectHash,
                    subjectFrozenAt,
                    subjectFrozenAtValue,
                    expectedSource,
                    expectedVersion,
                    consumerScope)
                || !RosterAuthorizes(roster, role, actor))
            {
                return (files.Length, false);
            }
        }

        return (files.Length, validRoles.SetEquals(RequiredApprovalRoles));
    }

    private static bool ReceiptTupleMatches(
        JsonElement receipt,
        string role,
        string subjectHash,
        string subjectFrozenAt,
        DateTimeOffset subjectFrozenAtValue,
        string expectedSource,
        string expectedVersion,
        string consumerScope)
    {
        if (JsonInput.RequiredString(receipt, "schema") != "hexalith.eventstore.frontcomposer-runtime-acceptance.v1"
            || RequiredHash(receipt, "subject_sha256", 64) != subjectHash
            || JsonInput.RequiredString(receipt, "subject_frozen_at") != subjectFrozenAt
            || JsonInput.RequiredString(receipt, "decision") != "accepted"
            || RequiredHash(receipt, "source_sha", 40) != expectedSource
            || JsonInput.RequiredString(receipt, "version") != expectedVersion
            || JsonInput.RequiredString(receipt, "consumer_scope") != consumerScope
            || !TryParseExplicitTimestamp(
                JsonInput.RequiredString(receipt, "accepted_at"),
                out DateTimeOffset acceptedAt)
            || acceptedAt <= subjectFrozenAtValue
            || acceptedAt > DateTimeOffset.UtcNow)
        {
            return false;
        }

        string durableSource = JsonInput.RequiredString(receipt, "durable_source", 1024);
        if (!Uri.TryCreate(durableSource, UriKind.Absolute, out Uri? receiptUri)
            || !IsExactIssueCommentReceipt(receiptUri))
        {
            return false;
        }

        string expectedStatement = role == "eventstore-owner"
            ? "I accept this exact EventStore source and signed NuGet.org package identity for Hexalith.FrontComposer Story 11.24 only."
            : "I authorize this exact EventStore source and signed NuGet.org package identity for migration by Hexalith.FrontComposer Story 11.24 only.";
        return JsonInput.RequiredString(receipt, "statement", 512) == expectedStatement;
    }

    private static bool IsExactIssueCommentReceipt(Uri receiptUri)
    {
        string[] segments = receiptUri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        const string commentPrefix = "#issuecomment-";
        if (segments.Length != 4
            || segments[0] != "Hexalith"
            || segments[1] != "Hexalith.EventStore"
            || segments[2] != "issues"
            || !IsPositiveDecimal(segments[3])
            || !receiptUri.Fragment.StartsWith(commentPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        string commentId = receiptUri.Fragment[commentPrefix.Length..];
        return IsPositiveDecimal(commentId)
            && receiptUri.OriginalString == $"https://github.com/Hexalith/Hexalith.EventStore/issues/{segments[3]}#issuecomment-{commentId}";
    }

    private static bool IsPositiveDecimal(string value)
        => value.Length > 0
            && value[0] is >= '1' and <= '9'
            && value.Skip(1).All(character => character is >= '0' and <= '9');

    private static DateTimeOffset RequireExplicitTimestamp(string value, string failureCode)
        => TryParseExplicitTimestamp(value, out DateTimeOffset timestamp)
            ? timestamp
            : throw new ProviderVerificationInputException(failureCode);

    private static bool TryParseExplicitTimestamp(string value, out DateTimeOffset timestamp)
    {
        timestamp = default;
        bool hasExplicitOffset = value.EndsWith('Z')
            || (value.Length >= 6
                && value[^6] is '+' or '-'
                && char.IsAsciiDigit(value[^5])
                && char.IsAsciiDigit(value[^4])
                && value[^3] == ':'
                && char.IsAsciiDigit(value[^2])
                && char.IsAsciiDigit(value[^1]));
        return hasExplicitOffset
            && value.Contains('T', StringComparison.Ordinal)
            && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out timestamp);
    }

    private static bool RosterAuthorizes(JsonElement roster, string role, string actor)
    {
        JsonElement roles = roster.GetProperty("roles");
        return roles.TryGetProperty(role, out JsonElement actors)
            && actors.ValueKind == JsonValueKind.Array
            && actors.EnumerateArray().Any(item => item.ValueKind == JsonValueKind.String && item.GetString() == actor);
    }

    private static string ReadRuntimeVersion()
    {
        Assembly assembly = typeof(CommandsController).Assembly;
        string? informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return informational ?? assembly.GetName().Version?.ToString() ?? string.Empty;
    }

    internal static bool VersionMatches(string expected, string observed, string observedSource)
    {
        if (string.Equals(observed, expected, StringComparison.Ordinal))
        {
            return true;
        }

        string prefix = expected + "+";
        return observed.StartsWith(prefix, StringComparison.Ordinal)
            && string.Equals(observed[prefix.Length..], observedSource, StringComparison.Ordinal);
    }

    internal static bool IsRuntimeMatch(
        string expectedSource,
        string observedSource,
        string expectedVersion,
        string observedVersion,
        string expectedBuilds,
        string observedBuilds,
        string expectedInventoryHash,
        string observedInventoryHash,
        bool providerWorktreeClean,
        bool buildsWorktreeClean)
        => expectedSource == observedSource
            && VersionMatches(expectedVersion, observedVersion, observedSource)
            && expectedBuilds == observedBuilds
            && expectedInventoryHash == observedInventoryHash
            && providerWorktreeClean
            && buildsWorktreeClean;

    internal static bool IsProviderWorktreeClean(string repositoryRoot)
        => RunGit(
            repositoryRoot,
            "status",
            "--porcelain=v1",
            "--untracked-files=all",
            "--",
            "src",
            "Directory.Build.props",
            "Directory.Build.targets",
            "Directory.Packages.props",
            "global.json").Length == 0;

    internal static bool IsGitWorktreeClean(string repositoryRoot)
        => RunGit(
            repositoryRoot,
            "status",
            "--porcelain=v1",
            "--untracked-files=all",
            "--",
            ".").Length == 0;

    private static (string Sha, bool IsClean) FindBuildsIdentity(string repositoryRoot)
    {
        string[] candidates =
        [
            Path.Combine(repositoryRoot, "references", "Hexalith.Builds"),
            Path.Combine(repositoryRoot, "..", "Hexalith.Builds"),
            Path.Combine(repositoryRoot, "..", "..", "references", "Hexalith.Builds"),
        ];
        foreach (string candidate in candidates)
        {
            if (Directory.Exists(candidate))
            {
                string expectedTopLevel = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate));
                string observedTopLevel = Path.TrimEndingDirectorySeparator(RunGit(candidate, "rev-parse", "--show-toplevel"));
                if (!string.Equals(expectedTopLevel, observedTopLevel, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string value = RunGit(candidate, "rev-parse", "HEAD");
                if (value.Length == 40)
                {
                    return (value, IsGitWorktreeClean(candidate));
                }
            }
        }

        return (string.Empty, false);
    }

    private static string RunGit(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new ProviderVerificationInputException("identity.runtime.unavailable");
        string output = process.StandardOutput.ReadToEnd();
        if (!process.WaitForExit(5000) || process.ExitCode != 0)
        {
            throw new ProviderVerificationInputException("identity.runtime.unavailable");
        }

        return output.Trim().ToLowerInvariant();
    }
}
