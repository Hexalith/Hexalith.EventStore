using System.Collections.Immutable;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

using CommunityToolkit.Aspire.Hosting.Dapr;

namespace Hexalith.EventStore.Aspire;

/// <summary>
/// Provides the Aspire hosting extension that wires a Hexalith.EventStore <b>domain module</b> (a domain
/// service such as the Counter sample or the Tenants service) with its DAPR sidecar. A domain module no longer
/// ships its own <c>*.AppHost</c>/<c>*.Aspire</c> project to hand-roll this wiring — the EventStore AppHost
/// adds the module's project and calls <see cref="AddEventStoreDomainModule"/>, which attaches a correctly
/// configured DAPR sidecar (Epic A4).
/// </summary>
public static class HexalithEventStoreDomainModuleExtensions {
    /// <summary>
    /// Attaches a DAPR sidecar to a domain-module project so it can run on the Hexalith.EventStore platform.
    /// </summary>
    /// <param name="domainModule">The domain-module project resource builder (from <c>builder.AddProject&lt;T&gt;(name)</c>).</param>
    /// <param name="eventStore">
    /// The EventStore topology resources returned by
    /// <see cref="HexalithEventStoreExtensions.AddHexalithEventStore(IDistributedApplicationBuilder, IResourceBuilder{ProjectResource}, IResourceBuilder{ProjectResource}, IResourceBuilder{ProjectResource}?, string?, string?, string?, int)"/>.
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
    /// <returns>The same domain-module resource builder for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="domainModule"/> or <paramref name="eventStore"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="appId"/> is <c>null</c> or whitespace.</exception>
    public static IResourceBuilder<ProjectResource> AddEventStoreDomainModule(
        this IResourceBuilder<ProjectResource> domainModule,
        HexalithEventStoreResources eventStore,
        string appId,
        string? daprConfigPath = null,
        string? isolatedDaprResourcesPath = null) {
        ArgumentNullException.ThrowIfNull(domainModule);
        ArgumentNullException.ThrowIfNull(eventStore);
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);

        bool isolated = !string.IsNullOrWhiteSpace(isolatedDaprResourcesPath);

        return domainModule.WithDaprSidecar(sidecar => {
            if (isolated) {
                // Zero infrastructure access: load only the empty resources directory so the sidecar does not
                // bind the EventStore state-store / pub/sub components at all. Stronger than scoping alone —
                // direct component access is impossible. AppPort is intentionally omitted so the CommunityToolkit
                // auto-detects the module's port (hardcoding it breaks Aspire Testing's randomized ports).
                _ = sidecar.WithOptions(new DaprSidecarOptions {
                    AppId = appId,
                    Config = daprConfigPath,
                    ResourcesPaths = ImmutableHashSet.Create(isolatedDaprResourcesPath!),
                });
            }
            else {
                // Shared infrastructure: the module reads/writes the same state store and publishes/subscribes
                // on the same pub/sub as EventStore (keyPrefix:none shares keys across app-ids).
                _ = sidecar
                    .WithOptions(new DaprSidecarOptions {
                        AppId = appId,
                        Config = daprConfigPath,
                    })
                    .WithReference(eventStore.StateStore)
                    .WithReference(eventStore.PubSub);
            }
        });
    }
}
