using System.Net;
using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Admin.Server.Services;
using Hexalith.EventStore.Admin.Server.Tests.Helpers;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

namespace Hexalith.EventStore.Admin.Server.Tests.Services;

public class DaprTenantCommandServiceTests {
    private const string EventStoreAppId = "eventstore";

    private static (DaprTenantCommandService Service, TestHttpMessageHandler Handler) CreateService(
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

        var service = new DaprTenantCommandService(
            daprClient,
            httpClientFactory,
            options,
            authContext,
            NullLogger<DaprTenantCommandService>.Instance);

        return (service, handler);
    }

    // === CreateTenantAsync ===

    [Fact]
    public async Task CreateTenantAsync_ReturnsSuccess_WhenTenantServiceResponds() {
        (DaprTenantCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupEmptyResponse(HttpStatusCode.Accepted);

        AdminOperationResult result = await service.CreateTenantAsync(
            new CreateTenantRequest("acme-corp", "Acme Corp", null));

        result.Success.ShouldBeTrue();
        result.OperationId.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CreateTenantAsync_ForwardsJwtToken() {
        IAdminAuthContext authContext = Substitute.For<IAdminAuthContext>();
        _ = authContext.GetToken().Returns("tenant-admin-token");

        (DaprTenantCommandService service, TestHttpMessageHandler handler) = CreateService(authContext: authContext);
        handler.SetupEmptyResponse(HttpStatusCode.Accepted);

        _ = await service.CreateTenantAsync(
            new CreateTenantRequest("acme-corp", "Acme Corp", null));

        _ = handler.LastRequest.ShouldNotBeNull();
        handler.LastRequest!.Headers.Authorization!.Parameter.ShouldBe("tenant-admin-token");
    }

    [Fact]
    public async Task CreateTenantAsync_ReturnsError_WhenServiceUnavailable() {
        (DaprTenantCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupException(new InvalidOperationException("Tenants service down"));

        AdminOperationResult result = await service.CreateTenantAsync(
            new CreateTenantRequest("acme-corp", "Acme Corp", null));

        result.Success.ShouldBeFalse();
        _ = result.ErrorCode.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateTenantAsync_ReturnsUnexpectedStatusError_WhenCommandEndpointReturnsOk() {
        (DaprTenantCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupJsonResponse(new { correlationId = "corr-1" }, HttpStatusCode.OK);

        AdminOperationResult result = await service.CreateTenantAsync(
            new CreateTenantRequest("acme-corp", "Acme Corp", null));

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("UNEXPECTED_STATUS");
    }

    [Fact]
    public async Task CreateTenantAsync_PropagatesCancellation() {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        (DaprTenantCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupException(new OperationCanceledException());

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => service.CreateTenantAsync(
                new CreateTenantRequest("acme-corp", "Acme Corp", null), cts.Token));
    }

    // === DisableTenantAsync ===

    [Fact]
    public async Task DisableTenantAsync_ReturnsSuccess_WhenTenantServiceResponds() {
        (DaprTenantCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupEmptyResponse(HttpStatusCode.Accepted);

        AdminOperationResult result = await service.DisableTenantAsync("acme-corp");

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task DisableTenantAsync_ReturnsError_WhenServiceUnavailable() {
        (DaprTenantCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupException(new InvalidOperationException("Tenants service down"));

        AdminOperationResult result = await service.DisableTenantAsync("acme-corp");

        result.Success.ShouldBeFalse();
    }

    // === EnableTenantAsync ===

    [Fact]
    public async Task EnableTenantAsync_ReturnsSuccess_WhenTenantServiceResponds() {
        (DaprTenantCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupEmptyResponse(HttpStatusCode.Accepted);

        AdminOperationResult result = await service.EnableTenantAsync("acme-corp");

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task EnableTenantAsync_UsesSortableIdentifiers_ForMessageAndCorrelationIds() {
        (DaprTenantCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupEmptyResponse(HttpStatusCode.Accepted);

        AdminOperationResult result = await service.EnableTenantAsync("manual-test-tenant-a");

        using JsonDocument body = JsonDocument.Parse(handler.LastRequestBody.ShouldNotBeNull());
        string messageId = body.RootElement.GetProperty("messageId").GetString().ShouldNotBeNull();
        string correlationId = body.RootElement.GetProperty("correlationId").GetString().ShouldNotBeNull();

        messageId.ShouldMatch("^[0-9A-HJKMNP-TV-Z]{26}$");
        correlationId.ShouldMatch("^[0-9A-HJKMNP-TV-Z]{26}$");
        result.OperationId.ShouldBe(correlationId);
    }

    [Fact]
    public async Task EnableTenantAsync_PreservesOperationId_WhenInvocationTimesOutAfterCommandBodyExists() {
        (DaprTenantCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupException(new OperationCanceledException("simulated DAPR timeout"));

        AdminOperationResult result = await service.EnableTenantAsync("manual-test-tenant-a");

        using JsonDocument body = JsonDocument.Parse(handler.LastRequestBody.ShouldNotBeNull());
        string correlationId = body.RootElement.GetProperty("correlationId").GetString().ShouldNotBeNull();

        result.Success.ShouldBeFalse();
        result.OperationId.ShouldBe(correlationId);
        result.OperationId.ShouldNotBe("error-no-operation");
        result.ErrorCode.ShouldBe("timeout");
        string message = result.Message.ShouldNotBeNull();
        message.ShouldContain("may still be processing");
        message.ShouldNotContain("OperationCanceledException");
        message.ShouldNotContain("simulated DAPR timeout");
    }

    [Fact]
    public async Task EnableTenantAsync_PreservesUnavailableCode_WhenProblemDetailsContainsType() {
        (DaprTenantCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupResponse(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) {
            Content = new StringContent(
                """{"type":"https://httpstatuses.com/503","title":"Service Unavailable","detail":"upstream down"}"""),
        });

        AdminOperationResult result = await service.EnableTenantAsync("manual-test-tenant-a");

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("unavailable");
        result.Message.ShouldNotBeNull().ShouldContain("503");
    }

    [Fact]
    public async Task EnableTenantAsync_ClassifiesUnexpectedExceptionAsUnexpected_AfterOperationIdExists() {
        (DaprTenantCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupException(new InvalidOperationException("local serialization defect"));

        AdminOperationResult result = await service.EnableTenantAsync("manual-test-tenant-a");

        using JsonDocument body = JsonDocument.Parse(handler.LastRequestBody.ShouldNotBeNull());
        string correlationId = body.RootElement.GetProperty("correlationId").GetString().ShouldNotBeNull();

        result.Success.ShouldBeFalse();
        result.OperationId.ShouldBe(correlationId);
        result.ErrorCode.ShouldBe("unexpected");
        result.Message.ShouldNotBeNull().ShouldContain("Unexpected tenant command failure");
        result.Message.ShouldNotContain("local serialization defect");
    }

    // === AddUserToTenantAsync ===

    [Fact]
    public async Task AddUserToTenantAsync_ReturnsSuccess_WhenTenantServiceResponds() {
        (DaprTenantCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupEmptyResponse(HttpStatusCode.Accepted);

        AdminOperationResult result = await service.AddUserToTenantAsync("acme-corp", "user-001", "tenantowner");

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task AddUserToTenantAsync_TrimsRoleBeforeValidation() {
        (DaprTenantCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupEmptyResponse(HttpStatusCode.Accepted);

        AdminOperationResult result = await service.AddUserToTenantAsync("acme-corp", "user-001", "  TenantOwner  ");

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task AddUserToTenantAsync_ReturnsError_WhenRoleIsNumeric() {
        (DaprTenantCommandService service, _) = CreateService();

        AdminOperationResult result = await service.AddUserToTenantAsync("acme-corp", "user-001", "0");

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("invalid-request");
    }

    [Fact]
    public async Task AddUserToTenantAsync_ReturnsError_WhenServiceUnavailable() {
        (DaprTenantCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupException(new HttpRequestException("Connection refused"));

        AdminOperationResult result = await service.AddUserToTenantAsync("acme-corp", "user-001", "TenantOwner");

        result.Success.ShouldBeFalse();
    }

    // === RemoveUserFromTenantAsync ===

    [Fact]
    public async Task RemoveUserFromTenantAsync_ReturnsSuccess_WhenTenantServiceResponds() {
        (DaprTenantCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupEmptyResponse(HttpStatusCode.Accepted);

        AdminOperationResult result = await service.RemoveUserFromTenantAsync("acme-corp", "user-001");

        result.Success.ShouldBeTrue();
    }

    // === ChangeUserRoleAsync ===

    [Fact]
    public async Task ChangeUserRoleAsync_ReturnsSuccess_WhenTenantServiceResponds() {
        (DaprTenantCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupEmptyResponse(HttpStatusCode.Accepted);

        AdminOperationResult result = await service.ChangeUserRoleAsync("acme-corp", "user-001", "tenantcontributor");

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task ChangeUserRoleAsync_ReturnsError_WhenServiceUnavailable() {
        (DaprTenantCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupException(new InvalidOperationException("Tenants service down"));

        AdminOperationResult result = await service.ChangeUserRoleAsync("acme-corp", "user-001", "TenantOwner");

        result.Success.ShouldBeFalse();
    }

    [Fact]
    public async Task CreateTenantAsync_DoesNotExposeRawErrorBody_WhenCommandFails() {
        (DaprTenantCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupResponse(new HttpResponseMessage(HttpStatusCode.Conflict) {
            Content = new StringContent("sensitive backend details"),
        });

        AdminOperationResult result = await service.CreateTenantAsync(
            new CreateTenantRequest("acme-corp", "Acme Corp", null));

        result.Success.ShouldBeFalse();
        _ = result.Message.ShouldNotBeNull();
        result.Message.ShouldContain("409");
        result.Message.ShouldNotContain("sensitive backend details");
    }

    // === Error code extraction ===

    [Fact]
    public async Task InvokePost_ExtractsHttpStatusCode_FromHttpRequestException() {
        (DaprTenantCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupErrorResponse(HttpStatusCode.Conflict);

        AdminOperationResult result = await service.CreateTenantAsync(
            new CreateTenantRequest("acme-corp", "Acme Corp", null));

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("409");
    }
}
