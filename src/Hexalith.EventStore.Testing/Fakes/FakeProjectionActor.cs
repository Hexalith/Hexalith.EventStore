
using System.Collections.Concurrent;
using System.Text.Json;

using Hexalith.EventStore.Server.Actors;

namespace Hexalith.EventStore.Testing.Fakes;

/// <summary>
/// Test double for IProjectionActor. Records invocations and returns
/// configurable results or throws configurable exceptions.
/// </summary>
public class FakeProjectionActor : IProjectionActor {
    private static readonly byte[] _defaultPayloadBytes = JsonSerializer.SerializeToUtf8Bytes(JsonDocument.Parse("{}").RootElement);
    private readonly ConcurrentQueue<QueryEnvelope> _receivedEnvelopes = new();

    /// <summary>Gets the list of received envelopes for assertion.</summary>
    public IReadOnlyCollection<QueryEnvelope> ReceivedEnvelopes
        => [.. _receivedEnvelopes];

    /// <summary>Gets or sets the result to return from QueryAsync.</summary>
    public QueryResult? ConfiguredResult { get; set; }

    /// <summary>Gets or sets the exception to throw from QueryAsync.</summary>
    public Exception? ConfiguredException { get; set; }

    /// <summary>Gets the number of queries received.</summary>
    public int QueryCount => _receivedEnvelopes.Count;

    /// <inheritdoc/>
    public Task<QueryResult> QueryAsync(QueryEnvelope envelope) {
        ArgumentNullException.ThrowIfNull(envelope);
        _receivedEnvelopes.Enqueue(envelope);

        if (ConfiguredException is not null) {
            throw ConfiguredException;
        }

        return Task.FromResult(
            ConfiguredResult
            ?? new QueryResult(true, _defaultPayloadBytes));
    }
}
