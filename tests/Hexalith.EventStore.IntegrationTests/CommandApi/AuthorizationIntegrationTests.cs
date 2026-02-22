extern alias commandapi;

using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.EventStore.IntegrationTests.Helpers;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Shouldly;

using CommandApiProgram = commandapi::Program;

namespace Hexalith.EventStore.IntegrationTests.CommandApi;

public class AuthorizationIntegrationTests
    : IClassFixture<AuthorizationIntegrationTests.AuthorizationLogCapturingFactory> {
    private readonly AuthorizationLogCapturingFactory _factory;

    public AuthorizationIntegrationTests(AuthorizationLogCapturingFactory factory) {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
    }

    [Fact]
    public async Task PostCommands_TenantNotInClaims_Returns403ProblemDetails() {
        // Arrange - token has tenant-a, but request uses tenant-b
        string token = TestJwtTokenGenerator.GenerateToken(tenants: ["tenant-a"]);
        HttpClient client = CreateClientWithToken(token);
        var request = new {
            tenant = "tenant-b",
            domain = "test-domain",
            aggregateId = "agg-001",
            commandType = "CreateOrder",
            payload = new { amount = 100 },
        };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType!.MediaType!.ShouldContain("problem+json");

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().ShouldBe(403);
        body.GetProperty("title").GetString().ShouldBe("Forbidden");
        body.GetProperty("type").GetString().ShouldBe("https://tools.ietf.org/html/rfc9457#section-3");
        body.GetProperty("detail").GetString()!.ShouldContain("tenant");
        body.GetProperty("tenantId").GetString().ShouldBe("tenant-b");
        body.GetProperty("correlationId").GetString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task PostCommands_NoTenantClaims_Returns403ProblemDetails() {
        // Arrange - token with no tenant claims at all
        string token = TestJwtTokenGenerator.GenerateToken();
        HttpClient client = CreateClientWithToken(token);
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
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType!.MediaType!.ShouldContain("problem+json");

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().ShouldBe(403);
        body.GetProperty("detail").GetString()!.ShouldContain("No tenant authorization claims found");
    }

    [Fact]
    public async Task PostCommands_DomainNotInClaims_Returns403ProblemDetails() {
        // Arrange - token has domain "orders" but request uses "inventory"
        string token = TestJwtTokenGenerator.GenerateToken(
            tenants: ["test-tenant"],
            domains: ["orders"]);
        HttpClient client = CreateClientWithToken(token);
        var request = new {
            tenant = "test-tenant",
            domain = "inventory",
            aggregateId = "agg-001",
            commandType = "CreateOrder",
            payload = new { amount = 100 },
        };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType!.MediaType!.ShouldContain("problem+json");

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().ShouldBe(403);
        body.GetProperty("detail").GetString()!.ShouldContain("domain");
    }

    [Fact]
    public async Task PostCommands_CommandTypeNotInClaims_Returns403ProblemDetails() {
        // Arrange - token has permission for "PlaceOrder" only
        string token = TestJwtTokenGenerator.GenerateToken(
            tenants: ["test-tenant"],
            permissions: ["PlaceOrder"]);
        HttpClient client = CreateClientWithToken(token);
        var request = new {
            tenant = "test-tenant",
            domain = "test-domain",
            aggregateId = "agg-001",
            commandType = "CancelOrder",
            payload = new { amount = 100 },
        };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType!.MediaType!.ShouldContain("problem+json");

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().ShouldBe(403);
        body.GetProperty("detail").GetString()!.ShouldContain("command type");
    }

    [Fact]
    public async Task PostCommands_NoDomainClaims_Returns202Accepted() {
        // Arrange - no domain claims means all domains allowed (AC #5)
        string token = TestJwtTokenGenerator.GenerateToken(tenants: ["test-tenant"]);
        HttpClient client = CreateClientWithToken(token);
        var request = new {
            tenant = "test-tenant",
            domain = "any-domain",
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
    public async Task PostCommands_NoPermissionClaims_Returns202Accepted() {
        // Arrange - no permission claims means all command types allowed (AC #5)
        string token = TestJwtTokenGenerator.GenerateToken(tenants: ["test-tenant"]);
        HttpClient client = CreateClientWithToken(token);
        var request = new {
            tenant = "test-tenant",
            domain = "test-domain",
            aggregateId = "agg-001",
            commandType = "AnyCommandType",
            payload = new { amount = 100 },
        };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task PostCommands_WildcardPermission_Returns202Accepted() {
        // Arrange - commands:* grants access to any command type
        string token = TestJwtTokenGenerator.GenerateToken(
            tenants: ["test-tenant"],
            permissions: ["commands:*"]);
        HttpClient client = CreateClientWithToken(token);
        var request = new {
            tenant = "test-tenant",
            domain = "test-domain",
            aggregateId = "agg-001",
            commandType = "AnyCommandAtAll",
            payload = new { amount = 100 },
        };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task PostCommands_MatchingTenantDomainPermission_Returns202Accepted() {
        // Arrange - fully authorized request
        string token = TestJwtTokenGenerator.GenerateToken(
            tenants: ["test-tenant"],
            domains: ["test-domain"],
            permissions: ["CreateOrder"]);
        HttpClient client = CreateClientWithToken(token);
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
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task PostCommands_TenantAuthFailure_RejectedBeforeMediatRPipeline() {
        // Arrange - tenant rejection should happen before MediatR pipeline
        _factory.LogProvider.Clear();
        string token = TestJwtTokenGenerator.GenerateToken(tenants: ["other-tenant"]);
        HttpClient client = CreateClientWithToken(token);
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
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // LoggingBehavior entry should NOT have fired (proving pre-pipeline rejection)
        var pipelineLogs = _factory.LogProvider.GetEntries()
            .Where(e => e.Message.Contains("MediatR pipeline entry"))
            .ToList();

        pipelineLogs.ShouldBeEmpty("Tenant rejection should happen before MediatR pipeline entry");
    }

    [Fact]
    public async Task PostCommands_AuthFailure_LogsWarningWithCorrelationId() {
        // Arrange
        _factory.LogProvider.Clear();
        string token = TestJwtTokenGenerator.GenerateToken(tenants: ["other-tenant"]);
        HttpClient client = CreateClientWithToken(token);
        var request = new {
            tenant = "test-tenant",
            domain = "test-domain",
            aggregateId = "agg-001",
            commandType = "CreateOrder",
            payload = new { amount = 100 },
        };

        // Act
        _ = await client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert - warning log should contain correlationId
        var warningLogs = _factory.LogProvider.GetEntries()
            .Where(e => e.Level == LogLevel.Warning && e.Message.Contains("authorization failed", StringComparison.OrdinalIgnoreCase))
            .ToList();

        warningLogs.ShouldNotBeEmpty("Authorization failure should produce warning log");
        warningLogs.ShouldContain(e => e.Message.Contains("CorrelationId="));

        // CRITICAL: JWT token must NOT appear in any log
        var allLogs = _factory.LogProvider.GetEntries().ToList();
        allLogs.ShouldNotContain(
            e => e.Message.Contains(token),
            "JWT token MUST NOT appear in log output (NFR11)");
    }

    [Fact]
    public async Task PostCommands_InvalidAndUnauthorized_Returns400NotForbidden() {
        // Arrange - request that fails BOTH validation (empty fields) and authorization (wrong tenant)
        // ValidateModelFilter runs before controller action, so validation rejects before tenant pre-check.
        // This verifies correct security behavior: don't leak authorization state for malformed requests.
        string token = TestJwtTokenGenerator.GenerateToken(tenants: ["other-tenant"]);
        HttpClient client = CreateClientWithToken(token);
        var request = new {
            tenant = "",
            domain = "",
            aggregateId = "",
            commandType = "",
            payload = new { amount = 100 },
        };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert - validation error (400) takes priority over authorization (403)
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostCommands_AuthorizationException_Returns403Not500() {
        // Arrange - token with matching tenant but wrong domain (triggers AuthorizationBehavior)
        string token = TestJwtTokenGenerator.GenerateToken(
            tenants: ["test-tenant"],
            domains: ["orders"]);
        HttpClient client = CreateClientWithToken(token);
        var request = new {
            tenant = "test-tenant",
            domain = "inventory",
            aggregateId = "agg-001",
            commandType = "CreateOrder",
            payload = new { amount = 100 },
        };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert - should be 403 (AuthorizationExceptionHandler), NOT 500 (GlobalExceptionHandler)
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType!.MediaType!.ShouldContain("problem+json");

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().ShouldBe(403);
        body.GetProperty("title").GetString().ShouldBe("Forbidden");
    }

    private HttpClient CreateClientWithToken(string token) {
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public class AuthorizationLogCapturingFactory : WebApplicationFactory<CommandApiProgram> {
        public AuthzLogProvider LogProvider { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder) {
            ArgumentNullException.ThrowIfNull(builder);
            _ = builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?> {
                ["Authentication:JwtBearer:Issuer"] = TestJwtTokenGenerator.Issuer,
                ["Authentication:JwtBearer:Audience"] = TestJwtTokenGenerator.Audience,
                ["Authentication:JwtBearer:SigningKey"] = TestJwtTokenGenerator.SigningKey,
                ["Authentication:JwtBearer:RequireHttpsMetadata"] = "false",
            }));
            _ = builder.ConfigureServices(services => {
                // Replace DAPR-dependent services with test fakes
                ServiceDescriptor? statusDescriptor = services.FirstOrDefault(
                    d => d.ServiceType == typeof(Server.Commands.ICommandStatusStore));
                if (statusDescriptor is not null) {
                    _ = services.Remove(statusDescriptor);
                }

                _ = services.AddSingleton<Server.Commands.ICommandStatusStore>(new Testing.Fakes.InMemoryCommandStatusStore());

                ServiceDescriptor? archiveDescriptor = services.FirstOrDefault(
                    d => d.ServiceType == typeof(Server.Commands.ICommandArchiveStore));
                if (archiveDescriptor is not null) {
                    _ = services.Remove(archiveDescriptor);
                }

                _ = services.AddSingleton<Server.Commands.ICommandArchiveStore>(new Testing.Fakes.InMemoryCommandArchiveStore());

                Testing.Fakes.TestServiceOverrides.ReplaceCommandRouter(services);
                Testing.Fakes.TestServiceOverrides.RemoveDaprHealthChecks(services);

                _ = services.AddLogging(logging => logging.AddProvider(LogProvider));
            });
        }
    }
}

public sealed class AuthzLogProvider : ILoggerProvider {
    private readonly ConcurrentQueue<AuthzLogEntry> _entries = [];

    public ILogger CreateLogger(string categoryName) => new AuthzLogger(_entries);

    public void Dispose() {
        // Nothing to dispose
    }

    public List<AuthzLogEntry> GetEntries() => [.. _entries];

    public void Clear() {
        while (_entries.TryDequeue(out _)) {
            // Drain
        }
    }

    private sealed class AuthzLogger(ConcurrentQueue<AuthzLogEntry> entries) : ILogger {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => entries.Enqueue(new AuthzLogEntry(logLevel, formatter(state, exception)));
    }
}

public record AuthzLogEntry(LogLevel Level, string Message);
