namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Counts actual Dapr actor remoting bodies over the real HTTP transport, without changing the actor codec.</summary>
internal sealed class ActorTransportByteObserver() : DelegatingHandler(new HttpClientHandler())
{
    internal long Requests { get; private set; }
    internal long RequestBodyBytes { get; private set; }
    internal long ResponseBodyBytes { get; private set; }

    internal void Reset()
    {
        Requests = 0;
        RequestBodyBytes = 0;
        ResponseBodyBytes = 0;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests++;
        if (request.Content is not null)
            RequestBodyBytes += (await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false)).LongLength;
        HttpResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        ResponseBodyBytes += (await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false)).LongLength;
        return response;
    }
}
