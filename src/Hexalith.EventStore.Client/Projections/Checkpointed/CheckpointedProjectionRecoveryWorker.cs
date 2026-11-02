using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hexalith.EventStore.Client.Projections.Checkpointed;

/// <summary>
/// Restores a gapped checkpointed projection by replaying authoritative history under a fenced lease.
/// </summary>
/// <typeparam name="TState">The projection state type.</typeparam>
/// <remarks>
/// <para>
/// The worker compare-and-swaps a lease (fencing token plus record version) into the record, replays the
/// authoritative range from the immutable <c>Expected</c> sequence through the current recovery horizon with the
/// domain transition, and validates every pending evidence entry against that history. It restores
/// <see cref="CheckpointedProjectionHealth.Healthy"/> only with a final compare-and-swap proving that its lease,
/// checkpoint, and horizon are unchanged and that no unvalidated pending evidence remains; a concurrently
/// extended horizon is replayed before trying again. An expired or superseded worker never writes.
/// </para>
/// <para>
/// Missing or contradicting authoritative history is persisted as terminal corruption. Authorization never
/// calls this worker; only deliveries (and their redeliveries) drive recovery.
/// </para>
/// </remarks>
public sealed class CheckpointedProjectionRecoveryWorker<TState>
    where TState : class {
    private const int MaxHorizonExtensions = 64;

    private readonly IReadModelStore _store;
    private readonly ICheckpointedProjectionTransition<TState> _transition;
    private readonly CheckpointedProjectionHistory? _history;
    private readonly CheckpointedProjectionOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckpointedProjectionRecoveryWorker{TState}"/> class.
    /// </summary>
    /// <param name="store">The read-model store providing ETag compare-and-swap.</param>
    /// <param name="transition">The domain-owned pure transition.</param>
    /// <param name="history">The authoritative range reader; <see langword="null"/> makes recovery unavailable.</param>
    /// <param name="options">The recovery options.</param>
    /// <param name="timeProvider">The lease clock.</param>
    /// <param name="logger">The logger.</param>
    public CheckpointedProjectionRecoveryWorker(
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
        _timeProvider = timeProvider ?? TimeProvider.System;
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>
    /// Recovers the record at <paramref name="key"/> when it is gapped.
    /// </summary>
    /// <param name="storeName">The read-model state-store component name.</param>
    /// <param name="key">The record key.</param>
    /// <param name="stream">The authoritative stream the record follows.</param>
    /// <param name="cancellationToken">A cancellation token. Cancellation never releases a held lease.</param>
    /// <returns>
    /// <see cref="CheckpointedProjectionOutcome.Recovered"/> when the record is (or became) healthy, or
    /// <see cref="CheckpointedProjectionOutcome.Corrupt"/> when it is terminally corrupt.
    /// </returns>
    /// <exception cref="CheckpointedProjectionUnavailableException">
    /// Recovery could not complete (including when the record is absent, or another invocation holds an unexpired
    /// lease); retry later.
    /// </exception>
    public async Task<CheckpointedProjectionResult<TState>> RecoverAsync(
        string storeName,
        string key,
        CheckpointedProjectionStream stream,
        CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.IsComplete) {
            throw new ArgumentException("The authoritative stream identity is incomplete.", nameof(stream));
        }

        // Every invocation owns a distinct lease identity, so two recoveries in one process never supersede each
        // other: the later one finds the earlier lease held and stays retryable.
        string leaseOwner = _options.WorkerId + "/" + Guid.NewGuid().ToString("N");
        (CheckpointedProjectionResult<TState>? settled, CheckpointedProjectionRecord<TState>? leased) = await AcquireLeaseAsync(
            storeName,
            key,
            leaseOwner,
            cancellationToken).ConfigureAwait(false);
        if (settled is not null) {
            return settled;
        }

        long fencingToken = leased!.Lease!.FencingToken;
        try {
            return await ReplayAndCommitAsync(storeName, key, stream, leased, cancellationToken).ConfigureAwait(false);
        }
        catch (CheckpointedProjectionUnavailableException exception)
            when (exception.Reason != CheckpointedProjectionUnavailableReason.RecoveryLeaseLost) {
            // Every failure except a lost lease releases the lease so another delivery can retry immediately;
            // a lost lease belongs to a newer invocation and is never touched.
            CheckpointedProjectionLog.Unavailable(_logger, storeName, key, exception.Reason);
            await TryReleaseLeaseAsync(storeName, key, leaseOwner, fencingToken, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Applies the domain transition, classifying a transition failure as retryable unavailability.
    /// </summary>
    /// <param name="transition">The domain transition.</param>
    /// <param name="current">The current state.</param>
    /// <param name="event">The contiguous event.</param>
    /// <returns>The next state, or <see langword="null"/> when the stream still has no materialized state.</returns>
    internal static TState? ApplyTransition(
        ICheckpointedProjectionTransition<TState> transition,
        TState? current,
        CheckpointedProjectionEvent @event) {
        TState? next;
        try {
            next = transition.Apply(current, @event);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception exception) {
            throw new CheckpointedProjectionUnavailableException(CheckpointedProjectionUnavailableReason.TransitionFailed, exception);
        }

        return next;
    }

    private static CheckpointedProjectionResult<TState> Recovered(CheckpointedProjectionRecord<TState> record)
        => new(CheckpointedProjectionOutcome.Recovered, record);

    private static CheckpointedProjectionResult<TState> Corrupt(CheckpointedProjectionRecord<TState> record)
        => new(CheckpointedProjectionOutcome.Corrupt, record);

    private async Task<(CheckpointedProjectionResult<TState>? Settled, CheckpointedProjectionRecord<TState>? Leased)> AcquireLeaseAsync(
        string storeName,
        string key,
        string leaseOwner,
        CancellationToken cancellationToken) {
        for (int attempt = 1; attempt <= _options.MaxAttempts; attempt++) {
            cancellationToken.ThrowIfCancellationRequested();
            (CheckpointedProjectionRecord<TState>? current, string etag) = await CheckpointedProjectionStorage
                .LoadAsync<TState>(_store, storeName, key, cancellationToken)
                .ConfigureAwait(false);
            if (current is null) {
                // The pending delivery committed evidence to this record; its disappearance proves nothing.
                CheckpointedProjectionLog.Unavailable(_logger, storeName, key, CheckpointedProjectionUnavailableReason.RecordMissing);
                throw new CheckpointedProjectionUnavailableException(CheckpointedProjectionUnavailableReason.RecordMissing);
            }

            if (current.Health == CheckpointedProjectionHealth.Healthy) {
                return (Recovered(current), null);
            }

            if (current.Health == CheckpointedProjectionHealth.Corrupt) {
                return (Corrupt(current), null);
            }

            if (_history is null) {
                CheckpointedProjectionLog.Unavailable(_logger, storeName, key, CheckpointedProjectionUnavailableReason.HistoryUnavailable);
                throw new CheckpointedProjectionUnavailableException(CheckpointedProjectionUnavailableReason.HistoryUnavailable);
            }

            DateTimeOffset now = _timeProvider.GetUtcNow();
            if (current.Lease is { } held && held.ExpiresAt > now) {
                CheckpointedProjectionLog.Unavailable(_logger, storeName, key, CheckpointedProjectionUnavailableReason.RecoveryLeaseHeld);
                throw new CheckpointedProjectionUnavailableException(CheckpointedProjectionUnavailableReason.RecoveryLeaseHeld);
            }

            long token = current.LastFencingToken + 1;
            long version = current.Version + 1;
            CheckpointedProjectionRecord<TState> leased = current with {
                Lease = new CheckpointedProjectionLease(leaseOwner, token, version, now + _options.LeaseDuration),
                LastFencingToken = token,
                Version = version,
            };
            if (await CheckpointedProjectionStorage.TrySaveAsync(_store, storeName, key, leased, etag, cancellationToken).ConfigureAwait(false)) {
                return (null, leased);
            }

            CheckpointedProjectionLog.ConcurrencyConflict(_logger, storeName, key, attempt, _options.MaxAttempts);
        }

        CheckpointedProjectionLog.Unavailable(_logger, storeName, key, CheckpointedProjectionUnavailableReason.ConcurrencyExhausted);
        throw new CheckpointedProjectionUnavailableException(CheckpointedProjectionUnavailableReason.ConcurrencyExhausted);
    }

    private async Task<CheckpointedProjectionResult<TState>> ReplayAndCommitAsync(
        string storeName,
        string key,
        CheckpointedProjectionStream stream,
        CheckpointedProjectionRecord<TState> leased,
        CancellationToken cancellationToken) {
        CheckpointedProjectionLease lease = leased.Lease!;
        long baseline = leased.LastContiguousSequence;
        long expected = leased.Expected!.Value;
        TState? state = leased.State;
        long applied = baseline;
        long horizon = leased.RecoveryThrough!.Value;
        CheckpointedProjectionEvidence? lastAccepted = leased.LastAccepted;
        Dictionary<long, CheckpointedProjectionEvidence> replayed = [];
        int conflicts = 0;

        for (int round = 0; round <= MaxHorizonExtensions; round++) {
            if (horizon > applied) {
                IReadOnlyList<CheckpointedProjectionEvent> events = await _history!
                    .ReadRangeAsync(stream, applied + 1, horizon, cancellationToken)
                    .ConfigureAwait(false);
                Dictionary<long, CheckpointedProjectionEvent> bySequence = events.ToDictionary(static e => e.Sequence);
                for (long sequence = applied + 1; sequence <= horizon; sequence++) {
                    if (!bySequence.TryGetValue(sequence, out CheckpointedProjectionEvent? authoritative)) {
                        return await PersistCorruptAsync(storeName, key, lease, CheckpointedProjectionCorruptionReason.HistoryMissing, sequence, cancellationToken)
                            .ConfigureAwait(false);
                    }

                    state = ApplyTransition(_transition, state, authoritative);
                    replayed[sequence] = authoritative.Evidence;
                    lastAccepted = authoritative.Evidence;
                }

                applied = horizon;
            }

            cancellationToken.ThrowIfCancellationRequested();
            (CheckpointedProjectionRecord<TState>? current, string etag) = await CheckpointedProjectionStorage
                .LoadAsync<TState>(_store, storeName, key, cancellationToken)
                .ConfigureAwait(false);
            if (current is null) {
                throw LeaseLost(storeName, key);
            }

            if (current.Health == CheckpointedProjectionHealth.Corrupt) {
                return Corrupt(current);
            }

            if (current.Health == CheckpointedProjectionHealth.Healthy && current.LastContiguousSequence >= applied) {
                // Another worker already restored this horizon; never overwrite it.
                return Recovered(current);
            }

            if (current.Lease is not { } held
                || held.FencingToken != lease.FencingToken
                || !string.Equals(held.WorkerId, lease.WorkerId, StringComparison.Ordinal)
                || _timeProvider.GetUtcNow() >= held.ExpiresAt
                || current.Health != CheckpointedProjectionHealth.Gapped
                || current.LastContiguousSequence != baseline
                || current.Expected != expected) {
                throw LeaseLost(storeName, key);
            }

            foreach (CheckpointedProjectionPendingEvidence pending in current.Pending) {
                if (pending.Sequence <= applied && replayed[pending.Sequence] != pending.Evidence) {
                    return await PersistCorruptAsync(storeName, key, lease, CheckpointedProjectionCorruptionReason.RecoveryEvidenceMismatch, pending.Sequence, cancellationToken)
                        .ConfigureAwait(false);
                }
            }

            if (current.RecoveryThrough!.Value > applied) {
                // A concurrent delivery extended the horizon; replay through the enlarged horizon first.
                horizon = current.RecoveryThrough.Value;
                continue;
            }

            CheckpointedProjectionRecord<TState> healthy = current with {
                State = state,
                Health = CheckpointedProjectionHealth.Healthy,
                LastContiguousSequence = applied,
                LastAccepted = lastAccepted,
                Expected = null,
                RecoveryThrough = null,
                Pending = [],
                Lease = null,
                Version = current.Version + 1,
            };
            if (await CheckpointedProjectionStorage.TrySaveAsync(_store, storeName, key, healthy, etag, cancellationToken).ConfigureAwait(false)) {
                CheckpointedProjectionLog.Recovered(_logger, storeName, key, applied, lease.FencingToken);
                return Recovered(healthy);
            }

            CheckpointedProjectionLog.ConcurrencyConflict(_logger, storeName, key, ++conflicts, _options.MaxAttempts);
            if (conflicts >= _options.MaxAttempts) {
                break;
            }
        }

        CheckpointedProjectionLog.Unavailable(_logger, storeName, key, CheckpointedProjectionUnavailableReason.ConcurrencyExhausted);
        throw new CheckpointedProjectionUnavailableException(CheckpointedProjectionUnavailableReason.ConcurrencyExhausted);
    }

    private async Task<CheckpointedProjectionResult<TState>> PersistCorruptAsync(
        string storeName,
        string key,
        CheckpointedProjectionLease lease,
        CheckpointedProjectionCorruptionReason reason,
        long sequence,
        CancellationToken cancellationToken) {
        for (int attempt = 1; attempt <= _options.MaxAttempts; attempt++) {
            (CheckpointedProjectionRecord<TState>? current, string etag) = await CheckpointedProjectionStorage
                .LoadAsync<TState>(_store, storeName, key, cancellationToken)
                .ConfigureAwait(false);
            if (current?.Health == CheckpointedProjectionHealth.Corrupt) {
                return Corrupt(current);
            }

            if (current?.Lease is not { } held
                || held.FencingToken != lease.FencingToken
                || !string.Equals(held.WorkerId, lease.WorkerId, StringComparison.Ordinal)
                || _timeProvider.GetUtcNow() >= held.ExpiresAt) {
                throw LeaseLost(storeName, key);
            }

            CheckpointedProjectionRecord<TState> corrupt = CheckpointedProjectionStorage.MarkCorrupt(current, reason, sequence);
            if (await CheckpointedProjectionStorage.TrySaveAsync(_store, storeName, key, corrupt, etag, cancellationToken).ConfigureAwait(false)) {
                CheckpointedProjectionLog.Corrupted(_logger, storeName, key, sequence, reason);
                return Corrupt(corrupt);
            }

            CheckpointedProjectionLog.ConcurrencyConflict(_logger, storeName, key, attempt, _options.MaxAttempts);
        }

        throw new CheckpointedProjectionUnavailableException(CheckpointedProjectionUnavailableReason.ConcurrencyExhausted);
    }

    private async Task TryReleaseLeaseAsync(string storeName, string key, string leaseOwner, long fencingToken, CancellationToken cancellationToken) {
        if (cancellationToken.IsCancellationRequested) {
            return;
        }

        try {
            (CheckpointedProjectionRecord<TState>? current, string etag) = await CheckpointedProjectionStorage
                .LoadAsync<TState>(_store, storeName, key, cancellationToken)
                .ConfigureAwait(false);
            if (current?.Lease is { } held
                && held.FencingToken == fencingToken
                && string.Equals(held.WorkerId, leaseOwner, StringComparison.Ordinal)) {
                _ = await CheckpointedProjectionStorage
                    .TrySaveAsync(_store, storeName, key, current with { Lease = null, Version = current.Version + 1 }, etag, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (CheckpointedProjectionUnavailableException) {
            // Best effort: an unreleased lease simply expires.
        }
    }

    private CheckpointedProjectionUnavailableException LeaseLost(string storeName, string key) {
        CheckpointedProjectionLog.Unavailable(_logger, storeName, key, CheckpointedProjectionUnavailableReason.RecoveryLeaseLost);
        return new CheckpointedProjectionUnavailableException(CheckpointedProjectionUnavailableReason.RecoveryLeaseLost);
    }
}
