using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Proves the exact hash-bound promotion target and whether it is executable.</summary>
[DataContract]
public sealed record IdempotencyAdmissionPromotionAcknowledgement(
    [property: DataMember] int SchemaVersion,
    [property: DataMember] string SourceActorId,
    [property: DataMember] string MigrationId,
    [property: DataMember] string SourceEvidenceDigest,
    [property: DataMember] string ImportDigest,
    [property: DataMember] bool Activated,
    [property: DataMember] string? CurrentStateDigest = null);
