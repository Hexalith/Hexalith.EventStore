using Hexalith.EventStore.Sample.Api.Services;

using Microsoft.AspNetCore.Http;

using Shouldly;

namespace Hexalith.EventStore.Sample.Tests.SampleApi;

public sealed class SampleApiGatewayHandlerTests
{
    [Fact]
    public async Task InboundBearerForwardingHandler_WhenAuthorizationHeaderExists_ForwardsBearer()
    {
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext(),
        };
        accessor.HttpContext.Request.Headers.Authorization = "Bearer sample-token";
        var terminal = new CaptureHandler();
        using var handler = new InboundBearerForwardingHandler(accessor)
        {
            InnerHandler = terminal,
        };
        using var invoker = new HttpMessageInvoker(handler);

        using HttpResponseMessage response = await invoker.SendAsync(
            new HttpRequestMessage(HttpMethod.Post, "http://eventstore/api/v1/commands"),
            TestContext.Current.CancellationToken);

        HttpRequestMessage request = terminal.Request.ShouldNotBeNull();
        request.Headers.Authorization.ShouldNotBeNull();
        request.Headers.Authorization.Scheme.ShouldBe("Bearer");
        request.Headers.Authorization.Parameter.ShouldBe("sample-token");
    }

    [Fact]
    public async Task DaprAppIdHandler_AddsEventStoreRoutingHeaders()
    {
        var terminal = new CaptureHandler();
        using var handler = new DaprAppIdHandler("eventstore", "secret-token")
        {
            InnerHandler = terminal,
        };
        using var invoker = new HttpMessageInvoker(handler);

        using HttpResponseMessage response = await invoker.SendAsync(
            new HttpRequestMessage(HttpMethod.Post, "http://localhost:3500/api/v1/queries"),
            TestContext.Current.CancellationToken);

        HttpRequestMessage request = terminal.Request.ShouldNotBeNull();
        request.Headers.GetValues("dapr-app-id").ShouldBe(["eventstore"]);
        request.Headers.GetValues("dapr-api-token").ShouldBe(["secret-token"]);
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.Accepted));
        }
    }
}
