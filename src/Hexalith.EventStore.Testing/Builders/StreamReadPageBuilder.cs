using Hexalith.Commons.UniqueIds;
using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Testing.Builders;

/// <summary>
/// Fluent builder for deterministic public stream read pages in downstream tests.
/// </summary>
public sealed class StreamReadPageBuilder {
    private readonly List<StreamReadEvent> _events = [];
    private string _tenant = "tenant-a";
    private string _domain = "domain-a";
    private string? _aggregateId = "aggregate-1";
    private long _fromSequence;
    private long? _toSequence;
    private long _latestSequence;
    private ReplayContinuationToken? _nextContinuationToken;
    // P20: optional deterministic-id factory so tests asserting exact MessageId/CorrelationId/
    // CausationId values pin determinism. When null, the builder generates fresh ULIDs per build.
    private Func<long, string>? _idFactory;

    /// <summary>
    /// Configures a deterministic id factory for MessageId/CorrelationId/CausationId.
    /// </summary>
    public StreamReadPageBuilder WithDeterministicIds(Func<long, string> idFactory) {
        ArgumentNullException.ThrowIfNull(idFactory);
        _idFactory = idFactory;
        return this;
    }

    /// <summary>
    /// Creates a builder with standard tenant/domain/aggregate values.
    /// </summary>
    public static StreamReadPageBuilder Create() => new();

    /// <summary>
    /// Sets the stream identity.
    /// </summary>
    public StreamReadPageBuilder ForStream(string tenant, string domain, string? aggregateId = null) {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        _tenant = tenant;
        _domain = domain;
        _aggregateId = aggregateId;
        return this;
    }

    /// <summary>
    /// Sets the requested sequence range.
    /// </summary>
    public StreamReadPageBuilder WithRange(long fromSequence, long? toSequence = null) {
        ArgumentOutOfRangeException.ThrowIfNegative(fromSequence);
        if (toSequence.HasValue && toSequence.Value < fromSequence) {
            throw new ArgumentOutOfRangeException(nameof(toSequence), "Upper sequence bound must be greater than or equal to the lower bound.");
        }

        _fromSequence = fromSequence;
        _toSequence = toSequence;
        return this;
    }

    /// <summary>
    /// Adds an event with deterministic metadata.
    /// </summary>
    public StreamReadPageBuilder AddEvent(long sequenceNumber, string eventTypeName = "TestEvent", byte[]? payload = null) {
        ArgumentOutOfRangeException.ThrowIfLessThan(sequenceNumber, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventTypeName);
        string messageId = _idFactory?.Invoke(sequenceNumber) ?? UniqueIdHelper.GenerateSortableUniqueStringId();
        string correlationId = _idFactory?.Invoke(sequenceNumber) ?? UniqueIdHelper.GenerateSortableUniqueStringId();
        string causationId = _idFactory?.Invoke(sequenceNumber) ?? UniqueIdHelper.GenerateSortableUniqueStringId();
        _events.Add(new StreamReadEvent(
            sequenceNumber,
            eventTypeName,
            payload ?? [],
            "json",
            1,
            messageId,
            correlationId,
            causationId,
            DateTimeOffset.UnixEpoch.AddSeconds(sequenceNumber),
            "user-1"));
        _latestSequence = Math.Max(_latestSequence, sequenceNumber);
        return this;
    }

    /// <summary>
    /// Adds an opaque continuation token to the page metadata.
    /// </summary>
    public StreamReadPageBuilder WithNextContinuation(string token) {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        _nextContinuationToken = new ReplayContinuationToken(token);
        return this;
    }

    /// <summary>
    /// Sets the latest known stream sequence.
    /// </summary>
    public StreamReadPageBuilder WithLatestSequence(long latestSequence) {
        ArgumentOutOfRangeException.ThrowIfNegative(latestSequence);
        _latestSequence = latestSequence;
        return this;
    }

    /// <summary>
    /// Builds the stream read page.
    /// </summary>
    public StreamReadPage Build() {
        long? lastSequenceReturned = _events.Count == 0 ? null : _events.Max(e => e.SequenceNumber);
        long latestSequence = Math.Max(_latestSequence, lastSequenceReturned ?? _fromSequence);
        return new StreamReadPage(
            _tenant,
            _domain,
            _aggregateId,
            [.. _events.OrderBy(e => e.SequenceNumber)],
            new StreamReadMetadata(
                _fromSequence,
                _toSequence,
                lastSequenceReturned,
                latestSequence,
                _events.Count,
                _nextContinuationToken is not null,
                _nextContinuationToken));
    }
}
