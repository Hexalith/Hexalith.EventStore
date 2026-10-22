
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Hexalith.EventStore.HealthChecks;

/// <summary>
/// Health check that verifies the DAPR actor host has joined the placement table.
/// </summary>
/// <remarks>
/// Distinct from <see cref="DaprSidecarHealthCheck"/>: the sidecar's own <c>/healthz</c> reports
/// healthy even when actor placement is unreachable, so a placement outage is otherwise silent —
/// every actor invocation (projection queries, command aggregates) then hangs until its client
/// timeout and surfaces upstream as an opaque <c>TaskCanceledException</c>. This check fails fast
/// and explicitly when <c>actorRuntime.hostReady</c> is <see langword="false"/>.
/// </remarks>
public sealed class DaprActorPlacementHealthCheck(IDaprActorPlacementProbe probe) : IHealthCheck {
    private readonly IDaprActorPlacementProbe _probe = probe
        ?? throw new ArgumentNullException(nameof(probe));

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(context);

        try {
            DaprActorPlacementStatus status = await _probe.CheckAsync(cancellationToken)
                .ConfigureAwait(false);

            if (status.HostReady) {
                return HealthCheckResult.Healthy(
                    $"DAPR actor host ready ({status.Placement ?? "placement: connected"}).");
            }

            return new HealthCheckResult(
                context.Registration.FailureStatus,
                $"DAPR actor host not ready ({status.Placement ?? "placement: disconnected"}, "
                + $"runtimeStatus={status.RuntimeStatus ?? "unknown"}). Actor invocations (projection "
                + "queries, command aggregates) will hang until timeout. Check the DAPR placement service.");
        }
        catch (Exception ex) {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                $"DAPR actor placement health check failed: {ex.GetType().Name}",
                exception: ex);
        }
    }
}
