extern alias eventstore;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.EventStore.IntegrationTests.Helpers;
using Hexalith.EventStore.Server.Pipeline.Commands;

using MediatR;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using EventStoreProgram = eventstore::Program;

namespace Hexalith.EventStore.IntegrationTests.EventStore;

public class ValidationTests(JwtAuthenticatedWebApplicationFactory factory)
    : IClassFixture<JwtAuthenticatedWebApplicationFactory> {
    private readonly HttpClient _client = CreateAuthenticatedClient(factory);

    [Fact]
    public async Task PostCommands_MissingFields_Returns400WithValidationErrors() {
        // Arrange - provide tenant but omit other required fields
        var request = new { tenant = "test-tenant" };

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType!.ShouldContain("problem+json");

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().ShouldBe(400);
        body.GetProperty("title").GetString().ShouldBe("Command Validation Failed");
        body.GetProperty("type").GetString().ShouldBe("https://hexalith.io/problems/validation-error");
        body.GetProperty("correlationId").GetString().ShouldNotBeNullOrEmpty();
        body.TryGetProperty("errors", out JsonElement errors).ShouldBeTrue();
        errors.EnumerateObject().Count().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task PostCommands_InjectionInExtensions_Returns400WithProblemDetails() {
        // Arrange - extension value contains dangerous characters
        var request = new {
            messageId = Guid.NewGuid().ToString(),
            tenant = "test-tenant",
            domain = "test-domain",
            aggregateId = "agg-001",
            commandType = "CreateOrder",
            payload = new { amount = 100 },
            extensions = new Dictionary<string, string> {
                ["evil"] = "<script>alert('xss')</script>",
            },
        };

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType!.ShouldContain("problem+json");

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().ShouldBe(400);
        body.GetProperty("type").GetString().ShouldBe("https://hexalith.io/problems/validation-error");
        body.GetProperty("title").GetString().ShouldBe("Command Validation Failed");
        body.GetProperty("correlationId").GetString().ShouldNotBeNullOrEmpty();
        body.GetProperty("tenantId").GetString().ShouldBe("test-tenant");
    }

    [Fact]
    public async Task PostCommands_OversizedExtensions_Returns400WithProblemDetails() {
        // Arrange - create extensions that exceed 64KB total size
        var extensions = new Dictionary<string, string>();
        string largeValue = new('x', 1000);
        for (int i = 0; i < 50; i++) {
            extensions[$"key{i:D3}"] = largeValue;
        }

        // 50 entries * (6 + 1000) chars * 2 bytes = ~100KB > 64KB limit
        var request = new {
            messageId = Guid.NewGuid().ToString(),
            tenant = "test-tenant",
            domain = "test-domain",
            aggregateId = "agg-001",
            commandType = "CreateOrder",
            payload = new { amount = 100 },
            extensions,
        };

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType!.ShouldContain("problem+json");

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().ShouldBe(400);
        body.GetProperty("type").GetString().ShouldBe("https://hexalith.io/problems/validation-error");
        body.GetProperty("title").GetString().ShouldBe("Command Validation Failed");
    }

    [Fact]
    public async Task PostCommands_EmptyTenant_Returns400WithInstanceAndCorrelationId() {
        // Arrange
        var request = new {
            messageId = Guid.NewGuid().ToString(),
            tenant = "",
            domain = "test-domain",
            aggregateId = "agg-001",
            commandType = "CreateOrder",
            payload = new { amount = 100 },
        };

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType!.ShouldContain("problem+json");

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("title").GetString().ShouldBe("Command Validation Failed");
        body.GetProperty("type").GetString().ShouldBe("https://hexalith.io/problems/validation-error");
        body.GetProperty("correlationId").GetString().ShouldNotBeNullOrEmpty();
        body.TryGetProperty("errors", out JsonElement errors).ShouldBeTrue();
        errors.EnumerateObject().Count().ShouldBeGreaterThan(0);
        body.GetProperty("instance").GetString().ShouldBe("/api/v1/commands");
        // Verify old format is gone
        body.TryGetProperty("validationErrors", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task PostCommands_FieldLengthExceeded_Returns400() {
        // Arrange - tenant exceeds 128 char limit
        var request = new {
            messageId = Guid.NewGuid().ToString(),
            tenant = new string('a', 129),
            domain = "test-domain",
            aggregateId = "agg-001",
            commandType = "CreateOrder",
            payload = new { amount = 100 },
        };

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostCommands_InvalidTenantChars_Returns400() {
        // Arrange - tenant with uppercase (violates AggregateIdentity pattern)
        var request = new {
            messageId = Guid.NewGuid().ToString(),
            tenant = "INVALID_TENANT",
            domain = "test-domain",
            aggregateId = "agg-001",
            commandType = "CreateOrder",
            payload = new { amount = 100 },
        };

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostCommands_JavascriptInjectionInExtensions_Returns400() {
        // Arrange
        var request = new {
            messageId = Guid.NewGuid().ToString(),
            tenant = "test-tenant",
            domain = "test-domain",
            aggregateId = "agg-001",
            commandType = "CreateOrder",
            payload = new { amount = 100 },
            extensions = new Dictionary<string, string> {
                ["redirect"] = "javascript:alert(1)",
            },
        };

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostCommands_ValidTenantInvalidDomain_Returns400WithTenantIdInExtensions() {
        // Arrange - valid tenant but empty domain triggers validation error
        // tenantId should appear in ProblemDetails extensions (AC3)
        var request = new {
            messageId = Guid.NewGuid().ToString(),
            tenant = "test-tenant",
            domain = "",
            aggregateId = "agg-001",
            commandType = "CreateOrder",
            payload = new { amount = 100 },
        };

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType!.ShouldContain("problem+json");

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().ShouldBe(400);
        body.GetProperty("title").GetString().ShouldBe("Command Validation Failed");
        body.GetProperty("type").GetString().ShouldBe("https://hexalith.io/problems/validation-error");
        body.GetProperty("correlationId").GetString().ShouldNotBeNullOrEmpty();
        body.GetProperty("instance").GetString().ShouldBe("/api/v1/commands");
        body.GetProperty("tenantId").GetString().ShouldBe("test-tenant");
        body.TryGetProperty("errors", out JsonElement errors).ShouldBeTrue();
        errors.EnumerateObject().Count().ShouldBeGreaterThan(0);
        // Verify old format is gone
        body.TryGetProperty("validationErrors", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task PostCommands_UnhandledException_Returns500ProblemDetailsWithoutStackTrace() {
        // Arrange - Override handler to simulate an unhandled exception
        using WebApplicationFactory<EventStoreProgram> customFactory = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services => {
            ServiceDescriptor? handlerDescriptor = services.FirstOrDefault(
                d => d.ServiceType == typeof(IRequestHandler<SubmitCommand, SubmitCommandResult>));
            if (handlerDescriptor is not null) {
                _ = services.Remove(handlerDescriptor);
            }

            _ = services.AddTransient<IRequestHandler<SubmitCommand, SubmitCommandResult>>(
                _ => new ThrowingSubmitCommandHandler());
        }));

        HttpClient client = customFactory.CreateClient();
        string token = TestJwtTokenGenerator.GenerateToken(tenants: ["test-tenant"], domains: ["test-domain"]);
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

        // Assert - RFC 7807 ProblemDetails with no stack traces (enforcement rule #13)
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType!.MediaType!.ShouldContain("problem+json");

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().ShouldBe(500);
        body.GetProperty("title").GetString().ShouldBe("Internal Server Error");
        body.GetProperty("type").GetString()!.ShouldContain("rfc9457");
        body.GetProperty("instance").GetString().ShouldBe("/api/v1/commands");
        body.GetProperty("correlationId").GetString().ShouldNotBeNullOrEmpty();

        // Critical: no stack traces or exception details in response
        string detail = body.GetProperty("detail").GetString()!;
        detail.ShouldNotContain("at ");
        detail.ShouldNotContain("ThrowingSubmitCommandHandler");
        detail.ShouldNotContain("Simulated failure");
    }

    private static HttpClient CreateAuthenticatedClient(JwtAuthenticatedWebApplicationFactory factory) {
        HttpClient client = factory.CreateClient();
        string token = TestJwtTokenGenerator.GenerateToken(tenants: ["test-tenant"], domains: ["test-domain"]);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private sealed class ThrowingSubmitCommandHandler : IRequestHandler<SubmitCommand, SubmitCommandResult> {
        public Task<SubmitCommandResult> Handle(SubmitCommand request, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Simulated failure for testing GlobalExceptionHandler.");
    }
}
