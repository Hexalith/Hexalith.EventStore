using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Persists a source redirect only after the target import is acknowledged.</summary>
/// <param name="TargetActorId">The prepared target actor.</param>
[DataContract]
public sealed record IdempotencyAdmissionRedirectRequest(
    [property: DataMember] string TargetActorId);
