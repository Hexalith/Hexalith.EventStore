using System.Diagnostics;
using System.Formats.Tar;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Hexalith.EventStore.Contracts.Tests.Packaging;

/// <summary>
/// Exercises the Story 3.14 corrective OCI provenance build contract.
/// </summary>
public sealed class CorrectiveOciProvenanceReleaseTests
{
    private const string SourceSha = "dddddddddddddddddddddddddddddddddddddddd";
    private const string Version = "0.0.0-ci-test";
    private const string Created = "2026-08-21T09:15:00Z";
    private static readonly TimeSpan HelperTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan PublishTimeout = TimeSpan.FromMinutes(8);

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
    /// Verifies the shared authority fixtures execute replay, mismatch, expiry, and wrong-role cases.
    /// </summary>
    [Fact]
    public void PublicationAuthorityFixturesPassWithoutSkippedCases()
    {
        string root = FindRepositoryRoot();
        string builds = PackagingRepositoryPaths.ResolveDependency(
            root, "Hexalith.Builds", Environment.GetEnvironmentVariable("HEXALITH_BUILDS_SOURCE"));
        string workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "release.yml"));
        Match releasePin = Regex.Match(
            workflow,
            @"repository: Hexalith/Hexalith\.Builds\s+ref: (?<sha>[0-9a-f]{40})");
        releasePin.Success.ShouldBeTrue();
        workflow.ShouldContain("uses: ./.hexalith/builds-execution/Github/publish-containers");
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
    /// pinned provenance labels. The v3.96.2 release shipped with
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

        if (fileName == "git")
        {
            PackagingRepositoryPaths.RemoveRepositorySelectors(start);
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
        if (fileName == "git")
        {
            PackagingRepositoryPaths.RemoveRepositorySelectors(start);
            start.ArgumentList.Add("--no-replace-objects");
        }

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
                return PackagingRepositoryPaths.VerifyRepositoryRoot(directory.FullName, "Hexalith.EventStore");
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the EventStore repository root.");
    }

    private sealed record ProcessResult(int ExitCode, string Output, string Error);
}
