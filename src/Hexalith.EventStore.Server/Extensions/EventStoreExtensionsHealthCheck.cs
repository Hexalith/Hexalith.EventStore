using System.Globalization;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Hexalith.EventStore.Server.Extensions;

/// <summary>Reports the verified extension identities and majors this gateway loaded.</summary>
/// <param name="catalog">The verified extension catalog.</param>
internal sealed class EventStoreExtensionsHealthCheck(EventStoreExtensionCatalog catalog) : IHealthCheck
{
    /// <inheritdoc/>
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        Dictionary<string, object> data = new(StringComparer.Ordinal);
        foreach (EventStoreLoadedExtension extension in catalog.Extensions)
        {
            data[extension.ExtensionId] = extension.ApiMajor;
        }

        string loaded = string.Join(", ", catalog.Extensions.Select(extension =>
            extension.ExtensionId + "@" + extension.ApiMajor.ToString(CultureInfo.InvariantCulture) + " (" + extension.PackageId + ")"));
        return Task.FromResult(HealthCheckResult.Healthy("Loaded EventStore extensions: " + loaded + ".", data));
    }
}
