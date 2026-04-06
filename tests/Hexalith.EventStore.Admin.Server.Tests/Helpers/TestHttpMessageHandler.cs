using System.Net;
using System.Net.Http.Json;

namespace Hexalith.EventStore.Admin.Server.Tests.Helpers;

/// <summary>
/// Test helper that captures outgoing HTTP requests and returns pre-configured responses.
/// Used to mock <see cref="IHttpClientFactory"/> in tests where production code calls
/// <c>httpClient.SendAsync</c> instead of <c>DaprClient.InvokeMethodAsync</c>.
/// </summary>
internal sealed class TestHttpMessageHandler : HttpMessageHandler
{
    private Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

    public TestHttpMessageHandler()
    {
        _handler = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }

    /// <summary>
    /// Gets the last <see cref="HttpRequestMessage"/> that was sent through this handler.
    /// </summary>
    public HttpRequestMessage? LastRequest { get; private set; }

    /// <summary>
    /// Configures the handler to return a fixed <see cref="HttpResponseMessage"/>.
    /// </summary>
    public void SetupResponse(HttpResponseMessage response)
    {
        _handler = (_, _) => Task.FromResult(response);
    }

    /// <summary>
    /// Configures the handler to return a JSON-serialized response body.
    /// </summary>
    public void SetupJsonResponse<T>(T content, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _handler = (_, _) => Task.FromResult(new HttpResponseMessage(statusCode)
        {
            Content = JsonContent.Create(content),
        });
    }

    /// <summary>
    /// Configures the handler to return an empty body with the specified status code.
    /// Useful for simulating null deserialization (empty 200 OK body).
    /// </summary>
    public void SetupEmptyResponse(HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _handler = (_, _) => Task.FromResult(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(string.Empty),
        });
    }

    /// <summary>
    /// Configures the handler to return a "null" JSON literal body, which deserializes to null.
    /// </summary>
    public void SetupNullJsonResponse(HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _handler = (_, _) => Task.FromResult(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent("null", System.Text.Encoding.UTF8, "application/json"),
        });
    }

    /// <summary>
    /// Configures the handler to throw the specified exception.
    /// </summary>
    public void SetupException(Exception exception)
    {
        _handler = (_, _) => throw exception;
    }

    /// <summary>
    /// Configures the handler to return a non-success status code (triggers EnsureSuccessStatusCode failure).
    /// </summary>
    public void SetupErrorResponse(HttpStatusCode statusCode, string? reasonPhrase = null)
    {
        _handler = (_, _) => Task.FromResult(new HttpResponseMessage(statusCode)
        {
            ReasonPhrase = reasonPhrase,
        });
    }

    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        return _handler(request, cancellationToken);
    }
}
