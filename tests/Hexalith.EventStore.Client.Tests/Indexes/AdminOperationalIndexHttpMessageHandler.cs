namespace Hexalith.EventStore.Client.Tests.Indexes;

/// <summary>Returns a fresh configured response for operational-index hosted-service tests.</summary>
internal sealed class AdminOperationalIndexHttpMessageHandler : HttpMessageHandler {
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

    /// <summary>Creates a handler that ignores the request and returns a factory response.</summary>
    /// <param name="responseFactory">Builds the HTTP response.</param>
    internal AdminOperationalIndexHttpMessageHandler(Func<HttpResponseMessage> responseFactory)
        : this(_ => responseFactory()) {
        ArgumentNullException.ThrowIfNull(responseFactory);
    }

    /// <summary>Creates a handler that can vary the response by request.</summary>
    /// <param name="responseFactory">Builds the HTTP response from the outgoing request.</param>
    internal AdminOperationalIndexHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) {
        ArgumentNullException.ThrowIfNull(responseFactory);
        _responseFactory = responseFactory;
    }

    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) =>
        Task.FromResult(_responseFactory(request));
}
