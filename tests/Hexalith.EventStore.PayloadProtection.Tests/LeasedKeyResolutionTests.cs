using System.Security.Cryptography;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>V064/V069/V073/V074 current authority, exact-version cache reads and late-result withholding.</summary>
public sealed class LeasedKeyResolutionTests
{
    [Fact]
    public async Task V069_LeasesAndCurrentStatePrecedeBothCacheMissAndHit()
    {
        ReservationTestFixture fixture = await ReservationTestFixture.CreateAsync();
        byte[]? fixtureDek = null;
        fixture.Backend.Wrap = (_, bytes, _) => { fixtureDek = bytes.ToArray(); return ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.None, new byte[32], "fixture:version-01")); };
        var write = await fixture.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        byte[]? unwrapped = null;
        fixture.Backend.Unwrap = async (operation, provider, _, token) =>
        {
            provider.ShouldBe("fixture:version-01");
            (await fixture.Store.ReadAsync<ReadLeaseV1>(PayloadProtectionStateKeys.Lease(fixture.Scope, operation), token)).ShouldNotBeNull();
            unwrapped = fixtureDek!.ToArray();
            return new(PayloadBackendFailure.None, unwrapped, provider);
        };
        await using var cache = new PayloadKeyCache(timeProvider: fixture.Clock);
        PayloadKeyResolver resolver = Resolver(fixture, cache);
        var entropy = new CryptographicPayloadProtectionEntropy();
        for (int i = 0; i < 2; i++)
        {
            string operation = entropy.CreateKeyReference();
            CoreUnprotectionResult read = await new PayloadProtectionCore().TryUnprotectEventAsync(write.Result.PayloadBytes, TestFixture.Context(),
                (key, version, token) => resolver.ResolveAsync(fixture.Identity, key, version, operation, token));
            read.IsReadable.ShouldBeTrue();
            read.PayloadBytes.ShouldBe(CompatibilityTestData.PlainJson());
            (await fixture.Store.ReadAsync<ReadLeaseV1>(PayloadProtectionStateKeys.Lease(fixture.Scope, operation))).ShouldBeNull();
            CryptographicOperations.ZeroMemory(read.PayloadBytes!);
        }
        fixture.Backend.UnwrapCalls.ShouldBe(1);
        await cache.ClearScopeAsync(fixture.Scope);
        unwrapped.ShouldNotBeNull();
        unwrapped.ShouldAllBe(b => b == 0);
        CryptographicOperations.ZeroMemory(fixtureDek!);
        CryptographicOperations.ZeroMemory(write.Result.PayloadBytes);
    }

    [Fact]
    public async Task V064_V074_InvalidationAfterUnwrapWithholdsAllPlaintextAndAStaleCacheCannotAuthorizeAnotherRead()
    {
        ReservationTestFixture fixture = await ReservationTestFixture.CreateAsync();
        byte[]? fixtureDek = null;
        fixture.Backend.Wrap = (_, bytes, _) => { fixtureDek = bytes.ToArray(); return ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.None, new byte[32], "fixture:version-01")); };
        var write = await fixture.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        fixture.Backend.Unwrap = async (_, provider, _, token) =>
        {
            PayloadProtectionStateValue<LifecycleV1> lifecycle = (await fixture.Store.ReadAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(fixture.Scope), token))!;
            await fixture.Store.CommitAsync([PayloadProtectionStateMutation.Put(lifecycle.Value with { State = "Invalidating", Epoch = 2, ReasonCode = "domain-invalidating" }, lifecycle.ETag)], token);
            fixture.Erasure.State = new(PayloadErasureState.Invalidating, 2, PayloadErasureReasonCode.DomainInvalidating, fixture.Clock.GetUtcNow());
            return new(PayloadBackendFailure.None, fixtureDek!.ToArray(), provider);
        };
        await using var cache = new PayloadKeyCache(timeProvider: fixture.Clock);
        PayloadKeyResolver resolver = Resolver(fixture, cache);
        var entropy = new CryptographicPayloadProtectionEntropy();
        for (int i = 0; i < 2; i++)
        {
            string operation = entropy.CreateKeyReference();
            CoreUnprotectionResult read = await new PayloadProtectionCore().TryUnprotectEventAsync(write.Result.PayloadBytes, TestFixture.Context(),
                (key, version, token) => resolver.ResolveAsync(fixture.Identity, key, version, operation, token));
            read.PayloadBytes.ShouldBeNull();
            read.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.KeyInvalidatedOrDeleted);
            CompatibilityTestData.ShouldNotLeak(read);
        }
        fixture.Backend.UnwrapCalls.ShouldBe(1);
        await cache.ClearScopeAsync(fixture.Scope);
        CryptographicOperations.ZeroMemory(fixtureDek!);
        CryptographicOperations.ZeroMemory(write.Result.PayloadBytes);
    }

    [Fact]
    public async Task SuspendedUnwrapClearsLateOutputAndCannotOutliveTheReadLease()
    {
        ReservationTestFixture fixture = await ReservationTestFixture.CreateAsync();
        byte[]? fixtureDek = null;
        fixture.Backend.Wrap = (_, bytes, _) => { fixtureDek = bytes.ToArray(); return ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.None, new byte[32], "fixture:version-01")); };
        var write = await fixture.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        byte[]? late = null;
        fixture.Backend.Unwrap = (_, provider, _, _) =>
        {
            late = fixtureDek!.ToArray();
            fixture.Clock.Advance(TimeSpan.FromSeconds(46));
            return ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.None, late, provider));
        };
        await using var cache = new PayloadKeyCache(timeProvider: fixture.Clock);
        PayloadKeyResolver resolver = Resolver(fixture, cache);
        CoreUnprotectionResult read = await new PayloadProtectionCore().TryUnprotectEventAsync(write.Result.PayloadBytes, TestFixture.Context(),
            (key, version, token) => resolver.ResolveAsync(fixture.Identity, key, version, "01J00000000000000000000010", token));
        read.PayloadBytes.ShouldBeNull();
        read.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.ProviderUnavailable);
        late.ShouldNotBeNull(); late.ShouldAllBe(b => b == 0);
        CryptographicOperations.ZeroMemory(fixtureDek!);
        CryptographicOperations.ZeroMemory(write.Result.PayloadBytes);
    }

    private static PayloadKeyResolver Resolver(ReservationTestFixture fixture, PayloadKeyCache cache)
        => new(fixture.Store, new([new("tenant-a", "parties")]), new(new([new("tenant-a", "parties")]), [new("parties", fixture.Erasure)]), fixture.Leases,
            fixture.Backend, new(fixture.Clock), cache, fixture.Clock);
}
