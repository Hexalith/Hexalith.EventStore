using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Returns the canonical actor and any persisted promotion work.</summary>
/// <param name="CanonicalActorId">The current canonical admission actor.</param>
/// <param name="PromotionPhase">The current persisted promotion phase.</param>
/// <param name="PromotionSourceActorId">The optional promotion source.</param>
/// <param name="PromotionTargetActorId">The optional promotion target.</param>
[DataContract]
public sealed record IdempotencyAdmissionDirectoryResult(
    [property: DataMember] string CanonicalActorId,
    [property: DataMember] IdempotencyAdmissionPromotionPhase PromotionPhase,
    [property: DataMember] string? PromotionSourceActorId = null,
    [property: DataMember] string? PromotionTargetActorId = null);
