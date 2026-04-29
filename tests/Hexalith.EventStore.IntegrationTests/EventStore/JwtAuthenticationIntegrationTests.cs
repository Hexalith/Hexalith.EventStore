extern alias eventstore;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.EventStore.IntegrationTests.Helpers;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Shouldly;

using EventStoreProgram = eventstore::Program;

namespace Hexalith.EventStore.IntegrationTests.EventStore;

public class JwtAuthenticationIntegrationTests
    : IClassFixture<JwtAuthenticationIntegrationTests.JwtLogCapturingFactory> {
    private readonly JwtLogCapturingFactory _factory;

    public JwtAuthenticationIntegrationTests(JwtLogCapturingFactory factory) {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
    }

    [Fact]
    public async Task PostCommands_NoAuthToken_Returns401ProblemDetails() {
        // Arrange - no Authorization header
        HttpClient client = _factory.CreateClient();
        var request = new {
            tenant = "test-tenant",
            domain = "test-domain",
            aggregateId = "agg-001",
            commandType = "CreateOrder",
            payload = new { amount = 100 },
        };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType!.ShouldContain("problem+json");

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().ShouldBe(401);
        body.GetProperty("title").GetString().ShouldBe("Unauthorized");
        body.GetProperty("type").GetString().ShouldBe("https://hexalith.io/problems/authentication-required");

        // UX-DR2: 401 responses from the auth challenge intentionally omit correlationId
        // and tenantId (pre-pipeline rejection — no authenticated context to attach to).
        body.TryGetProperty("correlationId", out _).ShouldBeFalse();
        body.TryGetProperty("tenantId", out _).ShouldBeFalse();

        // UX-DR4: WWW-Authenticate Bearer challenge per RFC 6750.
        // Realm is asserted by substring (`hexalith-eventstore`) so a future ops change to
        // namespace the realm (e.g., `hexalith-eventstore-prod`) does not break the test.
        string wwwAuth = response.Headers.WwwAuthenticate.ToString();
        wwwAuth.ShouldStartWith("Bearer realm=\"");
        wwwAuth.ShouldContain("hexalith-eventstore");
    }

    [Fact]
    public async Task PostCommands_InvalidToken_Returns401ProblemDetails() {
        // Arrange - garbage token
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "this.is.not.a.valid.jwt");
        var request = new {
            tenant = "test-tenant",
            domain = "test-domain",
            aggregateId = "agg-001",
            commandType = "CreateOrder",
            payload = new { amount = 100 },
        };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType!.ShouldContain("problem+json");

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().ShouldBe(401);
        body.GetProperty("title").GetString().ShouldBe("Unauthorized");

        // UX-DR8: invalid (non-expired) JWT maps to authentication-required URI.
        body.GetProperty("type").GetString().ShouldBe("https://hexalith.io/problems/authentication-required");

        // UX-DR4: invalid-token WWW-Authenticate carries error="invalid_token".
        string wwwAuth = response.Headers.WwwAuthenticate.ToString();
        wwwAuth.ShouldStartWith("Bearer realm=\"");
        wwwAuth.ShouldContain("hexalith-eventstore");
        wwwAuth.ShouldContain("error=\"invalid_token\"");
    }

    [Fact]
    public async Task PostCommands_ExpiredToken_Returns401ProblemDetails() {
        // Arrange - token expired in the past
        string expiredToken = TestJwtTokenGenerator.GenerateToken(
            expires: DateTime.UtcNow.AddHours(-1));
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", expiredToken);
        var request = new {
            tenant = "test-tenant",
            domain = "test-domain",
            aggregateId = "agg-001",
            commandType = "CreateOrder",
            payload = new { amount = 100 },
        };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType!.ShouldContain("problem+json");

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().ShouldBe(401);
        body.GetProperty("detail").GetString()!.ShouldContain("expired");

        // UX-DR8: distinct URI for expired vs missing/invalid JWT.
        body.GetProperty("type").GetString().ShouldBe("https://hexalith.io/problems/token-expired");

        // UX-DR4: expired-token WWW-Authenticate carries error="invalid_token" + error_description="The token has expired".
        string wwwAuth = response.Headers.WwwAuthenticate.ToString();
        wwwAuth.ShouldStartWith("Bearer realm=\"");
        wwwAuth.ShouldContain("hexalith-eventstore");
        wwwAuth.ShouldContain("error=\"invalid_token\"");
        wwwAuth.ShouldContain("error_description=\"The token has expired\"");
    }

    [Fact]
    public async Task PostCommands_WrongIssuer_Returns401ProblemDetails() {
        // Arrange - token with wrong issuer
        string wrongIssuerToken = TestJwtTokenGenerator.GenerateToken(
            issuer: "wrong-issuer");
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", wrongIssuerToken);
        var request = new {
            tenant = "test-tenant",
            domain = "test-domain",
            aggregateId = "agg-001",
            commandType = "CreateOrder",
            payload = new { amount = 100 },
        };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType!.ShouldContain("problem+json");

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().ShouldBe(401);

        // UX-DR8: wrong-issuer is an "invalid" JWT, not "expired" — maps to authentication-required.
        body.GetProperty("type").GetString().ShouldBe("https://hexalith.io/problems/authentication-required");

        // Story 3.5 task 2.5 consolidated the wrong-issuer detail to the canonical invalid-token text.
        // Asserting the exact string locks that consolidation against future regressions.
        body.GetProperty("detail").GetString().ShouldBe("The provided authentication token is invalid.");

        // UX-DR4: invalid-token WWW-Authenticate carries error="invalid_token".
        string wwwAuth = response.Headers.WwwAuthenticate.ToString();
        wwwAuth.ShouldStartWith("Bearer realm=\"");
        wwwAuth.ShouldContain("hexalith-eventstore");
        wwwAuth.ShouldContain("error=\"invalid_token\"");
    }

    [Fact]
    public async Task PostCommands_ValidToken_Returns202Accepted() {
        // Arrange - valid JWT token
        string token = TestJwtTokenGenerator.GenerateToken(
            tenants: ["test-tenant"],
            domains: ["test-domain"]);
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var request = new {
            messageId = Guid.NewGuid().ToString(),
            tenant = "test-tenant",
            domain = "test-domain",
            aggregateId = "agg-001",
            commandType = "CreateOrder",
            payload = new { amount = 100 },
        };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task PostCommands_ValidTokenWithTenantClaims_ClaimsTransformedCorrectly() {
        // Arrange - JWT with tenant claims; request must use a matching tenant/domain
        string token = TestJwtTokenGenerator.GenerateToken(
            tenants: ["tenant-a", "tenant-b"],
            domains: ["orders", "inventory"],
            permissions: ["commands:*"]);
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var request = new {
            messageId = Guid.NewGuid().ToString(),
            tenant = "tenant-a",
            domain = "orders",
            aggregateId = "agg-001",
            commandType = "CreateOrder",
            payload = new { amount = 100 },
        };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert - valid token with matching claims should reach the controller and return 202
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task HealthEndpoint_NoAuth_Returns200() {
        // Arrange - no auth token, hitting health endpoint
        HttpClient client = _factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/health");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AliveEndpoint_NoAuth_Returns200() {
        // Arrange - no auth token, hitting alive endpoint
        HttpClient client = _factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/alive");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PostCommands_AuthFailure_LogsWithoutJwtToken() {
        // Arrange
        _factory.LogProvider.Clear();
        string invalidToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.invalid.signature";
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", invalidToken);
        var request = new {
            tenant = "test-tenant",
            domain = "test-domain",
            aggregateId = "agg-001",
            commandType = "CreateOrder",
            payload = new { amount = 100 },
        };

        // Act
        _ = await client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert - verify log entries exist for auth failure
        var authLogs = _factory.LogProvider.GetEntries()
            .Where(e => e.Level == LogLevel.Warning &&
                       (e.Message.Contains("Authentication failed") || e.Message.Contains("Authentication challenge")))
            .ToList();

        authLogs.ShouldNotBeEmpty("Auth failure should produce warning logs");

        // Verify correlationId is present in log messages
        authLogs.ShouldContain(e => e.Message.Contains("CorrelationId="));

        // CRITICAL: Verify JWT token is NOT present in any log
        var allLogs = _factory.LogProvider.GetEntries().ToList();
        allLogs.ShouldNotContain(
            e => e.Message.Contains(invalidToken),
            "JWT token MUST NOT appear in log output (NFR11)");
    }

    /// <summary>
    /// WebApplicationFactory with JWT auth config and log capturing for auth failure tests.
    /// </summary>
    public class JwtLogCapturingFactory : WebApplicationFactory<EventStoreProgram> {
        public TestLogProvider LogProvider { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder) {
            ArgumentNullException.ThrowIfNull(builder);
            _ = builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?> {
                ["Authentication:JwtBearer:Issuer"] = TestJwtTokenGenerator.Issuer,
                ["Authentication:JwtBearer:Audience"] = TestJwtTokenGenerator.Audience,
                ["Authentication:JwtBearer:SigningKey"] = TestJwtTokenGenerator.SigningKey,
                ["Authentication:JwtBearer:RequireHttpsMetadata"] = "false",
            }));
            _ = builder.ConfigureServices(services => {
                // Replace Dapr stores with InMemory for tests
                ServiceDescriptor? statusDescriptor = services.FirstOrDefault(
                    d => d.ServiceType == typeof(ICommandStatusStore));
                if (statusDescriptor is not null) {
                    _ = services.Remove(statusDescriptor);
                }

                _ = services.AddSingleton<ICommandStatusStore>(new InMemoryCommandStatusStore());

                ServiceDescriptor? archiveDescriptor = services.FirstOrDefault(
                    d => d.ServiceType == typeof(ICommandArchiveStore));
                if (archiveDescriptor is not null) {
                    _ = services.Remove(archiveDescriptor);
                }

                _ = services.AddSingleton<ICommandArchiveStore>(new InMemoryCommandArchiveStore());

                TestServiceOverrides.ReplaceCommandRouter(services);
                TestServiceOverrides.RemoveDaprHealthChecks(services);

                _ = services.AddLogging(logging => logging.AddProvider(LogProvider));
            });
        }
    }
}
