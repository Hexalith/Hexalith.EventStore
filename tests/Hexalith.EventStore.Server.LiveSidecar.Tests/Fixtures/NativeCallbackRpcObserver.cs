namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Counts the real owned HTTP/2 publish RPC without replacing its transport, payload or result.</summary>
internal sealed class NativeCallbackRpcObserver : DelegatingHandler
{
    private int _publishCalls;
    internal int PublishCalls => Volatile.Read(ref _publishCalls);

    internal NativeCallbackRpcObserver() => InnerHandler = new SocketsHttpHandler { EnableMultipleHttp2Connections = true };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri?.AbsolutePath.EndsWith("/PublishEvent", StringComparison.Ordinal) == true) Interlocked.Increment(ref _publishCalls);
        return base.SendAsync(request, cancellationToken);
    }
}
