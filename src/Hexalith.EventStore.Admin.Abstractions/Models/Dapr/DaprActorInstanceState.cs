namespace Hexalith.EventStore.Admin.Abstractions.Models.Dapr;

/// <summary>
/// The inspected state of a specific DAPR actor instance.
/// </summary>
/// <param name="ActorType">The actor type name.</param>
/// <param name="ActorId">The actor instance ID.</param>
/// <param name="StateEntries">The state entries for this actor instance.</param>
/// <param name="TotalSizeBytes">The total estimated size in bytes across all state entries.</param>
/// <param name="InspectedAtUtc">The UTC timestamp when the inspection was performed.</param>
/// <param name="LookupStatus">The classified lookup result.</param>
/// <param name="OwnerAppId">The DAPR app id used to compose actor state keys.</param>
/// <param name="StateStoreName">The DAPR state store component queried.</param>
/// <param name="LookupSource">The source/path used to inspect actor state.</param>
/// <param name="Message">Operator-facing lookup detail.</param>
public record DaprActorInstanceState(
    string ActorType,
    string ActorId,
    IReadOnlyList<DaprActorStateEntry> StateEntries,
    long TotalSizeBytes,
    DateTimeOffset InspectedAtUtc,
    DaprActorLookupStatus LookupStatus = DaprActorLookupStatus.Available,
    string OwnerAppId = "eventstore",
    string StateStoreName = "statestore",
    string LookupSource = "DaprStateStoreActorKeys",
    string? Message = null) {
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

    /// <summary>Gets the DAPR app id used to compose actor state keys.</summary>
    public string OwnerAppId { get; init; } = !string.IsNullOrWhiteSpace(OwnerAppId)
        ? OwnerAppId
        : throw new ArgumentException("OwnerAppId cannot be null, empty, or whitespace.", nameof(OwnerAppId));

    /// <summary>Gets the DAPR state store component queried.</summary>
    public string StateStoreName { get; init; } = !string.IsNullOrWhiteSpace(StateStoreName)
        ? StateStoreName
        : throw new ArgumentException("StateStoreName cannot be null, empty, or whitespace.", nameof(StateStoreName));

    /// <summary>Gets the source/path used to inspect actor state.</summary>
    public string LookupSource { get; init; } = !string.IsNullOrWhiteSpace(LookupSource)
        ? LookupSource
        : throw new ArgumentException("LookupSource cannot be null, empty, or whitespace.", nameof(LookupSource));
}
