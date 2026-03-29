#pragma warning disable CS8620 // Nullability mismatch in NSubstitute Returns() with nullable Dapr client methods

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Admin.Server.Services;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Hexalith.EventStore.Admin.Server.Tests.Services;

public class DaprTenantCommandServiceTests
{
    private const string TenantServiceAppId = "tenants";

    private static DaprTenantCommandService CreateService(
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

        return new DaprTenantCommandService(
            daprClient,
            options,
            authContext,
            NullLogger<DaprTenantCommandService>.Instance);
    }

    // === CreateTenantAsync ===

    [Fact]
    public async Task CreateTenantAsync_ReturnsSuccess_WhenTenantServiceResponds()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var expected = new AdminOperationResult(true, "op-1", "Tenant created", null);

        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .Returns(_ => expected);

        DaprTenantCommandService service = CreateService(daprClient);

        AdminOperationResult result = await service.CreateTenantAsync(
            new CreateTenantRequest("acme-corp", "Acme Corp", "Standard", 10000, 1000000));

        result.Success.ShouldBeTrue();
        result.OperationId.ShouldBe("op-1");
    }

    [Fact]
    public async Task CreateTenantAsync_ForwardsJwtToken()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        HttpRequestMessage? capturedRequest = null;
        IAdminAuthContext authContext = Substitute.For<IAdminAuthContext>();
        authContext.GetToken().Returns("tenant-admin-token");

        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Do<HttpRequestMessage>(r => capturedRequest = r),
            Arg.Any<CancellationToken>())
            .Returns(_ => new AdminOperationResult(true, "op-1", null, null));

        DaprTenantCommandService service = CreateService(daprClient, authContext);

        await service.CreateTenantAsync(
            new CreateTenantRequest("acme-corp", "Acme Corp", "Standard", 10000, 1000000));

        capturedRequest.ShouldNotBeNull();
        capturedRequest!.Headers.Authorization!.Parameter.ShouldBe("tenant-admin-token");
    }

    [Fact]
    public async Task CreateTenantAsync_ReturnsError_WhenServiceUnavailable()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Tenants service down"));

        DaprTenantCommandService service = CreateService(daprClient);

        AdminOperationResult result = await service.CreateTenantAsync(
            new CreateTenantRequest("acme-corp", "Acme Corp", "Standard", 10000, 1000000));

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateTenantAsync_ReturnsNullResponseError_WhenTenantServiceReturnsNull()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .Returns(_ => (AdminOperationResult?)null);

        DaprTenantCommandService service = CreateService(daprClient);

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

        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .Returns<AdminOperationResult?>(_ => throw new OperationCanceledException());

        DaprTenantCommandService service = CreateService(daprClient);

        await Should.ThrowAsync<OperationCanceledException>(
            () => service.CreateTenantAsync(
                new CreateTenantRequest("acme-corp", "Acme Corp", "Standard", 10000, 1000000), cts.Token));
    }

    // === DisableTenantAsync ===

    [Fact]
    public async Task DisableTenantAsync_ReturnsSuccess_WhenTenantServiceResponds()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var expected = new AdminOperationResult(true, "op-2", "Tenant disabled", null);

        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .Returns(_ => expected);

        DaprTenantCommandService service = CreateService(daprClient);

        AdminOperationResult result = await service.DisableTenantAsync("acme-corp");

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task DisableTenantAsync_ReturnsError_WhenServiceUnavailable()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Tenants service down"));

        DaprTenantCommandService service = CreateService(daprClient);

        AdminOperationResult result = await service.DisableTenantAsync("acme-corp");

        result.Success.ShouldBeFalse();
    }

    // === EnableTenantAsync ===

    [Fact]
    public async Task EnableTenantAsync_ReturnsSuccess_WhenTenantServiceResponds()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var expected = new AdminOperationResult(true, "op-3", "Tenant enabled", null);

        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .Returns(_ => expected);

        DaprTenantCommandService service = CreateService(daprClient);

        AdminOperationResult result = await service.EnableTenantAsync("acme-corp");

        result.Success.ShouldBeTrue();
    }

    // === AddUserToTenantAsync ===

    [Fact]
    public async Task AddUserToTenantAsync_ReturnsSuccess_WhenTenantServiceResponds()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var expected = new AdminOperationResult(true, "op-4", "User added", null);

        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .Returns(_ => expected);

        DaprTenantCommandService service = CreateService(daprClient);

        AdminOperationResult result = await service.AddUserToTenantAsync("acme-corp", "user@acme.com", "Operator");

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task AddUserToTenantAsync_ReturnsError_WhenServiceUnavailable()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        DaprTenantCommandService service = CreateService(daprClient);

        AdminOperationResult result = await service.AddUserToTenantAsync("acme-corp", "user@acme.com", "Operator");

        result.Success.ShouldBeFalse();
    }

    // === RemoveUserFromTenantAsync ===

    [Fact]
    public async Task RemoveUserFromTenantAsync_ReturnsSuccess_WhenTenantServiceResponds()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var expected = new AdminOperationResult(true, "op-5", "User removed", null);

        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .Returns(_ => expected);

        DaprTenantCommandService service = CreateService(daprClient);

        AdminOperationResult result = await service.RemoveUserFromTenantAsync("acme-corp", "user@acme.com");

        result.Success.ShouldBeTrue();
    }

    // === ChangeUserRoleAsync ===

    [Fact]
    public async Task ChangeUserRoleAsync_ReturnsSuccess_WhenTenantServiceResponds()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var expected = new AdminOperationResult(true, "op-6", "Role changed", null);

        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .Returns(_ => expected);

        DaprTenantCommandService service = CreateService(daprClient);

        AdminOperationResult result = await service.ChangeUserRoleAsync("acme-corp", "user@acme.com", "Admin");

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task ChangeUserRoleAsync_ReturnsError_WhenServiceUnavailable()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Tenants service down"));

        DaprTenantCommandService service = CreateService(daprClient);

        AdminOperationResult result = await service.ChangeUserRoleAsync("acme-corp", "user@acme.com", "Admin");

        result.Success.ShouldBeFalse();
    }

    // === Error code extraction ===

    [Fact]
    public async Task InvokePost_ExtractsHttpStatusCode_FromHttpRequestException()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Conflict", null, System.Net.HttpStatusCode.Conflict));

        DaprTenantCommandService service = CreateService(daprClient);

        AdminOperationResult result = await service.CreateTenantAsync(
            new CreateTenantRequest("acme-corp", "Acme Corp", "Standard", 10000, 1000000));

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("409");
    }
}
