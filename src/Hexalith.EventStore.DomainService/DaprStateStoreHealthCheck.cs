using Dapr.Client;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Health check that verifies a DAPR state store is reachable by performing a lightweight read through the
/// sidecar (so a downed sidecar also fails the probe). Generalizes the per-domain copies that domain modules
/// previously hand-wrote (Epic A5): the store name and probe key are configurable instead of hard-coded.
/// </summary>
internal sealed class DaprStateStoreHealthCheck : IHealthCheck {
    private readonly DaprClient _daprClient;
    private readonly string _stateStoreName;
    private readonly string _probeKey;

    /// <summary>Initializes a new instance of the <see cref="DaprStateStoreHealthCheck"/> class.</summary>
    /// <param name="daprClient">The DAPR client used to probe the store.</param>
    /// <param name="stateStoreName">The DAPR state-store component name to probe.</param>
    /// <param name="probeKey">The key read by the probe (its presence is irrelevant — only reachability matters).</param>
    public DaprStateStoreHealthCheck(DaprClient daprClient, string stateStoreName, string probeKey) {
        ArgumentNullException.ThrowIfNull(daprClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(stateStoreName);
        ArgumentException.ThrowIfNullOrWhiteSpace(probeKey);
        _daprClient = daprClient;
        _stateStoreName = stateStoreName;
        _probeKey = probeKey;
    }

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default) {
        try {
            _ = await _daprClient.GetStateAsync<string>(_stateStoreName, _probeKey, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return HealthCheckResult.Healthy();
        }
#pragma warning disable CA1031 // A health-check probe must treat any failure (sidecar down, network, store error) as Unhealthy.
        catch (Exception) {
#pragma warning restore CA1031
            return HealthCheckResult.Unhealthy($"DAPR state store '{_stateStoreName}' is unreachable");
        }
    }
}

/// <summary>
/// Registers the platform DAPR state-store health check for a domain module (Epic A5).
/// </summary>
public static class DaprStateStoreHealthCheckExtensions {
    /// <summary>
    /// Adds a convention-named DAPR state-store health check. Requires a registered <see cref="DaprClient"/>.
    /// </summary>
    /// <param name="builder">The health-checks builder.</param>
    /// <param name="domain">The kebab-case domain name, used to derive the registration name (e.g. <c>dapr-statestore-tenants</c>).</param>
    /// <param name="stateStoreName">The DAPR state-store component name to probe. Defaults to <c>"statestore"</c>.</param>
    /// <param name="probeKey">The probe key. Defaults to <c>"health-probe"</c>.</param>
    /// <param name="failureStatus">The status reported on failure. Defaults to <see cref="HealthStatus.Unhealthy"/>.</param>
    /// <param name="tags">Optional tags for filtering (e.g. <c>"ready"</c>).</param>
    /// <returns>The health-checks builder for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="domain"/> is <c>null</c> or whitespace.</exception>
    public static IHealthChecksBuilder AddEventStoreDomainStateStoreHealthCheck(
        this IHealthChecksBuilder builder,
        string domain,
        string stateStoreName = "statestore",
        string probeKey = "health-probe",
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null) {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);

        return builder.Add(new HealthCheckRegistration(
            EventStoreDomainTelemetry.StateStoreHealthCheckName(domain),
            sp => new DaprStateStoreHealthCheck(sp.GetRequiredService<DaprClient>(), stateStoreName, probeKey),
            failureStatus,
            tags));
    }
}
