using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// Carries an explicit idempotency decision and any cached command result.
/// </summary>
/// <param name="Outcome">The lookup outcome.</param>
/// <param name="Result">The cached result when the outcome carries one.</param>
/// <param name="StateMutationStaged">Whether the checker staged actor-state changes that require a commit.</param>
[DataContract]
public sealed record IdempotencyCheckResult(
    [property: DataMember] IdempotencyCheckOutcome Outcome,
    [property: DataMember] CommandProcessingResult? Result = null,
    [property: DataMember] bool StateMutationStaged = false);
