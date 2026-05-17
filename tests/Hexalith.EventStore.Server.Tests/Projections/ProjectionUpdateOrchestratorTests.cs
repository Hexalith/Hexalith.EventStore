
using System.Text;
using System.Text.Json;
using System.Net;

using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Queries;
using Hexalith.EventStore.Server.Tests.TestUtilities;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

/// <summary>
/// Story 11-3 Task 6: ProjectionUpdateOrchestrator tests.
/// Verifies orchestrator behavior for all 3 acceptance criteria.
/// NOTE: DaprClient.InvokeMethodAsync is non-virtual and cannot be mocked with NSubstitute.
/// Full pipeline tests (DAPR invocation + write actor update) require integration tests (Tier 3).
/// Unit tests verify: resolver calls, aggregate actor proxy creation, event reading, error handling.
/// </summary>
public class ProjectionUpdateOrchestratorTests {
    private static readonly AggregateIdentity TestIdentity = new("test-tenant", "test-domain", "agg-001");

    private static EventEnvelope CreateTestEnvelope(long sequenceNumber = 1, string aggregateId = "agg-001") =>
        new(
            MessageId: $"msg-{sequenceNumber}",
            AggregateId: aggregateId,
            AggregateType: "test-aggregate",
            TenantId: "test-tenant",
            Domain: "test-domain",
            SequenceNumber: sequenceNumber,
            GlobalPosition: sequenceNumber * 10,
            Timestamp: DateTimeOffset.UtcNow,
            CorrelationId: "corr-001",
            CausationId: "cause-001",
            UserId: "user-1",
            DomainServiceVersion: "1.0.0",
            EventTypeName: "CounterIncremented",
            MetadataVersion: 1,
            SerializationFormat: "json",
            Payload: [1, 2, 3],
            Extensions: null);

    private static (ProjectionUpdateOrchestrator Sut, IActorProxyFactory ActorProxyFactory, DaprClient DaprClient, IDomainServiceResolver Resolver, IProjectionCheckpointTracker CheckpointTracker) CreateSut(
        IProjectionCheckpointTracker? checkpointTracker = null,
        DaprClient? daprClient = null,
        IHttpClientFactory? httpClientFactory = null,
        ILogger<ProjectionUpdateOrchestrator>? logger = null,
        IProjectionRebuildCheckpointStore? rebuildCheckpointStore = null) {
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        daprClient ??= Substitute.For<DaprClient>();
        IDomainServiceResolver resolver = Substitute.For<IDomainServiceResolver>();
        if (checkpointTracker is null) {
            checkpointTracker = Substitute.For<IProjectionCheckpointTracker>();
            _ = checkpointTracker.ReadLastDeliveredSequenceAsync(Arg.Any<AggregateIdentity>(), Arg.Any<CancellationToken>())
                .Returns(0);
        }
        IOptions<ProjectionOptions> projectionOptions = Options.Create(new ProjectionOptions());
        var sut = new ProjectionUpdateOrchestrator(actorProxyFactory, daprClient, httpClientFactory ?? Substitute.For<IHttpClientFactory>(), resolver, checkpointTracker, projectionOptions, logger ?? NullLogger<ProjectionUpdateOrchestrator>.Instance, rebuildCheckpointStore);
        return (sut, actorProxyFactory, daprClient, resolver, checkpointTracker);
    }

    private static async Task WaitForSignalAsync(Task signalTask, int timeoutMs = 2000) {
        Task completed = await Task.WhenAny(signalTask, Task.Delay(timeoutMs)).ConfigureAwait(false);
        completed.ShouldBe(signalTask);
        await signalTask.ConfigureAwait(false);
    }

    private static async Task WaitForNoSignalAsync(Task signalTask, int waitMs = 500) {
        Task completed = await Task.WhenAny(signalTask, Task.Delay(waitMs)).ConfigureAwait(false);
        completed.ShouldNotBe(signalTask);
    }

    // --- Test 1: AC 3 - No domain service registered ---

    [Fact]
    public async Task DeliverProjectionAsync_WithActiveOperatorRebuild_SkipsNormalDeliveryBeforeActorProxy() {
        // D3-B: poller now consults HasActiveOperatorRebuildForDomainAsync (active-rebuilds index)
        // instead of probing by (tenant, domain, domain, null, null) which assumed projectionName == domain.
        IProjectionRebuildCheckpointStore rebuildCheckpointStore = Substitute.For<IProjectionRebuildCheckpointStore>();
        _ = rebuildCheckpointStore.HasActiveOperatorRebuildForDomainAsync(
                TestIdentity.TenantId,
                TestIdentity.Domain,
                Arg.Any<CancellationToken>())
            .Returns(true);
        (ProjectionUpdateOrchestrator sut, IActorProxyFactory actorProxyFactory, _, IDomainServiceResolver resolver, _) = CreateSut(rebuildCheckpointStore: rebuildCheckpointStore);

        await sut.DeliverProjectionAsync(TestIdentity);

        _ = await rebuildCheckpointStore.Received(1).HasActiveOperatorRebuildForDomainAsync(
            TestIdentity.TenantId,
            TestIdentity.Domain,
            Arg.Any<CancellationToken>());
        _ = actorProxyFactory.DidNotReceiveWithAnyArgs().CreateActorProxy<IAggregateActor>(default!, default!);
        _ = await resolver.DidNotReceiveWithAnyArgs().ResolveAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task DeliverProjectionAsync_PollerProceedsWhenNoActiveOperatorRebuildForDomain() {
        // D3-B regression test: when no projection in (tenant, domain) has an active rebuild,
        // the poller is allowed to proceed and call the resolver (which we stub to return null
        // here just to short-circuit the rest).
        IProjectionRebuildCheckpointStore rebuildCheckpointStore = Substitute.For<IProjectionRebuildCheckpointStore>();
        _ = rebuildCheckpointStore.HasActiveOperatorRebuildForDomainAsync(
                TestIdentity.TenantId,
                TestIdentity.Domain,
                Arg.Any<CancellationToken>())
            .Returns(false);
        (ProjectionUpdateOrchestrator sut, _, _, IDomainServiceResolver resolver, _) = CreateSut(rebuildCheckpointStore: rebuildCheckpointStore);
        _ = resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((DomainServiceRegistration?)null);

        await sut.DeliverProjectionAsync(TestIdentity);

        _ = await resolver.Received(1).ResolveAsync(
            TestIdentity.TenantId,
            TestIdentity.Domain,
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RebuildProjectionAsync_AcceptedApplyAdvancesRebuildCheckpoint() {
        IProjectionCheckpointTracker checkpointTracker = Substitute.For<IProjectionCheckpointTracker>();
        _ = checkpointTracker.EnumerateTrackedIdentitiesAsync(Arg.Any<CancellationToken>())
            .Returns(EnumerateTracked(TestIdentity));
        _ = checkpointTracker.ReadLastDeliveredSequenceAsync(TestIdentity, Arg.Any<CancellationToken>())
            .Returns(0);
        _ = checkpointTracker.SaveDeliveredSequenceAsync(TestIdentity, Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(true);
        IProjectionRebuildCheckpointStore rebuildCheckpointStore = Substitute.For<IProjectionRebuildCheckpointStore>();
        ProjectionRebuildCheckpoint currentCheckpoint = CreateRebuildCheckpoint(0, ProjectionRebuildStatus.Running, "operation-1", toPosition: 2);
        _ = rebuildCheckpointStore.ReadAsync(Arg.Any<ProjectionRebuildCheckpointScope>(), Arg.Any<CancellationToken>())
            .Returns(_ => currentCheckpoint);
        _ = rebuildCheckpointStore.SaveAsync(
                Arg.Any<ProjectionRebuildCheckpointScope>(),
                Arg.Any<long>(),
                Arg.Any<ProjectionRebuildStatus>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<long?>())
            .Returns(call => {
                currentCheckpoint = CreateRebuildCheckpoint(call.ArgAt<long>(1), call.ArgAt<ProjectionRebuildStatus>(2), "operation-1", call.ArgAt<long?>(5));
                return ProjectionRebuildCheckpointSaveResult.Success(currentCheckpoint);
            });

        IProjectionWriteActor writeActor = Substitute.For<IProjectionWriteActor>();
        IHttpClientFactory httpClientFactory = CreateHttpClientFactory("""{"projectionType":"counter-summary","state":{"value":1}}""");
        DaprClient daprClient = new DaprClientBuilder().Build();
        (ProjectionUpdateOrchestrator sut, IActorProxyFactory actorProxyFactory, _, IDomainServiceResolver resolver, _) = CreateSut(
            checkpointTracker,
            daprClient,
            httpClientFactory,
            rebuildCheckpointStore: rebuildCheckpointStore);
        var registration = new DomainServiceRegistration("counter-service", "project", TestIdentity.TenantId, TestIdentity.Domain, "v1");
        _ = resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(registration);
        IAggregateActor aggregateActor = Substitute.For<IAggregateActor>();
        // C1: orchestrator now reads from per-aggregate progress via ReadEventsRangeAsync.
        // Actor honors toSequence (toPosition=2 ⇒ events at seq 1 and 2 only).
        _ = aggregateActor.ReadEventsRangeAsync(0L, 2L, Arg.Any<int>())
            .Returns([CreateTestEnvelope(1), CreateTestEnvelope(2)]);
        _ = actorProxyFactory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), "AggregateActor")
            .Returns(aggregateActor);
        _ = actorProxyFactory.CreateActorProxy<IProjectionWriteActor>(Arg.Any<ActorId>(), QueryRouter.ProjectionActorTypeName)
            .Returns(writeActor);

        await sut.RebuildProjectionAsync(CreateRebuildScope());

        await writeActor.Received(1).UpdateProjectionAsync(Arg.Any<ProjectionState>());
        _ = await rebuildCheckpointStore.Received(1).SaveAsync(
            Arg.Is<ProjectionRebuildCheckpointScope>(scope =>
                scope.Tenant == TestIdentity.TenantId
                && scope.Domain == TestIdentity.Domain
                && scope.ProjectionName == TestIdentity.Domain
                && scope.OperationId == "operation-1"),
            2,
            ProjectionRebuildStatus.Running,
            null,
            Arg.Any<CancellationToken>(),
            2,
            isPerAggregateProgress: true);
        _ = await rebuildCheckpointStore.DidNotReceive().SaveAsync(
            Arg.Any<ProjectionRebuildCheckpointScope>(),
            Arg.Any<long>(),
            ProjectionRebuildStatus.Succeeded,
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<long?>(),
            Arg.Any<bool>());
    }

    [Fact]
    public async Task RebuildProjectionAsync_BoundedApplyUsesPerAggregateProgressForTerminalSuccess() {
        IProjectionCheckpointTracker checkpointTracker = Substitute.For<IProjectionCheckpointTracker>();
        _ = checkpointTracker.EnumerateTrackedIdentitiesAsync(Arg.Any<CancellationToken>())
            .Returns(EnumerateTracked(TestIdentity));
        _ = checkpointTracker.SaveDeliveredSequenceAsync(TestIdentity, Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(true);

        IProjectionRebuildCheckpointStore rebuildCheckpointStore = Substitute.For<IProjectionRebuildCheckpointStore>();
        ProjectionRebuildCheckpoint operatorCheckpoint = CreateRebuildCheckpoint(0, ProjectionRebuildStatus.Running, "operation-1", toPosition: 2);
        _ = rebuildCheckpointStore.ReadAsync(
                Arg.Is<ProjectionRebuildCheckpointScope>(scope => scope.AggregateId == null),
                Arg.Any<CancellationToken>())
            .Returns(operatorCheckpoint);
        _ = rebuildCheckpointStore.ReadAsync(
                Arg.Is<ProjectionRebuildCheckpointScope>(scope => scope.AggregateId == TestIdentity.AggregateId),
                Arg.Any<CancellationToken>())
            .Returns((ProjectionRebuildCheckpoint?)null);
        _ = rebuildCheckpointStore.SaveAsync(
                Arg.Any<ProjectionRebuildCheckpointScope>(),
                Arg.Any<long>(),
                Arg.Any<ProjectionRebuildStatus>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<long?>(),
                Arg.Any<bool>())
            .Returns(call => ProjectionRebuildCheckpointSaveResult.Success(
                CreateRebuildCheckpoint(call.ArgAt<long>(1), call.ArgAt<ProjectionRebuildStatus>(2), "operation-1", call.ArgAt<long?>(5))));

        IProjectionWriteActor writeActor = Substitute.For<IProjectionWriteActor>();
        IHttpClientFactory httpClientFactory = CreateHttpClientFactory("""{"projectionType":"counter-summary","state":{"value":1}}""");
        DaprClient daprClient = new DaprClientBuilder().Build();
        (ProjectionUpdateOrchestrator sut, IActorProxyFactory actorProxyFactory, _, IDomainServiceResolver resolver, _) = CreateSut(
            checkpointTracker,
            daprClient,
            httpClientFactory,
            rebuildCheckpointStore: rebuildCheckpointStore);
        _ = resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new DomainServiceRegistration("counter-service", "project", TestIdentity.TenantId, TestIdentity.Domain, "v1"));
        IAggregateActor aggregateActor = Substitute.For<IAggregateActor>();
        _ = aggregateActor.ReadEventsRangeAsync(0L, 2L, Arg.Any<int>())
            .Returns([CreateTestEnvelope(1), CreateTestEnvelope(2)]);
        _ = actorProxyFactory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), "AggregateActor")
            .Returns(aggregateActor);
        _ = actorProxyFactory.CreateActorProxy<IProjectionWriteActor>(Arg.Any<ActorId>(), QueryRouter.ProjectionActorTypeName)
            .Returns(writeActor);

        await sut.RebuildProjectionAsync(CreateRebuildScope());

        _ = await rebuildCheckpointStore.Received(1).SaveAsync(
            Arg.Is<ProjectionRebuildCheckpointScope>(scope => scope.AggregateId == TestIdentity.AggregateId),
            2,
            ProjectionRebuildStatus.Running,
            null,
            Arg.Any<CancellationToken>(),
            2,
            isPerAggregateProgress: true);
        _ = await rebuildCheckpointStore.Received(1).SaveAsync(
            Arg.Is<ProjectionRebuildCheckpointScope>(scope => scope.AggregateId == null),
            0,
            ProjectionRebuildStatus.Succeeded,
            null,
            Arg.Any<CancellationToken>(),
            2,
            isPerAggregateProgress: false);
    }

    [Fact]
    public async Task RebuildProjectionAsync_ProjectRejectionDoesNotAdvanceRebuildCheckpoint() {
        IProjectionCheckpointTracker checkpointTracker = Substitute.For<IProjectionCheckpointTracker>();
        _ = checkpointTracker.EnumerateTrackedIdentitiesAsync(Arg.Any<CancellationToken>())
            .Returns(EnumerateTracked(TestIdentity));
        _ = checkpointTracker.ReadLastDeliveredSequenceAsync(TestIdentity, Arg.Any<CancellationToken>())
            .Returns(0);
        IProjectionRebuildCheckpointStore rebuildCheckpointStore = Substitute.For<IProjectionRebuildCheckpointStore>();
        _ = rebuildCheckpointStore.ReadAsync(Arg.Any<ProjectionRebuildCheckpointScope>(), Arg.Any<CancellationToken>())
            .Returns(CreateRebuildCheckpoint(0, ProjectionRebuildStatus.Running, "operation-1", toPosition: 10));

        IProjectionWriteActor writeActor = Substitute.For<IProjectionWriteActor>();
        IHttpClientFactory httpClientFactory = CreateHttpClientFactory(new FixedResponseHandler(HttpStatusCode.BadRequest, new StringContent("rejected")));
        DaprClient daprClient = new DaprClientBuilder().Build();
        (ProjectionUpdateOrchestrator sut, IActorProxyFactory actorProxyFactory, _, IDomainServiceResolver resolver, _) = CreateSut(
            checkpointTracker,
            daprClient,
            httpClientFactory,
            rebuildCheckpointStore: rebuildCheckpointStore);
        var registration = new DomainServiceRegistration("counter-service", "project", TestIdentity.TenantId, TestIdentity.Domain, "v1");
        _ = resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(registration);
        IAggregateActor aggregateActor = Substitute.For<IAggregateActor>();
        // C1: orchestrator now reads from per-aggregate progress via ReadEventsRangeAsync.
        _ = aggregateActor.ReadEventsRangeAsync(0L, Arg.Any<long?>(), Arg.Any<int>())
            .Returns([CreateTestEnvelope(1)]);
        _ = actorProxyFactory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), "AggregateActor")
            .Returns(aggregateActor);
        _ = actorProxyFactory.CreateActorProxy<IProjectionWriteActor>(Arg.Any<ActorId>(), QueryRouter.ProjectionActorTypeName)
            .Returns(writeActor);

        await sut.RebuildProjectionAsync(CreateRebuildScope());

        // Rejection records a safe Failed status without advancing beyond the last applied sequence.
        await writeActor.DidNotReceiveWithAnyArgs().UpdateProjectionAsync(default!);
        _ = await rebuildCheckpointStore.Received(1).ResetAsync(
            Arg.Is<ProjectionRebuildCheckpointScope>(scope => scope.OperationId == "operation-1"),
            0,
            ProjectionRebuildStatus.Failed,
            StreamReplayReasonCodes.ProjectionApplyRejected,
            Arg.Any<CancellationToken>(),
            10);
    }

    [Fact]
    public async Task RebuildProjectionAsync_CanceledOperationDoesNotAdvanceRebuildCheckpoint() {
        IProjectionCheckpointTracker checkpointTracker = Substitute.For<IProjectionCheckpointTracker>();
        _ = checkpointTracker.EnumerateTrackedIdentitiesAsync(Arg.Any<CancellationToken>())
            .Returns(EnumerateTracked(TestIdentity));
        IProjectionRebuildCheckpointStore rebuildCheckpointStore = Substitute.For<IProjectionRebuildCheckpointStore>();
        _ = rebuildCheckpointStore.ReadAsync(Arg.Any<ProjectionRebuildCheckpointScope>(), Arg.Any<CancellationToken>())
            .Returns(
                CreateRebuildCheckpoint(0, ProjectionRebuildStatus.Running, "operation-1", toPosition: 10),
                CreateRebuildCheckpoint(0, ProjectionRebuildStatus.Canceled, "operation-1", toPosition: 10));
        (ProjectionUpdateOrchestrator sut, _, _, IDomainServiceResolver resolver, _) = CreateSut(
            checkpointTracker,
            rebuildCheckpointStore: rebuildCheckpointStore);

        await sut.RebuildProjectionAsync(CreateRebuildScope());

        _ = await resolver.DidNotReceiveWithAnyArgs().ResolveAsync(default!, default!, default!, default);
        _ = await rebuildCheckpointStore.DidNotReceiveWithAnyArgs().SaveAsync(default!, default, default, default, default, default);
    }

    [Fact]
    public async Task RebuildProjectionAsync_WithMoreEventsThanPageSizeDoesNotWriteSucceeded() {
        IProjectionCheckpointTracker checkpointTracker = Substitute.For<IProjectionCheckpointTracker>();
        _ = checkpointTracker.EnumerateTrackedIdentitiesAsync(Arg.Any<CancellationToken>())
            .Returns(EnumerateTracked(TestIdentity));
        _ = checkpointTracker.ReadLastDeliveredSequenceAsync(TestIdentity, Arg.Any<CancellationToken>())
            .Returns(0);
        _ = checkpointTracker.SaveDeliveredSequenceAsync(TestIdentity, Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(true);
        IProjectionRebuildCheckpointStore rebuildCheckpointStore = Substitute.For<IProjectionRebuildCheckpointStore>();
        ProjectionRebuildCheckpoint currentCheckpoint = CreateRebuildCheckpoint(0, ProjectionRebuildStatus.Running, "operation-1", toPosition: 1000);
        _ = rebuildCheckpointStore.ReadAsync(Arg.Any<ProjectionRebuildCheckpointScope>(), Arg.Any<CancellationToken>())
            .Returns(_ => currentCheckpoint);
        _ = rebuildCheckpointStore.SaveAsync(
                Arg.Any<ProjectionRebuildCheckpointScope>(),
                Arg.Any<long>(),
                Arg.Any<ProjectionRebuildStatus>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<long?>())
            .Returns(call => {
                currentCheckpoint = CreateRebuildCheckpoint(call.ArgAt<long>(1), call.ArgAt<ProjectionRebuildStatus>(2), "operation-1", call.ArgAt<long?>(5));
                return ProjectionRebuildCheckpointSaveResult.Success(currentCheckpoint);
            });
        IProjectionWriteActor writeActor = Substitute.For<IProjectionWriteActor>();
        IHttpClientFactory httpClientFactory = CreateHttpClientFactory("""{"projectionType":"counter-summary","state":{"value":1}}""");
        DaprClient daprClient = new DaprClientBuilder().Build();
        (ProjectionUpdateOrchestrator sut, IActorProxyFactory actorProxyFactory, _, IDomainServiceResolver resolver, _) = CreateSut(
            checkpointTracker,
            daprClient,
            httpClientFactory,
            rebuildCheckpointStore: rebuildCheckpointStore);
        _ = resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new DomainServiceRegistration("counter-service", "project", TestIdentity.TenantId, TestIdentity.Domain, "v1"));
        IAggregateActor aggregateActor = Substitute.For<IAggregateActor>();
        EventEnvelope[] firstPage = Enumerable.Range(1, 256).Select(sequence => CreateTestEnvelope(sequence)).ToArray();
        _ = aggregateActor.ReadEventsRangeAsync(0L, 1000L, Arg.Any<int>()).Returns(firstPage);
        _ = actorProxyFactory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), "AggregateActor").Returns(aggregateActor);
        _ = actorProxyFactory.CreateActorProxy<IProjectionWriteActor>(Arg.Any<ActorId>(), QueryRouter.ProjectionActorTypeName).Returns(writeActor);

        await sut.RebuildProjectionAsync(CreateRebuildScope());

        _ = await rebuildCheckpointStore.Received(1).SaveAsync(
            Arg.Any<ProjectionRebuildCheckpointScope>(),
            256,
            ProjectionRebuildStatus.Running,
            null,
            Arg.Any<CancellationToken>(),
            1000,
            isPerAggregateProgress: true);
        _ = await rebuildCheckpointStore.DidNotReceive().SaveAsync(
            Arg.Any<ProjectionRebuildCheckpointScope>(),
            Arg.Any<long>(),
            ProjectionRebuildStatus.Succeeded,
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<long?>(),
            Arg.Any<bool>());
    }

    [Fact]
    public async Task RebuildProjectionAsync_MixedPageCompletionDoesNotWriteSucceeded() {
        var secondIdentity = new AggregateIdentity(TestIdentity.TenantId, TestIdentity.Domain, "agg-002");
        IProjectionCheckpointTracker checkpointTracker = Substitute.For<IProjectionCheckpointTracker>();
        _ = checkpointTracker.EnumerateTrackedIdentitiesAsync(Arg.Any<CancellationToken>())
            .Returns(EnumerateTracked(TestIdentity, secondIdentity));
        _ = checkpointTracker.SaveDeliveredSequenceAsync(Arg.Any<AggregateIdentity>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(true);
        IProjectionRebuildCheckpointStore rebuildCheckpointStore = Substitute.For<IProjectionRebuildCheckpointStore>();
        ProjectionRebuildCheckpoint operatorCheckpoint = CreateRebuildCheckpoint(0, ProjectionRebuildStatus.Running, "operation-1", toPosition: 1000);
        _ = rebuildCheckpointStore.ReadAsync(
                Arg.Is<ProjectionRebuildCheckpointScope>(scope => scope.AggregateId == null),
                Arg.Any<CancellationToken>())
            .Returns(operatorCheckpoint);
        _ = rebuildCheckpointStore.ReadAsync(
                Arg.Is<ProjectionRebuildCheckpointScope>(scope => scope.AggregateId != null),
                Arg.Any<CancellationToken>())
            .Returns((ProjectionRebuildCheckpoint?)null);
        _ = rebuildCheckpointStore.SaveAsync(
                Arg.Any<ProjectionRebuildCheckpointScope>(),
                Arg.Any<long>(),
                Arg.Any<ProjectionRebuildStatus>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<long?>(),
                Arg.Any<bool>())
            .Returns(call => ProjectionRebuildCheckpointSaveResult.Success(
                CreateRebuildCheckpoint(call.ArgAt<long>(1), call.ArgAt<ProjectionRebuildStatus>(2), "operation-1", call.ArgAt<long?>(5))));
        IProjectionWriteActor writeActor = Substitute.For<IProjectionWriteActor>();
        IHttpClientFactory httpClientFactory = CreateHttpClientFactory("""{"projectionType":"counter-summary","state":{"value":1}}""");
        DaprClient daprClient = new DaprClientBuilder().Build();
        (ProjectionUpdateOrchestrator sut, IActorProxyFactory actorProxyFactory, _, IDomainServiceResolver resolver, _) = CreateSut(
            checkpointTracker,
            daprClient,
            httpClientFactory,
            rebuildCheckpointStore: rebuildCheckpointStore);
        _ = resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new DomainServiceRegistration("counter-service", "project", TestIdentity.TenantId, TestIdentity.Domain, "v1"));
        IAggregateActor aggregateActor = Substitute.For<IAggregateActor>();
        EventEnvelope[] firstPage = Enumerable.Range(1, 256).Select(sequence => CreateTestEnvelope(sequence)).ToArray();
        EventEnvelope[] secondPage = [CreateTestEnvelope(1, secondIdentity.AggregateId)];
        _ = aggregateActor.ReadEventsRangeAsync(0L, 1000L, Arg.Any<int>()).Returns(firstPage, secondPage);
        _ = actorProxyFactory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), "AggregateActor").Returns(aggregateActor);
        _ = actorProxyFactory.CreateActorProxy<IProjectionWriteActor>(Arg.Any<ActorId>(), QueryRouter.ProjectionActorTypeName).Returns(writeActor);

        await sut.RebuildProjectionAsync(CreateRebuildScope());

        await writeActor.Received(2).UpdateProjectionAsync(Arg.Any<ProjectionState>());
        _ = await rebuildCheckpointStore.DidNotReceive().SaveAsync(
            Arg.Any<ProjectionRebuildCheckpointScope>(),
            Arg.Any<long>(),
            ProjectionRebuildStatus.Succeeded,
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<long?>(),
            Arg.Any<bool>());
    }

    [Fact]
    public async Task RebuildProjectionAsync_PerAggregateOperationChangeInterruptsBeforeProjectionWrite() {
        IProjectionCheckpointTracker checkpointTracker = Substitute.For<IProjectionCheckpointTracker>();
        _ = checkpointTracker.EnumerateTrackedIdentitiesAsync(Arg.Any<CancellationToken>())
            .Returns(EnumerateTracked(TestIdentity));
        IProjectionRebuildCheckpointStore rebuildCheckpointStore = Substitute.For<IProjectionRebuildCheckpointStore>();
        ProjectionRebuildCheckpoint operatorCheckpoint = CreateRebuildCheckpoint(0, ProjectionRebuildStatus.Running, "operation-2", toPosition: 10);
        ProjectionRebuildCheckpoint initialPerAggregateCheckpoint = CreateRebuildCheckpoint(0, ProjectionRebuildStatus.Running, "operation-2", toPosition: 10);
        ProjectionRebuildCheckpoint overwrittenPerAggregateCheckpoint = CreateRebuildCheckpoint(0, ProjectionRebuildStatus.Running, "operation-3", toPosition: 10);
        _ = rebuildCheckpointStore.ReadAsync(
                Arg.Is<ProjectionRebuildCheckpointScope>(scope => scope.AggregateId == null),
                Arg.Any<CancellationToken>())
            .Returns(operatorCheckpoint);
        _ = rebuildCheckpointStore.ReadAsync(
                Arg.Is<ProjectionRebuildCheckpointScope>(scope => scope.AggregateId == TestIdentity.AggregateId),
                Arg.Any<CancellationToken>())
            .Returns(initialPerAggregateCheckpoint, overwrittenPerAggregateCheckpoint);
        IProjectionWriteActor writeActor = Substitute.For<IProjectionWriteActor>();
        IHttpClientFactory httpClientFactory = CreateHttpClientFactory("""{"projectionType":"counter-summary","state":{"value":1}}""");
        DaprClient daprClient = new DaprClientBuilder().Build();
        (ProjectionUpdateOrchestrator sut, IActorProxyFactory actorProxyFactory, _, IDomainServiceResolver resolver, _) = CreateSut(
            checkpointTracker,
            daprClient,
            httpClientFactory,
            rebuildCheckpointStore: rebuildCheckpointStore);
        _ = resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new DomainServiceRegistration("counter-service", "project", TestIdentity.TenantId, TestIdentity.Domain, "v1"));
        IAggregateActor aggregateActor = Substitute.For<IAggregateActor>();
        _ = aggregateActor.ReadEventsRangeAsync(0L, 10L, Arg.Any<int>()).Returns([CreateTestEnvelope(1)]);
        _ = actorProxyFactory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), "AggregateActor").Returns(aggregateActor);
        _ = actorProxyFactory.CreateActorProxy<IProjectionWriteActor>(Arg.Any<ActorId>(), QueryRouter.ProjectionActorTypeName).Returns(writeActor);

        await sut.RebuildProjectionAsync(CreateRebuildScope() with { OperationId = "operation-2" });

        await writeActor.DidNotReceiveWithAnyArgs().UpdateProjectionAsync(default!);
        _ = await rebuildCheckpointStore.DidNotReceive().SaveAsync(
            Arg.Is<ProjectionRebuildCheckpointScope>(scope => scope.AggregateId == TestIdentity.AggregateId),
            Arg.Any<long>(),
            ProjectionRebuildStatus.Running,
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<long?>(),
            isPerAggregateProgress: true);
    }

    [Fact]
    public async Task RebuildProjectionAsync_LifecycleCanceledBeforeWriteDoesNotUpdateProjection() {
        IProjectionCheckpointTracker checkpointTracker = Substitute.For<IProjectionCheckpointTracker>();
        _ = checkpointTracker.EnumerateTrackedIdentitiesAsync(Arg.Any<CancellationToken>())
            .Returns(EnumerateTracked(TestIdentity));
        _ = checkpointTracker.ReadLastDeliveredSequenceAsync(TestIdentity, Arg.Any<CancellationToken>())
            .Returns(0);
        IProjectionRebuildCheckpointStore rebuildCheckpointStore = Substitute.For<IProjectionRebuildCheckpointStore>();
        _ = rebuildCheckpointStore.ReadAsync(Arg.Any<ProjectionRebuildCheckpointScope>(), Arg.Any<CancellationToken>())
            .Returns(
                CreateRebuildCheckpoint(0, ProjectionRebuildStatus.Running, "operation-1", toPosition: 10),
                CreateRebuildCheckpoint(0, ProjectionRebuildStatus.Running, "operation-1", toPosition: 10),
                CreateRebuildCheckpoint(0, ProjectionRebuildStatus.Canceled, "operation-1", toPosition: 10));
        IProjectionWriteActor writeActor = Substitute.For<IProjectionWriteActor>();
        IHttpClientFactory httpClientFactory = CreateHttpClientFactory("""{"projectionType":"counter-summary","state":{"value":1}}""");
        DaprClient daprClient = new DaprClientBuilder().Build();
        (ProjectionUpdateOrchestrator sut, IActorProxyFactory actorProxyFactory, _, IDomainServiceResolver resolver, _) = CreateSut(
            checkpointTracker,
            daprClient,
            httpClientFactory,
            rebuildCheckpointStore: rebuildCheckpointStore);
        _ = resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new DomainServiceRegistration("counter-service", "project", TestIdentity.TenantId, TestIdentity.Domain, "v1"));
        IAggregateActor aggregateActor = Substitute.For<IAggregateActor>();
        _ = aggregateActor.ReadEventsRangeAsync(0L, 10L, Arg.Any<int>()).Returns([CreateTestEnvelope(1)]);
        _ = actorProxyFactory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), "AggregateActor").Returns(aggregateActor);
        _ = actorProxyFactory.CreateActorProxy<IProjectionWriteActor>(Arg.Any<ActorId>(), QueryRouter.ProjectionActorTypeName).Returns(writeActor);

        await sut.RebuildProjectionAsync(CreateRebuildScope());

        await writeActor.DidNotReceiveWithAnyArgs().UpdateProjectionAsync(default!);
        _ = await rebuildCheckpointStore.DidNotReceiveWithAnyArgs().SaveAsync(default!, default, default, default, default, default);
    }

    [Fact]
    public async Task RebuildProjectionAsync_PerAggregateCheckpointReadFailureDoesNotRestartAggregateFromZero() {
        // D3-A/D3-E: orchestrator now reads per-aggregate progress from the rebuild checkpoint
        // store (per-aggregate scope), not the poller checkpoint tracker. A failure on the
        // per-aggregate ReadAsync must propagate as a thrown exception rather than silently
        // restart the aggregate from sequence 0.
        IProjectionCheckpointTracker checkpointTracker = Substitute.For<IProjectionCheckpointTracker>();
        _ = checkpointTracker.EnumerateTrackedIdentitiesAsync(Arg.Any<CancellationToken>())
            .Returns(EnumerateTracked(TestIdentity));
        IProjectionRebuildCheckpointStore rebuildCheckpointStore = Substitute.For<IProjectionRebuildCheckpointStore>();
        // Operator scope read returns active checkpoint; per-aggregate scope read throws.
        _ = rebuildCheckpointStore.ReadAsync(Arg.Is<ProjectionRebuildCheckpointScope>(s => s.AggregateId == null), Arg.Any<CancellationToken>())
            .Returns(CreateRebuildCheckpoint(0, ProjectionRebuildStatus.Running, "operation-1", toPosition: 10));
        _ = rebuildCheckpointStore.ReadAsync(Arg.Is<ProjectionRebuildCheckpointScope>(s => s.AggregateId == TestIdentity.AggregateId), Arg.Any<CancellationToken>())
            .Returns<ProjectionRebuildCheckpoint?>(_ => throw new InvalidOperationException("rebuild checkpoint read failed"));
        _ = rebuildCheckpointStore.ResetAsync(
                Arg.Any<ProjectionRebuildCheckpointScope>(),
                Arg.Any<long>(),
                ProjectionRebuildStatus.Failed,
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<long?>())
            .Returns(ProjectionRebuildCheckpointSaveResult.Success(CreateRebuildCheckpoint(0, ProjectionRebuildStatus.Failed, "operation-1", toPosition: 10)));
        IAggregateActor aggregateActor = Substitute.For<IAggregateActor>();
        (ProjectionUpdateOrchestrator sut, IActorProxyFactory actorProxyFactory, _, IDomainServiceResolver resolver, _) = CreateSut(
            checkpointTracker,
            rebuildCheckpointStore: rebuildCheckpointStore);
        _ = actorProxyFactory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), "AggregateActor").Returns(aggregateActor);

        await Should.ThrowAsync<InvalidOperationException>(() => sut.RebuildProjectionAsync(CreateRebuildScope()));

        _ = await aggregateActor.DidNotReceiveWithAnyArgs().ReadEventsRangeAsync(default, default, default);
    }

    [Fact]
    public async Task RebuildProjectionAsync_CancellationDuringEnumerationWritesCanceledCheckpoint() {
        // DEC3-P: cancel-cleanup uses ResetAsync (not SaveAsync) to bypass IsLifecycleProtected
        // guards. Operator-intentional terminal writes flow through the documented trust boundary.
        IProjectionCheckpointTracker checkpointTracker = Substitute.For<IProjectionCheckpointTracker>();
        _ = checkpointTracker.EnumerateTrackedIdentitiesAsync(Arg.Any<CancellationToken>())
            .Returns(ThrowCanceledBeforeFirstIdentity());
        IProjectionRebuildCheckpointStore rebuildCheckpointStore = Substitute.For<IProjectionRebuildCheckpointStore>();
        _ = rebuildCheckpointStore.ReadAsync(Arg.Any<ProjectionRebuildCheckpointScope>(), Arg.Any<CancellationToken>())
            .Returns(CreateRebuildCheckpoint(7, ProjectionRebuildStatus.Running, "operation-1", toPosition: 10));
        _ = rebuildCheckpointStore.ResetAsync(
                Arg.Any<ProjectionRebuildCheckpointScope>(),
                Arg.Any<long>(),
                Arg.Any<ProjectionRebuildStatus>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<long?>())
            .Returns(ProjectionRebuildCheckpointSaveResult.Success(CreateRebuildCheckpoint(7, ProjectionRebuildStatus.Canceled, "operation-1", toPosition: 10)));
        (ProjectionUpdateOrchestrator sut, _, _, _, _) = CreateSut(checkpointTracker, rebuildCheckpointStore: rebuildCheckpointStore);

        _ = await Should.ThrowAsync<OperationCanceledException>(() => sut.RebuildProjectionAsync(CreateRebuildScope()));

        _ = await rebuildCheckpointStore.Received(1).ResetAsync(
            Arg.Any<ProjectionRebuildCheckpointScope>(),
            7,
            ProjectionRebuildStatus.Canceled,
            StreamReplayReasonCodes.RebuildCanceled,
            Arg.Any<CancellationToken>(),
            10);
    }

    [Fact]
    public async Task RebuildProjectionAsync_PerAggregateProgressDrivesReadEventsRangeFromBoundary() {
        // P24: pin inclusive/exclusive `fromSequence` semantic. Per-aggregate progress at 5L
        // must drive ReadEventsRangeAsync(5L, ...) — a regression that flips the semantic would
        // silently re-apply or skip the next event.
        IProjectionCheckpointTracker checkpointTracker = Substitute.For<IProjectionCheckpointTracker>();
        _ = checkpointTracker.EnumerateTrackedIdentitiesAsync(Arg.Any<CancellationToken>())
            .Returns(EnumerateTracked(TestIdentity));
        _ = checkpointTracker.SaveDeliveredSequenceAsync(TestIdentity, Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(true);
        IProjectionRebuildCheckpointStore rebuildCheckpointStore = Substitute.For<IProjectionRebuildCheckpointStore>();
        // Operator scope read returns the lifecycle checkpoint.
        ProjectionRebuildCheckpoint operatorSnap = CreateRebuildCheckpoint(0, ProjectionRebuildStatus.Running, "operation-1", toPosition: 10);
        _ = rebuildCheckpointStore.ReadAsync(Arg.Is<ProjectionRebuildCheckpointScope>(s => s.AggregateId == null), Arg.Any<CancellationToken>())
            .Returns(operatorSnap);
        // Per-aggregate scope read returns existing progress at 5L.
        ProjectionRebuildCheckpoint perAggregateSnap = CreateRebuildCheckpoint(5, ProjectionRebuildStatus.Running, "operation-1", toPosition: 10);
        _ = rebuildCheckpointStore.ReadAsync(Arg.Is<ProjectionRebuildCheckpointScope>(s => s.AggregateId == TestIdentity.AggregateId), Arg.Any<CancellationToken>())
            .Returns(perAggregateSnap);
        _ = rebuildCheckpointStore.SaveAsync(
                Arg.Any<ProjectionRebuildCheckpointScope>(),
                Arg.Any<long>(),
                Arg.Any<ProjectionRebuildStatus>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<long?>(),
                Arg.Any<bool>())
            .Returns(call => ProjectionRebuildCheckpointSaveResult.Success(
                CreateRebuildCheckpoint(call.ArgAt<long>(1), call.ArgAt<ProjectionRebuildStatus>(2), "operation-1", call.ArgAt<long?>(5))));

        IProjectionWriteActor writeActor = Substitute.For<IProjectionWriteActor>();
        IHttpClientFactory httpClientFactory = CreateHttpClientFactory("""{"projectionType":"counter-summary","state":{"value":1}}""");
        DaprClient daprClient = new DaprClientBuilder().Build();
        (ProjectionUpdateOrchestrator sut, IActorProxyFactory actorProxyFactory, _, IDomainServiceResolver resolver, _) = CreateSut(
            checkpointTracker,
            daprClient,
            httpClientFactory,
            rebuildCheckpointStore: rebuildCheckpointStore);
        _ = resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new DomainServiceRegistration("counter-service", "project", TestIdentity.TenantId, TestIdentity.Domain, "v1"));
        IAggregateActor aggregateActor = Substitute.For<IAggregateActor>();
        // The new event at sequence 6 is past the per-aggregate progress (5).
        _ = aggregateActor.ReadEventsRangeAsync(5L, 10L, Arg.Any<int>())
            .Returns([CreateTestEnvelope(6)]);
        _ = actorProxyFactory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), "AggregateActor")
            .Returns(aggregateActor);
        _ = actorProxyFactory.CreateActorProxy<IProjectionWriteActor>(Arg.Any<ActorId>(), QueryRouter.ProjectionActorTypeName)
            .Returns(writeActor);

        await sut.RebuildProjectionAsync(CreateRebuildScope());

        // Critical assertion: ReadEventsRangeAsync was called with from=5, NOT from=0.
        _ = await aggregateActor.Received(1).ReadEventsRangeAsync(5L, 10L, Arg.Any<int>());
        _ = await aggregateActor.DidNotReceive().ReadEventsRangeAsync(0L, Arg.Any<long?>(), Arg.Any<int>());
    }

    [Fact]
    public async Task RebuildProjectionAsync_CancelCleanupNullSnapshotIsNoOpWithoutSave() {
        // P26: when the post-cancel ReadAsync returns null (e.g., state-store transient
        // unavailability), the cancel-cleanup path must not attempt a Save/ResetAsync that
        // would NRE; it should silently no-op while still propagating the cancellation.
        IProjectionCheckpointTracker checkpointTracker = Substitute.For<IProjectionCheckpointTracker>();
        _ = checkpointTracker.EnumerateTrackedIdentitiesAsync(Arg.Any<CancellationToken>())
            .Returns(ThrowCanceledBeforeFirstIdentity());
        IProjectionRebuildCheckpointStore rebuildCheckpointStore = Substitute.For<IProjectionRebuildCheckpointStore>();
        // First read (initial CanRunRebuild check) returns Running.
        // Second read (post-cancel) returns null (state-store unavailable mid-cancel).
        int readCount = 0;
        _ = rebuildCheckpointStore.ReadAsync(Arg.Any<ProjectionRebuildCheckpointScope>(), Arg.Any<CancellationToken>())
            .Returns(_ => {
                readCount++;
                return readCount == 1
                    ? CreateRebuildCheckpoint(7, ProjectionRebuildStatus.Running, "operation-1", toPosition: 10)
                    : null;
            });
        (ProjectionUpdateOrchestrator sut, _, _, _, _) = CreateSut(checkpointTracker, rebuildCheckpointStore: rebuildCheckpointStore);

        _ = await Should.ThrowAsync<OperationCanceledException>(() => sut.RebuildProjectionAsync(CreateRebuildScope()));

        // Critical assertion: no ResetAsync was attempted on the null-snapshot path.
        _ = await rebuildCheckpointStore.DidNotReceiveWithAnyArgs().ResetAsync(default!, default, default, default, default, default);
        _ = await rebuildCheckpointStore.DidNotReceiveWithAnyArgs().SaveAsync(default!, default, default, default, default, default);
    }

    [Fact]
    public async Task UpdateProjectionAsync_NoDomainServiceRegistered_ReturnsWithoutError() {
        // Arrange
        (ProjectionUpdateOrchestrator sut, IActorProxyFactory actorProxyFactory, _, IDomainServiceResolver resolver, _) = CreateSut();
        _ = resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((DomainServiceRegistration?)null);

        // Act
        await sut.UpdateProjectionAsync(TestIdentity);

        // Assert - no actor proxy calls
        _ = actorProxyFactory.DidNotReceiveWithAnyArgs().CreateActorProxy<IAggregateActor>(default!, default!);
    }

    // --- Test 2: AC 3 - No events ---

    [Fact]
    public async Task UpdateProjectionAsync_NoEvents_ReturnsWithoutError() {
        // Arrange
        (ProjectionUpdateOrchestrator sut, IActorProxyFactory actorProxyFactory, _, IDomainServiceResolver resolver, IProjectionCheckpointTracker checkpointTracker) = CreateSut();
        var registration = new DomainServiceRegistration("counter-service", "project", "test-tenant", "test-domain", "v1");
        _ = resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(registration);

        IAggregateActor aggregateActor = Substitute.For<IAggregateActor>();
        _ = aggregateActor.GetEventsAsync(0).Returns(Array.Empty<EventEnvelope>());
        _ = actorProxyFactory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), "AggregateActor")
            .Returns(aggregateActor);

        // Act
        await sut.UpdateProjectionAsync(TestIdentity);

        // Assert - GetEventsAsync was called but no further processing
        _ = await aggregateActor.Received(1).GetEventsAsync(0);
        await checkpointTracker.DidNotReceiveWithAnyArgs().SaveDeliveredSequenceAsync(default!, default, default);
        // No write actor proxy should be created
        _ = actorProxyFactory.DidNotReceiveWithAnyArgs().CreateActorProxy<IProjectionWriteActor>(default!, default!);
    }

    // --- Test 3: AC 2 - Resolver called with correct parameters ---

    [Fact]
    public async Task UpdateProjectionAsync_WithEvents_CallsResolverWithCorrectParameters() {
        // Arrange
        (ProjectionUpdateOrchestrator sut, IActorProxyFactory actorProxyFactory, _, IDomainServiceResolver resolver, _) = CreateSut();
        var registration = new DomainServiceRegistration("counter-service", "project", "test-tenant", "test-domain", "v1");
        _ = resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(registration);

        IAggregateActor aggregateActor = Substitute.For<IAggregateActor>();
        _ = aggregateActor.GetEventsAsync(0).Returns(new[] { CreateTestEnvelope() });
        _ = actorProxyFactory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(aggregateActor);

        // Act -- DaprClient.InvokeMethodAsync is non-virtual, so it will throw internally;
        // the orchestrator's try/catch handles it gracefully.
        await sut.UpdateProjectionAsync(TestIdentity);

        // Assert - Resolver was called with correct identity parameters
        _ = await resolver.Received(1).ResolveAsync("test-tenant", "test-domain", "v1", Arg.Any<CancellationToken>());
    }

    // --- Test 4: AC 2 - Aggregate actor proxy uses correct type name and actor ID ---

    [Fact]
    public async Task UpdateProjectionAsync_CreatesAggregateActorProxyWithCorrectTypeNameAndId() {
        // Arrange
        (ProjectionUpdateOrchestrator sut, IActorProxyFactory actorProxyFactory, _, IDomainServiceResolver resolver, _) = CreateSut();
        var registration = new DomainServiceRegistration("counter-service", "project", "test-tenant", "test-domain", "v1");
        _ = resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(registration);

        IAggregateActor aggregateActor = Substitute.For<IAggregateActor>();
        _ = aggregateActor.GetEventsAsync(0).Returns(new[] { CreateTestEnvelope() });
        _ = actorProxyFactory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(aggregateActor);

        // Act
        await sut.UpdateProjectionAsync(TestIdentity);

        // Assert - Actor proxy created with correct type name "AggregateActor" and correct actor ID
        string expectedActorId = TestIdentity.ActorId; // "test-tenant:test-domain:agg-001"
        _ = actorProxyFactory.Received(1).CreateActorProxy<IAggregateActor>(
            Arg.Is<ActorId>(id => id.GetId() == expectedActorId),
            Arg.Is("AggregateActor"));
    }

    // --- Test 5: AC 2 - GetEventsAsync called with 0 (full replay) ---

    [Fact]
    public async Task UpdateProjectionAsync_MissingCheckpoint_CallsGetEventsAsyncWithZero() {
        // Arrange
        (ProjectionUpdateOrchestrator sut, IActorProxyFactory actorProxyFactory, _, IDomainServiceResolver resolver, _) = CreateSut();
        var registration = new DomainServiceRegistration("counter-service", "project", "test-tenant", "test-domain", "v1");
        _ = resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(registration);

        IAggregateActor aggregateActor = Substitute.For<IAggregateActor>();
        _ = aggregateActor.GetEventsAsync(0).Returns(new[] { CreateTestEnvelope() });
        _ = actorProxyFactory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(aggregateActor);

        // Act
        await sut.UpdateProjectionAsync(TestIdentity);

        // Assert - First delivery replays from sequence 0
        _ = await aggregateActor.Received(1).GetEventsAsync(0);
    }

    [Fact]
    public async Task UpdateProjectionAsync_ImmediateDelivery_AlwaysReadsFullHistory() {
        // Arrange
        IProjectionCheckpointTracker checkpointTracker = Substitute.For<IProjectionCheckpointTracker>();
        (ProjectionUpdateOrchestrator sut, IActorProxyFactory actorProxyFactory, _, IDomainServiceResolver resolver, _) = CreateSut(checkpointTracker);
        var registration = new DomainServiceRegistration("counter-service", "project", "test-tenant", "test-domain", "v1");
        _ = resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(registration);

        IAggregateActor aggregateActor = Substitute.For<IAggregateActor>();
        _ = aggregateActor.GetEventsAsync(0).Returns(Array.Empty<EventEnvelope>());
        _ = aggregateActor.GetEventsAsync(7).Returns([CreateTestEnvelope(8)]);
        _ = actorProxyFactory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), "AggregateActor")
            .Returns(aggregateActor);

        // Act
        await sut.UpdateProjectionAsync(TestIdentity);

        // Assert -- full-replay contract preserved (no GetEventsAsync(7) shortcut). Checkpoint
        // is read for drift detection (DW1), but immediate delivery still replays from 0.
        _ = await aggregateActor.Received(1).GetEventsAsync(0);
        _ = await aggregateActor.DidNotReceive().GetEventsAsync(7);
    }

    [Fact]
    public async Task UpdateProjectionAsync_ExistingCheckpoint_ReplaysFromZeroUntilIncrementalContractIsResolved() {
        // Arrange
        IProjectionCheckpointTracker checkpointTracker = Substitute.For<IProjectionCheckpointTracker>();
        _ = checkpointTracker.ReadLastDeliveredSequenceAsync(TestIdentity, Arg.Any<CancellationToken>())
            .Returns(7);
        (ProjectionUpdateOrchestrator sut, IActorProxyFactory actorProxyFactory, _, IDomainServiceResolver resolver, _) = CreateSut(checkpointTracker);
        var registration = new DomainServiceRegistration("counter-service", "project", "test-tenant", "test-domain", "v1");
        _ = resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(registration);

        IAggregateActor aggregateActor = Substitute.For<IAggregateActor>();
        // Use a non-empty stream so this test stays focused on full-replay contract rather
        // than the drift-detection branch (covered by UpdateProjectionAsync_EmptyEventsWithStaleCheckpoint).
        _ = aggregateActor.GetEventsAsync(0).Returns([CreateTestEnvelope(8)]);
        _ = actorProxyFactory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(aggregateActor);

        // Act
        await sut.UpdateProjectionAsync(TestIdentity);

        // Assert -- immediate delivery stays full-replay (no GetEventsAsync(7)) until the
        // projection handler state contract is resolved. The checkpoint read is now used
        // for drift detection (DW1) but does not change the from-sequence value passed to
        // GetEventsAsync.
        _ = await aggregateActor.Received(1).GetEventsAsync(0);
        _ = await aggregateActor.DidNotReceive().GetEventsAsync(7);
    }

    [Fact]
    public async Task UpdateProjectionAsync_RepeatTriggersOnSameAggregate_ProducesIdenticalProjectionState() {
        // Arrange
        IProjectionCheckpointTracker checkpointTracker = Substitute.For<IProjectionCheckpointTracker>();
        _ = checkpointTracker.ReadLastDeliveredSequenceAsync(TestIdentity, Arg.Any<CancellationToken>())
            .Returns(1);
        _ = checkpointTracker.SaveDeliveredSequenceAsync(TestIdentity, Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var capturedStates = new List<ProjectionState>();
        IProjectionWriteActor writeActor = Substitute.For<IProjectionWriteActor>();
        _ = writeActor
            .UpdateProjectionAsync(Arg.Do<ProjectionState>(capturedStates.Add))
            .Returns(Task.CompletedTask);

        IHttpClientFactory httpClientFactory = CreateHttpClientFactory(new CountingProjectionResponseHandler());
        DaprClient daprClient = new DaprClientBuilder().Build();
        (ProjectionUpdateOrchestrator sut, IActorProxyFactory actorProxyFactory, _, IDomainServiceResolver resolver, _) = CreateSut(checkpointTracker, daprClient, httpClientFactory);
        var registration = new DomainServiceRegistration("counter-service", "project", "test-tenant", "test-domain", "v1");
        _ = resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(registration);

        IAggregateActor aggregateActor = Substitute.For<IAggregateActor>();
        _ = aggregateActor.GetEventsAsync(0).Returns([CreateTestEnvelope(1), CreateTestEnvelope(2)]);
        _ = aggregateActor.GetEventsAsync(1).Returns([CreateTestEnvelope(2)]);
        _ = actorProxyFactory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), "AggregateActor")
            .Returns(aggregateActor);
        _ = actorProxyFactory.CreateActorProxy<IProjectionWriteActor>(Arg.Any<ActorId>(), QueryRouter.ProjectionActorTypeName)
            .Returns(writeActor);

        // Act
        await sut.UpdateProjectionAsync(TestIdentity);
        await sut.UpdateProjectionAsync(TestIdentity);

        // Assert
        _ = await aggregateActor.Received(2).GetEventsAsync(0);
        _ = await aggregateActor.DidNotReceive().GetEventsAsync(1);
        capturedStates.Count.ShouldBe(2);
        capturedStates[0].ProjectionType.ShouldBe("counter-summary");
        capturedStates[1].ProjectionType.ShouldBe("counter-summary");
        capturedStates[0].GetState().GetProperty("count").GetInt32().ShouldBe(2);
        capturedStates[1].GetState().GetProperty("count").GetInt32().ShouldBe(2);
        capturedStates[1].StateBytes.ShouldBe(capturedStates[0].StateBytes);
    }

    [Fact]
    public async Task UpdateProjectionAsync_CheckpointReadFails_ReplaysFromZeroWithoutThrowing() {
        // Arrange
        IProjectionCheckpointTracker checkpointTracker = Substitute.For<IProjectionCheckpointTracker>();
        _ = checkpointTracker.ReadLastDeliveredSequenceAsync(TestIdentity, Arg.Any<CancellationToken>())
            .Returns<long>(_ => throw new InvalidOperationException("checkpoint unavailable"));
        (ProjectionUpdateOrchestrator sut, IActorProxyFactory actorProxyFactory, _, IDomainServiceResolver resolver, _) = CreateSut(checkpointTracker);
        var registration = new DomainServiceRegistration("counter-service", "project", "test-tenant", "test-domain", "v1");
        _ = resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(registration);

        IAggregateActor aggregateActor = Substitute.For<IAggregateActor>();
        _ = aggregateActor.GetEventsAsync(0).Returns(Array.Empty<EventEnvelope>());
        _ = actorProxyFactory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(aggregateActor);

        // Act & Assert -- a throwing checkpoint read is logged and falls through to the
        // empty-events branch without raising. The orchestrator must still call
        // GetEventsAsync(0) and not propagate the read exception.
        await Should.NotThrowAsync(() => sut.UpdateProjectionAsync(TestIdentity));
        _ = await aggregateActor.Received(1).GetEventsAsync(0);
    }

    // --- Test 6: AC 3 - Domain service failure does not throw ---

    [Fact]
    public async Task UpdateProjectionAsync_DomainServiceFails_DoesNotThrow() {
        // Arrange
        (ProjectionUpdateOrchestrator sut, IActorProxyFactory actorProxyFactory, _, IDomainServiceResolver resolver, IProjectionCheckpointTracker checkpointTracker) = CreateSut();
        var registration = new DomainServiceRegistration("counter-service", "project", "test-tenant", "test-domain", "v1");
        _ = resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(registration);

        IAggregateActor aggregateActor = Substitute.For<IAggregateActor>();
        _ = aggregateActor.GetEventsAsync(0).Returns(new[] { CreateTestEnvelope() });
        _ = actorProxyFactory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), "AggregateActor")
            .Returns(aggregateActor);

        // DaprClient.InvokeMethodAsync is non-virtual -- it will throw internally on the substitute.
        // The orchestrator's try/catch must handle it.

        // Act & Assert - no exception thrown (fire-and-forget safe)
        await Should.NotThrowAsync(() => sut.UpdateProjectionAsync(TestIdentity));
        await checkpointTracker.DidNotReceiveWithAnyArgs().SaveDeliveredSequenceAsync(default!, default, default);
    }

    // --- Test 7: AC 3 - Resolver failure does not throw ---

    [Fact]
    public async Task UpdateProjectionAsync_ResolverFails_DoesNotThrow() {
        // Arrange
        (ProjectionUpdateOrchestrator sut, _, _, IDomainServiceResolver resolver, IProjectionCheckpointTracker checkpointTracker) = CreateSut();
        _ = resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<DomainServiceRegistration?>(_ => throw new InvalidOperationException("Config store unavailable"));

        // Act & Assert - no exception thrown
        await Should.NotThrowAsync(() => sut.UpdateProjectionAsync(TestIdentity));
        await checkpointTracker.DidNotReceiveWithAnyArgs().SaveDeliveredSequenceAsync(default!, default, default);
    }

    // --- Test 8: AC 3 - GetEventsAsync failure does not throw ---

    [Fact]
    public async Task UpdateProjectionAsync_GetEventsAsyncFails_DoesNotThrow() {
        // Arrange
        (ProjectionUpdateOrchestrator sut, IActorProxyFactory actorProxyFactory, _, IDomainServiceResolver resolver, IProjectionCheckpointTracker checkpointTracker) = CreateSut();
        var registration = new DomainServiceRegistration("counter-service", "project", "test-tenant", "test-domain", "v1");
        _ = resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(registration);

        IAggregateActor aggregateActor = Substitute.For<IAggregateActor>();
        _ = aggregateActor.GetEventsAsync(0).Returns<EventEnvelope[]>(_ => throw new InvalidOperationException("Actor unavailable"));
        _ = actorProxyFactory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), "AggregateActor")
            .Returns(aggregateActor);

        // Act & Assert - no exception thrown
        await Should.NotThrowAsync(() => sut.UpdateProjectionAsync(TestIdentity));
        await checkpointTracker.DidNotReceiveWithAnyArgs().SaveDeliveredSequenceAsync(default!, default, default);
    }

    [Fact]
    public async Task UpdateProjectionAsync_SuccessfulProjectionWrite_SavesMaximumReturnedSequence() {
        // Arrange
        IProjectionCheckpointTracker checkpointTracker = Substitute.For<IProjectionCheckpointTracker>();
        _ = checkpointTracker.ReadLastDeliveredSequenceAsync(TestIdentity, Arg.Any<CancellationToken>())
            .Returns(1);
        _ = checkpointTracker.SaveDeliveredSequenceAsync(TestIdentity, Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(true);
        IProjectionWriteActor writeActor = Substitute.For<IProjectionWriteActor>();
        IHttpClientFactory httpClientFactory = CreateHttpClientFactory("""{"projectionType":"counter-summary","state":{"value":2}}""");
        DaprClient daprClient = new DaprClientBuilder().Build();
        (ProjectionUpdateOrchestrator sut, IActorProxyFactory actorProxyFactory, _, IDomainServiceResolver resolver, _) = CreateSut(checkpointTracker, daprClient, httpClientFactory);
        var registration = new DomainServiceRegistration("counter-service", "project", "test-tenant", "test-domain", "v1");
        _ = resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(registration);

        IAggregateActor aggregateActor = Substitute.For<IAggregateActor>();
        _ = aggregateActor.GetEventsAsync(0).Returns(new[] { CreateTestEnvelope(2), CreateTestEnvelope(5) });
        _ = actorProxyFactory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), "AggregateActor")
            .Returns(aggregateActor);
        _ = actorProxyFactory.CreateActorProxy<IProjectionWriteActor>(Arg.Any<ActorId>(), QueryRouter.ProjectionActorTypeName)
            .Returns(writeActor);

        // Act
        await sut.UpdateProjectionAsync(TestIdentity);

        // Assert
        _ = await aggregateActor.Received(1).GetEventsAsync(0);
        await writeActor.Received(1).UpdateProjectionAsync(Arg.Any<ProjectionState>());
        await checkpointTracker.Received(1).SaveDeliveredSequenceAsync(TestIdentity, 5, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateProjectionAsync_InvalidProjectionResponse_DoesNotSaveCheckpoint() {
        // Arrange
        IProjectionCheckpointTracker checkpointTracker = Substitute.For<IProjectionCheckpointTracker>();
        _ = checkpointTracker.ReadLastDeliveredSequenceAsync(TestIdentity, Arg.Any<CancellationToken>())
            .Returns(0);
        IProjectionWriteActor writeActor = Substitute.For<IProjectionWriteActor>();
        IHttpClientFactory httpClientFactory = CreateHttpClientFactory("""{"projectionType":"","state":{"value":1}}""");
        DaprClient daprClient = new DaprClientBuilder().Build();
        (ProjectionUpdateOrchestrator sut, IActorProxyFactory actorProxyFactory, _, IDomainServiceResolver resolver, _) = CreateSut(checkpointTracker, daprClient, httpClientFactory);
        var registration = new DomainServiceRegistration("counter-service", "project", "test-tenant", "test-domain", "v1");
        _ = resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(registration);

        IAggregateActor aggregateActor = Substitute.For<IAggregateActor>();
        _ = aggregateActor.GetEventsAsync(0).Returns(new[] { CreateTestEnvelope(2) });
        _ = actorProxyFactory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), "AggregateActor")
            .Returns(aggregateActor);
        _ = actorProxyFactory.CreateActorProxy<IProjectionWriteActor>(Arg.Any<ActorId>(), QueryRouter.ProjectionActorTypeName)
            .Returns(writeActor);

        // Act
        await sut.UpdateProjectionAsync(TestIdentity);

        // Assert - empty ProjectionType short-circuits the orchestrator before write/save
        await writeActor.DidNotReceiveWithAnyArgs().UpdateProjectionAsync(default!);
        await checkpointTracker.DidNotReceiveWithAnyArgs().SaveDeliveredSequenceAsync(default!, default, default);
    }

    [Fact]
    public async Task UpdateProjectionAsync_InvalidProjectionResponseWithNullState_DoesNotSaveCheckpoint() {
        // Arrange
        IProjectionCheckpointTracker checkpointTracker = Substitute.For<IProjectionCheckpointTracker>();
        IProjectionWriteActor writeActor = Substitute.For<IProjectionWriteActor>();
        IHttpClientFactory httpClientFactory = CreateHttpClientFactory("""{"projectionType":"counter-summary","state":null}""");
        DaprClient daprClient = new DaprClientBuilder().Build();
        (ProjectionUpdateOrchestrator sut, IActorProxyFactory actorProxyFactory, _, IDomainServiceResolver resolver, _) = CreateSut(checkpointTracker, daprClient, httpClientFactory);
        var registration = new DomainServiceRegistration("counter-service", "project", "test-tenant", "test-domain", "v1");
        _ = resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(registration);

        IAggregateActor aggregateActor = Substitute.For<IAggregateActor>();
        _ = aggregateActor.GetEventsAsync(0).Returns(new[] { CreateTestEnvelope(2) });
        _ = actorProxyFactory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), "AggregateActor")
            .Returns(aggregateActor);
        _ = actorProxyFactory.CreateActorProxy<IProjectionWriteActor>(Arg.Any<ActorId>(), QueryRouter.ProjectionActorTypeName)
            .Returns(writeActor);

        // Act
        await sut.UpdateProjectionAsync(TestIdentity);

        // Assert - null State short-circuits the orchestrator before write/save.
        await writeActor.DidNotReceiveWithAnyArgs().UpdateProjectionAsync(default!);
        await checkpointTracker.DidNotReceiveWithAnyArgs().SaveDeliveredSequenceAsync(default!, default, default);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, ProjectionReasonCodes.ProjectUpstream4xx)]
    [InlineData(HttpStatusCode.InternalServerError, ProjectionReasonCodes.ProjectUpstream5xx)]
    [InlineData(HttpStatusCode.MultipleChoices, ProjectionReasonCodes.ProjectUnexpectedStatus)]
    public async Task UpdateProjectionAsync_ProjectUpstreamFailure_LogsStableReasonAndDoesNotSaveCheckpoint(
        HttpStatusCode statusCode,
        string expectedReasonCode) {
        IProjectionCheckpointTracker checkpointTracker = Substitute.For<IProjectionCheckpointTracker>();
        _ = checkpointTracker.ReadLastDeliveredSequenceAsync(TestIdentity, Arg.Any<CancellationToken>())
            .Returns(0);
        var entries = new List<LogEntry>();
        IHttpClientFactory httpClientFactory = CreateHttpClientFactory(new FixedResponseHandler(
            statusCode,
            new StringContent("""{"error":"not logged"}""", Encoding.UTF8, "application/json")));
        DaprClient daprClient = new DaprClientBuilder().Build();
        (ProjectionUpdateOrchestrator sut, IActorProxyFactory actorProxyFactory, _, IDomainServiceResolver resolver, _) = CreateSut(
            checkpointTracker,
            daprClient,
            httpClientFactory,
            new TestLogger<ProjectionUpdateOrchestrator>(entries));
        var registration = new DomainServiceRegistration("counter-service", "project", "test-tenant", "test-domain", "v1");
        _ = resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(registration);
        IAggregateActor aggregateActor = Substitute.For<IAggregateActor>();
        _ = aggregateActor.GetEventsAsync(0).Returns([CreateTestEnvelope(2)]);
        _ = actorProxyFactory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), "AggregateActor")
            .Returns(aggregateActor);

        await sut.UpdateProjectionAsync(TestIdentity);

        entries.ShouldContain(e =>
            e.EventId.Id == 1141
            && e.Message.Contains($"ReasonCode={expectedReasonCode}", StringComparison.Ordinal)
            && e.Message.Contains($"HttpStatus={(int)statusCode}", StringComparison.Ordinal)
            && e.Message.Contains("AppId=counter-service", StringComparison.Ordinal));
        await checkpointTracker.DidNotReceiveWithAnyArgs().SaveDeliveredSequenceAsync(default!, default, default);
    }

    [Theory]
    [InlineData("text/plain", ProjectionReasonCodes.ProjectUnsupportedContentType)]
    [InlineData(null, ProjectionReasonCodes.ProjectUnsupportedContentType)]
    public async Task UpdateProjectionAsync_ProjectUnsupportedContentType_LogsStableReasonAndDoesNotSaveCheckpoint(
        string? contentType,
        string expectedReasonCode) {
        IProjectionCheckpointTracker checkpointTracker = Substitute.For<IProjectionCheckpointTracker>();
        _ = checkpointTracker.ReadLastDeliveredSequenceAsync(TestIdentity, Arg.Any<CancellationToken>())
            .Returns(0);
        var entries = new List<LogEntry>();
        HttpContent content = contentType is null
            ? new ByteArrayContent(Encoding.UTF8.GetBytes("""{"projectionType":"counter-summary","state":{"value":1}}"""))
            : new StringContent("""{"projectionType":"counter-summary","state":{"value":1}}""", Encoding.UTF8, contentType);
        IHttpClientFactory httpClientFactory = CreateHttpClientFactory(new FixedResponseHandler(HttpStatusCode.OK, content));
        DaprClient daprClient = new DaprClientBuilder().Build();
        (ProjectionUpdateOrchestrator sut, IActorProxyFactory actorProxyFactory, _, IDomainServiceResolver resolver, _) = CreateSut(
            checkpointTracker,
            daprClient,
            httpClientFactory,
            new TestLogger<ProjectionUpdateOrchestrator>(entries));
        var registration = new DomainServiceRegistration("counter-service", "project", "test-tenant", "test-domain", "v1");
        _ = resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(registration);
        IAggregateActor aggregateActor = Substitute.For<IAggregateActor>();
        _ = aggregateActor.GetEventsAsync(0).Returns([CreateTestEnvelope(2)]);
        _ = actorProxyFactory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), "AggregateActor")
            .Returns(aggregateActor);

        await sut.UpdateProjectionAsync(TestIdentity);

        entries.ShouldContain(e => e.EventId.Id == 1141 && e.Message.Contains($"ReasonCode={expectedReasonCode}", StringComparison.Ordinal));
        await checkpointTracker.DidNotReceiveWithAnyArgs().SaveDeliveredSequenceAsync(default!, default, default);
    }

    [Fact]
    public async Task UpdateProjectionAsync_ProjectMalformedJson_LogsStableReasonAndDoesNotSaveCheckpoint() {
        IProjectionCheckpointTracker checkpointTracker = Substitute.For<IProjectionCheckpointTracker>();
        _ = checkpointTracker.ReadLastDeliveredSequenceAsync(TestIdentity, Arg.Any<CancellationToken>())
            .Returns(0);
        var entries = new List<LogEntry>();
        IHttpClientFactory httpClientFactory = CreateHttpClientFactory("{not-json");
        DaprClient daprClient = new DaprClientBuilder().Build();
        (ProjectionUpdateOrchestrator sut, IActorProxyFactory actorProxyFactory, _, IDomainServiceResolver resolver, _) = CreateSut(
            checkpointTracker,
            daprClient,
            httpClientFactory,
            new TestLogger<ProjectionUpdateOrchestrator>(entries));
        var registration = new DomainServiceRegistration("counter-service", "project", "test-tenant", "test-domain", "v1");
        _ = resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(registration);
        IAggregateActor aggregateActor = Substitute.For<IAggregateActor>();
        _ = aggregateActor.GetEventsAsync(0).Returns([CreateTestEnvelope(2)]);
        _ = actorProxyFactory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), "AggregateActor")
            .Returns(aggregateActor);

        await sut.UpdateProjectionAsync(TestIdentity);

        entries.ShouldContain(e =>
            e.EventId.Id == 1142
            && e.Message.Contains($"ReasonCode={ProjectionReasonCodes.ProjectMalformedJson}", StringComparison.Ordinal)
            && e.Message.Contains("ExceptionType=JsonException", StringComparison.Ordinal)
            && e.Message.Contains("HttpStatus=200", StringComparison.Ordinal)
            && e.Message.Contains("ContentType=application/json", StringComparison.Ordinal));
        await checkpointTracker.DidNotReceiveWithAnyArgs().SaveDeliveredSequenceAsync(default!, default, default);
    }

    [Fact]
    public async Task UpdateProjectionAsync_ProjectInvalidCharset_LogsStableReasonAndDoesNotSaveCheckpoint() {
        IProjectionCheckpointTracker checkpointTracker = Substitute.For<IProjectionCheckpointTracker>();
        _ = checkpointTracker.ReadLastDeliveredSequenceAsync(TestIdentity, Arg.Any<CancellationToken>())
            .Returns(0);
        var entries = new List<LogEntry>();
        var content = new ByteArrayContent(Encoding.UTF8.GetBytes("""{"projectionType":"counter-summary","state":{"value":1}}"""));
        _ = content.Headers.TryAddWithoutValidation("Content-Type", "application/json; charset=definitely-not-a-charset");
        IHttpClientFactory httpClientFactory = CreateHttpClientFactory(new FixedResponseHandler(HttpStatusCode.OK, content));
        DaprClient daprClient = new DaprClientBuilder().Build();
        (ProjectionUpdateOrchestrator sut, IActorProxyFactory actorProxyFactory, _, IDomainServiceResolver resolver, _) = CreateSut(
            checkpointTracker,
            daprClient,
            httpClientFactory,
            new TestLogger<ProjectionUpdateOrchestrator>(entries));
        var registration = new DomainServiceRegistration("counter-service", "project", "test-tenant", "test-domain", "v1");
        _ = resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(registration);
        IAggregateActor aggregateActor = Substitute.For<IAggregateActor>();
        _ = aggregateActor.GetEventsAsync(0).Returns([CreateTestEnvelope(2)]);
        _ = actorProxyFactory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), "AggregateActor")
            .Returns(aggregateActor);

        await sut.UpdateProjectionAsync(TestIdentity);

        entries.ShouldContain(e => e.EventId.Id == 1141 && e.Message.Contains($"ReasonCode={ProjectionReasonCodes.ProjectInvalidCharset}", StringComparison.Ordinal));
        await checkpointTracker.DidNotReceiveWithAnyArgs().SaveDeliveredSequenceAsync(default!, default, default);
    }

    [Fact]
    public async Task UpdateProjectionAsync_ProjectStringEmptyState_LogsStableReasonAndDoesNotSaveCheckpoint() {
        IProjectionCheckpointTracker checkpointTracker = Substitute.For<IProjectionCheckpointTracker>();
        _ = checkpointTracker.ReadLastDeliveredSequenceAsync(TestIdentity, Arg.Any<CancellationToken>())
            .Returns(0);
        IProjectionWriteActor writeActor = Substitute.For<IProjectionWriteActor>();
        var entries = new List<LogEntry>();
        IHttpClientFactory httpClientFactory = CreateHttpClientFactory("""{"projectionType":"counter-summary","state":""}""");
        DaprClient daprClient = new DaprClientBuilder().Build();
        (ProjectionUpdateOrchestrator sut, IActorProxyFactory actorProxyFactory, _, IDomainServiceResolver resolver, _) = CreateSut(
            checkpointTracker,
            daprClient,
            httpClientFactory,
            new TestLogger<ProjectionUpdateOrchestrator>(entries));
        var registration = new DomainServiceRegistration("counter-service", "project", "test-tenant", "test-domain", "v1");
        _ = resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(registration);
        IAggregateActor aggregateActor = Substitute.For<IAggregateActor>();
        _ = aggregateActor.GetEventsAsync(0).Returns([CreateTestEnvelope(2)]);
        _ = actorProxyFactory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), "AggregateActor")
            .Returns(aggregateActor);
        _ = actorProxyFactory.CreateActorProxy<IProjectionWriteActor>(Arg.Any<ActorId>(), QueryRouter.ProjectionActorTypeName)
            .Returns(writeActor);

        await sut.UpdateProjectionAsync(TestIdentity);

        entries.ShouldContain(e => e.EventId.Id == 1116 && e.Message.Contains($"ReasonCode={ProjectionReasonCodes.ProjectInvalidState}", StringComparison.Ordinal));
        await writeActor.DidNotReceiveWithAnyArgs().UpdateProjectionAsync(default!);
        await checkpointTracker.DidNotReceiveWithAnyArgs().SaveDeliveredSequenceAsync(default!, default, default);
    }

    [Fact]
    public async Task UpdateProjectionAsync_ProjectTimeout_LogsStableReasonAndDoesNotPropagateAsShutdown() {
        IProjectionCheckpointTracker checkpointTracker = Substitute.For<IProjectionCheckpointTracker>();
        _ = checkpointTracker.ReadLastDeliveredSequenceAsync(TestIdentity, Arg.Any<CancellationToken>())
            .Returns(0);
        var entries = new List<LogEntry>();
        IHttpClientFactory httpClientFactory = CreateHttpClientFactory(new ThrowingResponseHandler(new TaskCanceledException("service timeout")));
        DaprClient daprClient = new DaprClientBuilder().Build();
        (ProjectionUpdateOrchestrator sut, IActorProxyFactory actorProxyFactory, _, IDomainServiceResolver resolver, _) = CreateSut(
            checkpointTracker,
            daprClient,
            httpClientFactory,
            new TestLogger<ProjectionUpdateOrchestrator>(entries));
        var registration = new DomainServiceRegistration("counter-service", "project", "test-tenant", "test-domain", "v1");
        _ = resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(registration);
        IAggregateActor aggregateActor = Substitute.For<IAggregateActor>();
        _ = aggregateActor.GetEventsAsync(0).Returns([CreateTestEnvelope(2)]);
        _ = actorProxyFactory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), "AggregateActor")
            .Returns(aggregateActor);

        await Should.NotThrowAsync(() => sut.UpdateProjectionAsync(TestIdentity));

        entries.ShouldContain(e => e.EventId.Id == 1142 && e.Message.Contains($"ReasonCode={ProjectionReasonCodes.ProjectTimeout}", StringComparison.Ordinal));
        await checkpointTracker.DidNotReceiveWithAnyArgs().SaveDeliveredSequenceAsync(default!, default, default);
    }

    [Fact]
    public async Task UpdateProjectionAsync_HostCancellationToken_PropagatesAsOperationCanceled() {
        IProjectionCheckpointTracker checkpointTracker = Substitute.For<IProjectionCheckpointTracker>();
        _ = checkpointTracker.ReadLastDeliveredSequenceAsync(TestIdentity, Arg.Any<CancellationToken>())
            .Returns(0);
        IHttpClientFactory httpClientFactory = CreateHttpClientFactory(new ThrowingResponseHandler(new TaskCanceledException("caller cancelled")));
        DaprClient daprClient = new DaprClientBuilder().Build();
        (ProjectionUpdateOrchestrator sut, IActorProxyFactory actorProxyFactory, _, IDomainServiceResolver resolver, _) = CreateSut(
            checkpointTracker,
            daprClient,
            httpClientFactory);
        var registration = new DomainServiceRegistration("counter-service", "project", "test-tenant", "test-domain", "v1");
        _ = resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(registration);
        IAggregateActor aggregateActor = Substitute.For<IAggregateActor>();
        _ = aggregateActor.GetEventsAsync(0).Returns([CreateTestEnvelope(2)]);
        _ = actorProxyFactory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), "AggregateActor")
            .Returns(aggregateActor);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Caller-token cancellation must propagate as OperationCanceledException —
        // it must NOT collapse into the project_timeout/unknown reason-code path.
        _ = await Should.ThrowAsync<OperationCanceledException>(() => sut.UpdateProjectionAsync(TestIdentity, cts.Token));
        await checkpointTracker.DidNotReceiveWithAnyArgs().SaveDeliveredSequenceAsync(default!, default, default);
    }

    [Fact]
    public async Task UpdateProjectionAsync_CheckpointReadThrows_FallsThroughWithNonRegressionSave() {
        // DW1 review patch P2: explicit regression evidence for the fail-open path. When
        // ReadLastDeliveredSequenceAsync throws, the orchestrator logs CheckpointReadFailed
        // (EventId 1118), continues with lastDeliveredSequence=0, drift comparison stays
        // false, and SaveDeliveredSequenceAsync is invoked with the highest event sequence.
        // The non-regression Math.Max in the tracker save protects against backward movement.
        IProjectionCheckpointTracker checkpointTracker = Substitute.For<IProjectionCheckpointTracker>();
        _ = checkpointTracker.ReadLastDeliveredSequenceAsync(TestIdentity, Arg.Any<CancellationToken>())
            .Returns<long>(_ => throw new InvalidOperationException("checkpoint read failed"));
        _ = checkpointTracker.SaveDeliveredSequenceAsync(TestIdentity, Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(true);
        var entries = new List<LogEntry>();
        IHttpClientFactory httpClientFactory = CreateHttpClientFactory("""{"projectionType":"counter-summary","state":{"value":1}}""");
        DaprClient daprClient = new DaprClientBuilder().Build();
        (ProjectionUpdateOrchestrator sut, IActorProxyFactory actorProxyFactory, _, IDomainServiceResolver resolver, _) = CreateSut(
            checkpointTracker,
            daprClient,
            httpClientFactory,
            new TestLogger<ProjectionUpdateOrchestrator>(entries));
        var registration = new DomainServiceRegistration("counter-service", "project", "test-tenant", "test-domain", "v1");
        _ = resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(registration);
        IAggregateActor aggregateActor = Substitute.For<IAggregateActor>();
        _ = aggregateActor.GetEventsAsync(0).Returns([CreateTestEnvelope(5)]);
        _ = actorProxyFactory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), "AggregateActor")
            .Returns(aggregateActor);
        _ = actorProxyFactory.CreateActorProxy<IProjectionWriteActor>(Arg.Any<ActorId>(), QueryRouter.ProjectionActorTypeName)
            .Returns(Substitute.For<IProjectionWriteActor>());

        await sut.UpdateProjectionAsync(TestIdentity);

        entries.ShouldContain(e => e.EventId.Id == 1118 && e.Message.Contains("Stage=ProjectionCheckpointReadFailed", StringComparison.Ordinal));
        // Drift signal must NOT fire on a read failure — the path is fail-open, not drift.
        entries.ShouldNotContain(e => e.EventId.Id == 1143);
        await checkpointTracker.Received(1).SaveDeliveredSequenceAsync(TestIdentity, 5, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateProjectionAsync_EmptyEventsWithStaleCheckpoint_LogsDriftAndDoesNotInvokeProject() {
        // DW1 review patch P1: drift detection must cover the canonical "stale checkpoint +
        // empty stream" case (state-store backup/restore mismatch) — without this branch the
        // orchestrator would log NoEventsFound and silently skip projection delivery forever.
        IProjectionCheckpointTracker checkpointTracker = Substitute.For<IProjectionCheckpointTracker>();
        _ = checkpointTracker.ReadLastDeliveredSequenceAsync(TestIdentity, Arg.Any<CancellationToken>())
            .Returns(7);
        var entries = new List<LogEntry>();
        var handler = new CountingProjectionResponseHandler();
        IHttpClientFactory httpClientFactory = CreateHttpClientFactory(handler);
        DaprClient daprClient = new DaprClientBuilder().Build();
        (ProjectionUpdateOrchestrator sut, IActorProxyFactory actorProxyFactory, _, IDomainServiceResolver resolver, _) = CreateSut(
            checkpointTracker,
            daprClient,
            httpClientFactory,
            new TestLogger<ProjectionUpdateOrchestrator>(entries));
        var registration = new DomainServiceRegistration("counter-service", "project", "test-tenant", "test-domain", "v1");
        _ = resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(registration);
        IAggregateActor aggregateActor = Substitute.For<IAggregateActor>();
        _ = aggregateActor.GetEventsAsync(0).Returns([]);
        _ = actorProxyFactory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), "AggregateActor")
            .Returns(aggregateActor);

        await sut.UpdateProjectionAsync(TestIdentity);

        handler.CallCount.ShouldBe(0);
        entries.ShouldContain(e =>
            e.EventId.Id == 1143
            && e.Message.Contains($"ReasonCode={ProjectionReasonCodes.CheckpointDrift}", StringComparison.Ordinal)
            && e.Message.Contains("LastDeliveredSequence=7", StringComparison.Ordinal)
            && e.Message.Contains("HighestEventSequence=0", StringComparison.Ordinal));
        await checkpointTracker.DidNotReceiveWithAnyArgs().SaveDeliveredSequenceAsync(default!, default, default);
    }

    [Fact]
    public async Task UpdateProjectionAsync_CheckpointGreaterThanEventSequence_LogsDriftAndDoesNotInvokeProject() {
        IProjectionCheckpointTracker checkpointTracker = Substitute.For<IProjectionCheckpointTracker>();
        _ = checkpointTracker.ReadLastDeliveredSequenceAsync(TestIdentity, Arg.Any<CancellationToken>())
            .Returns(7);
        var entries = new List<LogEntry>();
        var handler = new CountingProjectionResponseHandler();
        IHttpClientFactory httpClientFactory = CreateHttpClientFactory(handler);
        DaprClient daprClient = new DaprClientBuilder().Build();
        (ProjectionUpdateOrchestrator sut, IActorProxyFactory actorProxyFactory, _, IDomainServiceResolver resolver, _) = CreateSut(
            checkpointTracker,
            daprClient,
            httpClientFactory,
            new TestLogger<ProjectionUpdateOrchestrator>(entries));
        var registration = new DomainServiceRegistration("counter-service", "project", "test-tenant", "test-domain", "v1");
        _ = resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(registration);
        IAggregateActor aggregateActor = Substitute.For<IAggregateActor>();
        _ = aggregateActor.GetEventsAsync(0).Returns([CreateTestEnvelope(1), CreateTestEnvelope(2)]);
        _ = actorProxyFactory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), "AggregateActor")
            .Returns(aggregateActor);

        await sut.UpdateProjectionAsync(TestIdentity);

        handler.CallCount.ShouldBe(0);
        entries.ShouldContain(e =>
            e.EventId.Id == 1143
            && e.Message.Contains($"ReasonCode={ProjectionReasonCodes.CheckpointDrift}", StringComparison.Ordinal)
            && e.Message.Contains("LastDeliveredSequence=7", StringComparison.Ordinal)
            && e.Message.Contains("HighestEventSequence=2", StringComparison.Ordinal));
        await checkpointTracker.DidNotReceiveWithAnyArgs().SaveDeliveredSequenceAsync(default!, default, default);
    }

    [Fact]
    public async Task UpdateProjectionAsync_OverlappingSameAggregateDelivery_SerializesProjectInvocation() {
        IProjectionCheckpointTracker checkpointTracker = Substitute.For<IProjectionCheckpointTracker>();
        _ = checkpointTracker.ReadLastDeliveredSequenceAsync(TestIdentity, Arg.Any<CancellationToken>())
            .Returns(0);
        _ = checkpointTracker.SaveDeliveredSequenceAsync(TestIdentity, Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(true);
        var handler = new ConcurrencyProbeProjectionResponseHandler();
        IHttpClientFactory httpClientFactory = CreateHttpClientFactory(handler);
        DaprClient daprClient = new DaprClientBuilder().Build();
        (ProjectionUpdateOrchestrator sut, IActorProxyFactory actorProxyFactory, _, IDomainServiceResolver resolver, _) = CreateSut(
            checkpointTracker,
            daprClient,
            httpClientFactory);
        var registration = new DomainServiceRegistration("counter-service", "project", "test-tenant", "test-domain", "v1");
        _ = resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(registration);
        IAggregateActor aggregateActor = Substitute.For<IAggregateActor>();
        _ = aggregateActor.GetEventsAsync(0).Returns([CreateTestEnvelope(1), CreateTestEnvelope(2)]);
        _ = actorProxyFactory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), "AggregateActor")
            .Returns(aggregateActor);
        _ = actorProxyFactory.CreateActorProxy<IProjectionWriteActor>(Arg.Any<ActorId>(), QueryRouter.ProjectionActorTypeName)
            .Returns(Substitute.For<IProjectionWriteActor>());

        Task first = sut.UpdateProjectionAsync(TestIdentity);
        await WaitForSignalAsync(handler.FirstEntered);
        Task second = sut.UpdateProjectionAsync(TestIdentity);

        // DW1 review patch P10: replace Task.Delay(150) with bounded polling. The polling
        // wait fails fast if serialization breaks (CallCount becoming 2 before ReleaseFirst
        // would mean the second task entered the handler concurrently) and tolerates slow
        // CI without changing the assertion semantics.
        for (int i = 0; i < 100 && handler.CallCount == 1; i++) {
            await Task.Delay(20);
        }

        handler.CallCount.ShouldBe(1);
        handler.ReleaseFirst();

        await Task.WhenAll(first, second);

        handler.CallCount.ShouldBe(2);
        handler.MaxConcurrent.ShouldBe(1);
    }

    [Fact]
    public async Task UpdateProjectionAsync_ProjectionActorWriteFails_DoesNotSaveCheckpoint() {
        // Arrange
        IProjectionCheckpointTracker checkpointTracker = Substitute.For<IProjectionCheckpointTracker>();
        _ = checkpointTracker.ReadLastDeliveredSequenceAsync(TestIdentity, Arg.Any<CancellationToken>())
            .Returns(0);
        IProjectionWriteActor writeActor = Substitute.For<IProjectionWriteActor>();
        _ = writeActor.UpdateProjectionAsync(Arg.Any<ProjectionState>())
            .Returns(_ => throw new InvalidOperationException("projection actor write failed"));
        IHttpClientFactory httpClientFactory = CreateHttpClientFactory("""{"projectionType":"counter-summary","state":{"value":1}}""");
        DaprClient daprClient = new DaprClientBuilder().Build();
        (ProjectionUpdateOrchestrator sut, IActorProxyFactory actorProxyFactory, _, IDomainServiceResolver resolver, _) = CreateSut(checkpointTracker, daprClient, httpClientFactory);
        var registration = new DomainServiceRegistration("counter-service", "project", "test-tenant", "test-domain", "v1");
        _ = resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(registration);

        IAggregateActor aggregateActor = Substitute.For<IAggregateActor>();
        _ = aggregateActor.GetEventsAsync(0).Returns(new[] { CreateTestEnvelope(4) });
        _ = actorProxyFactory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), "AggregateActor")
            .Returns(aggregateActor);
        _ = actorProxyFactory.CreateActorProxy<IProjectionWriteActor>(Arg.Any<ActorId>(), QueryRouter.ProjectionActorTypeName)
            .Returns(writeActor);

        // Act & Assert
        await Should.NotThrowAsync(() => sut.UpdateProjectionAsync(TestIdentity));
        await checkpointTracker.DidNotReceiveWithAnyArgs().SaveDeliveredSequenceAsync(default!, default, default);
    }

    [Fact]
    public async Task UpdateProjectionAsync_CheckpointSaveFailsAfterProjectionWrite_DoesNotThrow() {
        // Arrange
        IProjectionCheckpointTracker checkpointTracker = Substitute.For<IProjectionCheckpointTracker>();
        _ = checkpointTracker.ReadLastDeliveredSequenceAsync(TestIdentity, Arg.Any<CancellationToken>())
            .Returns(0);
        _ = checkpointTracker.SaveDeliveredSequenceAsync(TestIdentity, Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns<bool>(_ => throw new InvalidOperationException("checkpoint save failed"));
        IProjectionWriteActor writeActor = Substitute.For<IProjectionWriteActor>();
        IHttpClientFactory httpClientFactory = CreateHttpClientFactory("""{"projectionType":"counter-summary","state":{"value":1}}""");
        DaprClient daprClient = new DaprClientBuilder().Build();
        (ProjectionUpdateOrchestrator sut, IActorProxyFactory actorProxyFactory, _, IDomainServiceResolver resolver, _) = CreateSut(checkpointTracker, daprClient, httpClientFactory);
        var registration = new DomainServiceRegistration("counter-service", "project", "test-tenant", "test-domain", "v1");
        _ = resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(registration);

        IAggregateActor aggregateActor = Substitute.For<IAggregateActor>();
        _ = aggregateActor.GetEventsAsync(0).Returns(new[] { CreateTestEnvelope(3) });
        _ = actorProxyFactory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), "AggregateActor")
            .Returns(aggregateActor);
        _ = actorProxyFactory.CreateActorProxy<IProjectionWriteActor>(Arg.Any<ActorId>(), QueryRouter.ProjectionActorTypeName)
            .Returns(writeActor);

        // Act & Assert
        await Should.NotThrowAsync(() => sut.UpdateProjectionAsync(TestIdentity));
        await writeActor.Received(1).UpdateProjectionAsync(Arg.Any<ProjectionState>());
        await checkpointTracker.Received(1).SaveDeliveredSequenceAsync(TestIdentity, 3, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateProjectionAsync_CheckpointSaveExhausted_LogsOperatorSignal() {
        IProjectionCheckpointTracker checkpointTracker = Substitute.For<IProjectionCheckpointTracker>();
        _ = checkpointTracker.ReadLastDeliveredSequenceAsync(TestIdentity, Arg.Any<CancellationToken>())
            .Returns(0);
        _ = checkpointTracker.SaveDeliveredSequenceAsync(TestIdentity, Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(false);
        IProjectionWriteActor writeActor = Substitute.For<IProjectionWriteActor>();
        IHttpClientFactory httpClientFactory = CreateHttpClientFactory("""{"projectionType":"counter-summary","state":{"value":1}}""");
        DaprClient daprClient = new DaprClientBuilder().Build();
        var entries = new List<LogEntry>();
        (ProjectionUpdateOrchestrator sut, IActorProxyFactory actorProxyFactory, _, IDomainServiceResolver resolver, _) = CreateSut(
            checkpointTracker,
            daprClient,
            httpClientFactory,
            new TestLogger<ProjectionUpdateOrchestrator>(entries));
        var registration = new DomainServiceRegistration("counter-service", "project", "test-tenant", "test-domain", "v1");
        _ = resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(registration);
        IAggregateActor aggregateActor = Substitute.For<IAggregateActor>();
        _ = aggregateActor.GetEventsAsync(0).Returns([CreateTestEnvelope(3)]);
        _ = actorProxyFactory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), "AggregateActor")
            .Returns(aggregateActor);
        _ = actorProxyFactory.CreateActorProxy<IProjectionWriteActor>(Arg.Any<ActorId>(), QueryRouter.ProjectionActorTypeName)
            .Returns(writeActor);

        await sut.UpdateProjectionAsync(TestIdentity);

        entries.ShouldContain(e =>
            e.EventId.Id == 1120
            && e.Message.Contains("Stage=ProjectionCheckpointSaveExhausted", StringComparison.Ordinal)
            && e.Message.Contains("AttemptedSequence=3", StringComparison.Ordinal));
    }

    // --- Test 9: AC 1 - EventPublisher triggers orchestrator ---

    [Fact]
    public async Task EventPublisher_AfterSuccessfulPublish_TriggersProjectionOrchestrator() {
        // Arrange
        DaprClient daprClient = Substitute.For<DaprClient>();
        IOptions<EventPublisherOptions> options = Options.Create(new EventPublisherOptions());
        ILogger<EventPublisher> logger = Substitute.For<ILogger<EventPublisher>>();
        _ = logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        IProjectionUpdateOrchestrator orchestrator = Substitute.For<IProjectionUpdateOrchestrator>();
        TaskCompletionSource<bool> signal = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = orchestrator
            .UpdateProjectionAsync(Arg.Any<AggregateIdentity>(), Arg.Any<CancellationToken>())
            .Returns(ci => {
                _ = signal.TrySetResult(true);
                return Task.CompletedTask;
            });
        var publisher = new EventPublisher(daprClient, options, logger, new NoOpEventPayloadProtectionService(), orchestrator);

        var identity = new AggregateIdentity("test-tenant", "test-domain", "agg-001");
        var events = new List<EventEnvelope> { CreateTestEnvelope() };

        // Act
        _ = await publisher.PublishEventsAsync(identity, events, "corr-001");

        // Wait deterministically for fire-and-forget callback.
        await WaitForSignalAsync(signal.Task);

        // Assert - Orchestrator was called with the correct identity
        await orchestrator.Received(1).UpdateProjectionAsync(
            Arg.Is<AggregateIdentity>(id =>
                id.TenantId == "test-tenant"
                && id.Domain == "test-domain"
                && id.AggregateId == "agg-001"),
            Arg.Any<CancellationToken>());
    }

    // --- Test 10: AC 2 - ProjectionEventDto mapping verified via construction ---

    [Fact]
    public void ProjectionEventDto_MapsProjectionFields_FromEventEnvelope() {
        // This test verifies the mapping logic at the DTO level.
        // The actual mapping in the orchestrator follows this exact pattern.
        DateTimeOffset testTimestamp = new(2026, 3, 20, 12, 0, 0, TimeSpan.Zero);
        EventEnvelope envelope = new(
            MessageId: "msg-1",
            AggregateId: "agg-001",
            AggregateType: "test-aggregate",
            TenantId: "test-tenant",
            Domain: "test-domain",
            SequenceNumber: 5,
            GlobalPosition: 100,
            Timestamp: testTimestamp,
            CorrelationId: "corr-map-test",
            CausationId: "cause-secret",
            UserId: "user-secret",
            DomainServiceVersion: "2.0.0",
            EventTypeName: "CounterIncremented",
            MetadataVersion: 2,
            SerializationFormat: "json",
            Payload: [10, 20, 30],
            Extensions: new Dictionary<string, string> { ["key"] = "value" });

        // Perform the same mapping as ProjectionUpdateOrchestrator
        ProjectionEventDto mapped = new(
            envelope.EventTypeName,
            envelope.Payload,
            envelope.SerializationFormat,
            envelope.SequenceNumber,
            envelope.Timestamp,
            envelope.CorrelationId,
            envelope.MessageId,
            envelope.UserId);

        // Assert
        mapped.EventTypeName.ShouldBe("CounterIncremented");
        mapped.Payload.ShouldBe(new byte[] { 10, 20, 30 });
        mapped.SerializationFormat.ShouldBe("json");
        mapped.SequenceNumber.ShouldBe(5);
        mapped.Timestamp.ShouldBe(testTimestamp);
        mapped.CorrelationId.ShouldBe("corr-map-test");
        mapped.MessageId.ShouldBe("msg-1");
        mapped.UserId.ShouldBe("user-secret");
    }

    // --- Test 11: AC 2 - QueryActorIdHelper derives correct projection actor ID ---

    [Fact]
    public void DeriveProjectionActorId_FollowsTier1Pattern() {
        // Verify the projection actor ID derivation follows the expected pattern
        string actorId = QueryActorIdHelper.DeriveActorId("counter-summary", "test-tenant", "agg-001", []);

        actorId.ShouldBe("counter-summary:test-tenant:agg-001");
    }

    // --- Test 12: AC 1 - EventPublisher does not trigger on failed publish ---

    [Fact]
    public async Task EventPublisher_FailedPublish_DoesNotTriggerProjectionOrchestrator() {
        // Arrange
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.PublishEventAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<EventEnvelope>(),
            Arg.Any<Dictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("pub/sub failure"));
        IOptions<EventPublisherOptions> options = Options.Create(new EventPublisherOptions());
        ILogger<EventPublisher> logger = Substitute.For<ILogger<EventPublisher>>();
        IProjectionUpdateOrchestrator orchestrator = Substitute.For<IProjectionUpdateOrchestrator>();
        TaskCompletionSource<bool> signal = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = orchestrator
            .UpdateProjectionAsync(Arg.Any<AggregateIdentity>(), Arg.Any<CancellationToken>())
            .Returns(ci => {
                _ = signal.TrySetResult(true);
                return Task.CompletedTask;
            });
        var publisher = new EventPublisher(daprClient, options, logger, new NoOpEventPayloadProtectionService(), orchestrator);

        var identity = new AggregateIdentity("test-tenant", "test-domain", "agg-001");
        var events = new List<EventEnvelope> { CreateTestEnvelope() };

        // Act
        _ = await publisher.PublishEventsAsync(identity, events, "corr-001");

        // Ensure no background callback was observed.
        await WaitForNoSignalAsync(signal.Task);

        // Assert - Orchestrator was NOT called because publication failed
        await orchestrator.DidNotReceiveWithAnyArgs().UpdateProjectionAsync(default!, default);
    }

    [Fact]
    public async Task UpdateProjectionAsync_HappyPath_SendsExpectedProjectionRequestBody() {
        // Pins the orchestrator's outgoing HTTP body (tenant/domain/aggregateId/event count
        // and the exact SequenceNumber set) against its EventEnvelope[] input. Without this,
        // existing tests either ignored the request body or re-implemented the mapping inside
        // the test (which only verifies the test's own output).
        IProjectionCheckpointTracker checkpointTracker = Substitute.For<IProjectionCheckpointTracker>();
        _ = checkpointTracker.SaveDeliveredSequenceAsync(Arg.Any<AggregateIdentity>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(true);
        var capture = new RequestCapturingHandler("""{"projectionType":"counter-summary","state":{"value":1}}""");
        IHttpClientFactory httpClientFactory = CreateHttpClientFactory(capture);
        DaprClient daprClient = new DaprClientBuilder().Build();
        (ProjectionUpdateOrchestrator sut, IActorProxyFactory actorProxyFactory, _, IDomainServiceResolver resolver, _) = CreateSut(checkpointTracker, daprClient, httpClientFactory);
        var registration = new DomainServiceRegistration("counter-service", "project", "test-tenant", "test-domain", "v1");
        _ = resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(registration);

        IProjectionWriteActor writeActor = Substitute.For<IProjectionWriteActor>();
        IAggregateActor aggregateActor = Substitute.For<IAggregateActor>();
        EventEnvelope[] events = [CreateTestEnvelope(2), CreateTestEnvelope(7), CreateTestEnvelope(11)];
        _ = aggregateActor.GetEventsAsync(0).Returns(events);
        _ = actorProxyFactory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), "AggregateActor")
            .Returns(aggregateActor);
        _ = actorProxyFactory.CreateActorProxy<IProjectionWriteActor>(Arg.Any<ActorId>(), QueryRouter.ProjectionActorTypeName)
            .Returns(writeActor);

        // Act
        await sut.UpdateProjectionAsync(TestIdentity);

        // Assert
        capture.CapturedBody.ShouldNotBeNull();
        ProjectionRequest? body = JsonSerializer.Deserialize<ProjectionRequest>(
            capture.CapturedBody!,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        body.ShouldNotBeNull();
        body!.TenantId.ShouldBe("test-tenant");
        body.Domain.ShouldBe("test-domain");
        body.AggregateId.ShouldBe("agg-001");
        body.Events.Length.ShouldBe(events.Length);
        body.Events.Select(e => e.SequenceNumber).ShouldBe(new long[] { 2, 7, 11 });
        body.Events.Select(e => e.MessageId).ShouldBe(new string?[] { "msg-2", "msg-7", "msg-11" });
        body.Events.Select(e => e.UserId).ShouldBe(new string?[] { "user-1", "user-1", "user-1" });
    }

    [Fact]
    public async Task UpdateProjectionAsync_OperationCanceledException_PropagatesThroughOuterCatch() {
        // The outer catch must filter OCE so cooperative cancellation unwinds cleanly.
        // The inner checkpoint-read and checkpoint-save catches already use the same
        // `when (ex is not OperationCanceledException)` filter; the outer catch must
        // remain consistent with them.
        (ProjectionUpdateOrchestrator sut, _, _, IDomainServiceResolver resolver, _) = CreateSut();
        _ = resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<DomainServiceRegistration?>(_ => throw new OperationCanceledException("test cancellation"));

        _ = await Should.ThrowAsync<OperationCanceledException>(() => sut.UpdateProjectionAsync(TestIdentity));
    }

    private static IHttpClientFactory CreateHttpClientFactory(string json) {
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        var client = new HttpClient(new JsonResponseHandler(json));
        _ = httpClientFactory.CreateClient(Arg.Any<string>()).Returns(client);
        _ = httpClientFactory.CreateClient().Returns(client);
        return httpClientFactory;
    }

    private static IHttpClientFactory CreateHttpClientFactory(HttpMessageHandler handler) {
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        var client = new HttpClient(handler);
        _ = httpClientFactory.CreateClient(Arg.Any<string>()).Returns(client);
        _ = httpClientFactory.CreateClient().Returns(client);
        return httpClientFactory;
    }

    private static ProjectionRebuildCheckpointScope CreateRebuildScope()
        => new(TestIdentity.TenantId, TestIdentity.Domain, TestIdentity.Domain, null, null);

    private static ProjectionRebuildCheckpoint CreateRebuildCheckpoint(
        long lastAppliedSequence,
        ProjectionRebuildStatus status,
        string operationId,
        long? toPosition)
        => new(
            TestIdentity.TenantId,
            TestIdentity.Domain,
            TestIdentity.Domain,
            null,
            operationId,
            lastAppliedSequence,
            status,
            DateTimeOffset.UtcNow,
            null,
            toPosition);

    private static async IAsyncEnumerable<AggregateIdentity> EnumerateTracked(params AggregateIdentity[] identities) {
        foreach (AggregateIdentity identity in identities) {
            yield return identity;
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    private static async IAsyncEnumerable<AggregateIdentity> ThrowCanceledBeforeFirstIdentity() {
        await Task.CompletedTask.ConfigureAwait(false);
        throw new OperationCanceledException("test cancellation");
        #pragma warning disable CS0162
        yield return TestIdentity;
        #pragma warning restore CS0162
    }

    private sealed class JsonResponseHandler(string json) : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
    }

    private sealed class RequestCapturingHandler(string responseJson) : HttpMessageHandler {
        public string? CapturedBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            CapturedBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK) {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class CountingProjectionResponseHandler : HttpMessageHandler {
        public int CallCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            CallCount++;
            string json = request.Content is null
                ? "{}"
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            ProjectionRequest? projectionRequest = JsonSerializer.Deserialize<ProjectionRequest>(json, JsonSerializerOptions.Web);
            int eventCount = projectionRequest?.Events.Length ?? 0;
            string responseJson = JsonSerializer.Serialize(
                new {
                    projectionType = "counter-summary",
                    state = new { count = eventCount },
                },
                JsonSerializerOptions.Web);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK) {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class FixedResponseHandler(HttpStatusCode statusCode, HttpContent content) : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode) {
                Content = content,
            });
    }

    private sealed class ThrowingResponseHandler(Exception exception) : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(exception);
    }

    private sealed class ConcurrencyProbeProjectionResponseHandler : HttpMessageHandler {
        private readonly TaskCompletionSource _firstEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseFirst = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _active;

        public Task FirstEntered => _firstEntered.Task;

        public int CallCount { get; private set; }

        public int MaxConcurrent { get; private set; }

        public void ReleaseFirst() => _releaseFirst.SetResult();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            int active = Interlocked.Increment(ref _active);
            MaxConcurrent = Math.Max(MaxConcurrent, active);
            try {
                CallCount++;
                if (CallCount == 1) {
                    _firstEntered.SetResult();
                    await _releaseFirst.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                }

                return new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new StringContent("""{"projectionType":"counter-summary","state":{"value":1}}""", Encoding.UTF8, "application/json"),
                };
            }
            finally {
                _ = Interlocked.Decrement(ref _active);
            }
        }
    }
}
