using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Requests a fenced transition before crossing the side-effect boundary.</summary>
/// <param name="FencingToken">The reservation fence.</param>
[DataContract]
public sealed record IdempotencyAdmissionTransitionRequest([property: DataMember] long FencingToken);
