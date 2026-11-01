namespace Hexalith.EventStore.Client.Projections.Checkpointed;

/// <summary>
/// The content-free reason a checkpointed projection became terminally corrupt.
/// </summary>
public enum CheckpointedProjectionCorruptionReason {
    /// <summary>A delivery at the accepted checkpoint carried a different identity or payload.</summary>
    AcceptedIdentityMismatch = 1,

    /// <summary>An older delivery did not match authoritative history at its sequence.</summary>
    HistoryIdentityMismatch = 2,

    /// <summary>Authoritative history had no event at a sequence the projection depends on.</summary>
    HistoryMissing = 3,

    /// <summary>Two deliveries offered conflicting evidence at one pending sequence.</summary>
    PendingEvidenceConflict = 4,

    /// <summary>Recovery found pending evidence that contradicts authoritative history.</summary>
    RecoveryEvidenceMismatch = 5,
}
