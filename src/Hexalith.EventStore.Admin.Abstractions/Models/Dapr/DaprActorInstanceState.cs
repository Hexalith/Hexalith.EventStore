namespace Hexalith.EventStore.Admin.Abstractions.Models.Dapr;

/// <summary>
/// The inspected state of a specific DAPR actor instance.
/// </summary>
/// <param name="ActorType">The actor type name.</param>
/// <param name="ActorId">The actor instance ID.</param>
/// <param name="StateEntries">The state entries for this actor instance.</param>
/// <param name="TotalSizeBytes">The total estimated size in bytes across all state entries.</param>
/// <param name="InspectedAtUtc">The UTC timestamp when the inspection was performed.</param>
public record DaprActorInstanceState(
    string ActorType,
    string ActorId,
    IReadOnlyList<DaprActorStateEntry> StateEntries,
    long TotalSizeBytes,
    DateTimeOffset InspectedAtUtc)
{
    /// <summary>Gets the actor type name.</summary>
    public string ActorType { get; } = !string.IsNullOrWhiteSpace(ActorType)
        ? ActorType
        : throw new ArgumentException("ActorType cannot be null, empty, or whitespace.", nameof(ActorType));

    /// <summary>Gets the actor instance ID.</summary>
    public string ActorId { get; } = !string.IsNullOrWhiteSpace(ActorId)
        ? ActorId
        : throw new ArgumentException("ActorId cannot be null, empty, or whitespace.", nameof(ActorId));

    /// <summary>Gets the state entries for this actor instance.</summary>
    public IReadOnlyList<DaprActorStateEntry> StateEntries { get; } = StateEntries
        ?? throw new ArgumentNullException(nameof(StateEntries));
}
