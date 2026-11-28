using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Requests a fenced transition before crossing the side-effect boundary.</summary>
/// <param name="FencingToken">The reservation fence.</param>
/// <param name="Authority">Optional authority verification request validated atomically with the transition.</param>
[DataContract]
public sealed record IdempotencyAdmissionTransitionRequest(
    [property: DataMember] long FencingToken,
    [property: DataMember] IdempotencyAdmissionAuthorityRequest? Authority = null);
