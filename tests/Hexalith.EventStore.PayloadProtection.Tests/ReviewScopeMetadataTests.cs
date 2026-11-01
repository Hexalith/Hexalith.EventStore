using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Lifetime scope metadata remains bounded without dropping epoch or clearing evidence.</summary>
public sealed class ReviewScopeMetadataTests
{
    [Fact]
    public async Task ErasureObservationCapacityRejectsNewScopesAndPreservesExistingMonotonicEvidence()
    {
        var provider = new LifecycleTestErasureProvider();
        var registry = new ErasureStateRegistry(new([new("tenant-a", "parties")]), [new("parties", provider)]);
        for (int index = 0; index < PayloadProtectionOperationalLimits.ScopeMetadata; index++)
        { _ = await registry.ObserveAsync(new("tenant-a", "parties", index.ToString(System.Globalization.CultureInfo.InvariantCulture))); }
        Capacity(await Should.ThrowAsync<PayloadProtectionStateException>(() => registry.ObserveAsync(new("tenant-a", "parties", "overflow")).AsTask()));
        provider.State = new(PayloadErasureState.Pending, 2, PayloadErasureReasonCode.DomainPending, DateTimeOffset.UtcNow);
        (await registry.ObserveAsync(new("tenant-a", "parties", "0"))).LifecycleEpoch.ShouldBe(2ul);
        provider.State = new(PayloadErasureState.Active, 1, PayloadErasureReasonCode.None, DateTimeOffset.UtcNow);
        (await Should.ThrowAsync<PayloadProtectionStateException>(() => registry.ObserveAsync(new("tenant-a", "parties", "0")).AsTask()))
            .Reason.ShouldBe(UnreadableProtectedDataReason.ConsistencyMismatch);
    }

    [Fact]
    public async Task CacheAndLocalMetadataCapacityPreservesExistingIdentitiesAndPermanentBlocks()
    {
        var existing = new AggregateIdentity("tenant-a", "parties", "existing");
        string scope = PayloadProtectionStateKeys.ScopeDigest(existing);
        var operations = new PayloadLocalOperations();
        using (operations.Begin(existing)) { }
        await using var cache = new PayloadKeyCache();
        var key = new PayloadKeyCacheKey(scope, "01J00000000000000000009999", 1, 1, "fixture");
        using (var copy = await cache.GetAsync(key, _ => ValueTask.FromResult(PayloadKeyResolution.Success(new byte[32])))) { }
        for (int index = 0; index < PayloadProtectionOperationalLimits.ScopeMetadata - 1; index++)
        {
            string blocked = index.ToString("x64");
            await operations.DrainScopeAsync(blocked);
            await cache.ClearScopeAsync(blocked);
        }
        using (operations.Begin(existing)) { }
        using (var hit = await cache.GetAsync(key, _ => throw new InvalidOperationException("must remain cached"))) { hit.Reason.ShouldBeNull(); }
        Capacity(Should.Throw<PayloadProtectionStateException>(() => operations.Begin(new("tenant-a", "parties", "overflow"))));
        Capacity(await Should.ThrowAsync<PayloadProtectionStateException>(() => operations.DrainScopeAsync(new('f', 64)).AsTask()));
        Capacity(await Should.ThrowAsync<PayloadProtectionStateException>(() => cache.ClearScopeAsync(new('f', 64)).AsTask()));
        Capacity(await Should.ThrowAsync<PayloadProtectionStateException>(() => cache.GetAsync(key with { ScopeDigest = new('f', 64) }, _ => throw new InvalidOperationException("must not unwrap")).AsTask()));
        await operations.DrainScopeAsync(scope); await cache.ClearScopeAsync(scope);
        (Should.Throw<PayloadProtectionStateException>(() => operations.Begin(existing))).Reason.ShouldBe(UnreadableProtectedDataReason.KeyInvalidatedOrDeleted);
        using var denied = await cache.GetAsync(key, _ => throw new InvalidOperationException("blocked"));
        denied.Reason.ShouldBe(UnreadableProtectedDataReason.KeyInvalidatedOrDeleted);
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
