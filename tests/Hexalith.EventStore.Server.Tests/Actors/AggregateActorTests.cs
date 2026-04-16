using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Actors;

using Microsoft.Extensions.Logging;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

using static Hexalith.EventStore.Server.Tests.Actors.AggregateActorTestHelper;

namespace Hexalith.EventStore.Server.Tests.Actors;

public class AggregateActorTests {
    [Fact]
    public async Task ProcessCommandAsync_ValidCommand_ReturnsAccepted() {
        // Arrange
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        CommandProcessingResult result = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeTrue();
    }

    [Fact]
    public async Task ProcessCommandAsync_ValidCommand_ReturnsCorrelationId() {
        // Arrange
        string correlationId = Guid.NewGuid().ToString();
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        CommandEnvelope envelope = CreateTestEnvelope(correlationId: correlationId);

        // Act
        CommandProcessingResult result = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert
        result.CorrelationId.ShouldBe(correlationId);
    }

    [Fact]
    public async Task ProcessCommandAsync_ValidCommand_LogsCommandReceipt() {
        // Arrange
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        _ = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert
        ctx.Logger.Received().Log(
            LogLevel.Debug,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Actor activated")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task ProcessCommandAsync_NewCommand_CallsSaveStateAsync() {
        // Arrange
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        _ = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert -- Story 4.3 adds a final save for the persisted pending-count decrement.
        await ctx.StateManager.Received(3).SaveStateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommandAsync_AdvisoryStatusWriteFails_StillReturnsAccepted() {
        // Arrange -- Rule 12: WriteAdvisoryStatusAsync must swallow exceptions without blocking pipeline
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);

        // Configure status store to throw on every write
        _ = ctx.StatusStore.WriteStatusAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Hexalith.EventStore.Contracts.Commands.CommandStatusRecord>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("DAPR state store unavailable"));

        CommandEnvelope envelope = CreateTestEnvelope();

        // Act -- should NOT throw despite advisory status write failures
        CommandProcessingResult result = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert -- pipeline completed successfully (Rule 12: advisory failures never block)
        result.Accepted.ShouldBeTrue();
    }
}
