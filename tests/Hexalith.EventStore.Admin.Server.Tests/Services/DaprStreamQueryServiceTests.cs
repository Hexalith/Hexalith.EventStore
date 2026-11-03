#pragma warning disable CS8620 // Nullability mismatch in NSubstitute Returns() with nullable Dapr client methods

using System.Reflection;
using System.Security.Claims;

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Commands;
using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Admin.Server.Authorization;
using Hexalith.EventStore.Admin.Server.Controllers;
using Hexalith.EventStore.Admin.Server.Services;
using Hexalith.EventStore.Admin.Server.Tests.Helpers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Problems;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Testing.Security;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
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
    public async Task GetRecentCommandsAsync_PlatformRead_ForwardsValidatedScopeAndHumanBearer() {
        IAdminAuthContext authContext = Substitute.For<IAdminAuthContext>();
        _ = authContext.GetToken().Returns("operator-token");
        _ = authContext.GetValidatedPlatformScope().Returns(AdminPlatformScopeCatalog.Namespace);
        (DaprStreamQueryService service, TestHttpMessageHandler handler) = CreateService(authContext: authContext);
        handler.SetupJsonResponse(new PagedResult<CommandSummary>([], 0, null));

        _ = await service.GetRecentCommandsAsync(null, null, null);

        handler.RequestCount.ShouldBe(1);
        HttpRequestMessage request = handler.LastRequest.ShouldNotBeNull();
        request.Headers.Authorization!.Parameter.ShouldBe("operator-token");
        request.Headers.GetValues("X-Hexalith-Scope").ShouldBe([AdminPlatformScopeCatalog.Namespace]);
        request.Headers.Contains("X-Hexalith-Tenant").ShouldBeFalse();
        request.RequestUri!.ToString().ShouldNotContain("tenantId=");
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task GlobalHealthMetrics_RequireAdmittedScopeBeforeNestedListReads(bool includeScope, bool includePermission) {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var httpContext = new DefaultHttpContext {
            User = new ClaimsPrincipal(new ClaimsIdentity(includePermission
                ? [new Claim("eventstore:permission", AdminPlatformScopeCatalog.ReadPermission)] : [], "Test")),
        };
        httpContext.Request.Headers.Authorization = "Bearer health-operator-token";
        if (includeScope) {
            httpContext.Request.Headers["X-Hexalith-Scope"] = AdminPlatformScopeCatalog.Namespace;
        }

        var descriptor = new ControllerActionDescriptor {
            ControllerTypeInfo = typeof(AdminHealthController).GetTypeInfo(),
            MethodInfo = typeof(AdminHealthController).GetMethod(nameof(AdminHealthController.GetSystemHealth))!,
        };
        var context = new ActionExecutingContext(
            new ActionContext(httpContext, new RouteData(), descriptor), [], new Dictionary<string, object?>(), new object());
        var authContext = new HttpContextAdminAuthContext(new HttpContextAccessor { HttpContext = httpContext });
        (DaprStreamQueryService service, TestHttpMessageHandler handler) = CreateService(daprClient, authContext);
        handler.SetupJsonResponse(new PagedResult<CommandSummary>([], 0, null));
        var filter = new AdminTenantAuthorizationFilter(NullLogger<AdminTenantAuthorizationFilter>.Instance);
        bool actionCalled = false;

        await filter.OnActionExecutionAsync(context, async () => {
            actionCalled = true;
            _ = await service.GetRecentlyActiveStreamsAsync(null, null);
            _ = await service.GetRecentCommandsAsync(null, null, null);
            return new ActionExecutedContext(context, [], context.Controller);
        });

        bool allowed = includeScope && includePermission;
        actionCalled.ShouldBe(allowed);
        handler.RequestCount.ShouldBe(allowed ? 1 : 0);
        if (allowed) {
            HttpRequestMessage request = handler.LastRequest.ShouldNotBeNull();
            request.Headers.Authorization!.Parameter.ShouldBe("health-operator-token");
            request.Headers.GetValues("X-Hexalith-Scope").ShouldBe([AdminPlatformScopeCatalog.Namespace]);
        }
        else {
            context.Result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
            daprClient.ReceivedCalls().ShouldBeEmpty();
        }
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(null, "platform:tenants")]
    [InlineData("tenant-a", "platform:eventstore-admin")]
    public async Task ListReads_MissingOrConflictingScope_RejectBeforeHttpOrStateAccess(string? tenantId, string? scope) {
        DaprClient daprClient = Substitute.For<DaprClient>();
        IAdminAuthContext authContext = Substitute.For<IAdminAuthContext>();
        _ = authContext.GetValidatedPlatformScope().Returns(scope);
        (DaprStreamQueryService service, TestHttpMessageHandler handler) = CreateService(daprClient, authContext);

        _ = await Should.ThrowAsync<UnauthorizedAccessException>(
            () => service.GetRecentCommandsAsync(tenantId, null, null));
        _ = await Should.ThrowAsync<UnauthorizedAccessException>(
            () => service.GetRecentlyActiveStreamsAsync(tenantId, null));

        handler.RequestCount.ShouldBe(0);
        daprClient.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task GetRecentlyActiveStreamsAsync_ValidatedPlatformRead_PreservesCrossTenantResults() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        IAdminAuthContext authContext = Substitute.For<IAdminAuthContext>();
        _ = authContext.GetValidatedPlatformScope().Returns(AdminPlatformScopeCatalog.Namespace);
        _ = daprClient.GetStateAsync<List<StreamSummary>>(
            StateStoreName, "admin:stream-activity:all", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new List<StreamSummary> {
                new("tenant-a", "orders", "order-1", 1, DateTimeOffset.UtcNow, 1, false, StreamStatus.Active),
                new("tenant-b", "orders", "order-2", 2, DateTimeOffset.UtcNow, 2, false, StreamStatus.Active),
            });
        (DaprStreamQueryService service, _) = CreateService(daprClient, authContext);

        PagedResult<StreamSummary> result = await service.GetRecentlyActiveStreamsAsync(null, null);

        result.Items.Select(item => item.TenantId).ShouldBe(["tenant-a", "tenant-b"], ignoreOrder: true);
    }

    [Fact]
    public async Task GetRecentCommandsAsync_TenantRead_ForwardsCanonicalTenantWithoutPlatformScope() {
        (DaprStreamQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupJsonResponse(new PagedResult<CommandSummary>([], 0, null));

        _ = await service.GetRecentCommandsAsync("Acme", null, null);

        HttpRequestMessage request = handler.LastRequest.ShouldNotBeNull();
        request.RequestUri!.ToString().ShouldContain("tenantId=acme");
        request.Headers.Contains("X-Hexalith-Scope").ShouldBeFalse();
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
    public async Task GetEventDetailAsync_DownstreamNotFound_ThrowsKeyNotFoundException() {
        (DaprStreamQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupErrorResponse(System.Net.HttpStatusCode.NotFound);

        KeyNotFoundException ex = await Should.ThrowAsync<KeyNotFoundException>(
            () => service.GetEventDetailAsync("tenant1", "orders", "order-1", 5));
        ex.Message.ShouldBe("Event not found.");
    }

    [Fact]
    public async Task GetEventDetailAsync_DownstreamBadRequest_ThrowsArgumentException() {
        (DaprStreamQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupErrorResponse(System.Net.HttpStatusCode.BadRequest);

        _ = await Should.ThrowAsync<ArgumentException>(
            () => service.GetEventDetailAsync("tenant1", "orders", "order-1", 5));
    }

    [Fact]
    public async Task GetEventDetailAsync_DownstreamServerError_ThrowsHttpRequestException() {
        (DaprStreamQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupErrorResponse(System.Net.HttpStatusCode.InternalServerError);

        HttpRequestException ex = await Should.ThrowAsync<HttpRequestException>(
            () => service.GetEventDetailAsync("tenant1", "orders", "order-1", 5));
        ex.StatusCode.ShouldBe(System.Net.HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task GetEventDetailAsync_DownstreamServiceUnavailable_ThrowsHttpRequestException() {
        (DaprStreamQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupErrorResponse(System.Net.HttpStatusCode.ServiceUnavailable);

        HttpRequestException ex = await Should.ThrowAsync<HttpRequestException>(
            () => service.GetEventDetailAsync("tenant1", "orders", "order-1", 5));
        ex.StatusCode.ShouldBe(System.Net.HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task GetEventDetailAsync_ProtectedProblemDetails_PreservesSafeExtensionsAndDropsProviderPrivateValues() {
        (DaprStreamQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupJsonResponse(new {
            type = UnreadableProtectedDataProblem.TypeUri,
            title = UnreadableProtectedDataProblem.DefaultTitle,
            status = 503,
            detail = "Protection provider is temporarily unavailable. Retry later with backoff.",
            correlationId = "corr-1",
            reasonCode = "provider-unavailable",
            stage = "admin-inspection",
            tenantId = "tenant-a",
            domain = "Counter",
            aggregateId = "agg-1",
            sequenceNumber = 5,
            retryable = true,
            permanent = false,
            providerPrivateMetadata = ProtectedDataLeakSentinel.ProtectedProviderPrivateBlob,
            rawException = ProtectedDataLeakSentinel.ProtectedProviderExceptionText,
        }, System.Net.HttpStatusCode.ServiceUnavailable);

        AdminUpstreamProblemException ex = await Should.ThrowAsync<AdminUpstreamProblemException>(
            () => service.GetEventDetailAsync("tenant-a", "Counter", "agg-1", 5));

        ex.StatusCode.ShouldBe(System.Net.HttpStatusCode.ServiceUnavailable);
        ex.ProblemDetails.Type.ShouldBe(UnreadableProtectedDataProblem.TypeUri);
        ex.ProblemDetails.Extensions["reasonCode"].ShouldBe("provider-unavailable");
        ex.ProblemDetails.Extensions["stage"].ShouldBe("admin-inspection");
        ex.ProblemDetails.Extensions.ContainsKey("providerPrivateMetadata").ShouldBeFalse();
        ex.ProblemDetails.Extensions.ContainsKey("rawException").ShouldBeFalse();

        string serialized = System.Text.Json.JsonSerializer.Serialize(ex.ProblemDetails);
        ProtectedDataLeakSentinel.AssertNoLeak([serialized]);
    }

    [Fact]
    public async Task GetEventDetailAsync_ProtectedProblemDetails_DropsNestedValuesEvenWhenExtensionNameIsAllowed() {
        (DaprStreamQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupJsonResponse(new {
            type = UnreadableProtectedDataProblem.TypeUri,
            title = UnreadableProtectedDataProblem.DefaultTitle,
            status = 503,
            detail = "Protection provider is temporarily unavailable. Retry later with backoff.",
            correlationId = "corr-1",
            reasonCode = new {
                code = "provider-unavailable",
                providerPrivateMetadata = ProtectedDataLeakSentinel.ProtectedProviderPrivateBlob,
            },
            stage = "admin-inspection",
        }, System.Net.HttpStatusCode.ServiceUnavailable);

        AdminUpstreamProblemException ex = await Should.ThrowAsync<AdminUpstreamProblemException>(
            () => service.GetEventDetailAsync("tenant-a", "Counter", "agg-1", 5));

        ex.ProblemDetails.Extensions.ContainsKey("reasonCode").ShouldBeFalse();
        ex.ProblemDetails.Extensions["stage"].ShouldBe("admin-inspection");
        string serialized = System.Text.Json.JsonSerializer.Serialize(ex.ProblemDetails);
        ProtectedDataLeakSentinel.AssertNoLeak([serialized]);
    }

    [Fact]
    public async Task GetEventDetailAsync_NullJsonBody_ThrowsKeyNotFoundException() {
        (DaprStreamQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupNullJsonResponse();

        KeyNotFoundException ex = await Should.ThrowAsync<KeyNotFoundException>(
            () => service.GetEventDetailAsync("tenant1", "orders", "order-1", 5));
        ex.Message.ShouldBe("Event not found.");
    }

    [Fact]
    public async Task GetEventDetailAsync_BuildsCanonicalEndpoint_AndForwardsBearerToken() {
        IAdminAuthContext authContext = Substitute.For<IAdminAuthContext>();
        _ = authContext.GetToken().Returns("event-detail-token");

        DateTimeOffset timestamp = new(2026, 5, 5, 12, 0, 0, TimeSpan.Zero);
        EventDetail expected = new("tenant 1", "orders", "order/1", 7, "OrderShipped", timestamp, "corr-1", null, null, "{\"value\":1}");

        (DaprStreamQueryService service, TestHttpMessageHandler handler) = CreateService(authContext: authContext);
        handler.SetupJsonResponse(expected);

        EventDetail result = await service.GetEventDetailAsync("tenant 1", "orders", "order/1", 7);

        result.SequenceNumber.ShouldBe(7);
        _ = handler.LastRequest.ShouldNotBeNull();
        handler.LastRequest!.Method.ShouldBe(HttpMethod.Get);
        string url = handler.LastRequest.RequestUri!.AbsoluteUri;
        url.ShouldContain("api/v1/admin/streams/tenant%201/orders/order%2F1/events/7");
        url.ShouldNotContain("?");
        handler.LastRequest.Headers.Authorization!.Parameter.ShouldBe("event-detail-token");
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
