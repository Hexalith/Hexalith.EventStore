namespace Hexalith.EventStore.Server.Tests.Actors;

using System.Reflection;

using Dapr.Actors;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;

using Hexalith.EventStore.Server.Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

using EventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;

/// <summary>
/// Story 4.1 Task 7: AggregateActor publication integration tests.
/// Verifies state machine transitions with EventPublisher integration.
/// </summary>
public class EventPublicationIntegrationTests
{
    private static CommandEnvelope CreateTestEnvelope(
        string tenantId = "test-tenant",
        string? correlationId = null,
        string? causationId = null) => new(
        TenantId: tenantId,
        Domain: "test-domain",
        AggregateId: "agg-001",
        CommandType: "CreateOrder",
        Payload: [1, 2, 3],
        CorrelationId: correlationId ?? "corr-pub-test",
        CausationId: causationId,
        UserId: "system",
        Extensions: null);

    private static (AggregateActor Actor, IActorStateManager StateManager, IDomainServiceInvoker Invoker, IEventPublisher EventPublisher, ICommandStatusStore StatusStore) CreateActor()
    {
        var stateManager = Substitute.For<IActorStateManager>();
        var logger = Substitute.For<ILogger<AggregateActor>>();
        var invoker = Substitute.For<IDomainServiceInvoker>();
        var snapshotManager = Substitute.For<ISnapshotManager>();
        var statusStore = Substitute.For<ICommandStatusStore>();
        var eventPublisher = Substitute.For<IEventPublisher>();
        var host = ActorHost.CreateForTest<AggregateActor>(
            new ActorTestOptions { ActorId = new ActorId("test-tenant:test-domain:agg-001") });
        var actor = new AggregateActor(host, logger, invoker, snapshotManager, statusStore, eventPublisher, Options.Create(new EventDrainOptions()), Substitute.For<IDeadLetterPublisher>());

        PropertyInfo? prop = typeof(Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance);
        prop?.SetValue(actor, stateManager);

        // Default: no idempotency record
        stateManager.TryGetStateAsync<IdempotencyRecord>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(false, default!));

        // Default: no metadata (new aggregate)
        stateManager.TryGetStateAsync<AggregateMetadata>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(false, default!));

        // Default: no pipeline state
        stateManager.TryGetStateAsync<PipelineState>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<PipelineState>(false, default!));

        // Default: domain returns NoOp
        invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>())
            .Returns(DomainResult.NoOp());

        // Default: event publisher succeeds
        eventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(callInfo => new EventPublishResult(true, callInfo.ArgAt<IReadOnlyList<EventEnvelope>>(1).Count, null));

        return (actor, stateManager, invoker, eventPublisher, statusStore);
    }

    // --- Task 7.1: Happy path transitions ---

    [Fact]
    public async Task ProcessCommand_Success_TransitionsEventStored_EventsPublished_Completed()
    {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, IDomainServiceInvoker invoker, IEventPublisher eventPublisher, ICommandStatusStore statusStore) = CreateActor();
        var successResult = DomainResult.Success(new IEventPayload[] { new TestEvent() });
        invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(successResult);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeTrue();

        // EventsPublished checkpoint was written
        await stateManager.Received().SetStateAsync(
            Arg.Any<string>(),
            Arg.Is<PipelineState>(ps => ps.CurrentStage == CommandStatus.EventsPublished),
            Arg.Any<CancellationToken>());

        // EventPublisher was called
        await eventPublisher.Received(1).PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            "corr-pub-test",
            Arg.Any<CancellationToken>());

        // Advisory status writes include EventsPublished
        await statusStore.Received().WriteStatusAsync(
            "test-tenant",
            "corr-pub-test",
            Arg.Is<CommandStatusRecord>(r => r.Status == CommandStatus.EventsPublished));
    }

    // --- Task 7.2: Publication fails ---

    [Fact]
    public async Task ProcessCommand_PublishFails_TransitionsToPublishFailed()
    {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, IDomainServiceInvoker invoker, IEventPublisher eventPublisher, ICommandStatusStore statusStore) = CreateActor();
        var successResult = DomainResult.Success(new IEventPayload[] { new TestEvent() });
        invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(successResult);

        eventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(new EventPublishResult(false, 0, "Pub/sub unavailable"));

        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("publication failed");

        // PublishFailed checkpoint was written
        await stateManager.Received().SetStateAsync(
            Arg.Any<string>(),
            Arg.Is<PipelineState>(ps => ps.CurrentStage == CommandStatus.PublishFailed),
            Arg.Any<CancellationToken>());
    }

    // --- Task 7.3: No-op skips publication ---

    [Fact]
    public async Task ProcessCommand_NoOp_SkipsPublication_TransitionsDirectlyToCompleted()
    {
        // Arrange
        (AggregateActor actor, _, IDomainServiceInvoker invoker, IEventPublisher eventPublisher, _) = CreateActor();
        invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(DomainResult.NoOp());
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeTrue();
        result.EventCount.ShouldBe(0);

        // EventPublisher should NOT be called for no-op
        await eventPublisher.DidNotReceive().PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    // --- Task 7.4: Rejection events are published ---

    [Fact]
    public async Task ProcessCommand_Rejection_PublishesRejectionEvents_ThenCompleted()
    {
        // Arrange
        (AggregateActor actor, _, IDomainServiceInvoker invoker, IEventPublisher eventPublisher, _) = CreateActor();
        var rejectionResult = DomainResult.Rejection(new IRejectionEvent[] { new TestRejectionEvent() });
        invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(rejectionResult);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert -- D3: rejection events ARE published
        result.Accepted.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("Domain rejection");

        // EventPublisher WAS called with the rejection events
        await eventPublisher.Received(1).PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Is<IReadOnlyList<EventEnvelope>>(events => events.Count == 1),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    // --- Task 7.5: PublishFailed pipeline cleanup ---

    [Fact]
    public async Task ProcessCommand_PublishFailed_PipelineStateCleaned()
    {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, IDomainServiceInvoker invoker, IEventPublisher eventPublisher, _) = CreateActor();
        var successResult = DomainResult.Success(new IEventPayload[] { new TestEvent() });
        invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(successResult);

        eventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(new EventPublishResult(false, 0, "Timeout"));

        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        await actor.ProcessCommandAsync(envelope);

        // Assert -- pipeline state was cleaned up (TryRemoveStateAsync called for pipeline key)
        await stateManager.Received().TryRemoveStateAsync(
            Arg.Is<string>(s => s.Contains(":pipeline:")),
            Arg.Any<CancellationToken>());
    }

    // --- Task 7.6: EventsPublished advisory status ---

    [Fact]
    public async Task ProcessCommand_EventsPublished_StatusWrittenAdvisory()
    {
        // Arrange
        (AggregateActor actor, _, IDomainServiceInvoker invoker, _, ICommandStatusStore statusStore) = CreateActor();
        var successResult = DomainResult.Success(new IEventPayload[] { new TestEvent() });
        invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(successResult);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        await actor.ProcessCommandAsync(envelope);

        // Assert -- advisory EventsPublished status written
        await statusStore.Received().WriteStatusAsync(
            "test-tenant",
            "corr-pub-test",
            Arg.Is<CommandStatusRecord>(r => r.Status == CommandStatus.EventsPublished));
    }

    // --- Task 7.7: PublishFailed advisory status ---

    [Fact]
    public async Task ProcessCommand_PublishFailed_StatusWrittenAdvisory()
    {
        // Arrange
        (AggregateActor actor, _, IDomainServiceInvoker invoker, IEventPublisher eventPublisher, ICommandStatusStore statusStore) = CreateActor();
        var successResult = DomainResult.Success(new IEventPayload[] { new TestEvent() });
        invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(successResult);

        eventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(new EventPublishResult(false, 0, "Broker down"));

        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        await actor.ProcessCommandAsync(envelope);

        // Assert -- advisory PublishFailed status written
        await statusStore.Received().WriteStatusAsync(
            "test-tenant",
            "corr-pub-test",
            Arg.Is<CommandStatusRecord>(r => r.Status == CommandStatus.PublishFailed));
    }

    // --- Task 7.8: EventsPublished checkpoint atomicity ---

    [Fact]
    public async Task ProcessCommand_PublishSuccess_EventsPublishedCheckpointAtomic()
    {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, IDomainServiceInvoker invoker, _, _) = CreateActor();
        var successResult = DomainResult.Success(new IEventPayload[] { new TestEvent() });
        invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(successResult);
        CommandEnvelope envelope = CreateTestEnvelope();

        var stageCheckpoints = new List<CommandStatus>();
        stateManager.SetStateAsync(
            Arg.Any<string>(),
            Arg.Any<PipelineState>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => stageCheckpoints.Add(ci.ArgAt<PipelineState>(1).CurrentStage));

        // Act
        await actor.ProcessCommandAsync(envelope);

        // Assert -- checkpoints were staged in order (Processing, EventsStored, EventsPublished)
        // Note: CleanupPipelineAsync removes the pipeline state at the end, so we check the staged order
        stageCheckpoints.ShouldContain(CommandStatus.Processing);
        stageCheckpoints.ShouldContain(CommandStatus.EventsStored);
        stageCheckpoints.ShouldContain(CommandStatus.EventsPublished);
        stageCheckpoints.IndexOf(CommandStatus.EventsStored).ShouldBeGreaterThan(stageCheckpoints.IndexOf(CommandStatus.Processing));
        stageCheckpoints.IndexOf(CommandStatus.EventsPublished).ShouldBeGreaterThan(stageCheckpoints.IndexOf(CommandStatus.EventsStored));

        // SaveStateAsync was called (to commit the checkpoint)
        // Story 4.1: 3 saves = Processing + EventsStored + (EventsPublished checkpoint batched with terminal)
        await stateManager.Received(3).SaveStateAsync(Arg.Any<CancellationToken>());
    }

    // Test event types
    private sealed record TestEvent : IEventPayload;

    private sealed record TestRejectionEvent : IRejectionEvent;
}
