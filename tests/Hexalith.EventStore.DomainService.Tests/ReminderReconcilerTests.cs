using Hexalith.EventStore.Client.Reminders;
using Hexalith.EventStore.Contracts.Reminders;
using Hexalith.EventStore.DomainService.Tests.Fixtures;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>
/// Recovery row of the Story 4.11 matrix plus readiness: periodic passes re-fold discovered streams, reissue
/// due work, re-arm future work, and keep readiness Degraded while anything stays unresolved.
/// </summary>
public sealed class ReminderReconcilerTests
{
    private static readonly ReminderTarget Item = ReminderTestHarness.Target("item-1");

    /// <summary>A firing lost by the scheduler is reissued from the stream once it is due.</summary>
    [Fact]
    public async Task LostFiringIsReissuedWhenDue()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(1));
        harness.Source.Set(Item, intent);
        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
        harness.SchedulerFor(actorId).Lose(ReminderTestHarness.Name(intent));
        harness.Time.Advance(TimeSpan.FromHours(2));

        ReminderReconciliationPass pass = await harness.CreateReconciler().RunPassAsync(CancellationToken.None);

        pass.ShouldBe(new ReminderReconciliationPass(1, 1, 0, 1, 0, 0, 0, 0));
        harness.Submitter.Receipts.Count.ShouldBe(1);
        harness.Disposition(actorId, ReminderTestHarness.Name(intent)).ShouldNotBeNull().Disposition.ShouldBe(ReminderDisposition.Submitted);
        harness.ItemState(actorId).ShouldBeNull();
        harness.Candidates().ShouldBeEmpty();
        (await CheckAsync(harness)).Status.ShouldBe(HealthStatus.Healthy);
    }

    /// <summary>A scheduler reminder deleted before it fires is re-armed for its remaining time.</summary>
    [Fact]
    public async Task DeletedSchedulerReminderIsRearmed()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(4));
        string name = ReminderTestHarness.Name(intent);
        harness.Source.Set(Item, intent);
        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
        harness.SchedulerFor(actorId).Lose(name);
        harness.Time.Advance(TimeSpan.FromHours(1));

        ReminderReconciliationPass pass = await harness.CreateReconciler().RunPassAsync(CancellationToken.None);

        pass.Armed.ShouldBe(1);
        harness.SchedulerFor(actorId).Armed[name].DueTime.ShouldBe(TimeSpan.FromHours(3));
        harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem().Status.ShouldBe(ReminderEntryStatus.Armed);
        harness.Submitter.Calls.ShouldBeEmpty();
    }

    /// <summary>One unreadable stream leaves the pass incomplete and readiness Degraded while every other tenant converges.</summary>
    [Fact]
    public async Task PartialScanDegradesReadinessAndRetainsWork()
    {
        var harness = new ReminderTestHarness();
        ReminderTarget healthy = ReminderTestHarness.Target("item-1", "tenant-a");
        ReminderTarget broken = ReminderTestHarness.Target("item-2", "tenant-b");
        harness.Source.Set(healthy, ReminderTestHarness.Intent(healthy, harness.Time.Now.AddHours(1)));
        harness.Source.Set(broken, ReminderTestHarness.Intent(broken, harness.Time.Now.AddHours(1)));
        ReminderRegistrar registrar = harness.CreateRegistrar();
        _ = await registrar.ConvergeAsync(healthy);
        _ = await registrar.ConvergeAsync(broken);
        harness.Source.Failing.Add(broken);
        harness.Time.Advance(TimeSpan.FromHours(2));

        ReminderReconciliationPass partial = await harness.CreateReconciler().RunPassAsync(CancellationToken.None);

        partial.Tenants.ShouldBe(2);
        partial.Submitted.ShouldBe(1);
        partial.Incomplete.ShouldBe(1);
        harness.ItemState(ReminderTestHarness.ActorId(healthy)).ShouldBeNull();
        harness.ItemState(ReminderTestHarness.ActorId(broken)).ShouldNotBeNull().Entries.ShouldHaveSingleItem();
        harness.Candidates("tenant-b").ShouldHaveSingleItem();
        HealthCheckResult degraded = await CheckAsync(harness);
        degraded.Status.ShouldBe(HealthStatus.Degraded);
        degraded.Data["incompleteScans"].ShouldBe(1);

        harness.Source.Failing.Clear();
        ReminderReconciliationPass complete = await harness.CreateReconciler().RunPassAsync(CancellationToken.None);

        complete.Incomplete.ShouldBe(0);
        complete.Submitted.ShouldBe(1);
        harness.Candidates("tenant-b").ShouldBeEmpty();
        (await CheckAsync(harness)).Status.ShouldBe(HealthStatus.Healthy);
    }

    /// <summary>A corrupt tenant index that names another tenant never crosses tenants; the pass stays incomplete.</summary>
    [Fact]
    public async Task CorruptTenantIndexIsNotCrossed()
    {
        var harness = new ReminderTestHarness();
        ReminderTarget item = ReminderTestHarness.Target("item-1", "tenant-b");
        harness.Source.Set(item, ReminderTestHarness.Intent(item, harness.Time.Now.AddHours(1)));
        _ = await harness.CreateRegistrar().ConvergeAsync(item);
        harness.Store.SeedRaw(
            harness.Options.StateStoreName,
            ReminderStateKeys.TenantCandidates(harness.Options.ActorTypeName, "tenant-b"),
            new ReminderTenantCandidates("tenant-c", [new ReminderCandidate("widget", "item-9", "wra-X")]));

        ReminderReconciliationPass pass = await harness.CreateReconciler().RunPassAsync(CancellationToken.None);

        pass.Incomplete.ShouldBe(1);
        pass.Candidates.ShouldBe(0);
        harness.Source.Reads.ShouldBe(1);
    }

    /// <summary>Null and actor-mismatched candidates are skipped independently without invoking their registrar.</summary>
    [Fact]
    public async Task CorruptCandidatesDoNotInvokeRegistrarAndPassContinues()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        harness.Store.SeedRaw(
            harness.Options.StateStoreName,
            ReminderStateKeys.TenantRegistry(harness.Options.ActorTypeName),
            new ReminderTenantRegistry([ReminderTestHarness.Tenant]));
        harness.Store.SeedRaw(
            harness.Options.StateStoreName,
            ReminderStateKeys.TenantCandidates(harness.Options.ActorTypeName, ReminderTestHarness.Tenant),
            new ReminderTenantCandidates(ReminderTestHarness.Tenant, [
                null!,
                new ReminderCandidate(Item.Domain, Item.Aggregate, "wra-MISMATCH"),
                new ReminderCandidate(Item.Domain, Item.Aggregate, actorId),
            ]));
        IReminderRegistrar registrar = Substitute.For<IReminderRegistrar>();
        registrar.ConvergeAsync(Item, Arg.Any<CancellationToken>())
            .Returns(new ReminderConvergenceResult(1, 0, 0, 0, 0));
        using var reconciler = new ReminderReconciler(
            harness.CreateIndex(),
            registrar,
            harness.Status,
            Options.Create(harness.Options),
            harness.Time,
            NullLogger<ReminderReconciler>.Instance);

        ReminderReconciliationPass pass = await reconciler.RunPassAsync(CancellationToken.None);

        pass.ShouldBe(new ReminderReconciliationPass(1, 3, 1, 0, 0, 0, 0, 2));
        _ = registrar.Received(1).ConvergeAsync(Item, Arg.Any<CancellationToken>());
        registrar.ReceivedCalls().Count().ShouldBe(1);
    }

    /// <summary>After a restart readiness stays Degraded until the first pass rebuilds it from durable state.</summary>
    [Fact]
    public async Task RestartRebuildsReadinessFromDurableState()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        harness.Source.Set(Item, ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(1)));
        harness.Submitter.FailuresRemaining = 1;
        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
        harness.Time.Advance(TimeSpan.FromHours(1));

        var restarted = new ReminderRuntimeStatus();
        (await CheckAsync(harness, restarted)).Description.ShouldBe("No reminder reconciliation pass has completed yet.");

        ReminderReconciliationPass retained = await harness.CreateReconciler(restarted).RunPassAsync(CancellationToken.None);

        retained.Unresolved.ShouldBe(1);
        harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem().Status.ShouldBe(ReminderEntryStatus.Retrying);
        HealthCheckResult degraded = await CheckAsync(harness, restarted);
        degraded.Status.ShouldBe(HealthStatus.Degraded);
        degraded.Data["unresolved"].ShouldBe(1);

        // The retained witness is resubmitted only once its backoff window has elapsed.
        harness.Time.Advance(TimeSpan.FromSeconds(31));
        ReminderReconciliationPass resolved = await harness.CreateReconciler(restarted).RunPassAsync(CancellationToken.None);

        resolved.Submitted.ShouldBe(1);
        (await CheckAsync(harness, restarted)).Status.ShouldBe(HealthStatus.Healthy);
    }

    /// <summary>Quarantine keeps readiness Degraded; a missing app-channel token outside Development makes it Unhealthy.</summary>
    [Fact]
    public async Task QuarantineDegradesAndMissingTokenFailsReadiness()
    {
        var harness = new ReminderTestHarness();
        harness.Source.Set(Item, ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(1)) with { ScheduleRevision = -1 });
        _ = await harness.CreateReconciler().RunPassAsync(CancellationToken.None);
        _ = await harness.CreateRegistrar().ConvergeAsync(Item);

        HealthCheckResult quarantined = await CheckAsync(harness);
        HealthCheckResult unconfigured = await CheckAsync(harness, harness.Status, Environments.Production, token: null);

        quarantined.Status.ShouldBe(HealthStatus.Degraded);
        quarantined.Data["quarantined"].ShouldBe(1);
        unconfigured.Status.ShouldBe(HealthStatus.Unhealthy);
        unconfigured.Data["callbackTokenConfigured"].ShouldBe(false);
    }

    /// <summary>A pass that finds a registered witness now carrying different evidence quarantines it and submits nothing.</summary>
    [Fact]
    public async Task WitnessCollisionOnConvergencePathIsQuarantined()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(1), sequence: 3);
        harness.Source.Set(Item, intent);
        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
        harness.Source.Set(Item, intent with { SourceSequence = 9 });
        harness.Time.Advance(TimeSpan.FromHours(2));

        ReminderReconciliationPass pass = await harness.CreateReconciler().RunPassAsync(CancellationToken.None);

        pass.Submitted.ShouldBe(0);
        pass.Quarantined.ShouldBe(2);
        harness.Submitter.Calls.ShouldBeEmpty();
        ReminderItemState state = harness.ItemState(actorId).ShouldNotBeNull();
        ReminderEntry entry = state.Entries.ShouldHaveSingleItem();
        entry.Status.ShouldBe(ReminderEntryStatus.Quarantined);
        entry.LastReasonCode.ShouldBe("witness-collision");
        state.Quarantine.ShouldHaveSingleItem().ReasonCode.ShouldBe("witness-collision");
    }

    /// <summary>With reconciliation disabled the hosted service never completes a pass, so readiness stays Degraded.</summary>
    [Fact]
    public async Task DisabledReconciliationNeverCompletesAPass()
    {
        var harness = new ReminderTestHarness();
        harness.Options.ReconciliationEnabled = false;
        using ReminderReconciler reconciler = harness.CreateReconciler();

        await reconciler.StartAsync(CancellationToken.None);
        await Task.Delay(200, TestContext.Current.CancellationToken);
        await reconciler.StopAsync(CancellationToken.None);

        harness.Status.Snapshot().PassCompleted.ShouldBeFalse();
        (await CheckAsync(harness)).Status.ShouldBe(HealthStatus.Degraded);
    }

    /// <summary>The hosted reconciler runs its first pass at startup without waiting for the interval.</summary>
    [Fact]
    public async Task FirstPassRunsAtStartup()
    {
        var harness = new ReminderTestHarness();
        harness.Options.ReconciliationInterval = TimeSpan.FromHours(1);
        using ReminderReconciler reconciler = harness.CreateReconciler();

        await reconciler.StartAsync(CancellationToken.None);
        for (int attempt = 0; attempt < 100 && !harness.Status.Snapshot().PassCompleted; attempt++)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        await reconciler.StopAsync(CancellationToken.None);

        harness.Status.Snapshot().PassCompleted.ShouldBeTrue();
    }

    /// <summary>An incomplete hosted pass retries on the short retry cadence instead of the normal interval.</summary>
    [Fact]
    public async Task IncompleteHostedPassUsesRetryCadence()
    {
        var harness = new ReminderTestHarness();
        harness.Options.RetryInitialDelay = TimeSpan.FromMilliseconds(20);
        harness.Options.ReconciliationInterval = TimeSpan.FromHours(1);
        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(1));
        harness.Source.Set(Item, intent);
        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
        harness.Source.Failing.Add(Item);
        int expectedReads = harness.Source.Reads + 2;
        var retried = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Source.OnRead = reads =>
        {
            if (reads >= expectedReads)
            {
                _ = retried.TrySetResult();
            }
        };
        using ReminderReconciler reconciler = harness.CreateReconciler(timeProvider: TimeProvider.System);

        await reconciler.StartAsync(CancellationToken.None);
        await retried.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        await reconciler.StopAsync(CancellationToken.None);

        harness.Source.Reads.ShouldBeGreaterThanOrEqualTo(expectedReads);
        harness.Status.Snapshot().IncompleteScans.ShouldBe(1);
    }

    private static Task<HealthCheckResult> CheckAsync(
        ReminderTestHarness harness,
        ReminderRuntimeStatus? status = null,
        string environmentName = "Production",
        string? token = "app-token")
    {
        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [ReminderCallbackTokenFilter.ConfigurationKey] = token })
            .Build();
        var filter = new ReminderCallbackTokenFilter(
            environment,
            configuration,
            Options.Create(harness.Options),
            NullLogger<ReminderCallbackTokenFilter>.Instance);
        return new ReminderReadinessHealthCheck(status ?? harness.Status, filter)
            .CheckHealthAsync(new HealthCheckContext());
    }
}
