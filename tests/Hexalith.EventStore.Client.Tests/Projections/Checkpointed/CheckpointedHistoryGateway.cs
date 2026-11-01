using System.Text;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Subscriptions;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Client.Tests.Projections.Checkpointed;

/// <summary>An authoritative stream served through the gateway range-read contract.</summary>
internal sealed class CheckpointedHistoryGateway : IEventStoreGatewayClient {
    public const string Tenant = "system";
    public const string Domain = "checkpointed";
    public const string Aggregate = "aggregate-1";
    public const string EventType = "Checkpointed.TestEvent";

    private readonly SortedDictionary<long, StreamReadEvent> _events = [];
    private readonly Lock _gate = new();

    public bool Unavailable { get; set; }

    public int PageSize { get; set; } = 2;

    public Func<StreamReadRequest, CancellationToken, Task>? BeforeRead { get; set; }

    public List<StreamReadRequest> Requests { get; } = [];

    public static byte[] PayloadOf(long sequence, string? value = null) => Encoding.UTF8.GetBytes(value ?? $"value-{sequence}");

    public static string MessageIdOf(long sequence) => $"message-{sequence:D4}";

    public static string FingerprintOf(long sequence, string? value = null) => EventStorePayloadFingerprint.Compute(PayloadOf(sequence, value));

    public void Append(long sequence, string? value = null, string? messageId = null) {
        lock (_gate) {
            _events[sequence] = new StreamReadEvent(
                sequence,
                EventType,
                PayloadOf(sequence, value),
                "json",
                1,
                messageId ?? MessageIdOf(sequence),
                $"correlation-{sequence}",
                null,
                DateTimeOffset.UnixEpoch.AddMinutes(sequence),
                null);
        }
    }

    public void AppendRange(long from, long to) {
        for (long sequence = from; sequence <= to; sequence++) {
            Append(sequence);
        }
    }

    public void Remove(long sequence) {
        lock (_gate) {
            _ = _events.Remove(sequence);
        }
    }

    public async Task<StreamReadPage> ReadStreamAsync(StreamReadRequest request, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate) {
            Requests.Add(request);
        }

        if (BeforeRead is { } before) {
            await before(request, cancellationToken).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (Unavailable) {
            throw new EventStoreGatewayException(503, "Synthetic history outage");
        }

        lock (_gate) {
            long to = request.ToSequence ?? long.MaxValue;
            StreamReadEvent[] inRange = [.. _events.Values.Where(e => e.SequenceNumber > request.FromSequence && e.SequenceNumber <= to)];
            StreamReadEvent[] page = [.. inRange.Take(request.PageSize > 0 ? Math.Min(request.PageSize, PageSize) : PageSize)];
            long latest = _events.Count == 0 ? 0 : _events.Keys.Max();
            return new StreamReadPage(
                request.Tenant,
                request.Domain,
                request.AggregateId,
                page,
                new StreamReadMetadata(
                    request.FromSequence,
                    request.ToSequence,
                    page.Length == 0 ? null : page[^1].SequenceNumber,
                    latest,
                    page.Length,
                    inRange.Length > page.Length,
                    null));
        }
    }

    public Task<SubmitCommandResponse> SubmitCommandAsync(SubmitCommandRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<EventStoreQueryResult> SubmitQueryAsync(SubmitQueryRequest request, string? ifNoneMatch = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<EventStoreQueryResult<T>> SubmitQueryAsync<T>(SubmitQueryRequest request, string? ifNoneMatch = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}
