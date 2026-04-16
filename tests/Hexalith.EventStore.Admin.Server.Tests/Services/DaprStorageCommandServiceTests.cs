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

public class DaprStorageCommandServiceTests {
    private const string EventStoreAppId = "eventstore";

    private static (DaprStorageCommandService Service, TestHttpMessageHandler Handler) CreateService(
        DaprClient? daprClient = null,
        IAdminAuthContext? authContext = null) {
        daprClient ??= Substitute.For<DaprClient>();
        authContext ??= new NullAdminAuthContext();
        IOptions<AdminServerOptions> options = Options.Create(new AdminServerOptions {
            EventStoreAppId = EventStoreAppId,
            ServiceInvocationTimeoutSeconds = 30,
        });

        var handler = new TestHttpMessageHandler();
        HttpClient httpClient = new(handler) { BaseAddress = new Uri("http://localhost") };
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        _ = httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        var service = new DaprStorageCommandService(
            daprClient,
            httpClientFactory,
            options,
            authContext,
            NullLogger<DaprStorageCommandService>.Instance);

        return (service, handler);
    }

    // === TriggerCompactionAsync ===

    [Fact]
    public async Task TriggerCompactionAsync_ReturnsSuccess_WhenEventStoreResponds() {
        var expected = new AdminOperationResult(true, "op-1", "Compaction started", null);
        (DaprStorageCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupJsonResponse(expected);

        AdminOperationResult result = await service.TriggerCompactionAsync("tenant-a", "Counter");

        result.Success.ShouldBeTrue();
        result.OperationId.ShouldBe("op-1");
    }

    [Fact]
    public async Task TriggerCompactionAsync_ForwardsJwtToken() {
        IAdminAuthContext authContext = Substitute.For<IAdminAuthContext>();
        _ = authContext.GetToken().Returns("storage-token");

        (DaprStorageCommandService service, TestHttpMessageHandler handler) = CreateService(authContext: authContext);
        handler.SetupJsonResponse(new AdminOperationResult(true, "op-1", null, null));

        _ = await service.TriggerCompactionAsync("tenant-a", null);

        _ = handler.LastRequest.ShouldNotBeNull();
        handler.LastRequest!.Headers.Authorization!.Parameter.ShouldBe("storage-token");
    }

    [Fact]
    public async Task TriggerCompactionAsync_ReturnsError_WhenServiceUnavailable() {
        (DaprStorageCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupException(new InvalidOperationException("EventStore down"));

        AdminOperationResult result = await service.TriggerCompactionAsync("tenant-a", null);

        result.Success.ShouldBeFalse();
        _ = result.ErrorCode.ShouldNotBeNull();
    }

    [Fact]
    public async Task TriggerCompactionAsync_ReturnsNullResponseError() {
        (DaprStorageCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupNullJsonResponse();

        AdminOperationResult result = await service.TriggerCompactionAsync("tenant-a", null);

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("NULL_RESPONSE");
    }

    [Fact]
    public async Task TriggerCompactionAsync_PropagatesCancellation() {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        (DaprStorageCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupException(new OperationCanceledException());

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => service.TriggerCompactionAsync("tenant-a", null, cts.Token));
    }

    // === CreateSnapshotAsync ===

    [Fact]
    public async Task CreateSnapshotAsync_ReturnsSuccess_WhenEventStoreResponds() {
        var expected = new AdminOperationResult(true, "op-2", "Snapshot created", null);
        (DaprStorageCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupJsonResponse(expected);

        AdminOperationResult result = await service.CreateSnapshotAsync("tenant-a", "Counter", "counter-1");

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task CreateSnapshotAsync_ReturnsError_WhenServiceUnavailable() {
        (DaprStorageCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupException(new HttpRequestException("Connection refused"));

        AdminOperationResult result = await service.CreateSnapshotAsync("tenant-a", "Counter", "counter-1");

        result.Success.ShouldBeFalse();
    }

    // === SetSnapshotPolicyAsync ===

    [Fact]
    public async Task SetSnapshotPolicyAsync_ReturnsSuccess_WhenEventStoreResponds() {
        var expected = new AdminOperationResult(true, "op-3", "Policy set", null);
        (DaprStorageCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupJsonResponse(expected);

        AdminOperationResult result = await service.SetSnapshotPolicyAsync("tenant-a", "Counter", "CounterAggregate", 100);

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task SetSnapshotPolicyAsync_ReturnsError_WhenServiceUnavailable() {
        (DaprStorageCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupException(new InvalidOperationException("EventStore down"));

        AdminOperationResult result = await service.SetSnapshotPolicyAsync("tenant-a", "Counter", "CounterAggregate", 100);

        result.Success.ShouldBeFalse();
    }

    // === DeleteSnapshotPolicyAsync ===

    [Fact]
    public async Task DeleteSnapshotPolicyAsync_ReturnsSuccess_WhenEventStoreResponds() {
        var expected = new AdminOperationResult(true, "op-4", "Policy deleted", null);
        (DaprStorageCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupJsonResponse(expected);

        AdminOperationResult result = await service.DeleteSnapshotPolicyAsync("tenant-a", "Counter", "CounterAggregate");

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteSnapshotPolicyAsync_ReturnsError_WhenServiceUnavailable() {
        (DaprStorageCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupException(new InvalidOperationException("EventStore down"));

        AdminOperationResult result = await service.DeleteSnapshotPolicyAsync("tenant-a", "Counter", "CounterAggregate");

        result.Success.ShouldBeFalse();
    }

    // === Error code extraction ===

    [Fact]
    public async Task InvokePost_ExtractsHttpStatusCode_FromHttpRequestException() {
        (DaprStorageCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupErrorResponse(HttpStatusCode.NotFound);

        AdminOperationResult result = await service.TriggerCompactionAsync("tenant-a", null);

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("404");
    }
}
