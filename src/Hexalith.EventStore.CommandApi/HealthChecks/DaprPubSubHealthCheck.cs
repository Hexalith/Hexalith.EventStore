namespace Hexalith.EventStore.CommandApi.HealthChecks;

using Dapr.Client;

using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Health check that verifies DAPR pub/sub component availability via the metadata API.
/// </summary>
public class DaprPubSubHealthCheck(DaprClient daprClient, string pubSubName) : IHealthCheck {
    private readonly DaprClient _daprClient = daprClient
        ?? throw new ArgumentNullException(nameof(daprClient));
    private readonly string _pubSubName = pubSubName
        ?? throw new ArgumentNullException(nameof(pubSubName));

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(context);

        try {
            var metadata = await _daprClient.GetMetadataAsync(cancellationToken)
                .ConfigureAwait(false);

            var component = metadata.Components.FirstOrDefault(c =>
                string.Equals(c.Name, _pubSubName, StringComparison.OrdinalIgnoreCase)
                && c.Type.StartsWith("pubsub.", StringComparison.OrdinalIgnoreCase));

            return component != null
                ? HealthCheckResult.Healthy($"Dapr pub/sub component '{_pubSubName}' is available (type: {component.Type}).")
                : new HealthCheckResult(
                    context.Registration.FailureStatus,
                    $"Dapr pub/sub component '{_pubSubName}' not found in metadata.");
        }
        catch (Exception ex) {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                $"Dapr pub/sub health check failed: {ex.GetType().Name}",
                exception: ex);
        }
    }
}
