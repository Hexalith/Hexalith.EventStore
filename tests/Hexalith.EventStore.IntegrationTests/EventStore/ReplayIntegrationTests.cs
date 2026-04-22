extern alias eventstore;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using eventstore::Hexalith.EventStore.Models;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.IntegrationTests.Helpers;

using Shouldly;

namespace Hexalith.EventStore.IntegrationTests.EventStore;

public class ReplayIntegrationTests(JwtAuthenticatedWebApplicationFactory factory)
    : IClassFixture<JwtAuthenticatedWebApplicationFactory> {
    private static async Task<string> SubmitCommandAndGetCorrelationId(HttpClient client, string tenant = "test-tenant") {
        var request = new {
            messageId = Guid.NewGuid().ToString(),
            tenant,
            domain = "test-domain",
            aggregateId = "agg-replay",
            commandType = "CreateOrder",
            payload = new { amount = 100 },
        };

        HttpResponseMessage submitResponse = await client.PostAsJsonAsync("/api/v1/commands", request).ConfigureAwait(false);
        submitResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        SubmitCommandResponse? submitResult = await submitResponse.Content.ReadFromJsonAsync<SubmitCommandResponse>().ConfigureAwait(false);
        _ = submitResult.ShouldNotBeNull();
        return submitResult.CorrelationId;
    }

    private async Task SetCommandStatus(string tenant, string correlationId, CommandStatus status) => await factory.StatusStore.WriteStatusAsync(
            tenant,
            correlationId,
            new CommandStatusRecord(status, DateTimeOffset.UtcNow, "agg-replay", null, null, null, null),
            CancellationToken.None).ConfigureAwait(false);

    [Fact]
    public async Task PostReplay_RejectedCommand_Returns202() {
        // Arrange
        HttpClient client = CreateAuthenticatedClient("test-tenant");
        string correlationId = await SubmitCommandAndGetCorrelationId(client);
        await SetCommandStatus("test-tenant", correlationId, CommandStatus.Rejected);

        // Act
        HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/commands/replay/{correlationId}", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        response.Headers.TryGetValues("Retry-After", out System.Collections.Generic.IEnumerable<string>? retryValues).ShouldBeTrue();
        retryValues!.First().ShouldBe("1");
        ReplayCommandResponse? replayResult = await response.Content.ReadFromJsonAsync<ReplayCommandResponse>();
        _ = replayResult.ShouldNotBeNull();
        replayResult.IsReplay.ShouldBeTrue();
        replayResult.PreviousStatus.ShouldBe("Rejected");
    }

    [Fact]
    public async Task PostReplay_PublishFailedCommand_Returns202() {
        // Arrange
        HttpClient client = CreateAuthenticatedClient("test-tenant");
        string correlationId = await SubmitCommandAndGetCorrelationId(client);
        await SetCommandStatus("test-tenant", correlationId, CommandStatus.PublishFailed);

        // Act
        HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/commands/replay/{correlationId}", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        ReplayCommandResponse? replayResult = await response.Content.ReadFromJsonAsync<ReplayCommandResponse>();
        _ = replayResult.ShouldNotBeNull();
        replayResult.IsReplay.ShouldBeTrue();
        replayResult.PreviousStatus.ShouldBe("PublishFailed");
    }

    [Fact]
    public async Task PostReplay_TimedOutCommand_Returns202() {
        // Arrange
        HttpClient client = CreateAuthenticatedClient("test-tenant");
        string correlationId = await SubmitCommandAndGetCorrelationId(client);
        await SetCommandStatus("test-tenant", correlationId, CommandStatus.TimedOut);

        // Act
        HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/commands/replay/{correlationId}", null);

        // Assert — replay generates a new correlation ID for tracking
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        _ = response.Headers.Location.ShouldNotBeNull();
        response.Headers.Location.ToString().ShouldContain("/api/v1/commands/status/");
        response.Headers.TryGetValues("Retry-After", out System.Collections.Generic.IEnumerable<string>? retryValues).ShouldBeTrue();
        retryValues!.First().ShouldBe("1");
        ReplayCommandResponse? replayResult = await response.Content.ReadFromJsonAsync<ReplayCommandResponse>();
        _ = replayResult.ShouldNotBeNull();
        replayResult.CorrelationId.ShouldNotBe(correlationId);
        Guid.TryParse(replayResult.CorrelationId, out _).ShouldBeTrue();
        replayResult.IsReplay.ShouldBeTrue();
        replayResult.PreviousStatus.ShouldBe("TimedOut");
    }

    [Fact]
    public async Task PostReplay_CompletedCommand_Returns409ProblemDetails() {
        // Arrange
        HttpClient client = CreateAuthenticatedClient("test-tenant");
        string correlationId = await SubmitCommandAndGetCorrelationId(client);
        await SetCommandStatus("test-tenant", correlationId, CommandStatus.Completed);

        // Act
        HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/commands/replay/{correlationId}", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType!.MediaType!.ShouldContain("problem+json");

        JsonElement problemDetails = await response.Content.ReadFromJsonAsync<JsonElement>();
        problemDetails.GetProperty("status").GetInt32().ShouldBe(409);
        problemDetails.GetProperty("detail").GetString()!.ShouldContain("completed successfully");
        problemDetails.TryGetProperty("correlationId", out _).ShouldBeTrue();
        problemDetails.TryGetProperty("currentStatus", out JsonElement currentStatus).ShouldBeTrue();
        currentStatus.GetString().ShouldBe("Completed");
    }

    [Fact]
    public async Task PostReplay_InFlightCommand_Returns409ProblemDetails() {
        // Arrange - submit command (status is Received = in-flight)
        HttpClient client = CreateAuthenticatedClient("test-tenant");
        string correlationId = await SubmitCommandAndGetCorrelationId(client);
        // Status is already Received from submission

        // Act
        HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/commands/replay/{correlationId}", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        JsonElement problemDetails = await response.Content.ReadFromJsonAsync<JsonElement>();
        problemDetails.GetProperty("detail").GetString()!.ShouldContain("in-flight");
    }

    [Fact]
    public async Task PostReplay_NonExistentCorrelationId_Returns404ProblemDetails() {
        // Arrange
        HttpClient client = CreateAuthenticatedClient("test-tenant");
        string correlationId = Guid.NewGuid().ToString();

        // Act
        HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/commands/replay/{correlationId}", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType!.ShouldContain("problem+json");

        JsonElement problemDetails = await response.Content.ReadFromJsonAsync<JsonElement>();
        problemDetails.GetProperty("detail").GetString()!.ShouldContain(correlationId);
    }

    [Fact]
    public async Task PostReplay_WrongTenant_Returns404ProblemDetails() {
        // Arrange - submit as tenant-a
        HttpClient tenantAClient = CreateAuthenticatedClient("tenant-a");
        string correlationId = await SubmitCommandAndGetCorrelationId(tenantAClient, "tenant-a");
        await SetCommandStatus("tenant-a", correlationId, CommandStatus.Rejected);

        // Act - replay as tenant-b (SEC-3)
        HttpClient tenantBClient = CreateAuthenticatedClient("tenant-b");
        HttpResponseMessage response = await tenantBClient.PostAsync(
            $"/api/v1/commands/replay/{correlationId}", null);

        // Assert - 404, not 403
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostReplay_NoAuthentication_Returns401() {
        // Arrange
        HttpClient client = factory.CreateClient();
        string correlationId = Guid.NewGuid().ToString();

        // Act
        HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/commands/replay/{correlationId}", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostReplay_NoTenantClaims_Returns403() {
        // Arrange - JWT with NO tenant claims
        string token = TestJwtTokenGenerator.GenerateToken(subject: "no-tenant-user");
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        string correlationId = Guid.NewGuid().ToString();

        // Act
        HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/commands/replay/{correlationId}", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType!.MediaType!.ShouldContain("problem+json");

        JsonElement problemDetails = await response.Content.ReadFromJsonAsync<JsonElement>();
        problemDetails.GetProperty("detail").GetString()!.ShouldContain("No tenant authorization claims found");
    }

    [Fact]
    public async Task PostReplay_ReplayedCommand_StatusResetToReceived() {
        // Arrange
        HttpClient client = CreateAuthenticatedClient("test-tenant");
        string correlationId = await SubmitCommandAndGetCorrelationId(client);
        await SetCommandStatus("test-tenant", correlationId, CommandStatus.Rejected);

        // Act - replay (generates a new correlation ID)
        HttpResponseMessage replayResponse = await client.PostAsync(
            $"/api/v1/commands/replay/{correlationId}", null);
        replayResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        ReplayCommandResponse? replayResult = await replayResponse.Content.ReadFromJsonAsync<ReplayCommandResponse>();
        _ = replayResult.ShouldNotBeNull();
        string replayCorrelationId = replayResult.CorrelationId;

        // Assert - status of the new replay correlation ID should be Received
        HttpResponseMessage statusResponse = await client.GetAsync(
            $"/api/v1/commands/status/{replayCorrelationId}");
        statusResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        CommandStatusResponse? status = await statusResponse.Content.ReadFromJsonAsync<CommandStatusResponse>();
        _ = status.ShouldNotBeNull();
        status.Status.ShouldBe("Received");
    }

    [Fact]
    public async Task PostReplay_LocationHeader_PointsToStatusEndpoint() {
        // Arrange
        HttpClient client = CreateAuthenticatedClient("test-tenant");
        string correlationId = await SubmitCommandAndGetCorrelationId(client);
        await SetCommandStatus("test-tenant", correlationId, CommandStatus.Rejected);

        // Act
        HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/commands/replay/{correlationId}", null);

        // Assert — Location header points to status endpoint with the new replay correlation ID
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        string? location = response.Headers.Location?.ToString();
        _ = location.ShouldNotBeNull();
        location.ShouldContain("/api/v1/commands/status/");
        // The location uses the new replay correlation ID, not the original
        ReplayCommandResponse? replayResult = await response.Content.ReadFromJsonAsync<ReplayCommandResponse>();
        _ = replayResult.ShouldNotBeNull();
        location.ShouldContain(replayResult.CorrelationId);
    }

    [Fact]
    public async Task PostReplay_ResponseIncludesPreviousStatus() {
        // Arrange
        HttpClient client = CreateAuthenticatedClient("test-tenant");
        string correlationId = await SubmitCommandAndGetCorrelationId(client);
        await SetCommandStatus("test-tenant", correlationId, CommandStatus.TimedOut);

        // Act
        HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/commands/replay/{correlationId}", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        ReplayCommandResponse? replayResult = await response.Content.ReadFromJsonAsync<ReplayCommandResponse>();
        _ = replayResult.ShouldNotBeNull();
        replayResult.PreviousStatus.ShouldBe("TimedOut");
    }

    [Fact]
    public async Task PostReplay_ReplayedThenFailedAgain_CanReplayAgain() {
        // Arrange
        HttpClient client = CreateAuthenticatedClient("test-tenant");
        string correlationId = await SubmitCommandAndGetCorrelationId(client);

        // First failure + replay
        await SetCommandStatus("test-tenant", correlationId, CommandStatus.Rejected);
        HttpResponseMessage firstReplay = await client.PostAsync(
            $"/api/v1/commands/replay/{correlationId}", null);
        firstReplay.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        // Second failure
        await SetCommandStatus("test-tenant", correlationId, CommandStatus.Rejected);

        // Act - second replay (H2: no limit on replay attempts)
        HttpResponseMessage secondReplay = await client.PostAsync(
            $"/api/v1/commands/replay/{correlationId}", null);

        // Assert
        secondReplay.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task PostReplay_ResponseIncludesCorrelationIdInProblemDetails() {
        // Arrange
        HttpClient client = CreateAuthenticatedClient("test-tenant");
        string correlationId = Guid.NewGuid().ToString();

        // Act
        HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/commands/replay/{correlationId}", null);

        // Assert - 404 ProblemDetails should have correlationId extension
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        JsonElement problemDetails = await response.Content.ReadFromJsonAsync<JsonElement>();
        problemDetails.TryGetProperty("correlationId", out JsonElement corrIdProp).ShouldBeTrue();
        corrIdProp.GetString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task PostReplay_ValidationRulesChanged_Returns400() {
        // Arrange - seed an archived command with empty Payload (simulates data that
        // would fail under tighter validation rules added after original submission)
        string correlationId = Guid.NewGuid().ToString();
        var archivedCommand = new ArchivedCommand(
            Tenant: "test-tenant",
            Domain: "test-domain",
            AggregateId: "agg-replay",
            CommandType: "CreateOrder",
            Payload: [], // empty payload fails SubmitCommandValidator
            Extensions: null,
            OriginalTimestamp: DateTimeOffset.UtcNow);

        await factory.ArchiveStore.WriteCommandAsync("test-tenant", correlationId, archivedCommand, CancellationToken.None);
        await SetCommandStatus("test-tenant", correlationId, CommandStatus.Rejected);

        HttpClient client = CreateAuthenticatedClient("test-tenant");

        // Act
        HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/commands/replay/{correlationId}", null);

        // Assert - replay goes through ValidationBehavior in MediatR pipeline
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private HttpClient CreateAuthenticatedClient(string tenantId) {
        HttpClient client = factory.CreateClient();
        string token = TestJwtTokenGenerator.GenerateToken(tenants: [tenantId], domains: ["test-domain"]);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
