using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Storage;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Admin.Server.Services;
using Hexalith.EventStore.Admin.Server.Tests.Helpers;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

namespace Hexalith.EventStore.Admin.Server.Tests.Services;

public class DaprStorageCommandServiceTests {
    private const string EventStoreAppId = "eventstore";

    private static (DaprStorageCommandService Service, TestHttpMessageHandler Handler) CreateService(IAdminAuthContext? authContext = null) {
        DaprClient daprClient = Substitute.For<DaprClient>();
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
            authContext ?? new NullAdminAuthContext(),
            NullLogger<DaprStorageCommandService>.Instance);

        return (service, handler);
    }

    [Fact]
    public async Task TriggerCompactionAsync_ReturnsDeferred_WithoutCallingEventStore() {
        (DaprStorageCommandService service, TestHttpMessageHandler handler) = CreateService();

        AdminOperationResult result = await service.TriggerCompactionAsync("tenant-a", "Counter");

        result.Success.ShouldBeFalse();
        result.OperationId.ShouldBe("deferred-compaction");
        result.ErrorCode.ShouldBe("Deferred");
        result.Message!.ShouldContain("Compaction is deferred");
        handler.RequestCount.ShouldBe(0);
    }

    [Fact]
    public async Task CreateSnapshotAsync_InvokesEventStoreAndReturnsTypedResult() {
        (DaprStorageCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupJsonResponse(new AdminOperationResult(true, "manual-snapshot-abc", "Manual snapshot created.", null));

        AdminOperationResult result = await service.CreateSnapshotAsync("tenant-a", "Counter", "counter-1");

        result.Success.ShouldBeTrue();
        result.OperationId.ShouldBe("manual-snapshot-abc");
        result.ErrorCode.ShouldBeNull();
        handler.RequestCount.ShouldBe(1);
        handler.LastRequest!.Method.ShouldBe(HttpMethod.Post);
        string body = handler.LastRequestBody.ShouldNotBeNull();
        body.ShouldContain("\"tenantId\":\"tenant-a\"");
        body.ShouldContain("\"domain\":\"Counter\"");
        body.ShouldContain("\"aggregateId\":\"counter-1\"");
    }

    [Fact]
    public async Task SetSnapshotPolicyAsync_InvokesEventStoreAndForwardsBearerToken() {
        IAdminAuthContext authContext = Substitute.For<IAdminAuthContext>();
        _ = authContext.GetToken().Returns("jwt-token");
        (DaprStorageCommandService service, TestHttpMessageHandler handler) = CreateService(authContext);
        handler.SetupJsonResponse(new AdminOperationResult(true, "snapshot-policy-set-abc", "Snapshot policy saved.", null));

        AdminOperationResult result = await service.SetSnapshotPolicyAsync("tenant-a", "Counter", "CounterAggregate", 100);

        result.Success.ShouldBeTrue();
        result.OperationId.ShouldBe("snapshot-policy-set-abc");
        handler.RequestCount.ShouldBe(1);
        handler.LastRequest!.Method.ShouldBe(HttpMethod.Put);
        handler.LastRequest.RequestUri!.ToString().ShouldContain("api/v1/admin/storage/snapshot-policy");
        handler.LastRequest.Headers.Authorization!.Scheme.ShouldBe("Bearer");
        handler.LastRequest.Headers.Authorization.Parameter.ShouldBe("jwt-token");
        string body = handler.LastRequestBody.ShouldNotBeNull();
        body.ShouldContain("\"tenantId\":\"tenant-a\"");
        body.ShouldContain("\"domain\":\"Counter\"");
        body.ShouldContain("\"aggregateType\":\"CounterAggregate\"");
        body.ShouldContain("\"intervalEvents\":100");
    }

    [Fact]
    public async Task DeleteSnapshotPolicyAsync_InvokesEventStore() {
        (DaprStorageCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupJsonResponse(new AdminOperationResult(true, "snapshot-policy-delete-abc", "Snapshot policy deleted.", null));

        AdminOperationResult result = await service.DeleteSnapshotPolicyAsync("tenant-a", "Counter", "CounterAggregate");

        result.Success.ShouldBeTrue();
        result.OperationId.ShouldBe("snapshot-policy-delete-abc");
        handler.RequestCount.ShouldBe(1);
        handler.LastRequest!.Method.ShouldBe(HttpMethod.Delete);
        string body = handler.LastRequestBody.ShouldNotBeNull();
        body.ShouldContain("\"tenantId\":\"tenant-a\"");
        body.ShouldContain("\"domain\":\"Counter\"");
        body.ShouldContain("\"aggregateType\":\"CounterAggregate\"");
    }
}
