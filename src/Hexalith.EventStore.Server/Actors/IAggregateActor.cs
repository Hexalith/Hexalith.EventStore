
using Dapr.Actors;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Events;

namespace Hexalith.EventStore.Server.Actors;
/// <summary>
/// DAPR actor interface for aggregate command processing.
/// </summary>
public interface IAggregateActor : IActor {
    /// <summary>
    /// Processes a command envelope within the aggregate actor context.
    /// </summary>
    /// <param name="command">The command envelope to process.</param>
    /// <returns>The result of processing the command.</returns>
    Task<CommandProcessingResult> ProcessCommandAsync(CommandEnvelope command);

    /// <summary>
    /// Reads events from the aggregate's event stream starting after the given sequence number.
    /// </summary>
    /// <param name="fromSequence">The sequence number to read after (exclusive). Events with SequenceNumber &gt; fromSequence are returned.</param>
    /// <returns>An array of event envelopes ordered by sequence number. Empty array if no events exist after fromSequence.</returns>
    /// <exception cref="MissingEventException">Thrown when an expected event key is missing from the state store (data corruption).</exception>
    /// <exception cref="EventDeserializationException">Thrown when an event cannot be deserialized from the state store.</exception>
    Task<EventEnvelope[]> GetEventsAsync(long fromSequence);
}
