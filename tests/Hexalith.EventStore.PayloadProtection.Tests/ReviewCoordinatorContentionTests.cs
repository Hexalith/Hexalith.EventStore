// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base sections 10.3/10.6; pass-3 contention and ownership regressions.
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Valid root contention retries only state transitions and never repeats a successful wrap.</summary>
public sealed class ReviewCoordinatorContentionTests
{
    [Fact]
    public async Task DelayedOriginalCompletionCommitIsReconciledWhenTheNextAttemptConflicts()
    {
        var f = await ReservationTestFixture.CreateAsync();
        var write = await f.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        PayloadProtectionStateMutation[]? original = null;
        CompletionLeaseV1? originalLease = null;
        int attempts = 0;
        f.Store.Committing = mutations =>
        {
            var leaseMutation = mutations.SingleOrDefault(m => m.Key.StartsWith("eventstore:pp:v2:completion:", StringComparison.Ordinal));
            if (leaseMutation is null) { return; }
            attempts++;
            if (original is null)
            {
                original = mutations.ToArray(); originalLease = PayloadProtectionStateCodec.Decode<CompletionLeaseV1>(leaseMutation.Bytes!);
                f.Store.NextCommitResult = PayloadStateCommitResult.Unknown;
                f.Clock.Advance(TimeSpan.FromMilliseconds(1));
            }
            else
            {
                f.Store.Committing = null;
                (f.Store.CommitAsync(original).GetAwaiter().GetResult()).ShouldBe(PayloadStateCommitResult.Committed);
            }
        };
        await f.Reservations.AcquireCompletionAsync(write.Completion!);
        attempts.ShouldBe(2);
        var persisted = (await f.Store.ReadAsync<CompletionLeaseV1>(PayloadProtectionStateKeys.Completion(f.Scope, write.Completion!.KeyReference, 1, write.Completion.OperationId)))!;
        persisted.Value.ShouldBe(originalLease);
        await f.Reservations.CompleteAsync(write.Completion, PayloadProtectionPersistenceOutcome.Persisted);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RepeatedUtcRenewalRequiresANewETagToProveTtlRefresh(bool committed)
    {
        var f = await ReservationTestFixture.CreateAsync();
        var clock = new IndependentLifecycleTestClock();
        var leases = new PayloadProtectionLeaseCoordinator(f.Store, new([new("tenant-a", "parties")]),
            new(new('b', 40), new('c', 64), f.Backend.BackendId, ["json", "json+pdenc-v2"], ["V2"]), clock);
        await leases.RegisterAsync(f.Identity);
        string key = PayloadProtectionStateKeys.Capability(f.Root, 1, leases.InstanceId);
        var before = (await f.Store.ReadAsync<CapabilityLeaseV1>(key))!;
        DateTimeOffset utc = clock.GetUtcNow(); clock.AdvanceMonotonic(TimeSpan.FromSeconds(30));
        clock.GetUtcNow().ShouldBe(utc); clock.GetTimestamp().ShouldBe(TimeSpan.FromSeconds(30).Ticks);
        int attempts = 0;
        f.Store.Committing = mutations =>
        {
            if (!mutations.Any(m => m.Key == key)) { return; }
            attempts++;
            if (committed) { f.Store.LoseNextAcknowledgement = true; }
            else { f.Store.NextCommitResult = PayloadStateCommitResult.Unknown; }
        };
        if (committed) { await leases.RenewAsync(f.Identity); attempts.ShouldBe(1); }
        else { (await Should.ThrowAsync<PayloadProtectionStateException>(() => leases.RenewAsync(f.Identity).AsTask())).Reason.ShouldBe(UnreadableProtectedDataReason.ProviderUnavailable); attempts.ShouldBeGreaterThan(1); }
        f.Store.Committing = null;
        var after = (await f.Store.ReadAsync<CapabilityLeaseV1>(key))!;
        after.Bytes.ShouldBe(before.Bytes);
        if (committed) { after.ETag.ShouldNotBe(before.ETag); }
        else { after.ETag.ShouldBe(before.ETag); }
        clock.AdvanceMonotonic(TimeSpan.FromSeconds(10));
        var lifecycle = (await f.Store.ReadAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(f.Scope)))!.Value;
        if (committed) { _ = await leases.AdmissionAsync(f.Identity, lifecycle, false); }
        else { await Should.ThrowAsync<PayloadProtectionStateException>(() => leases.AdmissionAsync(f.Identity, lifecycle, false).AsTask()); }
    }

    [Theory]
    [InlineData("read", false)]
    [InlineData("read", true)]
    [InlineData("renew", false)]
    [InlineData("renew", true)]
    [InlineData("completion", false)]
    [InlineData("completion", true)]
    [InlineData("intent", false)]
    [InlineData("intent", true)]
    [InlineData("reservation", false)]
    [InlineData("returned", false)]
    [InlineData("returned", true)]
    public async Task UnknownStateCommitReconcilesStableIdentityBeforeRetryingWithoutAnotherWrap(string boundary, bool committed)
    {
        var f = await ReservationTestFixture.CreateAsync();
        PayloadProtectionCompletionContext? completion = boundary == "completion"
            ? (await f.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context())).Completion : null;
        bool injected = false;
        f.Store.Committing = mutations =>
        {
            bool selected = boundary switch
            {
                "read" => mutations.Any(m => m.Key.StartsWith("eventstore:pp:v2:lease:", StringComparison.Ordinal)),
                "renew" => mutations.Any(m => m.Key.StartsWith("eventstore:pp:v2:capability:", StringComparison.Ordinal)),
                "completion" => mutations.Any(m => m.Key.StartsWith("eventstore:pp:v2:completion:", StringComparison.Ordinal)),
                _ => mutations.Where(m => m.Key.StartsWith("eventstore:pp:v2:operation:", StringComparison.Ordinal))
                    .Any(m => PayloadProtectionStateCodec.Decode<OperationV1>(m.Bytes!).Phase == (boundary == "intent" ? "Started" : boundary == "reservation" ? "Reserved" : "PayloadReturned")),
            };
            if (!selected) { return; }
            f.Store.Committing = null; injected = true;
            if (committed) { f.Store.LoseNextAcknowledgement = true; }
            else { f.Store.NextCommitResult = PayloadStateCommitResult.Unknown; }
        };
        if (boundary == "read")
        {
            await using var lease = await f.Leases.AcquireReadAsync(f.Identity, "", 0, "01J00000000000000000009003", (await f.Store.ReadAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(f.Scope)))!);
            lease.AssertCurrent();
            await AssertDurableReadAsync(f, "01J00000000000000000009003");
        }
        else if (boundary == "renew") { f.Clock.Advance(TimeSpan.FromSeconds(1)); await f.Leases.RenewAsync(f.Identity); }
        else if (boundary == "completion") { await f.Reservations.AcquireCompletionAsync(completion!); }
        else { _ = await f.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context()); }
        injected.ShouldBeTrue(); f.Backend.WrapCalls.ShouldBe(boundary is "read" or "renew" ? 0 : 1);
    }

    [Theory]
    [InlineData("read")]
    [InlineData("renew")]
    [InlineData("completion")]
    [InlineData("intent")]
    [InlineData("reservation")]
    [InlineData("returned")]
    public async Task UnchangedAuthorityCasConflictReReadsAndRetriesTheExactStateTransition(string boundary)
    {
        var f = await ReservationTestFixture.CreateAsync();
        PayloadProtectionCompletionContext? completion = null;
        if (boundary == "completion") { completion = (await f.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context())).Completion; }
        bool collided = false;
        f.Store.Committing = mutations =>
        {
            bool selected = boundary switch
            {
                "read" => mutations.Any(m => m.Key.StartsWith("eventstore:pp:v2:lease:", StringComparison.Ordinal)),
                "renew" => mutations.Any(m => m.Key.StartsWith("eventstore:pp:v2:capability:", StringComparison.Ordinal)),
                "completion" => mutations.Any(m => m.Key.StartsWith("eventstore:pp:v2:completion:", StringComparison.Ordinal)),
                _ => mutations.Where(m => m.Key.StartsWith("eventstore:pp:v2:operation:", StringComparison.Ordinal))
                    .Any(m => PayloadProtectionStateCodec.Decode<OperationV1>(m.Bytes!).Phase == (boundary == "intent" ? "Started" : boundary == "reservation" ? "Reserved" : "PayloadReturned")),
            };
            if (!selected) { return; }
            f.Store.Committing = null; collided = true;
            var generation = mutations.Single(m => m.Key == PayloadProtectionStateKeys.Deployment(f.Root));
            f.Store.CommitAsync([generation]).GetAwaiter().GetResult().ShouldBe(PayloadStateCommitResult.Committed);
        };
        if (boundary == "read")
        {
            var lifecycle = (await f.Store.ReadAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(f.Scope)))!;
            await using var lease = await f.Leases.AcquireReadAsync(f.Identity, "", 0, "01J00000000000000000009001", lifecycle);
            lease.AssertCurrent();
            await AssertDurableReadAsync(f, "01J00000000000000000009001");
        }
        else if (boundary == "renew") { await f.Leases.RenewAsync(f.Identity); }
        else if (boundary == "completion") { await f.Reservations.AcquireCompletionAsync(completion!); }
        else { completion = (await f.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context())).Completion; }
        collided.ShouldBeTrue();
        f.Backend.WrapCalls.ShouldBe(boundary is "read" or "renew" ? 0 : 1);
        if (completion is not null) { (await f.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(f.Scope, completion.KeyReference, 1)))!.Value.State.ShouldBe("Reserved"); }
    }

    [Fact]
    public async Task ConcurrentSameRootWritesBothReserveWithoutRewrappingAndKeepTheV2WatermarkAtOne()
    {
        var f = await ReservationTestFixture.CreateAsync();
        using var barrier = new Barrier(2);
        int entrants = 0; int wraps = 0;
        f.Backend.Wrap = (_, _, _) => { Interlocked.Increment(ref wraps); return ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.None, new byte[32], "fixture:version-01")); };
        f.Store.Committing = mutations =>
        {
            if (mutations.Any(m => m.Key.StartsWith("eventstore:pp:v2:operation:", StringComparison.Ordinal)
                && PayloadProtectionStateCodec.Decode<OperationV1>(m.Bytes!).Phase == "Started") && Interlocked.Increment(ref entrants) <= 2)
            { barrier.SignalAndWait(TimeSpan.FromSeconds(5)).ShouldBeTrue(); }
        };
        var writes = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Task.Run(async () =>
            await f.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context()))));
        f.Store.Committing = null;
        wraps.ShouldBe(2); writes.Select(w => w.Completion!.KeyReference).Distinct().Count().ShouldBe(2);
        (await f.Store.ReadAsync<FenceV1>(PayloadProtectionStateKeys.Fence(f.Root)))!.Value.HighestWrittenEpoch.ShouldBe(1ul);
        foreach (var write in writes)
        {
            (await f.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(f.Scope, write.Completion!.KeyReference, 1)))!.Value.HighestWrittenEpoch.ShouldBe(1ul);
            await f.Reservations.AcquireCompletionAsync(write.Completion!);
            await f.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.Persisted);
        }
        (await new PayloadProtectionCatalogs(f.Store, f.Clock).ReadWorkAsync(f.Root)).Head.Value.EntryCount.ShouldBe(0u);
    }

    [Theory]
    [InlineData(30, true)]
    [InlineData(31, false)]
    [InlineData(90, false)]
    public async Task CompletionLeaseRequiresSixtySecondsOfUnclaimedForegroundOwnership(int seconds, bool allowed)
    {
        var f = await ReservationTestFixture.CreateAsync();
        var write = await f.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        for (int elapsed = 0; elapsed < seconds; )
        {
            int step = Math.Min(30, seconds - elapsed); f.Clock.Advance(TimeSpan.FromSeconds(step)); elapsed += step;
            await f.Leases.RenewAsync(f.Identity);
        }
        if (allowed) { await f.Reservations.AcquireCompletionAsync(write.Completion!); }
        else
        {
            int commits = f.Store.Commits;
            await Should.ThrowAsync<PayloadProtectionStateException>(() => f.Reservations.AcquireCompletionAsync(write.Completion!).AsTask());
            f.Store.Commits.ShouldBe(commits);
            (await f.Store.ReadAsync<CompletionLeaseV1>(PayloadProtectionStateKeys.Completion(f.Scope, write.Completion!.KeyReference, 1, write.Completion.OperationId))).ShouldBeNull();
        }
    }

    [Fact]
    public async Task BarrierTimePersistedCompletionWithoutAnEarlierLeaseCannotChangeOperationOrWork()
    {
        var f = await ReservationTestFixture.CreateAsync();
        var write = await f.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        await using var cache = new PayloadKeyCache(timeProvider: f.Clock);
        var lifecycle = new KeyLifecycleCoordinator(f.Store, new([new("tenant-a", "parties")]), f.Leases, cache, f.Backend.BackendId, _ => true, "fixture-actor", f.Clock);
        await lifecycle.BeginAsync(f.Identity, LifecycleDrainTests.Approval(f.Identity), "01J00000000000000000009002", 2);
        var operation = (await f.Store.ReadAsync<OperationV1>(PayloadProtectionStateKeys.Operation(write.Completion!.OperationId)))!;
        var claim = (await f.Store.ReadAsync<ReconciliationV1>(PayloadProtectionStateKeys.Reconciliation(write.Completion.OperationId)))!;
        var work = (await new PayloadProtectionCatalogs(f.Store, f.Clock).ReadWorkAsync(f.Root)).Head;
        await Should.ThrowAsync<PayloadProtectionStateException>(() => f.Reservations.CompleteAsync(write.Completion, PayloadProtectionPersistenceOutcome.Persisted).AsTask());
        (await f.Store.ReadAsync<OperationV1>(PayloadProtectionStateKeys.Operation(write.Completion.OperationId)))!.ETag.ShouldBe(operation.ETag);
        (await f.Store.ReadAsync<ReconciliationV1>(PayloadProtectionStateKeys.Reconciliation(write.Completion.OperationId)))!.ETag.ShouldBe(claim.ETag);
        (await new PayloadProtectionCatalogs(f.Store, f.Clock).ReadWorkAsync(f.Root)).Head.ETag.ShouldBe(work.ETag);
    }

    [Fact]
    public async Task AbsentCompletionDoesNotCompareAnUnrelatedWorkHead()
    {
        var f = await ReservationTestFixture.CreateAsync();
        var write = await f.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        f.Store.Committing = mutations =>
        {
            f.Store.Committing = null;
            mutations.ShouldNotContain(m => m.Key == PayloadProtectionStateKeys.WorkHead(f.Root));
            var head = f.Store.ReadAsync<WorkHeadV1>(PayloadProtectionStateKeys.WorkHead(f.Root)).GetAwaiter().GetResult()!;
            f.Store.CommitAsync([PayloadProtectionStateMutation.Compare(head)]).GetAwaiter().GetResult().ShouldBe(PayloadStateCommitResult.Committed);
        };
        await f.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.NotPersisted);
        (await f.Store.ReadAsync<OperationV1>(PayloadProtectionStateKeys.Operation(write.Completion!.OperationId)))!.Value.Result.ShouldBe("Failed");
    }

    private static async ValueTask AssertDurableReadAsync(ReservationTestFixture f, string operation)
    {
        var durable = (await f.Store.ReadAsync<ReadLeaseV1>(PayloadProtectionStateKeys.Lease(f.Scope, operation)))!;
        durable.ShouldNotBeNull(); durable.Value.OperationId.ShouldBe(operation); durable.Value.ScopeDigest.ShouldBe(f.Scope);
        durable.Value.InstanceId.ShouldBe(f.Leases.InstanceId); durable.Value.LifecycleEpoch.ShouldBe(1ul);
        durable.Value.KeyRef.ShouldBe(""); durable.Value.DekVersion.ShouldBe(0u);
    }
}
