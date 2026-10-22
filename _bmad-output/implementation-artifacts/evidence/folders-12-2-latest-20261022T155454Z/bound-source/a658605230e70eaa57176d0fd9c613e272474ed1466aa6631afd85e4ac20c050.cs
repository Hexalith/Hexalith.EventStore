using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Authorization;

using NSubstitute;

namespace Hexalith.EventStore.Admin.Server.Tests.IntegrationTests;

public class AdminAuthorizationIntegrationTests : IDisposable {
    private readonly AdminTestHost _host;
    private readonly HttpClient _client;

    public AdminAuthorizationIntegrationTests() {
        _host = new AdminTestHost();
        _client = _host.CreateClient();
    }

    [Fact]
    public async Task NoAuth_Returns401() {
        // No claims header → auth handler returns Fail → 401.
        // Route renamed in 48d5126e: the stream list endpoint is now
        // /api/v1/admin/streams/GetRecentlyActiveStreams (was bare /api/v1/admin/streams).
        HttpResponseMessage response = await _client.GetAsync("/api/v1/admin/streams/GetRecentlyActiveStreams");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ReadOnlyRole_GetStreams_NotForbiddenOrUnauthorized() {
        SetClaims(
            new Claim(AdminClaimTypes.AdminRole, "ReadOnly"),
            new Claim(AdminClaimTypes.Tenant, "tenant-a"));

        HttpResponseMessage response = await _client.GetAsync("/api/v1/admin/streams/GetRecentlyActiveStreams");

        // Authorization should pass (mock service returns null → 200/204, not 401/403)
        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ReadOnlyRole_PostProjectionPause_Returns403() {
        SetClaims(
            new Claim(AdminClaimTypes.AdminRole, "ReadOnly"),
            new Claim(AdminClaimTypes.Tenant, "tenant-a"));

        HttpResponseMessage response = await _client.PostAsync(
            "/api/v1/admin/projections/tenant-a/proj1/pause",
            null);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task OperatorRole_PostProjectionPause_NotForbiddenOrUnauthorized() {
        SetClaims(
            new Claim(AdminClaimTypes.AdminRole, "Operator"),
            new Claim(AdminClaimTypes.Tenant, "tenant-a"));

        using var content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        HttpResponseMessage response = await _client.PostAsync(
            "/api/v1/admin/projections/tenant-a/proj1/pause",
            content);

        // Authorization should pass (mock service may cause 500 — but NOT 401/403)
        response.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden);
        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task OperatorRole_GetTenants_ReturnsForbidden() {
        SetClaims(
            new Claim(AdminClaimTypes.AdminRole, "Operator"),
            new Claim(AdminClaimTypes.Tenant, "tenant-a"));

        using HttpResponseMessage response = await _client.GetAsync("/api/v1/admin/tenants");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminRole_GetTenants_ReturnsOk() {
        ITenantQueryService tenantQueryService = _host.GetService<ITenantQueryService>();
        IReadOnlyList<TenantSummary> expectedTenants =
        [
            new("tenant-a", "Tenant A", TenantStatusType.Active),
        ];
        _ = tenantQueryService
            .ListTenantsAsync(Arg.Any<CancellationToken>())
            .Returns(expectedTenants);

        SetClaims(
            new Claim(AdminClaimTypes.AdminRole, "Admin"),
            new Claim(AdminClaimTypes.Tenant, "tenant-a"));

        using HttpResponseMessage response = await _client.GetAsync("/api/v1/admin/tenants");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        IReadOnlyList<TenantSummary>? tenants = await response.Content
            .ReadFromJsonAsync<IReadOnlyList<TenantSummary>>();
        tenants.ShouldNotBeNull();
        tenants.Count.ShouldBe(1);
        tenants[0].ShouldBe(expectedTenants[0]);
        _ = tenantQueryService.Received(1).ListTenantsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AdminRole_PostTenant_ReturnsAccepted() {
        ITenantCommandService tenantCommandService = _host.GetService<ITenantCommandService>();
        _ = tenantCommandService
            .CreateTenantAsync(Arg.Any<CreateTenantRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AdminOperationResult(true, "01JAXYZ1234567890ABCDEFGH", "Accepted", null));

        SetClaims(
            new Claim(AdminClaimTypes.AdminRole, "Admin"),
            new Claim(AdminClaimTypes.Tenant, "tenant-a"));

        var request = new CreateTenantRequest("tenant-a", "Tenant A", "Test tenant");
        using HttpResponseMessage response = await _client.PostAsJsonAsync("/api/v1/admin/tenants", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        _ = tenantCommandService.Received(1).CreateTenantAsync(
            Arg.Is<CreateTenantRequest>(value =>
                value.TenantId == request.TenantId
                && value.Name == request.Name
                && value.Description == request.Description),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidRole_WrongTenantClaim_Returns403() {
        SetClaims(
            new Claim(AdminClaimTypes.AdminRole, "Operator"),
            new Claim(AdminClaimTypes.Tenant, "tenant-b"));

        HttpResponseMessage response = await _client.PostAsync(
            "/api/v1/admin/storage/tenant-a/compact",
            null);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminRole_TenantScopedEndpoint_NotBlockedByTenantFilter() {
        // Admin users should access any tenant-scoped endpoint even without tenant claims
        SetClaims(
            new Claim(AdminClaimTypes.AdminRole, "Admin"));

        HttpResponseMessage response = await _client.GetAsync(
            "/api/v1/admin/streams/any-tenant/domain/agg/timeline");

        // Should NOT be 403 — admin is exempt from tenant filter
        response.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden);
        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
    }

    private void SetClaims(params Claim[] claims) {
        var dtos = claims.Select(c => new { c.Type, c.Value }).ToArray();
        string json = JsonSerializer.Serialize(dtos);
        _ = _client.DefaultRequestHeaders.Remove(TestAuthHandler.ClaimsHeader);
        _client.DefaultRequestHeaders.Add(TestAuthHandler.ClaimsHeader, json);
    }

    public void Dispose() {
        _client?.Dispose();
        _host?.Dispose();
        GC.SuppressFinalize(this);
    }
}
