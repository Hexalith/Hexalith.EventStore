using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.RestApi.Generators.Tests;

internal sealed class FakeEventStoreGatewayClient : IEventStoreGatewayClient
{
    public Func<SubmitCommandRequest, CancellationToken, Task<SubmitCommandResponse>>? CommandHandler { get; set; }

    public Func<SubmitQueryRequest, string?, FakeEventStoreGatewayClient, Task<EventStoreQueryResult>>? QueryHandler { get; set; }

    public int CommandCallCount { get; private set; }

    public SubmitQueryRequest? LastQueryRequest { get; set; }

    public string? LastIfNoneMatch { get; set; }

    public Task<SubmitCommandResponse> SubmitCommandAsync(
        SubmitCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        CommandCallCount++;
        return CommandHandler is null
            ? Task.FromResult(new SubmitCommandResponse("01KTESTCOMMAND000000000000"))
            : CommandHandler(request, cancellationToken);
    }

    public Task<EventStoreQueryResult> SubmitQueryAsync(
        SubmitQueryRequest request,
        string? ifNoneMatch = null,
        CancellationToken cancellationToken = default)
        => QueryHandler is null
            ? Task.FromResult(new EventStoreQueryResult("01KTESTQUERY0000000000000", null, false, null))
            : QueryHandler(request, ifNoneMatch, this);

    public Task<EventStoreQueryResult<T>> SubmitQueryAsync<T>(
        SubmitQueryRequest request,
        string? ifNoneMatch = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<StreamReadPage> ReadStreamAsync(
        StreamReadRequest request,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}
