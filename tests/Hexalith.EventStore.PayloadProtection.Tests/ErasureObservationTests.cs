// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base sections 9-10 and 15; lifecycle amendment sections 1-8.
using System.Security.Cryptography;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Bounded least-recently-observed erasure evidence never refuses a new aggregate and never hides a durable regression.</summary>
public sealed class ErasureObservationTests
{
    [Fact]
    public async Task ANewAggregateAtTheLimitIsAdmittedAndEvictsOnlyTheLeastRecentlyObservedScope()
    {
        var provider = new LifecycleTestErasureProvider { State = State(PayloadErasureState.Active, 2) };
        var registry = new ErasureStateRegistry(new([new("tenant-a", "parties")]), [new("parties", provider)], observationLimit: 2);
        _ = await registry.ObserveAsync(Identity("a"));
        _ = await registry.ObserveAsync(Identity("b"));
        _ = await registry.ObserveAsync(Identity("a"));
        (await registry.ObserveAsync(Identity("c"))).LifecycleEpoch.ShouldBe(2ul);
        provider.State = State(PayloadErasureState.Active, 1);
        // The refreshed scope keeps its in-memory regression check; the least recent one gave it up.
        (await Should.ThrowAsync<PayloadProtectionStateException>(() => registry.ObserveAsync(Identity("a")).AsTask())).Reason.ShouldBe(UnreadableProtectedDataReason.ConsistencyMismatch);
        (await registry.ObserveAsync(Identity("b"))).LifecycleEpoch.ShouldBe(1ul);
        for (int index = 0; index < 100; index++) { (await registry.ObserveAsync(Identity("new-" + index))).State.ShouldBe(PayloadErasureState.Active); }
        Should.Throw<PayloadProtectionFormatException>(() => new ErasureStateRegistry(new([new("tenant-a", "parties")]), [new("parties", provider)], observationLimit: 0));
    }

    [Fact]
    public async Task AScopeEvictedAtEpochTwoWhoseProviderRegressesToEpochOneIsRefusedByTheResolverAndTheReservation()
    {
        ReservationTestFixture f = await ReservationTestFixture.CreateAsync();
        PayloadProtectionStateValue<LifecycleV1> lifecycle = (await f.Store.ReadAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(f.Scope)))!;
        (await f.Store.CommitAsync([PayloadProtectionStateMutation.Put(lifecycle.Value with { Epoch = 2 }, lifecycle.ETag)])).ShouldBe(PayloadStateCommitResult.Committed);
        f.Erasure.State = State(PayloadErasureState.Active, 2);
        var roots = new PayloadProtectionScopeRegistry([new("tenant-a", "parties")]);
        var registry = new ErasureStateRegistry(roots, [new("parties", f.Erasure)], observationLimit: 1);
        var reservations = new KeyReservationCoordinator(f.Store, roots, f.Leases, registry, f.Backend, new(f.Clock), _ => true, "fixture-actor", timeProvider: f.Clock);
        await using var cache = new PayloadKeyCache(timeProvider: f.Clock);
        var resolver = new PayloadKeyResolver(f.Store, roots, registry, f.Leases, f.Backend, new(f.Clock), cache, f.Clock);
        f.Backend.Unwrap = (_, version, _, _) => ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.None, new byte[32], version));
        var write = await reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        await using (PayloadKeyResolution admitted = await resolver.ResolveAsync(f.Identity, write.Completion!.KeyReference, 1, "01J00000000000000000000711"))
        {
            admitted.Reason.ShouldBeNull();
            CryptographicOperations.ZeroMemory(admitted.TakeBytes());
        }
        // Observing another aggregate evicts this scope's epoch-2 evidence from the one-entry registry.
        _ = await registry.ObserveAsync(Identity("party-02"));
        f.Erasure.State = State(PayloadErasureState.Active, 1);
        await using (PayloadKeyResolution regressed = await resolver.ResolveAsync(f.Identity, write.Completion.KeyReference, 1, "01J00000000000000000000712"))
        {
            regressed.Reason.ShouldBe(UnreadableProtectedDataReason.ConsistencyMismatch);
        }
        int unwraps = f.Backend.UnwrapCalls, wraps = f.Backend.WrapCalls;
        uint work = (await new PayloadProtectionCatalogs(f.Store, f.Clock).ReadWorkAsync(f.Root)).Head.Value.EntryCount;
        await Should.ThrowAsync<PayloadProtectionCryptographicException>(() => reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context()).AsTask());
        // The reservation authority check fails before any intent, wrap or unwrap, with the typed durable mismatch.
        (await Should.ThrowAsync<PayloadProtectionStateException>(() => reservations.AcquireCompletionAsync(write.Completion).AsTask())).Reason.ShouldBe(UnreadableProtectedDataReason.ConsistencyMismatch);
        f.Backend.WrapCalls.ShouldBe(wraps); f.Backend.UnwrapCalls.ShouldBe(unwraps);
        (await new PayloadProtectionCatalogs(f.Store, f.Clock).ReadWorkAsync(f.Root)).Head.Value.EntryCount.ShouldBe(work);
    }

    private static AggregateIdentity Identity(string aggregate) => new("tenant-a", "parties", aggregate);

    private static PayloadErasureStateResult State(PayloadErasureState state, ulong epoch)
        => new(state, epoch, state == PayloadErasureState.Active ? PayloadErasureReasonCode.None : PayloadErasureReasonCode.DomainPending, DateTimeOffset.UtcNow);
}
