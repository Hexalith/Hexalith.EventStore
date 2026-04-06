using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Admin.Server.Services;
using Hexalith.EventStore.Admin.Server.Tests.Helpers;
using Hexalith.EventStore.Contracts.Queries;

using Hexalith.Tenants.Contracts.Enums;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using ContractsTenantSummary = Hexalith.Tenants.Contracts.Queries.TenantSummary;
using ContractsTenantDetail = Hexalith.Tenants.Contracts.Queries.TenantDetail;
using ContractsTenantMember = Hexalith.Tenants.Contracts.Queries.TenantMember;
using ContractsTenantMemberPage = Hexalith.Tenants.Contracts.Queries.PaginatedResult<Hexalith.Tenants.Contracts.Queries.TenantMember>;
using ContractsTenantSummaryPage = Hexalith.Tenants.Contracts.Queries.PaginatedResult<Hexalith.Tenants.Contracts.Queries.TenantSummary>;

namespace Hexalith.EventStore.Admin.Server.Tests.Services;

public class DaprTenantQueryServiceTests {
    private const string EventStoreAppId = "eventstore";

    private static (DaprTenantQueryService Service, TestHttpMessageHandler Handler) CreateService(
        DaprClient? daprClient = null,
        IAdminAuthContext? authContext = null) {
        daprClient ??= Substitute.For<DaprClient>();
        daprClient.CreateInvokeMethodRequest(Arg.Any<HttpMethod>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(callInfo => new HttpRequestMessage(
                callInfo.ArgAt<HttpMethod>(0),
                callInfo.ArgAt<string>(2)));
        authContext ??= new NullAdminAuthContext();
        IOptions<AdminServerOptions> options = Options.Create(new AdminServerOptions {
            EventStoreAppId = EventStoreAppId,
            ServiceInvocationTimeoutSeconds = 30,
        });

        var handler = new TestHttpMessageHandler();
        HttpClient httpClient = new(handler) { BaseAddress = new Uri("http://localhost") };
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        var service = new DaprTenantQueryService(
            daprClient,
            httpClientFactory,
            options,
            authContext,
            NullLogger<DaprTenantQueryService>.Instance);

        return (service, handler);
    }

    private static SubmitQueryResponse CreateQueryResponse<T>(T payload)
        => new("corr-1", JsonSerializer.SerializeToElement(payload));

    private static HttpResponseMessage CreateJsonResponse<T>(T payload)
        => new(HttpStatusCode.OK) {
            Content = JsonContent.Create(payload),
        };

    [Fact]
    public async Task ListTenantsAsync_ReturnsTenants_WhenServiceAvailable() {
        ContractsTenantSummaryPage tenants = new(
            [
                new ContractsTenantSummary("tenant1", "Acme Corp", TenantStatus.Active),
                new ContractsTenantSummary("tenant2", "Widget Co", TenantStatus.Disabled),
            ],
            null,
            false);

        (DaprTenantQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupJsonResponse(CreateQueryResponse(tenants));

        IReadOnlyList<TenantSummary> result = await service.ListTenantsAsync();

        result.Count.ShouldBe(2);
        result[0].TenantId.ShouldBe("tenant1");
        result[1].Status.ShouldBe(TenantStatusType.Disabled);
    }

    [Fact]
    public async Task ListTenantsAsync_FollowsPaginationUntilAllPagesReturned() {
        ContractsTenantSummaryPage firstPage = new(
            [new ContractsTenantSummary("tenant1", "Acme Corp", TenantStatus.Active)],
            "tenant1",
            true);
        ContractsTenantSummaryPage secondPage = new(
            [new ContractsTenantSummary("tenant2", "Widget Co", TenantStatus.Disabled)],
            null,
            false);

        (DaprTenantQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupResponseSequence(
            CreateJsonResponse(CreateQueryResponse(firstPage)),
            CreateJsonResponse(CreateQueryResponse(secondPage)));

        IReadOnlyList<TenantSummary> result = await service.ListTenantsAsync();

        result.Select(p => p.TenantId).ShouldBe(["tenant1", "tenant2"]);
        handler.RequestCount.ShouldBe(2);

        string requestBody = handler.LastRequestBody!;
        requestBody.ShouldContain("\"cursor\":\"tenant1\"");
        requestBody.ShouldContain("\"pageSize\":100");
    }

    [Fact]
    public async Task GetTenantDetailAsync_ReturnsNull_WhenQueryReturnsNotFound() {
        (DaprTenantQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupErrorResponse(HttpStatusCode.NotFound);

        TenantDetail? result = await service.GetTenantDetailAsync("missing-tenant");

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetTenantUsersAsync_FollowsPaginationUntilAllPagesReturned() {
        ContractsTenantMemberPage firstPage = new(
            [new ContractsTenantMember("user-001", TenantRole.TenantOwner)],
            "user-001",
            true);
        ContractsTenantMemberPage secondPage = new(
            [new ContractsTenantMember("user-002", TenantRole.TenantReader)],
            null,
            false);

        (DaprTenantQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupResponseSequence(
            CreateJsonResponse(CreateQueryResponse(firstPage)),
            CreateJsonResponse(CreateQueryResponse(secondPage)));

        IReadOnlyList<TenantUser> result = await service.GetTenantUsersAsync("tenant1");

        result.Select(p => p.UserId).ShouldBe(["user-001", "user-002"]);
        handler.RequestCount.ShouldBe(2);

        string requestBody = handler.LastRequestBody!;
        requestBody.ShouldContain("\"cursor\":\"user-001\"");
        requestBody.ShouldContain("\"pageSize\":100");
    }

    [Fact]
    public async Task ListTenantsAsync_Throws_WhenServiceUnavailable() {
        (DaprTenantQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupException(new InvalidOperationException("Tenants service down"));

        await Should.ThrowAsync<InvalidOperationException>(
            () => service.ListTenantsAsync());
    }

    [Fact]
    public async Task ListTenantsAsync_ThrowsTimeoutException_WhenQueryTimesOut() {
        (DaprTenantQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupException(new OperationCanceledException());

        await Should.ThrowAsync<TimeoutException>(
            () => service.ListTenantsAsync());
    }

    [Fact]
    public async Task ListTenantsAsync_Throws_WhenQueryResponseBodyIsNull() {
        (DaprTenantQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupNullJsonResponse();

        await Should.ThrowAsync<InvalidOperationException>(
            () => service.ListTenantsAsync());
    }

    [Fact]
    public async Task ListTenantsAsync_PropagatesCancellation() {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        (DaprTenantQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupException(new OperationCanceledException());

        await Should.ThrowAsync<OperationCanceledException>(
            () => service.ListTenantsAsync(cts.Token));
    }
}
