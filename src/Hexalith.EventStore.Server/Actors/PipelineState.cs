
using Hexalith.EventStore.Contracts.Commands;

namespace Hexalith.EventStore.Server.Actors;
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
/// <param name="ResultPayload">
/// Legacy serialized result payload retained only for old in-flight checkpoint deserialization.
/// Pipeline checkpoints are crash-recovery control state and new writes must keep this value null.
/// </param>
/// <param name="MessageId">The command message identifier, or <c>null</c> for a legacy checkpoint.</param>
/// <param name="CausationId">The normalized causation identifier, or <c>null</c> for a legacy checkpoint.</param>
/// <param name="StartSequence">
/// The first aggregate sequence number of this command's committed events, or <c>null</c> for a
/// pre-range legacy checkpoint. Persisted so resume/handoff never re-derive the range from the
/// mutable stream head (which an interleaved command may have advanced).
/// </param>
/// <param name="EndSequence">
/// The last aggregate sequence number of this command's committed events, or <c>null</c> for a
/// pre-range legacy checkpoint.
/// </param>
public record PipelineState(
    string CorrelationId,
    CommandStatus CurrentStage,
    string CommandType,
    DateTimeOffset StartedAt,
    int? EventCount,
    string? RejectionEventType,
    string? ResultPayload = null,
    string? MessageId = null,
    string? CausationId = null,
    long? StartSequence = null,
    long? EndSequence = null);
