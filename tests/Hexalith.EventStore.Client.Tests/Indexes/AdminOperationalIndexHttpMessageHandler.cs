namespace Hexalith.EventStore.Client.Tests.Indexes;

/// <summary>Returns a fresh configured response for operational-index hosted-service tests.</summary>
internal sealed class AdminOperationalIndexHttpMessageHandler(Func<HttpResponseMessage> responseFactory) : HttpMessageHandler {
    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) =>
        Task.FromResult(responseFactory());
}
