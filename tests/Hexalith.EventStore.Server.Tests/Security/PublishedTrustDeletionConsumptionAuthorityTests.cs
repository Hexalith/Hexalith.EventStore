using System.Text.Json;

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
        TenantGovernanceGuardState later = admitted.State with { Revision = holdReceipt.GuardHighWater, Holds = [hold], Receipts = [.. admitted.State.Receipts, holdReceipt] };
        TenantGovernanceGuardState shared = later with
        {
            Revision = restricted.DispatchGuardRevision,
            Deletions = [admitted.Deletion with { Batches = [admitted.Batch, restrictedBatch] }],
            Receipts = [.. later.Receipts, .. restrictedReceipts],
        };
        var guard = Substitute.For<IGovernanceScopeGuard>();
        guard.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(later);
        var authority = new PublishedTrustDeletionConsumptionAuthority([admitted.Trust], guard);
        (await authority.VerifyDispatchAsync(admitted.Request, TestContext.Current.CancellationToken)).ShouldBeTrue();
        guard.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(shared);
        (await authority.VerifyDispatchAsync(admitted.Request, TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await authority.VerifyDispatchAsync(restricted, TestContext.Current.CancellationToken)).ShouldBeFalse();
        await guard.DidNotReceive().ExecuteAsync(Arg.Any<GovernanceGuardTransition>(), Arg.Any<IReadOnlyList<GuardedStateMutation>>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Missing or malformed hold membership and release evidence cannot be treated as a nonrestrictive guard.</summary>
    [Theory]
    [InlineData("null-hold")]
    [InlineData("null-hold-scope")]
    [InlineData("foreign-hold-scope")]
    [InlineData("unknown-hold-scope")]
    [InlineData("ambiguous-hold-scope")]
    [InlineData("empty-hold-id")]
    [InlineData("empty-disposition")]
    [InlineData("duplicate-hold")]
    [InlineData("missing-release")]
    [InlineData("active-release")]
    [InlineData("future-registration")]
    [InlineData("empty-registration-operation")]
    [InlineData("empty-registration-receipt")]
    [InlineData("empty-registration-digest")]
    [InlineData("bad-registration-digest")]
    [InlineData("oversized-registration-operation")]
    [InlineData("oversized-registration-receipt")]
    [InlineData("future-dispatch")]
    [InlineData("null-deletion-scope")]
    [InlineData("foreign-deletion-scope")]
    [InlineData("unknown-deletion-scope")]
    [InlineData("ambiguous-deletion-scope")]
    public async Task MalformedGuardScopesAndUnattributedHoldsDeny(string mutation)
    {
        PublishedTrustDispatchFixture world = PublishedTrustDispatchFixture.Create();
        GovernanceProtocolReceipt registration = PublishedTrustDispatchFixture.HoldReceipt();
        var hold = new GovernanceHoldState("hold-later", world.Scope, "policy-a", true, true, "");
        GovernanceDeletionState deletion = world.Deletion;
        long revision = registration.GuardHighWater;
        switch (mutation)
        {
            case "null-hold": hold = null!; break;
            case "null-hold-scope": hold = hold with { Scope = null! }; break;
            case "foreign-hold-scope": hold = hold with { Scope = world.Scope with { TenantId = "tenant-b" } }; break;
            case "unknown-hold-scope": hold = hold with { Scope = world.Scope with { Kind = "FutureScope" } }; break;
            case "ambiguous-hold-scope": hold = hold with { Scope = world.Scope with { SourceConversationId = "conversation-a" } }; break;
            case "empty-hold-id": hold = hold with { HoldId = "" }; break;
            case "empty-disposition": hold = hold with { DispositionVersion = "" }; break;
            case "missing-release": hold = hold with { Active = false }; break;
            case "active-release": hold = hold with { ReleaseReceiptId = "release-authority" }; break;
            case "future-registration": registration = registration with { GuardHighWater = revision + 1 }; break;
            case "empty-registration-operation": registration = registration with { OperationId = "" }; break;
            case "empty-registration-receipt": registration = registration with { ReceiptId = "" }; break;
            case "empty-registration-digest": registration = registration with { IntentDigest = "" }; break;
            case "bad-registration-digest": registration = registration with { IntentDigest = new string('Z', 64) }; break;
            case "oversized-registration-operation": registration = registration with { OperationId = new string('O', 2049) }; break;
            case "oversized-registration-receipt": registration = registration with { ReceiptId = new string('R', 2049) }; break;
            case "future-dispatch": revision = world.Request.DispatchGuardRevision - 1; hold = hold with { Active = false, ReleaseReceiptId = "release-authority" }; break;
            case "null-deletion-scope": deletion = deletion with { Scope = null! }; break;
            case "foreign-deletion-scope": deletion = deletion with { Scope = world.Scope with { TenantId = "tenant-b" } }; break;
            case "unknown-deletion-scope": deletion = deletion with { Scope = world.Scope with { Kind = "FutureScope" } }; break;
            case "ambiguous-deletion-scope": deletion = deletion with { Scope = world.Scope with { SourceConversationId = "conversation-a" } }; break;
        }

        world.Guard.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(world.State with
        {
            Revision = revision,
            Holds = mutation == "duplicate-hold" ? [hold, hold] : [hold],
            Deletions = [deletion],
            Receipts = [.. world.State.Receipts, registration],
        });

        (await world.Authority.VerifyDispatchAsync(world.Request, TestContext.Current.CancellationToken)).ShouldBeFalse();
        await world.Guard.DidNotReceive().ExecuteAsync(Arg.Any<GovernanceGuardTransition>(), Arg.Any<IReadOnlyList<GuardedStateMutation>>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Actual guard commits bind registration to its hold id and preserve independent release evidence without revoking a prior dispatch.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("caller-substituted-reference")]
    public async Task PersistedGuardHoldTransitionsPreserveEarlierDispatch(string callerReference)
    {
        PublishedTrustDispatchFixture world = PublishedTrustDispatchFixture.Create();
        var guard = new GovernanceGuardFixture();
        TenantGovernanceGuardState state = world.State with
        {
            InstallationId = guard.Backend.Target.InstallationId,
            Deletions = [world.Deletion with { AdmissionFenceGuardRevision = 1 }],
        };
        guard.Backend.Stored[guard.Backend.Key(guard.Backend.Target.GuardCellId)] = JsonSerializer.SerializeToUtf8Bytes(
            new GuardedStateCell(state.TenantId, state.InstallationId, guard.Backend.Target.GuardCellId, state.Revision, JsonSerializer.SerializeToUtf8Bytes(state)));
        var authority = new PublishedTrustDeletionConsumptionAuthority([world.Trust], guard.Owner);
        (await authority.VerifyDispatchAsync(world.Request, TestContext.Current.CancellationToken)).ShouldBeTrue();

        var hold = new GovernanceHoldCommand("hold-later", world.Scope, "policy-a", "");
        GovernanceGuardTransition registration = guard.Command(GovernanceGuardOperation.RegisterHold, hold: hold, reference: callerReference);
        GovernanceProtocolReceipt committed = await guard.Apply(registration);
        committed.Status.ShouldBe("Committed");
        committed.ReferenceId.ShouldBe(hold.HoldId);
        guard.State.Holds.Single().Active.ShouldBeTrue();
        guard.State.Receipts.Single(receipt => receipt.ReceiptId == committed.ReceiptId).ShouldBe(committed);
        (await authority.VerifyDispatchAsync(world.Request, TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await guard.Apply(registration)).ShouldBe(committed);

        const string releaseAuthority = "independent-acknowledged-release";
        GovernanceProtocolReceipt released = await guard.Apply(guard.Command(GovernanceGuardOperation.ReleaseHold,
            hold: hold with { ReleaseReceiptId = releaseAuthority }));
        released.Status.ShouldBe("Committed");
        guard.State.Holds.Single().ReleaseReceiptId.ShouldBe(releaseAuthority);
        guard.State.Holds.Single().Active.ShouldBeFalse();
        released.ReceiptId.ShouldNotBe(releaseAuthority);
        (await authority.VerifyDispatchAsync(world.Request, TestContext.Current.CancellationToken)).ShouldBeTrue();
    }

    /// <summary>Distinct accepted and containment batches retain independent active credentials under one deletion request.</summary>
    [Fact]
    public async Task DistinctDispatchedBatchesAdmitTheirOwnSoleAttestations()
    {
        var (first, second, secondBatch, secondReceipts) = PublishedTrustDispatchFixture.CreateHoldPair();
        TenantGovernanceGuardState shared = first.State with
        {
            Revision = second.DispatchGuardRevision,
            Deletions = [first.Deletion with { Batches = [first.Batch, secondBatch] }],
            Receipts = [.. first.State.Receipts, .. secondReceipts],
        };
        first.Guard.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(shared);

        (await first.Authority.VerifyDispatchAsync(first.Request, TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await first.Authority.VerifyDispatchAsync(second, TestContext.Current.CancellationToken)).ShouldBeTrue();
    }

    /// <summary>An alternate current credential for the exact batch is ambiguous even if it has another digest or key version.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DuplicateBatchAttestationsDenyEvenWithDifferentKeyOrDigest(bool anotherDeletion)
    {
        PublishedTrustDispatchFixture world = PublishedTrustDispatchFixture.Create();
        GovernanceBatchState alternate = world.Batch with { AttestationOrdinal = 2, CapabilityKeyVersion = "key-v2", AttestationDigest = new string('A', 64) };
        TenantGovernanceGuardState ambiguous = world.State with
        {
            Deletions = anotherDeletion
                ? [world.Deletion, world.Deletion with { RequestId = "request-2", Batches = [alternate] }]
                : [world.Deletion with { Batches = [world.Batch, alternate] }],
        };
        world.Guard.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(ambiguous);

        (await world.Authority.VerifyDispatchAsync(world.Request, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    /// <summary>Guard receipts cannot change the signed issue revision or reuse one transition for issue and dispatch.</summary>
    [Theory]
    [InlineData("issue")]
    [InlineData("operation")]
    public async Task CorrelatedReceiptsCannotSubstituteTheSignedIssueOrDispatchOperation(string mutation)
    {
        PublishedTrustDispatchFixture world = PublishedTrustDispatchFixture.Create();
        GovernanceProtocolReceipt issue = world.State.Receipts[0];
        GovernanceProtocolReceipt dispatch = world.State.Receipts[1];
        DeletionBatchConsumptionRequest request = world.Request;
        GovernanceBatchState batch = world.Batch;
        if (mutation == "issue")
        {
            request = request with { CommittedIssuedGuardRevision = issue.GuardHighWater + 1 };
            batch = batch with { IssuedGuardRevision = request.CommittedIssuedGuardRevision };
            issue = issue with { GuardHighWater = request.CommittedIssuedGuardRevision };
        }
        else { dispatch = dispatch with { OperationId = issue.OperationId }; }
        world.Guard.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(world.WithBatch(batch) with { Receipts = [issue, dispatch] });

        (await world.Authority.VerifyDispatchAsync(request, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    /// <summary>Mutating a shared guard object between reads cannot turn a stale first observation into a matching pair.</summary>
    [Fact]
    public async Task MutationOfSharedGuardCollectionsBetweenReadsDenies()
    {
        PublishedTrustDispatchFixture world = PublishedTrustDispatchFixture.Create();
        var deletions = new List<GovernanceDeletionState> { world.Deletion with { IntegrityCompromised = true } };
        TenantGovernanceGuardState shared = world.State with { Deletions = deletions };
        int reads = 0;
        world.Guard.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            if (++reads == 2) { deletions[0] = world.Deletion; }
            return shared;
        });

        (await world.Authority.VerifyDispatchAsync(world.Request, TestContext.Current.CancellationToken)).ShouldBeFalse();
        reads.ShouldBe(2);
    }

    /// <summary>The compared guard bytes remain authoritative when a shared collection changes during admission.</summary>
    [Fact]
    public async Task AdmissionUsesTheComparedGuardSnapshotWhenSharedHoldsChange()
    {
        PublishedTrustDispatchFixture world = PublishedTrustDispatchFixture.Create();
        var hold = new GovernanceHoldState("hold-later", world.Scope, "policy-a", true, true, "");
        var holds = Substitute.For<IReadOnlyList<GovernanceHoldState>>();
        int enumerations = 0;
        holds.Count.Returns(1);
        holds[0].Returns(hold);
        holds.GetEnumerator().Returns(_ => (++enumerations <= 2 ? new[] { hold } : Array.Empty<GovernanceHoldState>())
            .AsEnumerable().GetEnumerator());
        world.Guard.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(world.State with
        {
            Holds = holds,
            Receipts = [.. world.State.Receipts, PublishedTrustDispatchFixture.HoldReceipt() with { GuardHighWater = 9 }],
        });

        (await world.Authority.VerifyDispatchAsync(world.Request, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    /// <summary>The trusted public key must be exactly one complete SPKI object.</summary>
    [Fact]
    public async Task PublicAnchorWithTrailingBytesDeniesBeforeGuardRead()
    {
        PublishedTrustDispatchFixture world = PublishedTrustDispatchFixture.Create();
        var authority = new PublishedTrustDeletionConsumptionAuthority(
            [world.Trust with { SubjectPublicKeyInfo = [.. world.Trust.SubjectPublicKeyInfo, 0] }], world.Guard);

        (await authority.VerifyDispatchAsync(world.Request, TestContext.Current.CancellationToken)).ShouldBeFalse();
        await world.Guard.DidNotReceiveWithAnyArgs().ReadAsync(default!, default);
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
