// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base sections 8-10, 13-15; lifecycle amendment sections 1-8.
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>V064-V068 and amendment restart, cutoff, stale-claim and explicit-clearing proof boundaries.</summary>
public sealed class LifecycleDrainTests
{
    private const string Operation = "01J00000000000000000000101";

    [Fact]
    public async Task V064_V065_BarrierBlocksReadsAndCompletionNeedsExactAcknowledgementAfterAFullWait()
    {
        ReservationTestFixture fixture = await ReservationTestFixture.CreateAsync();
        await using var cache = new PayloadKeyCache(timeProvider: fixture.Clock);
        int waits = 0;
        KeyLifecycleCoordinator lifecycle = Coordinator(fixture, cache, (duration, _) => { duration.ShouldBe(TimeSpan.FromSeconds(60)); waits++; fixture.Clock.Advance(duration); return ValueTask.CompletedTask; });
        InvalidationDrainV1 drain = await lifecycle.BeginAsync(fixture.Identity, Approval(fixture.Identity), Operation, 2);
        drain.MemberCutoff.ShouldBe(1u);
        var claims = new PayloadReconciliationClaims(fixture.Store, fixture.Clock);
        ReconciliationV1 claim = (await claims.TryClaimAsync(new(Operation, fixture.Scope)))!.Value;
        (await lifecycle.ResumeAsync(fixture.Identity, claim)).ShouldBeFalse();
        waits.ShouldBe(1);
        (await fixture.Store.ReadAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(fixture.Scope)))!.Value.State.ShouldBe("Invalidating");
        await lifecycle.AcknowledgeAsync(fixture.Identity, Operation);
        fixture.Clock.Advance(TimeSpan.FromSeconds(30));
        claim = (await claims.TryClaimAsync(new(Operation, fixture.Scope)))!.Value;
        (await lifecycle.ResumeAsync(fixture.Identity, claim)).ShouldBeTrue();
        waits.ShouldBe(2); // A resumed drain never reuses an earlier wait or a wall-clock timestamp.
        LifecycleV1 terminal = (await fixture.Store.ReadAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(fixture.Scope)))!.Value;
        terminal.State.ShouldBe("Invalidated"); terminal.Epoch.ShouldBe(3ul);
        (await new PayloadProtectionCatalogs(fixture.Store, fixture.Clock).ReadWorkAsync(fixture.Root)).Head.Value.EntryCount.ShouldBe(0u);
    }

    [Fact]
    public async Task CutoffSurvivesLostBarrierAcknowledgementAndExcludesLaterRegistrations()
    {
        ReservationTestFixture fixture = await ReservationTestFixture.CreateAsync();
        await using var cache = new PayloadKeyCache(timeProvider: fixture.Clock);
        KeyLifecycleCoordinator lifecycle = Coordinator(fixture, cache);
        fixture.Store.LoseNextAcknowledgement = true;
        InvalidationDrainV1 drain = await lifecycle.BeginAsync(fixture.Identity, Approval(fixture.Identity), Operation, 2);
        var catalogs = new PayloadProtectionCatalogs(fixture.Store, fixture.Clock);
        MemberCatalogSnapshot members = await catalogs.ReadMembersAsync(fixture.Root);
        await fixture.Store.CommitAsync(catalogs.AppendMember(members, 1, "01J00000000000000000000102"));
        (await lifecycle.BeginAsync(fixture.Identity, Approval(fixture.Identity), Operation, 2)).ShouldBe(drain);
        await lifecycle.AcknowledgeAsync(fixture.Identity, Operation);
        ReconciliationV1 claim = (await new PayloadReconciliationClaims(fixture.Store, fixture.Clock).TryClaimAsync(new(Operation, fixture.Scope)))!.Value;
        (await lifecycle.ResumeAsync(fixture.Identity, claim)).ShouldBeTrue();
    }

    [Fact]
    public async Task ClearingWaitsForForegroundAndAbandonedBackendBuffersAndNeverRevivesScope()
    {
        var operations = new PayloadLocalOperations();
        var identity = new AggregateIdentity("tenant-a", "parties", "party-01");
        PayloadLocalOperation operation = operations.Begin(identity);
        var lateClearing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        operation.RetainUntil(lateClearing.Task);
        Task clearing = operations.DrainScopeAsync(PayloadProtectionStateKeys.ScopeDigest(identity)).AsTask();
        operation.Token.IsCancellationRequested.ShouldBeTrue();
        clearing.IsCompleted.ShouldBeFalse();
        operation.Dispose();
        clearing.IsCompleted.ShouldBeFalse();
        lateClearing.SetResult();
        await clearing.WaitAsync(TimeSpan.FromSeconds(2));
        Should.Throw<PayloadProtectionStateException>(() => operations.Begin(identity));
    }

    [Fact]
    public async Task StaleWorkerClaimAndShortenedWaitCannotPublishCompletion()
    {
        ReservationTestFixture fixture = await ReservationTestFixture.CreateAsync();
        await using var cache = new PayloadKeyCache(timeProvider: fixture.Clock);
        KeyLifecycleCoordinator lifecycle = Coordinator(fixture, cache);
        await lifecycle.BeginAsync(fixture.Identity, Approval(fixture.Identity), Operation, 2);
        await lifecycle.AcknowledgeAsync(fixture.Identity, Operation);
        var claims = new PayloadReconciliationClaims(fixture.Store, fixture.Clock);
        ReconciliationV1 stale = (await claims.TryClaimAsync(new(Operation, fixture.Scope)))!.Value;
        (await claims.TryClaimAsync(new(Operation, fixture.Scope))).ShouldBeNull();
        fixture.Clock.Advance(TimeSpan.FromSeconds(90));
        ReconciliationV1 replacement = (await claims.TryClaimAsync(new(Operation, fixture.Scope)))!.Value;
        replacement.Attempt.ShouldBe(stale.Attempt + 1);
        await Should.ThrowAsync<PayloadProtectionStateException>(() => lifecycle.ResumeAsync(fixture.Identity, stale).AsTask());
        KeyLifecycleCoordinator shortWait = Coordinator(fixture, cache, (_, _) => { fixture.Clock.Advance(TimeSpan.FromSeconds(59)); return ValueTask.CompletedTask; });
        await Should.ThrowAsync<PayloadProtectionStateException>(() => shortWait.ResumeAsync(fixture.Identity, replacement).AsTask());
        (await fixture.Store.ReadAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(fixture.Scope)))!.Value.State.ShouldBe("Invalidating");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IndexedInvalidationAndDeletionMutateOnlyTheApprovedKeyAndBothIndexes(bool delete)
    {
        ReservationTestFixture fixture = await ReservationTestFixture.CreateAsync();
        var write = await fixture.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        await fixture.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.NotPersisted);
        await using var cache = new PayloadKeyCache(timeProvider: fixture.Clock);
        KeyLifecycleCoordinator lifecycle = Coordinator(fixture, cache);
        await lifecycle.BeginAsync(fixture.Identity, Approval(fixture.Identity), Operation, 2, delete);
        await lifecycle.AcknowledgeAsync(fixture.Identity, Operation);
        ReconciliationV1 claim = (await new PayloadReconciliationClaims(fixture.Store, fixture.Clock).TryClaimAsync(new(Operation, fixture.Scope)))!.Value;
        (await lifecycle.ResumeAsync(fixture.Identity, claim)).ShouldBeTrue();
        PayloadProtectionStateValue<WrappedDekV1>? key = await fixture.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(fixture.Scope, write.Completion!.KeyReference, 1));
        var indexes = new PayloadKeyIndexes(fixture.Store, fixture.Clock);
        ScopeKeyIndexSnapshot scope = await indexes.ReadScopeAsync(fixture.Scope);
        KekKeyIndexSnapshot inventory = await indexes.ReadKekAsync(PayloadProtectionStateKeys.Digest(fixture.Backend.BackendId), PayloadProtectionStateKeys.Digest("fixture:version-01"));
        if (delete)
        {
            key.ShouldBeNull(); scope.Head!.Value.EntryCount.ShouldBe(0u); inventory.Head!.Value.EntryCount.ShouldBe(0ul);
        }
        else
        {
            key!.Value.State.ShouldBe("Invalidated"); PayloadKeyIndexes.VerifyMembership(key.Value, scope, inventory);
        }
    }

    [Fact]
    public async Task RetirementRequiresConfirmedWaitAndClearsTheActualRootCacheBeforeRecordingThePermanentTombstone()
    {
        ReservationTestFixture fixture = await ReservationTestFixture.CreateAsync();
        var roots = new PayloadProtectionScopeRegistry([new("tenant-a", "parties")]);
        var profile = new PayloadProtectionInstanceProfile(new('b', 40), new('c', 64), fixture.Backend.BackendId, ["json", "json+pdenc-v2"], ["V2"]);
        int waits = 0;
        var leases = new PayloadProtectionLeaseCoordinator(fixture.Store, roots, profile, fixture.Clock, delay: (duration, _) => { waits++; fixture.Clock.Advance(duration); return ValueTask.CompletedTask; });
        await leases.RegisterAsync(fixture.Identity);
        await using var cache = new PayloadKeyCache(timeProvider: fixture.Clock);
        byte[] cached = Enumerable.Repeat((byte)17, 32).ToArray();
        using (PayloadKeyResolution copy = await cache.GetAsync(new(fixture.Scope, "01J00000000000000000000109", 1, 1, "fixture:version-01"),
            _ => ValueTask.FromResult(PayloadKeyResolution.Success(cached)), rootDigest: fixture.Root)) { }
        await leases.RetireAsync(fixture.Identity, cache);
        waits.ShouldBe(1); cached.ShouldAllBe(b => b == 0);
        MemberCatalogSnapshot members = await new PayloadProtectionCatalogs(fixture.Store, fixture.Clock).ReadMembersAsync(fixture.Root);
        members.Pages.SelectMany(p => p.Value.Entries).Single(e => e.InstanceId == leases.InstanceId).State.ShouldBe("Cleared");
        await Should.ThrowAsync<PayloadProtectionStateException>(() => leases.RenewAsync(fixture.Identity).AsTask());
    }

    [Fact]
    public async Task V065_UnknownActorPersistenceWithholdsCompletionAfterExpiryAndClearing()
    {
        ReservationTestFixture fixture = await ReservationTestFixture.CreateAsync();
        var write = await fixture.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        await fixture.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.Unknown);
        await using var cache = new PayloadKeyCache(timeProvider: fixture.Clock);
        KeyLifecycleCoordinator lifecycle = Coordinator(fixture, cache);
        await lifecycle.BeginAsync(fixture.Identity, Approval(fixture.Identity), Operation, 2, true);
        await lifecycle.AcknowledgeAsync(fixture.Identity, Operation);
        ReconciliationV1 claim = (await new PayloadReconciliationClaims(fixture.Store, fixture.Clock).TryClaimAsync(new(Operation, fixture.Scope)))!.Value;
        (await lifecycle.ResumeAsync(fixture.Identity, claim)).ShouldBeFalse();
        (await fixture.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(fixture.Scope, write.Completion!.KeyReference, 1)))!.Value.State.ShouldBe("Reserved");
    }

    [Fact]
    public async Task ReusedImmutableClearingProofCannotOutliveItsExactLifecycleObservation()
    {
        var fixture = await ReservationTestFixture.CreateAsync();
        var write = await fixture.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        await fixture.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.NotPersisted);
        await using var cache = new PayloadKeyCache(timeProvider: fixture.Clock); var lifecycle = Coordinator(fixture, cache);
        await lifecycle.BeginAsync(fixture.Identity, Approval(fixture.Identity), Operation, 2);
        await lifecycle.AcknowledgeAsync(fixture.Identity, Operation);
        var claim = (await new PayloadReconciliationClaims(fixture.Store, fixture.Clock).TryClaimAsync(new(Operation, fixture.Scope)))!.Value;
        bool proofRead = false; bool raced = false;
        fixture.Store.Reading = key =>
        {
            if (key == PayloadProtectionStateKeys.ClearAck(fixture.Scope, 2, fixture.Leases.InstanceId)) { proofRead = true; }
            if (!proofRead || key != PayloadProtectionStateKeys.IndexHead(fixture.Scope)) { return; }
            fixture.Store.Reading = null; raced = true;
            var current = fixture.Store.ReadAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(fixture.Scope)).GetAwaiter().GetResult()!;
            fixture.Store.CommitAsync([PayloadProtectionStateMutation.Put(current.Value with { ObservedAtUtc = PayloadProtectionStateCodec.Timestamp(fixture.Clock.GetUtcNow().AddSeconds(1)) }, current.ETag)]).GetAwaiter().GetResult().ShouldBe(PayloadStateCommitResult.Committed);
        };
        await Should.ThrowAsync<PayloadProtectionStateException>(() => lifecycle.ResumeAsync(fixture.Identity, claim).AsTask());
        raced.ShouldBeTrue();
        (await fixture.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(fixture.Scope, write.Completion!.KeyReference, 1)))!.Value.State.ShouldBe("Reserved");
    }

    internal static CryptoShreddingWorkflowDecision Approval(AggregateIdentity identity) => new(
        new("01J00000000000000000000100", identity.TenantId, identity.Domain, CryptoShreddingWorkflowScope.Aggregate, identity.AggregateId,
            null, null, KeyReferencePolicy.NoKeyReference, null), CryptoShreddingWorkflowState.Approved, "approved", CryptoShreddingNextAction.None,
        null, null, "fixture-actor", DateTimeOffset.UtcNow, false, false);

    private static KeyLifecycleCoordinator Coordinator(ReservationTestFixture fixture, PayloadKeyCache cache,
        Func<TimeSpan, CancellationToken, ValueTask>? delay = null) => new(fixture.Store, new([new("tenant-a", "parties")]), fixture.Leases,
            cache, fixture.Backend.BackendId, _ => true, "fixture-actor", fixture.Clock,
            delay ?? ((duration, _) => { fixture.Clock.Advance(duration); return ValueTask.CompletedTask; }));
}
