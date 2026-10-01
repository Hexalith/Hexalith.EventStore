using System.Threading.Channels;

using Hexalith.EventStore.Client.Reminders;
using Hexalith.EventStore.Contracts.Effects;
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

    /// <summary>A completed scan keeps unresolved work registered after its discovery snapshot while pruning old records.</summary>
    [Fact]
    public async Task CompletedPassRetainsWorkRecordedDuringDiscoverySnapshot()
    {
        var harness = new ReminderTestHarness();
        ReminderIntent existing = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(1));
        harness.Source.Set(Item, existing);
        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
        harness.Status.RecordItem("wra-no-longer-discovered", 1, 0);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        IReminderRegistrar registrar = Substitute.For<IReminderRegistrar>();
        registrar.ConvergeAsync(Item, Arg.Any<CancellationToken>()).Returns(async call =>
        {
            entered.SetResult();
            await resume.Task.WaitAsync(call.Arg<CancellationToken>());
            return await harness.CreateRegistrar().ConvergeAsync(Item, call.Arg<CancellationToken>());
        });
        using var reconciler = new ReminderReconciler(harness.CreateIndex(), registrar, harness.Status,
            Options.Create(harness.Options), harness.Time, NullLogger<ReminderReconciler>.Instance);
        Task<ReminderReconciliationPass> running = reconciler.RunPassAsync(TestContext.Current.CancellationToken);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        ReminderTarget added = ReminderTestHarness.Target("item-added");
        string actorId = ReminderTestHarness.ActorId(added);
        ReminderIntent pending = ReminderTestHarness.Intent(added, harness.Time.Now.AddHours(2));
        harness.Source.Set(added, pending);
        harness.SchedulerFor(actorId).ArmFailure = new InvalidOperationException("Synthetic Scheduler outage.");
        try
        {
            (await harness.CreateRegistrar().ConvergeAsync(added)).Unresolved.ShouldBe(1);
        }
        finally
        {
            resume.SetResult();
        }

        ReminderReconciliationPass pass = await running;

        pass.Incomplete.ShouldBe(0);
        pass.Candidates.ShouldBe(1);
        ReminderEntry witness = harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem();
        witness.ReminderName.ShouldBe(ReminderTestHarness.Name(pending));
        witness.Status.ShouldBe(ReminderEntryStatus.Pending);
        harness.Candidates().Count.ShouldBe(2);
        harness.Candidates().ShouldContain(new ReminderCandidate(added.Domain, added.Aggregate, actorId));
        harness.Status.Snapshot().UnresolvedItems.ShouldBe(1);
        harness.Status.Snapshot().Unresolved.ShouldBe(1);
        (await CheckAsync(harness)).Status.ShouldBe(HealthStatus.Degraded);
    }

    /// <summary>A full or over-capacity restored index keeps scanning and lets caller redelivery recover rejected work.</summary>
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public async Task FullTenantIndexDegradesReadinessAndStillConvergesCandidates(int retainedCount)
    {
        var harness = new ReminderTestHarness();
        harness.Options.MaxCandidatesPerTenant = retainedCount;
        ReminderTarget[] retained = Enumerable.Range(1, retainedCount)
            .Select(number => ReminderTestHarness.Target($"item-{number}"))
            .ToArray();
        foreach (ReminderTarget target in retained)
        {
            ReminderIntent intent = ReminderTestHarness.Intent(target, harness.Time.Now.AddHours(1));
            harness.Source.Set(target, intent);
            _ = await harness.CreateRegistrar().ConvergeAsync(target);
            harness.SchedulerFor(ReminderTestHarness.ActorId(target)).Lose(ReminderTestHarness.Name(intent));
        }

        ReminderTenantCandidates restored = harness.Store.Snapshot<ReminderTenantCandidates>(
            harness.Options.StateStoreName,
            ReminderStateKeys.TenantCandidates(harness.Options.ActorTypeName, ReminderTestHarness.Tenant)).ShouldNotBeNull();
        harness.Options.MaxCandidatesPerTenant = 2;
        harness.Store.SeedRaw(harness.Options.StateStoreName,
            ReminderStateKeys.TenantCandidates(harness.Options.ActorTypeName, ReminderTestHarness.Tenant), restored);
        ReminderTarget rejected = ReminderTestHarness.Target("item-rejected");
        string rejectedActorId = ReminderTestHarness.ActorId(rejected);
        harness.Source.Set(rejected, ReminderTestHarness.Intent(rejected, harness.Time.Now.AddHours(1)));
        harness.Status.CompletePass(harness.Time.Now, 0, retained.Select(ReminderTestHarness.ActorId).ToArray(), harness.Status.BeginPass());
        (await CheckAsync(harness)).Status.ShouldBe(HealthStatus.Healthy);
        ReminderFailClosedException failure = await Should.ThrowAsync<ReminderFailClosedException>(
            () => harness.CreateRegistrar().ConvergeAsync(rejected));
        failure.ReasonCode.ShouldBe("index-capacity");

        ReminderReconciliationPass pass = await harness.CreateReconciler().RunPassAsync(CancellationToken.None);

        pass.ShouldBe(new ReminderReconciliationPass(1, retainedCount, retainedCount, 0, 0, 0, 0, 1, 1));
        harness.ItemState(rejectedActorId).ShouldBeNull();
        harness.SchedulerFor(rejectedActorId).ArmCalls.ShouldBe(0);
        harness.Candidates().Count.ShouldBe(retainedCount);
        foreach (ReminderTarget target in retained)
        {
            string actorId = ReminderTestHarness.ActorId(target);
            harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem().Status.ShouldBe(ReminderEntryStatus.Armed);
            harness.SchedulerFor(actorId).ArmCalls.ShouldBe(2);
            harness.Source.Set(target);
        }

        harness.Submitter.Calls.ShouldBeEmpty();
        HealthCheckResult degraded = await CheckAsync(harness);
        degraded.Status.ShouldBe(HealthStatus.Degraded);
        degraded.Data["incompleteScans"].ShouldBe(1);

        ReminderReconciliationPass drained = await harness.CreateReconciler().RunPassAsync(CancellationToken.None);
        drained.Cancelled.ShouldBe(retainedCount);
        harness.Candidates().ShouldBeEmpty();
        ReminderReconciliationPass recovered = await harness.CreateReconciler().RunPassAsync(CancellationToken.None);
        recovered.Incomplete.ShouldBe(0);
        (await CheckAsync(harness)).Status.ShouldBe(HealthStatus.Healthy);

        // The rejected item is still undiscoverable until the caller redelivers its failed convergence.
        harness.ItemState(rejectedActorId).ShouldBeNull();
        harness.Submitter.Calls.ShouldBeEmpty();
        harness.Time.Advance(TimeSpan.FromHours(2));
        ReminderConvergenceResult redelivered = await harness.CreateRegistrar().ConvergeAsync(rejected);

        redelivered.ShouldBe(new ReminderConvergenceResult(0, 1, 0, 0, 0));
        string expectedEffectId = EffectIdentityCodec.ComputeEffectId(new EffectIdentity(
            ReminderTestHarness.Tenant, rejected.Domain, rejected.Aggregate, 3,
            EffectKindCatalog.DateResume, rejected.Domain, rejected.Aggregate, 0));
        TrustedEffectResult receipt = harness.Submitter.Receipts.ShouldHaveSingleItem().Value;
        receipt.EffectId.ShouldBe(expectedEffectId);
        receipt.Disposition.ShouldBe(TrustedEffectDisposition.Success);
        harness.ItemState(rejectedActorId).ShouldBeNull();
        harness.Candidates().ShouldHaveSingleItem().ActorId.ShouldBe(rejectedActorId);
        harness.Status.Snapshot().Quarantined.ShouldBe(0);
    }

    /// <summary>Present discovery documents with null lists keep persisted work unresolved instead of pruning readiness.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task NullDiscoveryCollectionRetainsRecordedItemsAndDegradesReadiness(bool registry)
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        harness.Source.Set(Item, ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(1)));
        harness.SchedulerFor(actorId).ArmFailure = new HttpRequestException("Synthetic scheduler outage.");
        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
        if (registry)
        {
            harness.Store.SeedRaw(harness.Options.StateStoreName,
                ReminderStateKeys.TenantRegistry(harness.Options.ActorTypeName), new ReminderTenantRegistry(null!));
        }
        else
        {
            harness.Store.SeedRaw(harness.Options.StateStoreName,
                ReminderStateKeys.TenantCandidates(harness.Options.ActorTypeName, ReminderTestHarness.Tenant),
                new ReminderTenantCandidates(ReminderTestHarness.Tenant, null!));
        }

        string malformedKey = registry
            ? ReminderStateKeys.TenantRegistry(harness.Options.ActorTypeName)
            : ReminderStateKeys.TenantCandidates(harness.Options.ActorTypeName, ReminderTestHarness.Tenant);
        (bool Present, string Etag) before = await harness.Store.TryReadEtagAsync(harness.Options.StateStoreName, malformedKey);
        ReminderTarget another = ReminderTestHarness.Target("item-2", registry ? "tenant-b" : ReminderTestHarness.Tenant);
        harness.Source.Set(another, ReminderTestHarness.Intent(another, harness.Time.Now.AddHours(1)));
        ReminderFailClosedException failure = await Should.ThrowAsync<ReminderFailClosedException>(
            () => harness.CreateRegistrar().ConvergeAsync(another));
        failure.ReasonCode.ShouldBe(registry ? "index-registry-invalid" : "index-candidates-invalid");
        harness.ItemState(ReminderTestHarness.ActorId(another)).ShouldBeNull();
        harness.SchedulerFor(ReminderTestHarness.ActorId(another)).ArmCalls.ShouldBe(0);

        ReminderReconciliationPass pass = await harness.CreateReconciler().RunPassAsync(CancellationToken.None);

        pass.ShouldBe(new ReminderReconciliationPass(registry ? 0 : 1, 0, 0, 0, 0, 0, 0, 1));
        (await harness.Store.TryReadEtagAsync(harness.Options.StateStoreName, malformedKey)).ShouldBe(before);
        harness.Status.Snapshot().UnresolvedItems.ShouldBe(1);
        harness.Status.Snapshot().Unresolved.ShouldBe(1);
        harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem().Status.ShouldBe(ReminderEntryStatus.Pending);
        (await CheckAsync(harness)).Status.ShouldBe(HealthStatus.Degraded);
        harness.Source.Reads.ShouldBe(2);
        if (registry)
        {
            harness.Store.Snapshot<ReminderTenantRegistry>(harness.Options.StateStoreName,
                ReminderStateKeys.TenantRegistry(harness.Options.ActorTypeName)).ShouldNotBeNull().Tenants.ShouldBeNull();
            harness.Candidates().ShouldHaveSingleItem().ActorId.ShouldBe(actorId);
            harness.Store.SeedRaw(harness.Options.StateStoreName,
                ReminderStateKeys.TenantRegistry(harness.Options.ActorTypeName), new ReminderTenantRegistry([ReminderTestHarness.Tenant]));
        }
        else
        {
            harness.Store.Snapshot<ReminderTenantCandidates>(harness.Options.StateStoreName,
                ReminderStateKeys.TenantCandidates(harness.Options.ActorTypeName, ReminderTestHarness.Tenant))
                .ShouldNotBeNull().Candidates.ShouldBeNull();
            harness.Tenants().ShouldBe([ReminderTestHarness.Tenant]);
            harness.Store.SeedRaw(harness.Options.StateStoreName,
                ReminderStateKeys.TenantCandidates(harness.Options.ActorTypeName, ReminderTestHarness.Tenant),
                new ReminderTenantCandidates(ReminderTestHarness.Tenant, [new ReminderCandidate(Item.Domain, Item.Aggregate, actorId)]));
        }

        harness.SchedulerFor(actorId).ArmFailure = null;
        ReminderReconciliationPass recovered = await harness.CreateReconciler().RunPassAsync(CancellationToken.None);
        recovered.Incomplete.ShouldBe(0);
        recovered.Armed.ShouldBe(1);
        harness.Candidates().ShouldHaveSingleItem().ActorId.ShouldBe(actorId);
        (await CheckAsync(harness)).Status.ShouldBe(HealthStatus.Healthy);
    }

    /// <summary>An unreadable registry keeps recorded work and discovery intact while reporting an incomplete pass.</summary>
    [Fact]
    public async Task UnreadableTenantRegistryRetainsRecordedItemsAndDegradesReadiness()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        harness.Source.Set(Item, ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(1)));
        harness.SchedulerFor(actorId).ArmFailure = new HttpRequestException("Synthetic scheduler outage.");
        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
        harness.Status.Snapshot().Unresolved.ShouldBe(1);
        harness.Store.SeedRaw(
            harness.Options.StateStoreName,
            ReminderStateKeys.TenantRegistry(harness.Options.ActorTypeName),
            "unreadable registry");

        ReminderReconciliationPass pass = await harness.CreateReconciler().RunPassAsync(CancellationToken.None);

        pass.ShouldBe(new ReminderReconciliationPass(0, 0, 0, 0, 0, 0, 0, 1));
        harness.Status.Snapshot().UnresolvedItems.ShouldBe(1);
        harness.Status.Snapshot().Unresolved.ShouldBe(1);
        harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem().Status.ShouldBe(ReminderEntryStatus.Pending);
        harness.Candidates().ShouldHaveSingleItem().ActorId.ShouldBe(actorId);
        HealthCheckResult degraded = await CheckAsync(harness);
        degraded.Status.ShouldBe(HealthStatus.Degraded);
        degraded.Data["incompleteScans"].ShouldBe(1);
        harness.Source.Reads.ShouldBe(1);
        harness.Submitter.Calls.ShouldBeEmpty();
    }

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
        harness.Candidates().ShouldHaveSingleItem();
        (await CheckAsync(harness)).Status.ShouldBe(HealthStatus.Healthy);
    }

    /// <summary>An unavailable cleanup fold retains discovery and degrades readiness until a later pass succeeds.</summary>
    [Theory]
    [InlineData("callback", false)]
    [InlineData("callback", true)]
    [InlineData("convergence", false)]
    [InlineData("convergence", true)]
    [InlineData("stale-callback", false)]
    [InlineData("stale-callback", true)]
    public async Task UnavailableCleanupFoldRetainsUnresolvedReadiness(string operation, bool throws)
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(1));
        string name = ReminderTestHarness.Name(intent);
        harness.Source.Set(Item, intent);
        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
        harness.Status.CompletePass(harness.Time.Now, 0, [actorId], harness.Status.BeginPass());
        (await CheckAsync(harness)).Status.ShouldBe(HealthStatus.Healthy);
        harness.Time.Advance(TimeSpan.FromHours(1));
        if (operation == "stale-callback")
        {
            harness.Source.Set(Item);
        }

        int cleanupRead = harness.Source.Reads + 2;
        harness.Source.OnRead = reads =>
        {
            if (reads == cleanupRead)
            {
                if (throws)
                {
                    _ = harness.Source.Failing.Add(Item);
                }
                else
                {
                    harness.Source.ReturnNull = true;
                }
            }
        };

        if (operation == "convergence")
        {
            ReminderConvergenceResult result = await harness.CreateRegistrar().ConvergeAsync(Item);
            result.Submitted.ShouldBe(1);
            result.Unresolved.ShouldBe(1);
        }
        else
        {
            (await harness.FireAsync(actorId, name)).ShouldBe(operation == "stale-callback"
                ? ReminderDisposition.Stale
                : ReminderDisposition.Submitted);
        }

        harness.ItemState(actorId).ShouldBeNull();
        harness.SchedulerFor(actorId).Armed.ShouldBeEmpty();
        harness.Candidates().ShouldHaveSingleItem().ActorId.ShouldBe(actorId);
        harness.Submitter.Receipts.Count.ShouldBe(operation == "stale-callback" ? 0 : 1);
        harness.Status.Snapshot().Unresolved.ShouldBe(1);
        HealthCheckResult retained = await CheckAsync(harness);
        retained.Status.ShouldBe(HealthStatus.Degraded);
        retained.Data["unresolved"].ShouldBe(1);

        harness.Source.OnRead = null;
        harness.Source.ReturnNull = false;
        harness.Source.Failing.Clear();
        harness.Source.Set(Item);
        ReminderReconciliationPass recovered = await harness.CreateReconciler().RunPassAsync(CancellationToken.None);

        recovered.Unresolved.ShouldBe(0);
        recovered.Incomplete.ShouldBe(0);
        harness.Candidates().ShouldBeEmpty();
        harness.Status.Snapshot().Unresolved.ShouldBe(0);
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
        // The fake source still reports both intents, so the recovered item submits and the healthy item submits again.
        complete.Submitted.ShouldBe(2);
        harness.Candidates("tenant-b").ShouldHaveSingleItem();
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
        ReminderDispositionRecord audit = harness.Disposition(actorId, ReminderTestHarness.Name(intent)).ShouldNotBeNull();
        audit.Disposition.ShouldBe(ReminderDisposition.Quarantined);
        audit.ReasonCode.ShouldBe("witness-collision");
    }

    /// <summary>With reconciliation disabled the hosted service never completes a pass, so readiness stays Degraded.</summary>
    [Fact]
    public async Task DisabledReconciliationNeverCompletesAPass()
    {
        var harness = new ReminderTestHarness();
        harness.Options.ReconciliationEnabled = false;
        using ReminderReconciler reconciler = harness.CreateReconciler();

        await reconciler.StartAsync(CancellationToken.None);
        await reconciler.ExecuteTask.ShouldNotBeNull().WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        await reconciler.StopAsync(CancellationToken.None);

        harness.Status.Snapshot().PassCompleted.ShouldBeFalse();
        harness.Source.Reads.ShouldBe(0);
        (await CheckAsync(harness)).Status.ShouldBe(HealthStatus.Degraded);
    }

    /// <summary>Absent discovery documents are an empty complete scan that makes a fresh host ready.</summary>
    [Fact]
    public async Task EmptyDiscoveryIndexCompletesPass()
    {
        var harness = new ReminderTestHarness();

        ReminderReconciliationPass pass = await harness.CreateReconciler().RunPassAsync(CancellationToken.None);

        pass.ShouldBe(new ReminderReconciliationPass(0, 0, 0, 0, 0, 0, 0, 0));
        (await CheckAsync(harness)).Status.ShouldBe(HealthStatus.Healthy);
        harness.Store.Count.ShouldBe(0);
        harness.Source.Reads.ShouldBe(0);
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

    /// <summary>Scan failures use the effective minimum delay; capacity and retained null folds keep the normal cadence.</summary>
    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(true, false, false, false)]
    [InlineData(true, true, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(true, false, true, false)]
    [InlineData(false, true, false, true)]
    public async Task HostedPassUsesEffectiveReconciliationDelay(bool fullIndex, bool sourceUnavailable, bool nullFold, bool longerRetryDelay)
    {
        var harness = new ReminderTestHarness();
        harness.Options.RetryInitialDelay = longerRetryDelay ? TimeSpan.FromHours(2) : TimeSpan.FromMilliseconds(20);
        harness.Options.RetryMaxDelay = longerRetryDelay ? TimeSpan.FromHours(3) : harness.Options.RetryMaxDelay;
        harness.Options.ReconciliationInterval = TimeSpan.FromHours(1);
        harness.Options.MaxCandidatesPerTenant = fullIndex ? 1 : 10_000;
        harness.Options.Validate().ShouldBeEmpty();
        harness.Source.Set(Item, ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(4)));
        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
        if (sourceUnavailable)
        {
            harness.Source.Failing.Add(Item);
        }

        harness.Source.ReturnNull = nullFold;
        int expectedReads = harness.Source.Reads + 1;
        int expectedIncomplete = (fullIndex ? 1 : 0) + (sourceUnavailable ? 1 : 0);
        int expectedUnresolved = nullFold ? 1 : 0;
        HealthStatus expectedHealth = expectedIncomplete > 0 || nullFold ? HealthStatus.Degraded : HealthStatus.Healthy;
        TimeSpan expectedDelay = sourceUnavailable && !longerRetryDelay ? harness.Options.RetryInitialDelay : harness.Options.ReconciliationInterval;
        var timers = Channel.CreateUnbounded<(TimeSpan DueTime, TimeSpan Period, Action Fire)>();
        TimeProvider time = Substitute.For<TimeProvider>();
        time.GetUtcNow().Returns(_ => harness.Time.Now);
        time.CreateTimer(Arg.Any<TimerCallback>(), Arg.Any<object?>(), Arg.Any<TimeSpan>(), Arg.Any<TimeSpan>())
            .Returns(call =>
        {
            TimerCallback callback = call.ArgAt<TimerCallback>(0);
            object? state = call.ArgAt<object?>(1);
            _ = timers.Writer.TryWrite((call.ArgAt<TimeSpan>(2), call.ArgAt<TimeSpan>(3), () => callback(state)));
            return Substitute.For<ITimer>();
        });
        using ReminderReconciler reconciler = harness.CreateReconciler(timeProvider: time);

        await reconciler.StartAsync(CancellationToken.None);
        try
        {
            var first = await timers.Reader.ReadAsync(TestContext.Current.CancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(2));
            first.DueTime.ShouldBe(expectedDelay);
            first.Period.ShouldBe(Timeout.InfiniteTimeSpan);
            harness.Status.Snapshot().PassCompleted.ShouldBeTrue();
            harness.Status.Snapshot().IncompleteScans.ShouldBe(expectedIncomplete);
            harness.Status.Snapshot().Unresolved.ShouldBe(expectedUnresolved);
            (await CheckAsync(harness)).Status.ShouldBe(expectedHealth);
            harness.Source.Reads.ShouldBe(expectedReads);
            timers.Reader.TryRead(out _).ShouldBeFalse();

            harness.Time.Advance(first.DueTime);
            first.Fire();
            var second = await timers.Reader.ReadAsync(TestContext.Current.CancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(2));
            second.DueTime.ShouldBe(expectedDelay);
            harness.Source.Reads.ShouldBe(expectedReads + 1);
            harness.Status.Snapshot().LastPassAt.ShouldBe(harness.Time.Now);
            harness.Status.Snapshot().IncompleteScans.ShouldBe(expectedIncomplete);
            harness.Status.Snapshot().Unresolved.ShouldBe(expectedUnresolved);
            (await CheckAsync(harness)).Status.ShouldBe(expectedHealth);
            if (nullFold)
            {
                harness.ItemState(ReminderTestHarness.ActorId(Item)).ShouldNotBeNull().Entries.ShouldHaveSingleItem().Status.ShouldBe(ReminderEntryStatus.Armed);
                harness.Candidates().ShouldHaveSingleItem().ActorId.ShouldBe(ReminderTestHarness.ActorId(Item));
                harness.Submitter.Calls.ShouldBeEmpty();
            }
        }
        finally
        {
            await reconciler.StopAsync(CancellationToken.None);
        }
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
        string? token = "__HEXALITH_REMINDER_CALLBACK_TOKEN__")
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
