
using System.Collections.Concurrent;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Events;

namespace Hexalith.EventStore.Testing.Fakes;
/// <summary>
/// Fake aggregate actor for testing. Records all invocations for assertion.
/// Optionally simulates idempotency by tracking processed causation IDs.
/// </summary>
public class FakeAggregateActor : IAggregateActor {
    /// <inheritdoc/>
    public Task<SourceEventRange> ReadNativeEventsBoundedRangeAsync(long fromSequence, long toSequence, int maxCount, int maxBytes)
        => Task.FromException<SourceEventRange>(new NotSupportedException("The legacy fake does not supply native source authority."));

    /// <inheritdoc/>
    public Task<TrustedEffectResult> ProcessTrustedEffectAsync(
        TrustedEffectSubmission submission,
        TrustedEffectContext context,
        string gatewayProof)
        => throw new InvalidOperationException("Fake trusted effect processing requires an explicit receipt implementation.");

    private readonly ConcurrentQueue<CommandEnvelope> _receivedCommands = new();
    private readonly ConcurrentQueue<IdempotencyExecutionContext> _receivedExecutionContexts = new();
    private readonly ConcurrentQueue<string> _receivedHumanAuthorityReceipts = new();
    private readonly ConcurrentDictionary<string, CommandProcessingResult> _processedCausationIds = new();

    /// <summary>Gets the list of received commands for assertion.</summary>
    public IReadOnlyCollection<CommandEnvelope> ReceivedCommands => [.. _receivedCommands];

    /// <summary>Gets internal execution contexts received through the fenced boundary.</summary>
    public IReadOnlyCollection<IdempotencyExecutionContext> ReceivedExecutionContexts
        => [.. _receivedExecutionContexts];

    /// <summary>Gets ephemeral human receipts received by actor invocations.</summary>
    public IReadOnlyCollection<string> ReceivedHumanAuthorityReceipts => [.. _receivedHumanAuthorityReceipts];

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

    /// <summary>Gets or sets explicit source evidence; a fake never infers a trusted anchor.</summary>
    public SourceProofInspection? ConfiguredSourceProof { get; set; }

    /// <inheritdoc/>
    public Task<SourceProofInspection> InspectSourceProofAsync()
        => Task.FromResult(ConfiguredSourceProof ?? new SourceProofInspection(
            SourceProofInspectionStatus.Unsupported, ConfiguredEvents.Length > 0,
            ConfiguredEvents.Length == 0 ? 0 : ConfiguredEvents.Max(static item => item.SequenceNumber), false, null));

    /// <inheritdoc/>
    public Task<SourceProofInspection> BootstrapSourceProofAsync(SourceProofBootstrapRequest request)
        => throw new InvalidOperationException("Fake source bootstrap requires explicit maintenance authority and durable actor state.");

    /// <inheritdoc/>
    public async Task<EventEnvelope[]> ReadEventsBoundedRangeAsync(
        long fromSequence, long toSequence, int maxCount, int maxBytes)
    {
        SourceEventRange range = await ReadMeasuredEventsBoundedRangeAsync(fromSequence, toSequence, maxCount, maxBytes).ConfigureAwait(false);
        if (range.ReasonCode is not null) throw new InvalidOperationException(range.ReasonCode);
        return range.Events;
    }

    /// <inheritdoc/>
    public async Task<SourceEventRange> ReadMeasuredEventsBoundedRangeAsync(
        long fromSequence, long toSequence, int maxCount, int maxBytes)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxCount, 256);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxBytes, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxBytes, 2_097_152);
        EventEnvelope[] candidates = await ReadEventsRangeAsync(fromSequence, toSequence, maxCount).ConfigureAwait(false);
        var result = new List<EventEnvelope>();
        long bytes = 2;
        foreach (EventEnvelope envelope in candidates)
        {
            int length = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(envelope,
                System.Text.Json.JsonSerializerOptions.Web).Length;
            if (length > maxBytes - 2) return new([], 2) { ReasonCode = "delivery_page_limit_exceeded" };
            long next = bytes + length + (result.Count == 0 ? 0 : 1);
            if (next > maxBytes) break;
            result.Add(envelope);
            bytes = next;
        }

        return new([.. result], bytes);
    }

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
    public Task<long?> GetRetainedFloorAsync()
        => Task.FromResult<long?>(ConfiguredEvents.Length == 0 ? null : ConfiguredEvents.Min(e => e.SequenceNumber));

    /// <inheritdoc/>
    public Task EraseTrustedEffectEvidenceAsync(TrustedEffectAggregateErasure request)
        => throw new InvalidOperationException("Fake aggregate actor does not model durable trusted effect erasure.");

    /// <inheritdoc/>
    public Task FenceTrustedEffectsAsync(TrustedEffectAggregateErasure request)
        => throw new InvalidOperationException("Fake aggregate actor does not model durable trusted effect deletion fencing.");

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
    public Task<CommandProcessingResult> ProcessAuthorizedCommandAsync(AuthorizedCommandEnvelope request)
    {
        ArgumentNullException.ThrowIfNull(request);
        _receivedHumanAuthorityReceipts.Enqueue(request.HumanAuthorityReceipt);
        return ProcessCommandAsync(request.Command, CancellationToken.None);
    }

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
        if (request.HumanAuthorityReceipt is not null)
        {
            _receivedHumanAuthorityReceipts.Enqueue(request.HumanAuthorityReceipt);
        }
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
