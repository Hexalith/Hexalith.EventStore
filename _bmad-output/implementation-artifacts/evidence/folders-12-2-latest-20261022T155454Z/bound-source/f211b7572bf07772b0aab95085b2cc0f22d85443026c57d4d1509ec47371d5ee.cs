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

public class DaprProjectionCommandServiceTests {
    private const string EventStoreAppId = "eventstore";

    private static (DaprProjectionCommandService Service, TestHttpMessageHandler Handler) CreateService(
        DaprClient? daprClient = null,
        IAdminAuthContext? authContext = null) {
        daprClient ??= Substitute.For<DaprClient>();
        authContext ??= new NullAdminAuthContext();

        IOptions<AdminServerOptions> options = Options.Create(new AdminServerOptions {
            EventStoreAppId = EventStoreAppId,
        });

        var handler = new TestHttpMessageHandler();
        HttpClient httpClient = new(handler) { BaseAddress = new Uri("http://localhost") };
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        _ = httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        var service = new DaprProjectionCommandService(
            daprClient,
            httpClientFactory,
            options,
            authContext,
            NullLogger<DaprProjectionCommandService>.Instance);

        return (service, handler);
    }

    [Fact]
    public async Task PauseProjectionAsync_DelegatesToEventStore() {
        var expected = new AdminOperationResult(true, "op-1", null, null);
        (DaprProjectionCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupJsonResponse(expected);

        AdminOperationResult result = await service.PauseProjectionAsync("tenant1", "OrderSummary");

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task ResumeProjectionAsync_DelegatesToEventStore() {
        var expected = new AdminOperationResult(true, "op-1", null, null);
        (DaprProjectionCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupJsonResponse(expected);

        AdminOperationResult result = await service.ResumeProjectionAsync("tenant1", "OrderSummary");

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task PauseProjectionAsync_ReturnsFailure_WhenExceptionThrown() {
        (DaprProjectionCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupException(new InvalidOperationException("Service unavailable"));

        AdminOperationResult result = await service.PauseProjectionAsync("tenant1", "OrderSummary");

        result.Success.ShouldBeFalse();
        result.Message!.ShouldContain("Service unavailable");
    }

    [Fact]
    public async Task ResetProjectionAsync_DelegatesToEventStore() {
        var expected = new AdminOperationResult(true, "op-1", null, null);
        (DaprProjectionCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupJsonResponse(expected);

        AdminOperationResult result = await service.ResetProjectionAsync("tenant1", "OrderSummary", 0);

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task ReplayProjectionAsync_DelegatesToEventStore() {
        var expected = new AdminOperationResult(true, "op-1", null, null);
        (DaprProjectionCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupJsonResponse(expected);

        AdminOperationResult result = await service.ReplayProjectionAsync("tenant1", "OrderSummary", 0, 100);

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task PauseProjectionAsync_PropagatesCancellation() {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        (DaprProjectionCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupException(new OperationCanceledException());

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => service.PauseProjectionAsync("tenant1", "OrderSummary", cts.Token));
    }

    [Fact]
    public async Task PauseProjectionAsync_MapsHttpStatusCode_WhenRequestFails() {
        (DaprProjectionCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupErrorResponse(HttpStatusCode.Conflict);

        AdminOperationResult result = await service.PauseProjectionAsync("tenant1", "OrderSummary");

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("409");
    }

    [Fact]
    public async Task PauseProjectionAsync_WithProblemDetails_PreservesReasonCode() {
        (DaprProjectionCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupResponse(new HttpResponseMessage(HttpStatusCode.Conflict) {
            Content = new StringContent(
                """
                {
                  "type": "https://hexalith.io/problems/concurrency-conflict",
                  "title": "Conflict",
                  "status": 409,
                  "detail": "Projection rebuild checkpoint update conflicted with another worker.",
                  "reasonCode": "checkpoint-conflict"
                }
                """,
                System.Text.Encoding.UTF8,
                "application/problem+json"),
        });

        AdminOperationResult result = await service.PauseProjectionAsync("tenant1", "OrderSummary");

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("checkpoint-conflict");
        result.Message.ShouldBe("Projection rebuild checkpoint update conflicted with another worker.");
    }

    [Fact]
    public async Task PauseProjectionAsync_WithOversizedProblemDetails_UsesStableStatusMessage() {
        (DaprProjectionCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupResponse(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) {
            ReasonPhrase = null,
            Content = new StringContent(
                $$"""
                {
                  "title": "{{new string('x', 70_000)}}"
                }
                """,
                System.Text.Encoding.UTF8,
                "application/problem+json"),
        });

        AdminOperationResult result = await service.PauseProjectionAsync("tenant1", "OrderSummary");

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("503");
        result.Message.ShouldBe("Service Unavailable");
    }

    [Fact]
    public async Task PauseProjectionAsync_WithNullReasonPhrase_UsesStatusCodeNameFallback() {
        (DaprProjectionCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupResponse(new HttpResponseMessage(HttpStatusCode.BadGateway) {
            ReasonPhrase = null,
            Content = new StringContent(string.Empty),
        });

        AdminOperationResult result = await service.PauseProjectionAsync("tenant1", "OrderSummary");

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("502");
        result.Message.ShouldBe("Bad Gateway");
    }
}
