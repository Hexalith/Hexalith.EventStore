namespace Hexalith.EventStore.Admin.Abstractions.Models.Dapr;

/// <summary>
/// Aggregate runtime information about registered DAPR actors.
/// </summary>
/// <param name="ActorTypes">The list of registered actor types with their details.</param>
/// <param name="TotalActiveActors">The total number of active actor instances (excludes unknown counts).</param>
/// <param name="Configuration">The actor runtime configuration.</param>
/// <param name="IsRemoteMetadataAvailable">Whether actor metadata was retrieved successfully.</param>
public record DaprActorRuntimeInfo(
    IReadOnlyList<DaprActorTypeInfo> ActorTypes,
    int TotalActiveActors,
    DaprActorRuntimeConfig Configuration,
    bool IsRemoteMetadataAvailable)
{
    /// <summary>Gets the list of registered actor types with their details.</summary>
    public IReadOnlyList<DaprActorTypeInfo> ActorTypes { get; } = ActorTypes
        ?? throw new ArgumentNullException(nameof(ActorTypes));

    /// <summary>Gets the actor runtime configuration.</summary>
    public DaprActorRuntimeConfig Configuration { get; } = Configuration
        ?? throw new ArgumentNullException(nameof(Configuration));
}
