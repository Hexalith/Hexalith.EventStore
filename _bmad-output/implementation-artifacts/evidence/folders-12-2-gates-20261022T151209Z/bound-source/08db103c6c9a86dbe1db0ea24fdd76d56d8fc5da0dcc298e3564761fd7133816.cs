using Hexalith.EventStore.Authentication;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Hexalith.EventStore.HealthChecks;

/// <summary>Fails readiness when allow-listed internal callers exist outside Development without an app-channel secret.</summary>
public sealed class DaprAppChannelTokenHealthCheck(DaprAppChannelTokenValidator validator) : IHealthCheck
{
    /// <inheritdoc/>
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(validator.IsConfigured
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Dapr app-channel token is not configured."));
    }
}
