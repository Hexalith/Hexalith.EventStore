using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Short-lived operator authorization for actor-owned source-proof bootstrap.</summary>
/// <param name="TenantId">The exact tenant.</param>
/// <param name="Domain">The exact domain.</param>
/// <param name="AggregateId">The exact aggregate.</param>
/// <param name="ExpectedHead">The inventoried inclusive source head.</param>
/// <param name="OperatorId">The authenticated maintenance operator.</param>
/// <param name="CutoverCommit">The full deployed canonical commit.</param>
/// <param name="BackupReference">The verified consistent backup identity.</param>
/// <param name="SpecSha256">The approved normative digest.</param>
/// <param name="WritersQuiesced">Whether every append and projection writer is quiesced.</param>
/// <param name="BackupRestoreVerified">Whether the consistent backup restore was verified.</param>
/// <param name="ExpiresAt">The bounded authorization expiry.</param>
/// <param name="KeyVersion">The platform authority key version.</param>
/// <param name="Authorization">The domain-separated signed authorization.</param>
[DataContract]
public sealed record SourceProofBootstrapRequest(
    [property: DataMember] string TenantId, [property: DataMember] string Domain, [property: DataMember] string AggregateId, [property: DataMember] long ExpectedHead, [property: DataMember] string OperatorId,
    [property: DataMember] string CutoverCommit, [property: DataMember] string BackupReference, [property: DataMember] string SpecSha256, [property: DataMember] bool WritersQuiesced,
    [property: DataMember] bool BackupRestoreVerified, [property: DataMember] DateTimeOffset ExpiresAt, [property: DataMember] string KeyVersion, [property: DataMember] string Authorization)
{
    /// <summary>Redacts authorization and operational identity from diagnostic formatting.</summary>
    public override string ToString() => "SourceProofBootstrapRequest { [REDACTED] }";
}
