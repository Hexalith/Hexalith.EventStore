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
            operation.Dispose(); operation.Cleared.IsCompleted.ShouldBeFalse();
            Task resumed = operations.DrainScopeAsync(scope).AsTask();
            resumed.IsCompleted.ShouldBeFalse(); callbacks.ShouldBe(1);
            release.Set(); await callbackDone.Task.WaitAsync(TimeSpan.FromSeconds(2));
            resumed.IsCompleted.ShouldBeFalse(); operation.Cleared.IsCompleted.ShouldBeFalse();
            lateCleared.SetResult();
            await resumed.WaitAsync(TimeSpan.FromSeconds(2));
            operation.Cleared.IsCompletedSuccessfully.ShouldBeTrue(); callbacks.ShouldBe(1);
        }
        finally { release.Set(); lateCleared.TrySetResult(); }
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
