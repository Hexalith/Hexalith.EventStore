namespace Hexalith.EventStore.Admin.Cli.Tests.Client;

/// <summary>
/// Test helper that returns responses from a queue on successive calls.
/// Supports both normal responses and exception-throwing entries via lambda factories.
/// When the queue is exhausted, the last factory is replayed indefinitely.
/// </summary>
internal class QueuedMockHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpResponseMessage>> _responseFactories;
    private Func<HttpResponseMessage>? _lastFactory;

    public QueuedMockHttpMessageHandler(params Func<HttpResponseMessage>[] factories)
    {
        _responseFactories = new Queue<Func<HttpResponseMessage>>(factories);
    }

    public QueuedMockHttpMessageHandler(params HttpResponseMessage[] responses)
        : this(responses.Select<HttpResponseMessage, Func<HttpResponseMessage>>(r => () => r).ToArray())
    {
    }

    public int CallCount { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        Func<HttpResponseMessage> factory;
        if (_responseFactories.Count > 0)
        {
            factory = _responseFactories.Dequeue();
            _lastFactory = factory;
        }
        else if (_lastFactory is not null)
        {
            factory = _lastFactory;
        }
        else
        {
            throw new InvalidOperationException("No queued responses.");
        }

        return Task.FromResult(factory());
    }
}
