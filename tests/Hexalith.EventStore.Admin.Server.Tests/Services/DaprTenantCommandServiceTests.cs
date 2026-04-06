using System.Net;

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

public class DaprTenantCommandServiceTests
{
    private const string TenantServiceAppId = "tenants";

    private static (DaprTenantCommandService Service, TestHttpMessageHandler Handler) CreateService(
        DaprClient? daprClient = null,
        IAdminAuthContext? authContext = null)
    {
        daprClient ??= Substitute.For<DaprClient>();
        authContext ??= new NullAdminAuthContext();
        IOptions<AdminServerOptions> options = Options.Create(new AdminServerOptions
        {
            TenantServiceAppId = TenantServiceAppId,
            ServiceInvocationTimeoutSeconds = 30,
        });

        var handler = new TestHttpMessageHandler();
        HttpClient httpClient = new(handler) { BaseAddress = new Uri("http://localhost") };
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

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
    public async Task CreateTenantAsync_ReturnsSuccess_WhenTenantServiceResponds()
    {
        var expected = new AdminOperationResult(true, "op-1", "Tenant created", null);
        (DaprTenantCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupJsonResponse(expected);

        AdminOperationResult result = await service.CreateTenantAsync(
            new CreateTenantRequest("acme-corp", "Acme Corp", "Standard", 10000, 1000000));

        result.Success.ShouldBeTrue();
        result.OperationId.ShouldBe("op-1");
    }

    [Fact]
    public async Task CreateTenantAsync_ForwardsJwtToken()
    {
        IAdminAuthContext authContext = Substitute.For<IAdminAuthContext>();
        authContext.GetToken().Returns("tenant-admin-token");

        (DaprTenantCommandService service, TestHttpMessageHandler handler) = CreateService(authContext: authContext);
        handler.SetupJsonResponse(new AdminOperationResult(true, "op-1", null, null));

        await service.CreateTenantAsync(
            new CreateTenantRequest("acme-corp", "Acme Corp", "Standard", 10000, 1000000));

        handler.LastRequest.ShouldNotBeNull();
        handler.LastRequest!.Headers.Authorization!.Parameter.ShouldBe("tenant-admin-token");
    }

    [Fact]
    public async Task CreateTenantAsync_ReturnsError_WhenServiceUnavailable()
    {
        (DaprTenantCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupException(new InvalidOperationException("Tenants service down"));

        AdminOperationResult result = await service.CreateTenantAsync(
            new CreateTenantRequest("acme-corp", "Acme Corp", "Standard", 10000, 1000000));

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateTenantAsync_ReturnsNullResponseError_WhenTenantServiceReturnsNull()
    {
        (DaprTenantCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupNullJsonResponse();

        AdminOperationResult result = await service.CreateTenantAsync(
            new CreateTenantRequest("acme-corp", "Acme Corp", "Standard", 10000, 1000000));

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("NULL_RESPONSE");
    }

    [Fact]
    public async Task CreateTenantAsync_PropagatesCancellation()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        (DaprTenantCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupException(new OperationCanceledException());

        await Should.ThrowAsync<OperationCanceledException>(
            () => service.CreateTenantAsync(
                new CreateTenantRequest("acme-corp", "Acme Corp", "Standard", 10000, 1000000), cts.Token));
    }

    // === DisableTenantAsync ===

    [Fact]
    public async Task DisableTenantAsync_ReturnsSuccess_WhenTenantServiceResponds()
    {
        var expected = new AdminOperationResult(true, "op-2", "Tenant disabled", null);
        (DaprTenantCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupJsonResponse(expected);

        AdminOperationResult result = await service.DisableTenantAsync("acme-corp");

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task DisableTenantAsync_ReturnsError_WhenServiceUnavailable()
    {
        (DaprTenantCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupException(new InvalidOperationException("Tenants service down"));

        AdminOperationResult result = await service.DisableTenantAsync("acme-corp");

        result.Success.ShouldBeFalse();
    }

    // === EnableTenantAsync ===

    [Fact]
    public async Task EnableTenantAsync_ReturnsSuccess_WhenTenantServiceResponds()
    {
        var expected = new AdminOperationResult(true, "op-3", "Tenant enabled", null);
        (DaprTenantCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupJsonResponse(expected);

        AdminOperationResult result = await service.EnableTenantAsync("acme-corp");

        result.Success.ShouldBeTrue();
    }

    // === AddUserToTenantAsync ===

    [Fact]
    public async Task AddUserToTenantAsync_ReturnsSuccess_WhenTenantServiceResponds()
    {
        var expected = new AdminOperationResult(true, "op-4", "User added", null);
        (DaprTenantCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupJsonResponse(expected);

        AdminOperationResult result = await service.AddUserToTenantAsync("acme-corp", "user@acme.com", "Operator");

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task AddUserToTenantAsync_ReturnsError_WhenServiceUnavailable()
    {
        (DaprTenantCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupException(new HttpRequestException("Connection refused"));

        AdminOperationResult result = await service.AddUserToTenantAsync("acme-corp", "user@acme.com", "Operator");

        result.Success.ShouldBeFalse();
    }

    // === RemoveUserFromTenantAsync ===

    [Fact]
    public async Task RemoveUserFromTenantAsync_ReturnsSuccess_WhenTenantServiceResponds()
    {
        var expected = new AdminOperationResult(true, "op-5", "User removed", null);
        (DaprTenantCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupJsonResponse(expected);

        AdminOperationResult result = await service.RemoveUserFromTenantAsync("acme-corp", "user@acme.com");

        result.Success.ShouldBeTrue();
    }

    // === ChangeUserRoleAsync ===

    [Fact]
    public async Task ChangeUserRoleAsync_ReturnsSuccess_WhenTenantServiceResponds()
    {
        var expected = new AdminOperationResult(true, "op-6", "Role changed", null);
        (DaprTenantCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupJsonResponse(expected);

        AdminOperationResult result = await service.ChangeUserRoleAsync("acme-corp", "user@acme.com", "Admin");

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task ChangeUserRoleAsync_ReturnsError_WhenServiceUnavailable()
    {
        (DaprTenantCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupException(new InvalidOperationException("Tenants service down"));

        AdminOperationResult result = await service.ChangeUserRoleAsync("acme-corp", "user@acme.com", "Admin");

        result.Success.ShouldBeFalse();
    }

    // === Error code extraction ===

    [Fact]
    public async Task InvokePost_ExtractsHttpStatusCode_FromHttpRequestException()
    {
        (DaprTenantCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupErrorResponse(HttpStatusCode.Conflict);

        AdminOperationResult result = await service.CreateTenantAsync(
            new CreateTenantRequest("acme-corp", "Acme Corp", "Standard", 10000, 1000000));

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("409");
    }
}
