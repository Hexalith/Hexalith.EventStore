namespace Hexalith.EventStore.Server.Events;

/// <summary>
/// Distinguishable outcomes of a snapshot load inspection.
/// </summary>
/// <remarks>
/// <para>
/// Added by DW16 (manual snapshot creation backend) so the actor-owned manual snapshot path can
/// distinguish an absent snapshot from an existing protected/provider-opaque/unreadable snapshot.
/// </para>
/// <para>
/// The command-time rehydration path treats <see cref="UnreadableProtected"/>, <see cref="ProviderOpaque"/>,
/// and <see cref="Corrupt"/> as "no snapshot present" (falling back to full replay through the
/// protected-data boundary). The manual snapshot overwrite path, by contrast, MUST fail closed on
/// those outcomes — silently overwriting an unreadable existing snapshot would lose recoverable
/// audit state.
/// </para>
/// </remarks>
public enum SnapshotLoadOutcome {
    /// <summary>No snapshot key exists; the stream may be replayed in full.</summary>
    Absent,

    /// <summary>A snapshot exists and was unprotected successfully (or is plaintext).</summary>
    Readable,

    /// <summary>A snapshot exists but the unprotection provider classified it as unreadable.</summary>
    UnreadableProtected,

    /// <summary>A snapshot exists with provider-opaque protection metadata (cannot be read here).</summary>
    ProviderOpaque,

    /// <summary>A snapshot exists but failed plaintext deserialization (would have been deleted by the command-time path).</summary>
    Corrupt,
}
