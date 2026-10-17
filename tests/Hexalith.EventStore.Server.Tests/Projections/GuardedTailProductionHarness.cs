using System.Security.Cryptography;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Client;

using Hexalith.Commons.UniqueIds;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Tests.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Sample.Counter.Events;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Server.Tests.Projections;

/// <summary>Production guard, DomainService dispatch and Dapr read-model protocol over existing recording state seams.</summary>
internal sealed partial class GuardedTailProductionHarness : IDisposable
{
    internal AggregateIdentity Identity { get; } = new("tenant-a", "counter", "counter-1");
    internal RecordingDaprClient Backend { get; } = new() { PersistTypedState = true };
    internal DaprReadModelStore Models { get; }
    internal Dictionary<string, ProjectionDeliveryState> Rows { get; } = new(StringComparer.Ordinal);
    internal List<long> ReturnedSequences { get; } = [];
    internal List<(long After, long Head, int PageSize)> Ranges { get; } = [];
    internal List<GuardedTailProjectionHandler> Handlers { get; } = [];
    internal IEventPayloadProtectionService Protection { get; } = Substitute.For<IEventPayloadProtectionService>();
    internal int ProtectedReads { get; private set; }
    internal EventEnvelope[] Events { get; private set; }
    internal SourceProof Head { get; private set; }
    internal int? GrowOnInspectionNumber { get; set; }
    internal int SourceInspections { get; private set; }
    internal Exception? InspectionFailure { get; set; }
    internal Exception? StateReadFailure { get; set; }
    internal NamedProjectionRouteCatalogEntry Catalog { get; }
    internal ProjectionSourceGuardDelivery Probe { get; private set; }
    internal ProjectionSourceGuardTailDelivery Tail { get; }
    internal GuardedTailProductionHttpHandler Http { get; }
    internal ProjectionDeliveryRetryWorkItem? Work { get; private set; }
    internal int RowWrites { get; private set; }
    internal FakeTimeProvider Clock { get; }
    internal TimeSpan ReleaseDelay { get; set; }
    internal TimeSpan CleanupDelay { get; set; }
    internal bool RejectSchedulerDelete { get; set; }
    internal string? InterruptBeforePendingRoute { get; set; }
    internal string? InterruptBeforeCanonicalRoute { get; set; }
    internal TimeSpan HistoryReadDelay { get; set; }
    internal long? ChangeApplicationSequence { get; set; }
    internal int ReceiptLimit { get; }
    private readonly ServiceProvider _services;
    private readonly HttpClient _http;
    private readonly DaprClient _invocation = new DaprClientBuilder().Build();
    private readonly Dictionary<string, long> _revisions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _leases = new(StringComparer.Ordinal);
    private readonly IProjectionLifecycleGateway _lifecycle;
    private readonly IProjectionSelectedOwnerProofGateway _owners;
    private int _passStarts;
    private readonly IActorProxyFactory _actors;
    private readonly ProjectionSourceGuardHistoryReader _history;
    private readonly IProjectionDeliveryStateStore _states;
    private readonly NamedProjectionDispatchCoordinator _named;
    private readonly IProjectionDeliveryRetryScheduler _scheduler;

    private GuardedTailProductionHarness(int head, int baseSequence, int routeCount, string handlerSemantics, bool sharedLastRoute,
        int receiptLimit, bool unguardedLastRoute, bool extraDomain, int manifestLimit, bool productionScheduler, int transitionAttempts, bool mixedReplay)
    {
        ReceiptLimit = receiptLimit;
        TransitionAttempts = transitionAttempts;
        Models = new(Backend, Options.Create(new ReadModelBatchOptions { MaxCanonicalManifestBytes = manifestLimit }));
        DateTimeOffset now = DateTimeOffset.Parse("2026-10-15T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        Clock = new(now);
        SourceProof seed = SourceProofCodec.CreateSeed(Identity, "01ARZ3NDEKTSV4RRFFQ69G5FAV");
        SourceProof chain = seed;
        Events = [.. Enumerable.Range(1, head).Select(index => new EventEnvelope(UniqueIdHelper.GenerateSortableUniqueStringId(),
            Identity.AggregateId, "Counter", Identity.TenantId, Identity.Domain, index, index, now, "correlation-1", "cause-1", "user-1",
            "v1", typeof(CounterIncremented).FullName!, 1, "json", JsonSerializer.SerializeToUtf8Bytes(new CounterIncremented(), JsonSerializerOptions.Web), null))];
        foreach (EventEnvelope item in Events) chain = SourceProofCodec.Extend(chain, item);
        Head = chain;
        string[] routes = [.. Enumerable.Range(1, routeCount).Select(index => "counter-route-" + index)];
        bool mapped = handlerSemantics == "mapped-incremental-replay-v1";
        bool fullReplay = handlerSemantics == "full-replay-v1";
        var capability = new ProjectionSourceGuardCapability(1, 1, 1, handlerSemantics, "projection-owner-proof/v1",
            fullReplay ? null : "event-message-id/v1", fullReplay ? null : mapped ? "mapped-append/v1" : "read-model-batch/v1")
            { InitialOwnerProtocol = baseSequence == 0 && !mapped && !fullReplay ? "aggregate-first-event/v1" : null,
                CanonicalReplayProtocol = !mapped && !fullReplay ? "staged-full-replay/v1" : null };
        ProjectionSourceGuardCapability CapabilityFor(string route) => mixedReplay && route == routes[^1]
            ? new(1, 1, 1, "full-replay-v1", "projection-owner-proof/v1", null, null) : sharedLastRoute && route == routes[^1]
            ? new(1, 1, 1, "shared-epoch-v1", "projection-owner-proof/v1", "source-sequence-position/v1", "shared-applied/v1") : capability;
        bool Guarded(string route) => !unguardedLastRoute || route != routes[^1];
        ProjectionDispatchRoute[] descriptors = [.. routes.Select(route => new ProjectionDispatchRoute(Identity.Domain, route)
            { SourceGuard = Guarded(route) ? CapabilityFor(route) : null })];
        string fingerprint = ProjectionRouteCatalogFingerprint.Compute("sample", "v1", descriptors,
            routes.Select(route => new ProjectionDispatchRoute(Identity.Domain, route)));
        Catalog = new("sample", "v1", Identity.Domain, 2, ProjectionDispatchProtocol.Capability, fingerprint, routes,
            fullReplayProjectionTypes: mapped || fullReplay ? routes.Where(route => CapabilityFor(route).HandlerSemantics != "shared-epoch-v1") : [],
            singleEventProjectionTypes: mapped || fullReplay ? [] : routes.Where(route => CapabilityFor(route).HandlerSemantics != "shared-epoch-v1"),
            sharedEpochProjectionTypes: routes.Where(route => CapabilityFor(route).HandlerSemantics == "shared-epoch-v1"),
            mappedOnlyStreamedProjectionTypes: mapped ? routes.Where(route => CapabilityFor(route).HandlerSemantics == "mapped-incremental-replay-v1") : [],
            incrementalReplayProjectionTypes: mapped ? routes.Where(route => CapabilityFor(route).HandlerSemantics == "mapped-incremental-replay-v1") : [])
        { SourceGuardCapabilities = routes.Where(Guarded).ToDictionary(static route => route, CapabilityFor, StringComparer.Ordinal) };
        var services = new ServiceCollection();
        services.AddSingleton(Options.Create(new DomainProjectionIdentityOptions { AppId = "sample", ServiceVersion = "v1" }));
        services.AddSingleton<IReadModelBatchStore>(Models);
        services.AddSingleton<IReadModelStore>(Models);
        services.AddSingleton<IReadModelBatchStagingStore>(Models);
        services.AddSingleton<IReadModelConditionalEraser>(Models);
        services.AddSingleton<IReadModelOwnerProofStore>(Models);
        services.AddSingleton<IReadModelAppliedReceiptStore>(Models);
        services.AddSingleton<IReadModelAppliedReceiptRecoveryStore>(Models);
        foreach (string route in routes)
        {
            var handler = new GuardedTailProjectionHandler(route, Models, baseSequence == 0 && !mapped);
            Handlers.Add(handler);
            services.AddSingleton<IAsyncDomainProjectionHandler>(mixedReplay && route == routes[^1]
                ? new GuardedFullReplayProjectionHandler(handler) : handler);
            if (Guarded(route)) services.AddSingleton(new DomainProjectionOwnerProofRegistration(Identity.Domain, route, CapabilityFor(route), new("models", route)));
            _revisions[route] = 1;
        }
        if (extraDomain)
        {
            IAsyncDomainProjectionSingleEventHandler sibling = Substitute.For<IAsyncDomainProjectionSingleEventHandler>();
            sibling.Domain.Returns("other-domain");
            sibling.ProjectionType.Returns("other-route");
            sibling.OrdinaryDeliveryEnabled.Returns(true);
            services.AddSingleton<IAsyncDomainProjectionHandler>(sibling);
            services.AddSingleton(new DomainProjectionOwnerProofRegistration(sibling.Domain, sibling.ProjectionType,
                capability, new("models", sibling.ProjectionType)));
        }
        _services = services.BuildServiceProvider();
        var registry = new DomainProjectionCatalogRegistry();
        registry.Register(fingerprint, descriptors);
        Http = new(_services, registry);
        _http = new(Http);
        IHttpClientFactory clients = Substitute.For<IHttpClientFactory>();
        clients.CreateClient(Arg.Any<string>()).Returns(_http);
        _lifecycle = Substitute.For<IProjectionLifecycleGateway>();
        _lifecycle.ReadSnapshotAsync(Identity, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            string route = call.ArgAt<string>(1);
            return new ProjectionLifecycleSnapshot(_leases.ContainsKey(route) ? ProjectionLifecyclePhase.Delivering : ProjectionLifecyclePhase.Idle, _revisions[route]);
        });
        _lifecycle.BeginDeliveryWriteAsync(Identity, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            string route = call.ArgAt<string>(1), operationId = call.ArgAt<string>(2);
            if (_leases.ContainsKey(route)) return false;
            _leases[route] = operationId;
            _revisions[route]++;
            return true;
        });
        _lifecycle.CompleteDeliveryWriteAsync(Identity, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            string route = call.ArgAt<string>(1), operationId = call.ArgAt<string>(2);
            if (!_leases.TryGetValue(route, out string? current) || current != operationId) return false;
            Clock.Advance(ReleaseDelay);
            call.ArgAt<CancellationToken>(3).ThrowIfCancellationRequested();
            _leases.Remove(route);
            _revisions[route]++;
            return true;
        });
        _owners = new DaprProjectionSelectedOwnerProofGateway(_invocation, clients, _lifecycle);
        IProjectionDeliveryStateStore states = Substitute.For<IProjectionDeliveryStateStore>();
        _states = states;
        states.ReadAsync(Identity, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            if (StateReadFailure is { } failure) throw failure;
            string route = call.ArgAt<string>(1);
            if (_productionStates is not null)
                return await _productionStates.ReadAsync(Identity, route, call.ArgAt<CancellationToken>(2)).ConfigureAwait(false);
            ProjectionSourceGuard? marker = Catalog.SourceGuardCapabilities.TryGetValue(route, out ProjectionSourceGuardCapability? descriptor)
                ? new(1, 1, 2, 2, 1, Identity.TenantId, Identity.Domain, Identity.AggregateId,
                    route, seed.SourceIncarnation, fingerprint, descriptor.HandlerSemantics,
                    ProjectionSourceGuard.ApprovedSpecSha256, new string('b', 40), now) : null;
            return new ProjectionDeliveryStateReadResult(Rows.GetValueOrDefault(route), RowWrites.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ProjectionDeliveryStateClassification.Current, true) { SourceGuard = marker };
        });
        states.TrySaveAsync(Identity, Arg.Any<string>(), Arg.Any<ProjectionDeliveryState>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            if (InterruptBeforeCanonicalRoute == call.ArgAt<string>(1)
                && call.ArgAt<ProjectionDeliveryState>(2).ActiveReservation?.GuardBinding?.CanonicalReplay == true)
                throw new InvalidOperationException("injected canonical mode switch interruption");
            if (InterruptBeforePendingRoute == call.ArgAt<string>(1) && call.ArgAt<ProjectionDeliveryState>(2).IdleBindingPending is not null)
            {
                throw new InvalidOperationException("injected pending interruption");
            }
            if (_productionStates is not null)
            {
                bool saved = await _productionStates.TrySaveAsync(Identity, call.ArgAt<string>(1), call.ArgAt<ProjectionDeliveryState>(2),
                    call.ArgAt<string>(3), call.ArgAt<CancellationToken>(4)).ConfigureAwait(false);
                if (saved)
                {
                    string route = call.ArgAt<string>(1);
                    ProjectionDeliveryState updated = call.ArgAt<ProjectionDeliveryState>(2);
                    bool completed = updated.LastDeliveredSequence > Rows[route].LastDeliveredSequence;
                    Rows[route] = updated;
                    RowWrites++;
                    if (completed && AfterCompletionSaveAsync is { } after)
                    {
                        AfterCompletionSaveAsync = null;
                        await after().ConfigureAwait(false);
                    }
                }
                return saved;
            }
            if (call.ArgAt<string>(3) != RowWrites.ToString(System.Globalization.CultureInfo.InvariantCulture)) return false;
            Rows[call.ArgAt<string>(1)] = call.ArgAt<ProjectionDeliveryState>(2);
            RowWrites++;
            return true;
        });
        IAggregateActor source = Substitute.For<IAggregateActor>();
        source.InspectSourceProofAsync().Returns(async _ =>
        {
            if (InspectionFailure is { } failure) throw failure;
            SourceInspections++;
            if (_productionSource is not null) return await _productionSource.InspectSourceProofAsync().ConfigureAwait(false);
            if (GrowOnInspectionNumber == SourceInspections)
            {
                AppendSourceEvent();
                GrowOnInspectionNumber = null;
            }
            return new SourceProofInspection(SourceProofInspectionStatus.Available, true, Head.HeadSequence, false, Head);
        });
        source.ReadMeasuredEventsBoundedRangeAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<int>(), 2_097_152).Returns(async call =>
        {
            long after = call.ArgAt<long>(0), target = call.ArgAt<long>(1);
            int count = call.ArgAt<int>(2);
            Ranges.Add((after, target, count));
            Clock.Advance(HistoryReadDelay);
            if (_productionSource is not null)
            {
                SourceEventRange measured = await _productionSource.ReadMeasuredEventsBoundedRangeAsync(after, target, count, 2_097_152).ConfigureAwait(false);
                ReturnedSequences.AddRange(measured.Events.Select(static item => item.SequenceNumber));
                ObserveEventRange(measured);
                return measured;
            }
            if (after == baseSequence) _passStarts++;
            EventEnvelope[] returned = [.. Events.Where(item => item.SequenceNumber > after && item.SequenceNumber <= target).Take(count)];
            if (_passStarts == routeCount + 1 && ChangeApplicationSequence is { } changed)
                returned = [.. returned.Select(item => item.SequenceNumber == changed ? item with { Payload = "{\"changed\":true}"u8.ToArray() } : item)];
            ReturnedSequences.AddRange(returned.Select(static item => item.SequenceNumber));
            var response = new SourceEventRange(returned, System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(returned,
                System.Text.Json.JsonSerializerOptions.Web).Length);
            ObserveEventRange(response);
            return response;
        });
        IActorProxyFactory actors = Substitute.For<IActorProxyFactory>();
        _actors = actors;
        actors.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), Arg.Any<string>(), Arg.Any<ActorProxyOptions>())
            .Returns(source);
        IProjectionDeliveryRetryScheduler scheduler = productionScheduler
            ? new DaprProjectionDeliveryRetryScheduler(Backend, Options.Create(new ProjectionOptions()), Options.Create(new ProjectionDispatchOptions()))
            : Substitute.For<IProjectionDeliveryRetryScheduler>();
        _scheduler = scheduler;
        if (!productionScheduler)
        {
            scheduler.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => Work?.WorkId == call.ArgAt<string>(0) ? Work : null);
            scheduler.ScheduleAsync(Arg.Any<ProjectionDeliveryRetryWorkItem>(), Arg.Any<CancellationToken>()).Returns(call => Work ??= call.ArgAt<ProjectionDeliveryRetryWorkItem>(0));
            scheduler.TryAcquireAsync(Arg.Any<ProjectionDeliveryRetryWorkItem>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(call =>
            {
                if (Work is null || call.ArgAt<ProjectionDeliveryRetryWorkItem>(0).Revision != Work.Revision) return null;
                Work = Work! with { Revision = Work!.Revision + 1, LeaseOwner = call.ArgAt<string>(1), LeaseExpiresUtc = now.AddMinutes(1) };
                return Work;
            });
            scheduler.TryUpdateAsync(Arg.Any<ProjectionDeliveryRetryWorkItem>(), Arg.Any<CancellationToken>()).Returns(call =>
            {
                ProjectionDeliveryRetryWorkItem proposed = call.ArgAt<ProjectionDeliveryRetryWorkItem>(0);
                if (Work is null || proposed.Revision != Work.Revision || proposed.LeaseOwner != Work.LeaseOwner) return false;
                Work = proposed with { Revision = proposed.Revision + 1, LeaseOwner = null, LeaseExpiresUtc = null };
                return true;
            });
            scheduler.TryDeleteAsync(Arg.Any<ProjectionDeliveryRetryWorkItem>(), Arg.Any<CancellationToken>()).Returns(call =>
            {
                Clock.Advance(CleanupDelay);
                call.ArgAt<CancellationToken>(1).ThrowIfCancellationRequested();
                if (RejectSchedulerDelete) return false;
                Work = null;
                return true;
            });
        }
        var actorOptions = Options.Create(new EventStoreActorOptions());
        var history = new ProjectionSourceGuardHistoryReader(actors, Protection, actorOptions);
        _history = history;
        var coordinator = new ProjectionDeliveryIdempotencyCoordinator(states, Options.Create(new ProjectionDeliveryIdempotencyOptions
            { CompletedReceiptLimit = receiptLimit, MaxStateTransitionAttempts = transitionAttempts }), Clock);
        var finalizer = new ProjectionSourceGuardFinalizer(coordinator, states, _lifecycle, _owners, history, actors, actorOptions, Clock);
        Tail = new(history, coordinator, scheduler, _lifecycle, _owners, new DaprProjectionSourceGuardTransport(_invocation, clients), finalizer,
            Options.Create(new ProjectionDispatchOptions()), Clock);
        INamedProjectionRouteCatalog catalogs = Substitute.For<INamedProjectionRouteCatalog>();
        catalogs.Current.Returns(new NamedProjectionRouteCatalogSnapshot([Catalog]));
        _named = new NamedProjectionDispatchCoordinator(catalogs, Substitute.For<IProjectionDeliveryCheckpointStore>(),
            _lifecycle, actors, _invocation, clients, Options.Create(new ProjectionDispatchOptions()),
            NullLogger<NamedProjectionDispatchCoordinator>.Instance, scheduler, Clock, idempotencyCoordinator: coordinator);
        Probe = new(catalogs, states, _lifecycle, _owners, actors, actorOptions, finalizer, scheduler,
            Options.Create(new ProjectionDispatchOptions()), timeProvider: Clock, tailDelivery: Tail,
            transport: new DaprProjectionSourceGuardTransport(_invocation, clients), compatibleDelivery: _named, compatibleHistory: history);
    }

    internal Task<ProjectionSourceGuardReplayAdmission> AdmitCanonicalRouteAsync(string route,
        ProjectionEventDto[]? sharedEvents = null, bool retainEvents = true)
        => _history.AdmitReplayAsync(Identity, route, Rows[route], Head, 256, true,
            CancellationToken.None, sharedEvents, retainEvents);

    internal static async Task<GuardedTailProductionHarness> CreateAsync(int head, int baseSequence, int routeCount = 1,
        string handlerSemantics = "single-event-v1", bool sharedLastRoute = false, int receiptLimit = 256,
        bool unguardedLastRoute = false, bool extraDomain = false, int manifestLimit = 1_048_576, bool productionScheduler = false, int transitionAttempts = 8,
        string ownerGeneration = "generation-1", bool mixedReplay = false)
    {
        var result = new GuardedTailProductionHarness(head, baseSequence, routeCount, handlerSemantics, sharedLastRoute, receiptLimit, unguardedLastRoute, extraDomain, manifestLimit, productionScheduler, transitionAttempts, mixedReplay);
        if (ownerGeneration != "generation-1")
            foreach (GuardedTailProjectionHandler handler in result.Handlers) handler.ReplayGenerationOverride = ownerGeneration;
        SourceProof source = SourceProofCodec.CreateSeed(result.Identity, result.Head.SourceIncarnation);
        foreach (EventEnvelope item in result.Events.Take(baseSequence)) source = SourceProofCodec.Extend(source, item);
        ProjectionEventDto[] wire = (await ProjectionEventWireBuilder.BuildAsync(Substitute.For<IEventPayloadProtectionService>(), result.Identity,
            result.Events.Take(baseSequence).ToArray(), CancellationToken.None).ConfigureAwait(false)).Events!;
        foreach (string route in result.Catalog.OrdinaryProjectionTypes)
        {
            if (baseSequence == 0)
            {
                result.Rows[route] = ProjectionDeliveryState.CreateEmpty(result.Identity, route,
                    ProjectionDeliveryFingerprint.ComputeInitial(result.Identity, route), DateTimeOffset.UtcNow) with
                { SchemaVersion = 2, GuardEvidenceVersion = 1 };
                continue;
            }
            EventEnvelope last = result.Events[baseSequence - 1];
            var selected = new ProjectionOwnerSelection(1, result.Identity.TenantId, result.Identity.Domain, result.Identity.AggregateId,
                route, "owner-1", ownerGeneration, baseSequence, last.MessageId, last.MessageId, "version-" + baseSequence);
            var model = new GuardedTailModel(baseSequence, selected.FreshnessVersion, selected);
            string key = result.Identity.ActorId + ":" + route;
            var batch = new ReadModelBatch(new("models", result.Identity.TenantId, result.Identity.Domain, result.Identity.AggregateId, route, last.MessageId),
            [ReadModelBatchOperation.Write(key, model, ReadModelBatchConcurrency.LastWrite), ReadModelBatchOperation.Write(key + "-index", model, ReadModelBatchConcurrency.LastWrite)]) { OwnerSelection = selected };
            if (!(await result.Models.ExecuteAsync(batch).ConfigureAwait(false)).IsSuccess) throw new InvalidOperationException("Fixture seed failed.");
            ProjectionOwnerProof owner = (await result.Models.ReadOwnerProofAsync(new(result.Identity.TenantId, result.Identity.Domain,
                result.Identity.AggregateId, route, result.Catalog.CatalogFingerprint), new("models", route)).ConfigureAwait(false)).Proof!;
            ProjectionDeliveryFingerprintHistory history = ProjectionDeliveryFingerprint.ComputeHistory(result.Identity, route, wire);
            ProjectionDeliveryReceipt[] receipts = [.. history.Events.TakeLast(receiptLimit).Select(static item => new ProjectionDeliveryReceipt(item.SequenceNumber,
                item.MessageId, item.EventFingerprint, item.PrefixFingerprint))];
            bool guarded = result.Catalog.SourceGuardCapabilities.TryGetValue(route, out ProjectionSourceGuardCapability? descriptor);
            result.Rows[route] = ProjectionDeliveryState.CreateEmpty(result.Identity, route, history.InitialFingerprint, DateTimeOffset.UtcNow) with
            {
                SchemaVersion = guarded ? 2 : 1, GuardEvidenceVersion = guarded ? 1 : null,
                LastDeliveredSequence = baseSequence, LastCompletedMessageId = last.MessageId,
                CompletedPrefixFingerprint = history.PrefixFingerprint, CompletedReceipts = receipts, FirstRetainedSequence = receipts[0].SequenceNumber,
                CompletedMessageIdDigests = [.. wire.Select(static item => Convert.ToBase64String(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(item.MessageId!))))],
                GuardEvidence = guarded ? new(source, result.Catalog.CatalogFingerprint, descriptor!.HandlerSemantics, owner, 1) : null,
            };
        }
        return result;
    }

    internal void AppendSourceEvent()
    {
        var item = new EventEnvelope(UniqueIdHelper.GenerateSortableUniqueStringId(), Identity.AggregateId, "Counter", Identity.TenantId,
            Identity.Domain, Head.HeadSequence + 1, Head.HeadSequence + 1, Clock.GetUtcNow(), "correlation-1", "cause-1", "user-1", "v1",
            typeof(CounterIncremented).FullName!, 1, "json", JsonSerializer.SerializeToUtf8Bytes(new CounterIncremented(), JsonSerializerOptions.Web), null);
        Events = [.. Events, item];
        Head = SourceProofCodec.Extend(Head, item);
    }

    internal void ReconstructGuardedDelivery()
    {
        INamedProjectionRouteCatalog catalogs = Substitute.For<INamedProjectionRouteCatalog>();
        catalogs.Current.Returns(new NamedProjectionRouteCatalogSnapshot([Catalog]));
        var actorOptions = Options.Create(new EventStoreActorOptions());
        var coordinator = new ProjectionDeliveryIdempotencyCoordinator(_states, Options.Create(new ProjectionDeliveryIdempotencyOptions
            { CompletedReceiptLimit = ReceiptLimit, MaxStateTransitionAttempts = TransitionAttempts }), Clock);
        var finalizer = new ProjectionSourceGuardFinalizer(coordinator, _states, _lifecycle, _owners, _history, _actors, actorOptions, Clock);
        var transport = new DaprProjectionSourceGuardTransport(_invocation, SubstituteClients());
        var tail = new ProjectionSourceGuardTailDelivery(_history, coordinator, _scheduler, _lifecycle, _owners, transport,
            finalizer, Options.Create(new ProjectionDispatchOptions()), Clock);
        Probe = new(catalogs, _states, _lifecycle, _owners, _actors, actorOptions, finalizer, _scheduler,
            Options.Create(new ProjectionDispatchOptions()), Clock, tail, transport, _named, _history);
    }

    private IHttpClientFactory SubstituteClients()
    {
        IHttpClientFactory clients = Substitute.For<IHttpClientFactory>();
        clients.CreateClient(Arg.Any<string>()).Returns(_http);
        return clients;
    }

    internal Task<ProjectionSourceGuardDeliveryResult> DeliverAsync(int pageSize)
        => Probe.TryDeliverAsync(Identity, pageSize, CancellationToken.None);

    internal async Task RunGuardedRetryAsync(ProjectionDeliveryRetryWorkItem? selectedWork = null)
    {
        if (selectedWork is not null) Work = selectedWork;
        var original = new ProjectionDeliveryRetryWorkItem(ProjectionDeliveryRetryWorkItem.CreateWorkId(Identity.TenantId,
            Identity.Domain, Identity.AggregateId, Head.HeadSequence), Identity.TenantId, Identity.Domain, Identity.AggregateId,
            Catalog.AppId, Catalog.ServiceVersion, Head.HeadSequence, Head.HeadMessageId!, Catalog.OrdinaryProjectionTypes, [],
            Head.HeadMessageId!, Catalog.CatalogFingerprint, 0, Clock.GetUtcNow(), null);
        await _scheduler.ScheduleAsync(selectedWork ?? original).ConfigureAwait(false);
        _scheduler.GetDueAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => Work is null ? [] : new[] { Work });
        using var worker = new ProjectionDeliveryRetryWorker(_scheduler, _named, _actors,
            Substitute.For<IEventPayloadProtectionService>(), Options.Create(new EventStoreActorOptions()),
            Options.Create(new ProjectionDispatchOptions()), Substitute.For<IProjectionRebuildCheckpointStore>(), Clock,
            NullLogger<ProjectionDeliveryRetryWorker>.Instance, Probe);
        await worker.RunOnceAsync(CancellationToken.None).ConfigureAwait(false);
    }

    internal async Task<ProjectionDispatchResponse> CanonicalReplayAsync(long? checkpoint = null, string? operationId = null)
    {
        ProjectionEventDto[] events = (await ProjectionEventWireBuilder.BuildAsync(Substitute.For<IEventPayloadProtectionService>(),
            Identity, [.. Events.Where(item => item.SequenceNumber <= (checkpoint ?? Head.HeadSequence))], CancellationToken.None).ConfigureAwait(false)).Events!;
        string message = events[^1].MessageId!;
        var request = new ProjectionDispatchRequest(new(Identity.TenantId, Identity.Domain, Identity.AggregateId, events)
            { RebuildSourceHeadSequence = checkpoint ?? Head.HeadSequence, RebuildSourceHeadMessageId = operationId is null ? message : null,
                RebuildOwnerGenerations = operationId is not null ? null : Catalog.ProjectionTypes.ToDictionary(static route => route,
                    _ => "generation-1", StringComparer.Ordinal),
                RebuildOwnerIdentities = operationId is not null ? null : Catalog.ProjectionTypes.ToDictionary(static route => route,
                    _ => "owner-1", StringComparer.Ordinal) }, Catalog.ProjectionTypes, operationId ?? message, Catalog.CatalogFingerprint);
        return await DomainProjectionDispatcher.RebuildAsync(_services, request, new ProjectionDispatchOptions(),
            new DomainProjectionIdentityOptions { AppId = Catalog.AppId, ServiceVersion = Catalog.ServiceVersion }, CancellationToken.None).ConfigureAwait(false);
    }

    internal async Task<ProjectionDispatchResponse> StagedCanonicalReplayAsync(string action, bool changedInput = false,
        string ownerGeneration = "generation-1", bool legacyPrivateReference = false, bool compactTransport = false)
    {
        ProjectionEventDto[] events = (await ProjectionEventWireBuilder.BuildAsync(Substitute.For<IEventPayloadProtectionService>(),
            Identity, Events, CancellationToken.None).ConfigureAwait(false)).Events!;
        if (changedInput) events[^1] = events[^1] with { Payload = "{\"changed\":true}"u8.ToArray() };
        var request = new ProjectionDispatchRequest(new(Identity.TenantId, Identity.Domain, Identity.AggregateId, events)
            { RebuildSourceHeadSequence = Head.HeadSequence, RebuildSourceHeadMessageId = Head.HeadMessageId,
                RebuildOwnerGenerations = Catalog.ProjectionTypes.ToDictionary(static route => route,
                    _ => ownerGeneration, StringComparer.Ordinal),
                RebuildOwnerIdentities = Catalog.ProjectionTypes.ToDictionary(static route => route,
                    _ => "owner-1", StringComparer.Ordinal) }, Catalog.ProjectionTypes, Head.HeadMessageId!, Catalog.CatalogFingerprint);
        ProjectionDispatchRequest original = request;
        if (compactTransport)
            request = ProjectionReplayTransportContext.Bind(JsonSerializer.Deserialize<ProjectionDispatchRequest>(
                JsonSerializer.Serialize(request, JsonSerializerOptions.Web), JsonSerializerOptions.Web)!, ProjectionReplayTransportContext.Header(request));
        var options = new Hexalith.EventStore.Client.Projections.ProjectionDispatchOptions();
        var identity = new DomainProjectionIdentityOptions { AppId = Catalog.AppId, ServiceVersion = Catalog.ServiceVersion };
        // A new service provider on every action emulates request scopes and a process restart;
        // only the owning persisted store and explicit route configuration survive.
        var services = new ServiceCollection();
        services.AddSingleton<IReadModelStore>(Models);
        services.AddSingleton<IReadModelBatchStagingStore>(Models);
        foreach (GuardedTailProjectionHandler handler in Handlers)
        {
            services.AddSingleton<IAsyncDomainProjectionHandler>(handler);
            services.AddSingleton(new DomainProjectionOwnerProofRegistration(Identity.Domain, handler.ProjectionType,
                Catalog.SourceGuardCapabilities[handler.ProjectionType], new("models", handler.ProjectionType)));
        }
        using ServiceProvider provider = services.BuildServiceProvider();
        ProjectionDispatchResponse response = action switch
        {
            "stage" => await DomainProjectionDispatcher.StageRebuildAsync(provider, request, options, identity, CancellationToken.None).ConfigureAwait(false),
            "commit" => await DomainProjectionDispatcher.CommitRebuildAsync(provider, request, options, identity, CancellationToken.None).ConfigureAwait(false),
            "verify" => await DomainProjectionDispatcher.VerifyRebuildAsync(provider, request, options, identity, CancellationToken.None).ConfigureAwait(false),
            _ => await DomainProjectionDispatcher.AbortRebuildAsync(provider, request, options, identity, CancellationToken.None).ConfigureAwait(false),
        };
        if (legacyPrivateReference)
        {
            string key = DomainProjectionRebuildTransport.Key(request, -3);
            ReadModelEntry<DomainProjectionReplayManifest> read = await Models.GetAsync<DomainProjectionReplayManifest>("models", key).ConfigureAwait(false);
            using var hash = new DomainProjectionRequestDigestStream();
            JsonSerializer.Serialize(hash, original, JsonSerializerOptions.Web);
            JsonSerializer.Serialize(hash, ProjectionReplayTransportContext.Capture(original.Request), JsonSerializerOptions.Web);
            if (!await Models.TrySaveAsync("models", key, read.Value! with { Version = 1, RequestDigest = hash.Digest.ToUpperInvariant() },
                read.ETag!).ConfigureAwait(false)) throw new InvalidOperationException("Fixture legacy reference conversion failed.");
        }
        return response;
    }

    internal async Task CompleteOperatorRebuildAsync(string generation, long? checkpoint = null)
    {
        const string operation = "operator-rebuild-1";
        foreach (GuardedTailProjectionHandler handler in Handlers) handler.ReplayGenerationOverride = generation;
        if (checkpoint is not null && Catalog.FullReplayProjectionTypes.Count == 0)
        {
            // The explicit canonical replay capability on a single-event route
            // also supports authorized same-C operator repair through the SDK.
            foreach (string route in Catalog.ProjectionTypes)
                if (!await _lifecycle.BeginRebuildAsync(Identity, route, operation).ConfigureAwait(false))
                    throw new InvalidOperationException("Fixture rebuild admission failed.");
            ProjectionDispatchResponse repaired = await CanonicalReplayAsync(checkpoint, operation).ConfigureAwait(false);
            if (repaired.Outcomes.Any(static outcome => outcome.Status is not ProjectionDispatchStatus.Completed and not ProjectionDispatchStatus.AlreadyCompleted))
                throw new InvalidOperationException("Fixture canonical repair failed: " + repaired.Outcomes[0].ReasonCode);
            foreach (string route in Catalog.ProjectionTypes)
                if (!await _lifecycle.CompleteRebuildAsync(Identity, route, operation).ConfigureAwait(false))
                    throw new InvalidOperationException("Fixture rebuild release failed.");
            return;
        }
        ProjectionEventDto[] events = (await ProjectionEventWireBuilder.BuildAsync(Substitute.For<IEventPayloadProtectionService>(),
            Identity, [.. Events.Where(item => item.SequenceNumber <= (checkpoint ?? Head.HeadSequence))], CancellationToken.None).ConfigureAwait(false)).Events!;
        var registration = new Server.DomainServices.DomainServiceRegistration(Catalog.AppId, "project/v2", Identity.TenantId,
            Identity.Domain, Catalog.ServiceVersion);
        NamedProjectionRebuildResult staged = await _named.TryRebuildAsync(Identity, registration, events, checkpoint ?? Head.HeadSequence, operation, CancellationToken.None).ConfigureAwait(false);
        if (!staged.Succeeded) throw new InvalidOperationException("Fixture stage: " + JsonSerializer.Serialize(staged));
        NamedProjectionRebuildResult committed = await _named.CommitRebuildAsync(Identity, registration, events, operation, CancellationToken.None).ConfigureAwait(false);
        if (!committed.Succeeded) throw new InvalidOperationException("Fixture commit: " + JsonSerializer.Serialize(committed));
        NamedProjectionRebuildResult verified = await _named.VerifyRebuildAsync(Identity, registration, events, operation, CancellationToken.None).ConfigureAwait(false);
        if (!verified.Succeeded) throw new InvalidOperationException("Fixture verify: " + JsonSerializer.Serialize(verified));
        foreach (string route in Catalog.ProjectionTypes)
            if (!await _lifecycle.CompleteRebuildAsync(Identity, route, operation).ConfigureAwait(false))
                throw new InvalidOperationException("Fixture rebuild release failed.");
    }

    internal ProjectionSourceGuardQualification CreateQualification(IProjectionDeliveryStateStore states,
        NamedProjectionRouteCatalogEntry? catalog = null)
    {
        INamedProjectionRouteCatalog catalogs = Substitute.For<INamedProjectionRouteCatalog>();
        catalogs.Current.Returns(new NamedProjectionRouteCatalogSnapshot([catalog ?? Catalog]));
        return new(states, catalogs, _actors, Options.Create(new EventStoreActorOptions()), _history,
            _lifecycle, _owners, Options.Create(new ProjectionDeliveryIdempotencyOptions()), Clock);
    }

    internal Task<ProjectionOwnerProofResult> ReadOnFreshReplicaAsync(string action, string route, string? fingerprint = null)
    {
        var scope = new ProjectionOwnerProofRequest(Identity.TenantId, Identity.Domain, Identity.AggregateId,
            route, fingerprint ?? Catalog.CatalogFingerprint);
        var receipt = new ProjectionAppliedReceiptRequest(scope, Head.HeadSequence, Head.HeadMessageId!, "owner-1", "generation-1");
        var fresh = new DomainProjectionCatalogRegistry();
        return action switch
        {
            "owner" => DomainProjectionOwnerProof.ReadAsync(_services, scope, fresh, CancellationToken.None),
            "applied" => DomainProjectionOwnerProof.ReadAppliedReceiptAsync(_services, receipt, fresh, CancellationToken.None),
            _ => DomainProjectionOwnerProof.ReconcileAppliedReceiptAsync(_services, receipt, fresh, CancellationToken.None),
        };
    }

    internal string AllDomainsFingerprint()
    {
        IAsyncDomainProjectionHandler[] handlers = [.. _services.GetServices<IAsyncDomainProjectionHandler>()];
        return ProjectionRouteCatalogFingerprint.Compute(Catalog.AppId, Catalog.ServiceVersion,
            DomainProjectionOwnerProof.BindRoutes(_services, handlers.Select(handler => new ProjectionDispatchRoute(handler.Domain, handler.ProjectionType))),
            handlers.Where(static handler => handler.OrdinaryDeliveryEnabled)
                .Select(handler => new ProjectionDispatchRoute(handler.Domain, handler.ProjectionType)));
    }

    internal ProjectionSourceGuardDelivery CreateReadOnlyProbe(NamedProjectionRouteCatalogEntry catalog)
    {
        INamedProjectionRouteCatalog catalogs = Substitute.For<INamedProjectionRouteCatalog>();
        catalogs.Current.Returns(new NamedProjectionRouteCatalogSnapshot([catalog]));
        return new(catalogs, _states, _lifecycle, _owners, _actors, Options.Create(new EventStoreActorOptions()));
    }

    public void Dispose()
    {
        _http.Dispose();
        _services.Dispose();
        _invocation.Dispose();
        Backend.Dispose();
    }
}
