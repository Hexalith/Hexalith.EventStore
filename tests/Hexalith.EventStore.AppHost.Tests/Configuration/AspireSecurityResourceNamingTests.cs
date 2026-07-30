namespace Hexalith.EventStore.AppHost.Tests.Configuration;

using System.Diagnostics;

using global::Aspire.Hosting.ApplicationModel;
using global::Aspire.Hosting.Testing;

using Hexalith.EventStore.Aspire;

[Collection(AspireEnvironmentMutationCollection.Name)]
public sealed class AspireSecurityResourceNamingTests
{
    private const string ReferenceRelationshipType = "Reference";
    private const string SecurityResourceName = "security";

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
                static resource => string.Equals(resource.Name, "key" + "cloak", StringComparison.OrdinalIgnoreCase));

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
    public void RootOwnedSources_WhenScanned_ContainNoStaleSecurityRoleIdentities()
    {
        string repositoryRoot = RepositoryProjectPaths.GetRepositoryRoot();
        string implementationName = "key" + "cloak";
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

        string[] staleMatches = FindTrackedStaleIdentityMatches(repositoryRoot, staleIdentityPatterns);

        staleMatches.ShouldBeEmpty();
    }

    private static string[] FindTrackedStaleIdentityMatches(string repositoryRoot, IEnumerable<string> patterns)
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
        startInfo.ArgumentList.Add("src");
        startInfo.ArgumentList.Add("tests");
        startInfo.ArgumentList.Add("deploy");
        startInfo.ArgumentList.Add("docs");
        startInfo.ArgumentList.Add(":(exclude)docs/api/**");

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start git for the tracked stale-identity audit.");
        string standardOutput = process.StandardOutput.ReadToEnd();
        string standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode == 1)
        {
            return [];
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Tracked stale-identity audit failed with git exit code {process.ExitCode}: {standardError.Trim()}");
        }

        return standardOutput.Split(
            ['\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
