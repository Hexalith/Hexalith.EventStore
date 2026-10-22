using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Identifies the exact unactivated target import permitted for rollback.</summary>
[DataContract]
public sealed record IdempotencyAdmissionPromotionRollbackRequest(
    [property: DataMember] string SourceActorId,
    [property: DataMember] string MigrationId,
    [property: DataMember] string SourceEvidenceDigest,
    [property: DataMember] string ImportDigest);
