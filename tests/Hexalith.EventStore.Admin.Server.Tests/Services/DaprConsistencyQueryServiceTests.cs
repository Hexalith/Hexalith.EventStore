#pragma warning disable CS8620 // Nullability mismatch in NSubstitute Returns() with nullable Dapr client methods

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Consistency;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Admin.Server.Services;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

namespace Hexalith.EventStore.Admin.Server.Tests.Services;

public class DaprConsistencyQueryServiceTests
{
    private const string StateStoreName = "statestore";

    private static DaprConsistencyQueryService CreateService(DaprClient? daprClient = null)
    {
        daprClient ??= Substitute.For<DaprClient>();
        IOptions<AdminServerOptions> options = Options.Create(new AdminServerOptions
        {
            StateStoreName = StateStoreName,
        });

        return new DaprConsistencyQueryService(
            daprClient,
            options,
            NullLogger<DaprConsistencyQueryService>.Instance);
    }

    [Fact]
    public async Task GetChecks_ReadsIndex_ThenFetchesEachCheck()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        List<string> index = ["check-1", "check-2"];
        daprClient.GetStateAsync<List<string>>(
                StateStoreName, "admin:consistency:index", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => index);

        ConsistencyCheckResult check1 = CreateCheckResult("check-1", "tenant-a", ConsistencyCheckStatus.Completed);
        ConsistencyCheckResult check2 = CreateCheckResult("check-2", "tenant-a", ConsistencyCheckStatus.Completed);

        daprClient.GetStateAsync<ConsistencyCheckResult>(
                StateStoreName, "admin:consistency:check-1", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => check1);
        daprClient.GetStateAsync<ConsistencyCheckResult>(
                StateStoreName, "admin:consistency:check-2", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => check2);

        DaprConsistencyQueryService service = CreateService(daprClient);

        IReadOnlyList<ConsistencyCheckSummary> result = await service.GetChecksAsync(null);

        result.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetChecks_ReportsTimedOut_WhenRunningPastTimeout()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        List<string> index = ["check-timeout"];
        daprClient.GetStateAsync<List<string>>(
                StateStoreName, "admin:consistency:index", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => index);

        ConsistencyCheckResult timedOut = new(
            "check-timeout",
            ConsistencyCheckStatus.Running,
            "tenant-a",
            null,
            [ConsistencyCheckType.SequenceContinuity],
            DateTimeOffset.UtcNow.AddHours(-2),
            null,
            DateTimeOffset.UtcNow.AddHours(-1), // TimeoutUtc is in the past
            10,
            0,
            [],
            false,
            null);

        daprClient.GetStateAsync<ConsistencyCheckResult>(
                StateStoreName, "admin:consistency:check-timeout", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => timedOut);

        DaprConsistencyQueryService service = CreateService(daprClient);

        IReadOnlyList<ConsistencyCheckSummary> result = await service.GetChecksAsync(null);

        result.Count.ShouldBe(1);
        result[0].Status.ShouldBe(ConsistencyCheckStatus.Failed);
    }

    [Fact]
    public async Task GetCheckResult_ReturnsNull_WhenKeyMissing()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.GetStateAsync<ConsistencyCheckResult>(
                StateStoreName, "admin:consistency:nonexistent", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (ConsistencyCheckResult?)null);

        DaprConsistencyQueryService service = CreateService(daprClient);

        ConsistencyCheckResult? result = await service.GetCheckResultAsync("nonexistent");

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetChecks_ReturnsEmptyList_WhenIndexMissing()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.GetStateAsync<List<string>>(
                StateStoreName, "admin:consistency:index", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (List<string>?)null);

        DaprConsistencyQueryService service = CreateService(daprClient);

        IReadOnlyList<ConsistencyCheckSummary> result = await service.GetChecksAsync(null);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetChecks_FiltersByTenantId()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        List<string> index = ["check-a", "check-b"];
        daprClient.GetStateAsync<List<string>>(
                StateStoreName, "admin:consistency:index", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => index);

        ConsistencyCheckResult checkA = CreateCheckResult("check-a", "tenant-a", ConsistencyCheckStatus.Completed);
        ConsistencyCheckResult checkB = CreateCheckResult("check-b", "tenant-b", ConsistencyCheckStatus.Completed);

        daprClient.GetStateAsync<ConsistencyCheckResult>(
                StateStoreName, "admin:consistency:check-a", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => checkA);
        daprClient.GetStateAsync<ConsistencyCheckResult>(
                StateStoreName, "admin:consistency:check-b", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => checkB);

        DaprConsistencyQueryService service = CreateService(daprClient);

        IReadOnlyList<ConsistencyCheckSummary> result = await service.GetChecksAsync("tenant-a");

        result.Count.ShouldBe(1);
        result[0].CheckId.ShouldBe("check-a");
    }

    private static ConsistencyCheckResult CreateCheckResult(string checkId, string tenantId, ConsistencyCheckStatus status)
    {
        return new ConsistencyCheckResult(
            checkId,
            status,
            tenantId,
            null,
            [ConsistencyCheckType.SequenceContinuity],
            DateTimeOffset.UtcNow.AddMinutes(-10),
            status == ConsistencyCheckStatus.Completed ? DateTimeOffset.UtcNow : null,
            DateTimeOffset.UtcNow.AddMinutes(20),
            25,
            0,
            [],
            false,
            null);
    }
}
