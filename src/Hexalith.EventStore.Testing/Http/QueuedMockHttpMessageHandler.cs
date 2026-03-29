namespace Hexalith.EventStore.Testing.Http;

using System.Collections.Concurrent;
using System.Net;
using System.Text;

/// <summary>
/// A mock HTTP message handler that returns queued responses in order.
/// Supports fluent builder pattern for enqueueing responses.
/// When the queue is exhausted, the last factory is replayed indefinitely.
/// </summary>
public sealed class QueuedMockHttpMessageHandler : HttpMessageHandler
{
    private readonly ConcurrentQueue<Func<HttpRequestMessage, HttpResponseMessage>> _responseQueue = new();
    private Func<HttpRequestMessage, HttpResponseMessage>? _lastFactory;

    /// <summary>Gets the number of requests sent through this handler.</summary>
    public int CallCount { get; private set; }

    /// <summary>
    /// Enqueues a JSON response.
    /// </summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="json">The JSON response body.</param>
    /// <returns>This handler for fluent chaining.</returns>
    public QueuedMockHttpMessageHandler EnqueueJson(HttpStatusCode statusCode, string json)
    {
        _responseQueue.Enqueue(_ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });
        return this;
    }

    /// <summary>
    /// Enqueues a response with the specified status code and no body.
    /// </summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <returns>This handler for fluent chaining.</returns>
    public QueuedMockHttpMessageHandler EnqueueStatus(HttpStatusCode statusCode)
    {
        _responseQueue.Enqueue(_ => new HttpResponseMessage(statusCode));
        return this;
    }

    /// <summary>
    /// Enqueues a pre-built response to return on the next request.
    /// </summary>
    /// <param name="response">The response to return.</param>
    /// <returns>This handler for fluent chaining.</returns>
    public QueuedMockHttpMessageHandler EnqueueResponse(HttpResponseMessage response)
    {
        _responseQueue.Enqueue(_ => response);
        return this;
    }

    /// <summary>
    /// Enqueues a factory function that produces a response (or throws) on the next request.
    /// </summary>
    /// <param name="factory">A factory that returns an <see cref="HttpResponseMessage"/> or throws.</param>
    /// <returns>This handler for fluent chaining.</returns>
    public QueuedMockHttpMessageHandler EnqueueFactory(Func<HttpResponseMessage> factory)
    {
        _responseQueue.Enqueue(_ => factory());
        return this;
    }

    /// <summary>
    /// Enqueues an exception to be thrown on the next request.
    /// </summary>
    /// <param name="exception">The exception to throw.</param>
    /// <returns>This handler for fluent chaining.</returns>
    public QueuedMockHttpMessageHandler EnqueueException(Exception exception)
    {
        _responseQueue.Enqueue(_ => throw exception);
        return this;
    }

    /// <summary>
    /// Creates an HttpClient from this handler.
    /// </summary>
    /// <param name="baseAddress">The base address for the client.</param>
    /// <returns>A configured <see cref="HttpClient"/>.</returns>
    public HttpClient ToHttpClient(string baseAddress = "https://localhost:5443")
        => new(this) { BaseAddress = new Uri(baseAddress) };

    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CallCount++;

        if (_responseQueue.TryDequeue(out Func<HttpRequestMessage, HttpResponseMessage>? factory))
        {
            _lastFactory = factory;
        }
        else if (_lastFactory is null)
        {
            throw new InvalidOperationException("No queued responses.");
        }

        return Task.FromResult(_lastFactory!(request));
    }
}
