namespace Hexalith.EventStore.Server.Actors;

using Hexalith.EventStore.Contracts.Commands;

/// <summary>
/// Tracks in-flight command lifecycle through the actor pipeline.
/// Persisted via IActorStateManager for crash-recovery resume (NFR25).
/// </summary>
/// <param name="CorrelationId">The correlation identifier for the command.</param>
/// <param name="CurrentStage">The current pipeline stage (CommandStatus enum value).</param>
/// <param name="CommandType">The command type name.</param>
/// <param name="StartedAt">When pipeline processing began.</param>
/// <param name="EventCount">Number of events persisted (populated at EventsStored stage).</param>
/// <param name="RejectionEventType">Rejection event type name (populated for rejections).</param>
public record PipelineState(
    string CorrelationId,
    CommandStatus CurrentStage,
    string CommandType,
    DateTimeOffset StartedAt,
    int? EventCount,
    string? RejectionEventType);
