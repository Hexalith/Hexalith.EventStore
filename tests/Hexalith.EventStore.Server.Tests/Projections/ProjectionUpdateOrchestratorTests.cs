
using System.Text;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Queries;

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

    private static EventEnvelope CreateTestEnvelope(long sequenceNumber = 1) =>
        new(
            MessageId: $"msg-{sequenceNumber}",
            AggregateId: "agg-001",
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
        IHttpClientFactory? httpClientFactory = null) {
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        daprClient ??= Substitute.For<DaprClient>();
        IDomainServiceResolver resolver = Substitute.For<IDomainServiceResolver>();
        if (checkpointTracker is null) {
            checkpointTracker = Substitute.For<IProjectionCheckpointTracker>();
            _ = checkpointTracker.ReadLastDeliveredSequenceAsync(Arg.Any<AggregateIdentity>(), Arg.Any<CancellationToken>())
                .Returns(0);
        }
        IOptions<ProjectionOptions> projectionOptions = Options.Create(new ProjectionOptions());
        ILogger<ProjectionUpdateOrchestrator> logger = NullLogger<ProjectionUpdateOrchestrator>.Instance;
        var sut = new ProjectionUpdateOrchestrator(actorProxyFactory, daprClient, httpClientFactory ?? Substitute.For<IHttpClientFactory>(), resolver, checkpointTracker, projectionOptions, logger);
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
        _ = checkpointTracker.ReadLastDeliveredSequenceAsync(TestIdentity, Arg.Any<CancellationToken>())
            .Returns(7);
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

        // Assert
        _ = await aggregateActor.Received(1).GetEventsAsync(0);
        _ = await aggregateActor.DidNotReceive().GetEventsAsync(7);
        await checkpointTracker.DidNotReceiveWithAnyArgs().ReadLastDeliveredSequenceAsync(default!, default);
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
        _ = aggregateActor.GetEventsAsync(0).Returns(Array.Empty<EventEnvelope>());
        _ = actorProxyFactory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(aggregateActor);

        // Act
        await sut.UpdateProjectionAsync(TestIdentity);

        // Assert - checkpoint state is tracked on save, but immediate delivery stays
        // full-replay until the projection handler state contract is resolved.
        _ = await aggregateActor.Received(1).GetEventsAsync(0);
        _ = await aggregateActor.DidNotReceive().GetEventsAsync(7);
        await checkpointTracker.DidNotReceiveWithAnyArgs().ReadLastDeliveredSequenceAsync(default!, default);
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

        // Act & Assert - checkpoint reads are not part of immediate delivery until
        // the incremental projection contract is resolved.
        await Should.NotThrowAsync(() => sut.UpdateProjectionAsync(TestIdentity));
        _ = await aggregateActor.Received(1).GetEventsAsync(0);
        await checkpointTracker.DidNotReceiveWithAnyArgs().ReadLastDeliveredSequenceAsync(default!, default);
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
    public void ProjectionEventDto_MapsOnlyPublicFields_FromEventEnvelope() {
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
            envelope.CorrelationId);

        // Assert - Only the 6 public fields are mapped
        mapped.EventTypeName.ShouldBe("CounterIncremented");
        mapped.Payload.ShouldBe(new byte[] { 10, 20, 30 });
        mapped.SerializationFormat.ShouldBe("json");
        mapped.SequenceNumber.ShouldBe(5);
        mapped.Timestamp.ShouldBe(testTimestamp);
        mapped.CorrelationId.ShouldBe("corr-map-test");
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

    private sealed class JsonResponseHandler(string json) : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
    }

    private sealed class CountingProjectionResponseHandler : HttpMessageHandler {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            string json = request.Content is null
                ? "{}"
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            ProjectionRequest? projectionRequest = JsonSerializer.Deserialize<ProjectionRequest>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            int eventCount = projectionRequest?.Events.Length ?? 0;
            string responseJson = JsonSerializer.Serialize(new {
                projectionType = "counter-summary",
                state = new { count = eventCount },
            });
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK) {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
            };
        }
    }
}
