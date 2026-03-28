
using Dapr.Client;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Hexalith.EventStore.HealthChecks;
/// <summary>
/// Health check that verifies DAPR state store connectivity via a read-only sentinel key probe.
/// </summary>
public class DaprStateStoreHealthCheck(DaprClient daprClient, string storeName) : IHealthCheck {
    private readonly DaprClient _daprClient = daprClient
        ?? throw new ArgumentNullException(nameof(daprClient));
    private readonly string _storeName = storeName
        ?? throw new ArgumentNullException(nameof(storeName));

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(context);

        try {
            // Read-only probe: sentinel key that should not exist. Null result = healthy.
            _ = await _daprClient.GetStateAsync<string>(_storeName, "__health_check__", cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return HealthCheckResult.Healthy($"Dapr state store '{_storeName}' is accessible.");
        }
        catch (Exception ex) {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                $"Dapr state store '{_storeName}' is not accessible: {ex.GetType().Name}",
                exception: ex);
        }
    }
}
