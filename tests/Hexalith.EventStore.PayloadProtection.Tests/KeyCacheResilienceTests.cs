using System.Security.Cryptography;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>V069-V078 and V085-V097 cache ownership, cancellation, retry and breaker boundaries.</summary>
public sealed class KeyCacheResilienceTests
{
    private static PayloadKeyCacheKey Key(string scope = "a", uint version = 1) => new(new string(scope[0], 64), "01J00000000000000000000000", version, 1, "fixture-provider-version");

    [Fact]
    public async Task IdleAbsoluteExpiryClearsTheOwnedArrayWithoutAnotherCacheAccess()
    {
        var cleared = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observer = new RecordingBufferObserver(kind => { if (kind == SensitiveBufferKind.DataEncryptionKey) { cleared.TrySetResult(); } });
        byte[] owned = Enumerable.Repeat((byte)0x76, 32).ToArray();
        await using var cache = new PayloadKeyCache(ttl: TimeSpan.FromMilliseconds(50), timeProvider: TimeProvider.System, observer: observer);
        using (var copy = await cache.GetAsync(Key(), _ => ValueTask.FromResult(PayloadKeyResolution.Success(owned)))) { copy.Reason.ShouldBeNull(); }
        await cleared.Task.WaitAsync(TimeSpan.FromSeconds(2));
        owned.ShouldAllBe(value => value == 0);
        observer.Observed.ShouldContain(SensitiveBufferKind.DataEncryptionKey);
    }

    [Theory]
    [InlineData(UnreadableProtectedDataReason.ProviderDenied, false)]
    [InlineData(UnreadableProtectedDataReason.ProviderDenied, true)]
    [InlineData(UnreadableProtectedDataReason.KeyInvalidatedOrDeleted, false)]
    [InlineData(UnreadableProtectedDataReason.KeyInvalidatedOrDeleted, true)]
    public async Task TypedSnapshotCoreAndRouterPreserveReturnedAndThrownDenialWithoutState(UnreadableProtectedDataReason reason, bool thrown)
    {
        var snapshot = CompatibilityTestData.ProtectSnapshot();
        ValueTask<PayloadKeyResolution> Resolve(string _, uint __, CancellationToken ___) => thrown
            ? ValueTask.FromException<PayloadKeyResolution>(new PayloadProtectionStateException(reason))
            : ValueTask.FromResult(PayloadKeyResolution.Failure(reason));
        var result = await new PayloadProtectionCore().TryUnprotectSnapshotAsync(snapshot, TestFixture.SnapshotContext(), Resolve);
        result.UnreadableReason.ShouldBe(reason); result.PayloadBytes.ShouldBeNull();
        var router = new PayloadCompatibilityRouter(Resolve, snapshotTypes: CompatibilityTestData.Registry());
        var routed = await router.ReadSnapshotAsync(CompatibilityTestData.Snapshot(snapshot, CompatibilityTestData.V2Metadata()));
        routed.UnreadableReason.ShouldBe(reason); routed.State.ShouldBeNull(); routed.Metadata.ShouldBeNull();
        routed.IsReadable.ShouldBeFalse(); routed.AllowsCorruptLegacyDeletion.ShouldBeFalse();
        CompatibilityTestData.ShouldNotLeak(routed);
    }

    [Fact]
    public async Task BreakersShareEquivalentIdentitiesAndIsolateEitherChangedIdentityComponent()
    {
        var clock = new LifecycleTestClock();
        var executor = new PayloadBackendExecutor(clock);
        var original = new IdentityTestBackend("first-backend", new('a', 64));
        var equivalent = new IdentityTestBackend("first-backend", new('a', 64));
        var otherBackend = new IdentityTestBackend("other-backend", new('a', 64));
        var otherEndpoint = new IdentityTestBackend("first-backend", new('b', 64));
        int failedCalls = 0; int successCalls = 0;
        ValueTask<PayloadBackendResult> Fail(CancellationToken _) { failedCalls++; return ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.Unavailable)); }
        var outputs = new List<byte[]>();
        ValueTask<PayloadBackendResult> Succeed(CancellationToken _)
        {
            successCalls++;
            byte[] owned = Enumerable.Repeat((byte)0x53, 32).ToArray(); outputs.Add(owned);
            return ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.None, owned));
        }
        for (int attempt = 0; attempt < 5; attempt++)
        {
            using var failed = await executor.ExecuteAsync(attempt % 2 == 0 ? original : equivalent, Fail, allowRetry: false);
            failed.Failure.ShouldBe(PayloadBackendFailure.Unavailable);
        }
        using (var blocked = await executor.ExecuteAsync(equivalent, Succeed, allowRetry: false)) { blocked.Failure.ShouldBe(PayloadBackendFailure.Unavailable); }
        failedCalls.ShouldBe(5); successCalls.ShouldBe(0);
        foreach (var backend in new[] { otherBackend, otherEndpoint })
        {
            using var success = await executor.ExecuteAsync(backend, Succeed, allowRetry: false);
            success.Failure.ShouldBe(PayloadBackendFailure.None);
            byte[] bytes = success.TakeBytes(); bytes.ShouldAllBe(value => value == 0x53); CryptographicOperations.ZeroMemory(bytes);
        }
        successCalls.ShouldBe(2); failedCalls.ShouldBe(5); outputs.ShouldAllBe(bytes => bytes.All(value => value == 0));
    }

    [Fact]
    public async Task V076_GlobalShutdownDoesNotWaitForACancellationIgnoringLoaderAndClearsItsLateArray()
    {
        var cache = new PayloadKeyCache();
        var pending = new TaskCompletionSource<PayloadKeyResolution>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<PayloadKeyResolution> read = cache.GetAsync(Key(), _ => new(pending.Task)).AsTask();
        await cache.DisposeAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => read);
        byte[] late = Enumerable.Repeat((byte)0x97, 32).ToArray();
        var cleared = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        pending.SetResult(PayloadKeyResolution.Success(late, () => cleared.SetResult()));
        await cleared.Task;
        late.ShouldAllBe(b => b == 0);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(2000, true)]
    [InlineData(-1, false)]
    [InlineData(2001, false)]
    public async Task V086_RetryAfterHonorsOnlyTheApprovedNonnegativeTwoSecondRange(int milliseconds, bool honored)
    {
        var clock = new LifecycleTestClock();
        var pauses = new List<TimeSpan>();
        var executor = new PayloadBackendExecutor(clock, (pause, _) => { pauses.Add(pause); clock.Advance(pause); return ValueTask.CompletedTask; });
        using PayloadBackendResult result = await executor.ExecuteAsync(new LifecycleTestBackend(), _ => ValueTask.FromResult(
            new PayloadBackendResult(PayloadBackendFailure.Unavailable, retryAfter: TimeSpan.FromMilliseconds(milliseconds))));
        pauses.Count.ShouldBe(2);
        if (honored) { pauses.ShouldAllBe(p => p == TimeSpan.FromMilliseconds(milliseconds)); }
        else { pauses[0].TotalMilliseconds.ShouldBeInRange(160, 240); pauses[1].TotalMilliseconds.ShouldBeInRange(400, 600); }
    }

    [Fact]
    public async Task V070_V075_OneHundredWaitersShareOneUnwrapAndCancellationDetachesOnlyOne()
    {
        await using var cache = new PayloadKeyCache();
        var release = new TaskCompletionSource<PayloadKeyResolution>(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        ValueTask<PayloadKeyResolution> Load(CancellationToken _) { Interlocked.Increment(ref calls); return new(release.Task); }
        using var cancelled = new CancellationTokenSource();
        Task<PayloadKeyResolution> first = cache.GetAsync(Key(), Load, cancelled.Token).AsTask();
        Task<PayloadKeyResolution>[] rest = Enumerable.Range(0, 100).Select(_ => cache.GetAsync(Key(), Load).AsTask()).ToArray();
        cancelled.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => first);
        release.SetResult(PayloadKeyResolution.Success(Enumerable.Repeat((byte)0x9a, 32).ToArray()));
        PayloadKeyResolution[] results = await Task.WhenAll(rest);
        calls.ShouldBe(1);
        var copies = results.Select(r => r.TakeBytes()).ToArray();
        copies.Distinct(ReferenceEqualityComparer.Instance).Count().ShouldBe(100);
        copies.All(b => b.All(x => x == 0x9a)).ShouldBeTrue();
        foreach ((PayloadKeyResolution resolution, byte[] bytes) in results.Zip(copies)) { CryptographicOperations.ZeroMemory(bytes); resolution.Dispose(); }
    }

    [Fact]
    public async Task V071_V072_V077_AbsoluteExpiryAndDeterministicEvictionClearOwnedArrays()
    {
        var clock = new LifecycleTestClock();
        var owned = new List<byte[]>();
        int calls = 0;
        ValueTask<PayloadKeyResolution> Load(CancellationToken _) { calls++; byte[] bytes = Enumerable.Repeat((byte)calls, 32).ToArray(); owned.Add(bytes); return ValueTask.FromResult(PayloadKeyResolution.Success(bytes)); }
        await using var cache = new PayloadKeyCache(1, TimeSpan.FromSeconds(60), clock);
        using (PayloadKeyResolution first = await cache.GetAsync(Key(), Load)) { first.Reason.ShouldBeNull(); }
        clock.Advance(TimeSpan.FromSeconds(60) - TimeSpan.FromTicks(1));
        using (PayloadKeyResolution hit = await cache.GetAsync(Key(), Load)) { hit.Reason.ShouldBeNull(); }
        calls.ShouldBe(1);
        clock.Advance(TimeSpan.FromTicks(1));
        using (PayloadKeyResolution expired = await cache.GetAsync(Key(), Load)) { expired.Reason.ShouldBeNull(); }
        owned[0].ShouldAllBe(x => x == 0);
        using (PayloadKeyResolution replacement = await cache.GetAsync(Key(version: 2), Load)) { replacement.Reason.ShouldBeNull(); }
        owned[1].ShouldAllBe(x => x == 0);
        await cache.ClearScopeAsync(Key().ScopeDigest);
        owned[2].ShouldAllBe(x => x == 0);
        Should.Throw<PayloadProtectionFormatException>(() => new PayloadKeyCache(4097));
        Should.Throw<PayloadProtectionFormatException>(() => new PayloadKeyCache(ttl: TimeSpan.FromMinutes(5) + TimeSpan.FromTicks(1)));
    }

    [Fact]
    public async Task V065_V077_ClearingWaitsForOwnedCopiesAndPreventsLatePopulation()
    {
        await using var cache = new PayloadKeyCache();
        byte[] owned = Enumerable.Repeat((byte)0xb3, 32).ToArray();
        PayloadKeyResolution copy = await cache.GetAsync(Key(), _ => ValueTask.FromResult(PayloadKeyResolution.Success(owned)));
        Task clearing = cache.ClearScopeAsync(Key().ScopeDigest).AsTask();
        clearing.IsCompleted.ShouldBeFalse();
        owned.ShouldAllBe(x => x == 0);
        copy.Dispose();
        await clearing;
        using PayloadKeyResolution blocked = await cache.GetAsync(Key(), _ => throw new InvalidOperationException("must never unwrap"));
        blocked.Reason.ShouldBe(UnreadableProtectedDataReason.KeyInvalidatedOrDeleted);

        var pending = new TaskCompletionSource<PayloadKeyResolution>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<PayloadKeyResolution> late = cache.GetAsync(Key("b"), _ => new(pending.Task)).AsTask();
        Task drain = cache.ClearScopeAsync(Key("b").ScopeDigest).AsTask();
        drain.IsCompleted.ShouldBeFalse();
        byte[] lateBytes = Enumerable.Repeat((byte)0xc1, 32).ToArray();
        pending.SetResult(PayloadKeyResolution.Success(lateBytes));
        using PayloadKeyResolution denied = await late;
        await drain;
        denied.Reason.ShouldBe(UnreadableProtectedDataReason.KeyInvalidatedOrDeleted);
        lateBytes.ShouldAllBe(x => x == 0);
    }

    [Fact]
    public async Task V076_GlobalShutdownCancelsAllWaitersAndClearsLateOutput()
    {
        var cache = new PayloadKeyCache();
        var pending = new TaskCompletionSource<PayloadKeyResolution>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<PayloadKeyResolution> first = cache.GetAsync(Key(), async token => { await Task.Delay(Timeout.InfiniteTimeSpan, token); return await pending.Task; }).AsTask();
        Task<PayloadKeyResolution> second = cache.GetAsync(Key(), _ => new(pending.Task)).AsTask();
        await cache.DisposeAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => first);
        await Should.ThrowAsync<OperationCanceledException>(() => second);
    }

    [Theory]
    [InlineData(UnreadableProtectedDataReason.ProviderDenied)]
    [InlineData(UnreadableProtectedDataReason.KeyInvalidatedOrDeleted)]
    [InlineData(UnreadableProtectedDataReason.ConsistencyMismatch)]
    public async Task TypedCoreAndRouterPreserveFailureReasons(UnreadableProtectedDataReason reason)
    {
        var core = new PayloadProtectionCore();
        CoreUnprotectionResult result = await core.TryUnprotectEventAsync(CompatibilityTestData.V2Json(), TestFixture.Context(),
            (_, _, _) => ValueTask.FromResult(PayloadKeyResolution.Failure(reason)));
        result.UnreadableReason.ShouldBe(reason);
        result.PayloadBytes.ShouldBeNull();
        var router = new PayloadCompatibilityRouter((_, _, _) => ValueTask.FromResult(PayloadKeyResolution.Failure(reason)));
        CompatibilityEventReadResult routed = await router.ReadEventAsync(CompatibilityTestData.V2Event(1));
        CompatibilityTestData.ShouldNotLeak(routed);
        routed.UnreadableReason.ShouldBe(reason);
        CoreUnprotectionResult thrown = await core.TryUnprotectEventAsync(CompatibilityTestData.V2Json(), TestFixture.Context(),
            (_, _, _) => ValueTask.FromException<PayloadKeyResolution>(new PayloadProtectionStateException(reason)));
        thrown.UnreadableReason.ShouldBe(reason);
    }

    [Fact]
    public async Task V085_V086_V087_TransientRetriesHaveThreeAttemptAndDelayBounds()
    {
        var clock = new LifecycleTestClock();
        var pauses = new List<TimeSpan>();
        var executor = new PayloadBackendExecutor(clock, (pause, _) => { pauses.Add(pause); clock.Advance(pause); return ValueTask.CompletedTask; });
        int calls = 0;
        using PayloadBackendResult result = await executor.ExecuteAsync(new LifecycleTestBackend(), _ => { calls++; return ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.Unavailable)); });
        result.Failure.ShouldBe(PayloadBackendFailure.Unavailable);
        calls.ShouldBe(3);
        pauses.Count.ShouldBe(2);
        pauses[0].TotalMilliseconds.ShouldBeInRange(160, 240);
        pauses[1].TotalMilliseconds.ShouldBeInRange(400, 600);
    }

    [Theory]
    [InlineData(PayloadBackendFailure.Denied)]
    [InlineData(PayloadBackendFailure.Missing)]
    [InlineData(PayloadBackendFailure.Invalidated)]
    [InlineData(PayloadBackendFailure.ConsistencyMismatch)]
    [InlineData(PayloadBackendFailure.Ambiguous)]
    public async Task PermanentAndAmbiguousFailuresAreNeverBlindlyRetried(int failureValue)
    {
        var failure = (PayloadBackendFailure)failureValue;
        int calls = 0;
        using PayloadBackendResult result = await new PayloadBackendExecutor().ExecuteAsync(new LifecycleTestBackend(), _ => { calls++; return ValueTask.FromResult(new PayloadBackendResult(failure)); });
        result.Failure.ShouldBe(failure);
        calls.ShouldBe(1);
    }

    [Fact]
    public async Task V092_V093_V094_V095_V096_BreakerAllowsOneProbeAndCapsDoubledIntervals()
    {
        var clock = new LifecycleTestClock();
        var executor = new PayloadBackendExecutor(clock);
        var backend = new LifecycleTestBackend();
        int calls = 0;
        ValueTask<PayloadBackendResult> Fail(CancellationToken _) { calls++; return ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.Unavailable)); }
        for (int i = 0; i < 5; i++) { using PayloadBackendResult _ = await executor.ExecuteAsync(backend, Fail, allowRetry: false); }
        using (PayloadBackendResult open = await executor.ExecuteAsync(backend, Fail)) { open.Failure.ShouldBe(PayloadBackendFailure.Unavailable); }
        calls.ShouldBe(5);
        clock.Advance(TimeSpan.FromSeconds(30));
        var release = new TaskCompletionSource<PayloadBackendResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<PayloadBackendResult> probe = executor.ExecuteAsync(backend, _ => new(release.Task)).AsTask();
        for (int i = 0; i < 99; i++) { using PayloadBackendResult rejected = await executor.ExecuteAsync(backend, Fail); rejected.Failure.ShouldBe(PayloadBackendFailure.Unavailable); }
        calls.ShouldBe(5);
        release.SetResult(new(PayloadBackendFailure.Unavailable));
        using PayloadBackendResult failedProbe = await probe;
        foreach (int interval in new[] { 60, 120, 240, 300 })
        {
            clock.Advance(TimeSpan.FromSeconds(interval) - TimeSpan.FromTicks(1));
            int before = calls;
            using (PayloadBackendResult rejected = await executor.ExecuteAsync(backend, Fail, allowRetry: false)) { calls.ShouldBe(before); }
            clock.Advance(TimeSpan.FromTicks(1));
            using PayloadBackendResult failed = await executor.ExecuteAsync(backend, Fail, allowRetry: false);
            calls.ShouldBe(before + 1);
        }
        clock.Advance(TimeSpan.FromSeconds(300));
        using PayloadBackendResult success = await executor.ExecuteAsync(backend, _ => ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.None, new byte[32])));
        success.Failure.ShouldBe(PayloadBackendFailure.None);
        using PayloadBackendResult next = await executor.ExecuteAsync(backend, Fail, allowRetry: false);
        calls.ShouldBe(10);
    }

    [Theory]
    [InlineData(PayloadBackendFailure.Denied)]
    [InlineData(PayloadBackendFailure.Missing)]
    [InlineData(PayloadBackendFailure.Invalidated)]
    [InlineData(PayloadBackendFailure.ConsistencyMismatch)]
    [InlineData(PayloadBackendFailure.Ambiguous)]
    [InlineData(PayloadBackendFailure.Unavailable)]
    public async Task EveryFailedHalfOpenProbeDoublesItsIntervalEvenForAPermanentFailure(int failureValue)
    {
        var clock = new LifecycleTestClock(); var backend = new LifecycleTestBackend(); var executor = new PayloadBackendExecutor(clock);
        int calls = 0;
        ValueTask<PayloadBackendResult> Fail(PayloadBackendFailure reason) { calls++; return ValueTask.FromResult(new PayloadBackendResult(reason)); }
        for (int i = 0; i < 5; i++) { using var result = await executor.ExecuteAsync(backend, _ => Fail(PayloadBackendFailure.Unavailable), allowRetry: false); }
        clock.Advance(TimeSpan.FromSeconds(30));
        using var probe = await executor.ExecuteAsync(backend, _ => Fail((PayloadBackendFailure)failureValue), allowRetry: false);
        foreach (int interval in new[] { 60, 120, 240, 300 })
        {
            int before = calls; clock.Advance(TimeSpan.FromSeconds(interval) - TimeSpan.FromTicks(1));
            using var rejected = await executor.ExecuteAsync(backend, _ => Fail((PayloadBackendFailure)failureValue), allowRetry: false);
            calls.ShouldBe(before); clock.Advance(TimeSpan.FromTicks(1));
            using var failed = await executor.ExecuteAsync(backend, _ => Fail((PayloadBackendFailure)failureValue), allowRetry: false);
            calls.ShouldBe(before + 1);
        }
    }

    [Fact]
    public async Task V089_CallerCancellationIsImmediateAndLateBackendOutputIsCleared()
    {
        var pending = new TaskCompletionSource<PayloadBackendResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        Task<PayloadBackendResult> request = new PayloadBackendExecutor().ExecuteAsync(new LifecycleTestBackend(), _ => new(pending.Task), cancellation.Token).AsTask();
        cancellation.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => request);
        byte[] late = Enumerable.Repeat((byte)0xf1, 32).ToArray();
        pending.SetResult(new(PayloadBackendFailure.None, late));
        for (int i = 0; i < 100 && late.Any(b => b != 0); i++) { await Task.Delay(1); }
        late.ShouldAllBe(b => b == 0);
    }
}
