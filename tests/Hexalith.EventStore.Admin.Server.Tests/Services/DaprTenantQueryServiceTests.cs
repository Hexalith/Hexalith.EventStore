#pragma warning disable CS8620 // Nullability mismatch in NSubstitute Returns() with nullable Dapr client methods

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Admin.Server.Services;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Hexalith.EventStore.Admin.Server.Tests.Services;

public class DaprTenantQueryServiceTests {
    private const string TenantServiceAppId = "tenants";

    private static DaprTenantQueryService CreateService(DaprClient? daprClient = null) {
        daprClient ??= Substitute.For<DaprClient>();
        IOptions<AdminServerOptions> options = Options.Create(new AdminServerOptions {
            TenantServiceAppId = TenantServiceAppId,
        });

        return new DaprTenantQueryService(
            daprClient,
            options,
            new NullAdminAuthContext(),
            NullLogger<DaprTenantQueryService>.Instance);
    }

    [Fact]
    public async Task ListTenantsAsync_ReturnsTenants_WhenServiceAvailable() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var tenants = new List<TenantSummary>
        {
            new("tenant1", "Acme Corp", TenantStatusType.Active, 1000, 5),
            new("tenant2", "Widget Co", TenantStatusType.Suspended, 500, 2),
        };

        daprClient.InvokeMethodAsync<IReadOnlyList<TenantSummary>>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .Returns(_ => (IReadOnlyList<TenantSummary>?)tenants);

        DaprTenantQueryService service = CreateService(daprClient);

        IReadOnlyList<TenantSummary> result = await service.ListTenantsAsync();

        result.Count.ShouldBe(2);
        result[0].TenantId.ShouldBe("tenant1");
    }

    [Fact]
    public async Task ListTenantsAsync_ReturnsEmpty_WhenServiceUnavailable() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.InvokeMethodAsync<IReadOnlyList<TenantSummary>>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Tenants service down"));

        DaprTenantQueryService service = CreateService(daprClient);

        IReadOnlyList<TenantSummary> result = await service.ListTenantsAsync();

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetTenantQuotasAsync_ReturnsQuotas_WhenServiceAvailable() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var quotas = new TenantQuotas("tenant1", 10000, 1000000, 500000);

        daprClient.InvokeMethodAsync<TenantQuotas>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .Returns(_ => quotas);

        DaprTenantQueryService service = CreateService(daprClient);

        TenantQuotas result = await service.GetTenantQuotasAsync("tenant1");

        result.TenantId.ShouldBe("tenant1");
        result.MaxEventsPerDay.ShouldBe(10000);
    }

    [Fact]
    public async Task GetTenantQuotasAsync_ReturnsFallback_WhenServiceUnavailable() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.InvokeMethodAsync<TenantQuotas>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Tenants service down"));

        DaprTenantQueryService service = CreateService(daprClient);

        TenantQuotas result = await service.GetTenantQuotasAsync("tenant1");

        result.TenantId.ShouldBe("tenant1");
        result.MaxEventsPerDay.ShouldBe(0);
    }

    [Fact]
    public async Task CompareTenantUsageAsync_ReturnsFallback_WhenServiceUnavailable() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.InvokeMethodAsync<TenantComparison>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Tenants service down"));

        DaprTenantQueryService service = CreateService(daprClient);

        TenantComparison result = await service.CompareTenantUsageAsync(["tenant1", "tenant2"]);

        result.Tenants.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListTenantsAsync_PropagatesCancellation()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.InvokeMethodAsync<IReadOnlyList<TenantSummary>>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<TenantSummary>?>(_ => throw new OperationCanceledException());

        DaprTenantQueryService service = CreateService(daprClient);

        await Should.ThrowAsync<OperationCanceledException>(
            () => service.ListTenantsAsync(cts.Token));
    }

    [Fact]
    public async Task CompareTenantUsageAsync_ReturnsEmpty_WhenTenantIdsEmpty()
    {
        DaprTenantQueryService service = CreateService();

        TenantComparison result = await service.CompareTenantUsageAsync([]);

        result.Tenants.ShouldBeEmpty();
    }

    [Fact]
    public async Task CompareTenantUsageAsync_DelegatesToTenantService_WithBearerToken() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        HttpRequestMessage? capturedRequest = null;
        IAdminAuthContext authContext = Substitute.For<IAdminAuthContext>();
        authContext.GetToken().Returns("tenant-token");
        TenantComparison expected = new(
            [new TenantSummary("tenant1", "Acme Corp", TenantStatusType.Active, 1000, 5)],
            DateTimeOffset.UtcNow);

        daprClient.InvokeMethodAsync<TenantComparison>(
            Arg.Do<HttpRequestMessage>(request => capturedRequest = request),
            Arg.Any<CancellationToken>())
            .Returns(_ => expected);

        DaprTenantQueryService service = new(
            daprClient,
            Options.Create(new AdminServerOptions {
                TenantServiceAppId = TenantServiceAppId,
            }),
            authContext,
            NullLogger<DaprTenantQueryService>.Instance);

        TenantComparison result = await service.CompareTenantUsageAsync(["tenant1", "tenant2"]);

        result.ShouldBe(expected);
        capturedRequest.ShouldNotBeNull();
        capturedRequest!.Method.ShouldBe(HttpMethod.Post);
        capturedRequest.RequestUri!.ToString().ShouldContain("api/v1/tenants/compare");
        capturedRequest.Headers.Authorization!.Parameter.ShouldBe("tenant-token");
    }
}
