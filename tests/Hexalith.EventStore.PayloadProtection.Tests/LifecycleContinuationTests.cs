// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base sections 10, 13-15; amendment sections 3-7.
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Restarted persistence recovery, stale claims and terminal-preserving indexed continuations.</summary>
public sealed class LifecycleContinuationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task V053_TrustedPersistedReferenceActivatesAnExactReservationAfterRestart(bool loseCommitAcknowledgement)
    {
        ReservationTestFixture fixture = await ReservationTestFixture.CreateAsync();
        var write = await fixture.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        await fixture.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.Unknown);
        await using var cache = new PayloadKeyCache(timeProvider: fixture.Clock);
        var roots = new PayloadProtectionScopeRegistry([new("tenant-a", "parties")]);
        var lifecycle = Coordinator(fixture, cache);
        var evidence = new LifecyclePersistenceEvidence(PayloadProtectionPersistenceOutcome.Persisted);
        evidence.Observed = (_, _) => { fixture.Store.LoseNextAcknowledgement = loseCommitAcknowledgement; return ValueTask.CompletedTask; };
        using var worker = new PayloadProtectionReconciler(fixture.Store, roots, lifecycle, fixture.Backend.BackendId, _ => true, "fixture-actor", fixture.Clock, persistence: evidence);
        (await worker.RunPassAsync()).ShouldBe(1);
        WrappedDekV1 key = (await fixture.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(fixture.Scope, write.Completion!.KeyReference, 1)))!.Value;
        key.State.ShouldBe("Active");
        var indexes = new PayloadKeyIndexes(fixture.Store, fixture.Clock);
        PayloadKeyIndexes.VerifyMembership(key, await indexes.ReadScopeAsync(fixture.Scope), await indexes.ReadKekAsync(PayloadProtectionStateKeys.Digest(key.BackendId), PayloadProtectionStateKeys.Digest(key.ProviderKeyVersionId)));
        (await fixture.Store.ReadAsync<OperationV1>(PayloadProtectionStateKeys.Operation(write.Completion.OperationId)))!.Value.Phase.ShouldBe("Completed");
        (await new PayloadProtectionCatalogs(fixture.Store, fixture.Clock).ReadWorkAsync(fixture.Root)).Head.Value.EntryCount.ShouldBe(0u);
        (await worker.RunPassAsync()).ShouldBe(0);
        fixture.Backend.WrapCalls.ShouldBe(1); fixture.Backend.UnwrapCalls.ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task V053_RecoveryCannotActivateThroughAReplacementClaimOrAnInvalidationBarrier(bool replaceClaim)
    {
        ReservationTestFixture fixture = await ReservationTestFixture.CreateAsync();
        var write = await fixture.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        await using var cache = new PayloadKeyCache(timeProvider: fixture.Clock);
        await fixture.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.Unknown);
        var evidence = new LifecyclePersistenceEvidence(PayloadProtectionPersistenceOutcome.Persisted);
        evidence.Observed = async (operation, cancellation) =>
        {
            if (replaceClaim) { fixture.Clock.Advance(TimeSpan.FromSeconds(90)); _ = await new PayloadReconciliationClaims(fixture.Store, fixture.Clock).TryClaimAsync(new(operation.OperationId, fixture.Scope)); }
            else
            {
                PayloadProtectionStateValue<LifecycleV1> state = (await fixture.Store.ReadAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(fixture.Scope)))!;
                await fixture.Store.CommitAsync([PayloadProtectionStateMutation.Put(state.Value with { State = "Invalidating", Epoch = 2, ReasonCode = "domain-invalidating" }, state.ETag)]);
            }
        };
        using var worker = new PayloadProtectionReconciler(fixture.Store, new([new("tenant-a", "parties")]), Coordinator(fixture, cache), fixture.Backend.BackendId, _ => true, "fixture-actor", fixture.Clock, persistence: evidence);
        await worker.RunPassAsync();
        (await fixture.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(fixture.Scope, write.Completion!.KeyReference, 1)))!.Value.State.ShouldBe("Reserved");
        (await new PayloadProtectionCatalogs(fixture.Store, fixture.Clock).ReadWorkAsync(fixture.Root)).Head.Value.EntryCount.ShouldBe(replaceClaim ? 1u : 0u);
    }

    [Fact]
    public async Task V067_InvalidatedScopeCanBeDeletedAtGreaterEpochWithoutRegressingItsTerminalState()
    {
        ReservationTestFixture fixture = await ReservationTestFixture.CreateAsync();
        var write = await fixture.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        await fixture.Reservations.AcquireCompletionAsync(write.Completion!);
        await fixture.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.Persisted);
        await using var cache = new PayloadKeyCache(timeProvider: fixture.Clock);
        var lifecycle = Coordinator(fixture, cache);
        const string invalidate = "01J00000000000000000000401";
        const string delete = "01J00000000000000000000402";
        await lifecycle.BeginAsync(fixture.Identity, LifecycleDrainTests.Approval(fixture.Identity), invalidate, 2);
        await lifecycle.AcknowledgeAsync(fixture.Identity, invalidate);
        var claims = new PayloadReconciliationClaims(fixture.Store, fixture.Clock);
        (await lifecycle.ResumeAsync(fixture.Identity, (await claims.TryClaimAsync(new(invalidate, fixture.Scope)))!.Value)).ShouldBeTrue();
        await lifecycle.BeginAsync(fixture.Identity, LifecycleDrainTests.Approval(fixture.Identity), delete, 4, true);
        LifecycleV1 barrier = (await fixture.Store.ReadAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(fixture.Scope)))!.Value;
        barrier.State.ShouldBe("Invalidated"); barrier.Epoch.ShouldBe(4ul);
        await lifecycle.AcknowledgeAsync(fixture.Identity, delete);
        (await lifecycle.ResumeAsync(fixture.Identity, (await claims.TryClaimAsync(new(delete, fixture.Scope)))!.Value)).ShouldBeTrue();
        (await fixture.Store.ReadAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(fixture.Scope)))!.Value.State.ShouldBe("Deleted");
        (await fixture.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(fixture.Scope, write.Completion!.KeyReference, 1))).ShouldBeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PerKeyContinuationRequiresScopeDrainAndCompletesItsExactWorkWithoutCompletingTheScope(bool delete)
    {
        ReservationTestFixture fixture = await ReservationTestFixture.CreateAsync();
        var write = await fixture.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        await fixture.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.NotPersisted);
        await using var cache = new PayloadKeyCache(timeProvider: fixture.Clock);
        var lifecycle = Coordinator(fixture, cache);
        const string barrierId = "01J00000000000000000000403";
        const string child = "01J00000000000000000000404";
        await lifecycle.BeginAsync(fixture.Identity, LifecycleDrainTests.Approval(fixture.Identity), barrierId, 2, delete);
        string now = PayloadProtectionStateCodec.Timestamp(fixture.Clock.GetUtcNow());
        string kind = delete ? "Delete" : "Invalidate";
        var catalogs = new PayloadProtectionCatalogs(fixture.Store, fixture.Clock);
        await fixture.Store.CommitAsync([PayloadProtectionStateMutation.Put(new OperationV1(child, kind, fixture.Scope, write.Completion!.KeyReference, 1, "Started", 1, now, now, "Pending", "domain-invalidating")),
            PayloadProtectionStateMutation.Put(new ReconciliationV1(child, fixture.Scope, write.Completion.KeyReference, 1, kind, now, 0, "Pending", "domain-invalidating", now)),
            .. catalogs.AddWork(await catalogs.ReadWorkAsync(fixture.Root), new(child, fixture.Scope))]);
        ReconciliationV1 claim = (await new PayloadReconciliationClaims(fixture.Store, fixture.Clock).TryClaimAsync(new(child, fixture.Scope)))!.Value;
        (await lifecycle.ResumeAsync(fixture.Identity, claim)).ShouldBeFalse();
        await lifecycle.AcknowledgeAsync(fixture.Identity, barrierId);
        fixture.Clock.Advance(TimeSpan.FromSeconds(30));
        claim = (await new PayloadReconciliationClaims(fixture.Store, fixture.Clock).TryClaimAsync(new(child, fixture.Scope)))!.Value;
        (await lifecycle.ResumeAsync(fixture.Identity, claim)).ShouldBeTrue();
        (await fixture.Store.ReadAsync<OperationV1>(PayloadProtectionStateKeys.Operation(child)))!.Value.Phase.ShouldBe("Completed");
        fixture.Store.Audits.Where(a => a.OperationId == child).ShouldAllBe(a => a.WorkflowId == LifecycleDrainTests.Approval(fixture.Identity).Identity.WorkflowId);
        (await fixture.Store.ReadAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(fixture.Scope)))!.Value.State.ShouldBe("Invalidating");
    }

    private static KeyLifecycleCoordinator Coordinator(ReservationTestFixture fixture, PayloadKeyCache cache) => new(fixture.Store,
        new([new("tenant-a", "parties")]), fixture.Leases, cache, fixture.Backend.BackendId, _ => true, "fixture-actor", fixture.Clock,
        (duration, _) => { fixture.Clock.Advance(duration); return ValueTask.CompletedTask; });
}
