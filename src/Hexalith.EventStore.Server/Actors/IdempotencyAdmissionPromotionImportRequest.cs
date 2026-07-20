using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Prepares a non-executable imported admission record on a promotion target.</summary>
/// <param name="SourceActorId">The prior canonical source actor.</param>
/// <param name="Record">The protected live record translated to the target digest version.</param>
/// <param name="Tombstone">The protected consumed-key tombstone translated to the target digest version.</param>
[DataContract]
public sealed record IdempotencyAdmissionPromotionImportRequest(
    [property: DataMember] string SourceActorId,
    [property: DataMember] IdempotencyAdmissionRecord? Record = null,
    [property: DataMember] IdempotencyAdmissionTombstone? Tombstone = null);
