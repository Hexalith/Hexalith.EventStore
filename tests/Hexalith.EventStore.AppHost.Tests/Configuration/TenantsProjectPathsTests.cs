using Hexalith.EventStore.AppHost;

namespace Hexalith.EventStore.AppHost.Tests.Configuration;

/// <summary>Exercises standalone and sibling checkout layouts without initializing submodules.</summary>
public sealed class TenantsProjectPathsTests : IDisposable
{
    private readonly string _workspaceRoot = Path.Combine(Path.GetTempPath(), $"eventstore-tenants-paths-{Guid.NewGuid():N}");

    /// <summary>Prefers the standalone checkout even when a sibling is available.</summary>
    /// <param name="hasSibling">Whether a second complete checkout is present.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResolvePrefersCompleteRepositoryLocalHosts(bool hasSibling)
    {
        string repositoryRoot = Path.Combine(_workspaceRoot, "Hexalith.EventStore");
        TenantsProjectPaths nested = CreateHosts(Path.Combine(repositoryRoot, "references", "Hexalith.Tenants"));
        if (hasSibling)
        {
            _ = CreateHosts(Path.Combine(_workspaceRoot, "Hexalith.Tenants"));
        }

        TenantsProjectPaths.Resolve(repositoryRoot).ShouldBe(nested);
    }

    /// <summary>Uses both sibling hosts when either repository-local host is unavailable.</summary>
    /// <param name="existingNestedHost">The host left in the incomplete local checkout, if any.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("Hexalith.Tenants")]
    [InlineData("Hexalith.Tenants.Api")]
    public void ResolveFallsBackToCompleteSiblingHosts(string? existingNestedHost)
    {
        string repositoryRoot = Path.Combine(_workspaceRoot, "Hexalith.EventStore");
        string nestedRoot = Path.Combine(repositoryRoot, "references", "Hexalith.Tenants");
        Directory.CreateDirectory(nestedRoot);
        if (existingNestedHost is not null)
        {
            WriteProject(nestedRoot, existingNestedHost);
        }

        TenantsProjectPaths sibling = CreateHosts(Path.Combine(_workspaceRoot, "Hexalith.Tenants"));

        TenantsProjectPaths.Resolve(repositoryRoot).ShouldBe(sibling);
    }

    /// <summary>Fails with both attempted locations and safe checkout advice when no hosts exist.</summary>
    [Fact]
    public void ResolveWithoutHostsReportsBothLocationsWithoutNestedInitializationAdvice()
    {
        string repositoryRoot = Path.Combine(_workspaceRoot, "Hexalith.EventStore");

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => TenantsProjectPaths.Resolve(repositoryRoot));

        exception.Message.ShouldContain(Path.Combine(repositoryRoot, "references", "Hexalith.Tenants"));
        exception.Message.ShouldContain(Path.Combine(_workspaceRoot, "Hexalith.Tenants"));
        exception.Message.ShouldContain("Hexalith.Tenants.csproj");
        exception.Message.ShouldContain("Hexalith.Tenants.Api.csproj");
        exception.Message.ShouldContain("workspace root-declared Tenants checkout");
        exception.Message.ShouldContain("Never initialize nested submodules");
    }

    /// <summary>Reports the missing host through the production resolver for an incomplete checkout.</summary>
    /// <param name="existingHost">The only host project present.</param>
    [Theory]
    [InlineData("Hexalith.Tenants")]
    [InlineData("Hexalith.Tenants.Api")]
    public void ResolveWithOnlyOneHostReportsMissingHost(string existingHost)
    {
        string repositoryRoot = Path.Combine(_workspaceRoot, "Hexalith.EventStore");
        string checkoutRoot = Path.Combine(repositoryRoot, "references", "Hexalith.Tenants");
        string existingPath = WriteProject(checkoutRoot, existingHost);
        string missingHost = existingHost == "Hexalith.Tenants" ? "Hexalith.Tenants.Api" : "Hexalith.Tenants";

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => TenantsProjectPaths.Resolve(repositoryRoot));

        exception.Message.ShouldContain(Path.Combine(checkoutRoot, "src", missingHost, missingHost + ".csproj"));
        exception.Message.ShouldNotContain(existingPath);
        exception.Message.ShouldContain("Never initialize nested submodules");
    }

    /// <summary>Never mixes a domain host from one checkout with an API host from another.</summary>
    /// <param name="nestedHost">The only host present in the repository-local checkout.</param>
    [Theory]
    [InlineData("Hexalith.Tenants")]
    [InlineData("Hexalith.Tenants.Api")]
    public void ResolveRejectsHostsSplitAcrossCheckouts(string nestedHost)
    {
        string repositoryRoot = Path.Combine(_workspaceRoot, "Hexalith.EventStore");
        WriteProject(Path.Combine(repositoryRoot, "references", "Hexalith.Tenants"), nestedHost);
        WriteProject(Path.Combine(_workspaceRoot, "Hexalith.Tenants"),
            nestedHost == "Hexalith.Tenants" ? "Hexalith.Tenants.Api" : "Hexalith.Tenants");

        Should.Throw<InvalidOperationException>(() => TenantsProjectPaths.Resolve(repositoryRoot))
            .Message.ShouldContain("same checkout");
    }

    /// <summary>Removes only this test's temporary checkout layout.</summary>
    public void Dispose()
    {
        if (Directory.Exists(_workspaceRoot))
        {
            Directory.Delete(_workspaceRoot, recursive: true);
        }
    }

    private static TenantsProjectPaths CreateHosts(string checkoutRoot)
        => new(WriteProject(checkoutRoot, "Hexalith.Tenants"), WriteProject(checkoutRoot, "Hexalith.Tenants.Api"));

    private static string WriteProject(string checkoutRoot, string projectName)
    {
        string projectDirectory = Path.Combine(checkoutRoot, "src", projectName);
        Directory.CreateDirectory(projectDirectory);
        string projectPath = Path.Combine(projectDirectory, projectName + ".csproj");
        File.WriteAllText(projectPath, "<Project />");
        return projectPath;
    }
}
