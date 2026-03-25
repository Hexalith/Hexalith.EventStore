using System.Net;

namespace Hexalith.EventStore.Admin.Cli.Tests.Client;

/// <summary>
/// Test helper that returns a pre-configured response from <see cref="HttpClient"/>.
/// </summary>
internal class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

    public MockHttpMessageHandler(HttpResponseMessage response)
    {
        _handler = _ => Task.FromResult(response);
    }

    public MockHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
    {
        _handler = handler;
    }

    public HttpRequestMessage? LastRequest { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        return _handler(request);
    }
}
