using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Actors.Runtime;
using Dapr.Client;

using Hexalith.Commons.UniqueIds;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Tests.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Sample.Counter.Projections;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Queries;
using Hexalith.EventStore.Server.Tests.Actors;
using Hexalith.EventStore.Server.Tests.TestUtilities;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

namespace Hexalith.EventStore.Server.Tests.Projections;

/// <summary>Actual source/projection/lifecycle actors and owning Dapr outbox/row stores for compatible replay.</summary>
internal sealed class LegacyReplayProductionHarness : IDisposable, IProjectionRebuildWriteGateway,
    IProjectionRebuildInspectionGateway, IProjectionReplayPublicationGateway
{
    internal AggregateIdentity Identity { get; } = new("tenant-a", "counter", "counter-1");
    internal FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero));
    internal RecordingDaprClient Backend { get; } = new() { PersistTypedState = true };
    internal FaultInjectingActorStateManager SourceState { get; } = new();
    internal FaultInjectingActorStateManager ModelState { get; } = new();
    internal AggregateActor Source { get; }
    internal EventReplayProjectionActor Model { get; }
    internal ProjectionLifecycleActor LifecycleActor { get; }
    internal IProjectionChangeNotifier Notifications { get; } = Substitute.For<IProjectionChangeNotifier>();
    internal ILogger<ProjectionUpdateOrchestrator> Logger { get; } = Substitute.For<ILogger<ProjectionUpdateOrchestrator>>();
    internal ILogger<EventReplayProjectionActor> ActorLogger { get; } = Substitute.For<ILogger<EventReplayProjectionActor>>();
    internal IProjectionLifecycleGateway Lifecycle { get; } = Substitute.For<IProjectionLifecycleGateway>();
    internal DaprProjectionActivationOutbox Outbox { get; }
    internal IProjectionDeliveryStateStore Rows { get; }
    internal LegacyReplayProductionHttpHandler Http { get; }
    internal ProjectionCheckpointTracker Checkpoints { get; }
    internal List<EventEnvelope> Events { get; } = [];
    internal SourceProof Head { get; private set; }
    internal bool LoseStageResponse { get; set; }
    internal bool LosePromotionResponse { get; set; }
    internal bool LoseReleaseResponse { get; set; }
    internal bool FailReadbackAfterPromotionLoss { get; set; } = true;
    internal Func<Task>? AfterStage { get; set; }
    internal Func<Task>? AfterPromotion { get; set; }
    internal Func<Task>? BeforeLease { get; set; }
    internal Func<ProjectionReplayPublicationRequest, CancellationToken, Task>? BeforeReconciliation { get; set; }
    internal int StagedPublications { get; private set; }
    internal int BoundReconciliations { get; private set; }
    internal int ReleasedWrites { get; private set; }
    internal bool PublicationMethodUnavailable { get; set; }
    internal INamedProjectionDispatchCoordinator? NamedCoordinator { get; set; }
    internal DaprProjectionDeliveryRetryScheduler? NamedScheduler { get; private set; }
    private readonly IActorProxyFactory _actors = Substitute.For<IActorProxyFactory>();
    private readonly ServiceProvider _services;
    private readonly HttpClient _http;
    private readonly DaprClient _invocation = new DaprClientBuilder().Build();
    private readonly ProjectionOptions _options;
    private readonly IProjectionDeliveryIdempotencyCoordinator _coordinator;
    private readonly IHttpClientFactory _clients = Substitute.For<IHttpClientFactory>();
    private readonly IDomainServiceResolver _resolver = Substitute.For<IDomainServiceResolver>();
    private bool _inspectionUnavailable;

    internal LegacyReplayProductionHarness(int pageSize = 256, int maxEvents = 10_000)
    {
        Logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        ActorLogger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        _options = new() { RebuildPageSize = pageSize, RebuildMaxPrefixEventCount = maxEvents };
        Head = SourceProofCodec.CreateSeed(Identity, "01ARZ3NDEKTSV4RRFFQ69G5FAV");
        Source = new(ActorHost.CreateForTest<AggregateActor>(new ActorTestOptions { ActorId = new(Identity.ActorId) }),
            NullLogger<AggregateActor>.Instance, Substitute.For<IDomainServiceInvoker>(), Substitute.For<ISnapshotManager>(),
            new NoOpEventPayloadProtectionService(), Substitute.For<ICommandStatusStore>(), Substitute.For<IEventPublisher>(),
            Options.Create(new EventDrainOptions()), Options.Create(new BackpressureOptions()), Substitute.For<IDeadLetterPublisher>());
        ActorStateManagerTestHelper.SetStateManager(Source, SourceState);
        _actors.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), Arg.Any<string>(), Arg.Any<ActorProxyOptions>()).Returns(Source);
        LifecycleActor = new(ActorHost.CreateForTest<ProjectionLifecycleActor>(new ActorTestOptions
            { ActorId = new(Identity.ActorId + ":counter") }), NullLogger<ProjectionLifecycleActor>.Instance, Clock);
        ActorStateManagerTestHelper.SetStateManager(LifecycleActor, new FaultInjectingActorStateManager());
        Lifecycle.ReadSnapshotAsync(Identity, "counter", Arg.Any<CancellationToken>()).Returns(call =>
        { call.ArgAt<CancellationToken>(2).ThrowIfCancellationRequested(); return LifecycleActor.ReadSnapshotAsync(); });
        Lifecycle.OwnsDeliveryWriteAsync(Identity, "counter", Arg.Any<string>(), Arg.Any<ProjectionLifecycleSnapshot>(), Arg.Any<CancellationToken>())
            .Returns(call => LifecycleActor.OwnsDeliveryLeaseAsync(new(call.ArgAt<string>(2), call.ArgAt<ProjectionLifecycleSnapshot>(3))));
        Lifecycle.BeginDeliveryWriteAsync(Identity, "counter", Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            if (BeforeLease is { } action) { BeforeLease = null; await action().ConfigureAwait(false); }
            return await LifecycleActor.BeginDeliveryWriteAsync(new(call.ArgAt<string>(2))).ConfigureAwait(false);
        });
        Lifecycle.CompleteDeliveryWriteAsync(Identity, "counter", Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            call.ArgAt<CancellationToken>(3).ThrowIfCancellationRequested();
            bool released = await LifecycleActor.CompleteDeliveryWriteAsync(new(call.ArgAt<string>(2))).ConfigureAwait(false);
            if (LoseReleaseResponse) { LoseReleaseResponse = false; throw new IOException("lost release response"); }
            return released;
        });
        Model = new(ActorHost.CreateForTest<EventReplayProjectionActor>(new ActorTestOptions { ActorId = new("counter:tenant-a:counter-1") }),
            Substitute.For<IETagService>(), Notifications, ActorLogger);
        ActorStateManagerTestHelper.SetStateManager(Model, ModelState);
        Model.SetPublicationLifecycle(Lifecycle);
        IProjectionWriteActor released = Substitute.For<IProjectionWriteActor>();
        released.UpdateProjectionAsync(Arg.Any<ProjectionState>()).Returns(call =>
        {
            ReleasedWrites++;
            return Model.UpdateProjectionAsync(call.Arg<ProjectionState>());
        });
        _actors.CreateActorProxy<IProjectionWriteActor>(Arg.Any<ActorId>(), Arg.Any<string>(), Arg.Any<ActorProxyOptions>()).Returns(released);
        IETagActor etags = Substitute.For<IETagActor>();
        etags.RegenerateAsync().Returns("released-freshness");
        _actors.CreateActorProxy<IETagActor>(Arg.Any<ActorId>(), Arg.Any<string>(), Arg.Any<ActorProxyOptions>()).Returns(etags);
        var configuration = Options.Create(_options);
        Rows = new DaprProjectionDeliveryStateStore(Backend, configuration);
        Outbox = new(Backend, configuration, Clock);
        Checkpoints = new(Backend, configuration, NullLogger<ProjectionCheckpointTracker>.Instance);
        _coordinator = new ProjectionDeliveryIdempotencyCoordinator(Rows, Options.Create(new ProjectionDeliveryIdempotencyOptions()), Clock);
        _resolver.ResolveAsync(Identity.TenantId, Identity.Domain, "v1", Arg.Any<CancellationToken>())
            .Returns(new DomainServiceRegistration("sample", "project", Identity.TenantId, Identity.Domain, "v1"));
        var services = new ServiceCollection();
        services.AddSingleton<IDomainProjectionHandler, CounterProjection>();
        _services = services.BuildServiceProvider();
        Http = new(_services);
        _http = new(Http);
        _clients.CreateClient(Arg.Any<string>()).Returns(_http);
    }

    internal async Task InitializeAsync(int count, bool writerActive = true)
    {
        await AppendAsync(count).ConfigureAwait(false);
        if (writerActive) await Rows.TryActivateWriterProtocolAsync(new(1, 2, new string('a', 40), Clock.GetUtcNow())).ConfigureAwait(false);
        await Outbox.EnsureAsync(Identity).ConfigureAwait(false);
    }

    internal void UseOlderSource(Exception? failure = null)
    {
        IAggregateActor older = Substitute.For<IAggregateActor>();
        older.InspectSourceProofAsync().Returns<SourceProofInspection>(_ => throw failure ?? MissingActorMethod());
        older.GetEventsAsync(Arg.Any<long>()).Returns(call => Source.GetEventsAsync(call.Arg<long>()));
        _actors.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), Arg.Any<string>(), Arg.Any<ActorProxyOptions>()).Returns(older);
    }

    private static ActorMethodInvocationException MissingActorMethod()
        => new("bounded unsupported method", new ActorInvokeException(typeof(MissingMethodException).FullName!, "bounded"), false);

    internal void UseProductionNamedCoordinator(bool hasNamedRoute)
    {
        var catalog = new NamedProjectionRouteCatalog();
        if (hasNamedRoute) catalog.Upsert([new("sample", "v1", Identity.Domain, 2,
            ProjectionDispatchProtocol.Capability, new string('a', 64), ["counter-named"])]);
        NamedScheduler = new(Backend, Options.Create(_options));
        NamedCoordinator = new NamedProjectionDispatchCoordinator(catalog, Checkpoints, Lifecycle, _actors,
            _invocation, _clients, Options.Create(new ProjectionDispatchOptions()),
            NullLogger<NamedProjectionDispatchCoordinator>.Instance, NamedScheduler, Clock,
            idempotencyCoordinator: _coordinator);
    }

    internal async Task AppendAsync(int count)
    {
        for (int index = 0; index < count; index++)
        {
            long sequence = Events.Count + 1;
            var item = new EventEnvelope(UniqueIdHelper.GenerateSortableUniqueStringId(), Identity.AggregateId, "Counter",
                Identity.TenantId, Identity.Domain, sequence, sequence, Clock.GetUtcNow(), "correlation-1", "cause-1", "user-1",
                "v1", "CounterIncremented", 1, "json", "{}"u8.ToArray(), null);
            Events.Add(item);
            Head = SourceProofCodec.Extend(Head, item);
            await SourceState.SetStateAsync(Identity.EventStreamKeyPrefix + sequence, item).ConfigureAwait(false);
        }
        await SourceState.SetStateAsync(Identity.MetadataKey,
            new AggregateMetadata(Events.Count, Clock.GetUtcNow(), null) { SourceProof = Head }).ConfigureAwait(false);
        await SourceState.SaveStateAsync().ConfigureAwait(false);
    }

    internal async Task SeedReleasedCompletedRowAndActorAsync()
    {
        ProjectionEventDto[] readable = (await ProjectionEventWireBuilder.BuildAsync(new NoOpEventPayloadProtectionService(), Identity,
            [.. Events], CancellationToken.None).ConfigureAwait(false)).Events!;
        ProjectionDeliveryReservation reservation = (await _coordinator.TryAdmitAsync(Identity, "counter", readable, true).ConfigureAwait(false)).Reservation!;
        await Model.UpdateProjectionAsync(ProjectionState.FromJsonElement("counter", Identity.TenantId,
            JsonSerializer.SerializeToElement(new { count = Events.Count }))).ConfigureAwait(false);
        await _coordinator.CompleteAsync(Identity, "counter", readable, reservation).ConfigureAwait(false);
        Notifications.ClearReceivedCalls();
    }

    private ProjectionUpdateOrchestrator CreateOrchestrator()
        => new(_actors, _invocation, _clients, _resolver,
            Checkpoints, Options.Create(_options), Logger,
            deliveryCheckpointStore: Checkpoints,
            lifecycleGateway: Lifecycle, namedProjectionDispatchCoordinator: NamedCoordinator,
            projectionActivationOutbox: Outbox, rebuildWriteGateway: this,
            deliveryStateStore: Rows, deliveryIdempotency: _coordinator, timeProvider: Clock);

    internal Task DeliverAsync(CancellationToken cancellationToken = default)
        => CreateOrchestrator().DeliverProjectionAsync(Identity, cancellationToken);

    internal Task DeliverReplicaAsync(ProjectionActivationWorkItem sampledWork)
        => CreateOrchestrator().DeliverCompatibleReplayAsync(Identity,
            new("sample", "project", Identity.TenantId, Identity.Domain, "v1"), sampledWork,
            this, this, Outbox, CancellationToken.None);

    public async Task<ProjectionRebuildInspection> StageReplayAsync(string actorId, ProjectionReplayPublicationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StagedPublications++;
        if (PublicationMethodUnavailable) throw MissingActorMethod();
        ProjectionRebuildInspection result = await Model.StageReplayAsync(request).ConfigureAwait(false);
        if (AfterStage is { } action) { AfterStage = null; await action().ConfigureAwait(false); }
        if (LoseStageResponse) { LoseStageResponse = false; throw new IOException("lost stage response"); }
        return result;
    }

    public async Task<ProjectionRebuildInspection> PromoteReplayAsync(string actorId, ProjectionReplayPublicationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ProjectionRebuildInspection result = await Model.PromoteReplayAsync(request).ConfigureAwait(false);
        if (AfterPromotion is { } action) { AfterPromotion = null; await action().ConfigureAwait(false); }
        if (LosePromotionResponse)
        { LosePromotionResponse = false; _inspectionUnavailable = FailReadbackAfterPromotionLoss; throw new IOException("lost promotion response"); }
        return result;
    }

    public Task<bool> FinalizeReplayAsync(string actorId, ProjectionReplayPublicationRequest request, CancellationToken cancellationToken)
    { cancellationToken.ThrowIfCancellationRequested(); return Model.FinalizeReplayAsync(request).WaitAsync(cancellationToken); }
    public Task<bool> ReadReplayCompletionAsync(string actorId, ProjectionReplayPublicationRequest request, CancellationToken cancellationToken)
    { cancellationToken.ThrowIfCancellationRequested(); return Model.ReadReplayCompletionAsync(request).WaitAsync(cancellationToken); }
    public async Task<bool> ReconcileBoundReplayCompletionAsync(string actorId, ProjectionReplayPublicationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        BoundReconciliations++;
        if (PublicationMethodUnavailable) throw MissingActorMethod();
        if (BeforeReconciliation is { } action) await action(request, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return await Model.ReconcileBoundReplayCompletionAsync(request).WaitAsync(cancellationToken).ConfigureAwait(false);
    }
    public Task<ProjectionRebuildInspection> InspectAsync(string actorId, string operationId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_inspectionUnavailable) { _inspectionUnavailable = false; throw new IOException("original receipt unavailable"); }
        return Model.InspectProjectionRebuildAsync(new(operationId)).WaitAsync(cancellationToken);
    }
    public Task StageAsync(string actorId, ProjectionRebuildCandidate candidate, CancellationToken cancellationToken) => Model.StageProjectionAsync(candidate);
    public Task<bool> PromoteAsync(string actorId, string operationId, CancellationToken cancellationToken) => Model.PromoteProjectionAsync(new(operationId));
    public Task<bool> DiscardAsync(string actorId, string operationId, CancellationToken cancellationToken) => Model.DiscardProjectionAsync(new(operationId));
    public Task<bool> RollbackAsync(string actorId, string operationId, CancellationToken cancellationToken) => Model.RollbackProjectionAsync(new(operationId));
    public Task<bool> FinalizeAsync(string actorId, string operationId, CancellationToken cancellationToken) => Model.FinalizeProjectionAsync(new(operationId));
    public Task<ProjectionState?> ReadAsync(string actorId, CancellationToken cancellationToken) => Model.ReadProjectionStateAsync();

    public void Dispose() { _http.Dispose(); _invocation.Dispose(); Backend.Dispose(); _services.Dispose(); }
}
