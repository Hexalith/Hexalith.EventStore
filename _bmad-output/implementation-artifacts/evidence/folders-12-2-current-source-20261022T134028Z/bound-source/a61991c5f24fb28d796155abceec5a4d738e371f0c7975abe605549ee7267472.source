namespace Hexalith.EventStore.Contracts.Security;

/// <summary>
/// Stable kebab-case wire codes for <see cref="ProtectedDataDecisionStage"/>.
/// </summary>
public static class ProtectedDataReadabilityDecisionStageCodes {
    /// <summary>Code for <see cref="ProtectedDataDecisionStage.Rehydrate"/>.</summary>
    public const string Rehydrate = "rehydrate";

    /// <summary>Code for <see cref="ProtectedDataDecisionStage.Publish"/>.</summary>
    public const string Publish = "publish";

    /// <summary>Code for <see cref="ProtectedDataDecisionStage.Replay"/>.</summary>
    public const string Replay = "replay";

    /// <summary>Code for <see cref="ProtectedDataDecisionStage.Rebuild"/>.</summary>
    public const string Rebuild = "rebuild";

    /// <summary>Code for <see cref="ProtectedDataDecisionStage.SnapshotLoad"/>.</summary>
    public const string SnapshotLoad = "snapshot-load";

    /// <summary>Code for <see cref="ProtectedDataDecisionStage.BackupAdmission"/>.</summary>
    public const string BackupAdmission = "backup-admission";

    /// <summary>Code for <see cref="ProtectedDataDecisionStage.AdminInspection"/>.</summary>
    public const string AdminInspection = "admin-inspection";

    /// <summary>Maps a <see cref="ProtectedDataDecisionStage"/> to its stable wire code.</summary>
    /// <param name="stage">The pipeline stage.</param>
    /// <returns>The stable wire code.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the stage is outside the defined enum range.</exception>
    public static string From(ProtectedDataDecisionStage stage) => stage switch {
        ProtectedDataDecisionStage.Rehydrate => Rehydrate,
        ProtectedDataDecisionStage.Publish => Publish,
        ProtectedDataDecisionStage.Replay => Replay,
        ProtectedDataDecisionStage.Rebuild => Rebuild,
        ProtectedDataDecisionStage.SnapshotLoad => SnapshotLoad,
        ProtectedDataDecisionStage.BackupAdmission => BackupAdmission,
        ProtectedDataDecisionStage.AdminInspection => AdminInspection,
        _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unknown ProtectedDataDecisionStage value."),
    };
}
