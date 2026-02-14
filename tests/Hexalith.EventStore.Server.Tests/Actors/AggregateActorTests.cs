namespace Hexalith.EventStore.Server.Tests.Actors;

using System.Reflection;

using Dapr.Actors;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;

using Microsoft.Extensions.Logging;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

using EventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;

public class AggregateActorTests
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
        CorrelationId: correlationId ?? Guid.NewGuid().ToString(),
        CausationId: causationId,
        UserId: "system",
        Extensions: null);

    private static (AggregateActor Actor, IActorStateManager StateManager, ILogger<AggregateActor> Logger, IDomainServiceInvoker Invoker) CreateActorWithMockState()
    {
        var stateManager = Substitute.For<IActorStateManager>();
        var logger = Substitute.For<ILogger<AggregateActor>>();
        var invoker = Substitute.For<IDomainServiceInvoker>();
        var host = ActorHost.CreateForTest<AggregateActor>(
            new ActorTestOptions { ActorId = new ActorId("test-tenant:test-domain:agg-001") });
        var actor = new AggregateActor(host, logger, invoker);

        // Set the mock state manager via reflection (Dapr runtime normally sets this)
        PropertyInfo? prop = typeof(Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance);
        prop?.SetValue(actor, stateManager);

        // Default: domain service returns NoOp
        invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>())
            .Returns(DomainResult.NoOp());

        return (actor, stateManager, logger, invoker);
    }

    private static void ConfigureNoDuplicate(IActorStateManager stateManager)
    {
        stateManager.TryGetStateAsync<IdempotencyRecord>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(false, default!));

        // Default: new aggregate (no metadata) -- Step 3 returns null state
        stateManager.TryGetStateAsync<AggregateMetadata>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(false, default!));
    }

    private static void ConfigureExistingAggregate(IActorStateManager stateManager, int eventCount)
    {
        var metadata = new AggregateMetadata(eventCount, DateTimeOffset.UtcNow, null);
        stateManager.TryGetStateAsync<AggregateMetadata>(
            "test-tenant:test-domain:agg-001:metadata", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, metadata));

        string keyPrefix = "test-tenant:test-domain:agg-001:events:";
        for (int i = 1; i <= eventCount; i++)
        {
            int seq = i;
            var evt = new EventEnvelope(
                "agg-001", "test-tenant", "test-domain", seq, DateTimeOffset.UtcNow,
                $"corr-{seq}", $"cause-{seq}", "user-1", "1.0.0", "OrderCreated", "json",
                [1, 2, 3], null);
            stateManager.TryGetStateAsync<EventEnvelope>($"{keyPrefix}{seq}", Arg.Any<CancellationToken>())
                .Returns(new ConditionalValue<EventEnvelope>(true, evt));
        }
    }

    private static void ConfigureDuplicate(IActorStateManager stateManager, string causationId, string correlationId)
    {
        var record = new IdempotencyRecord(causationId, correlationId, true, null, DateTimeOffset.UtcNow);
        stateManager.TryGetStateAsync<IdempotencyRecord>($"idempotency:{causationId}", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(true, record));

        // Default for other keys
        stateManager.TryGetStateAsync<IdempotencyRecord>(
            Arg.Is<string>(s => s != $"idempotency:{causationId}"), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(false, default!));
    }

    [Fact]
    public async Task ProcessCommandAsync_ValidCommand_ReturnsAccepted()
    {
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
    public async Task ProcessCommandAsync_ValidCommand_ReturnsCorrelationId()
    {
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
    public async Task ProcessCommandAsync_ValidCommand_LogsCommandReceipt()
    {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, ILogger<AggregateActor> logger, _) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        await actor.ProcessCommandAsync(envelope);

        // Assert
        logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("received command")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task ProcessCommandAsync_NewCommand_StoresIdempotencyRecord()
    {
        // Arrange
        string correlationId = "corr-new";
        (AggregateActor actor, IActorStateManager stateManager, _, _) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        CommandEnvelope envelope = CreateTestEnvelope(correlationId: correlationId, causationId: "cause-new");

        // Act
        await actor.ProcessCommandAsync(envelope);

        // Assert
        await stateManager.Received(1).SetStateAsync(
            "idempotency:cause-new",
            Arg.Is<IdempotencyRecord>(r => r.CausationId == "cause-new" && r.CorrelationId == correlationId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommandAsync_NewCommand_CallsSaveStateAsync()
    {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, _) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        await actor.ProcessCommandAsync(envelope);

        // Assert
        await stateManager.Received(1).SaveStateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommandAsync_DuplicateCommand_ReturnsCachedResult()
    {
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
    public async Task ProcessCommandAsync_DuplicateCommand_DoesNotCallSaveState()
    {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, _) = CreateActorWithMockState();
        ConfigureDuplicate(stateManager, "cause-dup", "corr-dup");
        CommandEnvelope envelope = CreateTestEnvelope(correlationId: "corr-dup", causationId: "cause-dup");

        // Act
        await actor.ProcessCommandAsync(envelope);

        // Assert
        await stateManager.DidNotReceive().SaveStateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommandAsync_DuplicateCommand_DoesNotStoreNewRecord()
    {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, _) = CreateActorWithMockState();
        ConfigureDuplicate(stateManager, "cause-dup", "corr-dup");
        CommandEnvelope envelope = CreateTestEnvelope(correlationId: "corr-dup", causationId: "cause-dup");

        // Act
        await actor.ProcessCommandAsync(envelope);

        // Assert
        await stateManager.DidNotReceive().SetStateAsync(
            Arg.Any<string>(),
            Arg.Any<IdempotencyRecord>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommandAsync_DuplicateCommand_LogsDuplicateDetection()
    {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, ILogger<AggregateActor> logger, _) = CreateActorWithMockState();
        ConfigureDuplicate(stateManager, "cause-dup", "corr-dup");
        CommandEnvelope envelope = CreateTestEnvelope(correlationId: "corr-dup", causationId: "cause-dup");

        // Act
        await actor.ProcessCommandAsync(envelope);

        // Assert
        logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Duplicate command detected")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task ProcessCommandAsync_NullCausationId_UseCorrelationIdAsFallback()
    {
        // Arrange
        string correlationId = "corr-fallback";
        (AggregateActor actor, IActorStateManager stateManager, _, _) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        CommandEnvelope envelope = CreateTestEnvelope(correlationId: correlationId, causationId: null);

        // Act
        await actor.ProcessCommandAsync(envelope);

        // Assert -- should use correlationId as the key since causationId is null
        await stateManager.Received(1).TryGetStateAsync<IdempotencyRecord>(
            $"idempotency:{correlationId}",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommandAsync_TenantMismatch_ReturnsRejection()
    {
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
    public async Task ProcessCommandAsync_TenantMismatch_DoesNotExecuteSteps3Through5()
    {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, ILogger<AggregateActor> logger, _) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        CommandEnvelope envelope = CreateTestEnvelope(tenantId: "wrong-tenant");

        // Act
        await actor.ProcessCommandAsync(envelope);

        // Assert -- Step 3 logs "State rehydrated" at Information level; should NOT appear after tenant mismatch
        logger.DidNotReceive().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("State rehydrated")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task ProcessCommandAsync_TenantMismatch_StoresRejectionInIdempotencyCache()
    {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, _) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        CommandEnvelope envelope = CreateTestEnvelope(tenantId: "wrong-tenant", causationId: "cause-mismatch");

        // Act
        await actor.ProcessCommandAsync(envelope);

        // Assert
        await stateManager.Received(1).SetStateAsync(
            "idempotency:cause-mismatch",
            Arg.Is<IdempotencyRecord>(r => r.Accepted == false),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommandAsync_TenantMismatch_CallsSaveStateAsync()
    {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, _) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        CommandEnvelope envelope = CreateTestEnvelope(tenantId: "wrong-tenant");

        // Act
        await actor.ProcessCommandAsync(envelope);

        // Assert
        await stateManager.Received(1).SaveStateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommandAsync_MatchingTenant_ProceedsToStep3()
    {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, ILogger<AggregateActor> logger, _) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        CommandEnvelope envelope = CreateTestEnvelope(); // test-tenant matches actor ID test-tenant:test-domain:agg-001

        // Act
        await actor.ProcessCommandAsync(envelope);

        // Assert -- Step 3 should have logged state rehydration at Information level
        logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("State rehydrated")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task ProcessCommandAsync_DuplicateRejectedCommand_ReturnsCachedRejection()
    {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, _) = CreateActorWithMockState();
        var record = new IdempotencyRecord("cause-rejected", "corr-rejected", false, "TenantMismatch: ...", DateTimeOffset.UtcNow);
        stateManager.TryGetStateAsync<IdempotencyRecord>("idempotency:cause-rejected", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(true, record));
        CommandEnvelope envelope = CreateTestEnvelope(tenantId: "wrong-tenant", correlationId: "corr-rejected", causationId: "cause-rejected");

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeFalse();
    }

    [Fact]
    public async Task ProcessCommandAsync_TenantMismatch_RejectionContainsBothTenants()
    {
        // Arrange (F-SA6)
        (AggregateActor actor, IActorStateManager stateManager, _, _) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        CommandEnvelope envelope = CreateTestEnvelope(tenantId: "tenant-b");

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldContain("tenant-b");
        result.ErrorMessage.ShouldContain("test-tenant");
    }

    // === Story 3.4: State Rehydration Tests ===

    [Fact]
    public async Task ProcessCommandAsync_NewAggregate_RehydratesNullState()
    {
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
    public async Task ProcessCommandAsync_ExistingAggregate_RehydratesState()
    {
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
    public async Task ProcessCommandAsync_StateRehydrated_ProceedsToStep4()
    {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, ILogger<AggregateActor> logger, _) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        await actor.ProcessCommandAsync(envelope);

        // Assert -- Step 4 domain service invocation logs result at Information level
        logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Domain service result")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task ProcessCommandAsync_UnknownEvent_PropagatesException()
    {
        // Arrange -- metadata says 2 events but event 2 is missing (simulates gap)
        (AggregateActor actor, IActorStateManager stateManager, _, _) = CreateActorWithMockState();
        stateManager.TryGetStateAsync<IdempotencyRecord>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(false, default!));

        var metadata = new AggregateMetadata(2, DateTimeOffset.UtcNow, null);
        stateManager.TryGetStateAsync<AggregateMetadata>(
            "test-tenant:test-domain:agg-001:metadata", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, metadata));

        string keyPrefix = "test-tenant:test-domain:agg-001:events:";
        var evt1 = new EventEnvelope("agg-001", "test-tenant", "test-domain", 1, DateTimeOffset.UtcNow,
            "corr-1", "cause-1", "user-1", "1.0.0", "OrderCreated", "json", [1], null);
        stateManager.TryGetStateAsync<EventEnvelope>($"{keyPrefix}1", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(true, evt1));
        stateManager.TryGetStateAsync<EventEnvelope>($"{keyPrefix}2", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(false, default!));

        CommandEnvelope envelope = CreateTestEnvelope();

        // Act & Assert -- MissingEventException propagates from actor
        await Should.ThrowAsync<MissingEventException>(() => actor.ProcessCommandAsync(envelope));
    }

    [Fact]
    public async Task ProcessCommandAsync_StateRehydration_LogsStateType()
    {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, ILogger<AggregateActor> logger, _) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        ConfigureExistingAggregate(stateManager, 2);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        await actor.ProcessCommandAsync(envelope);

        // Assert
        logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("State rehydrated")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    // === Story 3.5: Domain Service Invocation Tests ===

    [Fact]
    public async Task ProcessCommandAsync_DomainSuccess_ProceedsToStep5()
    {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, ILogger<AggregateActor> logger, IDomainServiceInvoker invoker) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        var successResult = DomainResult.Success(new Hexalith.EventStore.Contracts.Events.IEventPayload[] { new TestEvent() });
        invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(successResult);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeTrue();
        logger.Received().Log(
            LogLevel.Debug,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Step 5")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task ProcessCommandAsync_DomainRejection_ReturnsRejectionResult()
    {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, IDomainServiceInvoker invoker) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        var rejectionResult = DomainResult.Rejection(new Hexalith.EventStore.Contracts.Events.IRejectionEvent[] { new TestRejectionEvent() });
        invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(rejectionResult);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldContain("Domain rejection");
    }

    [Fact]
    public async Task ProcessCommandAsync_DomainRejection_StoresInIdempotencyCache()
    {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, IDomainServiceInvoker invoker) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        var rejectionResult = DomainResult.Rejection(new Hexalith.EventStore.Contracts.Events.IRejectionEvent[] { new TestRejectionEvent() });
        invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(rejectionResult);
        CommandEnvelope envelope = CreateTestEnvelope(causationId: "cause-reject");

        // Act
        await actor.ProcessCommandAsync(envelope);

        // Assert
        await stateManager.Received(1).SetStateAsync(
            "idempotency:cause-reject",
            Arg.Is<IdempotencyRecord>(r => r.Accepted == false),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommandAsync_DomainNoOp_ReturnsAccepted()
    {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, IDomainServiceInvoker invoker) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(DomainResult.NoOp());
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeTrue();
    }

    [Fact]
    public async Task ProcessCommandAsync_DomainServiceNotFound_PropagatesException()
    {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, IDomainServiceInvoker invoker) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>())
            .ThrowsAsync(new DomainServiceNotFoundException("test-tenant", "test-domain"));
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act & Assert
        await Should.ThrowAsync<DomainServiceNotFoundException>(() => actor.ProcessCommandAsync(envelope));
    }

    [Fact]
    public async Task ProcessCommandAsync_DomainInfrastructureFailure_PropagatesException()
    {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, IDomainServiceInvoker invoker) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>())
            .ThrowsAsync(new HttpRequestException("Service unavailable"));
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act & Assert
        await Should.ThrowAsync<HttpRequestException>(() => actor.ProcessCommandAsync(envelope));
    }

    [Fact]
    public async Task ProcessCommandAsync_DomainInvocation_LogsResultType()
    {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, ILogger<AggregateActor> logger, IDomainServiceInvoker invoker) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        var successResult = DomainResult.Success(new Hexalith.EventStore.Contracts.Events.IEventPayload[] { new TestEvent() });
        invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(successResult);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        await actor.ProcessCommandAsync(envelope);

        // Assert
        logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Domain service result") && o.ToString()!.Contains("Success")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    // Test event types for domain invocation tests
    private sealed record TestEvent : Hexalith.EventStore.Contracts.Events.IEventPayload;

    private sealed record TestRejectionEvent : Hexalith.EventStore.Contracts.Events.IRejectionEvent;
}
