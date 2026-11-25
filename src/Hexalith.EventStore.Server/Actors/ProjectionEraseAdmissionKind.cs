namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// Outcome kind of a <see cref="IProjectionLifecycleActor.BeginEraseAsync"/> admission decision.
/// </summary>
public enum ProjectionEraseAdmissionKind {
    /// <summary>The erase operation was newly admitted from the <see cref="ProjectionLifecyclePhase.Idle"/> phase.</summary>
    Admitted = 0,

    /// <summary>The same operation is already in progress; the caller resumes it.</summary>
    Resume = 1,

    /// <summary>A different operation is already in progress; admission is refused.</summary>
    Conflict = 2,

    /// <summary>
    /// The lifecycle scope is idle, but a fresh begin is not allowed by the caller's fail-closed rebuild gate.
    /// </summary>
    BeginNotAllowed = 3,
}
