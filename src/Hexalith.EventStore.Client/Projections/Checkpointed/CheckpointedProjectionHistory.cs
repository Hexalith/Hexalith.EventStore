using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Streams;

using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.Client.Projections.Checkpointed;

/// <summary>
/// Reads bounded ranges of authoritative stream history for checkpointed projection verification and replay.
/// </summary>
/// <remarks>
/// <para>
/// Uses the gateway range read (<see cref="IEventStoreGatewayClient.ReadStreamAsync"/> with
/// <see cref="StreamReadRequest.FromSequence"/> and <see cref="StreamReadRequest.ToSequence"/>) rather than a
/// whole-stream read. Every gateway failure, a truncated page that makes no progress, and a stream head that has
/// not yet reached the requested range surface as a retryable <see cref="CheckpointedProjectionUnavailableException"/>.
/// Only a sequence the gateway omits while its head already covers the range is reported by its absence, so
/// callers classify it as missing authoritative history.
/// </para>
/// <para>
/// The scope-factory constructor resolves a fresh <see cref="IEventStoreGatewayClient"/> in its own service scope
/// for every range read, so a long-lived projection never holds one typed HTTP client for its whole lifetime.
/// </para>
/// </remarks>
public sealed class CheckpointedProjectionHistory {
    private readonly IEventStoreGatewayClient? _gateway;
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly int _pageSize;
    private readonly int _maxPages;

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckpointedProjectionHistory"/> class over one gateway client.
    /// </summary>
    /// <param name="gateway">The EventStore gateway client.</param>
    /// <param name="options">Optional paging options.</param>
    public CheckpointedProjectionHistory(IEventStoreGatewayClient gateway, CheckpointedProjectionOptions? options = null)
        : this(options) {
        ArgumentNullException.ThrowIfNull(gateway);
        _gateway = gateway;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckpointedProjectionHistory"/> class that resolves a gateway
    /// client in a new service scope for every range read.
    /// </summary>
    /// <param name="scopeFactory">The service scope factory resolving <see cref="IEventStoreGatewayClient"/>.</param>
    /// <param name="options">Optional paging options.</param>
    public CheckpointedProjectionHistory(IServiceScopeFactory scopeFactory, CheckpointedProjectionOptions? options = null)
        : this(options) {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        _scopeFactory = scopeFactory;
    }

    private CheckpointedProjectionHistory(CheckpointedProjectionOptions? options) {
        options ??= new CheckpointedProjectionOptions();
        options.Validate();
        _pageSize = options.HistoryPageSize;
        _maxPages = options.MaxHistoryPages;
    }

    /// <summary>
    /// Reads the authoritative events whose sequences are in the inclusive range
    /// [<paramref name="fromSequence"/>, <paramref name="toSequence"/>].
    /// </summary>
    /// <param name="stream">The authoritative stream.</param>
    /// <param name="fromSequence">The first sequence to read (inclusive, at least one).</param>
    /// <param name="toSequence">The last sequence to read (inclusive).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>
    /// The returned events in ascending sequence order. A sequence is omitted only when the stream head already
    /// covers <paramref name="toSequence"/> and the gateway returned no event for it.
    /// </returns>
    /// <exception cref="CheckpointedProjectionUnavailableException">The range could not be read completely yet.</exception>
    public async Task<IReadOnlyList<CheckpointedProjectionEvent>> ReadRangeAsync(
        CheckpointedProjectionStream stream,
        long fromSequence,
        long toSequence,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.IsComplete) {
            throw new ArgumentException("The authoritative stream identity is incomplete.", nameof(stream));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(fromSequence, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(toSequence, fromSequence);

        if (_gateway is not null) {
            return await ReadRangeAsync(_gateway, stream, fromSequence, toSequence, cancellationToken).ConfigureAwait(false);
        }

        AsyncServiceScope scope = _scopeFactory!.CreateAsyncScope();
        await using (scope.ConfigureAwait(false)) {
            IEventStoreGatewayClient gateway;
            try {
                gateway = scope.ServiceProvider.GetRequiredService<IEventStoreGatewayClient>();
            }
            catch (InvalidOperationException exception) {
                throw new CheckpointedProjectionUnavailableException(CheckpointedProjectionUnavailableReason.HistoryUnavailable, exception);
            }

            return await ReadRangeAsync(gateway, stream, fromSequence, toSequence, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Reads the single authoritative event at <paramref name="sequence"/>.
    /// </summary>
    /// <param name="stream">The authoritative stream.</param>
    /// <param name="sequence">The sequence to read.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The event, or <see langword="null"/> when authoritative history has no event at that sequence.</returns>
    /// <exception cref="CheckpointedProjectionUnavailableException">The sequence could not be read yet.</exception>
    public async Task<CheckpointedProjectionEvent?> ReadAtAsync(
        CheckpointedProjectionStream stream,
        long sequence,
        CancellationToken cancellationToken = default) {
        IReadOnlyList<CheckpointedProjectionEvent> events = await ReadRangeAsync(stream, sequence, sequence, cancellationToken)
            .ConfigureAwait(false);
        return events.Count == 1 && events[0].Sequence == sequence ? events[0] : null;
    }

    private async Task<IReadOnlyList<CheckpointedProjectionEvent>> ReadRangeAsync(
        IEventStoreGatewayClient gateway,
        CheckpointedProjectionStream stream,
        long fromSequence,
        long toSequence,
        CancellationToken cancellationToken) {
        SortedDictionary<long, CheckpointedProjectionEvent> events = [];
        long cursor = fromSequence - 1;
        long head = 0;
        bool complete = false;
        for (int page = 0; page < _maxPages && !complete; page++) {
            cancellationToken.ThrowIfCancellationRequested();
            var request = new StreamReadRequest(
                stream.TenantId,
                stream.Domain,
                stream.AggregateId,
                FromSequence: cursor,
                ToSequence: toSequence,
                PageSize: _pageSize);
            StreamReadPage result;
            try {
                result = await gateway.ReadStreamAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                throw;
            }
            catch (Exception exception) {
                throw new CheckpointedProjectionUnavailableException(
                    CheckpointedProjectionUnavailableReason.HistoryUnavailable,
                    exception);
            }

            if (result?.Events is null || result.Metadata is null) {
                throw new CheckpointedProjectionUnavailableException(CheckpointedProjectionUnavailableReason.HistoryUnavailable);
            }

            head = Math.Max(head, result.Metadata.LatestSequence);
            long highest = cursor;
            foreach (StreamReadEvent item in result.Events) {
                if (item is null || item.SequenceNumber <= cursor || item.SequenceNumber > toSequence) {
                    continue;
                }

                if (item.Payload is null) {
                    // A missing payload is not the empty payload: never fingerprint it into a false mismatch.
                    throw new CheckpointedProjectionUnavailableException(CheckpointedProjectionUnavailableReason.HistoryUnavailable);
                }

                CheckpointedProjectionEvent candidate = CheckpointedProjectionEvent.FromHistory(stream, item);
                if (events.TryGetValue(item.SequenceNumber, out CheckpointedProjectionEvent? existing)) {
                    if (existing.Evidence != candidate.Evidence) {
                        // Two different events at one sequence cannot be authoritative; never pick one.
                        throw new CheckpointedProjectionUnavailableException(CheckpointedProjectionUnavailableReason.HistoryUnavailable);
                    }

                    continue;
                }

                events.Add(item.SequenceNumber, candidate);
                highest = Math.Max(highest, item.SequenceNumber);
            }

            if (highest >= toSequence) {
                return [.. events.Values];
            }

            if (highest == cursor) {
                if (result.Metadata.IsTruncated) {
                    // A truncated page that returned nothing new proves nothing about the rest of the range.
                    throw new CheckpointedProjectionUnavailableException(CheckpointedProjectionUnavailableReason.HistoryUnavailable);
                }

                complete = true;
            }
            else if (!result.Metadata.IsTruncated) {
                complete = true;
            }
            else {
                cursor = highest;
            }
        }

        if (!complete || head < toSequence) {
            // Either the page budget ran out, or the authoritative head has not reached the range yet (for example
            // a read model lagging delivery): absent sequences are not proven missing, so retry instead.
            throw new CheckpointedProjectionUnavailableException(CheckpointedProjectionUnavailableReason.HistoryUnavailable);
        }

        return [.. events.Values];
    }
}
