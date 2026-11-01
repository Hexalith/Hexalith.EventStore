using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hexalith.EventStore.Client.Projections.Checkpointed;

/// <summary>
/// Applies live deliveries to a checkpointed projection record with one compare-and-swap per decision.
/// </summary>
/// <typeparam name="TState">The projection state type.</typeparam>
/// <remarks>
/// <para>
/// Exact-next sequences commit transition and checkpoint atomically; an identical accepted delivery is a no-op;
/// an older delivery is compared with authoritative history and never reapplied; a future delivery records an
/// immutable gap with a monotonic recovery horizon and sequence-keyed evidence; and contradicting identity is
/// persisted as terminal corruption. After a pending commit the writer drives fenced recovery under the lease.
/// </para>
/// <para>
/// Durable outcomes are returned and may be acknowledged. Compare-and-swap exhaustion (the
/// <see cref="ReadModelWritePolicy.DefaultMaxAttempts"/> total-attempt budget by default), provider failures,
/// unavailable history, and unavailable recovery throw <see cref="CheckpointedProjectionUnavailableException"/>
/// so the delivery is not acknowledged and its redelivery re-drives the decision.
/// </para>
/// </remarks>
public sealed class CheckpointedProjectionWriter<TState>
    where TState : class {
    private readonly IReadModelStore _store;
    private readonly ICheckpointedProjectionTransition<TState> _transition;
    private readonly CheckpointedProjectionHistory? _history;
    private readonly CheckpointedProjectionOptions _options;
    private readonly CheckpointedProjectionRecoveryWorker<TState> _recovery;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckpointedProjectionWriter{TState}"/> class.
    /// </summary>
    /// <param name="store">The read-model store providing ETag compare-and-swap.</param>
    /// <param name="transition">The domain-owned pure transition.</param>
    /// <param name="history">The authoritative range reader; <see langword="null"/> makes verification and recovery unavailable.</param>
    /// <param name="options">The write and recovery options.</param>
    /// <param name="timeProvider">The clock used for recovery leases.</param>
    /// <param name="logger">The logger.</param>
    public CheckpointedProjectionWriter(
        IReadModelStore store,
        ICheckpointedProjectionTransition<TState> transition,
        CheckpointedProjectionHistory? history = null,
        CheckpointedProjectionOptions? options = null,
        TimeProvider? timeProvider = null,
        ILogger? logger = null) {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(transition);
        options ??= new CheckpointedProjectionOptions();
        options.Validate();
        _store = store;
        _transition = transition;
        _history = history;
        _options = options;
        _logger = logger ?? NullLogger.Instance;
        _recovery = new CheckpointedProjectionRecoveryWorker<TState>(store, transition, history, options, timeProvider, logger);
    }

    /// <summary>
    /// Offers one live delivery to the record at <paramref name="key"/>.
    /// </summary>
    /// <param name="storeName">The read-model state-store component name.</param>
    /// <param name="key">The record key.</param>
    /// <param name="delivery">The delivered event, carrying its identity and decoded payload.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The durable outcome and the record it committed or observed.</returns>
    /// <exception cref="CheckpointedProjectionUnavailableException">No durable outcome could be reached.</exception>
    public async Task<CheckpointedProjectionResult<TState>> ApplyAsync(
        string storeName,
        string key,
        CheckpointedProjectionEvent delivery,
        CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ValidateDelivery(delivery);

        for (int attempt = 1; attempt <= _options.MaxAttempts; attempt++) {
            cancellationToken.ThrowIfCancellationRequested();
            (CheckpointedProjectionRecord<TState>? loaded, string etag) = await CheckpointedProjectionStorage
                .LoadAsync<TState>(_store, storeName, key, cancellationToken)
                .ConfigureAwait(false);
            CheckpointedProjectionRecord<TState> current = loaded ?? new CheckpointedProjectionRecord<TState>();

            if (current.Health == CheckpointedProjectionHealth.Corrupt) {
                return new CheckpointedProjectionResult<TState>(CheckpointedProjectionOutcome.Corrupt, current);
            }

            long sequence = delivery.Sequence;
            long last = current.LastContiguousSequence;
            CheckpointedProjectionRecord<TState> next;
            CheckpointedProjectionOutcome outcome;
            if (sequence == last) {
                if (current.LastAccepted == delivery.Evidence) {
                    return new CheckpointedProjectionResult<TState>(CheckpointedProjectionOutcome.Duplicate, current);
                }

                next = CheckpointedProjectionStorage.MarkCorrupt(current, CheckpointedProjectionCorruptionReason.AcceptedIdentityMismatch, sequence);
                outcome = CheckpointedProjectionOutcome.Corrupt;
            }
            else if (sequence < last) {
                CheckpointedProjectionCorruptionReason? mismatch = await VerifyOlderAsync(storeName, key, delivery, cancellationToken)
                    .ConfigureAwait(false);
                if (mismatch is null) {
                    return new CheckpointedProjectionResult<TState>(CheckpointedProjectionOutcome.Duplicate, current);
                }

                next = CheckpointedProjectionStorage.MarkCorrupt(current, mismatch.Value, sequence);
                outcome = CheckpointedProjectionOutcome.Corrupt;
            }
            else if (current.Health == CheckpointedProjectionHealth.Healthy && sequence == last + 1) {
                TState state = CheckpointedProjectionRecoveryWorker<TState>.ApplyTransition(_transition, current.State, delivery);
                next = current with {
                    State = state,
                    LastContiguousSequence = sequence,
                    LastAccepted = delivery.Evidence,
                    Version = current.Version + 1,
                };
                outcome = CheckpointedProjectionOutcome.Applied;
            }
            else {
                CheckpointedProjectionPendingEvidence? observed = current.Pending.FirstOrDefault(p => p.Sequence == sequence);
                if (observed is not null) {
                    if (observed.Evidence == delivery.Evidence) {
                        // Identical evidence is a no-op; the redelivery exists to re-drive recovery.
                        return await _recovery.RecoverAsync(storeName, key, delivery.Stream, cancellationToken).ConfigureAwait(false);
                    }

                    next = CheckpointedProjectionStorage.MarkCorrupt(current, CheckpointedProjectionCorruptionReason.PendingEvidenceConflict, sequence);
                    outcome = CheckpointedProjectionOutcome.Corrupt;
                }
                else {
                    next = MergeEvidence(current, delivery);
                    outcome = CheckpointedProjectionOutcome.Pending;
                }
            }

            if (await CheckpointedProjectionStorage.TrySaveAsync(_store, storeName, key, next, etag, cancellationToken).ConfigureAwait(false)) {
                switch (outcome) {
                    case CheckpointedProjectionOutcome.Corrupt:
                        CheckpointedProjectionLog.Corrupted(_logger, storeName, key, sequence, next.CorruptionReason!.Value);
                        return new CheckpointedProjectionResult<TState>(outcome, next);
                    case CheckpointedProjectionOutcome.Pending:
                        CheckpointedProjectionLog.Gapped(_logger, storeName, key, next.Expected!.Value, next.RecoveryThrough!.Value);
                        return await _recovery.RecoverAsync(storeName, key, delivery.Stream, cancellationToken).ConfigureAwait(false);
                    default:
                        return new CheckpointedProjectionResult<TState>(outcome, next);
                }
            }

            CheckpointedProjectionLog.ConcurrencyConflict(_logger, storeName, key, attempt, _options.MaxAttempts);
        }

        CheckpointedProjectionLog.Unavailable(_logger, storeName, key, CheckpointedProjectionUnavailableReason.ConcurrencyExhausted);
        throw new CheckpointedProjectionUnavailableException(CheckpointedProjectionUnavailableReason.ConcurrencyExhausted);
    }

    /// <summary>
    /// Reads the record at <paramref name="key"/> without changing it or starting recovery.
    /// </summary>
    /// <param name="storeName">The read-model state-store component name.</param>
    /// <param name="key">The record key.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The record, or <see langword="null"/> when no delivery was ever committed.</returns>
    /// <exception cref="CheckpointedProjectionUnavailableException">The record could not be read or is malformed.</exception>
    public async Task<CheckpointedProjectionRecord<TState>?> ReadAsync(
        string storeName,
        string key,
        CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        (CheckpointedProjectionRecord<TState>? record, _) = await CheckpointedProjectionStorage
            .LoadAsync<TState>(_store, storeName, key, cancellationToken)
            .ConfigureAwait(false);
        return record;
    }

    private static CheckpointedProjectionRecord<TState> MergeEvidence(
        CheckpointedProjectionRecord<TState> current,
        CheckpointedProjectionEvent delivery) {
        long expected = current.Health == CheckpointedProjectionHealth.Gapped
            ? current.Expected!.Value
            : current.LastContiguousSequence + 1;
        CheckpointedProjectionPendingEvidence[] pending = [
            .. current.Pending.Append(new CheckpointedProjectionPendingEvidence(delivery.Sequence, delivery.Evidence))
                .OrderBy(static p => p.Sequence),
        ];
        return current with {
            Health = CheckpointedProjectionHealth.Gapped,
            Expected = expected,
            RecoveryThrough = Math.Max(current.RecoveryThrough ?? 0, delivery.Sequence),
            Pending = pending,
            Version = current.Version + 1,
        };
    }

    private static void ValidateDelivery(CheckpointedProjectionEvent delivery) {
        ArgumentNullException.ThrowIfNull(delivery);
        if (delivery.Stream is null || !delivery.Stream.IsComplete || delivery.Evidence is null || !delivery.Evidence.IsComplete) {
            throw new ArgumentException("A checkpointed delivery requires a complete stream and event identity.", nameof(delivery));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(delivery.Sequence, 1);
    }

    private async Task<CheckpointedProjectionCorruptionReason?> VerifyOlderAsync(
        string storeName,
        string key,
        CheckpointedProjectionEvent delivery,
        CancellationToken cancellationToken) {
        if (_history is null) {
            CheckpointedProjectionLog.Unavailable(_logger, storeName, key, CheckpointedProjectionUnavailableReason.HistoryUnavailable);
            throw new CheckpointedProjectionUnavailableException(CheckpointedProjectionUnavailableReason.HistoryUnavailable);
        }

        CheckpointedProjectionEvent? authoritative = await _history
            .ReadAtAsync(delivery.Stream, delivery.Sequence, cancellationToken)
            .ConfigureAwait(false);
        if (authoritative is null) {
            return CheckpointedProjectionCorruptionReason.HistoryMissing;
        }

        return authoritative.Evidence == delivery.Evidence
            ? null
            : CheckpointedProjectionCorruptionReason.HistoryIdentityMismatch;
    }
}
