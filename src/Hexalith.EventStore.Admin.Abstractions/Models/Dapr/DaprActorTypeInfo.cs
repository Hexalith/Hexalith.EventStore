namespace Hexalith.EventStore.Admin.Abstractions.Models.Dapr;

/// <summary>
/// Information about a registered DAPR actor type.
/// </summary>
/// <param name="TypeName">The actor type name as registered with DAPR.</param>
/// <param name="ActiveCount">The number of active actor instances (-1 if unknown).</param>
/// <param name="Description">A human-readable description of the actor type's purpose.</param>
/// <param name="ActorIdFormat">The expected format for actor IDs of this type.</param>
public record DaprActorTypeInfo(
    string TypeName,
    int ActiveCount,
    string Description,
    string ActorIdFormat) {
    /// <summary>Gets the actor type name as registered with DAPR.</summary>
    public string TypeName { get; } = !string.IsNullOrWhiteSpace(TypeName)
        ? TypeName
        : throw new ArgumentException("TypeName cannot be null, empty, or whitespace.", nameof(TypeName));

    /// <summary>Gets the number of active actor instances (-1 if unknown).</summary>
    public int ActiveCount { get; } = ActiveCount >= -1
        ? ActiveCount
        : throw new ArgumentOutOfRangeException(nameof(ActiveCount), ActiveCount, "ActiveCount must be >= -1.");

    /// <summary>Gets a human-readable description of the actor type's purpose.</summary>
    public string Description { get; } = !string.IsNullOrWhiteSpace(Description)
        ? Description
        : throw new ArgumentException("Description cannot be null, empty, or whitespace.", nameof(Description));

    /// <summary>Gets the expected format for actor IDs of this type.</summary>
    public string ActorIdFormat { get; } = !string.IsNullOrWhiteSpace(ActorIdFormat)
        ? ActorIdFormat
        : throw new ArgumentException("ActorIdFormat cannot be null, empty, or whitespace.", nameof(ActorIdFormat));
}
