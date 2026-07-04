
using Dapr.Actors;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Tests.TestUtilities;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

using EventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;

namespace Hexalith.EventStore.Server.Tests.Actors;
/// <summary>
/// Story 3.11 Task 8: AggregateActor state machine integration tests.
/// Verifies checkpointed stage transitions, crash recovery, advisory status writes,
/// and pipeline state cleanup during command processing.
/// </summary>
public class StateMachineIntegrationTests {
    private const string PendingCommandCountKey = "pending_command_count";

    private static CommandEnvelope CreateTestEnvelope(
        string tenantId = "test-tenant",
        string? correlationId = null,
        string? causationId = null) => new(
        MessageId: Guid.NewGuid().ToString(),
        TenantId: tenantId,
        Domain: "test-domain",
        AggregateId: "agg-001",
        CommandType: "CreateOrder",
        Payload: [1, 2, 3],
        CorrelationId: correlationId ?? "corr-sm-test",
        CausationId: causationId,
        UserId: "system",
        Extensions: null);

    private static (AggregateActor Actor, IActorStateManager StateManager, IDomainServiceInvoker Invoker, ISnapshotManager SnapshotManager, ICommandStatusStore StatusStore, IEventPublisher EventPublisher) CreateActor() {
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        ILogger<AggregateActor> logger = Substitute.For<ILogger<AggregateActor>>();
        IDomainServiceInvoker invoker = Substitute.For<IDomainServiceInvoker>();
        ISnapshotManager snapshotManager = Substitute.For<ISnapshotManager>();
        ICommandStatusStore statusStore = Substitute.For<ICommandStatusStore>();
        IEventPublisher eventPublisher = Substitute.For<IEventPublisher>();
        var host = ActorHost.CreateForTest<AggregateActor>(
            new ActorTestOptions { ActorId = new ActorId("test-tenant:test-domain:agg-001") });
        var actor = new AggregateActor(host, logger, invoker, snapshotManager, new NoOpEventPayloadProtectionService(), statusStore, eventPublisher, Options.Create(new EventDrainOptions()), Options.Create(new BackpressureOptions()), Substitute.For<IDeadLetterPublisher>());

        ActorStateManagerTestHelper.SetStateManager(actor, stateManager);

        // Default: no idempotency record
        _ = stateManager.TryGetStateAsync<IdempotencyRecord>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(false, default!));

        // Default: no metadata (new aggregate)
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(false, default!));

        // Default: no pipeline state
        _ = stateManager.TryGetStateAsync<PipelineState>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<PipelineState>(false, default!));

        // Default: domain returns NoOp
        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>())
            .Returns(DomainResult.NoOp());

        // Default: event publisher succeeds
        _ = eventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(callInfo => new EventPublishResult(true, callInfo.ArgAt<IReadOnlyList<EventEnvelope>>(1).Count, null));

        return (actor, stateManager, invoker, snapshotManager, statusStore, eventPublisher);
    }

    // --- Task 8.1: Happy path transitions ---

    [Fact]
    public async Task ProcessCommand_Success_CheckpointsProcessingThenEventsStoredThenCleanup() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, IDomainServiceInvoker invoker, _, _, _) = CreateActor();
        var successResult = DomainResult.Success(new Hexalith.EventStore.Contracts.Events.IEventPayload[] { new TestEvent() });
        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(successResult);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert -- accepted
        result.Accepted.ShouldBeTrue();
        result.EventCount.ShouldBe(1);

        // Processing checkpoint was staged
        await stateManager.Received().SetStateAsync(
            Arg.Is<string>(s => s.Contains(":pipeline:") && s.Contains(envelope.CorrelationId)),
            Arg.Is<PipelineState>(p => p.CurrentStage == CommandStatus.Processing),
            Arg.Any<CancellationToken>());

        // EventsStored checkpoint was staged
        await stateManager.Received().SetStateAsync(
            Arg.Is<string>(s => s.Contains(":pipeline:") && s.Contains(envelope.CorrelationId)),
            Arg.Is<PipelineState>(p => p.CurrentStage == CommandStatus.EventsStored && p.EventCount == 1),
            Arg.Any<CancellationToken>());

        // Pipeline key removed at terminal (cleanup)
        _ = await stateManager.Received().TryRemoveStateAsync(
            Arg.Is<string>(s => s.Contains(":pipeline:") && s.Contains(envelope.CorrelationId)),
            Arg.Any<CancellationToken>());

        // 4 SaveStateAsync calls: Processing (with pending count), EventsStored+events, terminal, pending-count decrement
        await stateManager.Received(4).SaveStateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommand_Success_WithResultPayload_PreservesTerminalPayloadButScrubsCheckpoints() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, IDomainServiceInvoker invoker, _, _, _) = CreateActor();
        const string resultPayload = "{\"result\":{\"applied\":[\"Updated person details\"]},\"party\":{\"id\":\"agg-001\"}}";
        var successResult = new PayloadDomainResult([new TestEvent()], resultPayload);
        var checkpointedStates = new List<PipelineState>();
        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(successResult);
        _ = stateManager.SetStateAsync(
            Arg.Any<string>(),
            Arg.Any<PipelineState>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(callInfo => checkpointedStates.Add(callInfo.ArgAt<PipelineState>(1)));
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeTrue();
        result.EventCount.ShouldBe(1);
        result.ResultPayload.ShouldBe(resultPayload);
        checkpointedStates.ShouldContain(state => state.CurrentStage == CommandStatus.Processing);
        checkpointedStates.ShouldContain(state => state.CurrentStage == CommandStatus.EventsStored);
        checkpointedStates.ShouldContain(state => state.CurrentStage == CommandStatus.EventsPublished);
        checkpointedStates.ShouldAllBe(state => state.ResultPayload == null);
    }

    // --- Task 8.2: Rejection path ---

    [Fact]
    public async Task ProcessCommand_Rejection_TransitionsProcessingEventsStoredCompleted() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, IDomainServiceInvoker invoker, _, _, _) = CreateActor();
        var rejectionResult = DomainResult.Rejection(new Hexalith.EventStore.Contracts.Events.IRejectionEvent[] { new TestRejectionEvent() });
        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(rejectionResult);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeFalse();
        _ = result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldContain("Domain rejection");

        // EventsStored checkpoint includes rejection event type
        await stateManager.Received().SetStateAsync(
            Arg.Is<string>(s => s.Contains(":pipeline:")),
            Arg.Is<PipelineState>(p => p.CurrentStage == CommandStatus.EventsStored && p.RejectionEventType != null),
            Arg.Any<CancellationToken>());

        // Pipeline cleaned up at terminal
        _ = await stateManager.Received().TryRemoveStateAsync(
            Arg.Is<string>(s => s.Contains(":pipeline:")),
            Arg.Any<CancellationToken>());
    }

    // --- Task 8.3: No-op path ---

    [Fact]
    public async Task ProcessCommand_NoOp_TransitionsProcessingDirectlyToCompleted() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, IDomainServiceInvoker invoker, _, _, _) = CreateActor();
        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(DomainResult.NoOp());
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeTrue();
        result.EventCount.ShouldBe(0);

        // Processing checkpoint was staged
        await stateManager.Received().SetStateAsync(
            Arg.Is<string>(s => s.Contains(":pipeline:")),
            Arg.Is<PipelineState>(p => p.CurrentStage == CommandStatus.Processing),
            Arg.Any<CancellationToken>());

        // NO EventsStored checkpoint (skipped for no-op)
        await stateManager.DidNotReceive().SetStateAsync(
            Arg.Any<string>(),
            Arg.Is<PipelineState>(p => p.CurrentStage == CommandStatus.EventsStored),
            Arg.Any<CancellationToken>());

        // Pipeline cleaned up
        _ = await stateManager.Received().TryRemoveStateAsync(
            Arg.Is<string>(s => s.Contains(":pipeline:")),
            Arg.Any<CancellationToken>());

        // 3 SaveStateAsync calls: Processing checkpoint + terminal + pending-count decrement
        await stateManager.Received(3).SaveStateAsync(Arg.Any<CancellationToken>());
    }

    // --- Task 8.4: Crash recovery from EventsStored (NFR25) ---

    [Fact]
    public async Task ProcessCommand_CrashAtEventsStored_Resume_DoesNotRePersistEvents() {
        // Arrange -- simulate existing EventsStored pipeline state (crash scenario)
        (AggregateActor actor, IActorStateManager stateManager, IDomainServiceInvoker invoker, _, _, IEventPublisher eventPublisher) = CreateActor();

        var existingPipeline = new PipelineState(
            "corr-sm-test", CommandStatus.EventsStored, "CreateOrder",
            DateTimeOffset.UtcNow.AddSeconds(-5), EventCount: 2, RejectionEventType: null);

        _ = stateManager.TryGetStateAsync<PipelineState>(
            Arg.Is<string>(s => s.Contains(":pipeline:corr-sm-test")),
            Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<PipelineState>(true, existingPipeline));

        var metadata = new AggregateMetadata(2, DateTimeOffset.UtcNow, null);
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(
            "test-tenant:test-domain:agg-001:metadata",
            Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, metadata));

        _ = stateManager.TryGetStateAsync<EventEnvelope>(
            "test-tenant:test-domain:agg-001:events:1",
            Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(
                true,
                new EventEnvelope(
                    "msg-1",
                    "agg-001",
                    "test-aggregate",
                    "test-tenant",
                    "test-domain",
                    1,
                    0,
                    DateTimeOffset.UtcNow,
                    "corr-sm-test",
                    "cause-1",
                    "system",
                    "1.0.0",
                    "TestEvent",
                    1,
                    "json",
                    [1],
                    null)));

        _ = stateManager.TryGetStateAsync<EventEnvelope>(
            "test-tenant:test-domain:agg-001:events:2",
            Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(
                true,
                new EventEnvelope(
                    "msg-1",
                    "agg-001",
                    "test-aggregate",
                    "test-tenant",
                    "test-domain",
                    2,
                    0,
                    DateTimeOffset.UtcNow,
                    "corr-sm-test",
                    "cause-2",
                    "system",
                    "1.0.0",
                    "TestEvent",
                    1,
                    "json",
                    [2],
                    null)));

        _ = stateManager.TryGetStateAsync<int>(PendingCommandCountKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<int>(true, 1));

        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert -- accepted (resume from EventsStored)
        result.Accepted.ShouldBeTrue();
        result.EventCount.ShouldBe(2);

        // Domain service was NOT invoked (events already stored)
        _ = await invoker.DidNotReceive().InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>());

        // Event publication is resumed from persisted events
        _ = await eventPublisher.Received(1).PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Is<IReadOnlyList<EventEnvelope>>(events => events.Count == 2),
            "corr-sm-test",
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>());

        // No event writes (events already persisted)
        await stateManager.DidNotReceive().SetStateAsync(
            Arg.Is<string>(s => s.Contains(":events:")),
            Arg.Any<EventEnvelope>(),
            Arg.Any<CancellationToken>());

        // Pipeline cleaned up
        _ = await stateManager.Received().TryRemoveStateAsync(
            Arg.Is<string>(s => s.Contains(":pipeline:")),
            Arg.Any<CancellationToken>());

        await stateManager.Received().SetStateAsync(PendingCommandCountKey, 0, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommand_CrashAtEventsStored_Resume_DropsLegacyResultPayload() {
        // Arrange -- legacy payloads may exist in old checkpoints, but resume must not propagate them.
        (AggregateActor actor, IActorStateManager stateManager, IDomainServiceInvoker invoker, _, _, IEventPublisher eventPublisher) = CreateActor();

        const string resultPayload = "{\"result\":{\"applied\":[\"Updated person details\"]},\"party\":{\"id\":\"agg-001\"}}";
        var existingPipeline = new PipelineState(
            "corr-sm-test", CommandStatus.EventsStored, "CreateOrder",
            DateTimeOffset.UtcNow.AddSeconds(-5), EventCount: 2, RejectionEventType: null, ResultPayload: resultPayload);

        _ = stateManager.TryGetStateAsync<PipelineState>(
            Arg.Is<string>(s => s.Contains(":pipeline:corr-sm-test")),
            Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<PipelineState>(true, existingPipeline));

        _ = stateManager.TryGetStateAsync<int>(PendingCommandCountKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<int>(true, 1));

        var metadata = new AggregateMetadata(2, DateTimeOffset.UtcNow, null);
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(
            "test-tenant:test-domain:agg-001:metadata",
            Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, metadata));

        _ = stateManager.TryGetStateAsync<EventEnvelope>(
            "test-tenant:test-domain:agg-001:events:1",
            Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(
                true,
                new EventEnvelope(
                    "msg-1",
                    "agg-001",
                    "test-aggregate",
                    "test-tenant",
                    "test-domain",
                    1,
                    0,
                    DateTimeOffset.UtcNow,
                    "corr-sm-test",
                    "cause-1",
                    "system",
                    "1.0.0",
                    "TestEvent",
                    1,
                    "json",
                    [1],
                    null)));

        _ = stateManager.TryGetStateAsync<EventEnvelope>(
            "test-tenant:test-domain:agg-001:events:2",
            Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(
                true,
                new EventEnvelope(
                    "msg-1",
                    "agg-001",
                    "test-aggregate",
                    "test-tenant",
                    "test-domain",
                    2,
                    0,
                    DateTimeOffset.UtcNow,
                    "corr-sm-test",
                    "cause-2",
                    "system",
                    "1.0.0",
                    "TestEvent",
                    1,
                    "json",
                    [2],
                    null)));

        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeTrue();
        result.EventCount.ShouldBe(2);
        result.ResultPayload.ShouldBeNull();
        _ = await invoker.DidNotReceive().InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>());
        _ = await eventPublisher.Received(1).PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Is<IReadOnlyList<EventEnvelope>>(events => events.Count == 2),
            "corr-sm-test",
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>());
        await stateManager.DidNotReceive().SetStateAsync(
            Arg.Is<string>(s => s.Contains(":events:")),
            Arg.Any<EventEnvelope>(),
            Arg.Any<CancellationToken>());
        _ = await stateManager.Received().TryRemoveStateAsync(
            Arg.Is<string>(s => s.Contains(":pipeline:")),
            Arg.Any<CancellationToken>());
        await stateManager.Received().SetStateAsync(PendingCommandCountKey, 0, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommand_PublishFailed_WithResultPayload_ScrubsCheckpointAndTerminalResult() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, IDomainServiceInvoker invoker, _, _, IEventPublisher eventPublisher) = CreateActor();
        const string resultPayload = "{\"result\":{\"applied\":[\"Updated person details\"]},\"party\":{\"id\":\"agg-001\"}}";
        var successResult = new PayloadDomainResult([new TestEvent()], resultPayload);
        var checkpointedStates = new List<PipelineState>();
        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(successResult);
        _ = eventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(new EventPublishResult(false, 0, "Broker down"));
        _ = stateManager.SetStateAsync(
            Arg.Any<string>(),
            Arg.Any<PipelineState>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(callInfo => checkpointedStates.Add(callInfo.ArgAt<PipelineState>(1)));
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeTrue();
        result.EventCount.ShouldBe(1);
        result.ResultPayload.ShouldBeNull();
        checkpointedStates.ShouldContain(state => state.CurrentStage == CommandStatus.PublishFailed);
        checkpointedStates.ShouldAllBe(state => state.ResultPayload == null);
    }

    // --- Task 8.5: Crash recovery from Processing ---

    [Fact]
    public async Task ProcessCommand_CrashAtProcessing_Resume_ReprocessesFromScratch() {
        // Arrange -- simulate existing Processing pipeline state (crash before event persistence)
        (AggregateActor actor, IActorStateManager stateManager, IDomainServiceInvoker invoker, _, _, _) = CreateActor();

        var existingPipeline = new PipelineState(
            "corr-sm-test", CommandStatus.Processing, "CreateOrder",
            DateTimeOffset.UtcNow.AddSeconds(-5), EventCount: null, RejectionEventType: null);

        _ = stateManager.TryGetStateAsync<PipelineState>(
            Arg.Is<string>(s => s.Contains(":pipeline:corr-sm-test")),
            Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<PipelineState>(true, existingPipeline));

        _ = stateManager.TryGetStateAsync<int>(PendingCommandCountKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<int>(true, 1));

        var successResult = DomainResult.Success(new Hexalith.EventStore.Contracts.Events.IEventPayload[] { new TestEvent() });
        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(successResult);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert -- reprocessed from scratch (domain service WAS invoked)
        result.Accepted.ShouldBeTrue();
        result.EventCount.ShouldBe(1);
        _ = await invoker.Received(1).InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>());

        // Events were persisted (full reprocess)
        await stateManager.Received().SetStateAsync(
            Arg.Is<string>(s => s.Contains(":events:")),
            Arg.Any<EventEnvelope>(),
            Arg.Any<CancellationToken>());

        await stateManager.DidNotReceive().SetStateAsync(PendingCommandCountKey, 2, Arg.Any<CancellationToken>());
        await stateManager.Received().SetStateAsync(PendingCommandCountKey, 0, Arg.Any<CancellationToken>());
    }

    // --- Task 8.6: Advisory status write failure ---

    [Fact]
    public async Task ProcessCommand_StatusWriteFailure_DoesNotBlockPipeline() {
        // Arrange -- status store throws on every call
        (AggregateActor actor, _, IDomainServiceInvoker invoker, _, ICommandStatusStore statusStore, _) = CreateActor();
        _ = statusStore.WriteStatusAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CommandStatusRecord>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Status store unavailable"));

        var successResult = DomainResult.Success(new Hexalith.EventStore.Contracts.Events.IEventPayload[] { new TestEvent() });
        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(successResult);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act -- should NOT throw despite status store failures (rule #12)
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert -- pipeline completed successfully
        result.Accepted.ShouldBeTrue();
        result.EventCount.ShouldBe(1);

        // Status writes were attempted (they just failed)
        await statusStore.Received().WriteStatusAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CommandStatusRecord>(), Arg.Any<CancellationToken>());
    }

    // --- Task 8.7: Pipeline state cleaned up on completion ---

    [Fact]
    public async Task ProcessCommand_PipelineStateCleanedUp_OnCompletion() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, IDomainServiceInvoker invoker, _, _, _) = CreateActor();
        var successResult = DomainResult.Success(new Hexalith.EventStore.Contracts.Events.IEventPayload[] { new TestEvent() });
        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(successResult);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert -- TryRemoveStateAsync called for pipeline key
        _ = await stateManager.Received().TryRemoveStateAsync(
            Arg.Is<string>(s => s == "test-tenant:test-domain:agg-001:pipeline:corr-sm-test"),
            Arg.Any<CancellationToken>());
    }

    // --- Task 8.8: Pipeline state cleaned up on rejection ---

    [Fact]
    public async Task ProcessCommand_PipelineStateCleanedUp_OnRejection() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, IDomainServiceInvoker invoker, _, _, _) = CreateActor();
        var rejectionResult = DomainResult.Rejection(new Hexalith.EventStore.Contracts.Events.IRejectionEvent[] { new TestRejectionEvent() });
        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(rejectionResult);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert -- TryRemoveStateAsync called for pipeline key
        _ = await stateManager.Received().TryRemoveStateAsync(
            Arg.Is<string>(s => s == "test-tenant:test-domain:agg-001:pipeline:corr-sm-test"),
            Arg.Any<CancellationToken>());
    }

    // Test event types
    private sealed record TestEvent : Hexalith.EventStore.Contracts.Events.IEventPayload;

    private sealed record TestRejectionEvent : Hexalith.EventStore.Contracts.Events.IRejectionEvent;

    private sealed record PayloadDomainResult(IReadOnlyList<IEventPayload> Events, string Payload) : DomainResult(Events) {
        public override string? ResultPayload => Payload;
    }
}
