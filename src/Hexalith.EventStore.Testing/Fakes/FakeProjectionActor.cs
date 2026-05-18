
using System.Collections.Concurrent;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Queries;

namespace Hexalith.EventStore.Testing.Fakes;

/// <summary>
/// Test double for IProjectionActor. Records invocations and returns
/// configurable results or throws configurable exceptions.
/// </summary>
public class FakeProjectionActor : IProjectionActor {
    private static readonly byte[] _defaultPayloadBytes = JsonSerializer.SerializeToUtf8Bytes(JsonDocument.Parse("{}").RootElement);
    private readonly ConcurrentQueue<QueryEnvelope> _receivedEnvelopes = new();
    private readonly ConcurrentQueue<CancellationToken> _receivedCancellationTokens = new();

    /// <summary>Gets the list of received envelopes for assertion.</summary>
    public IReadOnlyCollection<QueryEnvelope> ReceivedEnvelopes
        => [.. _receivedEnvelopes];

    /// <summary>Gets the cancellation tokens received through the cancellation-aware test path.</summary>
    public IReadOnlyCollection<CancellationToken> ReceivedCancellationTokens
        => [.. _receivedCancellationTokens];

    /// <summary>Gets or sets the result to return from QueryAsync.</summary>
    public QueryResult? ConfiguredResult { get; set; }

    /// <summary>Gets or sets the exception to throw from QueryAsync.</summary>
    public Exception? ConfiguredException { get; set; }

    /// <summary>Gets the number of queries received.</summary>
    public int QueryCount => _receivedEnvelopes.Count;

    /// <inheritdoc/>
    public Task<QueryResult> QueryAsync(QueryEnvelope envelope) =>
        QueryAsync(envelope, CancellationToken.None);

    /// <summary>
    /// Test helper path that simulates cancellation-aware projection query execution.
    /// </summary>
    /// <param name="envelope">The query envelope.</param>
    /// <param name="cancellationToken">The cancellation token to record and observe.</param>
    /// <returns>The configured query result.</returns>
    public Task<QueryResult> QueryAsync(QueryEnvelope envelope, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(envelope);
        cancellationToken.ThrowIfCancellationRequested();
        _receivedEnvelopes.Enqueue(envelope);
        _receivedCancellationTokens.Enqueue(cancellationToken);

        if (ConfiguredException is not null) {
            throw ConfiguredException;
        }

        return Task.FromResult(
            ConfiguredResult
            ?? new QueryResult(true, _defaultPayloadBytes));
    }
}
