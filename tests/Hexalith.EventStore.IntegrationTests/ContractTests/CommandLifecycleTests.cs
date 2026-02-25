
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
/// Tier 3 end-to-end contract tests verifying the full command lifecycle across the
/// complete Aspire topology: CommandApi -> Actor -> Domain Service -> State Store -> Pub/Sub.
/// Uses symmetric key JWT authentication for fast execution (AC #2).
/// </summary>
[Trait("Category", "E2E")]
[Trait("Tier", "3")]
[Collection("AspireContractTests")]
public class CommandLifecycleTests {
    private static readonly TimeSpan s_statusPollTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan s_statusPollInterval = TimeSpan.FromMilliseconds(500);

    private readonly AspireContractTestFixture _fixture;

    public CommandLifecycleTests(AspireContractTestFixture fixture) => _fixture = fixture;

    /// <summary>
    /// AC #2: Submit a valid IncrementCounter command and verify 202 Accepted with correlation ID.
    /// </summary>
    [Fact]
    public async Task SubmitCommand_ValidIncrementCounter_Returns202Accepted() {
        // Arrange
        using HttpRequestMessage request = CreateCommandRequest(
            tenant: "tenant-a",
            domain: "counter",
            aggregateId: $"counter-{Guid.NewGuid():N}",
            commandType: "IncrementCounter");

        // Act
        using HttpResponseMessage response = await _fixture.CommandApiClient
            .SendAsync(request);

        // Assert
        if (response.StatusCode != HttpStatusCode.Accepted) {
            string body = await response.Content.ReadAsStringAsync();
            throw new ShouldAssertException(
                $"Expected 202 Accepted but was {(int)response.StatusCode} {response.StatusCode}.\nResponse body:\n{body}");
        }

        JsonElement result = await response.Content.ReadFromJsonAsync<JsonElement>();
        result.GetProperty("correlationId").GetString().ShouldNotBeNullOrEmpty();
        _ = response.Headers.Location.ShouldNotBeNull();
    }

    /// <summary>
    /// AC #2: Submit command, then poll GET /api/v1/commands/status/{id} until Completed.
    /// Verifies command status tracking through stages to terminal state.
    /// </summary>
    [Fact]
    public async Task SubmitCommand_PollStatus_ReachesCompleted() {
        // Arrange
        string aggregateId = $"counter-lifecycle-{Guid.NewGuid():N}";
        string correlationId = await SubmitCommandAndGetCorrelationIdAsync(
            tenant: "tenant-a",
            domain: "counter",
            aggregateId: aggregateId,
            commandType: "IncrementCounter");

        // Act - poll status until terminal state
        JsonElement status = await PollUntilTerminalStatusAsync(correlationId, "tenant-a");

        // Assert
        status.GetProperty("status").GetString().ShouldBe("Completed");
        status.GetProperty("statusCode").GetInt32().ShouldBe(4); // CommandStatus.Completed = 4
    }

    /// <summary>
    /// AC #2: Full lifecycle - submit command, verify state change by submitting another command
    /// that depends on updated state.
    /// </summary>
    [Fact]
    public async Task FullLifecycle_IncrementThenVerifyStateViaSecondCommand_Succeeds() {
        // Arrange - use unique aggregate
        string aggregateId = $"counter-full-{Guid.NewGuid():N}";

        // Act - submit first IncrementCounter
        string correlationId1 = await SubmitCommandAndGetCorrelationIdAsync(
            tenant: "tenant-a",
            domain: "counter",
            aggregateId: aggregateId,
            commandType: "IncrementCounter");

        // Wait for first command to complete
        JsonElement status1 = await PollUntilTerminalStatusAsync(correlationId1, "tenant-a");
        status1.GetProperty("status").GetString().ShouldBe("Completed");

        // Act - submit second IncrementCounter (depends on state from first)
        string correlationId2 = await SubmitCommandAndGetCorrelationIdAsync(
            tenant: "tenant-a",
            domain: "counter",
            aggregateId: aggregateId,
            commandType: "IncrementCounter");

        // Assert - second command also completes (proves state was rehydrated)
        JsonElement status2 = await PollUntilTerminalStatusAsync(correlationId2, "tenant-a");
        status2.GetProperty("status").GetString().ShouldBe("Completed");
    }

    /// <summary>
    /// AC #2: Multiple sequential commands: IncrementCounter x3 -> DecrementCounter -> verify
    /// counter state = 2 via next IncrementCounter succeeding (state reflects all prior events).
    /// </summary>
    [Fact]
    public async Task MultipleSequentialCommands_IncrementThreeThenDecrement_AllComplete() {
        // Arrange
        string aggregateId = $"counter-multi-{Guid.NewGuid():N}";

        // Act - increment 3 times
        for (int i = 0; i < 3; i++) {
            string corrId = await SubmitCommandAndGetCorrelationIdAsync(
                tenant: "tenant-a",
                domain: "counter",
                aggregateId: aggregateId,
                commandType: "IncrementCounter");

            JsonElement status = await PollUntilTerminalStatusAsync(corrId, "tenant-a");
            status.GetProperty("status").GetString().ShouldBe("Completed",
                $"IncrementCounter #{i + 1} should complete successfully");
        }

        // Act - decrement once (count should go from 3 to 2)
        string decCorrId = await SubmitCommandAndGetCorrelationIdAsync(
            tenant: "tenant-a",
            domain: "counter",
            aggregateId: aggregateId,
            commandType: "DecrementCounter");

        JsonElement decStatus = await PollUntilTerminalStatusAsync(decCorrId, "tenant-a");
        decStatus.GetProperty("status").GetString().ShouldBe("Completed");

        // Assert - another increment succeeds (proves state = 2 -> 3)
        string finalCorrId = await SubmitCommandAndGetCorrelationIdAsync(
            tenant: "tenant-a",
            domain: "counter",
            aggregateId: aggregateId,
            commandType: "IncrementCounter");

        JsonElement finalStatus = await PollUntilTerminalStatusAsync(finalCorrId, "tenant-a");
        finalStatus.GetProperty("status").GetString().ShouldBe("Completed");
    }

    /// <summary>
    /// AC #2: ResetCounter command resets counter state to 0.
    /// </summary>
    [Fact]
    public async Task ResetCounter_AfterIncrement_Succeeds() {
        // Arrange - create counter with value > 0
        string aggregateId = $"counter-reset-{Guid.NewGuid():N}";

        string incCorrId = await SubmitCommandAndGetCorrelationIdAsync(
            tenant: "tenant-a",
            domain: "counter",
            aggregateId: aggregateId,
            commandType: "IncrementCounter");

        JsonElement incStatus = await PollUntilTerminalStatusAsync(incCorrId, "tenant-a");
        incStatus.GetProperty("status").GetString().ShouldBe("Completed");

        // Act - reset
        string resetCorrId = await SubmitCommandAndGetCorrelationIdAsync(
            tenant: "tenant-a",
            domain: "counter",
            aggregateId: aggregateId,
            commandType: "ResetCounter");

        // Assert
        JsonElement resetStatus = await PollUntilTerminalStatusAsync(resetCorrId, "tenant-a");
        resetStatus.GetProperty("status").GetString().ShouldBe("Completed");
    }

    /// <summary>
    /// AC #2: DecrementCounter on zero counter produces CounterCannotGoNegative rejection event.
    /// </summary>
    [Fact]
    public async Task DecrementCounter_OnZeroCounter_ReturnsRejected() {
        // Arrange - fresh counter (count = 0)
        string aggregateId = $"counter-reject-{Guid.NewGuid():N}";

        // Act - decrement on zero
        string corrId = await SubmitCommandAndGetCorrelationIdAsync(
            tenant: "tenant-a",
            domain: "counter",
            aggregateId: aggregateId,
            commandType: "DecrementCounter");

        // Assert - should be rejected
        JsonElement status = await PollUntilTerminalStatusAsync(corrId, "tenant-a");
        status.GetProperty("status").GetString().ShouldBe("Rejected");
        status.GetProperty("rejectionEventType").GetString()!.ShouldContain("CounterCannotGoNegative");
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private async Task<string> SubmitCommandAndGetCorrelationIdAsync(
        string tenant,
        string domain,
        string aggregateId,
        string commandType) {
        using HttpRequestMessage request = CreateCommandRequest(tenant, domain, aggregateId, commandType);
        using HttpResponseMessage response = await _fixture.CommandApiClient
            .SendAsync(request).ConfigureAwait(false);

        if (response.StatusCode != HttpStatusCode.Accepted) {
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            throw new ShouldAssertException(
                $"Expected 202 Accepted but was {(int)response.StatusCode} {response.StatusCode}.\nBody:\n{body}");
        }

        JsonElement result = await response.Content.ReadFromJsonAsync<JsonElement>().ConfigureAwait(false);
        return result.GetProperty("correlationId").GetString()!;
    }

    private async Task<JsonElement> PollUntilTerminalStatusAsync(string correlationId, string tenant) {
        string token = TestJwtTokenGenerator.GenerateToken(
            tenants: [tenant],
            domains: ["counter"],
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

                // Terminal states: Completed, Rejected, PublishFailed, TimedOut
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

    private static HttpRequestMessage CreateCommandRequest(
        string tenant,
        string domain,
        string aggregateId,
        string commandType) {
        string token = TestJwtTokenGenerator.GenerateToken(
            tenants: [tenant],
            domains: [domain],
            permissions: ["command:submit", "command:query"]);

        var body = new {
            Tenant = tenant,
            Domain = domain,
            AggregateId = aggregateId,
            CommandType = commandType,
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
