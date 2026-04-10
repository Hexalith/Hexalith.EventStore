namespace Hexalith.EventStore.Admin.Abstractions.Models.Dapr;

/// <summary>
/// Aggregate runtime information about registered DAPR actors.
/// </summary>
/// <param name="ActorTypes">The list of registered actor types with their details.</param>
/// <param name="TotalActiveActors">The total number of active actor instances (excludes unknown counts).</param>
/// <param name="Configuration">The actor runtime configuration.</param>
/// <param name="RemoteMetadataStatus">
/// The outcome of the remote EventStore sidecar metadata query:
/// <see cref="RemoteMetadataStatus.NotConfigured"/> when no endpoint is configured,
/// <see cref="RemoteMetadataStatus.Available"/> when the call succeeded,
/// or <see cref="RemoteMetadataStatus.Unreachable"/> when the call failed with an exception.
/// </param>
/// <param name="RemoteEndpoint">
/// The attempted remote endpoint URL (from <c>AdminServer:EventStoreDaprHttpEndpoint</c>).
/// <c>null</c> only when <paramref name="RemoteMetadataStatus"/> is <see cref="RemoteMetadataStatus.NotConfigured"/>.
/// </param>
public record DaprActorRuntimeInfo(
    IReadOnlyList<DaprActorTypeInfo> ActorTypes,
    int TotalActiveActors,
    DaprActorRuntimeConfig Configuration,
    RemoteMetadataStatus RemoteMetadataStatus,
    string? RemoteEndpoint)
{
    /// <summary>Gets the list of registered actor types with their details.</summary>
    public IReadOnlyList<DaprActorTypeInfo> ActorTypes { get; } = ActorTypes
        ?? throw new ArgumentNullException(nameof(ActorTypes));

    /// <summary>Gets the actor runtime configuration.</summary>
    public DaprActorRuntimeConfig Configuration { get; } = Configuration
        ?? throw new ArgumentNullException(nameof(Configuration));
}
