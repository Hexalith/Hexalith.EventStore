namespace Hexalith.EventStore.Contracts.Replay;

/// <summary>
/// Wire request asking a domain service to replay events for an aggregate via its
/// canonical Apply path. Sent by the EventStore server to the domain service's
/// <c>POST /replay-state</c> endpoint.
/// </summary>
/// <param name="TenantId">Tenant identifier for the aggregate.</param>
/// <param name="Domain">Domain (bounded context) identifying which aggregate processor owns the replay.</param>
/// <param name="AggregateType">Optional aggregate type hint. May be empty when the domain identifies the aggregate uniquely.</param>
/// <param name="AggregateId">Aggregate identifier.</param>
/// <param name="UpToSequence">Inclusive replay target. Events with sequence/version &lt;= this value are eligible for replay.</param>
/// <param name="Events">Events available for replay. The reconstructor sorts by <see cref="ReplayEventEnvelope.SequenceNumber"/>.</param>
/// <param name="IncludeTimeline">When true the result also returns the per-event state snapshots in <see cref="AggregateReconstructionResult.Timeline"/>.</param>
/// <param name="RequestId">Optional correlation id used by logs and traces.</param>
public sealed record AggregateReconstructionRequest(
    string TenantId,
    string Domain,
    string AggregateType,
    string AggregateId,
    long UpToSequence,
    IReadOnlyList<ReplayEventEnvelope> Events,
    bool IncludeTimeline,
    string? RequestId);
