using System.Text.Json;
using Dapr.Actors;
using Dapr.Actors.Runtime;
using Hexalith.EventStore.Client.Security;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Identity;
using Hexalith.EventStore.Server.Tests.TestUtilities;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Identity;

public sealed class ActorRegistryTests
{
    private const string ActorIdValue = "01HX0000000000000000000001";
    private static ActorRegistryState Empty() => new(new(), new(), new());
    private static ActorRegistryMutation Mutation(string operation = "enroll", string alias = "A", long revision = 0)
        => new(IdentityActorRegistryActor.RegistryNamespace, operation, new string(alias[0], 64), ActorIdValue, revision, true, true, ActorIdValue);

    [Fact]
    public void ReplacementAlias_PreservesActorAndRetiredAliasThroughSerializedRestart()
    {
        (ActorRegistryState state, ActorRegistryEntry first) = ActorRegistryTransitions.Apply(Empty(), Mutation());
        (state, ActorRegistryEntry linked) = ActorRegistryTransitions.Apply(state,
            Mutation("replace", "B", first.Revision) with { RetiredAliasDigest = new string('A', 64) });
        ActorRegistryState restored = JsonSerializer.Deserialize<ActorRegistryState>(JsonSerializer.Serialize(state))!;
        restored.Aliases[new string('A', 64)].Active.ShouldBeFalse();
        restored.Aliases[new string('B', 64)].Active.ShouldBeTrue();
        restored.Aliases[new string('B', 64)].ActorId.ShouldBe(first.ActorId);
        restored.Actors[ActorIdValue].ActorId.ShouldBe(linked.ActorId);
        ActorRegistryTransitions.Apply(restored, Mutation()).Result.ShouldBe(first);
    }

    [Fact]
    public void NonCanonicalActorSpelling_Denies()
    {
        Should.Throw<InvalidOperationException>(() => ActorRegistryTransitions.Apply(Empty(), Mutation() with { ActorId = ActorIdValue.ToLowerInvariant() }));
    }

    [Fact]
    public void AliasOwnershipAndOperationIntent_AreImmutable()
    {
        (ActorRegistryState state, _) = ActorRegistryTransitions.Apply(Empty(), Mutation());
        Should.Throw<InvalidOperationException>(() => ActorRegistryTransitions.Apply(state,
            Mutation("other") with { ActorId = "01HX0000000000000000000002" }));
        Should.Throw<InvalidOperationException>(() => ActorRegistryTransitions.Apply(state,
            Mutation() with { Active = false }));
    }

    [Theory]
    [InlineData("party-reader", "identity-registry", "01HX0000000000000000000001")]
    [InlineData("registry-service", "party", "01HX0000000000000000000001")]
    [InlineData("registry-service", "identity-registry", "01HX0000000000000000000002")]
    public async Task WrongSourcePurposeOrProvenance_DeniesBeforeStateLookup(string source, string domain, string operatorActor)
    {
        IIdentityAdmissionProof verifier = Substitute.For<IIdentityAdmissionProof>();
        verifier.Verify(Arg.Any<string>(), Arg.Any<IdentityAdmissionScope>()).Returns(call => domain == "identity-registry"
            ? new IdentityAdmissionEvidence(call.Arg<IdentityAdmissionScope>(), source, operatorActor, null, 0, false,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(1), 1) : null);
        IActorStateManager manager = Substitute.For<IActorStateManager>();
        var actor = new IdentityActorRegistryActor(ActorHost.CreateForTest<IdentityActorRegistryActor>(new ActorTestOptions
            { ActorId = new ActorId(IdentityActorRegistryActor.RegistryNamespace) }), verifier,
            Options.Create(new ActorRegistryTrustOptions { WriterSources = ["registry-service"], ReaderSources = ["registry-service"] }));
        ActorStateManagerTestHelper.SetStateManager(actor, manager);
        await Should.ThrowAsync<InvalidOperationException>(() => actor.MutateAsync(Mutation(), "proof"));
        await manager.DidNotReceiveWithAnyArgs().TryGetStateAsync<ActorRegistryState>(default!, default);
    }
}
