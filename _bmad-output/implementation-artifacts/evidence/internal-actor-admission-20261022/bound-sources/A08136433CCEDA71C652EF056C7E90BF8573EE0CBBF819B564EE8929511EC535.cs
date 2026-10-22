using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Requires the store-global v2 writer marker before the server becomes ready.</summary>
internal sealed class ProjectionDeliveryWriterProtocolHealthCheck(IProjectionDeliveryStateStore stateStore) : IHealthCheck {
    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(context);
        try {
            ProjectionDeliveryWriterProtocol? marker = await stateStore
                .ReadWriterProtocolAsync(cancellationToken)
                .ConfigureAwait(false);
            return marker?.IsCurrent == true
                ? HealthCheckResult.Healthy("Projection delivery writer protocol v2 is active.")
                : HealthCheckResult.Unhealthy("Projection delivery writer protocol v2 is not active.");
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch {
            return HealthCheckResult.Unhealthy("Projection delivery writer protocol could not be verified.");
        }
    }
}
