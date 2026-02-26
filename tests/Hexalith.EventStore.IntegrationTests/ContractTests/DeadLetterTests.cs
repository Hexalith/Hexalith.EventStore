
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.IntegrationTests.Fixtures;
using Hexalith.EventStore.IntegrationTests.Helpers;

using Shouldly;

namespace Hexalith.EventStore.IntegrationTests.ContractTests;

/// <summary>
/// Tier 3 end-to-end tests verifying dead-letter routing for simulated failures (AC #5).
/// Commands to non-existent domain services trigger dead-letter routing with full context.
/// </summary>
[Trait("Category", "E2E")]
[Trait("Tier", "3")]
[Collection("AspireContractTests")]
public class DeadLetterTests {
    private static readonly TimeSpan s_statusPollTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan s_statusPollInterval = TimeSpan.FromMilliseconds(500);

    private readonly AspireContractTestFixture _fixture;

    public DeadLetterTests(AspireContractTestFixture fixture) => _fixture = fixture;

    /// <summary>
    /// AC #5: Command to non-existent domain service triggers dead-letter routing.
    /// The command is accepted (202) but processing fails because the domain service
    /// is not registered, resulting in a terminal failure status.
    /// </summary>
    [Fact]
    public async Task SubmitCommand_NonExistentDomain_ReachesFailureOrTimeout() {
        // Arrange - submit command for a domain that has no registered processor
        string token = TestJwtTokenGenerator.GenerateToken(
            tenants: ["tenant-a"],
            domains: ["nonexistent-domain"],
            permissions: ["command:submit", "command:query"]);

        var body = new {
            Tenant = "tenant-a",
            Domain = "nonexistent-domain",
            AggregateId = $"dead-letter-{Guid.NewGuid():N}",
            CommandType = "UnknownCommand",
            Payload = new { id = "test" },
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/commands") {
            Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act - submit should be accepted (routing happens asynchronously via actors)
        using HttpResponseMessage response = await _fixture.CommandApiClient
            .SendAsync(request);

        if (response.StatusCode != HttpStatusCode.Accepted) {
            string responseBody = await response.Content.ReadAsStringAsync();
            throw new ShouldAssertException(
                $"Expected 202 Accepted but was {(int)response.StatusCode} {response.StatusCode}.\nBody:\n{responseBody}");
        }

        JsonElement result = await response.Content.ReadFromJsonAsync<JsonElement>();
        string correlationId = result.GetProperty("correlationId").GetString()!;

        // Assert - poll status; expect a non-Completed terminal state (Rejected, PublishFailed, or TimedOut)
        JsonElement status = await PollUntilTerminalStatusAsync(correlationId, "tenant-a");
        string statusValue = status.GetProperty("status").GetString()!;

        // Dead-letter routing should result in a terminal error state, not "Completed"
        statusValue.ShouldNotBe("Completed",
            "Command to non-existent domain should not reach Completed status");

        // Should be one of the terminal failure states
        statusValue.ShouldBeOneOf("Rejected", "PublishFailed", "TimedOut");
    }

    /// <summary>
    /// AC #5: Dead-letter includes full context. Verifies status response contains meaningful
    /// failure context fields (failureReason, rejectionEventType, or timeoutDuration) with
    /// non-empty values that describe the actual failure. The dead-letter message published to
    /// the Dapr pub/sub topic (deadletter.{tenant}.{domain}.events) contains the full
    /// CommandEnvelope, failure stage, exception details, correlation ID, tenant, domain, and
    /// aggregate ID per DeadLetterMessage contract. Status endpoint fields mirror this context.
    /// </summary>
    [Fact]
    public async Task SubmitCommand_NonExistentDomain_StatusIncludesFailureContext() {
        // Arrange
        string aggregateId = $"dead-ctx-{Guid.NewGuid():N}";
        string token = TestJwtTokenGenerator.GenerateToken(
            tenants: ["tenant-a"],
            domains: ["missing-service"],
            permissions: ["command:submit", "command:query"]);

        var body = new {
            Tenant = "tenant-a",
            Domain = "missing-service",
            AggregateId = aggregateId,
            CommandType = "SomeCommand",
            Payload = new { id = "test" },
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/commands") {
            Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using HttpResponseMessage response = await _fixture.CommandApiClient
            .SendAsync(request);

        if (response.StatusCode != HttpStatusCode.Accepted) {
            string responseBody = await response.Content.ReadAsStringAsync();
            throw new ShouldAssertException(
                $"Expected 202 Accepted but was {(int)response.StatusCode} {response.StatusCode}.\nBody:\n{responseBody}");
        }

        JsonElement result = await response.Content.ReadFromJsonAsync<JsonElement>();
        string correlationId = result.GetProperty("correlationId").GetString()!;

        // Act
        JsonElement status = await PollUntilTerminalStatusAsync(correlationId, "tenant-a");

        // Assert - terminal failure status should NOT be Completed
        string statusValue = status.GetProperty("status").GetString()!;
        statusValue.ShouldNotBe("Completed",
            "Command to non-existent domain should trigger dead-letter routing, not completion");

        // Assert - the status record must include meaningful failure context.
        // This mirrors the DeadLetterMessage contract which includes full command envelope,
        // failure stage, exception type/message, correlationId, tenant, domain, and aggregateId.
        bool hasFailureReason = status.TryGetProperty("failureReason", out JsonElement failureReason)
            && failureReason.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(failureReason.GetString());
        bool hasRejectionType = status.TryGetProperty("rejectionEventType", out JsonElement rejectionType)
            && rejectionType.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(rejectionType.GetString());
        bool hasTimeoutDuration = status.TryGetProperty("timeoutDuration", out _);

        (hasFailureReason || hasRejectionType || hasTimeoutDuration).ShouldBeTrue(
            $"Terminal failure status '{statusValue}' must include non-empty context field "
            + "(failureReason, rejectionEventType, or timeoutDuration) reflecting dead-letter payload context. "
            + $"Status response: {status}");

        // Assert - if failureReason is present, it should contain meaningful diagnostic info
        if (hasFailureReason) {
            string reason = failureReason.GetString()!;
            reason.Length.ShouldBeGreaterThan(5,
                "failureReason should contain a meaningful description of the failure, not a stub");
        }

        // Assert - aggregateId MUST be present in the status record (proves command context is preserved)
        status.TryGetProperty("aggregateId", out JsonElement aggIdProp).ShouldBeTrue(
            "Status record must include aggregateId for dead-letter traceability");
        aggIdProp.ValueKind.ShouldBe(JsonValueKind.String,
            "aggregateId must be a string value");
        aggIdProp.GetString().ShouldBe(aggregateId,
            "Status record must preserve the original aggregateId");

        // NOTE: Direct dead-letter topic inspection (reading from deadletter.{tenant}.{domain}.events
        // Dapr pub/sub topic) is not feasible in Tier 3 E2E tests because there is no consumer/subscriber
        // endpoint for the dead-letter topic in the test topology. The status endpoint fields mirror the
        // DeadLetterMessage contract (full CommandEnvelope, failure stage, exception details, correlationId,
        // tenant, domain, aggregateId) and serve as the observable proxy for dead-letter payload verification.
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private async Task<JsonElement> PollUntilTerminalStatusAsync(string correlationId, string tenant) {
        string token = TestJwtTokenGenerator.GenerateToken(
            tenants: [tenant],
            domains: ["nonexistent-domain", "missing-service"],
            permissions: ["command:submit", "command:query"]);

        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(s_statusPollTimeout);
        JsonElement lastStatus = default;

        while (DateTimeOffset.UtcNow < deadline) {
            using var statusRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/commands/status/{correlationId}");
            statusRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using HttpResponseMessage statusResponse = await _fixture.CommandApiClient
                .SendAsync(statusRequest).ConfigureAwait(false);

            if (statusResponse.StatusCode == HttpStatusCode.OK) {
                lastStatus = await statusResponse.Content.ReadFromJsonAsync<JsonElement>().ConfigureAwait(false);
                string statusValue = lastStatus.GetProperty("status").GetString()!;

                if (statusValue is "Completed" or "Rejected" or "PublishFailed" or "TimedOut") {
                    return lastStatus;
                }
            }

            await Task.Delay(s_statusPollInterval).ConfigureAwait(false);
        }

        throw new TimeoutException(
            $"Command {correlationId} did not reach terminal status within {s_statusPollTimeout}. "
            + $"Last status: {lastStatus}");
    }
}
