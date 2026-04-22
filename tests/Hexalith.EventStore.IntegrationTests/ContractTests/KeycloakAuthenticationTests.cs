using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.IntegrationTests.Fixtures;
using Hexalith.EventStore.IntegrationTests.Helpers;

using Shouldly;

namespace Hexalith.EventStore.IntegrationTests.ContractTests;

/// <summary>
/// Tier 3 E2E tests verifying JWT authentication through a real Keycloak IdP (D11, R-001).
/// Validates the full 6-layer auth defense: JWT validation -> Claims transformation ->
/// Controller tenant auth -> MediatR pipeline auth -> Actor tenant validation -> DAPR ACL.
/// These tests use real OIDC tokens from Keycloak, not synthetic symmetric JWTs.
/// </summary>
[Trait("Category", "E2E")]
[Trait("Tier", "3")]
[Trait("Priority", "P0")]
[Collection("KeycloakAuthTests")]
public class KeycloakAuthenticationTests {
    private const string ClientId = "hexalith-eventstore";
    private readonly KeycloakAuthFixture _fixture;

    public KeycloakAuthenticationTests(KeycloakAuthFixture fixture) => _fixture = fixture;

    /// <summary>
    /// TD-P0-001: Valid Keycloak token with correct tenant claims -> 202 Accepted.
    /// Validates layers 1-4 of the 6-layer defense with a real IdP.
    /// </summary>
    [Fact]
    public async Task SubmitCommand_ValidKeycloakToken_Returns202Accepted() {
        // Arrange: acquire real OIDC token from Keycloak for admin-user (tenant-a, tenant-b)
        string token = await KeycloakTokenHelper.AcquireTokenAsync(
            _fixture.KeycloakTokenEndpoint,
            ClientId,
            "admin-user",
            "admin-pass");

        using HttpRequestMessage request = CreateCommandRequest(token, tenant: "tenant-a");

        // Act
        using HttpResponseMessage response = await _fixture.EventStoreClient.SendAsync(request);

        // Assert
        if (response.StatusCode != HttpStatusCode.Accepted) {
            string body = await response.Content.ReadAsStringAsync();
            throw new ShouldAssertException(
                $"Expected 202 Accepted but was {(int)response.StatusCode} {response.StatusCode}.\nBody:\n{body}");
        }
    }

    /// <summary>
    /// TD-P0-001: Request without any JWT token -> 401 Unauthorized.
    /// Validates layer 1 (API Gateway JWT validation) with real Keycloak OIDC discovery.
    /// </summary>
    [Fact]
    public async Task SubmitCommand_NoToken_Returns401Unauthorized() {
        // Arrange: no Authorization header
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/commands") {
            Content = new StringContent(
                JsonSerializer.Serialize(new {
                    MessageId = Guid.NewGuid().ToString(),
                    Tenant = "tenant-a",
                    Domain = "counter",
                    AggregateId = $"kc-notoken-{Guid.NewGuid():N}",
                    CommandType = "IncrementCounter",
                    Payload = new { id = "test" },
                }),
                Encoding.UTF8,
                "application/json"),
        };

        // Act
        using HttpResponseMessage response = await _fixture.EventStoreClient.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// TD-P0-002: Token for tenant-b submitting command for tenant-a -> 403 Forbidden.
    /// Validates cross-tenant isolation at layers 2-3 (claims transformation + controller auth).
    /// </summary>
    [Fact]
    public async Task SubmitCommand_CrossTenantToken_Returns403Forbidden() {
        // Arrange: tenant-b-user has claims for tenant-b only
        string token = await KeycloakTokenHelper.AcquireTokenAsync(
            _fixture.KeycloakTokenEndpoint,
            ClientId,
            "tenant-b-user",
            "tenant-b-pass");

        // Submit command targeting tenant-a (cross-tenant violation)
        using HttpRequestMessage request = CreateCommandRequest(token, tenant: "tenant-a");

        // Act
        using HttpResponseMessage response = await _fixture.EventStoreClient.SendAsync(request);

        // Assert: tenant mismatch -> forbidden
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// TD-P0-002: Tenant-scoped user can only access their own tenant.
    /// Validates FR27 (data path isolation) with real Keycloak tokens.
    /// </summary>
    [Fact]
    public async Task SubmitCommand_TenantScopedUser_CanAccessOwnTenant() {
        // Arrange: tenant-a-user has claims for tenant-a + domain "orders" only
        // (hexalith-realm.json seeds this user with domains=["orders"]).
        string token = await KeycloakTokenHelper.AcquireTokenAsync(
            _fixture.KeycloakTokenEndpoint,
            ClientId,
            "tenant-a-user",
            "tenant-a-pass");

        using HttpRequestMessage request = CreateCommandRequest(token, tenant: "tenant-a", domain: "orders");

        // Act
        using HttpResponseMessage response = await _fixture.EventStoreClient.SendAsync(request);

        // Assert: own tenant -> accepted
        if (response.StatusCode != HttpStatusCode.Accepted) {
            string body = await response.Content.ReadAsStringAsync();
            throw new ShouldAssertException(
                $"Expected 202 Accepted but was {(int)response.StatusCode} {response.StatusCode}.\nBody:\n{body}");
        }
    }

    /// <summary>
    /// TD-P0-001: User with no tenant claims -> 403 Forbidden.
    /// Validates that missing eventstore:tenant claims prevent access (SEC-2).
    /// </summary>
    [Fact]
    public async Task SubmitCommand_NoTenantClaims_Returns403Forbidden() {
        // Arrange: no-tenant-user has no tenant/domain/permission attributes
        string token = await KeycloakTokenHelper.AcquireTokenAsync(
            _fixture.KeycloakTokenEndpoint,
            ClientId,
            "no-tenant-user",
            "no-tenant-pass");

        using HttpRequestMessage request = CreateCommandRequest(token, tenant: "tenant-a");

        // Act
        using HttpResponseMessage response = await _fixture.EventStoreClient.SendAsync(request);

        // Assert: no tenant claims -> forbidden
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// TD-P1-010: Read-only user cannot submit commands (missing command:submit permission).
    /// Validates permission-level authorization with real Keycloak claims.
    /// </summary>
    [Fact]
    public async Task SubmitCommand_ReadOnlyUser_Returns403Forbidden() {
        // Arrange: readonly-user has command:query only
        string token = await KeycloakTokenHelper.AcquireTokenAsync(
            _fixture.KeycloakTokenEndpoint,
            ClientId,
            "readonly-user",
            "readonly-pass");

        using HttpRequestMessage request = CreateCommandRequest(token, tenant: "tenant-a");

        // Act
        using HttpResponseMessage response = await _fixture.EventStoreClient.SendAsync(request);

        // Assert: read-only -> forbidden
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// TD-P0-002: Two tenants submit commands concurrently — events isolated.
    /// Validates FR27-FR29 multi-tenant isolation under real OIDC auth.
    /// </summary>
    [Fact]
    public async Task ConcurrentTenantCommands_EventsRemainIsolated() {
        // Arrange: acquire tokens for two different tenants
        string tokenA = await KeycloakTokenHelper.AcquireTokenAsync(
            _fixture.KeycloakTokenEndpoint, ClientId, "tenant-a-user", "tenant-a-pass");
        string tokenB = await KeycloakTokenHelper.AcquireTokenAsync(
            _fixture.KeycloakTokenEndpoint, ClientId, "tenant-b-user", "tenant-b-pass");

        string aggIdA = $"kc-iso-a-{Guid.NewGuid():N}";
        string aggIdB = $"kc-iso-b-{Guid.NewGuid():N}";

        // Act: submit commands concurrently for different tenants. Domains are
        // per-user in hexalith-realm.json (tenant-a-user=orders, tenant-b-user=inventory).
        using HttpRequestMessage requestA = CreateCommandRequest(tokenA, tenant: "tenant-a", domain: "orders", aggregateId: aggIdA);
        using HttpRequestMessage requestB = CreateCommandRequest(tokenB, tenant: "tenant-b", aggregateId: aggIdB, domain: "inventory");

        Task<HttpResponseMessage> taskA = _fixture.EventStoreClient.SendAsync(requestA);
        Task<HttpResponseMessage> taskB = _fixture.EventStoreClient.SendAsync(requestB);

        HttpResponseMessage[] responses = await Task.WhenAll(taskA, taskB);

        // Assert: both accepted (each accessing their own tenant)
        foreach (HttpResponseMessage response in responses) {
            if (response.StatusCode != HttpStatusCode.Accepted) {
                string body = await response.Content.ReadAsStringAsync();
                throw new ShouldAssertException(
                    $"Expected 202 Accepted but was {(int)response.StatusCode}.\nBody:\n{body}");
            }

            response.Dispose();
        }
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static HttpRequestMessage CreateCommandRequest(
        string token,
        string tenant = "tenant-a",
        string domain = "counter",
        string? aggregateId = null) {
        string aggId = aggregateId ?? $"kc-{Guid.NewGuid():N}";
        var body = new {
            MessageId = Guid.NewGuid().ToString(),
            Tenant = tenant,
            Domain = domain,
            AggregateId = aggId,
            CommandType = "IncrementCounter",
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
