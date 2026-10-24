using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Independent member clearing, bounded stalled roots and root/page cursor fairness.</summary>
public sealed class ReviewWorkerTests
{
    [Fact]
    public async Task DistinctRegisteredMembersClearTheirOwnCachesWhileAnotherOwnerHoldsTheClaim()
    {
        var f = await ReservationTestFixture.CreateAsync(); var roots = new PayloadProtectionScopeRegistry([new("tenant-a", "parties")]);
        var other = new PayloadProtectionLeaseCoordinator(f.Store, roots, new(new('b', 40), new('c', 64), f.Backend.BackendId, ["json", "json+pdenc-v2"], ["V2"]), f.Clock);
        await other.RegisterAsync(f.Identity); other.InstanceId.ShouldNotBe(f.Leases.InstanceId);
        await using var firstCache = new PayloadKeyCache(timeProvider: f.Clock); await using var secondCache = new PayloadKeyCache(timeProvider: f.Clock);
        byte[] first = Enumerable.Repeat((byte)4, 32).ToArray(); byte[] second = Enumerable.Repeat((byte)5, 32).ToArray();
        var key = new PayloadKeyCacheKey(f.Scope, "01J00000000000000000001101", 1, 1, "fixture:version-01");
        await using (var copy = await firstCache.GetAsync(key, _ => ValueTask.FromResult(PayloadKeyResolution.Success(first)), rootDigest: f.Root)) { }
        await using (var copy = await secondCache.GetAsync(key, _ => ValueTask.FromResult(PayloadKeyResolution.Success(second)), rootDigest: f.Root)) { }
        var firstLifecycle = Lifecycle(f, roots, f.Leases, firstCache); var secondLifecycle = Lifecycle(f, roots, other, secondCache);
        const string operation = "01J00000000000000000001102";
        await firstLifecycle.BeginAsync(f.Identity, LifecycleDrainTests.Approval(f.Identity), operation, 2);
        var claim = (await new PayloadReconciliationClaims(f.Store, f.Clock).TryClaimAsync(new(operation, f.Scope)))!.Value;
        using var firstWorker = new PayloadProtectionReconciler(f.Store, roots, firstLifecycle, f.Backend.BackendId, _ => true, "fixture-actor", f.Clock);
        using var secondWorker = new PayloadProtectionReconciler(f.Store, roots, secondLifecycle, f.Backend.BackendId, _ => true, "fixture-actor", f.Clock);
        (await firstWorker.RunPassAsync()).ShouldBe(0); (await secondWorker.RunPassAsync()).ShouldBe(0);
        foreach (string instance in new[] { f.Leases.InstanceId, other.InstanceId })
        {
            (await f.Store.ReadAsync<CacheClearAckV1>(PayloadProtectionStateKeys.ClearAck(f.Scope, 2, instance)))!.Value.OperationId.ShouldBe(operation);
        }
        first.ShouldAllBe(b => b == 0); second.ShouldAllBe(b => b == 0);
        (await firstLifecycle.ResumeAsync(f.Identity, claim)).ShouldBeTrue();
    }

    [Fact]
    public async Task AStalledLocalDrainDoesNotAcknowledgeOrPreventTheNextRootFromProgressing()
    {
        var f = await ReservationTestFixture.CreateAsync(); var second = new AggregateIdentity("tenant-b", "parties", "party-01");
        var roots = new PayloadProtectionScopeRegistry([new("tenant-a", "parties"), new("tenant-b", "parties")]);
        await SeedAsync(f, second, 1);
        await using var cache = new PayloadKeyCache(timeProvider: f.Clock); var lifecycle = Lifecycle(f, roots, f.Leases, cache);
        const string barrier = "01J00000000000000000001103";
        await lifecycle.BeginAsync(f.Identity, LifecycleDrainTests.Approval(f.Identity), barrier, 2);
        using var held = f.Leases.LocalOperations.Begin(f.Identity);
        var late = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); held.RetainUntil(late.Task);
        using var worker = new PayloadProtectionReconciler(f.Store, roots, lifecycle, f.Backend.BackendId, _ => true, "fixture-actor", f.Clock);
        using (var budget = new PayloadOperationBudget(TimeProvider.System, default, TimeSpan.FromMilliseconds(30)))
        { (await worker.RunPassAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2))).ShouldBe(0); }
        (await f.Store.ReadAsync<CacheClearAckV1>(PayloadProtectionStateKeys.ClearAck(f.Scope, 2, f.Leases.InstanceId))).ShouldBeNull();
        (await worker.RunPassAsync()).ShouldBe(1);
        late.SetResult(); held.Dispose();
        await f.Leases.LocalOperations.DrainScopeAsync(f.Scope).AsTask().WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task BoundedPassesRotateTwoRootsAndReachTheRetainedSecondPage()
    {
        var f = await ReservationTestFixture.CreateAsync(); var second = new AggregateIdentity("tenant-b", "parties", "party-01");
        await SeedAsync(f, f.Identity, 257); await SeedAsync(f, second, 1);
        var roots = new PayloadProtectionScopeRegistry([new("tenant-a", "parties"), new("tenant-b", "parties")]);
        await using var cache = new PayloadKeyCache(timeProvider: f.Clock); var observed = new List<string>();
        var evidence = new LifecyclePersistenceEvidence(PayloadProtectionPersistenceOutcome.Unknown) { Observed = (op, _) => { observed.Add(op.ScopeDigest); return ValueTask.CompletedTask; } };
        using var worker = new PayloadProtectionReconciler(f.Store, roots, Lifecycle(f, roots, f.Leases, cache), f.Backend.BackendId, _ => true, "fixture-actor", f.Clock, persistence: evidence);
        (await worker.RunPassAsync()).ShouldBe(256); observed.Count.ShouldBe(256); observed.ShouldAllBe(s => s == f.Scope);
        (await worker.RunPassAsync()).ShouldBe(1); observed[^1].ShouldBe(PayloadProtectionStateKeys.ScopeDigest(second));
        (await worker.RunPassAsync()).ShouldBe(1); observed.Count.ShouldBe(258); observed[^1].ShouldBe(f.Scope);
        (await new PayloadProtectionCatalogs(f.Store, f.Clock).ReadWorkAsync(f.Root)).Pages[1].Value.Entries.Length.ShouldBe(1);
    }

    private static KeyLifecycleCoordinator Lifecycle(ReservationTestFixture f, PayloadProtectionScopeRegistry roots, PayloadProtectionLeaseCoordinator process, PayloadKeyCache cache)
        => new(f.Store, roots, process, cache, f.Backend.BackendId, _ => true, "fixture-actor", f.Clock, (duration, _) => { f.Clock.Advance(duration); return ValueTask.CompletedTask; });
    private static async ValueTask SeedAsync(ReservationTestFixture f, AggregateIdentity identity, int count)
    {
        string root = PayloadProtectionStateKeys.TenantDomainDigest(identity.TenantId, identity.Domain); string scope = PayloadProtectionStateKeys.ScopeDigest(identity);
        string now = PayloadProtectionStateCodec.Timestamp(f.Clock.GetUtcNow()); var catalogs = new PayloadProtectionCatalogs(f.Store, f.Clock);
        if (root != f.Root)
        {
            await catalogs.InitializeAsync(root);
            await f.Store.CommitAsync([PayloadProtectionStateMutation.Put(new LifecycleV1(identity.TenantId, identity.Domain, identity.AggregateId, scope, "Active", 1, "none", "01J00000000000000000001104", now))]);
        }
        var entropy = new CryptographicPayloadProtectionEntropy();
        for (int i = 0; i < count; i++)
        {
            string operation = entropy.CreateKeyReference(); string key = entropy.CreateKeyReference();
            await f.Store.CommitAsync([PayloadProtectionStateMutation.Put(new OperationV1(operation, "Create", scope, key, 1, "PayloadReturned", 1, now, now, "Pending", "none")),
                PayloadProtectionStateMutation.Put(new ReconciliationV1(operation, scope, key, 1, "Create", now, 0, "Pending", "none", now)),
                .. catalogs.AddWork(await catalogs.ReadWorkAsync(root), new(operation, scope))]);
        }
    }
}
