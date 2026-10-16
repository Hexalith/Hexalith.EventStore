namespace Hexalith.EventStore.Controllers;

/// <summary>Quiescent maintenance attestations for actor-owned source bootstrap.</summary>
/// <param name="ExpectedHead">The inventoried aggregate-local head.</param>
/// <param name="CutoverCommit">The exact full fleet commit.</param>
/// <param name="BackupReference">The consistent verified backup identity.</param>
/// <param name="SpecSha256">The approved normative digest.</param>
/// <param name="WritersQuiesced">Whether every append and projection writer is stopped.</param>
/// <param name="BackupRestoreVerified">Whether the consistent backup restore has been verified.</param>
public sealed record ProjectionSourceBootstrapRequestBody(long ExpectedHead, string CutoverCommit,
    string BackupReference, string SpecSha256, bool WritersQuiesced, bool BackupRestoreVerified);
