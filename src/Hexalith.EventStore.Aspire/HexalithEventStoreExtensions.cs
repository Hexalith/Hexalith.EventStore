using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

using CommunityToolkit.Aspire.Hosting.Dapr;

namespace Hexalith.EventStore.Aspire;

/// <summary>
/// Provides extension methods for adding the Hexalith EventStore topology
/// to an Aspire distributed application.
/// </summary>
public static class HexalithEventStoreExtensions {
    /// <summary>
    /// Adds the Hexalith EventStore topology to the distributed application builder.
    /// This configures DAPR state store (Redis-backed with actor support), DAPR pub/sub,
    /// and wires the EventStore and Admin.Server services with DAPR sidecars.
    /// Redis is provided externally by <c>dapr init</c> at localhost:6379.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="eventStore">The EventStore project resource builder.</param>
    /// <param name="adminServer">The Admin.Server.Host project resource builder.</param>
    /// <param name="adminUI">Optional Admin.UI project resource builder.</param>
    /// <param name="eventStoreDaprConfigPath">
    /// Path to the Dapr access control configuration file loaded by the EventStore sidecar.
    /// This config governs incoming invocations to EventStore.
    /// </param>
    /// <param name="adminServerDaprConfigPath">
    /// Path to the Dapr access control configuration file loaded by the Admin.Server sidecar.
    /// This config governs incoming invocations to Admin.Server.
    /// </param>
    /// <returns>A <see cref="HexalithEventStoreResources"/> containing the resource builders for further customization.</returns>
    public static HexalithEventStoreResources AddHexalithEventStore(
        this IDistributedApplicationBuilder builder,
        IResourceBuilder<ProjectResource> eventStore,
        IResourceBuilder<ProjectResource> adminServer,
        IResourceBuilder<ProjectResource>? adminUI = null,
        string? eventStoreDaprConfigPath = null,
        string? adminServerDaprConfigPath = null) {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(eventStore);
        ArgumentNullException.ThrowIfNull(adminServer);

        // Redis is provided by `dapr init` at localhost:6379, not managed by Aspire.
        // The dapr init container runs independently of the Aspire lifecycle, so state
        // survives AppHost restarts naturally. DAPR component YAMLs default to
        // localhost:6379 via {env:REDIS_HOST|localhost:6379}.

        // Use AddDaprComponent instead of AddDaprStateStore so that WithMetadata
        // actually propagates into the generated YAML. AddDaprStateStore spawns a
        // separate in-memory provider process whose lifecycle hook ignores metadata.
        // actorStateStore is required for Dapr actor state management.
        IResourceBuilder<IDaprComponentResource> stateStore = builder
            .AddDaprComponent("statestore", "state.redis")
            .WithMetadata("actorStateStore", "true")
            .WithMetadata("redisHost", "localhost:6379")
            .WithMetadata("keyPrefix", "none");
        IResourceBuilder<IDaprComponentResource> pubSub = builder.AddDaprPubSub("pubsub");

        // Wire up EventStore with DAPR sidecar and component references.
        // AppPort is intentionally omitted so the CommunityToolkit auto-detects
        // the app's actual port from the Aspire resource model. Hardcoding AppPort
        // breaks Aspire Testing, which randomizes project ports.
        // DaprHttpPort is fixed (3501) so Admin.Server can query the EventStore
        // sidecar's metadata endpoint for actor type discovery (story 19-2).
        // IDaprSidecarResource does not implement IResourceWithEndpoints in
        // CommunityToolkit 13.0.0, so a dynamic endpoint reference is not possible.
        const int EventStoreDaprHttpPort = 3501;
        _ = eventStore
            .WithDaprSidecar(sidecar => sidecar
                .WithOptions(new DaprSidecarOptions {
                    AppId = "eventstore",
                    DaprHttpPort = EventStoreDaprHttpPort,
                    Config = eventStoreDaprConfigPath,
                })
                .WithReference(stateStore)
                .WithReference(pubSub));

        // Wire Admin.Server with DAPR sidecar.
        // Admin.Server needs state store for direct reads (health, admin indexes,
        // tenant projections written by the tenants service).
        // All services use keyPrefix:none so keys are shared across appIds.
        // It does not publish or subscribe directly, so it intentionally does not
        // reference the pub/sub component.
        _ = adminServer
            .WithReference(eventStore)
            .WithEnvironment("AdminServer__EventStoreDaprHttpEndpoint", "http://localhost:" + EventStoreDaprHttpPort)
            .WithDaprSidecar(sidecar => sidecar
                .WithOptions(new DaprSidecarOptions {
                    AppId = "eventstore-admin",
                    Config = adminServerDaprConfigPath,
                })
                .WithReference(stateStore));

        // Wire Admin.UI with Admin.Server reference for HTTP API calls.
        // Admin.UI does not use DAPR directly — it communicates exclusively via
        // HTTP REST API to Admin.Server (ADR-P4 deviation).
        if (adminUI is not null) {
            _ = adminUI
                .WithReference(adminServer)
                .WaitFor(adminServer)
                .WithExternalHttpEndpoints();
        }

        return new HexalithEventStoreResources(stateStore, pubSub, eventStore, adminServer, adminUI);
    }

}
