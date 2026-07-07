extern alias SampleApi;

using System.Net;
using System.Text;
using System.Text.Json;

using Hexalith.Commons.UniqueIds;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Contracts.Commands;
using DaprAppIdHandler = SampleApi::Hexalith.EventStore.Sample.Api.Services.DaprAppIdHandler;
using InboundBearerForwardingHandler = SampleApi::Hexalith.EventStore.Sample.Api.Services.InboundBearerForwardingHandler;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

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

    [Fact]
    public async Task GatewayClient_WhenRegisteredLikeSampleApi_UsesSidecarBaseAddressAndHandlers()
    {
        string statusId = UniqueIdHelper.GenerateSortableUniqueStringId();
        var terminal = new CaptureHandler(new HttpResponseMessage(HttpStatusCode.Accepted)
        {
            Content = new StringContent(
                $"{{\"correlationId\":\"{statusId}\"}}",
                Encoding.UTF8,
                "application/json"),
        });
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext(),
        };
        accessor.HttpContext.Request.Headers.Authorization = "Bearer sample-token";

        var services = new ServiceCollection();
        _ = services.AddSingleton<IHttpContextAccessor>(accessor);
        _ = services.AddTransient<InboundBearerForwardingHandler>();
        _ = services.AddEventStoreGatewayClient(options => options.BaseAddress = new Uri("http://localhost:3500"))
            .AddHttpMessageHandler<InboundBearerForwardingHandler>()
            .AddHttpMessageHandler(() => new DaprAppIdHandler("eventstore", "secret-token"))
            .ConfigurePrimaryHttpMessageHandler(() => terminal);

        using ServiceProvider provider = services.BuildServiceProvider();
        IEventStoreGatewayClient gateway = provider.GetRequiredService<IEventStoreGatewayClient>();

        SubmitCommandResponse response = await gateway.SubmitCommandAsync(
            new SubmitCommandRequest(
                UniqueIdHelper.GenerateSortableUniqueStringId(),
                "tenant-a",
                "counter",
                "counter-1",
                "increment-counter",
                JsonSerializer.SerializeToElement(new { counterId = "counter-1" })),
            TestContext.Current.CancellationToken);

        response.CorrelationId.ShouldBe(statusId);
        HttpRequestMessage request = terminal.Request.ShouldNotBeNull();
        request.RequestUri.ShouldBe(new Uri("http://localhost:3500/api/v1/commands"));
        request.Headers.Authorization.ShouldNotBeNull();
        request.Headers.Authorization.Scheme.ShouldBe("Bearer");
        request.Headers.Authorization.Parameter.ShouldBe("sample-token");
        request.Headers.GetValues("dapr-app-id").ShouldBe(["eventstore"]);
        request.Headers.GetValues("dapr-api-token").ShouldBe(["secret-token"]);
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public CaptureHandler()
            : this(new HttpResponseMessage(HttpStatusCode.Accepted))
        {
        }

        public CaptureHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(_response);
        }
    }
}
