namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Observes each actor transport SendAsync attempt while preserving streaming content and cancellation.</summary>
internal sealed class LiveActorSendHandler(LiveActorSendCounter counter) : DelegatingHandler(new HttpClientHandler())
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        counter.Observe(request);
        return base.SendAsync(request, cancellationToken);
    }
}
