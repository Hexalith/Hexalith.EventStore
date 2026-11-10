using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Ordinary scope traffic remains available while permanent blocks and epoch evidence are retained.</summary>
public sealed class ReviewScopeMetadataTests
{
    [Fact]
    public async Task ErasureObservationsAtTheProductionLimitEvictTheLeastRecentScopeAndKeepRecentMonotonicEvidence()
    {
        var provider = new LifecycleTestErasureProvider();
        var registry = new ErasureStateRegistry(new([new("tenant-a", "parties")]), [new("parties", provider)]);
        for (int index = 0; index < PayloadProtectionOperationalLimits.ErasureObservations; index++)
        { _ = await registry.ObserveAsync(new("tenant-a", "parties", index.ToString(System.Globalization.CultureInfo.InvariantCulture))); }
        _ = await registry.ObserveAsync(new("tenant-a", "parties", "0"));
        // A new aggregate is never refused at the limit; the least recently observed scope ("1") is evicted instead.
        (await registry.ObserveAsync(new("tenant-a", "parties", "overflow"))).State.ShouldBe(PayloadErasureState.Active);
        provider.State = new(PayloadErasureState.Active, 0, PayloadErasureReasonCode.None, DateTimeOffset.UtcNow);
        (await Should.ThrowAsync<PayloadProtectionStateException>(() => registry.ObserveAsync(new("tenant-a", "parties", "2")).AsTask()))
            .Reason.ShouldBe(UnreadableProtectedDataReason.ConsistencyMismatch);
        (await registry.ObserveAsync(new("tenant-a", "parties", "1"))).LifecycleEpoch.ShouldBe(0ul);
        provider.State = new(PayloadErasureState.Pending, 2, PayloadErasureReasonCode.DomainPending, DateTimeOffset.UtcNow);
        (await registry.ObserveAsync(new("tenant-a", "parties", "0"))).LifecycleEpoch.ShouldBe(2ul);
        provider.State = new(PayloadErasureState.Active, 1, PayloadErasureReasonCode.None, DateTimeOffset.UtcNow);
        (await Should.ThrowAsync<PayloadProtectionStateException>(() => registry.ObserveAsync(new("tenant-a", "parties", "0")).AsTask()))
            .Reason.ShouldBe(UnreadableProtectedDataReason.ConsistencyMismatch);
    }

    [Fact]
    public async Task CacheAndLocalTrafficBeyondTheBlockLimitPreservesPermanentBlocks()
    {
        var existing = new AggregateIdentity("tenant-a", "parties", "existing");
        string scope = PayloadProtectionStateKeys.ScopeDigest(existing);
        var operations = new PayloadLocalOperations();
        using (operations.Begin(existing)) { }
        await using var cache = new PayloadKeyCache();
        var key = new PayloadKeyCacheKey(scope, "01J00000000000000000009999", 1, 1, "fixture");
        using (var copy = await cache.GetAsync(key, _ => ValueTask.FromResult(PayloadKeyResolution.Success(new byte[32])))) { }
        await operations.DrainScopeAsync(scope); await cache.ClearScopeAsync(scope);
        for (int index = 0; index <= PayloadProtectionOperationalLimits.ScopeMetadata; index++)
        {
            var identity = new AggregateIdentity("tenant-a", "parties", index.ToString(System.Globalization.CultureInfo.InvariantCulture));
            using (operations.Begin(identity)) { }
            var transientKey = key with { ScopeDigest = PayloadProtectionStateKeys.ScopeDigest(identity) };
            using var read = await cache.GetAsync(transientKey, _ => ValueTask.FromResult(PayloadKeyResolution.Success(new byte[32])));
            read.Reason.ShouldBeNull();
        }
        var overflow = new AggregateIdentity("tenant-a", "parties", "overflow");
        using (operations.Begin(overflow)) { }
        using (var read = await cache.GetAsync(key with { ScopeDigest = PayloadProtectionStateKeys.ScopeDigest(overflow) },
            _ => ValueTask.FromResult(PayloadKeyResolution.Success(new byte[32])))) { read.Reason.ShouldBeNull(); }
        (Should.Throw<PayloadProtectionStateException>(() => operations.Begin(existing))).Reason.ShouldBe(UnreadableProtectedDataReason.KeyInvalidatedOrDeleted);
        using var denied = await cache.GetAsync(key, _ => throw new InvalidOperationException("blocked"));
        denied.Reason.ShouldBe(UnreadableProtectedDataReason.KeyInvalidatedOrDeleted);
        var late = new AggregateIdentity("tenant-a", "parties", "late-block");
        string lateScope = PayloadProtectionStateKeys.ScopeDigest(late);
        await operations.DrainScopeAsync(lateScope); await cache.ClearScopeAsync(lateScope);
        (Should.Throw<PayloadProtectionStateException>(() => operations.Begin(late))).Reason.ShouldBe(UnreadableProtectedDataReason.KeyInvalidatedOrDeleted);
        using var lateDenied = await cache.GetAsync(key with { ScopeDigest = lateScope }, _ => throw new InvalidOperationException("blocked"));
        lateDenied.Reason.ShouldBe(UnreadableProtectedDataReason.KeyInvalidatedOrDeleted);
    }

    [Fact]
    public async Task PermanentRootBlocksHaveTheSameBoundWithoutLosingExistingBlocks()
    {
        var operations = new PayloadLocalOperations();
        await using var cache = new PayloadKeyCache();
        for (int index = 0; index < PayloadProtectionOperationalLimits.ScopeMetadata; index++)
        {
            string root = index.ToString("x64");
            await operations.DrainRootAsync(root); await cache.ClearRootAsync(root);
        }
        await operations.DrainRootAsync(new('0', 64)); await cache.ClearRootAsync(new('0', 64));
        Capacity(await Should.ThrowAsync<PayloadProtectionStateException>(() => operations.DrainRootAsync(new('f', 64)).AsTask()));
        Capacity(await Should.ThrowAsync<PayloadProtectionStateException>(() => cache.ClearRootAsync(new('f', 64)).AsTask()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedScopeAdmissionWithholdsDurableClearingAcknowledgement(bool exhaustLocal)
    {
        var fixture = await ReservationTestFixture.CreateAsync();
        await using var cache = new PayloadKeyCache(timeProvider: fixture.Clock);
        for (int index = 0; index < PayloadProtectionOperationalLimits.ScopeMetadata; index++)
        {
            if (exhaustLocal) { await fixture.Leases.LocalOperations.DrainScopeAsync(index.ToString("x64")); }
            else { await cache.ClearScopeAsync(index.ToString("x64")); }
        }
        var lifecycle = new KeyLifecycleCoordinator(fixture.Store, new([new("tenant-a", "parties")]), fixture.Leases, cache,
            fixture.Backend.BackendId, _ => true, "fixture-actor", fixture.Clock);
        const string operation = "01J00000000000000000009888";
        await lifecycle.BeginAsync(fixture.Identity, LifecycleDrainTests.Approval(fixture.Identity), operation, 2);
        Capacity(await Should.ThrowAsync<PayloadProtectionStateException>(() => lifecycle.AcknowledgeAsync(fixture.Identity, operation).AsTask()));
        (await fixture.Store.ReadAsync<CacheClearAckV1>(PayloadProtectionStateKeys.ClearAck(fixture.Scope, 2, fixture.Leases.InstanceId))).ShouldBeNull();
    }

    private static void Capacity(PayloadProtectionStateException failure)
    {
        failure.Reason.ShouldBe(UnreadableProtectedDataReason.ProviderUnavailable);
        failure.IsAdmissionLimit.ShouldBeTrue();
    }
}
