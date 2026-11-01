using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Client.Projections.Checkpointed;

/// <summary>
/// Reads bounded ranges of authoritative stream history for checkpointed projection verification and replay.
/// </summary>
/// <remarks>
/// Uses the gateway range read (<see cref="IEventStoreGatewayClient.ReadStreamAsync"/> with
/// <see cref="StreamReadRequest.FromSequence"/> and <see cref="StreamReadRequest.ToSequence"/>) rather than a
/// whole-stream read. Every gateway failure surfaces as a retryable
/// <see cref="CheckpointedProjectionUnavailableException"/>; a sequence the gateway does not return is reported
/// by its absence so callers can classify missing authoritative history.
/// </remarks>
public sealed class CheckpointedProjectionHistory {
    private readonly IEventStoreGatewayClient _gateway;
    private readonly int _pageSize;
    private readonly int _maxPages;

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckpointedProjectionHistory"/> class.
    /// </summary>
    /// <param name="gateway">The EventStore gateway client.</param>
    /// <param name="options">Optional paging options.</param>
    public CheckpointedProjectionHistory(IEventStoreGatewayClient gateway, CheckpointedProjectionOptions? options = null) {
        ArgumentNullException.ThrowIfNull(gateway);
        options ??= new CheckpointedProjectionOptions();
        options.Validate();
        _gateway = gateway;
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
    /// <returns>The returned events in ascending sequence order; absent sequences are omitted.</returns>
    /// <exception cref="CheckpointedProjectionUnavailableException">The range could not be read.</exception>
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

        SortedDictionary<long, CheckpointedProjectionEvent> events = [];
        long cursor = fromSequence - 1;
        for (int page = 0; page < _maxPages; page++) {
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
                result = await _gateway.ReadStreamAsync(request, cancellationToken).ConfigureAwait(false);
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

            long highest = cursor;
            foreach (StreamReadEvent item in result.Events) {
                if (item is null || item.SequenceNumber <= cursor || item.SequenceNumber > toSequence) {
                    continue;
                }

                if (events.TryGetValue(item.SequenceNumber, out CheckpointedProjectionEvent? existing)) {
                    CheckpointedProjectionEvent duplicate = CheckpointedProjectionEvent.FromHistory(stream, item);
                    if (existing.Evidence != duplicate.Evidence) {
                        // Two different events at one sequence cannot be authoritative; never pick one.
                        throw new CheckpointedProjectionUnavailableException(CheckpointedProjectionUnavailableReason.HistoryUnavailable);
                    }

                    continue;
                }

                events.Add(item.SequenceNumber, CheckpointedProjectionEvent.FromHistory(stream, item));
                highest = Math.Max(highest, item.SequenceNumber);
            }

            if (highest == cursor || highest >= toSequence || !result.Metadata.IsTruncated) {
                return [.. events.Values];
            }

            cursor = highest;
        }

        throw new CheckpointedProjectionUnavailableException(CheckpointedProjectionUnavailableReason.HistoryUnavailable);
    }

    /// <summary>
    /// Reads the single authoritative event at <paramref name="sequence"/>.
    /// </summary>
    /// <param name="stream">The authoritative stream.</param>
    /// <param name="sequence">The sequence to read.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The event, or <see langword="null"/> when authoritative history has no event at that sequence.</returns>
    /// <exception cref="CheckpointedProjectionUnavailableException">The sequence could not be read.</exception>
    public async Task<CheckpointedProjectionEvent?> ReadAtAsync(
        CheckpointedProjectionStream stream,
        long sequence,
        CancellationToken cancellationToken = default) {
        IReadOnlyList<CheckpointedProjectionEvent> events = await ReadRangeAsync(stream, sequence, sequence, cancellationToken)
            .ConfigureAwait(false);
        return events.Count == 1 && events[0].Sequence == sequence ? events[0] : null;
    }
}
