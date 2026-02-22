
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.Extensions.Logging;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Actors;

public class ActorStateMachineTests {
    private const string PipelineKeyPrefix = "test-tenant:test-domain:agg-001:pipeline:";
    private const string CorrelationId = "corr-123";

    private static PipelineState CreateTestPipelineState(
        CommandStatus stage = CommandStatus.Processing,
        string correlationId = CorrelationId) => new(
        correlationId,
        stage,
        "CreateOrder",
        DateTimeOffset.UtcNow,
        EventCount: null,
        RejectionEventType: null);

    private static ActorStateMachine CreateStateMachine(InMemoryStateManager stateManager) {
        ILogger<ActorStateMachine> logger = Substitute.For<ILogger<ActorStateMachine>>();
        return new ActorStateMachine(stateManager, logger);
    }

    [Fact]
    public async Task Checkpoint_StoresCorrectPipelineState() {
        // Arrange
        var stateManager = new InMemoryStateManager();
        ActorStateMachine sm = CreateStateMachine(stateManager);
        PipelineState state = CreateTestPipelineState();

        // Act
        await sm.CheckpointAsync(PipelineKeyPrefix, state);
        await stateManager.SaveStateAsync(); // Simulate AggregateActor atomic commit

        // Assert
        string expectedKey = $"{PipelineKeyPrefix}{CorrelationId}";
        stateManager.CommittedState.ShouldContainKey(expectedKey);
        var persisted = (PipelineState)stateManager.CommittedState[expectedKey];
        persisted.CorrelationId.ShouldBe(CorrelationId);
        persisted.CurrentStage.ShouldBe(CommandStatus.Processing);
        persisted.CommandType.ShouldBe("CreateOrder");
    }

    [Fact]
    public async Task LoadPipelineState_ExistingPipeline_ReturnsState() {
        // Arrange
        var stateManager = new InMemoryStateManager();
        ActorStateMachine sm = CreateStateMachine(stateManager);
        PipelineState state = CreateTestPipelineState(CommandStatus.EventsStored);
        await sm.CheckpointAsync(PipelineKeyPrefix, state);
        await stateManager.SaveStateAsync();

        // Act
        PipelineState? loaded = await sm.LoadPipelineStateAsync(PipelineKeyPrefix, CorrelationId);

        // Assert
        _ = loaded.ShouldNotBeNull();
        loaded.CurrentStage.ShouldBe(CommandStatus.EventsStored);
    }

    [Fact]
    public async Task LoadPipelineState_NoPipeline_ReturnsNull() {
        // Arrange
        var stateManager = new InMemoryStateManager();
        ActorStateMachine sm = CreateStateMachine(stateManager);

        // Act
        PipelineState? loaded = await sm.LoadPipelineStateAsync(PipelineKeyPrefix, "nonexistent");

        // Assert
        loaded.ShouldBeNull();
    }

    [Fact]
    public async Task Cleanup_RemovesPipelineStateKey() {
        // Arrange
        var stateManager = new InMemoryStateManager();
        ActorStateMachine sm = CreateStateMachine(stateManager);
        PipelineState state = CreateTestPipelineState();
        await sm.CheckpointAsync(PipelineKeyPrefix, state);
        await stateManager.SaveStateAsync();

        // Act
        await sm.CleanupPipelineAsync(PipelineKeyPrefix, CorrelationId);
        await stateManager.SaveStateAsync();

        // Assert
        string key = $"{PipelineKeyPrefix}{CorrelationId}";
        stateManager.CommittedState.ShouldNotContainKey(key);
    }

    [Fact]
    public async Task PipelineKeyPattern_MatchesConvention() {
        // Arrange
        var stateManager = new InMemoryStateManager();
        ActorStateMachine sm = CreateStateMachine(stateManager);
        PipelineState state = CreateTestPipelineState();

        // Act
        await sm.CheckpointAsync(PipelineKeyPrefix, state);
        await stateManager.SaveStateAsync();

        // Assert -- key pattern: {tenant}:{domain}:{aggId}:pipeline:{correlationId}
        string expectedKey = "test-tenant:test-domain:agg-001:pipeline:corr-123";
        stateManager.CommittedState.ShouldContainKey(expectedKey);
    }

    [Fact]
    public async Task Checkpoint_OverwritesExistingState() {
        // Arrange -- simulate stage transition: Processing -> EventsStored
        var stateManager = new InMemoryStateManager();
        ActorStateMachine sm = CreateStateMachine(stateManager);

        PipelineState processingState = CreateTestPipelineState(CommandStatus.Processing);
        await sm.CheckpointAsync(PipelineKeyPrefix, processingState);
        await stateManager.SaveStateAsync();

        // Act
        var eventsStoredState = new PipelineState(
            CorrelationId, CommandStatus.EventsStored, "CreateOrder",
            processingState.StartedAt, EventCount: 3, RejectionEventType: null);
        await sm.CheckpointAsync(PipelineKeyPrefix, eventsStoredState);
        await stateManager.SaveStateAsync();

        // Assert
        string key = $"{PipelineKeyPrefix}{CorrelationId}";
        var persisted = (PipelineState)stateManager.CommittedState[key];
        persisted.CurrentStage.ShouldBe(CommandStatus.EventsStored);
        persisted.EventCount.ShouldBe(3);
    }

    [Fact]
    public async Task Cleanup_NonexistentKey_DoesNotThrow() {
        // Arrange
        var stateManager = new InMemoryStateManager();
        ActorStateMachine sm = CreateStateMachine(stateManager);

        // Act & Assert -- should not throw
        await sm.CleanupPipelineAsync(PipelineKeyPrefix, "nonexistent");
    }
}
