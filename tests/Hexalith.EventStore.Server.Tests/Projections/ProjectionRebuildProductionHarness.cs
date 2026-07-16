using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Client;

using Hexalith.EventStore.Client.Conventions;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Replay;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Queries;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

namespace Hexalith.EventStore.Server.Tests.Projections;

internal sealed class ProjectionRebuildProductionHarness : IDisposable {
    public const string StoreName = "projection-store";
    public const string DetailKey = "agg-001:detail";
    public const string IndexKey = "test-domain:index";
    public const string OperationId = "operation-1";

    private const string TenantId = "test-tenant";
    private const string Domain = "test-domain";
    private const string AggregateId = "agg-001";
    private const string AppId = "test-domain-service";
    private const string ServiceVersion = "v1";
    private const string DetailProjectionType = "aggregate-detail";
    private const string IndexProjectionType = "aggregate-index";
    private const int PageSize = 3;

    private static readonly DateTimeOffset ProjectedAt = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly ServiceProvider _services;
    private readonly HttpClient _httpClient;
    private readonly DaprClient _daprClient;

    public ProjectionRebuildProductionHarness(int eventCount, long? toPosition = null) {
        Identity = new AggregateIdentity(TenantId, Domain, AggregateId);
        Events = [.. Enumerable.Range(1, eventCount).Select(CreateEvent)];
        long effectiveBound = Math.Min(eventCount, toPosition ?? eventCount);
        Expected = effectiveBound == 0
            ? null
            : ProjectionRebuildEquivalenceOracle.Build(
                TenantId,
                Domain,
                AggregateId,
                ToReplayEvents(Events.Where(item => item.SequenceNumber <= effectiveBound)),
                effectiveBound,
                ProjectedAt);

        OldDetail = new AggregateReadModel(AggregateId, "live-before-rebuild", 0, DateTimeOffset.UnixEpoch, "0");
        OldIndex = new AggregateIndexReadModel(["live-before-rebuild"], DateTimeOffset.UnixEpoch, "0");
        ReadModels = new InMemoryReadModelStore();
        ReadModels.SeedRaw(StoreName, DetailKey, OldDetail);
        ReadModels.SeedRaw(StoreName, IndexKey, OldIndex);
        ActorState = ProjectionState.FromJsonElement(
            DetailProjectionType,
            TenantId,
            JsonSerializer.SerializeToElement(OldDetail, JsonSerializerOptions.Web));

        IDomainProjectionHandler legacy = Substitute.For<IDomainProjectionHandler>();
        legacy.Domain.Returns(Domain);
        legacy.Project(Arg.Any<ProjectionRequest>()).Returns(call => {
            ProjectionRebuildEquivalenceSnapshot snapshot = BuildSnapshot(call.Arg<ProjectionRequest>());
            return new ProjectionResponse(
                DetailProjectionType,
                JsonSerializer.SerializeToElement(snapshot.Detail, JsonSerializerOptions.Web));
        });

        IAsyncDomainProjectionRebuildHandler detail = CreateNamedHandler(DetailProjectionType);
        detail.PrepareRebuildAsync(
                Arg.Any<ProjectionRequest>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(call => {
                ProjectionRebuildEquivalenceSnapshot snapshot = BuildSnapshot(call.ArgAt<ProjectionRequest>(0));
                return new DomainProjectionRebuildPlan(
                    StoreName,
                    [ReadModelBatchOperation.Write(DetailKey, snapshot.Detail, ReadModelBatchConcurrency.LastWrite)]);
            });
        IAsyncDomainProjectionRebuildHandler index = CreateNamedHandler(IndexProjectionType);
        index.PrepareRebuildAsync(
                Arg.Any<ProjectionRequest>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(call => {
                if (FailIndexPreparation) {
                    throw new InvalidOperationException("Injected named projection preparation failure.");
                }

                ProjectionRebuildEquivalenceSnapshot snapshot = BuildSnapshot(call.ArgAt<ProjectionRequest>(0));
                return new DomainProjectionRebuildPlan(
                    StoreName,
                    [ReadModelBatchOperation.Write(IndexKey, snapshot.Index, ReadModelBatchConcurrency.LastWrite)]);
            });

        var serviceCollection = new ServiceCollection();
        _ = serviceCollection.AddSingleton(legacy);
        _ = serviceCollection.AddSingleton<IAsyncDomainProjectionHandler>(detail);
        _ = serviceCollection.AddSingleton<IAsyncDomainProjectionHandler>(index);
        _ = serviceCollection.AddSingleton<IReadModelBatchStore>(ReadModels);
        _services = serviceCollection.BuildServiceProvider();

        var dispatchOptions = new ProjectionDispatchOptions();
        var identityOptions = new DomainProjectionIdentityOptions {
            AppId = AppId,
            ServiceVersion = ServiceVersion,
        };
        var httpHandler = new ProjectionRebuildProductionHttpMessageHandler(
            _services,
            dispatchOptions,
            identityOptions);
        _httpClient = new HttpClient(httpHandler);
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(_httpClient);
        httpClientFactory.CreateClient().Returns(_httpClient);
        _daprClient = new DaprClientBuilder().Build();

        ActorProxyFactory = Substitute.For<IActorProxyFactory>();
        AggregateActor = Substitute.For<IAggregateActor>();
        _ = AggregateActor.GetCurrentSequenceAsync().Returns(eventCount);
        _ = AggregateActor.ReadEventsRangeAsync(Arg.Any<long>(), Arg.Any<long?>(), Arg.Any<int>())
            .Returns(call => {
                long cursor = call.ArgAt<long>(0);
                long? bound = call.ArgAt<long?>(1);
                int maxCount = call.ArgAt<int>(2);
                if (PageReadOverride is not null) {
                    return PageReadOverride(cursor, bound, maxCount);
                }

                return Events
                    .Where(item => item.SequenceNumber > cursor
                        && (bound is null || item.SequenceNumber <= bound.Value))
                    .Take(maxCount)
                    .ToArray();
            });
        _ = ActorProxyFactory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), "AggregateActor")
            .Returns(AggregateActor);
        ProjectionWriteActor = Substitute.For<IProjectionWriteActor>();
        _ = ProjectionWriteActor.UpdateProjectionAsync(Arg.Do<ProjectionState>(state => ActorState = state));
        _ = ActorProxyFactory.CreateActorProxy<IProjectionWriteActor>(
                Arg.Any<ActorId>(),
                QueryRouter.ProjectionActorTypeName)
            .Returns(ProjectionWriteActor);
        RebuildWrites = Substitute.For<IProjectionRebuildWriteGateway>();
        ProjectionRebuildCandidate? stagedCandidate = null;
        _ = RebuildWrites.StageAsync(
                Arg.Any<string>(),
                Arg.Any<ProjectionRebuildCandidate>(),
                Arg.Any<CancellationToken>())
            .Returns(call => {
                stagedCandidate = call.ArgAt<ProjectionRebuildCandidate>(1);
                return Task.CompletedTask;
            });
        _ = RebuildWrites.PromoteAsync(
                Arg.Any<string>(),
                OperationId,
                Arg.Any<CancellationToken>())
            .Returns(_ => {
                if (stagedCandidate is null) {
                    return false;
                }

                ActorState = stagedCandidate.State;
                stagedCandidate = null;
                return true;
            });
        _ = RebuildWrites.DiscardAsync(
                Arg.Any<string>(),
                OperationId,
                Arg.Any<CancellationToken>())
            .Returns(_ => {
                stagedCandidate = null;
                return true;
            });
        IETagActor etagActor = Substitute.For<IETagActor>();
        _ = etagActor.RegenerateAsync().Returns("transport-etag-not-a-projection-version");
        _ = ActorProxyFactory.CreateActorProxy<IETagActor>(Arg.Any<ActorId>(), ETagActor.ETagActorTypeName)
            .Returns(etagActor);

        Lifecycle = Substitute.For<IProjectionLifecycleGateway>();
        _ = Lifecycle.BeginRebuildAsync(
                Identity,
                Arg.Any<string>(),
                OperationId,
                Arg.Any<CancellationToken>())
            .Returns(true);
        _ = Lifecycle.CompleteRebuildAsync(
                Identity,
                Arg.Any<string>(),
                OperationId,
                Arg.Any<CancellationToken>())
            .Returns(true);

        DeliveryCheckpoints = Substitute.For<IProjectionDeliveryCheckpointStore>();
        string fingerprint = ProjectionRouteCatalogFingerprint.Compute(
            AppId,
            ServiceVersion,
            [
                new ProjectionDispatchRoute(Domain, DetailProjectionType),
                new ProjectionDispatchRoute(Domain, IndexProjectionType),
            ]);
        var catalog = new NamedProjectionRouteCatalog();
        catalog.Replace(new NamedProjectionRouteCatalogSnapshot([
            new NamedProjectionRouteCatalogEntry(
                AppId,
                ServiceVersion,
                Domain,
                ProjectionDispatchProtocol.Version,
                ProjectionDispatchProtocol.Capability,
                fingerprint,
                [DetailProjectionType, IndexProjectionType]),
        ]));
        var namedCoordinator = new NamedProjectionDispatchCoordinator(
            catalog,
            DeliveryCheckpoints,
            Lifecycle,
            ActorProxyFactory,
            _daprClient,
            httpClientFactory,
            Options.Create(dispatchOptions),
            NullLogger<NamedProjectionDispatchCoordinator>.Instance);

        CheckpointTracker = Substitute.For<IProjectionCheckpointTracker>();
        _ = CheckpointTracker.EnumerateTrackedIdentitiesAsync(Arg.Any<CancellationToken>())
            .Returns(EnumerateIdentity(Identity));
        RebuildCheckpoints = new InMemoryProjectionRebuildCheckpointStore();
        OperatorScope = new ProjectionRebuildCheckpointScope(TenantId, Domain, DetailProjectionType, null, OperationId);
        AggregateScope = OperatorScope with { AggregateId = AggregateId };
        SeedOperator(ProjectionRebuildStatus.Running, toPosition);

        IDomainServiceResolver resolver = Substitute.For<IDomainServiceResolver>();
        _ = resolver.ResolveAsync(TenantId, Domain, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new DomainServiceRegistration(AppId, "project", TenantId, Domain, ServiceVersion));
        Sut = new ProjectionUpdateOrchestrator(
            ActorProxyFactory,
            _daprClient,
            httpClientFactory,
            resolver,
            CheckpointTracker,
            Options.Create(new ProjectionOptions { RebuildPageSize = PageSize }),
            NullLogger<ProjectionUpdateOrchestrator>.Instance,
            RebuildCheckpoints,
            lifecycleGateway: Lifecycle,
            namedProjectionDispatchCoordinator: namedCoordinator,
            rebuildWriteGateway: RebuildWrites);
    }

    public AggregateIdentity Identity { get; }

    public EventEnvelope[] Events { get; }

    public ProjectionRebuildEquivalenceSnapshot? Expected { get; }

    public AggregateReadModel OldDetail { get; }

    public AggregateIndexReadModel OldIndex { get; }

    public InMemoryReadModelStore ReadModels { get; }

    public ProjectionState ActorState { get; private set; }

    public ProjectionUpdateOrchestrator Sut { get; }

    public IActorProxyFactory ActorProxyFactory { get; }

    public IAggregateActor AggregateActor { get; }

    public IProjectionWriteActor ProjectionWriteActor { get; }

    public IProjectionRebuildWriteGateway RebuildWrites { get; }

    public IProjectionLifecycleGateway Lifecycle { get; }

    public IProjectionDeliveryCheckpointStore DeliveryCheckpoints { get; }

    public IProjectionCheckpointTracker CheckpointTracker { get; }

    public InMemoryProjectionRebuildCheckpointStore RebuildCheckpoints { get; }

    public ProjectionRebuildCheckpointScope OperatorScope { get; }

    public ProjectionRebuildCheckpointScope AggregateScope { get; }

    public Func<long, long?, int, EventEnvelope[]>? PageReadOverride { get; set; }

    public bool FailIndexPreparation { get; set; }

    public Task RunAsync(CancellationToken cancellationToken = default)
        => Sut.RebuildProjectionAsync(OperatorScope with { OperationId = null }, cancellationToken);

    public AggregateReadModel ActorSnapshot()
        => JsonSerializer.Deserialize<AggregateReadModel>(ActorState.StateBytes, JsonSerializerOptions.Web)
            ?? throw new InvalidOperationException("Projection actor state was missing.");

    public void SeedOperator(ProjectionRebuildStatus status, long? toPosition = null)
        => RebuildCheckpoints.Seed(new ProjectionRebuildCheckpoint(
            TenantId,
            Domain,
            DetailProjectionType,
            null,
            OperationId,
            0,
            status,
            DateTimeOffset.UtcNow,
            null,
            toPosition));

    public void SeedAggregateProgress(long sequence, long? toPosition = null)
        => RebuildCheckpoints.Seed(new ProjectionRebuildCheckpoint(
            TenantId,
            Domain,
            DetailProjectionType,
            AggregateId,
            OperationId,
            sequence,
            ProjectionRebuildStatus.Running,
            DateTimeOffset.UtcNow,
            null,
            toPosition));

    public void Dispose() {
        _httpClient.Dispose();
        _daprClient.Dispose();
        _services.Dispose();
    }

    private static IAsyncDomainProjectionRebuildHandler CreateNamedHandler(string projectionType) {
        IAsyncDomainProjectionRebuildHandler handler = Substitute.For<IAsyncDomainProjectionRebuildHandler>();
        handler.Domain.Returns(Domain);
        handler.ProjectionType.Returns(projectionType);
        handler.RebuildSemantics.Returns(DomainProjectionRebuildSemantics.FullReplay);
        return handler;
    }

    private static EventEnvelope CreateEvent(int sequence) {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(
            new ProjectionStatusChanged(AggregateId, $"status-{sequence}"),
            JsonSerializerOptions.Web);
        return new EventEnvelope(
            $"01J{sequence:D23}",
            AggregateId,
            nameof(ProjectionRebuildAggregateState),
            TenantId,
            Domain,
            sequence,
            sequence,
            ProjectedAt.AddMinutes(sequence),
            "01J00000000000000000000001",
            "01J00000000000000000000002",
            "test-user",
            ServiceVersion,
            nameof(ProjectionStatusChanged),
            1,
            "json",
            payload,
            null);
    }

    private static ReplayEventEnvelope[] ToReplayEvents(IEnumerable<EventEnvelope> events)
        => [.. events.Select(static item => new ReplayEventEnvelope(
            item.SequenceNumber,
            item.EventTypeName,
            item.Payload,
            item.SerializationFormat,
            item.MetadataVersion,
            item.MessageId,
            item.CorrelationId,
            item.CausationId))];

    private static ProjectionRebuildEquivalenceSnapshot BuildSnapshot(ProjectionRequest request) {
        long upToSequence = request.Events.Max(static item => item.SequenceNumber);
        ReplayEventEnvelope[] replayEvents = [.. request.Events.Select(static item => new ReplayEventEnvelope(
            item.SequenceNumber,
            item.EventTypeName,
            item.Payload,
            item.SerializationFormat,
            1,
            item.MessageId,
            item.CorrelationId,
            null))];
        return ProjectionRebuildEquivalenceOracle.Build(
            request.TenantId,
            request.Domain,
            request.AggregateId,
            replayEvents,
            upToSequence,
            ProjectedAt);
    }

    private static async IAsyncEnumerable<AggregateIdentity> EnumerateIdentity(AggregateIdentity identity) {
        yield return identity;
        await Task.CompletedTask.ConfigureAwait(false);
    }
}
