
using Dapr.Client;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Hexalith.EventStore.CommandApi.HealthChecks;
/// <summary>
/// Extension methods for registering DAPR health checks.
/// </summary>
public static class HealthCheckBuilderExtensions {
    /// <summary>
    /// Registers all DAPR infrastructure health checks for the EventStore CommandApi.
    /// </summary>
    public static IHealthChecksBuilder AddEventStoreDaprHealthChecks(
        this IHealthChecksBuilder builder,
        string stateStoreName = "statestore",
        string pubSubName = "pubsub",
        string configStoreName = "configstore") {
        ArgumentNullException.ThrowIfNull(builder);

        // Use 15s timeout so E2E/CI (Aspire Testing, Docker) have time for sidecar and
        // Dapr infrastructure to become ready; healthy sidecar still responds in milliseconds.
        var healthCheckTimeout = TimeSpan.FromSeconds(15);
        _ = builder
            .AddCheck<DaprSidecarHealthCheck>(
                "dapr-sidecar",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready"],
                timeout: healthCheckTimeout)
            .Add(new HealthCheckRegistration(
                "dapr-statestore",
                sp => new DaprStateStoreHealthCheck(
                    sp.GetRequiredService<DaprClient>(), stateStoreName),
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready"],
                timeout: healthCheckTimeout))
            .Add(new HealthCheckRegistration(
                "dapr-pubsub",
                sp => new DaprPubSubHealthCheck(
                    sp.GetRequiredService<DaprClient>(), pubSubName),
                failureStatus: HealthStatus.Degraded,
                tags: ["ready"],
                timeout: healthCheckTimeout))
            .Add(new HealthCheckRegistration(
                "dapr-configstore",
                sp => new DaprConfigStoreHealthCheck(
                    sp.GetRequiredService<DaprClient>(), configStoreName),
                failureStatus: HealthStatus.Degraded,
                tags: ["ready"],
                timeout: healthCheckTimeout));

        return builder;
    }
}
