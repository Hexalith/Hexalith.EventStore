using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;

namespace Hexalith.EventStore.Testing.Fakes;

/// <summary>
/// Test double for <see cref="IEventStoreGatewayClient"/>.
/// </summary>
public sealed class FakeEventStoreGatewayClient : IEventStoreGatewayClient {
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly ConcurrentQueue<SubmitCommandRequest> _submittedCommands = new();
    private readonly ConcurrentQueue<SubmittedQuery> _submittedQueries = new();

    /// <summary>
    /// Gets the command requests submitted to the fake.
    /// </summary>
    public IReadOnlyCollection<SubmitCommandRequest> SubmittedCommands => [.. _submittedCommands];

    /// <summary>
    /// Gets the query requests submitted to the fake.
    /// </summary>
    public IReadOnlyCollection<SubmittedQuery> SubmittedQueries => [.. _submittedQueries];

    /// <summary>
    /// Gets or sets the command response returned by the fake.
    /// </summary>
    public SubmitCommandResponse CommandResponse { get; set; } = new("test-correlation-id");

    /// <summary>
    /// Gets or sets the query result returned by the fake.
    /// </summary>
    public EventStoreQueryResult QueryResult { get; set; }
        = new("test-correlation-id", JsonSerializer.SerializeToElement(new { }, JsonOptions), IsNotModified: false, ETag: null);

    /// <summary>
    /// Gets or sets the exception thrown for command submissions.
    /// </summary>
    public EventStoreGatewayException? CommandException { get; set; }

    /// <summary>
    /// Gets or sets the exception thrown for query submissions.
    /// </summary>
    public EventStoreGatewayException? QueryException { get; set; }

    /// <inheritdoc />
    public Task<SubmitCommandResponse> SubmitCommandAsync(
        SubmitCommandRequest request,
        CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        _submittedCommands.Enqueue(request);

        if (CommandException is not null) {
            throw CommandException;
        }

        return Task.FromResult(CommandResponse);
    }

    /// <inheritdoc />
    public Task<EventStoreQueryResult> SubmitQueryAsync(
        SubmitQueryRequest request,
        string? ifNoneMatch = null,
        CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        _submittedQueries.Enqueue(new SubmittedQuery(request, ifNoneMatch));

        if (QueryException is not null) {
            throw QueryException;
        }

        return Task.FromResult(QueryResult);
    }

    /// <inheritdoc />
    public async Task<EventStoreQueryResult<T>> SubmitQueryAsync<T>(
        SubmitQueryRequest request,
        string? ifNoneMatch = null,
        CancellationToken cancellationToken = default) {
        EventStoreQueryResult result = await SubmitQueryAsync(request, ifNoneMatch, cancellationToken)
            .ConfigureAwait(false);

        if (result.IsNotModified) {
            return new EventStoreQueryResult<T>(result.CorrelationId, default, IsNotModified: true, result.ETag);
        }

        T? payload = result.Payload.HasValue
            ? result.Payload.Value.Deserialize<T>(JsonOptions)
            : default;
        return new EventStoreQueryResult<T>(result.CorrelationId, payload, IsNotModified: false, result.ETag);
    }
}

/// <summary>
/// Captures a query submitted to <see cref="FakeEventStoreGatewayClient"/>.
/// </summary>
/// <param name="Request">The query request.</param>
/// <param name="IfNoneMatch">The supplied conditional ETag value.</param>
public sealed record SubmittedQuery(SubmitQueryRequest Request, string? IfNoneMatch);
