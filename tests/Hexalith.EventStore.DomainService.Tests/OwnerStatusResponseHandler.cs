using System.Net;
namespace Hexalith.EventStore.DomainService.Tests;
internal sealed class OwnerStatusResponseHandler(HttpStatusCode status) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(new HttpResponseMessage(status));
}
