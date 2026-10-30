// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base section 10; amendment sections 3-4/8; pass-3 recovery regressions.
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using NSubstitute;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Recovery preserves discoverability, immutable authority and bounded failure classifications.</summary>
public sealed class ReviewRecoveryCorrectionsTests
{
    [Fact]
    public async Task AdmissionLimitReadFailureHasOneAttemptAndNoDelay()
    {
        var clock = new LifecycleTestClock(); int attempts = 0; int delays = 0;
        var executor = new PayloadStateReadExecutor(clock, (_, _) => { delays++; return ValueTask.CompletedTask; });
        var failure = await Should.ThrowAsync<PayloadProtectionStateException>(() => executor.ExecuteAsync<int>(_ =>
        {
            attempts++;
            return ValueTask.FromException<int>(new PayloadProtectionStateException(UnreadableProtectedDataReason.ProviderUnavailable, isAdmissionLimit: true));
        }).AsTask());
        failure.IsAdmissionLimit.ShouldBeTrue(); attempts.ShouldBe(1); delays.ShouldBe(0);
    }

    [Fact]
    public async Task ExpiredForegroundRotationStillRecordsItsBoundedFailureBookkeepingAndAudit()
    {
        var f = await ReservationTestFixture.CreateAsync();
        var write = await f.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        await f.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.NotPersisted);
        f.Backend.Unwrap = (_, provider, _, _) => ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.None, new byte[32], provider));
        f.Backend.WrapExact = (_, _, _, _) =>
        {
            f.Clock.Advance(TimeSpan.FromSeconds(30));
            // Model the qualified primitive's inherited budget check during cleanup reads.
            f.Store.Reading = _ => PayloadOperationBudget.RequireCurrent();
            return ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.Ambiguous));
        };
        const string operation = "01J00000000000000000009207";
        var rotation = new PayloadKeyRotationCoordinator(f.Store, new([new("tenant-a", "parties")]), f.Leases, f.Backend, new(f.Clock), _ => true, "fixture-actor", f.Clock);
        await Should.ThrowAsync<PayloadProtectionStateException>(() => rotation.RewrapAsync(f.Identity, write.Completion!.KeyReference, 1, operation, "fixture:version-02").AsTask());
        f.Store.Reading = null;
        var recorded = (await f.Store.ReadAsync<OperationV1>(PayloadProtectionStateKeys.Operation(operation)))!.Value;
        recorded.Phase.ShouldBe("Ambiguous"); recorded.Result.ShouldBe("Pending"); recorded.ReasonCode.ShouldBe("ambiguous-outcome");
        var claim = (await f.Store.ReadAsync<ReconciliationV1>(PayloadProtectionStateKeys.Reconciliation(operation)))!.Value;
        claim.Status.ShouldBe("Pending"); claim.ReasonCode.ShouldBe("ambiguous-outcome");
        PayloadProtectionStateCodec.ParseTimestamp(claim.NotBeforeUtc).ShouldBe(f.Clock.GetUtcNow());
        f.Store.Audits.Single(a => a.OperationId == operation).Result.ShouldBe("Pending");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CommittedReservationFailureRemainsQuarantinableWithUnknownPersistence(bool unknownAcknowledgement)
    {
        var f = await ReservationTestFixture.CreateAsync();
        f.Store.Committing = mutations =>
        {
            if (!mutations.Any(m => m.Key.StartsWith("eventstore:pp:v2:key:", StringComparison.Ordinal))) { return; }
            f.Store.Committing = null;
            if (unknownAcknowledgement) { f.Store.LoseNextAcknowledgement = true; }
            else { f.Clock.Advance(TimeSpan.FromSeconds(10)); }
        };
        await Should.ThrowAsync<PayloadProtectionCryptographicException>(() => f.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context()).AsTask());
        var catalogs = new PayloadProtectionCatalogs(f.Store, f.Clock);
        var entry = (await catalogs.ReadWorkAsync(f.Root)).Pages.Single().Value.Entries.Single();
        var operation = (await f.Store.ReadAsync<OperationV1>(PayloadProtectionStateKeys.Operation(entry.OperationId)))!.Value;
        operation.Phase.ShouldBe("Reserved"); operation.Result.ShouldBe("Failed"); operation.ReasonCode.ShouldBe("not-persisted");
        f.Clock.Advance(TimeSpan.FromHours(24));
        await using var cache = new PayloadKeyCache(timeProvider: f.Clock);
        var lifecycle = Lifecycle(f, cache);
        using var worker = new PayloadProtectionReconciler(f.Store, new([new("tenant-a", "parties")]), lifecycle, f.Backend.BackendId, _ => true, "fixture-actor", f.Clock);
        (await worker.RunPassAsync()).ShouldBe(1);
        (await f.Store.ReadAsync<ReconciliationV1>(PayloadProtectionStateKeys.Reconciliation(entry.OperationId)))!.Value.Status.ShouldBe("Quarantined");
        (await f.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(f.Scope, operation.KeyRef, 1)))!.Value.State.ShouldBe("Reserved");
        f.Backend.WrapCalls.ShouldBe(1);
    }

    [Fact]
    public async Task ReservedPendingCreateRefusesRotationBeforeAnyMutationOrBackendCall()
    {
        var f = await ReservationTestFixture.CreateAsync();
        var write = await f.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        int commits = f.Store.Commits;
        var rotation = new PayloadKeyRotationCoordinator(f.Store, new([new("tenant-a", "parties")]), f.Leases, f.Backend, new(f.Clock), _ => true, "fixture-actor", f.Clock);
        (await Should.ThrowAsync<PayloadProtectionStateException>(() => rotation.RewrapAsync(f.Identity, write.Completion!.KeyReference, 1, "01J00000000000000000009201", "fixture:version-02").AsTask())).Reason.ShouldBe(UnreadableProtectedDataReason.ConsistencyMismatch);
        f.Store.Commits.ShouldBe(commits); f.Backend.WrapCalls.ShouldBe(1); f.Backend.UnwrapCalls.ShouldBe(0);
    }

    [Fact]
    public async Task UnfinishedAmbiguousRotationKeepsItsWorkerDelayAndPendingAudit()
    {
        var f = await ReservationTestFixture.CreateAsync();
        var write = await f.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        await f.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.NotPersisted);
        f.Backend.Unwrap = (_, provider, _, _) => ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.None, new byte[32], provider));
        var clock = Substitute.For<TimeProvider>();
        clock.TimestampFrequency.Returns(TimeSpan.TicksPerSecond); clock.GetTimestamp().Returns(_ => f.Clock.GetTimestamp());
        Action? expire = null;
        clock.CreateTimer(Arg.Any<TimerCallback>(), Arg.Any<object?>(), Arg.Any<TimeSpan>(), Arg.Any<TimeSpan>()).Returns(call =>
        { expire = () => call.Arg<TimerCallback>()(call.ArgAt<object?>(1)); return Substitute.For<ITimer>(); });
        var late = new TaskCompletionSource<PayloadBackendResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Backend.WrapExact = (_, _, _, _) => { expire!(); return new(late.Task); };
        const string operation = "01J00000000000000000009202";
        var rotation = new PayloadKeyRotationCoordinator(f.Store, new([new("tenant-a", "parties")]), f.Leases, f.Backend, new(clock), _ => true, "fixture-actor", f.Clock);
        (await rotation.RewrapAsync(f.Identity, write.Completion!.KeyReference, 1, operation, "fixture:version-02")).ShouldBeFalse();
        var claim = (await f.Store.ReadAsync<ReconciliationV1>(PayloadProtectionStateKeys.Reconciliation(operation)))!.Value;
        (PayloadProtectionStateCodec.ParseTimestamp(claim.NotBeforeUtc) - f.Clock.GetUtcNow()).ShouldBe(TimeSpan.FromSeconds(90));
        (await new PayloadReconciliationClaims(f.Store, f.Clock).TryClaimAsync(new(operation, f.Scope))).ShouldBeNull();
        f.Store.Audits.Single(a => a.OperationId == operation).Result.ShouldBe("Pending");
        byte[] response = Enumerable.Repeat((byte)7, 32).ToArray();
        late.SetResult(new(PayloadBackendFailure.None, response, "fixture:version-02"));
        await f.Leases.LocalOperations.DrainScopeAsync(f.Scope).AsTask().WaitAsync(TimeSpan.FromSeconds(2));
        response.ShouldAllBe(b => b == 0);
    }

    [Theory]
    [InlineData("format")]
    [InlineData("invalid-operation")]
    [InlineData("overflow")]
    public async Task WorkerSurvivesANonStateRootFaultAndVisitsTheNextRootBeforeSleeping(string failure)
    {
        var f = await ReservationTestFixture.CreateAsync();
        string second = PayloadProtectionStateKeys.TenantDomainDigest("tenant-b", "parties");
        await new PayloadProtectionCatalogs(f.Store, f.Clock).InitializeAsync(second);
        var visited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool faulted = false;
        f.Store.Reading = key =>
        {
            if (key == PayloadProtectionStateKeys.WorkHead(second)) { visited.TrySetResult(); }
            if (faulted || key != PayloadProtectionStateKeys.WorkHead(f.Root)) { return; }
            faulted = true;
            throw failure == "format" ? new PayloadProtectionFormatException() : failure == "overflow" ? new OverflowException() : new InvalidOperationException();
        };
        await using var cache = new PayloadKeyCache(timeProvider: f.Clock);
        using var worker = new PayloadProtectionReconciler(f.Store, new([new("tenant-a", "parties"), new("tenant-b", "parties")]), Lifecycle(f, cache), f.Backend.BackendId, _ => true, "fixture-actor", f.Clock);
        await worker.StartAsync(default);
        await visited.Task.WaitAsync(TimeSpan.FromMilliseconds(800));
        await worker.StopAsync(default);
        worker.ExecuteTask!.IsFaulted.ShouldBeFalse(); faulted.ShouldBeTrue();
    }

    [Fact]
    public async Task DueStartedCreateReconcilesByStableIdEvenWhenAcknowledgementFails()
    {
        var f = await ReservationTestFixture.CreateAsync();
        const string create = "01J00000000000000000009203"; const string barrier = "01J00000000000000000009204";
        string now = PayloadProtectionStateCodec.Timestamp(f.Clock.GetUtcNow());
        var catalogs = new PayloadProtectionCatalogs(f.Store, f.Clock);
        await f.Store.CommitAsync([PayloadProtectionStateMutation.Put(new OperationV1(create, "Create", f.Scope, "01J00000000000000000009205", 1, "Started", 1, now, now, "Pending", "none")),
            PayloadProtectionStateMutation.Put(new ReconciliationV1(create, f.Scope, "01J00000000000000000009205", 1, "Create", now, 0, "Pending", "none", now)),
            .. catalogs.AddWork(await catalogs.ReadWorkAsync(f.Root), new(create, f.Scope))]);
        f.Backend.Reconcile = (operation, _) => { operation.ShouldBe(create); return ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.Missing)); };
        await using var cache = new PayloadKeyCache(timeProvider: f.Clock);
        var lifecycle = Lifecycle(f, cache);
        await lifecycle.BeginAsync(f.Identity, LifecycleDrainTests.Approval(f.Identity), barrier, 2);
        f.Store.Committing = mutations => { if (mutations.Any(m => m.Key.StartsWith("eventstore:pp:v2:clear-ack:", StringComparison.Ordinal))) { throw new InvalidOperationException(); } };
        using var worker = new PayloadProtectionReconciler(f.Store, new([new("tenant-a", "parties")]), lifecycle, f.Backend.BackendId, _ => true, "fixture-actor", f.Clock, backend: f.Backend);
        await worker.RunPassAsync();
        (await f.Store.ReadAsync<OperationV1>(PayloadProtectionStateKeys.Operation(create)))!.Value.ReasonCode.ShouldBe("not-persisted");
        f.Store.Audits.Single(a => a.OperationId == create).Operation.ShouldBe("Reconcile"); f.Backend.WrapCalls.ShouldBe(0); f.Backend.ReconcileCalls.ShouldBe(1);
    }

    [Fact]
    public async Task UnknownUncommittedBarrierReturnsRetryableUnavailableAndDefiniteConflictReturnsMismatch()
    {
        var f = await ReservationTestFixture.CreateAsync();
        await using var cache = new PayloadKeyCache(timeProvider: f.Clock);
        foreach (var result in new[] { PayloadStateCommitResult.Unknown, PayloadStateCommitResult.Conflict })
        {
            f.Store.NextCommitResult = result;
            var coordinator = new KeyLifecycleCoordinator(f.Store, new([new("tenant-a", "parties")]), f.Leases, cache, f.Backend.BackendId, _ => true, "fixture-actor", f.Clock);
            (await Should.ThrowAsync<PayloadProtectionStateException>(() => coordinator.BeginAsync(f.Identity, LifecycleDrainTests.Approval(f.Identity), "01J00000000000000000009206", 2).AsTask())).Reason
                .ShouldBe(result == PayloadStateCommitResult.Unknown ? UnreadableProtectedDataReason.ProviderUnavailable : UnreadableProtectedDataReason.ConsistencyMismatch);
        }
    }

    private static KeyLifecycleCoordinator Lifecycle(ReservationTestFixture f, PayloadKeyCache cache)
        => new(f.Store, new([new("tenant-a", "parties")]), f.Leases, cache, f.Backend.BackendId, _ => true, "fixture-actor", f.Clock);
}
