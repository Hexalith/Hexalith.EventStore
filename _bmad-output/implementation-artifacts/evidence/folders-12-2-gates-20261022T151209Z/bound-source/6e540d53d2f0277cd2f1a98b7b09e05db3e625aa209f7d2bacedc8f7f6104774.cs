
using Dapr.Client;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Hexalith.EventStore.HealthChecks;
/// <summary>
/// Health check that verifies DAPR sidecar responsiveness.
/// </summary>
public class DaprSidecarHealthCheck(DaprClient daprClient) : IHealthCheck {
    private readonly DaprClient _daprClient = daprClient
        ?? throw new ArgumentNullException(nameof(daprClient));

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(context);

        try {
            bool isHealthy = await _daprClient.CheckHealthAsync(cancellationToken)
                .ConfigureAwait(false);

            return isHealthy
                ? HealthCheckResult.Healthy("Dapr sidecar is responsive.")
                : new HealthCheckResult(
                    context.Registration.FailureStatus,
                    "Dapr sidecar is not responsive.");
        }
        catch (Exception ex) {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                $"Dapr sidecar health check failed: {ex.GetType().Name}",
                exception: ex);
        }
    }
}
