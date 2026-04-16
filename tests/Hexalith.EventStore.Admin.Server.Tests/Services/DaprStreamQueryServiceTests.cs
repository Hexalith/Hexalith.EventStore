#pragma warning disable CS8620 // Nullability mismatch in NSubstitute Returns() with nullable Dapr client methods

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Commands;
using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Admin.Server.Services;
using Hexalith.EventStore.Admin.Server.Tests.Helpers;
using Hexalith.EventStore.Contracts.Commands;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Hexalith.EventStore.Admin.Server.Tests.Services;

public class DaprStreamQueryServiceTests {
    private const string StateStoreName = "statestore";
    private const string EventStoreAppId = "eventstore";

    private static (DaprStreamQueryService Service, TestHttpMessageHandler Handler) CreateService(
        DaprClient? daprClient = null,
        IAdminAuthContext? authContext = null) {
        daprClient ??= Substitute.For<DaprClient>();
        authContext ??= new NullAdminAuthContext();

        IOptions<AdminServerOptions> options = Options.Create(new AdminServerOptions {
            StateStoreName = StateStoreName,
            EventStoreAppId = EventStoreAppId,
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
    public async Task GetRecentlyActiveStreamsAsync_ReturnsStreams_WhenIndexExists() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var streams = new List<StreamSummary>
        {
            new("tenant1", "orders", "order-1", 5, DateTimeOffset.UtcNow, 5, false, StreamStatus.Active),
            new("tenant1", "orders", "order-2", 3, DateTimeOffset.UtcNow, 3, true, StreamStatus.Idle),
        };

        _ = daprClient.GetStateAsync<List<StreamSummary>>(
            StateStoreName,
            "admin:stream-activity:all",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => streams);

        (DaprStreamQueryService service, _) = CreateService(daprClient);

        PagedResult<StreamSummary> result = await service.GetRecentlyActiveStreamsAsync("tenant1", null);

        result.Items.Count.ShouldBe(2);
        result.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task GetRecentlyActiveStreamsAsync_ReturnsEmpty_WhenIndexNotFound() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetStateAsync<List<StreamSummary>>(
            StateStoreName,
            Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (List<StreamSummary>?)null);

        (DaprStreamQueryService service, _) = CreateService(daprClient);

        PagedResult<StreamSummary> result = await service.GetRecentlyActiveStreamsAsync("tenant1", null);

        result.Items.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task GetRecentlyActiveStreamsAsync_FiltersByDomain() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var streams = new List<StreamSummary>
        {
            new("tenant1", "orders", "order-1", 5, DateTimeOffset.UtcNow, 5, false, StreamStatus.Active),
            new("tenant1", "shipping", "ship-1", 2, DateTimeOffset.UtcNow, 2, false, StreamStatus.Active),
        };

        _ = daprClient.GetStateAsync<List<StreamSummary>>(
            StateStoreName,
            "admin:stream-activity:all",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => streams);

        (DaprStreamQueryService service, _) = CreateService(daprClient);

        PagedResult<StreamSummary> result = await service.GetRecentlyActiveStreamsAsync("tenant1", "orders");

        result.Items.Count.ShouldBe(1);
        result.Items[0].Domain.ShouldBe("orders");
    }

    [Fact]
    public async Task GetRecentlyActiveStreamsAsync_WithTenantFilter_FiltersFromGlobalKey() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var streams = new List<StreamSummary>
        {
            new("tenant-a", "orders", "order-1", 5, DateTimeOffset.UtcNow, 5, false, StreamStatus.Active),
            new("tenant-b", "orders", "order-2", 3, DateTimeOffset.UtcNow, 3, false, StreamStatus.Active),
            new("tenant-a", "shipping", "ship-1", 2, DateTimeOffset.UtcNow, 2, false, StreamStatus.Active),
        };

        _ = daprClient.GetStateAsync<List<StreamSummary>>(
            StateStoreName,
            "admin:stream-activity:all",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => streams);

        (DaprStreamQueryService service, _) = CreateService(daprClient);

        PagedResult<StreamSummary> result = await service.GetRecentlyActiveStreamsAsync("tenant-a", null);

        result.Items.Count.ShouldBe(2);
        result.Items.ShouldAllBe(s => s.TenantId == "tenant-a");

        _ = await daprClient.Received(1).GetStateAsync<List<StreamSummary>>(
            StateStoreName,
            "admin:stream-activity:all",
            consistencyMode: Arg.Any<ConsistencyMode?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetRecentlyActiveStreamsAsync_ThrowsException_WhenDaprThrows() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetStateAsync<List<StreamSummary>>(
            StateStoreName,
            Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Connection failed"));

        (DaprStreamQueryService service, _) = CreateService(daprClient);

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => service.GetRecentlyActiveStreamsAsync("tenant1", null));
    }

    [Fact]
    public async Task GetRecentCommandsAsync_ReturnsResults_WhenEventStoreReturnsData() {
        var expectedResult = new PagedResult<CommandSummary>(
            [
                new("tenant1", "orders", "order-1", "corr-1", "CreateOrder", CommandStatus.Received, DateTimeOffset.UtcNow, null, null),
                new("tenant1", "orders", "order-2", "corr-2", "CreateOrder", CommandStatus.Completed, DateTimeOffset.UtcNow.AddMinutes(-1), 2, null),
            ],
            2,
            null);

        (DaprStreamQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupJsonResponse(expectedResult);

        PagedResult<CommandSummary> result = await service.GetRecentCommandsAsync("tenant1", null, null);

        result.Items.Count.ShouldBe(2);
        result.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task GetRecentCommandsAsync_ThrowsException_WhenEventStoreUnavailable() {
        (DaprStreamQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupException(new HttpRequestException("Connection failed"));

        _ = await Should.ThrowAsync<HttpRequestException>(
            () => service.GetRecentCommandsAsync("tenant1", null, null));
    }

    [Fact]
    public async Task GetStreamTimelineAsync_ThrowsException_WhenEventStoreUnavailable() {
        (DaprStreamQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupException(new InvalidOperationException("Service unavailable"));

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => service.GetStreamTimelineAsync("tenant1", "orders", "order-1", null, null));
    }

    [Fact]
    public async Task GetStreamTimelineAsync_ForwardsBearerToken_AndBuildsExpectedEndpoint() {
        IAdminAuthContext authContext = Substitute.For<IAdminAuthContext>();
        _ = authContext.GetToken().Returns("stream-token");
        PagedResult<TimelineEntry> expected = new([], 0, null);

        (DaprStreamQueryService service, TestHttpMessageHandler handler) = CreateService(authContext: authContext);
        handler.SetupJsonResponse(expected);

        PagedResult<TimelineEntry> result = await service.GetStreamTimelineAsync(
            "tenant1",
            "orders",
            "order-1",
            10,
            20,
            50);

        result.Items.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
        _ = handler.LastRequest.ShouldNotBeNull();
        handler.LastRequest!.Method.ShouldBe(HttpMethod.Get);
        handler.LastRequest.RequestUri!.ToString().ShouldContain("api/v1/admin/streams/tenant1/orders/order-1/timeline");
        handler.LastRequest.RequestUri!.ToString().ShouldContain("from=10");
        handler.LastRequest.RequestUri!.ToString().ShouldContain("to=20");
        handler.LastRequest.RequestUri!.ToString().ShouldContain("count=50");
        handler.LastRequest.Headers.Authorization!.Parameter.ShouldBe("stream-token");
    }

    [Fact]
    public async Task GetAggregateStateAtPositionAsync_ThrowsException_WhenEventStoreUnavailable() {
        (DaprStreamQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupException(new InvalidOperationException("Service unavailable"));

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => service.GetAggregateStateAtPositionAsync("tenant1", "orders", "order-1", 5));
    }

    [Fact]
    public async Task DiffAggregateStateAsync_ThrowsException_WhenEventStoreUnavailable() {
        (DaprStreamQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupException(new InvalidOperationException("Service unavailable"));

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => service.DiffAggregateStateAsync("tenant1", "orders", "order-1", 1, 5));
    }

    [Fact]
    public async Task TraceCausationChainAsync_ThrowsException_WhenEventStoreUnavailable() {
        (DaprStreamQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupException(new InvalidOperationException("Service unavailable"));

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => service.TraceCausationChainAsync("tenant1", "orders", "order-1", 5));
    }

    [Fact]
    public async Task GetAggregateBlameAsync_ThrowsException_WhenEventStoreUnavailable() {
        (DaprStreamQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupException(new InvalidOperationException("Service unavailable"));

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => service.GetAggregateBlameAsync("tenant1", "orders", "order-1", 5));
    }

    [Fact]
    public async Task GetRecentlyActiveStreamsAsync_PropagatesCancellation() {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetStateAsync<List<StreamSummary>>(
            StateStoreName,
            Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns<List<StreamSummary>?>(_ => throw new OperationCanceledException());

        (DaprStreamQueryService service, _) = CreateService(daprClient);

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => service.GetRecentlyActiveStreamsAsync("tenant1", null, ct: cts.Token));
    }

    [Fact]
    public async Task GetEventDetailAsync_ThrowsException_WhenEventStoreUnavailable() {
        (DaprStreamQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupException(new InvalidOperationException("Service unavailable"));

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => service.GetEventDetailAsync("tenant1", "orders", "order-1", 5));
    }

    [Fact]
    public async Task GetEventStepFrameAsync_ThrowsArgumentException_WhenSequenceNumberLessThanOne() {
        (DaprStreamQueryService service, _) = CreateService();

        _ = await Should.ThrowAsync<ArgumentException>(
            () => service.GetEventStepFrameAsync("tenant1", "orders", "order-1", 0));
    }

    [Fact]
    public async Task GetCorrelationTraceMapAsync_ThrowsException_WhenEventStoreUnavailable() {
        (DaprStreamQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupException(new InvalidOperationException("Service unavailable"));

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => service.GetCorrelationTraceMapAsync("tenant1", "corr-1", null, null));
    }

    [Fact]
    public async Task GetEventStepFrameAsync_ThrowsException_WhenEventStoreUnavailable() {
        (DaprStreamQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupException(new InvalidOperationException("Service unavailable"));

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => service.GetEventStepFrameAsync("tenant1", "orders", "order-1", 5));
    }

    [Fact]
    public async Task GetEventStepFrameAsync_ReturnsFallback_WhenResultIsNull() {
        (DaprStreamQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupNullJsonResponse();

        EventStepFrame result = await service.GetEventStepFrameAsync(
            "tenant1", "orders", "order-1", 5);

        _ = result.ShouldNotBeNull();
        result.TenantId.ShouldBe("tenant1");
        result.TotalEvents.ShouldBe(0);
    }

    [Fact]
    public async Task GetEventStepFrameAsync_PropagatesCancellation() {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        (DaprStreamQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupException(new OperationCanceledException());

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => service.GetEventStepFrameAsync("tenant1", "orders", "order-1", 5, cts.Token));
    }
}
