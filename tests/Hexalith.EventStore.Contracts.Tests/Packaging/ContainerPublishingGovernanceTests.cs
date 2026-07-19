using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hexalith.EventStore.Contracts.Tests.Packaging;

/// <summary>
/// Guards the EventStore-owned edge of the shared multi-platform publication contract.
/// </summary>
public sealed class ContainerPublishingGovernanceTests
{
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
    /// Verifies that full publication authority runs before the first irreversible command.
    /// </summary>
    [Fact]
    public void SemanticReleaseRequiresAuthorityBeforeTagNuGetAndContainerPublication()
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
        int verifyAuthorityPreflight = verifyReleaseCommand.IndexOf(
            "scripts/validate-release-authority.sh",
            StringComparison.Ordinal);
        verifySecretPreflight.ShouldBeGreaterThanOrEqualTo(0);
        verifyAuthorityPreflight.ShouldBeGreaterThan(verifySecretPreflight);
        verifyReleaseCommand.ShouldContain("${nextRelease.version} verify");
        verifyReleaseCommand.ShouldNotContain("dotnet nuget push");
        verifyReleaseCommand.ShouldNotContain("publish-containers.sh");

        int secretPreflight = publishCommand.IndexOf("scripts/validate-release-secrets.sh", StringComparison.Ordinal);
        int authorityPreflight = publishCommand.IndexOf("scripts/validate-release-authority.sh", StringComparison.Ordinal);
        int nugetPublish = publishCommand.IndexOf("dotnet nuget push", StringComparison.Ordinal);
        int containerPublish = publishCommand.IndexOf("./.hexalith/release/publish-containers.sh", StringComparison.Ordinal);

        secretPreflight.ShouldBeGreaterThanOrEqualTo(0);
        authorityPreflight.ShouldBeGreaterThan(secretPreflight);
        publishCommand.ShouldContain("${nextRelease.version} publish");
        nugetPublish.ShouldBeGreaterThan(authorityPreflight);
        containerPublish.ShouldBeGreaterThan(nugetPublish);
        publishCommand.ShouldNotContain("--skip-duplicate");
    }

    /// <summary>
    /// Verifies that the local wrapper delegates durable authority and destination checks to the shared validator.
    /// </summary>
    [Fact]
    public void AuthorityWrapperBindsReleaseIdentityAndSharedContract()
    {
        string root = FindRepositoryRoot();
        string scriptPath = Path.Combine(root, "scripts", "validate-release-authority.sh");
        File.Exists(scriptPath).ShouldBeTrue();
        string script = File.ReadAllText(scriptPath);

        script.ShouldContain("./.hexalith/release/publication_authority.py");
        script.ShouldContain("HEXALITH_RELEASE_AUTHORITY_URL");
        script.ShouldContain("HEXALITH_BUILDS_EXECUTION_SHA");
        script.ShouldContain("GITHUB_SHA");
        script.ShouldNotContain("git rev-parse HEAD");
        script.ShouldContain("tools/release-packages.json");
        script.ShouldContain("1-20-github-approval-role-allowlist.json");
        script.ShouldContain("--phase \"$phase\"");
        script.ShouldContain("registry.hexalith.com/eventstore");
    }

    /// <summary>
    /// Verifies that the thin caller supplies exact authority inputs without widening secrets or mappings.
    /// </summary>
    [Fact]
    public void ThinReleaseCallerSuppliesExactAuthorityInputsAndOneMapping()
    {
        string root = FindRepositoryRoot();
        string workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "release.yml"));

        Match releaseWorkflow = Regex.Match(
            workflow,
            @"uses: Hexalith/Hexalith\.Builds/\.github/workflows/domain-release\.yml@(?<sha>[0-9a-f]{40})");
        releaseWorkflow.Success.ShouldBeTrue();
        string buildsSha = releaseWorkflow.Groups["sha"].Value;
        workflow.ShouldContain($"builds-execution-sha: {buildsSha}");
        workflow.ShouldNotContain("domain-release.yml@main");
        workflow.ShouldNotContain("vars.HEXALITH_BUILDS_RELEASE_SHA");
        workflow.ShouldContain("release-authority-url: ${{ vars.HEXALITH_RELEASE_AUTHORITY_URL }}");
        workflow.ShouldContain(
            "release-owner-allowlist: _bmad-output/implementation-artifacts/1-20-github-approval-role-allowlist.json");
        workflow.ShouldNotContain("secrets: inherit");

        string inputsBlock = ExtractYamlBlock(workflow, "    with:");
        MatchCollection timeoutInputs = Regex.Matches(
            inputsBlock,
            @"(?m)^\s{6}timeout-minutes:\s*(?<minutes>\d+)\s*$");
        timeoutInputs.Count.ShouldBe(1);
        timeoutInputs[0].Groups["minutes"].Value.ShouldBe("60");

        string buildsRoot = Path.Combine(root, "references", "Hexalith.Builds");
        ReadGitHead(buildsRoot).ShouldBe(buildsSha);
        string sharedWorkflow = File.ReadAllText(
            Path.Combine(buildsRoot, ".github", "workflows", "domain-release.yml"));
        sharedWorkflow.ShouldContain("timeout-minutes: ${{ inputs.timeout-minutes }}");

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
    /// Verifies the exact shared multi-platform invocation and post-publish gates.
    /// </summary>
    [Fact]
    public void SharedPublisherContractIsExactTwoPlatformAndValidationGated()
    {
        string root = FindRepositoryRoot();
        string publisher = File.ReadAllText(
            Path.Combine(root, "references", "Hexalith.Builds", "Github", "publish-containers", "publish-containers.sh"));
        string smoke = File.ReadAllText(
            Path.Combine(
                root,
                "references",
                "Hexalith.Builds",
                "Github",
                "publish-containers",
                "smoke_container_platforms.py"));

        publisher.ShouldContain("linux-musl-x64;linux-musl-arm64");
        publisher.ShouldContain("\"-p:RuntimeIdentifiers=\\\"$runtime_identifiers\\\"\"");
        publisher.ShouldContain("\"-p:ContainerRuntimeIdentifiers=\\\"$runtime_identifiers\\\"\"");
        publisher.ShouldContain("-p:ContainerImageFormat=OCI");
        publisher.ShouldContain("-p:UseHexalithProjectReferences=false");
        publisher.ShouldNotContain("--os linux");
        publisher.ShouldNotContain("--arch x64");
        publisher.IndexOf("\n  \"$authority_validator\"", StringComparison.Ordinal).ShouldBeLessThan(
            publisher.IndexOf("dotnet publish", StringComparison.Ordinal));
        publisher.LastIndexOf("\n  \"$validator\"", StringComparison.Ordinal).ShouldBeGreaterThan(
            publisher.IndexOf("dotnet publish", StringComparison.Ordinal));
        publisher.LastIndexOf("\n  \"$smoke\"", StringComparison.Ordinal).ShouldBeGreaterThan(
            publisher.LastIndexOf("\n  \"$validator\"", StringComparison.Ordinal));
        smoke.ShouldContain("DEFAULT_SMOKE_TIMEOUT_SECONDS = \"180\"");
        smoke.ShouldContain("Authentication__JwtBearer__Issuer=");
        smoke.ShouldContain("Authentication__JwtBearer__Audience=");
        smoke.ShouldContain("Authentication__JwtBearer__SigningKey=");
        smoke.ShouldContain("Authentication__JwtBearer__AllowInsecureSymmetricKey=true");
    }

    /// <summary>
    /// Verifies that an authority rejection prevents both external mutation commands.
    /// </summary>
    [Fact]
    public void RejectedAuthorityBehaviorallyBlocksNuGetAndContainerMutation()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        string root = FindRepositoryRoot();
        string temporary = Path.Combine(Path.GetTempPath(), $"hexalith-authority-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporary);
        try
        {
            string rejectingValidator = Path.Combine(temporary, "reject-authority.sh");
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
            start.ArgumentList.Add("authority-test");
            start.ArgumentList.Add(Path.Combine(root, "scripts", "validate-release-authority.sh"));
            start.ArgumentList.Add(nugetMarker);
            start.ArgumentList.Add(containerMarker);
            start.Environment["HEXALITH_RELEASE_AUTHORITY_URL"] =
                "https://api.github.com/repos/Hexalith/Hexalith.EventStore/issues/comments/1";
            start.Environment["HEXALITH_BUILDS_EXECUTION_SHA"] = new string('a', 40);
            start.Environment["GITHUB_SHA"] = new string('b', 40);
            start.Environment["HEXALITH_PUBLICATION_AUTHORITY_VALIDATOR"] = rejectingValidator;
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

    private static string ReadGitHead(string repository)
    {
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                WorkingDirectory = repository,
            },
        };
        process.StartInfo.ArgumentList.Add("rev-parse");
        process.StartInfo.ArgumentList.Add("HEAD");

        process.Start().ShouldBeTrue();
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        process.ExitCode.ShouldBe(0, $"Could not resolve the Builds submodule HEAD: {error}");
        return output.Trim();
    }
}
