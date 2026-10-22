namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Current key-delivery observation. A restored snapshot is not a current root, and a required missing tombstone denies release.</summary>
/// <param name="CurrentRoot">Whether this result is the current root rather than a restored copy.</param>
/// <param name="TombstoneRequired">Whether an independent tombstone must already exist.</param>
/// <param name="TombstonePresent">Whether that tombstone was present.</param>
/// <param name="KeyBytes">Candidate 32-byte material. The adapter copies it only when the result is current and complete.</param>
public sealed record InteractionOccurrenceKeyDeliveryResult(
    bool CurrentRoot,
    bool TombstoneRequired,
    bool TombstonePresent,
    byte[]? KeyBytes);
