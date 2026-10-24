// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; base sections 10.3-10.8 and 15; amendment sections 3-7.
using System.Text;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>V049-V058 mutation faults, enclosing deadlines and constructive failure audit evidence.</summary>
public sealed class LifecycleFaultBudgetTests
{
    [Fact]
    public async Task V085_CreateAuditCountsTheActualBoundedBackendAttemptsAndMonotonicElapsedDuration()
    {
        ReservationTestFixture fixture = await ReservationTestFixture.CreateAsync();
        int calls = 0;
        fixture.Backend.Wrap = (_, _, _) => { calls++; fixture.Clock.Advance(TimeSpan.FromMilliseconds(7)); return ValueTask.FromResult(calls < 3
            ? new PayloadBackendResult(PayloadBackendFailure.Unavailable) : new PayloadBackendResult(PayloadBackendFailure.None, new byte[32], "fixture:version-01")); };
        var roots = new PayloadProtectionScopeRegistry([new("tenant-a", "parties")]);
        var executor = new PayloadBackendExecutor(fixture.Clock, (pause, _) => { fixture.Clock.Advance(pause); return ValueTask.CompletedTask; });
        var reservations = new KeyReservationCoordinator(fixture.Store, roots, fixture.Leases, new(roots, [new("parties", fixture.Erasure)]), fixture.Backend, executor, _ => true, "fixture-actor", timeProvider: fixture.Clock);
        var write = await reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        fixture.Store.Audits.Single().AttemptCount.ShouldBe(3u);
        fixture.Store.Audits.Single().DurationMilliseconds.ShouldBeInRange(581ul, 861ul);
        (await fixture.Store.ReadAsync<OperationV1>(PayloadProtectionStateKeys.Operation(write.Completion!.OperationId)))!.Value.Attempt.ShouldBe(3u);
    }

    [Fact]
    public async Task V049_AmbiguousCreateReconcilesTheStableWrapOnceAndQuarantinesWithoutInventingAPayloadOrReservation()
    {
        ReservationTestFixture fixture = await ReservationTestFixture.CreateAsync();
        fixture.Backend.Wrap = (_, _, _) => ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.Ambiguous));
        await Should.ThrowAsync<PayloadProtectionCryptographicException>(() => fixture.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context()).AsTask());
        WorkEntryV1 work = (await new PayloadProtectionCatalogs(fixture.Store, fixture.Clock).ReadWorkAsync(fixture.Root)).Pages.Single().Value.Entries.Single();
        byte[] output = Enumerable.Repeat((byte)0xe9, 32).ToArray();
        fixture.Backend.Reconcile = (operation, _) => { operation.ShouldBe(work.OperationId); return ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.None, output, "fixture:version-01")); };
        await using var cache = new PayloadKeyCache(timeProvider: fixture.Clock);
        var roots = new PayloadProtectionScopeRegistry([new("tenant-a", "parties")]);
        var lifecycle = new KeyLifecycleCoordinator(fixture.Store, roots, fixture.Leases, cache, fixture.Backend.BackendId, _ => true, "fixture-actor", fixture.Clock);
        using var worker = new PayloadProtectionReconciler(fixture.Store, roots, lifecycle, fixture.Backend.BackendId, _ => true, "fixture-actor", fixture.Clock, backend: fixture.Backend);
        (await worker.RunPassAsync()).ShouldBe(1); output.ShouldAllBe(b => b == 0);
        OperationV1 operation = (await fixture.Store.ReadAsync<OperationV1>(PayloadProtectionStateKeys.Operation(work.OperationId)))!.Value;
        operation.Phase.ShouldBe("Failed"); operation.ReasonCode.ShouldBe("not-persisted");
        (await worker.RunPassAsync()).ShouldBe(0);
        fixture.Clock.Advance(TimeSpan.FromHours(24));
        (await worker.RunPassAsync()).ShouldBe(1);
        (await fixture.Store.ReadAsync<ReconciliationV1>(PayloadProtectionStateKeys.Reconciliation(work.OperationId)))!.Value.Status.ShouldBe("Quarantined");
        (await fixture.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(fixture.Scope, operation.KeyRef, 1))).ShouldBeNull();
        fixture.Backend.WrapCalls.ShouldBe(1); fixture.Backend.ReconcileCalls.ShouldBe(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task V049_UnobservedWrapHasOneAttemptAndDurableAmbiguityWithoutKeyOrPayload(bool throws)
    {
        ReservationTestFixture fixture = await ReservationTestFixture.CreateAsync();
        byte[]? dek = null;
        fixture.Backend.Wrap = (_, memory, _) =>
        {
            dek = System.Runtime.InteropServices.MemoryMarshal.TryGetArray(memory, out ArraySegment<byte> array) ? array.Array : throw new InvalidOperationException();
            fixture.Clock.Advance(TimeSpan.FromMilliseconds(17));
            return throws ? ValueTask.FromException<PayloadBackendResult>(new InvalidOperationException("private-provider-canary-q9n7"))
                : ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.Ambiguous));
        };
        PayloadProtectionCryptographicException failure = await Should.ThrowAsync<PayloadProtectionCryptographicException>(() => fixture.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context()).AsTask());
        fixture.Backend.WrapCalls.ShouldBe(1); dek!.ShouldAllBe(b => b == 0);
        failure.InnerException.ShouldBeNull(); failure.Data.Count.ShouldBe(0); failure.ToString().ShouldNotContain("private-provider-canary-q9n7");
        WorkEntryV1 work = (await new PayloadProtectionCatalogs(fixture.Store, fixture.Clock).ReadWorkAsync(fixture.Root)).Pages.Single().Value.Entries.Single();
        OperationV1 operation = (await fixture.Store.ReadAsync<OperationV1>(PayloadProtectionStateKeys.Operation(work.OperationId)))!.Value;
        operation.Phase.ShouldBe("Ambiguous"); operation.Result.ShouldBe("Pending");
        (await fixture.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(fixture.Scope, operation.KeyRef, 1))).ShouldBeNull();
        PayloadProtectionAuditRecord audit = fixture.Store.Audits.Single();
        audit.AttemptCount.ShouldBe(1u); audit.DurationMilliseconds.ShouldBe(17ul); audit.ReasonCode.ShouldBe("ambiguous-outcome");
        audit.ToState.ShouldBeNull();
        Encoding.UTF8.GetString(PayloadProtectionStateCodec.Encode(audit)).ShouldNotContain("private-provider-canary-q9n7");
        Encoding.UTF8.GetString(PayloadProtectionStateCodec.Encode(audit)).ShouldNotContain(operation.KeyRef);
    }

    [Fact]
    public async Task V049_LostReservationAcknowledgementKeepsTheKeyBothIndexesAndDiscoverableAmbiguousOperation()
    {
        ReservationTestFixture fixture = await ReservationTestFixture.CreateAsync();
        fixture.Store.Committing = mutations =>
        {
            if (mutations.Any(m => m.Key.StartsWith("eventstore:pp:v2:key:", StringComparison.Ordinal))) { fixture.Store.LoseNextAcknowledgement = true; fixture.Store.Committing = null; }
        };
        await Should.ThrowAsync<PayloadProtectionCryptographicException>(() => fixture.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context()).AsTask());
        WorkEntryV1 work = (await new PayloadProtectionCatalogs(fixture.Store, fixture.Clock).ReadWorkAsync(fixture.Root)).Pages.Single().Value.Entries.Single();
        OperationV1 operation = (await fixture.Store.ReadAsync<OperationV1>(PayloadProtectionStateKeys.Operation(work.OperationId)))!.Value;
        operation.Phase.ShouldBe("Ambiguous");
        WrappedDekV1 key = (await fixture.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(fixture.Scope, operation.KeyRef, 1)))!.Value;
        key.State.ShouldBe("Reserved");
        var indexes = new PayloadKeyIndexes(fixture.Store, fixture.Clock);
        PayloadKeyIndexes.VerifyMembership(key, await indexes.ReadScopeAsync(fixture.Scope), await indexes.ReadKekAsync(PayloadProtectionStateKeys.Digest(key.BackendId), PayloadProtectionStateKeys.Digest(key.ProviderKeyVersionId)));
        fixture.Backend.WrapCalls.ShouldBe(1); fixture.Store.Audits.Count.ShouldBe(2);
    }

    [Fact]
    public async Task V089_CancellationAfterWrapRecordsPendingAmbiguityAndClearsEveryOwnedArray()
    {
        ReservationTestFixture fixture = await ReservationTestFixture.CreateAsync();
        using var cancelled = new CancellationTokenSource();
        byte[] response = Enumerable.Repeat((byte)0x8d, 32).ToArray();
        fixture.Backend.Wrap = (_, _, _) => { cancelled.Cancel(); return ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.None, response, "fixture:version-01")); };
        await Should.ThrowAsync<OperationCanceledException>(() => fixture.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context(), cancelled.Token).AsTask());
        response.ShouldAllBe(b => b == 0);
        WorkEntryV1 work = (await new PayloadProtectionCatalogs(fixture.Store, fixture.Clock).ReadWorkAsync(fixture.Root)).Pages.Single().Value.Entries.Single();
        (await fixture.Store.ReadAsync<OperationV1>(PayloadProtectionStateKeys.Operation(work.OperationId)))!.Value.Phase.ShouldBe("Ambiguous");
        fixture.Store.Audits.Single().Result.ShouldBe("Pending"); fixture.Backend.WrapCalls.ShouldBe(1);
    }

    [Fact]
    public async Task CatalogReadsShareOneDeadlineAcrossPagesRatherThanStartingTenSecondsForEveryRead()
    {
        ReservationTestFixture fixture = await ReservationTestFixture.CreateAsync();
        int reads = 0;
        fixture.Store.Reading = _ => { reads++; fixture.Clock.Advance(TimeSpan.FromSeconds(4)); };
        PayloadProtectionStateException failure = await Should.ThrowAsync<PayloadProtectionStateException>(() => new PayloadProtectionCatalogs(fixture.Store, fixture.Clock).ReadMembersAsync(fixture.Root).AsTask());
        failure.Reason.ShouldBe(UnreadableProtectedDataReason.ProviderUnavailable); reads.ShouldBe(3);
        fixture.Store.Reading = null;
        (await new PayloadProtectionCatalogs(fixture.Store, fixture.Clock).ReadMembersAsync(fixture.Root)).Head.Value.EntryCount.ShouldBe(1u);
    }

    [Fact]
    public async Task ReservationReadsShareTheEnclosingDeadlineAndCannotCallWrapAfterSlowAdmission()
    {
        ReservationTestFixture fixture = await ReservationTestFixture.CreateAsync();
        fixture.Store.Reading = _ => fixture.Clock.Advance(TimeSpan.FromSeconds(3));
        await Should.ThrowAsync<PayloadProtectionCryptographicException>(() => fixture.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context()).AsTask());
        fixture.Backend.WrapCalls.ShouldBe(0); fixture.Store.Reading = null;
        (await new PayloadProtectionCatalogs(fixture.Store, fixture.Clock).ReadWorkAsync(fixture.Root)).Head.Value.EntryCount.ShouldBe(0u);
    }
}
