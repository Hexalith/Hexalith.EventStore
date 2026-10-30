namespace Hexalith.EventStore.Testing;

/// <summary>Links every request to fixture disposal and prevents credential-bearing redirects.</summary>
internal sealed class TestEnvironmentRequestHandler(Uri endpoint, CancellationToken lifetime) : DelegatingHandler(new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false, UseProxy = false })
{
    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Uri target = request.RequestUri ?? throw new TestEnvironmentDescriptorException("HXT109", "A request URI is required.");
        if (target.Scheme != endpoint.Scheme || target.IdnHost != endpoint.IdnHost || target.Port != endpoint.Port || target.UserInfo.Length != 0)
        {
            throw new TestEnvironmentDescriptorException("HXT109", "Fixture requests must remain within their exact declared endpoint origin.");
        }

        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(lifetime, cancellationToken);
        return await base.SendAsync(request, linked.Token).ConfigureAwait(false);
    }
}
