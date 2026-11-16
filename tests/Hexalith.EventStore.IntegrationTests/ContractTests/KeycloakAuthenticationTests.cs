using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.IntegrationTests.Security;
using Hexalith.EventStore.IntegrationTests.Helpers;

using Shouldly;
using StackExchange.Redis;

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
[Collection("AspireTopology")]
public class KeycloakAuthenticationTests {
    private readonly AspireTopologyFixture _fixture;

    public KeycloakAuthenticationTests(AspireTopologyFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task SubmitCommand_RealTokenWithoutSurfaceGrant_IsForbiddenBeforePersistence() {
        string username = "tenant-a-user";
        string token = await _fixture.AcquireUnmarkedTokenAsync(username);
        new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(token).Claims
            .ShouldNotContain(claim => claim.Type == "eventstore:surface");
        string aggregateId = $"unmarked-{Guid.NewGuid():N}";
        using HttpRequestMessage request = CreateCommandRequest(token, aggregateId: aggregateId);
        using HttpResponseMessage response = await _fixture.EventStoreClient.SendAsync(request);
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var redis = await ConnectionMultiplexer.ConnectAsync(_fixture.RedisEndpoint);
        string key = $"eventstore||{_fixture.AggregateActorTypeName}||tenant-a:counter:{aggregateId}||tenant-a:counter:{aggregateId}:events:1";
        (await redis.GetDatabase().KeyExistsAsync(key)).ShouldBeFalse();
    }

    [Fact]
    public async Task SubmitCommand_RealTokenWithReservedTenantGrant_IsForbiddenBeforePersistence() {
        string username = "tenant-a-user";
        string token = await _fixture.AcquireReservedTenantTokenAsync(username);
        var claims = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(token).Claims.ToArray();
        claims.ShouldContain(claim => claim.Type == "eventstore:tenant" && claim.Value == "tenant-a");
        claims.ShouldContain(claim => claim.Type == "eventstore:tenant" && claim.Value == "system");
        string aggregateId = $"reserved-{Guid.NewGuid():N}";
        using HttpRequestMessage request = CreateCommandRequest(token, aggregateId: aggregateId);
        using HttpResponseMessage response = await _fixture.EventStoreClient.SendAsync(request);
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var redis = await ConnectionMultiplexer.ConnectAsync(_fixture.RedisEndpoint);
        string key = $"eventstore||{_fixture.AggregateActorTypeName}||tenant-a:counter:{aggregateId}||tenant-a:counter:{aggregateId}:events:1";
        (await redis.GetDatabase().KeyExistsAsync(key)).ShouldBeFalse();
    }

    /// <summary>
    /// TD-P0-001: Valid Keycloak token with correct tenant claims -> 202 Accepted.
    /// Validates layers 1-4 of the 6-layer defense with a real IdP.
    /// </summary>
    [Fact]
    public async Task SubmitCommand_ValidKeycloakToken_Returns202Accepted() {
        // Arrange: acquire real OIDC token from Keycloak for admin-user (tenant-a, tenant-b)
        string username = "admin-user";
        string token = await _fixture.GetTokenAsync(username);

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
        string username = "tenant-b-user";
        string token = await _fixture.GetTokenAsync(username);

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
        // Arrange: tenant-a-user has claims for tenant-a + domain "counter" only
        // (hexalith-realm.json seeds this user with domains=["counter"]).
        string username = "tenant-a-user";
        string token = await _fixture.GetTokenAsync(username);

        using HttpRequestMessage request = CreateCommandRequest(token, tenant: "tenant-a", domain: "counter");

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
        string username = "no-tenant-user";
        string token = await _fixture.GetTokenAsync(username);

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
        string username = "readonly-user";
        string token = await _fixture.GetTokenAsync(username);

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
        string tokenA = await _fixture.GetTokenAsync("tenant-a-user");
        string tokenB = await _fixture.GetTokenAsync("tenant-b-user");

        string aggIdA = $"kc-iso-{Guid.NewGuid():N}";
        string aggIdB = aggIdA;
        string commandIdA = Guid.NewGuid().ToString();
        string commandIdB = Guid.NewGuid().ToString();
        commandIdA.ShouldNotBe(commandIdB);

        // Act: submit commands concurrently for different tenants. Domains are
        // per-user in hexalith-realm.json (tenant-a-user=counter, tenant-b-user=counter).
        using HttpRequestMessage requestA = CreateCommandRequest(tokenA, tenant: "tenant-a", domain: "counter", aggregateId: aggIdA, messageId: commandIdA);
        using HttpRequestMessage requestB = CreateCommandRequest(tokenB, tenant: "tenant-b", aggregateId: aggIdB, domain: "counter", messageId: commandIdB);

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

        // Read the actual actor state, not only the asynchronous 202 response.
        using var redis = await StackExchange.Redis.ConnectionMultiplexer.ConnectAsync(_fixture.RedisEndpoint);
        StackExchange.Redis.IDatabase database = redis.GetDatabase();
        string prefixA = $"eventstore||{_fixture.AggregateActorTypeName}||tenant-a:counter:{aggIdA}||tenant-a:counter:{aggIdA}:events:";
        string prefixB = $"eventstore||{_fixture.AggregateActorTypeName}||tenant-b:counter:{aggIdB}||tenant-b:counter:{aggIdB}:events:";
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        StackExchange.Redis.RedisValue eventA = StackExchange.Redis.RedisValue.Null;
        StackExchange.Redis.RedisValue eventB = RedisValue.Null;
        while (!eventA.HasValue || !eventB.HasValue) {
            timeout.Token.ThrowIfCancellationRequested();
            eventA = await database.HashGetAsync(prefixA + "1", "data");
            eventB = await database.HashGetAsync(prefixB + "1", "data");
            await Task.Delay(100, timeout.Token);
        }
        using JsonDocument persistedA = JsonDocument.Parse(eventA.ToString());
        using JsonDocument persistedB = JsonDocument.Parse(eventB.ToString());
        AssertPersistedIdentity(persistedA.RootElement, "tenant-a", aggIdA, commandIdA);
        AssertPersistedIdentity(persistedB.RootElement, "tenant-b", aggIdB, commandIdB);
        // A command rejected by tenant authorization never reaches actor persistence.
        string forbiddenId = $"kc-forbidden-{Guid.NewGuid():N}";
        using HttpRequestMessage forbidden = CreateCommandRequest(tokenA, tenant: "tenant-b", aggregateId: forbiddenId);
        using HttpResponseMessage rejected = await _fixture.EventStoreClient.SendAsync(forbidden);
        rejected.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        string forbiddenKey = $"eventstore||{_fixture.AggregateActorTypeName}||tenant-b:counter:{forbiddenId}||tenant-b:counter:{forbiddenId}:events:1";
        (await database.KeyExistsAsync(forbiddenKey)).ShouldBeFalse();
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static void AssertPersistedIdentity(JsonElement envelope, string tenant, string aggregateId, string commandId) {
        envelope.GetProperty("eventTypeName").GetString().ShouldBe("Hexalith.EventStore.Sample.Counter.Events.CounterIncremented");
        envelope.GetProperty("tenantId").GetString().ShouldBe(tenant);
        envelope.GetProperty("aggregateId").GetString().ShouldBe(aggregateId);
        envelope.GetProperty("causationId").GetString().ShouldBe(commandId);
        using JsonDocument payload = JsonDocument.Parse(Convert.FromBase64String(envelope.GetProperty("payload").GetString()!));
        payload.RootElement.ValueKind.ShouldBe(JsonValueKind.Object);
        payload.RootElement.EnumerateObject().ShouldBeEmpty();
    }

    private static HttpRequestMessage CreateCommandRequest(
        string token,
        string tenant = "tenant-a",
        string domain = "counter",
        string? aggregateId = null,
        string? messageId = null) {
        string aggId = aggregateId ?? $"kc-{Guid.NewGuid():N}";
        var body = new {
            MessageId = messageId ?? Guid.NewGuid().ToString(),
            Tenant = tenant,
            Domain = domain,
            AggregateId = aggId,
            CommandType = "IncrementCounter",
            Payload = new { counterId = aggId },
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
