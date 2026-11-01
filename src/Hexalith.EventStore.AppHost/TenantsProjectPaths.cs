using Hexalith.EventStore.Aspire;

namespace Hexalith.EventStore.AppHost;

/// <summary>
/// Resolves and validates the checked-out Tenants host projects used by plain local Aspire runs.
/// </summary>
/// <param name="DomainServiceProjectPath">The Tenants domain-service project path.</param>
/// <param name="ApiProjectPath">The Tenants external API project path.</param>
internal sealed record TenantsProjectPaths(
    string DomainServiceProjectPath,
    string ApiProjectPath)
{
    /// <summary>Resolves both Tenants host paths and fails before either resource is registered.</summary>
    /// <returns>The validated Tenants host project paths.</returns>
    internal static TenantsProjectPaths Resolve()
        => Resolve(RepositoryProjectPaths.GetRepositoryRoot());

    /// <summary>Prefers complete repository-local hosts, then the sibling Tenants checkout.</summary>
    /// <param name="repositoryRoot">The EventStore repository root.</param>
    /// <returns>The validated host paths from one checkout.</returns>
    internal static TenantsProjectPaths Resolve(string repositoryRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);

        string root = Path.GetFullPath(repositoryRoot);
        TenantsProjectPaths[] candidates =
        [
            FromCheckout(Path.Combine(root, "references", "Hexalith.Tenants")),
            FromCheckout(Path.GetFullPath(Path.Combine(root, "..", "Hexalith.Tenants"))),
        ];

        foreach (TenantsProjectPaths candidate in candidates)
        {
            if (File.Exists(candidate.DomainServiceProjectPath) && File.Exists(candidate.ApiProjectPath))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException(
            "Plain local Aspire run requires both Hexalith.Tenants host projects from the same checkout before any resources can start. "
            + "Use the workspace root-declared Tenants checkout. Never initialize nested submodules. "
            + $"Missing project(s): {string.Join(", ", candidates.SelectMany(static paths => new[] { paths.DomainServiceProjectPath, paths.ApiProjectPath })
                .Where(static path => !File.Exists(path)).Select(static path => $"'{path}'"))}");
    }

    /// <summary>Builds both host paths within a single Tenants checkout.</summary>
    /// <param name="checkoutRoot">The Tenants checkout root.</param>
    /// <returns>The host project paths.</returns>
    private static TenantsProjectPaths FromCheckout(string checkoutRoot)
        => new(
            Path.Combine(checkoutRoot, "src", "Hexalith.Tenants", "Hexalith.Tenants.csproj"),
            Path.Combine(checkoutRoot, "src", "Hexalith.Tenants.Api", "Hexalith.Tenants.Api.csproj"));
}
