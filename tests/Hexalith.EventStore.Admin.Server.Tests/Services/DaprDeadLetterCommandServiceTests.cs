using System.Net;

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Admin.Server.Services;
using Hexalith.EventStore.Admin.Server.Tests.Helpers;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

namespace Hexalith.EventStore.Admin.Server.Tests.Services;

public class DaprDeadLetterCommandServiceTests
{
    private const string EventStoreAppId = "eventstore";

    private static (DaprDeadLetterCommandService Service, TestHttpMessageHandler Handler) CreateService(
        DaprClient? daprClient = null,
        IAdminAuthContext? authContext = null)
    {
        daprClient ??= Substitute.For<DaprClient>();
        authContext ??= new NullAdminAuthContext();
        IOptions<AdminServerOptions> options = Options.Create(new AdminServerOptions
        {
            EventStoreAppId = EventStoreAppId,
            ServiceInvocationTimeoutSeconds = 30,
        });

        var handler = new TestHttpMessageHandler();
        HttpClient httpClient = new(handler) { BaseAddress = new Uri("http://localhost") };
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        var service = new DaprDeadLetterCommandService(
            daprClient,
            httpClientFactory,
            options,
            authContext,
            NullLogger<DaprDeadLetterCommandService>.Instance);

        return (service, handler);
    }

    // === RetryDeadLettersAsync ===

    [Fact]
    public async Task RetryDeadLettersAsync_ReturnsSuccess_WhenEventStoreResponds()
    {
        var expected = new AdminOperationResult(true, "op-1", "Retry started", null);
        (DaprDeadLetterCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupJsonResponse(expected);

        AdminOperationResult result = await service.RetryDeadLettersAsync("tenant-a", ["msg-1", "msg-2"]);

        result.Success.ShouldBeTrue();
        result.OperationId.ShouldBe("op-1");
    }

    [Fact]
    public async Task RetryDeadLettersAsync_ForwardsJwtToken()
    {
        IAdminAuthContext authContext = Substitute.For<IAdminAuthContext>();
        authContext.GetToken().Returns("dl-token");

        (DaprDeadLetterCommandService service, TestHttpMessageHandler handler) = CreateService(authContext: authContext);
        handler.SetupJsonResponse(new AdminOperationResult(true, "op-1", null, null));

        await service.RetryDeadLettersAsync("tenant-a", ["msg-1"]);

        handler.LastRequest.ShouldNotBeNull();
        handler.LastRequest!.Headers.Authorization!.Parameter.ShouldBe("dl-token");
    }

    [Fact]
    public async Task RetryDeadLettersAsync_ReturnsError_WhenServiceUnavailable()
    {
        (DaprDeadLetterCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupException(new InvalidOperationException("EventStore down"));

        AdminOperationResult result = await service.RetryDeadLettersAsync("tenant-a", ["msg-1"]);

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldNotBeNull();
    }

    [Fact]
    public async Task RetryDeadLettersAsync_ReturnsNullResponseError()
    {
        (DaprDeadLetterCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupNullJsonResponse();

        AdminOperationResult result = await service.RetryDeadLettersAsync("tenant-a", ["msg-1"]);

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("NULL_RESPONSE");
    }

    [Fact]
    public async Task RetryDeadLettersAsync_PropagatesCancellation()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        (DaprDeadLetterCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupException(new OperationCanceledException());

        await Should.ThrowAsync<OperationCanceledException>(
            () => service.RetryDeadLettersAsync("tenant-a", ["msg-1"], cts.Token));
    }

    [Fact]
    public async Task RetryDeadLettersAsync_ReturnsTimeoutError_WhenServiceTimesOut()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        IOptions<AdminServerOptions> options = Options.Create(new AdminServerOptions
        {
            EventStoreAppId = EventStoreAppId,
            ServiceInvocationTimeoutSeconds = 0,
        });

        var handler = new TestHttpMessageHandler();
        // The handler respects the cancellation token — when timeout is 0s the linked CTS fires immediately
        handler.SetupException(new OperationCanceledException());

        HttpClient httpClient = new(handler) { BaseAddress = new Uri("http://localhost") };
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        DaprDeadLetterCommandService service = new(
            daprClient,
            httpClientFactory,
            options,
            new NullAdminAuthContext(),
            NullLogger<DaprDeadLetterCommandService>.Instance);

        AdminOperationResult result = await service.RetryDeadLettersAsync("tenant-a", ["msg-1"]);

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("TIMEOUT");
    }

    // === SkipDeadLettersAsync ===

    [Fact]
    public async Task SkipDeadLettersAsync_ReturnsSuccess_WhenEventStoreResponds()
    {
        var expected = new AdminOperationResult(true, "op-2", "Skip complete", null);
        (DaprDeadLetterCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupJsonResponse(expected);

        AdminOperationResult result = await service.SkipDeadLettersAsync("tenant-a", ["msg-1"]);

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task SkipDeadLettersAsync_ReturnsError_WhenServiceUnavailable()
    {
        (DaprDeadLetterCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupException(new HttpRequestException("Connection refused"));

        AdminOperationResult result = await service.SkipDeadLettersAsync("tenant-a", ["msg-1"]);

        result.Success.ShouldBeFalse();
    }

    // === ArchiveDeadLettersAsync ===

    [Fact]
    public async Task ArchiveDeadLettersAsync_ReturnsSuccess_WhenEventStoreResponds()
    {
        var expected = new AdminOperationResult(true, "op-3", "Archive complete", null);
        (DaprDeadLetterCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupJsonResponse(expected);

        AdminOperationResult result = await service.ArchiveDeadLettersAsync("tenant-a", ["msg-1"]);

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task ArchiveDeadLettersAsync_ReturnsError_WhenServiceUnavailable()
    {
        (DaprDeadLetterCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupException(new InvalidOperationException("EventStore down"));

        AdminOperationResult result = await service.ArchiveDeadLettersAsync("tenant-a", ["msg-1"]);

        result.Success.ShouldBeFalse();
    }

    // === Error code extraction ===

    [Fact]
    public async Task InvokePost_ExtractsHttpStatusCode_FromHttpRequestException()
    {
        (DaprDeadLetterCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupErrorResponse(HttpStatusCode.NotFound);

        AdminOperationResult result = await service.RetryDeadLettersAsync("tenant-a", ["msg-1"]);

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("404");
    }
}
