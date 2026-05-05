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

using ContractsTenantMember = Hexalith.Tenants.Contracts.Queries.TenantMember;
using ContractsTenantMemberPage = Hexalith.Tenants.Contracts.Queries.PaginatedResult<Hexalith.Tenants.Contracts.Queries.TenantMember>;
using ContractsTenantSummary = Hexalith.Tenants.Contracts.Queries.TenantSummary;
using ContractsTenantSummaryPage = Hexalith.Tenants.Contracts.Queries.PaginatedResult<Hexalith.Tenants.Contracts.Queries.TenantSummary>;

namespace Hexalith.EventStore.Admin.Server.Tests.Services;

public class DaprTenantQueryServiceTests {
    private const string EventStoreAppId = "eventstore";

    private static (DaprTenantQueryService Service, TestHttpMessageHandler Handler) CreateService(
        DaprClient? daprClient = null,
        IAdminAuthContext? authContext = null) {
        daprClient ??= Substitute.For<DaprClient>();
        _ = daprClient.CreateInvokeMethodRequest(Arg.Any<HttpMethod>(), Arg.Any<string>(), Arg.Any<string>())
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
        _ = httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

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

    private static SubmitQueryResponse CreateFailedEnvelope(string errorMessage)
        => new(
            "corr-1",
            JsonDocument.Parse("null").RootElement.Clone(),
            Success: false,
            ErrorMessage: errorMessage);

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

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => service.ListTenantsAsync());
    }

    [Fact]
    public async Task ListTenantsAsync_ThrowsTimeoutException_WhenQueryTimesOut() {
        (DaprTenantQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupException(new OperationCanceledException());

        _ = await Should.ThrowAsync<TimeoutException>(
            () => service.ListTenantsAsync());
    }

    [Fact]
    public async Task ListTenantsAsync_Throws_WhenQueryResponseBodyIsNull() {
        (DaprTenantQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupNullJsonResponse();

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => service.ListTenantsAsync());
    }

    [Fact]
    public async Task ListTenantsAsync_PropagatesCancellation() {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        (DaprTenantQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupException(new OperationCanceledException());

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => service.ListTenantsAsync(cts.Token));
    }

    // ST2: tenant query actors serialize TenantStatus with JsonStringEnumConverter
    // ("Active"); legacy clients may emit numeric (0). Both wire formats must deserialize.
    [Theory]
    [InlineData("\"Active\"", TenantStatusType.Active)]
    [InlineData("0", TenantStatusType.Active)]
    [InlineData("\"Disabled\"", TenantStatusType.Disabled)]
    [InlineData("1", TenantStatusType.Disabled)]
    public async Task ListTenantsAsync_DeserializesTenantStatus_ForBothStringAndNumericPayloads(
        string statusJson,
        TenantStatusType expected) {
        string payloadJson = $$"""
            {
              "items": [
                { "tenantId": "tenant1", "name": "Acme Corp", "status": {{statusJson}} }
              ],
              "cursor": null,
              "hasMore": false
            }
            """;
        SubmitQueryResponse response = new("corr-1", JsonDocument.Parse(payloadJson).RootElement.Clone());

        (DaprTenantQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupJsonResponse(response);

        IReadOnlyList<TenantSummary> result = await service.ListTenantsAsync();

        result.Count.ShouldBe(1);
        result[0].TenantId.ShouldBe("tenant1");
        result[0].Status.ShouldBe(expected);
    }

    // ST2: same matrix for tenant member roles delivered to GetTenantUsersAsync.
    [Theory]
    [InlineData("\"TenantOwner\"", "TenantOwner")]
    [InlineData("0", "TenantOwner")]
    [InlineData("\"TenantContributor\"", "TenantContributor")]
    [InlineData("1", "TenantContributor")]
    [InlineData("\"TenantReader\"", "TenantReader")]
    [InlineData("2", "TenantReader")]
    public async Task GetTenantUsersAsync_DeserializesTenantRole_ForBothStringAndNumericPayloads(
        string roleJson,
        string expectedRole) {
        string payloadJson = $$"""
            {
              "items": [
                { "userId": "user-001", "role": {{roleJson}} }
              ],
              "cursor": null,
              "hasMore": false
            }
            """;
        SubmitQueryResponse response = new("corr-1", JsonDocument.Parse(payloadJson).RootElement.Clone());

        (DaprTenantQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupJsonResponse(response);

        IReadOnlyList<TenantUser> result = await service.GetTenantUsersAsync("tenant1");

        result.Count.ShouldBe(1);
        result[0].UserId.ShouldBe("user-001");
        result[0].Role.ShouldBe(expectedRole);
    }

    // ST4: failed envelopes must be classified BEFORE attempting payload deserialization.
    // Forbidden -> HttpRequestException(403). Detail/Users see "Tenant not found" -> HttpRequestException(404).
    // List sees "Tenant not found" as upstream contract error -> TenantQueryFailedException (502, never empty list).
    // Unrecognized error -> TenantQueryFailedException (502 via dedicated path, never 503).
    [Fact]
    public async Task ListTenantsAsync_FailedEnvelopeForbidden_ThrowsHttpRequestForbiddenAsync() {
        (DaprTenantQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupJsonResponse(CreateFailedEnvelope("Forbidden"));

        HttpRequestException ex = await Should.ThrowAsync<HttpRequestException>(() => service.ListTenantsAsync());
        ex.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListTenantsAsync_FailedEnvelopeTenantNotFound_ThrowsTenantQueryFailedAsync() {
        // List queries treat "Tenant not found" as a contract error (must not silently empty-list).
        (DaprTenantQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupJsonResponse(CreateFailedEnvelope("Tenant not found"));

        TenantQueryFailedException ex = await Should.ThrowAsync<TenantQueryFailedException>(() => service.ListTenantsAsync());
        ex.UpstreamMessage.ShouldBe("Tenant not found");
    }

    [Fact]
    public async Task ListTenantsAsync_FailedEnvelopeUnrecognized_ThrowsTenantQueryFailedAsync() {
        (DaprTenantQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupJsonResponse(CreateFailedEnvelope("Catastrophic upstream pipeline failure"));

        TenantQueryFailedException ex = await Should.ThrowAsync<TenantQueryFailedException>(() => service.ListTenantsAsync());
        ex.UpstreamMessage.ShouldBe("Catastrophic upstream pipeline failure");
    }

    [Fact]
    public async Task GetTenantDetailAsync_FailedEnvelopeForbidden_ThrowsHttpRequestForbiddenAsync() {
        (DaprTenantQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupJsonResponse(CreateFailedEnvelope("Forbidden"));

        HttpRequestException ex = await Should.ThrowAsync<HttpRequestException>(() => service.GetTenantDetailAsync("tenant-a"));
        ex.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetTenantDetailAsync_FailedEnvelopeTenantNotFound_ReturnsNullAsync() {
        // Detail catches HttpRequestException(NotFound) thrown by the envelope guard and returns null.
        (DaprTenantQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupJsonResponse(CreateFailedEnvelope("Tenant not found"));

        TenantDetail? result = await service.GetTenantDetailAsync("missing-tenant");

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetTenantDetailAsync_FailedEnvelopeUnrecognized_ThrowsTenantQueryFailedAsync() {
        (DaprTenantQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupJsonResponse(CreateFailedEnvelope("Random pipeline error"));

        TenantQueryFailedException ex = await Should.ThrowAsync<TenantQueryFailedException>(() => service.GetTenantDetailAsync("tenant-a"));
        ex.UpstreamMessage.ShouldBe("Random pipeline error");
    }

    [Fact]
    public async Task GetTenantUsersAsync_FailedEnvelopeForbidden_ThrowsHttpRequestForbiddenAsync() {
        (DaprTenantQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupJsonResponse(CreateFailedEnvelope("Forbidden"));

        HttpRequestException ex = await Should.ThrowAsync<HttpRequestException>(() => service.GetTenantUsersAsync("tenant-a"));
        ex.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetTenantUsersAsync_FailedEnvelopeTenantNotFound_ThrowsHttpRequestNotFoundAsync() {
        (DaprTenantQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupJsonResponse(CreateFailedEnvelope("Tenant not found"));

        HttpRequestException ex = await Should.ThrowAsync<HttpRequestException>(() => service.GetTenantUsersAsync("missing-tenant"));
        ex.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetTenantUsersAsync_FailedEnvelopeUnrecognized_ThrowsTenantQueryFailedAsync() {
        (DaprTenantQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupJsonResponse(CreateFailedEnvelope("Random pipeline error"));

        TenantQueryFailedException ex = await Should.ThrowAsync<TenantQueryFailedException>(() => service.GetTenantUsersAsync("tenant-a"));
        ex.UpstreamMessage.ShouldBe("Random pipeline error");
    }

    [Fact]
    public async Task ListTenantsAsync_FailedEnvelopeWithMalformedPayload_ClassifiesFromErrorMessageWithoutDeserializingAsync() {
        // Payload would fail TenantSummary construction if deserialized, proving classification
        // happens BEFORE Payload.Deserialize<T> for Success=false envelopes.
        SubmitQueryResponse response = new(
            "corr-1",
            JsonDocument.Parse("\"unparsable scalar\"").RootElement.Clone(),
            Success: false,
            ErrorMessage: "Forbidden");

        (DaprTenantQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupJsonResponse(response);

        HttpRequestException ex = await Should.ThrowAsync<HttpRequestException>(() => service.ListTenantsAsync());
        ex.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ST2: GetTenantDetailAsync uses the same shared options and must accept both wire formats.
    [Theory]
    [InlineData("\"Active\"", TenantStatusType.Active)]
    [InlineData("0", TenantStatusType.Active)]
    [InlineData("\"Disabled\"", TenantStatusType.Disabled)]
    [InlineData("1", TenantStatusType.Disabled)]
    public async Task GetTenantDetailAsync_DeserializesTenantStatus_ForBothStringAndNumericPayloads(
        string statusJson,
        TenantStatusType expected) {
        string payloadJson = $$"""
            {
              "tenantId": "tenant1",
              "name": "Acme Corp",
              "description": null,
              "status": {{statusJson}},
              "members": [],
              "configuration": {},
              "createdAt": "2026-05-05T00:00:00+00:00"
            }
            """;
        SubmitQueryResponse response = new("corr-1", JsonDocument.Parse(payloadJson).RootElement.Clone());

        (DaprTenantQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupJsonResponse(response);

        TenantDetail? result = await service.GetTenantDetailAsync("tenant1");

        result.ShouldNotBeNull();
        result.TenantId.ShouldBe("tenant1");
        result.Status.ShouldBe(expected);
    }
}
