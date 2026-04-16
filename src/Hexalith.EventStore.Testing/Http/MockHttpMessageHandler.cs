
using System.Net;
using System.Text;

namespace Hexalith.EventStore.Testing.Http;
/// <summary>
/// A mock HTTP message handler that delegates to a provided callback.
/// Provides static factory methods for common test scenarios.
/// </summary>
public sealed class MockHttpMessageHandler : HttpMessageHandler {
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

    /// <summary>
    /// Initializes a new instance of the <see cref="MockHttpMessageHandler"/> class.
    /// </summary>
    /// <param name="handler">The async handler function invoked for each request.</param>
    public MockHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) => _handler = handler;

    /// <summary>
    /// Initializes a new instance of the <see cref="MockHttpMessageHandler"/> class with a synchronous handler.
    /// </summary>
    /// <param name="handler">The synchronous handler function invoked for each request.</param>
    public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) => _handler = (request, _) => Task.FromResult(handler(request));

    /// <summary>
    /// Initializes a new instance of the <see cref="MockHttpMessageHandler"/> class that returns a fixed response.
    /// </summary>
    /// <param name="response">The fixed response to return for every request.</param>
    public MockHttpMessageHandler(HttpResponseMessage response) => _handler = (_, _) => Task.FromResult(response);

    /// <summary>Gets the last request sent through this handler.</summary>
    public HttpRequestMessage? LastRequest { get; private set; }

    /// <summary>Gets the number of requests sent through this handler.</summary>
    public int CallCount { get; private set; }

    /// <summary>
    /// Creates an HttpClient that returns a fixed JSON response.
    /// </summary>
    /// <param name="statusCode">The HTTP status code to return.</param>
    /// <param name="json">The JSON response body.</param>
    /// <param name="baseAddress">The base address for the client.</param>
    /// <returns>A configured <see cref="HttpClient"/>.</returns>
    public static HttpClient CreateJsonClient(HttpStatusCode statusCode, string json, string baseAddress = "https://localhost:5443") {
        MockHttpMessageHandler handler = new((_, _) =>
            Task.FromResult(new HttpResponseMessage(statusCode) {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            }));
        return new HttpClient(handler) { BaseAddress = new Uri(baseAddress) };
    }

    /// <summary>
    /// Creates an HttpClient that returns a response with no body.
    /// </summary>
    /// <param name="statusCode">The HTTP status code to return.</param>
    /// <param name="baseAddress">The base address for the client.</param>
    /// <returns>A configured <see cref="HttpClient"/>.</returns>
    public static HttpClient CreateStatusClient(HttpStatusCode statusCode, string baseAddress = "https://localhost:5443") {
        MockHttpMessageHandler handler = new((_, _) =>
            Task.FromResult(new HttpResponseMessage(statusCode)));
        return new HttpClient(handler) { BaseAddress = new Uri(baseAddress) };
    }

    /// <summary>
    /// Creates an HttpClient that throws the specified exception on every request.
    /// </summary>
    /// <param name="exception">The exception to throw.</param>
    /// <param name="baseAddress">The base address for the client.</param>
    /// <returns>A configured <see cref="HttpClient"/>.</returns>
    public static HttpClient CreateThrowingClient(Exception exception, string baseAddress = "https://localhost:5443") {
        MockHttpMessageHandler handler = new((_, _) => throw exception);
        return new HttpClient(handler) { BaseAddress = new Uri(baseAddress) };
    }

    /// <summary>
    /// Creates an HttpClient that captures each request and returns a fixed JSON response.
    /// </summary>
    /// <param name="onRequest">Callback invoked with each request for inspection.</param>
    /// <param name="statusCode">The HTTP status code to return.</param>
    /// <param name="json">The JSON response body.</param>
    /// <param name="baseAddress">The base address for the client.</param>
    /// <returns>A configured <see cref="HttpClient"/>.</returns>
    public static HttpClient CreateCapturingClient(
        Action<HttpRequestMessage> onRequest,
        HttpStatusCode statusCode,
        string json,
        string baseAddress = "https://localhost:5443") {
        MockHttpMessageHandler handler = new((request, _) => {
            onRequest(request);
            return Task.FromResult(new HttpResponseMessage(statusCode) {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        });
        return new HttpClient(handler) { BaseAddress = new Uri(baseAddress) };
    }

    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        LastRequest = request;
        CallCount++;
        return _handler(request, cancellationToken);
    }
}
