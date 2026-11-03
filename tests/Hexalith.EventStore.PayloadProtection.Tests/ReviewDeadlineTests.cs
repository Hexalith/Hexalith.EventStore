using Dapr.Client;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using NSubstitute;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Cancellation-ignoring callbacks and mutations remain bounded and safely observed.</summary>
public sealed class ReviewDeadlineTests
{
    [Fact]
    public async Task BlockingCancellationCallbackCannotBypassDrainDeadlineOrResumedClearingProof()
    {
        var operations = new PayloadLocalOperations();
        var identity = new AggregateIdentity("tenant-a", "parties", "callback");
        string scope = PayloadProtectionStateKeys.ScopeDigest(identity);
        using var release = new ManualResetEventSlim();
        using var deadline = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lateCleared = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var operation = operations.Begin(identity);
        operation.RetainUntil(lateCleared.Task);
        int callbacks = 0;
        using var registration = operation.Token.Register(() =>
        {
            Interlocked.Increment(ref callbacks); entered.TrySetResult();
            release.Wait(); callbackDone.TrySetResult();
        });
        try
        {
            Task initial = operations.DrainScopeAsync(scope, deadline.Token).AsTask();
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            deadline.Cancel();
            await Should.ThrowAsync<OperationCanceledException>(() => initial.WaitAsync(TimeSpan.FromSeconds(2)));
            operation.Dispose();
            await Should.ThrowAsync<TimeoutException>(() => operation.Cleared.WaitAsync(TimeSpan.FromMilliseconds(100)));
            Task resumed = operations.DrainScopeAsync(scope).AsTask();
            await Should.ThrowAsync<TimeoutException>(() => resumed.WaitAsync(TimeSpan.FromMilliseconds(100)));
            callbacks.ShouldBe(1);
            release.Set(); await callbackDone.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await Should.ThrowAsync<TimeoutException>(() => resumed.WaitAsync(TimeSpan.FromMilliseconds(100)));
            lateCleared.SetResult();
            await resumed.WaitAsync(TimeSpan.FromSeconds(2));
            operation.Cleared.IsCompletedSuccessfully.ShouldBeTrue(); callbacks.ShouldBe(1);
        }
        finally { release.Set(); lateCleared.TrySetResult(); }
    }

    [Fact]
    public async Task DrainStartedBlockingCallbackAloneHoldsResumedClearing()
    {
        var operations = new PayloadLocalOperations();
        var identity = new AggregateIdentity("tenant-a", "parties", "callback-only");
        string scope = PayloadProtectionStateKeys.ScopeDigest(identity);
        using var release = new ManualResetEventSlim();
        using var deadline = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lateCleared = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var operation = operations.Begin(identity);
        operation.RetainUntil(lateCleared.Task);
        using var registration = operation.Token.Register(() => { entered.TrySetResult(); release.Wait(); callbackDone.TrySetResult(); });
        try
        {
            Task initial = operations.DrainScopeAsync(scope, deadline.Token).AsTask();
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            deadline.Cancel();
            await Should.ThrowAsync<OperationCanceledException>(() => initial.WaitAsync(TimeSpan.FromSeconds(2)));
            // With the late result and foreground both released, only CancelAsync's reference holds clearing open.
            lateCleared.SetResult(); operation.Dispose();
            await Should.ThrowAsync<TimeoutException>(() => operation.Cleared.WaitAsync(TimeSpan.FromMilliseconds(100)));
            Task resumed = operations.DrainScopeAsync(scope).AsTask();
            await Should.ThrowAsync<TimeoutException>(() => resumed.WaitAsync(TimeSpan.FromMilliseconds(100)));
            await Should.ThrowAsync<TimeoutException>(() => operation.Cleared.WaitAsync(TimeSpan.FromMilliseconds(100)));
            release.Set(); await callbackDone.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await resumed.WaitAsync(TimeSpan.FromSeconds(2));
            operation.Cleared.IsCompletedSuccessfully.ShouldBeTrue();
        }
        finally { release.Set(); lateCleared.TrySetResult(); }
    }

    [Fact]
    public async Task CallerCancellationCallbackCannotBlockTheDrainDeadline()
    {
        var operations = new PayloadLocalOperations();
        var identity = new AggregateIdentity("tenant-a", "parties", "caller-callback");
        string scope = PayloadProtectionStateKeys.ScopeDigest(identity);
        using var release = new ManualResetEventSlim();
        using var caller = new CancellationTokenSource();
        using var deadline = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var operation = operations.Begin(identity, caller.Token);
        using var registration = operation.Token.Register(() => { entered.TrySetResult(); release.Wait(); });
        Task cancelling = Task.Run(caller.Cancel);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Task drain = operations.DrainScopeAsync(scope, deadline.Token).AsTask();
            operation.Dispose();
            deadline.Cancel();
            await Should.ThrowAsync<OperationCanceledException>(() => drain.WaitAsync(TimeSpan.FromSeconds(2)));
            Task resumed = operations.DrainScopeAsync(scope).AsTask();
            await Should.ThrowAsync<TimeoutException>(() => resumed.WaitAsync(TimeSpan.FromMilliseconds(100)));
            await Should.ThrowAsync<TimeoutException>(() => operation.Cleared.WaitAsync(TimeSpan.FromMilliseconds(100)));
            release.Set(); await cancelling.WaitAsync(TimeSpan.FromSeconds(2));
            await resumed.WaitAsync(TimeSpan.FromSeconds(2));
            operation.Cleared.IsCompletedSuccessfully.ShouldBeTrue();
        }
        finally { release.Set(); }
    }

    [Fact]
    public async Task DisposedForegroundRemainsInLaterDrainUntilCallerCallbackCompletes()
    {
        var operations = new PayloadLocalOperations();
        var identity = new AggregateIdentity("tenant-a", "parties", "disposed-callback");
        string scope = PayloadProtectionStateKeys.ScopeDigest(identity);
        using var release = new ManualResetEventSlim();
        using var caller = new CancellationTokenSource();
        using var deadline = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var operation = operations.Begin(identity, caller.Token);
        using var registration = operation.Token.Register(() => { entered.TrySetResult(); release.Wait(); });
        Task cancelling = Task.Run(caller.Cancel);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await Task.Run(operation.Dispose).WaitAsync(TimeSpan.FromSeconds(2));
            await Should.ThrowAsync<TimeoutException>(() => operation.Cleared.WaitAsync(TimeSpan.FromMilliseconds(100)));
            Task initial = operations.DrainScopeAsync(scope, deadline.Token).AsTask();
            await Should.ThrowAsync<TimeoutException>(() => initial.WaitAsync(TimeSpan.FromMilliseconds(100)));
            deadline.Cancel();
            await Should.ThrowAsync<OperationCanceledException>(() => initial.WaitAsync(TimeSpan.FromSeconds(2)));
            Task resumed = operations.DrainScopeAsync(scope).AsTask();
            await Should.ThrowAsync<TimeoutException>(() => resumed.WaitAsync(TimeSpan.FromMilliseconds(100)));
            await Should.ThrowAsync<TimeoutException>(() => operation.Cleared.WaitAsync(TimeSpan.FromMilliseconds(100)));
            release.Set(); await cancelling.WaitAsync(TimeSpan.FromSeconds(2));
            await resumed.WaitAsync(TimeSpan.FromSeconds(2));
            operation.Cleared.IsCompletedSuccessfully.ShouldBeTrue();
        }
        finally { release.Set(); }
    }

    [Fact]
    public async Task ThrowingOperationCallbackDuringCallerCancelStillClears()
    {
        var operations = new PayloadLocalOperations();
        var identity = new AggregateIdentity("tenant-a", "parties", "throwing-callback");
        using var caller = new CancellationTokenSource();
        using var operation = operations.Begin(identity, caller.Token);
        int invoked = 0;
        using var registration = operation.Token.Register(() =>
        {
            Interlocked.Increment(ref invoked);
            throw new InvalidOperationException("callback failure");
        });
        Exception? failure = null;
        try { caller.Cancel(throwOnFirstException: true); }
        catch (Exception exception) { failure = exception; }
        failure.ShouldNotBeNull(); invoked.ShouldBe(1);
        operation.Dispose();
        await operations.DrainScopeAsync(PayloadProtectionStateKeys.ScopeDigest(identity)).AsTask().WaitAsync(TimeSpan.FromSeconds(2));
        operation.Cleared.IsCompletedSuccessfully.ShouldBeTrue();
    }

    [Fact]
    public async Task BlockedCallbackInAnotherScopeSharingCallerDoesNotDelayThisDrain()
    {
        var operations = new PayloadLocalOperations();
        var identity = new AggregateIdentity("tenant-a", "parties", "unblocked-scope");
        var otherIdentity = new AggregateIdentity("tenant-a", "parties", "blocked-scope");
        using var caller = new CancellationTokenSource();
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        // Caller callbacks run last-in-first-out: beginning `other` second runs its blocked callback before this operation's link.
        using var operation = operations.Begin(identity, caller.Token);
        using var other = operations.Begin(otherIdentity, caller.Token);
        using var registration = other.Token.Register(() => { entered.TrySetResult(); release.Wait(); });
        Task cancelling = Task.Run(caller.Cancel);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            operation.Token.IsCancellationRequested.ShouldBeFalse();
            operation.Dispose();
            Task drained = operations.DrainScopeAsync(PayloadProtectionStateKeys.ScopeDigest(identity)).AsTask();
            await drained.WaitAsync(TimeSpan.FromSeconds(2));
            operation.Cleared.IsCompletedSuccessfully.ShouldBeTrue();
        }
        finally
        {
            release.Set();
            await cancelling.WaitAsync(TimeSpan.FromSeconds(2));
        }
    }

    [Fact]
    public async Task IgnoredErasureCancellationCannotHoldAdmissionOrLocalClearing()
    {
        var provider = Substitute.For<IErasureStateProvider>(); provider.ContractVersion.Returns(1);
        var pending = new TaskCompletionSource<PayloadErasureStateResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        provider.GetStateAsync(Arg.Any<PayloadErasureStateRequest>(), Arg.Any<CancellationToken>()).Returns(new ValueTask<PayloadErasureStateResult>(pending.Task));
        var registry = new ErasureStateRegistry(new([new("tenant-a", "parties")]), [new("parties", provider)]);
        var operations = new PayloadLocalOperations(); var identity = new AggregateIdentity("tenant-a", "parties", "party-01");
        async Task ObserveAsync()
        {
            using var local = operations.Begin(identity);
            await registry.ObserveAsync(identity, cancellationToken: local.Token);
        }
        Task observe = ObserveAsync(); Task clearing = operations.DrainScopeAsync(PayloadProtectionStateKeys.ScopeDigest(identity)).AsTask();
        await Should.ThrowAsync<OperationCanceledException>(() => observe.WaitAsync(TimeSpan.FromSeconds(2)));
        await clearing.WaitAsync(TimeSpan.FromSeconds(2));
        pending.SetException(new InvalidOperationException("private-erasure-late-canary"));
    }

    [Fact]
    public async Task ErasureAndMutationObservationsObeyTheEnclosingDeadlineWithoutBlindRetry()
    {
        var provider = Substitute.For<IErasureStateProvider>(); provider.ContractVersion.Returns(1);
        var pending = new TaskCompletionSource<PayloadErasureStateResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        provider.GetStateAsync(Arg.Any<PayloadErasureStateRequest>(), Arg.Any<CancellationToken>()).Returns(new ValueTask<PayloadErasureStateResult>(pending.Task));
        using (var budget = new PayloadOperationBudget(TimeProvider.System, default, TimeSpan.FromMilliseconds(30)))
        {
            var registry = new ErasureStateRegistry(new([new("tenant-a", "parties")]), [new("parties", provider)]);
            (await Should.ThrowAsync<PayloadProtectionStateException>(() => registry.ObserveAsync(new("tenant-a", "parties", "party-01")).AsTask().WaitAsync(TimeSpan.FromSeconds(2)))).Reason.ShouldBe(UnreadableProtectedDataReason.ProviderUnavailable);
        }
        pending.SetException(new InvalidOperationException("private-erasure-deadline-canary"));
        var clock = new SubmittedMutationDeadlineClock();
        var client = Substitute.For<DaprClient>(); var mutation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        client.ExecuteStateTransactionAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<StateTransactionRequest>>(), Arg.Any<IReadOnlyDictionary<string, string>?>(), Arg.Any<CancellationToken>()).Returns(_ => { clock.Expire(); return mutation.Task; });
        var store = new DaprPayloadProtectionStateStore(client); store.ConfirmQualification();
        // Encode the valid closed record before timing its submitted native mutation.
        // First-use serializer/JIT work is not the cancellation-ignoring I/O under test.
        var value = new WorkHeadV1(new('a', 64), 0, 0, 0, PayloadProtectionStateCodec.Timestamp(DateTimeOffset.UtcNow));
        var changes = new[] { PayloadProtectionStateMutation.Put(value) };
        using (var budget = new PayloadOperationBudget(clock, default, TimeSpan.FromMilliseconds(30)))
        {
            (await store.CommitAsync(changes).AsTask().WaitAsync(TimeSpan.FromSeconds(2))).ShouldBe(PayloadStateCommitResult.Unknown);
        }
        mutation.SetResult();
        await client.Received(1).ExecuteStateTransactionAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<StateTransactionRequest>>(), Arg.Any<IReadOnlyDictionary<string, string>?>(), Arg.Any<CancellationToken>());
    }
}
