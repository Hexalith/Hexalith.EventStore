using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hexalith.EventStore.Contracts.Tests.Packaging;

/// <summary>
/// Verifies the unapproved OQ8 v5 candidate workflow preserves historical authority.
/// </summary>
public sealed class Oq8V5CandidateTests
{
    /// <summary>
    /// Exercises the active validator core with in-memory synthetic receipts only.
    /// </summary>
    [Fact]
    public void SyntheticActivePathFixtureIsNonAuthorizingAndRejectsMutations()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("The synthetic OQ8 fixture requires a POSIX Git checkout.");
        }

        string root = FindRepositoryRoot();
        (int code, string output, string error) = RunPython(
            root,
            "tests/Hexalith.EventStore.Contracts.Tests/Packaging/Fixtures/oq8-v5-synthetic-fixture.py");
        code.ShouldBe(0, error);
        output.ShouldContain("non-authorizing and in-memory only");
    }

    /// <summary>
    /// Exercises the reviewable packet schema without creating or accepting a receipt.
    /// </summary>
    [Fact]
    public void V5SubjectPacketRemainsInactiveAndRejectsChangedInputs()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("The historical OQ8 candidate tool requires a POSIX Git checkout.");
        }

        string root = FindRepositoryRoot();
        string schemaPath = Path.Combine(root, "tools", "oq8-v5-packet.schema.json");
        using JsonDocument schema = JsonDocument.Parse(File.ReadAllText(schemaPath));
        schema.RootElement.GetProperty("oneOf").GetArrayLength().ShouldBe(2);
        string packetPath = Path.Combine(Path.GetTempPath(), "oq8-v5-subject-draft-" + Guid.NewGuid() + ".json");
        try
        {
            (int generationCode, _, string generationError) = RunPython(
                root,
                "tools/oq8-v5-packet.py",
                "--prepare-draft",
                packetPath);
            generationCode.ShouldBe(0, generationError);
            (int draftCode, string draftOutput, string draftError) = RunPython(
                root,
                "tools/validate-oq8-platform-evidence.py",
                "--v5-subject-draft",
                packetPath);
            draftCode.ShouldBe(0, draftError);
            draftOutput.ShouldContain("inactive and unapproved");

            string rendered = File.ReadAllText(packetPath);
            using JsonDocument draft = JsonDocument.Parse(rendered);
            JsonElement packet = draft.RootElement;
            packet.GetProperty("status").GetString().ShouldBe("draft-unapproved");
            packet.GetProperty("frozenAt").ValueKind.ShouldBe(JsonValueKind.Null);
            packet.GetProperty("reviews").GetProperty("architecture").GetString().ShouldBe("pending");
            packet.GetProperty("authority").GetProperty("currentSourceApproved").GetBoolean().ShouldBeFalse();
            packet.GetProperty("subjectInputs").GetProperty("sourceIdentity")
                .GetProperty("releaseSourceSha256")
                .TryGetProperty("tools/oq8-v5-packet.py", out _).ShouldBeTrue();

            JsonNode mutation = JsonNode.Parse(rendered).ShouldNotBeNull();
            mutation["authority"]!["currentSourceApproved"] = true;
            File.WriteAllText(packetPath, mutation.ToJsonString());
            (int mutationCode, _, string mutationError) = RunPython(
                root,
                "tools/validate-oq8-platform-evidence.py",
                "--v5-subject-draft",
                packetPath);
            mutationCode.ShouldBe(1);
            mutationError.ShouldContain("subject-input draft validation failed");
        }
        finally
        {
            File.Delete(packetPath);
        }

        (int activeCode, _, string activeError) = RunPython(root, "tools/oq8-v5-packet.py", "--validate-active");
        activeCode.ShouldBe(1);
        activeError.ShouldContain("V5 packet directory unavailable");
    }

    /// <summary>
    /// The versioned validator accepts only a current, explicitly unapproved draft.
    /// </summary>
    [Fact]
    public void V5DraftValidatorRejectsAuthorityAndSourceMutations()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("The historical OQ8 candidate tool requires a POSIX Git checkout.");
        }

        string root = FindRepositoryRoot();
        (int generationCode, string generated, string generationError) = RunPython(
            root,
            "tools/prepare-oq8-v5-candidate.py");
        generationCode.ShouldBe(0, generationError);
        string candidatePath = Path.Combine(Path.GetTempPath(), "oq8-v5-draft-" + Guid.NewGuid() + ".json");
        try
        {
            File.WriteAllText(candidatePath, generated);
            (int acceptedCode, string accepted, string acceptedError) = RunPython(
                root,
                "tools/validate-oq8-platform-evidence.py",
                "--v5-candidate",
                candidatePath);
            acceptedCode.ShouldBe(0, acceptedError);
            accepted.ShouldContain("inactive and unapproved");

            JsonNode authorityMutation = JsonNode.Parse(generated).ShouldNotBeNull();
            authorityMutation["authority"]!["releaseApproved"] = true;
            File.WriteAllText(candidatePath, authorityMutation.ToJsonString());
            (int authorityCode, _, string authorityError) = RunPython(
                root,
                "tools/validate-oq8-platform-evidence.py",
                "--v5-candidate",
                candidatePath);
            authorityCode.ShouldBe(1);
            authorityError.ShouldContain("claims source or publication authority");

            JsonNode reviewMutation = JsonNode.Parse(generated).ShouldNotBeNull();
            reviewMutation["review"]!["architecture"] = "approved";
            File.WriteAllText(candidatePath, reviewMutation.ToJsonString());
            (int reviewCode, _, string reviewError) = RunPython(
                root,
                "tools/validate-oq8-platform-evidence.py",
                "--v5-candidate",
                candidatePath);
            reviewCode.ShouldBe(1);
            reviewError.ShouldContain("claims review authority");

            JsonNode sourceMutation = JsonNode.Parse(generated).ShouldNotBeNull();
            sourceMutation["changedGateInputs"]![
                "tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs"]![
                "candidateSha256"] = new string('0', 64);
            File.WriteAllText(candidatePath, sourceMutation.ToJsonString());
            (int sourceCode, _, string sourceError) = RunPython(
                root,
                "tools/validate-oq8-platform-evidence.py",
                "--v5-candidate",
                candidatePath);
            sourceCode.ShouldBe(1);
            sourceError.ShouldContain("draft source or historical identity drift");
        }
        finally
        {
            File.Delete(candidatePath);
        }
    }

    /// <summary>
    /// Runs the historical validators and checks that the draft has no approval authority.
    /// </summary>
    [Fact]
    public void DraftCandidateVerifiesV1ThroughV4WithoutActivatingV5()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("The historical OQ8 candidate tool requires a POSIX Git checkout.");
        }

        string root = FindRepositoryRoot();
        string selector = Path.Combine(
            root,
            "_bmad-output",
            "implementation-artifacts",
            "4-15-oq8-platform-closure-successor.json");
        string v4Manifest = Path.Combine(
            root,
            "_bmad-output",
            "implementation-artifacts",
            "evidence",
            "story-4-15-successors",
            "v4",
            "closure-sha256.txt");
        byte[] selectorBefore = SHA256.HashData(File.ReadAllBytes(selector));
        byte[] manifestBefore = SHA256.HashData(File.ReadAllBytes(v4Manifest));

        ProcessStartInfo start = new("python3")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("tools/prepare-oq8-v5-candidate.py");
        using Process process = Process.Start(start).ShouldNotBeNull();
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        process.ExitCode.ShouldBe(0, error);

        using JsonDocument candidate = JsonDocument.Parse(output);
        JsonElement document = candidate.RootElement;
        document.GetProperty("status").GetString().ShouldBe("draft-unapproved");
        JsonElement historical = document.GetProperty("historical").GetProperty("validation");
        foreach (string version in new[] { "v1", "v2", "v3", "v4" })
        {
            historical.GetProperty(version).GetString().ShouldBe("passed");
        }

        JsonElement authority = document.GetProperty("authority");
        authority.GetProperty("currentSourceApproved").GetBoolean().ShouldBeFalse();
        authority.GetProperty("releaseApproved").GetBoolean().ShouldBeFalse();
        authority.GetProperty("packageAuthority").GetBoolean().ShouldBeFalse();
        authority.GetProperty("registryAuthority").GetBoolean().ShouldBeFalse();
        JsonElement review = document.GetProperty("review");
        review.GetProperty("subjectFrozen").GetBoolean().ShouldBeFalse();
        review.GetProperty("subjectSha256").ValueKind.ShouldBe(JsonValueKind.Null);
        review.GetProperty("architecture").GetString().ShouldBe("pending");
        review.GetProperty("security").GetString().ShouldBe("pending");
        review.GetProperty("test").GetString().ShouldBe("pending");
        document.GetProperty("changedGateInputs")
            .TryGetProperty(
                "tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs",
                out _).ShouldBeTrue();
        document.GetProperty("changedGateInputs")
            .TryGetProperty("docs/ci.md", out _).ShouldBeTrue();
        document.GetProperty("changedGateInputs")
            .TryGetProperty("tools/validate-oq8-platform-evidence.py", out _).ShouldBeTrue();

        SHA256.HashData(File.ReadAllBytes(selector)).ShouldBe(selectorBefore);
        SHA256.HashData(File.ReadAllBytes(v4Manifest)).ShouldBe(manifestBefore);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "tools", "prepare-oq8-v5-candidate.py")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("EventStore repository root not found.");
    }

    private static (int ExitCode, string Output, string Error) RunPython(
        string root,
        string script,
        params string[] arguments)
    {
        ProcessStartInfo start = new("python3")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add(script);
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start).ShouldNotBeNull();
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output, error);
    }
}
