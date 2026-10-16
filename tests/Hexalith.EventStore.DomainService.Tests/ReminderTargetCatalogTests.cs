using Hexalith.EventStore.Client.Processes;
using Hexalith.EventStore.Client.Reminders;
using Hexalith.EventStore.Contracts.Reminders;
using Hexalith.EventStore.DomainService.Tests.Fixtures;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>SDK periodic discovery recovers targets whose first convergence wakeup was lost.</summary>
public sealed class ReminderTargetCatalogTests
{
    /// <summary>Healthy new targets converge despite another unavailable scope and a new key before the old cursor.</summary>
    [Fact]
    public async Task PartialCatalogRetainsDegradationAndDiscoversEarlierTargetAfterRestart()
    {
        var harness = new ReminderTestHarness();
        var late = ReminderTestHarness.Target("late", "tenant-z");
        var early = ReminderTestHarness.Target("early", "tenant-b");
        ReminderTarget[] targets = [late];
        harness.Source.Set(late, ReminderTestHarness.Intent(late, harness.Time.Now.AddHours(1)));
        harness.Source.Set(early, ReminderTestHarness.Intent(early, harness.Time.Now.AddHours(1)));
        var catalog = Substitute.For<IReminderTargetCatalog>();
        catalog.ListAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            string? after = call.ArgAt<string?>(0);
            return Task.FromResult(new ProcessSourcePage(targets.Select(target => new ProcessSource(target.Tenant, target.Domain, target.Aggregate))
                .Where(source => after is null || string.CompareOrdinal(source.CatalogKey, after) > 0).ToArray(), null) { IsIncomplete = true });
        });
        using var services = new ServiceCollection().AddSingleton(catalog).BuildServiceProvider();
        ReminderReconciler Reconciler() => new(harness.CreateIndex(), harness.CreateRegistrar(), harness.Status,
            Options.Create(harness.Options), harness.Time, NullLogger<ReminderReconciler>.Instance, services.GetRequiredService<IServiceScopeFactory>());
        (await Reconciler().RunPassAsync(CancellationToken.None)).Incomplete.ShouldBeGreaterThan(0);
        harness.ItemState(ReminderTestHarness.ActorId(late)).ShouldNotBeNull();
        targets = [early, late];
        (await Reconciler().RunPassAsync(CancellationToken.None)).Incomplete.ShouldBeGreaterThan(0);
        harness.ItemState(ReminderTestHarness.ActorId(early)).ShouldNotBeNull();
        var progress = harness.Store.Snapshot<ReminderCatalogDiscoveryState>(harness.Options.StateStoreName,
            ReminderStateKeys.TenantRegistry(harness.Options.ActorTypeName) + ":catalog")!;
        progress.Complete.ShouldBeFalse();
        progress.AfterKey.ShouldBeNull();
        progress.ReasonCode.ShouldBe("catalog-unavailable");
    }

    /// <summary>Failure after a partial page retains the omitted-scope claim through a healthy tail and restart.</summary>
    [Fact]
    public async Task PartialSweepFailureNeedsFreshCompleteSweep()
    {
        var harness = new ReminderTestHarness();
        var first = new ProcessSource("tenant-a", "widget", "first");
        var last = new ProcessSource("tenant-z", "widget", "last");
        bool fail = true;
        bool partial = true;
        var catalog = Substitute.For<IReminderTargetCatalog>();
        catalog.ListAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            string? after = call.ArgAt<string?>(0);
            if (after is null) return Task.FromResult(new ProcessSourcePage([first], first.CatalogKey) { IsIncomplete = partial });
            if (fail) throw new HttpRequestException("later page unavailable");
            return Task.FromResult(new ProcessSourcePage([last], null));
        });
        using var services = new ServiceCollection().AddSingleton(catalog).BuildServiceProvider();
        ReminderReconciler Reconciler(ReminderRuntimeStatus status) => new(harness.CreateIndex(), harness.CreateRegistrar(status), status,
            Options.Create(harness.Options), harness.Time, NullLogger<ReminderReconciler>.Instance, services.GetRequiredService<IServiceScopeFactory>());
        (await Reconciler(harness.Status).RunPassAsync(CancellationToken.None)).Incomplete.ShouldBeGreaterThan(0);
        string key = ReminderStateKeys.TenantRegistry(harness.Options.ActorTypeName) + ":catalog";
        ReminderCatalogDiscoveryState retained = harness.Store.Snapshot<ReminderCatalogDiscoveryState>(harness.Options.StateStoreName, key)!;
        retained.AfterKey.ShouldBe(first.CatalogKey);
        retained.IsIncomplete.ShouldBeTrue();
        fail = false;
        var restarted = new ReminderRuntimeStatus();
        (await Reconciler(restarted).RunPassAsync(CancellationToken.None)).Incomplete.ShouldBeGreaterThan(0);
        retained = harness.Store.Snapshot<ReminderCatalogDiscoveryState>(harness.Options.StateStoreName, key)!;
        retained.Complete.ShouldBeFalse();
        retained.IsIncomplete.ShouldBeTrue();
        retained.AfterKey.ShouldBeNull();
        restarted.Snapshot().IncompleteScans.ShouldBeGreaterThan(0);
        partial = false;
        (await Reconciler(restarted).RunPassAsync(CancellationToken.None)).Incomplete.ShouldBe(0);
        harness.Store.Snapshot<ReminderCatalogDiscoveryState>(harness.Options.StateStoreName, key)!.Complete.ShouldBeTrue();
    }

    /// <summary>No-intent targets cannot occupy the last candidate slot before an active target.</summary>
    [Fact]
    public async Task TerminalBeforeActiveDoesNotLivelockAtCapacityOne()
    {
        var harness = new ReminderTestHarness();
        harness.Options.MaxCandidatesPerTenant = 1;
        var terminal = ReminderTestHarness.Target("a-terminal");
        var active = ReminderTestHarness.Target("z-active");
        harness.Source.Set(active, ReminderTestHarness.Intent(active, harness.Time.Now.AddHours(1)));
        var catalog = Substitute.For<IReminderTargetCatalog>();
        catalog.ListAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(
            new ProcessSourcePage([new(terminal.Tenant, terminal.Domain, terminal.Aggregate), new(active.Tenant, active.Domain, active.Aggregate)], null));
        using var services = new ServiceCollection().AddSingleton(catalog).BuildServiceProvider();
        using var reconciler = new ReminderReconciler(harness.CreateIndex(), harness.CreateRegistrar(), harness.Status,
            Options.Create(harness.Options), harness.Time, NullLogger<ReminderReconciler>.Instance, services.GetRequiredService<IServiceScopeFactory>());
        (await reconciler.RunPassAsync(CancellationToken.None)).Armed.ShouldBe(1);
        harness.ItemState(ReminderTestHarness.ActorId(terminal)).ShouldBeNull();
        harness.Candidates().ShouldHaveSingleItem().Aggregate.ShouldBe(active.Aggregate);
        harness.Time.Advance(TimeSpan.FromHours(2));
        (await reconciler.RunPassAsync(CancellationToken.None)).Submitted.ShouldBe(1);
        harness.Submitter.Receipts.ShouldHaveSingleItem();
    }

    /// <summary>A failed first tenant publication retains failure but cannot starve later healthy tenants across restart.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedTenantPublicationAllowsHealthyLaterTenant(bool capacity)
    {
        var harness = new ReminderTestHarness();
        harness.Options.MaxCandidatesPerTenant = 1;
        var failed = ReminderTestHarness.Target("z-failed");
        var healthy = ReminderTestHarness.Target("healthy", "tenant-z");
        harness.Source.Set(failed, ReminderTestHarness.Intent(failed, harness.Time.Now.AddHours(1)));
        harness.Source.Set(healthy, ReminderTestHarness.Intent(healthy, harness.Time.Now.AddHours(1)));
        if (capacity)
        {
            var retained = ReminderTestHarness.Target("a-retained");
            harness.Source.Set(retained, ReminderTestHarness.Intent(retained, harness.Time.Now.AddHours(1)));
            await harness.CreateRegistrar().ConvergeAsync(retained);
        }
        else harness.Source.Failing.Add(failed);
        var catalog = Substitute.For<IReminderTargetCatalog>();
        catalog.ListAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(
            new ProcessSourcePage([new(failed.Tenant, failed.Domain, failed.Aggregate), new(healthy.Tenant, healthy.Domain, healthy.Aggregate)], null));
        using var services = new ServiceCollection().AddSingleton(catalog).BuildServiceProvider();
        for (int pass = 0; pass < 3; pass++)
        {
            var status = new ReminderRuntimeStatus();
            using var reconciler = new ReminderReconciler(harness.CreateIndex(), harness.CreateRegistrar(status), status,
                Options.Create(harness.Options), harness.Time, NullLogger<ReminderReconciler>.Instance, services.GetRequiredService<IServiceScopeFactory>());
            (await reconciler.RunPassAsync(CancellationToken.None)).Incomplete.ShouldBeGreaterThan(0);
            harness.ItemState(ReminderTestHarness.ActorId(healthy)).ShouldNotBeNull();
            harness.Store.Snapshot<ReminderCatalogDiscoveryState>(harness.Options.StateStoreName,
                ReminderStateKeys.TenantRegistry(harness.Options.ActorTypeName) + ":catalog")!.IsIncomplete.ShouldBeTrue();
            status.Snapshot().IncompleteScans.ShouldBeGreaterThan(0);
        }
    }

    /// <summary>Already indexed due work precedes blocked discovery, with pending health and finite waits even for noncooperative reads.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IndexedDueWorkPrecedesBoundedDiscoveryAndPendingReadiness(bool nonCooperative)
    {
        var harness = new ReminderTestHarness();
        harness.Options.CatalogOperationTimeout = TimeSpan.FromMilliseconds(100);
        harness.Options.CatalogServiceBudget = TimeSpan.FromMilliseconds(200);
        var target = ReminderTestHarness.Target("due");
        harness.Source.Set(target, ReminderTestHarness.Intent(target, harness.Time.Now.AddHours(1)));
        await harness.CreateRegistrar().ConvergeAsync(target);
        harness.Status.CompletePass(harness.Time.Now, 0, [ReminderTestHarness.ActorId(target)], harness.Status.BeginPass());
        harness.Time.Advance(TimeSpan.FromHours(2));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var catalog = Substitute.For<IReminderTargetCatalog>();
        catalog.ListAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            entered.TrySetResult();
            if (nonCooperative) await release.Task;
            else await Task.Delay(Timeout.InfiniteTimeSpan, call.Arg<CancellationToken>());
            return new ProcessSourcePage([], null);
        });
        using var services = new ServiceCollection().AddSingleton(catalog).BuildServiceProvider();
        using var reconciler = new ReminderReconciler(harness.CreateIndex(), harness.CreateRegistrar(), harness.Status,
            Options.Create(harness.Options), harness.Time, NullLogger<ReminderReconciler>.Instance, services.GetRequiredService<IServiceScopeFactory>());
        try
        {
            Task<ReminderReconciliationPass> running = reconciler.RunPassAsync(CancellationToken.None);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            harness.Submitter.Receipts.ShouldHaveSingleItem();
            harness.Status.Snapshot().PassPending.ShouldBeTrue();
            harness.Status.Snapshot().IncompleteScans.ShouldBeGreaterThan(0);
            var environment = Substitute.For<Microsoft.Extensions.Hosting.IHostEnvironment>();
            environment.EnvironmentName.Returns("Development");
            var filter = new ReminderCallbackTokenFilter(environment, new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(),
                Options.Create(harness.Options), NullLogger<ReminderCallbackTokenFilter>.Instance);
            var health = new ReminderReadinessHealthCheck(harness.Status, filter);
            (await health.CheckHealthAsync(new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext())).Status
                .ShouldBe(Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded);
            (await running.WaitAsync(TimeSpan.FromSeconds(3))).Incomplete.ShouldBeGreaterThan(0);
            harness.Store.Snapshot<ReminderCatalogDiscoveryState>(harness.Options.StateStoreName,
                ReminderStateKeys.TenantRegistry(harness.Options.ActorTypeName) + ":catalog")!.Complete.ShouldBeFalse();
        }
        finally { release.TrySetResult(); }
    }

    /// <summary>A newly pending catalog marker degrades readiness before a noncooperative storage write returns.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PendingCatalogControlCannotExceedServiceBudgetOrRetainPriorHealthySnapshot(bool blockRead)
    {
        var harness = new ReminderTestHarness();
        harness.Options.CatalogOperationTimeout = TimeSpan.FromSeconds(30);
        harness.Options.CatalogServiceBudget = TimeSpan.FromMilliseconds(100);
        harness.Status.CompletePass(harness.Time.Now, 0, [], harness.Status.BeginPass());
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task BlockAsync(string key)
        {
            if (!key.EndsWith(":catalog", StringComparison.Ordinal)) return;
            entered.TrySetResult();
            await release.Task;
        }
        if (blockRead) harness.CoordinatorStore.BeforeReadAsync = BlockAsync;
        else harness.CoordinatorStore.BeforeTrySaveAsync = BlockAsync;
        var catalog = Substitute.For<IReminderTargetCatalog>();
        catalog.ListAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(new ProcessSourcePage([], null));
        using var services = new ServiceCollection().AddSingleton(catalog).BuildServiceProvider();
        using var reconciler = new ReminderReconciler(new ReminderIntentIndex(harness.CoordinatorStore, Options.Create(harness.Options)),
            harness.CreateRegistrar(), harness.Status, Options.Create(harness.Options), harness.Time,
            NullLogger<ReminderReconciler>.Instance, services.GetRequiredService<IServiceScopeFactory>());
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            Task<ReminderReconciliationPass> running = reconciler.RunPassAsync(CancellationToken.None);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            harness.Status.Snapshot().PassPending.ShouldBeTrue();
            harness.Status.Snapshot().IncompleteScans.ShouldBeGreaterThan(0);
            (await running.WaitAsync(TimeSpan.FromSeconds(2))).Incomplete.ShouldBeGreaterThan(0);
            elapsed.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(2));
            harness.Status.Snapshot().IncompleteScans.ShouldBeGreaterThan(0);
        }
        finally { release.TrySetResult(); }
    }

    /// <summary>Lost initial delivery, lost firing, and reconstructed hosts converge through persisted SDK state.</summary>
    [Fact]
    public async Task CatalogRecoversInitialExpiryOnlyTargetAndRestart()
    {
        var harness = new ReminderTestHarness();
        ReminderTarget target = ReminderTestHarness.Target("expiry-only");
        harness.Source.Set(target, ReminderTestHarness.Intent(target, harness.Time.Now.AddHours(1),
            kind: Hexalith.EventStore.Contracts.Effects.EffectKindCatalog.Expiry));
        IReminderTargetCatalog catalog = Substitute.For<IReminderTargetCatalog>();
        catalog.ListAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessSourcePage([new ProcessSource(target.Tenant, target.Domain, target.Aggregate)], null));
        using ServiceProvider services = new ServiceCollection().AddSingleton(catalog).BuildServiceProvider();
        ReminderReconciler Reconciler() => new(harness.CreateIndex(), harness.CreateRegistrar(), harness.Status,
            Options.Create(harness.Options), harness.Time, NullLogger<ReminderReconciler>.Instance,
            services.GetRequiredService<IServiceScopeFactory>());

        (await Reconciler().RunPassAsync(CancellationToken.None)).Armed.ShouldBe(1);
        harness.ItemState(ReminderTestHarness.ActorId(target)).ShouldNotBeNull();
        harness.Time.Advance(TimeSpan.FromHours(2));
        (await Reconciler().RunPassAsync(CancellationToken.None)).Submitted.ShouldBe(1);
        harness.Submitter.Receipts.Count.ShouldBe(1);
        harness.ItemState(ReminderTestHarness.ActorId(target)).ShouldBeNull();
    }

    /// <summary>Repeating, foreign, malformed and unavailable catalog pages retain durable unresolved discovery.</summary>
    [Theory]
    [InlineData("repeating")]
    [InlineData("foreign")]
    [InlineData("malformed")]
    [InlineData("unavailable")]
    public async Task InvalidCatalogKeepsReadinessDegraded(string failure)
    {
        var harness = new ReminderTestHarness();
        var source = new ProcessSource(failure == "foreign" ? "UpperTenant" : "tenant-a", "widget", "item");
        IReminderTargetCatalog catalog = Substitute.For<IReminderTargetCatalog>();
        catalog.ListAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => failure == "unavailable" ? throw new HttpRequestException()
                : Task.FromResult(new ProcessSourcePage([source], failure == "malformed" ? "altered" : source.CatalogKey)));
        using ServiceProvider services = new ServiceCollection().AddSingleton(catalog).BuildServiceProvider();
        var reconciler = new ReminderReconciler(harness.CreateIndex(), harness.CreateRegistrar(), harness.Status,
            Options.Create(harness.Options), harness.Time, NullLogger<ReminderReconciler>.Instance,
            services.GetRequiredService<IServiceScopeFactory>());

        (await reconciler.RunPassAsync(CancellationToken.None)).Incomplete.ShouldBeGreaterThan(0);
        harness.Status.Snapshot().IncompleteScans.ShouldBeGreaterThan(0);
        ReminderCatalogDiscoveryState? persisted = harness.Store.Snapshot<ReminderCatalogDiscoveryState>(
            harness.Options.StateStoreName, ReminderStateKeys.TenantRegistry(harness.Options.ActorTypeName) + ":catalog");
        persisted.ShouldNotBeNull().Complete.ShouldBeFalse();
    }
    /// <summary>Caller cancellation interrupts a catalog read while the durable unfinished marker survives.</summary>
    [Fact]
    public async Task CancellationRetainsUnfinishedDiscovery()
    {
        var harness = new ReminderTestHarness();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        IReminderTargetCatalog catalog = Substitute.For<IReminderTargetCatalog>();
        catalog.ListAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, call.Arg<CancellationToken>());
            return new ProcessSourcePage([], null);
        });
        using ServiceProvider services = new ServiceCollection().AddSingleton(catalog).BuildServiceProvider();
        var reconciler = new ReminderReconciler(harness.CreateIndex(), harness.CreateRegistrar(), harness.Status,
            Options.Create(harness.Options), harness.Time, NullLogger<ReminderReconciler>.Instance,
            services.GetRequiredService<IServiceScopeFactory>());
        using var cancellation = new CancellationTokenSource();
        Task<ReminderReconciliationPass> pass = reconciler.RunPassAsync(cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await cancellation.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => pass);
        harness.Store.Snapshot<ReminderCatalogDiscoveryState>(harness.Options.StateStoreName,
            ReminderStateKeys.TenantRegistry(harness.Options.ActorTypeName) + ":catalog")!.Complete.ShouldBeFalse();
    }

    /// <summary>A finite pass persists its cursor and a reconstructed reconciler resumes the remaining targets.</summary>
    [Fact]
    public async Task BoundedSweepResumesDurableContinuationAfterRestart()
    {
        var harness = new ReminderTestHarness();
        IReminderTargetCatalog catalog = Substitute.For<IReminderTargetCatalog>();
        int pages = 0;
        catalog.ListAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            string? after = call.ArgAt<string?>(0);
            int next = after is null ? 0 : int.Parse(after.Split('\0')[2][5..], System.Globalization.CultureInfo.InvariantCulture) + 1;
            var source = new ProcessSource("tenant-a", "widget", "item-" + next.ToString("D4", System.Globalization.CultureInfo.InvariantCulture));
            pages++;
            return Task.FromResult(new ProcessSourcePage([source], next == 2 ? null : source.CatalogKey));
        });
        using ServiceProvider services = new ServiceCollection().AddSingleton(catalog).BuildServiceProvider();
        ReminderReconciler Reconciler() => new(harness.CreateIndex(), harness.CreateRegistrar(), harness.Status,
            Options.Create(harness.Options), harness.Time, NullLogger<ReminderReconciler>.Instance,
            services.GetRequiredService<IServiceScopeFactory>());
        (await Reconciler().RunPassAsync(CancellationToken.None)).Incomplete.ShouldBeGreaterThan(0);
        pages.ShouldBe(2);
        (await Reconciler().RunPassAsync(CancellationToken.None)).Incomplete.ShouldBe(0);
        pages.ShouldBe(3);
        harness.Store.Snapshot<ReminderCatalogDiscoveryState>(harness.Options.StateStoreName,
            ReminderStateKeys.TenantRegistry(harness.Options.ActorTypeName) + ":catalog")!.Complete.ShouldBeTrue();
    }

}
