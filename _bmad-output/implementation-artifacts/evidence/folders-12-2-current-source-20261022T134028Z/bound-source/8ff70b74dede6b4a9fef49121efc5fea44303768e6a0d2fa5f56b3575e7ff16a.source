using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Requests acknowledgement of one exact prepared target import.</summary>
[DataContract]
public sealed record IdempotencyAdmissionPromotionAcknowledgementRequest(
    [property: DataMember] string SourceActorId,
    [property: DataMember] string MigrationId,
    [property: DataMember] string SourceEvidenceDigest,
    [property: DataMember] string ImportDigest);
