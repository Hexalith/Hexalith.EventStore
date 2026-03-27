#pragma warning disable CS8620 // Nullability mismatch in NSubstitute Returns() with nullable Dapr client methods

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Admin.Server.Services;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Hexalith.EventStore.Admin.Server.Tests.Services;

public class DaprStreamQueryServiceSandboxTests
{
    private const string StateStoreName = "statestore";
    private const string CommandApiAppId = "command-api";

    private static DaprStreamQueryService CreateService(
        DaprClient? daprClient = null,
        IAdminAuthContext? authContext = null)
    {
        daprClient ??= Substitute.For<DaprClient>();
        authContext ??= new NullAdminAuthContext();

        IOptions<AdminServerOptions> options = Options.Create(new AdminServerOptions
        {
            StateStoreName = StateStoreName,
            CommandApiAppId = CommandApiAppId,
            ServiceInvocationTimeoutSeconds = 30,
        });

        return new DaprStreamQueryService(
            daprClient,
            options,
            authContext,
            NullLogger<DaprStreamQueryService>.Instance);
    }

    [Fact]
    public async Task SandboxCommandAsync_WithEmptyCommandType_ThrowsArgumentException()
    {
        DaprStreamQueryService service = CreateService();
        var request = new SandboxCommandRequest(string.Empty, "{}", null, null, null);

        await Should.ThrowAsync<ArgumentException>(
            () => service.SandboxCommandAsync("tenant1", "orders", "order-1", request));
    }

    [Fact]
    public async Task SandboxCommandAsync_PropagatesOperationCanceledException()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.InvokeMethodAsync<SandboxResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .Returns<SandboxResult?>(_ => throw new OperationCanceledException());

        DaprStreamQueryService service = CreateService(daprClient);
        var request = new SandboxCommandRequest("IncrementCounter", "{}", null, null, null);

        await Should.ThrowAsync<OperationCanceledException>(
            () => service.SandboxCommandAsync("tenant1", "orders", "order-1", request, cts.Token));
    }

    [Fact]
    public async Task SandboxCommandAsync_WithNegativeAtSequence_ThrowsArgumentException()
    {
        DaprStreamQueryService service = CreateService();
        var request = new SandboxCommandRequest("IncrementCounter", "{}", -1, null, null);

        await Should.ThrowAsync<ArgumentException>(
            () => service.SandboxCommandAsync("tenant1", "orders", "order-1", request));
    }

    [Fact]
    public async Task SandboxCommandAsync_InvokesPostWithRequestBody()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();

        // Capture request properties inside the InvokeMethodAsync callback
        // (Content is disposed after InvokeMethodAsync returns due to 'using' in the SUT)
        HttpMethod? capturedMethod = null;
        bool capturedHasContent = false;
        string? capturedContentType = null;

        daprClient.CreateInvokeMethodRequest(
            Arg.Any<HttpMethod>(),
            Arg.Any<string>(),
            Arg.Any<string>())
            .Returns(callInfo =>
            {
                var msg = new HttpRequestMessage(
                    callInfo.ArgAt<HttpMethod>(0),
                    $"http://localhost/{callInfo.ArgAt<string>(2)}");
                return msg;
            });

        daprClient.InvokeMethodAsync<SandboxResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                HttpRequestMessage req = callInfo.ArgAt<HttpRequestMessage>(0);
                capturedMethod = req.Method;
                capturedHasContent = req.Content is not null;
                capturedContentType = req.Content?.Headers.ContentType?.MediaType;
                return new SandboxResult(
                    "tenant1", "orders", "order-1", 5, "IncrementCounter",
                    "accepted", [], "{}", [], null, 10);
            });

        DaprStreamQueryService service = CreateService(daprClient);
        var request = new SandboxCommandRequest("IncrementCounter", "{\"Amount\":1}", 5, null, null);

        _ = await service.SandboxCommandAsync("tenant1", "orders", "order-1", request);

        // Verify the POST request was made with JSON content body
        capturedMethod.ShouldBe(HttpMethod.Post);
        capturedHasContent.ShouldBeTrue();
        capturedContentType.ShouldBe("application/json");
    }
}
