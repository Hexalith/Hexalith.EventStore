// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base section 10; amendment sections 3-4/8; pass-3 regressions.
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Exercises drain admission, preconditions, bounded continuation and retained terminal bookkeeping.</summary>
public sealed class ReviewLifecycleCorrectionsTests
{
    private const string BarrierId = "01J00000000000000000009101";

    [Theory]
    [InlineData("requested")]
    [InlineData("rejected")]
    [InlineData("other-aggregate")]
    [InlineData("domain")]
    [InlineData("same-epoch")]
    [InlineData("max-epoch")]
    [InlineData("invalidated")]
    public async Task InvalidBeginApprovalOrEpochNeverSubmitsABarrier(string scenario)
    {
        var f = await ReservationTestFixture.CreateAsync();
        await using var cache = new PayloadKeyCache(timeProvider: f.Clock);
        var coordinator = Coordinator(f, cache);
        var approval = LifecycleDrainTests.Approval(f.Identity);
        ulong epoch = scenario == "same-epoch" ? 1ul : scenario == "max-epoch" ? ulong.MaxValue : 2ul;
        approval = scenario switch
        {
            "requested" => approval with { State = CryptoShreddingWorkflowState.Requested },
            "rejected" => approval with { State = CryptoShreddingWorkflowState.Rejected },
            "other-aggregate" => approval with { Identity = approval.Identity with { AggregateId = "another-aggregate" } },
            "domain" => approval with { Identity = approval.Identity with { Scope = CryptoShreddingWorkflowScope.Domain, AggregateId = null } },
            _ => approval,
        };
        if (scenario == "invalidated")
        {
            var live = (await f.Store.ReadAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(f.Scope)))!;
            await f.Store.CommitAsync([PayloadProtectionStateMutation.Put(live.Value with { State = "Invalidated", ReasonCode = "domain-invalidated" }, live.ETag)]);
        }
        int commits = f.Store.Commits;
        await Should.ThrowAsync<PayloadProtectionStateException>(() => coordinator.BeginAsync(f.Identity, approval, BarrierId, epoch).AsTask());
        f.Store.Commits.ShouldBe(commits);
        (await f.Store.ReadAsync<InvalidationDrainV1>(PayloadProtectionStateKeys.Drain(f.Scope, BarrierId))).ShouldBeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OnlyClearedTombstonesCanReplaceAnAbsentAcknowledgementBelowTheCutoff(bool cleared)
    {
        var f = await ReservationTestFixture.CreateAsync();
        await using var cache = new PayloadKeyCache(timeProvider: f.Clock);
        int waits = 0;
        var coordinator = Coordinator(f, cache, (duration, _) => { waits++; f.Clock.Advance(duration); return ValueTask.CompletedTask; });
        await coordinator.BeginAsync(f.Identity, LifecycleDrainTests.Approval(f.Identity), BarrierId, 2);
        var catalogs = new PayloadProtectionCatalogs(f.Store, f.Clock);
        await f.Store.CommitAsync(catalogs.TransitionMember(await catalogs.ReadMembersAsync(f.Root), f.Leases.InstanceId, "Draining"));
        if (cleared) { await f.Store.CommitAsync(catalogs.TransitionMember(await catalogs.ReadMembersAsync(f.Root), f.Leases.InstanceId, "Cleared")); }
        var claim = (await new PayloadReconciliationClaims(f.Store, f.Clock).TryClaimAsync(new(BarrierId, f.Scope)))!.Value;
        (await coordinator.ResumeAsync(f.Identity, claim)).ShouldBe(cleared);
        waits.ShouldBe(cleared ? 1 : 0);
        (await f.Store.ReadAsync<CacheClearAckV1>(PayloadProtectionStateKeys.ClearAck(f.Scope, 2, f.Leases.InstanceId))).ShouldBeNull();
        (await f.Store.ReadAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(f.Scope)))!.Value.State.ShouldBe(cleared ? "Invalidated" : "Invalidating");
    }

    [Fact]
    public async Task ClearingAndPersistenceAreCheckedAgainAfterTheFullDrainInterval()
    {
        var f = await ReservationTestFixture.CreateAsync();
        await using var cache = new PayloadKeyCache(timeProvider: f.Clock);
        var coordinator = Coordinator(f, cache, async (duration, _) =>
        {
            f.Clock.Advance(duration);
            var work = await new PayloadProtectionCatalogs(f.Store, f.Clock).ReadWorkAsync(f.Root);
            string now = PayloadProtectionStateCodec.Timestamp(f.Clock.GetUtcNow());
            const string other = "01J00000000000000000009102";
            await f.Store.CommitAsync([PayloadProtectionStateMutation.Put(new OperationV1(other, "Create", f.Scope, "01J00000000000000000009103", 1, "Started", 1, now, now, "Pending", "none")),
                .. new PayloadProtectionCatalogs(f.Store, f.Clock).AddWork(work, new(other, f.Scope))]);
        });
        await coordinator.BeginAsync(f.Identity, LifecycleDrainTests.Approval(f.Identity), BarrierId, 2);
        await coordinator.AcknowledgeAsync(f.Identity, BarrierId);
        var claim = (await new PayloadReconciliationClaims(f.Store, f.Clock).TryClaimAsync(new(BarrierId, f.Scope)))!.Value;
        (await coordinator.ResumeAsync(f.Identity, claim)).ShouldBeFalse();
        (await f.Store.ReadAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(f.Scope)))!.Value.State.ShouldBe("Invalidating");
    }

    [Fact]
    public async Task ScopeWith257KeysRequiresASecondClaimAfterExactly256Mutations()
    {
        var f = await ReservationTestFixture.CreateAsync();
        for (int i = 0; i < 257; i++)
        {
            var write = await f.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
            await f.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.NotPersisted);
        }
        await using var cache = new PayloadKeyCache(timeProvider: f.Clock);
        var coordinator = Coordinator(f, cache);
        await coordinator.BeginAsync(f.Identity, LifecycleDrainTests.Approval(f.Identity), BarrierId, 2);
        await coordinator.AcknowledgeAsync(f.Identity, BarrierId);
        var claims = new PayloadReconciliationClaims(f.Store, f.Clock);
        var claim = (await claims.TryClaimAsync(new(BarrierId, f.Scope)))!.Value;
        (await coordinator.ResumeAsync(f.Identity, claim)).ShouldBeFalse();
        var indexes = new PayloadKeyIndexes(f.Store, f.Clock);
        var first = await indexes.ReadScopeAsync(f.Scope);
        first.Pages.SelectMany(p => p.Value.Entries).Count(e => e.State == "Invalidated").ShouldBe(256);
        first.Pages.SelectMany(p => p.Value.Entries).Count(e => e.State == "Reserved").ShouldBe(1);
        (await f.Store.ReadAsync<OperationV1>(PayloadProtectionStateKeys.Operation(BarrierId)))!.Value.Result.ShouldBe("Pending");
        f.Clock.Advance(TimeSpan.FromSeconds(30));
        claim = (await claims.TryClaimAsync(new(BarrierId, f.Scope)))!.Value;
        (await coordinator.ResumeAsync(f.Identity, claim)).ShouldBeTrue();
        (await indexes.ReadScopeAsync(f.Scope)).Pages.SelectMany(p => p.Value.Entries).ShouldAllBe(e => e.State == "Invalidated");
        (await f.Store.ReadAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(f.Scope)))!.Value.State.ShouldBe("Invalidated");
    }

    [Fact]
    public async Task FailedCreateCanQuarantineAfterScopeInvalidationWithoutChangingTheInvalidatedKey()
    {
        var f = await ReservationTestFixture.CreateAsync();
        var write = await f.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        await f.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.NotPersisted);
        await using var cache = new PayloadKeyCache(timeProvider: f.Clock);
        var coordinator = Coordinator(f, cache);
        await coordinator.BeginAsync(f.Identity, LifecycleDrainTests.Approval(f.Identity), BarrierId, 2);
        await coordinator.AcknowledgeAsync(f.Identity, BarrierId);
        var claim = (await new PayloadReconciliationClaims(f.Store, f.Clock).TryClaimAsync(new(BarrierId, f.Scope)))!.Value;
        (await coordinator.ResumeAsync(f.Identity, claim)).ShouldBeTrue();
        string key = PayloadProtectionStateKeys.Key(f.Scope, write.Completion!.KeyReference, 1);
        var before = (await f.Store.ReadAsync<WrappedDekV1>(key))!;
        f.Clock.Advance(TimeSpan.FromHours(24));
        using var worker = new PayloadProtectionReconciler(f.Store, new([new("tenant-a", "parties")]), coordinator, f.Backend.BackendId, _ => true, "fixture-actor", f.Clock);
        (await worker.RunPassAsync()).ShouldBe(1);
        (await f.Store.ReadAsync<WrappedDekV1>(key))!.Value.ShouldBe(before.Value);
        (await f.Store.ReadAsync<WrappedDekV1>(key))!.Bytes.ShouldBe(before.Bytes);
        (await f.Store.ReadAsync<ReconciliationV1>(PayloadProtectionStateKeys.Reconciliation(write.Completion.OperationId)))!.Value.Status.ShouldBe("Quarantined");
        (await new PayloadProtectionCatalogs(f.Store, f.Clock).ReadWorkAsync(f.Root)).Head.Value.EntryCount.ShouldBe(0u);
    }

    [Fact]
    public async Task UnregisteredRetirementReturnsOnlyATypedMismatch()
    {
        var f = await ReservationTestFixture.CreateAsync();
        var leases = new PayloadProtectionLeaseCoordinator(f.Store, new([new("tenant-a", "parties")]), new(new('b', 40), new('c', 64), f.Backend.BackendId, ["json+pdenc-v2"], ["V2"]), f.Clock);
        await using var cache = new PayloadKeyCache(timeProvider: f.Clock);
        (await Should.ThrowAsync<PayloadProtectionStateException>(() => leases.RetireAsync(f.Identity, cache).AsTask())).Reason.ShouldBe(UnreadableProtectedDataReason.ConsistencyMismatch);
    }

    private static KeyLifecycleCoordinator Coordinator(ReservationTestFixture f, PayloadKeyCache cache, Func<TimeSpan, CancellationToken, ValueTask>? delay = null)
        => new(f.Store, new([new("tenant-a", "parties")]), f.Leases, cache, f.Backend.BackendId, _ => true, "fixture-actor", f.Clock,
            delay ?? ((duration, _) => { f.Clock.Advance(duration); return ValueTask.CompletedTask; }));
}
