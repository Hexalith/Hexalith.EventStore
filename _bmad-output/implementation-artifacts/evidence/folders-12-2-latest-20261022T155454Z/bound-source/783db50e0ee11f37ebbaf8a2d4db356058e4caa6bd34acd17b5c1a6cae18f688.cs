using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Requests the irreversible payload-free redirect for an exact proven source.</summary>
[DataContract]
internal sealed record IdempotencyLegacySourceRedirectRequest(
    [property: DataMember] IdempotencyLegacySourceRequest Source,
    [property: DataMember] string TargetAdmissionActorId);
