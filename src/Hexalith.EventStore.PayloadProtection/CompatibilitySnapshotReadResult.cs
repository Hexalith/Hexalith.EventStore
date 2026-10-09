// Normative authority: de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e; sections 8.4, 12.3, 13.2, and 15.
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Represents one routed snapshot: readable state or one bounded unreadable reason, never both (normative section 12.3).
/// </summary>
/// <param name="Route">The route that produced the decision, or <see cref="CompatibilityReadRoute.Rejected"/>.</param>
/// <param name="State">
/// The readable state, or <see langword="null"/>. Pass-through routes return the stored state unchanged; the
/// registered v1 route returns a <see cref="System.Text.Json.JsonElement"/>; the shared v2 route returns the object
/// deserialized through the registered <see cref="System.Text.Json.Serialization.Metadata.JsonTypeInfo"/>.
/// </param>
/// <param name="Metadata">The readable metadata, or <see langword="null"/> for an unreadable result.</param>
/// <param name="UnreadableReason">The bounded unreadable reason, or <see langword="null"/>.</param>
internal sealed record CompatibilitySnapshotReadResult(
    CompatibilityReadRoute Route,
    object? State,
    EventStorePayloadProtectionMetadata? Metadata,
    UnreadableProtectedDataReason? UnreadableReason)
{
    /// <summary>Gets a value indicating whether readable state is available.</summary>
    internal bool IsReadable => UnreadableReason is null && State is not null;

    /// <summary>
    /// Gets a value indicating whether the caller may still apply the existing corrupt-snapshot deletion exception.
    /// </summary>
    /// <remarks>
    /// Only readable legacy or unprotected pass-through state that later fails the caller's own deserialization may
    /// be deleted. Every unreadable result and every protected-route snapshot must be retained in storage.
    /// </remarks>
    internal bool AllowsCorruptLegacyDeletion
        => IsReadable && Route is CompatibilityReadRoute.LegacyUnprotected or CompatibilityReadRoute.Unprotected;

    /// <summary>Creates a readable snapshot result.</summary>
    internal static CompatibilitySnapshotReadResult Readable(
        CompatibilityReadRoute route,
        object state,
        EventStorePayloadProtectionMetadata metadata)
        => new(route, state, metadata, null);

    /// <summary>Creates an unreadable snapshot result that returns no state and requires retention.</summary>
    internal static CompatibilitySnapshotReadResult Unreadable(
        CompatibilityReadRoute route,
        UnreadableProtectedDataReason reason)
        => new(route, null, null, reason);

    /// <inheritdoc/>
    public override string ToString() => nameof(CompatibilitySnapshotReadResult);
}
