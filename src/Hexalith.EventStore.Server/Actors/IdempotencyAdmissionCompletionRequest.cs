using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Requests terminal finalization under the active fence.</summary>
/// <param name="FencingToken">The active fence.</param>
/// <param name="Result">The deterministic command result to retain for replay.</param>
[DataContract]
public sealed record IdempotencyAdmissionCompletionRequest(
    [property: DataMember] long FencingToken,
    [property: DataMember] CommandProcessingResult Result);
