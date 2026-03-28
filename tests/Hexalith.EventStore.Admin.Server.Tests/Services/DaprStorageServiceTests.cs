#pragma warning disable CS8620 // Nullability mismatch in NSubstitute Returns() with nullable Dapr client methods

using System.Net;

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Storage;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Admin.Server.Services;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Hexalith.EventStore.Admin.Server.Tests.Services;

public class DaprStorageServiceTests {
    private const string StateStoreName = "statestore";
    private const string EventStoreAppId = "eventstore";

    private static DaprStorageQueryService CreateQueryService(DaprClient? daprClient = null) {
        daprClient ??= Substitute.For<DaprClient>();
        IOptions<AdminServerOptions> options = Options.Create(new AdminServerOptions {
            StateStoreName = StateStoreName,
        });

        return new DaprStorageQueryService(
            daprClient,
            options,
            NullLogger<DaprStorageQueryService>.Instance);
    }

    private static DaprStorageCommandService CreateCommandService(DaprClient? daprClient = null) {
        daprClient ??= Substitute.For<DaprClient>();
        IOptions<AdminServerOptions> options = Options.Create(new AdminServerOptions {
            EventStoreAppId = EventStoreAppId,
        });

        return new DaprStorageCommandService(
            daprClient,
            options,
            new NullAdminAuthContext(),
            NullLogger<DaprStorageCommandService>.Instance);
    }

    [Fact]
    public async Task GetStorageOverviewAsync_ReturnsOverview_WhenIndexExists() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var overview = new StorageOverview(1000, 50000, [], 125);

        daprClient.GetStateAsync<StorageOverview>(
            StateStoreName,
            "admin:storage-overview:tenant1",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => overview);

        DaprStorageQueryService service = CreateQueryService(daprClient);

        StorageOverview result = await service.GetStorageOverviewAsync("tenant1");

        result.TotalEventCount.ShouldBe(1000);
        result.TotalSizeBytes.ShouldBe(50000);
        result.TotalStreamCount.ShouldBe(125);
    }

    [Fact]
    public async Task GetStorageOverviewAsync_UsesOptionalStreamCountIndex_WhenOverviewStreamCountMissing()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var overview = new StorageOverview(1000, 50000, []);

        daprClient.GetStateAsync<StorageOverview>(
            StateStoreName,
            "admin:storage-overview:tenant1",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => overview);

        daprClient.GetStateAsync<long?>(
            StateStoreName,
            "admin:storage-stream-count:tenant1",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => 321L);

        DaprStorageQueryService service = CreateQueryService(daprClient);

        StorageOverview result = await service.GetStorageOverviewAsync("tenant1");

        result.TotalStreamCount.ShouldBe(321);
    }

    [Fact]
    public async Task GetStorageOverviewAsync_IgnoresOptionalStreamCount_WhenInvalid()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var overview = new StorageOverview(1000, 50000, []);

        daprClient.GetStateAsync<StorageOverview>(
            StateStoreName,
            "admin:storage-overview:tenant1",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => overview);

        daprClient.GetStateAsync<long?>(
            StateStoreName,
            "admin:storage-stream-count:tenant1",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => -1L);

        DaprStorageQueryService service = CreateQueryService(daprClient);

        StorageOverview result = await service.GetStorageOverviewAsync("tenant1");

        result.TotalStreamCount.ShouldBeNull();
    }

    [Fact]
    public async Task GetStorageOverviewAsync_IgnoresOptionalStreamCount_WhenLookupFails()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var overview = new StorageOverview(1000, 50000, []);

        daprClient.GetStateAsync<StorageOverview>(
            StateStoreName,
            "admin:storage-overview:tenant1",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => overview);

        daprClient.GetStateAsync<long?>(
            StateStoreName,
            "admin:storage-stream-count:tenant1",
            cancellationToken: Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("stream count index unavailable"));

        DaprStorageQueryService service = CreateQueryService(daprClient);

        StorageOverview result = await service.GetStorageOverviewAsync("tenant1");

        result.TotalEventCount.ShouldBe(1000);
        result.TotalSizeBytes.ShouldBe(50000);
        result.TotalStreamCount.ShouldBeNull();
    }

    [Fact]
    public async Task GetStorageOverviewAsync_ReturnsEmpty_WhenIndexNotFound() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.GetStateAsync<StorageOverview>(
            StateStoreName,
            Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (StorageOverview?)null);

        DaprStorageQueryService service = CreateQueryService(daprClient);

        StorageOverview result = await service.GetStorageOverviewAsync("tenant1");

        result.TotalEventCount.ShouldBe(0);
        result.TotalSizeBytes.ShouldBeNull();
        result.TenantBreakdown.ShouldBeEmpty();
        result.TotalStreamCount.ShouldBe(0);
    }

    [Fact]
    public async Task GetHotStreamsAsync_ReturnsLimitedResults() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var streams = new List<StreamStorageInfo>
        {
            new("t1", "d1", "a1", "OrderAggregate", 100, null, false, null),
            new("t1", "d1", "a2", "OrderAggregate", 50, null, true, TimeSpan.FromHours(1)),
            new("t1", "d1", "a3", "OrderAggregate", 200, null, false, null),
        };

        daprClient.GetStateAsync<List<StreamStorageInfo>>(
            StateStoreName,
            "admin:storage-hot-streams:t1",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => streams);

        DaprStorageQueryService service = CreateQueryService(daprClient);

        IReadOnlyList<StreamStorageInfo> result = await service.GetHotStreamsAsync("t1", 2);

        result.Count.ShouldBe(2);
        result[0].EventCount.ShouldBe(200); // Sorted descending
        result[1].EventCount.ShouldBe(100);
    }

    [Fact]
    public async Task TriggerCompactionAsync_DelegatesToEventStore() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var expected = new AdminOperationResult(true, "op-1", null, null);

        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .Returns(_ => expected);

        DaprStorageCommandService service = CreateCommandService(daprClient);

        AdminOperationResult result = await service.TriggerCompactionAsync("tenant1", "orders");

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task CreateSnapshotAsync_DelegatesToEventStore() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var expected = new AdminOperationResult(true, "op-1", null, null);

        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .Returns(_ => expected);

        DaprStorageCommandService service = CreateCommandService(daprClient);

        AdminOperationResult result = await service.CreateSnapshotAsync("tenant1", "orders", "order-1");

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task TriggerCompactionAsync_ReturnsFailure_WhenExceptionThrown() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Timeout"));

        DaprStorageCommandService service = CreateCommandService(daprClient);

        AdminOperationResult result = await service.TriggerCompactionAsync("tenant1", null);

        result.Success.ShouldBeFalse();
        result.Message!.ShouldContain("Timeout");
    }

    [Fact]
    public async Task SetSnapshotPolicyAsync_DelegatesToEventStore() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var expected = new AdminOperationResult(true, "op-1", null, null);
        HttpRequestMessage? capturedRequest = null;
        IAdminAuthContext authContext = Substitute.For<IAdminAuthContext>();
        authContext.GetToken().Returns("storage-token");

        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Do<HttpRequestMessage>(request => capturedRequest = request),
            Arg.Any<CancellationToken>())
            .Returns(_ => expected);

        DaprStorageCommandService service = CreateCommandService(daprClient);
        service = new DaprStorageCommandService(
            daprClient,
            Options.Create(new AdminServerOptions {
                EventStoreAppId = EventStoreAppId,
            }),
            authContext,
            NullLogger<DaprStorageCommandService>.Instance);

        AdminOperationResult result = await service.SetSnapshotPolicyAsync("tenant1", "orders", "OrderAggregate", 100);

        result.Success.ShouldBeTrue();
        capturedRequest.ShouldNotBeNull();
        capturedRequest!.Method.ShouldBe(HttpMethod.Put);
        capturedRequest.RequestUri.ShouldNotBeNull();
        capturedRequest.RequestUri!.ToString().ShouldContain("api/v1/admin/storage/snapshot-policy");
        capturedRequest.Headers.Authorization.ShouldNotBeNull();
        capturedRequest.Headers.Authorization!.Scheme.ShouldBe("Bearer");
        capturedRequest.Headers.Authorization.Parameter.ShouldBe("storage-token");
    }

    [Fact]
    public async Task TriggerCompactionAsync_PropagatesCancellation()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .Returns<AdminOperationResult?>(_ => throw new OperationCanceledException());

        DaprStorageCommandService service = CreateCommandService(daprClient);

        await Should.ThrowAsync<OperationCanceledException>(
            () => service.TriggerCompactionAsync("tenant1", null, cts.Token));
    }

    [Fact]
    public async Task GetHotStreamsAsync_ThrowsOnZeroCount()
    {
        DaprStorageQueryService service = CreateQueryService();

        await Should.ThrowAsync<ArgumentOutOfRangeException>(
            () => service.GetHotStreamsAsync("tenant1", 0));
    }

    [Fact]
    public async Task SetSnapshotPolicyAsync_MapsHttpStatusCode_WhenRequestFails() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Forbidden", null, HttpStatusCode.Forbidden));

        DaprStorageCommandService service = CreateCommandService(daprClient);

        AdminOperationResult result = await service.SetSnapshotPolicyAsync("tenant1", "orders", "OrderAggregate", 100);

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("403");
    }

    [Fact]
    public async Task GetSnapshotPoliciesAsync_ReturnsPolicies_WhenIndexExists() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var policies = new List<SnapshotPolicy>
        {
            new("tenant1", "orders", "OrderAggregate", 100, DateTimeOffset.UtcNow),
        };

        daprClient.GetStateAsync<List<SnapshotPolicy>>(
            StateStoreName,
            "admin:storage-snapshot-policies:tenant1",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => policies);

        DaprStorageQueryService service = CreateQueryService(daprClient);

        IReadOnlyList<SnapshotPolicy> result = await service.GetSnapshotPoliciesAsync("tenant1");

        result.Count.ShouldBe(1);
        result[0].AggregateType.ShouldBe("OrderAggregate");
    }
}
