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
        (await new ProcessReadinessHealthCheck(sweeps, Options.Create(options), TimeProvider.System)
            .CheckHealthAsync(new HealthCheckContext())).Status.ShouldBe(HealthStatus.Degraded);
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

        (await new ProcessReadinessHealthCheck(sweeps, Options.Create(options), TimeProvider.System)
            .CheckHealthAsync(new HealthCheckContext())).Status.ShouldBe(HealthStatus.Degraded);
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
        IProcessSourceCatalog catalog = Substitute.For<IProcessSourceCatalog>();
        var never = new TaskCompletionSource<ProcessSourcePage>(TaskCreationOptions.RunContinuationsAsynchronously);
        catalog.ListAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(never.Task);
        (ProcessCheckpointStore checkpoints, ProcessSweepStore sweeps, ProcessReconciler reconciler) = Runtime(store, options, catalog);
        await checkpoints.EnsureCandidateAsync(source);
        using (reconciler)
        {
            for (int pass = 0; pass < 12 && (await checkpoints.GetAsync(source)).Checkpoint == 0; pass++)
                _ = await reconciler.RunPassAsync();
        }

        (await checkpoints.GetAsync(source)).Checkpoint.ShouldBe(1);
        (await sweeps.GetAsync()).Reasons.ShouldContainKey("catalog-unavailable");
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
        if (pendingPublication) (await sweeps.PreparePublicationAsync(source)).ShouldBeTrue();
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

    /// <summary>A long valid source run renews the sweep lease until its checkpoint is covered.</summary>
    [Fact]
    public async Task LongSourceRunRenewsSweepOwnership()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new()
        {
            SourcesPerPass = 4,
            OperationTimeout = TimeSpan.FromMilliseconds(500),
            LeaseDuration = TimeSpan.FromMilliseconds(600),
        };
        ProcessSource source = new("tenant-a", "sample", "slow");
        IProcessTranslator translator = Substitute.For<IProcessTranslator>();
        translator.TranslateAsync(Arg.Any<ProcessSource>(), Arg.Any<StreamReadEvent>(), Arg.Any<CancellationToken>())
            .Returns(call => new ValueTask<IReadOnlyList<ProcessEffect>>(TranslateSlowlyAsync(call.Arg<CancellationToken>())));
        (ProcessCheckpointStore checkpoints, ProcessSweepStore sweeps, ProcessReconciler reconciler) = Runtime(store, options, Catalog([source]),
            request => new StreamReadPage(request.Tenant, request.Domain, request.AggregateId,
                [new StreamReadEvent(1, "SyntheticEvent", [1], "json", 1, "message-1", null, null, DateTimeOffset.UtcNow, null),
                 new StreamReadEvent(2, "SyntheticEvent", [2], "json", 1, "message-2", null, null, DateTimeOffset.UtcNow, null)],
                new StreamReadMetadata(request.FromSequence, null, 2, 2, 2, false, null)), translator);
        using (reconciler)
        {
            for (int pass = 0; pass < 10 && !(await sweeps.GetAsync()).Complete; pass++)
                _ = await reconciler.RunPassAsync();
        }

        (await checkpoints.GetAsync(source)).Checkpoint.ShouldBe(2);
        (await sweeps.GetAsync()).Complete.ShouldBeTrue();
    }

    private static async Task<IReadOnlyList<ProcessEffect>> TranslateSlowlyAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(400, cancellationToken);
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

    private static (ProcessCheckpointStore Checkpoints, ProcessSweepStore Sweeps, ProcessReconciler Reconciler) Runtime(
        IReadModelStore store, EventStoreProcessOptions options, IProcessSourceCatalog catalog,
        Func<StreamReadRequest, StreamReadPage>? pageFactory = null, IProcessTranslator? processTranslator = null)
    {
        ProcessCheckpointStore checkpoints = new(store, Options.Create(options), TimeProvider.System);
        ProcessSweepStore sweeps = new(store, Options.Create(options), TimeProvider.System);
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
        CheckpointedProcessRunner runner = new(gateway, translator, delegation, submitter, checkpoints, Options.Create(options));
        var services = new ServiceCollection();
        services.AddSingleton(catalog);
        services.AddSingleton(runner);
        ServiceProvider provider = services.BuildServiceProvider();
        ProcessReconciler reconciler = new(provider.GetRequiredService<IServiceScopeFactory>(), sweeps, checkpoints, Options.Create(options), TimeProvider.System);
        return (checkpoints, sweeps, reconciler);
    }

}
