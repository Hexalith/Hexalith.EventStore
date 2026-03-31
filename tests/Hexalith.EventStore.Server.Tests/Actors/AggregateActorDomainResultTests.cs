
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;

using Microsoft.Extensions.Logging;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

using static Hexalith.EventStore.Server.Tests.Actors.AggregateActorTestHelper;

using EventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;

namespace Hexalith.EventStore.Server.Tests.Actors;

public class AggregateActorDomainResultTests {
    // === Story 3.4: State Rehydration Tests ===

    [Fact]
    public async Task ProcessCommandAsync_NewAggregate_RehydratesNullState() {
        // Arrange
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        CommandProcessingResult result = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeTrue();
        ctx.Logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("State rehydrated") && o.ToString()!.Contains("null")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task ProcessCommandAsync_ExistingAggregate_RehydratesState() {
        // Arrange
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        ConfigureExistingAggregate(ctx.StateManager, 3);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        CommandProcessingResult result = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeTrue();
        ctx.Logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("State rehydrated") && o.ToString()!.Contains("DomainServiceCurrentState")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task ProcessCommandAsync_StateRehydrated_ProceedsToStep4() {
        // Arrange
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        _ = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert -- Step 4 domain service invocation logs result at Information level
        ctx.Logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Domain service result")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task ProcessCommandAsync_UnknownEvent_DeadLettersAndRejectsCommand() {
        // Arrange -- metadata says 2 events but event 2 is missing (simulates gap)
        // Story 4.5: Infrastructure exceptions (MissingEventException) trigger dead-letter routing
        ActorTestContext ctx = CreateActor();
        _ = ctx.StateManager.TryGetStateAsync<IdempotencyRecord>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(false, default!));

        var metadata = new AggregateMetadata(2, DateTimeOffset.UtcNow, null);
        _ = ctx.StateManager.TryGetStateAsync<AggregateMetadata>(
            "test-tenant:test-domain:agg-001:metadata", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, metadata));

        string keyPrefix = "test-tenant:test-domain:agg-001:events:";
        var evt1 = new EventEnvelope("msg-1", "agg-001", "test-aggregate", "test-tenant", "test-domain", 1, 0, DateTimeOffset.UtcNow,
            "corr-1", "cause-1", "user-1", "1.0.0", "OrderCreated", 1, "json", [1], null);
        _ = ctx.StateManager.TryGetStateAsync<EventEnvelope>($"{keyPrefix}1", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(true, evt1));
        _ = ctx.StateManager.TryGetStateAsync<EventEnvelope>($"{keyPrefix}2", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(false, default!));

        CommandEnvelope envelope = CreateTestEnvelope();

        // Act -- Story 4.5: dead-letter routing returns Rejected result instead of propagating
        CommandProcessingResult result = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeFalse();
    }

    [Fact]
    public async Task ProcessCommandAsync_StateRehydration_LogsStateType() {
        // Arrange
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        ConfigureExistingAggregate(ctx.StateManager, 2);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        _ = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert
        ctx.Logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("State rehydrated")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task ProcessCommandAsync_WithSnapshot_PassesSnapshotAwareCurrentStateToDomainService() {
        // Arrange
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        ConfigureExistingAggregate(ctx.StateManager, 2);

        _ = ctx.SnapshotManager.LoadSnapshotAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IActorStateManager>(),
            Arg.Any<string?>())
            .Returns(new SnapshotRecord(1, new { State = "snapshot-state" }, DateTimeOffset.UtcNow, "test-domain", "agg-001", "test-tenant"));

        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        _ = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert -- domain invocation gets snapshot state plus tail events without forcing a second full replay
        _ = await ctx.Invoker.Received(1).InvokeAsync(
            Arg.Any<CommandEnvelope>(),
            Arg.Is<object?>(o => IsSnapshotAwareCurrentState(o)));
    }

    // === Story 3.5: Domain Service Invocation Tests ===

    [Fact]
    public async Task ProcessCommandAsync_DomainSuccess_PersistsEventsViaStep5() {
        // Arrange
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        var successResult = DomainResult.Success(new Hexalith.EventStore.Contracts.Events.IEventPayload[] { new TestEvent() });
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(successResult);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        CommandProcessingResult result = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert -- Step 5 (EventPersister) writes event to state store
        result.Accepted.ShouldBeTrue();
        result.EventCount.ShouldBe(1);
        await ctx.StateManager.Received().SetStateAsync(
            Arg.Is<string>(s => s.Contains(":events:")),
            Arg.Any<EventEnvelope>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommandAsync_DomainRejection_ReturnsRejectionResult() {
        // Arrange
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        var rejectionResult = DomainResult.Rejection(new Hexalith.EventStore.Contracts.Events.IRejectionEvent[] { new TestRejectionEvent() });
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(rejectionResult);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        CommandProcessingResult result = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeFalse();
        _ = result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldContain("Domain rejection");
    }

    [Fact]
    public async Task ProcessCommandAsync_DomainRejection_StoresInIdempotencyCache() {
        // Arrange
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        var rejectionResult = DomainResult.Rejection(new Hexalith.EventStore.Contracts.Events.IRejectionEvent[] { new TestRejectionEvent() });
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(rejectionResult);
        CommandEnvelope envelope = CreateTestEnvelope(causationId: "cause-reject");

        // Act
        _ = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert
        await ctx.StateManager.Received(1).SetStateAsync(
            "idempotency:cause-reject",
            Arg.Is<IdempotencyRecord>(r => r.Accepted == false),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommandAsync_DomainNoOp_ReturnsAccepted() {
        // Arrange
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(DomainResult.NoOp());
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        CommandProcessingResult result = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeTrue();
    }

    [Fact]
    public async Task ProcessCommandAsync_DomainServiceNotFound_DeadLettersAndRejectsCommand() {
        // Arrange -- Story 4.5: Infrastructure exceptions trigger dead-letter routing
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>())
            .ThrowsAsync(new DomainServiceNotFoundException("test-tenant", "test-domain"));
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act -- dead-letter routing returns Rejected result
        CommandProcessingResult result = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeFalse();
    }

    [Fact]
    public async Task ProcessCommandAsync_DomainInfrastructureFailure_DeadLettersAndRejectsCommand() {
        // Arrange -- Story 4.5: Infrastructure exceptions trigger dead-letter routing
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>())
            .ThrowsAsync(new HttpRequestException("Service unavailable"));
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act -- dead-letter routing returns Rejected result
        CommandProcessingResult result = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("Service unavailable");
    }

    [Fact]
    public async Task ProcessCommandAsync_DomainInfrastructureFailure_WritesRejectedStatusWithFailureReason() {
        // Arrange -- infrastructure failures must remain distinguishable from domain rejections
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>())
            .ThrowsAsync(new HttpRequestException("Service unavailable"));
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        CommandProcessingResult result = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeFalse();
        await ctx.StatusStore.Received().WriteStatusAsync(
            envelope.TenantId,
            envelope.CorrelationId,
            Arg.Is<CommandStatusRecord>(record =>
                record.Status == CommandStatus.Rejected
                && record.FailureReason == "Service unavailable"
                && record.RejectionEventType == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommandAsync_DomainInvocation_LogsResultType() {
        // Arrange
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        var successResult = DomainResult.Success(new Hexalith.EventStore.Contracts.Events.IEventPayload[] { new TestEvent() });
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(successResult);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        _ = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert
        ctx.Logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Domain service result") && o.ToString()!.Contains("Success")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    // === Story 3.7: Event Persistence (Step 5) Tests ===

    [Fact]
    public async Task ProcessCommandAsync_AfterEventPersistence_SaveStateAsyncCalledThreeTimes() {
        // Arrange -- Story 3.11: Processing checkpoint + EventsStored atomic commit + terminal commit
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        var successResult = DomainResult.Success(new Hexalith.EventStore.Contracts.Events.IEventPayload[] { new TestEvent() });
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(successResult);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        _ = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert -- 4 SaveStateAsync calls: Processing checkpoint, EventsStored+events, terminal, pending-count decrement
        await ctx.StateManager.Received(4).SaveStateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommandAsync_NoOpResult_SaveStateAsyncStillCalled() {
        // Arrange -- no-op still saves (idempotency record + pipeline cleanup)
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(DomainResult.NoOp());
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        _ = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert -- Story 4.3 adds a final save for the persisted pending-count decrement.
        await ctx.StateManager.Received(3).SaveStateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommandAsync_DomainSuccess_ResultIncludesCorrectEventCount() {
        // Arrange
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        var successResult = DomainResult.Success(new Hexalith.EventStore.Contracts.Events.IEventPayload[]
        {
            new TestEvent(), new TestEvent(), new TestEvent(),
        });
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(successResult);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        CommandProcessingResult result = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert
        result.EventCount.ShouldBe(3);
    }

    [Fact]
    public async Task ProcessCommandAsync_NoOpResult_EventCountIsZero() {
        // Arrange
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(DomainResult.NoOp());
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        CommandProcessingResult result = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert
        result.EventCount.ShouldBe(0);
    }

    [Fact]
    public async Task ProcessCommandAsync_SaveStateAsyncThrows_ThrowsConcurrencyConflictException() {
        // Arrange -- AC #6: concurrency exception handling
        // Story 3.11: allow Processing checkpoint SaveStateAsync to succeed,
        // then throw on the EventsStored batch commit
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        var successResult = DomainResult.Success(new Hexalith.EventStore.Contracts.Events.IEventPayload[] { new TestEvent() });
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(successResult);

        int saveCallCount = 0;
        _ = ctx.StateManager.SaveStateAsync(Arg.Any<CancellationToken>())
            .Returns(_ => {
                saveCallCount++;
                // First call: Processing checkpoint (succeeds)
                if (saveCallCount == 1) {
                    return Task.CompletedTask;
                }

                // Second call: EventsStored batch (throws -- simulating concurrency conflict)
                return Task.FromException(new InvalidOperationException("ETag mismatch"));
            });

        CommandEnvelope envelope = CreateTestEnvelope(correlationId: "corr-conflict");

        // Act & Assert
        ConcurrencyConflictException ex = await Should.ThrowAsync<Server.Commands.ConcurrencyConflictException>(
            () => ctx.Actor.ProcessCommandAsync(envelope));
        ex.CorrelationId.ShouldBe("corr-conflict");
        ex.AggregateId.ShouldBe("agg-001");
    }

    // === Code Review Fix: D3 -- Rejection events persisted ===

    [Fact]
    public async Task ProcessCommandAsync_DomainRejection_PersistsRejectionEventsViaEventPersister() {
        // Arrange -- D3: rejection events are events, they must be persisted
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        var rejectionResult = DomainResult.Rejection(new Hexalith.EventStore.Contracts.Events.IRejectionEvent[] { new TestRejectionEvent() });
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(rejectionResult);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        _ = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert -- EventPersister should have written rejection event to state store
        await ctx.StateManager.Received().SetStateAsync(
            Arg.Is<string>(s => s.Contains(":events:")),
            Arg.Any<EventEnvelope>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommandAsync_DomainRejection_ResultIncludesEventCount() {
        // Arrange
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        var rejectionResult = DomainResult.Rejection(new Hexalith.EventStore.Contracts.Events.IRejectionEvent[] { new TestRejectionEvent() });
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(rejectionResult);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        CommandProcessingResult result = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert
        result.EventCount.ShouldBe(1);
    }

    // === Code Review Fix: M2 -- OperationCanceledException passthrough ===

    [Fact]
    public async Task ProcessCommandAsync_SaveStateAsyncThrowsOperationCanceled_PropagatesDirectly() {
        // Arrange -- OperationCanceledException should NOT be wrapped as ConcurrencyConflictException
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(DomainResult.NoOp());

        _ = ctx.StateManager.SaveStateAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new OperationCanceledException("Shutdown")));

        CommandEnvelope envelope = CreateTestEnvelope();

        // Act & Assert -- should propagate directly, not wrapped
        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => ctx.Actor.ProcessCommandAsync(envelope));
    }

    // === Story 3.9: Snapshot Integration Tests ===

    [Fact]
    public async Task ProcessCommandAsync_DomainSuccess_CallsShouldCreateSnapshotWithCorrectSequence() {
        // Arrange
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        ConfigureExistingAggregate(ctx.StateManager, 3);

        var successResult = DomainResult.Success(new Hexalith.EventStore.Contracts.Events.IEventPayload[] { new TestEvent() });
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(successResult);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        _ = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert -- ShouldCreateSnapshotAsync called with newSequence=4 (3 existing + 1 new), lastSnapshotSequence=0
        _ = await ctx.SnapshotManager.Received(1).ShouldCreateSnapshotAsync("test-tenant", "test-domain", 4, 0);
    }

    [Fact]
    public async Task ProcessCommandAsync_ShouldSnapshotTrue_CallsCreateSnapshotWithPreEventSequence() {
        // Arrange
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        ConfigureExistingAggregate(ctx.StateManager, 3);

        var successResult = DomainResult.Success(new Hexalith.EventStore.Contracts.Events.IEventPayload[] { new TestEvent() });
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(successResult);
        _ = ctx.SnapshotManager.ShouldCreateSnapshotAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<long>())
            .Returns(true);

        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        _ = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert -- H3 fix: snapshot created at preEventSequence (4-1=3), not newSequence (4)
        await ctx.SnapshotManager.Received(1).CreateSnapshotAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            3, // preEventSequence = newSequence(4) - events.Count(1)
            Arg.Any<object>(),
            ctx.StateManager,
            envelope.CorrelationId);
    }

    [Fact]
    public async Task ProcessCommandAsync_ShouldSnapshotFalse_DoesNotCallCreateSnapshot() {
        // Arrange
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        ConfigureExistingAggregate(ctx.StateManager, 3);

        var successResult = DomainResult.Success(new Hexalith.EventStore.Contracts.Events.IEventPayload[] { new TestEvent() });
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(successResult);
        _ = ctx.SnapshotManager.ShouldCreateSnapshotAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<long>())
            .Returns(false);

        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        _ = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert
        await ctx.SnapshotManager.DidNotReceive().CreateSnapshotAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<long>(),
            Arg.Any<object>(),
            Arg.Any<IActorStateManager>(),
            Arg.Any<string?>());
    }

    [Fact]
    public async Task ProcessCommandAsync_ExistingSnapshot_UsesSnapshotSequenceForInterval() {
        // Arrange -- H2 fix: existing snapshot at seq 100 means lastSnapshotSequence=100
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        ConfigureExistingAggregate(ctx.StateManager, 3);

        var existingSnapshot = new SnapshotRecord(100, new object(), DateTimeOffset.UtcNow, "test-domain", "agg-001", "test-tenant");
        _ = ctx.SnapshotManager.LoadSnapshotAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IActorStateManager>(),
            Arg.Any<string?>())
            .Returns(existingSnapshot);

        var successResult = DomainResult.Success(new Hexalith.EventStore.Contracts.Events.IEventPayload[] { new TestEvent() });
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(successResult);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        _ = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert -- ShouldCreateSnapshotAsync called with lastSnapshotSequence=100 from loaded snapshot
        _ = await ctx.SnapshotManager.Received(1).ShouldCreateSnapshotAsync("test-tenant", "test-domain", 4, 100);
    }

    [Fact]
    public async Task ProcessCommandAsync_DomainRejection_AlsoChecksSnapshotInterval() {
        // Arrange -- M2 fix: rejection events count toward snapshot interval
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        ConfigureExistingAggregate(ctx.StateManager, 3);

        var rejectionResult = DomainResult.Rejection(new Hexalith.EventStore.Contracts.Events.IRejectionEvent[] { new TestRejectionEvent() });
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(rejectionResult);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        _ = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert -- ShouldCreateSnapshotAsync called on rejection path
        _ = await ctx.SnapshotManager.Received(1).ShouldCreateSnapshotAsync("test-tenant", "test-domain", Arg.Any<long>(), Arg.Is<long>(0));
    }
}
