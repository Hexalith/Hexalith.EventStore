// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; amendment section 8.
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Competing workers, foreground ownership, barrier evidence and bounded evidence callbacks.</summary>
public sealed class ReviewOwnershipTests
{
    [Fact]
    public async Task TwoWorkersCannotReplaceTheOwnerDuringItsFreshFullDrain()
    {
        ReservationTestFixture fixture = await ReservationTestFixture.CreateAsync();
        await using var cache = new PayloadKeyCache(timeProvider: fixture.Clock);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var roots = new PayloadProtectionScopeRegistry([new("tenant-a", "parties")]);
        var lifecycle = new KeyLifecycleCoordinator(fixture.Store, roots, fixture.Leases, cache, fixture.Backend.BackendId, _ => true, "fixture-actor", fixture.Clock,
            async (duration, token) => { started.TrySetResult(); await release.Task.WaitAsync(token); fixture.Clock.Advance(duration); });
        const string operation = "01J00000000000000000000601";
        var approval = LifecycleDrainTests.Approval(fixture.Identity);
        InvalidationDrainV1 drain = await lifecycle.BeginAsync(fixture.Identity, approval, operation, 2);
        drain.WorkflowId.ShouldBe(approval.Identity.WorkflowId);
        using var first = new PayloadProtectionReconciler(fixture.Store, roots, lifecycle, fixture.Backend.BackendId, _ => true, "fixture-actor", fixture.Clock);
        using var second = new PayloadProtectionReconciler(fixture.Store, roots, lifecycle, fixture.Backend.BackendId, _ => true, "fixture-actor", fixture.Clock);
        Task<int> running = first.RunPassAsync().AsTask();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        ReconciliationV1 owner = (await fixture.Store.ReadAsync<ReconciliationV1>(PayloadProtectionStateKeys.Reconciliation(operation)))!.Value;
        owner.Attempt.ShouldBe(1u); owner.Status.ShouldBe("Running");
        (await second.RunPassAsync()).ShouldBe(0);
        (await fixture.Store.ReadAsync<ReconciliationV1>(PayloadProtectionStateKeys.Reconciliation(operation)))!.Value.ShouldBe(owner);
        release.SetResult(); (await running).ShouldBe(1);
        fixture.Store.Audits.Where(a => a.OperationId == operation).ShouldAllBe(a => a.WorkflowId == drain.WorkflowId);
        fixture.Store.Audits.Single(a => a.OperationId == operation && a.Result == "Succeeded").ToState.ShouldBe("Invalidated");
    }

    [Fact]
    public async Task LiveForegroundCreateDefersRecoveryForNinetySeconds()
    {
        ReservationTestFixture fixture = await ReservationTestFixture.CreateAsync();
        var response = new TaskCompletionSource<PayloadBackendResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Backend.Wrap = (_, _, _) => new(response.Task);
        Task<(CoreProtectionResult Result, PayloadProtectionCompletionContext? Completion)> creating = fixture.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context()).AsTask();
        await using var cache = new PayloadKeyCache(timeProvider: fixture.Clock);
        var roots = new PayloadProtectionScopeRegistry([new("tenant-a", "parties")]);
        var lifecycle = new KeyLifecycleCoordinator(fixture.Store, roots, fixture.Leases, cache, fixture.Backend.BackendId, _ => true, "fixture-actor", fixture.Clock);
        using var worker = new PayloadProtectionReconciler(fixture.Store, roots, lifecycle, fixture.Backend.BackendId, _ => true, "fixture-actor", fixture.Clock);
        (await worker.RunPassAsync()).ShouldBe(0);
        WorkEntryV1 entry = (await new PayloadProtectionCatalogs(fixture.Store, fixture.Clock).ReadWorkAsync(fixture.Root)).Pages.Single().Value.Entries.Single();
        ReconciliationV1 intent = (await fixture.Store.ReadAsync<ReconciliationV1>(PayloadProtectionStateKeys.Reconciliation(entry.OperationId)))!.Value;
        (PayloadProtectionStateCodec.ParseTimestamp(intent.NotBeforeUtc) - fixture.Clock.GetUtcNow()).ShouldBe(TimeSpan.FromSeconds(90));
        response.SetResult(new(PayloadBackendFailure.None, new byte[32], "fixture:version-01"));
        var write = await creating;
        (await fixture.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(fixture.Scope, write.Completion!.KeyReference, 1)))!.Value.State.ShouldBe("Reserved");
        fixture.Backend.WrapCalls.ShouldBe(1);
    }

    [Fact]
    public async Task PersistedEvidenceUnderInvalidatingCompletesBookkeepingWithoutActivatingAndAllowsDeletion()
    {
        ReservationTestFixture fixture = await ReservationTestFixture.CreateAsync();
        var write = await fixture.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        await fixture.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.Unknown);
        await using var cache = new PayloadKeyCache(timeProvider: fixture.Clock);
        var roots = new PayloadProtectionScopeRegistry([new("tenant-a", "parties")]);
        var lifecycle = new KeyLifecycleCoordinator(fixture.Store, roots, fixture.Leases, cache, fixture.Backend.BackendId, _ => true, "fixture-actor", fixture.Clock,
            (duration, _) => { fixture.Clock.Advance(duration); return ValueTask.CompletedTask; });
        const string deletion = "01J00000000000000000000602";
        await lifecycle.BeginAsync(fixture.Identity, LifecycleDrainTests.Approval(fixture.Identity), deletion, 2, true);
        using var worker = new PayloadProtectionReconciler(fixture.Store, roots, lifecycle, fixture.Backend.BackendId, _ => true, "fixture-actor", fixture.Clock,
            persistence: new LifecyclePersistenceEvidence(PayloadProtectionPersistenceOutcome.Persisted));
        await worker.RunPassAsync();
        OperationV1 persisted = (await fixture.Store.ReadAsync<OperationV1>(PayloadProtectionStateKeys.Operation(write.Completion!.OperationId)))!.Value;
        persisted.Phase.ShouldBe("Persisted"); persisted.Result.ShouldBe("Succeeded");
        var retained = await fixture.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(fixture.Scope, write.Completion.KeyReference, 1));
        if (retained is not null) { retained.Value.State.ShouldBe("Reserved"); }
        // ULID order may visit deletion first; its next due owner then gets a fresh full wait.
        fixture.Clock.Advance(TimeSpan.FromSeconds(90));
        await worker.RunPassAsync();
        (await fixture.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(fixture.Scope, write.Completion.KeyReference, 1))).ShouldBeNull();
        (await fixture.Store.ReadAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(fixture.Scope)))!.Value.State.ShouldBe("Deleted");
        fixture.Store.Audits.Where(a => a.Operation == "Activate").ShouldAllBe(a => a.ToState == "Reserved");
    }

    [Fact]
    public async Task CancelledForegroundWithAnUnfinishedWrapDoesNotReleaseItsInitialRecoveryDelay()
    {
        ReservationTestFixture f = await ReservationTestFixture.CreateAsync();
        var late = new TaskCompletionSource<PayloadBackendResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Backend.Wrap = (_, _, _) => new(late.Task);
        using var cancellation = new CancellationTokenSource();
        Task pending = f.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context(), cancellation.Token).AsTask();
        cancellation.Cancel(); await Should.ThrowAsync<OperationCanceledException>(() => pending);
        WorkEntryV1 entry = (await new PayloadProtectionCatalogs(f.Store, f.Clock).ReadWorkAsync(f.Root)).Pages.Single().Value.Entries.Single();
        (await new PayloadReconciliationClaims(f.Store, f.Clock).TryClaimAsync(entry)).ShouldBeNull();
        ReconciliationV1 intent = (await f.Store.ReadAsync<ReconciliationV1>(PayloadProtectionStateKeys.Reconciliation(entry.OperationId)))!.Value;
        (PayloadProtectionStateCodec.ParseTimestamp(intent.NotBeforeUtc) - f.Clock.GetUtcNow()).ShouldBe(TimeSpan.FromSeconds(90));
        byte[] output = Enumerable.Repeat((byte)0xef, 32).ToArray(); late.SetResult(new(PayloadBackendFailure.None, output, "fixture:version-01"));
        await f.Leases.LocalOperations.DrainScopeAsync(f.Scope).AsTask().WaitAsync(TimeSpan.FromSeconds(2)); output.ShouldAllBe(b => b == 0);
    }

    [Fact]
    public async Task EvidenceDeadlineBoundsACancellationIgnoringCallbackAndObservesItsAbandonedFault()
    {
        var late = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var budget = new PayloadOperationBudget(TimeProvider.System, default, TimeSpan.FromMilliseconds(30));
        PayloadProtectionStateException failure = await Should.ThrowAsync<PayloadProtectionStateException>(() => PayloadOperationBudget.AwaitAsync(_ => new ValueTask<int>(late.Task), default).AsTask().WaitAsync(TimeSpan.FromSeconds(2)));
        failure.Reason.ShouldBe(UnreadableProtectedDataReason.ProviderUnavailable);
        late.SetException(new InvalidOperationException("private-late-evidence-canary"));
        failure.InnerException.ShouldBeNull(); failure.ToString().ShouldNotContain("private-late-evidence-canary");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ForegroundPersistedCompletionUnderTheBarrierRetainsReservedMaterialAndReconcilesLostAcknowledgement(bool lost)
    {
        ReservationTestFixture f = await ReservationTestFixture.CreateAsync();
        var write = await f.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        await f.Reservations.AcquireCompletionAsync(write.Completion!);
        await using var cache = new PayloadKeyCache(timeProvider: f.Clock);
        var lifecycle = new KeyLifecycleCoordinator(f.Store, new([new("tenant-a", "parties")]), f.Leases, cache, f.Backend.BackendId, _ => true, "fixture-actor", f.Clock);
        await lifecycle.BeginAsync(f.Identity, LifecycleDrainTests.Approval(f.Identity), "01J00000000000000000000603", 2);
        f.Store.LoseNextAcknowledgement = lost;
        if (lost) { await Should.ThrowAsync<PayloadProtectionStateException>(() => f.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.Persisted).AsTask()); }
        await f.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.Persisted);
        (await f.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(f.Scope, write.Completion!.KeyReference, 1)))!.Value.State.ShouldBe("Reserved");
        (await f.Store.ReadAsync<OperationV1>(PayloadProtectionStateKeys.Operation(write.Completion.OperationId)))!.Value.Phase.ShouldBe("Persisted");
        (await f.Store.ReadAsync<ReconciliationV1>(PayloadProtectionStateKeys.Reconciliation(write.Completion.OperationId)))!.Value.Status.ShouldBe("Completed");
    }

    [Fact]
    public async Task CancellationIgnoringPersistenceEvidenceCannotOccupyTheWorkerIndefinitely()
    {
        ReservationTestFixture fixture = await ReservationTestFixture.CreateAsync();
        var write = await fixture.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        await fixture.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.Unknown);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var late = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var evidence = new LifecyclePersistenceEvidence(PayloadProtectionPersistenceOutcome.Persisted) { Observed = async (_, _) => { entered.SetResult(); await late.Task; } };
        await using var cache = new PayloadKeyCache(timeProvider: fixture.Clock);
        var roots = new PayloadProtectionScopeRegistry([new("tenant-a", "parties")]);
        var lifecycle = new KeyLifecycleCoordinator(fixture.Store, roots, fixture.Leases, cache, fixture.Backend.BackendId, _ => true, "fixture-actor", fixture.Clock);
        using var worker = new PayloadProtectionReconciler(fixture.Store, roots, lifecycle, fixture.Backend.BackendId, _ => true, "fixture-actor", fixture.Clock, persistence: evidence);
        using var cancellation = new CancellationTokenSource();
        Task<int> running = worker.RunPassAsync(cancellation.Token).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2)); cancellation.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => running.WaitAsync(TimeSpan.FromSeconds(2)));
        late.SetException(new InvalidOperationException("private-evidence-late-canary"));
        (await fixture.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(fixture.Scope, write.Completion!.KeyReference, 1)))!.Value.State.ShouldBe("Reserved");
        (await new PayloadProtectionCatalogs(fixture.Store, fixture.Clock).ReadWorkAsync(fixture.Root)).Head.Value.EntryCount.ShouldBe(1u);
        (await worker.RunPassAsync()).ShouldBe(0); // Claim remains held until its 90-second due boundary.
    }
}
