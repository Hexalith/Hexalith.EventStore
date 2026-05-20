#pragma warning disable CS8620 // Nullability mismatch in NSubstitute Returns() with nullable Dapr client methods

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
        _ = httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

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

        _ = daprClient.GetStateAsync<List<ProjectionStatus>>(
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
        _ = daprClient.GetStateAsync<List<ProjectionStatus>>(
            StateStoreName,
            Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (List<ProjectionStatus>?)null);

        (DaprProjectionQueryService service, _) = CreateService(daprClient);

        IReadOnlyList<ProjectionStatus> result = await service.ListProjectionsAsync("tenant1");

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListProjectionsAsync_FallsBackToAllIndex_WhenTenantScopedIndexMissing() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var projections = new List<ProjectionStatus>
        {
            new("counter", "all", ProjectionStatusType.Running, 0, 0, 0, 0, DateTimeOffset.UnixEpoch),
        };

        _ = daprClient.GetStateAsync<List<ProjectionStatus>>(
            StateStoreName,
            "admin:projections:tenant-a",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (List<ProjectionStatus>?)null);
        _ = daprClient.GetStateAsync<List<ProjectionStatus>>(
            StateStoreName,
            "admin:projections:all",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => projections);

        (DaprProjectionQueryService service, _) = CreateService(daprClient);

        IReadOnlyList<ProjectionStatus> result = await service.ListProjectionsAsync("tenant-a");

        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("counter");
        result[0].TenantId.ShouldBe("tenant-a");
    }

    [Fact]
    public async Task ListProjectionsAsync_FiltersAllIndexFallback_ToRequestedTenant() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var projections = new List<ProjectionStatus>
        {
            new("counter", "tenant-b", ProjectionStatusType.Running, 0, 0, 0, 0, DateTimeOffset.UnixEpoch),
            new("orders", "all", ProjectionStatusType.Running, 0, 0, 0, 0, DateTimeOffset.UnixEpoch),
        };

        _ = daprClient.GetStateAsync<List<ProjectionStatus>>(
            StateStoreName,
            "admin:projections:tenant-a",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (List<ProjectionStatus>?)null);
        _ = daprClient.GetStateAsync<List<ProjectionStatus>>(
            StateStoreName,
            "admin:projections:all",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => projections);

        (DaprProjectionQueryService service, _) = CreateService(daprClient);

        IReadOnlyList<ProjectionStatus> result = await service.ListProjectionsAsync("tenant-a");

        result.Single().Name.ShouldBe("orders");
        result.Single().TenantId.ShouldBe("tenant-a");
    }

    [Fact]
    public async Task GetProjectionDetailAsync_ReturnsNull_WhenEventStoreReturnsNullBody() {
        // 200 OK with a "null" JSON body is treated as a missing detail; no fallback is used.
        DaprClient daprClient = Substitute.For<DaprClient>();
        (DaprProjectionQueryService service, TestHttpMessageHandler handler) = CreateService(daprClient);
        handler.SetupNullJsonResponse();

        ProjectionDetail? result = await service.GetProjectionDetailAsync("tenant1", "OrderSummary");

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ListProjectionsAsync_Throws_WhenDaprThrows() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetStateAsync<List<ProjectionStatus>>(
            StateStoreName,
            Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns<List<ProjectionStatus>?>(_ => throw new InvalidOperationException("Connection failed"));

        (DaprProjectionQueryService service, _) = CreateService(daprClient);

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => service.ListProjectionsAsync("tenant1"));
    }

    [Fact]
    public async Task ListProjectionsAsync_PropagatesCancellation() {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetStateAsync<List<ProjectionStatus>>(
            StateStoreName,
            Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns<List<ProjectionStatus>?>(_ => throw new OperationCanceledException());

        (DaprProjectionQueryService service, _) = CreateService(daprClient);

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => service.ListProjectionsAsync("tenant1", cts.Token));
    }

    [Fact]
    public async Task GetProjectionDetailAsync_ReturnsProjectionDetail_WhenEventStoreSucceeds() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        IAdminAuthContext authContext = Substitute.For<IAdminAuthContext>();
        _ = authContext.GetToken().Returns("projection-token");
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

        ProjectionDetail? result = await service.GetProjectionDetailAsync("tenant1", "OrderSummary");

        _ = result.ShouldNotBeNull();
        result!.Name.ShouldBe(expected.Name);
        result.TenantId.ShouldBe(expected.TenantId);
        result.Status.ShouldBe(expected.Status);
        result.Lag.ShouldBe(expected.Lag);
        result.LastProcessedPosition.ShouldBe(expected.LastProcessedPosition);
        result.SubscribedEventTypes.Count.ShouldBe(1);
        result.SubscribedEventTypes[0].ShouldBe("OrderCreated");
        _ = handler.LastRequest.ShouldNotBeNull();
        handler.LastRequest!.Method.ShouldBe(HttpMethod.Get);
        handler.LastRequest.RequestUri!.ToString().ShouldContain("api/v1/admin/projections/tenant1/OrderSummary");
        handler.LastRequest.Headers.Authorization!.Parameter.ShouldBe("projection-token");
    }

    [Theory]
    [InlineData(System.Net.HttpStatusCode.NotFound)]
    [InlineData(System.Net.HttpStatusCode.MethodNotAllowed)]
    [InlineData(System.Net.HttpStatusCode.NotImplemented)]
    public async Task GetProjectionDetailAsync_FallsBackToTenantIndex_OnUnsupportedDetailStatus(
        System.Net.HttpStatusCode upstreamStatus) {
        // AC5/AC6: Admin read-model fallback for 404/405/501 from EventStore.
        DaprClient daprClient = Substitute.For<DaprClient>();
        var lastProcessedUtc = DateTimeOffset.UtcNow;
        var projections = new List<ProjectionStatus>
        {
            new("counter", "tenant-a", ProjectionStatusType.Running, 0, 4.2, 0, 18, lastProcessedUtc),
        };
        _ = daprClient.GetStateAsync<List<ProjectionStatus>>(
            StateStoreName,
            "admin:projections:tenant-a",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => projections);

        (DaprProjectionQueryService service, TestHttpMessageHandler handler) = CreateService(daprClient);
        handler.SetupErrorResponse(upstreamStatus);

        ProjectionDetail? result = await service.GetProjectionDetailAsync("tenant-a", "counter");

        _ = result.ShouldNotBeNull();
        result!.Name.ShouldBe("counter");
        result.TenantId.ShouldBe("tenant-a");
        result.Status.ShouldBe(ProjectionStatusType.Running);
        result.Lag.ShouldBe(0);
        result.Throughput.ShouldBe(4.2);
        result.ErrorCount.ShouldBe(0);
        result.LastProcessedPosition.ShouldBe(18);
        result.LastProcessedUtc.ShouldBe(lastProcessedUtc);
        result.Errors.ShouldBeEmpty();
        result.Configuration.ShouldBe(DaprProjectionQueryService.FallbackEmptyConfiguration);
        result.SubscribedEventTypes.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetProjectionDetailAsync_PrefersTenantIndex_OverAllIndex() {
        // AC7: tenant-specific entries take precedence over `all`.
        DaprClient daprClient = Substitute.For<DaprClient>();
        var tenantIndex = new List<ProjectionStatus>
        {
            new("counter", "tenant-a", ProjectionStatusType.Running, 5, 1.0, 2, 99, DateTimeOffset.UnixEpoch),
        };
        var allIndex = new List<ProjectionStatus>
        {
            new("counter", "all", ProjectionStatusType.Paused, 0, 0, 0, 0, DateTimeOffset.UnixEpoch),
        };
        _ = daprClient.GetStateAsync<List<ProjectionStatus>>(
            StateStoreName, "admin:projections:tenant-a", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => tenantIndex);
        _ = daprClient.GetStateAsync<List<ProjectionStatus>>(
            StateStoreName, "admin:projections:all", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => allIndex);

        (DaprProjectionQueryService service, TestHttpMessageHandler handler) = CreateService(daprClient);
        handler.SetupErrorResponse(System.Net.HttpStatusCode.NotFound);

        ProjectionDetail? result = await service.GetProjectionDetailAsync("tenant-a", "counter");

        _ = result.ShouldNotBeNull();
        result!.Status.ShouldBe(ProjectionStatusType.Running);
        result.LastProcessedPosition.ShouldBe(99);
        result.ErrorCount.ShouldBe(2);
    }

    [Fact]
    public async Task GetProjectionDetailAsync_FallsBackToAllIndex_WhenTenantIndexMissesProjection() {
        // AC7: fallback may use `all` index when entry is tenant-neutral.
        DaprClient daprClient = Substitute.For<DaprClient>();
        var tenantIndex = new List<ProjectionStatus>
        {
            new("orders", "tenant-a", ProjectionStatusType.Running, 0, 0, 0, 0, DateTimeOffset.UnixEpoch),
        };
        var allIndex = new List<ProjectionStatus>
        {
            new("counter", "all", ProjectionStatusType.Running, 0, 0, 0, 7, DateTimeOffset.UnixEpoch),
        };
        _ = daprClient.GetStateAsync<List<ProjectionStatus>>(
            StateStoreName, "admin:projections:tenant-a", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => tenantIndex);
        _ = daprClient.GetStateAsync<List<ProjectionStatus>>(
            StateStoreName, "admin:projections:all", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => allIndex);

        (DaprProjectionQueryService service, TestHttpMessageHandler handler) = CreateService(daprClient);
        handler.SetupErrorResponse(System.Net.HttpStatusCode.NotFound);

        ProjectionDetail? result = await service.GetProjectionDetailAsync("tenant-a", "counter");

        _ = result.ShouldNotBeNull();
        result!.TenantId.ShouldBe("tenant-a");
        result.Name.ShouldBe("counter");
        result.LastProcessedPosition.ShouldBe(7);
    }

    [Fact]
    public async Task GetProjectionDetailAsync_DoesNotLeakTenant_WhenAllIndexHoldsDifferentTenant() {
        // AC7: never return a detail for a different tenant.
        DaprClient daprClient = Substitute.For<DaprClient>();
        var allIndex = new List<ProjectionStatus>
        {
            new("counter", "tenant-b", ProjectionStatusType.Running, 0, 0, 0, 99, DateTimeOffset.UnixEpoch),
        };
        _ = daprClient.GetStateAsync<List<ProjectionStatus>>(
            StateStoreName, "admin:projections:tenant-a", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (List<ProjectionStatus>?)null);
        _ = daprClient.GetStateAsync<List<ProjectionStatus>>(
            StateStoreName, "admin:projections:all", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => allIndex);

        (DaprProjectionQueryService service, TestHttpMessageHandler handler) = CreateService(daprClient);
        handler.SetupErrorResponse(System.Net.HttpStatusCode.NotFound);

        ProjectionDetail? result = await service.GetProjectionDetailAsync("tenant-a", "counter");

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetProjectionDetailAsync_DoesNotLeakTenant_WhenTenantIndexHoldsDifferentTenant() {
        // AC7: never trust a polluted tenant-scoped key if the row identity belongs to another tenant.
        DaprClient daprClient = Substitute.For<DaprClient>();
        var tenantIndex = new List<ProjectionStatus>
        {
            new("counter", "tenant-b", ProjectionStatusType.Running, 0, 0, 0, 99, DateTimeOffset.UnixEpoch),
        };
        _ = daprClient.GetStateAsync<List<ProjectionStatus>>(
            StateStoreName, "admin:projections:tenant-a", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => tenantIndex);
        _ = daprClient.GetStateAsync<List<ProjectionStatus>>(
            StateStoreName, "admin:projections:all", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (List<ProjectionStatus>?)null);

        (DaprProjectionQueryService service, TestHttpMessageHandler handler) = CreateService(daprClient);
        handler.SetupErrorResponse(System.Net.HttpStatusCode.NotFound);

        ProjectionDetail? result = await service.GetProjectionDetailAsync("tenant-a", "counter");

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetProjectionDetailAsync_ReturnsNull_WhenFallbackIndexesEmpty() {
        // AC8: if absent from both indexes, return a clear not-found result.
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetStateAsync<List<ProjectionStatus>>(
            StateStoreName, Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (List<ProjectionStatus>?)null);

        (DaprProjectionQueryService service, TestHttpMessageHandler handler) = CreateService(daprClient);
        handler.SetupErrorResponse(System.Net.HttpStatusCode.NotFound);

        ProjectionDetail? result = await service.GetProjectionDetailAsync("tenant-a", "counter");

        result.ShouldBeNull();
    }

    [Theory]
    [InlineData(System.Net.HttpStatusCode.Unauthorized)]
    [InlineData(System.Net.HttpStatusCode.Forbidden)]
    [InlineData(System.Net.HttpStatusCode.Conflict)]
    [InlineData(System.Net.HttpStatusCode.InternalServerError)]
    [InlineData(System.Net.HttpStatusCode.BadGateway)]
    [InlineData(System.Net.HttpStatusCode.ServiceUnavailable)]
    public async Task GetProjectionDetailAsync_DoesNotFallback_OnNonAllowedStatuses(
        System.Net.HttpStatusCode upstreamStatus) {
        // AC6: must NOT fallback on 401/403/409/500/etc.; surface upstream failure.
        DaprClient daprClient = Substitute.For<DaprClient>();
        (DaprProjectionQueryService service, TestHttpMessageHandler handler) = CreateService(daprClient);
        handler.SetupErrorResponse(upstreamStatus);

        _ = await Should.ThrowAsync<HttpRequestException>(
            () => service.GetProjectionDetailAsync("tenant-a", "counter"));

        // The fallback path must not even consult the admin index when the upstream status is
        // outside the narrow allow-list.
        await daprClient.DidNotReceive().GetStateAsync<List<ProjectionStatus>>(
            Arg.Any<string>(), Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetProjectionDetailAsync_UsesBoundedToken_ForFallbackStateReads() {
        // The fallback state reads should stay under the same configured invocation timeout.
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetStateAsync<List<ProjectionStatus>>(
            StateStoreName, Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (List<ProjectionStatus>?)null);

        (DaprProjectionQueryService service, TestHttpMessageHandler handler) = CreateService(daprClient);
        handler.SetupErrorResponse(System.Net.HttpStatusCode.NotFound);

        _ = await service.GetProjectionDetailAsync("tenant-a", "counter");

        await daprClient.Received().GetStateAsync<List<ProjectionStatus>>(
            StateStoreName,
            "admin:projections:tenant-a",
            cancellationToken: Arg.Is<CancellationToken>(token => token.CanBeCanceled));
    }

    [Fact]
    public async Task GetProjectionDetailAsync_EmitsStructuredFallbackLog() {
        // AC9: emit informational log on fallback with tenant id, projection name, upstream status,
        // and fallback source key — payload-free.
        DaprClient daprClient = Substitute.For<DaprClient>();
        var projections = new List<ProjectionStatus>
        {
            new("counter", "tenant-a", ProjectionStatusType.Running, 0, 0, 0, 18, DateTimeOffset.UnixEpoch),
        };
        _ = daprClient.GetStateAsync<List<ProjectionStatus>>(
            StateStoreName, "admin:projections:tenant-a", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => projections);

        var recorder = new Helpers.RecordingLogger<DaprProjectionQueryService>();
        IOptions<AdminServerOptions> options = Options.Create(new AdminServerOptions {
            StateStoreName = StateStoreName,
            EventStoreAppId = "eventstore",
        });
        var handler = new TestHttpMessageHandler();
        handler.SetupErrorResponse(System.Net.HttpStatusCode.NotFound);
        HttpClient httpClient = new(handler) { BaseAddress = new Uri("http://localhost") };
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        _ = httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);
        var service = new DaprProjectionQueryService(daprClient, httpClientFactory, options, new NullAdminAuthContext(), recorder);

        ProjectionDetail? result = await service.GetProjectionDetailAsync("tenant-a", "counter");

        _ = result.ShouldNotBeNull();
        recorder.Records.ShouldContain(entry =>
            entry.Level == Microsoft.Extensions.Logging.LogLevel.Information
            && entry.Message.Contains("Projection detail fallback used", StringComparison.Ordinal)
            && entry.Message.Contains("tenant-a", StringComparison.Ordinal)
            && entry.Message.Contains("counter", StringComparison.Ordinal)
            && entry.Message.Contains("404", StringComparison.Ordinal)
            && entry.Message.Contains("admin:projections:tenant-a", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetProjectionDetailAsync_LogsAllIndexSource_WhenAllIndexSuppliesTenantRow() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var projections = new List<ProjectionStatus>
        {
            new("counter", "tenant-a", ProjectionStatusType.Running, 0, 0, 0, 18, DateTimeOffset.UnixEpoch),
        };
        _ = daprClient.GetStateAsync<List<ProjectionStatus>>(
            StateStoreName, "admin:projections:tenant-a", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (List<ProjectionStatus>?)null);
        _ = daprClient.GetStateAsync<List<ProjectionStatus>>(
            StateStoreName, "admin:projections:all", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => projections);

        var recorder = new Helpers.RecordingLogger<DaprProjectionQueryService>();
        IOptions<AdminServerOptions> options = Options.Create(new AdminServerOptions {
            StateStoreName = StateStoreName,
            EventStoreAppId = "eventstore",
        });
        var handler = new TestHttpMessageHandler();
        handler.SetupErrorResponse(System.Net.HttpStatusCode.NotFound);
        HttpClient httpClient = new(handler) { BaseAddress = new Uri("http://localhost") };
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        _ = httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);
        var service = new DaprProjectionQueryService(daprClient, httpClientFactory, options, new NullAdminAuthContext(), recorder);

        ProjectionDetail? result = await service.GetProjectionDetailAsync("tenant-a", "counter");

        _ = result.ShouldNotBeNull();
        recorder.Records.ShouldContain(entry =>
            entry.Level == Microsoft.Extensions.Logging.LogLevel.Information
            && entry.Message.Contains("Projection detail fallback used", StringComparison.Ordinal)
            && entry.Message.Contains("admin:projections:all", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetProjectionDetailAsync_EmitsStructuredFallbackMissLog_WhenIndexEmpty() {
        // AC9: fallback miss is also logged structurally.
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetStateAsync<List<ProjectionStatus>>(
            StateStoreName, Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (List<ProjectionStatus>?)null);

        var recorder = new Helpers.RecordingLogger<DaprProjectionQueryService>();
        IOptions<AdminServerOptions> options = Options.Create(new AdminServerOptions {
            StateStoreName = StateStoreName,
            EventStoreAppId = "eventstore",
        });
        var handler = new TestHttpMessageHandler();
        handler.SetupErrorResponse(System.Net.HttpStatusCode.NotFound);
        HttpClient httpClient = new(handler) { BaseAddress = new Uri("http://localhost") };
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        _ = httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);
        var service = new DaprProjectionQueryService(daprClient, httpClientFactory, options, new NullAdminAuthContext(), recorder);

        ProjectionDetail? result = await service.GetProjectionDetailAsync("tenant-a", "counter");

        result.ShouldBeNull();
        recorder.Records.ShouldContain(entry =>
            entry.Level == Microsoft.Extensions.Logging.LogLevel.Information
            && entry.Message.Contains("Projection detail fallback miss", StringComparison.Ordinal)
            && entry.Message.Contains("tenant-a", StringComparison.Ordinal)
            && entry.Message.Contains("counter", StringComparison.Ordinal)
            && entry.Message.Contains("404", StringComparison.Ordinal));
    }
}
