using System.Net;
namespace Hexalith.EventStore.Server.Tests.Streams;

internal sealed class NativeSourceMetadataHttpHandler(byte[] body) : HttpMessageHandler
{
    internal Uri? LastUri { get; private set; }
    internal string? ApiToken { get; private set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested(); LastUri = request.RequestUri;
        ApiToken = request.Headers.TryGetValues("dapr-api-token", out var values) ? values.Single() : null;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) });
    }
}
