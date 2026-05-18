namespace Hexalith.EventStore.Contracts.Security;

/// <summary>
/// Story 22.7c — stable pipeline-stage labels recorded by every decision result. Stage names are
/// part of the operator-facing wire contract and must not change without a versioned migration.
/// </summary>
public enum ProtectedDataDecisionStage {
    /// <summary>Command-time rehydration (pre-domain readability boundary in the actor).</summary>
    Rehydrate = 0,

    /// <summary>DAPR pub/sub publication path.</summary>
    Publish = 1,

    /// <summary>Public stream read/replay endpoint.</summary>
    Replay = 2,

    /// <summary>Projection rebuild path.</summary>
    Rebuild = 3,

    /// <summary>Snapshot load path.</summary>
    SnapshotLoad = 4,

    /// <summary>Restored-backup admission decision boundary.</summary>
    BackupAdmission = 5,

    /// <summary>Admin inspection / debugging surfaces (admin stream query, blame view, etc.).</summary>
    AdminInspection = 6,
}
