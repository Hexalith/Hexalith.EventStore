using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

using CommunityToolkit.Aspire.Hosting.Dapr;

namespace Hexalith.EventStore.Aspire;

/// <summary>
/// Provides extension methods for adding the Hexalith EventStore topology
/// to an Aspire distributed application.
/// </summary>
public static class HexalithEventStoreExtensions
{
    /// <summary>
    /// Adds the Hexalith EventStore topology to the distributed application builder.
    /// This provisions Redis, DAPR state store, DAPR pub/sub, and wires the CommandApi service
    /// with a DAPR sidecar, reducing boilerplate for consumers.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="commandApi">The CommandApi project resource builder.</param>
    /// <param name="daprConfigPath">
    /// Path to the Dapr sidecar configuration file (access control policies, D4/FR34).
    /// When null, the sidecar starts without access control -- all service-to-service
    /// calls are allowed. This is a security risk in production environments.
    /// </param>
    /// <returns>A <see cref="HexalithEventStoreResources"/> containing the resource builders for further customization.</returns>
    public static HexalithEventStoreResources AddHexalithEventStore(
        this IDistributedApplicationBuilder builder,
        IResourceBuilder<ProjectResource> commandApi,
        string? daprConfigPath = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(commandApi);

        // Infrastructure
        var redis = builder.AddRedis("redis");

        // DAPR components backed by Redis
        var stateStore = builder.AddDaprStateStore("statestore");
        var pubSub = builder.AddDaprPubSub("pubsub");

        // Wire up CommandApi with DAPR sidecar and component references
        commandApi
            .WithReference(redis)
            .WaitFor(redis)
            .WithDaprSidecar(sidecar => sidecar
                .WithOptions(new DaprSidecarOptions
                {
                    AppId = "commandapi",
                    AppPort = 8080,
                    Config = daprConfigPath,
                })
                .WithReference(stateStore)
                .WithReference(pubSub));

        return new HexalithEventStoreResources(redis, stateStore, pubSub, commandApi);
    }
}
