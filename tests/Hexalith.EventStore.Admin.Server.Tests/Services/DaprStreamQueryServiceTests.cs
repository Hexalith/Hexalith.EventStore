#pragma warning disable CS8620 // Nullability mismatch in NSubstitute Returns() with nullable Dapr client methods

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Commands;
using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Admin.Server.Services;
using Hexalith.EventStore.Contracts.Commands;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Hexalith.EventStore.Admin.Server.Tests.Services;

public class DaprStreamQueryServiceTests {
    private const string StateStoreName = "statestore";
    private const string EventStoreAppId = "eventstore";

    private static DaprStreamQueryService CreateService(
        DaprClient? daprClient = null,
        IAdminAuthContext? authContext = null) {
        daprClient ??= Substitute.For<DaprClient>();
        authContext ??= new NullAdminAuthContext();

        IOptions<AdminServerOptions> options = Options.Create(new AdminServerOptions {
            StateStoreName = StateStoreName,
            EventStoreAppId = EventStoreAppId,
        });

        return new DaprStreamQueryService(
            daprClient,
            options,
            authContext,
            NullLogger<DaprStreamQueryService>.Instance);
    }

    [Fact]
    public async Task GetRecentlyActiveStreamsAsync_ReturnsStreams_WhenIndexExists() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var streams = new List<StreamSummary>
        {
            new("tenant1", "orders", "order-1", 5, DateTimeOffset.UtcNow, 5, false, StreamStatus.Active),
            new("tenant1", "orders", "order-2", 3, DateTimeOffset.UtcNow, 3, true, StreamStatus.Idle),
        };

        daprClient.GetStateAsync<List<StreamSummary>>(
            StateStoreName,
            "admin:stream-activity:tenant1",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => streams);

        DaprStreamQueryService service = CreateService(daprClient);

        PagedResult<StreamSummary> result = await service.GetRecentlyActiveStreamsAsync("tenant1", null);

        result.Items.Count.ShouldBe(2);
        result.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task GetRecentlyActiveStreamsAsync_ReturnsEmpty_WhenIndexNotFound() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.GetStateAsync<List<StreamSummary>>(
            StateStoreName,
            Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (List<StreamSummary>?)null);

        DaprStreamQueryService service = CreateService(daprClient);

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

        daprClient.GetStateAsync<List<StreamSummary>>(
            StateStoreName,
            "admin:stream-activity:tenant1",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => streams);

        DaprStreamQueryService service = CreateService(daprClient);

        PagedResult<StreamSummary> result = await service.GetRecentlyActiveStreamsAsync("tenant1", "orders");

        result.Items.Count.ShouldBe(1);
        result.Items[0].Domain.ShouldBe("orders");
    }

    [Fact]
    public async Task GetRecentlyActiveStreamsAsync_ReturnsEmpty_WhenDaprThrows() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.GetStateAsync<List<StreamSummary>>(
            StateStoreName,
            Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Connection failed"));

        DaprStreamQueryService service = CreateService(daprClient);

        PagedResult<StreamSummary> result = await service.GetRecentlyActiveStreamsAsync("tenant1", null);

        result.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetRecentCommandsAsync_ReturnsResults_WhenEventStoreReturnsData() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var expectedResult = new PagedResult<CommandSummary>(
            [
                new("tenant1", "orders", "order-1", "corr-1", "CreateOrder", CommandStatus.Received, DateTimeOffset.UtcNow, null, null),
                new("tenant1", "orders", "order-2", "corr-2", "CreateOrder", CommandStatus.Completed, DateTimeOffset.UtcNow.AddMinutes(-1), 2, null),
            ],
            2,
            null);

        daprClient.InvokeMethodAsync<PagedResult<CommandSummary>>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .Returns(_ => expectedResult);

        DaprStreamQueryService service = CreateService(daprClient);

        PagedResult<CommandSummary> result = await service.GetRecentCommandsAsync("tenant1", null, null);

        result.Items.Count.ShouldBe(2);
        result.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task GetRecentCommandsAsync_ReturnsEmpty_WhenEventStoreUnavailable() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.InvokeMethodAsync<PagedResult<CommandSummary>>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection failed"));

        DaprStreamQueryService service = CreateService(daprClient);

        PagedResult<CommandSummary> result = await service.GetRecentCommandsAsync("tenant1", null, null);

        result.Items.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task GetStreamTimelineAsync_ReturnsEmpty_WhenEventStoreUnavailable() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.InvokeMethodAsync<PagedResult<TimelineEntry>>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Service unavailable"));

        DaprStreamQueryService service = CreateService(daprClient);

        PagedResult<TimelineEntry> result = await service.GetStreamTimelineAsync(
            "tenant1", "orders", "order-1", null, null);

        result.Items.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task GetStreamTimelineAsync_ForwardsBearerToken_AndBuildsExpectedEndpoint() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        HttpRequestMessage? capturedRequest = null;
        IAdminAuthContext authContext = Substitute.For<IAdminAuthContext>();
        authContext.GetToken().Returns("stream-token");
        PagedResult<TimelineEntry> expected = new([], 0, null);

        daprClient.InvokeMethodAsync<PagedResult<TimelineEntry>>(
            Arg.Do<HttpRequestMessage>(request => capturedRequest = request),
            Arg.Any<CancellationToken>())
            .Returns(_ => expected);

        DaprStreamQueryService service = CreateService(daprClient, authContext);

        PagedResult<TimelineEntry> result = await service.GetStreamTimelineAsync(
            "tenant1",
            "orders",
            "order-1",
            10,
            20,
            50);

        result.ShouldBe(expected);
        capturedRequest.ShouldNotBeNull();
        capturedRequest!.Method.ShouldBe(HttpMethod.Get);
        capturedRequest.RequestUri!.ToString().ShouldContain("api/v1/admin/streams/tenant1/orders/order-1/timeline");
        capturedRequest.RequestUri!.ToString().ShouldContain("from=10");
        capturedRequest.RequestUri!.ToString().ShouldContain("to=20");
        capturedRequest.RequestUri!.ToString().ShouldContain("count=50");
        capturedRequest.Headers.Authorization!.Parameter.ShouldBe("stream-token");
    }

    [Fact]
    public async Task GetAggregateStateAtPositionAsync_ReturnsFallback_WhenEventStoreUnavailable() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.InvokeMethodAsync<AggregateStateSnapshot>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Service unavailable"));

        DaprStreamQueryService service = CreateService(daprClient);

        AggregateStateSnapshot result = await service.GetAggregateStateAtPositionAsync(
            "tenant1", "orders", "order-1", 5);

        result.ShouldNotBeNull();
        result.TenantId.ShouldBe("tenant1");
        result.Domain.ShouldBe("orders");
        result.AggregateId.ShouldBe("order-1");
        result.SequenceNumber.ShouldBe(5);
    }

    [Fact]
    public async Task DiffAggregateStateAsync_ReturnsFallback_WhenEventStoreUnavailable() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.InvokeMethodAsync<AggregateStateDiff>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Service unavailable"));

        DaprStreamQueryService service = CreateService(daprClient);

        AggregateStateDiff result = await service.DiffAggregateStateAsync(
            "tenant1", "orders", "order-1", 1, 5);

        result.ShouldNotBeNull();
        result.FromSequence.ShouldBe(1);
        result.ToSequence.ShouldBe(5);
        result.ChangedFields.ShouldBeEmpty();
    }

    [Fact]
    public async Task TraceCausationChainAsync_ReturnsFallback_WhenEventStoreUnavailable() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.InvokeMethodAsync<CausationChain>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Service unavailable"));

        DaprStreamQueryService service = CreateService(daprClient);

        CausationChain result = await service.TraceCausationChainAsync(
            "tenant1", "orders", "order-1", 5);

        result.ShouldNotBeNull();
        result.Events.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAggregateBlameAsync_ThrowsException_WhenEventStoreUnavailable() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.InvokeMethodAsync<AggregateBlameView>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Service unavailable"));

        DaprStreamQueryService service = CreateService(daprClient);

        await Should.ThrowAsync<InvalidOperationException>(
            () => service.GetAggregateBlameAsync("tenant1", "orders", "order-1", 5));
    }

    [Fact]
    public async Task GetRecentlyActiveStreamsAsync_PropagatesCancellation() {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.GetStateAsync<List<StreamSummary>>(
            StateStoreName,
            Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns<List<StreamSummary>?>(_ => throw new OperationCanceledException());

        DaprStreamQueryService service = CreateService(daprClient);

        await Should.ThrowAsync<OperationCanceledException>(
            () => service.GetRecentlyActiveStreamsAsync("tenant1", null, ct: cts.Token));
    }

    [Fact]
    public async Task GetEventDetailAsync_ReturnsFallback_WhenEventStoreUnavailable() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.InvokeMethodAsync<EventDetail>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Service unavailable"));

        DaprStreamQueryService service = CreateService(daprClient);

        EventDetail result = await service.GetEventDetailAsync(
            "tenant1", "orders", "order-1", 5);

        result.ShouldNotBeNull();
        result.TenantId.ShouldBe("tenant1");
    }

    [Fact]
    public async Task GetEventStepFrameAsync_ThrowsArgumentException_WhenSequenceNumberLessThanOne() {
        DaprStreamQueryService service = CreateService();

        await Should.ThrowAsync<ArgumentException>(
            () => service.GetEventStepFrameAsync("tenant1", "orders", "order-1", 0));
    }

    [Fact]
    public async Task GetEventStepFrameAsync_ThrowsException_WhenEventStoreUnavailable() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.InvokeMethodAsync<EventStepFrame>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Service unavailable"));

        DaprStreamQueryService service = CreateService(daprClient);

        await Should.ThrowAsync<InvalidOperationException>(
            () => service.GetEventStepFrameAsync("tenant1", "orders", "order-1", 5));
    }

    [Fact]
    public async Task GetEventStepFrameAsync_ReturnsFallback_WhenResultIsNull() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.InvokeMethodAsync<EventStepFrame>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .Returns((EventStepFrame?)null);

        DaprStreamQueryService service = CreateService(daprClient);

        EventStepFrame result = await service.GetEventStepFrameAsync(
            "tenant1", "orders", "order-1", 5);

        result.ShouldNotBeNull();
        result.TenantId.ShouldBe("tenant1");
        result.TotalEvents.ShouldBe(0);
    }

    [Fact]
    public async Task GetEventStepFrameAsync_PropagatesCancellation() {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.InvokeMethodAsync<EventStepFrame>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .Returns<EventStepFrame?>(_ => throw new OperationCanceledException());

        DaprStreamQueryService service = CreateService(daprClient);

        await Should.ThrowAsync<OperationCanceledException>(
            () => service.GetEventStepFrameAsync("tenant1", "orders", "order-1", 5, cts.Token));
    }
}
