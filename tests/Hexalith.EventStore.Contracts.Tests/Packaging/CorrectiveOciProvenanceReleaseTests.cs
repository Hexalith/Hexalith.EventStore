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
    [Trait("Category", "HeavyweightContainerPublish")]
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
            }).ConfigureAwait(true);
            publish.ExitCode.ShouldBe(0, publish.Output + publish.Error);

            Dictionary<string, byte[]> entries = ReadTarEntries(archive);
            using JsonDocument rootIndex = JsonDocument.Parse(entries["index.json"]);
            string nestedDigest = rootIndex.RootElement.GetProperty("manifests")[0]
                .GetProperty("digest").GetString().ShouldNotBeNull();
            using JsonDocument index = JsonDocument.Parse(entries[BlobPath(nestedDigest)]);
            JsonElement[] manifests = index.RootElement.GetProperty("manifests").EnumerateArray().ToArray();
            manifests.Length.ShouldBe(2);

            Dictionary<string, string>? expected = null;
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
                string observedCreated = labels.GetProperty("org.opencontainers.image.created")
                    .GetString().ShouldNotBeNull();
                expected ??= ExpectedLabels(observedCreated);
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
    [Trait("Category", "HeavyweightContainerPublish")]
    [InlineData("source")]
    [InlineData("version")]
    [InlineData("created")]
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
            ];
            if (missingProperty != "source")
            {
                arguments.Add($"-p:ContainerProvenanceSourceSha={SourceSha}");
            }

            if (missingProperty != "version")
            {
                arguments.Add($"-p:ContainerProvenanceReleaseVersion={Version}");
            }

            if (missingProperty != "created")
            {
                arguments.Add($"-p:ContainerProvenanceCreated={Created}");
            }

            ProcessResult result = await RunProcessAsync(
                root,
                "dotnet",
                PublishTimeout,
                arguments).ConfigureAwait(true);
            result.ExitCode.ShouldNotBe(0);
            (result.Output + result.Error).ShouldContain(
                missingProperty switch
                {
                    "source" => "ContainerProvenanceSourceSha must be an exact lowercase 40-character commit SHA.",
                    "version" => "ContainerProvenanceReleaseVersion must be SemVer",
                    _ => "ContainerProvenanceCreated must be an exact UTC RFC 3339 second.",
                });
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
    [InlineData("ContainerProvenanceSourceSha", "DDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDD", "ContainerProvenanceSourceSha must be an exact lowercase 40-character commit SHA.", false)]
    [InlineData("ContainerProvenanceReleaseVersion", "01.2.3", "ContainerProvenanceReleaseVersion must be SemVer without build metadata or leading-zero numeric identifiers.", false)]
    [InlineData("ContainerImageTag", "0.0.0-other", "ContainerImageTag must equal ContainerProvenanceReleaseVersion.", false)]
    [InlineData("ContainerImageTags", "latest", "ContainerImageTags must be exactly ContainerProvenanceReleaseVersion and nothing else.", false)]
    [InlineData("ContainerProvenanceCreated", "2026-08-21T09:15Z", "ContainerProvenanceCreated must be an exact UTC RFC 3339 second.", false)]
    [InlineData("ContainerProvenanceRepositoryUrl", "https", "ContainerProvenanceRepositoryUrl must be a well-formed https URL.", false)]
    [InlineData("ContainerProvenanceReleaseUrl", "https", "ContainerProvenanceReleaseUrl must be a well-formed https URL.", false)]
    [InlineData("ContainerProvenanceDocumentationUrl", "https", "ContainerProvenanceDocumentationUrl must be a well-formed https URL.", false)]
    [InlineData("ContainerProvenanceRepositoryUrl", "http://github.com/Hexalith/Hexalith.EventStore", "ContainerProvenanceRepositoryUrl must be a well-formed https URL.", false)]
    [InlineData("ContainerProvenanceRepositoryUrl", "https://.", "ContainerProvenanceRepositoryUrl must be a well-formed https URL.", false)]
    [InlineData("ContainerProvenanceRepositoryUrl", "https://github.com/Hexalith/Hexalith.EventStore\n", "ContainerProvenanceRepositoryUrl must be a well-formed https URL.", false)]
    [InlineData("ContainerProvenanceRepositoryUrl", "https", "ContainerProvenanceRepositoryUrl must be a well-formed https URL.", true)]
    [InlineData("ContainerProvenanceReleaseUrl", "https", "ContainerProvenanceReleaseUrl must be a well-formed https URL.", true)]
    [InlineData("ContainerProvenanceDocumentationUrl", "https", "ContainerProvenanceDocumentationUrl must be a well-formed https URL.", true)]
    public void ContainerPublicationRejectsMalformedProvenanceInputs(
        string property,
        string value,
        string expectedError,
        bool useEnvironment)
    {
        Dictionary<string, string> properties = new(StringComparer.Ordinal)
        {
            ["ContainerProvenanceSourceSha"] = SourceSha,
            ["ContainerProvenanceReleaseVersion"] = Version,
            ["ContainerImageTag"] = Version,
            ["ContainerProvenanceCreated"] = Created,
        };
        if (!useEnvironment)
        {
            properties[property] = value;
        }
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

        ProcessResult result = useEnvironment
            ? RunProcess(
                FindRepositoryRoot(),
                "dotnet",
                new Dictionary<string, string> { [property] = value },
                arguments.ToArray())
            : RunProcess(FindRepositoryRoot(), "dotnet", arguments.ToArray());
        result.ExitCode.ShouldNotBe(0);
        (result.Output + result.Error).ShouldContain(expectedError);
    }

    /// <summary>
    /// Verifies an omitted image tag defaults to the exact provenance version rather than a
    /// non-SemVer staging alias.
    /// </summary>
    [Fact]
    public void ContainerPublicationDefaultsTagToProvenanceVersion()
    {
        ProcessResult result = RunProcess(
            FindRepositoryRoot(),
            "dotnet",
            "msbuild",
            "src/Hexalith.EventStore/Hexalith.EventStore.csproj",
            "-t:ValidateContainerProvenanceInputs",
            "-p:EnableContainer=true",
            "-p:UseHexalithProjectReferences=false",
            "-p:NuGetAudit=false",
            "-p:MinVerVersionOverride=1.0.0",
            $"-p:ContainerProvenanceSourceSha={SourceSha}",
            $"-p:ContainerProvenanceReleaseVersion={Version}",
            $"-p:ContainerProvenanceCreated={Created}");

        result.ExitCode.ShouldBe(0, result.Output + result.Error);
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
            "from tools.release_evidence_codec import EvidenceError,repository_issue_html_url as u;" +
            "assert u('https://api.github.com/repos/Hexalith/Hexalith.EventStore/issues/42')==" +
            "'https://github.com/Hexalith/Hexalith.EventStore/issues/42';" +
            "invalid=['007','٣','²'];" +
            "\nfor value in invalid:\n" +
            " try:u('https://api.github.com/repos/Hexalith/Hexalith.EventStore/issues/'+value);" +
            "raise AssertionError('accepted invalid issue number')\n" +
            " except EvidenceError:pass\n" +
            "print('pass')";
        ProcessResult result = RunProcess(FindRepositoryRoot(), "python3", "-c", Script);
        result.ExitCode.ShouldBe(0, result.Error);
        result.Output.Trim().ShouldBe("pass");
    }

    /// <summary>
    /// Verifies the checked-in Story 3.14 packet still validates at the frozen canonical digest.
    /// </summary>
    [Fact]
    public void CanonicalReleaseIdentityValidatesCheckedInPacket()
    {
        string root = FindRepositoryRoot();
        string checkedInPacket = CheckedInStory314Packet(root);
        ProcessResult checkedIn = RunEvidenceValidator(
            root,
            Path.Combine(checkedInPacket, "release-identity.json"),
            checkedInPacket);
        checkedIn.ExitCode.ShouldBe(0, checkedIn.Error);
        checkedIn.Output.Trim().ShouldBe(
            "[corrective-release-evidence] pass: " +
            "sha256:4d1a0c336397e971bf10001095d5e427dd03c499ee428a3121a913926da8c4a9");
    }

    /// <summary>
    /// Verifies a mutated workflow SHA fails closed with a workflow-identity diagnostic.
    /// </summary>
    [Fact]
    public void CanonicalReleaseIdentityRejectsWorkflowMutation()
    {
        string root = FindRepositoryRoot();
        string checkedInPacket = CheckedInStory314Packet(root);
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
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies reordered package bindings fail closed with a package-order diagnostic.
    /// </summary>
    [Fact]
    public void CanonicalReleaseIdentityRejectsPackageOrderMutation()
    {
        string root = FindRepositoryRoot();
        string checkedInPacket = CheckedInStory314Packet(root);
        string temporary = Path.Combine(Path.GetTempPath(), $"eventstore-release-evidence-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporary);
        try
        {
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
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies a mutated package nuspec repository identity fails closed.
    /// </summary>
    [Fact]
    public void CanonicalReleaseIdentityRejectsPackageOriginMutation()
    {
        string root = FindRepositoryRoot();
        string checkedInPacket = CheckedInStory314Packet(root);
        string temporary = Path.Combine(Path.GetTempPath(), $"eventstore-release-evidence-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporary);
        try
        {
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
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies a mutated retained OCI config label fails closed.
    /// </summary>
    [Fact]
    public void CanonicalReleaseIdentityRejectsRawConfigLabelMutation()
    {
        string root = FindRepositoryRoot();
        string checkedInPacket = CheckedInStory314Packet(root);
        string temporary = Path.Combine(Path.GetTempPath(), $"eventstore-release-evidence-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporary);
        try
        {
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
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies a mutated packet checksum inventory fails closed.
    /// </summary>
    [Fact]
    public void CanonicalReleaseIdentityRejectsChecksumManifestMutation()
    {
        string root = FindRepositoryRoot();
        string checkedInPacket = CheckedInStory314Packet(root);
        string temporary = Path.Combine(Path.GetTempPath(), $"eventstore-release-evidence-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporary);
        try
        {
            string checksumPacket = CopyPacket(checkedInPacket, Path.Combine(temporary, "checksum"));
            File.AppendAllText(Path.Combine(checksumPacket, "observations.json"), " \n");
            ProcessResult checksumMutation = RunEvidenceValidator(
                root,
                Path.Combine(checksumPacket, "release-identity.json"),
                checksumPacket);
            checksumMutation.ExitCode.ShouldNotBe(0);
            checksumMutation.Error.ShouldContain("packet checksum manifest digest mismatch");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies a mutated raw smoke cleanup outcome fails closed.
    /// </summary>
    [Fact]
    public void CanonicalReleaseIdentityRejectsSmokeOutcomeMutation()
    {
        string root = FindRepositoryRoot();
        string checkedInPacket = CheckedInStory314Packet(root);
        string temporary = Path.Combine(Path.GetTempPath(), $"eventstore-release-evidence-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporary);
        try
        {
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
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies an edited authority record timestamp fails closed with a window diagnostic.
    /// </summary>
    [Fact]
    public void CanonicalReleaseIdentityRejectsAuthorityRecordMutation()
    {
        string root = FindRepositoryRoot();
        string checkedInPacket = CheckedInStory314Packet(root);
        string temporary = Path.Combine(Path.GetTempPath(), $"eventstore-release-evidence-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporary);
        try
        {
            string authorityPacket = CopyPacket(checkedInPacket, Path.Combine(temporary, "authority"));
            JsonObject authorityIdentity = LoadIdentity(authorityPacket);
            JsonNode authority = authorityIdentity["authority"].ShouldNotBeNull();
            string authorityFile = authority["authority_record_file"]!.GetValue<string>();
            JsonObject authorityRecord = JsonNode.Parse(File.ReadAllText(Path.Combine(authorityPacket, authorityFile)))!
                .AsObject();
            DateTimeOffset createdAt = DateTimeOffset.Parse(
                authorityRecord["created_at"]!.GetValue<string>(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind);
            authorityRecord["updated_at"] = createdAt.AddSeconds(1).UtcDateTime.ToString(
                "yyyy-MM-dd'T'HH:mm:ss'Z'",
                CultureInfo.InvariantCulture);
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
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies a mutated consumption receipt schema fails closed.
    /// </summary>
    [Fact]
    public void CanonicalReleaseIdentityRejectsReceiptSchemaMutation()
    {
        string root = FindRepositoryRoot();
        string checkedInPacket = CheckedInStory314Packet(root);
        string temporary = Path.Combine(Path.GetTempPath(), $"eventstore-release-evidence-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporary);
        try
        {
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
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies repository-scoped role evidence is accepted when correctly scoped.
    /// </summary>
    [Fact]
    public void CanonicalReleaseIdentityAcceptsRepositoryScopedRoleEvidence()
    {
        string root = FindRepositoryRoot();
        string checkedInPacket = CheckedInStory314Packet(root);
        string temporary = Path.Combine(Path.GetTempPath(), $"eventstore-release-evidence-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporary);
        try
        {
            string scopedRolePacket = CopyPacket(
                checkedInPacket,
                Path.Combine(temporary, "repository-scoped-role"));
            JsonObject scopedRoleIdentity = LoadIdentity(scopedRolePacket);
            JsonNode scopedRoleAuthority = scopedRoleIdentity["authority"].ShouldNotBeNull();
            string scopedRoleFile = scopedRoleAuthority["role_evidence_file"]!.GetValue<string>();
            string scopedRolePath = Path.Combine(scopedRolePacket, scopedRoleFile);
            JsonNode legacyRoleResponse = JsonNode.Parse(File.ReadAllText(scopedRolePath)).ShouldNotBeNull();
            JsonObject scopedRoleEnvelope = new()
            {
                ["schema"] = "hexalith.github-repository-permission-evidence.v1",
                ["repository"] = "Hexalith/Hexalith.EventStore",
                ["request_url"] =
                    "https://api.github.com/repos/Hexalith/Hexalith.EventStore/collaborators/jpiquot/permission",
                ["response"] = legacyRoleResponse,
            };
            byte[] scopedRoleBytes = CanonicalJsonBytes(scopedRoleEnvelope);
            File.WriteAllBytes(scopedRolePath, scopedRoleBytes);
            scopedRoleAuthority["role_evidence_sha256"] = Sha256(scopedRoleBytes);
            RefreshPacketManifest(root, scopedRolePacket, scopedRoleIdentity);
            ProcessResult scopedRole = RunEvidenceValidator(
                root,
                Path.Combine(scopedRolePacket, "release-identity.json"),
                scopedRolePacket);
            scopedRole.ExitCode.ShouldBe(0, scopedRole.Output + scopedRole.Error);
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies repository-scoped role evidence fails closed when the request URL is mis-scoped.
    /// </summary>
    [Fact]
    public void CanonicalReleaseIdentityRejectsMisScopedRepositoryRoleEvidence()
    {
        string root = FindRepositoryRoot();
        string checkedInPacket = CheckedInStory314Packet(root);
        string temporary = Path.Combine(Path.GetTempPath(), $"eventstore-release-evidence-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporary);
        try
        {
            string scopedRolePacket = CopyPacket(
                checkedInPacket,
                Path.Combine(temporary, "repository-mis-scoped-role"));
            JsonObject scopedRoleIdentity = LoadIdentity(scopedRolePacket);
            JsonNode scopedRoleAuthority = scopedRoleIdentity["authority"].ShouldNotBeNull();
            string scopedRoleFile = scopedRoleAuthority["role_evidence_file"]!.GetValue<string>();
            string scopedRolePath = Path.Combine(scopedRolePacket, scopedRoleFile);
            JsonNode legacyRoleResponse = JsonNode.Parse(File.ReadAllText(scopedRolePath)).ShouldNotBeNull();
            JsonObject scopedRoleEnvelope = new()
            {
                ["schema"] = "hexalith.github-repository-permission-evidence.v1",
                ["repository"] = "Hexalith/Hexalith.EventStore",
                ["request_url"] =
                    "https://api.github.com/repos/Hexalith/Other/collaborators/jpiquot/permission",
                ["response"] = legacyRoleResponse,
            };
            byte[] wrongRepositoryRoleBytes = CanonicalJsonBytes(scopedRoleEnvelope);
            File.WriteAllBytes(scopedRolePath, wrongRepositoryRoleBytes);
            scopedRoleAuthority["role_evidence_sha256"] = Sha256(wrongRepositoryRoleBytes);
            RefreshPacketManifest(root, scopedRolePacket, scopedRoleIdentity);
            ProcessResult wrongRepositoryRole = RunEvidenceValidator(
                root,
                Path.Combine(scopedRolePacket, "release-identity.json"),
                scopedRolePacket);
            wrongRepositoryRole.ExitCode.ShouldNotBe(0);
            wrongRepositoryRole.Error.ShouldContain("repository role proof is invalid");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies handoff flags cannot grant deployment selection or mutation authority.
    /// </summary>
    /// <param name="field">The handoff boolean to force true.</param>
    [Theory]
    [InlineData("selects_deployed_identity")]
    [InlineData("grants_mutation_authority")]
    public void CanonicalReleaseIdentityRejectsDeployedIdentityAndMutationAuthorityHandoff(string field)
    {
        string root = FindRepositoryRoot();
        string checkedInPacket = CheckedInStory314Packet(root);
        string temporary = Path.Combine(Path.GetTempPath(), $"eventstore-release-evidence-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporary);
        try
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
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies unsupported codec version or digest cannot select a trusted live handler.
    /// </summary>
    /// <param name="field">The dispatch metadata field to mutate.</param>
    [Theory]
    [InlineData("version")]
    [InlineData("sha256")]
    public void CanonicalReleaseIdentityRejectsUntrustedHandlerDispatch(string field)
    {
        string root = FindRepositoryRoot();
        string checkedInPacket = CheckedInStory314Packet(root);
        string temporary = Path.Combine(Path.GetTempPath(), $"eventstore-release-evidence-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporary);
        try
        {
            string dispatchPacket = CopyPacket(
                checkedInPacket,
                Path.Combine(temporary, $"handler-{field}"));
            JsonObject dispatchIdentity = LoadIdentity(dispatchPacket);
            if (field == "version")
            {
                dispatchIdentity["codec"]![field] = 4;
            }
            else
            {
                dispatchIdentity["codec"]!["codec"]![field] = new string('0', 64);
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
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies unhashable dispatch metadata fails closed without a Python traceback.
    /// </summary>
    [Fact]
    public void CanonicalReleaseIdentityRejectsUnhashableDispatchMetadata()
    {
        string root = FindRepositoryRoot();
        string checkedInPacket = CheckedInStory314Packet(root);
        string temporary = Path.Combine(Path.GetTempPath(), $"eventstore-release-evidence-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporary);
        try
        {
            string unhashableDispatchPacket = CopyPacket(
                checkedInPacket,
                Path.Combine(temporary, "handler-unhashable-version"));
            JsonObject unhashableDispatchIdentity = LoadIdentity(unhashableDispatchPacket);
            unhashableDispatchIdentity["codec"]!["version"] = new JsonArray(1);
            File.WriteAllBytes(
                Path.Combine(unhashableDispatchPacket, "release-identity.json"),
                CanonicalJsonBytes(unhashableDispatchIdentity));
            ProcessResult unhashableDispatchMutation = RunEvidenceValidator(
                root,
                Path.Combine(unhashableDispatchPacket, "release-identity.json"),
                unhashableDispatchPacket);
            unhashableDispatchMutation.ExitCode.ShouldNotBe(0);
            unhashableDispatchMutation.Error.ShouldContain(
                "[corrective-release-evidence] fail: release identity dispatch metadata is invalid");
            unhashableDispatchMutation.Error.ShouldNotContain("Traceback");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies conflicting smoke outcomes in one log fail closed.
    /// </summary>
    [Fact]
    public void CanonicalReleaseIdentityRejectsConflictingSmokeOutcomes()
    {
        string root = FindRepositoryRoot();
        string checkedInPacket = CheckedInStory314Packet(root);
        string temporary = Path.Combine(Path.GetTempPath(), $"eventstore-release-evidence-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporary);
        try
        {
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
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies divergent per-platform smoke summaries fail closed.
    /// </summary>
    [Fact]
    public void CanonicalReleaseIdentityRejectsSplitSmokeSummaries()
    {
        string root = FindRepositoryRoot();
        string checkedInPacket = CheckedInStory314Packet(root);
        string temporary = Path.Combine(Path.GetTempPath(), $"eventstore-release-evidence-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporary);
        try
        {
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
    /// Verifies retained authority cannot expire before creation or exceed a 24-hour window.
    /// </summary>
    /// <param name="expiresOffsetSeconds">Expiry offset from the retained created_at timestamp.</param>
    [Theory]
    [InlineData(-60)]
    [InlineData(90001)]
    public void RetainedAuthorityRejectsInvalidValidityWindow(int expiresOffsetSeconds)
    {
        string root = FindRepositoryRoot();
        string source = CheckedInStory314Packet(root);
        string temporary = Path.Combine(Path.GetTempPath(), $"eventstore-authority-window-{Guid.NewGuid():N}");
        try
        {
            string packetRoot = CopyPacket(source, temporary);
            JsonObject identity = LoadIdentity(packetRoot);
            JsonNode authority = identity["authority"].ShouldNotBeNull();
            string authorityFile = authority["authority_record_file"]!.GetValue<string>();
            string authorityPath = Path.Combine(packetRoot, authorityFile);
            JsonObject record = JsonNode.Parse(File.ReadAllText(authorityPath))!.AsObject();
            JsonObject body = JsonNode.Parse(record["body"]!.GetValue<string>())!.AsObject();
            DateTimeOffset createdAt = DateTimeOffset.Parse(
                record["created_at"]!.GetValue<string>(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind);
            body["expires_at"] = createdAt.AddSeconds(expiresOffsetSeconds).UtcDateTime.ToString(
                "yyyy-MM-dd'T'HH:mm:ss'Z'",
                CultureInfo.InvariantCulture);
            record["body"] = Encoding.UTF8.GetString(CanonicalJsonBytes(body)).TrimEnd('\n');

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
            result.Error.ShouldNotContain("publication authority evidence");
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
    /// Verifies an edited authority record update timestamp fails closed independently of window bounds.
    /// </summary>
    [Fact]
    public void RetainedAuthorityRejectsEditedRecordTimestamp()
    {
        string root = FindRepositoryRoot();
        string source = CheckedInStory314Packet(root);
        string temporary = Path.Combine(Path.GetTempPath(), $"eventstore-authority-edit-{Guid.NewGuid():N}");
        try
        {
            string packetRoot = CopyPacket(source, temporary);
            JsonObject identity = LoadIdentity(packetRoot);
            JsonNode authority = identity["authority"].ShouldNotBeNull();
            string authorityFile = authority["authority_record_file"]!.GetValue<string>();
            string authorityPath = Path.Combine(packetRoot, authorityFile);
            JsonObject record = JsonNode.Parse(File.ReadAllText(authorityPath))!.AsObject();
            DateTimeOffset createdAt = DateTimeOffset.Parse(
                record["created_at"]!.GetValue<string>(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind);
            record["updated_at"] = createdAt.AddSeconds(1).UtcDateTime.ToString(
                "yyyy-MM-dd'T'HH:mm:ss'Z'",
                CultureInfo.InvariantCulture);

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
            result.Error.ShouldNotContain("publication authority evidence");
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
    /// Verifies an otherwise well-formed packet with an unsupported codec version cannot select a
    /// trusted live handler.
    /// </summary>
    [Fact]
    public void UnsupportedPacketVersionCannotSelectTrustedLiveHandler()
    {
        string root = FindRepositoryRoot();
        string checkedInPacket = Path.Combine(
            root, "_bmad-output", "implementation-artifacts", "evidence", "story-3-14",
            "f343bb0153e9cdcb8b12ec10153813072f5ad38d");
        string temporary = Path.Combine(Path.GetTempPath(), $"eventstore-unsupported-evidence-{Guid.NewGuid():N}");
        try
        {
            string packetRoot = CopyPacket(checkedInPacket, temporary);
            JsonObject identity = LoadIdentity(packetRoot);
            identity["codec"]!["version"] = 4;
            File.WriteAllBytes(
                Path.Combine(packetRoot, "release-identity.json"),
                CanonicalJsonBytes(identity));

            ProcessResult result = RunEvidenceValidator(
                root,
                Path.Combine(packetRoot, "release-identity.json"),
                packetRoot);
            result.ExitCode.ShouldNotBe(0);
            result.Error.ShouldContain("does not select a trusted live handler");
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
    /// Verifies an unreviewed edit to the executing live handler fails closed even when the retained
    /// packet's declared codec digest, schema, and version are all untouched and still dispatch to it.
    /// </summary>
    /// <param name="tamperedFile">Import-path file to append unreviewed code to.</param>
    [Theory]
    [InlineData("v3.py")]
    [InlineData("__init__.py")]
    public void TamperedLiveHandlerBytesCannotExecuteEvenWithAValidPacket(string tamperedFile)
    {
        string root = FindRepositoryRoot();
        string checkedInPacket = Path.Combine(
            root, "_bmad-output", "implementation-artifacts", "evidence", "story-3-14",
            "f343bb0153e9cdcb8b12ec10153813072f5ad38d");
        string temporary = Path.Combine(Path.GetTempPath(), $"eventstore-handler-tamper-{Guid.NewGuid():N}");
        try
        {
            string tools = Path.Combine(temporary, "tools");
            string handlers = Path.Combine(tools, "release_evidence_handlers");
            Directory.CreateDirectory(handlers);
            File.Copy(
                Path.Combine(root, "tools", "validate-corrective-release-evidence.py"),
                Path.Combine(tools, "validate-corrective-release-evidence.py"));
            File.Copy(
                Path.Combine(root, "tools", "release_evidence_handlers", "__init__.py"),
                Path.Combine(handlers, "__init__.py"));
            File.Copy(
                Path.Combine(root, "tools", "release_evidence_handlers", "v3.py"),
                Path.Combine(handlers, "v3.py"));

            // Control: without tampering, this partial tree must actually validate. Otherwise the
            // expected failure below could come from the incomplete copy rather than the guard.
            ProcessResult control = RunProcess(
                temporary,
                "python3",
                "tools/validate-corrective-release-evidence.py",
                Path.Combine(checkedInPacket, "release-identity.json"),
                "--manifest",
                Path.Combine(root, "tools", "release-packages.json"),
                "--packet-root",
                checkedInPacket);
            control.ExitCode.ShouldBe(0, control.Error);

            // Importing the leaf also executes its package initializer, so both are on the
            // import path and both must be pinned before the first import.
            File.AppendAllText(
                Path.Combine(handlers, tamperedFile), "\nprint('untrusted-handler-executed')\n");

            ProcessResult tampered = RunProcess(
                temporary,
                "python3",
                "tools/validate-corrective-release-evidence.py",
                Path.Combine(checkedInPacket, "release-identity.json"),
                "--manifest",
                Path.Combine(root, "tools", "release-packages.json"),
                "--packet-root",
                checkedInPacket);
            tampered.ExitCode.ShouldNotBe(0);
            tampered.Error.ShouldContain("trusted live handler source does not match its pinned SHA-256");
            tampered.Output.ShouldNotContain("untrusted-handler-executed");
        }
        finally
        {
            if (Directory.Exists(temporary))
            {
                Directory.Delete(temporary, recursive: true);
            }
        }

        ProcessResult restored = RunEvidenceValidator(
            root, Path.Combine(checkedInPacket, "release-identity.json"), checkedInPacket);
        restored.ExitCode.ShouldBe(0, restored.Error);
    }

    /// <summary>
    /// Verifies the source-only predecessor dispatcher removes its own tools directory from import
    /// resolution, so a repository-local zipfile shadow cannot execute from verified handler code.
    /// </summary>
    [Fact]
    public void CorrectiveDispatcherRejectsRepositoryLocalImportShadowing()
    {
        string root = FindRepositoryRoot();
        string checkedInPacket = Path.Combine(
            root, "_bmad-output", "implementation-artifacts", "evidence", "story-3-14",
            "f343bb0153e9cdcb8b12ec10153813072f5ad38d");
        string temporary = Path.Combine(Path.GetTempPath(), $"eventstore-corrective-shadow-{Guid.NewGuid():N}");
        try
        {
            string tools = Path.Combine(temporary, "tools");
            string handlers = Path.Combine(tools, "release_evidence_handlers");
            Directory.CreateDirectory(handlers);
            File.Copy(
                Path.Combine(root, "tools", "validate-corrective-release-evidence.py"),
                Path.Combine(tools, "validate-corrective-release-evidence.py"));
            File.Copy(
                Path.Combine(root, "tools", "release_evidence_handlers", "__init__.py"),
                Path.Combine(handlers, "__init__.py"));
            File.Copy(
                Path.Combine(root, "tools", "release_evidence_handlers", "v3.py"),
                Path.Combine(handlers, "v3.py"));
            File.WriteAllText(
                Path.Combine(tools, "zipfile.py"),
                "print('corrective-zipfile-shadow-executed')\nraise RuntimeError('shadow loaded')\n");

            ProcessResult result = RunProcess(
                temporary,
                "python3",
                "tools/validate-corrective-release-evidence.py",
                Path.Combine(checkedInPacket, "release-identity.json"),
                "--manifest",
                Path.Combine(root, "tools", "release-packages.json"),
                "--packet-root",
                checkedInPacket);

            result.ExitCode.ShouldBe(0, result.Error);
            result.Output.ShouldNotContain("corrective-zipfile-shadow-executed");
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
    /// Verifies preloaded modules are displaced before verified initializer and handler bytes
    /// execute. The trusted-name shape alone could not fail -- the source-only loader overwrites
    /// <c>sys.modules[name]</c> unconditionally before the handler is used -- so the case that
    /// actually exercises the displacement loop preloads a repository-local module the verified
    /// handler imports for real. Without the loop that fake would answer the handler's import.
    /// </summary>
    /// <param name="shape">Preloaded module shape.</param>
    [Theory]
    [InlineData("trusted-name")]
    [InlineData("repository-local-dependency")]
    public void CorrectiveDispatcherCannotReusePreloadedModules(string shape)
    {
        string root = FindRepositoryRoot();
        string checkedInPacket = Path.Combine(
            root, "_bmad-output", "implementation-artifacts", "evidence", "story-3-14",
            "f343bb0153e9cdcb8b12ec10153813072f5ad38d");
        string repositoryLocalOrigin = Path.Combine(root, "tools", "zipfile.py").Replace("\\", "/");
        string wrapper = shape == "trusted-name"
            ? "import runpy,sys,types;" +
                "p=types.ModuleType('release_evidence_handlers');p.__file__='/tmp/preloaded/__init__.py';p.__path__=[];" +
                "m=types.ModuleType('release_evidence_handlers.v3');m.__file__='/tmp/preloaded/v3.py';" +
                "m.validate_identity=lambda *a,**k:(print('preloaded-handler-executed'),b'')[1];" +
                "p.v3=m;sys.modules[p.__name__]=p;sys.modules[m.__name__]=m;" +
                "sys.argv=[sys.argv[1],*sys.argv[2:]];runpy.run_path(sys.argv[0],run_name='__main__')"
            : "import runpy,sys,types;" +
                "z=types.ModuleType('zipfile');" +
                $"z.__file__='{repositoryLocalOrigin}';" +
                "z.ZipFile=lambda *a,**k:(print('preloaded-handler-executed'),(_ for _ in ()).throw(RuntimeError('preloaded zipfile used')))[1];" +
                "z.BadZipFile=RuntimeError;sys.modules['zipfile']=z;" +
                "sys.argv=[sys.argv[1],*sys.argv[2:]];runpy.run_path(sys.argv[0],run_name='__main__')";

        ProcessResult result = RunProcess(
            root,
            "python3",
            "-c",
            wrapper,
            Path.Combine(root, "tools", "validate-corrective-release-evidence.py"),
            Path.Combine(checkedInPacket, "release-identity.json"),
            "--manifest",
            Path.Combine(root, "tools", "release-packages.json"),
            "--packet-root",
            checkedInPacket);

        result.ExitCode.ShouldBe(0, result.Error);
        result.Output.ShouldNotContain("preloaded-handler-executed");
        result.Output.ShouldContain("pass: sha256:");
    }

    /// <summary>
    /// Verifies the corrective dispatcher requires an exact JSON integer for the codec version.
    /// Without that check <c>3.0</c> passes: it hashes equal to <c>3</c>, so the handler tuple
    /// lookup succeeds and every downstream <c>!= 3</c> comparison is false -- demonstrated by
    /// deleting the guard in a scratch copy, which produced a full pass. The Story 3.15 sibling had
    /// this coverage; this one did not.
    /// </summary>
    /// <param name="jsonValue">Non-integer JSON value written into the codec version.</param>
    [Theory]
    [InlineData("3.0")]
    [InlineData("true")]
    public void CorrectiveDispatcherCodecVersionRequiresAnExactJsonInteger(string jsonValue)
    {
        string root = FindRepositoryRoot();
        string checkedInPacket = Path.Combine(
            root, "_bmad-output", "implementation-artifacts", "evidence", "story-3-14",
            "f343bb0153e9cdcb8b12ec10153813072f5ad38d");
        string identity = Path.Combine(checkedInPacket, "release-identity.json");
        string temporary = Path.Combine(
            Path.GetTempPath(),
            $"eventstore-corrective-version-{Guid.NewGuid():N}.json");
        try
        {
            // Control: the untouched identity file validates, so the mutation is the only variable.
            RunProcess(
                root,
                "python3",
                Path.Combine(root, "tools", "validate-corrective-release-evidence.py"),
                identity,
                "--manifest",
                Path.Combine(root, "tools", "release-packages.json"),
                "--packet-root",
                checkedInPacket).ExitCode.ShouldBe(0);

            string text = File.ReadAllText(identity);
            string mutated = Regex.Replace(text, "\"version\":3(?![0-9])", $"\"version\":{jsonValue}");
            mutated.ShouldNotBe(text);
            File.WriteAllText(temporary, mutated);

            ProcessResult result = RunProcess(
                root,
                "python3",
                Path.Combine(root, "tools", "validate-corrective-release-evidence.py"),
                temporary,
                "--manifest",
                Path.Combine(root, "tools", "release-packages.json"),
                "--packet-root",
                checkedInPacket);

            result.ExitCode.ShouldBe(1, result.Error);
            result.Error.ShouldContain("release identity dispatch metadata is invalid");
            result.Output.ShouldNotContain("pass:");
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    /// <summary>
    /// Verifies timestamp-valid stale bytecode cannot replace the exact v3 source bytes whose hash
    /// the corrective dispatcher checked.
    /// </summary>
    [Fact]
    public void CorrectiveDispatcherNeverExecutesStaleHandlerBytecode()
    {
        string root = FindRepositoryRoot();
        string checkedInPacket = Path.Combine(
            root, "_bmad-output", "implementation-artifacts", "evidence", "story-3-14",
            "f343bb0153e9cdcb8b12ec10153813072f5ad38d");
        string temporary = Path.Combine(Path.GetTempPath(), $"eventstore-corrective-pyc-{Guid.NewGuid():N}");
        try
        {
            string tools = Path.Combine(temporary, "tools");
            string handlers = Path.Combine(tools, "release_evidence_handlers");
            Directory.CreateDirectory(handlers);
            File.Copy(
                Path.Combine(root, "tools", "validate-corrective-release-evidence.py"),
                Path.Combine(tools, "validate-corrective-release-evidence.py"));
            File.Copy(
                Path.Combine(root, "tools", "release_evidence_handlers", "__init__.py"),
                Path.Combine(handlers, "__init__.py"));
            string handlerPath = Path.Combine(handlers, "v3.py");
            File.Copy(Path.Combine(root, "tools", "release_evidence_handlers", "v3.py"), handlerPath);

            byte[] trusted = File.ReadAllBytes(handlerPath);
            string trustedText = Encoding.UTF8.GetString(trusted);
            const string ReplaceableLine =
                "# sit in tools/ today, so a later fix to this file cannot invalidate an already-frozen packet.";
            int lineStart = trustedText.IndexOf(ReplaceableLine, StringComparison.Ordinal);
            lineStart.ShouldBeGreaterThan(0);
            string maliciousLine = "print('stale-corrective-bytecode-executed')".PadRight(ReplaceableLine.Length);
            maliciousLine.Length.ShouldBe(ReplaceableLine.Length);
            byte[] malicious = [.. trusted];
            Encoding.ASCII.GetBytes(maliciousLine).CopyTo(malicious, lineStart);
            DateTime timestampCandidate = DateTime.UtcNow.AddMinutes(-1);
            DateTime cacheTimestamp = new(
                timestampCandidate.Ticks - (timestampCandidate.Ticks % TimeSpan.TicksPerSecond),
                DateTimeKind.Utc);
            File.WriteAllBytes(handlerPath, malicious);
            File.SetLastWriteTimeUtc(handlerPath, cacheTimestamp);
            ProcessResult compile = RunProcess(temporary, "python3", "-m", "py_compile", handlerPath);
            compile.ExitCode.ShouldBe(0, compile.Error);
            File.WriteAllBytes(handlerPath, trusted);
            File.SetLastWriteTimeUtc(handlerPath, cacheTimestamp);

            // Control 1: py_compile really produced a cache. Without it this test passes when
            // nothing was written, which proves nothing about the loader.
            string cacheDirectory = Path.Combine(handlers, "__pycache__");
            Directory.Exists(cacheDirectory).ShouldBeTrue();
            Directory.GetFiles(cacheDirectory, "v3.*.pyc").Length.ShouldBeGreaterThan(0);

            // Control 2: the stale cache genuinely wins under an ordinary import, so the marker
            // would execute if the dispatcher resolved this module through importlib. Only with
            // this control does the negative assertion below mean the source-only loader is what
            // keeps the tampered bytecode out.
            ProcessResult control = RunProcess(
                tools,
                "python3",
                "-c",
                "import release_evidence_handlers.v3");
            control.ExitCode.ShouldBe(0, control.Error);
            control.Output.ShouldContain("stale-corrective-bytecode-executed");

            ProcessResult result = RunProcess(
                temporary,
                "python3",
                "tools/validate-corrective-release-evidence.py",
                Path.Combine(checkedInPacket, "release-identity.json"),
                "--manifest",
                Path.Combine(root, "tools", "release-packages.json"),
                "--packet-root",
                checkedInPacket);

            result.ExitCode.ShouldBe(0, result.Error);
            result.Output.ShouldNotContain("stale-corrective-bytecode-executed");
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
        string releaseSha = releasePin.Groups["sha"].Value;

        ProcessResult pinAvailability = RunProcess(builds, "git", "cat-file", "-e", $"{releaseSha}^{{commit}}");
        pinAvailability.ExitCode.ShouldBe(
            0,
            $"Pinned Builds release SHA {releaseSha} is unavailable in references/Hexalith.Builds. " +
            "Fetch that exact pin before running the immutable fixture. " + pinAvailability.Error);

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
                releaseSha,
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

    private static void UpdateBinding(JsonNode binding, byte[] bytes)
    {
        string hash = Sha256(bytes);
        binding["size"] = bytes.Length;
        binding["sha256"] = hash;
        binding["digest"] = $"sha256:{hash}";
    }

    private static string CheckedInStory314Packet(string root) =>
        Path.Combine(
            root,
            "_bmad-output",
            "implementation-artifacts",
            "evidence",
            "story-3-14",
            "f343bb0153e9cdcb8b12ec10153813072f5ad38d");

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

    private static Dictionary<string, string> ExpectedLabels(string created = Created) => new(StringComparer.Ordinal)
    {
        ["org.opencontainers.image.source"] = "https://github.com/Hexalith/Hexalith.EventStore",
        ["org.opencontainers.image.url"] =
            $"https://github.com/Hexalith/Hexalith.EventStore/releases/tag/v{Version}",
        ["org.opencontainers.image.documentation"] =
            $"https://github.com/Hexalith/Hexalith.EventStore/blob/{SourceSha}/README.md",
        ["org.opencontainers.image.revision"] = SourceSha,
        ["org.opencontainers.image.version"] = Version,
        ["org.opencontainers.image.created"] = created,
        ["org.opencontainers.artifact.created"] = created,
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
        => RunProcess(workingDirectory, fileName, null, arguments);

    private static ProcessResult RunProcess(
        string workingDirectory,
        string fileName,
        IReadOnlyDictionary<string, string>? environment,
        params string[] arguments)
    {
        ProcessStartInfo start = CreateProcessStartInfo(workingDirectory, fileName, arguments);
        if (environment is not null)
        {
            foreach ((string name, string value) in environment)
            {
                start.Environment[name] = value;
            }
        }

        using Process process = Process.Start(start).ShouldNotBeNull();
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit((int)HelperTimeout.TotalMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            string capturedOutput = output.GetAwaiter().GetResult();
            string capturedError = error.GetAwaiter().GetResult();
            throw new TimeoutException(
                $"Process '{fileName}' exceeded the {HelperTimeout.TotalSeconds}-second test timeout. " +
                $"Output: {capturedOutput} Error: {capturedError}");
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
            await process.WaitForExitAsync(timeoutSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync().ConfigureAwait(false);
            string[] timeoutDiagnostics = await Task.WhenAll(output, error).ConfigureAwait(false);
            throw new TimeoutException(
                $"Process '{fileName}' exceeded the {timeout.TotalSeconds}-second test timeout. " +
                $"Output: {timeoutDiagnostics[0]} Error: {timeoutDiagnostics[1]}");
        }

        string[] diagnostics = await Task.WhenAll(output, error).ConfigureAwait(false);
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

    private sealed record ProcessResult(int ExitCode, string Output, string Error);
}
