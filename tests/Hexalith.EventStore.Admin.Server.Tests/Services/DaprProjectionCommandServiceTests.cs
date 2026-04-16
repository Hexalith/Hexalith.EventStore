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
}
