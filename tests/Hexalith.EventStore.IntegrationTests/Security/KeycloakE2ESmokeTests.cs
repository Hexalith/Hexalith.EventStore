
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using Shouldly;

namespace Hexalith.EventStore.IntegrationTests.Security;
/// <summary>
/// Smoke tests validating that the full Aspire topology with Keycloak
/// can start, acquire real OIDC tokens, and make authenticated API calls (D11).
/// </summary>
[Trait("Category", "E2E")]
[Collection("AspireTopology")]
public class KeycloakE2ESmokeTests : KeycloakE2ETestBase {
    public KeycloakE2ESmokeTests(AspireTopologyFixture fixture)
        : base(fixture) {
    }

    /// <summary>
    /// Verifies that a real OIDC token can be acquired from Keycloak
    /// for the admin test user and used to call the EventStore successfully.
    /// This is the primary smoke test for the Keycloak integration (Task 8.6).
    /// </summary>
    [Fact]
    public async Task AuthenticatedCommandSubmission_WithKeycloakToken_ReturnsAccepted() {
        // Arrange: acquire a real OIDC token from Keycloak
        string token = await GetTokenAsync("admin-user", "admin-pass");
        token.ShouldNotBeNullOrEmpty("Keycloak token acquisition failed");

        var request = new {
            Tenant = "tenant-a",
            Domain = "orders",
            AggregateId = Guid.NewGuid().ToString(),
            CommandType = "IncrementCounter",
            Payload = new { orderId = "smoke-test-001", amount = 42.00 },
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/commands") {
            Content = new StringContent(
                JsonSerializer.Serialize(request),
                Encoding.UTF8,
                "application/json"),
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        using HttpResponseMessage response = await EventStoreClient
            .SendAsync(httpRequest);

        // Assert: 202 Accepted means the full auth pipeline (Keycloak OIDC discovery,
        // JWT validation, claims transformation, tenant authorization) succeeded.
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    /// <summary>
    /// Verifies that a request without a token is rejected with 401 Unauthorized.
    /// </summary>
    [Fact]
    public async Task UnauthenticatedRequest_Returns401() {
        var request = new {
            Tenant = "tenant-a",
            Domain = "orders",
            AggregateId = Guid.NewGuid().ToString(),
            CommandType = "IncrementCounter",
            Payload = new { orderId = "unauth-test", amount = 1.00 },
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/commands") {
            Content = new StringContent(
                JsonSerializer.Serialize(request),
                Encoding.UTF8,
                "application/json"),
        };

        // No Authorization header set

        using HttpResponseMessage response = await EventStoreClient
            .SendAsync(httpRequest);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
