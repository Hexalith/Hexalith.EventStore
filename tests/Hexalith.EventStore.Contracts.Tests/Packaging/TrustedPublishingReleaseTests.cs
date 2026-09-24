using System.Diagnostics;
using System.Text.Json;

namespace Hexalith.EventStore.Contracts.Tests.Packaging;

/// <summary>
/// Guards the EventStore-owned NuGet trusted publishing boundary.
/// </summary>
public sealed class TrustedPublishingReleaseTests
{
    /// <summary>
    /// Ensures the protected EventStore job obtains OIDC authority and never maps a stored NuGet key.
    /// </summary>
    [Fact]
    public void PublishJobUsesEventStoreWorkflowIdentityAndTemporaryNuGetKey()
    {
        string root = FindRepositoryRoot();
        string workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "release.yml"));
        string job = workflow[workflow.IndexOf("\n  release:", StringComparison.Ordinal)..];

        job.ShouldContain("needs: verify-source");
        job.ShouldContain("runs-on: ubuntu-latest");
        job.ShouldContain("environment: production");
        job.ShouldContain("id-token: write");
        job.ShouldContain("NuGet/login@8d196754b4036150537f80ac539e15c2f1028841");
        job.ShouldContain("user: ${{ vars.NUGET_TRUSTED_PUBLISHING_USER }}");
        job.ShouldContain("NUGET_TRUSTED_PUBLISHING_KEY: ${{ steps.nuget-login.outputs.NUGET_API_KEY }}");
        job.ShouldNotContain("secrets.NUGET_API_KEY");
        job.ShouldNotContain("domain-release.yml");
        job.IndexOf("Revalidate current source before Semantic Release", StringComparison.Ordinal)
            .ShouldBeLessThan(job.IndexOf("Exchange GitHub OIDC for temporary NuGet key", StringComparison.Ordinal));
        job.IndexOf("Exchange GitHub OIDC for temporary NuGet key", StringComparison.Ordinal)
            .ShouldBeLessThan(job.IndexOf("name: Semantic Release", StringComparison.Ordinal));
    }

    /// <summary>
    /// Ensures the release retains its approved shared actions, source, package, and container gates.
    /// </summary>
    [Fact]
    public void PublishJobRetainsExactSourceAndDestinationPreflights()
    {
        string root = FindRepositoryRoot();
        string workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "release.yml"));
        string configuration = File.ReadAllText(Path.Combine(root, ".releaserc.json"));
        using JsonDocument manifest = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(root, "tools", "release-packages.json")));

        manifest.RootElement.GetProperty("packages").GetArrayLength().ShouldBe(14);
        workflow.ShouldContain("22a578b576a515d2af214fe81859447fffc97981");
        workflow.ShouldContain("uses: ./.hexalith/builds-execution/Github/publish-containers");
        workflow.ShouldContain("source_ci_workflow=\"ci.yml\"");
        workflow.ShouldContain("source_ci_workflow=\"commitlint.yml\"");
        workflow.ShouldContain("HEXALITH_RELEASE_EXPECTED_PACKAGE_COUNT: '14'");
        workflow.ShouldContain("HEXALITH_RELEASE_REQUIRE_AUTHORITY: 'false'");
        workflow.ShouldContain("src/Hexalith.EventStore/Hexalith.EventStore.csproj|eventstore");
        configuration.ShouldContain("scripts/validate-publication-preflight.sh");
        configuration.ShouldContain("dotnet nuget push");
        configuration.ShouldContain("$NUGET_TRUSTED_PUBLISHING_KEY");
        configuration.ShouldNotContain("$NUGET_API_KEY");
    }

    /// <summary>
    /// Ensures the credential preflight accepts only the temporary trusted publishing key.
    /// </summary>
    [Fact]
    public void CredentialPreflightDoesNotAcceptLegacyNuGetSecret()
    {
        string root = FindRepositoryRoot();
        string preflight = File.ReadAllText(Path.Combine(root, "scripts", "validate-release-secrets.sh"));
        string checklist = File.ReadAllText(Path.Combine(root, "docs", "ci-secrets-checklist.md"));

        preflight.ShouldContain("NUGET_TRUSTED_PUBLISHING_KEY");
        preflight.ShouldNotContain("${NUGET_API_KEY");
        checklist.ShouldContain("Total user-managed secrets: 7");
        checklist.ShouldContain("`release.yml`");
        checklist.ShouldContain("environment `production`");
    }

    /// <summary>
    /// Proves a legacy API key cannot satisfy the preflight, while a temporary key can.
    /// </summary>
    [Fact]
    public void CredentialPreflightRequiresTemporaryKey()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("The release credential preflight is a POSIX shell script.");
        }

        string root = FindRepositoryRoot();
        string script = Path.Combine(root, "scripts", "validate-release-secrets.sh");
        string workingDirectory = Path.Combine(Path.GetTempPath(), "eventstore-trusted-publishing-" + Guid.NewGuid());
        Directory.CreateDirectory(workingDirectory);
        try
        {
            RunPreflight(script, workingDirectory, temporaryKey: null).ShouldNotBe(0);
            RunPreflight(script, workingDirectory, temporaryKey: "synthetic-ephemeral-key").ShouldBe(0);
        }
        finally
        {
            Directory.Delete(workingDirectory);
        }
    }

    private static int RunPreflight(string script, string workingDirectory, string? temporaryKey)
    {
        ProcessStartInfo start = new("bash")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add(script);
        start.Environment.Remove("NUGET_TRUSTED_PUBLISHING_KEY");
        start.Environment.Remove("HEXALITH_REQUIRE_CONTAINER_PUBLISHER");
        start.Environment.Remove("HEXALITH_CONTAINER_PROJECTS");
        start.Environment["NUGET_API_KEY"] = "legacy-key-must-not-pass";
        if (temporaryKey is not null)
        {
            start.Environment["NUGET_TRUSTED_PUBLISHING_KEY"] = temporaryKey;
        }

        using Process process = Process.Start(start).ShouldNotBeNull();
        process.WaitForExit();
        return process.ExitCode;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, ".releaserc.json"))
                && Directory.Exists(Path.Combine(current.FullName, ".github", "workflows")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("EventStore repository root not found.");
    }
}
