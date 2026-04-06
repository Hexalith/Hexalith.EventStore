#pragma warning disable CS8620 // Nullability mismatch in NSubstitute Returns() with nullable Dapr client methods

using System.Net;

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Projections;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Admin.Server.Services;
using Hexalith.EventStore.Admin.Server.Tests.Helpers;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

namespace Hexalith.EventStore.Admin.Server.Tests.Services;

public class DaprProjectionQueryServiceTests {
    private const string StateStoreName = "statestore";

    private static (DaprProjectionQueryService Service, TestHttpMessageHandler Handler) CreateService(
        DaprClient? daprClient = null,
        IAdminAuthContext? authContext = null) {
        daprClient ??= Substitute.For<DaprClient>();
        authContext ??= new NullAdminAuthContext();
        IOptions<AdminServerOptions> options = Options.Create(new AdminServerOptions {
            StateStoreName = StateStoreName,
            EventStoreAppId = "eventstore",
        });

        var handler = new TestHttpMessageHandler();
        HttpClient httpClient = new(handler) { BaseAddress = new Uri("http://localhost") };
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        var service = new DaprProjectionQueryService(
            daprClient,
            httpClientFactory,
            options,
            authContext,
            NullLogger<DaprProjectionQueryService>.Instance);

        return (service, handler);
    }

    [Fact]
    public async Task ListProjectionsAsync_ReturnsProjections_WhenIndexExists() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var projections = new List<ProjectionStatus>
        {
            new("OrderSummary", "tenant1", ProjectionStatusType.Running, 0, 10.5, 0, 100, DateTimeOffset.UtcNow),
            new("ShippingView", "tenant1", ProjectionStatusType.Paused, 50, 0, 1, 50, DateTimeOffset.UtcNow),
        };

        daprClient.GetStateAsync<List<ProjectionStatus>>(
            StateStoreName,
            "admin:projections:tenant1",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => projections);

        (DaprProjectionQueryService service, _) = CreateService(daprClient);

        IReadOnlyList<ProjectionStatus> result = await service.ListProjectionsAsync("tenant1");

        result.Count.ShouldBe(2);
        result[0].Name.ShouldBe("OrderSummary");
    }

    [Fact]
    public async Task ListProjectionsAsync_ReturnsEmpty_WhenIndexNotFound() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.GetStateAsync<List<ProjectionStatus>>(
            StateStoreName,
            Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (List<ProjectionStatus>?)null);

        (DaprProjectionQueryService service, _) = CreateService(daprClient);

        IReadOnlyList<ProjectionStatus> result = await service.ListProjectionsAsync("tenant1");

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetProjectionDetailAsync_ReturnsFallback_WhenEventStoreUnavailable() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        (DaprProjectionQueryService service, TestHttpMessageHandler handler) = CreateService(daprClient);
        handler.SetupNullJsonResponse();

        ProjectionDetail result = await service.GetProjectionDetailAsync("tenant1", "OrderSummary");

        result.ShouldNotBeNull();
        result.Name.ShouldBe("OrderSummary");
        result.TenantId.ShouldBe("tenant1");
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].Message.ShouldContain("not-found");
    }

    [Fact]
    public async Task ListProjectionsAsync_Throws_WhenDaprThrows()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.GetStateAsync<List<ProjectionStatus>>(
            StateStoreName,
            Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns<List<ProjectionStatus>?>(_ => throw new InvalidOperationException("Connection failed"));

        (DaprProjectionQueryService service, _) = CreateService(daprClient);

        await Should.ThrowAsync<InvalidOperationException>(
            () => service.ListProjectionsAsync("tenant1"));
    }

    [Fact]
    public async Task ListProjectionsAsync_PropagatesCancellation()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.GetStateAsync<List<ProjectionStatus>>(
            StateStoreName,
            Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns<List<ProjectionStatus>?>(_ => throw new OperationCanceledException());

        (DaprProjectionQueryService service, _) = CreateService(daprClient);

        await Should.ThrowAsync<OperationCanceledException>(
            () => service.ListProjectionsAsync("tenant1", cts.Token));
    }

    [Fact]
    public async Task GetProjectionDetailAsync_ReturnsProjectionDetail_WhenEventStoreSucceeds() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        IAdminAuthContext authContext = Substitute.For<IAdminAuthContext>();
        authContext.GetToken().Returns("projection-token");
        ProjectionDetail expected = new(
            "OrderSummary",
            "tenant1",
            ProjectionStatusType.Running,
            3,
            12.5,
            0,
            42,
            DateTimeOffset.UtcNow,
            [],
            "{\"mode\":\"live\"}",
            ["OrderCreated"]);

        (DaprProjectionQueryService service, TestHttpMessageHandler handler) = CreateService(daprClient, authContext);
        handler.SetupJsonResponse(expected);

        ProjectionDetail result = await service.GetProjectionDetailAsync("tenant1", "OrderSummary");

        result.Name.ShouldBe(expected.Name);
        result.TenantId.ShouldBe(expected.TenantId);
        result.Status.ShouldBe(expected.Status);
        result.Lag.ShouldBe(expected.Lag);
        result.LastProcessedPosition.ShouldBe(expected.LastProcessedPosition);
        result.SubscribedEventTypes.Count.ShouldBe(1);
        result.SubscribedEventTypes[0].ShouldBe("OrderCreated");
        handler.LastRequest.ShouldNotBeNull();
        handler.LastRequest!.Method.ShouldBe(HttpMethod.Get);
        handler.LastRequest.RequestUri!.ToString().ShouldContain("api/v1/admin/projections/tenant1/OrderSummary");
        handler.LastRequest.Headers.Authorization!.Parameter.ShouldBe("projection-token");
    }
}
