using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Processes;
using Hexalith.EventStore.Client.Reminders;
using Hexalith.EventStore.Contracts.Reminders;
using Hexalith.EventStore.DomainService.Tests.Fixtures;
using Hexalith.EventStore.Testing.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>Owned discovery proofs and bounded fair service survive races, abandonment and restart.</summary>
public sealed class ReminderDiscoveryOwnershipTests
{
    /// <summary>A competing failure inserted at completion CAS survives both old completion and cleanup.</summary>
    [Fact]
    public async Task CompetingFailureFencesCompletionAndCleanup()
    {
        var harness = new ReminderTestHarness();
        ReminderIntentIndex index = harness.CreateIndex();
        ReminderCatalogDiscoveryState owned = await index.ClaimDiscoveryAsync(false, CancellationToken.None);
        string key = ReminderStateKeys.TenantRegistry(harness.Options.ActorTypeName) + ":catalog";
        ReminderCatalogDiscoveryState newer = owned with { Generation = owned.Generation + 1, Owner = "new-owner", IsIncomplete = true, ReasonCode = "catalog-unavailable" };
        harness.Store.ConcurrentWriteBeforeTrySave = () =>
        {
            harness.Store.ConcurrentWriteBeforeTrySave = null;
            harness.Store.SeedRaw(harness.Options.StateStoreName, key, newer);
        };
        await Should.ThrowAsync<ReminderFailClosedException>(() => index.UpdateDiscoveryAsync(false, owned, true, null, false, CancellationToken.None));
        await Should.ThrowAsync<ReminderFailClosedException>(() => index.UpdateDiscoveryAsync(false, owned, false, null, false, CancellationToken.None));
        harness.Store.Snapshot<ReminderCatalogDiscoveryState>(harness.Options.StateStoreName, key).ShouldBe(newer);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => index.UpdateDiscoveryAsync(false, newer, true, null, false, cancelled.Token));
        harness.Store.Snapshot<ReminderCatalogDiscoveryState>(harness.Options.StateStoreName, key).ShouldBe(newer);
    }

    /// <summary>An abandoned noncooperative write cannot overwrite a reconstructed owner's later failure.</summary>
    [Fact]
    public async Task LateProviderCompletionCannotOverwriteNewOwner()
    {
        var harness = new ReminderTestHarness();
        ReminderIntentIndex plain = harness.CreateIndex();
        ReminderCatalogDiscoveryState owned = await plain.ClaimDiscoveryAsync(false, CancellationToken.None);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        IReadModelStore provider = Substitute.For<IReadModelStore>();
        provider.GetAsync<ReminderCatalogDiscoveryState>(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => harness.Store.GetAsync<ReminderCatalogDiscoveryState>(call.ArgAt<string>(0), call.ArgAt<string>(1)));
        provider.TrySaveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReminderCatalogDiscoveryState>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                entered.TrySetResult();
                await release.Task;
                try { return await harness.Store.TrySaveAsync(call.ArgAt<string>(0), call.ArgAt<string>(1), call.ArgAt<ReminderCatalogDiscoveryState>(2), call.ArgAt<string>(3), CancellationToken.None); }
                finally { finished.TrySetResult(); }
            });
        var blocked = new ReminderIntentIndex(provider, Options.Create(harness.Options));
        Task writing = ProcessReadDeadline.RunControlAsync(ct => blocked.UpdateDiscoveryAsync(false, owned, true, null, false, ct), TimeSpan.FromMilliseconds(100), CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        try
        {
            await Should.ThrowAsync<TimeoutException>(() => writing);
            var reconstructed = harness.CreateIndex();
            ReminderCatalogDiscoveryState next = await reconstructed.ClaimDiscoveryAsync(false, CancellationToken.None);
            next = await reconstructed.UpdateDiscoveryAsync(false, next, false, "tenant-a\0widget\0later", true, CancellationToken.None);
            release.TrySetResult();
            await finished.Task.WaitAsync(TimeSpan.FromSeconds(3));
            (await reconstructed.ReadCatalogAsync(CancellationToken.None)).ShouldBe(next);
            await Should.ThrowAsync<ReminderFailClosedException>(() => blocked.UpdateDiscoveryAsync(false, owned, false, null, false, CancellationToken.None));
        }
        finally { release.TrySetResult(); }
    }

    /// <summary>A blocked registered actor receives finite service while healthy indexed and lost-initial work progress.</summary>
    [Fact]
    public async Task BlockedIndexedTargetCannotStarveHealthyIndexedOrInitialDiscovery()
    {
        var harness = new ReminderTestHarness();
        harness.Options.CatalogPageSize = 1;
        harness.Options.CatalogPagesPerPass = 1;
        harness.Options.CatalogOperationTimeout = TimeSpan.FromMilliseconds(100);
        harness.Options.CatalogServiceBudget = TimeSpan.FromMilliseconds(150);
        ReminderTarget[] indexed = [.. Enumerable.Range(0, 3).Select(i => ReminderTestHarness.Target("indexed-" + i)).OrderBy(ReminderTestHarness.ActorId, StringComparer.Ordinal)];
        foreach (ReminderTarget target in indexed)
        {
            harness.Source.Set(target, ReminderTestHarness.Intent(target, harness.Time.Now.AddHours(1)));
            await harness.CreateIndex().EnsureCandidateAsync(target, ReminderTestHarness.ActorId(target), CancellationToken.None);
        }
        var initial = ReminderTestHarness.Target("lost-initial", "tenant-z");
        harness.Source.Set(initial, ReminderTestHarness.Intent(initial, harness.Time.Now.AddHours(1)));
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int blockedCalls = 0;
        var real = harness.CreateRegistrar();
        var registrar = Substitute.For<IReminderRegistrar>();
        registrar.ConvergeAsync(Arg.Any<ReminderTarget>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            ReminderTarget target = call.Arg<ReminderTarget>();
            if (target == indexed[0])
            {
                Interlocked.Increment(ref blockedCalls);
                await release.Task; // Deliberately ignores caller cancellation.
                try { return await real.ConvergeAsync(target); }
                finally { finished.TrySetResult(); }
            }
            return await real.ConvergeAsync(target, call.Arg<CancellationToken>());
        });
        var catalog = Substitute.For<IReminderTargetCatalog>();
        catalog.ListAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(new ProcessSourcePage([new(initial.Tenant, initial.Domain, initial.Aggregate)], null));
        using var services = new ServiceCollection().AddSingleton(catalog).BuildServiceProvider();
        ReminderReconciler Reconstructed() => new(harness.CreateIndex(), registrar, harness.Status, Options.Create(harness.Options), harness.Time,
            NullLogger<ReminderReconciler>.Instance, services.GetRequiredService<IServiceScopeFactory>());
        try
        {
            for (int pass = 0; pass < 7; pass++)
            {
                using var reconciler = Reconstructed();
                (await reconciler.RunPassAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(3))).Incomplete.ShouldBeGreaterThan(0);
                harness.ItemState(ReminderTestHarness.ActorId(initial)).ShouldNotBeNull();
            }
            blockedCalls.ShouldBe(1);
            foreach (ReminderTarget healthy in indexed.Skip(1)) harness.ItemState(ReminderTestHarness.ActorId(healthy)).ShouldNotBeNull();
            harness.Status.Snapshot().IncompleteScans.ShouldBeGreaterThan(0);
            release.TrySetResult();
            await finished.Task.WaitAsync(TimeSpan.FromSeconds(3));
            for (int pass = 0; pass < 12 && harness.Status.Snapshot().IncompleteScans > 0; pass++)
            {
                using var reconciler = Reconstructed();
                await reconciler.RunPassAsync(CancellationToken.None);
            }
            harness.Status.Snapshot().IncompleteScans.ShouldBe(0);
        }
        finally { release.TrySetResult(); }
    }

    /// <summary>Full coverage across several slices eventually prunes removed stale evidence, preserving later callbacks.</summary>
    [Fact]
    public async Task MultiPassCoveragePrunesRemovedItemsOnlyAtSweepCompletion()
    {
        var harness = new ReminderTestHarness();
        harness.Options.CatalogPageSize = 1;
        harness.Options.CatalogPagesPerPass = 1;
        harness.Status.RecordItem("removed-before-sweep", 1, 0);
        for (int i = 0; i < 3; i++)
        {
            var target = ReminderTestHarness.Target("item-" + i);
            harness.Source.Set(target, ReminderTestHarness.Intent(target, harness.Time.Now.AddHours(1)));
            await harness.CreateRegistrar().ConvergeAsync(target);
        }
        using (var first = harness.CreateReconciler()) await first.RunPassAsync(CancellationToken.None);
        harness.Status.Snapshot().Unresolved.ShouldBe(1);
        harness.Status.RecordItem("callback-after-start", 1, 0);
        for (int pass = 0; pass < 3 && harness.Status.Snapshot().IncompleteScans > 0; pass++)
        {
            using var reconstructed = harness.CreateReconciler();
            await reconstructed.RunPassAsync(CancellationToken.None);
        }
        harness.Status.Snapshot().IncompleteScans.ShouldBe(0);
        harness.Status.Snapshot().Unresolved.ShouldBe(1); // Only the newer callback remains.
        using (var first = harness.CreateReconciler()) await first.RunPassAsync(CancellationToken.None);
        for (int pass = 0; pass < 3 && harness.Status.Snapshot().IncompleteScans > 0; pass++)
        {
            using var reconstructed = harness.CreateReconciler();
            await reconstructed.RunPassAsync(CancellationToken.None);
        }
        harness.Status.Snapshot().Unresolved.ShouldBe(0);
    }
    /// <summary>A replica that missed another owner's middle slice cannot prune its unobserved actors at the tail.</summary>
    [Fact]
    public async Task ReplicaCoverageDiscontinuityPreventsPartialPruning()
    {
        var harness = new ReminderTestHarness();
        harness.Options.CatalogPageSize = 1;
        harness.Options.CatalogPagesPerPass = 1;
        ReminderTarget[] targets = [.. Enumerable.Range(0, 3).Select(i => ReminderTestHarness.Target("replica-" + i))
            .OrderBy(ReminderTestHarness.ActorId, StringComparer.Ordinal)];
        foreach (ReminderTarget target in targets)
        {
            harness.Source.Set(target, ReminderTestHarness.Intent(target, harness.Time.Now.AddHours(1)));
            await harness.CreateRegistrar().ConvergeAsync(target);
        }
        var replicaA = new ReminderRuntimeStatus();
        var replicaB = new ReminderRuntimeStatus();
        replicaA.RecordItem(ReminderTestHarness.ActorId(targets[1]), 1, 0);
        using (var first = harness.CreateReconciler(replicaA)) await first.RunPassAsync(CancellationToken.None);
        using (var middle = harness.CreateReconciler(replicaB)) await middle.RunPassAsync(CancellationToken.None);
        using (var tail = harness.CreateReconciler(replicaA)) await tail.RunPassAsync(CancellationToken.None);
        replicaA.Snapshot().IncompleteScans.ShouldBe(0);
        replicaA.Snapshot().Unresolved.ShouldBe(1);
        for (int pass = 0; pass < 3; pass++)
        {
            using var next = harness.CreateReconciler(replicaA);
            await next.RunPassAsync(CancellationToken.None);
        }
        replicaA.Snapshot().Unresolved.ShouldBe(0);
    }

}
