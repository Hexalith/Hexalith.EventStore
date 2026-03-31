
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Actors;

using Microsoft.Extensions.Logging;

using NSubstitute;

using Shouldly;

using static Hexalith.EventStore.Server.Tests.Actors.AggregateActorTestHelper;

namespace Hexalith.EventStore.Server.Tests.Actors;

public class AggregateActorIdempotencyTests {
    [Fact]
    public async Task ProcessCommandAsync_NewCommand_StoresIdempotencyRecord() {
        // Arrange
        string correlationId = "corr-new";
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        CommandEnvelope envelope = CreateTestEnvelope(correlationId: correlationId, causationId: "cause-new");

        // Act
        _ = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert
        await ctx.StateManager.Received(1).SetStateAsync(
            "idempotency:cause-new",
            Arg.Is<IdempotencyRecord>(r => r.CausationId == "cause-new" && r.CorrelationId == correlationId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommandAsync_DuplicateCommand_ReturnsCachedResult() {
        // Arrange
        ActorTestContext ctx = CreateActor();
        ConfigureDuplicate(ctx.StateManager, "cause-dup", "corr-dup");
        CommandEnvelope envelope = CreateTestEnvelope(correlationId: "corr-dup", causationId: "cause-dup");

        // Act
        CommandProcessingResult result = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeTrue();
        result.CorrelationId.ShouldBe("corr-dup");
    }

    [Fact]
    public async Task ProcessCommandAsync_DuplicateCommand_DoesNotCallSaveState() {
        // Arrange
        ActorTestContext ctx = CreateActor();
        ConfigureDuplicate(ctx.StateManager, "cause-dup", "corr-dup");
        CommandEnvelope envelope = CreateTestEnvelope(correlationId: "corr-dup", causationId: "cause-dup");

        // Act
        _ = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert
        await ctx.StateManager.DidNotReceive().SaveStateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommandAsync_DuplicateCommand_DoesNotStoreNewRecord() {
        // Arrange
        ActorTestContext ctx = CreateActor();
        ConfigureDuplicate(ctx.StateManager, "cause-dup", "corr-dup");
        CommandEnvelope envelope = CreateTestEnvelope(correlationId: "corr-dup", causationId: "cause-dup");

        // Act
        _ = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert
        await ctx.StateManager.DidNotReceive().SetStateAsync(
            Arg.Any<string>(),
            Arg.Any<IdempotencyRecord>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommandAsync_DuplicateCommand_LogsDuplicateDetection() {
        // Arrange
        ActorTestContext ctx = CreateActor();
        ConfigureDuplicate(ctx.StateManager, "cause-dup", "corr-dup");
        CommandEnvelope envelope = CreateTestEnvelope(correlationId: "corr-dup", causationId: "cause-dup");

        // Act
        _ = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert
        ctx.Logger.Received().Log(
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
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        CommandEnvelope envelope = CreateTestEnvelope(correlationId: correlationId, causationId: null);

        // Act
        _ = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert -- should use correlationId as the key since causationId is null
        _ = await ctx.StateManager.Received(1).TryGetStateAsync<IdempotencyRecord>(
            $"idempotency:{correlationId}",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommandAsync_DuplicateRejectedCommand_ReturnsCachedRejection() {
        // Arrange
        ActorTestContext ctx = CreateActor();
        var record = new IdempotencyRecord("cause-rejected", "corr-rejected", false, "TenantMismatch: ...", DateTimeOffset.UtcNow);
        _ = ctx.StateManager.TryGetStateAsync<IdempotencyRecord>("idempotency:cause-rejected", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(true, record));
        CommandEnvelope envelope = CreateTestEnvelope(tenantId: "wrong-tenant", correlationId: "corr-rejected", causationId: "cause-rejected");

        // Act
        CommandProcessingResult result = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeFalse();
    }
}
