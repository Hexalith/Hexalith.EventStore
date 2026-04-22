
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using Shouldly;

namespace Hexalith.EventStore.IntegrationTests.Security;
/// <summary>
/// E2E security tests exercising the full Aspire topology with real Keycloak OIDC tokens.
/// Verifies multi-tenant isolation, permission enforcement, and authorization pipeline
/// against a running Keycloak instance with the hexalith realm (D11, Tasks 9.1-9.4).
/// </summary>
[Trait("Category", "E2E")]
[Collection("AspireTopology")]
public class KeycloakE2ESecurityTests : KeycloakE2ETestBase {
    public KeycloakE2ESecurityTests(AspireTopologyFixture fixture)
        : base(fixture) {
    }

    // ------------------------------------------------------------------
    // Task 9.1: Authenticated command submission with valid Keycloak token
    // ------------------------------------------------------------------

    /// <summary>
    /// AC #1, #2, #8: admin-user with full permissions submits a command.
    /// The real Keycloak OIDC token flows through JWT validation,
    /// claims transformation, and tenant/domain/permission authorization.
    /// </summary>
    [Fact]
    public async Task AdminUser_SubmitCommand_ReturnsAcceptedAsync() {
        // Arrange
        string token = await GetTokenAsync("admin-user", "admin-pass");

        using HttpRequestMessage request = CreateCommandRequest(
            token,
            tenant: "tenant-a",
            domain: "orders",
            commandType: "IncrementCounter");

        // Act
        using HttpResponseMessage response = await EventStoreClient
            .SendAsync(request);

        // Assert
        if (response.StatusCode != HttpStatusCode.Accepted) {
            string body = await response.Content.ReadAsStringAsync();
            throw new Shouldly.ShouldAssertException(
                $"response.StatusCode should be Accepted but was {response.StatusCode}.\nResponse body:\n{body}");
        }

        _ = response.Headers.Location.ShouldNotBeNull();
    }

    /// <summary>
    /// AC #1, #2: tenant-a-user submits a command for tenant-a/orders.
    /// Verifies scoped user (single tenant, single domain) can operate within scope.
    /// </summary>
    [Fact]
    public async Task TenantAUser_SubmitCommandForOwnTenant_ReturnsAcceptedAsync() {
        string token = await GetTokenAsync("tenant-a-user", "tenant-a-pass");

        using HttpRequestMessage request = CreateCommandRequest(
            token,
            tenant: "tenant-a",
            domain: "orders",
            commandType: "IncrementCounter");

        using HttpResponseMessage response = await EventStoreClient
            .SendAsync(request);

        if (response.StatusCode != HttpStatusCode.Accepted) {
            string body = await response.Content.ReadAsStringAsync();
            throw new Shouldly.ShouldAssertException(
                $"response.StatusCode should be Accepted but was {response.StatusCode}.\nResponse body:\n{body}");
        }
    }

    // ------------------------------------------------------------------
    // Task 9.3: Cross-tenant isolation with real tokens (AC #5, FR27, NFR13)
    // ------------------------------------------------------------------

    /// <summary>
    /// AC #5, FR27: tenant-a-user attempts command for tenant-b -> 403.
    /// Claims transformation maps Keycloak 'tenants' attribute to eventstore:tenant claims.
    /// Controller checks tenant claims before MediatR pipeline.
    /// </summary>
    [Fact]
    public async Task TenantAUser_SubmitCommandForTenantB_Returns403Async() {
        string token = await GetTokenAsync("tenant-a-user", "tenant-a-pass");

        using HttpRequestMessage request = CreateCommandRequest(
            token,
            tenant: "tenant-b",
            domain: "orders",
            commandType: "IncrementCounter");

        using HttpResponseMessage response = await EventStoreClient
            .SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// AC #5, FR27: tenant-b-user attempts command for tenant-a -> 403.
    /// Symmetric test ensuring isolation works in both directions.
    /// </summary>
    [Fact]
    public async Task TenantBUser_SubmitCommandForTenantA_Returns403Async() {
        string token = await GetTokenAsync("tenant-b-user", "tenant-b-pass");

        using HttpRequestMessage request = CreateCommandRequest(
            token,
            tenant: "tenant-a",
            domain: "inventory",
            commandType: "IncrementCounter");

        using HttpResponseMessage response = await EventStoreClient
            .SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ------------------------------------------------------------------
    // Task 9.4: Permission enforcement with real tokens
    // ------------------------------------------------------------------

    /// <summary>
    /// readonly-user has permission 'command:query' only, not 'command:submit'.
    /// Submitting a command requires 'command:submit' permission. The MediatR
    /// AuthorizationBehavior rejects with 403 because the user lacks this permission.
    /// </summary>
    [Fact]
    public async Task ReadonlyUser_SubmitCommand_Returns403Async() {
        string token = await GetTokenAsync("readonly-user", "readonly-pass");

        using HttpRequestMessage request = CreateCommandRequest(
            token,
            tenant: "tenant-a",
            domain: "orders",
            commandType: "IncrementCounter");

        using HttpResponseMessage response = await EventStoreClient
            .SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// no-tenant-user has empty tenants array in Keycloak.
    /// Controller's pre-pipeline check rejects with 403 (no tenant claims found).
    /// </summary>
    [Fact]
    public async Task NoTenantUser_SubmitCommand_Returns403Async() {
        string token = await GetTokenAsync("no-tenant-user", "no-tenant-pass");

        using HttpRequestMessage request = CreateCommandRequest(
            token,
            tenant: "tenant-a",
            domain: "orders",
            commandType: "IncrementCounter");

        using HttpResponseMessage response = await EventStoreClient
            .SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static HttpRequestMessage CreateCommandRequest(
        string token,
        string tenant,
        string domain,
        string commandType) {
        var body = new {
            MessageId = Guid.NewGuid().ToString(),
            Tenant = tenant,
            Domain = domain,
            AggregateId = Guid.NewGuid().ToString(),
            CommandType = commandType,
            Payload = new { id = Guid.NewGuid().ToString() },
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/commands") {
            Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}
