using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Streams;

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
    private readonly ConcurrentQueue<StreamReadRequest> _submittedStreamReads = new();

    /// <summary>
    /// Gets the command requests submitted to the fake.
    /// </summary>
    public IReadOnlyCollection<SubmitCommandRequest> SubmittedCommands => [.. _submittedCommands];

    /// <summary>
    /// Gets the query requests submitted to the fake.
    /// </summary>
    public IReadOnlyCollection<SubmittedQuery> SubmittedQueries => [.. _submittedQueries];

    /// <summary>
    /// Gets the stream read requests submitted to the fake.
    /// </summary>
    public IReadOnlyCollection<StreamReadRequest> SubmittedStreamReads => [.. _submittedStreamReads];

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
    /// Gets or sets the stream read page returned by the fake.
    /// </summary>
    public StreamReadPage StreamReadPage { get; set; }
        = new("test-tenant", "test-domain", null, [], new StreamReadMetadata(0, null, null, 0, 0, false, null));

    /// <summary>
    /// Gets or sets the exception thrown for command submissions.
    /// </summary>
    public EventStoreGatewayException? CommandException { get; set; }

    /// <summary>
    /// Gets or sets the exception thrown for query submissions.
    /// </summary>
    public EventStoreGatewayException? QueryException { get; set; }

    /// <summary>
    /// Gets or sets the exception thrown for stream reads.
    /// </summary>
    public EventStoreGatewayException? StreamReadException { get; set; }

    /// <summary>
    /// Configures the fake to return a command accepted response.
    /// </summary>
    public FakeEventStoreGatewayClient ConfigureCommandAccepted(string correlationId) {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        CommandException = null;
        CommandResponse = new SubmitCommandResponse(correlationId);
        return this;
    }

    /// <summary>
    /// Configures the fake to throw a command gateway failure.
    /// </summary>
    public FakeEventStoreGatewayClient ConfigureCommandFailure(EventStoreGatewayException exception) {
        ArgumentNullException.ThrowIfNull(exception);
        CommandException = exception;
        return this;
    }

    /// <summary>
    /// Configures the fake to return a successful query result.
    /// </summary>
    public FakeEventStoreGatewayClient ConfigureQuerySuccess(
        JsonElement payload,
        string correlationId,
        string? eTag = null,
        QueryResponseMetadata? metadata = null) {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        QueryException = null;
        QueryResult = new EventStoreQueryResult(correlationId, payload, IsNotModified: false, eTag) {
            Metadata = metadata ?? new QueryResponseMetadata(ETag: eTag, IsNotModified: false),
        };
        return this;
    }

    /// <summary>
    /// Configures the fake to throw the public semantic query failure used by the HTTP client.
    /// </summary>
    public FakeEventStoreGatewayClient ConfigureQuerySemanticFailure(string correlationId, string errorMessage) {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
        QueryException = new EventStoreGatewayException(
            200,
            "Query semantic failure",
            detail: errorMessage,
            correlationId: correlationId);
        return this;
    }

    /// <summary>
    /// Configures the fake to throw a query gateway failure.
    /// </summary>
    public FakeEventStoreGatewayClient ConfigureQueryFailure(EventStoreGatewayException exception) {
        ArgumentNullException.ThrowIfNull(exception);
        QueryException = exception;
        return this;
    }

    /// <summary>
    /// Configures the fake to return a not-modified query result.
    /// </summary>
    /// <param name="eTag">The cache validator exposed by both the result and its metadata.</param>
    /// <param name="metadata">Optional metadata whose stable evidence is preserved while cache fields are normalized.</param>
    /// <returns>The configured fake.</returns>
    public FakeEventStoreGatewayClient ConfigureQueryNotModified(
        string? eTag = null,
        QueryResponseMetadata? metadata = null) {
        QueryException = null;
        QueryResult = new EventStoreQueryResult(null, null, IsNotModified: true, eTag) {
            Metadata = (metadata ?? new QueryResponseMetadata()) with {
                ETag = eTag,
                IsNotModified = true,
            },
        };
        return this;
    }

    /// <summary>
    /// Configures the fake to return a stream read page.
    /// </summary>
    public FakeEventStoreGatewayClient ConfigureStreamReadSuccess(StreamReadPage page) {
        ArgumentNullException.ThrowIfNull(page);
        StreamReadException = null;
        StreamReadPage = page;
        return this;
    }

    /// <summary>
    /// Configures the fake to throw a stream read gateway failure.
    /// </summary>
    public FakeEventStoreGatewayClient ConfigureStreamReadFailure(EventStoreGatewayException exception) {
        ArgumentNullException.ThrowIfNull(exception);
        StreamReadException = exception;
        return this;
    }

    /// <summary>
    /// Configures the fake to return an empty stream page.
    /// </summary>
    public FakeEventStoreGatewayClient ConfigureStreamReadEmpty(string tenant, string domain, string? aggregateId = null) {
        StreamReadException = null;
        StreamReadPage = new StreamReadPage(
            tenant,
            domain,
            aggregateId,
            [],
            new StreamReadMetadata(0, null, null, 0, 0, false, null));
        return this;
    }

    /// <summary>
    /// Configures the fake to return a page with a next-continuation token.
    /// </summary>
    public FakeEventStoreGatewayClient ConfigureStreamReadContinuation(StreamReadPage page, string nextContinuationToken) {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentException.ThrowIfNullOrWhiteSpace(nextContinuationToken);
        StreamReadException = null;
        long? lastSequenceReturned = page.Events.Count == 0
            ? null
            : page.Events.Max(e => e.SequenceNumber);
        long latestSequence = Math.Max(page.Metadata.LatestSequence, lastSequenceReturned ?? page.Metadata.FromSequence);
        StreamReadPage = page with {
            Metadata = page.Metadata with {
                LastSequenceReturned = lastSequenceReturned,
                LatestSequence = latestSequence,
                EventCount = page.Events.Count,
                IsTruncated = true,
                NextContinuationToken = new ReplayContinuationToken(nextContinuationToken),
            },
        };
        return this;
    }

    /// <summary>
    /// Configures the fake to throw an invalid range failure.
    /// </summary>
    public FakeEventStoreGatewayClient ConfigureStreamReadInvalidRange(string tenant)
        => ConfigureStreamReadFailure(CreateStreamFailure(400, "Bad Request", tenant, StreamReplayReasonCodes.InvalidRange));

    /// <summary>
    /// Configures the fake to throw an invalid continuation failure.
    /// </summary>
    public FakeEventStoreGatewayClient ConfigureStreamReadInvalidContinuation(string tenant)
        => ConfigureStreamReadFailure(CreateStreamFailure(400, "Bad Request", tenant, StreamReplayReasonCodes.InvalidContinuation));

    /// <summary>
    /// Configures the fake to throw an unauthorized tenant failure.
    /// </summary>
    public FakeEventStoreGatewayClient ConfigureStreamReadUnauthorizedTenant(string tenant)
        => ConfigureStreamReadFailure(CreateStreamFailure(403, "Forbidden", tenant, StreamReplayReasonCodes.UnauthorizedTenant));

    /// <summary>
    /// Configures the fake to throw a missing stream failure.
    /// </summary>
    public FakeEventStoreGatewayClient ConfigureStreamReadMissingStream(string tenant)
        => ConfigureStreamReadFailure(CreateStreamFailure(404, "Not Found", tenant, StreamReplayReasonCodes.MissingStream));

    /// <summary>
    /// Configures the fake to throw a checkpoint conflict failure.
    /// </summary>
    public FakeEventStoreGatewayClient ConfigureStreamReadCheckpointConflict(string tenant)
        => ConfigureStreamReadFailure(CreateStreamFailure(409, "Conflict", tenant, StreamReplayReasonCodes.CheckpointConflict));

    /// <summary>
    /// Configures the fake to throw a paused rebuild failure.
    /// </summary>
    public FakeEventStoreGatewayClient ConfigureStreamReadPausedRebuild(string tenant)
        => ConfigureStreamReadFailure(CreateStreamFailure(409, "Conflict", tenant, StreamReplayReasonCodes.RebuildPaused));

    /// <summary>
    /// Configures the fake to throw a canceled rebuild failure.
    /// </summary>
    public FakeEventStoreGatewayClient ConfigureStreamReadCanceledRebuild(string tenant)
        => ConfigureStreamReadFailure(CreateStreamFailure(409, "Conflict", tenant, StreamReplayReasonCodes.RebuildCanceled));

    /// <summary>
    /// Configures the fake to throw a service-unavailable failure.
    /// </summary>
    public FakeEventStoreGatewayClient ConfigureStreamReadUnavailable(string tenant)
        => ConfigureStreamReadFailure(CreateStreamFailure(503, "Service Unavailable", tenant, StreamReplayReasonCodes.ServiceUnavailable));

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
            return new EventStoreQueryResult<T>(result.CorrelationId, default, IsNotModified: true, result.ETag) {
                Metadata = result.Metadata ?? new QueryResponseMetadata(ETag: result.ETag, IsNotModified: true),
            };
        }

        T? payload;
        try {
            payload = result.Payload.HasValue
                ? result.Payload.Value.Deserialize<T>(JsonOptions)
                : default;
        }
        catch (JsonException ex) {
            throw new EventStoreGatewayException(
                200,
                "OK",
                detail: "Query response payload could not be deserialized.",
                innerException: ex);
        }

        return new EventStoreQueryResult<T>(result.CorrelationId, payload, IsNotModified: false, result.ETag) {
            Metadata = result.Metadata ?? new QueryResponseMetadata(ETag: result.ETag, IsNotModified: false),
        };
    }

    /// <inheritdoc />
    public Task<StreamReadPage> ReadStreamAsync(
        StreamReadRequest request,
        CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        _submittedStreamReads.Enqueue(request);

        if (StreamReadException is not null) {
            throw StreamReadException;
        }

        return Task.FromResult(StreamReadPage);
    }

    private static EventStoreGatewayException CreateStreamFailure(
        int statusCode,
        string title,
        string tenant,
        string reasonCode)
        => new(
            statusCode,
            title,
            type: $"https://hexalith.io/problems/stream-replay/{reasonCode}",
            detail: $"Stream replay failed with reason '{reasonCode}'.",
            tenantId: tenant,
            reasonCode: reasonCode);
}

/// <summary>
/// Captures a query submitted to <see cref="FakeEventStoreGatewayClient"/>.
/// </summary>
/// <param name="Request">The query request.</param>
/// <param name="IfNoneMatch">The supplied conditional ETag value.</param>
public sealed record SubmittedQuery(SubmitQueryRequest Request, string? IfNoneMatch);
