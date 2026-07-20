using System.Text;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Actors;

public class AggregateActorFencingTests
{
    [Fact]
    public async Task ProcessFencedCommandAsync_TamperedFence_RejectsBeforeStateOrDomainAccess()
    {
        IdempotencyExecutionContextProtector protector = CreateProtector();
        ActorTestContext actorContext = AggregateActorTestHelper.CreateActor(
            executionContextProtector: protector);
        CommandEnvelope envelope = AggregateActorTestHelper.CreateTestEnvelope(
            correlationId: "trace-a");
        SubmitCommand command = ToSubmitCommand(envelope);
        IdempotencyExecutionContext executionContext = await protector.ProtectAsync(
            "test-tenant:v1:key-digest",
            7,
            "v1",
            command);

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => actorContext.Actor.ProcessFencedCommandAsync(
                new FencedCommandEnvelope(
                    envelope,
                    executionContext with { FencingToken = 8 })));

        actorContext.StateManager.ReceivedCalls().ShouldBeEmpty();
        _ = actorContext.Invoker.DidNotReceiveWithAnyArgs()
            .InvokeAsync(default!, default);
    }

    [Fact]
    public async Task ProcessFencedCommandAsync_MissingValidator_RejectsBeforeDomainAccess()
    {
        IdempotencyExecutionContextProtector protector = CreateProtector();
        ActorTestContext actorContext = AggregateActorTestHelper.CreateActor();
        CommandEnvelope envelope = AggregateActorTestHelper.CreateTestEnvelope(
            correlationId: "trace-a");
        IdempotencyExecutionContext executionContext = await protector.ProtectAsync(
            "test-tenant:v1:key-digest",
            7,
            "v1",
            ToSubmitCommand(envelope));

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => actorContext.Actor.ProcessFencedCommandAsync(
                new FencedCommandEnvelope(envelope, executionContext)));

        _ = actorContext.Invoker.DidNotReceiveWithAnyArgs()
            .InvokeAsync(default!, default);
    }

    [Fact]
    public async Task ReconcileFencedCommandAsync_ExactResult_ReadsOnlyIdempotencyState()
    {
        IdempotencyExecutionContextProtector protector = CreateProtector();
        ActorTestContext actorContext = AggregateActorTestHelper.CreateActor(
            executionContextProtector: protector);
        CommandEnvelope envelope = AggregateActorTestHelper.CreateTestEnvelope(
            correlationId: "trace-a");
        string causationId = envelope.CausationId ?? envelope.MessageId;
        var stored = new IdempotencyRecord(
            causationId,
            envelope.CorrelationId,
            true,
            null,
            DateTimeOffset.UtcNow,
            EventCount: 1,
            MessageId: envelope.MessageId,
            CommandType: envelope.CommandType,
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(1),
            Disposition: IdempotencyRecordDisposition.Terminal);
        _ = actorContext.StateManager.TryGetStateAsync<IdempotencyRecord>(
                $"idempotency:{envelope.MessageId}",
                Arg.Any<CancellationToken>())
            .Returns(new Dapr.Actors.Runtime.ConditionalValue<IdempotencyRecord>(true, stored));
        IdempotencyExecutionContext executionContext = await protector.ProtectAsync(
            "test-tenant:v1:key-digest",
            7,
            "v1",
            ToSubmitCommand(envelope));

        IdempotencyCheckResult result = await actorContext.Actor.ReconcileFencedCommandAsync(
            new FencedCommandEnvelope(envelope, executionContext));

        result.Outcome.ShouldBe(IdempotencyCheckOutcome.ExactTerminalDuplicate);
        result.Result.ShouldBe(stored.ToResult());
        _ = actorContext.Invoker.DidNotReceiveWithAnyArgs().InvokeAsync(default!, default);
        await actorContext.StateManager.DidNotReceive().SetStateAsync(
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
        await actorContext.StateManager.DidNotReceive().SaveStateAsync(Arg.Any<CancellationToken>());
    }

    private static IdempotencyExecutionContextProtector CreateProtector()
        => new(
            new StaticIdempotencyDigestKeyProvider(
                "v1",
                new Dictionary<string, byte[]>(StringComparer.Ordinal)
                {
                    ["v1"] = Encoding.UTF8.GetBytes("0123456789abcdef0123456789abcdef"),
                },
                []));

    private static SubmitCommand ToSubmitCommand(CommandEnvelope envelope)
        => new(
            envelope.MessageId,
            envelope.TenantId,
            envelope.Domain,
            envelope.AggregateId,
            envelope.CommandType,
            envelope.Payload,
            envelope.CorrelationId,
            envelope.UserId,
            envelope.Extensions);
}
