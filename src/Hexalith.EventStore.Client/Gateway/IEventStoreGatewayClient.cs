using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Client.Gateway;

/// <summary>
/// High-level HTTP client for the EventStore command and query gateway.
/// </summary>
public interface IEventStoreGatewayClient {
    /// <summary>
    /// Submits a command through <c>POST /api/v1/commands</c>.
    /// </summary>
    Task<SubmitCommandResponse> SubmitCommandAsync(
        SubmitCommandRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a command's recorded status through <c>GET /api/v1/commands/status/{messageId}</c>.
    /// </summary>
    /// <param name="messageId">The command message identifier returned on the submission receipt.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The recorded status, or <c>null</c> when no status exists for the identifier.</returns>
    /// <remarks>
    /// The default preserves compatibility with gateway implementations created before status reads were added and
    /// fails closed by reporting no recorded status.
    /// </remarks>
    Task<CommandStatusQueryResponse?> GetCommandStatusAsync(
        string messageId,
        CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<CommandStatusQueryResponse?>(null);
    }

    /// <summary>
    /// Submits a query through <c>POST /api/v1/queries</c>.
    /// </summary>
    Task<EventStoreQueryResult> SubmitQueryAsync(
        SubmitQueryRequest request,
        string? ifNoneMatch = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits a query and deserializes the returned payload.
    /// </summary>
    Task<EventStoreQueryResult<T>> SubmitQueryAsync<T>(
        SubmitQueryRequest request,
        string? ifNoneMatch = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a public stream/replay page through <c>POST /api/v1/streams/read</c>.
    /// </summary>
    Task<StreamReadPage> ReadStreamAsync(
        StreamReadRequest request,
        CancellationToken cancellationToken = default);
}
