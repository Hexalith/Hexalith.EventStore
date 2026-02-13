extern alias commandapi;

namespace Hexalith.EventStore.IntegrationTests.CommandApi;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using commandapi::Hexalith.EventStore.CommandApi.Models;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.IntegrationTests.Helpers;
using Hexalith.EventStore.Server.Commands;

using Shouldly;

public class CommandStatusIntegrationTests(JwtAuthenticatedWebApplicationFactory factory)
    : IClassFixture<JwtAuthenticatedWebApplicationFactory>
{
    [Fact]
    public async Task PostCommands_ThenGetStatus_Returns200WithReceivedStatus()
    {
        // Arrange
        HttpClient client = CreateAuthenticatedClient("test-tenant");

        var request = new
        {
            tenant = "test-tenant",
            domain = "test-domain",
            aggregateId = "agg-status-1",
            commandType = "CreateOrder",
            payload = new { amount = 100 },
        };

        // Act - submit command
        HttpResponseMessage submitResponse = await client.PostAsJsonAsync("/api/v1/commands", request);
        submitResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        SubmitCommandResponse? submitResult = await submitResponse.Content.ReadFromJsonAsync<SubmitCommandResponse>();
        submitResult.ShouldNotBeNull();

        // Act - query status
        HttpResponseMessage statusResponse = await client.GetAsync(
            $"/api/v1/commands/status/{submitResult.CorrelationId}");

        // Assert
        statusResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        CommandStatusResponse? status = await statusResponse.Content.ReadFromJsonAsync<CommandStatusResponse>();
        status.ShouldNotBeNull();
        status.Status.ShouldBe("Received");
        status.StatusCode.ShouldBe(0);
        status.AggregateId.ShouldBe("agg-status-1");
    }

    [Fact]
    public async Task GetStatus_NonExistentCorrelationId_Returns404ProblemDetails()
    {
        // Arrange
        HttpClient client = CreateAuthenticatedClient("test-tenant");
        string nonExistentId = Guid.NewGuid().ToString();

        // Act
        HttpResponseMessage response = await client.GetAsync(
            $"/api/v1/commands/status/{nonExistentId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType!.ShouldContain("problem+json");

        JsonElement problemDetails = await response.Content.ReadFromJsonAsync<JsonElement>();
        problemDetails.GetProperty("status").GetInt32().ShouldBe(404);
        problemDetails.GetProperty("detail").GetString()!.ShouldContain(nonExistentId);
    }

    [Fact]
    public async Task GetStatus_WrongTenant_Returns404ProblemDetails()
    {
        // Arrange - submit command as tenant-a
        HttpClient tenantAClient = CreateAuthenticatedClient("tenant-a");
        var request = new
        {
            tenant = "tenant-a",
            domain = "test-domain",
            aggregateId = "agg-sec3",
            commandType = "CreateOrder",
            payload = new { amount = 100 },
        };

        HttpResponseMessage submitResponse = await tenantAClient.PostAsJsonAsync("/api/v1/commands", request);
        submitResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        SubmitCommandResponse? submitResult = await submitResponse.Content.ReadFromJsonAsync<SubmitCommandResponse>();
        submitResult.ShouldNotBeNull();

        // Act - query status as tenant-b (SEC-3: should return 404, not 403)
        HttpClient tenantBClient = CreateAuthenticatedClient("tenant-b");
        HttpResponseMessage statusResponse = await tenantBClient.GetAsync(
            $"/api/v1/commands/status/{submitResult.CorrelationId}");

        // Assert
        statusResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetStatus_NoAuthentication_Returns401()
    {
        // Arrange - no auth header
        HttpClient client = factory.CreateClient();
        string correlationId = Guid.NewGuid().ToString();

        // Act
        HttpResponseMessage response = await client.GetAsync(
            $"/api/v1/commands/status/{correlationId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetStatus_ValidTenant_Returns200()
    {
        // Arrange
        string tenantId = "valid-tenant-200";
        HttpClient client = CreateAuthenticatedClient(tenantId);

        // Pre-seed status via the store
        string correlationId = Guid.NewGuid().ToString();
        await factory.StatusStore.WriteStatusAsync(
            tenantId,
            correlationId,
            new CommandStatusRecord(CommandStatus.Completed, DateTimeOffset.UtcNow, "agg-ok", 3, null, null, null),
            CancellationToken.None);

        // Act
        HttpResponseMessage response = await client.GetAsync(
            $"/api/v1/commands/status/{correlationId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        CommandStatusResponse? status = await response.Content.ReadFromJsonAsync<CommandStatusResponse>();
        status.ShouldNotBeNull();
        status.Status.ShouldBe("Completed");
        status.EventCount.ShouldBe(3);
    }

    [Fact]
    public async Task GetStatus_NoTenantClaims_Returns403()
    {
        // Arrange - JWT with NO tenant claims
        string token = TestJwtTokenGenerator.GenerateToken(subject: "no-tenant-user");
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        string correlationId = Guid.NewGuid().ToString();

        // Act
        HttpResponseMessage response = await client.GetAsync(
            $"/api/v1/commands/status/{correlationId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType!.MediaType!.ShouldContain("problem+json");

        JsonElement problemDetails = await response.Content.ReadFromJsonAsync<JsonElement>();
        problemDetails.GetProperty("detail").GetString()!.ShouldContain("No tenant authorization claims found");
    }

    [Fact]
    public async Task PostCommands_StatusWriteIncludesAggregateId()
    {
        // Arrange
        HttpClient client = CreateAuthenticatedClient("test-tenant");
        var request = new
        {
            tenant = "test-tenant",
            domain = "test-domain",
            aggregateId = "specific-agg-id",
            commandType = "CreateOrder",
            payload = new { amount = 100 },
        };

        // Act
        HttpResponseMessage submitResponse = await client.PostAsJsonAsync("/api/v1/commands", request);
        submitResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        SubmitCommandResponse? submitResult = await submitResponse.Content.ReadFromJsonAsync<SubmitCommandResponse>();

        HttpResponseMessage statusResponse = await client.GetAsync(
            $"/api/v1/commands/status/{submitResult!.CorrelationId}");

        // Assert
        statusResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        CommandStatusResponse? status = await statusResponse.Content.ReadFromJsonAsync<CommandStatusResponse>();
        status!.AggregateId.ShouldBe("specific-agg-id");
    }

    [Fact]
    public async Task GetStatus_ResponseIncludesCorrelationIdInProblemDetails()
    {
        // Arrange
        HttpClient client = CreateAuthenticatedClient("test-tenant");
        string correlationId = Guid.NewGuid().ToString();

        // Act
        HttpResponseMessage response = await client.GetAsync(
            $"/api/v1/commands/status/{correlationId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        JsonElement problemDetails = await response.Content.ReadFromJsonAsync<JsonElement>();
        problemDetails.TryGetProperty("correlationId", out JsonElement corrIdProp).ShouldBeTrue();
        corrIdProp.GetString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetStatus_ExpiredCorrelationId_Returns404()
    {
        // Arrange - write status with very short TTL
        string tenantId = "ttl-tenant";
        HttpClient client = CreateAuthenticatedClient(tenantId);
        string correlationId = Guid.NewGuid().ToString();

        // Set TTL to -1 so the entry is immediately expired (expiry in the past)
        factory.StatusStore.TtlSeconds = -1;
        var record = new CommandStatusRecord(CommandStatus.Received, DateTimeOffset.UtcNow.AddHours(-1), "agg-expired", null, null, null, null);
        await factory.StatusStore.WriteStatusAsync(tenantId, correlationId, record, CancellationToken.None);
        factory.StatusStore.TtlSeconds = CommandStatusConstants.DefaultTtlSeconds; // Reset

        // Act
        HttpResponseMessage response = await client.GetAsync(
            $"/api/v1/commands/status/{correlationId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostCommands_StatusLocationHeader_MatchesGetEndpoint()
    {
        // Arrange
        HttpClient client = CreateAuthenticatedClient("test-tenant");
        var request = new
        {
            tenant = "test-tenant",
            domain = "test-domain",
            aggregateId = "agg-location",
            commandType = "CreateOrder",
            payload = new { amount = 100 },
        };

        // Act
        HttpResponseMessage submitResponse = await client.PostAsJsonAsync("/api/v1/commands", request);
        submitResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        SubmitCommandResponse? submitResult = await submitResponse.Content.ReadFromJsonAsync<SubmitCommandResponse>();
        submitResult.ShouldNotBeNull();

        // Get the Location header
        string? locationHeader = submitResponse.Headers.Location?.ToString();
        locationHeader.ShouldNotBeNull();
        locationHeader.ShouldContain($"/api/v1/commands/status/{submitResult.CorrelationId}");

        // Act - verify the Location URL is accessible
        HttpResponseMessage statusResponse = await client.GetAsync(
            $"/api/v1/commands/status/{submitResult.CorrelationId}");

        // Assert - the endpoint works
        statusResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private HttpClient CreateAuthenticatedClient(string tenantId)
    {
        HttpClient client = factory.CreateClient();
        string token = TestJwtTokenGenerator.GenerateToken(tenants: [tenantId], domains: ["test-domain"]);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
