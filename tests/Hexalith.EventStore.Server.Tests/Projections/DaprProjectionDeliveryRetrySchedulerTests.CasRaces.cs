using Dapr.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Server.Projections;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public sealed partial class DaprProjectionDeliveryRetrySchedulerTests
{
    [Theory]
    [InlineData("claim")]
    [InlineData("update")]
    [InlineData("delete")]
    public async Task LostLedgerCasFollowedByCompetingRevisionCannotReportAnUnpersistedSuccess(string action)
    {
        var ledgers = new Dictionary<string, ProjectionDeliveryRetryLedger>(StringComparer.Ordinal);
        using DaprClient client = CreateStatefulDaprClient(ledgers);
        DaprProjectionDeliveryRetryScheduler scheduler = CreateScheduler(client);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ProjectionDeliveryRetryWorkItem original = CreateWorkItem("race-work", "race-owner");
        await scheduler.ScheduleAsync(original).ConfigureAwait(true);
        ProjectionDeliveryRetryWorkItem input = action == "claim" ? original
            : (await scheduler.TryAcquireAsync(original, "original-owner", now, TimeSpan.FromMinutes(1)).ConfigureAwait(true))!;
        ProjectionDeliveryRetryWorkItem? winner = null;
        int saves = 0;
        client.TrySaveStateAsync("statestore", Arg.Any<string>(), Arg.Any<ProjectionDeliveryRetryLedger>(), Arg.Any<string>(),
            stateOptions: Arg.Any<StateOptions?>(), metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>()).Returns(call =>
        {
            saves++;
            string key = call.ArgAt<string>(1);
            ProjectionDeliveryRetryWorkItem current = ledgers[key].Items.Single();
            winner = current with { Revision = current.Revision + 1, LeaseOwner = "competing-owner",
                LeaseExpiresUtc = now.AddMinutes(2) };
            ledgers[key] = new([winner]);
            return false;
        });
        using ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope();
        if (action == "claim")
            (await scheduler.TryAcquireAsync(input, "original-owner", now, TimeSpan.FromMinutes(1)).ConfigureAwait(true)).ShouldBeNull();
        else if (action == "update") (await scheduler.TryUpdateAsync(input with { Attempt = 1 }).ConfigureAwait(true)).ShouldBeFalse();
        else (await scheduler.TryDeleteAsync(input).ConfigureAwait(true)).ShouldBeFalse();
        saves.ShouldBe(1);
        ledgers.Values.SelectMany(static ledger => ledger.Items).ShouldHaveSingleItem().ShouldBe(winner);
        winner!.LeaseOwner.ShouldBe("competing-owner");
        winner.DispatchId.ShouldBe(original.DispatchId);
        winner.PendingRoutes.ShouldBe(original.PendingRoutes);
        cost[ProjectionDeliveryCost.SchedulerShardReads].ShouldBe(2);
        cost[ProjectionDeliveryCost.SchedulerWriteAttempts].ShouldBe(1);
    }
}
