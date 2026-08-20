using System.Diagnostics;
using System.Formats.Tar;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hexalith.EventStore.Contracts.Tests.Packaging;

/// <summary>
/// Exercises the Story 3.14 corrective OCI provenance and evidence matrix.
/// </summary>
public sealed class CorrectiveOciProvenanceReleaseTests
{
    private const string SourceSha = "dddddddddddddddddddddddddddddddddddddddd";
    private const string Version = "0.0.0-ci-test";

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
            ProcessStartInfo start = new("dotnet")
            {
                WorkingDirectory = root,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };
            foreach (string argument in new[]
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
            })
            {
                start.ArgumentList.Add(argument);
            }

            using Process process = Process.Start(start).ShouldNotBeNull();
            Task<string> output = process.StandardOutput.ReadToEndAsync();
            Task<string> error = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            string[] diagnostics = await Task.WhenAll(output, error);
            process.ExitCode.ShouldBe(0, diagnostics[0] + diagnostics[1]);

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
            foreach (JsonElement descriptor in manifests)
            {
                string manifestDigest = descriptor.GetProperty("digest").GetString().ShouldNotBeNull();
                using JsonDocument manifest = JsonDocument.Parse(entries[BlobPath(manifestDigest)]);
                string configDigest = manifest.RootElement.GetProperty("config").GetProperty("digest")
                    .GetString().ShouldNotBeNull();
                using JsonDocument config = JsonDocument.Parse(entries[BlobPath(configDigest)]);
                JsonElement labels = config.RootElement.GetProperty("config").GetProperty("Labels");
                ValidateLabels(labels, expected);
            }
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
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
    }

    /// <summary>
    /// Verifies release selection uses the first absent version above every observed destination floor.
    /// </summary>
    [Fact]
    public void CandidateSelectionUsesANewerAbsentVersionWithoutHardCodingProjection()
    {
        string script =
            "from tools.release_evidence_codec import select_absent_version;" +
            "print(select_absent_version(['3.94.1','3.95.0','3.96.0'], {'3.96.1'}))";
        ProcessResult result = RunProcess(FindRepositoryRoot(), "python3", "-c", script);
        result.ExitCode.ShouldBe(0, result.Error);
        result.Output.Trim().ShouldBe("3.96.2");
    }

    /// <summary>
    /// Verifies a partial write is immutable non-authorizing evidence and requires a new version and authority.
    /// </summary>
    [Fact]
    public void PartialPublicationIsQuarantinedAndCannotBeRetriedInPlace()
    {
        string script =
            "from tools.release_evidence_codec import publication_disposition as d;" +
            "import json;print(json.dumps(d('3.96.1',['nuget:Contracts'],False),sort_keys=True))";
        ProcessResult result = RunProcess(FindRepositoryRoot(), "python3", "-c", script);
        result.ExitCode.ShouldBe(0, result.Error);
        using JsonDocument disposition = JsonDocument.Parse(result.Output);
        disposition.RootElement.GetProperty("result").GetString().ShouldBe("partial");
        disposition.RootElement.GetProperty("immutable_non_authorizing").GetBoolean().ShouldBeTrue();
        disposition.RootElement.GetProperty("retry_requires_new_version").GetBoolean().ShouldBeTrue();
        disposition.RootElement.GetProperty("retry_requires_new_authority").GetBoolean().ShouldBeTrue();
    }

    /// <summary>
    /// Verifies the shared authority fixtures execute replay, mismatch, expiry, and wrong-role cases.
    /// </summary>
    [Fact]
    public void PublicationAuthorityFixturesPassWithoutSkippedCases()
    {
        string builds = Path.Combine(FindRepositoryRoot(), "references", "Hexalith.Builds");
        ProcessResult result = RunProcess(
            builds,
            "python3",
            "-m", "unittest", "-v",
            "Github.publish-containers.tests.test_publication_preflight.PublicationPreflightTests.test_github_authority_binds_identity_owner_expiry_and_one_use_consumption",
            "Github.publish-containers.tests.test_publication_preflight.PublicationPreflightTests.test_github_authority_rejects_expired_wrong_owner_and_identity_mismatch");
        result.ExitCode.ShouldBe(0, result.Output + result.Error);
        result.Error.ShouldContain("Ran 2 tests");
        result.Error.ShouldContain("OK");
        result.Error.ShouldNotContain("skipped");
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
    };

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

        using Process process = Process.Start(start).ShouldNotBeNull();
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return new(process.ExitCode, output.GetAwaiter().GetResult(), error.GetAwaiter().GetResult());
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
