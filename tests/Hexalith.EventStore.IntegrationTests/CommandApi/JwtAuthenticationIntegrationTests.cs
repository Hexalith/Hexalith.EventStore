extern alias commandapi;

namespace Hexalith.EventStore.IntegrationTests.CommandApi;

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

using CommandApiProgram = commandapi::Program;

public class JwtAuthenticationIntegrationTests
    : IClassFixture<JwtAuthenticationIntegrationTests.JwtLogCapturingFactory>
{
    private readonly JwtLogCapturingFactory _factory;

    public JwtAuthenticationIntegrationTests(JwtLogCapturingFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
    }

    [Fact]
    public async Task PostCommands_NoAuthToken_Returns401ProblemDetails()
    {
        // Arrange - no Authorization header
        HttpClient client = _factory.CreateClient();
        var request = new
        {
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
        body.GetProperty("type").GetString().ShouldBe("https://tools.ietf.org/html/rfc9457#section-3");
        body.GetProperty("correlationId").GetString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task PostCommands_InvalidToken_Returns401ProblemDetails()
    {
        // Arrange - garbage token
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "this.is.not.a.valid.jwt");
        var request = new
        {
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
    }

    [Fact]
    public async Task PostCommands_ExpiredToken_Returns401ProblemDetails()
    {
        // Arrange - token expired in the past
        string expiredToken = TestJwtTokenGenerator.GenerateToken(
            expires: DateTime.UtcNow.AddHours(-1));
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", expiredToken);
        var request = new
        {
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
    }

    [Fact]
    public async Task PostCommands_WrongIssuer_Returns401ProblemDetails()
    {
        // Arrange - token with wrong issuer
        string wrongIssuerToken = TestJwtTokenGenerator.GenerateToken(
            issuer: "wrong-issuer");
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", wrongIssuerToken);
        var request = new
        {
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
    }

    [Fact]
    public async Task PostCommands_ValidToken_Returns202Accepted()
    {
        // Arrange - valid JWT token
        string token = TestJwtTokenGenerator.GenerateToken(
            tenants: ["test-tenant"],
            domains: ["test-domain"]);
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var request = new
        {
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
    public async Task PostCommands_ValidTokenWithTenantClaims_ClaimsTransformedCorrectly()
    {
        // Arrange - JWT with tenant claims; request must use a matching tenant/domain
        string token = TestJwtTokenGenerator.GenerateToken(
            tenants: ["tenant-a", "tenant-b"],
            domains: ["orders", "inventory"],
            permissions: ["commands:*"]);
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var request = new
        {
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
    public async Task HealthEndpoint_NoAuth_Returns200()
    {
        // Arrange - no auth token, hitting health endpoint
        HttpClient client = _factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/health");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AliveEndpoint_NoAuth_Returns200()
    {
        // Arrange - no auth token, hitting alive endpoint
        HttpClient client = _factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/alive");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PostCommands_AuthFailure_LogsWithoutJwtToken()
    {
        // Arrange
        _factory.LogProvider.Clear();
        string invalidToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.invalid.signature";
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", invalidToken);
        var request = new
        {
            tenant = "test-tenant",
            domain = "test-domain",
            aggregateId = "agg-001",
            commandType = "CreateOrder",
            payload = new { amount = 100 },
        };

        // Act
        await client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert - verify log entries exist for auth failure
        List<TestLogEntry> authLogs = _factory.LogProvider.GetEntries()
            .Where(e => e.Level == LogLevel.Warning &&
                       (e.Message.Contains("Authentication failed") || e.Message.Contains("Authentication challenge")))
            .ToList();

        authLogs.ShouldNotBeEmpty("Auth failure should produce warning logs");

        // Verify correlationId is present in log messages
        authLogs.ShouldContain(e => e.Message.Contains("CorrelationId="));

        // CRITICAL: Verify JWT token is NOT present in any log
        List<TestLogEntry> allLogs = _factory.LogProvider.GetEntries().ToList();
        allLogs.ShouldNotContain(
            e => e.Message.Contains(invalidToken),
            "JWT token MUST NOT appear in log output (NFR11)");
    }

    /// <summary>
    /// WebApplicationFactory with JWT auth config and log capturing for auth failure tests.
    /// </summary>
    public class JwtLogCapturingFactory : WebApplicationFactory<CommandApiProgram>
    {
        public TestLogProvider LogProvider { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);
            builder.ConfigureAppConfiguration(config =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Authentication:JwtBearer:Issuer"] = TestJwtTokenGenerator.Issuer,
                    ["Authentication:JwtBearer:Audience"] = TestJwtTokenGenerator.Audience,
                    ["Authentication:JwtBearer:SigningKey"] = TestJwtTokenGenerator.SigningKey,
                    ["Authentication:JwtBearer:RequireHttpsMetadata"] = "false",
                });
            });
            builder.ConfigureServices(services =>
            {
                // Replace Dapr stores with InMemory for tests
                ServiceDescriptor? statusDescriptor = services.FirstOrDefault(
                    d => d.ServiceType == typeof(ICommandStatusStore));
                if (statusDescriptor is not null)
                {
                    services.Remove(statusDescriptor);
                }

                services.AddSingleton<ICommandStatusStore>(new InMemoryCommandStatusStore());

                ServiceDescriptor? archiveDescriptor = services.FirstOrDefault(
                    d => d.ServiceType == typeof(ICommandArchiveStore));
                if (archiveDescriptor is not null)
                {
                    services.Remove(archiveDescriptor);
                }

                services.AddSingleton<ICommandArchiveStore>(new InMemoryCommandArchiveStore());

                TestServiceOverrides.ReplaceCommandRouter(services);

                services.AddLogging(logging => logging.AddProvider(LogProvider));
            });
        }
    }
}
