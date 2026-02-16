namespace Hexalith.EventStore.CommandApi.HealthChecks;

using Dapr.Client;

using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Health check that verifies DAPR configuration store accessibility via the metadata API.
/// </summary>
public class DaprConfigStoreHealthCheck(DaprClient daprClient, string configStoreName) : IHealthCheck
{
    private readonly DaprClient _daprClient = daprClient
        ?? throw new ArgumentNullException(nameof(daprClient));
    private readonly string _configStoreName = configStoreName
        ?? throw new ArgumentNullException(nameof(configStoreName));

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

            var component = metadata.Components.FirstOrDefault(c =>
                string.Equals(c.Name, _configStoreName, StringComparison.OrdinalIgnoreCase)
                && c.Type.StartsWith("configuration.", StringComparison.OrdinalIgnoreCase));

            return component != null
                ? HealthCheckResult.Healthy($"Dapr config store '{_configStoreName}' is accessible (type: {component.Type}).")
                : new HealthCheckResult(
                    context.Registration.FailureStatus,
                    $"Dapr config store component '{_configStoreName}' not found in metadata.");
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                $"Dapr config store health check failed: {ex.GetType().Name}",
                exception: ex);
        }
    }
}
