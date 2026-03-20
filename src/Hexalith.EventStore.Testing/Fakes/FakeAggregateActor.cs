
using System.Collections.Concurrent;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Events;

namespace Hexalith.EventStore.Testing.Fakes;
/// <summary>
/// Fake aggregate actor for testing. Records all invocations for assertion.
/// Optionally simulates idempotency by tracking processed causation IDs.
/// </summary>
public class FakeAggregateActor : IAggregateActor {
    private readonly ConcurrentQueue<CommandEnvelope> _receivedCommands = new();
    private readonly ConcurrentDictionary<string, CommandProcessingResult> _processedCausationIds = new();

    /// <summary>Gets the list of received commands for assertion.</summary>
    public IReadOnlyCollection<CommandEnvelope> ReceivedCommands => [.. _receivedCommands];

    /// <summary>Gets or sets the result to return from ProcessCommandAsync.</summary>
    public CommandProcessingResult? ConfiguredResult { get; set; }

    /// <summary>Gets or sets the exception to throw from ProcessCommandAsync.</summary>
    public Exception? ConfiguredException { get; set; }

    /// <summary>Gets or sets whether to simulate idempotency checking via causation ID tracking.</summary>
    public bool SimulateIdempotency { get; set; }

    /// <summary>Gets the number of commands that were actually processed (not short-circuited by idempotency).</summary>
    public int ProcessedCount => _processedCausationIds.Count;

    /// <summary>Gets or sets the configurable events for GetEventsAsync.</summary>
    public EventEnvelope[] ConfiguredEvents { get; set; } = [];

    /// <inheritdoc/>
    public Task<EventEnvelope[]> GetEventsAsync(long fromSequence)
        => Task.FromResult(ConfiguredEvents
            .Where(e => e.SequenceNumber > fromSequence)
            .OrderBy(e => e.SequenceNumber)
            .ToArray());

    /// <inheritdoc/>
    public Task<CommandProcessingResult> ProcessCommandAsync(CommandEnvelope command) {
        ArgumentNullException.ThrowIfNull(command);
        _receivedCommands.Enqueue(command);

        if (ConfiguredException is not null) {
            throw ConfiguredException;
        }

        CommandProcessingResult result = ConfiguredResult ?? new CommandProcessingResult(Accepted: true, CorrelationId: command.CorrelationId);

        if (SimulateIdempotency) {
            string causationId = command.CausationId ?? command.CorrelationId;
            if (_processedCausationIds.TryGetValue(causationId, out CommandProcessingResult? cached)) {
                return Task.FromResult(cached);
            }

            _processedCausationIds[causationId] = result;
        }

        return Task.FromResult(result);
    }
}
