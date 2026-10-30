namespace Hexalith.EventStore.Testing;

/// <summary>Links every request to fixture disposal and prevents credential-bearing redirects.</summary>
internal sealed class TestEnvironmentRequestHandler(Uri endpoint, CancellationToken lifetime, TimeSpan timeout) : DelegatingHandler(new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false, UseProxy = false })
{
    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Uri target = request.RequestUri ?? throw new TestEnvironmentDescriptorException("HXT109", "A request URI is required.");
        if (target.Scheme != endpoint.Scheme || target.IdnHost != endpoint.IdnHost || target.Port != endpoint.Port || target.UserInfo.Length != 0)
        {
            throw new TestEnvironmentDescriptorException("HXT109", "Fixture requests must remain within their exact declared endpoint origin.");
        }

        CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(lifetime, cancellationToken);
        linked.CancelAfter(timeout);
        try
        {
            HttpResponseMessage response = await base.SendAsync(request, linked.Token).ConfigureAwait(false);
            response.Content = new TestEnvironmentResponseContent(response.Content, linked);
            return response;
        }
        catch
        {
            linked.Dispose();
            throw;
        }
    }
}
