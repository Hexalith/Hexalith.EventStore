namespace Hexalith.EventStore.AppHost.Tests.Configuration;

using System.ComponentModel;
using System.Diagnostics;

using global::Aspire.Hosting.ApplicationModel;
using global::Aspire.Hosting.Testing;

using Hexalith.EventStore.Aspire;

[Collection(AspireEnvironmentMutationCollection.Name)]
public sealed class AspireSecurityResourceNamingTests
{
    private const string ReferenceRelationshipType = "Reference";
    private const string SecurityResourceName = "security";

    // A pattern guaranteed to match tracked text inside the audited pathspec. Without it, a
    // mis-resolved repository root or a pathspec that no longer matches any file would make
    // `git grep` exit 1 (no match) and the negative audit would pass vacuously.
    private const string PositiveControlPattern = "HexalithEventStoreSecurityOptions";

    // Root-owned trees that carry operator- and agent-facing guidance. `references` (submodules)
    // and `_bmad`/`_bmad-output` (workflow artifacts that quote these patterns as data) are out of
    // scope. `CHANGELOG.md` is generated from commit subjects and is not hand-correctable.
    private static readonly string[] _auditPathspec =
    [
        ".agents",
        ".claude",
        ".codex",
        ".github",
        ".opencode",
        "deploy",
        "docs",
        "perf",
        "samples",
        "scripts",
        "src",
        "tests",
        "tools",
        ":(glob,top)*.md",
        ":(exclude)docs/api/**",
        ":(exclude,glob,top)CHANGELOG.md",
    ];

    [Fact]
    public async Task AppHostModel_WhenSecurityEnabled_UsesSecurityResourceAndDependencyEdges()
    {
        string? originalSkipPrerequisiteCheck = Environment.GetEnvironmentVariable("SKIP_PREREQUISITE_CHECK");
        string? originalEnableKeycloak = Environment.GetEnvironmentVariable(
            HexalithEventStoreSecurityOptions.DefaultEnableKeycloakConfigurationKey);

        try
        {
            Environment.SetEnvironmentVariable("SKIP_PREREQUISITE_CHECK", "true");
            Environment.SetEnvironmentVariable(
                HexalithEventStoreSecurityOptions.DefaultEnableKeycloakConfigurationKey,
                "true");

            await using IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder
                .CreateAsync<Projects.Hexalith_EventStore_AppHost>()
                .ConfigureAwait(true);

            IResource security = builder.Resources.Single(
                static resource => string.Equals(
                    resource.Name,
                    SecurityResourceName,
                    StringComparison.Ordinal));

            HexalithEventStoreSecurityOptions.DefaultResourceName.ShouldBe(SecurityResourceName);
            security.ShouldBeOfType<KeycloakResource>();
            builder.Resources.ShouldNotContain(
                static resource => string.Equals(resource.Name, ObsoleteRoleName(), StringComparison.OrdinalIgnoreCase));

            Dictionary<string, int> expectedReferenceCounts = new(StringComparer.Ordinal)
            {
                ["eventstore"] = 3,
                ["eventstore-admin"] = 3,
                ["eventstore-admin-ui"] = 2,
                ["sample-api"] = 2,
                ["sample-blazor-ui"] = 2,
            };
            if (builder.Resources.Any(static resource => string.Equals(resource.Name, "tenants", StringComparison.Ordinal)))
            {
                expectedReferenceCounts.Add("tenants", 3);
            }

            if (builder.Resources.Any(static resource => string.Equals(resource.Name, "tenants-api", StringComparison.Ordinal)))
            {
                expectedReferenceCounts.Add("tenants-api", 2);
            }

            string[] expectedDependents = expectedReferenceCounts.Keys.Order(StringComparer.Ordinal).ToArray();
            builder.Resources
                .Where(resource => resource.Annotations
                    .OfType<ResourceRelationshipAnnotation>()
                    .Any(annotation => string.Equals(annotation.Type, ReferenceRelationshipType, StringComparison.Ordinal)
                        && ReferenceEquals(annotation.Resource, security)))
                .Select(static resource => resource.Name)
                .Order(StringComparer.Ordinal)
                .ShouldBe(expectedDependents);
            builder.Resources
                .Where(resource => resource.Annotations
                    .OfType<WaitAnnotation>()
                    .Any(annotation => ReferenceEquals(annotation.Resource, security)))
                .Select(static resource => resource.Name)
                .Order(StringComparer.Ordinal)
                .ShouldBe(expectedDependents);

            foreach (KeyValuePair<string, int> expected in expectedReferenceCounts)
            {
                IResource dependent = builder.Resources.Single(resource => string.Equals(
                    resource.Name,
                    expected.Key,
                    StringComparison.Ordinal));
                dependent.Annotations
                    .OfType<ResourceRelationshipAnnotation>()
                    .Count(annotation => string.Equals(annotation.Type, ReferenceRelationshipType, StringComparison.Ordinal)
                        && ReferenceEquals(annotation.Resource, security))
                    .ShouldBe(expected.Value, $"Expected the exact Reference edge count for {expected.Key}.");
                dependent.Annotations
                    .OfType<WaitAnnotation>()
                    .Count(annotation => ReferenceEquals(annotation.Resource, security))
                    .ShouldBe(1, $"Expected exactly one WaitFor edge from {expected.Key} to {SecurityResourceName}.");
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("SKIP_PREREQUISITE_CHECK", originalSkipPrerequisiteCheck);
            Environment.SetEnvironmentVariable(
                HexalithEventStoreSecurityOptions.DefaultEnableKeycloakConfigurationKey,
                originalEnableKeycloak);
        }
    }

    [Fact]
    public async Task AppHostModel_WhenSecurityDisabled_HasNoSecurityResourceOrDependencyEdges()
    {
        string? originalSkipPrerequisiteCheck = Environment.GetEnvironmentVariable("SKIP_PREREQUISITE_CHECK");
        string? originalEnableKeycloak = Environment.GetEnvironmentVariable(
            HexalithEventStoreSecurityOptions.DefaultEnableKeycloakConfigurationKey);

        try
        {
            Environment.SetEnvironmentVariable("SKIP_PREREQUISITE_CHECK", "true");
            Environment.SetEnvironmentVariable(
                HexalithEventStoreSecurityOptions.DefaultEnableKeycloakConfigurationKey,
                "false");

            await using IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder
                .CreateAsync<Projects.Hexalith_EventStore_AppHost>()
                .ConfigureAwait(true);

            builder.Resources.ShouldNotContain(
                static resource => string.Equals(resource.Name, SecurityResourceName, StringComparison.Ordinal));

            builder.Resources
                .SelectMany(static resource => resource.Annotations
                    .OfType<ResourceRelationshipAnnotation>()
                    .Select(annotation => new { Dependent = resource.Name, Target = annotation.Resource.Name }))
                .Where(static edge => string.Equals(edge.Target, SecurityResourceName, StringComparison.Ordinal))
                .Select(static edge => edge.Dependent)
                .ShouldBeEmpty();
            builder.Resources
                .SelectMany(static resource => resource.Annotations
                    .OfType<WaitAnnotation>()
                    .Select(annotation => new { Dependent = resource.Name, Target = annotation.Resource.Name }))
                .Where(static edge => string.Equals(edge.Target, SecurityResourceName, StringComparison.Ordinal))
                .Select(static edge => edge.Dependent)
                .ShouldBeEmpty();
        }
        finally
        {
            Environment.SetEnvironmentVariable("SKIP_PREREQUISITE_CHECK", originalSkipPrerequisiteCheck);
            Environment.SetEnvironmentVariable(
                HexalithEventStoreSecurityOptions.DefaultEnableKeycloakConfigurationKey,
                originalEnableKeycloak);
        }
    }

    [Fact]
    public async Task RootOwnedSources_WhenScanned_ContainNoStaleSecurityRoleIdentities()
    {
        string repositoryRoot = RepositoryProjectPaths.GetRepositoryRoot();
        string implementationName = ObsoleteRoleName();
        string optionalNamedArgument = @"(?:[A-Za-z_][A-Za-z0-9_]*\s*:\s*)?";
        string[] staleIdentityPatterns =
        [
            $@"AddKeycloak\s*\(\s*{optionalNamedArgument}""{implementationName}""",
            $@"(?:GetEndpoint|CreateHttpClient|WaitForResourceHealthyAsync)\s*\(\s*{optionalNamedArgument}""{implementationName}""",
            $@"https?://{implementationName}(?=[:/""'\s]|$)",
            $@"`{implementationName}`",
            $@"^\s*{implementationName}\s*:",
            $@"compose\s+(?:ps|logs)\s+{implementationName}(?=\s|$)",
            $@"name\s*=\s*{implementationName}(?=[""'\s)]|$)",
            $@"WaitFor\s*\(\s*{implementationName}\s*\)",
            $@"""to""\s*:\s*""{implementationName}""",
            $@"SecurityResourceName\s*=\s*""{implementationName}""",
            $@"^\s*\|\s*All\s+services\s*\|\s*{implementationName}\s*\|",
        ];

        // Positive control first: prove the scan actually reaches tracked repository text, so a
        // clean negative result cannot be produced by a scan that inspected nothing.
        string[] controlMatches = await FindTrackedMatchesAsync(repositoryRoot, [PositiveControlPattern])
            .ConfigureAwait(true);
        controlMatches.ShouldNotBeEmpty(
            "The stale-identity audit scanned no tracked text; its repository root or pathspec is wrong.");

        string[] staleMatches = await FindTrackedMatchesAsync(repositoryRoot, staleIdentityPatterns)
            .ConfigureAwait(true);

        staleMatches.ShouldBeEmpty();
    }

    /// <summary>
    /// Builds the obsolete role identity at run time. The audit's own patterns are interpolated
    /// from this value for the same reason: a literal in this file would match the patterns it
    /// defines and turn the audit permanently red against itself. Do not inline it.
    /// </summary>
    private static string ObsoleteRoleName() => "key" + "cloak";

    private static async Task<string[]> FindTrackedMatchesAsync(string repositoryRoot, IEnumerable<string> patterns)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("grep");
        startInfo.ArgumentList.Add("--line-number");
        startInfo.ArgumentList.Add("--full-name");
        startInfo.ArgumentList.Add("--ignore-case");
        startInfo.ArgumentList.Add("--perl-regexp");
        startInfo.ArgumentList.Add("-I");
        foreach (string pattern in patterns)
        {
            startInfo.ArgumentList.Add("-e");
            startInfo.ArgumentList.Add(pattern);
        }

        startInfo.ArgumentList.Add("--");
        foreach (string pathspec in _auditPathspec)
        {
            startInfo.ArgumentList.Add(pathspec);
        }

        Process process;
        try
        {
            process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Could not start git for the tracked stale-identity audit.");
        }
        catch (Win32Exception exception)
        {
            throw new InvalidOperationException(
                "git must be available on PATH to run the tracked stale-identity audit.",
                exception);
        }

        using (process)
        {
            // Drain both pipes concurrently before waiting: reading them one after the other
            // deadlocks whenever the unread pipe fills its buffer.
            Task<string> standardOutputTask = process.StandardOutput.ReadToEndAsync();
            Task<string> standardErrorTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync().ConfigureAwait(true);
            string standardOutput = await standardOutputTask.ConfigureAwait(true);
            string standardError = await standardErrorTask.ConfigureAwait(true);

            if (process.ExitCode == 1)
            {
                return [];
            }

            return process.ExitCode == 0
                ? standardOutput.Split(
                    ['\r', '\n'],
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                : throw new InvalidOperationException(
                    $"Tracked stale-identity audit failed with git exit code {process.ExitCode}: {standardError.Trim()}");
        }
    }
}
