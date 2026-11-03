namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Records the final owned metadata frame after real SDK catalog admission.</summary>
internal sealed class HeldMetadataTestTransport(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
{
    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => send(request, cancellationToken);
}
