namespace Hexalith.EventStore.Contracts.Security;

/// <summary>
/// Story 22.7c — state machine for restored-backup admission decisions. Allowed transitions are
/// declared in <see cref="RestoredBackupAdmissionTransitions"/>.
/// </summary>
public enum RestoredBackupAdmissionState {
    /// <summary>Admission has been requested but not yet decided.</summary>
    Pending = 0,

    /// <summary>The restored backup has been accepted by an explicit operator decision. Terminal.</summary>
    Accepted = 1,

    /// <summary>The restored backup conflicts with an irreversible workflow; reading is blocked. Terminal.</summary>
    Blocked = 2,

    /// <summary>The restored data has been quarantined; explicit close required.</summary>
    Quarantined = 3,

    /// <summary>An explicit operator decision is required to make progress.</summary>
    OperatorDecisionRequired = 4,

    /// <summary>
    /// Admission cannot be proved with current evidence; transient unreadable state until
    /// additional evidence is provided.
    /// </summary>
    DeferredValidation = 5,
}
