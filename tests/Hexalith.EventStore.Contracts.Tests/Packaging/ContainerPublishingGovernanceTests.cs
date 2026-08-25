using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hexalith.EventStore.Contracts.Tests.Packaging;

/// <summary>
/// Guards the EventStore-owned edge of the shared multi-platform publication contract.
/// </summary>
public sealed class ContainerPublishingGovernanceTests
{
    private const string ApprovedBuildsReleaseSha = "a07078ad74d3727bc5a6b6d85d47d56a6e5c9fec";
    private const int ExpectedPackageCount = 14;
    private static readonly TimeSpan PublicationPreflightTimeout = TimeSpan.FromSeconds(10);

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
    /// Verifies that GitHub assets remain published without optional issue or pull-request success notifications.
    /// </summary>
    [Fact]
    public void SemanticReleasePublishesGitHubAssetsWithoutSuccessNotifications()
    {
        string root = FindRepositoryRoot();
        using JsonDocument configuration = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(root, ".releaserc.json")));
        JsonElement[] githubPlugin = configuration.RootElement
            .GetProperty("plugins")
            .EnumerateArray()
            .Where(plugin => plugin.ValueKind == JsonValueKind.Array)
            .Select(plugin => plugin.EnumerateArray().ToArray())
            .Single(plugin => plugin.Length == 2 &&
                plugin[0].ValueKind == JsonValueKind.String &&
                string.Equals(plugin[0].GetString(), "@semantic-release/github", StringComparison.Ordinal));
        JsonElement githubConfiguration = githubPlugin[1];

        githubConfiguration.ValueKind.ShouldBe(JsonValueKind.Object);
        string[] assets = githubConfiguration
            .GetProperty("assets")
            .EnumerateArray()
            .Select(asset => asset.GetString().ShouldNotBeNull())
            .ToArray();
        assets.ShouldBe(["nupkgs/*.nupkg"]);
        githubConfiguration
            .EnumerateObject()
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)
            .ShouldBe(["assets", "successCommentCondition"]);
        JsonElement successCommentCondition = githubConfiguration.GetProperty("successCommentCondition");
        successCommentCondition.ValueKind.ShouldBe(JsonValueKind.False);
        successCommentCondition.GetBoolean().ShouldBeFalse();
        githubConfiguration.TryGetProperty("successComment", out _).ShouldBeFalse();
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
        script.ShouldContain("HEXALITH_RELEASE_RESERVED_VERSION");
        script.ShouldContain("HEXALITH_RELEASE_AUTHORITY_ISSUE_URL");
        script.ShouldContain("--authority-owner \"$authority_owner\"");
        script.ShouldNotContain("1-20-github-approval-role-allowlist.json");
        script.ShouldContain("--phase \"$phase\"");
        script.ShouldContain("--source-branch \"$source_branch\"");
        script.ShouldContain("--source-ci-workflow \"$source_ci_workflow\"");
        script.ShouldContain("--package-manifest \"$package_manifest\"");
        script.ShouldContain("${HEXALITH_RELEASE_EXPECTED_PACKAGE_COUNT-}");
        script.ShouldContain("--expected-package-count \"$expected_package_count\"");
        script.ShouldContain("registry.hexalith.com/eventstore");
    }

    /// <summary>
    /// Verifies the disabled posture forwards no authority arguments while still preflighting.
    /// </summary>
    [Theory]
    [InlineData("verify")]
    [InlineData("publish")]
    public void PublicationPreflightWrapperOmitsAuthorityArgumentsWhenTheGateIsDisabled(string phase)
    {
        (int exitCode, _, string error, bool invoked, string[] arguments) =
            RunWrapperWithPosture(FindRepositoryRoot(), phase, new Dictionary<string, string?>
            {
                ["HEXALITH_RELEASE_REQUIRE_AUTHORITY"] = "false",
            });

        exitCode.ShouldBe(0, error);
        invoked.ShouldBeTrue();
        arguments.ShouldNotContain("--authority-issue-url");
        arguments.ShouldNotContain("--authority-owner");

        // Opting out of the authority gate must not opt out of the destination proof.
        arguments.ShouldContain("--phase");
        arguments.ShouldContain(phase);
        arguments.ShouldContain("--package-manifest");
        arguments.ShouldContain("--expected-package-count");
    }

    /// <summary>
    /// Verifies the enabled posture still reaches the shared preflight with both authority values.
    /// </summary>
    [Fact]
    public void PublicationPreflightWrapperForwardsAuthorityArgumentsWhenTheGateIsEnabled()
    {
        (int exitCode, _, string error, bool invoked, string[] arguments) =
            RunWrapperWithPosture(FindRepositoryRoot(), "verify", new Dictionary<string, string?>
            {
                ["HEXALITH_RELEASE_REQUIRE_AUTHORITY"] = "true",
                ["HEXALITH_RELEASE_RESERVED_VERSION"] = "99.0.0",
                ["HEXALITH_RELEASE_AUTHORITY_ISSUE_URL"] =
                    "https://api.github.com/repos/Hexalith/Hexalith.EventStore/issues/123",
                ["HEXALITH_RELEASE_AUTHORITY_OWNER"] = "github:jpiquot",
            });

        exitCode.ShouldBe(0, error);
        invoked.ShouldBeTrue();
        arguments.ShouldContain("--authority-issue-url");
        arguments[Array.IndexOf(arguments, "--authority-issue-url") + 1]
            .ShouldBe("https://api.github.com/repos/Hexalith/Hexalith.EventStore/issues/123");
        arguments.ShouldContain("--authority-owner");
        arguments[Array.IndexOf(arguments, "--authority-owner") + 1].ShouldBe("github:jpiquot");
    }

    /// <summary>
    /// Verifies malformed corrective-release inputs fail before the shared preflight runs.
    /// </summary>
    /// <param name="version">The reserved release version.</param>
    /// <param name="issueUrl">The authority issue API URL.</param>
    /// <param name="expectedError">The expected fail-closed diagnostic.</param>
    [Theory]
    [InlineData(
        "01.2.3",
        "https://api.github.com/repos/Hexalith/Hexalith.EventStore/issues/123",
        "must be a stable semantic version")]
    [InlineData(
        "1.02.3",
        "https://api.github.com/repos/Hexalith/Hexalith.EventStore/issues/123",
        "must be a stable semantic version")]
    [InlineData(
        "1.2.03",
        "https://api.github.com/repos/Hexalith/Hexalith.EventStore/issues/123",
        "must be a stable semantic version")]
    [InlineData(
        "99.0.0",
        "https://api.github.com/repos/Hexalith/Another.Repository/issues/123",
        "must identify an EventStore GitHub issue")]
    public void PublicationPreflightWrapperRejectsInvalidCorrectiveInputs(
        string version,
        string issueUrl,
        string expectedError)
    {
        (int exitCode, _, string error, bool invoked, _) =
            RunWrapperWithPosture(FindRepositoryRoot(), "verify", new Dictionary<string, string?>
            {
                ["HEXALITH_RELEASE_REQUIRE_AUTHORITY"] = "true",
                ["HEXALITH_RELEASE_RESERVED_VERSION"] = version,
                ["HEXALITH_RELEASE_AUTHORITY_ISSUE_URL"] = issueUrl,
                ["HEXALITH_RELEASE_AUTHORITY_OWNER"] = "github:jpiquot",
            });

        exitCode.ShouldNotBe(0);
        error.ShouldContain(expectedError);
        invoked.ShouldBeFalse();
    }

    /// <summary>
    /// Verifies a value the disabled posture would ignore fails closed instead of passing unnoticed.
    /// </summary>
    [Theory]
    [InlineData("HEXALITH_RELEASE_RESERVED_VERSION", "99.0.0")]
    [InlineData("HEXALITH_RELEASE_AUTHORITY_ISSUE_URL", "https://api.github.com/repos/Hexalith/Hexalith.EventStore/issues/123")]
    [InlineData("HEXALITH_RELEASE_AUTHORITY_OWNER", "github:jpiquot")]
    public void PublicationPreflightWrapperRejectsIgnoredReservationInputs(string name, string value)
    {
        (int exitCode, _, string error, bool invoked, _) =
            RunWrapperWithPosture(FindRepositoryRoot(), "verify", new Dictionary<string, string?>
            {
                ["HEXALITH_RELEASE_REQUIRE_AUTHORITY"] = "false",
                [name] = value,
            });

        exitCode.ShouldNotBe(0);
        error.ShouldContain(name);
        error.ShouldContain("authority gate is disabled");
        invoked.ShouldBeFalse();
    }

    /// <summary>
    /// Verifies an unset posture stays guarded and a malformed one fails closed.
    /// </summary>
    [Theory]
    [InlineData(null, "HEXALITH_RELEASE_RESERVED_VERSION must be a stable semantic version")]
    [InlineData("", "must be exactly true or false")]
    [InlineData("True", "must be exactly true or false")]
    [InlineData("yes", "must be exactly true or false")]
    [InlineData("0", "must be exactly true or false")]
    public void PublicationPreflightWrapperRejectsAnUndeclaredOrMalformedPosture(
        string? declared,
        string expectedError)
    {
        (int exitCode, _, string error, bool invoked, _) =
            RunWrapperWithPosture(FindRepositoryRoot(), "verify", new Dictionary<string, string?>
            {
                ["HEXALITH_RELEASE_REQUIRE_AUTHORITY"] = declared,
            });

        exitCode.ShouldNotBe(0);
        error.ShouldContain(expectedError);
        invoked.ShouldBeFalse();
    }

    /// <summary>
    /// Verifies that the manifest, release caller, and wrapper independently agree on the package inventory count.
    /// </summary>
    [Fact]
    public void ReleasePackageCountContractMatchesManifestCallerAndWrapper()
    {
        string root = FindRepositoryRoot();
        using JsonDocument manifest = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(root, "tools", "release-packages.json")));
        int manifestPackageCount = manifest.RootElement.GetProperty("packages").GetArrayLength();
        string workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "release.yml"));
        string wrapper = File.ReadAllText(Path.Combine(root, "scripts", "validate-publication-preflight.sh"));
        string releaseInputs = ExtractYamlBlock(workflow, "    with:");

        manifestPackageCount.ShouldBe(ExpectedPackageCount);
        MatchCollection callerPackageCounts = Regex.Matches(
            releaseInputs,
            @"(?m)^\s*expected-package-count\s*:\s*(?<count>[0-9]+)\s*(?:#.*)?$");
        callerPackageCounts.Count.ShouldBe(1);
        int.Parse(callerPackageCounts[0].Groups["count"].Value, CultureInfo.InvariantCulture)
            .ShouldBe(manifestPackageCount);
        MatchCollection wrapperPackageCounts = Regex.Matches(
            wrapper,
            @"(?m)^\s*readonly\s+expected_package_count\s*=\s*(?<count>[0-9]+)\s*(?:#.*)?$");
        wrapperPackageCounts.Count.ShouldBe(1);
        int.Parse(wrapperPackageCounts[0].Groups["count"].Value, CultureInfo.InvariantCulture)
            .ShouldBe(manifestPackageCount);
        wrapper.ShouldContain(
            "[[ \"${HEXALITH_RELEASE_EXPECTED_PACKAGE_COUNT-}\" = \"$expected_package_count\" ]]");
        wrapper.ShouldContain("--expected-package-count \"$expected_package_count\"");
    }

    /// <summary>
    /// Verifies that a matching workflow count reaches the shared preflight with the exact reviewed count.
    /// </summary>
    /// <param name="phase">The publication preflight phase.</param>
    [Theory]
    [InlineData("verify")]
    [InlineData("publish")]
    public void PublicationPreflightWrapperForwardsExactReviewedPackageCount(string phase)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        string root = FindRepositoryRoot();
        (int exitCode, string _, string _, bool preflightInvoked, string[] arguments) =
            RunPublicationPreflightWrapper(
                root,
                phase,
                ExpectedPackageCount.ToString(CultureInfo.InvariantCulture));

        exitCode.ShouldBe(0);
        preflightInvoked.ShouldBeTrue();
        arguments.Count(argument => argument == "--expected-package-count").ShouldBe(1);
        int expectedCountArgument = Array.IndexOf(arguments, "--expected-package-count");
        expectedCountArgument.ShouldBeGreaterThanOrEqualTo(0);
        int expectedCountValueArgument = expectedCountArgument + 1;
        expectedCountValueArgument.ShouldBeLessThan(arguments.Length);
        arguments[expectedCountValueArgument].ShouldBe(ExpectedPackageCount.ToString(CultureInfo.InvariantCulture));
        arguments[^1].ShouldBe(phase);
    }

    /// <summary>
    /// Verifies that missing and empty workflow counts fail before the shared preflight is invoked.
    /// </summary>
    /// <param name="workflowPackageCount">The missing or empty workflow package-count value.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void PublicationPreflightWrapperRejectsMissingOrEmptyPackageCount(string? workflowPackageCount)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        string root = FindRepositoryRoot();
        (int exitCode, string _, string error, bool preflightInvoked, string[] arguments) =
            RunPublicationPreflightWrapper(root, "verify", workflowPackageCount);

        exitCode.ShouldNotBe(0);
        error.ShouldContain("workflow expected-package-count input must be exactly 14");
        preflightInvoked.ShouldBeFalse();
        arguments.ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies that non-exact workflow counts fail before the shared preflight is invoked.
    /// </summary>
    /// <param name="workflowPackageCount">The mismatched workflow package-count text.</param>
    [Theory]
    [InlineData("13")]
    [InlineData("15")]
    [InlineData("014")]
    [InlineData("14 ")]
    [InlineData("abc")]
    public void PublicationPreflightWrapperRejectsMismatchedPackageCount(string workflowPackageCount)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        string root = FindRepositoryRoot();
        (int exitCode, string _, string error, bool preflightInvoked, string[] arguments) =
            RunPublicationPreflightWrapper(root, "verify", workflowPackageCount);

        exitCode.ShouldNotBe(0);
        error.ShouldContain("workflow expected-package-count input must be exactly 14");
        preflightInvoked.ShouldBeFalse();
        arguments.ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies a reservation mismatch is rejected before preflight or a later publication marker.
    /// </summary>
    [Fact]
    public void ReservedVersionMismatchRejectsBeforePreflightOrPublication()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        string root = FindRepositoryRoot();
        string temporary = Path.Combine(Path.GetTempPath(), $"hexalith-reservation-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporary);
        try
        {
            string preflightMarker = Path.Combine(temporary, "preflight-ran");
            string publishMarker = Path.Combine(temporary, "publish-ran");
            string preflight = Path.Combine(temporary, "preflight.sh");
            File.WriteAllText(preflight, "#!/usr/bin/env bash\nset -euo pipefail\ntouch \"$PREFLIGHT_MARKER\"\n");
            File.SetUnixFileMode(
                preflight,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            ProcessStartInfo start = new("bash")
            {
                WorkingDirectory = root,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };
            start.ArgumentList.Add("-c");
            start.ArgumentList.Add("bash \"$1\" 99.0.1 verify && touch \"$2\"");
            start.ArgumentList.Add("reservation-test");
            start.ArgumentList.Add(Path.Combine(root, "scripts", "validate-publication-preflight.sh"));
            start.ArgumentList.Add(publishMarker);
            start.Environment["HEXALITH_BUILDS_EXECUTION_SHA"] = new string('a', 40);
            start.Environment["HEXALITH_RELEASE_ENVIRONMENT"] = "production";
            start.Environment["HEXALITH_RELEASE_SOURCE_BRANCH"] = "main";
            start.Environment["HEXALITH_RELEASE_SOURCE_CI_WORKFLOW"] = "ci.yml";
            start.Environment["HEXALITH_RELEASE_PACKAGE_MANIFEST"] = "tools/release-packages.json";
            start.Environment["HEXALITH_RELEASE_EXPECTED_PACKAGE_COUNT"] =
                ExpectedPackageCount.ToString(CultureInfo.InvariantCulture);
            start.Environment["HEXALITH_RELEASE_REQUIRE_AUTHORITY"] = "true";
            start.Environment["HEXALITH_RELEASE_RESERVED_VERSION"] = "99.0.0";
            start.Environment["HEXALITH_RELEASE_AUTHORITY_ISSUE_URL"] =
                "https://api.github.com/repos/Hexalith/Hexalith.EventStore/issues/123";
            start.Environment["HEXALITH_RELEASE_AUTHORITY_OWNER"] = "github:jpiquot";
            start.Environment["GITHUB_SHA"] = new string('b', 40);
            start.Environment["HEXALITH_PUBLICATION_PREFLIGHT"] = preflight;
            start.Environment["HEXALITH_ZOT_REGISTRY"] = "registry.hexalith.com";
            start.Environment["PREFLIGHT_MARKER"] = preflightMarker;

            using Process process = Process.Start(start).ShouldNotBeNull();
            process.WaitForExit();

            process.ExitCode.ShouldNotBe(0);
            process.StandardError.ReadToEnd().ShouldContain("different from the authorized reservation");
            File.Exists(preflightMarker).ShouldBeFalse();
            File.Exists(publishMarker).ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies that the caller uses one immutable release pin independently of the development gitlink.
    /// </summary>
    [Fact]
    public void ReleaseCallerPinsSharedExecutionAndOneMappingWithoutPublicationAuthorityInputs()
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
        workflow.ShouldContain("governed-release: false");
        workflow.ShouldContain("source-branch: main");
        workflow.ShouldContain("source-ci-workflow: ci.yml");
        workflow.ShouldContain("package-manifest: tools/release-packages.json");
        // Dispatching a release must not require the operator to answer anything. The
        // reservation inputs were a Story 3.14 corrective-release gate; the caller now
        // declares the opt-out explicitly so a dropped input can never be read as one.
        workflow.ShouldContain("require-publication-authority: false");
        workflow.ShouldNotContain("${{ inputs.");
        workflow.ShouldNotContain("github.event.inputs.");
        // A bare `\s*$` assertion is vacuous here: in multiline mode `$` matches before the
        // immediately following newline regardless of what an indented `inputs:` child below it
        // contains, so it accepts the old input-carrying form too. Extract the block instead.
        workflow.ShouldContain("  workflow_dispatch:");
        string dispatchBlock = ExtractYamlBlock(workflow, "  workflow_dispatch:");
        Regex.IsMatch(dispatchBlock, "(?m)^\\s*(?:inputs|[\"']inputs[\"']):").ShouldBeFalse();
        workflow.ShouldNotContain("release-owner-allowlist:");
        workflow.ShouldNotContain("references/Hexalith.Builds");
        workflow.ShouldNotContain("secrets: inherit");

        Match releaseJob = Regex.Match(workflow, @"(?ms)^  release:\r?\n(?<block>.*)\z");
        releaseJob.Success.ShouldBeTrue();
        string releaseJobBlock = releaseJob.Groups["block"].Value;
        releaseJobBlock.ShouldContain("attestations: write");
        releaseJobBlock.ShouldContain("id-token: write");
        releaseJobBlock.ShouldContain("governed-release: false");

        string gitlink = RunGit(root, "ls-tree", "HEAD", "references/Hexalith.Builds");
        Match gitlinkEntry = Regex.Match(gitlink, @"^160000 commit (?<sha>[0-9a-f]{40})\s+references/Hexalith\.Builds$");
        gitlinkEntry.Success.ShouldBeTrue();
        gitlinkEntry.Groups["sha"].Value.ShouldNotBe(ApprovedBuildsReleaseSha);

        workflow.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Count(line => line.Equals("    with:", StringComparison.Ordinal))
            .ShouldBe(1);
        string inputsBlock = ExtractYamlBlock(workflow, "    with:");
        inputsBlock.ShouldNotContain("release-version:");
        inputsBlock.ShouldNotContain("reserved-version:");
        inputsBlock.ShouldNotContain("release-authority-issue-url:");
        inputsBlock.ShouldNotContain("release-authority-owner:");
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
    /// Verifies the caller never re-enables the corrective-release authority gate without also
    /// re-pinning who may hold it. The live preflight only shape-checks the owner it is given
    /// (any well-formed <c>github:&lt;login&gt;</c> passes), while the post-hoc evidence verifier
    /// still hardcodes <c>jpiquot</c>; without this guard a future edit could re-enable the gate
    /// for a different owner that the live preflight would accept but evidence could never verify.
    /// </summary>
    [Fact]
    public void ReleaseAuthorityOwnerIsPinnedWheneverTheAuthorityGateIsEnabled()
    {
        string root = FindRepositoryRoot();
        string workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "release.yml"));
        AssertAuthorityOwnerPin(workflow);
    }

    /// <summary>
    /// Verifies the enabled authority-posture branch requires every reservation input and an exact,
    /// anchored owner identity.
    /// </summary>
    [Fact]
    public void EnabledReleaseAuthorityFixturePinsAllReservationInputsAndExactOwner()
    {
        const string EnabledWorkflow = """
            jobs:
              release:
                with:
                  require-publication-authority: true
                  reserved-version: ${{ inputs.release-version }}
                  release-authority-issue-url: ${{ inputs.release-authority-issue-url }}
                  release-authority-owner: github:jpiquot
            """;

        AssertAuthorityOwnerPin(EnabledWorkflow);
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
            string preflightInvocationMarker = Path.Combine(temporary, "preflight-invoked");
            File.WriteAllText(
                rejectingValidator,
                "#!/usr/bin/env bash\n" +
                "set -euo pipefail\n" +
                ": > \"$PREFLIGHT_INVOCATION_MARKER\"\n" +
                "exit 1\n");
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
            start.Environment["HEXALITH_RELEASE_EXPECTED_PACKAGE_COUNT"] =
                ExpectedPackageCount.ToString(CultureInfo.InvariantCulture);
            start.Environment["HEXALITH_RELEASE_REQUIRE_AUTHORITY"] = "false";
            start.Environment["GITHUB_SHA"] = new string('b', 40);
            start.Environment["HEXALITH_PUBLICATION_PREFLIGHT"] = rejectingValidator;
            start.Environment["HEXALITH_ZOT_REGISTRY"] = "registry.hexalith.com";
            start.Environment["PREFLIGHT_INVOCATION_MARKER"] = preflightInvocationMarker;

            using Process process = Process.Start(start).ShouldNotBeNull();
            process.WaitForExit();

            process.ExitCode.ShouldNotBe(0);
            File.Exists(preflightInvocationMarker).ShouldBeTrue();
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

        // The checklist states the publication pin in prose, so nothing kept it honest
        // and it drifted two rotations behind. Bind it to the workflow it describes.
        secrets.ShouldContain(ApprovedBuildsReleaseSha);
        ci.ShouldContain(ApprovedBuildsReleaseSha);
        targets.ShouldContain("mcr.microsoft.com/dotnet/aspnet:10.0-alpine");
        targets.ShouldContain("<ContainerUser>app</ContainerUser>");
        targets.ShouldContain("<ContainerPort Include=\"8080\"");
        project.ShouldContain("<ContainerRepository>eventstore</ContainerRepository>");
    }

    /// <summary>
    /// Verifies every tracked digest-bearing raw OCI object cannot be rewritten by text
    /// normalization. The tracked set is enumerated rather than hand-listed: git check-attr answers
    /// for any path string, so a hand-listed path that no longer exists passes vacuously while the
    /// real evidence goes unchecked, and a newly added packet is silently left uncovered.
    /// </summary>
    [Fact]
    public void DigestBearingRawOciEvidenceIsBinary()
    {
        string root = FindRepositoryRoot();
        string[] tracked = RunGit(root, "ls-files", "-z", "*.raw")
            .Split('\0', StringSplitOptions.RemoveEmptyEntries);

        // Coverage control: an empty enumeration would make every assertion below vacuous.
        tracked.Length.ShouldBeGreaterThan(0);

        List<string> normalizable = [];
        foreach (string path in tracked)
        {
            File.Exists(Path.Combine(root, path)).ShouldBeTrue(path);
            if (RunGit(root, "check-attr", "text", "--", path) != $"{path}: text: unset")
            {
                normalizable.Add(path);
            }
        }

        normalizable.ShouldBeEmpty(
            "these tracked digest-bearing raw objects are not marked binary in .gitattributes, so a "
            + "core.autocrlf checkout would rewrite them and break their recorded SHA-256:\n"
            + string.Join('\n', normalizable));
    }

    /// <summary>
    /// Verifies YAML block extraction captures indented children and stops at the next peer key.
    /// </summary>
    [Fact]
    public void ExtractYamlBlockCapturesIndentedChildrenFromLiteralFixture()
    {
        const string Fixture = """
            on:
              workflow_dispatch:
                "inputs":
                  release-version:
                    required: true
              push:
            """;

        string block = ExtractYamlBlock(Fixture, "  workflow_dispatch:");
        block.ShouldContain("    \"inputs\":");
        block.ShouldContain("      release-version:");
        block.ShouldNotContain("  push:");
    }

    private static void AssertAuthorityOwnerPin(string workflow)
    {
        string withBlock = ExtractYamlBlock(workflow, "    with:");
        if (Regex.IsMatch(withBlock, @"(?m)^\s*require-publication-authority:\s*true\s*$"))
        {
            Regex.IsMatch(withBlock, @"(?m)^\s*reserved-version:\s*\S[^\r\n]*$").ShouldBeTrue();
            Regex.IsMatch(withBlock, @"(?m)^\s*release-authority-issue-url:\s*\S[^\r\n]*$").ShouldBeTrue();
            Regex.IsMatch(
                withBlock,
                @"(?m)^\s*release-authority-owner:\s*github:jpiquot\s*$").ShouldBeTrue();
        }
        else
        {
            Regex.IsMatch(withBlock, @"(?m)^\s*require-publication-authority:\s*false\s*$").ShouldBeTrue();
            Regex.IsMatch(withBlock, @"(?m)^\s*release-authority-owner:").ShouldBeFalse();
        }
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

    private static (int ExitCode, string Output, string Error, bool PreflightInvoked, string[] Arguments)
        RunWrapperWithPosture(string root, string phase, Dictionary<string, string?> posture)
    {
        string temporary = Path.Combine(Path.GetTempPath(), $"hexalith-posture-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporary);
        try
        {
            string invocationMarker = Path.Combine(temporary, "preflight-invoked");
            string argumentsPath = Path.Combine(temporary, "preflight-arguments");
            string recordingPreflight = Path.Combine(temporary, "record-preflight.sh");
            File.WriteAllText(
                recordingPreflight,
                "#!/usr/bin/env bash\n" +
                "set -euo pipefail\n" +
                ": > \"$PREFLIGHT_INVOCATION_MARKER\"\n" +
                "printf '%s\\n' \"$@\" > \"$PREFLIGHT_ARGUMENTS\"\n");
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(
                    recordingPreflight,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }

            ProcessStartInfo start = new("bash")
            {
                WorkingDirectory = root,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };
            start.ArgumentList.Add(Path.Combine(root, "scripts", "validate-publication-preflight.sh"));
            start.ArgumentList.Add("99.0.0");
            start.ArgumentList.Add(phase);
            start.Environment["HEXALITH_BUILDS_EXECUTION_SHA"] = new string('a', 40);
            start.Environment["HEXALITH_RELEASE_ENVIRONMENT"] = "production";
            start.Environment["HEXALITH_RELEASE_SOURCE_BRANCH"] = "main";
            start.Environment["HEXALITH_RELEASE_SOURCE_CI_WORKFLOW"] = "ci.yml";
            start.Environment["HEXALITH_RELEASE_PACKAGE_MANIFEST"] = "tools/release-packages.json";
            start.Environment["HEXALITH_RELEASE_EXPECTED_PACKAGE_COUNT"] =
                ExpectedPackageCount.ToString(CultureInfo.InvariantCulture);
            start.Environment["GITHUB_SHA"] = new string('b', 40);
            start.Environment["HEXALITH_PUBLICATION_PREFLIGHT"] = recordingPreflight;
            start.Environment["HEXALITH_ZOT_REGISTRY"] = "registry.hexalith.com";
            start.Environment["PREFLIGHT_INVOCATION_MARKER"] = invocationMarker;
            start.Environment["PREFLIGHT_ARGUMENTS"] = argumentsPath;

            // Do not inherit a developer machine's corrective-release posture. Each fixture
            // declares every authority input it intends to exercise below.
            foreach (string name in new[]
            {
                "HEXALITH_RELEASE_REQUIRE_AUTHORITY",
                "HEXALITH_RELEASE_RESERVED_VERSION",
                "HEXALITH_RELEASE_AUTHORITY_ISSUE_URL",
                "HEXALITH_RELEASE_AUTHORITY_OWNER",
            })
            {
                start.Environment.Remove(name);
            }

            // A null value removes the variable, so an unset posture is distinguishable
            // from a set-but-empty one instead of collapsing into the same case.
            foreach ((string name, string? value) in posture)
            {
                if (value is null)
                {
                    start.Environment.Remove(name);
                }
                else
                {
                    start.Environment[name] = value;
                }
            }

            using Process process = new() { StartInfo = start };
            process.Start().ShouldBeTrue("Could not start the publication preflight wrapper.");
            Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
            Task<string> errorTask = process.StandardError.ReadToEndAsync();
            bool exited = process.WaitForExit((int)PublicationPreflightTimeout.TotalMilliseconds);
            if (!exited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
            }

            string output = outputTask.GetAwaiter().GetResult();
            string error = errorTask.GetAwaiter().GetResult();
            if (!exited)
            {
                throw new TimeoutException(
                    $"Publication preflight wrapper timed out after {PublicationPreflightTimeout}. " +
                    $"Output: {output} Error: {error}");
            }

            string[] arguments = File.Exists(argumentsPath)
                ? File.ReadAllLines(argumentsPath)
                : [];
            return (process.ExitCode, output, error, File.Exists(invocationMarker), arguments);
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    private static (int ExitCode, string Output, string Error, bool PreflightInvoked, string[] Arguments)
        RunPublicationPreflightWrapper(string root, string phase, string? workflowPackageCount)
    {
        string temporary = Path.Combine(Path.GetTempPath(), $"hexalith-package-count-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporary);
        try
        {
            string invocationMarker = Path.Combine(temporary, "preflight-invoked");
            string argumentsPath = Path.Combine(temporary, "preflight-arguments");
            string recordingPreflight = Path.Combine(temporary, "record-preflight.sh");
            File.WriteAllText(
                recordingPreflight,
                "#!/usr/bin/env bash\n" +
                "set -euo pipefail\n" +
                ": > \"$PREFLIGHT_INVOCATION_MARKER\"\n" +
                "printf '%s\\n' \"$@\" > \"$PREFLIGHT_ARGUMENTS\"\n");
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(
                    recordingPreflight,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }

            ProcessStartInfo start = new("bash")
            {
                WorkingDirectory = root,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };
            start.ArgumentList.Add(Path.Combine(root, "scripts", "validate-publication-preflight.sh"));
            start.ArgumentList.Add("99.0.0");
            start.ArgumentList.Add(phase);
            start.Environment["HEXALITH_BUILDS_EXECUTION_SHA"] = new string('a', 40);
            start.Environment["HEXALITH_RELEASE_ENVIRONMENT"] = "production";
            start.Environment["HEXALITH_RELEASE_SOURCE_BRANCH"] = "main";
            start.Environment["HEXALITH_RELEASE_SOURCE_CI_WORKFLOW"] = "ci.yml";
            start.Environment["HEXALITH_RELEASE_PACKAGE_MANIFEST"] = "tools/release-packages.json";
            start.Environment["GITHUB_SHA"] = new string('b', 40);
            start.Environment["HEXALITH_PUBLICATION_PREFLIGHT"] = recordingPreflight;
            start.Environment["HEXALITH_ZOT_REGISTRY"] = "registry.hexalith.com";
            start.Environment["PREFLIGHT_INVOCATION_MARKER"] = invocationMarker;
            start.Environment["PREFLIGHT_ARGUMENTS"] = argumentsPath;
            start.Environment["HEXALITH_RELEASE_REQUIRE_AUTHORITY"] = "false";
            start.Environment.Remove("HEXALITH_RELEASE_EXPECTED_PACKAGE_COUNT");
            if (workflowPackageCount is not null)
            {
                start.Environment["HEXALITH_RELEASE_EXPECTED_PACKAGE_COUNT"] = workflowPackageCount;
            }

            using Process process = new() { StartInfo = start };
            process.Start().ShouldBeTrue("Could not start the publication preflight wrapper.");
            Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
            Task<string> errorTask = process.StandardError.ReadToEndAsync();
            bool exited = process.WaitForExit((int)PublicationPreflightTimeout.TotalMilliseconds);
            if (!exited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
            }

            string output = outputTask.GetAwaiter().GetResult();
            string error = errorTask.GetAwaiter().GetResult();
            if (!exited)
            {
                throw new TimeoutException(
                    $"Publication preflight wrapper timed out after {PublicationPreflightTimeout}. " +
                    $"Output: {output} Error: {error}");
            }

            string[] arguments = File.Exists(argumentsPath)
                ? File.ReadAllLines(argumentsPath)
                : [];
            return (process.ExitCode, output, error, File.Exists(invocationMarker), arguments);
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
