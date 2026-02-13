extern alias commandapi;

namespace Hexalith.EventStore.IntegrationTests.CommandApi;

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.Mvc.Testing;

using Shouldly;

using CommandApiProgram = commandapi::Program;

public class ValidationTests(WebApplicationFactory<CommandApiProgram> factory)
    : IClassFixture<WebApplicationFactory<CommandApiProgram>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task PostCommands_MissingFields_Returns400WithValidationErrors()
    {
        // Arrange - provide tenant but omit other required fields
        var request = new { tenant = "test-tenant" };

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType!.ShouldContain("problem+json");

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().ShouldBe(400);
        body.GetProperty("title").GetString().ShouldNotBeNullOrEmpty();
        body.TryGetProperty("type", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task PostCommands_InjectionInExtensions_Returns400WithProblemDetails()
    {
        // Arrange - extension value contains dangerous characters
        var request = new
        {
            tenant = "test-tenant",
            domain = "test-domain",
            aggregateId = "agg-001",
            commandType = "CreateOrder",
            payload = new { amount = 100 },
            extensions = new Dictionary<string, string>
            {
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
    }

    [Fact]
    public async Task PostCommands_OversizedExtensions_Returns400WithProblemDetails()
    {
        // Arrange - create extensions that exceed 64KB total size
        var extensions = new Dictionary<string, string>();
        string largeValue = new('x', 1000);
        for (int i = 0; i < 50; i++)
        {
            extensions[$"key{i:D3}"] = largeValue;
        }

        // 50 entries * (6 + 1000) chars * 2 bytes = ~100KB > 64KB limit
        var request = new
        {
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
    }

    [Fact]
    public async Task PostCommands_EmptyTenant_Returns400WithInstanceAndCorrelationId()
    {
        // Arrange
        var request = new
        {
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
        body.GetProperty("title").GetString().ShouldBe("Validation Failed");
        body.GetProperty("correlationId").GetString().ShouldNotBeNullOrEmpty();
        body.GetProperty("validationErrors").GetArrayLength().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task PostCommands_FieldLengthExceeded_Returns400()
    {
        // Arrange - tenant exceeds 128 char limit
        var request = new
        {
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
    public async Task PostCommands_InvalidTenantChars_Returns400()
    {
        // Arrange - tenant with uppercase (violates AggregateIdentity pattern)
        var request = new
        {
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
    public async Task PostCommands_JavascriptInjectionInExtensions_Returns400()
    {
        // Arrange
        var request = new
        {
            tenant = "test-tenant",
            domain = "test-domain",
            aggregateId = "agg-001",
            commandType = "CreateOrder",
            payload = new { amount = 100 },
            extensions = new Dictionary<string, string>
            {
                ["redirect"] = "javascript:alert(1)",
            },
        };

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
