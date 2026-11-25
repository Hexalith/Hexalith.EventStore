namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// Persisted lifecycle phase of a projection with respect to rebuild and erasure. The
/// <see cref="ProjectionLifecycleActor"/> persists this phase so that projection delivery
/// writes and projection erasure are mutually exclusive per
/// (tenant, domain, aggregate, projection).
/// </summary>
public enum ProjectionLifecyclePhase {
    /// <summary>No rebuild or erase is in progress; projection delivery writes are admitted.</summary>
    Idle = 0,

    /// <summary>An erase operation is in progress; projection delivery writes are deferred.</summary>
    Erasing = 1,

    /// <summary>A rebuild operation is in progress; ordinary delivery writes are deferred.</summary>
    Rebuilding = 2,

    /// <summary>An admitted delivery owns the lifecycle scope until its projection write completes.</summary>
    Delivering = 3,
}
