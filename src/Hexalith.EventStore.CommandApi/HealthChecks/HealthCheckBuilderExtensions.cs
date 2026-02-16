namespace Microsoft.Extensions.DependencyInjection;

using Dapr.Client;

using Hexalith.EventStore.CommandApi.HealthChecks;

using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Extension methods for registering DAPR health checks.
/// </summary>
public static class HealthCheckBuilderExtensions
{
    /// <summary>
    /// Registers all DAPR infrastructure health checks for the EventStore CommandApi.
    /// </summary>
    public static IHealthChecksBuilder AddEventStoreDaprHealthChecks(
        this IHealthChecksBuilder builder,
        string stateStoreName = "statestore",
        string pubSubName = "pubsub",
        string configStoreName = "configstore")
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder
            .AddCheck<DaprSidecarHealthCheck>(
                "dapr-sidecar",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready"],
                timeout: TimeSpan.FromSeconds(3))
            .Add(new HealthCheckRegistration(
                "dapr-statestore",
                sp => new DaprStateStoreHealthCheck(
                    sp.GetRequiredService<DaprClient>(), stateStoreName),
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready"],
                timeout: TimeSpan.FromSeconds(3)))
            .Add(new HealthCheckRegistration(
                "dapr-pubsub",
                sp => new DaprPubSubHealthCheck(
                    sp.GetRequiredService<DaprClient>(), pubSubName),
                failureStatus: HealthStatus.Degraded,
                tags: ["ready"],
                timeout: TimeSpan.FromSeconds(3)))
            .Add(new HealthCheckRegistration(
                "dapr-configstore",
                sp => new DaprConfigStoreHealthCheck(
                    sp.GetRequiredService<DaprClient>(), configStoreName),
                failureStatus: HealthStatus.Degraded,
                tags: ["ready"],
                timeout: TimeSpan.FromSeconds(3)));

        return builder;
    }
}
