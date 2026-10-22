using System.Security.Claims;

using Hexalith.EventStore.Client.Effects;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Processes;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.Contracts.Processes;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests.Processes;

/// <summary>Persisted candidate, sweep, readiness, and disposition recovery cases.</summary>
public sealed class ProcessRecoveryTests
{
    /// <summary>Partial catalogs retain degradation and discover healthy additions before an old cursor after restart.</summary>
    [Fact]
    public async Task PartialCatalogContinuesHealthyDiscoveryAndRestartsAtHead()
    {
        var late = new ProcessSource("tenant-z", "sample", "late");
        var early = new ProcessSource("tenant-b", "sample", "early");
        ProcessSource[] members = [late];
        var catalog = Substitute.For<IProcessSourceCatalog>();
        catalog.ListAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            string? after = call.ArgAt<string?>(0);
            int limit = call.ArgAt<int>(1);
            var remaining = members.Where(source => after is null || string.CompareOrdinal(source.CatalogKey, after) > 0).Take(limit + 1).ToArray();
            var page = remaining.Take(limit).ToArray();
            return Task.FromResult(new ProcessSourcePage(page, remaining.Length > limit ? page[^1].CatalogKey : null) { IsIncomplete = true });
        });
        var store = new InMemoryReadModelStore();
        var options = new EventStoreProcessOptions { SourcesPerPass = 1 };
        var first = Runtime(store, options, catalog);
        using (first.Reconciler)
            for (int pass = 0; pass < 20 && !(await first.Sweeps.GetAsync()).Complete; pass++)
                await first.Reconciler.RunPassAsync();
        (await first.Checkpoints.GetAsync(late)).Checkpoint.ShouldBe(1);
        (await first.Sweeps.GetAsync()).Unresolved.ShouldBeGreaterThan(0);
        members = [early, late];
        var restarted = Runtime(store, options, catalog);
        using (restarted.Reconciler)
            for (int pass = 0; pass < 30 && (await restarted.Checkpoints.GetAsync(early)).Checkpoint == 0; pass++)
                await restarted.Reconciler.RunPassAsync();
        (await restarted.Checkpoints.GetAsync(early)).Checkpoint.ShouldBe(1);
        (await new ProcessReadinessHealthCheck(restarted.Sweeps, Options.Create(options), TimeProvider.System)
            .CheckHealthAsync(new HealthCheckContext())).Status.ShouldBe(HealthStatus.Degraded);
    }

    /// <summary>A later transition cannot be covered by an observation of an earlier handoff.</summary>
    [Fact]
    public async Task LaterTransitionKeepsReadinessHandoffAfterEarlierObservation()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new();
        ProcessSource source = new("tenant-a", "sample", "source");
        ProcessSweepStore sweeps = new(store, Options.Create(options), TimeProvider.System);
        ProcessPendingTransition earlier = await sweeps.InvalidateSourceAsync(source);
        ProcessPendingTransition later = await sweeps.InvalidateSourceAsync(source);
        earlier.Token.ShouldNotBe(later.Token);

        ProcessSweepState owned = (await sweeps.AcquireAsync()).ShouldNotBeNull();
        owned = await sweeps.UpdateOwnedAsync(owned, current => current with
        {
            PendingObservations = new Dictionary<string, ProcessPendingObservation>
            {
                [ProcessStateKeys.SourceId(source)] = new(source, current.Cycle, false),
            },
        });
        owned = await sweeps.FinishObservationAsync(owned, source, earlier.Token);
        owned.PendingTransitions[ProcessStateKeys.SourceId(source)].Token.ShouldBe(later.Token);
        owned.Complete.ShouldBeFalse();

        owned = await sweeps.UpdateOwnedAsync(owned, current => current with
        {
            PendingObservations = new Dictionary<string, ProcessPendingObservation>
            {
                [ProcessStateKeys.SourceId(source)] = new(source, current.Cycle, false),
            },
        });
        owned = await sweeps.FinishObservationAsync(owned, source, later.Token);
        owned.PendingTransitions.ShouldBeEmpty();
    }

    /// <summary>A one-source pass requests one catalog entry, even with a larger configured page.</summary>
    [Fact]
    public async Task OneSourcePassChargesEveryCatalogRequest()
    {
        ProcessSource[] sources = [new("tenant-a", "sample", "a"), new("tenant-a", "sample", "b"), new("tenant-a", "sample", "c")];
        var requests = new List<int>();
        IProcessSourceCatalog catalog = Substitute.For<IProcessSourceCatalog>();
        catalog.ListAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            string? after = call.ArgAt<string?>(0);
            int limit = call.ArgAt<int>(1);
            requests.Add(limit);
            ProcessSource[] remaining = sources.Where(source => after is null || string.CompareOrdinal(source.CatalogKey, after) > 0).ToArray();
            ProcessSource[] page = remaining.Take(limit).ToArray();
            return Task.FromResult(new ProcessSourcePage(page, remaining.Length > page.Length ? page[^1].CatalogKey : null));
        });
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { SourcesPerPass = 1, DiscoveryPageSize = 64 };
        (ProcessCheckpointStore checkpoints, _, ProcessReconciler reconciler) = Runtime(store, options, catalog);
        using (reconciler)
        {
            for (int pass = 0; pass < 30 && (await checkpoints.GetAsync(sources[2])).Checkpoint == 0; pass++)
                _ = await reconciler.RunPassAsync();
        }
        (await checkpoints.GetAsync(sources[2])).Checkpoint.ShouldBe(1);
        requests.ShouldNotBeEmpty();
        requests.Distinct().Single().ShouldBe(1);
    }

    /// <summary>Persisted sweep validation uses the registered clock for future-date checks.</summary>
    [Fact]
    public async Task InjectedClockValidatesCompletedSweep()
    {
        DateTimeOffset now = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new();
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Sweep,
            new ProcessSweepState(1, 1, 1, true, now, now, null, true, 0, 0, 0, 0, true, true,
                0, new Dictionary<string, int>(), new Dictionary<string, ProcessPendingDisposition>(), null, null, 0));
        ManualProcessTimeProvider clock = new(now);
        ProcessSweepStore sweeps = new(store, Options.Create(options), clock);

        (await sweeps.GetAsync()).Complete.ShouldBeTrue();
        (await new ProcessReadinessHealthCheck(sweeps, Options.Create(options), clock)
            .CheckHealthAsync(new HealthCheckContext())).Status.ShouldBe(HealthStatus.Healthy);
    }

    /// <summary>Clock lag does not make durable control state unreadable; readiness still rejects its future completion.</summary>
    [Fact]
    public async Task FutureCompletionRemainsReadableButDegraded()
    {
        DateTimeOffset completed = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new();
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Sweep,
            new ProcessSweepState(1, 1, 1, true, completed, completed, null, true, 0, 0, 0, 0, true, true,
                0, new Dictionary<string, int>(), new Dictionary<string, ProcessPendingDisposition>(), null, null, 0));
        ManualProcessTimeProvider lagging = new(completed.AddMinutes(-1));
        ProcessSweepStore sweeps = new(store, Options.Create(options), lagging);

        (await sweeps.GetAsync()).Complete.ShouldBeTrue();
        (await new ProcessReadinessHealthCheck(sweeps, Options.Create(options), lagging)
            .CheckHealthAsync(new HealthCheckContext())).Status.ShouldBe(HealthStatus.Degraded);
    }

    /// <summary>Durable sweep cursors cannot approach the much larger index storage ceiling.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ExcessivePersistedSweepPageIsRejected(bool tenantPage)
    {
        ProcessSweepState state = new(1, 1, 1, false, DateTimeOffset.UtcNow, null, null, false,
            tenantPage ? 100_000 : 0, 0, tenantPage ? 0 : 100_000, 0, false, true,
            0, new Dictionary<string, int>(), new Dictionary<string, ProcessPendingDisposition>(), null, null, 0);

        Should.Throw<InvalidOperationException>(() => state.Validate());
    }

    /// <summary>Count limits alone cannot admit a multi-map record beyond the state-item byte ceiling.</summary>
    [Fact]
    public void MaximumLengthPendingIdentitiesCannotOverflowSweepRecord()
    {
        var pending = new Dictionary<string, ProcessPendingObservation>(StringComparer.Ordinal);
        for (int index = 0; index < 700; index++)
        {
            ProcessSource source = new("tenant-a", "sample", $"{index:0000}" + new string('x', 252));
            pending.Add(ProcessStateKeys.SourceId(source), new ProcessPendingObservation(source, 1, false));
        }
        ProcessSweepState state = new(1, 1, 1, false, DateTimeOffset.UtcNow, null, null, false,
            0, 0, 0, 0, false, true, 0, new Dictionary<string, int>(),
            new Dictionary<string, ProcessPendingDisposition>(), null, null, 0)
        { PendingObservations = pending };

        state.SerializedSize().ShouldBeGreaterThan(ProcessSweepState.MaxSerializedBytes);
        Should.Throw<InvalidOperationException>(() => state.Validate());
    }

    /// <summary>A failed hosted pass degrades local readiness immediately even over fresh completed coverage.</summary>
    [Fact]
    public async Task HostedPreAcquireFailureInvalidatesLocalHealthyReadiness()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { ReconciliationInterval = TimeSpan.FromSeconds(1) };
        (_, ProcessSweepStore seeded, ProcessReconciler first) = Runtime(store, options, Catalog([]));
        using (first)
            _ = await first.RunPassAsync();
        (await seeded.GetAsync()).Complete.ShouldBeTrue();

        FaultOnceOnSweepReadStore faulting = new(store);
        (_, _, ProcessReconciler hosted) = Runtime(faulting, options, Catalog([]));
        using (hosted)
        {
            ProcessReadinessHealthCheck health = new(seeded, Options.Create(options), TimeProvider.System, hosted);
            await hosted.StartAsync(CancellationToken.None);
            for (int attempt = 0; attempt < 100 && hosted.LocalFailureReason is null; attempt++)
                await Task.Delay(10);
            hosted.LocalFailureReason.ShouldBe("hosted-pass-failed");
            HealthCheckResult failed = await health.CheckHealthAsync(new HealthCheckContext());
            failed.Status.ShouldBe(HealthStatus.Degraded);
            ((Dictionary<string, int>)failed.Data["reasonCounts"])["hosted-pass-failed"].ShouldBe(1);

            for (int attempt = 0; attempt < 300 && hosted.LocalFailureReason is not null; attempt++)
                await Task.Delay(10);
            hosted.LocalFailureReason.ShouldBeNull();
            (await health.CheckHealthAsync(new HealthCheckContext())).Status.ShouldBe(HealthStatus.Healthy);
            await hosted.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>A valid one-source budget advances a keyset catalog and a retained candidate registry.</summary>
    [Fact]
    public async Task SmallBudgetReachesLaterCatalogSourceAndCompletesSweep()
    {
        ProcessSource[] sources = [new("tenant-a", "sample", "a"), new("tenant-a", "sample", "b"), new("tenant-a", "sample", "c")];
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { SourcesPerPass = 1, DiscoveryPageSize = 64 };
        IProcessSourceCatalog catalog = Catalog(sources);
        (ProcessCheckpointStore checkpoints, ProcessSweepStore sweeps, ProcessReconciler reconciler) = Runtime(store, options, catalog);
        using (reconciler)
        {
            for (int pass = 0; pass < 30; pass++)
            {
                ProcessReconciliationPass result = await reconciler.RunPassAsync();
                if (result.CycleComplete) break;
            }
        }

        (await checkpoints.GetAsync(sources[2])).Checkpoint.ShouldBe(1);
        ProcessSweepState sweep = await sweeps.GetAsync();
        sweep.Complete.ShouldBeTrue();
        sweep.Unresolved.ShouldBe(0);
        (await new ProcessReadinessHealthCheck(sweeps, Options.Create(options), TimeProvider.System)
            .CheckHealthAsync(new HealthCheckContext())).Status.ShouldBe(HealthStatus.Healthy);
    }

    /// <summary>A catalog adapter that blocks before returning its task cannot hold retained candidate work.</summary>
    [Fact]
    public async Task SynchronouslyBlockingCatalogYieldsToRetainedCandidate()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new()
        {
            OperationTimeout = TimeSpan.FromMilliseconds(100),
            SourceServiceQuantum = TimeSpan.FromMilliseconds(800),
            MaxEffectsPerEnvelope = 1,
            SourcesPerPass = 2,
        };
        ProcessSource retained = new("tenant-a", "sample", "retained");
        ProcessCheckpointStore prepared = new(store, Options.Create(options), TimeProvider.System);
        await prepared.EnsureCandidateAsync(retained);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        IProcessSourceCatalog catalog = Substitute.For<IProcessSourceCatalog>();
        catalog.ListAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                release.Task.GetAwaiter().GetResult();
                return Task.FromResult(new ProcessSourcePage([], null));
            });
        var (checkpoints, _, reconciler) = Runtime(store, options, catalog);

        try
        {
            using (reconciler)
            {
                _ = await reconciler.RunPassAsync().WaitAsync(TimeSpan.FromSeconds(2));
            }
            (await checkpoints.GetAsync(retained)).Checkpoint.ShouldBe(1);
        }
        finally
        {
            release.SetResult();
        }
    }

    /// <summary>A catalog page is reused across source observations instead of fetched once per source.</summary>
    [Fact]
    public async Task CatalogDiscoveryUsesBoundedMultiSourcePages()
    {
        ProcessSource[] sources = Enumerable.Range(0, 6)
            .Select(index => new ProcessSource("tenant-a", "sample", $"source-{index}"))
            .ToArray();
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { SourcesPerPass = 16, DiscoveryPageSize = 3 };
        IProcessSourceCatalog catalog = Catalog(sources);
        (ProcessCheckpointStore checkpoints, ProcessSweepStore sweeps, ProcessReconciler reconciler) = Runtime(store, options, catalog);
        using (reconciler)
        {
            for (int pass = 0; pass < 12 && !(await sweeps.GetAsync()).Complete; pass++)
                _ = await reconciler.RunPassAsync();
        }

        (await sweeps.GetAsync()).Complete.ShouldBeTrue();
        (await checkpoints.GetAsync(sources[^1])).Checkpoint.ShouldBe(1);
        _ = await catalog.Received(2).ListAsync(Arg.Any<string?>(), 3, Arg.Any<CancellationToken>());
        catalog.ReceivedCalls().Count().ShouldBe(2);
    }

    /// <summary>Validated capacity includes operation latency, a complete envelope, and repair work.</summary>
    [Fact]
    public void CapacityValidationIncludesSourceServiceAndPageLatency()
    {
        EventStoreProcessOptions options = new();
        options.Validate().ShouldBeEmpty();
        options.SupportedCandidateCount = 10;
        options.Validate().ShouldContain(error => error.Contains("SupportedCandidateCount", StringComparison.Ordinal));

        options.SupportedCandidateCount = 1;
        options.SourceServiceQuantum = TimeSpan.FromSeconds(1);
        options.Validate().ShouldContain(error => error.Contains("SourceServiceQuantum", StringComparison.Ordinal));

        options.SourceServiceQuantum = TimeSpan.FromSeconds(12);
        options.LeaseDuration = TimeSpan.FromSeconds(1);
        options.Validate().ShouldContain(error => error.Contains("LeaseDuration", StringComparison.Ordinal));
    }

    /// <summary>A lost marker write followed by later page growth does not duplicate the first source.</summary>
    [Fact]
    public async Task CandidateMarkerCrashRecoversAcrossLinkedPages()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new();
        ProcessSource first = new("tenant-a", "sample", "item-000");
        ProcessCheckpointStore crashing = new(new FaultOnceOnCandidateMarkerStore(store, first), Options.Create(options), TimeProvider.System);
        await Should.ThrowAsync<InvalidOperationException>(() => crashing.EnsureCandidateAsync(first));
        ProcessCheckpointStore checkpoints = new(store, Options.Create(options), TimeProvider.System);
        for (int i = 1; i < 258; i++)
        {
            await checkpoints.EnsureCandidateAsync(new ProcessSource("tenant-a", "sample", $"item-{i:000}"));
        }

        (await store.GetAsync<ProcessCandidateMarker>(options.StateStoreName, ProcessStateKeys.CandidateMarker(first))).Value.ShouldBeNull();
        await checkpoints.EnsureCandidateAsync(first);

        ProcessIndexPage<ProcessSource> page0 = (await checkpoints.ReadCandidatePageAsync("tenant-a", 0)).ShouldNotBeNull();
        ProcessIndexPage<ProcessSource> page1 = (await checkpoints.ReadCandidatePageAsync("tenant-a", 1)).ShouldNotBeNull();
        page0.Entries.Concat(page1.Entries).Count(source => source == first).ShouldBe(1);
        (await checkpoints.ContainsCandidateAsync(first)).ShouldBeTrue();
    }

    /// <summary>A corrupt candidate page is retained as a scan reason while a reachable later page executes.</summary>
    [Fact]
    public async Task CorruptCandidatePageDoesNotStrandLaterPage()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { SourcesPerPass = 1 };
        ProcessSource first = new("tenant-a", "sample", "first");
        ProcessSource later = new("tenant-a", "sample", "later");
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Tenants(0), new ProcessIndexPage<string>(1, 0, ["tenant-a"], null));
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Candidates("tenant-a", 0), new ProcessIndexPage<ProcessSource>(1, 0, [first], 1));
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Candidates("tenant-a", 1), new ProcessIndexPage<ProcessSource>(99, 1, [first], 2));
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Candidates("tenant-a", 2), new ProcessIndexPage<ProcessSource>(1, 2, [later], null));
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.CandidateMarker(later), new ProcessCandidateMarker(1, later));
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.TenantMarker("tenant-a"), new ProcessTenantMarker(1, "tenant-a"));
        (ProcessCheckpointStore checkpoints, ProcessSweepStore sweeps, ProcessReconciler reconciler) = Runtime(store, options, Catalog([]));
        using (reconciler)
        {
            for (int pass = 0; pass < 15 && (await checkpoints.GetAsync(later)).Checkpoint == 0; pass++)
                _ = await reconciler.RunPassAsync();
        }

        (await checkpoints.GetAsync(later)).Checkpoint.ShouldBe(1);
        (await sweeps.GetAsync()).Reasons.ShouldContainKey("candidate-page-unreadable");
    }

    /// <summary>A corrupt earlier tenant page cannot prevent a later tenant from reaching a nonzero checkpoint.</summary>
    [Fact]
    public async Task CorruptTenantPageDoesNotStrandLaterTenant()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { SourcesPerPass = 1 };
        ProcessSource later = new("tenant-b", "sample", "later");
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Tenants(0), new ProcessIndexPage<string>(99, 0, ["tenant-a"], 1));
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Tenants(1), new ProcessIndexPage<string>(1, 1, ["tenant-b"], null));
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Candidates("tenant-b", 0), new ProcessIndexPage<ProcessSource>(1, 0, [later], null));
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.CandidateMarker(later), new ProcessCandidateMarker(1, later));
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.TenantMarker("tenant-b"), new ProcessTenantMarker(1, "tenant-b"));
        (ProcessCheckpointStore checkpoints, ProcessSweepStore sweeps, ProcessReconciler reconciler) = Runtime(store, options, Catalog([]));
        using (reconciler)
        {
            for (int pass = 0; pass < 15 && (await checkpoints.GetAsync(later)).Checkpoint == 0; pass++)
                _ = await reconciler.RunPassAsync();
        }

        (await checkpoints.GetAsync(later)).Checkpoint.ShouldBe(1);
        (await sweeps.GetAsync()).Reasons.ShouldContainKey("tenant-page-unreadable");
    }

    /// <summary>An invalid tenant tail remains unresolved while a later sequential tenant executes.</summary>
    [Fact]
    public async Task UnreadableTenantTailDoesNotStrandLaterTenant()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { SourcesPerPass = 1 };
        ProcessSource later = new("tenant-b", "sample", "later");
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Tenants(0), new ProcessIndexPage<string>(1, 0, ["tenant-a"], null));
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.TenantsTail, new ProcessIndexTail(99, 1));
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Tenants(1), new ProcessIndexPage<string>(1, 1, ["tenant-b"], null));
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Candidates("tenant-b", 0), new ProcessIndexPage<ProcessSource>(1, 0, [later], null));
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.CandidateMarker(later), new ProcessCandidateMarker(1, later));
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.TenantMarker("tenant-b"), new ProcessTenantMarker(1, "tenant-b"));
        (ProcessCheckpointStore checkpoints, ProcessSweepStore sweeps, ProcessReconciler reconciler) = Runtime(store, options, Catalog([]));
        using (reconciler)
        {
            for (int pass = 0; pass < 24 && (await checkpoints.GetAsync(later)).Checkpoint == 0; pass++)
                _ = await reconciler.RunPassAsync();
        }

        (await checkpoints.GetAsync(later)).Checkpoint.ShouldBe(1);
        (await sweeps.GetAsync()).Reasons.ShouldContainKey("tenant-tail-unreadable");
    }

    /// <summary>An invalid candidate tail remains unresolved while a later sequential page executes.</summary>
    [Fact]
    public async Task UnreadableCandidateTailDoesNotStrandLaterPage()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { SourcesPerPass = 1 };
        ProcessSource first = new("tenant-a", "sample", "first");
        ProcessSource later = new("tenant-a", "sample", "later");
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Tenants(0), new ProcessIndexPage<string>(1, 0, ["tenant-a"], null));
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Candidates("tenant-a", 0), new ProcessIndexPage<ProcessSource>(1, 0, [first], null));
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.CandidatesTail("tenant-a"), new ProcessIndexTail(99, 1));
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Candidates("tenant-a", 1), new ProcessIndexPage<ProcessSource>(1, 1, [later], null));
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.CandidateMarker(later), new ProcessCandidateMarker(1, later));
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.TenantMarker("tenant-a"), new ProcessTenantMarker(1, "tenant-a"));
        (ProcessCheckpointStore checkpoints, ProcessSweepStore sweeps, ProcessReconciler reconciler) = Runtime(store, options, Catalog([]));
        using (reconciler)
        {
            for (int pass = 0; pass < 24 && (await checkpoints.GetAsync(later)).Checkpoint == 0; pass++)
                _ = await reconciler.RunPassAsync();
        }

        (await checkpoints.GetAsync(later)).Checkpoint.ShouldBe(1);
        (await sweeps.GetAsync()).Reasons.ShouldContainKey("candidate-tail-unreadable");
    }

    /// <summary>A timed-out tenant tail read remains unresolved while a later tenant executes.</summary>
    [Fact]
    public async Task TenantTailReadTimeoutDoesNotStrandLaterTenant()
    {
        InMemoryReadModelStore persisted = new();
        EventStoreProcessOptions options = new() { SourcesPerPass = 1 };
        ProcessSource later = new("tenant-b", "sample", "later");
        persisted.SeedRaw(options.StateStoreName, ProcessStateKeys.Tenants(0), new ProcessIndexPage<string>(1, 0, ["tenant-a"], null));
        persisted.SeedRaw(options.StateStoreName, ProcessStateKeys.Tenants(1), new ProcessIndexPage<string>(1, 1, ["tenant-b"], null));
        persisted.SeedRaw(options.StateStoreName, ProcessStateKeys.TenantsTail, new ProcessIndexTail(1, 1));
        persisted.SeedRaw(options.StateStoreName, ProcessStateKeys.Candidates("tenant-b", 0), new ProcessIndexPage<ProcessSource>(1, 0, [later], null));
        persisted.SeedRaw(options.StateStoreName, ProcessStateKeys.CandidateMarker(later), new ProcessCandidateMarker(1, later));
        persisted.SeedRaw(options.StateStoreName, ProcessStateKeys.TenantMarker("tenant-b"), new ProcessTenantMarker(1, "tenant-b"));
        FaultOnProcessTailReadStore faulting = new(persisted, ProcessStateKeys.TenantsTail);
        (ProcessCheckpointStore checkpoints, ProcessSweepStore sweeps, ProcessReconciler reconciler) = Runtime(faulting, options, Catalog([]));

        using (reconciler)
        {
            for (int pass = 0; pass < 24 && (await checkpoints.GetAsync(later)).Checkpoint == 0; pass++)
                _ = await reconciler.RunPassAsync();
        }

        faulting.TailReads.ShouldBeGreaterThan(0);
        (await checkpoints.GetAsync(later)).Checkpoint.ShouldBe(1);
        (await sweeps.GetAsync()).Reasons.ShouldContainKey("tenant-tail-unreadable");
    }

    /// <summary>A timed-out candidate tail read remains unresolved while a later page executes.</summary>
    [Fact]
    public async Task CandidateTailReadTimeoutDoesNotStrandLaterPage()
    {
        InMemoryReadModelStore persisted = new();
        EventStoreProcessOptions options = new() { SourcesPerPass = 1 };
        ProcessSource first = new("tenant-a", "sample", "first");
        ProcessSource later = new("tenant-a", "sample", "later");
        persisted.SeedRaw(options.StateStoreName, ProcessStateKeys.Tenants(0), new ProcessIndexPage<string>(1, 0, ["tenant-a"], null));
        persisted.SeedRaw(options.StateStoreName, ProcessStateKeys.Candidates("tenant-a", 0), new ProcessIndexPage<ProcessSource>(1, 0, [first], null));
        persisted.SeedRaw(options.StateStoreName, ProcessStateKeys.Candidates("tenant-a", 1), new ProcessIndexPage<ProcessSource>(1, 1, [later], null));
        persisted.SeedRaw(options.StateStoreName, ProcessStateKeys.CandidatesTail("tenant-a"), new ProcessIndexTail(1, 1));
        persisted.SeedRaw(options.StateStoreName, ProcessStateKeys.CandidateMarker(first), new ProcessCandidateMarker(1, first));
        persisted.SeedRaw(options.StateStoreName, ProcessStateKeys.CandidateMarker(later), new ProcessCandidateMarker(1, later));
        persisted.SeedRaw(options.StateStoreName, ProcessStateKeys.TenantMarker("tenant-a"), new ProcessTenantMarker(1, "tenant-a"));
        FaultOnProcessTailReadStore faulting = new(persisted, ProcessStateKeys.CandidatesTail("tenant-a"));
        (ProcessCheckpointStore checkpoints, ProcessSweepStore sweeps, ProcessReconciler reconciler) = Runtime(faulting, options, Catalog([]));

        using (reconciler)
        {
            for (int pass = 0; pass < 24 && (await checkpoints.GetAsync(later)).Checkpoint == 0; pass++)
                _ = await reconciler.RunPassAsync();
        }

        faulting.TailReads.ShouldBeGreaterThan(0);
        (await checkpoints.GetAsync(later)).Checkpoint.ShouldBe(1);
        (await sweeps.GetAsync()).Reasons.ShouldContainKey("candidate-tail-unreadable");
    }

    /// <summary>A wrong keyset continuation cannot skip the next authoritative source.</summary>
    [Fact]
    public async Task MalformedCatalogNextKeyKeepsCoverageUnresolved()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { SourcesPerPass = 2 };
        ProcessSource source = new("tenant-a", "sample", "first");
        IProcessSourceCatalog catalog = Substitute.For<IProcessSourceCatalog>();
        catalog.ListAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProcessSourcePage([source], "tenant-a:sample:skipped")));
        (_, ProcessSweepStore sweeps, ProcessReconciler reconciler) = Runtime(store, options, catalog);
        using (reconciler)
            _ = await reconciler.RunPassAsync();

        ProcessSweepState sweep = await sweeps.GetAsync();
        sweep.Reasons.ShouldContainKey("catalog-unavailable");
        sweep.Unresolved.ShouldBeGreaterThan(0);
        sweep.CatalogAfterKey.ShouldBeNull();
    }

    /// <summary>An unknown persisted source version is unresolved evidence and does not block a later source.</summary>
    [Fact]
    public async Task UnknownSourceStateVersionDoesNotStrandLaterCatalogSource()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { SourcesPerPass = 2 };
        ProcessSource corrupt = new("tenant-a", "sample", "a");
        ProcessSource later = new("tenant-a", "sample", "b");
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Source(corrupt),
            new ProcessSourceState(99, corrupt, 0, ProcessSourceStatus.Active, 0, null, null, null, null, 0, 0, DateTimeOffset.UtcNow));
        (ProcessCheckpointStore checkpoints, ProcessSweepStore sweeps, ProcessReconciler reconciler) = Runtime(store, options, Catalog([corrupt, later]));
        using (reconciler)
        {
            for (int pass = 0; pass < 12 && (await checkpoints.GetAsync(later)).Checkpoint == 0; pass++)
                _ = await reconciler.RunPassAsync();
        }

        (await checkpoints.GetAsync(later)).Checkpoint.ShouldBe(1);
        (await sweeps.GetAsync()).Reasons.ShouldContainKey("source-state-unavailable");
    }

    /// <summary>Audit failure and anonymous identity cannot mutate; prepared disposition survives restart.</summary>
    [Fact]
    public async Task DispositionPreparationDegradesUntilFreshCoverageAfterRestart()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { SourcesPerPass = 2 };
        ProcessSource source = new("tenant-a", "sample", "source");
        (ProcessCheckpointStore checkpoints, ProcessSweepStore sweeps, ProcessReconciler reconciler) = Runtime(store, options, Catalog([]));
        await checkpoints.EnsureCandidateAsync(source);
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "operator")], "synthetic"));
        IProcessDispositionAuthorizer authorizer = Substitute.For<IProcessDispositionAuthorizer>();
        authorizer.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), source, Arg.Any<CancellationToken>()).Returns(true);
        IProcessDispositionAuditSink audit = Substitute.For<IProcessDispositionAuditSink>();
        ProcessDispositionService disposition = new(authorizer, audit, checkpoints, sweeps);

        await Should.ThrowAsync<UnauthorizedAccessException>(() => disposition.RevalidateAsync(new ClaimsPrincipal(new ClaimsIdentity()), source, "retry"));
        (await sweeps.GetAsync()).Pending.Count.ShouldBe(0);
        audit.RecordAsync(principal, source, "retry", Arg.Any<CancellationToken>()).Returns(Task.FromException(new InvalidOperationException("Synthetic audit outage.")));
        await Should.ThrowAsync<InvalidOperationException>(() => disposition.RevalidateAsync(principal, source, "retry"));
        (await sweeps.GetAsync()).Pending.Count.ShouldBe(0);

        // Simulate a crash exactly after durable invalidation and before source mutation.
        _ = await sweeps.PrepareDispositionAsync(source);
        HealthCheckResult result = await new ProcessReadinessHealthCheck(sweeps, Options.Create(options), TimeProvider.System)
            .CheckHealthAsync(new HealthCheckContext());
        result.Status.ShouldBe(HealthStatus.Degraded);
        result.Data["complete"].ShouldBe(false);
        result.Data["reasonCounts"].ShouldBeOfType<Dictionary<string, int>>().ShouldBeEmpty();
        using (reconciler)
        {
            for (int pass = 0; pass < 15; pass++)
            {
                if ((await reconciler.RunPassAsync()).CycleComplete) break;
            }
        }

        ProcessSweepState complete = await sweeps.GetAsync();
        complete.Pending.Count.ShouldBe(0);
        complete.Complete.ShouldBeTrue();
        (await checkpoints.GetAsync(source)).Status.ShouldBe(ProcessSourceStatus.Active);
    }

    /// <summary>An audited disposition invalidates a healthy snapshot before source revalidation and recovers after a fresh sweep.</summary>
    [Fact]
    public async Task AuditedDispositionImmediatelyInvalidatesHealthyReadiness()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { SourcesPerPass = 4 };
        ProcessSource source = new("tenant-a", "sample", "source");
        (ProcessCheckpointStore checkpoints, ProcessSweepStore sweeps, ProcessReconciler reconciler) = Runtime(store, options, Catalog([source]));
        using (reconciler)
        {
            for (int pass = 0; pass < 10 && !(await sweeps.GetAsync()).Complete; pass++)
                _ = await reconciler.RunPassAsync();
            var health = new ProcessReadinessHealthCheck(sweeps, Options.Create(options), TimeProvider.System);
            (await health.CheckHealthAsync(new HealthCheckContext())).Status.ShouldBe(HealthStatus.Healthy);

            var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "operator")], "synthetic"));
            IProcessDispositionAuthorizer authorizer = Substitute.For<IProcessDispositionAuthorizer>();
            authorizer.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), source, Arg.Any<CancellationToken>()).Returns(true);
            IProcessDispositionAuditSink audit = Substitute.For<IProcessDispositionAuditSink>();
            ProcessDispositionService disposition = new(authorizer, audit, checkpoints, sweeps);
            (await disposition.RevalidateAsync(principal, source, "retry")).ShouldBeTrue();
            (await health.CheckHealthAsync(new HealthCheckContext())).Status.ShouldBe(HealthStatus.Degraded);
            await audit.Received(1).RecordAsync(principal, source, "retry", Arg.Any<CancellationToken>());

            for (int pass = 0; pass < 10 && !(await sweeps.GetAsync()).Complete; pass++)
                _ = await reconciler.RunPassAsync();
            (await health.CheckHealthAsync(new HealthCheckContext())).Status.ShouldBe(HealthStatus.Healthy);
        }
    }

    /// <summary>Persisted reason totals must agree with unresolved evidence before readiness can use a sweep.</summary>
    [Fact]
    public async Task InconsistentPersistedSweepCountsAreRejected()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new();
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Sweep,
            new ProcessSweepState(1, 1, 1, true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
                null, true, 0, 0, 0, 0, true, true, 0,
                new Dictionary<string, int> { ["parked"] = 1 },
                new Dictionary<string, ProcessPendingDisposition>(), null, null, 0));
        ProcessSweepStore sweeps = new(store, Options.Create(options), TimeProvider.System);

        await Should.ThrowAsync<InvalidOperationException>(() => sweeps.GetAsync());
        (await new ProcessReadinessHealthCheck(sweeps, Options.Create(options), TimeProvider.System)
            .CheckHealthAsync(new HealthCheckContext())).Status.ShouldBe(HealthStatus.Degraded);
    }

    /// <summary>A completion timestamp ahead of the current clock is never fresh.</summary>
    [Fact]
    public async Task FutureCompletionTimestampDegradesReadiness()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Sweep,
            new ProcessSweepState(1, 1, 1, true, now, now.AddMinutes(1), null, true,
                0, 0, 0, 0, true, true, 0, new Dictionary<string, int>(),
                new Dictionary<string, ProcessPendingDisposition>(), null, null, 0));
        ProcessSweepStore sweeps = new(store, Options.Create(options), TimeProvider.System);

        HealthCheckResult result = await new ProcessReadinessHealthCheck(sweeps, Options.Create(options), TimeProvider.System)
            .CheckHealthAsync(new HealthCheckContext());
        result.Status.ShouldBe(HealthStatus.Degraded);
        result.Data["complete"].ShouldBe(true);
        result.Data["reasonCounts"].ShouldBeOfType<Dictionary<string, int>>().ShouldBeEmpty();
    }

    /// <summary>Slow operator adapters time out before disposition or source mutation.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OperatorAdapterTimeoutLeavesSourceUnchanged(bool auditTimeout)
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { OperationTimeout = TimeSpan.FromMilliseconds(100) };
        ProcessCheckpointStore checkpoints = new(store, Options.Create(options), TimeProvider.System);
        ProcessSource source = new("tenant-a", "sample", "operator-timeout");
        await checkpoints.EnsureCandidateAsync(source);
        ProcessSweepStore sweeps = new(store, Options.Create(options), TimeProvider.System);
        IProcessDispositionAuthorizer authorizer = Substitute.For<IProcessDispositionAuthorizer>();
        IProcessDispositionAuditSink audit = Substitute.For<IProcessDispositionAuditSink>();
        var pendingAuthorization = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pendingAudit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        authorizer.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), source, Arg.Any<CancellationToken>())
            .Returns(auditTimeout ? Task.FromResult(true) : pendingAuthorization.Task);
        audit.RecordAsync(Arg.Any<ClaimsPrincipal>(), source, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(auditTimeout ? pendingAudit.Task : Task.CompletedTask);
        ProcessDispositionService disposition = new(authorizer, audit, checkpoints, sweeps, Options.Create(options));
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "operator")], "synthetic"));

        await Should.ThrowAsync<TimeoutException>(() => disposition.RevalidateAsync(principal, source, "retry"));

        (await checkpoints.GetAsync(source)).Status.ShouldBe(ProcessSourceStatus.Active);
        (await sweeps.GetAsync()).Pending.ShouldBeEmpty();
        if (!auditTimeout)
            await audit.DidNotReceive().RecordAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<ProcessSource>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>A retained Active candidate keeps processing after its authoritative catalog entry is removed.</summary>
    [Fact]
    public async Task RetainedActiveSourceExecutesAfterCatalogRemoval()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { SourcesPerPass = 2 };
        ProcessSource source = new("tenant-a", "sample", "retained");
        (ProcessCheckpointStore checkpoints, _, ProcessReconciler reconciler) = Runtime(store, options, Catalog([]), request =>
        {
            StreamReadEvent[] events = request.FromSequence == 1
                ? [new StreamReadEvent(2, "SyntheticEvent", [123], "json", 1, "message-2", null, null, DateTimeOffset.UtcNow, null)]
                : [];
            return new StreamReadPage(request.Tenant, request.Domain, request.AggregateId, events,
                new StreamReadMetadata(request.FromSequence, null, events.Length == 0 ? null : 2, 2, events.Length, false, null));
        });
        await checkpoints.EnsureCandidateAsync(source);
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Source(source),
            new ProcessSourceState(1, source, 1, ProcessSourceStatus.Active, 0, null, null, null, null, 0, 0, DateTimeOffset.UtcNow));
        using (reconciler)
        {
            for (int pass = 0; pass < 12 && (await checkpoints.GetAsync(source)).Checkpoint < 2; pass++)
                _ = await reconciler.RunPassAsync();
        }

        (await checkpoints.GetAsync(source)).Checkpoint.ShouldBe(2);
    }

    /// <summary>An unresponsive catalog consumes its deadline while persisted candidates still execute.</summary>
    [Fact]
    public async Task HungCatalogDoesNotBlockPersistedCandidates()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { SourcesPerPass = 2, OperationTimeout = TimeSpan.FromMilliseconds(100) };
        ProcessSource source = new("tenant-a", "sample", "retained");
        TaskCompletionSource<ProcessSourcePage> held = new(TaskCreationOptions.RunContinuationsAsynchronously);
        IProcessSourceCatalog catalog = Substitute.For<IProcessSourceCatalog>();
        catalog.ListAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => held.Task);
        (ProcessCheckpointStore checkpoints, ProcessSweepStore sweeps, ProcessReconciler reconciler) = Runtime(store, options, catalog);
        await checkpoints.EnsureCandidateAsync(source);
        try
        {
            using (reconciler)
            {
                for (int pass = 0; pass < 12 && (await checkpoints.GetAsync(source)).Checkpoint == 0; pass++)
                    _ = await reconciler.RunPassAsync();
            }

            (await checkpoints.GetAsync(source)).Checkpoint.ShouldBe(1);
            (await sweeps.GetAsync()).Reasons.ShouldContainKey("catalog-unavailable");
        }
        finally
        {
            held.TrySetResult(new ProcessSourcePage([], null));
        }
    }

    /// <summary>A parked source remains discoverable and readiness-degraded over separate complete coverage cycles.</summary>
    [Fact]
    public async Task ParkedCandidatePersistsAlertEvidenceAcrossRecoveryCycles()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { SourcesPerPass = 4 };
        ProcessSource source = new("tenant-a", "sample", "parked");
        (ProcessCheckpointStore checkpoints, ProcessSweepStore sweeps, ProcessReconciler reconciler) = Runtime(store, options, Catalog([]));
        await checkpoints.EnsureCandidateAsync(source);
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Source(source),
            new ProcessSourceState(1, source, 0, ProcessSourceStatus.Parked, 3, null, "source-read", null, null, 0, 0, DateTimeOffset.UtcNow));
        using (reconciler)
        {
            for (int cycle = 1; cycle <= 2; cycle++)
            {
                for (int pass = 0; pass < 10; pass++)
                {
                    if ((await reconciler.RunPassAsync()).CycleComplete) break;
                }

                ProcessSweepState sweep = await sweeps.GetAsync();
                sweep.Cycle.ShouldBe(cycle);
                sweep.Complete.ShouldBeTrue();
                sweep.Unresolved.ShouldBe(1);
                sweep.Reasons["source-read"].ShouldBe(1);
                (await new ProcessReadinessHealthCheck(sweeps, Options.Create(options), TimeProvider.System)
                    .CheckHealthAsync(new HealthCheckContext())).Status.ShouldBe(HealthStatus.Degraded);
            }
        }

        ProcessSourceState state = await checkpoints.GetAsync(source);
        state.Status.ShouldBe(ProcessSourceStatus.Parked);
        state.Attempts.ShouldBe(3);
    }

    /// <summary>Audited disposition clears both parked and quarantined evidence before replaying the same source.</summary>
    [Theory]
    [InlineData(ProcessSourceStatus.Parked, false)]
    [InlineData(ProcessSourceStatus.Parked, true)]
    [InlineData(ProcessSourceStatus.Quarantined, false)]
    [InlineData(ProcessSourceStatus.Quarantined, true)]
    public async Task AuditedDispositionRevalidatesUnresolvedSourceAndReplaysEvidence(ProcessSourceStatus initialStatus, bool invalidEvidence)
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { SourcesPerPass = 4 };
        ProcessSource source = new("tenant-a", "sample", "disposition-replay");
        ProcessCheckpointStore checkpoints = new(store, Options.Create(options), TimeProvider.System);
        await checkpoints.EnsureCandidateAsync(source);
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Source(source),
            new ProcessSourceState(1, source, 0, initialStatus, 3, 1, "invalid-evidence", null, null, 4, 0, DateTimeOffset.UtcNow));
        ProcessSourceState before = await checkpoints.GetAsync(source);
        IProcessDispositionAuthorizer authorizer = Substitute.For<IProcessDispositionAuthorizer>();
        authorizer.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), source, Arg.Any<CancellationToken>()).Returns(true);
        IProcessDispositionAuditSink audit = Substitute.For<IProcessDispositionAuditSink>();
        ProcessSweepStore sweeps = new(store, Options.Create(options), TimeProvider.System);
        ProcessDispositionService disposition = new(authorizer, audit, checkpoints, sweeps);
        ClaimsPrincipal principal = new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "operator")], "synthetic"));

        (await disposition.RevalidateAsync(principal, source, "retry")).ShouldBeTrue();
        await audit.Received(1).RecordAsync(principal, source, "retry", Arg.Any<CancellationToken>());
        ProcessSourceState prepared = await checkpoints.GetAsync(source);
        prepared.Status.ShouldBe(ProcessSourceStatus.Revalidating);
        prepared.Attempts.ShouldBe(0);
        prepared.ReasonCode.ShouldBeNull();
        prepared.FailureSequence.ShouldBeNull();
        prepared.Fence.ShouldBeGreaterThan(before.Fence);

        IProcessTranslator invalidTranslator = Substitute.For<IProcessTranslator>();
        invalidTranslator.TranslateAsync(Arg.Any<ProcessSource>(), Arg.Any<StreamReadEvent>(), Arg.Any<CancellationToken>())
            .Returns(_ => invalidEvidence
                ? ValueTask.FromException<IReadOnlyList<ProcessEffect>>(new InvalidOperationException("Unsupported synthetic event."))
                : new ValueTask<IReadOnlyList<ProcessEffect>>([]));
        (_, _, ProcessReconciler reconciler) = Runtime(store, options, Catalog([]), request =>
            new StreamReadPage(request.Tenant, request.Domain, request.AggregateId,
                [new StreamReadEvent(1, "SyntheticEvent", [1], "json", 1, "message-1", null, null, DateTimeOffset.UtcNow, null)],
                new StreamReadMetadata(request.FromSequence, null, 1, 1, 1, false, null)), invalidTranslator);
        using (reconciler)
        {
            for (int pass = 0; pass < 12; pass++)
            {
                _ = await reconciler.RunPassAsync();
                ProcessSourceState current = await checkpoints.GetAsync(source);
                if (current.Status == ProcessSourceStatus.Quarantined || current.Checkpoint == 1) break;
            }
        }

        ProcessSourceState replayed = await checkpoints.GetAsync(source);
        replayed.Status.ShouldBe(invalidEvidence ? ProcessSourceStatus.Quarantined : ProcessSourceStatus.Active);
        replayed.ReasonCode.ShouldBe(invalidEvidence ? "invalid-evidence" : null);
        replayed.FailureSequence.ShouldBe(invalidEvidence ? 1 : null);
        replayed.Checkpoint.ShouldBe(invalidEvidence ? 0 : 1);
    }

    /// <summary>Wrong-tenant denial and failed audit leave both source and sweep records untouched.</summary>
    [Fact]
    public async Task WrongTenantAndAuditFailureDoNotMutateDurableProcessState()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new();
        ProcessSource source = new("tenant-a", "sample", "source");
        ProcessCheckpointStore checkpoints = new(store, Options.Create(options), TimeProvider.System);
        ProcessSweepStore sweeps = new(store, Options.Create(options), TimeProvider.System);
        await checkpoints.EnsureCandidateAsync(source);
        long initialGeneration = (await sweeps.GetAsync()).Generation;
        IProcessDispositionAuthorizer authorizer = Substitute.For<IProcessDispositionAuthorizer>();
        authorizer.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), source, Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<ClaimsPrincipal>().FindFirst("tenant")?.Value == source.Tenant);
        IProcessDispositionAuditSink audit = Substitute.For<IProcessDispositionAuditSink>();
        ProcessDispositionService disposition = new(authorizer, audit, checkpoints, sweeps);
        ClaimsPrincipal wrongTenant = new(new ClaimsIdentity([new Claim("tenant", "tenant-b")], "synthetic"));

        await Should.ThrowAsync<UnauthorizedAccessException>(() => disposition.RevalidateAsync(wrongTenant, source, "retry"));
        await audit.DidNotReceive().RecordAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<ProcessSource>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        ClaimsPrincipal correctTenant = new(new ClaimsIdentity([new Claim("tenant", source.Tenant)], "synthetic"));
        audit.RecordAsync(correctTenant, source, "retry", Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Synthetic audit outage.")));
        await Should.ThrowAsync<InvalidOperationException>(() => disposition.RevalidateAsync(correctTenant, source, "retry"));

        (await store.GetAsync<ProcessSourceState>(options.StateStoreName, ProcessStateKeys.Source(source))).Value.ShouldBeNull();
        ProcessSweepState sweep = await sweeps.GetAsync();
        sweep.Generation.ShouldBe(initialGeneration);
        sweep.Pending.ShouldBeEmpty();
    }

    /// <summary>Neither an unpublished source nor an unfinished publication may enter operator disposition.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnpublishedSourceDispositionDoesNotAuditOrPrepare(bool pendingPublication)
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new();
        ProcessSource source = new("tenant-a", "sample", "unpublished");
        ProcessCheckpointStore checkpoints = new(store, Options.Create(options), TimeProvider.System);
        ProcessSweepStore sweeps = new(store, Options.Create(options), TimeProvider.System);
        if (pendingPublication)
        {
            FaultInjectingProcessStore crashing = new(store, (key, value) =>
                key == ProcessStateKeys.TenantMarker(source.Tenant) && value is ProcessTenantMarker);
            await Should.ThrowAsync<InvalidOperationException>(() =>
                new ProcessCheckpointStore(crashing, Options.Create(options), TimeProvider.System).EnsureCandidateAsync(source));
            crashing.Fired.ShouldBeTrue();
            (await store.GetAsync<ProcessCandidateMarker>(options.StateStoreName, ProcessStateKeys.CandidateMarker(source))).Value.ShouldNotBeNull();
            (await store.GetAsync<ProcessTenantMarker>(options.StateStoreName, ProcessStateKeys.TenantMarker(source.Tenant))).Value.ShouldNotBeNull();
            (await sweeps.GetAsync()).PendingPublications.Values.ShouldContain(source);
            (await checkpoints.ContainsCandidateAsync(source)).ShouldBeFalse();
        }
        IProcessDispositionAuthorizer authorizer = Substitute.For<IProcessDispositionAuthorizer>();
        authorizer.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), source, Arg.Any<CancellationToken>()).Returns(true);
        IProcessDispositionAuditSink audit = Substitute.For<IProcessDispositionAuditSink>();
        ProcessDispositionService disposition = new(authorizer, audit, checkpoints, sweeps);
        ClaimsPrincipal principal = new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "operator")], "synthetic"));

        await Should.ThrowAsync<UnauthorizedAccessException>(() => disposition.RevalidateAsync(principal, source, "retry"));

        await audit.DidNotReceive().RecordAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<ProcessSource>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        (await sweeps.GetAsync()).Pending.ShouldBeEmpty();
        (await store.GetAsync<ProcessSourceState>(options.StateStoreName, ProcessStateKeys.Source(source))).Value.ShouldBeNull();
    }

    /// <summary>A live source owner blocks disposition mutation until a later repair pass can claim it.</summary>
    [Fact]
    public async Task LiveOwnerDefersDispositionAndLaterPassAppliesIt()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { SourcesPerPass = 2 };
        ProcessSource source = new("tenant-a", "sample", "busy-disposition");
        ProcessCheckpointStore checkpoints = new(store, Options.Create(options), TimeProvider.System);
        ProcessSweepStore sweeps = new(store, Options.Create(options), TimeProvider.System);
        await checkpoints.EnsureCandidateAsync(source);
        ProcessSourceState owner = (await checkpoints.AcquireAsync(source)).ShouldNotBeNull();
        IProcessDispositionAuthorizer authorizer = Substitute.For<IProcessDispositionAuthorizer>();
        authorizer.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), source, Arg.Any<CancellationToken>()).Returns(true);
        IProcessDispositionAuditSink audit = Substitute.For<IProcessDispositionAuditSink>();
        ProcessDispositionService disposition = new(authorizer, audit, checkpoints, sweeps);
        ClaimsPrincipal principal = new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "operator")], "synthetic"));

        (await disposition.RevalidateAsync(principal, source, "retry")).ShouldBeFalse();
        ProcessSourceState beforeRelease = await checkpoints.GetAsync(source);
        beforeRelease.Fence.ShouldBe(owner.Fence);
        beforeRelease.Owner.ShouldBe(owner.Owner);
        (await sweeps.GetAsync()).Pending.Values.Single().Applied.ShouldBeFalse();

        await checkpoints.ReleaseAsync(owner);
        (_, _, ProcessReconciler reconciler) = Runtime(store, options, Catalog([]));
        using (reconciler)
        {
            for (int pass = 0; pass < 8 && (await checkpoints.GetAsync(source)).LastDispositionGeneration == 0; pass++)
                _ = await reconciler.RunPassAsync();
        }

        (await checkpoints.GetAsync(source)).LastDispositionGeneration.ShouldBeGreaterThan(0);
        (await checkpoints.GetAsync(source)).Fence.ShouldBeGreaterThan(owner.Fence);
    }

    /// <summary>Reconciler repair releases a failed disposition claim immediately when a live source owner wins.</summary>
    [Fact]
    public async Task BusyDispositionRepairReleasesClaimForNextPass()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { SourcesPerPass = 2 };
        ProcessSource source = new("tenant-a", "sample", "busy-repair");
        ProcessCheckpointStore checkpoints = new(store, Options.Create(options), TimeProvider.System);
        ProcessSweepStore sweeps = new(store, Options.Create(options), TimeProvider.System);
        await checkpoints.EnsureCandidateAsync(source);
        ProcessSourceState owner = (await checkpoints.AcquireAsync(source)).ShouldNotBeNull();
        long generation = await sweeps.PrepareDispositionAsync(source);
        (_, _, ProcessReconciler reconciler) = Runtime(store, options, Catalog([]));
        using (reconciler)
        {
            _ = await reconciler.RunPassAsync();
            ProcessPendingDisposition pending = (await sweeps.GetAsync()).Pending[ProcessStateKeys.SourceId(source)];
            pending.PreparedGeneration.ShouldBe(generation);
            pending.ClaimToken.ShouldBeNull();
            pending.ClaimUntil.ShouldBeNull();
            await checkpoints.ReleaseAsync(owner);
            _ = await reconciler.RunPassAsync();
        }
        (await checkpoints.GetAsync(source)).LastDispositionGeneration.ShouldBe(generation);
        (await sweeps.GetAsync()).Pending.ShouldNotContainKey(ProcessStateKeys.SourceId(source));
    }

    /// <summary>Every audited revalidation gets a fresh sweep generation even while the same source is pending.</summary>
    [Fact]
    public async Task RepeatedDispositionInvalidatesPreparedAndAppliedPendingStates()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new();
        ProcessSource source = new("tenant-a", "sample", "source");
        ProcessCheckpointStore checkpoints = new(store, Options.Create(options), TimeProvider.System);
        ProcessSweepStore sweeps = new(store, Options.Create(options), TimeProvider.System);
        await checkpoints.EnsureCandidateAsync(source);

        long firstPreparation = await sweeps.PrepareDispositionAsync(source);
        long preparedGeneration = (await sweeps.GetAsync()).Generation;
        long secondPreparation = await sweeps.PrepareDispositionAsync(source);
        ProcessSweepState preparedAgain = await sweeps.GetAsync();
        preparedAgain.Generation.ShouldBeGreaterThan(preparedGeneration);
        preparedAgain.Pending.Values.Single().Applied.ShouldBeFalse();
        (await sweeps.ClaimDispositionAsync(source, firstPreparation)).ShouldBeNull();
        ProcessPendingDisposition claim = (await sweeps.ClaimDispositionAsync(source, secondPreparation)).ShouldNotBeNull();
        (await checkpoints.RevalidateAsync(claim)).ShouldBeTrue();
        (await sweeps.MarkDispositionAppliedAsync(claim)).ShouldBeTrue();
        long appliedGeneration = (await sweeps.GetAsync()).Generation;
        (await sweeps.GetAsync()).Pending.Values.Single().Applied.ShouldBeTrue();

        ClaimsPrincipal principal = new(new ClaimsIdentity([new Claim("tenant", source.Tenant)], "synthetic"));
        IProcessDispositionAuthorizer authorizer = Substitute.For<IProcessDispositionAuthorizer>();
        authorizer.AuthorizeAsync(principal, source, Arg.Any<CancellationToken>()).Returns(true);
        IProcessDispositionAuditSink audit = Substitute.For<IProcessDispositionAuditSink>();
        ProcessDispositionService disposition = new(authorizer, audit, checkpoints, sweeps);
        (await disposition.RevalidateAsync(principal, source, "retry-again")).ShouldBeTrue();

        ProcessSweepState repeated = await sweeps.GetAsync();
        repeated.Generation.ShouldBeGreaterThan(appliedGeneration);
        repeated.Complete.ShouldBeFalse();
        repeated.Pending.Values.Single().Applied.ShouldBeTrue();
        repeated.Pending.Values.Single().AppliedGeneration.ShouldBe(repeated.Generation);
        (await new ProcessReadinessHealthCheck(sweeps, Options.Create(options), TimeProvider.System)
            .CheckHealthAsync(new HealthCheckContext())).Status.ShouldBe(HealthStatus.Degraded);
        await audit.Received(1).RecordAsync(principal, source, "retry-again", Arg.Any<CancellationToken>());
    }

    /// <summary>A crash after sweep counting but before the source marker replays without double counting.</summary>
    [Fact]
    public async Task CountedObservationCrashReplaysExactlyOnceWithinCycle()
    {
        InMemoryReadModelStore persisted = new();
        EventStoreProcessOptions options = new() { SourcesPerPass = 1 };
        ProcessSource source = new("tenant-a", "sample", "parked");
        persisted.SeedRaw(options.StateStoreName, ProcessStateKeys.Source(source),
            new ProcessSourceState(1, source, 0, ProcessSourceStatus.Parked, 3, null, "source-read", null, null, 0, 0, DateTimeOffset.UtcNow));
        FaultOnObservationStore faulting = new(persisted, null, 1);
        (_, ProcessSweepStore sweeps, ProcessReconciler first) = Runtime(faulting, options, Catalog([source]));
        using (first)
        {
            (await first.RunPassAsync()).CycleComplete.ShouldBeFalse();
        }

        ProcessSweepState interrupted = await sweeps.GetAsync();
        interrupted.Unresolved.ShouldBe(1);
        interrupted.PendingObservations.Values.Single().Source.ShouldBe(source);
        (await persisted.GetAsync<ProcessSourceState>(options.StateStoreName, ProcessStateKeys.Source(source))).Value.ShouldNotBeNull().ObservedCycle.ShouldBe(0);
        (ProcessCheckpointStore checkpoints, ProcessSweepStore recoveredSweeps, ProcessReconciler restarted) = Runtime(persisted, options, Catalog([source]));
        using (restarted)
        {
            for (int pass = 0; pass < 10 && !(await recoveredSweeps.GetAsync()).Complete; pass++)
                _ = await restarted.RunPassAsync();
        }

        ProcessSweepState complete = await recoveredSweeps.GetAsync();
        complete.Complete.ShouldBeTrue();
        complete.Cycle.ShouldBe(interrupted.Cycle);
        complete.Unresolved.ShouldBe(1);
        complete.Reasons["source-read"].ShouldBe(1);
        complete.PendingObservations.ShouldBeEmpty();
        (await checkpoints.GetAsync(source)).ObservedCycle.ShouldBe(complete.Cycle);
    }

    /// <summary>Finishing a pending marker uses the sole source turn in a one-source pass.</summary>
    [Fact]
    public async Task PendingObservationConsumesOneSourcePassBudget()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { SourcesPerPass = 1 };
        ProcessSource prior = new("tenant-a", "sample", "prior");
        ProcessSource later = new("tenant-b", "sample", "later");
        ProcessSweepState seed = new(1, 1, 1, false, DateTimeOffset.UtcNow, null, null, false,
            0, 0, 0, 0, false, true, 0, new Dictionary<string, int>(),
            new Dictionary<string, ProcessPendingDisposition>(), null, null, 1)
        {
            PendingObservations = new Dictionary<string, ProcessPendingObservation>
            {
                [ProcessStateKeys.SourceId(prior)] = new(prior, 1, false),
            },
        };
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Sweep, seed);
        IProcessSourceCatalog catalog = Catalog([later]);
        (ProcessCheckpointStore checkpoints, ProcessSweepStore sweeps, ProcessReconciler reconciler) = Runtime(store, options, catalog);
        using (reconciler)
        {
            _ = await reconciler.RunPassAsync();
            await catalog.DidNotReceive().ListAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
            (await checkpoints.GetAsync(prior)).ObservedCycle.ShouldBe(1);
            (await sweeps.GetAsync()).PendingObservations.ShouldBeEmpty();

            for (int pass = 0; pass < 8 && (await checkpoints.GetAsync(later)).Checkpoint == 0; pass++)
                _ = await reconciler.RunPassAsync();
        }

        (await checkpoints.GetAsync(later)).Checkpoint.ShouldBe(1);
    }

    /// <summary>A marker write that stays unavailable leaves a pending count but does not hold later catalog sources.</summary>
    [Fact]
    public async Task UnavailableObservationMarkerDoesNotBlockLaterSource()
    {
        InMemoryReadModelStore persisted = new();
        EventStoreProcessOptions options = new() { SourcesPerPass = 4 };
        ProcessSource first = new("tenant-a", "sample", "a-parked");
        ProcessSource later = new("tenant-a", "sample", "b-active");
        persisted.SeedRaw(options.StateStoreName, ProcessStateKeys.Source(first),
            new ProcessSourceState(1, first, 0, ProcessSourceStatus.Parked, 3, null, "source-read", null, null, 0, 0, DateTimeOffset.UtcNow));
        FaultOnObservationStore faulting = new(persisted, first, int.MaxValue);
        (ProcessCheckpointStore checkpoints, ProcessSweepStore sweeps, ProcessReconciler reconciler) = Runtime(faulting, options, Catalog([first, later]));
        using (reconciler)
        {
            for (int pass = 0; pass < 10 && (await checkpoints.GetAsync(later)).Checkpoint == 0; pass++)
                _ = await reconciler.RunPassAsync();
        }

        (await checkpoints.GetAsync(later)).Checkpoint.ShouldBe(1);
        ProcessSweepState sweep = await sweeps.GetAsync();
        sweep.PendingObservations.Values.Single().Source.ShouldBe(first);
        sweep.Unresolved.ShouldBe(1);
        sweep.Complete.ShouldBeFalse();
        (await new ProcessReadinessHealthCheck(sweeps, Options.Create(options), TimeProvider.System)
            .CheckHealthAsync(new HealthCheckContext())).Status.ShouldBe(HealthStatus.Degraded);
    }

    /// <summary>A full serialized sweep budget pauses at the cursor and resumes after one marker is recovered.</summary>
    [Fact]
    public async Task PendingObservationCapacityPausesAndResumesAtDurableCursor()
    {
        InMemoryReadModelStore persisted = new();
        EventStoreProcessOptions options = new() { SourcesPerPass = 1 };
        ProcessSource later = new("tenant-z", "sample", "later");
        var pending = new Dictionary<string, ProcessPendingObservation>(StringComparer.Ordinal);
        ProcessSweepState seed = new(1, 1, 1, false, DateTimeOffset.UtcNow, null, null, false,
            0, 0, 0, 0, false, true, 0, new Dictionary<string, int>(),
            new Dictionary<string, ProcessPendingDisposition>(), null, null, 0);
        for (int index = 0; index < 4096; index++)
        {
            ProcessSource source = new("tenant-a", "sample", $"item-{index:0000}");
            pending.Add(ProcessStateKeys.SourceId(source), new ProcessPendingObservation(source, 1, false));
            if ((seed with { PendingObservations = pending }).SerializedSize() >= ProcessSweepState.MaxSerializedBytes - 8192)
                break;
        }

        persisted.SeedRaw(options.StateStoreName, ProcessStateKeys.Sweep, seed with { PendingObservations = pending });
        IProcessSourceCatalog catalog = Catalog([later]);
        FaultOnObservationStore faulting = new(persisted, null, int.MaxValue);
        (_, ProcessSweepStore firstSweeps, ProcessReconciler first) = Runtime(faulting, options, catalog);
        using (first)
        {
            (await first.RunPassAsync()).CycleComplete.ShouldBeFalse();
        }

        ProcessSweepState paused = await firstSweeps.GetAsync();
        paused.PendingObservations.Count.ShouldBe(pending.Count);
        paused.CatalogAfterKey.ShouldBeNull();
        paused.Complete.ShouldBeFalse();
        (ProcessCheckpointStore checkpoints, ProcessSweepStore recoveredSweeps, ProcessReconciler restarted) = Runtime(persisted, options, catalog);
        using (restarted)
        {
            for (int pass = 0; pass < 12 && (await checkpoints.GetAsync(later)).Checkpoint == 0; pass++)
                _ = await restarted.RunPassAsync();
        }

        (await checkpoints.GetAsync(later)).Checkpoint.ShouldBe(1);
        (await recoveredSweeps.GetAsync()).PendingObservations.Count.ShouldBeLessThan(pending.Count);
    }

    /// <summary>Catalog and candidate discovery attempt one source only once in a cycle.</summary>
    [Fact]
    public async Task CatalogAndCandidateVisitsAttemptSourceOncePerCycle()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { SourcesPerPass = 4 };
        ProcessSource source = new("tenant-a", "sample", "source");
        int reads = 0;
        (ProcessCheckpointStore checkpoints, ProcessSweepStore sweeps, ProcessReconciler reconciler) = Runtime(store, options, Catalog([source]), request =>
        {
            if (++reads > 1) throw new HttpRequestException("Synthetic read outage after an earlier observation.");
            return new StreamReadPage(request.Tenant, request.Domain, request.AggregateId, [],
                new StreamReadMetadata(request.FromSequence, null, null, 0, 0, false, null));
        });
        using (reconciler)
        {
            for (int pass = 0; pass < 10 && !(await sweeps.GetAsync()).Complete; pass++)
                _ = await reconciler.RunPassAsync();
        }

        reads.ShouldBe(1);
        (await checkpoints.GetAsync(source)).Status.ShouldBe(ProcessSourceStatus.Active);
        ProcessSweepState sweep = await sweeps.GetAsync();
        sweep.Complete.ShouldBeTrue();
        sweep.Unresolved.ShouldBe(0);
        (await new ProcessReadinessHealthCheck(sweeps, Options.Create(options), TimeProvider.System)
            .CheckHealthAsync(new HealthCheckContext())).Status.ShouldBe(HealthStatus.Healthy);
    }

    /// <summary>An active record held by another runner is unresolved until a later fresh cycle completes it.</summary>
    [Fact]
    public async Task BusyActiveOwnerCannotYieldHealthyCoverage()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { SourcesPerPass = 4 };
        ProcessSource source = new("tenant-a", "sample", "busy");
        (ProcessCheckpointStore checkpoints, ProcessSweepStore sweeps, ProcessReconciler reconciler) = Runtime(store, options, Catalog([source]));
        await checkpoints.EnsureCandidateAsync(source);
        ProcessSourceState owner = (await checkpoints.AcquireAsync(source)).ShouldNotBeNull();
        using (reconciler)
        {
            for (int pass = 0; pass < 10 && !(await sweeps.GetAsync()).Complete; pass++)
                _ = await reconciler.RunPassAsync();
            ProcessSweepState busy = await sweeps.GetAsync();
            busy.Complete.ShouldBeTrue();
            busy.Unresolved.ShouldBe(1);
            busy.Reasons["source-busy"].ShouldBe(1);
            (await checkpoints.GetAsync(source)).Checkpoint.ShouldBe(0);
            (await new ProcessReadinessHealthCheck(sweeps, Options.Create(options), TimeProvider.System)
                .CheckHealthAsync(new HealthCheckContext())).Status.ShouldBe(HealthStatus.Degraded);
            await checkpoints.ReleaseAsync(owner);
            for (int pass = 0; pass < 10 && (await checkpoints.GetAsync(source)).Checkpoint == 0; pass++)
                _ = await reconciler.RunPassAsync();
        }

        (await checkpoints.GetAsync(source)).Checkpoint.ShouldBe(1);
    }

    /// <summary>A missing registry root cannot hide retained work behind a healthy sweep.</summary>
    [Fact]
    public async Task MissingTenantRootWithRetainedCandidatesDegradesReadiness()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { SourcesPerPass = 4 };
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Sweep,
            new ProcessSweepState(1, 0, 0, false, null, null, null, false, 0, 0, 0, 0, false, true,
                0, new Dictionary<string, int>(), new Dictionary<string, ProcessPendingDisposition>(), null, null, 0)
            { RetainedCandidates = 1 });
        (_, ProcessSweepStore sweeps, ProcessReconciler reconciler) = Runtime(store, options, Catalog([]));
        using (reconciler)
        {
            for (int pass = 0; pass < 10 && !(await sweeps.GetAsync()).Complete; pass++)
                _ = await reconciler.RunPassAsync();
        }

        ProcessSweepState complete = await sweeps.GetAsync();
        complete.Complete.ShouldBeTrue();
        complete.Reasons["tenant-page-missing"].ShouldBe(1);
        (await new ProcessReadinessHealthCheck(sweeps, Options.Create(options), TimeProvider.System)
            .CheckHealthAsync(new HealthCheckContext())).Status.ShouldBe(HealthStatus.Degraded);
    }

    /// <summary>One failing source visited by both indexes contributes one attempt and reason.</summary>
    [Fact]
    public async Task CatalogAndCandidateFailureCountsSourceOnce()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { SourcesPerPass = 4 };
        ProcessSource source = new("tenant-a", "sample", "failing");
        int reads = 0;
        (ProcessCheckpointStore checkpoints, ProcessSweepStore sweeps, ProcessReconciler reconciler) = Runtime(store, options, Catalog([source]), _ =>
        {
            reads++;
            throw new HttpRequestException("Synthetic read outage.");
        });
        using (reconciler)
        {
            for (int pass = 0; pass < 10 && !(await sweeps.GetAsync()).Complete; pass++)
                _ = await reconciler.RunPassAsync();
        }

        reads.ShouldBe(1);
        (await checkpoints.GetAsync(source)).Attempts.ShouldBe(1);
        ProcessSweepState complete = await sweeps.GetAsync();
        complete.Unresolved.ShouldBe(1);
        complete.Reasons["source-read"].ShouldBe(1);
    }

    /// <summary>A lease past the validated maximum cannot freeze every replica.</summary>
    [Fact]
    public async Task FarFutureSweepLeaseIsTakenOver()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new();
        TimeProvider clock = TimeProvider.System;
        ProcessSweepStore sweeps = new(store, Options.Create(options), clock);
        ProcessSweepState owned = (await sweeps.AcquireAsync()).ShouldNotBeNull();
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Sweep, owned with
        {
            LeaseUntil = clock.GetUtcNow().AddHours(2),
        });

        ProcessSweepState taken = (await sweeps.AcquireAsync()).ShouldNotBeNull();
        taken.Owner.ShouldNotBe(owned.Owner);
    }

    /// <summary>A lease inside the validated maximum still belongs to its owner.</summary>
    [Fact]
    public async Task LegalSweepLeaseBlocksAnotherReplica()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new();
        TimeProvider clock = TimeProvider.System;
        ProcessSweepStore sweeps = new(store, Options.Create(options), clock);
        ProcessSweepState owned = (await sweeps.AcquireAsync()).ShouldNotBeNull();
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Sweep, owned with
        {
            LeaseUntil = clock.GetUtcNow().AddMinutes(10),
        });

        (await sweeps.AcquireAsync()).ShouldBeNull();
    }

    /// <summary>A long valid source run renews the sweep lease until its checkpoint is covered.</summary>
    [Fact]
    public async Task LongSourceRunRenewsSweepOwnership()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new()
        {
            SourcesPerPass = 4,
            SupportedCandidateCount = 1,
            MaxCommandBytesPerPage = 2 * 1024 * 1024,
            OperationTimeout = TimeSpan.FromMilliseconds(500),
            LeaseDuration = TimeSpan.FromMilliseconds(1500),
        };
        options.Validate().ShouldBeEmpty();
        ProcessSource source = new("tenant-a", "sample", "slow");
        IProcessTranslator translator = Substitute.For<IProcessTranslator>();
        translator.TranslateAsync(Arg.Any<ProcessSource>(), Arg.Any<StreamReadEvent>(), Arg.Any<CancellationToken>())
            .Returns(call => new ValueTask<IReadOnlyList<ProcessEffect>>(TranslateSlowlyAsync(call.Arg<CancellationToken>())));
        (ProcessCheckpointStore checkpoints, ProcessSweepStore sweeps, ProcessReconciler reconciler) = Runtime(store, options, Catalog([source]),
            request => new StreamReadPage(request.Tenant, request.Domain, request.AggregateId,
                Enumerable.Range(1, 7).Select(position => new StreamReadEvent(position, "SyntheticEvent", [1], "json", 1,
                    $"message-{position}", null, null, DateTimeOffset.UtcNow, null)).ToArray(),
                new StreamReadMetadata(request.FromSequence, null, 7, 7, 7, false, null)), translator);
        using (reconciler)
        {
            for (int pass = 0; pass < 10 && !(await sweeps.GetAsync()).Complete; pass++)
                _ = await reconciler.RunPassAsync();
        }

        (await checkpoints.GetAsync(source)).Checkpoint.ShouldBe(7);
        (await sweeps.GetAsync()).Complete.ShouldBeTrue();
    }

    private static async Task<IReadOnlyList<ProcessEffect>> TranslateSlowlyAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(250, cancellationToken);
        return [];
    }

    /// <summary>A stale worker cannot reapply a disposition after another worker applied and covered its generation.</summary>
    [Fact]
    public async Task StaleDispositionClaimCannotMutateFreshlyCoveredSource()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { SourcesPerPass = 4 };
        ProcessSource source = new("tenant-a", "sample", "source");
        (ProcessCheckpointStore checkpoints, ProcessSweepStore sweeps, ProcessReconciler reconciler) = Runtime(store, options, Catalog([]));
        await checkpoints.EnsureCandidateAsync(source);
        long generation = await sweeps.PrepareDispositionAsync(source);
        ProcessPendingDisposition claim = (await sweeps.ClaimDispositionAsync(source, generation)).ShouldNotBeNull();
        (await checkpoints.RevalidateAsync(claim)).ShouldBeTrue();
        (await sweeps.MarkDispositionAppliedAsync(claim)).ShouldBeTrue();
        using (reconciler)
        {
            for (int attempt = 0; attempt < 20 && (await sweeps.GetAsync()).Pending.Count != 0; attempt++)
                _ = await reconciler.RunPassAsync();
        }
        (await sweeps.GetAsync()).Pending.ShouldBeEmpty();
        ProcessSourceState covered = await checkpoints.GetAsync(source);

        (await sweeps.ClaimDispositionAsync(source, generation)).ShouldBeNull();
        (await checkpoints.RevalidateAsync(claim)).ShouldBeFalse();
        (await checkpoints.GetAsync(source)).ShouldBe(covered);
    }

    /// <summary>A direct runner can expose retry evidence only after invalidating a healthy sweep.</summary>
    [Fact]
    public async Task DirectRunnerFailureInvalidatesHealthyCoverage()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { SourcesPerPass = 4 };
        ProcessSource source = new("tenant-a", "sample", "source");
        (ProcessCheckpointStore checkpoints, ProcessSweepStore sweeps, ProcessReconciler reconciler) = Runtime(store, options, Catalog([]));
        await checkpoints.EnsureCandidateAsync(source);
        using (reconciler)
        {
            for (int attempt = 0; attempt < 20 && !(await sweeps.GetAsync()).Complete; attempt++)
                _ = await reconciler.RunPassAsync();
        }
        (await new ProcessReadinessHealthCheck(sweeps, Options.Create(options), TimeProvider.System)
            .CheckHealthAsync(new HealthCheckContext())).Status.ShouldBe(HealthStatus.Healthy);
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<StreamReadPage>(new HttpRequestException("Synthetic outage.")));
        CheckpointedProcessRunner direct = new(gateway, Substitute.For<IProcessTranslator>(),
            Substitute.For<IProcessDelegationProvider>(), Substitute.For<ITrustedEffectSubmitter>(), checkpoints, Options.Create(options));

        (await direct.RunAsync(source)).Status.ShouldBe(ProcessSourceStatus.Retrying);

        (await sweeps.GetAsync()).Complete.ShouldBeFalse();
        (await new ProcessReadinessHealthCheck(sweeps, Options.Create(options), TimeProvider.System)
            .CheckHealthAsync(new HealthCheckContext())).Status.ShouldBe(HealthStatus.Degraded);
    }

    /// <summary>Direct runner coverage is degraded while its first source transition is still paused before CAS.</summary>
    [Fact]
    public async Task DirectRunnerInvalidatesBeforeFirstSourceMutation()
    {
        InMemoryReadModelStore inner = new();
        EventStoreProcessOptions options = new() { OperationTimeout = TimeSpan.FromSeconds(5), SourcesPerPass = 4 };
        ProcessSource source = new("tenant-a", "sample", "direct-order");
        ProcessCheckpointStore initial = new(inner, Options.Create(options), TimeProvider.System);
        await initial.EnsureCandidateAsync(source);
        (_, ProcessSweepStore sweeps, ProcessReconciler reconciler) = Runtime(inner, options, Catalog([]), request =>
            new StreamReadPage(request.Tenant, request.Domain, request.AggregateId, [],
                new StreamReadMetadata(request.FromSequence, null, null, 0, 0, false, null)));
        using (reconciler)
        {
            for (int pass = 0; pass < 10 && !(await sweeps.GetAsync()).Complete; pass++)
                _ = await reconciler.RunPassAsync();
        }
        ProcessReadinessHealthCheck health = new(sweeps, Options.Create(options), TimeProvider.System);
        (await health.CheckHealthAsync(new HealthCheckContext())).Status.ShouldBe(HealthStatus.Healthy);
        PausingProcessSourceStore paused = new(inner, ProcessStateKeys.Source(source));
        ProcessCheckpointStore checkpoints = new(paused, Options.Create(options), TimeProvider.System);
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
            Task.FromResult(new StreamReadPage(source.Tenant, source.Domain, source.Aggregate,
                [new StreamReadEvent(1, "SyntheticEvent", [1], "json", 1, "message-1", null, null, DateTimeOffset.UtcNow, null)],
                new StreamReadMetadata(call.Arg<StreamReadRequest>().FromSequence, null, 1, 1, 1, false, null))));
        IProcessTranslator translator = Substitute.For<IProcessTranslator>();
        translator.TranslateAsync(Arg.Any<ProcessSource>(), Arg.Any<StreamReadEvent>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IReadOnlyList<ProcessEffect>>([]));
        CheckpointedProcessRunner direct = new(gateway, translator, Substitute.For<IProcessDelegationProvider>(),
            Substitute.For<ITrustedEffectSubmitter>(), checkpoints, Options.Create(options));

        Task<ProcessRunResult> running = direct.RunAsync(source);
        try
        {
            await paused.Paused.WaitAsync(TimeSpan.FromSeconds(3));
            (await initial.GetAsync(source)).Status.ShouldBe(ProcessSourceStatus.Active);
            (await health.CheckHealthAsync(new HealthCheckContext())).Status.ShouldBe(HealthStatus.Degraded);
            (await sweeps.GetAsync()).PendingTransitions.ShouldContainKey(ProcessStateKeys.SourceId(source));
        }
        finally
        {
            paused.Release();
        }
        (await running).Checkpoint.ShouldBe(1);
    }

    /// <summary>Cancelling an in-flight recovery pass leaves its source evidence and checkpoint intact.</summary>
    [Fact]
    public async Task CancelledRecoveryPassDoesNotConsumeRetryBudget()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { SourcesPerPass = 4, OperationTimeout = TimeSpan.FromSeconds(5) };
        ProcessSource source = new("tenant-a", "sample", "cancelled-pass");
        ProcessCheckpointStore checkpoints = new(store, Options.Create(options), TimeProvider.System);
        await checkpoints.EnsureCandidateAsync(source);
        ProcessSourceState initial = new(1, source, 0, ProcessSourceStatus.Incomplete, 0, null, null,
            null, null, 0, 0, DateTimeOffset.UtcNow);
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Source(source), initial);
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        bool resume = false;
        IProcessTranslator translator = Substitute.For<IProcessTranslator>();
        translator.TranslateAsync(Arg.Any<ProcessSource>(), Arg.Any<StreamReadEvent>(), Arg.Any<CancellationToken>())
            .Returns(call => resume
                ? new ValueTask<IReadOnlyList<ProcessEffect>>([])
                : new ValueTask<IReadOnlyList<ProcessEffect>>(WaitForTranslationCancellationAsync(call.Arg<CancellationToken>(), entered)));
        (_, ProcessSweepStore sweeps, ProcessReconciler reconciler) = Runtime(store, options, Catalog([source]), processTranslator: translator);
        using (reconciler)
        using (CancellationTokenSource cancellation = new())
        {
            Task<ProcessReconciliationPass> running = reconciler.RunPassAsync(cancellation.Token);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            cancellation.Cancel();
            await Should.ThrowAsync<OperationCanceledException>(() => running);
            ProcessSourceState after = await checkpoints.GetAsync(source);
            after.Checkpoint.ShouldBe(initial.Checkpoint);
            after.Status.ShouldBe(initial.Status);
            after.Attempts.ShouldBe(initial.Attempts);
            after.ReasonCode.ShouldBe(initial.ReasonCode);
            after.Owner.ShouldBeNull();
            after.LeaseUntil.ShouldBeNull();
            ProcessSweepState releasedSweep = await sweeps.GetAsync();
            releasedSweep.Complete.ShouldBeFalse();
            releasedSweep.Owner.ShouldBeNull();
            releasedSweep.LeaseUntil.ShouldBeNull();
            ProcessSourceState reacquiredSource = (await checkpoints.AcquireAsync(source)).ShouldNotBeNull();
            await checkpoints.ReleaseAsync(reacquiredSource);
            ProcessSweepState reacquiredSweep = (await sweeps.AcquireAsync()).ShouldNotBeNull();
            await sweeps.ReleaseAsync(reacquiredSweep);
            resume = true;
            for (int pass = 0; pass < 12 && (await checkpoints.GetAsync(source)).Checkpoint == 0; pass++)
                _ = await reconciler.RunPassAsync();
            (await checkpoints.GetAsync(source)).Checkpoint.ShouldBe(1);
        }
    }

    /// <summary>A second replica cannot run sources or publish Healthy while another owns the sweep lease.</summary>
    [Fact]
    public async Task CompetingReconcilersRespectSweepOwnerBeforeHealthyCoverage()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { SourcesPerPass = 4, OperationTimeout = TimeSpan.FromSeconds(5) };
        ProcessSource source = new("tenant-a", "sample", "replica-source");
        (_, ProcessSweepStore initialSweeps, ProcessReconciler initial) = Runtime(store, options, Catalog([]));
        using (initial)
            _ = await initial.RunPassAsync();
        (await initialSweeps.GetAsync()).Complete.ShouldBeTrue();
        (await new ProcessReadinessHealthCheck(initialSweeps, Options.Create(options), TimeProvider.System)
            .CheckHealthAsync(new HealthCheckContext())).Status.ShouldBe(HealthStatus.Healthy);
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<IReadOnlyList<ProcessEffect>> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        IProcessTranslator translator = Substitute.For<IProcessTranslator>();
        translator.TranslateAsync(Arg.Any<ProcessSource>(), Arg.Any<StreamReadEvent>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                entered.TrySetResult();
                return new ValueTask<IReadOnlyList<ProcessEffect>>(release.Task);
            });
        (ProcessCheckpointStore checkpoints, ProcessSweepStore firstSweeps, ProcessReconciler first) = Runtime(store, options, Catalog([source]), processTranslator: translator);
        (_, ProcessSweepStore secondSweeps, ProcessReconciler second) = Runtime(store, options, Catalog([source]));
        using (first)
        using (second)
        {
            Task<ProcessReconciliationPass> firstPass = first.RunPassAsync();
            try
            {
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
                (await firstSweeps.GetAsync()).Owner.ShouldNotBeNull();
                (await second.RunPassAsync()).Sources.ShouldBe(0);
                (await checkpoints.GetAsync(source)).Checkpoint.ShouldBe(0);
                HealthCheckResult overlap = await new ProcessReadinessHealthCheck(secondSweeps, Options.Create(options), TimeProvider.System)
                    .CheckHealthAsync(new HealthCheckContext());
                overlap.Status.ShouldBe(HealthStatus.Degraded);
                overlap.Data["complete"].ShouldBe(false);
            }
            finally
            {
                release.TrySetResult([]);
                await firstPass;
            }
            for (int pass = 0; pass < 10 && !(await firstSweeps.GetAsync()).Complete; pass++)
                _ = await first.RunPassAsync();
            (await checkpoints.GetAsync(source)).Checkpoint.ShouldBe(1);
            (await firstSweeps.GetAsync()).Complete.ShouldBeTrue();
        }
    }

    /// <summary>A competing sweep cannot complete Healthy between invalidation and source CAS.</summary>
    [Fact]
    public async Task DirectTransitionHandoffSurvivesCompetingSweep()
    {
        InMemoryReadModelStore inner = new();
        EventStoreProcessOptions options = new() { SourcesPerPass = 4, OperationTimeout = TimeSpan.FromSeconds(5) };
        ProcessSource source = new("tenant-a", "sample", "transition-race");
        (ProcessCheckpointStore initial, ProcessSweepStore initialSweeps, ProcessReconciler first) = Runtime(inner, options, Catalog([]));
        await initial.EnsureCandidateAsync(source);
        using (first)
        {
            for (int pass = 0; pass < 20 && !(await initialSweeps.GetAsync()).Complete; pass++)
                _ = await first.RunPassAsync();
        }
        (await initialSweeps.GetAsync()).Complete.ShouldBeTrue();

        PausingProcessSourceStore paused = new(inner, ProcessStateKeys.Source(source));
        (ProcessCheckpointStore checkpoints, ProcessSweepStore sweeps, ProcessReconciler competing) = Runtime(paused, options, Catalog([]));
        using (competing)
        {
            ProcessSourceState owned = (await checkpoints.AcquireAsync(source)).ShouldNotBeNull();
            Task<ProcessSourceState> write = checkpoints.UpdateOwnedAsync(owned, state => state with { Status = ProcessSourceStatus.Incomplete });
            try
            {
                await paused.Paused.WaitAsync(TimeSpan.FromSeconds(3));
                (await sweeps.GetAsync()).PendingTransitions.ShouldContainKey(ProcessStateKeys.SourceId(source));
                _ = await competing.RunPassAsync();
                (await sweeps.GetAsync()).PendingTransitions.ShouldContainKey(ProcessStateKeys.SourceId(source));
                (await new ProcessReadinessHealthCheck(sweeps, Options.Create(options), TimeProvider.System)
                    .CheckHealthAsync(new HealthCheckContext())).Status.ShouldBe(HealthStatus.Degraded);
            }
            finally
            {
                paused.Release();
            }

            ProcessSourceState written = await write;
            written.LastTransitionGeneration.ShouldBeGreaterThan(0);
            await checkpoints.ReleaseAsync(written);
            for (int pass = 0; pass < 20 && (await sweeps.GetAsync()).PendingTransitions.Count != 0; pass++)
                _ = await competing.RunPassAsync();
            (await sweeps.GetAsync()).PendingTransitions.ShouldBeEmpty();
        }
    }

    /// <summary>Worker pressure retains every registry cursor until the same page can be read.</summary>
    [Theory]
    [InlineData("tenant-page")]
    [InlineData("tenant-tail")]
    [InlineData("candidate-page")]
    [InlineData("candidate-tail")]
    [InlineData("source-state")]
    public async Task CandidateReadAdmissionRetainsCursorAndIncompleteCoverage(string phase)
    {
        InMemoryReadModelStore inner = new();
        EventStoreProcessOptions options = new() { SourcesPerPass = 64 };
        ProcessSource source = new("tenant-a", "sample", "admission");
        ProcessCheckpointStore initial = new(inner, Options.Create(options), TimeProvider.System);
        await initial.EnsureCandidateAsync(source);
        ProcessSweepStore initialSweeps = new(inner, Options.Create(options), TimeProvider.System);
        ProcessSweepState owned = (await initialSweeps.AcquireAsync()).ShouldNotBeNull();
        owned = await initialSweeps.UpdateOwnedAsync(owned, current => current with
        {
            CatalogComplete = true, NextCatalog = false,
            TenantOffset = phase == "tenant-tail" ? 1 : 0,
            CandidateOffset = phase == "candidate-tail" ? 1 : 0,
        });
        await initialSweeps.ReleaseAsync(owned);
        string key = phase switch
        {
            "tenant-page" => ProcessStateKeys.Tenants(0),
            "tenant-tail" => ProcessStateKeys.TenantsTail,
            "candidate-page" => ProcessStateKeys.Candidates(source.Tenant, 0),
            "candidate-tail" => ProcessStateKeys.CandidatesTail(source.Tenant),
            _ => ProcessStateKeys.Source(source),
        };
        AdmissionFaultProcessStore faulting = new(inner, key);
        (_, ProcessSweepStore sweeps, ProcessReconciler reconciler) = Runtime(faulting, options, Catalog([]));
        using (reconciler)
        {
            ProcessReconciliationPass pass = await reconciler.RunPassAsync();
            faulting.Fired.ShouldBeTrue();
            faulting.Refusals.ShouldBe(1);
            pass.CycleComplete.ShouldBeFalse();
            ProcessSweepState after = await sweeps.GetAsync();
            after.TenantPage.ShouldBe(owned.TenantPage);
            after.TenantOffset.ShouldBe(owned.TenantOffset);
            after.CandidatePage.ShouldBe(owned.CandidatePage);
            after.CandidateOffset.ShouldBe(owned.CandidateOffset);
            after.CandidatesComplete.ShouldBeFalse();
            after.Reasons["read-worker-exhausted"].ShouldBe(1);
            after.Reasons.Keys.ShouldNotContain("tenant-page-unreadable");
            after.Reasons.Keys.ShouldNotContain("candidate-page-unreadable");
            for (int attempt = 0; attempt < 12 && !(await sweeps.GetAsync()).Complete; attempt++)
                _ = await reconciler.RunPassAsync();
            (await sweeps.GetAsync()).Complete.ShouldBeTrue();
        }
    }

    /// <summary>Admission loss after completed execution retains the source marker and does not run it twice.</summary>
    [Fact]
    public async Task PostRunAdmissionRetainsCandidateCursorWithoutRepeatingSourceRun()
    {
        InMemoryReadModelStore inner = new();
        EventStoreProcessOptions options = new() { SourcesPerPass = 64 };
        ProcessSource source = new("tenant-a", "sample", "post-run-admission");
        ProcessCheckpointStore initial = new(inner, Options.Create(options), TimeProvider.System);
        await initial.EnsureCandidateAsync(source);
        ProcessSweepStore initialSweeps = new(inner, Options.Create(options), TimeProvider.System);
        ProcessSweepState owned = (await initialSweeps.AcquireAsync()).ShouldNotBeNull();
        owned = await initialSweeps.UpdateOwnedAsync(owned, current => current with { CatalogComplete = true, NextCatalog = false });
        await initialSweeps.ReleaseAsync(owned);
        AdmissionFaultProcessStore faulting = new(inner, ProcessStateKeys.Source(source), afterSourceRelease: true);
        int reads = 0;
        (ProcessCheckpointStore checkpoints, ProcessSweepStore sweeps, ProcessReconciler reconciler) = Runtime(faulting, options, Catalog([]), request =>
        {
            reads++;
            StreamReadEvent[] events = request.FromSequence == 0
                ? [new(1, "SyntheticEvent", [1], "json", 1, "message-1", null, null, DateTimeOffset.UtcNow, null)] : [];
            return new StreamReadPage(request.Tenant, request.Domain, request.AggregateId, events,
                new StreamReadMetadata(request.FromSequence, null, events.Length == 0 ? null : 1, 1, events.Length, false, null));
        });
        using (reconciler)
        {
            _ = await reconciler.RunPassAsync();
            faulting.Fired.ShouldBeTrue();
            ProcessSweepState afterFailure = await sweeps.GetAsync();
            afterFailure.CandidateOffset.ShouldBe(0);
            afterFailure.CandidatesComplete.ShouldBeFalse();
            afterFailure.Reasons["read-worker-exhausted"].ShouldBe(1);
            ProcessSourceState persisted = await checkpoints.GetAsync(source);
            persisted.Checkpoint.ShouldBe(1);
            persisted.ObservedCycle.ShouldBe(afterFailure.Cycle);
            persisted.ObservedUnresolved.ShouldBeTrue();
            for (int pass = 0; pass < 12 && !(await sweeps.GetAsync()).Complete; pass++) _ = await reconciler.RunPassAsync();
            (await sweeps.GetAsync()).Complete.ShouldBeTrue();
            reads.ShouldBe(1);
        }
    }

    /// <summary>A refused tail read after missing or unreadable pages yields without losing any persisted cursor.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task TailAdmissionAfterUnavailablePageYieldsOncePerPassAndCountsOncePerCycle(bool candidate, bool missing)
    {
        InMemoryReadModelStore inner = new();
        EventStoreProcessOptions options = new() { SourcesPerPass = 64 };
        ProcessSource source = new("tenant-a", "sample", "tail-admission");
        ProcessCheckpointStore initial = new(inner, Options.Create(options), TimeProvider.System);
        await initial.EnsureCandidateAsync(source);
        inner.SeedRaw(options.StateStoreName, ProcessStateKeys.Tenants(2), new ProcessIndexPage<string>(1, 2, [source.Tenant], null));
        ProcessSweepStore initialSweeps = new(inner, Options.Create(options), TimeProvider.System);
        ProcessSweepState owned = (await initialSweeps.AcquireAsync()).ShouldNotBeNull();
        owned = await initialSweeps.UpdateOwnedAsync(owned, current => current with
        {
            CatalogComplete = true, NextCatalog = false, TenantPage = 2, TenantOffset = 0,
            CandidatePage = 3, CandidateOffset = 1, ExpectedTenantPage = true, ExpectedCandidatePage = true,
            TenantProbeMisses = 2, CandidateProbeMisses = 3,
        });
        await initialSweeps.ReleaseAsync(owned);
        string pageKey = candidate ? ProcessStateKeys.Candidates(source.Tenant, 3) : ProcessStateKeys.Tenants(2);
        string tailKey = candidate ? ProcessStateKeys.CandidatesTail(source.Tenant) : ProcessStateKeys.TenantsTail;
        FaultOnProcessPageReadStore pageFault = new(inner, pageKey, missing);
        AdmissionFaultProcessStore tailFault = new(pageFault, tailKey, refusals: 2);
        (_, ProcessSweepStore sweeps, ProcessReconciler reconciler) = Runtime(tailFault, options, Catalog([]));
        using (reconciler)
        {
            for (int pass = 1; pass <= 2; pass++)
            {
                (await reconciler.RunPassAsync()).CycleComplete.ShouldBeFalse();
                pageFault.PageReads.ShouldBe(pass);
                tailFault.Refusals.ShouldBe(pass);
                ProcessSweepState persisted = await sweeps.GetAsync();
                persisted.Cycle.ShouldBe(owned.Cycle);
                persisted.TenantPage.ShouldBe(owned.TenantPage);
                persisted.TenantOffset.ShouldBe(owned.TenantOffset);
                persisted.CandidatePage.ShouldBe(owned.CandidatePage);
                persisted.CandidateOffset.ShouldBe(owned.CandidateOffset);
                persisted.ExpectedTenantPage.ShouldBe(owned.ExpectedTenantPage);
                persisted.ExpectedCandidatePage.ShouldBe(owned.ExpectedCandidatePage);
                persisted.TenantProbeMisses.ShouldBe(owned.TenantProbeMisses);
                persisted.CandidateProbeMisses.ShouldBe(owned.CandidateProbeMisses);
                persisted.CandidatesComplete.ShouldBeFalse();
                persisted.Complete.ShouldBeFalse();
                persisted.Unresolved.ShouldBe(1);
                persisted.Reasons.Single().Key.ShouldBe("read-worker-exhausted");
                persisted.Reasons["read-worker-exhausted"].ShouldBe(1);
            }
        }
    }

    /// <summary>A folded admission reason stays counted once across passes and resets with the next cycle.</summary>
    [Fact]
    public async Task FoldedScanAdmissionIsCountedOnceAndResetsWithCycle()
    {
        InMemoryReadModelStore inner = new();
        EventStoreProcessOptions options = new() { SourcesPerPass = 64 };
        ProcessSource source = new("tenant-a", "sample", "folded-admission");
        ProcessCheckpointStore initial = new(inner, Options.Create(options), TimeProvider.System);
        await initial.EnsureCandidateAsync(source);
        ProcessSweepStore initialSweeps = new(inner, Options.Create(options), TimeProvider.System);
        ProcessSweepState owned = (await initialSweeps.AcquireAsync()).ShouldNotBeNull();
        owned = await initialSweeps.UpdateOwnedAsync(owned, current => current with
        {
            CatalogComplete = true, NextCatalog = false, Unresolved = 15,
            Reasons = Enumerable.Range(0, 15).ToDictionary(index => $"reason-{index}", _ => 1, StringComparer.Ordinal),
        });
        await initialSweeps.ReleaseAsync(owned);
        AdmissionFaultProcessStore refused = new(inner, ProcessStateKeys.Tenants(0), refusals: 2);
        (_, ProcessSweepStore sweeps, ProcessReconciler reconciler) = Runtime(refused, options, Catalog([]));
        using (reconciler)
        {
            for (int pass = 1; pass <= 2; pass++)
            {
                (await reconciler.RunPassAsync()).CycleComplete.ShouldBeFalse();
                refused.Refusals.ShouldBe(pass);
                ProcessSweepState persisted = await sweeps.GetAsync();
                persisted.Reasons["other"].ShouldBe(1);
                persisted.Reasons.Count.ShouldBe(16);
                persisted.Unresolved.ShouldBe(16);
                persisted.ScanReadAdmissionRecorded.ShouldBeTrue();
            }
            (await reconciler.RunPassAsync()).CycleComplete.ShouldBeTrue();
            ProcessSweepState fresh = (await sweeps.AcquireAsync()).ShouldNotBeNull();
            try
            {
                fresh.Cycle.ShouldBeGreaterThan(owned.Cycle);
                fresh.ScanReadAdmissionRecorded.ShouldBeFalse();
                fresh.Unresolved.ShouldBe(0);
            }
            finally { await sweeps.ReleaseAsync(fresh, CancellationToken.None); }
        }
    }

    /// <summary>A refused catalog source read keeps its key and is not stored as unavailable state.</summary>
    [Fact]
    public async Task CatalogSourceAdmissionRetainsCursorAndReadWorkerReason()
    {
        InMemoryReadModelStore inner = new();
        EventStoreProcessOptions options = new() { SourcesPerPass = 64 };
        ProcessSource source = new("tenant-a", "sample", "catalog-admission");
        AdmissionFaultProcessStore faulting = new(inner, ProcessStateKeys.Source(source));
        (_, ProcessSweepStore sweeps, ProcessReconciler reconciler) = Runtime(faulting, options, Catalog([source]));
        using (reconciler)
        {
            ProcessReconciliationPass pass = await reconciler.RunPassAsync();
            faulting.Fired.ShouldBeTrue();
            faulting.Refusals.ShouldBe(1);
            pass.CycleComplete.ShouldBeFalse();
            ProcessSweepState after = await sweeps.GetAsync();
            after.CatalogAfterKey.ShouldBeNull();
            after.CatalogComplete.ShouldBeFalse();
            after.Unresolved.ShouldBe(1);
            after.ScanReadAdmissionRecorded.ShouldBeTrue();
            after.Reasons["read-worker-exhausted"].ShouldBe(1);
            after.Reasons.Keys.ShouldNotContain("source-state-unavailable");
            for (int attempt = 0; attempt < 12 && !(await sweeps.GetAsync()).Complete; attempt++)
                _ = await reconciler.RunPassAsync();
            (await sweeps.GetAsync()).Complete.ShouldBeTrue();
        }
    }

    /// <summary>A source-read admission folded into other still counts once.</summary>
    [Fact]
    public async Task FoldedSourceAdmissionIsCountedOnce()
    {
        InMemoryReadModelStore inner = new();
        EventStoreProcessOptions options = new() { SourcesPerPass = 64 };
        ProcessSource source = new("tenant-a", "sample", "folded-source-admission");
        ProcessCheckpointStore initial = new(inner, Options.Create(options), TimeProvider.System);
        await initial.EnsureCandidateAsync(source);
        ProcessSweepStore initialSweeps = new(inner, Options.Create(options), TimeProvider.System);
        ProcessSweepState owned = (await initialSweeps.AcquireAsync()).ShouldNotBeNull();
        owned = await initialSweeps.UpdateOwnedAsync(owned, current => current with
        {
            CatalogComplete = true, NextCatalog = false, Unresolved = 15,
            Reasons = Enumerable.Range(0, 15).ToDictionary(index => $"reason-{index}", _ => 1, StringComparer.Ordinal),
        });
        await initialSweeps.ReleaseAsync(owned);
        AdmissionFaultProcessStore refused = new(inner, ProcessStateKeys.Source(source), refusals: 4);
        (_, ProcessSweepStore sweeps, ProcessReconciler reconciler) = Runtime(refused, options, Catalog([]));
        using (reconciler)
        {
            _ = await reconciler.RunPassAsync();
            refused.Fired.ShouldBeTrue();
            ProcessSweepState persisted = await sweeps.GetAsync();
            persisted.Reasons["other"].ShouldBe(1);
            persisted.Reasons.Keys.ShouldNotContain("read-worker-exhausted");
            persisted.Unresolved.ShouldBe(16);
            persisted.ScanReadAdmissionRecorded.ShouldBeTrue();
            _ = await reconciler.RunPassAsync();
            persisted = await sweeps.GetAsync();
            persisted.Reasons["other"].ShouldBe(1);
            persisted.Unresolved.ShouldBe(16);
        }
    }

    /// <summary>A refused yield write keeps the recorded admission and does not fault the pass.</summary>
    [Fact]
    public async Task AdmissionYieldWriteFailureRetainsCursor()
    {
        InMemoryReadModelStore inner = new();
        EventStoreProcessOptions options = new() { SourcesPerPass = 64 };
        ProcessSource source = new("tenant-a", "sample", "yield-admission");
        ProcessCheckpointStore initial = new(inner, Options.Create(options), TimeProvider.System);
        await initial.EnsureCandidateAsync(source);
        ProcessSweepStore initialSweeps = new(inner, Options.Create(options), TimeProvider.System);
        ProcessSweepState owned = (await initialSweeps.AcquireAsync()).ShouldNotBeNull();
        owned = await initialSweeps.UpdateOwnedAsync(owned, current => current with { CatalogComplete = true, NextCatalog = false });
        await initialSweeps.ReleaseAsync(owned);
        AdmissionFaultProcessStore sourceFault = new(inner, ProcessStateKeys.Source(source));
        SweepYieldAdmissionFaultStore yieldFault = new(sourceFault);
        (_, ProcessSweepStore sweeps, ProcessReconciler reconciler) = Runtime(yieldFault, options, Catalog([]));
        using (reconciler)
        {
            ProcessReconciliationPass pass = await reconciler.RunPassAsync();
            sourceFault.Fired.ShouldBeTrue();
            yieldFault.Fired.ShouldBeTrue();
            pass.CycleComplete.ShouldBeFalse();
            ProcessSweepState after = await sweeps.GetAsync();
            after.CandidateOffset.ShouldBe(0);
            after.CandidatesComplete.ShouldBeFalse();
            after.Unresolved.ShouldBe(1);
            after.ScanReadAdmissionRecorded.ShouldBeTrue();
            after.Reasons["read-worker-exhausted"].ShouldBe(1);
            for (int attempt = 0; attempt < 12 && !(await sweeps.GetAsync()).Complete; attempt++)
                _ = await reconciler.RunPassAsync();
            (await sweeps.GetAsync()).Complete.ShouldBeTrue();
        }
    }

    /// <summary>Unavailable durable state preserves locally known reasons without asserting unseen durable counts.</summary>
    [Theory]
    [InlineData("disabled", "reconciliation-disabled")]
    [InlineData("stopped", "reconciler-stopped")]
    [InlineData("failed", "hosted-pass-failed")]
    public async Task UnavailableSweepRetainsLocalReadinessReasons(string mode, string reason)
    {
        InMemoryReadModelStore inner = new();
        EventStoreProcessOptions options = new() { ReconciliationEnabled = mode != "disabled" };
        FaultOnProcessPageReadStore unavailable = new(inner, ProcessStateKeys.Sweep);
        (_, _, ProcessReconciler reconciler) = Runtime(mode == "failed" ? unavailable : inner, options, Catalog([]));
        using (reconciler)
        {
            try
            {
                if (mode != "disabled")
                {
                    await reconciler.StartAsync(CancellationToken.None);
                    if (mode == "failed")
                    {
                        for (int attempt = 0; attempt < 100 && reconciler.LocalFailureReason is null; attempt++) await Task.Delay(10);
                        reconciler.LocalFailureReason.ShouldBe(reason);
                        reconciler.IsRunning.ShouldBeTrue();
                    }
                    else await reconciler.StopAsync(CancellationToken.None);
                }
                ProcessSweepStore sweeps = new(unavailable, Options.Create(options), TimeProvider.System);
                HealthCheckResult result = await new ProcessReadinessHealthCheck(sweeps, Options.Create(options), TimeProvider.System, reconciler)
                    .CheckHealthAsync(new HealthCheckContext());
                result.Status.ShouldBe(HealthStatus.Degraded);
                result.Data["stateAvailable"].ShouldBe(false);
                ((IReadOnlyDictionary<string, int>)result.Data["reasonCounts"]).Single().ShouldBe(new KeyValuePair<string, int>(reason, 1));
                result.Data.Keys.ShouldNotContain("complete");
                result.Data.Keys.ShouldNotContain("unresolved");
                result.Data.Keys.ShouldNotContain("cycle");
            }
            finally { await reconciler.StopAsync(CancellationToken.None); }
        }
    }

    /// <summary>An acquisition crash contributes one durable source-operation reason across both indexes.</summary>
    [Fact]
    public async Task RunnerAcquisitionFaultIsRecordedAsSourceOperation()
    {
        InMemoryReadModelStore inner = new();
        ProcessSource source = new("tenant-a", "sample", "operation-fault");
        FaultInjectingProcessStore faulting = new(inner, (_, value) => value is ProcessSourceState { Owner: not null });
        EventStoreProcessOptions options = new() { SourcesPerPass = 4 };
        (ProcessCheckpointStore checkpoints, ProcessSweepStore sweeps, ProcessReconciler reconciler) = Runtime(faulting, options, Catalog([source]));
        using (reconciler) _ = await reconciler.RunPassAsync();
        faulting.Fired.ShouldBeTrue();
        ProcessSweepState state = await sweeps.GetAsync();
        state.Reasons["source-operation"].ShouldBe(1);
        state.Unresolved.ShouldBe(1);
        ProcessSourceState persisted = await checkpoints.GetAsync(source);
        persisted.Checkpoint.ShouldBe(0);
        persisted.ObservedCycle.ShouldBe(state.Cycle);
        persisted.ObservedUnresolved.ShouldBeTrue();
    }

    /// <summary>A sixteenth distinct source reason folds into other without losing its count.</summary>
    [Fact]
    public async Task SixteenthReasonFoldsIntoOther()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { SourcesPerPass = 1 };
        ProcessSource source = new("tenant-a", "sample", "reason-fold");
        (ProcessCheckpointStore checkpoints, ProcessSweepStore sweeps, ProcessReconciler reconciler) = Runtime(store, options, Catalog([]));
        await checkpoints.EnsureCandidateAsync(source);
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Source(source),
            new ProcessSourceState(1, source, 0, ProcessSourceStatus.Parked, 3, 1, "sixteenth", null, null, 0, 0, DateTimeOffset.UtcNow));
        ProcessSweepState owned = (await sweeps.AcquireAsync()).ShouldNotBeNull();
        owned = await sweeps.UpdateOwnedAsync(owned, current => current with
        {
            CatalogComplete = true, NextCatalog = false, Unresolved = 15,
            Reasons = Enumerable.Range(0, 15).ToDictionary(index => $"reason-{index}", _ => 1, StringComparer.Ordinal),
        });
        await sweeps.ReleaseAsync(owned);
        using (reconciler) _ = await reconciler.RunPassAsync();
        ProcessSweepState result = await sweeps.GetAsync();
        result.Reasons.Count.ShouldBe(16);
        result.Reasons["other"].ShouldBe(1);
        result.Reasons.Keys.ShouldNotContain("sixteenth");
        result.Unresolved.ShouldBe(16);
        (await checkpoints.GetAsync(source)).ObservedUnresolved.ShouldBeTrue();
    }

    /// <summary>Persisted source text above the category limit never enters readiness reason data.</summary>
    [Theory]
    [InlineData(64)]
    [InlineData(65)]
    public async Task SourceReasonLengthIsBoundedInSweepData(int length)
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { SourcesPerPass = 1 };
        ProcessSource source = new("tenant-a", "sample", "reason-length");
        (ProcessCheckpointStore checkpoints, ProcessSweepStore sweeps, ProcessReconciler reconciler) = Runtime(store, options, Catalog([]));
        await checkpoints.EnsureCandidateAsync(source);
        string reason = new('x', length);
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Source(source),
            new ProcessSourceState(1, source, 0, ProcessSourceStatus.Parked, 3, 1, reason, null, null, 0, 0, DateTimeOffset.UtcNow));
        ProcessSweepState owned = (await sweeps.AcquireAsync()).ShouldNotBeNull();
        owned = await sweeps.UpdateOwnedAsync(owned, current => current with { CatalogComplete = true, NextCatalog = false });
        await sweeps.ReleaseAsync(owned);
        using (reconciler) _ = await reconciler.RunPassAsync();
        ProcessSweepState result = await sweeps.GetAsync();
        result.Reasons.Single().Key.ShouldBe(length == 64 ? reason : nameof(ProcessSourceStatus.Parked));
        result.Unresolved.ShouldBe(1);
        (await checkpoints.GetAsync(source)).ObservedUnresolved.ShouldBeTrue();
    }

    /// <summary>A near-full sweep retains its cursor and exposes the record-budget reason and pending count.</summary>
    [Fact]
    public async Task SweepRecordBudgetHasDurableReadinessEvidence()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { SourcesPerPass = 2 };
        (_, ProcessSweepStore sweeps, ProcessReconciler reconciler) = Runtime(store, options, Catalog([]));
        ProcessSweepState owned = (await sweeps.AcquireAsync()).ShouldNotBeNull();
        var observations = new Dictionary<string, ProcessPendingObservation>(StringComparer.Ordinal);
        ProcessSweepState pressure = owned;
        for (int index = 0; pressure.SerializedSize() < ProcessSweepState.MaxSerializedBytes - 8192; index++)
        {
            ProcessSource source = new("tenant-a", "sample", $"{index:0000}" + new string('x', 252));
            observations.Add(ProcessStateKeys.SourceId(source), new(source, owned.Cycle, true));
            store.SeedRaw(options.StateStoreName, ProcessStateKeys.Source(source),
                new ProcessSourceState(2, source, 0, ProcessSourceStatus.Active, 0, null, null, null, null, 0, 0, DateTimeOffset.UtcNow));
            pressure = owned with { PendingObservations = observations };
        }
        pressure.Validate();
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Sweep, pressure with { Owner = null, LeaseUntil = null });
        using (reconciler) _ = await reconciler.RunPassAsync();
        ProcessSweepState persisted = await sweeps.GetAsync();
        persisted.PendingObservations.Count.ShouldBe(observations.Count);
        persisted.CatalogComplete.ShouldBeFalse();
        persisted.CandidatesComplete.ShouldBeFalse();
        persisted.Reasons["sweep-record-budget"].ShouldBe(1);
        HealthCheckResult health = await new ProcessReadinessHealthCheck(sweeps, Options.Create(options), TimeProvider.System)
            .CheckHealthAsync(new HealthCheckContext());
        health.Status.ShouldBe(HealthStatus.Degraded);
        health.Data["pendingObservations"].ShouldBe(observations.Count);
        ((IReadOnlyDictionary<string, int>)health.Data["reasonCounts"])["sweep-record-budget"].ShouldBe(1);
    }

    private static IProcessSourceCatalog Catalog(IReadOnlyList<ProcessSource> sources)
    {
        IProcessSourceCatalog catalog = Substitute.For<IProcessSourceCatalog>();
        catalog.ListAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            string? after = call.ArgAt<string?>(0);
            int limit = call.ArgAt<int>(1);
            ProcessSource[] remaining = sources.OrderBy(static source => source.CatalogKey, StringComparer.Ordinal)
                .Where(source => after is null || string.CompareOrdinal(source.CatalogKey, after) > 0).ToArray();
            ProcessSource[] page = remaining.Take(limit).ToArray();
            string? next = remaining.Length > page.Length ? page[^1].CatalogKey : null;
            return Task.FromResult(new ProcessSourcePage(page, next));
        });
        return catalog;
    }

    private static async Task<IReadOnlyList<ProcessEffect>> WaitForTranslationCancellationAsync(CancellationToken cancellationToken, TaskCompletionSource entered)
    {
        entered.TrySetResult();
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return [];
    }

    /// <summary>Composes a persisted synthetic process runtime for recovery and renewal fault tests.</summary>
    internal static (ProcessCheckpointStore Checkpoints, ProcessSweepStore Sweeps, ProcessReconciler Reconciler) Runtime(
        IReadModelStore store, EventStoreProcessOptions options, IProcessSourceCatalog catalog,
        Func<StreamReadRequest, StreamReadPage>? pageFactory = null, IProcessTranslator? processTranslator = null, TimeProvider? time = null)
    {
        TimeProvider clock = time ?? TimeProvider.System;
        ProcessCheckpointStore checkpoints = new(store, Options.Create(options), clock);
        ProcessSweepStore sweeps = new(store, Options.Create(options), clock);
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            StreamReadRequest request = call.Arg<StreamReadRequest>();
            if (pageFactory is not null) return Task.FromResult(pageFactory(request));
            StreamReadEvent[] events = request.FromSequence == 0
                ? [new(1, "SyntheticEvent", [123], "json", 1, "message-1", null, null, DateTimeOffset.UtcNow, null)]
                : [];
            return Task.FromResult(new StreamReadPage(request.Tenant, request.Domain, request.AggregateId, events,
                new StreamReadMetadata(request.FromSequence, null, events.Length == 0 ? null : 1, 1, events.Length, false, null)));
        });
        IProcessTranslator translator = processTranslator ?? Substitute.For<IProcessTranslator>();
        if (processTranslator is null)
            translator.TranslateAsync(Arg.Any<ProcessSource>(), Arg.Any<StreamReadEvent>(), Arg.Any<CancellationToken>())
                .Returns(new ValueTask<IReadOnlyList<ProcessEffect>>([]));
        IProcessDelegationProvider delegation = Substitute.For<IProcessDelegationProvider>();
        ITrustedEffectSubmitter submitter = Substitute.For<ITrustedEffectSubmitter>();
        CheckpointedProcessRunner runner = new(gateway, translator, delegation, submitter, checkpoints, Options.Create(options), clock);
        var services = new ServiceCollection();
        services.AddSingleton(catalog);
        services.AddSingleton(runner);
        ServiceProvider provider = services.BuildServiceProvider();
        ProcessReconciler reconciler = new(provider.GetRequiredService<IServiceScopeFactory>(), sweeps, checkpoints, Options.Create(options), clock);
        return (checkpoints, sweeps, reconciler);
    }

}
