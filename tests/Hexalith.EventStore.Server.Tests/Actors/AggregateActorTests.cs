using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Tests.TestUtilities;

using Microsoft.Extensions.Logging;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

using static Hexalith.EventStore.Server.Tests.Actors.AggregateActorTestHelper;

namespace Hexalith.EventStore.Server.Tests.Actors;

public class AggregateActorTests {
    [Fact]
    public async Task ProcessCommandAsync_AuthenticatedEmptySourceSeedRehydratesAsNewWithoutReadingAnEnvelope()
    {
        ActorTestContext context = CreateActor();
        ConfigureNoDuplicate(context.StateManager);
        CommandEnvelope command = CreateTestEnvelope();
        var metadata = new Hexalith.EventStore.Server.Events.AggregateMetadata(0, DateTimeOffset.UnixEpoch, null)
        { SourceProof = Hexalith.EventStore.Server.Events.SourceProofCodec.CreateSeed(command.AggregateIdentity, "01ARZ3NDEKTSV4RRFFQ69G5FAV") };
        context.StateManager.TryGetStateAsync<Hexalith.EventStore.Server.Events.AggregateMetadata>(
            command.AggregateIdentity.MetadataKey, Arg.Any<CancellationToken>())
            .Returns(new Dapr.Actors.Runtime.ConditionalValue<Hexalith.EventStore.Server.Events.AggregateMetadata>(true, metadata));
        CommandProcessingResult result = await context.Actor.ProcessCommandAsync(command).ConfigureAwait(true);
        result.Accepted.ShouldBeTrue();
        await context.Invoker.Received(1).InvokeAsync(command, null, Arg.Any<CancellationToken>()).ConfigureAwait(true);
        await context.StateManager.DidNotReceiveWithAnyArgs().TryGetStateAsync<Hexalith.EventStore.Server.Events.EventEnvelope>(default!, default)
            .ConfigureAwait(true);
    }

    [Fact]
    public async Task ProcessCommandAsync_PreProcessingDeferralLeavesExecutionAvailableWithoutTerminalEvidence()
    {
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        var command = CreateTestEnvelope();
        ctx.Invoker.InvokeAsync(command, null, Arg.Any<CancellationToken>()).Returns(
            Task.FromException<Hexalith.EventStore.Contracts.Results.DomainResult>(
                new Hexalith.EventStore.Server.DomainServices.DomainServiceAdmissionDeferredException()),
            Task.FromResult(Hexalith.EventStore.Contracts.Results.DomainResult.NoOp()));
        var refusal = await ctx.Actor.ProcessCommandAsync(command);
        refusal.AdmissionDeferredBeforeProcessing.ShouldBeTrue();
        refusal.EventCount.ShouldBe(0);
        refusal.Accepted.ShouldBeFalse();
        ctx.EventPublisher.ReceivedCalls().ShouldBeEmpty();
        ctx.DeadLetterPublisher.ReceivedCalls().ShouldBeEmpty();
        await ctx.StatusStore.DidNotReceive().WriteStatusAsync(command.TenantId, command.MessageId,
            Arg.Is<CommandStatusRecord>(s => s.Status == CommandStatus.Completed || s.Status == CommandStatus.Rejected),
            Arg.Any<CancellationToken>());
        var retry = await ctx.Actor.ProcessCommandAsync(command);
        retry.Accepted.ShouldBeTrue();
        await ctx.Invoker.Received(2).InvokeAsync(command, null, Arg.Any<CancellationToken>());
    }

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
        var logEntries = new List<LogEntry>();
        ActorTestContext ctx = CreateActor(logger: new TestLogger<AggregateActor>(logEntries));
        ConfigureNoDuplicate(ctx.StateManager);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        _ = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert
        logEntries.ShouldContain(entry =>
            entry.Level == LogLevel.Debug
            && entry.Message == $"Actor activated: ActorId=test-tenant:test-domain:agg-001, CorrelationId={envelope.CorrelationId}, CausationId={envelope.MessageId}, TenantId=test-tenant, Domain=test-domain, AggregateId=agg-001, CommandType=CreateOrder, Stage=ActorActivated");
    }

    [Fact]
    public async Task ProcessCommandAsync_NewCommand_CallsSaveStateAsync() {
        // Arrange
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        _ = await ctx.Actor.ProcessCommandAsync(envelope);

        // Admission and terminal cleanup each commit once; terminal cleanup owns the counter update.
        // Confirmed advisory acknowledgement adds one save after the unchanged terminal batch.
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
