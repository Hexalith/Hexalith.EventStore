using System.Net;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Returns controlled response content for support-safe diagnostic tests.</summary>
/// <param name="body">The response content preserved by the diagnostic handler.</param>
internal sealed class Oq8DiagnosticResponseHandler(string body) : HttpMessageHandler
{
    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent(body),
        });
}
