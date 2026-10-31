namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>A deterministic HTTP boundary for actor serialization/cancellation checks.</summary>
/// <param name="callback">The request observer.</param>
internal sealed class CallbackHttpHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback) : HttpMessageHandler
{
    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => callback(request, cancellationToken);
}
