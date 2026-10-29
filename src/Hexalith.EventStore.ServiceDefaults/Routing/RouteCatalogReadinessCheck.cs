using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Readiness requires shared persisted authority and exact local host agreement in every environment.</summary>
public sealed class RouteCatalogReadinessCheck(IRouteCatalogAuthority authority, Microsoft.Extensions.Logging.ILogger<RouteCatalogReadinessCheck>? logger = null) : IHealthCheck
{
    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var readback = await authority.ReadbackAsync(cancellationToken).ConfigureAwait(false);
            return readback.Serving ? HealthCheckResult.Healthy("Committed catalog generation verified through Dapr.")
                : HealthCheckResult.Unhealthy("Catalog activation is fencing dispatch.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            RouteCatalogRefusalDiagnostics.Report(logger, "readiness", exception);
            return HealthCheckResult.Unhealthy("No activated catalog agrees with this host's profile, inventory, authentication and topology.");
        }
    }
}
