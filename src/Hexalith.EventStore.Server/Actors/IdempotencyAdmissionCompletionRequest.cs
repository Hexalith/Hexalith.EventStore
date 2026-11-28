using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Requests terminal finalization under the active fence.</summary>
/// <param name="FencingToken">The active fence.</param>
/// <param name="Result">The deterministic command result to retain for replay.</param>
/// <param name="Authority">Optional authority verification request validated atomically with finalization.</param>
[DataContract]
public sealed record IdempotencyAdmissionCompletionRequest(
    [property: DataMember] long FencingToken,
    [property: DataMember] CommandProcessingResult Result,
    [property: DataMember] IdempotencyAdmissionAuthorityRequest? Authority = null);
