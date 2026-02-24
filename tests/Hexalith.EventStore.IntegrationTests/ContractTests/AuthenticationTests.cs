
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.IntegrationTests.Fixtures;
using Hexalith.EventStore.IntegrationTests.Helpers;

using Shouldly;

namespace Hexalith.EventStore.IntegrationTests.ContractTests;

/// <summary>
/// Tier 3 end-to-end tests verifying JWT authentication and authorization flow
/// across the full Aspire topology (AC #3).
/// </summary>
[Trait("Category", "E2E")]
[Trait("Tier", "3")]
[Collection("AspireContractTests")]
public class AuthenticationTests {
    private readonly AspireContractTestFixture _fixture;

    public AuthenticationTests(AspireContractTestFixture fixture) => _fixture = fixture;

    /// <summary>
    /// AC #3: Request without JWT token returns 401 Unauthorized.
    /// </summary>
    [Fact]
    public async Task SubmitCommand_NoJwtToken_Returns401Unauthorized() {
        // Arrange - no Authorization header
        var body = new {
            Tenant = "tenant-a",
            Domain = "counter",
            AggregateId = "auth-test-1",
            CommandType = "IncrementCounter",
            Payload = new { id = "test" },
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/commands") {
            Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json"),
        };

        // Act
        using HttpResponseMessage response = await _fixture.CommandApiClient
            .SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// AC #3: Request with valid JWT but missing command:submit permission returns 403 Forbidden.
    /// </summary>
    [Fact]
    public async Task SubmitCommand_ValidJwtMissingSubmitPermission_Returns403Forbidden() {
        // Arrange - token with command:query only, no command:submit
        string token = TestJwtTokenGenerator.GenerateToken(
            tenants: ["tenant-a"],
            domains: ["counter"],
            permissions: ["command:query"]);

        using HttpRequestMessage request = CreateCommandRequest(token);

        // Act
        using HttpResponseMessage response = await _fixture.CommandApiClient
            .SendAsync(request);

        // Assert - should be 403 (no submit permission)
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// AC #3: Request with valid JWT including correct tenant, domain, and permissions returns 202 Accepted.
    /// </summary>
    [Fact]
    public async Task SubmitCommand_ValidJwtWithCorrectClaims_Returns202Accepted() {
        // Arrange
        string token = TestJwtTokenGenerator.GenerateToken(
            tenants: ["tenant-a"],
            domains: ["counter"],
            permissions: ["command:submit", "command:query"]);

        using HttpRequestMessage request = CreateCommandRequest(token, tenant: "tenant-a");

        // Act
        using HttpResponseMessage response = await _fixture.CommandApiClient
            .SendAsync(request);

        // Assert
        if (response.StatusCode != HttpStatusCode.Accepted) {
            string body = await response.Content.ReadAsStringAsync();
            throw new ShouldAssertException(
                $"Expected 202 Accepted but was {(int)response.StatusCode} {response.StatusCode}.\nBody:\n{body}");
        }
    }

    /// <summary>
    /// AC #3: Request with JWT for wrong tenant returns 403 (tenant validation rejection).
    /// </summary>
    [Fact]
    public async Task SubmitCommand_JwtForWrongTenant_Returns403Forbidden() {
        // Arrange - token authorizes tenant-b, but request targets tenant-a
        string token = TestJwtTokenGenerator.GenerateToken(
            tenants: ["tenant-b"],
            domains: ["counter"],
            permissions: ["command:submit"]);

        using HttpRequestMessage request = CreateCommandRequest(token, tenant: "tenant-a");

        // Act
        using HttpResponseMessage response = await _fixture.CommandApiClient
            .SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static HttpRequestMessage CreateCommandRequest(string token, string tenant = "tenant-a") {
        var body = new {
            Tenant = tenant,
            Domain = "counter",
            AggregateId = $"auth-{Guid.NewGuid():N}",
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
