using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hexalith.EventStore.Contracts.Tests.Packaging;

/// <summary>
/// Guards the EventStore-owned edge of the shared multi-platform publication contract.
/// </summary>
public sealed class ContainerPublishingGovernanceTests
{
    private const string ApprovedBuildsReleaseSha = "cf04c419378dfe1bd3c41a9244b5e3283092056e";

    /// <summary>
    /// Verifies that release automation never attempts to bypass the pull-request-only main branch.
    /// </summary>
    [Fact]
    public void SemanticReleaseDoesNotPushGeneratedCommitsToProtectedMain()
    {
        string root = FindRepositoryRoot();
        using JsonDocument configuration = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(root, ".releaserc.json")));
        string[] pluginNames = configuration.RootElement
            .GetProperty("plugins")
            .EnumerateArray()
            .Select(plugin => plugin.ValueKind == JsonValueKind.String
                ? plugin.GetString()
                : plugin.EnumerateArray().First().GetString())
            .Where(name => name is not null)
            .Select(name => name!)
            .ToArray();

        pluginNames.ShouldNotContain("@semantic-release/git");
        pluginNames.ShouldNotContain("@semantic-release/changelog");
        pluginNames.ShouldContain("@semantic-release/exec");
        pluginNames.ShouldContain("@semantic-release/github");
    }

    /// <summary>
    /// Verifies that the publication preflight runs before the first irreversible command.
    /// </summary>
    [Fact]
    public void SemanticReleaseRequiresPreflightBeforeTagNuGetAndContainerPublication()
    {
        string root = FindRepositoryRoot();
        using JsonDocument configuration = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(root, ".releaserc.json")));
        JsonElement execConfiguration = configuration.RootElement
            .GetProperty("plugins")
            .EnumerateArray()
            .Where(plugin => plugin.ValueKind == JsonValueKind.Array)
            .Select(plugin => plugin.EnumerateArray().ToArray())
            .Where(plugin => plugin.Length == 2 && plugin[1].ValueKind == JsonValueKind.Object)
            .Select(plugin => plugin[1])
            .Where(plugin => plugin.TryGetProperty("publishCmd", out _))
            .Single();
        string verifyReleaseCommand = execConfiguration
            .GetProperty("verifyReleaseCmd")
            .GetString()
            .ShouldNotBeNull();
        string publishCommand = execConfiguration
            .GetProperty("publishCmd")
            .GetString()
            .ShouldNotBeNull();

        int verifySecretPreflight = verifyReleaseCommand.IndexOf(
            "scripts/validate-release-secrets.sh",
            StringComparison.Ordinal);
        int verifyPublicationPreflight = verifyReleaseCommand.IndexOf(
            "scripts/validate-publication-preflight.sh",
            StringComparison.Ordinal);
        verifySecretPreflight.ShouldBeGreaterThanOrEqualTo(0);
        verifyPublicationPreflight.ShouldBeGreaterThan(verifySecretPreflight);
        verifyReleaseCommand.ShouldContain("${nextRelease.version} verify");
        verifyReleaseCommand.ShouldNotContain("dotnet nuget push");
        verifyReleaseCommand.ShouldNotContain("publish-containers.sh");

        int secretPreflight = publishCommand.IndexOf("scripts/validate-release-secrets.sh", StringComparison.Ordinal);
        int publicationPreflight = publishCommand.IndexOf(
            "scripts/validate-publication-preflight.sh",
            StringComparison.Ordinal);
        int nugetPublish = publishCommand.IndexOf("dotnet nuget push", StringComparison.Ordinal);
        int containerPublish = publishCommand.IndexOf("./.hexalith/release/publish-containers.sh", StringComparison.Ordinal);

        secretPreflight.ShouldBeGreaterThanOrEqualTo(0);
        publicationPreflight.ShouldBeGreaterThan(secretPreflight);
        publishCommand.ShouldContain("${nextRelease.version} publish");
        nugetPublish.ShouldBeGreaterThan(publicationPreflight);
        containerPublish.ShouldBeGreaterThan(nugetPublish);
        publishCommand.ShouldNotContain("--skip-duplicate");
    }

    /// <summary>
    /// Verifies that the local wrapper delegates immutable identity and destination checks to the shared preflight.
    /// </summary>
    [Fact]
    public void PublicationPreflightWrapperBindsReleaseIdentityAndSharedContract()
    {
        string root = FindRepositoryRoot();
        string scriptPath = Path.Combine(root, "scripts", "validate-publication-preflight.sh");
        File.Exists(scriptPath).ShouldBeTrue();
        string script = File.ReadAllText(scriptPath);

        script.ShouldContain("./.hexalith/release/publication_preflight.py");
        script.ShouldContain("HEXALITH_BUILDS_EXECUTION_SHA");
        script.ShouldContain("HEXALITH_RELEASE_ENVIRONMENT");
        script.ShouldContain("HEXALITH_RELEASE_SOURCE_BRANCH");
        script.ShouldContain("HEXALITH_RELEASE_SOURCE_CI_WORKFLOW");
        script.ShouldContain("HEXALITH_RELEASE_PACKAGE_MANIFEST");
        script.ShouldContain("GITHUB_SHA");
        script.ShouldNotContain("git rev-parse HEAD");
        script.ShouldContain("tools/release-packages.json");
        script.ShouldNotContain("HEXALITH_RELEASE_AUTHORITY_URL");
        script.ShouldNotContain("1-20-github-approval-role-allowlist.json");
        script.ShouldContain("--phase \"$phase\"");
        script.ShouldContain("--source-branch \"$source_branch\"");
        script.ShouldContain("--source-ci-workflow \"$source_ci_workflow\"");
        script.ShouldContain("--package-manifest \"$package_manifest\"");
        script.ShouldContain("registry.hexalith.com/eventstore");
    }

    /// <summary>
    /// Verifies that the caller uses one immutable release pin independently of the development gitlink.
    /// </summary>
    [Fact]
    public void ReleaseCallerPinsSharedExecutionAndOneMappingWithoutCommentAuthority()
    {
        string root = FindRepositoryRoot();
        string workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "release.yml"));

        Match releaseWorkflow = Regex.Match(
            workflow,
            @"uses: Hexalith/Hexalith\.Builds/\.github/workflows/domain-release\.yml@(?<sha>[0-9a-f]{40})");
        releaseWorkflow.Success.ShouldBeTrue();
        string buildsSha = releaseWorkflow.Groups["sha"].Value;
        buildsSha.ShouldBe(ApprovedBuildsReleaseSha);
        workflow.ShouldContain($"builds-execution-sha: {buildsSha}");
        workflow.ShouldNotContain("domain-release.yml@main");
        workflow.ShouldNotContain("vars.HEXALITH_BUILDS_RELEASE_SHA");
        workflow.ShouldContain("environment-name: production");
        workflow.ShouldContain("actions: read");
        workflow.ShouldContain("source-branch: main");
        workflow.ShouldContain("source-ci-workflow: ci.yml");
        workflow.ShouldContain("package-manifest: tools/release-packages.json");
        workflow.ShouldNotContain("release-authority-url:");
        workflow.ShouldNotContain("release-owner-allowlist:");
        workflow.ShouldNotContain("references/Hexalith.Builds");
        workflow.ShouldNotContain("secrets: inherit");

        string gitlink = RunGit(root, "ls-tree", "HEAD", "references/Hexalith.Builds");
        Match gitlinkEntry = Regex.Match(gitlink, @"^160000 commit (?<sha>[0-9a-f]{40})\s+references/Hexalith\.Builds$");
        gitlinkEntry.Success.ShouldBeTrue();
        gitlinkEntry.Groups["sha"].Value.ShouldNotBe(ApprovedBuildsReleaseSha);

        string inputsBlock = ExtractYamlBlock(workflow, "    with:");
        MatchCollection timeoutInputs = Regex.Matches(
            inputsBlock,
            @"(?m)^\s{6}timeout-minutes:\s*(?<minutes>\d+)\s*$");
        timeoutInputs.Count.ShouldBe(1);
        timeoutInputs[0].Groups["minutes"].Value.ShouldBe("60");

        string mappingBlock = ExtractYamlBlock(workflow, "      container-projects: |");
        mappingBlock
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ShouldBe(["src/Hexalith.EventStore/Hexalith.EventStore.csproj|eventstore"]);

        string secretsBlock = ExtractYamlBlock(workflow, "    secrets:");
        string[] secretNames = Regex.Matches(secretsBlock, @"(?m)^\s{6}([A-Z0-9_]+):")
            .Select(match => match.Groups[1].Value)
            .Order(StringComparer.Ordinal)
            .ToArray();
        secretNames.ShouldBe(
            ["HEXALITH_ZOT_API_KEY", "HEXALITH_ZOT_USERNAME", "NUGET_API_KEY"]);
    }

    /// <summary>
    /// Verifies that release is manual and invalid source cannot reach the protected release job.
    /// </summary>
    [Fact]
    public void ReleaseWorkflowRequiresExactGreenMainBeforeProtectedReleaseJob()
    {
        string root = FindRepositoryRoot();
        string workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "release.yml"));

        workflow.ShouldContain("  workflow_dispatch:");
        workflow.ShouldNotContain("workflow_run:");
        workflow.ShouldNotContain("  push:");
        workflow.ShouldContain("group: release-production");
        workflow.ShouldContain("cancel-in-progress: false");
        workflow.ShouldContain("DISPATCH_REF: ${{ github.ref }}");
        workflow.ShouldContain("DISPATCH_SHA: ${{ github.sha }}");
        workflow.ShouldContain("refs/heads/main");
        workflow.ShouldContain("git/ref/heads/main");
        workflow.ShouldContain("actions/workflows/ci.yml/runs");
        workflow.ShouldContain(".head_sha == $sha");
        workflow.ShouldContain(".event == \"push\"");
        workflow.ShouldContain(".conclusion == \"success\"");
        workflow.ShouldContain("release:\n    needs: verify-source");
        workflow.IndexOf("verify-source:", StringComparison.Ordinal).ShouldBeLessThan(
            workflow.IndexOf("  release:", StringComparison.Ordinal));
    }

    /// <summary>
    /// Verifies the manual source preflight rejects wrong refs, stale heads, and missing exact-source CI.
    /// </summary>
    [Fact]
    public void ReleaseSourcePreflightFailsClosedAndAcceptsOnlyExactSuccessfulPushCi()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        string root = FindRepositoryRoot();
        string workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "release.yml"));
        string script = ExtractNamedWorkflowRunBlock(
            workflow,
            "Require current main with successful exact-source CI");
        string dispatchSha = new('a', 40);
        string staleMainSha = new('b', 40);

        RunReleaseSourcePreflight(script, "refs/heads/release", dispatchSha, dispatchSha, []).ShouldNotBe(0);
        RunReleaseSourcePreflight(script, "refs/heads/main", dispatchSha, staleMainSha, []).ShouldNotBe(0);
        RunReleaseSourcePreflight(script, "refs/heads/main", dispatchSha, dispatchSha, []).ShouldNotBe(0);

        object[] successfulRun =
        [
            new
            {
                head_sha = dispatchSha,
                head_branch = "main",
                @event = "push",
                status = "completed",
                conclusion = "success",
            },
        ];
        RunReleaseSourcePreflight(
            script,
            "refs/heads/main",
            dispatchSha,
            dispatchSha,
            successfulRun).ShouldBe(0);
    }

    /// <summary>
    /// Verifies that a preflight rejection prevents both external mutation commands.
    /// </summary>
    [Fact]
    public void RejectedPreflightBehaviorallyBlocksNuGetAndContainerMutation()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        string root = FindRepositoryRoot();
        string temporary = Path.Combine(Path.GetTempPath(), $"hexalith-preflight-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporary);
        try
        {
            string rejectingValidator = Path.Combine(temporary, "reject-preflight.sh");
            File.WriteAllText(rejectingValidator, "#!/usr/bin/env bash\nexit 1\n");
            File.SetUnixFileMode(
                rejectingValidator,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            string nugetMarker = Path.Combine(temporary, "nuget-ran");
            string containerMarker = Path.Combine(temporary, "container-ran");
            ProcessStartInfo start = new("bash")
            {
                WorkingDirectory = root,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };
            start.ArgumentList.Add("-c");
            start.ArgumentList.Add("bash \"$1\" 99.0.0 publish && touch \"$2\" && touch \"$3\"");
            start.ArgumentList.Add("preflight-test");
            start.ArgumentList.Add(Path.Combine(root, "scripts", "validate-publication-preflight.sh"));
            start.ArgumentList.Add(nugetMarker);
            start.ArgumentList.Add(containerMarker);
            start.Environment["HEXALITH_BUILDS_EXECUTION_SHA"] = new string('a', 40);
            start.Environment["HEXALITH_RELEASE_ENVIRONMENT"] = "production";
            start.Environment["HEXALITH_RELEASE_SOURCE_BRANCH"] = "main";
            start.Environment["HEXALITH_RELEASE_SOURCE_CI_WORKFLOW"] = "ci.yml";
            start.Environment["HEXALITH_RELEASE_PACKAGE_MANIFEST"] = "tools/release-packages.json";
            start.Environment["GITHUB_SHA"] = new string('b', 40);
            start.Environment["HEXALITH_PUBLICATION_PREFLIGHT"] = rejectingValidator;
            start.Environment["HEXALITH_ZOT_REGISTRY"] = "registry.hexalith.com";

            using Process process = Process.Start(start).ShouldNotBeNull();
            process.WaitForExit();

            process.ExitCode.ShouldNotBe(0);
            File.Exists(nugetMarker).ShouldBeFalse();
            File.Exists(containerMarker).ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies active CI documentation and preserved EventStore release/container scope.
    /// </summary>
    [Fact]
    public void DocumentationAndContainerDefaultsDescribeTheExactReleaseContract()
    {
        string root = FindRepositoryRoot();
        string ci = File.ReadAllText(Path.Combine(root, "docs", "ci.md"));
        string secrets = File.ReadAllText(Path.Combine(root, "docs", "ci-secrets-checklist.md"));
        string targets = File.ReadAllText(Path.Combine(root, "Directory.Build.targets"));
        string project = File.ReadAllText(
            Path.Combine(root, "src", "Hexalith.EventStore", "Hexalith.EventStore.csproj"));

        ci.ShouldContain("application/vnd.oci.image.index.v1+json");
        ci.ShouldContain("linux/amd64");
        ci.ShouldContain("linux/arm64");
        ci.ShouldContain("environment/emulation-setup-failure");
        ci.ShouldContain("Story 1.20");
        secrets.ShouldContain("HEXALITH_ZOT_USERNAME");
        secrets.ShouldContain("HEXALITH_ZOT_API_KEY");
        secrets.ShouldContain("Total user-managed secrets: 8");
        targets.ShouldContain("mcr.microsoft.com/dotnet/aspnet:10.0-alpine");
        targets.ShouldContain("<ContainerUser>app</ContainerUser>");
        targets.ShouldContain("<ContainerPort Include=\"8080\"");
        project.ShouldContain("<ContainerRepository>eventstore</ContainerRepository>");
    }

    private static string ExtractYamlBlock(string source, string marker)
    {
        string normalized = source.Replace("\r\n", "\n", StringComparison.Ordinal);
        string[] lines = normalized.Split('\n');
        int markerIndex = Array.FindIndex(lines, line => line.Equals(marker, StringComparison.Ordinal));
        markerIndex.ShouldBeGreaterThanOrEqualTo(0);
        int markerIndent = lines[markerIndex].TakeWhile(char.IsWhiteSpace).Count();
        List<string> block = [];
        for (int index = markerIndex + 1; index < lines.Length; index++)
        {
            string line = lines[index];
            if (line.Length > 0 && line.TakeWhile(char.IsWhiteSpace).Count() <= markerIndent)
            {
                break;
            }

            block.Add(line);
        }

        return string.Join('\n', block);
    }

    private static string ExtractNamedWorkflowRunBlock(string source, string stepName)
    {
        string[] lines = source.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        int stepIndex = Array.FindIndex(lines, line => line.Trim().Equals($"- name: {stepName}", StringComparison.Ordinal));
        stepIndex.ShouldBeGreaterThanOrEqualTo(0);
        int runIndex = Array.FindIndex(
            lines,
            stepIndex + 1,
            line => line.Trim().Equals("run: |", StringComparison.Ordinal));
        runIndex.ShouldBeGreaterThan(stepIndex);
        int runIndent = lines[runIndex].TakeWhile(char.IsWhiteSpace).Count();
        List<string> block = [];
        for (int index = runIndex + 1; index < lines.Length; index++)
        {
            string line = lines[index];
            int indent = line.TakeWhile(char.IsWhiteSpace).Count();
            if (line.Length > 0 && indent <= runIndent)
            {
                break;
            }

            block.Add(line.Length == 0 ? string.Empty : line[(runIndent + 2)..]);
        }

        return string.Join('\n', block) + "\n";
    }

    private static int RunReleaseSourcePreflight(
        string script,
        string dispatchRef,
        string dispatchSha,
        string liveMainSha,
        object[] workflowRuns)
    {
        string temporary = Path.Combine(Path.GetTempPath(), $"hexalith-release-source-{Guid.NewGuid():N}");
        string fakeBin = Path.Combine(temporary, "bin");
        Directory.CreateDirectory(fakeBin);
        try
        {
            string fakeGh = Path.Combine(fakeBin, "gh");
            File.WriteAllText(
                fakeGh,
                "#!/usr/bin/env bash\n" +
                "set -euo pipefail\n" +
                "if [[ \"$*\" == *\"/git/ref/heads/main\"* ]]; then\n" +
                "  printf '%s\\n' \"$FAKE_LIVE_MAIN_SHA\"\n" +
                "else\n" +
                "  printf '%s\\n' \"$FAKE_CI_RUNS\"\n" +
                "fi\n");
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(
                    fakeGh,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }

            ProcessStartInfo start = new("bash")
            {
                WorkingDirectory = temporary,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };
            start.ArgumentList.Add("-c");
            start.ArgumentList.Add(script);
            start.Environment["PATH"] = $"{fakeBin}:{start.Environment["PATH"]}";
            start.Environment["GH_TOKEN"] = "test-token";
            start.Environment["REPOSITORY"] = "Hexalith/Hexalith.EventStore";
            start.Environment["DISPATCH_REF"] = dispatchRef;
            start.Environment["DISPATCH_SHA"] = dispatchSha;
            start.Environment["FAKE_LIVE_MAIN_SHA"] = liveMainSha;
            start.Environment["FAKE_CI_RUNS"] = JsonSerializer.Serialize(new { workflow_runs = workflowRuns });

            using Process process = Process.Start(start).ShouldNotBeNull();
            process.WaitForExit();
            return process.ExitCode;
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
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

        throw new DirectoryNotFoundException("Could not locate the Hexalith.EventStore repository root.");
    }

    private static string RunGit(string workingDirectory, params string[] arguments)
    {
        ProcessStartInfo start = new("git")
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
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        process.ExitCode.ShouldBe(0, error);
        return output.Trim();
    }
}
