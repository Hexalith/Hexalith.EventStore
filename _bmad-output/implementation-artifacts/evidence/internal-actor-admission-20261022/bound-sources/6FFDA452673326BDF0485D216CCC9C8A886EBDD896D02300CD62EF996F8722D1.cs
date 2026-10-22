namespace Hexalith.EventStore.Server.Projections;

/// <summary>Attributable, persisted maintenance authority; never substitutes for source or model proof.</summary>
/// <param name="OperatorId">The authenticated operator, supplied by the maintenance endpoint.</param>
/// <param name="CutoverCommit">The exact full revision installed throughout the fleet.</param>
/// <param name="BackupReference">The durable consistent pre-cutover backup identity.</param>
/// <param name="SpecSha256">The approved normative specification digest.</param>
/// <param name="TrafficStopped">Whether command and projection traffic is stopped.</param>
/// <param name="BackupRestoreVerified">Whether the consistent backup restore was verified.</param>
/// <param name="DowngradeProhibited">Whether in-place downgrade and selective restore are prohibited.</param>
/// <param name="WriterInventory">The complete upgraded and quiesced writer inventory.</param>
public sealed record ProjectionSourceGuardMaintenance(string OperatorId, string CutoverCommit,
    string BackupReference, string SpecSha256, bool TrafficStopped, bool BackupRestoreVerified,
    bool DowngradeProhibited, IReadOnlyList<ProjectionSourceGuardWriterInventory> WriterInventory)
{
    private static readonly string[] RequiredRoles = ["append", "immediate", "poller", "retry", "reconciliation", "rebuild", "erasure"];

    /// <summary>Checks every required role and exact deployed identity without admitting mixed writers.</summary>
    internal bool IsValid => !string.IsNullOrWhiteSpace(OperatorId)
        && IsCommit(CutoverCommit) && !string.IsNullOrWhiteSpace(BackupReference)
        && SpecSha256 == ProjectionSourceGuard.ApprovedSpecSha256
        && TrafficStopped && BackupRestoreVerified && DowngradeProhibited
        && WriterInventory is { Count: > 0 }
        && RequiredRoles.All(role => WriterInventory.Any(item => item is not null && item.Role == role))
        && WriterInventory.All(item => item is not null && RequiredRoles.Contains(item.Role, StringComparer.Ordinal)
            && item.Commit == CutoverCommit && item.UpgradedAndQuiesced
            && !string.IsNullOrWhiteSpace(item.PackageIdentity));

    private static bool IsCommit(string? value) => value is { Length: 40 }
        && value.All(static character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
