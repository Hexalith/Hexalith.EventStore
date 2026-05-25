
using Dapr.Client;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Hexalith.EventStore.HealthChecks;
/// <summary>
/// Extension methods for registering DAPR health checks.
/// </summary>
public static class HealthCheckBuilderExtensions {
    /// <summary>
    /// Registers all DAPR infrastructure health checks for the EventStore EventStore.
    /// </summary>
    public static IHealthChecksBuilder AddEventStoreDaprHealthChecks(
        this IHealthChecksBuilder builder,
        string stateStoreName = "statestore",
        string pubSubName = "pubsub",
        string configStoreName = "configstore") {
        ArgumentNullException.ThrowIfNull(builder);

        // The placement probe reads the local sidecar's raw /v1.0/metadata over HTTP — the typed
        // DaprClient metadata does not expose actorRuntime.hostReady/placement. Endpoint is resolved
        // from the sidecar-injected DAPR_HTTP_ENDPOINT/DAPR_HTTP_PORT env vars inside the probe.
        _ = builder.Services.AddHttpClient<IDaprActorPlacementProbe, DaprActorPlacementProbe>();

        var healthCheckTimeout = TimeSpan.FromSeconds(3);
        _ = builder
            .AddCheck<DaprSidecarHealthCheck>(
                "dapr-sidecar",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready"],
                timeout: healthCheckTimeout)
            .Add(new HealthCheckRegistration(
                "dapr-actor-placement",
                sp => new DaprActorPlacementHealthCheck(
                    sp.GetRequiredService<IDaprActorPlacementProbe>()),
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready"],
                timeout: healthCheckTimeout))
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
