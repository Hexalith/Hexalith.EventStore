namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>Cancels the underlying actor HTTP request when the fixture fences work.</summary>
/// <param name="runToken">The fixture's real cancellation token.</param>
/// <param name="inner">An optional isolated test transport.</param>
internal sealed class RunCancellationHandler(CancellationToken runToken, HttpMessageHandler? inner = null)
    : DelegatingHandler(inner ?? new HttpClientHandler())
{
    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Remove("dapr-api-token");
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, runToken);
        HttpResponseMessage response = await base.SendAsync(request, linked.Token);
        try
        {
            // Keep run cancellation alive through the body, before HttpClient resumes
            // its default buffering with the original caller token.
            await response.Content.LoadIntoBufferAsync(linked.Token);
            return response;
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }
}
