
using Dapr.Actors;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.Server.Events;

namespace Hexalith.EventStore.Server.Actors;
/// <summary>
/// DAPR actor interface for aggregate command processing.
/// </summary>
public interface IAggregateActor : IActor {
    /// <summary>Processes one effect against the target actor receipt authority.</summary>
    Task<TrustedEffectResult> ProcessTrustedEffectAsync(
        TrustedEffectSubmission submission,
        TrustedEffectContext context,
        string gatewayProof);

    /// <summary>Durably fences a registered partition before the tenant deletion transition commits.</summary>
    Task FenceTrustedEffectsAsync(TrustedEffectAggregateErasure request);

    /// <summary>Processes a protected command through the internal signed-fence boundary.</summary>
    /// <param name="request">The command and current-fence capability.</param>
    /// <returns>The exact command processing result.</returns>
    Task<CommandProcessingResult> ProcessFencedCommandAsync(FencedCommandEnvelope request);

    /// <summary>Reads authoritative aggregate idempotency outcome under a signed fence without executing work.</summary>
    Task<IdempotencyCheckResult> ReconcileFencedCommandAsync(FencedCommandEnvelope request);

    /// <summary>
    /// Processes a command envelope within the aggregate actor context.
    /// </summary>
    /// <param name="command">The command envelope to process.</param>
    /// <returns>The result of processing the command.</returns>
    Task<CommandProcessingResult> ProcessCommandAsync(CommandEnvelope command);

    /// <summary>Processes a command with separately signed, ephemeral human authority.</summary>
    Task<CommandProcessingResult> ProcessAuthorizedCommandAsync(AuthorizedCommandEnvelope request);

    /// <summary>
    /// Reads events from the aggregate's event stream starting after the given sequence number.
    /// </summary>
    /// <param name="fromSequence">The sequence number to read after (exclusive). Events with SequenceNumber &gt; fromSequence are returned.</param>
    /// <returns>An array of event envelopes ordered by sequence number. Empty array if no events exist after fromSequence.</returns>
    /// <exception cref="MissingEventException">Thrown when an expected event key is missing from the state store (data corruption).</exception>
    /// <exception cref="EventDeserializationException">Thrown when an event cannot be deserialized from the state store.</exception>
    Task<EventEnvelope[]> GetEventsAsync(long fromSequence);

    /// <summary>
    /// Reads a bounded range of events from the aggregate's event stream.
    /// </summary>
    /// <param name="fromSequence">The sequence number to read after (exclusive).</param>
    /// <param name="toSequence">Optional inclusive upper sequence bound.</param>
    /// <param name="maxCount">Maximum number of events to return.</param>
    /// <returns>An array of event envelopes ordered by sequence number. Empty array if no events exist in the requested range.</returns>
    /// <exception cref="MissingEventException">Thrown when an expected event key is missing from the state store (data corruption).</exception>
    /// <exception cref="EventDeserializationException">Thrown when an event cannot be deserialized from the state store.</exception>
    Task<EventEnvelope[]> ReadEventsRangeAsync(long fromSequence, long? toSequence, int maxCount);

    /// <summary>
    /// Gets the aggregate stream's current highest sequence number.
    /// </summary>
    /// <returns>The current sequence number, or 0 when the stream does not exist.</returns>
    Task<long> GetCurrentSequenceAsync();

    /// <summary>
    /// Gets aggregate stream metadata without reading event payload state.
    /// </summary>
    /// <returns>Stream existence and current sequence metadata.</returns>
    Task<AggregateStreamMetadata> GetStreamMetadataAsync();

    /// <summary>Inspects persisted source evidence using metadata only, preserving the actor cache barrier.</summary>
    Task<SourceProofInspection> InspectSourceProofAsync();

    /// <summary>Bootstraps an eligible legacy stream under a signed quiescent-maintenance authorization.</summary>
    Task<SourceProofInspection> BootstrapSourceProofAsync(SourceProofBootstrapRequest request);

    /// <summary>Reads an ordered range bounded by both event count and serialized UTF-8 array bytes.</summary>
    /// <param name="fromSequence">The exclusive lower sequence bound.</param>
    /// <param name="toSequence">The admitted inclusive head.</param>
    /// <param name="maxCount">The positive requested count, at most 256.</param>
    /// <param name="maxBytes">The compact array byte ceiling, at most 2,097,152.</param>
    Task<EventEnvelope[]> ReadEventsBoundedRangeAsync(long fromSequence, long toSequence, int maxCount, int maxBytes);

    /// <summary>Reads a bounded range with its exact actor-owned compact event-array byte length.</summary>
    /// <param name="fromSequence">The exclusive lower sequence bound.</param>
    /// <param name="toSequence">The admitted inclusive head.</param>
    /// <param name="maxCount">The positive requested count, at most 256.</param>
    /// <param name="maxBytes">The compact array byte ceiling, at most 2,097,152.</param>
    Task<SourceEventRange> ReadMeasuredEventsBoundedRangeAsync(long fromSequence, long toSequence, int maxCount, int maxBytes);

    /// <summary>Returns the source stream's authoritative inclusive retained floor, or null if absent.</summary>
    Task<long?> GetRetainedFloorAsync();

    /// <summary>Idempotently removes this actor's registered stream and effect evidence during tenant purge.</summary>
    Task EraseTrustedEffectEvidenceAsync(TrustedEffectAggregateErasure request);

    /// <summary>
    /// Creates a manual snapshot for the actor's current stream sequence from inside the
    /// aggregate actor boundary.
    /// </summary>
    /// <param name="correlationId">Optional correlation id for logs and diagnostics.</param>
    /// <returns>The actor-owned manual snapshot result.</returns>
    Task<ManualSnapshotResult> CreateManualSnapshotAsync(string? correlationId);
}
