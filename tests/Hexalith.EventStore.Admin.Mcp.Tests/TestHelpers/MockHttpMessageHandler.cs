namespace Hexalith.EventStore.Admin.Mcp.Tests.TestHelpers;

using System.Collections.Concurrent;
using System.Net;

/// <summary>
/// A mock HTTP message handler that delegates to a provided callback.
/// </summary>
internal sealed class MockHttpMessageHandler(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => handler(request, cancellationToken);

    /// <summary>
    /// Creates an HttpClient that returns a fixed JSON response.
    /// </summary>
    internal static HttpClient CreateJsonClient(HttpStatusCode statusCode, string json, string baseAddress = "https://localhost:5443")
    {
        var h = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
            }));
        return new HttpClient(h) { BaseAddress = new Uri(baseAddress) };
    }

    /// <summary>
    /// Creates an HttpClient that throws the specified exception on every request.
    /// </summary>
    internal static HttpClient CreateThrowingClient(Exception exception, string baseAddress = "https://localhost:5443")
    {
        var h = new MockHttpMessageHandler((_, _) => throw exception);
        return new HttpClient(h) { BaseAddress = new Uri(baseAddress) };
    }

    /// <summary>
    /// Creates an HttpClient that captures the request URI and returns a fixed JSON response.
    /// </summary>
    internal static HttpClient CreateCapturingClient(
        Action<HttpRequestMessage> onRequest,
        HttpStatusCode statusCode,
        string json,
        string baseAddress = "https://localhost:5443")
    {
        var h = new MockHttpMessageHandler((request, _) =>
        {
            onRequest(request);
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
            });
        });
        return new HttpClient(h) { BaseAddress = new Uri(baseAddress) };
    }
}

/// <summary>
/// A mock HTTP message handler that returns queued responses in order.
/// Useful for testing tools that make multiple sequential HTTP calls.
/// </summary>
internal sealed class QueuedMockHttpMessageHandler : HttpMessageHandler
{
    private readonly ConcurrentQueue<Func<HttpRequestMessage, HttpResponseMessage>> _responseQueue = new();

    /// <summary>
    /// Enqueues a JSON response.
    /// </summary>
    internal QueuedMockHttpMessageHandler EnqueueJson(HttpStatusCode statusCode, string json)
    {
        _responseQueue.Enqueue(_ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        });
        return this;
    }

    /// <summary>
    /// Enqueues an exception to be thrown.
    /// </summary>
    internal QueuedMockHttpMessageHandler EnqueueException(Exception exception)
    {
        _responseQueue.Enqueue(_ => throw exception);
        return this;
    }

    /// <summary>
    /// Creates an HttpClient from this handler.
    /// </summary>
    internal HttpClient ToHttpClient(string baseAddress = "https://localhost:5443")
        => new(this) { BaseAddress = new Uri(baseAddress) };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!_responseQueue.TryDequeue(out Func<HttpRequestMessage, HttpResponseMessage>? factory))
        {
            throw new InvalidOperationException("No more queued responses");
        }

        return Task.FromResult(factory(request));
    }
}
