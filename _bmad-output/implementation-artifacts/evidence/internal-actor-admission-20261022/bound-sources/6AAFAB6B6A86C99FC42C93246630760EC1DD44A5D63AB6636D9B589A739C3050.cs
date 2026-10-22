using Hexalith.EventStore.Server.Projections;

namespace Hexalith.EventStore.Controllers;

/// <summary>Explicit exact-scope maintenance attestations; the endpoint supplies authenticated operator identity.</summary>
/// <param name="ExpectedHead">The inventoried source head.</param>
/// <param name="ExpectedIncarnation">The inventoried source incarnation.</param>
/// <param name="ExpectedPreviousIncarnation">The exact previous scope marker incarnation for an authorized recreation.</param>
/// <param name="CutoverCommit">The full canonical revision deployed throughout the fleet.</param>
/// <param name="BackupReference">The consistent verified backup identity.</param>
/// <param name="SpecSha256">The approved normative digest.</param>
/// <param name="TrafficStopped">Whether command and projection traffic is stopped.</param>
/// <param name="BackupRestoreVerified">Whether the backup restore was verified.</param>
/// <param name="DowngradeProhibited">Whether in-place downgrade is prohibited.</param>
/// <param name="WriterInventory">Every upgraded and quiesced writer implementation.</param>
public sealed record ProjectionSourceGuardQualificationRequestBody(long ExpectedHead, string ExpectedIncarnation,
    string? ExpectedPreviousIncarnation, string CutoverCommit, string BackupReference, string SpecSha256,
    bool TrafficStopped, bool BackupRestoreVerified, bool DowngradeProhibited,
    IReadOnlyList<ProjectionSourceGuardWriterInventory> WriterInventory);
