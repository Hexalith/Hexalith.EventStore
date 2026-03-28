
using System.Text.Json;

using Hexalith.EventStore.IntegrationTests.Fixtures;
using Hexalith.EventStore.IntegrationTests.Helpers;

using Shouldly;

namespace Hexalith.EventStore.IntegrationTests.ContractTests;

/// <summary>
/// Tier 3 contract tests validating domain service hot reload: independent restart
/// without requiring EventStore or EventStore actor system restart.
/// Validates DAPR service discovery recovery, command flow continuity across restart,
/// and graceful handling of commands submitted during the restart window.
/// </summary>
[Trait("Category", "E2E")]
[Trait("Tier", "3")]
[Trait("Feature", "HotReload")]
[Collection("AspireContractTests")]
public class HotReloadTests {
    private static readonly TimeSpan s_resourceRecoveryTimeout = TimeSpan.FromSeconds(60);

    private readonly AspireContractTestFixture _fixture;

    public HotReloadTests(AspireContractTestFixture fixture) => _fixture = fixture;

    // ------------------------------------------------------------------
    // Task 2: Independent restart test (AC #1, #2)
    // ------------------------------------------------------------------

    /// <summary>
    /// AC #1, #2: Verify that the sample domain service can be stopped and restarted
    /// independently without restarting the EventStore or EventStore actor system.
    /// Commands submitted before and after restart both complete successfully.
    /// </summary>
    [Fact]
    public async Task ProcessCommand_AfterDomainServiceRestart_CompletesSuccessfully() {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        CancellationToken ct = cts.Token;
        HttpClient client = _fixture.EventStoreClient;

        // Phase 1 (baseline): Send IncrementCounter command, verify Completed with events
        string aggregateId = $"counter-hotreload-{Guid.NewGuid():N}";
        string correlationId1 = await ContractTestHelpers.SubmitCommandAndGetCorrelationIdAsync(
            client, tenant: "tenant-a", domain: "counter",
            aggregateId: aggregateId, commandType: "IncrementCounter");

        JsonElement status1 = await ContractTestHelpers.PollUntilTerminalStatusAsync(
            client, correlationId1, "tenant-a");
        status1.GetProperty("status").GetString().ShouldBe("Completed",
            "Baseline command should complete before restart");
        status1.TryGetProperty("eventCount", out JsonElement ec1).ShouldBeTrue();
        ec1.GetInt32().ShouldBeGreaterThan(0, "Baseline command should produce events");

        // AC #5: Verify EventStore is responsive before restart
        await ContractTestHelpers.AssertEventStoreResponsiveAsync(client);

        // Phase 2 (restart): Stop sample domain service, then restart it
        _ = await _fixture.App.ResourceCommands
            .ExecuteCommandAsync("sample", "resource-stop", ct);

        // AC #5: EventStore should remain responsive while domain service is stopped
        await ContractTestHelpers.AssertEventStoreResponsiveAsync(client);

        _ = await _fixture.App.ResourceCommands
            .ExecuteCommandAsync("sample", "resource-start", ct);

        // Wait for sample to become healthy again
        _ = await _fixture.App.ResourceNotifications
            .WaitForResourceHealthyAsync("sample", ct)
            .WaitAsync(s_resourceRecoveryTimeout, ct);

        // Phase 3 (verify): Send another command after restart, verify Completed
        string correlationId2 = await ContractTestHelpers.SubmitCommandAndGetCorrelationIdAsync(
            client, tenant: "tenant-a", domain: "counter",
            aggregateId: aggregateId, commandType: "IncrementCounter");

        JsonElement status2 = await ContractTestHelpers.PollUntilTerminalStatusAsync(
            client, correlationId2, "tenant-a");
        status2.GetProperty("status").GetString().ShouldBe("Completed",
            "Post-restart command should complete successfully (AC #1, #2)");
        status2.TryGetProperty("eventCount", out JsonElement ec2).ShouldBeTrue();
        ec2.GetInt32().ShouldBeGreaterThan(0, "Post-restart command should produce events");

        // Stronger continuity assertion: state updated by IncrementCounter should allow a DecrementCounter.
        string correlationId3 = await ContractTestHelpers.SubmitCommandAndGetCorrelationIdAsync(
            client, tenant: "tenant-a", domain: "counter",
            aggregateId: aggregateId, commandType: "DecrementCounter");

        JsonElement status3 = await ContractTestHelpers.PollUntilTerminalStatusAsync(
            client, correlationId3, "tenant-a");
        status3.GetProperty("status").GetString().ShouldBe("Completed",
            "Decrement after post-restart increment should complete, proving command flow continuity across restart");

        // AC #5: EventStore should still be responsive after restart cycle
        await ContractTestHelpers.AssertEventStoreResponsiveAsync(client);
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
        HttpClient client = _fixture.EventStoreClient;

        // Stop sample domain service
        _ = await _fixture.App.ResourceCommands
            .ExecuteCommandAsync("sample", "resource-stop", ct);

        // Submit while restart is in-flight (retry through transient transport windows).
        string aggregateId = $"counter-resiliency-{Guid.NewGuid():N}";
        Task<string> submitTask = ContractTestHelpers.SubmitCommandAndGetCorrelationIdWithRetryAsync(
            client, tenant: "tenant-a", domain: "counter",
            aggregateId: aggregateId, commandType: "IncrementCounter");

        // Restart sample domain service
        _ = await _fixture.App.ResourceCommands
            .ExecuteCommandAsync("sample", "resource-start", ct);

        // Wait for sample to become healthy again
        _ = await _fixture.App.ResourceNotifications
            .WaitForResourceHealthyAsync("sample", ct)
            .WaitAsync(s_resourceRecoveryTimeout, ct);

        string correlationId = await submitTask.ConfigureAwait(true);

        // Poll the in-flight command with extended timeout (DAPR retries may take time)
        JsonElement status = await ContractTestHelpers.PollUntilTerminalStatusAsync(
            client, correlationId, "tenant-a", timeout: s_resourceRecoveryTimeout);

        // AC #4: Command should reach a terminal state -- either Completed (DAPR retried)
        // or a terminal failure (PublishFailed/TimedOut). No hung commands.
        string statusValue = status.GetProperty("status").GetString()!;
        statusValue.ShouldBeOneOf(
            ["Completed", "PublishFailed", "TimedOut"],
            $"Command submitted during restart must reach terminal state, got: {statusValue}");
    }

    // ------------------------------------------------------------------
    // Task 4: EventStore resilience test (AC #5)
    // ------------------------------------------------------------------

    /// <summary>
    /// AC #5: Verify that EventStore remains responsive throughout the domain service
    /// restart cycle. Health endpoint returns 200 and new commands are accepted (202).
    /// </summary>
    [Fact]
    public async Task EventStore_DuringDomainServiceRestart_RemainsResponsive() {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        CancellationToken ct = cts.Token;
        HttpClient client = _fixture.EventStoreClient;

        // Stop sample domain service
        _ = await _fixture.App.ResourceCommands
            .ExecuteCommandAsync("sample", "resource-stop", ct);

        // AC #5 (Task 4.3): Assert EventStore remains reachable while domain service is stopped.
        await ContractTestHelpers.AssertEventStoreResponsiveAsync(client);

        // AC #5 (Task 4.4): Assert EventStore can accept new commands while domain service is down.
        string aggregateIdWhileDown = $"counter-apicheck-down-{Guid.NewGuid():N}";
        string correlationIdWhileDown = await ContractTestHelpers.SubmitCommandAndGetCorrelationIdWithRetryAsync(
            client, tenant: "tenant-a", domain: "counter",
            aggregateId: aggregateIdWhileDown, commandType: "IncrementCounter");
        correlationIdWhileDown.ShouldNotBeNullOrWhiteSpace(
            "EventStore should return a tracking correlationId while domain service is down (AC #5, Task 4.4)");

        // Restart sample domain service
        _ = await _fixture.App.ResourceCommands
            .ExecuteCommandAsync("sample", "resource-start", ct);

        _ = await _fixture.App.ResourceNotifications
            .WaitForResourceHealthyAsync("sample", ct)
            .WaitAsync(s_resourceRecoveryTimeout, ct);

        // Verify command acceptance also works post-restart.
        string aggregateId = $"counter-apicheck-{Guid.NewGuid():N}";
        string correlationId = await ContractTestHelpers.SubmitCommandAndGetCorrelationIdWithRetryAsync(
            client, tenant: "tenant-a", domain: "counter",
            aggregateId: aggregateId, commandType: "IncrementCounter");
        correlationId.ShouldNotBeNullOrWhiteSpace(
            "EventStore should return a tracking correlationId after restart");
    }
}
