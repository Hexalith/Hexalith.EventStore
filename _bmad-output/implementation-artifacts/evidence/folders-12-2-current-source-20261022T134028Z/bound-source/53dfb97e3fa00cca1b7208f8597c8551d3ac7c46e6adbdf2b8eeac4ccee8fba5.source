namespace Hexalith.EventStore.Server.Tests.Queries;

internal sealed class QueryInvokerResponseHandler(Func<HttpResponseMessage> response) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(response());
}
