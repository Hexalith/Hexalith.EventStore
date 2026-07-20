
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Actors;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using Shouldly;

using static Hexalith.EventStore.Server.Tests.Actors.AggregateActorTestHelper;

namespace Hexalith.EventStore.Server.Tests.Actors;

public class AggregateActorIdempotencyTests {
    [Fact]
    public async Task ProcessCommandAsync_ExpiredCommand_ReturnsTerminalExpiredWithoutDomainExecution() {
        var now = new DateTimeOffset(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(now);
        ActorTestContext ctx = CreateActor(timeProvider: timeProvider);
        CommandEnvelope envelope = CreateTestEnvelope(correlationId: "corr-expired", causationId: "cause-expired");
        var record = new IdempotencyRecord(
            "cause-expired",
            "corr-original",
            true,
            null,
            now.AddHours(-24),
            MessageId: envelope.MessageId,
            CommandType: envelope.CommandType,
            ExpiresAt: now,
            Disposition: IdempotencyRecordDisposition.Terminal);
        _ = ctx.StateManager.TryGetStateAsync<IdempotencyRecord>(
                $"idempotency:{envelope.MessageId}",
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(true, record));

        CommandProcessingResult result = await ctx.Actor.ProcessCommandAsync(envelope);

        result.Accepted.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("idempotency_key_expired");
        await ctx.Invoker.DidNotReceive().InvokeAsync(
            Arg.Any<CommandEnvelope>(),
            Arg.Any<object?>());
    }

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
            $"idempotency:{envelope.MessageId}",
            Arg.Is<IdempotencyRecord>(r =>
                r.MessageId == envelope.MessageId
                && r.CausationId == "cause-new"
                && r.CommandType == envelope.CommandType
                && r.CorrelationId == correlationId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommandAsync_DuplicateCommand_ReturnsCachedResult() {
        // Arrange
        ActorTestContext ctx = CreateActor();
        var original = new CommandProcessingResult(
            Accepted: true,
            ErrorMessage: null,
            CorrelationId: "corr-dup",
            EventCount: 3,
            ResultPayload: "{\"result\":\"cached\"}");
        CommandEnvelope envelope = CreateTestEnvelope(correlationId: "corr-dup", causationId: "cause-dup");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        IdempotencyRecord record = IdempotencyRecord.FromResult(
            new CommandProcessingIdentity(envelope.MessageId, "cause-dup", envelope.CommandType),
            original,
            now,
            now.AddHours(24),
            IdempotencyRecordDisposition.Terminal);
        _ = ctx.StateManager.TryGetStateAsync<IdempotencyRecord>($"idempotency:{envelope.MessageId}", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(true, record));

        // Act
        CommandProcessingResult result = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert
        result.ShouldBe(original);
    }

    [Fact]
    public async Task ProcessCommandAsync_DuplicateCommand_DoesNotCallSaveState() {
        // Arrange
        ActorTestContext ctx = CreateActor();
        CommandEnvelope envelope = CreateTestEnvelope(correlationId: "corr-dup", causationId: "cause-dup");
        ConfigureDuplicate(ctx.StateManager, envelope);

        // Act
        _ = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert
        await ctx.StateManager.DidNotReceive().SaveStateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommandAsync_DuplicateCommand_DoesNotStoreNewRecord() {
        // Arrange
        ActorTestContext ctx = CreateActor();
        CommandEnvelope envelope = CreateTestEnvelope(correlationId: "corr-dup", causationId: "cause-dup");
        ConfigureDuplicate(ctx.StateManager, envelope);

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
        CommandEnvelope envelope = CreateTestEnvelope(correlationId: "corr-dup", causationId: "cause-dup");
        ConfigureDuplicate(ctx.StateManager, envelope);

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
    public async Task ProcessCommandAsync_NullCausationId_UsesMessageIdAsNormalizedCausation() {
        // Arrange
        string correlationId = "corr-fallback";
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        CommandEnvelope envelope = CreateTestEnvelope(correlationId: correlationId, causationId: null);

        // Act
        _ = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert -- message id is both the primary key and normalized causation when causation is absent.
        _ = await ctx.StateManager.Received(1).TryGetStateAsync<IdempotencyRecord>(
            $"idempotency:{envelope.MessageId}",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommandAsync_DuplicateRejectedCommand_ReturnsCachedRejection() {
        // Arrange
        ActorTestContext ctx = CreateActor();
        var original = new CommandProcessingResult(
            Accepted: false,
            ErrorMessage: "Backpressure exceeded: 17 pending commands (threshold: 10)",
            CorrelationId: "corr-rejected",
            EventCount: 0,
            ResultPayload: null,
            BackpressureExceeded: true,
            BackpressurePendingCount: 17,
            BackpressureThreshold: 10);
        CommandEnvelope envelope = CreateTestEnvelope(correlationId: "corr-rejected", causationId: "cause-rejected");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        IdempotencyRecord record = IdempotencyRecord.FromResult(
            new CommandProcessingIdentity(envelope.MessageId, "cause-rejected", envelope.CommandType),
            original,
            now,
            now.AddHours(24),
            IdempotencyRecordDisposition.Terminal);
        _ = ctx.StateManager.TryGetStateAsync<IdempotencyRecord>($"idempotency:{envelope.MessageId}", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(true, record));

        // Act
        CommandProcessingResult result = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert
        result.ShouldBe(original);
    }
}
