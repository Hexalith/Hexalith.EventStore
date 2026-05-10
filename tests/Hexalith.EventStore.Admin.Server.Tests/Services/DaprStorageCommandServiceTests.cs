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

    private static (DaprStorageCommandService Service, TestHttpMessageHandler Handler) CreateService() {
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
            new NullAdminAuthContext(),
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
    public async Task CreateSnapshotAsync_ReturnsDeferred_WithoutCallingEventStore() {
        (DaprStorageCommandService service, TestHttpMessageHandler handler) = CreateService();

        AdminOperationResult result = await service.CreateSnapshotAsync("tenant-a", "Counter", "counter-1");

        result.Success.ShouldBeFalse();
        result.OperationId.ShouldBe("deferred-manual-snapshot");
        result.ErrorCode.ShouldBe("Deferred");
        result.Message!.ShouldContain("Manual snapshot creation is deferred");
        handler.RequestCount.ShouldBe(0);
    }

    [Fact]
    public async Task SetSnapshotPolicyAsync_ReturnsDeferred_WithoutCallingEventStore() {
        (DaprStorageCommandService service, TestHttpMessageHandler handler) = CreateService();

        AdminOperationResult result = await service.SetSnapshotPolicyAsync("tenant-a", "Counter", "CounterAggregate", 100);

        result.Success.ShouldBeFalse();
        result.OperationId.ShouldBe("deferred-snapshot-policy-set");
        result.ErrorCode.ShouldBe("Deferred");
        result.Message!.ShouldContain("Snapshot policy changes are deferred");
        handler.RequestCount.ShouldBe(0);
    }

    [Fact]
    public async Task DeleteSnapshotPolicyAsync_ReturnsDeferred_WithoutCallingEventStore() {
        (DaprStorageCommandService service, TestHttpMessageHandler handler) = CreateService();

        AdminOperationResult result = await service.DeleteSnapshotPolicyAsync("tenant-a", "Counter", "CounterAggregate");

        result.Success.ShouldBeFalse();
        result.OperationId.ShouldBe("deferred-snapshot-policy-delete");
        result.ErrorCode.ShouldBe("Deferred");
        result.Message!.ShouldContain("Snapshot policy deletion is deferred");
        handler.RequestCount.ShouldBe(0);
    }
}
