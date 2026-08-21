using System.Diagnostics;
using System.Formats.Tar;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Hexalith.EventStore.Contracts.Tests.Packaging;

/// <summary>
/// Exercises the Story 3.14 corrective OCI provenance and evidence matrix.
/// </summary>
public sealed class CorrectiveOciProvenanceReleaseTests
{
    private const string SourceSha = "dddddddddddddddddddddddddddddddddddddddd";
    private const string Version = "0.0.0-ci-test";
    private const string EvidenceVersion = "3.96.0";
    private const string Created = "2026-08-21T09:15:00Z";
    private static readonly TimeSpan HelperTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan PublishTimeout = TimeSpan.FromMinutes(8);

    /// <summary>
    /// Verifies the retained v3.94.1 raw configs reproduce the original SDK truncation defect.
    /// </summary>
    [Fact]
    public void RetainedV3941RawConfigsReproduceUrlTruncationAndMissingRevision()
    {
        string evidence = Path.Combine(
            FindRepositoryRoot(),
            "_bmad-output",
            "implementation-artifacts",
            "evidence",
            "story-3-13",
            "80d12ef5eee71a9fe3ea7be51171da4a71b69a28",
            "ab8784c8c9c67229ee178e9d6dd809df9554b3cdafb43ffb7bfd38c792e2afcd");
        foreach (string architecture in new[] { "amd64", "arm64" })
        {
            byte[] raw = File.ReadAllBytes(Path.Combine(evidence, $"child-linux-{architecture}.config.raw"));
            using JsonDocument config = JsonDocument.Parse(raw);
            JsonElement labels = config.RootElement.GetProperty("config").GetProperty("Labels");
            labels.GetProperty("org.opencontainers.image.source").GetString().ShouldBe("https");
            labels.GetProperty("org.opencontainers.image.url").GetString().ShouldBe("https");
            labels.GetProperty("org.opencontainers.image.documentation").GetString().ShouldBe("https");
            labels.TryGetProperty("org.opencontainers.image.revision", out _).ShouldBeFalse();
            labels.GetProperty("org.opencontainers.image.version").GetString().ShouldBe("3.94.1");
        }
    }

    /// <summary>
    /// Verifies a real SDK multi-RID archive preserves identical exact provenance labels in both configs.
    /// </summary>
    [Fact]
    public async Task RealMultiRidArchiveContainsExactProvenanceInBothChildConfigs()
    {
        string root = FindRepositoryRoot();
        string temporary = Path.Combine(Path.GetTempPath(), $"eventstore-oci-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporary);
        try
        {
            string archive = Path.Combine(temporary, "eventstore.tar");
            ProcessResult publish = await RunProcessAsync(
                root,
                "dotnet",
                PublishTimeout,
                new[]
            {
                "publish",
                "src/Hexalith.EventStore/Hexalith.EventStore.csproj",
                "--configuration", "Release",
                "-t:PublishContainer",
                "-m:1",
                "-p:UseHexalithProjectReferences=false",
                "-p:NuGetAudit=false",
                "-p:MinVerVersionOverride=1.0.0",
                "-p:RuntimeIdentifiers=\"linux-musl-x64;linux-musl-arm64\"",
                "-p:ContainerRuntimeIdentifiers=\"linux-musl-x64;linux-musl-arm64\"",
                "-p:ContainerImageFormat=OCI",
                $"-p:ContainerArchiveOutputPath={archive}",
                "-p:ContainerRegistry=registry.example.test",
                "-p:ContainerRepository=eventstore",
                $"-p:ContainerImageTag={Version}",
                $"-p:Version={Version}",
                $"-p:ContainerProvenanceSourceSha={SourceSha}",
                $"-p:ContainerProvenanceReleaseVersion={Version}",
                $"-p:ContainerProvenanceCreated={Created}",
            });
            publish.ExitCode.ShouldBe(0, publish.Output + publish.Error);

            Dictionary<string, byte[]> entries = ReadTarEntries(archive);
            using JsonDocument rootIndex = JsonDocument.Parse(entries["index.json"]);
            string nestedDigest = rootIndex.RootElement.GetProperty("manifests")[0]
                .GetProperty("digest").GetString().ShouldNotBeNull();
            using JsonDocument index = JsonDocument.Parse(entries[BlobPath(nestedDigest)]);
            JsonElement[] manifests = index.RootElement.GetProperty("manifests").EnumerateArray().ToArray();
            manifests.Length.ShouldBe(2);

            Dictionary<string, string> expected = ExpectedLabels();
            string[] platforms = manifests
                .Select(manifest => manifest.GetProperty("platform").GetProperty("architecture").GetString())
                .Where(architecture => architecture is not null)
                .Select(architecture => "linux/" + architecture)
                .Order(StringComparer.Ordinal)
                .ToArray()!;
            platforms.ShouldBe(["linux/amd64", "linux/arm64"]);
            List<Dictionary<string, string>> childLabels = [];
            foreach (JsonElement descriptor in manifests)
            {
                string manifestDigest = descriptor.GetProperty("digest").GetString().ShouldNotBeNull();
                using JsonDocument manifest = JsonDocument.Parse(entries[BlobPath(manifestDigest)]);
                string configDigest = manifest.RootElement.GetProperty("config").GetProperty("digest")
                    .GetString().ShouldNotBeNull();
                using JsonDocument config = JsonDocument.Parse(entries[BlobPath(configDigest)]);
                JsonElement labels = config.RootElement.GetProperty("config").GetProperty("Labels");
                ValidateLabels(labels, expected);
                childLabels.Add(labels.EnumerateObject().ToDictionary(
                    property => property.Name,
                    property => property.Value.GetString() ?? string.Empty,
                    StringComparer.Ordinal));
            }

            AssertNoLabelWasTruncatedAtItsFirstColon(childLabels);
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies a real publish cannot bypass either mandatory provenance identity input.
    /// </summary>
    /// <param name="missingProperty">The provenance property deliberately omitted.</param>
    [Theory]
    [InlineData("source")]
    [InlineData("version")]
    public async Task ContainerPublicationRejectsMissingProvenanceInputs(string missingProperty)
    {
        string root = FindRepositoryRoot();
        string temporary = Path.Combine(Path.GetTempPath(), $"eventstore-oci-negative-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporary);
        try
        {
            List<string> arguments =
            [
                "publish",
                "src/Hexalith.EventStore/Hexalith.EventStore.csproj",
                "--configuration", "Release",
                "--os", "linux",
                "--arch", "x64",
                "-t:PublishContainer",
                "-m:1",
                "-p:UseHexalithProjectReferences=false",
                "-p:NuGetAudit=false",
                "-p:MinVerVersionOverride=1.0.0",
                "-p:ContainerImageFormat=OCI",
                $"-p:ContainerArchiveOutputPath={Path.Combine(temporary, "eventstore.tar")}",
                "-p:ContainerRegistry=registry.example.test",
                "-p:ContainerRepository=eventstore",
                $"-p:ContainerImageTag={Version}",
                $"-p:Version={Version}",
                $"-p:ContainerProvenanceCreated={Created}",
            ];
            if (missingProperty != "source")
            {
                arguments.Add($"-p:ContainerProvenanceSourceSha={SourceSha}");
            }

            if (missingProperty != "version")
            {
                arguments.Add($"-p:ContainerProvenanceReleaseVersion={Version}");
            }

            ProcessResult result = await RunProcessAsync(
                root,
                "dotnet",
                PublishTimeout,
                arguments);
            result.ExitCode.ShouldNotBe(0);
            (result.Output + result.Error).ShouldContain(
                missingProperty == "source"
                    ? "ContainerProvenanceSourceSha must be an exact lowercase 40-character commit SHA."
                    : "ContainerProvenanceReleaseVersion must be SemVer");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies malformed provenance identity fields are rejected by the build target itself.
    /// </summary>
    /// <param name="property">The property to mutate.</param>
    /// <param name="value">The malformed value.</param>
    /// <param name="expectedError">The expected fail-closed diagnostic.</param>
    [Theory]
    [InlineData("ContainerProvenanceSourceSha", "DDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDD", "exact lowercase")]
    [InlineData("ContainerProvenanceReleaseVersion", "01.2.3", "must be SemVer")]
    [InlineData("ContainerImageTag", "0.0.0-other", "must equal ContainerProvenanceReleaseVersion")]
    [InlineData("ContainerProvenanceCreated", "2026-08-21T09:15Z", "exact UTC RFC 3339 second")]
    public void ContainerPublicationRejectsMalformedProvenanceInputs(
        string property,
        string value,
        string expectedError)
    {
        Dictionary<string, string> properties = new(StringComparer.Ordinal)
        {
            ["ContainerProvenanceSourceSha"] = SourceSha,
            ["ContainerProvenanceReleaseVersion"] = Version,
            ["ContainerImageTag"] = Version,
            ["ContainerProvenanceCreated"] = Created,
        };
        properties[property] = value;
        List<string> arguments =
        [
            "msbuild",
            "src/Hexalith.EventStore/Hexalith.EventStore.csproj",
            "-t:ValidateContainerProvenanceInputs",
            "-p:EnableContainer=true",
            "-p:UseHexalithProjectReferences=false",
            "-p:NuGetAudit=false",
            "-p:MinVerVersionOverride=1.0.0",
        ];
        arguments.AddRange(properties.Select(item => $"-p:{item.Key}={item.Value}"));

        ProcessResult result = RunProcess(FindRepositoryRoot(), "dotnet", arguments.ToArray());
        result.ExitCode.ShouldNotBe(0);
        (result.Output + result.Error).ShouldContain(expectedError);
    }

    /// <summary>
    /// Verifies URL truncation and a missing revision are independently rejected.
    /// </summary>
    [Fact]
    public void ProvenanceLabelMutationsFailClosed()
    {
        Dictionary<string, string> expected = ExpectedLabels();
        foreach ((string field, string? value) in new[]
        {
            ("org.opencontainers.image.source", "https"),
            ("org.opencontainers.image.url", "https"),
            ("org.opencontainers.image.documentation", "https"),
            ("org.opencontainers.image.revision", (string?)null),
            ("org.opencontainers.image.version", "9.9.9"),
        })
        {
            JsonObject labels = new(expected.Select(pair => KeyValuePair.Create<string, JsonNode?>(pair.Key, pair.Value)));
            if (value is null)
            {
                labels.Remove(field);
            }
            else
            {
                labels[field] = value;
            }

            using JsonDocument document = JsonDocument.Parse(labels.ToJsonString());
            Should.Throw<InvalidDataException>(() => ValidateLabels(document.RootElement, expected));
        }

        JsonObject withSdkLabels = new(
            expected.Select(pair => KeyValuePair.Create<string, JsonNode?>(pair.Key, pair.Value)))
        {
            ["net.dot.sdk.version"] = "10.0.303",
            ["org.opencontainers.image.base.digest"] = $"sha256:{new string('a', 64)}",
        };
        using JsonDocument extraLabels = JsonDocument.Parse(withSdkLabels.ToJsonString());
        Should.NotThrow(() => ValidateLabels(extraLabels.RootElement, expected));
    }

    /// <summary>
    /// Verifies the package manifest has one exact 14-entry ordered, case-insensitively unique schema.
    /// </summary>
    [Fact]
    public void ReleasePackageManifestRejectsSchemaCountAndCaseInsensitiveIdentityDrift()
    {
        string script =
            "import copy,json;from pathlib import Path;" +
            "from tools.release_evidence_codec import EvidenceError,validate_release_manifest as v;" +
            "m=json.loads(Path('tools/release-packages.json').read_text());v(m);" +
            "cases=[];" +
            "x=copy.deepcopy(m);x['extra']=1;cases.append(x);" +
            "x=copy.deepcopy(m);x['packages'].pop();cases.append(x);" +
            "x=copy.deepcopy(m);x['packages'][1]['id']=x['packages'][0]['id'].upper();cases.append(x);" +
            "x=copy.deepcopy(m);x['packages'][0]['extra']=1;cases.append(x);" +
            "\nfor c in cases:\n" +
            " try:v(c);raise AssertionError('accepted invalid manifest')\n" +
            " except EvidenceError:pass\n" +
            "print('pass')";
        ProcessResult result = RunProcess(FindRepositoryRoot(), "python3", "-c", script);
        result.ExitCode.ShouldBe(0, result.Error);
        result.Output.Trim().ShouldBe("pass");
    }

    /// <summary>
    /// Verifies authority HTML links are derived from the accepted issue rather than a frozen issue number.
    /// </summary>
    [Fact]
    public void AuthorityHtmlUrlFollowsTheAcceptedIssueUrl()
    {
        const string Script =
            "from tools.release_evidence_codec import repository_issue_html_url as u;" +
            "assert u('https://api.github.com/repos/Hexalith/Hexalith.EventStore/issues/42')==" +
            "'https://github.com/Hexalith/Hexalith.EventStore/issues/42';print('pass')";
        ProcessResult result = RunProcess(FindRepositoryRoot(), "python3", "-c", Script);
        result.ExitCode.ShouldBe(0, result.Error);
        result.Output.Trim().ShouldBe("pass");
    }

    /// <summary>
    /// Verifies canonical evidence is re-derived from all package, OCI, authority, and smoke bytes.
    /// </summary>
    [Fact]
    public void CanonicalReleaseIdentityBindsRetainedBytesAndRejectsMutations()
    {
        string root = FindRepositoryRoot();
        string checkedInPacket = Path.Combine(
            root,
            "_bmad-output",
            "implementation-artifacts",
            "evidence",
            "story-3-14",
            "f343bb0153e9cdcb8b12ec10153813072f5ad38d");
        ProcessResult checkedIn = RunEvidenceValidator(
            root,
            Path.Combine(checkedInPacket, "release-identity.json"),
            checkedInPacket);
        checkedIn.ExitCode.ShouldBe(0, checkedIn.Error);
        checkedIn.Output.Trim().ShouldBe(
            "[corrective-release-evidence] pass: " +
            "sha256:4d1a0c336397e971bf10001095d5e427dd03c499ee428a3121a913926da8c4a9");

        string temporary = Path.Combine(Path.GetTempPath(), $"eventstore-release-evidence-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporary);
        try
        {
            string workflowPacket = CopyPacket(checkedInPacket, Path.Combine(temporary, "workflow"));
            JsonObject workflowIdentity = LoadIdentity(workflowPacket);
            workflowIdentity["workflow"]!["workflow_sha"] = new string('e', 40);
            WriteSelectedCanonicalJson(root, Path.Combine(workflowPacket, "release-identity.json"), workflowIdentity);
            ProcessResult workflowMutation = RunEvidenceValidator(
                root,
                Path.Combine(workflowPacket, "release-identity.json"),
                workflowPacket);
            workflowMutation.ExitCode.ShouldNotBe(0);
            workflowMutation.Error.ShouldContain("workflow identity mismatch");

            string orderPacket = CopyPacket(checkedInPacket, Path.Combine(temporary, "package-order"));
            JsonObject orderIdentity = LoadIdentity(orderPacket);
            JsonArray orderedPackages = orderIdentity["packages"]!.AsArray();
            JsonNode firstPackage = orderedPackages[0]!.DeepClone();
            orderedPackages[0] = orderedPackages[1]!.DeepClone();
            orderedPackages[1] = firstPackage;
            WriteSelectedCanonicalJson(root, Path.Combine(orderPacket, "release-identity.json"), orderIdentity);
            ProcessResult orderMutation = RunEvidenceValidator(
                root,
                Path.Combine(orderPacket, "release-identity.json"),
                orderPacket);
            orderMutation.ExitCode.ShouldNotBe(0);
            orderMutation.Error.ShouldContain("release package identity or order mismatch");

            string packagePacket = CopyPacket(checkedInPacket, Path.Combine(temporary, "package-origin"));
            JsonObject packageIdentity = LoadIdentity(packagePacket);
            JsonNode package = packageIdentity["packages"]![0].ShouldNotBeNull();
            string packageFile = package["file"]!.GetValue<string>();
            string packagePath = Path.Combine(packagePacket, packageFile);
            MutateNuspecRepositoryUrl(packagePath);
            byte[] packageBytes = File.ReadAllBytes(packagePath);
            package["size"] = packageBytes.Length;
            package["sha256"] = Sha256(packageBytes);
            RefreshPacketManifest(root, packagePacket, packageIdentity);
            ProcessResult packageMutation = RunEvidenceValidator(
                root,
                Path.Combine(packagePacket, "release-identity.json"),
                packagePacket);
            packageMutation.ExitCode.ShouldNotBe(0);
            packageMutation.Error.ShouldContain("package nuspec repository identity does not match");

            string configPacket = CopyPacket(checkedInPacket, Path.Combine(temporary, "raw-config"));
            JsonObject configIdentity = LoadIdentity(configPacket);
            JsonNode oci = configIdentity["oci"].ShouldNotBeNull();
            JsonNode child = oci["children"]![0].ShouldNotBeNull();
            JsonNode configBinding = child["config"].ShouldNotBeNull();
            string configFile = configBinding["file"]!.GetValue<string>();
            string configPath = Path.Combine(configPacket, configFile);
            JsonObject rawConfig = JsonNode.Parse(File.ReadAllText(configPath))!.AsObject();
            rawConfig["config"]!["Labels"]!["org.opencontainers.image.source"] = "https";
            byte[] configBytes = CanonicalJsonBytes(rawConfig);
            File.WriteAllBytes(configPath, configBytes);
            UpdateBinding(configBinding, configBytes);

            JsonNode manifestBinding = child["manifest"].ShouldNotBeNull();
            string originalChildDigest = manifestBinding["digest"]!.GetValue<string>();
            string manifestFile = manifestBinding["file"]!.GetValue<string>();
            string manifestPath = Path.Combine(configPacket, manifestFile);
            JsonObject rawManifest = JsonNode.Parse(File.ReadAllText(manifestPath))!.AsObject();
            rawManifest["config"]!["digest"] = configBinding["digest"]!.GetValue<string>();
            rawManifest["config"]!["size"] = configBinding["size"]!.GetValue<int>();
            byte[] manifestBytes = CanonicalJsonBytes(rawManifest);
            File.WriteAllBytes(manifestPath, manifestBytes);
            UpdateBinding(manifestBinding, manifestBytes);

            JsonNode indexBinding = oci["index"].ShouldNotBeNull();
            string indexFile = indexBinding["file"]!.GetValue<string>();
            string indexPath = Path.Combine(configPacket, indexFile);
            JsonObject rawIndex = JsonNode.Parse(File.ReadAllText(indexPath))!.AsObject();
            rawIndex["manifests"]![0]!["digest"] = manifestBinding["digest"]!.GetValue<string>();
            rawIndex["manifests"]![0]!["size"] = manifestBinding["size"]!.GetValue<int>();
            byte[] indexBytes = CanonicalJsonBytes(rawIndex);
            File.WriteAllBytes(indexPath, indexBytes);
            UpdateBinding(indexBinding, indexBytes);

            JsonNode firstSmoke = configIdentity["smokes"]![0].ShouldNotBeNull();
            string childDigest = manifestBinding["digest"]!.GetValue<string>();
            firstSmoke["child_digest"] = childDigest;
            firstSmoke["immutable_image"] = $"registry.hexalith.com/eventstore@{childDigest}";
            string smokeSummaryFile = firstSmoke["evidence_file"]!.GetValue<string>();
            string smokeSummaryPath = Path.Combine(configPacket, smokeSummaryFile);
            JsonObject smokeSummary = JsonNode.Parse(File.ReadAllText(smokeSummaryPath))!.AsObject();
            smokeSummary["platforms"]![0]!["digest"] = childDigest;
            byte[] smokeSummaryBytes = CanonicalJsonBytes(smokeSummary);
            File.WriteAllBytes(smokeSummaryPath, smokeSummaryBytes);
            foreach (JsonNode? smokeNode in configIdentity["smokes"]!.AsArray())
            {
                smokeNode!["evidence_sha256"] = Sha256(smokeSummaryBytes);
            }

            JsonNode smokeLogBinding = firstSmoke["log"].ShouldNotBeNull();
            string smokeLogFile = smokeLogBinding["file"]!.GetValue<string>();
            string configSmokeLogPath = Path.Combine(configPacket, smokeLogFile);
            byte[] configSmokeLogBytes = Encoding.UTF8.GetBytes(
                File.ReadAllText(configSmokeLogPath).Replace(
                    $"registry.hexalith.com/eventstore@{originalChildDigest}",
                    $"registry.hexalith.com/eventstore@{childDigest}",
                    StringComparison.Ordinal));
            File.WriteAllBytes(configSmokeLogPath, configSmokeLogBytes);
            UpdateFileBinding(smokeLogBinding, configSmokeLogBytes);
            RefreshPacketManifest(root, configPacket, configIdentity);
            ProcessResult configMutation = RunEvidenceValidator(
                root,
                Path.Combine(configPacket, "release-identity.json"),
                configPacket);
            configMutation.ExitCode.ShouldNotBe(0);
            configMutation.Error.ShouldContain("retained OCI config platform or labels mismatch");

            string checksumPacket = CopyPacket(checkedInPacket, Path.Combine(temporary, "checksum"));
            File.AppendAllText(Path.Combine(checksumPacket, "observations.json"), " \n");
            ProcessResult checksumMutation = RunEvidenceValidator(
                root,
                Path.Combine(checksumPacket, "release-identity.json"),
                checksumPacket);
            checksumMutation.ExitCode.ShouldNotBe(0);
            checksumMutation.Error.ShouldContain("packet checksum manifest digest mismatch");

            string smokePacket = CopyPacket(checkedInPacket, Path.Combine(temporary, "smoke"));
            JsonObject smokeIdentity = LoadIdentity(smokePacket);
            JsonNode smoke = smokeIdentity["smokes"]![0].ShouldNotBeNull();
            string smokeLog = smoke["log"]!["file"]!.GetValue<string>();
            string smokeLogPath = Path.Combine(smokePacket, smokeLog);
            byte[] smokeBytes = Encoding.UTF8.GetBytes(
                File.ReadAllText(smokeLogPath).Replace("cleanup=pass", "cleanup=failure", StringComparison.Ordinal));
            File.WriteAllBytes(smokeLogPath, smokeBytes);
            UpdateFileBinding(smoke["log"].ShouldNotBeNull(), smokeBytes);
            RefreshPacketManifest(root, smokePacket, smokeIdentity);
            ProcessResult smokeMutation = RunEvidenceValidator(
                root,
                Path.Combine(smokePacket, "release-identity.json"),
                smokePacket);
            smokeMutation.ExitCode.ShouldNotBe(0);
            smokeMutation.Error.ShouldContain("retained raw smoke log identity or outcome mismatch");

            string authorityPacket = CopyPacket(checkedInPacket, Path.Combine(temporary, "authority"));
            JsonObject authorityIdentity = LoadIdentity(authorityPacket);
            JsonNode authority = authorityIdentity["authority"].ShouldNotBeNull();
            string authorityFile = authority["authority_record_file"]!.GetValue<string>();
            JsonObject authorityRecord = JsonNode.Parse(File.ReadAllText(Path.Combine(authorityPacket, authorityFile)))!
                .AsObject();
            authorityRecord["updated_at"] = "2026-08-20T11:06:07Z";
            byte[] authorityBytes = CanonicalJsonBytes(authorityRecord);
            File.WriteAllBytes(Path.Combine(authorityPacket, authorityFile), authorityBytes);
            authority["authority_record_sha256"] = Sha256(authorityBytes);
            RefreshPacketManifest(root, authorityPacket, authorityIdentity);
            ProcessResult authorityMutation = RunEvidenceValidator(
                root,
                Path.Combine(authorityPacket, "release-identity.json"),
                authorityPacket);
            authorityMutation.ExitCode.ShouldNotBe(0);
            authorityMutation.Error.ShouldContain("timestamps or validity window are invalid");

            string receiptPacket = CopyPacket(checkedInPacket, Path.Combine(temporary, "receipt"));
            JsonObject receiptIdentity = LoadIdentity(receiptPacket);
            JsonNode receiptAuthority = receiptIdentity["authority"].ShouldNotBeNull();
            string receiptFile = receiptAuthority["consumption_evidence_file"]!.GetValue<string>();
            JsonObject receipt = JsonNode.Parse(File.ReadAllText(Path.Combine(receiptPacket, receiptFile)))!.AsObject();
            JsonObject receiptBody = JsonNode.Parse(receipt["body"]!.GetValue<string>())!.AsObject();
            receiptBody["schema"] = "mutated";
            receipt["body"] = Encoding.UTF8.GetString(CanonicalJsonBytes(receiptBody)).TrimEnd('\n');
            byte[] receiptBytes = CanonicalJsonBytes(receipt);
            File.WriteAllBytes(Path.Combine(receiptPacket, receiptFile), receiptBytes);
            receiptAuthority["consumption_evidence_sha256"] = Sha256(receiptBytes);
            RefreshPacketManifest(root, receiptPacket, receiptIdentity);
            ProcessResult receiptMutation = RunEvidenceValidator(
                root,
                Path.Combine(receiptPacket, "release-identity.json"),
                receiptPacket);
            receiptMutation.ExitCode.ShouldNotBe(0);
            receiptMutation.Error.ShouldContain("consumption does not bind the selected identity");

            foreach (string field in new[] { "selects_deployed_identity", "grants_mutation_authority" })
            {
                string authorityHandoffPacket = CopyPacket(
                    checkedInPacket,
                    Path.Combine(temporary, field));
                JsonObject authorityHandoffIdentity = LoadIdentity(authorityHandoffPacket);
                authorityHandoffIdentity[field] = true;
                WriteSelectedCanonicalJson(
                    root,
                    Path.Combine(authorityHandoffPacket, "release-identity.json"),
                    authorityHandoffIdentity);
                ProcessResult authorityHandoffMutation = RunEvidenceValidator(
                    root,
                    Path.Combine(authorityHandoffPacket, "release-identity.json"),
                    authorityHandoffPacket);
                authorityHandoffMutation.ExitCode.ShouldNotBe(0);
                authorityHandoffMutation.Error.ShouldContain(
                    "must not select deployment or grant mutation authority");
            }

            foreach ((string field, JsonNode value) in new[]
            {
                ("version", JsonValue.Create(4)),
                ("sha256", JsonValue.Create(new string('0', 64))),
            })
            {
                string dispatchPacket = CopyPacket(
                    checkedInPacket,
                    Path.Combine(temporary, $"handler-{field}"));
                JsonObject dispatchIdentity = LoadIdentity(dispatchPacket);
                if (field == "version")
                {
                    dispatchIdentity["codec"]![field] = value;
                }
                else
                {
                    dispatchIdentity["codec"]!["codec"]![field] = value;
                }

                WriteSelectedCanonicalJson(
                    root,
                    Path.Combine(dispatchPacket, "release-identity.json"),
                    dispatchIdentity);
                ProcessResult dispatchMutation = RunEvidenceValidator(
                    root,
                    Path.Combine(dispatchPacket, "release-identity.json"),
                    dispatchPacket);
                dispatchMutation.ExitCode.ShouldNotBe(0);
                dispatchMutation.Error.ShouldContain("does not select a trusted live handler");
            }

            string conflictingSmokePacket = CopyPacket(
                checkedInPacket,
                Path.Combine(temporary, "conflicting-smoke"));
            JsonObject conflictingSmokeIdentity = LoadIdentity(conflictingSmokePacket);
            JsonNode conflictingSmoke = conflictingSmokeIdentity["smokes"]![0].ShouldNotBeNull();
            JsonNode conflictingLogBinding = conflictingSmoke["log"].ShouldNotBeNull();
            string conflictingLogPath = Path.Combine(
                conflictingSmokePacket,
                conflictingLogBinding["file"]!.GetValue<string>());
            byte[] conflictingLogBytes = Encoding.UTF8.GetBytes(
                File.ReadAllText(conflictingLogPath) + "outcome=failure\n");
            File.WriteAllBytes(conflictingLogPath, conflictingLogBytes);
            UpdateFileBinding(conflictingLogBinding, conflictingLogBytes);
            RefreshPacketManifest(root, conflictingSmokePacket, conflictingSmokeIdentity);
            ProcessResult conflictingSmokeMutation = RunEvidenceValidator(
                root,
                Path.Combine(conflictingSmokePacket, "release-identity.json"),
                conflictingSmokePacket);
            conflictingSmokeMutation.ExitCode.ShouldNotBe(0);
            conflictingSmokeMutation.Error.ShouldContain("raw smoke log identity or outcome mismatch");

            string splitSummaryPacket = CopyPacket(
                checkedInPacket,
                Path.Combine(temporary, "split-smoke-summary"));
            JsonObject splitSummaryIdentity = LoadIdentity(splitSummaryPacket);
            JsonNode secondSmoke = splitSummaryIdentity["smokes"]![1].ShouldNotBeNull();
            string originalSummary = secondSmoke["evidence_file"]!.GetValue<string>();
            string splitSummary = Path.Combine(Path.GetDirectoryName(originalSummary)!, "smoke-results-arm64.json")
                .Replace('\\', '/');
            File.Copy(
                Path.Combine(splitSummaryPacket, originalSummary),
                Path.Combine(splitSummaryPacket, splitSummary));
            secondSmoke["evidence_file"] = splitSummary;
            RefreshPacketManifest(root, splitSummaryPacket, splitSummaryIdentity);
            ProcessResult splitSummaryMutation = RunEvidenceValidator(
                root,
                Path.Combine(splitSummaryPacket, "release-identity.json"),
                splitSummaryPacket);
            splitSummaryMutation.ExitCode.ShouldNotBe(0);
            splitSummaryMutation.Error.ShouldContain("one shared two-platform summary");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies retained authority cannot expire before creation, exceed 24 hours, or be edited.
    /// </summary>
    /// <param name="expiresAt">Optional replacement expiry.</param>
    /// <param name="updatedAt">Optional replacement update timestamp.</param>
    [Theory]
    [InlineData("2026-08-20T11:05:00Z", null)]
    [InlineData("2026-08-21T11:06:07Z", null)]
    [InlineData(null, "2026-08-20T11:06:07Z")]
    public void RetainedAuthorityRejectsInvalidWindowAndEditedRecord(string? expiresAt, string? updatedAt)
    {
        string root = FindRepositoryRoot();
        string source = Path.Combine(
            root,
            "_bmad-output",
            "implementation-artifacts",
            "evidence",
            "story-3-14",
            "f343bb0153e9cdcb8b12ec10153813072f5ad38d");
        string temporary = Path.Combine(Path.GetTempPath(), $"eventstore-authority-{Guid.NewGuid():N}");
        try
        {
            string packetRoot = CopyPacket(source, temporary);
            JsonObject identity = LoadIdentity(packetRoot);
            JsonNode authority = identity["authority"].ShouldNotBeNull();
            string authorityFile = authority["authority_record_file"]!.GetValue<string>();
            string authorityPath = Path.Combine(packetRoot, authorityFile);
            JsonObject record = JsonNode.Parse(File.ReadAllText(authorityPath))!.AsObject();
            JsonObject body = JsonNode.Parse(record["body"]!.GetValue<string>())!.AsObject();
            if (expiresAt is not null)
            {
                body["expires_at"] = expiresAt;
                record["body"] = Encoding.UTF8.GetString(CanonicalJsonBytes(body)).TrimEnd('\n');
            }

            if (updatedAt is not null)
            {
                record["updated_at"] = updatedAt;
            }

            byte[] recordBytes = CanonicalJsonBytes(record);
            File.WriteAllBytes(authorityPath, recordBytes);
            authority["authority_record_sha256"] = Sha256(recordBytes);
            RefreshPacketManifest(root, packetRoot, identity);

            ProcessResult result = RunEvidenceValidator(
                root,
                Path.Combine(packetRoot, "release-identity.json"),
                packetRoot);
            result.ExitCode.ShouldNotBe(0);
            result.Error.ShouldContain("timestamps or validity window are invalid");
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
    /// Verifies the former synthetic v1 fixture cannot bypass the trusted live-handler dispatch table.
    /// </summary>
    [Fact]
    public void UnsupportedSyntheticPacketCannotSelectTrustedLiveHandler()
    {
        string root = FindRepositoryRoot();
        string temporary = Path.Combine(Path.GetTempPath(), $"eventstore-legacy-evidence-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporary);
        try
        {
            EvidenceFixture fixture = BuildUnsupportedSyntheticEvidencePacket(root, temporary);
            ProcessResult result = RunEvidenceValidator(root, fixture.IdentityPath, temporary);
            result.ExitCode.ShouldNotBe(0);
            result.Error.ShouldContain("dispatch metadata is invalid");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies the shared authority fixtures execute replay, mismatch, expiry, and wrong-role cases.
    /// </summary>
    [Fact]
    public void PublicationAuthorityFixturesPassWithoutSkippedCases()
    {
        string root = FindRepositoryRoot();
        string builds = Path.Combine(root, "references", "Hexalith.Builds");
        string workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "release.yml"));
        Match releasePin = Regex.Match(
            workflow,
            @"uses: Hexalith/Hexalith\.Builds/\.github/workflows/domain-release\.yml@(?<sha>[0-9a-f]{40})");
        releasePin.Success.ShouldBeTrue();

        string temporary = Path.Combine(Path.GetTempPath(), $"eventstore-builds-fixtures-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporary);
        try
        {
            string archive = Path.Combine(temporary, "builds.tar");
            ProcessResult snapshot = RunProcess(
                builds,
                "git",
                "archive",
                $"--output={archive}",
                releasePin.Groups["sha"].Value,
                "Github/publish-containers");
            snapshot.ExitCode.ShouldBe(0, snapshot.Error);
            TarFile.ExtractToDirectory(archive, temporary, overwriteFiles: true);

            ProcessResult result = RunProcess(
                temporary,
                "python3",
                "-m", "unittest", "-v",
                "Github.publish-containers.tests.test_publication_preflight.PublicationPreflightTests.test_github_authority_binds_identity_owner_expiry_and_one_use_consumption",
                "Github.publish-containers.tests.test_publication_preflight.PublicationPreflightTests.test_github_authority_rejects_expired_wrong_owner_and_identity_mismatch");
            result.ExitCode.ShouldBe(0, result.Output + result.Error);
            result.Error.ShouldContain("Ran 2 tests");
            result.Error.ShouldContain("OK");
            result.Error.ShouldNotContain("skipped");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    private static EvidenceFixture BuildUnsupportedSyntheticEvidencePacket(string root, string packetRoot)
    {
        string manifestPath = Path.Combine(root, "tools", "release-packages.json");
        byte[] manifestBytes = File.ReadAllBytes(manifestPath);
        using JsonDocument manifest = JsonDocument.Parse(manifestBytes);
        string[] packageIds = manifest.RootElement.GetProperty("packages")
            .EnumerateArray()
            .Select(item => item.GetProperty("id").GetString().ShouldNotBeNull())
            .ToArray();

        JsonArray packages = [];
        string packagesDirectory = Path.Combine(packetRoot, "packages");
        Directory.CreateDirectory(packagesDirectory);
        foreach (string packageId in packageIds)
        {
            string relative = $"packages/{packageId}.{EvidenceVersion}.nupkg";
            string path = Path.Combine(packetRoot, relative);
            using (FileStream stream = File.Create(path))
            using (ZipArchive archive = new(stream, ZipArchiveMode.Create))
            {
                ZipArchiveEntry nuspec = archive.CreateEntry($"{packageId}.nuspec");
                using StreamWriter writer = new(nuspec.Open(), new UTF8Encoding(false));
                writer.Write(
                    $"<package xmlns=\"http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd\">" +
                    $"<metadata><id>{packageId}</id><version>{EvidenceVersion}</version>" +
                    $"<repository type=\"git\" commit=\"{SourceSha}\" /></metadata></package>");
            }

            byte[] bytes = File.ReadAllBytes(path);
            packages.Add(new JsonObject
            {
                ["id"] = packageId,
                ["version"] = EvidenceVersion,
                ["file"] = relative,
                ["size"] = bytes.Length,
                ["sha256"] = Sha256(bytes),
                ["repository_commit"] = SourceSha,
            });
        }

        JsonArray children = [];
        JsonArray descriptors = [];
        string? firstConfigPath = null;
        byte[]? firstConfigBytes = null;
        foreach (string platform in new[] { "linux/amd64", "linux/arm64" })
        {
            string architecture = platform.Split('/')[1];
            JsonObject labels = ExpectedEvidenceLabels();
            labels["org.opencontainers.image.vendor"] = "Hexalith";
            labels["org.opencontainers.image.base.digest"] = $"sha256:{new string(architecture[0], 64)}";
            JsonObject config = new()
            {
                ["architecture"] = architecture,
                ["os"] = "linux",
                ["config"] = new JsonObject { ["Labels"] = labels.DeepClone() },
            };
            byte[] configBytes = CanonicalJsonBytes(config);
            string configRelative = $"oci/child-linux-{architecture}.config.raw";
            string configPath = Path.Combine(packetRoot, configRelative);
            Directory.CreateDirectory(Path.GetDirectoryName(configPath).ShouldNotBeNull());
            File.WriteAllBytes(configPath, configBytes);
            JsonObject configBinding = OciBinding(
                configRelative,
                configBytes,
                "application/vnd.oci.image.config.v1+json");

            JsonObject childManifest = new()
            {
                ["schemaVersion"] = 2,
                ["mediaType"] = "application/vnd.oci.image.manifest.v1+json",
                ["config"] = new JsonObject
                {
                    ["mediaType"] = configBinding["media_type"]!.GetValue<string>(),
                    ["digest"] = configBinding["digest"]!.GetValue<string>(),
                    ["size"] = configBinding["size"]!.GetValue<int>(),
                },
                ["layers"] = new JsonArray(),
            };
            byte[] childManifestBytes = CanonicalJsonBytes(childManifest);
            string manifestRelative = $"oci/child-linux-{architecture}.manifest.raw";
            File.WriteAllBytes(Path.Combine(packetRoot, manifestRelative), childManifestBytes);
            JsonObject manifestBinding = OciBinding(
                manifestRelative,
                childManifestBytes,
                "application/vnd.oci.image.manifest.v1+json");
            descriptors.Add(new JsonObject
            {
                ["mediaType"] = manifestBinding["media_type"]!.GetValue<string>(),
                ["digest"] = manifestBinding["digest"]!.GetValue<string>(),
                ["size"] = manifestBinding["size"]!.GetValue<int>(),
                ["platform"] = new JsonObject { ["os"] = "linux", ["architecture"] = architecture },
            });
            children.Add(new JsonObject
            {
                ["platform"] = platform,
                ["manifest"] = manifestBinding,
                ["config"] = configBinding,
                ["labels"] = labels,
            });
            firstConfigPath ??= configPath;
            firstConfigBytes ??= configBytes;
        }

        JsonObject index = new()
        {
            ["schemaVersion"] = 2,
            ["mediaType"] = "application/vnd.oci.image.index.v1+json",
            ["manifests"] = descriptors,
        };
        byte[] indexBytes = CanonicalJsonBytes(index);
        string indexRelative = "oci/index.raw";
        string indexPath = Path.Combine(packetRoot, indexRelative);
        File.WriteAllBytes(indexPath, indexBytes);
        JsonObject indexBinding = OciBinding(
            indexRelative,
            indexBytes,
            "application/vnd.oci.image.index.v1+json");

        JsonArray smokePlatforms = [];
        foreach (JsonNode? childNode in children)
        {
            JsonObject child = childNode!.AsObject();
            smokePlatforms.Add(new JsonObject
            {
                ["platform"] = child["platform"]!.GetValue<string>(),
                ["digest"] = child["manifest"]!["digest"]!.GetValue<string>(),
                ["outcome"] = "pass",
                ["cleanup"] = "pass",
            });
        }

        JsonObject smokeSummary = new()
        {
            ["result"] = "pass",
            ["image_repository"] = "registry.hexalith.com/eventstore",
            ["environment"] = "Development",
            ["endpoint"] = "/alive",
            ["timeout_seconds"] = 180,
            ["platforms"] = smokePlatforms,
        };
        byte[] smokeBytes = CanonicalJsonBytes(smokeSummary);
        string smokeRelative = "oci/smoke-results.json";
        string smokePath = Path.Combine(packetRoot, smokeRelative);
        File.WriteAllBytes(smokePath, smokeBytes);

        const long runId = 123456789;
        const int runAttempt = 1;
        string buildsSha = new('b', 40);
        JsonObject publicationIdentity = new()
        {
            ["schema"] = "hexalith.release-publication-preflight.v4",
            ["repository"] = "Hexalith/Hexalith.EventStore",
            ["version"] = EvidenceVersion,
            ["source_sha"] = SourceSha,
            ["source"] = new JsonObject
            {
                ["branch"] = "main",
                ["ref"] = "refs/heads/main",
                ["live_sha"] = SourceSha,
                ["ci_workflow"] = "ci.yml",
                ["ci_run"] = new JsonObject
                {
                    ["id"] = 987654321,
                    ["head_sha"] = SourceSha,
                    ["head_branch"] = "main",
                    ["event"] = "push",
                    ["status"] = "completed",
                    ["conclusion"] = "success",
                },
            },
            ["container_repository"] = "registry.hexalith.com/eventstore",
            ["container_repositories"] = new JsonArray("registry.hexalith.com/eventstore"),
            ["platforms"] = new JsonArray("linux/amd64", "linux/arm64"),
            ["environment"] = "production",
            ["run"] = new JsonObject
            {
                ["id"] = runId.ToString(),
                ["attempt"] = runAttempt.ToString(),
                ["workflow_sha"] = SourceSha,
                ["ref"] = "refs/heads/main",
            },
            ["builds"] = new JsonObject
            {
                ["workflow_sha"] = buildsSha,
                ["action_sha"] = buildsSha,
                ["files"] = BuildHelperHashes(),
            },
            ["packages"] = new JsonObject
            {
                ["ids"] = new JsonArray(packageIds.Select(id => JsonValue.Create(id)).ToArray()),
                ["manifest_sha256"] = Sha256(manifestBytes),
            },
        };
        byte[] publicationIdentityBytes = CanonicalJsonBytes(publicationIdentity);
        string publicationIdentityRelative = "preflight/publication-identity.json";
        Directory.CreateDirectory(Path.Combine(packetRoot, "preflight"));
        File.WriteAllBytes(Path.Combine(packetRoot, publicationIdentityRelative), publicationIdentityBytes);
        string publicationIdentityHash = Sha256(publicationIdentityBytes);

        string authorityUrl =
            "https://api.github.com/repos/Hexalith/Hexalith.EventStore/issues/comments/123456";
        string issueUrl = "https://api.github.com/repos/Hexalith/Hexalith.EventStore/issues/42";
        string authorityRecordHash = new('e', 64);
        string nonce = "story-3-14-authority-0001";
        JsonObject authorityEvidence = new()
        {
            ["url"] = authorityUrl,
            ["comment_id"] = 123456,
            ["issue_url"] = issueUrl,
            ["owner"] = "github:jpiquot",
            ["created_at"] = "2026-08-20T10:00:00Z",
            ["authorized_at"] = "2026-08-20T10:00:00Z",
            ["expires_at"] = "2026-08-20T18:00:00Z",
            ["rationale"] = "Publish the separately authorized Story 3.14 corrective release.",
            ["identity_sha256"] = publicationIdentityHash,
            ["record_sha256"] = authorityRecordHash,
            ["nonce"] = nonce,
        };
        byte[] authorityBytes = CanonicalJsonBytes(authorityEvidence);
        string authorityRelative = "preflight/publication-authority.json";
        File.WriteAllBytes(Path.Combine(packetRoot, authorityRelative), authorityBytes);

        JsonObject consumptionBody = new()
        {
            ["schema"] = "hexalith.release-publication-authority-consumption.v1",
            ["authority_comment_id"] = 123456,
            ["authority_record_sha256"] = authorityRecordHash,
            ["identity_sha256"] = publicationIdentityHash,
            ["run_id"] = runId.ToString(),
            ["run_attempt"] = runAttempt.ToString(),
            ["nonce"] = nonce,
        };
        JsonObject consumptionEvidence = new()
        {
            ["id"] = 654321,
            ["url"] = "https://api.github.com/repos/Hexalith/Hexalith.EventStore/issues/comments/654321",
            ["issue_url"] = issueUrl,
            ["user"] = new JsonObject { ["login"] = "github-actions[bot]" },
            ["body"] = Encoding.UTF8.GetString(CanonicalJsonBytes(consumptionBody)).TrimEnd('\n'),
        };
        byte[] consumptionBytes = CanonicalJsonBytes(consumptionEvidence);
        string consumptionRelative = "preflight/publication-authority-consumption.json";
        File.WriteAllBytes(Path.Combine(packetRoot, consumptionRelative), consumptionBytes);

        JsonArray smokes = [];
        foreach (JsonNode? childNode in children)
        {
            JsonObject child = childNode!.AsObject();
            string platform = child["platform"]!.GetValue<string>();
            string childDigest = child["manifest"]!["digest"]!.GetValue<string>();
            smokes.Add(new JsonObject
            {
                ["platform"] = platform,
                ["child_digest"] = childDigest,
                ["immutable_image"] = $"registry.hexalith.com/eventstore@{childDigest}",
                ["environment"] = "Development",
                ["endpoint"] = "/alive",
                ["timeout_seconds"] = 180,
                ["result"] = "pass",
                ["evidence_file"] = smokeRelative,
                ["evidence_sha256"] = Sha256(smokeBytes),
            });
        }

        JsonObject helpers = BuildHelperHashes();

        JsonObject identity = new()
        {
            ["schema"] = "hexalith.eventstore.corrective-release-identity.v1",
            ["codec"] = new JsonObject
            {
                ["schema"] = "hexalith.eventstore.corrective-release-identity.v1",
                ["version"] = 1,
                ["codec_file"] = "tools/release_evidence_codec.py",
                ["codec_sha256"] = Sha256(File.ReadAllBytes(Path.Combine(root, "tools", "release_evidence_codec.py"))),
                ["verifier_file"] = "tools/validate-corrective-release-evidence.py",
                ["verifier_sha256"] = Sha256(
                    File.ReadAllBytes(Path.Combine(root, "tools", "validate-corrective-release-evidence.py"))),
            },
            ["repository"] = "Hexalith/Hexalith.EventStore",
            ["version"] = EvidenceVersion,
            ["tag"] = $"v{EvidenceVersion}",
            ["source_sha"] = SourceSha,
            ["manifest"] = new JsonObject
            {
                ["file"] = "tools/release-packages.json",
                ["sha256"] = Sha256(manifestBytes),
                ["package_count"] = packageIds.Length,
            },
            ["workflow"] = new JsonObject
            {
                ["repository"] = "Hexalith/Hexalith.EventStore",
                ["workflow_file"] = ".github/workflows/release.yml",
                ["workflow_sha"] = SourceSha,
                ["run_id"] = runId,
                ["run_attempt"] = runAttempt,
                ["source_sha"] = SourceSha,
            },
            ["builds"] = new JsonObject { ["execution_sha"] = buildsSha, ["helpers"] = helpers },
            ["authority"] = new JsonObject
            {
                ["owner"] = "github:jpiquot",
                ["authority_url"] = authorityUrl,
                ["issue_url"] = issueUrl,
                ["publication_identity_file"] = publicationIdentityRelative,
                ["publication_identity_sha256"] = publicationIdentityHash,
                ["authority_evidence_file"] = authorityRelative,
                ["authority_evidence_sha256"] = Sha256(authorityBytes),
                ["consumption_evidence_file"] = consumptionRelative,
                ["consumption_evidence_sha256"] = Sha256(consumptionBytes),
                ["consumed_once"] = true,
            },
            ["packages"] = packages,
            ["oci"] = new JsonObject
            {
                ["image"] = $"registry.hexalith.com/eventstore:{EvidenceVersion}",
                ["index"] = indexBinding,
                ["children"] = children,
            },
            ["smokes"] = smokes,
            ["selects_deployed_identity"] = false,
            ["grants_mutation_authority"] = false,
        };
        string identityPath = Path.Combine(packetRoot, "release-identity.json");
        WriteSelectedCanonicalJson(root, identityPath, identity);
        return new(
            identityPath,
            identity,
            firstConfigPath.ShouldNotBeNull(),
            firstConfigBytes.ShouldNotBeNull(),
            indexPath,
            indexBytes,
            smokePath);
    }

    private static JsonObject ExpectedEvidenceLabels() => new()
    {
        ["org.opencontainers.image.source"] = "https://github.com/Hexalith/Hexalith.EventStore",
        ["org.opencontainers.image.url"] =
            $"https://github.com/Hexalith/Hexalith.EventStore/releases/tag/v{EvidenceVersion}",
        ["org.opencontainers.image.documentation"] =
            $"https://github.com/Hexalith/Hexalith.EventStore/blob/{SourceSha}/README.md",
        ["org.opencontainers.image.revision"] = SourceSha,
        ["org.opencontainers.image.version"] = EvidenceVersion,
    };

    private static JsonObject BuildHelperHashes()
    {
        JsonObject helpers = new();
        foreach (string helper in new[]
        {
            "publish-containers.sh",
            "oci_registry_validator.py",
            "publication_preflight.py",
            "smoke-container-platforms.sh",
            "smoke_container_platforms.py",
        })
        {
            helpers[helper] = new string('f', 64);
        }

        return helpers;
    }

    private static JsonObject OciBinding(string relative, byte[] bytes, string mediaType)
    {
        string hash = Sha256(bytes);
        return new()
        {
            ["file"] = relative,
            ["size"] = bytes.Length,
            ["sha256"] = hash,
            ["digest"] = $"sha256:{hash}",
            ["media_type"] = mediaType,
        };
    }

    private static void UpdateBinding(JsonNode binding, byte[] bytes)
    {
        string hash = Sha256(bytes);
        binding["size"] = bytes.Length;
        binding["sha256"] = hash;
        binding["digest"] = $"sha256:{hash}";
    }

    private static string CopyPacket(string source, string destination)
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

        return destination;
    }

    private static JsonObject LoadIdentity(string packetRoot) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(packetRoot, "release-identity.json")))!.AsObject();

    private static void MutateNuspecRepositoryUrl(string packagePath)
    {
        using FileStream stream = new(packagePath, FileMode.Open, FileAccess.ReadWrite);
        using ZipArchive archive = new(stream, ZipArchiveMode.Update);
        ZipArchiveEntry entry = archive.Entries.Single(item => item.FullName.EndsWith(".nuspec", StringComparison.Ordinal));
        string name = entry.FullName;
        string text;
        using (StreamReader reader = new(entry.Open(), Encoding.UTF8))
        {
            text = reader.ReadToEnd();
        }

        entry.Delete();
        ZipArchiveEntry replacement = archive.CreateEntry(name);
        using StreamWriter writer = new(replacement.Open(), new UTF8Encoding(false));
        writer.Write(
            text.Replace(
                "https://github.com/Hexalith/Hexalith.EventStore",
                "https://example.invalid/Hexalith.EventStore",
                StringComparison.Ordinal));
    }

    private static void RefreshPacketManifest(string root, string packetRoot, JsonObject identity)
    {
        string[] excluded = ["packet-sha256.txt", "release-identity.json"];
        string[] files = Directory.EnumerateFiles(packetRoot, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(packetRoot, path).Replace('\\', '/'))
            .Where(path => !excluded.Contains(path, StringComparer.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();
        string manifest = string.Concat(
            files.Select(path => $"{Sha256(File.ReadAllBytes(Path.Combine(packetRoot, path)))}  {path}\n"));
        byte[] manifestBytes = Encoding.UTF8.GetBytes(manifest);
        File.WriteAllBytes(Path.Combine(packetRoot, "packet-sha256.txt"), manifestBytes);
        UpdateFileBinding(identity["packet_manifest"].ShouldNotBeNull(), manifestBytes);
        WriteSelectedCanonicalJson(root, Path.Combine(packetRoot, "release-identity.json"), identity);
    }

    private static void UpdateFileBinding(JsonNode binding, byte[] bytes)
    {
        binding["size"] = bytes.Length;
        binding["sha256"] = Sha256(bytes);
    }

    private static ProcessResult RunEvidenceValidator(string root, string identityPath, string packetRoot) =>
        RunProcess(
            root,
            "python3",
            "tools/validate-corrective-release-evidence.py",
            identityPath,
            "--manifest",
            "tools/release-packages.json",
            "--packet-root",
            packetRoot);

    private static string Sha256(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static void WriteCanonicalJson(string path, JsonNode value) =>
        File.WriteAllBytes(path, CanonicalJsonBytes(value));

    private static void WriteSelectedCanonicalJson(string root, string path, JsonNode value)
    {
        WriteCanonicalJson(path, value);
        ProcessResult result = RunProcess(
            root,
            "python3",
            "-c",
            "from pathlib import Path;import sys;" +
            "from tools.release_evidence_codec import canonical_bytes,load_json_bytes;" +
            "p=Path(sys.argv[1]);p.write_bytes(canonical_bytes(load_json_bytes(p.read_bytes())))",
            path);
        if (result.ExitCode != 0)
        {
            throw new InvalidDataException(result.Error);
        }
    }

    private static byte[] CanonicalJsonBytes(JsonNode value)
    {
        using JsonDocument document = JsonDocument.Parse(value.ToJsonString());
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = false }))
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

    private static Dictionary<string, string> ExpectedLabels() => new(StringComparer.Ordinal)
    {
        ["org.opencontainers.image.source"] = "https://github.com/Hexalith/Hexalith.EventStore",
        ["org.opencontainers.image.url"] =
            $"https://github.com/Hexalith/Hexalith.EventStore/releases/tag/v{Version}",
        ["org.opencontainers.image.documentation"] =
            $"https://github.com/Hexalith/Hexalith.EventStore/blob/{SourceSha}/README.md",
        ["org.opencontainers.image.revision"] = SourceSha,
        ["org.opencontainers.image.version"] = Version,
        ["org.opencontainers.image.created"] = Created,
        ["org.opencontainers.artifact.created"] = Created,
    };

    /// <summary>
    /// Asserts the SDK multi-RID key:value round-trip did not truncate any label, not merely the
    /// five the evidence codec pins. The v3.96.2 release shipped with
    /// <c>org.opencontainers.image.created</c> cut to <c>2026-08-20T11</c> because the earlier
    /// assertion only ever read a five-key allowlist.
    /// </summary>
    private static void AssertNoLabelWasTruncatedAtItsFirstColon(
        IReadOnlyList<Dictionary<string, string>> childLabels)
    {
        childLabels.Count.ShouldBe(2);
        foreach (Dictionary<string, string> labels in childLabels)
        {
            // A truncated URL collapses to exactly its scheme.
            labels.Values.ShouldNotContain("https", "A label value collapsed to its URI scheme.");

            foreach (string name in new[]
            {
                "org.opencontainers.image.created",
                "org.opencontainers.artifact.created",
            })
            {
                labels.TryGetValue(name, out string? value).ShouldBeTrue($"{name} is missing.");
                DateTimeOffset.TryParseExact(
                    value,
                    "yyyy-MM-ddTHH:mm:ssZ",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out _)
                    .ShouldBeTrue($"{name} is not an RFC 3339 instant: '{value}'.");
            }
        }

        // Both children describe one build, so every label except the per-platform base image
        // digest must be byte-identical. A per-child truncation shows up here too.
        const string BaseDigest = "org.opencontainers.image.base.digest";
        Dictionary<string, string> first = new(childLabels[0], StringComparer.Ordinal);
        Dictionary<string, string> second = new(childLabels[1], StringComparer.Ordinal);
        first.Remove(BaseDigest);
        second.Remove(BaseDigest);
        second.ShouldBe(first, ignoreOrder: true);
        childLabels[0].ShouldContainKey(BaseDigest);
        childLabels[1].ShouldContainKey(BaseDigest);
    }

    private static void ValidateLabels(JsonElement labels, IReadOnlyDictionary<string, string> expected)
    {
        foreach ((string name, string value) in expected)
        {
            if (!labels.TryGetProperty(name, out JsonElement actual) ||
                !string.Equals(actual.GetString(), value, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Provenance label mismatch: {name}");
            }
        }
    }

    private static Dictionary<string, byte[]> ReadTarEntries(string archive)
    {
        Dictionary<string, byte[]> result = new(StringComparer.Ordinal);
        using FileStream stream = File.OpenRead(archive);
        using TarReader reader = new(stream);
        TarEntry? entry;
        while ((entry = reader.GetNextEntry()) is not null)
        {
            if (entry.DataStream is null)
            {
                continue;
            }

            using MemoryStream bytes = new();
            entry.DataStream.CopyTo(bytes);
            result[entry.Name] = bytes.ToArray();
        }

        return result;
    }

    private static string BlobPath(string digest) => "blobs/sha256/" + digest["sha256:".Length..];

    private static ProcessResult RunProcess(string workingDirectory, string fileName, params string[] arguments)
    {
        ProcessStartInfo start = CreateProcessStartInfo(workingDirectory, fileName, arguments);
        using Process process = Process.Start(start).ShouldNotBeNull();
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit((int)HelperTimeout.TotalMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            throw new TimeoutException(
                $"Process '{fileName}' exceeded the {HelperTimeout.TotalSeconds}-second test timeout.");
        }

        return new(process.ExitCode, output.GetAwaiter().GetResult(), error.GetAwaiter().GetResult());
    }

    private static async Task<ProcessResult> RunProcessAsync(
        string workingDirectory,
        string fileName,
        TimeSpan timeout,
        IEnumerable<string> arguments)
    {
        ProcessStartInfo start = CreateProcessStartInfo(workingDirectory, fileName, arguments);
        using Process process = Process.Start(start).ShouldNotBeNull();
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        using CancellationTokenSource timeoutSource = new(timeout);
        try
        {
            await process.WaitForExitAsync(timeoutSource.Token);
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            throw new TimeoutException(
                $"Process '{fileName}' exceeded the {timeout.TotalSeconds}-second test timeout.");
        }

        string[] diagnostics = await Task.WhenAll(output, error);
        return new(process.ExitCode, diagnostics[0], diagnostics[1]);
    }

    private static ProcessStartInfo CreateProcessStartInfo(
        string workingDirectory,
        string fileName,
        IEnumerable<string> arguments)
    {
        ProcessStartInfo start = new(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        return start;
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

        throw new DirectoryNotFoundException("Could not locate the EventStore repository root.");
    }

    private sealed record EvidenceFixture(
        string IdentityPath,
        JsonObject Identity,
        string ConfigPath,
        byte[] ConfigBytes,
        string IndexPath,
        byte[] IndexBytes,
        string SmokePath);

    private sealed record ProcessResult(int ExitCode, string Output, string Error);
}
