using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.PayloadProtection;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Security;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Security;

/// <summary>Component evidence for published-trust dispatch checks. Fixture keys are not installed anchors or Available evidence.</summary>
public sealed class PublishedTrustDeletionConsumptionAuthorityTests
{
    /// <summary>A valid detached ES256, one published entry, and a fresh guard with one active attestation and committed dispatch admit the batch.</summary>
    [Fact]
    public async Task ValidDispatchReadsTheGuardTwiceAndDoesNotDestroy()
    {
        PublishedTrustDispatchFixture world = PublishedTrustDispatchFixture.Create();
        (await world.Authority.VerifyDispatchAsync(world.Request, TestContext.Current.CancellationToken)).ShouldBeTrue();
        await world.Guard.Received(2).ReadAsync(world.Request.Capability.TenantId, Arg.Any<CancellationToken>());
        await world.Guard.DidNotReceive().ExecuteAsync(Arg.Any<GovernanceGuardTransition>(), Arg.Any<IReadOnlyList<GuardedStateMutation>>(), Arg.Any<CancellationToken>());
        (await world.Authority.VerifyDispatchAsync(world.Request, TestContext.Current.CancellationToken)).ShouldBeTrue();
        await world.Guard.Received(4).ReadAsync(world.Request.Capability.TenantId, Arg.Any<CancellationToken>());
    }

    /// <summary>Absence, substitution, revocation, a mismatched batch, and an unreadable guard deny without a guard effect.</summary>
    [Fact]
    public async Task AbsentTrustOrGuardDeniesWithoutDestruction()
    {
        PublishedTrustDispatchFixture world = PublishedTrustDispatchFixture.Create();
        var empty = new PublishedTrustDeletionConsumptionAuthority([], world.Guard);
        (await empty.VerifyDispatchAsync(world.Request, TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await new PublishedTrustDeletionConsumptionAuthority([world.Trust with { IsRevoked = true }], world.Guard)
            .VerifyDispatchAsync(world.Request, TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await new PublishedTrustDeletionConsumptionAuthority([world.Trust, world.Trust], world.Guard)
            .VerifyDispatchAsync(world.Request, TestContext.Current.CancellationToken)).ShouldBeFalse();
        string tampered = world.Request.DetachedJws[..^1] + (world.Request.DetachedJws[^1] == 'A' ? "B" : "A");
        (await world.Authority.VerifyDispatchAsync(world.Request with { DetachedJws = tampered }, TestContext.Current.CancellationToken)).ShouldBeFalse();

        world.Guard.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((TenantGovernanceGuardState?)null);
        (await world.Authority.VerifyDispatchAsync(world.Request, TestContext.Current.CancellationToken)).ShouldBeFalse();
        world.Guard.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<TenantGovernanceGuardState?>(new InvalidOperationException("Guard read unavailable.")));
        (await world.Authority.VerifyDispatchAsync(world.Request, TestContext.Current.CancellationToken)).ShouldBeFalse();

        world.Guard.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(world.State with { Deletions = [] });
        (await world.Authority.VerifyDispatchAsync(world.Request, TestContext.Current.CancellationToken)).ShouldBeFalse();
        world.Guard.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(world.WithBatch(world.Batch with { AttestationOrdinal = 2 }));
        (await world.Authority.VerifyDispatchAsync(world.Request, TestContext.Current.CancellationToken)).ShouldBeFalse();
        world.Guard.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(world.State with { CompromisedKeyVersions = [world.Request.Capability.CapabilityKeyVersion] });
        (await world.Authority.VerifyDispatchAsync(world.Request, TestContext.Current.CancellationToken)).ShouldBeFalse();
        world.Guard.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(world.State, world.State with { Revision = world.State.Revision + 1 });
        (await world.Authority.VerifyDispatchAsync(world.Request, TestContext.Current.CancellationToken)).ShouldBeFalse();
        await world.Guard.DidNotReceive().ExecuteAsync(Arg.Any<GovernanceGuardTransition>(), Arg.Any<IReadOnlyList<GuardedStateMutation>>(), Arg.Any<CancellationToken>());
        (await world.Authority.AuthorizeOperationAsync("tenant-a", "batch-a", "ReserveAndConsumeDeletionBatch", TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await world.Authority.ValidateStateAsync("tenant-a", 1, "digest", TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await world.Authority.VerifyAdmissionBlockAsync(null!, TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await world.Authority.VerifyRevocationAsync(null!, TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await world.Authority.VerifyActivationAsync(null!, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    /// <summary>A hold ordered after a committed dispatch does not cancel that batch. Another batch whose dispatch is not earlier stays restricted.</summary>
    [Fact]
    public async Task LaterHoldDoesNotCancelTheDispatchedBatchAndRestrictsAnother()
    {
        (PublishedTrustDispatchFixture admitted, DeletionBatchConsumptionRequest restricted, GovernanceBatchState restrictedBatch, IReadOnlyList<GovernanceProtocolReceipt> restrictedReceipts) = PublishedTrustDispatchFixture.CreateHoldPair();
        var hold = new GovernanceHoldState("hold-later", admitted.Scope, "policy-a", true, true, "");
        GovernanceProtocolReceipt holdReceipt = PublishedTrustDispatchFixture.HoldReceipt();
        TenantGovernanceGuardState later = admitted.State with { Holds = [hold], Receipts = [.. admitted.State.Receipts, holdReceipt] };
        TenantGovernanceGuardState shared = later with
        {
            Deletions = [admitted.Deletion with { Batches = [admitted.Batch, restrictedBatch] }],
            Receipts = [.. later.Receipts, .. restrictedReceipts],
        };
        var guard = Substitute.For<IGovernanceScopeGuard>();
        guard.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(later);
        var authority = new PublishedTrustDeletionConsumptionAuthority([admitted.Trust], guard);
        (await authority.VerifyDispatchAsync(admitted.Request, TestContext.Current.CancellationToken)).ShouldBeTrue();
        guard.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(shared);
        (await authority.VerifyDispatchAsync(restricted, TestContext.Current.CancellationToken)).ShouldBeFalse();
        await guard.DidNotReceive().ExecuteAsync(Arg.Any<GovernanceGuardTransition>(), Arg.Any<IReadOnlyList<GuardedStateMutation>>(), Arg.Any<CancellationToken>());
    }

    /// <summary>The engine opt-in registers only the two actors. Missing custody and authority services deny the first effect.</summary>
    [Fact]
    public async Task OptInResolvesActorsWithoutCreatingCustodyAndDeniesTheFirstEffect()
    {
        var defaults = new ServiceCollection();
        defaults.AddEventStoreServer(new ConfigurationBuilder().Build());
        using ServiceProvider defaultProvider = defaults.BuildServiceProvider();
        defaultProvider.GetRequiredService<IEventPayloadProtectionService>().ShouldBeOfType<NoOpEventPayloadProtectionService>();
        ActorNames(defaultProvider).ShouldNotContain(DeletionConsumptionActor.ActorTypeName);
        ActorNames(defaultProvider).ShouldNotContain(InteractionOccurrenceRegistryActor.ActorTypeName);

        var services = new ServiceCollection();
        services.AddInteractionPayloadProtection();
        using ServiceProvider provider = services.BuildServiceProvider();
        ActorNames(provider).ShouldContain(DeletionConsumptionActor.ActorTypeName);
        ActorNames(provider).ShouldContain(InteractionOccurrenceRegistryActor.ActorTypeName);
        provider.GetService<IInteractionOccurrenceKeyProvider>().ShouldBeNull();
        provider.GetService<IInteractionProtectionBindingProvider>().ShouldBeNull();
        provider.GetService<IAtomicDeletionManifestProvider>().ShouldBeNull();
        provider.GetService<IDeletionConsumptionAuthority>().ShouldBeNull();
        Should.Throw<InvalidOperationException>(() => provider.GetRequiredService<IEventPayloadProtectionService>());

        DeletionConsumptionActor consumption = DeletionConsumptionFixture.Create(new InMemoryStateManager());
        (await consumption.RegisterAsync(DeletionConsumptionFixture.Request())).Status.ShouldBe(DeletionConsumptionStatus.Unavailable);
        InteractionOccurrenceRegistryActor registry = InteractionOccurrenceFixture.Create(new InMemoryStateManager(), null);
        (await registry.ReserveAsync(InteractionOccurrenceFixture.Request())).Status.ShouldBe(InteractionOccurrenceReservationStatus.Unavailable);
    }

    private static IEnumerable<string> ActorNames(ServiceProvider provider)
        => provider.GetRequiredService<IOptions<ActorRuntimeOptions>>().Value.Actors.Select(registration => registration.Type.ActorTypeName);
}
