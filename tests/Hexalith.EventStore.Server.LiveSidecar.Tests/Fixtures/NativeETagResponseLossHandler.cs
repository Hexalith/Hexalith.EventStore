namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Loses exactly one real successful owned ETag actor response after its persisted method completes.</summary>
internal sealed class NativeETagResponseLossHandler(string endpoint) : DelegatingHandler(new HttpClientHandler())
{
    private int _armed;
    private int _completed;
    internal int CompletedResponses => Volatile.Read(ref _completed);
    internal void Arm() => Interlocked.Exchange(ref _armed, 1);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        bool selected = request.Method == HttpMethod.Put && request.RequestUri?.GetLeftPart(UriPartial.Authority) == endpoint.TrimEnd('/')
            && Uri.UnescapeDataString(request.RequestUri.AbsolutePath) == "/v1.0/actors/ETagActor/counter:tenant-a/method/RegenerateAsync"
            && Volatile.Read(ref _armed) == 1 && Interlocked.Exchange(ref _armed, 0) == 1;
        HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
        if (!selected) return response;
        try
        {
            response.EnsureSuccessStatusCode();
            if (response.Headers.Contains("X-DaprErrorResponseHeader")) throw new InvalidOperationException("The actual ETag actor failed instead of completing its persisted turn.");
            _ = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            Interlocked.Increment(ref _completed);
        }
        finally { response.Dispose(); }
        throw new HttpRequestException("The owned ETag response was lost after actual remote completion.");
    }
}
