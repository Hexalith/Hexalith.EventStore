using System.Net;
using System.Text;

namespace Hexalith.EventStore.Server.Tests.DomainServices;

/// <summary>Captures an actor's routed domain request and returns a bounded wire response.</summary>
internal sealed class VersionedActorResponseHandler(string responseJson) : HttpMessageHandler
{
    /// <summary>Gets the routed request JSON.</summary>
    internal string? RequestJson { get; private set; }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestJson = await request.Content!.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
        };
    }
}
