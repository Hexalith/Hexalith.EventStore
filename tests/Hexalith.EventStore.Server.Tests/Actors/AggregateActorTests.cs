
using System.Reflection;

using Dapr.Actors;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Commands;
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

public class AggregateActorTests {
    private static CommandEnvelope CreateTestEnvelope(
        string tenantId = "test-tenant",
        string? correlationId = null,
        string? causationId = null) => new(
        TenantId: tenantId,
        Domain: "test-domain",
        AggregateId: "agg-001",
        CommandType: "CreateOrder",
        Payload: [1, 2, 3],
        CorrelationId: correlationId ?? Guid.NewGuid().ToString(),
        CausationId: causationId,
        UserId: "system",
        Extensions: null);

    private static (AggregateActor Actor, IActorStateManager StateManager, ILogger<AggregateActor> Logger, IDomainServiceInvoker Invoker) CreateActorWithMockState() {
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        ILogger<AggregateActor> logger = Substitute.For<ILogger<AggregateActor>>();
        _ = logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        IDomainServiceInvoker invoker = Substitute.For<IDomainServiceInvoker>();
        ISnapshotManager snapshotManager = Substitute.For<ISnapshotManager>();
        ICommandStatusStore commandStatusStore = Substitute.For<ICommandStatusStore>();
        IEventPublisher eventPublisher = Substitute.For<IEventPublisher>();
        var host = ActorHost.CreateForTest<AggregateActor>(
            new ActorTestOptions { ActorId = new ActorId("test-tenant:test-domain:agg-001") });
        IDeadLetterPublisher deadLetterPublisher = Substitute.For<IDeadLetterPublisher>();
        _ = deadLetterPublisher.PublishDeadLetterAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
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
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(callInfo => new EventPublishResult(true, callInfo.ArgAt<IReadOnlyList<EventEnvelope>>(1).Count, null));

        return (actor, stateManager, logger, invoker);
    }

    private static void ConfigureNoDuplicate(IActorStateManager stateManager) {
        _ = stateManager.TryGetStateAsync<IdempotencyRecord>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(false, default!));

        // Default: new aggregate (no metadata) -- Step 3 returns null state
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(false, default!));
    }

    private static void ConfigureExistingAggregate(IActorStateManager stateManager, int eventCount) {
        var metadata = new AggregateMetadata(eventCount, DateTimeOffset.UtcNow, null);
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(
            "test-tenant:test-domain:agg-001:metadata", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, metadata));

        string keyPrefix = "test-tenant:test-domain:agg-001:events:";
        for (int i = 1; i <= eventCount; i++) {
            int seq = i;
            var evt = new EventEnvelope(
                "agg-001", "test-tenant", "test-domain", seq, DateTimeOffset.UtcNow,
                $"corr-{seq}", $"cause-{seq}", "user-1", "1.0.0", "OrderCreated", "json",
                [1, 2, 3], null);
            _ = stateManager.TryGetStateAsync<EventEnvelope>($"{keyPrefix}{seq}", Arg.Any<CancellationToken>())
                .Returns(new ConditionalValue<EventEnvelope>(true, evt));
        }
    }

    private static void ConfigureDuplicate(IActorStateManager stateManager, string causationId, string correlationId) {
        var record = new IdempotencyRecord(causationId, correlationId, true, null, DateTimeOffset.UtcNow);
        _ = stateManager.TryGetStateAsync<IdempotencyRecord>($"idempotency:{causationId}", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(true, record));

        // Default for other keys
        _ = stateManager.TryGetStateAsync<IdempotencyRecord>(
            Arg.Is<string>(s => s != $"idempotency:{causationId}"), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(false, default!));
    }

    [Fact]
    public async Task ProcessCommandAsync_ValidCommand_ReturnsAccepted() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, _) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeTrue();
    }

    [Fact]
    public async Task ProcessCommandAsync_ValidCommand_ReturnsCorrelationId() {
        // Arrange
        string correlationId = Guid.NewGuid().ToString();
        (AggregateActor actor, IActorStateManager stateManager, _, _) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        CommandEnvelope envelope = CreateTestEnvelope(correlationId: correlationId);

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert
        result.CorrelationId.ShouldBe(correlationId);
    }

    [Fact]
    public async Task ProcessCommandAsync_ValidCommand_LogsCommandReceipt() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, ILogger<AggregateActor> logger, _) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert
        logger.Received().Log(
            LogLevel.Debug,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Actor activated")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task ProcessCommandAsync_NewCommand_StoresIdempotencyRecord() {
        // Arrange
        string correlationId = "corr-new";
        (AggregateActor actor, IActorStateManager stateManager, _, _) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        CommandEnvelope envelope = CreateTestEnvelope(correlationId: correlationId, causationId: "cause-new");

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert
        await stateManager.Received(1).SetStateAsync(
            "idempotency:cause-new",
            Arg.Is<IdempotencyRecord>(r => r.CausationId == "cause-new" && r.CorrelationId == correlationId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommandAsync_NewCommand_CallsSaveStateAsync() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, _) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert -- Story 3.11: 2 SaveStateAsync calls for no-op (Processing checkpoint + terminal)
        await stateManager.Received(2).SaveStateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommandAsync_DuplicateCommand_ReturnsCachedResult() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, _) = CreateActorWithMockState();
        ConfigureDuplicate(stateManager, "cause-dup", "corr-dup");
        CommandEnvelope envelope = CreateTestEnvelope(correlationId: "corr-dup", causationId: "cause-dup");

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeTrue();
        result.CorrelationId.ShouldBe("corr-dup");
    }

    [Fact]
    public async Task ProcessCommandAsync_DuplicateCommand_DoesNotCallSaveState() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, _) = CreateActorWithMockState();
        ConfigureDuplicate(stateManager, "cause-dup", "corr-dup");
        CommandEnvelope envelope = CreateTestEnvelope(correlationId: "corr-dup", causationId: "cause-dup");

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert
        await stateManager.DidNotReceive().SaveStateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommandAsync_DuplicateCommand_DoesNotStoreNewRecord() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, _) = CreateActorWithMockState();
        ConfigureDuplicate(stateManager, "cause-dup", "corr-dup");
        CommandEnvelope envelope = CreateTestEnvelope(correlationId: "corr-dup", causationId: "cause-dup");

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert
        await stateManager.DidNotReceive().SetStateAsync(
            Arg.Any<string>(),
            Arg.Any<IdempotencyRecord>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommandAsync_DuplicateCommand_LogsDuplicateDetection() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, ILogger<AggregateActor> logger, _) = CreateActorWithMockState();
        ConfigureDuplicate(stateManager, "cause-dup", "corr-dup");
        CommandEnvelope envelope = CreateTestEnvelope(correlationId: "corr-dup", causationId: "cause-dup");

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert
        logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Duplicate command detected")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task ProcessCommandAsync_NullCausationId_UseCorrelationIdAsFallback() {
        // Arrange
        string correlationId = "corr-fallback";
        (AggregateActor actor, IActorStateManager stateManager, _, _) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        CommandEnvelope envelope = CreateTestEnvelope(correlationId: correlationId, causationId: null);

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert -- should use correlationId as the key since causationId is null
        _ = await stateManager.Received(1).TryGetStateAsync<IdempotencyRecord>(
            $"idempotency:{correlationId}",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommandAsync_TenantMismatch_ReturnsRejection() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, _) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        CommandEnvelope envelope = CreateTestEnvelope(tenantId: "wrong-tenant");

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("TenantMismatch");
    }

    [Fact]
    public async Task ProcessCommandAsync_TenantMismatch_DoesNotExecuteSteps3Through5() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, ILogger<AggregateActor> logger, _) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        CommandEnvelope envelope = CreateTestEnvelope(tenantId: "wrong-tenant");

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert -- Step 3 logs "State rehydrated" at Information level; should NOT appear after tenant mismatch
        logger.DidNotReceive().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("State rehydrated")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task ProcessCommandAsync_TenantMismatch_StoresRejectionInIdempotencyCache() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, _) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        CommandEnvelope envelope = CreateTestEnvelope(tenantId: "wrong-tenant", causationId: "cause-mismatch");

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert
        await stateManager.Received(1).SetStateAsync(
            "idempotency:cause-mismatch",
            Arg.Is<IdempotencyRecord>(r => r.Accepted == false),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommandAsync_TenantMismatch_CallsSaveStateAsync() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, _) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        CommandEnvelope envelope = CreateTestEnvelope(tenantId: "wrong-tenant");

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert
        await stateManager.Received(1).SaveStateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommandAsync_MatchingTenant_ProceedsToStep3() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, ILogger<AggregateActor> logger, _) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        CommandEnvelope envelope = CreateTestEnvelope(); // test-tenant matches actor ID test-tenant:test-domain:agg-001

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert -- Step 3 should have logged state rehydration at Information level
        logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("State rehydrated")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task ProcessCommandAsync_DuplicateRejectedCommand_ReturnsCachedRejection() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, _) = CreateActorWithMockState();
        var record = new IdempotencyRecord("cause-rejected", "corr-rejected", false, "TenantMismatch: ...", DateTimeOffset.UtcNow);
        _ = stateManager.TryGetStateAsync<IdempotencyRecord>("idempotency:cause-rejected", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(true, record));
        CommandEnvelope envelope = CreateTestEnvelope(tenantId: "wrong-tenant", correlationId: "corr-rejected", causationId: "cause-rejected");

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeFalse();
    }

    [Fact]
    public async Task ProcessCommandAsync_TenantMismatch_RejectionContainsBothTenants() {
        // Arrange (F-SA6)
        (AggregateActor actor, IActorStateManager stateManager, _, _) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        CommandEnvelope envelope = CreateTestEnvelope(tenantId: "tenant-b");

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert
        _ = result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldContain("tenant-b");
        result.ErrorMessage.ShouldContain("test-tenant");
    }

    // === Story 3.4: State Rehydration Tests ===

    [Fact]
    public async Task ProcessCommandAsync_NewAggregate_RehydratesNullState() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, ILogger<AggregateActor> logger, _) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeTrue();
        logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("State rehydrated") && o.ToString()!.Contains("null")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task ProcessCommandAsync_ExistingAggregate_RehydratesState() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, ILogger<AggregateActor> logger, _) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        ConfigureExistingAggregate(stateManager, 3);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeTrue();
        logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("State rehydrated") && o.ToString()!.Contains("List")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task ProcessCommandAsync_StateRehydrated_ProceedsToStep4() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, ILogger<AggregateActor> logger, _) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert -- Step 4 domain service invocation logs result at Information level
        logger.Received().Log(
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
        (AggregateActor actor, IActorStateManager stateManager, _, _) = CreateActorWithMockState();
        _ = stateManager.TryGetStateAsync<IdempotencyRecord>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(false, default!));

        var metadata = new AggregateMetadata(2, DateTimeOffset.UtcNow, null);
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(
            "test-tenant:test-domain:agg-001:metadata", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, metadata));

        string keyPrefix = "test-tenant:test-domain:agg-001:events:";
        var evt1 = new EventEnvelope("agg-001", "test-tenant", "test-domain", 1, DateTimeOffset.UtcNow,
            "corr-1", "cause-1", "user-1", "1.0.0", "OrderCreated", "json", [1], null);
        _ = stateManager.TryGetStateAsync<EventEnvelope>($"{keyPrefix}1", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(true, evt1));
        _ = stateManager.TryGetStateAsync<EventEnvelope>($"{keyPrefix}2", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(false, default!));

        CommandEnvelope envelope = CreateTestEnvelope();

        // Act -- Story 4.5: dead-letter routing returns Rejected result instead of propagating
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeFalse();
    }

    [Fact]
    public async Task ProcessCommandAsync_StateRehydration_LogsStateType() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, ILogger<AggregateActor> logger, _) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        ConfigureExistingAggregate(stateManager, 2);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert
        logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("State rehydrated")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task ProcessCommandAsync_WithSnapshot_RehydratesUsingListStateForDomainCompatibility() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, IDomainServiceInvoker invoker, ISnapshotManager snapshotManager) = CreateActorWithAllMocks();
        ConfigureNoDuplicate(stateManager);
        ConfigureExistingAggregate(stateManager, 2);

        _ = snapshotManager.LoadSnapshotAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IActorStateManager>(),
            Arg.Any<string?>())
            .Returns(new SnapshotRecord(1, new { State = "snapshot-state" }, DateTimeOffset.UtcNow, "test-domain", "agg-001", "test-tenant"));

        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert -- domain invocation gets a List<EventEnvelope> (backward-compatible contract), not RehydrationResult
        _ = await invoker.Received(1).InvokeAsync(
            Arg.Any<CommandEnvelope>(),
            Arg.Is<object?>(o => o is List<EventEnvelope> && ((List<EventEnvelope>)o).Count == 2));
    }

    // === Story 3.5: Domain Service Invocation Tests ===

    [Fact]
    public async Task ProcessCommandAsync_DomainSuccess_PersistsEventsViaStep5() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, ILogger<AggregateActor> logger, IDomainServiceInvoker invoker) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        var successResult = DomainResult.Success(new Hexalith.EventStore.Contracts.Events.IEventPayload[] { new TestEvent() });
        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(successResult);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert -- Step 5 (EventPersister) writes event to state store
        result.Accepted.ShouldBeTrue();
        result.EventCount.ShouldBe(1);
        await stateManager.Received().SetStateAsync(
            Arg.Is<string>(s => s.Contains(":events:")),
            Arg.Any<EventEnvelope>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommandAsync_DomainRejection_ReturnsRejectionResult() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, IDomainServiceInvoker invoker) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        var rejectionResult = DomainResult.Rejection(new Hexalith.EventStore.Contracts.Events.IRejectionEvent[] { new TestRejectionEvent() });
        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(rejectionResult);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeFalse();
        _ = result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldContain("Domain rejection");
    }

    [Fact]
    public async Task ProcessCommandAsync_DomainRejection_StoresInIdempotencyCache() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, IDomainServiceInvoker invoker) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        var rejectionResult = DomainResult.Rejection(new Hexalith.EventStore.Contracts.Events.IRejectionEvent[] { new TestRejectionEvent() });
        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(rejectionResult);
        CommandEnvelope envelope = CreateTestEnvelope(causationId: "cause-reject");

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert
        await stateManager.Received(1).SetStateAsync(
            "idempotency:cause-reject",
            Arg.Is<IdempotencyRecord>(r => r.Accepted == false),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommandAsync_DomainNoOp_ReturnsAccepted() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, IDomainServiceInvoker invoker) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(DomainResult.NoOp());
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeTrue();
    }

    [Fact]
    public async Task ProcessCommandAsync_DomainServiceNotFound_DeadLettersAndRejectsCommand() {
        // Arrange -- Story 4.5: Infrastructure exceptions trigger dead-letter routing
        (AggregateActor actor, IActorStateManager stateManager, _, IDomainServiceInvoker invoker) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>())
            .ThrowsAsync(new DomainServiceNotFoundException("test-tenant", "test-domain"));
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act -- dead-letter routing returns Rejected result
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeFalse();
    }

    [Fact]
    public async Task ProcessCommandAsync_DomainInfrastructureFailure_DeadLettersAndRejectsCommand() {
        // Arrange -- Story 4.5: Infrastructure exceptions trigger dead-letter routing
        (AggregateActor actor, IActorStateManager stateManager, _, IDomainServiceInvoker invoker) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>())
            .ThrowsAsync(new HttpRequestException("Service unavailable"));
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act -- dead-letter routing returns Rejected result
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("Service unavailable");
    }

    [Fact]
    public async Task ProcessCommandAsync_DomainInvocation_LogsResultType() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, ILogger<AggregateActor> logger, IDomainServiceInvoker invoker) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        var successResult = DomainResult.Success(new Hexalith.EventStore.Contracts.Events.IEventPayload[] { new TestEvent() });
        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(successResult);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert
        logger.Received().Log(
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
        (AggregateActor actor, IActorStateManager stateManager, _, IDomainServiceInvoker invoker) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        var successResult = DomainResult.Success(new Hexalith.EventStore.Contracts.Events.IEventPayload[] { new TestEvent() });
        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(successResult);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert -- 3 SaveStateAsync calls: Processing checkpoint, EventsStored+events, terminal
        await stateManager.Received(3).SaveStateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommandAsync_NoOpResult_SaveStateAsyncStillCalled() {
        // Arrange -- no-op still saves (idempotency record + pipeline cleanup)
        (AggregateActor actor, IActorStateManager stateManager, _, IDomainServiceInvoker invoker) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(DomainResult.NoOp());
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert -- Story 3.11: 2 SaveStateAsync calls for no-op (Processing checkpoint + terminal)
        await stateManager.Received(2).SaveStateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommandAsync_DomainSuccess_ResultIncludesCorrectEventCount() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, IDomainServiceInvoker invoker) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        var successResult = DomainResult.Success(new Hexalith.EventStore.Contracts.Events.IEventPayload[]
        {
            new TestEvent(), new TestEvent(), new TestEvent(),
        });
        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(successResult);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert
        result.EventCount.ShouldBe(3);
    }

    [Fact]
    public async Task ProcessCommandAsync_NoOpResult_EventCountIsZero() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, IDomainServiceInvoker invoker) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(DomainResult.NoOp());
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert
        result.EventCount.ShouldBe(0);
    }

    [Fact]
    public async Task ProcessCommandAsync_SaveStateAsyncThrows_ThrowsConcurrencyConflictException() {
        // Arrange -- AC #6: concurrency exception handling
        // Story 3.11: allow Processing checkpoint SaveStateAsync to succeed,
        // then throw on the EventsStored batch commit
        (AggregateActor actor, IActorStateManager stateManager, _, IDomainServiceInvoker invoker) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        var successResult = DomainResult.Success(new Hexalith.EventStore.Contracts.Events.IEventPayload[] { new TestEvent() });
        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(successResult);

        int saveCallCount = 0;
        _ = stateManager.SaveStateAsync(Arg.Any<CancellationToken>())
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
            () => actor.ProcessCommandAsync(envelope));
        ex.CorrelationId.ShouldBe("corr-conflict");
        ex.AggregateId.ShouldBe("agg-001");
    }

    // === Code Review Fix: D3 -- Rejection events persisted ===

    [Fact]
    public async Task ProcessCommandAsync_DomainRejection_PersistsRejectionEventsViaEventPersister() {
        // Arrange -- D3: rejection events are events, they must be persisted
        (AggregateActor actor, IActorStateManager stateManager, _, IDomainServiceInvoker invoker) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        var rejectionResult = DomainResult.Rejection(new Hexalith.EventStore.Contracts.Events.IRejectionEvent[] { new TestRejectionEvent() });
        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(rejectionResult);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert -- EventPersister should have written rejection event to state store
        await stateManager.Received().SetStateAsync(
            Arg.Is<string>(s => s.Contains(":events:")),
            Arg.Any<EventEnvelope>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommandAsync_DomainRejection_ResultIncludesEventCount() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, IDomainServiceInvoker invoker) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        var rejectionResult = DomainResult.Rejection(new Hexalith.EventStore.Contracts.Events.IRejectionEvent[] { new TestRejectionEvent() });
        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(rejectionResult);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert
        result.EventCount.ShouldBe(1);
    }

    // === Code Review Fix: M2 -- OperationCanceledException passthrough ===

    [Fact]
    public async Task ProcessCommandAsync_SaveStateAsyncThrowsOperationCanceled_PropagatesDirectly() {
        // Arrange -- OperationCanceledException should NOT be wrapped as ConcurrencyConflictException
        (AggregateActor actor, IActorStateManager stateManager, _, IDomainServiceInvoker invoker) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(DomainResult.NoOp());

        _ = stateManager.SaveStateAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new OperationCanceledException("Shutdown")));

        CommandEnvelope envelope = CreateTestEnvelope();

        // Act & Assert -- should propagate directly, not wrapped
        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => actor.ProcessCommandAsync(envelope));
    }

    // === Story 3.9: Snapshot Integration Tests ===

    private static (AggregateActor Actor, IActorStateManager StateManager, ILogger<AggregateActor> Logger, IDomainServiceInvoker Invoker, ISnapshotManager SnapshotManager) CreateActorWithAllMocks() {
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        ILogger<AggregateActor> logger = Substitute.For<ILogger<AggregateActor>>();
        _ = logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        IDomainServiceInvoker invoker = Substitute.For<IDomainServiceInvoker>();
        ISnapshotManager snapshotManager = Substitute.For<ISnapshotManager>();
        ICommandStatusStore commandStatusStore = Substitute.For<ICommandStatusStore>();
        IEventPublisher eventPublisher = Substitute.For<IEventPublisher>();
        var host = ActorHost.CreateForTest<AggregateActor>(
            new ActorTestOptions { ActorId = new ActorId("test-tenant:test-domain:agg-001") });
        IDeadLetterPublisher deadLetterPublisher = Substitute.For<IDeadLetterPublisher>();
        _ = deadLetterPublisher.PublishDeadLetterAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<DeadLetterMessage>(),
            Arg.Any<CancellationToken>())
            .Returns(true);
        var actor = new AggregateActor(host, logger, invoker, snapshotManager, new NoOpEventPayloadProtectionService(), commandStatusStore, eventPublisher, Options.Create(new EventDrainOptions()), deadLetterPublisher);

        PropertyInfo? prop = typeof(Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance);
        prop?.SetValue(actor, stateManager);

        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>())
            .Returns(DomainResult.NoOp());

        // Default: no pipeline state (fresh command, not a resume)
        _ = stateManager.TryGetStateAsync<PipelineState>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<PipelineState>(false, default!));

        // Default: event publisher succeeds
        _ = eventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(callInfo => new EventPublishResult(true, callInfo.ArgAt<IReadOnlyList<EventEnvelope>>(1).Count, null));

        return (actor, stateManager, logger, invoker, snapshotManager);
    }

    [Fact]
    public async Task ProcessCommandAsync_DomainSuccess_CallsShouldCreateSnapshotWithCorrectSequence() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, IDomainServiceInvoker invoker, ISnapshotManager snapshotManager) = CreateActorWithAllMocks();
        ConfigureNoDuplicate(stateManager);
        ConfigureExistingAggregate(stateManager, 3);

        var successResult = DomainResult.Success(new Hexalith.EventStore.Contracts.Events.IEventPayload[] { new TestEvent() });
        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(successResult);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert -- ShouldCreateSnapshotAsync called with newSequence=4 (3 existing + 1 new), lastSnapshotSequence=0
        _ = await snapshotManager.Received(1).ShouldCreateSnapshotAsync("test-domain", 4, 0);
    }

    [Fact]
    public async Task ProcessCommandAsync_ShouldSnapshotTrue_CallsCreateSnapshotWithPreEventSequence() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, IDomainServiceInvoker invoker, ISnapshotManager snapshotManager) = CreateActorWithAllMocks();
        ConfigureNoDuplicate(stateManager);
        ConfigureExistingAggregate(stateManager, 3);

        var successResult = DomainResult.Success(new Hexalith.EventStore.Contracts.Events.IEventPayload[] { new TestEvent() });
        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(successResult);
        _ = snapshotManager.ShouldCreateSnapshotAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Any<long>())
            .Returns(true);

        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert -- H3 fix: snapshot created at preEventSequence (4-1=3), not newSequence (4)
        await snapshotManager.Received(1).CreateSnapshotAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            3, // preEventSequence = newSequence(4) - events.Count(1)
            Arg.Any<object>(),
            stateManager,
            envelope.CorrelationId);
    }

    [Fact]
    public async Task ProcessCommandAsync_ShouldSnapshotFalse_DoesNotCallCreateSnapshot() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, IDomainServiceInvoker invoker, ISnapshotManager snapshotManager) = CreateActorWithAllMocks();
        ConfigureNoDuplicate(stateManager);
        ConfigureExistingAggregate(stateManager, 3);

        var successResult = DomainResult.Success(new Hexalith.EventStore.Contracts.Events.IEventPayload[] { new TestEvent() });
        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(successResult);
        _ = snapshotManager.ShouldCreateSnapshotAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Any<long>())
            .Returns(false);

        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert
        await snapshotManager.DidNotReceive().CreateSnapshotAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<long>(),
            Arg.Any<object>(),
            Arg.Any<IActorStateManager>(),
            Arg.Any<string?>());
    }

    [Fact]
    public async Task ProcessCommandAsync_ExistingSnapshot_UsesSnapshotSequenceForInterval() {
        // Arrange -- H2 fix: existing snapshot at seq 100 means lastSnapshotSequence=100
        (AggregateActor actor, IActorStateManager stateManager, _, IDomainServiceInvoker invoker, ISnapshotManager snapshotManager) = CreateActorWithAllMocks();
        ConfigureNoDuplicate(stateManager);
        ConfigureExistingAggregate(stateManager, 3);

        var existingSnapshot = new SnapshotRecord(100, new object(), DateTimeOffset.UtcNow, "test-domain", "agg-001", "test-tenant");
        _ = snapshotManager.LoadSnapshotAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IActorStateManager>(),
            Arg.Any<string?>())
            .Returns(existingSnapshot);

        var successResult = DomainResult.Success(new Hexalith.EventStore.Contracts.Events.IEventPayload[] { new TestEvent() });
        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(successResult);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert -- ShouldCreateSnapshotAsync called with lastSnapshotSequence=100 from loaded snapshot
        _ = await snapshotManager.Received(1).ShouldCreateSnapshotAsync("test-domain", 4, 100);
    }

    [Fact]
    public async Task ProcessCommandAsync_DomainRejection_AlsoChecksSnapshotInterval() {
        // Arrange -- M2 fix: rejection events count toward snapshot interval
        (AggregateActor actor, IActorStateManager stateManager, _, IDomainServiceInvoker invoker, ISnapshotManager snapshotManager) = CreateActorWithAllMocks();
        ConfigureNoDuplicate(stateManager);
        ConfigureExistingAggregate(stateManager, 3);

        var rejectionResult = DomainResult.Rejection(new Hexalith.EventStore.Contracts.Events.IRejectionEvent[] { new TestRejectionEvent() });
        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(rejectionResult);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert -- ShouldCreateSnapshotAsync called on rejection path
        _ = await snapshotManager.Received(1).ShouldCreateSnapshotAsync("test-domain", Arg.Any<long>(), Arg.Is<long>(0));
    }

    // Test event types for domain invocation tests
    private sealed record TestEvent : Hexalith.EventStore.Contracts.Events.IEventPayload;

    private sealed record TestRejectionEvent : Hexalith.EventStore.Contracts.Events.IRejectionEvent;
}
