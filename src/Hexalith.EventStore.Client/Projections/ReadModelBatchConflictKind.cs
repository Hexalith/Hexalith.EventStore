namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// Distinguishes the two conflict causes reported by <see cref="ReadModelBatchStatus.Conflict"/>.
/// </summary>
public enum ReadModelBatchConflictKind {
    /// <summary>Not a conflict.</summary>
    None,

    /// <summary>
    /// The batch identity was reused with a different canonical fingerprint; no new logical mutation ran.
    /// </summary>
    Identity,

    /// <summary>An expected ETag did not match the current stored value; the batch was compensated.</summary>
    Optimistic,
}
