using System.Net;
using System.Security.Claims;
using System.Text.Json;

using Hexalith.EventStore.Admin.Server.Authorization;

namespace Hexalith.EventStore.Admin.Server.Tests.IntegrationTests;

public class AdminAuthorizationIntegrationTests : IDisposable
{
    private readonly AdminTestHost _host;
    private readonly HttpClient _client;

    public AdminAuthorizationIntegrationTests()
    {
        _host = new AdminTestHost();
        _client = _host.CreateClient();
    }

    [Fact]
    public async Task NoAuth_Returns401()
    {
        // No claims header → auth handler returns Fail → 401
        HttpResponseMessage response = await _client.GetAsync("/api/v1/admin/streams");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ReadOnlyRole_GetStreams_NotForbiddenOrUnauthorized()
    {
        SetClaims(
            new Claim(AdminClaimTypes.AdminRole, "ReadOnly"),
            new Claim(AdminClaimTypes.Tenant, "tenant-a"));

        HttpResponseMessage response = await _client.GetAsync("/api/v1/admin/streams");

        // Authorization should pass (mock service returns null → 200/204, not 401/403)
        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ReadOnlyRole_PostProjectionPause_Returns403()
    {
        SetClaims(
            new Claim(AdminClaimTypes.AdminRole, "ReadOnly"),
            new Claim(AdminClaimTypes.Tenant, "tenant-a"));

        HttpResponseMessage response = await _client.PostAsync(
            "/api/v1/admin/projections/tenant-a/proj1/pause",
            null);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task OperatorRole_PostProjectionPause_NotForbiddenOrUnauthorized()
    {
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
    public async Task OperatorRole_GetTenants_Returns403()
    {
        SetClaims(
            new Claim(AdminClaimTypes.AdminRole, "Operator"),
            new Claim(AdminClaimTypes.Tenant, "tenant-a"));

        HttpResponseMessage response = await _client.GetAsync("/api/v1/admin/tenants");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminRole_GetTenants_NotForbiddenOrUnauthorized()
    {
        SetClaims(
            new Claim(AdminClaimTypes.AdminRole, "Admin"),
            new Claim(AdminClaimTypes.Tenant, "tenant-a"));

        HttpResponseMessage response = await _client.GetAsync("/api/v1/admin/tenants");

        // Authorization should pass for Admin role (mock service may return null → 204)
        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ValidRole_WrongTenantClaim_Returns403()
    {
        SetClaims(
            new Claim(AdminClaimTypes.AdminRole, "Operator"),
            new Claim(AdminClaimTypes.Tenant, "tenant-b"));

        HttpResponseMessage response = await _client.PostAsync(
            "/api/v1/admin/storage/tenant-a/compact",
            null);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminRole_TenantScopedEndpoint_NotBlockedByTenantFilter()
    {
        // Admin users should access any tenant-scoped endpoint even without tenant claims
        SetClaims(
            new Claim(AdminClaimTypes.AdminRole, "Admin"));

        HttpResponseMessage response = await _client.GetAsync(
            "/api/v1/admin/streams/any-tenant/domain/agg/timeline");

        // Should NOT be 403 — admin is exempt from tenant filter
        response.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden);
        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
    }

    private void SetClaims(params Claim[] claims)
    {
        var dtos = claims.Select(c => new { c.Type, c.Value }).ToArray();
        string json = JsonSerializer.Serialize(dtos);
        _client.DefaultRequestHeaders.Remove(TestAuthHandler.ClaimsHeader);
        _client.DefaultRequestHeaders.Add(TestAuthHandler.ClaimsHeader, json);
    }

    public void Dispose()
    {
        _client?.Dispose();
        _host?.Dispose();
        GC.SuppressFinalize(this);
    }
}
