using Aspire.Hosting.ApplicationModel;

using CommunityToolkit.Aspire.Hosting.Dapr;

namespace Hexalith.EventStore.Aspire;

/// <summary>
/// Contains shared DAPR component builders a domain module can reference from its sidecar.
/// </summary>
public sealed class AspireDaprSharedComponents {
    /// <summary>
    /// Initializes a new instance of the <see cref="AspireDaprSharedComponents"/> class.
    /// </summary>
    /// <param name="stateStore">The shared DAPR state-store component.</param>
    /// <param name="pubSub">The shared DAPR pub/sub component.</param>
    public AspireDaprSharedComponents(
        IResourceBuilder<IDaprComponentResource> stateStore,
        IResourceBuilder<IDaprComponentResource> pubSub) {
        ArgumentNullException.ThrowIfNull(stateStore);
        ArgumentNullException.ThrowIfNull(pubSub);

        StateStore = stateStore;
        PubSub = pubSub;
    }

    /// <summary>
    /// Gets the shared DAPR state-store component.
    /// </summary>
    public IResourceBuilder<IDaprComponentResource> StateStore { get; }

    /// <summary>
    /// Gets the shared DAPR pub/sub component.
    /// </summary>
    public IResourceBuilder<IDaprComponentResource> PubSub { get; }
}
