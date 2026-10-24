// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; base sections 8-10, 13-15; lifecycle amendment sections 1-7.
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>V054-V055 deterministic orphan thresholds and immutable retained material; these fakes are not PF-02 evidence.</summary>
public sealed class LifecycleRecoveryTests
{
    [Fact]
    public async Task V054_V055_ExactTwentyFourHourBoundaryQuarantinesOnlyBookkeepingAndRetainsBothIndexes()
    {
        ReservationTestFixture fixture = await ReservationTestFixture.CreateAsync();
        var write = await fixture.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        await fixture.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.NotPersisted);
        await using var cache = new PayloadKeyCache(timeProvider: fixture.Clock);
        var roots = new PayloadProtectionScopeRegistry([new("tenant-a", "parties")]);
        var lifecycle = new KeyLifecycleCoordinator(fixture.Store, roots, fixture.Leases, cache, fixture.Backend.BackendId, _ => true, "fixture-actor", fixture.Clock);
        using var worker = new PayloadProtectionReconciler(fixture.Store, roots, lifecycle, fixture.Backend.BackendId, _ => true, "fixture-actor", fixture.Clock);
        fixture.Clock.Advance(TimeSpan.FromHours(24) - TimeSpan.FromTicks(1));
        (await worker.RunPassAsync()).ShouldBe(0);
        (await fixture.Store.ReadAsync<ReconciliationV1>(PayloadProtectionStateKeys.Reconciliation(write.Completion!.OperationId)))!.Value.Status.ShouldBe("Pending");
        fixture.Clock.Advance(TimeSpan.FromTicks(1));
        (await worker.RunPassAsync()).ShouldBe(1);
        WrappedDekV1 retained = (await fixture.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(fixture.Scope, write.Completion.KeyReference, 1)))!.Value;
        retained.State.ShouldBe("Reserved");
        (await fixture.Store.ReadAsync<ReconciliationV1>(PayloadProtectionStateKeys.Reconciliation(write.Completion.OperationId)))!.Value.Status.ShouldBe("Quarantined");
        var indexes = new PayloadKeyIndexes(fixture.Store, fixture.Clock);
        PayloadKeyIndexes.VerifyMembership(retained, await indexes.ReadScopeAsync(fixture.Scope), await indexes.ReadKekAsync(PayloadProtectionStateKeys.Digest(retained.BackendId), PayloadProtectionStateKeys.Digest(retained.ProviderKeyVersionId)));
        WorkCatalogSnapshot work = await new PayloadProtectionCatalogs(fixture.Store, fixture.Clock).ReadWorkAsync(fixture.Root);
        work.Head.Value.EntryCount.ShouldBe(0u); work.Pages.Count.ShouldBe(1);
        fixture.Backend.WrapCalls.ShouldBe(1); fixture.Backend.UnwrapCalls.ShouldBe(0);
    }

    [Fact]
    public async Task UnknownPersistenceNeverBecomesAnOrphanFromAgeOrLeaseExpiry()
    {
        ReservationTestFixture fixture = await ReservationTestFixture.CreateAsync();
        var write = await fixture.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        await fixture.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.Unknown);
        fixture.Clock.Advance(TimeSpan.FromDays(2));
        await using var cache = new PayloadKeyCache(timeProvider: fixture.Clock);
        var roots = new PayloadProtectionScopeRegistry([new("tenant-a", "parties")]);
        var lifecycle = new KeyLifecycleCoordinator(fixture.Store, roots, fixture.Leases, cache, fixture.Backend.BackendId, _ => true, "fixture-actor", fixture.Clock);
        using var worker = new PayloadProtectionReconciler(fixture.Store, roots, lifecycle, fixture.Backend.BackendId, _ => true, "fixture-actor", fixture.Clock);
        (await worker.RunPassAsync()).ShouldBe(1);
        (await fixture.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(fixture.Scope, write.Completion!.KeyReference, 1)))!.Value.State.ShouldBe("Reserved");
        (await new PayloadProtectionCatalogs(fixture.Store, fixture.Clock).ReadWorkAsync(fixture.Root)).Head.Value.EntryCount.ShouldBe(1u);
    }

    [Fact]
    public async Task StrongReadRetriesOnlyTransientFailuresWithinTheSharedBudget()
    {
        var clock = new LifecycleTestClock();
        var delays = new List<TimeSpan>();
        var executor = new PayloadStateReadExecutor(clock, (pause, _) => { delays.Add(pause); clock.Advance(pause); return ValueTask.CompletedTask; });
        int attempts = 0;
        int result = await executor.ExecuteAsync<int>(_ => ++attempts == 3 ? ValueTask.FromResult(42)
            : ValueTask.FromException<int>(new PayloadProtectionStateException(UnreadableProtectedDataReason.ProviderUnavailable)));
        result.ShouldBe(42); attempts.ShouldBe(3); delays.Count.ShouldBe(2);
        delays[0].TotalMilliseconds.ShouldBeInRange(160, 240); delays[1].TotalMilliseconds.ShouldBeInRange(400, 600);
        attempts = 0;
        await Should.ThrowAsync<PayloadProtectionStateException>(() => executor.ExecuteAsync<int>(_ => { attempts++; return ValueTask.FromException<int>(new PayloadProtectionStateException(UnreadableProtectedDataReason.ProviderDenied)); }).AsTask());
        attempts.ShouldBe(1);
        await Should.ThrowAsync<PayloadProtectionStateException>(() => executor.ExecuteAsync(_ => { clock.Advance(TimeSpan.FromSeconds(10)); return ValueTask.FromResult(1); }).AsTask());
    }
}
