
using System.Collections.Concurrent;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Events;

namespace Hexalith.EventStore.Testing.Fakes;
/// <summary>
/// Fake aggregate actor for testing. Records all invocations for assertion.
/// Optionally simulates idempotency by tracking processed causation IDs.
/// </summary>
public class FakeAggregateActor : IAggregateActor {
    private readonly ConcurrentQueue<CommandEnvelope> _receivedCommands = new();
    private readonly ConcurrentQueue<IdempotencyExecutionContext> _receivedExecutionContexts = new();
    private readonly ConcurrentDictionary<string, CommandProcessingResult> _processedCausationIds = new();

    /// <summary>Gets the list of received commands for assertion.</summary>
    public IReadOnlyCollection<CommandEnvelope> ReceivedCommands => [.. _receivedCommands];

    /// <summary>Gets internal execution contexts received through the fenced boundary.</summary>
    public IReadOnlyCollection<IdempotencyExecutionContext> ReceivedExecutionContexts
        => [.. _receivedExecutionContexts];

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
    public Task<EventEnvelope[]> ReadEventsRangeAsync(long fromSequence, long? toSequence, int maxCount) {
        ArgumentOutOfRangeException.ThrowIfNegative(fromSequence);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCount);
        return Task.FromResult(ConfiguredEvents
            .Where(e => e.SequenceNumber > fromSequence && (!toSequence.HasValue || e.SequenceNumber <= toSequence.Value))
            .OrderBy(e => e.SequenceNumber)
            .Take(maxCount)
            .ToArray());
    }

    /// <inheritdoc/>
    public Task<long> GetCurrentSequenceAsync()
        => Task.FromResult(ConfiguredEvents.Length == 0 ? 0 : ConfiguredEvents.Max(e => e.SequenceNumber));

    /// <inheritdoc/>
    public Task<AggregateStreamMetadata> GetStreamMetadataAsync()
        => Task.FromResult(new AggregateStreamMetadata(Exists: ConfiguredEvents.Length > 0, CurrentSequence: ConfiguredEvents.Length == 0 ? 0 : ConfiguredEvents.Max(e => e.SequenceNumber)));

    /// <inheritdoc/>
    public Task<ManualSnapshotResult> CreateManualSnapshotAsync(string? correlationId) {
        long sequence = ConfiguredEvents.Length == 0 ? 0 : ConfiguredEvents.Max(e => e.SequenceNumber);
        return Task.FromResult(sequence == 0
            ? new ManualSnapshotResult(ManualSnapshotOutcome.NotFound, 0, null, "NotFound", "Aggregate stream was not found.")
            : new ManualSnapshotResult(ManualSnapshotOutcome.Created, sequence, "snapshot", null, null));
    }

    /// <inheritdoc/>
    public Task<CommandProcessingResult> ProcessCommandAsync(CommandEnvelope command)
        => ProcessCommandAsync(command, CancellationToken.None);

    /// <inheritdoc/>
    public Task<CommandProcessingResult> ProcessFencedCommandAsync(FencedCommandEnvelope request)
        => ProcessFencedCommandAsync(request, CancellationToken.None);

    /// <inheritdoc/>
    public Task<IdempotencyCheckResult> ReconcileFencedCommandAsync(FencedCommandEnvelope request)
    {
        ArgumentNullException.ThrowIfNull(request);
        string causationId = request.Command.CausationId ?? request.Command.MessageId;
        return Task.FromResult(_processedCausationIds.TryGetValue(causationId, out CommandProcessingResult? result)
            ? new IdempotencyCheckResult(IdempotencyCheckOutcome.ExactTerminalDuplicate, result)
            : new IdempotencyCheckResult(IdempotencyCheckOutcome.Miss));
    }

    /// <summary>Processes a fenced command while honoring local cancellation.</summary>
    public Task<CommandProcessingResult> ProcessFencedCommandAsync(
        FencedCommandEnvelope request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        _receivedExecutionContexts.Enqueue(request.ExecutionContext);
        return ProcessCommandAsync(request.Command, cancellationToken);
    }

    /// <summary>
    /// Processes a command envelope while honoring local/in-process cancellation.
    /// </summary>
    /// <param name="command">The command envelope.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The configured command processing result.</returns>
    public Task<CommandProcessingResult> ProcessCommandAsync(CommandEnvelope command, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
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
