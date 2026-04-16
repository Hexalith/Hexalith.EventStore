using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Admin.Server.Services;
using Hexalith.EventStore.Admin.Server.Tests.Helpers;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

namespace Hexalith.EventStore.Admin.Server.Tests.Services;

public class DaprStreamQueryServiceSandboxTests {
    private const string StateStoreName = "statestore";
    private const string EventStoreAppId = "command-api";

    private static (DaprStreamQueryService Service, TestHttpMessageHandler Handler) CreateService(
        DaprClient? daprClient = null,
        IAdminAuthContext? authContext = null) {
        daprClient ??= Substitute.For<DaprClient>();
        authContext ??= new NullAdminAuthContext();

        IOptions<AdminServerOptions> options = Options.Create(new AdminServerOptions {
            StateStoreName = StateStoreName,
            EventStoreAppId = EventStoreAppId,
            ServiceInvocationTimeoutSeconds = 30,
        });

        var handler = new TestHttpMessageHandler();
        HttpClient httpClient = new(handler) { BaseAddress = new Uri("http://localhost") };
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        _ = httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        var service = new DaprStreamQueryService(
            daprClient,
            httpClientFactory,
            options,
            authContext,
            NullLogger<DaprStreamQueryService>.Instance);

        return (service, handler);
    }

    [Fact]
    public async Task SandboxCommandAsync_WithEmptyCommandType_ThrowsArgumentException() {
        (DaprStreamQueryService service, _) = CreateService();
        var request = new SandboxCommandRequest(string.Empty, "{}", null, null, null);

        _ = await Should.ThrowAsync<ArgumentException>(
            () => service.SandboxCommandAsync("tenant1", "orders", "order-1", request));
    }

    [Fact]
    public async Task SandboxCommandAsync_PropagatesOperationCanceledException() {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        (DaprStreamQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupException(new OperationCanceledException());

        var request = new SandboxCommandRequest("IncrementCounter", "{}", null, null, null);

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => service.SandboxCommandAsync("tenant1", "orders", "order-1", request, cts.Token));
    }

    [Fact]
    public async Task SandboxCommandAsync_WithNegativeAtSequence_ThrowsArgumentException() {
        (DaprStreamQueryService service, _) = CreateService();
        var request = new SandboxCommandRequest("IncrementCounter", "{}", -1, null, null);

        _ = await Should.ThrowAsync<ArgumentException>(
            () => service.SandboxCommandAsync("tenant1", "orders", "order-1", request));
    }

    [Fact]
    public async Task SandboxCommandAsync_InvokesPostWithRequestBody() {
        var expectedResult = new SandboxResult(
            "tenant1", "orders", "order-1", 5, "IncrementCounter",
            "accepted", [], "{}", [], null, 10);

        (DaprStreamQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupJsonResponse(expectedResult);

        var request = new SandboxCommandRequest("IncrementCounter", "{\"Amount\":1}", 5, null, null);

        _ = await service.SandboxCommandAsync("tenant1", "orders", "order-1", request);

        // Verify the POST request was made with JSON content body
        _ = handler.LastRequest.ShouldNotBeNull();
        handler.LastRequest!.Method.ShouldBe(HttpMethod.Post);
        _ = handler.LastRequest.Content.ShouldNotBeNull();
        handler.LastRequest.Content!.Headers.ContentType!.MediaType.ShouldBe("application/json");
    }
}
