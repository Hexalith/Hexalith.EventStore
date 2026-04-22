
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
/// Tier 3 acceptance test: when a command is submitted via the EventStore API and reaches
/// terminal status, the Admin Server commands endpoint must reflect the new command.
/// Verifies cross-service visibility: EventStore (in-memory tracker) → DAPR service invocation → Admin Server.
/// </summary>
[Trait("Category", "E2E")]
[Trait("Tier", "3")]
[Collection("AspireContractTests")]
public class AdminCommandVisibilityTests {
    private static readonly TimeSpan s_statusPollTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan s_statusPollInterval = TimeSpan.FromMilliseconds(500);

    private readonly AspireContractTestFixture _fixture;

    public AdminCommandVisibilityTests(AspireContractTestFixture fixture) => _fixture = fixture;

    /// <summary>
    /// AC-1: Submit IncrementCounter → wait for Completed → admin commands count should increase by 1.
    /// </summary>
    [Fact]
    public async Task IncrementCounter_AdminCommandsCount_IncrementsByOne() {
        // Arrange — get baseline command count from admin API
        int baselineCount = await GetAdminCommandCountAsync("tenant-a");

        string aggregateId = $"counter-admin-vis-{Guid.NewGuid():N}";

        // Act — submit IncrementCounter and wait for completion
        string correlationId = await SubmitCommandAndGetCorrelationIdAsync(
            tenant: "tenant-a",
            domain: "counter",
            aggregateId: aggregateId,
            commandType: "IncrementCounter");

        JsonElement terminalStatus = await PollUntilTerminalStatusAsync(correlationId, "tenant-a");
        terminalStatus.GetProperty("status").GetString().ShouldBe("Completed",
            "IncrementCounter should complete before checking admin visibility");

        // Assert — poll admin commands count (eventual consistency via DAPR service invocation)
        _ = await PollUntilAdminCountReachesAsync("tenant-a", baselineCount + 1);
    }

    // ------------------------------------------------------------------
    // Admin API helpers
    // ------------------------------------------------------------------

    private async Task<int> PollUntilAdminCountReachesAsync(string tenantId, int expectedCount) {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(s_statusPollTimeout);
        int lastCount = -1;

        while (DateTimeOffset.UtcNow < deadline) {
            lastCount = await GetAdminCommandCountAsync(tenantId).ConfigureAwait(false);
            if (lastCount >= expectedCount) {
                return lastCount;
            }

            await Task.Delay(s_statusPollInterval).ConfigureAwait(false);
        }

        throw new TimeoutException(
            $"Admin commands count did not reach {expectedCount} within {s_statusPollTimeout}. "
            + $"Last count: {lastCount}");
    }

    private async Task<int> GetAdminCommandCountAsync(string tenantId) {
        string token = TestJwtTokenGenerator.GenerateToken(
            tenants: [tenantId],
            domains: ["counter"],
            permissions: ["command:submit", "command:query"],
            role: "GlobalAdministrator");

        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/v1/admin/streams/commands?tenantId={tenantId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using HttpResponseMessage response = await _fixture.AdminServerClient
            .SendAsync(request).ConfigureAwait(false);

        if (response.StatusCode != HttpStatusCode.OK) {
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            throw new ShouldAssertException(
                $"Admin commands query failed with {(int)response.StatusCode} {response.StatusCode}.\nBody:\n{body}");
        }

        JsonElement result = await response.Content.ReadFromJsonAsync<JsonElement>().ConfigureAwait(false);

        // PagedResult<CommandSummary> has Items array and TotalCount
        if (result.TryGetProperty("totalCount", out JsonElement totalCountProp)
            && totalCountProp.ValueKind == JsonValueKind.Number) {
            return totalCountProp.GetInt32();
        }

        // Fallback: count items array
        if (result.TryGetProperty("items", out JsonElement itemsProp)
            && itemsProp.ValueKind == JsonValueKind.Array) {
            return itemsProp.GetArrayLength();
        }

        return 0;
    }

    // ------------------------------------------------------------------
    // EventStore helpers (same pattern as CommandLifecycleTests)
    // ------------------------------------------------------------------

    private async Task<string> SubmitCommandAndGetCorrelationIdAsync(
        string tenant,
        string domain,
        string aggregateId,
        string commandType) {
        using HttpRequestMessage request = CreateCommandRequest(tenant, domain, aggregateId, commandType);
        using HttpResponseMessage response = await _fixture.EventStoreClient
            .SendAsync(request).ConfigureAwait(false);

        if (response.StatusCode != HttpStatusCode.Accepted) {
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            throw new ShouldAssertException(
                $"Expected 202 Accepted but was {(int)response.StatusCode} {response.StatusCode}.\nBody:\n{body}");
        }

        JsonElement result = await response.Content.ReadFromJsonAsync<JsonElement>().ConfigureAwait(false);
        return result.GetProperty("correlationId").GetString()!;
    }

    private async Task<JsonElement> PollUntilTerminalStatusAsync(
        string correlationId,
        string tenant) {
        string token = TestJwtTokenGenerator.GenerateToken(
            tenants: [tenant],
            domains: ["counter"],
            permissions: ["command:submit", "command:query"]);

        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(s_statusPollTimeout);
        JsonElement lastStatus = default;

        while (DateTimeOffset.UtcNow < deadline) {
            using var statusRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/commands/status/{correlationId}");
            statusRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using HttpResponseMessage statusResponse = await _fixture.EventStoreClient
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
            MessageId = Guid.NewGuid().ToString(),
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
