namespace Hexalith.EventStore.Admin.Abstractions.Models.Storage;

/// <summary>
/// Represents the status of a manual snapshot creation job.
/// </summary>
/// <remarks>
/// Manual snapshot creation is a bounded synchronous operation. Durable evidence normally
/// records terminal outcomes (<see cref="Done"/>, <see cref="AlreadyCurrent"/>, <see cref="Failed"/>);
/// <see cref="Queued"/> and <see cref="Running"/> exist only as optional transient evidence after
/// the deterministic sequence-scoped operation id is known.
/// </remarks>
public enum SnapshotJobStatus {
    /// <summary>The snapshot job has been accepted and is queued. Optional transient state.</summary>
    Queued,

    /// <summary>The snapshot job is currently running. Optional transient state.</summary>
    Running,

    /// <summary>The snapshot was created successfully.</summary>
    Done,

    /// <summary>An existing snapshot already covers the current sequence; no rewrite occurred.</summary>
    AlreadyCurrent,

    /// <summary>The snapshot job failed.</summary>
    Failed,
}
