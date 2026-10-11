using Hexalith.EventStore.Client.Effects;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Processes;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Processes;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests.Processes;

/// <summary>Fault injection and bounded discovery proofs for process publication.</summary>
public sealed class ProcessPublicationTests
{
    private static readonly ProcessSource Source = new("tenant-a", "sample", "source");

    /// <summary>Duplicate persisted identities fail closed before registry traversal.</summary>
    [Fact]
    public async Task DuplicateTenantAndCandidatePageEntriesAreRejected()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new();
        ProcessCheckpointStore checkpoints = new(store, Options.Create(options), TimeProvider.System);
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Tenants(0),
            new ProcessIndexPage<string>(1, 0, [Source.Tenant, Source.Tenant], null));
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Candidates(Source.Tenant, 0),
            new ProcessIndexPage<ProcessSource>(1, 0, [Source, Source], null));

        await Should.ThrowAsync<InvalidOperationException>(() => checkpoints.ReadTenantPageAsync(0));
        await Should.ThrowAsync<InvalidOperationException>(() => checkpoints.ReadCandidatePageAsync(Source.Tenant, 0));
    }

    /// <summary>Repeated registration is idempotent only for the same adapter pair.</summary>
    [Fact]
    public void RepeatedProcessRegistrationRejectsConflictingAdapters()
    {
        ServiceCollection services = new();
        services.AddEventStoreProcesses<EmptyCatalog, EmptyTranslator>();
        services.AddEventStoreProcesses<EmptyCatalog, EmptyTranslator>();
        services.Count(static service => service.ServiceType == typeof(ProcessReconciler)).ShouldBe(1);
        Should.Throw<InvalidOperationException>(() => services.AddEventStoreProcesses<EmptyCatalog, AlternateProcessTranslator>());
    }

    /// <summary>The registered hosted runtime requires operator authorization and audit adapters at startup.</summary>
    [Fact]
    public async Task HostedRuntimeRejectsMissingDispositionAuditAdapter()
    {
        using IHost host = Host.CreateDefaultBuilder().ConfigureServices((_, services) =>
        {
            services.AddSingleton<IReadModelStore>(new InMemoryReadModelStore());
            services.AddSingleton(Substitute.For<IEventStoreGatewayClient>());
            services.AddSingleton(Substitute.For<ITrustedEffectSubmitter>());
            services.AddSingleton(Substitute.For<IProcessDelegationProvider>());
            services.AddSingleton(Substitute.For<IProcessDispositionAuthorizer>());
            services.AddEventStoreProcesses<EmptyCatalog, EmptyTranslator>();
        }).Build();

        await Should.ThrowAsync<InvalidOperationException>(() => host.StartAsync());
    }

    /// <summary>One-source passes charge one catalog request per admitted source.</summary>
    [Fact]
    public void OneSourcePassCapacityIncludesRepeatedCatalogRequests()
    {
        EventStoreProcessOptions options = new()
        {
            SourcesPerPass = 1,
            DiscoveryPageSize = 64,
            SupportedCandidateCount = 6,
            MaxEffectsPerEnvelope = 1,
            OperationTimeout = TimeSpan.FromMilliseconds(100),
            SourceServiceQuantum = TimeSpan.FromMilliseconds(800),
            ReconciliationInterval = TimeSpan.FromSeconds(1),
            ReadinessMaxAge = TimeSpan.FromMilliseconds(44500),
        };

        options.Validate().ShouldContain(value => value.Contains("SupportedCandidateCount", StringComparison.Ordinal));
    }

    /// <summary>A rejected child-page CAS cannot leave a durable parent link to an absent page.</summary>
    [Fact]
    public async Task RejectedNewCandidatePageIsNotLinkedAndRepairsAfterRestart()
    {
        InMemoryReadModelStore inner = new();
        EventStoreProcessOptions options = new();
        ProcessSource[] existing = Enumerable.Range(0, 256)
            .Select(index => new ProcessSource(Source.Tenant, Source.Domain, $"prior-{index:000}"))
            .ToArray();
        inner.SeedRaw(options.StateStoreName, ProcessStateKeys.Candidates(Source.Tenant, 0),
            new ProcessIndexPage<ProcessSource>(1, 0, existing, null));
        inner.SeedRaw(options.StateStoreName, ProcessStateKeys.CandidatesTail(Source.Tenant), new ProcessIndexTail(1, 0));
        RejectProcessMarkerStore rejecting = new(inner, ProcessStateKeys.Candidates(Source.Tenant, 1), alwaysReject: true);
        ProcessCheckpointStore first = new(rejecting, Options.Create(options), TimeProvider.System);

        (await first.EnsureCandidateAsync(Source, null, CancellationToken.None)).ShouldBeFalse();
        (await first.ReadCandidatePageAsync(Source.Tenant, 0)).ShouldNotBeNull().NextPage.ShouldBeNull();
        (await first.ReadCandidatePageAsync(Source.Tenant, 1)).ShouldBeNull();
        ProcessSweepStore sweeps = new(inner, Options.Create(options), TimeProvider.System);
        (await sweeps.GetAsync()).PendingPublications.Values.ShouldContain(Source);

        ProcessCheckpointStore restarted = new(inner, Options.Create(options), TimeProvider.System);
        (await restarted.EnsureCandidateAsync(Source, null, CancellationToken.None)).ShouldBeTrue();
        (await restarted.ReadCandidatePageAsync(Source.Tenant, 0)).ShouldNotBeNull().NextPage.ShouldBe(1);
        (await restarted.ReadCandidatePageAsync(Source.Tenant, 1)).ShouldNotBeNull().Entries.ShouldContain(Source);
        (await restarted.ContainsCandidateAsync(Source)).ShouldBeTrue();
    }

    /// <summary>A linked absent page has unknown contents and remains unresolved through repair.</summary>
    [Fact]
    public async Task LinkedAbsentCandidatePageRetainsUnresolvedIdentity()
    {
        InMemoryReadModelStore inner = new();
        EventStoreProcessOptions options = new();
        ProcessSource prior = new(Source.Tenant, Source.Domain, "prior");
        inner.SeedRaw(options.StateStoreName, ProcessStateKeys.Candidates(Source.Tenant, 0),
            new ProcessIndexPage<ProcessSource>(1, 0, [prior], 1));
        ProcessSweepStore sweeps = new(inner, Options.Create(options), TimeProvider.System);
        (await sweeps.PreparePublicationAsync(Source)).ShouldBeTrue();
        ProcessCheckpointStore restarted = new(inner, Options.Create(options), TimeProvider.System);

        (await restarted.EnsureCandidateAsync(Source, null, CancellationToken.None)).ShouldBeFalse();

        (await restarted.ReadCandidatePageAsync(Source.Tenant, 1)).ShouldBeNull();
        (await restarted.ContainsCandidateAsync(Source)).ShouldBeFalse();
        ProcessSweepState state = await sweeps.GetAsync();
        state.PendingPublications.Values.ShouldContain(Source);
        state.PublicationCandidateUnknownPages.ContainsKey(ProcessStateKeys.SourceId(Source)).ShouldBeTrue();
    }

    /// <summary>A stale tail is traversed in durable bounded steps instead of one million inline reads.</summary>
    [Fact]
    public async Task StaleTailAppendResumesAtPersistedPageCursor()
    {
        InMemoryReadModelStore inner = new();
        EventStoreProcessOptions options = new();
        for (int page = 0; page < 18; page++)
        {
            ProcessSource[] entries = Enumerable.Range(0, 256)
                .Select(index => new ProcessSource(Source.Tenant, Source.Domain, $"prior-{page:00}-{index:000}"))
                .ToArray();
            inner.SeedRaw(options.StateStoreName, ProcessStateKeys.Candidates(Source.Tenant, page),
                new ProcessIndexPage<ProcessSource>(1, page, entries, page == 17 ? null : page + 1));
        }
        FaultInjectingProcessStore counted = new(inner, static (_, _) => false);
        ProcessCheckpointStore first = new(counted, Options.Create(options), TimeProvider.System);

        (await first.EnsureCandidateAsync(Source, null, CancellationToken.None)).ShouldBeFalse();
        counted.CandidatePageReads.ShouldBe(8);
        ProcessSweepStore sweeps = new(inner, Options.Create(options), TimeProvider.System);
        (await sweeps.GetAsync()).PublicationCandidateRepairPages[ProcessStateKeys.SourceId(Source)].ShouldBe(8);

        ProcessCheckpointStore restarted = new(counted, Options.Create(options), TimeProvider.System);
        for (int pass = 0; pass < 5 && !(await restarted.ContainsCandidateAsync(Source)); pass++)
            _ = await restarted.EnsureCandidateAsync(Source, null, CancellationToken.None);

        (await restarted.ContainsCandidateAsync(Source)).ShouldBeTrue();
        (await restarted.ReadCandidatePageAsync(Source.Tenant, 18)).ShouldNotBeNull().Entries.ShouldContain(Source);
        (await sweeps.GetAsync()).PublicationCandidateRepairPages.ShouldBeEmpty();
    }

    /// <summary>A saturated pending set rejects new admission before any candidate page write.</summary>
    [Fact]
    public async Task PendingPublicationSaturationRejectsUnacceptedSource()
    {
        InMemoryReadModelStore inner = new();
        EventStoreProcessOptions options = new();
        var pending = new Dictionary<string, ProcessSource>(StringComparer.Ordinal);
        for (int index = 0; index < 128; index++)
        {
            ProcessSource prior = new(Source.Tenant, Source.Domain, $"pending-{index:0000}");
            pending.Add(ProcessStateKeys.SourceId(prior), prior);
        }
        inner.SeedRaw(options.StateStoreName, ProcessStateKeys.Sweep,
            new ProcessSweepState(1, 1, 1, false, DateTimeOffset.UtcNow, null, null, false,
                0, 0, 0, 0, false, true, 0, new Dictionary<string, int>(),
                new Dictionary<string, ProcessPendingDisposition>(), null, null, 0)
            { PendingPublications = pending, RetainedCandidates = pending.Count });
        ProcessCheckpointStore first = new(inner, Options.Create(options), TimeProvider.System);

        await Should.ThrowAsync<InvalidOperationException>(() => first.EnsureCandidateAsync(Source));

        ProcessSweepStore sweeps = new(inner, Options.Create(options), TimeProvider.System);
        (await sweeps.GetAsync()).PendingPublications.Values.ShouldNotContain(Source);
        (await first.ReadCandidatePageAsync(Source.Tenant, 0)).ShouldBeNull();
    }

    /// <summary>A publication that cannot repair yields pass capacity to a later catalog source.</summary>
    [Fact]
    public async Task StuckPublicationRepairDoesNotStarveLaterCatalogSource()
    {
        InMemoryReadModelStore inner = new();
        EventStoreProcessOptions options = new() { SourcesPerPass = 1 };
        ProcessSource stuck = new("tenant-a", "sample", "stuck");
        ProcessSource later = new("tenant-b", "sample", "later");
        ProcessSweepStore sweeps = new(inner, Options.Create(options), TimeProvider.System);
        (await sweeps.PreparePublicationAsync(stuck)).ShouldBeTrue();
        RejectProcessMarkerStore rejecting = new(inner, ProcessStateKeys.Candidates(stuck.Tenant, 0), alwaysReject: true);
        IProcessSourceCatalog catalog = Substitute.For<IProcessSourceCatalog>();
        catalog.ListAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(call.ArgAt<string?>(0) is null
                ? new ProcessSourcePage([later], null)
                : new ProcessSourcePage([], null)));
        (ProcessCheckpointStore checkpoints, ProcessReconciler reconciler) = Runtime(inner, options, rejecting, catalog);

        using (reconciler)
        {
            for (int pass = 0; pass < 12 && (await checkpoints.GetAsync(later)).Checkpoint == 0; pass++)
                _ = await reconciler.RunPassAsync();
        }

        (await checkpoints.GetAsync(later)).Checkpoint.ShouldBe(1);
        (await sweeps.GetAsync()).PendingPublications.Values.ShouldContain(stuck);
    }

    /// <summary>Four failed publication intents each receive a repair turn despite queue parity.</summary>
    [Fact]
    public async Task PublicationRepairRotatesAcrossFourFailedIntents()
    {
        InMemoryReadModelStore inner = new();
        EventStoreProcessOptions options = new() { SourcesPerPass = 2 };
        ProcessSweepStore sweeps = new(inner, Options.Create(options), TimeProvider.System);
        ProcessSource[] sources = Enumerable.Range(0, 4)
            .Select(index => new ProcessSource(Source.Tenant, Source.Domain, $"repair-{index}"))
            .ToArray();
        foreach (ProcessSource source in sources)
            (await sweeps.PreparePublicationAsync(source)).ShouldBeTrue();
        RejectProcessMarkerStore rejecting = new(inner, ProcessStateKeys.Candidates(Source.Tenant, 0), alwaysReject: true);
        (_, ProcessReconciler reconciler) = Runtime(inner, options, rejecting);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        using (reconciler)
        {
            for (int pass = 0; pass < 12 && visited.Count < sources.Length; pass++)
            {
                _ = await reconciler.RunPassAsync();
                string? key = (await sweeps.GetAsync()).PublicationRepairAfterKey;
                if (key is not null) _ = visited.Add(key);
            }
        }

        visited.SetEquals(sources.Select(ProcessStateKeys.SourceId)).ShouldBeTrue();
        (await sweeps.GetAsync()).PendingPublications.Count.ShouldBe(4);
    }

    /// <summary>An oversized pending identity is rejected before it creates index state.</summary>
    [Fact]
    public async Task PendingPublicationByteOverloadWritesNoIndex()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new();
        var pending = new Dictionary<string, ProcessSource>(StringComparer.Ordinal);
        for (int index = 0; index < 100; index++)
        {
            ProcessSource prior = new(Source.Tenant, Source.Domain, $"{index:000}-{new string('a', 252)}");
            pending.Add(ProcessStateKeys.SourceId(prior), prior);
        }
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Sweep,
            new ProcessSweepState(1, 1, 1, false, DateTimeOffset.UtcNow, null, null, false,
                0, 0, 0, 0, false, true, 0, new Dictionary<string, int>(),
                new Dictionary<string, ProcessPendingDisposition>(), null, null, 0)
            { PendingPublications = pending, RetainedCandidates = pending.Count });
        ProcessCheckpointStore checkpoints = new(store, Options.Create(options), TimeProvider.System);

        await Should.ThrowAsync<InvalidOperationException>(() => checkpoints.EnsureCandidateAsync(Source));

        (await new ProcessSweepStore(store, Options.Create(options), TimeProvider.System).GetAsync()).PendingPublications.Count.ShouldBe(100);
        (await checkpoints.ReadCandidatePageAsync(Source.Tenant, 0)).ShouldBeNull();
    }

    /// <summary>Every partial publication remains discoverable after the catalog removes its source.</summary>
    [Theory]
    [InlineData("candidate-page")]
    [InlineData("candidate-marker")]
    [InlineData("tenant-page")]
    [InlineData("tenant-marker")]
    public async Task PublicationCrashAtEachWriteRecoversAfterCatalogRemoval(string phase)
    {
        InMemoryReadModelStore inner = new();
        EventStoreProcessOptions options = new() { SourcesPerPass = 4 };
        FaultInjectingProcessStore crashing = new(inner, (key, value) => phase switch
        {
            "candidate-page" => value is ProcessIndexPage<ProcessSource>,
            "candidate-marker" => key == ProcessStateKeys.CandidateMarker(Source),
            "tenant-page" => value is ProcessIndexPage<string>,
            "tenant-marker" => key == ProcessStateKeys.TenantMarker(Source.Tenant),
            _ => false,
        });
        ProcessCheckpointStore failed = new(crashing, Options.Create(options), TimeProvider.System);
        await Should.ThrowAsync<InvalidOperationException>(() => failed.EnsureCandidateAsync(Source));
        ProcessSweepState pending = await new ProcessSweepStore(inner, Options.Create(options), TimeProvider.System).GetAsync();
        pending.PendingPublications.Values.ShouldContain(Source);

        (ProcessCheckpointStore checkpoints, ProcessReconciler reconciler) = Runtime(inner, options);
        using (reconciler)
        {
            for (int attempt = 0; attempt < 20 && (await checkpoints.GetAsync(Source)).Checkpoint == 0; attempt++)
                _ = await reconciler.RunPassAsync();
        }

        (await checkpoints.GetAsync(Source)).Checkpoint.ShouldBe(1);
        (await checkpoints.ContainsCandidateAsync(Source)).ShouldBeTrue();
        (await new ProcessSweepStore(inner, Options.Create(options), TimeProvider.System).GetAsync()).PendingPublications.ShouldBeEmpty();
    }

    /// <summary>A rejected marker CAS retains the exact source until both markers are durable.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FalseMarkerSaveRetainsRepairIdentity(bool tenantMarker)
    {
        InMemoryReadModelStore inner = new();
        EventStoreProcessOptions options = new();
        string key = tenantMarker ? ProcessStateKeys.TenantMarker(Source.Tenant) : ProcessStateKeys.CandidateMarker(Source);
        ProcessCheckpointStore failed = new(new RejectProcessMarkerStore(inner, key), Options.Create(options), TimeProvider.System);

        await Should.ThrowAsync<InvalidOperationException>(() => failed.EnsureCandidateAsync(Source));

        ProcessSweepStore sweeps = new(inner, Options.Create(options), TimeProvider.System);
        (await sweeps.GetAsync()).PendingPublications.Values.ShouldContain(Source);
        (await inner.GetAsync<object>(options.StateStoreName, key)).Value.ShouldBeNull();
        (await failed.ContainsCandidateAsync(Source)).ShouldBeFalse();

        (ProcessCheckpointStore repaired, ProcessReconciler reconciler) = Runtime(inner, options);
        using (reconciler)
        {
            for (int attempt = 0; attempt < 20 && (await repaired.GetAsync(Source)).Checkpoint == 0; attempt++)
                _ = await reconciler.RunPassAsync();
        }

        (await repaired.GetAsync(Source)).Checkpoint.ShouldBe(1);
        (await repaired.ContainsCandidateAsync(Source)).ShouldBeTrue();
        (await sweeps.GetAsync()).PendingPublications.ShouldBeEmpty();
        (await repaired.ReadCandidatePageAsync(Source.Tenant, 0)).ShouldNotBeNull().Entries.Count(item => item == Source).ShouldBe(1);
        (await repaired.ReadTenantPageAsync(0)).ShouldNotBeNull().Entries.Count(item => item == Source.Tenant).ShouldBe(1);
    }

    /// <summary>A losing marker CAS may mean a concurrent writer published the exact marker.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentMarkerSaveIsVerifiedBeforePublicationCompletes(bool tenantMarker)
    {
        InMemoryReadModelStore inner = new();
        EventStoreProcessOptions options = new();
        string key = tenantMarker ? ProcessStateKeys.TenantMarker(Source.Tenant) : ProcessStateKeys.CandidateMarker(Source);
        ProcessCheckpointStore checkpoints = new(new RejectProcessMarkerStore(inner, key, persistBeforeReject: true),
            Options.Create(options), TimeProvider.System);

        await checkpoints.EnsureCandidateAsync(Source);

        (await checkpoints.ContainsCandidateAsync(Source)).ShouldBeTrue();
        (await new ProcessSweepStore(inner, Options.Create(options), TimeProvider.System).GetAsync()).PendingPublications.ShouldBeEmpty();
    }

    /// <summary>Repeated failure for one pending source contributes one publication reason to a cycle.</summary>
    [Fact]
    public async Task RepeatedPublicationFailureCountsOneSourcePerCycle()
    {
        InMemoryReadModelStore inner = new();
        EventStoreProcessOptions options = new() { SourcesPerPass = 4 };
        RejectProcessMarkerStore failing = new(inner, ProcessStateKeys.CandidateMarker(Source), alwaysReject: true);
        IProcessSourceCatalog catalog = Substitute.For<IProcessSourceCatalog>();
        catalog.ListAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(call.ArgAt<string?>(0) is null
                ? new ProcessSourcePage([Source], Source.CatalogKey)
                : new ProcessSourcePage([], null)));
        (_, ProcessReconciler reconciler) = Runtime(inner, options, failing, catalog);
        using (reconciler)
        {
            for (int attempt = 0; attempt < 3; attempt++)
                _ = await reconciler.RunPassAsync();
        }

        ProcessSweepState sweep = await new ProcessSweepStore(inner, Options.Create(options), TimeProvider.System).GetAsync();
        sweep.PendingPublications.Values.ShouldContain(Source);
        sweep.Reasons["candidate-publication"].ShouldBe(1);
        sweep.Unresolved.ShouldBe(1);
    }

    /// <summary>A restart resumes historical marker repair after at most eight candidate-page reads per call.</summary>
    [Fact]
    public async Task CandidateMarkerRepairResumesAcrossBoundedPasses()
    {
        InMemoryReadModelStore inner = new();
        EventStoreProcessOptions options = new();
        ProcessSweepStore sweeps = new(inner, Options.Create(options), TimeProvider.System);
        (await sweeps.PreparePublicationAsync(Source)).ShouldBeTrue();
        for (int page = 0; page < 18; page++)
            inner.SeedRaw(options.StateStoreName, ProcessStateKeys.Candidates(Source.Tenant, page),
                new ProcessIndexPage<ProcessSource>(1, page,
                    [page == 17 ? Source : new ProcessSource(Source.Tenant, Source.Domain, $"prior-{page:00}")],
                    page == 17 ? null : page + 1));
        inner.SeedRaw(options.StateStoreName, ProcessStateKeys.TenantMarker(Source.Tenant), new ProcessTenantMarker(1, Source.Tenant));
        FaultInjectingProcessStore counted = new(inner, static (_, _) => false);

        ProcessCheckpointStore first = new(counted, Options.Create(options), TimeProvider.System);
        (await first.EnsureCandidateAsync(Source, null, CancellationToken.None)).ShouldBeFalse();
        counted.CandidatePageReads.ShouldBe(8);
        (await sweeps.GetAsync()).PublicationCandidateRepairPages[ProcessStateKeys.SourceId(Source)].ShouldBe(8);
        (await sweeps.GetAsync()).PendingPublications.Values.ShouldContain(Source);

        ProcessCheckpointStore restarted = new(counted, Options.Create(options), TimeProvider.System);
        (await restarted.EnsureCandidateAsync(Source, null, CancellationToken.None)).ShouldBeFalse();
        counted.CandidatePageReads.ShouldBe(16);
        (await restarted.EnsureCandidateAsync(Source, null, CancellationToken.None)).ShouldBeTrue();

        counted.CandidatePageReads.ShouldBe(18);
        (await restarted.ContainsCandidateAsync(Source)).ShouldBeTrue();
        (await sweeps.GetAsync()).PendingPublications.ShouldBeEmpty();
        (await sweeps.GetAsync()).PublicationCandidateRepairPages.ShouldBeEmpty();
    }

    /// <summary>A restart also resumes the tenant-marker search without walking the full tenant history.</summary>
    [Fact]
    public async Task TenantMarkerRepairResumesAcrossBoundedPasses()
    {
        InMemoryReadModelStore inner = new();
        EventStoreProcessOptions options = new();
        ProcessSweepStore sweeps = new(inner, Options.Create(options), TimeProvider.System);
        (await sweeps.PreparePublicationAsync(Source)).ShouldBeTrue();
        inner.SeedRaw(options.StateStoreName, ProcessStateKeys.Candidates(Source.Tenant, 0),
            new ProcessIndexPage<ProcessSource>(1, 0, [Source], null));
        inner.SeedRaw(options.StateStoreName, ProcessStateKeys.CandidateMarker(Source), new ProcessCandidateMarker(1, Source));
        for (int page = 0; page < 18; page++)
            inner.SeedRaw(options.StateStoreName, ProcessStateKeys.Tenants(page),
                new ProcessIndexPage<string>(1, page, [page == 17 ? Source.Tenant : $"tenant-{page:02}"],
                    page == 17 ? null : page + 1));
        FaultInjectingProcessStore counted = new(inner, static (_, _) => false);

        ProcessCheckpointStore first = new(counted, Options.Create(options), TimeProvider.System);
        (await first.EnsureCandidateAsync(Source, null, CancellationToken.None)).ShouldBeFalse();
        counted.TenantPageReads.ShouldBe(8);
        (await sweeps.GetAsync()).PublicationTenantRepairPages[ProcessStateKeys.SourceId(Source)].ShouldBe(8);

        ProcessCheckpointStore restarted = new(counted, Options.Create(options), TimeProvider.System);
        (await restarted.EnsureCandidateAsync(Source, null, CancellationToken.None)).ShouldBeFalse();
        counted.TenantPageReads.ShouldBe(16);
        (await restarted.EnsureCandidateAsync(Source, null, CancellationToken.None)).ShouldBeTrue();

        counted.TenantPageReads.ShouldBe(18);
        (await restarted.ContainsCandidateAsync(Source)).ShouldBeTrue();
        (await sweeps.GetAsync()).PendingPublications.ShouldBeEmpty();
        (await sweeps.GetAsync()).PublicationTenantRepairPages.ShouldBeEmpty();
    }

    /// <summary>A crash after linking a full page is retried without duplicating its first entry.</summary>
    [Fact]
    public async Task LinkWriteCrashRetainsExactCandidate()
    {
        InMemoryReadModelStore inner = new();
        EventStoreProcessOptions options = new();
        ProcessCheckpointStore normal = new(inner, Options.Create(options), TimeProvider.System);
        for (int index = 0; index < 256; index++)
            await normal.EnsureCandidateAsync(new ProcessSource("tenant-a", "sample", $"prior-{index:000}"));
        FaultInjectingProcessStore crashing = new(inner, (key, value) => key == ProcessStateKeys.Candidates(Source.Tenant, 0)
            && value is ProcessIndexPage<ProcessSource> { NextPage: 1 });
        ProcessCheckpointStore failed = new(crashing, Options.Create(options), TimeProvider.System);
        await Should.ThrowAsync<InvalidOperationException>(() => failed.EnsureCandidateAsync(Source));
        await normal.EnsureCandidateAsync(Source);

        ProcessIndexPage<ProcessSource> second = (await normal.ReadCandidatePageAsync(Source.Tenant, 1)).ShouldNotBeNull();
        second.Entries.Count(item => item == Source).ShouldBe(1);
        (await new ProcessSweepStore(inner, Options.Create(options), TimeProvider.System).GetAsync()).PendingPublications.ShouldBeEmpty();
    }

    /// <summary>A crash after linking a tenant page retains its first source without a catalog retry.</summary>
    [Fact]
    public async Task TenantLinkWriteCrashRecoversAfterCatalogRemoval()
    {
        InMemoryReadModelStore inner = new();
        EventStoreProcessOptions options = new() { SourcesPerPass = 4096 };
        ProcessCheckpointStore normal = new(inner, Options.Create(options), TimeProvider.System);
        for (int index = 0; index < 256; index++)
            await normal.EnsureCandidateAsync(new ProcessSource($"tenant-{index:000}", "sample", "prior"));
        FaultInjectingProcessStore crashing = new(inner, (key, value) => key == ProcessStateKeys.Tenants(0)
            && value is ProcessIndexPage<string> { NextPage: 1 });
        ProcessSource later = new("tenant-z", "sample", "later");
        ProcessCheckpointStore failed = new(crashing, Options.Create(options), TimeProvider.System);
        await Should.ThrowAsync<InvalidOperationException>(() => failed.EnsureCandidateAsync(later));

        (ProcessCheckpointStore checkpoints, ProcessReconciler reconciler) = Runtime(inner, options);
        using (reconciler)
        {
            for (int attempt = 0; attempt < 20 && (await checkpoints.GetAsync(later)).Checkpoint == 0; attempt++)
                _ = await reconciler.RunPassAsync();
        }

        (await checkpoints.GetAsync(later)).Checkpoint.ShouldBe(1);
        ProcessIndexPage<string> second = (await checkpoints.ReadTenantPageAsync(1)).ShouldNotBeNull();
        second.Entries.Count(value => value == later.Tenant).ShouldBe(1);
        (await new ProcessSweepStore(inner, Options.Create(options), TimeProvider.System).GetAsync()).PendingPublications.ShouldBeEmpty();
    }

    /// <summary>Normal registration reads only the current tail, even with several retained pages.</summary>
    [Fact]
    public async Task RegistrationCostDoesNotGrowWithHistoricalPages()
    {
        InMemoryReadModelStore inner = new();
        EventStoreProcessOptions options = new()
        {
            SupportedCandidateCount = 521,
            SourcesPerPass = 4096,
            OperationTimeout = TimeSpan.FromMilliseconds(100),
            MaxEffectsPerEnvelope = 1,
            SourceServiceQuantum = TimeSpan.FromMilliseconds(800),
            ReconciliationInterval = TimeSpan.FromSeconds(1),
            ReadinessMaxAge = TimeSpan.FromMinutes(30),
        };
        ProcessCheckpointStore normal = new(inner, Options.Create(options), TimeProvider.System);
        for (int index = 0; index < 520; index++)
            await normal.EnsureCandidateAsync(new ProcessSource("tenant-a", "sample", $"prior-{index:0000}"));
        FaultInjectingProcessStore counted = new(inner, static (_, _) => false);
        ProcessCheckpointStore measured = new(counted, Options.Create(options), TimeProvider.System);

        await measured.EnsureCandidateAsync(Source);

        counted.CandidatePageReads.ShouldBeLessThan(5);
        (await measured.ContainsCandidateAsync(Source)).ShouldBeTrue();
    }

    /// <summary>A fresh tenant avoids a scan through historical tenant pages.</summary>
    [Fact]
    public async Task FreshTenantRegistrationUsesTailAfterManyTenants()
    {
        InMemoryReadModelStore inner = new();
        EventStoreProcessOptions options = new()
        {
            SupportedCandidateCount = 521,
            SourcesPerPass = 4096,
            OperationTimeout = TimeSpan.FromMilliseconds(100),
            MaxEffectsPerEnvelope = 1,
            SourceServiceQuantum = TimeSpan.FromMilliseconds(800),
            ReconciliationInterval = TimeSpan.FromSeconds(1),
            ReadinessMaxAge = TimeSpan.FromMinutes(30),
        };
        ProcessCheckpointStore normal = new(inner, Options.Create(options), TimeProvider.System);
        for (int index = 0; index < 520; index++)
            await normal.EnsureCandidateAsync(new ProcessSource($"tenant-{index:0000}", "sample", "prior"));
        FaultInjectingProcessStore counted = new(inner, static (_, _) => false);
        ProcessCheckpointStore measured = new(counted, Options.Create(options), TimeProvider.System);

        await measured.EnsureCandidateAsync(new ProcessSource("tenant-new", "sample", "source"));

        counted.TenantPageReads.ShouldBeLessThan(5);
        (await measured.ReadTenantPageAsync(2)).ShouldNotBeNull().Entries.ShouldContain("tenant-new");
    }

    /// <summary>An unpublished identity has no marker-write crash to repair and needs no page scan.</summary>
    [Fact]
    public async Task NegativeCandidateMembershipDoesNotTraverseRegistry()
    {
        InMemoryReadModelStore inner = new();
        EventStoreProcessOptions options = new();
        ProcessCheckpointStore normal = new(inner, Options.Create(options), TimeProvider.System);
        for (int index = 0; index < 520; index++)
            await normal.EnsureCandidateAsync(new ProcessSource(Source.Tenant, "sample", $"prior-{index:0000}"));
        FaultInjectingProcessStore counted = new(inner, static (_, _) => false);
        ProcessCheckpointStore measured = new(counted, Options.Create(options), TimeProvider.System);

        (await measured.ContainsCandidateAsync(Source)).ShouldBeFalse();

        counted.CandidatePageReads.ShouldBe(0);
    }

    /// <summary>Disposition membership stays bounded while marker-write recovery remains possible.</summary>
    [Fact]
    public async Task PendingMarkerCrashIsNotYetPublishedForDisposition()
    {
        InMemoryReadModelStore inner = new();
        EventStoreProcessOptions options = new();
        ProcessCheckpointStore crashing = new(new FaultOnceOnCandidateMarkerStore(inner, Source), Options.Create(options), TimeProvider.System);
        await Should.ThrowAsync<InvalidOperationException>(() => crashing.EnsureCandidateAsync(Source));
        FaultInjectingProcessStore counted = new(inner, static (_, _) => false);
        ProcessCheckpointStore recovered = new(counted, Options.Create(options), TimeProvider.System);

        (await recovered.ContainsCandidateAsync(Source)).ShouldBeFalse();
        counted.CandidatePageReads.ShouldBe(0);
        await recovered.EnsureCandidateAsync(Source);
        (await recovered.ContainsCandidateAsync(Source)).ShouldBeTrue();
        (await recovered.ReadCandidatePageAsync(Source.Tenant, 0)).ShouldNotBeNull().Entries.Count(item => item == Source).ShouldBe(1);
    }

    /// <summary>Publishing many sources in one tenant does not duplicate the tenant registry entry.</summary>
    [Fact]
    public async Task ExistingTenantMarkerPreventsDuplicateTenantEntries()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new();
        ProcessCheckpointStore checkpoints = new(store, Options.Create(options), TimeProvider.System);
        for (int index = 0; index < 300; index++)
            await checkpoints.EnsureCandidateAsync(new ProcessSource(Source.Tenant, Source.Domain, $"source-{index:000}"));

        ProcessIndexPage<string> tenants = (await checkpoints.ReadTenantPageAsync(0)).ShouldNotBeNull();
        tenants.Entries.ShouldBe([Source.Tenant]);
        tenants.NextPage.ShouldBeNull();
        (await checkpoints.ReadCandidatePageAsync(Source.Tenant, 1)).ShouldNotBeNull();
    }

    /// <summary>A missing linked candidate page is unresolved, while a later sequential page still executes.</summary>
    [Theory]
    [InlineData(2, false)]
    [InlineData(6, true)]
    public async Task MissingLinkedCandidatePageDoesNotHideLaterSource(int laterPage, bool seedTail)
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { SourcesPerPass = 4 };
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Tenants(0), new ProcessIndexPage<string>(1, 0, [Source.Tenant], null));
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Candidates(Source.Tenant, 0),
            new ProcessIndexPage<ProcessSource>(1, 0, [new ProcessSource(Source.Tenant, "sample", "earlier")], 1));
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Candidates(Source.Tenant, laterPage),
            new ProcessIndexPage<ProcessSource>(1, laterPage, [Source], null));
        if (seedTail)
            store.SeedRaw(options.StateStoreName, ProcessStateKeys.CandidatesTail(Source.Tenant), new ProcessIndexTail(1, laterPage));
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.CandidateMarker(Source), new ProcessCandidateMarker(1, Source));
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.TenantMarker(Source.Tenant), new ProcessTenantMarker(1, Source.Tenant));
        (ProcessCheckpointStore checkpoints, ProcessReconciler reconciler) = Runtime(store, options);

        using (reconciler)
        {
            for (int attempt = 0; attempt < 30 && (await checkpoints.GetAsync(Source)).Checkpoint == 0; attempt++)
                _ = await reconciler.RunPassAsync();
        }

        (await checkpoints.GetAsync(Source)).Checkpoint.ShouldBe(1);
        ProcessSweepState sweep = await new ProcessSweepStore(store, Options.Create(options), TimeProvider.System).GetAsync();
        sweep.Reasons.ShouldContainKey("candidate-page-missing");
        sweep.Unresolved.ShouldBeGreaterThan(0);
        (await new ProcessReadinessHealthCheck(new ProcessSweepStore(store, Options.Create(options), TimeProvider.System),
            Options.Create(options), TimeProvider.System).CheckHealthAsync(new HealthCheckContext())).Status.ShouldBe(HealthStatus.Degraded);
    }

    /// <summary>A missing linked tenant page is unresolved, while a later tenant still executes.</summary>
    [Theory]
    [InlineData(2, false)]
    [InlineData(6, true)]
    public async Task MissingLinkedTenantPageDoesNotHideLaterTenant(int laterPage, bool seedTail)
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { SourcesPerPass = 4 };
        ProcessSource later = new("tenant-b", "sample", "later");
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Tenants(0), new ProcessIndexPage<string>(1, 0, ["tenant-a"], 1));
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Tenants(laterPage), new ProcessIndexPage<string>(1, laterPage, [later.Tenant], null));
        if (seedTail)
            store.SeedRaw(options.StateStoreName, ProcessStateKeys.TenantsTail, new ProcessIndexTail(1, laterPage));
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Candidates(later.Tenant, 0), new ProcessIndexPage<ProcessSource>(1, 0, [later], null));
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.CandidateMarker(later), new ProcessCandidateMarker(1, later));
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.TenantMarker(later.Tenant), new ProcessTenantMarker(1, later.Tenant));
        (ProcessCheckpointStore checkpoints, ProcessReconciler reconciler) = Runtime(store, options);

        using (reconciler)
        {
            for (int attempt = 0; attempt < 30 && (await checkpoints.GetAsync(later)).Checkpoint == 0; attempt++)
                _ = await reconciler.RunPassAsync();
        }

        (await checkpoints.GetAsync(later)).Checkpoint.ShouldBe(1);
        ProcessSweepState sweep = await new ProcessSweepStore(store, Options.Create(options), TimeProvider.System).GetAsync();
        sweep.Reasons.ShouldContainKey("tenant-page-missing");
        (await new ProcessReadinessHealthCheck(new ProcessSweepStore(store, Options.Create(options), TimeProvider.System),
            Options.Create(options), TimeProvider.System).CheckHealthAsync(new HealthCheckContext())).Status.ShouldBe(HealthStatus.Degraded);
    }

    /// <summary>A candidate page ending before its durable tail cannot hide a later retained source.</summary>
    [Fact]
    public async Task UnlinkedCandidateTailIsUnresolvedAndLaterSourceExecutes()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { SourcesPerPass = 4 };
        ProcessSource earlier = new(Source.Tenant, Source.Domain, "earlier");
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Tenants(0), new ProcessIndexPage<string>(1, 0, [Source.Tenant], null));
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Candidates(Source.Tenant, 0),
            new ProcessIndexPage<ProcessSource>(1, 0, [earlier], null));
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Candidates(Source.Tenant, 2),
            new ProcessIndexPage<ProcessSource>(1, 2, [Source], null));
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.CandidatesTail(Source.Tenant), new ProcessIndexTail(1, 2));
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.CandidateMarker(Source), new ProcessCandidateMarker(1, Source));
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.TenantMarker(Source.Tenant), new ProcessTenantMarker(1, Source.Tenant));
        (ProcessCheckpointStore checkpoints, ProcessReconciler reconciler) = Runtime(store, options);
        ProcessSweepStore sweeps = new(store, Options.Create(options), TimeProvider.System);
        using (reconciler)
        {
            for (int attempt = 0; attempt < 30 && (await checkpoints.GetAsync(Source)).Checkpoint == 0; attempt++)
                _ = await reconciler.RunPassAsync();
        }

        (await checkpoints.GetAsync(Source)).Checkpoint.ShouldBe(1);
        (await sweeps.GetAsync()).Reasons.ShouldContainKey("candidate-tail-unlinked");
        (await new ProcessReadinessHealthCheck(sweeps, Options.Create(options), TimeProvider.System)
            .CheckHealthAsync(new HealthCheckContext())).Status.ShouldBe(HealthStatus.Degraded);
    }

    /// <summary>A tenant page ending before its durable tail cannot hide a later tenant.</summary>
    [Fact]
    public async Task UnlinkedTenantTailIsUnresolvedAndLaterTenantExecutes()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { SourcesPerPass = 4 };
        ProcessSource later = new("tenant-b", "sample", "later");
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Tenants(0), new ProcessIndexPage<string>(1, 0, [Source.Tenant], null));
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Tenants(2), new ProcessIndexPage<string>(1, 2, [later.Tenant], null));
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.TenantsTail, new ProcessIndexTail(1, 2));
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Candidates(Source.Tenant, 0),
            new ProcessIndexPage<ProcessSource>(1, 0, [Source], null));
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Candidates(later.Tenant, 0),
            new ProcessIndexPage<ProcessSource>(1, 0, [later], null));
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.CandidateMarker(Source), new ProcessCandidateMarker(1, Source));
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.CandidateMarker(later), new ProcessCandidateMarker(1, later));
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.TenantMarker(Source.Tenant), new ProcessTenantMarker(1, Source.Tenant));
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.TenantMarker(later.Tenant), new ProcessTenantMarker(1, later.Tenant));
        (ProcessCheckpointStore checkpoints, ProcessReconciler reconciler) = Runtime(store, options);
        ProcessSweepStore sweeps = new(store, Options.Create(options), TimeProvider.System);
        using (reconciler)
        {
            for (int attempt = 0; attempt < 30 && (await checkpoints.GetAsync(later)).Checkpoint == 0; attempt++)
                _ = await reconciler.RunPassAsync();
        }

        (await checkpoints.GetAsync(later)).Checkpoint.ShouldBe(1);
        (await sweeps.GetAsync()).Reasons.ShouldContainKey("tenant-tail-unlinked");
        (await new ProcessReadinessHealthCheck(sweeps, Options.Create(options), TimeProvider.System)
            .CheckHealthAsync(new HealthCheckContext())).Status.ShouldBe(HealthStatus.Degraded);
    }

    /// <summary>A high tail hint with no later page remains degraded rather than completing healthy.</summary>
    [Fact]
    public async Task StaleHighTenantTailDegradesCompletedSweep()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { SourcesPerPass = 4 };
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Tenants(0), new ProcessIndexPage<string>(1, 0, [Source.Tenant], null));
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.TenantsTail, new ProcessIndexTail(1, 2));
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Candidates(Source.Tenant, 0),
            new ProcessIndexPage<ProcessSource>(1, 0, [Source], null));
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.CandidateMarker(Source), new ProcessCandidateMarker(1, Source));
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.TenantMarker(Source.Tenant), new ProcessTenantMarker(1, Source.Tenant));
        (ProcessCheckpointStore checkpoints, ProcessReconciler reconciler) = Runtime(store, options);
        ProcessSweepStore sweeps = new(store, Options.Create(options), TimeProvider.System);
        using (reconciler)
        {
            for (int attempt = 0; attempt < 30 && !(await sweeps.GetAsync()).Complete; attempt++)
                _ = await reconciler.RunPassAsync();
        }

        (await checkpoints.GetAsync(Source)).Checkpoint.ShouldBe(1);
        ProcessSweepState sweep = await sweeps.GetAsync();
        sweep.Complete.ShouldBeTrue();
        sweep.Reasons.ShouldContainKey("tenant-tail-unlinked");
        sweep.Reasons.ShouldContainKey("tenant-page-missing");
        (await new ProcessReadinessHealthCheck(sweeps, Options.Create(options), TimeProvider.System)
            .CheckHealthAsync(new HealthCheckContext())).Status.ShouldBe(HealthStatus.Degraded);
    }

    /// <summary>A persistent tenant-page exception is bounded and cannot hide a later reachable tenant.</summary>
    [Fact]
    public async Task UnreadableTenantPageExceptionStillExecutesLaterTenant()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { SourcesPerPass = 4 };
        ProcessSource later = new("tenant-b", "sample", "later");
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Tenants(0),
            new ProcessIndexPage<string>(1, 0, [Source.Tenant], 1));
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Tenants(1),
            new ProcessIndexPage<string>(1, 1, [later.Tenant], null));
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.TenantsTail, new ProcessIndexTail(1, 1));
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Candidates(later.Tenant, 0),
            new ProcessIndexPage<ProcessSource>(1, 0, [later], null));
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.CandidateMarker(later), new ProcessCandidateMarker(1, later));
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.TenantMarker(later.Tenant), new ProcessTenantMarker(1, later.Tenant));
        FaultOnProcessPageReadStore fault = new(store, ProcessStateKeys.Tenants(0));
        (ProcessCheckpointStore checkpoints, ProcessReconciler reconciler) = Runtime(store, options, fault);
        using (reconciler)
        {
            for (int attempt = 0; attempt < 30 && (await checkpoints.GetAsync(later)).Checkpoint == 0; attempt++)
                _ = await reconciler.RunPassAsync();
        }

        fault.PageReads.ShouldBeGreaterThan(0);
        (await checkpoints.GetAsync(later)).Checkpoint.ShouldBe(1);
        ProcessSweepState sweep = await new ProcessSweepStore(store, Options.Create(options), TimeProvider.System).GetAsync();
        sweep.Reasons.ShouldContainKey("tenant-page-unreadable");
    }

    /// <summary>A persistent candidate-page exception yields to a later tenant after finite probes.</summary>
    [Fact]
    public async Task UnreadableCandidatePageExceptionStillExecutesLaterTenant()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { SourcesPerPass = 4 };
        ProcessSource later = new("tenant-b", "sample", "later");
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Tenants(0),
            new ProcessIndexPage<string>(1, 0, [Source.Tenant, later.Tenant], null));
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Candidates(Source.Tenant, 0),
            new ProcessIndexPage<ProcessSource>(1, 0, [Source], null));
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Candidates(later.Tenant, 0),
            new ProcessIndexPage<ProcessSource>(1, 0, [later], null));
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.CandidateMarker(later), new ProcessCandidateMarker(1, later));
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.TenantMarker(later.Tenant), new ProcessTenantMarker(1, later.Tenant));
        FaultOnProcessPageReadStore fault = new(store, ProcessStateKeys.Candidates(Source.Tenant, 0));
        (ProcessCheckpointStore checkpoints, ProcessReconciler reconciler) = Runtime(store, options, fault);
        using (reconciler)
        {
            for (int attempt = 0; attempt < 30 && (await checkpoints.GetAsync(later)).Checkpoint == 0; attempt++)
                _ = await reconciler.RunPassAsync();
        }

        fault.PageReads.ShouldBeGreaterThan(0);
        (await checkpoints.GetAsync(later)).Checkpoint.ShouldBe(1);
        ProcessSweepState sweep = await new ProcessSweepStore(store, Options.Create(options), TimeProvider.System).GetAsync();
        sweep.Reasons.ShouldContainKey("candidate-page-unreadable");
    }

    /// <summary>A new source cannot replace the missing root of an already published tenant.</summary>
    [Fact]
    public async Task MissingCandidateRootRetainsUnknownContents()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new();
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.TenantMarker(Source.Tenant), new ProcessTenantMarker(1, Source.Tenant));
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Tenants(0), new ProcessIndexPage<string>(1, 0, [Source.Tenant], null));
        ProcessCheckpointStore checkpoints = new(store, Options.Create(options), TimeProvider.System);

        (await checkpoints.EnsureCandidateAsync(Source, null, CancellationToken.None)).ShouldBeFalse();

        (await checkpoints.ReadCandidatePageAsync(Source.Tenant, 0)).ShouldBeNull();
        ProcessSweepState sweep = await new ProcessSweepStore(store, Options.Create(options), TimeProvider.System).GetAsync();
        sweep.PendingPublications.Values.ShouldContain(Source);
        sweep.PublicationCandidateUnknownPages.ShouldContainKey(ProcessStateKeys.SourceId(Source));
    }

    /// <summary>Retained history proves that a missing tenant registry root is not an empty index.</summary>
    [Fact]
    public async Task MissingTenantRootRetainsUnknownContents()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new();
        ProcessSweepStore sweeps = new(store, Options.Create(options), TimeProvider.System);
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Sweep, (await sweeps.GetAsync()) with { RetainedCandidates = 1 });
        ProcessCheckpointStore checkpoints = new(store, Options.Create(options), TimeProvider.System);

        (await checkpoints.EnsureCandidateAsync(Source, null, CancellationToken.None)).ShouldBeFalse();

        (await checkpoints.ReadTenantPageAsync(0)).ShouldBeNull();
        ProcessSweepState sweep = await sweeps.GetAsync();
        sweep.PendingPublications.Values.ShouldContain(Source);
        sweep.PublicationTenantUnknownPages.ShouldContainKey(ProcessStateKeys.SourceId(Source));
    }

    /// <summary>Uncanonical tenant entries cannot silently become empty candidate keys.</summary>
    [Fact]
    public async Task MalformedTenantEntryIsRejected()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new();
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Tenants(0), new ProcessIndexPage<string>(1, 0, ["Tenant-A"], null));
        ProcessCheckpointStore checkpoints = new(store, Options.Create(options), TimeProvider.System);

        await Should.ThrowAsync<InvalidOperationException>(() => checkpoints.ReadTenantPageAsync(0));
    }

    /// <summary>Corrupt persisted offsets beyond a page are recorded as unresolved scan evidence.</summary>
    [Theory]
    [InlineData(false, "tenant-offset-invalid")]
    [InlineData(true, "candidate-offset-invalid")]
    public async Task PersistedOffsetBeyondPageLengthDegradesSweep(bool candidateOffset, string reason)
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { SourcesPerPass = 4 };
        ProcessCheckpointStore checkpoints = new(store, Options.Create(options), TimeProvider.System);
        await checkpoints.EnsureCandidateAsync(Source);
        (_, ProcessReconciler reconciler) = Runtime(store, options);
        using (reconciler)
        {
            for (int attempt = 0; attempt < 10 && !(await new ProcessSweepStore(store, Options.Create(options), TimeProvider.System).GetAsync()).Complete; attempt++)
                _ = await reconciler.RunPassAsync();
            ProcessSweepStore sweeps = new(store, Options.Create(options), TimeProvider.System);
            ProcessSweepState complete = await sweeps.GetAsync();
            complete.Complete.ShouldBeTrue();
            store.SeedRaw(options.StateStoreName, ProcessStateKeys.Sweep, complete with
            {
                Complete = false,
                CandidatesComplete = false,
                TenantOffset = candidateOffset ? 0 : 2,
                CandidateOffset = candidateOffset ? 2 : 0,
                NextCatalog = false,
            });
            _ = await reconciler.RunPassAsync();
            (await sweeps.GetAsync()).Reasons.ShouldContainKey(reason);
        }
    }

    /// <summary>Late source registration invalidates a previously healthy completed sweep before index writes.</summary>
    [Fact]
    public async Task LateCandidateRegistrationImmediatelyDegradesReadiness()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new();
        (ProcessCheckpointStore checkpoints, ProcessReconciler reconciler) = Runtime(store, options);
        using (reconciler)
        {
            _ = await reconciler.RunPassAsync();
            ProcessSweepStore sweeps = new(store, Options.Create(options), TimeProvider.System);
            ProcessReadinessHealthCheck health = new(sweeps, Options.Create(options), TimeProvider.System);
            (await health.CheckHealthAsync(new HealthCheckContext())).Status.ShouldBe(HealthStatus.Healthy);

            await checkpoints.EnsureCandidateAsync(Source);

            (await health.CheckHealthAsync(new HealthCheckContext())).Status.ShouldBe(HealthStatus.Degraded);
            (await sweeps.GetAsync()).Complete.ShouldBeFalse();
        }
    }

    /// <summary>The local readiness check sees hosted-service stop without waiting for the stored age limit.</summary>
    [Fact]
    public async Task StoppedReconcilerImmediatelyDegradesReadiness()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { ReconciliationInterval = TimeSpan.FromSeconds(1) };
        (_, ProcessReconciler reconciler) = Runtime(store, options);
        using (reconciler)
        {
            ProcessSweepStore sweeps = new(store, Options.Create(options), TimeProvider.System);
            ProcessReadinessHealthCheck health = new(sweeps, Options.Create(options), TimeProvider.System, reconciler);
            await reconciler.StartAsync(CancellationToken.None);
            for (int attempt = 0; attempt < 100 && !(await sweeps.GetAsync()).Complete; attempt++)
                await Task.Delay(10);
            (await health.CheckHealthAsync(new HealthCheckContext())).Status.ShouldBe(HealthStatus.Healthy);

            await reconciler.StopAsync(CancellationToken.None);

            (await health.CheckHealthAsync(new HealthCheckContext())).Status.ShouldBe(HealthStatus.Degraded);
        }
    }

    /// <summary>The published host composition resolves disposition and its registered readiness check sees stop.</summary>
    [Fact]
    public async Task RegisteredDispositionAndReadinessComposeThroughHostStop()
    {
        InMemoryReadModelStore store = new();
        using IHost host = Host.CreateDefaultBuilder().ConfigureServices((_, services) =>
        {
            services.AddSingleton<IReadModelStore>(store);
            services.AddSingleton(Substitute.For<IEventStoreGatewayClient>());
            services.AddSingleton(Substitute.For<ITrustedEffectSubmitter>());
            services.AddSingleton(Substitute.For<IProcessDelegationProvider>());
            services.AddSingleton(Substitute.For<IProcessDispositionAuthorizer>());
            services.AddSingleton(Substitute.For<IProcessDispositionAuditSink>());
            services.AddEventStoreProcesses<EmptyCatalog, EmptyTranslator>(value => value.ReconciliationInterval = TimeSpan.FromSeconds(1));
        }).Build();

        using (IServiceScope scope = host.Services.CreateScope())
            scope.ServiceProvider.GetRequiredService<ProcessDispositionService>().ShouldNotBeNull();
        await host.StartAsync();
        ProcessSweepStore sweeps = host.Services.GetRequiredService<ProcessSweepStore>();
        for (int attempt = 0; attempt < 500 && !(await sweeps.GetAsync()).Complete; attempt++)
            await Task.Delay(10);
        (await sweeps.GetAsync()).Complete.ShouldBeTrue();
        HealthCheckService checks = host.Services.GetRequiredService<HealthCheckService>();
        HealthReport running = await checks.CheckHealthAsync(static registration => registration.Name == "eventstore-processes-unresolved");
        running.Entries["eventstore-processes-unresolved"].Status.ShouldBe(HealthStatus.Healthy);

        await host.StopAsync();

        HealthReport stopped = await checks.CheckHealthAsync(static registration => registration.Name == "eventstore-processes-unresolved");
        stopped.Entries["eventstore-processes-unresolved"].Status.ShouldBe(HealthStatus.Degraded);
        ProcessSweepState covered = await sweeps.GetAsync();
        store.SeedRaw("statestore", ProcessStateKeys.Sweep, covered with
        {
            Complete = false,
            Unresolved = 1,
            Reasons = new Dictionary<string, int> { ["source-read"] = 1 },
        });
        HealthReport unresolved = await checks.CheckHealthAsync(static registration => registration.Name == "eventstore-processes-unresolved");
        unresolved.Entries["eventstore-processes-unresolved"].Status.ShouldBe(HealthStatus.Degraded);
        ((Dictionary<string, int>)unresolved.Entries["eventstore-processes-unresolved"].Data["reasonCounts"])["source-read"].ShouldBe(1);
    }

    /// <summary>A completed sweep older than the configured freshness window is degraded.</summary>
    [Fact]
    public async Task ExpiredCompletedSweepIsNotReady()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { ReadinessMaxAge = TimeSpan.FromMinutes(1) };
        (_, ProcessReconciler reconciler) = Runtime(store, options);
        using (reconciler)
        {
            _ = await reconciler.RunPassAsync();
        }
        ProcessSweepStore sweeps = new(store, Options.Create(options), TimeProvider.System);
        ProcessSweepState complete = await sweeps.GetAsync();
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Sweep,
            complete with { CycleStartedAt = DateTimeOffset.UtcNow.AddMinutes(-3), LastCompleteAt = DateTimeOffset.UtcNow.AddMinutes(-2) });

        (await new ProcessReadinessHealthCheck(sweeps, Options.Create(options), TimeProvider.System)
            .CheckHealthAsync(new HealthCheckContext())).Status.ShouldBe(HealthStatus.Degraded);
    }

    /// <summary>Retention may exceed supported coverage, but the excess remains durable and alert-visible.</summary>
    [Fact]
    public async Task CapacityOverflowKeepsCandidateAndDegradesReadiness()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { SupportedCandidateCount = 1 };
        ProcessCheckpointStore checkpoints = new(store, Options.Create(options), TimeProvider.System);
        await checkpoints.EnsureCandidateAsync(Source);
        ProcessSource second = new("tenant-a", "sample", "second");
        await checkpoints.EnsureCandidateAsync(second);

        ProcessSweepState sweep = await new ProcessSweepStore(store, Options.Create(options), TimeProvider.System).GetAsync();
        sweep.RetainedCandidates.ShouldBe(2);
        sweep.CapacityExceeded.ShouldBeTrue();
        (await checkpoints.ContainsCandidateAsync(second)).ShouldBeTrue();
        (await new ProcessReadinessHealthCheck(new ProcessSweepStore(store, Options.Create(options), TimeProvider.System),
            Options.Create(options), TimeProvider.System).CheckHealthAsync(new HealthCheckContext())).Status.ShouldBe(HealthStatus.Degraded);
    }

    /// <summary>Configured coverage must fit catalog, candidate, and page transitions before readiness expires.</summary>
    [Fact]
    public void SupportedPopulationMustFitFreshnessBudget()
    {
        EventStoreProcessOptions options = new()
        {
            SourcesPerPass = 8,
            OperationTimeout = TimeSpan.FromMilliseconds(100),
            MaxEffectsPerEnvelope = 1,
            SourceServiceQuantum = TimeSpan.FromMilliseconds(800),
            ReconciliationInterval = TimeSpan.FromSeconds(1),
            ReadinessMaxAge = TimeSpan.FromSeconds(9),
            SupportedCandidateCount = 2,
        };
        options.Validate().ShouldBeEmpty();
        options.SupportedCandidateCount = 3;
        options.Validate().ShouldContain(value => value.Contains("SupportedCandidateCount", StringComparison.Ordinal));
    }

    /// <summary>Raising supported capacity lets a fresh complete sweep clear an earlier overflow.</summary>
    [Fact]
    public async Task CapacityIncreaseCanRestoreReadinessAfterFreshCoverage()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions oldOptions = new() { SupportedCandidateCount = 1 };
        ProcessCheckpointStore oldCheckpoints = new(store, Options.Create(oldOptions), TimeProvider.System);
        await oldCheckpoints.EnsureCandidateAsync(Source);
        await oldCheckpoints.EnsureCandidateAsync(new ProcessSource(Source.Tenant, Source.Domain, "second"));
        (await new ProcessSweepStore(store, Options.Create(oldOptions), TimeProvider.System).GetAsync()).CapacityExceeded.ShouldBeTrue();

        EventStoreProcessOptions expanded = new() { SupportedCandidateCount = 2 };
        (_, ProcessReconciler reconciler) = Runtime(store, expanded);
        using (reconciler)
        {
            _ = await reconciler.RunPassAsync();
        }
        ProcessSweepStore sweeps = new(store, Options.Create(expanded), TimeProvider.System);
        (await sweeps.GetAsync()).CapacityExceeded.ShouldBeFalse();
        (await new ProcessReadinessHealthCheck(sweeps, Options.Create(expanded), TimeProvider.System)
            .CheckHealthAsync(new HealthCheckContext())).Status.ShouldBe(HealthStatus.Healthy);
    }

    private static (ProcessCheckpointStore Checkpoints, ProcessReconciler Reconciler) Runtime(
        InMemoryReadModelStore store, EventStoreProcessOptions options, IReadModelStore? processStore = null, IProcessSourceCatalog? sourceCatalog = null)
    {
        IReadModelStore activeStore = processStore ?? store;
        ProcessCheckpointStore checkpoints = new(activeStore, Options.Create(options), TimeProvider.System);
        ProcessSweepStore sweeps = new(activeStore, Options.Create(options), TimeProvider.System);
        IProcessSourceCatalog catalog = sourceCatalog ?? Substitute.For<IProcessSourceCatalog>();
        if (sourceCatalog is null)
            catalog.ListAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(new ProcessSourcePage([], null)));
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            StreamReadRequest request = call.Arg<StreamReadRequest>();
            StreamReadEvent[] events = request.FromSequence == 0
                ? [new(1, "SyntheticEvent", [123], "json", 1, "message-1", null, null, DateTimeOffset.UtcNow, null)]
                : [];
            return Task.FromResult(new StreamReadPage(request.Tenant, request.Domain, request.AggregateId, events,
                new StreamReadMetadata(request.FromSequence, null, events.Length == 0 ? null : 1, 1, events.Length, false, null)));
        });
        IProcessTranslator translator = Substitute.For<IProcessTranslator>();
        translator.TranslateAsync(Arg.Any<ProcessSource>(), Arg.Any<StreamReadEvent>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IReadOnlyList<ProcessEffect>>([]));
        CheckpointedProcessRunner runner = new(gateway, translator, Substitute.For<IProcessDelegationProvider>(),
            Substitute.For<ITrustedEffectSubmitter>(), checkpoints, Options.Create(options));
        ServiceProvider provider = new ServiceCollection().AddSingleton(catalog).AddSingleton(runner).BuildServiceProvider();
        return (checkpoints, new ProcessReconciler(provider.GetRequiredService<IServiceScopeFactory>(), sweeps, checkpoints, Options.Create(options), TimeProvider.System));
    }
}
