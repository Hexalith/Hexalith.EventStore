using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Controllers;

/// <summary>Selects the additive metadata-only transport only for the explicit private source Development lane.</summary>
internal static class AtomicNativeMetadataInspectionPolicy
{
    /// <summary>Checks the explicit opt-in and the actual effective SDK profile and hosting environment.</summary>
    /// <param name="configuration">The actual host configuration.</param>
    /// <param name="options">The actual effective SDK host options.</param>
    /// <param name="environment">The actual hosting environment.</param>
    /// <returns>Whether this private Debug/Development metadata transport is selected.</returns>
    internal static bool IsEnabled(IConfiguration? configuration, IOptions<RouteCatalogHostOptions>? options, IHostEnvironment? environment)
    {
#if DEBUG
        return bool.TryParse(configuration?["EventStore:Testing:AtomicNativeMetadataInspection:Enabled"], out bool selected) && selected
            && environment?.IsDevelopment() == true && options?.Value is
                { DevelopmentFixture: true, Profile: "Development", RequireTechnicalHttpBinding: true };
#else
        return false;
#endif
    }
}
