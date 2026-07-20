using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Activates an acknowledged import after the directory canonical pointer flipped.</summary>
/// <param name="SourceActorId">The exact prior canonical source actor.</param>
[DataContract]
public sealed record IdempotencyAdmissionPromotionActivationRequest(
    [property: DataMember] string SourceActorId);
