namespace Hexalith.EventStore.Server.Tests.Actors;

using System.Reflection;

using Dapr.Actors;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Actors;

using Microsoft.Extensions.Logging;

using NSubstitute;

using Shouldly;

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

    private static (AggregateActor Actor, IActorStateManager StateManager, ILogger<AggregateActor> Logger) CreateActorWithMockState()
    {
        var stateManager = Substitute.For<IActorStateManager>();
        var logger = Substitute.For<ILogger<AggregateActor>>();
        var host = ActorHost.CreateForTest<AggregateActor>(
            new ActorTestOptions { ActorId = new ActorId("test-tenant:test-domain:agg-001") });
        var actor = new AggregateActor(host, logger);

        // Set the mock state manager via reflection (Dapr runtime normally sets this)
        PropertyInfo? prop = typeof(Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance);
        prop?.SetValue(actor, stateManager);

        return (actor, stateManager, logger);
    }

    private static void ConfigureNoDuplicate(IActorStateManager stateManager)
    {
        stateManager.TryGetStateAsync<IdempotencyRecord>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(false, default!));
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
        (AggregateActor actor, IActorStateManager stateManager, _) = CreateActorWithMockState();
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
        (AggregateActor actor, IActorStateManager stateManager, _) = CreateActorWithMockState();
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
        (AggregateActor actor, IActorStateManager stateManager, ILogger<AggregateActor> logger) = CreateActorWithMockState();
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
        (AggregateActor actor, IActorStateManager stateManager, _) = CreateActorWithMockState();
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
        (AggregateActor actor, IActorStateManager stateManager, _) = CreateActorWithMockState();
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
        (AggregateActor actor, IActorStateManager stateManager, _) = CreateActorWithMockState();
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
        (AggregateActor actor, IActorStateManager stateManager, _) = CreateActorWithMockState();
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
        (AggregateActor actor, IActorStateManager stateManager, _) = CreateActorWithMockState();
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
        (AggregateActor actor, IActorStateManager stateManager, ILogger<AggregateActor> logger) = CreateActorWithMockState();
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
        (AggregateActor actor, IActorStateManager stateManager, _) = CreateActorWithMockState();
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
        (AggregateActor actor, IActorStateManager stateManager, _) = CreateActorWithMockState();
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
        (AggregateActor actor, IActorStateManager stateManager, ILogger<AggregateActor> logger) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        CommandEnvelope envelope = CreateTestEnvelope(tenantId: "wrong-tenant");

        // Act
        await actor.ProcessCommandAsync(envelope);

        // Assert -- Step 3 stub logs "State rehydration" at Debug level; should NOT appear
        logger.DidNotReceive().Log(
            LogLevel.Debug,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("State rehydration")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task ProcessCommandAsync_TenantMismatch_StoresRejectionInIdempotencyCache()
    {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _) = CreateActorWithMockState();
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
        (AggregateActor actor, IActorStateManager stateManager, _) = CreateActorWithMockState();
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
        (AggregateActor actor, IActorStateManager stateManager, ILogger<AggregateActor> logger) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        CommandEnvelope envelope = CreateTestEnvelope(); // test-tenant matches actor ID test-tenant:test-domain:agg-001

        // Act
        await actor.ProcessCommandAsync(envelope);

        // Assert -- Step 3 stub should have logged
        logger.Received().Log(
            LogLevel.Debug,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("State rehydration")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task ProcessCommandAsync_DuplicateRejectedCommand_ReturnsCachedRejection()
    {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _) = CreateActorWithMockState();
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
        (AggregateActor actor, IActorStateManager stateManager, _) = CreateActorWithMockState();
        ConfigureNoDuplicate(stateManager);
        CommandEnvelope envelope = CreateTestEnvelope(tenantId: "tenant-b");

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldContain("tenant-b");
        result.ErrorMessage.ShouldContain("test-tenant");
    }
}
