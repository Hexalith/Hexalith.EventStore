namespace Hexalith.EventStore.Server.Events;

/// <summary>
/// Result of <see cref="ISnapshotManager.InspectSnapshotForManualOverwriteAsync"/>.
/// </summary>
/// <param name="Outcome">Classification of the snapshot load.</param>
/// <param name="Snapshot">The readable snapshot record when <see cref="SnapshotLoadOutcome.Readable"/>; otherwise null.</param>
/// <param name="ReasonCode">A safe, stable reason code when the outcome is non-readable; otherwise null.</param>
public sealed record SnapshotLoadResult(
    SnapshotLoadOutcome Outcome,
    SnapshotRecord? Snapshot,
    string? ReasonCode) {
    /// <summary>Convenience: a Readable result with the supplied snapshot.</summary>
    public static SnapshotLoadResult Readable(SnapshotRecord snapshot)
        => new(SnapshotLoadOutcome.Readable, snapshot, null);

    /// <summary>Convenience: an Absent result.</summary>
    public static SnapshotLoadResult Absent() => new(SnapshotLoadOutcome.Absent, null, null);

    /// <summary>Convenience: an UnreadableProtected result with a safe reason code.</summary>
    public static SnapshotLoadResult UnreadableProtected(string reasonCode)
        => new(SnapshotLoadOutcome.UnreadableProtected, null, reasonCode);

    /// <summary>Convenience: a ProviderOpaque result with a safe reason code.</summary>
    public static SnapshotLoadResult ProviderOpaque(string reasonCode)
        => new(SnapshotLoadOutcome.ProviderOpaque, null, reasonCode);

    /// <summary>Convenience: a Corrupt result with a safe reason code.</summary>
    public static SnapshotLoadResult Corrupt(string reasonCode)
        => new(SnapshotLoadOutcome.Corrupt, null, reasonCode);
}
