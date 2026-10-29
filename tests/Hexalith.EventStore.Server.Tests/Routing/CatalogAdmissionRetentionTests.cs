using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

public sealed class CatalogAdmissionRetentionTests
{
    [Theory]
    [InlineData("capability")]
    [InlineData("execution")]
    [InlineData("proof")]
    [InlineData("completed-cancellation")]
    public async Task LocalBoundaryProofFailuresAndPostCompletionCancellationReleaseTheirFences(string scenario)
    {
        var fixture = new RouteCatalogTestFixture(); var candidate = fixture.Candidate(); await fixture.ActivateAsync(candidate);
        var keys = Substitute.For<IIdempotencyDigestKeyProvider>();
        keys.GetKeyRingAsync(Arg.Any<CancellationToken>()).Returns(_ => ValueTask.FromResult(new IdempotencyDigestKeyRing("development-v1",
            new Dictionary<string, byte[]> { ["development-v1"] = new byte[32] }, [])));
        var actors = Substitute.For<IActorProxyFactory>(); var actor = Substitute.For<IIdempotencyAdmissionActor>();
        actors.CreateActorProxy<IIdempotencyAdmissionActor>(Arg.Any<Dapr.Actors.ActorId>(), Arg.Any<string>(), Arg.Any<ActorProxyOptions>()).Returns(actor);
        var protector = new IdempotencyExecutionContextProtector(keys, actors);
        var command = new SubmitCommand("01M61800000000000000000001", "tenant-a", "counter", "aggregate", "increment-counter", "{}"u8.ToArray(), "correlation", "user");
        var context = await protector.ProtectAsync("admission-actor", 1, "development-v1", command);
        if (scenario == "proof") context = context with { Proof = new string('0', context.Proof.Length) };
        var session = new IdempotencyAdmissionSession("admission-actor", 1, IdempotencyAdmissionDecision.Execute, ExecutionContext: context,
            ExecutionMessageId: command.MessageId, ExecutionCorrelationId: command.CorrelationId, CatalogRootDigest: candidate.RootDigest, CatalogEpoch: 1);
        var coordinator = new IdempotencyAdmissionCoordinator(fixture.Authorities[0], actors, new IdempotencyKeyProtector(keys), Substitute.For<IIdempotencyIntentAdapterRegistry>(), protector);
        if (scenario == "completed-cancellation")
        {
            using var cancellation = new CancellationTokenSource();
            actor.BeginAsync(Arg.Any<IdempotencyAdmissionTransitionRequest>()).Returns(_ => { cancellation.Cancel(); return Task.CompletedTask; });
            await Should.ThrowAsync<OperationCanceledException>(() => coordinator.BeginAsync(session, cancellation.Token));
            await actor.Received(1).BeginAsync(Arg.Any<IdempotencyAdmissionTransitionRequest>());
        }
        else
        {
            var selected = scenario == "proof" ? command : command with { AggregateId = "different-aggregate" };
            Task Validate() => scenario == "capability" ? coordinator.ValidateExecutionCapabilityAsync(session, selected) : coordinator.ValidateExecutionAsync(session, selected);
            await Should.ThrowAsync<InvalidOperationException>(Validate);
            actors.ReceivedCalls().ShouldBeEmpty(); actor.ReceivedCalls().ShouldBeEmpty();
        }
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
        (await fixture.Authorities[0].ReadbackAsync()).Serving.ShouldBeTrue();
    }
}
