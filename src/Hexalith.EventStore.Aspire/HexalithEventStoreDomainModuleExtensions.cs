using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Hexalith.EventStore.Aspire;

/// <summary>
/// Provides the Aspire hosting extension that wires a Hexalith.EventStore <b>domain module</b> (a domain
/// service such as the Counter sample or the Tenants service) with its DAPR sidecar. A domain module no longer
/// ships its own <c>*.AppHost</c>/<c>*.Aspire</c> project to hand-roll this wiring — the EventStore AppHost
/// adds the module's project and calls <see cref="AddEventStoreDomainModule"/>, which attaches a correctly
/// configured DAPR sidecar (Epic A4).
/// </summary>
public static class HexalithEventStoreDomainModuleExtensions {
    private const string DaprAppHealthCheckPath = "/alive";

    /// <summary>
    /// Attaches a DAPR sidecar to a domain-module project so it can run on the Hexalith.EventStore platform.
    /// </summary>
    /// <param name="domainModule">The domain-module project resource builder (from <c>builder.AddProject&lt;T&gt;(name)</c>).</param>
    /// <param name="eventStore">
    /// The EventStore topology resources returned by <c>AddHexalithEventStore</c>.
    /// Its state-store and pub/sub components are referenced when the module is not isolated.
    /// </param>
    /// <param name="appId">The DAPR application id for the module's sidecar (e.g. <c>"tenants"</c>, <c>"sample"</c>).</param>
    /// <param name="daprConfigPath">
    /// Optional path to the module's DAPR access-control (Configuration CRD) YAML governing inbound invocations
    /// to this module. <c>null</c> leaves the sidecar on the DAPR default configuration.
    /// </param>
    /// <param name="isolatedDaprResourcesPath">
    /// Optional path to an empty/isolated DAPR resources directory. When supplied, the sidecar loads <b>only</b>
    /// that directory and the module gets <b>zero infrastructure access</b> — it does not reference the
    /// EventStore state-store or pub/sub components at all (the strongest isolation, used by pure
    /// service-invocation domains like the Counter sample). When <c>null</c>, the module references the shared
    /// EventStore state-store and pub/sub components (used by domains that persist read models / subscribe to
    /// events, like Tenants).
    /// </param>
    /// <param name="daprPlacementHostAddress">
    /// Optional DAPR placement service address, formatted as <c>host</c> or <c>host:port</c>.
    /// Leave <c>null</c> to use the DAPR CLI default.
    /// </param>
    /// <param name="daprSchedulerHostAddress">
    /// Optional DAPR scheduler service address, formatted as <c>host</c> or <c>host:port</c>.
    /// Leave <c>null</c> to use the DAPR CLI default.
    /// </param>
    /// <returns>The same domain-module resource builder for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="domainModule"/> or <paramref name="eventStore"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="appId"/> is <c>null</c> or whitespace.</exception>
    public static IResourceBuilder<ProjectResource> AddEventStoreDomainModule(
        this IResourceBuilder<ProjectResource> domainModule,
        HexalithEventStoreResources eventStore,
        string appId,
        string? daprConfigPath = null,
        string? isolatedDaprResourcesPath = null,
        string? daprPlacementHostAddress = null,
        string? daprSchedulerHostAddress = null) {
        ArgumentNullException.ThrowIfNull(domainModule);
        ArgumentNullException.ThrowIfNull(eventStore);
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);

        bool isolated = !string.IsNullOrWhiteSpace(isolatedDaprResourcesPath);

        // DAPR app health gates service invocation/pub-sub traffic into the app. Use liveness here, not
        // readiness, so sidecar-dependent readiness checks cannot feed back into DAPR traffic eligibility.
        return domainModule.AddAspireDaprDomainModule(new AspireDaprDomainModuleOptions(
            appId,
            isolated ? AspireDaprInfrastructureMode.Isolated : AspireDaprInfrastructureMode.Shared) {
            Config = daprConfigPath,
            ResourcesPaths = isolated ? [isolatedDaprResourcesPath!] : [],
            SharedComponents = isolated ? null : new AspireDaprSharedComponents(eventStore.StateStore, eventStore.PubSub),
            EnableAppHealthCheck = true,
            AppHealthCheckPath = DaprAppHealthCheckPath,
            PlacementHostAddress = daprPlacementHostAddress,
            SchedulerHostAddress = daprSchedulerHostAddress,
        }).Project;
    }
}
