using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Tests.TestUtilities;

using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Actors;

public sealed class CoordinatedCommandActorTests
{
    [Fact]
    public async Task Stale_source_rejects_before_target_mutation_and_exact_retry_replays_the_durable_conflict()
    {
        (CoordinatedCommandActor actor, IActorStateManager state, IAggregateActor source, IAggregateActor target, _) = CreateActor();
        CommandEnvelope command = Command();

        CommandProcessingResult first = await actor.ProcessCommandAsync(command);
        CommandProcessingResult retry = await actor.ProcessCommandAsync(command);

        first.Accepted.ShouldBeFalse();
        first.FailureReason.ShouldBe("ConcurrencyConflict");
        retry.ShouldBe(first);
        await target.DidNotReceive().ProcessCommandAsync(Arg.Any<CommandEnvelope>());
        await state.Received(1).SaveStateAsync(Arg.Any<CancellationToken>());
        await source.Received(1).GetEventsAsync(0);
    }

    [Fact]
    public async Task Same_message_id_in_two_tenants_has_independent_stale_rejection_receipts()
    {
        (CoordinatedCommandActor actor, IActorStateManager state, IAggregateActor source, IAggregateActor target, _) = CreateActor();
        CommandEnvelope firstTenant = Command();
        CommandEnvelope secondTenant = firstTenant with { TenantId = "tenant-b", AggregateId = "tenant-b" };

        CommandProcessingResult first = await actor.ProcessCommandAsync(firstTenant);
        CommandProcessingResult second = await actor.ProcessCommandAsync(secondTenant);
        CommandProcessingResult retry = await actor.ProcessCommandAsync(firstTenant);

        first.Accepted.ShouldBeFalse();
        second.Accepted.ShouldBeFalse();
        first.FailureReason.ShouldBe("ConcurrencyConflict");
        second.FailureReason.ShouldBe("ConcurrencyConflict");
        retry.ShouldBe(first);
        await source.Received(2).GetEventsAsync(0);
        await target.DidNotReceive().ProcessCommandAsync(Arg.Any<CommandEnvelope>());
        await state.Received(2).SaveStateAsync(Arg.Any<CancellationToken>());
        state.ReceivedCalls().Where(call => call.GetMethodInfo().Name == nameof(IActorStateManager.SetStateAsync))
            .Select(call => call.GetArguments()[0]?.ToString()).Distinct(StringComparer.Ordinal).Count().ShouldBe(2);
    }

    [Fact]
    public async Task Previously_committed_fenced_target_reconciles_before_a_new_source_guard()
    {
        (CoordinatedCommandActor actor, IActorStateManager state, IAggregateActor source, IAggregateActor target, _) = CreateActor();
        CommandProcessingResult committed = new(true, CorrelationId: "corr", EventCount: 1);
        target.ReconcileFencedCommandAsync(Arg.Any<FencedCommandEnvelope>())
            .Returns(new IdempotencyCheckResult(IdempotencyCheckOutcome.ExactTerminalDuplicate, committed));

        CommandProcessingResult replay = await actor.ProcessFencedCommandAsync(new FencedCommandEnvelope(Command(), Fence()));

        replay.ShouldBe(committed);
        await source.DidNotReceive().GetEventsAsync(Arg.Any<long>());
        await state.DidNotReceive().SaveStateAsync(Arg.Any<CancellationToken>());
        await target.DidNotReceive().ProcessFencedCommandAsync(Arg.Any<FencedCommandEnvelope>());
    }

    [Fact]
    public async Task Fenced_stale_source_after_a_target_reconciliation_miss_cannot_mutate_tenant_state()
    {
        (CoordinatedCommandActor actor, IActorStateManager state, IAggregateActor source, IAggregateActor target, _) = CreateActor();
        target.ReconcileFencedCommandAsync(Arg.Any<FencedCommandEnvelope>())
            .Returns(new IdempotencyCheckResult(IdempotencyCheckOutcome.Miss, null));

        CommandProcessingResult result = await actor.ProcessFencedCommandAsync(
            new FencedCommandEnvelope(Command(), Fence()));

        result.Accepted.ShouldBeFalse();
        result.FailureReason.ShouldBe("ConcurrencyConflict");
        await target.Received(1).ReconcileFencedCommandAsync(Arg.Any<FencedCommandEnvelope>());
        await source.Received(1).GetEventsAsync(0);
        await target.DidNotReceive().ProcessFencedCommandAsync(Arg.Any<FencedCommandEnvelope>());
        await target.DidNotReceive().ProcessCommandAsync(Arg.Any<CommandEnvelope>());
        await state.Received(1).SaveStateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Invalid_fence_fails_before_coordinator_state_or_target_mutation()
    {
        (CoordinatedCommandActor actor, IActorStateManager state, IAggregateActor source, IAggregateActor target, _) = CreateActor();
        target.ReconcileFencedCommandAsync(Arg.Any<FencedCommandEnvelope>())
            .ThrowsAsync(new InvalidOperationException("invalid fence"));

        await Should.ThrowAsync<InvalidOperationException>(
            () => actor.ProcessFencedCommandAsync(new FencedCommandEnvelope(Command(), Fence())));

        await source.DidNotReceive().GetEventsAsync(Arg.Any<long>());
        await state.DidNotReceive().SetStateAsync(
            Arg.Any<string>(), Arg.Any<CoordinatedCommandRejection>(), Arg.Any<CancellationToken>());
        await target.DidNotReceive().ProcessFencedCommandAsync(Arg.Any<FencedCommandEnvelope>());
    }

    [Fact]
    public async Task Non_fenced_retry_of_a_committed_command_replays_the_target_outcome()
    {
        (CoordinatedCommandActor actor, IActorStateManager state, _, IAggregateActor target, _) = CreateActor();
        CommandEnvelope command = Command();
        CommandProcessingResult committed = new(true, CorrelationId: "corr", EventCount: 1);
        target.GetEventsAsync(0).Returns([new EventEnvelope("event-msg", "tenant-a", "tenant-provider-enablement",
            "tenant-a", "tenant-provider-enablement", 1, 1, DateTimeOffset.UnixEpoch, "corr", command.MessageId,
            "user", "v1", "ProviderDataHandlingDecided", 1, "json", [1], null)]);
        target.ProcessCommandAsync(command).Returns(committed);

        CommandProcessingResult replay = await actor.ProcessCommandAsync(command);

        replay.ShouldBe(committed);
        await target.Received(1).ProcessCommandAsync(command);
        await state.DidNotReceive().SaveStateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Same_message_id_with_divergent_intent_returns_an_identity_conflict()
    {
        (CoordinatedCommandActor actor, _, _, IAggregateActor target, ICoordinatedCommandPolicy policy) = CreateActor();
        policy.GetCommandDigest(Arg.Any<CommandEnvelope>())
            .Returns(call => Convert.ToHexString(call.Arg<CommandEnvelope>().Payload));
        CommandEnvelope command = Command();

        CommandProcessingResult first = await actor.ProcessCommandAsync(command);
        CommandProcessingResult divergent = await actor.ProcessCommandAsync(command with { Payload = [2] });

        first.FailureReason.ShouldBe("ConcurrencyConflict");
        divergent.Accepted.ShouldBeFalse();
        divergent.ErrorMessage.ShouldBe("command_identity_conflict");
        await target.DidNotReceive().ProcessCommandAsync(Arg.Any<CommandEnvelope>());
    }

    private static (CoordinatedCommandActor Actor, IActorStateManager State, IAggregateActor Source,
        IAggregateActor Target, ICoordinatedCommandPolicy Policy) CreateActor()
    {
        var sourceIdentity = new AggregateIdentity("system", "provider-catalog", "entry-1");
        IActorStateManager state = Substitute.For<IActorStateManager>();
        var stored = new Dictionary<string, CoordinatedCommandRejection>(StringComparer.Ordinal);
        state.TryGetStateAsync<CoordinatedCommandRejection>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => stored.TryGetValue(call.ArgAt<string>(0), out CoordinatedCommandRejection? value)
                ? new ConditionalValue<CoordinatedCommandRejection>(true, value)
                : new ConditionalValue<CoordinatedCommandRejection>(false, null!));
        state.SetStateAsync(Arg.Any<string>(), Arg.Any<CoordinatedCommandRejection>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                stored[call.ArgAt<string>(0)] = call.ArgAt<CoordinatedCommandRejection>(1);
                return Task.CompletedTask;
            });
        IAggregateActor source = Substitute.For<IAggregateActor>();
        source.GetEventsAsync(0).Returns([]);
        IAggregateActor target = Substitute.For<IAggregateActor>();
        target.GetEventsAsync(0).Returns([]);
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        factory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(call => call.ArgAt<ActorId>(0).ToString() == sourceIdentity.ActorId ? source : target);
        ICoordinatedCommandPolicy policy = Substitute.For<ICoordinatedCommandPolicy>();
        policy.Claims("tenant-provider-enablement", "DecideProviderDataHandling").Returns(true);
        policy.GetScope(Arg.Any<CommandEnvelope>()).Returns(new CoordinatedCommandScope(sourceIdentity, true));
        policy.GetCommandDigest(Arg.Any<CommandEnvelope>())
            .Returns(call => call.Arg<CommandEnvelope>().TenantId);
        policy.Validate(Arg.Any<CommandEnvelope>(), Arg.Any<IReadOnlyList<ProjectionEventDto>>()).Returns(false);
        var host = ActorHost.CreateForTest<CoordinatedCommandActor>(
            new ActorTestOptions { ActorId = new ActorId(sourceIdentity.ActorId) });
        var actor = new CoordinatedCommandActor(host, factory, Options.Create(new EventStoreActorOptions()),
            [policy], new NoOpEventPayloadProtectionService());
        ActorStateManagerTestHelper.SetStateManager(actor, state);
        return (actor, state, source, target, policy);
    }

    private static CommandEnvelope Command()
        => new("msg", "tenant-a", "tenant-provider-enablement", "tenant-a", "DecideProviderDataHandling",
            [1], "corr", null, "user", null);

    private static IdempotencyExecutionContext Fence()
        => new(1, "admission", 1, "v1", "msg", "corr", "tenant-a", "tenant-provider-enablement",
            "tenant-a", "DecideProviderDataHandling", "proof");
}
