using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Requests a fenced non-terminal recovery transition.</summary>
/// <param name="FencingToken">The active fence.</param>
/// <param name="State">The recoverable or unknown-outcome target state.</param>
[DataContract]
public sealed record IdempotencyAdmissionRecoveryRequest(
    [property: DataMember] long FencingToken,
    [property: DataMember] IdempotencyAdmissionState State);
