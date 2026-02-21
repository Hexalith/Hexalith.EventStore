namespace Hexalith.EventStore.CommandApi.HealthChecks;

using Dapr.Client;

using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Health check that verifies DAPR sidecar responsiveness via the gRPC metadata API.
/// Uses <see cref="DaprClient.GetMetadataAsync"/> (gRPC) instead of
/// <see cref="DaprClient.CheckHealthAsync"/> (HTTP) because the Dapr .NET SDK routes
/// health checks through the sidecar's HTTP endpoint, which may be unreachable in
/// Aspire Testing where dynamic port allocation can cause HTTP port mismatches.
/// The gRPC channel is reliably configured via DAPR_GRPC_PORT.
/// </summary>
public class DaprSidecarHealthCheck(DaprClient daprClient) : IHealthCheck
{
    private readonly DaprClient _daprClient = daprClient
        ?? throw new ArgumentNullException(nameof(daprClient));

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            var metadata = await _daprClient.GetMetadataAsync(cancellationToken)
                .ConfigureAwait(false);

            return metadata is not null
                ? HealthCheckResult.Healthy("Dapr sidecar is responsive.")
                : new HealthCheckResult(
                    context.Registration.FailureStatus,
                    "Dapr sidecar is not responsive.");
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                $"Dapr sidecar health check failed: {ex.GetType().Name}",
                exception: ex);
        }
    }
}
