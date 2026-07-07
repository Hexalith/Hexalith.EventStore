using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Sample.Tests.SampleApi;

internal sealed class FakeEventStoreGatewayClient : IEventStoreGatewayClient
{
    public Func<SubmitCommandRequest, CancellationToken, Task<SubmitCommandResponse>>? CommandHandler { get; set; }

    public int CommandCallCount { get; private set; }

    public int QueryCallCount { get; private set; }

    public string? LastIfNoneMatch { get; private set; }

    public SubmitCommandRequest? LastCommandRequest { get; private set; }

    public SubmitQueryRequest? LastQueryRequest { get; private set; }

    public Func<SubmitQueryRequest, string?, CancellationToken, Task<EventStoreQueryResult>>? QueryHandler { get; set; }

    public Task<SubmitCommandResponse> SubmitCommandAsync(
        SubmitCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        CommandCallCount++;
        LastCommandRequest = request;
        return CommandHandler is null
            ? Task.FromResult(new SubmitCommandResponse("01KTESTCOMMANDSTATUS000000"))
            : CommandHandler(request, cancellationToken);
    }

    public Task<EventStoreQueryResult> SubmitQueryAsync(
        SubmitQueryRequest request,
        string? ifNoneMatch = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        QueryCallCount++;
        LastQueryRequest = request;
        LastIfNoneMatch = ifNoneMatch;
        return QueryHandler is null
            ? Task.FromResult(new EventStoreQueryResult("01KTESTQUERY0000000000000", null, IsNotModified: false, ETag: null))
            : QueryHandler(request, ifNoneMatch, cancellationToken);
    }

    public Task<EventStoreQueryResult<T>> SubmitQueryAsync<T>(
        SubmitQueryRequest request,
        string? ifNoneMatch = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The generated sample controller uses the untyped query gateway method.");

    public Task<StreamReadPage> ReadStreamAsync(
        StreamReadRequest request,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The generated sample controller does not read public streams.");
}
