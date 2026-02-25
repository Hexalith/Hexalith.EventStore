
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
/// Tier 3 contract tests validating domain service hot reload: independent restart
/// without requiring CommandApi or EventStore actor system restart.
/// Validates DAPR service discovery recovery, command flow continuity across restart,
/// and graceful handling of commands submitted during the restart window.
/// </summary>
[Trait("Category", "E2E")]
[Trait("Tier", "3")]
[Trait("Feature", "HotReload")]
[Collection("AspireContractTests")]
public class HotReloadTests {
    private static readonly TimeSpan s_statusPollTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan s_statusPollInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan s_resourceRecoveryTimeout = TimeSpan.FromSeconds(60);

    private readonly AspireContractTestFixture _fixture;

    public HotReloadTests(AspireContractTestFixture fixture) => _fixture = fixture;

    // ------------------------------------------------------------------
    // Task 2: Independent restart test (AC #1, #2)
    // ------------------------------------------------------------------

    /// <summary>
    /// AC #1, #2: Verify that the sample domain service can be stopped and restarted
    /// independently without restarting the CommandApi or EventStore actor system.
    /// Commands submitted before and after restart both complete successfully.
    /// </summary>
    [Fact]
    public async Task ProcessCommand_AfterDomainServiceRestart_CompletesSuccessfully() {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        CancellationToken ct = cts.Token;

        // Phase 1 (baseline): Send IncrementCounter command, verify Completed with events
        string aggregateId = $"counter-hotreload-{Guid.NewGuid():N}";
        string correlationId1 = await SubmitCommandAndGetCorrelationIdAsync(
            tenant: "tenant-a",
            domain: "counter",
            aggregateId: aggregateId,
            commandType: "IncrementCounter");

        JsonElement status1 = await PollUntilTerminalStatusAsync(correlationId1, "tenant-a");
        status1.GetProperty("status").GetString().ShouldBe("Completed",
            "Baseline command should complete before restart");
        status1.TryGetProperty("eventCount", out JsonElement ec1).ShouldBeTrue();
        ec1.GetInt32().ShouldBeGreaterThan(0, "Baseline command should produce events");

        // AC #5: Verify CommandApi is responsive before restart
        await AssertCommandApiResponsiveAsync();

        // Phase 2 (restart): Stop sample domain service, then restart it
        await _fixture.App.ResourceCommands
            .ExecuteCommandAsync("sample", "resource-stop", ct);

        // AC #5: CommandApi should remain responsive while domain service is stopped
        await AssertCommandApiResponsiveAsync();

        await _fixture.App.ResourceCommands
            .ExecuteCommandAsync("sample", "resource-start", ct);

        // Wait for sample to become healthy again
        await _fixture.App.ResourceNotifications
            .WaitForResourceHealthyAsync("sample", ct)
            .WaitAsync(s_resourceRecoveryTimeout, ct);

        // Phase 3 (verify): Send another command after restart, verify Completed
        string correlationId2 = await SubmitCommandAndGetCorrelationIdAsync(
            tenant: "tenant-a",
            domain: "counter",
            aggregateId: aggregateId,
            commandType: "IncrementCounter");

        JsonElement status2 = await PollUntilTerminalStatusAsync(correlationId2, "tenant-a");
        status2.GetProperty("status").GetString().ShouldBe("Completed",
            "Post-restart command should complete successfully (AC #1, #2)");
        status2.TryGetProperty("eventCount", out JsonElement ec2).ShouldBeTrue();
        ec2.GetInt32().ShouldBeGreaterThan(0, "Post-restart command should produce events");

        // AC #5: CommandApi should still be responsive after restart cycle
        await AssertCommandApiResponsiveAsync();
    }

    // ------------------------------------------------------------------
    // Task 3: DAPR recovery test (AC #3, #4)
    // ------------------------------------------------------------------

    /// <summary>
    /// AC #3, #4: Verify that commands submitted while the domain service is unavailable
    /// are handled by DAPR resiliency policies. After restart, commands either reach
    /// Completed (DAPR retried successfully) or a terminal failure state -- no silent
    /// data loss or hung commands.
    /// </summary>
    [Fact]
    public async Task ProcessCommand_DuringDomainServiceRestart_HandledByResiliency() {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        CancellationToken ct = cts.Token;

        // Stop sample domain service
        await _fixture.App.ResourceCommands
            .ExecuteCommandAsync("sample", "resource-stop", ct);

        // Immediately submit a command while service is down
        string aggregateId = $"counter-resiliency-{Guid.NewGuid():N}";
        string correlationId = await SubmitCommandAndGetCorrelationIdAsync(
            tenant: "tenant-a",
            domain: "counter",
            aggregateId: aggregateId,
            commandType: "IncrementCounter");

        // Restart sample domain service
        await _fixture.App.ResourceCommands
            .ExecuteCommandAsync("sample", "resource-start", ct);

        // Wait for sample to become healthy again
        await _fixture.App.ResourceNotifications
            .WaitForResourceHealthyAsync("sample", ct)
            .WaitAsync(s_resourceRecoveryTimeout, ct);

        // Poll the in-flight command with extended timeout (DAPR retries may take time)
        JsonElement status = await PollUntilTerminalStatusAsync(
            correlationId,
            "tenant-a",
            timeout: s_resourceRecoveryTimeout);

        // AC #4: Command should reach a terminal state -- either Completed (DAPR retried)
        // or a terminal failure (PublishFailed/TimedOut). No hung commands.
        string statusValue = status.GetProperty("status").GetString()!;
        statusValue.ShouldBeOneOf(
            ["Completed", "Rejected", "PublishFailed", "TimedOut"],
            $"Command submitted during restart must reach terminal state, got: {statusValue}");
    }

    // ------------------------------------------------------------------
    // Task 4: CommandApi resilience test (AC #5)
    // ------------------------------------------------------------------

    /// <summary>
    /// AC #5: Verify that CommandApi remains responsive throughout the domain service
    /// restart cycle. Health endpoint returns 200 and new commands are accepted (202).
    /// </summary>
    [Fact]
    public async Task CommandApi_DuringDomainServiceRestart_RemainsResponsive() {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        CancellationToken ct = cts.Token;

        // Stop sample domain service
        await _fixture.App.ResourceCommands
            .ExecuteCommandAsync("sample", "resource-stop", ct);

        // AC #5: Assert CommandApi health endpoint returns 200 OK
        await AssertCommandApiResponsiveAsync();

        // AC #5: Assert CommandApi can accept new commands (returns 202 Accepted with tracking ID)
        string aggregateId = $"counter-apicheck-{Guid.NewGuid():N}";
        using HttpRequestMessage request = CreateCommandRequest(
            tenant: "tenant-a",
            domain: "counter",
            aggregateId: aggregateId,
            commandType: "IncrementCounter");

        using HttpResponseMessage response = await _fixture.CommandApiClient.SendAsync(request);

        if (response.StatusCode != HttpStatusCode.Accepted) {
            string body = await response.Content.ReadAsStringAsync();
            throw new ShouldAssertException(
                $"CommandApi should accept commands while domain service is down. "
                + $"Expected 202 but got {(int)response.StatusCode}.\nBody:\n{body}");
        }

        JsonElement result = await response.Content.ReadFromJsonAsync<JsonElement>();
        result.GetProperty("correlationId").GetString().ShouldNotBeNullOrEmpty(
            "CommandApi should return correlationId even when domain service is down");

        // Restart sample domain service (cleanup for other tests)
        await _fixture.App.ResourceCommands
            .ExecuteCommandAsync("sample", "resource-start", ct);

        await _fixture.App.ResourceNotifications
            .WaitForResourceHealthyAsync("sample", ct)
            .WaitAsync(s_resourceRecoveryTimeout, ct);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private async Task AssertCommandApiResponsiveAsync() {
        using var healthRequest = new HttpRequestMessage(HttpMethod.Get, "/");
        using HttpResponseMessage healthResponse = await _fixture.CommandApiClient
            .SendAsync(healthRequest).ConfigureAwait(false);
        healthResponse.StatusCode.ShouldBe(HttpStatusCode.OK,
            "CommandApi health endpoint should return 200 OK during domain service restart");
    }

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

    private async Task<JsonElement> PollUntilTerminalStatusAsync(
        string correlationId,
        string tenant,
        TimeSpan? timeout = null) {
        string token = TestJwtTokenGenerator.GenerateToken(
            tenants: [tenant],
            domains: ["counter"],
            permissions: ["command:submit", "command:query"]);

        TimeSpan effectiveTimeout = timeout ?? s_statusPollTimeout;
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(effectiveTimeout);
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
            $"Command {correlationId} did not reach terminal status within {effectiveTimeout}. "
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
