using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Degrades readiness while terminal or malformed delivery evidence remains durable.</summary>
internal sealed class ProjectionDeliveryUnresolvedHealthCheck(
    DaprProjectionDeliveryRetryScheduler scheduler) : IHealthCheck {
    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(context);
        try {
            return await scheduler.HasUnresolvedConflictAsync(cancellationToken).ConfigureAwait(false)
                ? HealthCheckResult.Unhealthy("Projection delivery has unresolved durable conflict evidence.")
                : HealthCheckResult.Healthy("Projection delivery has no unresolved durable conflict evidence.");
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch {
            return HealthCheckResult.Unhealthy("Projection delivery conflict evidence could not be verified.");
        }
    }
}
