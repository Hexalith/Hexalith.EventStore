using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Streams;
using Hexalith.EventStore.Testing.Fakes;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using static Hexalith.EventStore.Server.Tests.Actors.AggregateActorTestHelper;

namespace Hexalith.EventStore.Server.Tests.Actors;

public sealed class AggregateActorCommandSourceTests
{
    [Fact]
    public async Task LostCommandReplyAndActorRestartReuseOriginalAndNextTurnCapturesConsecutiveHead()
    {
        var backend = new InMemoryStateManager(); var command = CreateTestEnvelope();
        var authority = Substitute.For<ICompleteCommandSourceAuthority>(); authority.Requires(command.AggregateIdentity).Returns(true);
        authority.AuthorizeAsync(command.AggregateIdentity, Arg.Any<CancellationToken>()).Returns(
            new CompleteCommandSourceAuthorization(command.AggregateIdentity, "epoch-1", DateTimeOffset.UtcNow.AddMinutes(2)));
        using var services = new ServiceCollection().AddSingleton(authority).BuildServiceProvider();
        var observed = new List<CompleteCommandSource>();
        ActorTestContext Create()
        {
            var context = CreateActor(stateManager: backend, serviceProvider: services);
            context.Invoker.InvokeCompleteSourceAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object>(), Arg.Any<CompleteCommandSource>(),
                Arg.Any<string?>(), Arg.Any<TrustedEffectOrigin?>(), Arg.Any<CancellationToken>()).Returns(call =>
                { observed.Add(call.Arg<CompleteCommandSource>()); return DomainResult.Success([new TestEvent()]); });
            return context;
        }
        var first = await Create().Actor.ProcessCommandAsync(command); first.Accepted.ShouldBeTrue();
        var restarted = Create(); var duplicate = await restarted.Actor.ProcessCommandAsync(command);
        duplicate.ShouldBe(first);
        observed.Count.ShouldBe(1);
        (await restarted.Actor.ProcessCommandAsync(command with { MessageId = "second-command", CorrelationId = "second-correlation" })).Accepted.ShouldBeTrue();
        observed.Select(item => item.Proof.HeadSequence).ShouldBe(new long[] { 0, 1 });
        observed[1].Events.Single().SequenceNumber.ShouldBe(1);
        observed[0].Proof.SourceIncarnation.ShouldBe(observed[1].Proof.SourceIncarnation);
        backend.CommittedState.Values.OfType<EventEnvelope>().Count().ShouldBe(2);
        backend.CommittedState[command.AggregateIdentity.MetadataKey].ShouldBeOfType<AggregateMetadata>().SourceProof!.HeadSequence.ShouldBe(2);
    }
    [Theory]
    [InlineData("current")]
    [InlineData("revoked")]
    [InlineData("head")]
    [InlineData("incarnation")]
    public async Task EnrolledEmptyPrefixIsDurableAndComparedBeforeAnyAppend(string change)
    {
        var backend = new InMemoryStateManager(); var command = CreateTestEnvelope();
        var authority = Substitute.For<ICompleteCommandSourceAuthority>();
        authority.Requires(command.AggregateIdentity).Returns(true);
        bool current = true;
        authority.AuthorizeAsync(command.AggregateIdentity, Arg.Any<CancellationToken>()).Returns(_ => current
            ? new CompleteCommandSourceAuthorization(command.AggregateIdentity, "epoch-1", DateTimeOffset.UtcNow.AddMinutes(2)) : null);
        using var services = new ServiceCollection().AddSingleton(authority).BuildServiceProvider();
        var context = CreateActor(stateManager: backend, serviceProvider: services);
        CompleteCommandSource? captured = null;
        context.Invoker.InvokeCompleteSourceAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object>(), Arg.Any<CompleteCommandSource>(),
            Arg.Any<string?>(), Arg.Any<TrustedEffectOrigin?>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            captured = call.Arg<CompleteCommandSource>(); captured.Events.ShouldBeEmpty(); captured.Proof.HeadSequence.ShouldBe(0);
            var state = call.Arg<object>().ShouldBeOfType<DomainServiceCurrentState>(); state.UsedSnapshot.ShouldBeFalse(); state.CurrentSequence.ShouldBe(0);
            var metadata = backend.CommittedState[command.AggregateIdentity.MetadataKey].ShouldBeOfType<AggregateMetadata>();
            SourceProofCodec.ToNative(metadata.SourceProof!).ShouldBe(captured.Proof);
            if (change == "revoked") current = false;
            if (change is "head" or "incarnation")
            {
                var proof = change == "head" ? metadata.SourceProof! with { HeadSequence = 1 } : SourceProofCodec.CreateSeed(command.AggregateIdentity, "01ARZ3NDEKTSV4RRFFQ69G5FAV");
                await backend.SetStateAsync(command.AggregateIdentity.MetadataKey, metadata with { CurrentSequence = change == "head" ? 1 : 0, SourceProof = proof });
                await backend.SaveStateAsync();
            }
            return DomainResult.Success([new TestEvent()]);
        });
        var result = await context.Actor.ProcessCommandAsync(command);
        captured.ShouldNotBeNull(); result.Accepted.ShouldBe(change == "current");
        backend.CommittedState.Values.OfType<EventEnvelope>().Count().ShouldBe(change == "current" ? 1 : 0);
        await context.SnapshotManager.DidNotReceive().LoadSnapshotAsync(Arg.Any<AggregateIdentity>(), Arg.Any<Dapr.Actors.Runtime.IActorStateManager>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
