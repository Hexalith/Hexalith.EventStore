using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Carries an admission decision, fence, and optional replay result.</summary>
/// <param name="Decision">The safe admission action.</param>
/// <param name="FencingToken">The current monotonically increasing fence.</param>
/// <param name="ReplayResult">The replayable command result when terminal and equivalent.</param>
/// <param name="RedirectActorId">The protected promotion target for a redirect decision.</param>
/// <param name="ExecutionMessageId">The persisted stable execution identity for live states.</param>
/// <param name="ExecutionCorrelationId">The persisted stable aggregate-checkpoint identity for live states.</param>
[DataContract]
public sealed record IdempotencyAdmissionResult(
    [property: DataMember] IdempotencyAdmissionDecision Decision,
    [property: DataMember] long FencingToken = 0,
    [property: DataMember] CommandProcessingResult? ReplayResult = null,
    [property: DataMember] string? RedirectActorId = null,
    [property: DataMember] string? ExecutionMessageId = null,
    [property: DataMember] string? ExecutionCorrelationId = null);
