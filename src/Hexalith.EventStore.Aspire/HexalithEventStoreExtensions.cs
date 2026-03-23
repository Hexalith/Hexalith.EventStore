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
    /// This provisions DAPR state store (in-memory with actor support), DAPR pub/sub,
    /// and wires the CommandApi and Admin.Server services with DAPR sidecars.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="commandApi">The CommandApi project resource builder.</param>
    /// <param name="adminServer">The Admin.Server.Host project resource builder.</param>
    /// <param name="commandApiDaprConfigPath">
    /// Path to the Dapr access control configuration file loaded by the CommandApi sidecar.
    /// This config governs incoming invocations to CommandApi.
    /// </param>
    /// <param name="adminServerDaprConfigPath">
    /// Path to the Dapr access control configuration file loaded by the Admin.Server sidecar.
    /// This config governs incoming invocations to Admin.Server.
    /// </param>
    /// <returns>A <see cref="HexalithEventStoreResources"/> containing the resource builders for further customization.</returns>
    public static HexalithEventStoreResources AddHexalithEventStore(
        this IDistributedApplicationBuilder builder,
        IResourceBuilder<ProjectResource> commandApi,
        IResourceBuilder<ProjectResource> adminServer,
        IResourceBuilder<ProjectResource>? adminUI = null,
        string? commandApiDaprConfigPath = null,
        string? adminServerDaprConfigPath = null) {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(commandApi);
        ArgumentNullException.ThrowIfNull(adminServer);

        // Use AddDaprComponent instead of AddDaprStateStore so that WithMetadata
        // actually propagates into the generated YAML. AddDaprStateStore spawns a
        // separate in-memory provider process whose lifecycle hook ignores metadata.
        // actorStateStore is required for Dapr actor state management.
        IResourceBuilder<IDaprComponentResource> stateStore = builder
            .AddDaprComponent("statestore", "state.in-memory")
            .WithMetadata("actorStateStore", "true");
        IResourceBuilder<IDaprComponentResource> pubSub = builder.AddDaprPubSub("pubsub");

        // Wire up CommandApi with DAPR sidecar and component references.
        // AppPort is intentionally omitted so the CommunityToolkit auto-detects
        // the app's actual port from the Aspire resource model. Hardcoding AppPort
        // breaks Aspire Testing, which randomizes project ports.
        _ = commandApi
            .WithDaprSidecar(sidecar => sidecar
                .WithOptions(new DaprSidecarOptions {
                    AppId = "commandapi",
                    Config = commandApiDaprConfigPath,
                })
                .WithReference(stateStore)
                .WithReference(pubSub));

        // Wire Admin.Server with DAPR sidecar.
        // Admin.Server needs state store for direct reads (health, admin indexes)
        // and service invocation to CommandApi for write delegation (ADR-P4).
        // It does not publish or subscribe directly, so it intentionally does not
        // reference the pub/sub component.
        _ = adminServer
            .WithReference(commandApi)
            .WithDaprSidecar(sidecar => sidecar
                .WithOptions(new DaprSidecarOptions {
                    AppId = "admin-server",
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

        return new HexalithEventStoreResources(stateStore, pubSub, commandApi, adminServer, adminUI);
    }
}
