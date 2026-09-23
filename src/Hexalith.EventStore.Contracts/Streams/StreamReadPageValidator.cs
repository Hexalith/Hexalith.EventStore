namespace Hexalith.EventStore.Contracts.Streams;

/// <summary>
/// Validates a per-aggregate stream page before a recovery, replay, or rebuild reader advances its cursor.
/// </summary>
public static class StreamReadPageValidator
{
    /// <summary>
    /// Validates the returned stream identity, ordering, and page metadata, then returns the next exclusive
    /// <see cref="StreamReadRequest.FromSequence"/> value. A truncated page must advance the cursor.
    /// </summary>
    /// <param name="request">The exact per-aggregate read that produced the page.</param>
    /// <param name="page">The returned page.</param>
    /// <returns>The exclusive lower sequence bound to use for the next page.</returns>
    /// <exception cref="ArgumentException">The request does not identify one aggregate.</exception>
    /// <exception cref="InvalidOperationException">The page is outside the requested stream or cannot advance safely.</exception>
    public static long ValidateAndGetNextSequence(StreamReadRequest request, StreamReadPage page)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(page);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.AggregateId);

        if (!string.Equals(page.Tenant, request.Tenant, StringComparison.Ordinal)
            || !string.Equals(page.Domain, request.Domain, StringComparison.Ordinal)
            || !string.Equals(page.AggregateId, request.AggregateId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The stream page identity differs from the requested aggregate.");
        }

        StreamReadMetadata metadata = page.Metadata
            ?? throw new InvalidOperationException("The stream page has no read metadata.");
        IReadOnlyList<StreamReadEvent> events = page.Events
            ?? throw new InvalidOperationException("The stream page has no event collection.");
        if (metadata.FromSequence != request.FromSequence
            || metadata.ToSequence != request.ToSequence
            || metadata.EventCount != events.Count)
        {
            throw new InvalidOperationException("The stream page metadata differs from the requested range or event count.");
        }

        long last = request.FromSequence;
        foreach (StreamReadEvent streamEvent in events)
        {
            if (streamEvent is null
                || streamEvent.SequenceNumber <= last
                || (request.ToSequence is { } upperBound && streamEvent.SequenceNumber > upperBound))
            {
                throw new InvalidOperationException("The stream page contains an out-of-range or non-increasing event position.");
            }

            last = streamEvent.SequenceNumber;
        }

        if (metadata.LastSequenceReturned != (events.Count == 0 ? null : last)
            || (events.Count > 0 && metadata.LatestSequence < last)
            || (metadata.IsTruncated && events.Count == 0))
        {
            throw new InvalidOperationException("The stream page has an inconsistent or non-advancing continuation.");
        }

        return last;
    }
}
