using System.Net;
using System.Text;

namespace Hexalith.EventStore.Server.Tests.DomainServices;

/// <summary>Captures an actor's routed domain request and returns a bounded wire response.</summary>
internal sealed class VersionedActorResponseHandler : HttpMessageHandler
{
    private readonly Func<string, CancellationToken, Task<string>> _response;

    /// <summary>Creates a fixed response handler.</summary>
    internal VersionedActorResponseHandler(string responseJson)
        : this((_, _) => Task.FromResult(responseJson))
    {
    }

    /// <summary>Creates a handler that routes the captured request into a domain service.</summary>
    internal VersionedActorResponseHandler(Func<string, CancellationToken, Task<string>> response)
    {
        _response = response;
    }

    /// <summary>Gets the routed request JSON.</summary>
    internal string? RequestJson { get; private set; }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestJson = await request.Content!.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        string responseJson = await _response(RequestJson, cancellationToken).ConfigureAwait(false);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
        };
    }
}
