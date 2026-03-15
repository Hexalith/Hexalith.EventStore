using System.Reflection;

using Dapr.Actors;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

using EventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;

namespace Hexalith.EventStore.Server.Tests.Actors;

public class DeadLetterRoutingTests {
    private sealed record TestEvent : IEventPayload;

    private sealed record TestRejectionEvent : IRejectionEvent;

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
        CorrelationId: correlationId ?? Guid.NewGuid().ToString(),
        CausationId: causationId,
        UserId: "system",
        Extensions: null);

    private static (AggregateActor Actor, IActorStateManager StateManager, ILogger<AggregateActor> Logger, IDomainServiceInvoker Invoker, IDeadLetterPublisher DeadLetterPublisher) CreateActorWithMockState() {
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        ILogger<AggregateActor> logger = Substitute.For<ILogger<AggregateActor>>();
        IDomainServiceInvoker invoker = Substitute.For<IDomainServiceInvoker>();
        ISnapshotManager snapshotManager = Substitute.For<ISnapshotManager>();
        ICommandStatusStore commandStatusStore = Substitute.For<ICommandStatusStore>();
        IEventPublisher eventPublisher = Substitute.For<IEventPublisher>();
        var host = ActorHost.CreateForTest<AggregateActor>(
            new ActorTestOptions { ActorId = new ActorId("test-tenant:test-domain:agg-001") });
        IDeadLetterPublisher deadLetterPublisher = Substitute.For<IDeadLetterPublisher>();
        _ = deadLetterPublisher.PublishDeadLetterAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<DeadLetterMessage>(),
            Arg.Any<CancellationToken>())
            .Returns(true);
        var actor = new AggregateActor(host, logger, invoker, snapshotManager, new NoOpEventPayloadProtectionService(), commandStatusStore, eventPublisher, Options.Create(new EventDrainOptions()), deadLetterPublisher);

        // Set the mock state manager via reflection (Dapr runtime normally sets this)
        PropertyInfo? prop = typeof(Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance);
        prop?.SetValue(actor, stateManager);

        // Default: domain service returns NoOp
        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>())
            .Returns(DomainResult.NoOp());

        // Default: no pipeline state (fresh command, not a resume)
        _ = stateManager.TryGetStateAsync<PipelineState>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<PipelineState>(false, default!));

        // Default: event publisher succeeds
        _ = eventPublisher.PublishEventsAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(callInfo => new EventPublishResult(true, callInfo.ArgAt<IReadOnlyList<EventEnvelope>>(1).Count, null));

        return (actor, stateManager, logger, invoker, deadLetterPublisher);
    }

    private static void ConfigureNoDuplicate(IActorStateManager stateManager) {
        _ = stateManager.TryGetStateAsync<IdempotencyRecord>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(false, default!));

        // Default: new aggregate (no metadata) -- Step 3 returns null state
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(false, default!));
    }

    [Fact]
    public async Task ProcessCommand_DomainServiceInvocationFails_DeadLetterPublished() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, IDomainServiceInvoker invoker, IDeadLetterPublisher deadLetterPublisher) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        CommandEnvelope envelope = CreateTestEnvelope();

        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>())
            .ThrowsAsync(new HttpRequestException("Domain service unavailable"));

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert
        _ = await deadLetterPublisher.Received(1).PublishDeadLetterAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<DeadLetterMessage>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommand_DomainServiceInvocationFails_FullCommandInDeadLetter() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, IDomainServiceInvoker invoker, IDeadLetterPublisher deadLetterPublisher) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        CommandEnvelope envelope = CreateTestEnvelope();

        DeadLetterMessage? capturedMessage = null;
        _ = deadLetterPublisher.PublishDeadLetterAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Do<DeadLetterMessage>(m => capturedMessage = m),
            Arg.Any<CancellationToken>())
            .Returns(true);

        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>())
            .ThrowsAsync(new HttpRequestException("Domain service unavailable"));

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert
        _ = capturedMessage.ShouldNotBeNull();
        ReferenceEquals(capturedMessage.Command, envelope).ShouldBeTrue();
    }

    [Fact]
    public async Task ProcessCommand_DomainServiceInvocationFails_CorrectFailureStage() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, IDomainServiceInvoker invoker, IDeadLetterPublisher deadLetterPublisher) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        CommandEnvelope envelope = CreateTestEnvelope();

        DeadLetterMessage? capturedMessage = null;
        _ = deadLetterPublisher.PublishDeadLetterAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Do<DeadLetterMessage>(m => capturedMessage = m),
            Arg.Any<CancellationToken>())
            .Returns(true);

        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>())
            .ThrowsAsync(new HttpRequestException("Domain service unavailable"));

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert
        _ = capturedMessage.ShouldNotBeNull();
        capturedMessage.FailureStage.ShouldBe("Processing");
    }

    [Fact]
    public async Task ProcessCommand_DomainServiceInvocationFails_StatusRejected() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, IDomainServiceInvoker invoker, _) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        CommandEnvelope envelope = CreateTestEnvelope();

        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>())
            .ThrowsAsync(new HttpRequestException("Domain service unavailable"));

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeFalse();
    }

    [Fact]
    public async Task ProcessCommand_StateRehydrationFails_DeadLetterPublished() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, _, IDeadLetterPublisher deadLetterPublisher) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Configure existing aggregate with metadata, but state read throws
        var metadata = new AggregateMetadata(1, DateTimeOffset.UtcNow, null);
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, metadata));

        // Event stream read throws
        _ = stateManager.TryGetStateAsync<EventEnvelope>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("State store unavailable"));

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert
        _ = await deadLetterPublisher.Received(1).PublishDeadLetterAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<DeadLetterMessage>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommand_EventPersistenceFails_DeadLetterPublished() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, IDomainServiceInvoker invoker, IDeadLetterPublisher deadLetterPublisher) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        CommandEnvelope envelope = CreateTestEnvelope();

        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>())
            .Returns(DomainResult.Success([new TestEvent()]));

        // Configure SaveStateAsync to succeed first time (Processing checkpoint), fail second time (EventsStored commit), succeed third time (Rejected state)
        int saveCallCount = 0;
        _ = stateManager.SaveStateAsync(Arg.Any<CancellationToken>())
            .Returns(_ => {
                saveCallCount++;
                if (saveCallCount == 1) {
                    return Task.CompletedTask; // Processing checkpoint succeeds
                }

                if (saveCallCount == 2) {
                    throw new IOException("State store write failed"); // EventsStored commit fails
                }

                return Task.CompletedTask; // Rejected state save succeeds
            });

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert
        _ = await deadLetterPublisher.Received(1).PublishDeadLetterAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<DeadLetterMessage>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommand_EventPersistenceFails_CorrectEventCount() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, IDomainServiceInvoker invoker, IDeadLetterPublisher deadLetterPublisher) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        CommandEnvelope envelope = CreateTestEnvelope();

        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>())
            .Returns(DomainResult.Success([new TestEvent(), new TestEvent(), new TestEvent()]));

        DeadLetterMessage? capturedMessage = null;
        _ = deadLetterPublisher.PublishDeadLetterAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Do<DeadLetterMessage>(m => capturedMessage = m),
            Arg.Any<CancellationToken>())
            .Returns(true);

        // Configure SaveStateAsync to succeed first time (Processing checkpoint), fail second time (EventsStored commit), succeed third time (Rejected state)
        int saveCallCount = 0;
        _ = stateManager.SaveStateAsync(Arg.Any<CancellationToken>())
            .Returns(_ => {
                saveCallCount++;
                if (saveCallCount == 1) {
                    return Task.CompletedTask; // Processing checkpoint succeeds
                }

                if (saveCallCount == 2) {
                    throw new IOException("State store write failed"); // EventsStored commit fails
                }

                return Task.CompletedTask; // Rejected state save succeeds
            });

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert
        _ = capturedMessage.ShouldNotBeNull();
        capturedMessage.EventCountAtFailure.ShouldBe(3);
    }

    [Fact]
    public async Task ProcessCommand_DomainRejection_NoDeadLetter() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, IDomainServiceInvoker invoker, IDeadLetterPublisher deadLetterPublisher) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        CommandEnvelope envelope = CreateTestEnvelope();

        var rejectionResult = DomainResult.Rejection([new TestRejectionEvent()]);
        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(rejectionResult);

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert
        _ = await deadLetterPublisher.DidNotReceive().PublishDeadLetterAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<DeadLetterMessage>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommand_DomainReturnsEmpty_NoDeadLetter() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, IDomainServiceInvoker invoker, IDeadLetterPublisher deadLetterPublisher) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        CommandEnvelope envelope = CreateTestEnvelope();

        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(DomainResult.NoOp());

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert
        _ = await deadLetterPublisher.DidNotReceive().PublishDeadLetterAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<DeadLetterMessage>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommand_DeadLetterPublishFails_CommandStillRejectsNormally() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, IDomainServiceInvoker invoker, IDeadLetterPublisher deadLetterPublisher) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        CommandEnvelope envelope = CreateTestEnvelope();

        _ = deadLetterPublisher.PublishDeadLetterAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<DeadLetterMessage>(),
            Arg.Any<CancellationToken>())
            .Returns(false);

        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>())
            .ThrowsAsync(new HttpRequestException("Domain service unavailable"));

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeFalse();
    }

    [Fact]
    public async Task ProcessCommand_DeadLetterPublishFails_ErrorLogged() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, ILogger<AggregateActor> logger, IDomainServiceInvoker invoker, IDeadLetterPublisher deadLetterPublisher) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        CommandEnvelope envelope = CreateTestEnvelope();

        _ = deadLetterPublisher.PublishDeadLetterAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<DeadLetterMessage>(),
            Arg.Any<CancellationToken>())
            .Returns(false);

        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>())
            .ThrowsAsync(new HttpRequestException("Domain service unavailable"));

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert
        logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Dead-letter publication failed")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task ProcessCommand_DeadLetterPublished_CorrelationContextComplete() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, IDomainServiceInvoker invoker, IDeadLetterPublisher deadLetterPublisher) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        string correlationId = Guid.NewGuid().ToString();
        string causationId = Guid.NewGuid().ToString();
        CommandEnvelope envelope = CreateTestEnvelope(correlationId: correlationId, causationId: causationId);

        DeadLetterMessage? capturedMessage = null;
        _ = deadLetterPublisher.PublishDeadLetterAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Do<DeadLetterMessage>(m => capturedMessage = m),
            Arg.Any<CancellationToken>())
            .Returns(true);

        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>())
            .ThrowsAsync(new HttpRequestException("Domain service unavailable"));

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert
        _ = capturedMessage.ShouldNotBeNull();
        capturedMessage.CorrelationId.ShouldBe(correlationId);
        capturedMessage.CausationId.ShouldBe(causationId);
        capturedMessage.TenantId.ShouldBe("test-tenant");
        capturedMessage.Domain.ShouldBe("test-domain");
        capturedMessage.AggregateId.ShouldBe("agg-001");
        capturedMessage.CommandType.ShouldBe("CreateOrder");
    }

    [Fact]
    public async Task ProcessCommand_DeadLetterPublished_ReplayInfoSufficient() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, IDomainServiceInvoker invoker, IDeadLetterPublisher deadLetterPublisher) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        string correlationId = Guid.NewGuid().ToString();
        CommandEnvelope envelope = CreateTestEnvelope(correlationId: correlationId);

        DeadLetterMessage? capturedMessage = null;
        _ = deadLetterPublisher.PublishDeadLetterAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Do<DeadLetterMessage>(m => capturedMessage = m),
            Arg.Any<CancellationToken>())
            .Returns(true);

        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>())
            .ThrowsAsync(new HttpRequestException("Domain service unavailable"));

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert
        _ = capturedMessage.ShouldNotBeNull();
        capturedMessage.CorrelationId.ShouldBe(correlationId);
        capturedMessage.Command.CorrelationId.ShouldBe(correlationId);
    }
}
