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
    /// <summary>The root submodule initialization command named by startup diagnostics.</summary>
    internal const string SubmoduleInitializationCommand =
        "git submodule update --init references/Hexalith.Tenants";

    /// <summary>Resolves both Tenants host paths and fails before either resource is registered.</summary>
    /// <returns>The validated Tenants host project paths.</returns>
    internal static TenantsProjectPaths Resolve()
    {
        var paths = new TenantsProjectPaths(
            RepositoryProjectPaths.GetReferencedModuleProjectPath(
                "Hexalith.Tenants",
                "src",
                "Hexalith.Tenants",
                "Hexalith.Tenants.csproj"),
            RepositoryProjectPaths.GetReferencedModuleProjectPath(
                "Hexalith.Tenants",
                "src",
                "Hexalith.Tenants.Api",
                "Hexalith.Tenants.Api.csproj"));

        paths.Validate();
        return paths;
    }

    /// <summary>Validates that both resolved Tenants host projects exist.</summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when either host project is unavailable and the root Tenants submodule must be initialized.
    /// </exception>
    internal void Validate()
    {
        string[] missingProjects =
        [
            .. new[] { DomainServiceProjectPath, ApiProjectPath }
                .Where(static path => !File.Exists(path)),
        ];
        if (missingProjects.Length == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            "Plain local Aspire run requires both Hexalith.Tenants host projects before any resources can start. "
            + $"Initialize the root Tenants submodule with '{SubmoduleInitializationCommand}'. "
            + $"Missing project(s): {string.Join(", ", missingProjects.Select(static path => $"'{path}'"))}");
    }
}
