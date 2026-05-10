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
/// <param name="IsInventoryComplete">Whether all known EventStore actor types have exact counts from an authoritative source.</param>
/// <param name="InventorySource">The source used for actor inventory evidence.</param>
/// <param name="InventoryMessage">Operator-facing explanation of inventory completeness.</param>
/// <param name="ObservedAtUtc">The UTC time when the inventory evidence was observed.</param>
/// <param name="TotalKnownTypes">The bounded count of known EventStore actor types considered by this payload.</param>
public record DaprActorRuntimeInfo(
    IReadOnlyList<DaprActorTypeInfo> ActorTypes,
    int TotalActiveActors,
    DaprActorRuntimeConfig Configuration,
    RemoteMetadataStatus RemoteMetadataStatus,
    string? RemoteEndpoint,
    bool IsInventoryComplete = true,
    string InventorySource = "DaprMetadata",
    string? InventoryMessage = null,
    DateTimeOffset? ObservedAtUtc = null,
    int TotalKnownTypes = 0) {
    /// <summary>Gets the list of registered actor types with their details.</summary>
    public IReadOnlyList<DaprActorTypeInfo> ActorTypes { get; } = ActorTypes
        ?? throw new ArgumentNullException(nameof(ActorTypes));

    /// <summary>Gets the actor runtime configuration.</summary>
    public DaprActorRuntimeConfig Configuration { get; } = Configuration
        ?? throw new ArgumentNullException(nameof(Configuration));

    /// <summary>Gets the source used for actor inventory evidence.</summary>
    public string InventorySource { get; init; } = !string.IsNullOrWhiteSpace(InventorySource)
        ? InventorySource
        : throw new ArgumentException("InventorySource cannot be null, empty, or whitespace.", nameof(InventorySource));
}
