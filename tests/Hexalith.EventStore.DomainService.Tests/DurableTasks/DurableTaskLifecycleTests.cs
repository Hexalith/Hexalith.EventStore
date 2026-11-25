using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.DomainService.DurableTasks;
using Hexalith.EventStore.DomainService.Tests.Fixtures;
using Hexalith.EventStore.Testing.Fakes;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests.DurableTasks;

/// <summary>Exercises status by task identifier, the lifecycle graph and cancellation against the conditional store contract.</summary>
public sealed class DurableTaskLifecycleTests
{
    private const string Component = "g1-task-state";
    private const string UnknownTaskId = "01KAZZZZZZZZZZZZZZZZZZZZZZ";
    private const int FailureBudget = 3;
    private static readonly string RequestHash = new('d', 64);
    private static readonly DurableTaskScope Scope = new("tenant", "original-actor", "sample.provision@1", "lifecycle-key");
    private static readonly DateTimeOffset StartedAt = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Duration = TimeSpan.FromSeconds(30);

    /// <summary>Status by task identifier, cancellation and terminal completion converge across replicas.</summary>
    [Fact]
    public Task LifecycleConvergesAcrossReplicas()
    {
        var store = new InMemoryReadModelStore();
        return VerifyLifecycleAcrossReplicasAsync(store, store, Component);
    }

    /// <summary>Cancellation is refused while an owner step may be in flight, and the worker then checkpoints normally.</summary>
    [Fact]
    public async Task CancellationWaitsForAnInFlightOwnerStep()
    {
        var clock = new ManualTimeProvider(StartedAt);
        var admission = new DurableTaskAdmission(new InMemoryReadModelStore(), Component, Authority(), clock);
        var worker = new DurableTaskExecution(admission, clock, Duration, FailureBudget);
        CommittedDurableTaskAdmission task = (await admission.AdmitAsync(Scope, RequestHash)).Task.ShouldNotBeNull();
        DurableTaskLease lease = (await worker.TryAcquireAsync(task.TaskId)).ShouldNotBeNull();
        IDurableTaskEffect owner = Owner(DurableTaskEffectLookup.Absent, null);
        var requestStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reply = new TaskCompletionSource<(DurableTaskEffectLookup, string?)>(TaskCreationOptions.RunContinuationsAsynchronously);
        owner.ExecuteAsync(Arg.Any<DurableTaskEffectStep>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            requestStarted.SetResult();
            return reply.Task;
        });

        Task<string?> running = worker.RunStepAsync(lease, 1, owner);
        await requestStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        DurableTaskCancellationResult refused = await admission.CancelAsync(task.TaskId);
        refused.Outcome.ShouldBe(DurableTaskCancellationOutcome.Conflict);
        refused.Current.ShouldNotBeNull().Progress.EffectPending.ShouldBeTrue();
        refused.Current.Progress.Status.ShouldBe(DurableTaskStatus.Running);

        reply.SetResult((DurableTaskEffectLookup.Committed, "receipt-1"));
        (await running).ShouldBe("receipt-1");
        DurableTaskCancellationResult cancelled = await admission.CancelAsync(task.TaskId);
        cancelled.Outcome.ShouldBe(DurableTaskCancellationOutcome.Cancelled);
        cancelled.Current.ShouldNotBeNull().Progress.Receipts.ShouldBe(["receipt-1"]);
    }

    /// <summary>An owner outage records WaitingForDependency, and a later lease resumes through authoritative lookup.</summary>
    [Fact]
    public async Task DependencyOutageWaitsAndResumesFromTheCommittedCheckpoint()
    {
        var store = new InMemoryReadModelStore();
        var clock = new ManualTimeProvider(StartedAt);
        var admission = new DurableTaskAdmission(store, Component, Authority(), clock);
        var first = new DurableTaskExecution(admission, clock, Duration, FailureBudget);
        var second = new DurableTaskExecution(new DurableTaskAdmission(store, Component, Authority(), clock), clock, Duration, FailureBudget);
        CommittedDurableTaskAdmission task = (await admission.AdmitAsync(Scope, RequestHash)).Task.ShouldNotBeNull();
        DurableTaskLease stale = (await first.TryAcquireAsync(task)).ShouldNotBeNull();
        IDurableTaskEffect unavailable = Substitute.For<IDurableTaskEffect>();
        unavailable.LookupAsync(Arg.Any<DurableTaskEffectStep>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<(DurableTaskEffectLookup, string?)>(new IOException("Owner is unavailable.")));

        await Should.ThrowAsync<IOException>(() => first.RunStepAsync(stale, 1, unavailable));
        DurableTaskProgress waiting = (await admission.GetStatusAsync(task.TaskId)).ShouldNotBeNull().Progress;
        waiting.Status.ShouldBe(DurableTaskStatus.WaitingForDependency);
        waiting.EffectPending.ShouldBeTrue();
        waiting.Checkpoint.ShouldBe(0);
        (await admission.CancelAsync(task.TaskId)).Outcome.ShouldBe(DurableTaskCancellationOutcome.Conflict);
        await Should.ThrowAsync<InvalidOperationException>(() => first.CompleteAsync(stale, DurableTaskStatus.Failed));

        clock.Advance(Duration);
        DurableTaskLease lease = (await second.TryAcquireAsync(task.TaskId)).ShouldNotBeNull();
        lease.Fence.ShouldBe(stale.Fence + 1);
        (await admission.GetStatusAsync(task.TaskId)).ShouldNotBeNull().Progress.Status.ShouldBe(DurableTaskStatus.Running);
        IDurableTaskEffect owner = Owner(DurableTaskEffectLookup.Committed, "receipt-1");
        (await second.RunStepAsync(lease, 1, owner)).ShouldBe("receipt-1");
        await owner.DidNotReceive().ExecuteAsync(Arg.Any<DurableTaskEffectStep>(), Arg.Any<CancellationToken>());
        await unavailable.DidNotReceive().ExecuteAsync(Arg.Any<DurableTaskEffectStep>(), Arg.Any<CancellationToken>());
        DurableTaskProgress resumed = (await admission.GetStatusAsync(task.TaskId)).ShouldNotBeNull().Progress;
        resumed.Status.ShouldBe(DurableTaskStatus.Running);
        resumed.Receipts.ShouldBe(["receipt-1"]);
        resumed.EffectPending.ShouldBeFalse();
    }

    /// <summary>NeedsAttention can be cancelled before the irreversible checkpoint and keeps the unresolved step recorded.</summary>
    [Fact]
    public async Task NeedsAttentionCanBeCancelledOnlyBeforeTheIrreversibleCheckpoint()
    {
        var clock = new ManualTimeProvider(StartedAt);
        var admission = new DurableTaskAdmission(new InMemoryReadModelStore(), Component, Authority(), clock);
        var worker = new DurableTaskExecution(admission, clock, Duration, FailureBudget);
        IDurableTaskEffect unresolved = Owner(DurableTaskEffectLookup.Unresolved, null);

        CommittedDurableTaskAdmission reversible = (await admission.AdmitAsync(Scope with { IdempotencyKey = "reversible" }, RequestHash)).Task.ShouldNotBeNull();
        DurableTaskLease first = (await worker.TryAcquireAsync(reversible)).ShouldNotBeNull();
        (await worker.RunStepAsync(first, 1, unresolved)).ShouldBeNull();
        DurableTaskCancellationResult cancelled = await admission.CancelAsync(reversible.TaskId);
        cancelled.Outcome.ShouldBe(DurableTaskCancellationOutcome.Cancelled);
        cancelled.Current.ShouldNotBeNull().Progress.EffectPending.ShouldBeTrue();
        cancelled.Current.Progress.IsTerminal.ShouldBeTrue();
        clock.Advance(Duration);
        (await worker.TryAcquireAsync(reversible)).ShouldBeNull();

        CommittedDurableTaskAdmission irreversible = (await admission.AdmitAsync(Scope with { IdempotencyKey = "irreversible" }, RequestHash)).Task.ShouldNotBeNull();
        DurableTaskLease second = (await worker.TryAcquireAsync(irreversible)).ShouldNotBeNull();
        (await worker.RunIrreversibleStepAsync(second, 1, unresolved)).ShouldBeNull();
        DurableTaskCancellationResult refused = await admission.CancelAsync(irreversible.TaskId);
        refused.Outcome.ShouldBe(DurableTaskCancellationOutcome.Conflict);
        refused.Current.ShouldNotBeNull().Progress.Status.ShouldBe(DurableTaskStatus.NeedsAttention);
        refused.Current.Progress.IrreversibleStep.ShouldBe(1);
        await unresolved.DidNotReceive().ExecuteAsync(Arg.Any<DurableTaskEffectStep>(), Arg.Any<CancellationToken>());
    }

    /// <summary>The outcome an owner returns from the effect call is recorded, and contradictory evidence fences the task.</summary>
    /// <param name="outcome">The owner outcome.</param>
    /// <param name="receipt">The owner receipt.</param>
    /// <param name="expected">The committed status.</param>
    /// <param name="inconsistent">Whether the outcome contradicts the owner contract.</param>
    [Theory]
    [InlineData(DurableTaskEffectLookup.Committed, "receipt-1", DurableTaskStatus.Running, false)]
    [InlineData(DurableTaskEffectLookup.Rejected, null, DurableTaskStatus.Rejected, false)]
    [InlineData(DurableTaskEffectLookup.Unresolved, null, DurableTaskStatus.NeedsAttention, false)]
    [InlineData(DurableTaskEffectLookup.Absent, null, DurableTaskStatus.NeedsAttention, true)]
    [InlineData(DurableTaskEffectLookup.Committed, null, DurableTaskStatus.NeedsAttention, true)]
    [InlineData(DurableTaskEffectLookup.Committed, " ", DurableTaskStatus.NeedsAttention, true)]
    [InlineData(DurableTaskEffectLookup.Rejected, "receipt-1", DurableTaskStatus.NeedsAttention, true)]
    [InlineData(DurableTaskEffectLookup.Unresolved, "receipt-1", DurableTaskStatus.NeedsAttention, true)]
    public async Task OwnerEffectOutcomeIsRecorded(DurableTaskEffectLookup outcome, string? receipt, DurableTaskStatus expected, bool inconsistent)
    {
        var clock = new ManualTimeProvider(StartedAt);
        var admission = new DurableTaskAdmission(new InMemoryReadModelStore(), Component, Authority(), clock);
        var worker = new DurableTaskExecution(admission, clock, Duration, FailureBudget);
        CommittedDurableTaskAdmission task = (await admission.AdmitAsync(Scope, RequestHash)).Task.ShouldNotBeNull();
        DurableTaskLease lease = (await worker.TryAcquireAsync(task.TaskId)).ShouldNotBeNull();
        IDurableTaskEffect owner = Owner(DurableTaskEffectLookup.Absent, null);
        owner.ExecuteAsync(Arg.Any<DurableTaskEffectStep>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(DurableTaskEffectLookup, string?)>((outcome, receipt)));

        if (inconsistent)
        {
            (await Should.ThrowAsync<InvalidOperationException>(() => worker.RunStepAsync(lease, 1, owner))).Message.ShouldContain("effect returned inconsistent");
        }
        else
        {
            (await worker.RunStepAsync(lease, 1, owner)).ShouldBe(expected == DurableTaskStatus.Running ? receipt : null);
        }

        DurableTaskProgress progress = (await admission.GetStatusAsync(task.TaskId)).ShouldNotBeNull().Progress;
        progress.Status.ShouldBe(expected);
        progress.Checkpoint.ShouldBe(expected == DurableTaskStatus.Running ? 1 : 0);
        progress.EffectPending.ShouldBe(expected == DurableTaskStatus.NeedsAttention);
        clock.Advance(Duration);
        (await worker.TryAcquireAsync(task.TaskId) is not null).ShouldBe(expected == DurableTaskStatus.Running);
        await owner.Received(1).ExecuteAsync(Arg.Any<DurableTaskEffectStep>(), Arg.Any<CancellationToken>());
    }

    /// <summary>An owner's authoritative refusal found by lookup ends the task as Rejected without invoking the owner.</summary>
    [Fact]
    public async Task OwnerRefusalFoundByLookupRejectsTheTask()
    {
        var clock = new ManualTimeProvider(StartedAt);
        var admission = new DurableTaskAdmission(new InMemoryReadModelStore(), Component, Authority(), clock);
        var worker = new DurableTaskExecution(admission, clock, Duration, FailureBudget);
        CommittedDurableTaskAdmission task = (await admission.AdmitAsync(Scope, RequestHash)).Task.ShouldNotBeNull();
        DurableTaskLease lease = (await worker.TryAcquireAsync(task.TaskId)).ShouldNotBeNull();
        (await worker.RunStepAsync(lease, 1, Owner(DurableTaskEffectLookup.Committed, "receipt-1"))).ShouldBe("receipt-1");
        IDurableTaskEffect refusing = Owner(DurableTaskEffectLookup.Rejected, null);

        (await worker.RunIrreversibleStepAsync(lease, 2, refusing)).ShouldBeNull();
        DurableTaskProgress progress = (await admission.GetStatusAsync(task.TaskId)).ShouldNotBeNull().Progress;
        progress.Status.ShouldBe(DurableTaskStatus.Rejected);
        progress.Receipts.ShouldBe(["receipt-1"]);
        progress.EffectPending.ShouldBeFalse();
        progress.IrreversibleStep.ShouldBe(2);
        await refusing.DidNotReceive().ExecuteAsync(Arg.Any<DurableTaskEffectStep>(), Arg.Any<CancellationToken>());
        await Should.ThrowAsync<InvalidOperationException>(() => worker.CompleteAsync(lease, DurableTaskStatus.Succeeded));
        (await admission.CancelAsync(task.TaskId)).Outcome.ShouldBe(DurableTaskCancellationOutcome.Conflict);
    }

    /// <summary>A refusal that contradicts a receipt committed under the same lease fences the task in NeedsAttention.</summary>
    [Fact]
    public async Task RefusalOfACommittedStepRequiresAttention()
    {
        var clock = new ManualTimeProvider(StartedAt);
        var admission = new DurableTaskAdmission(new InMemoryReadModelStore(), Component, Authority(), clock);
        var worker = new DurableTaskExecution(admission, clock, Duration, FailureBudget);
        CommittedDurableTaskAdmission task = (await admission.AdmitAsync(Scope, RequestHash)).Task.ShouldNotBeNull();
        DurableTaskLease lease = (await worker.TryAcquireAsync(task.TaskId)).ShouldNotBeNull();
        IDurableTaskEffect owner = Substitute.For<IDurableTaskEffect>();
        var lateLookup = new TaskCompletionSource<(DurableTaskEffectLookup, string?)>(TaskCreationOptions.RunContinuationsAsynchronously);
        var lookupStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        owner.LookupAsync(Arg.Any<DurableTaskEffectStep>(), Arg.Any<CancellationToken>()).Returns(
            _ => Task.FromResult<(DurableTaskEffectLookup, string?)>((DurableTaskEffectLookup.Absent, null)),
            _ =>
            {
                lookupStarted.SetResult();
                return lateLookup.Task;
            });
        var reply = new TaskCompletionSource<(DurableTaskEffectLookup, string?)>(TaskCreationOptions.RunContinuationsAsynchronously);
        owner.ExecuteAsync(Arg.Any<DurableTaskEffectStep>(), Arg.Any<CancellationToken>()).Returns(reply.Task);

        Task<string?> committing = worker.RunStepAsync(lease, 1, owner);
        Task<string?> refusing = worker.RunStepAsync(lease, 1, owner);
        await lookupStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        reply.SetResult((DurableTaskEffectLookup.Committed, "receipt-1"));
        (await committing).ShouldBe("receipt-1");
        lateLookup.SetResult((DurableTaskEffectLookup.Rejected, null));

        (await Should.ThrowAsync<InvalidOperationException>(() => refusing)).Message.ShouldContain("committed receipt");
        DurableTaskProgress progress = (await admission.GetStatusAsync(task.TaskId)).ShouldNotBeNull().Progress;
        progress.Status.ShouldBe(DurableTaskStatus.NeedsAttention);
        progress.Receipts.ShouldBe(["receipt-1"]);
    }

    /// <summary>Owner failures count against the budget until a receipt commits, and an exhausted budget fences execution.</summary>
    [Fact]
    public async Task DependencyFailureBudgetResetsOnCommitAndRequiresAttention()
    {
        var clock = new ManualTimeProvider(StartedAt);
        var admission = new DurableTaskAdmission(new InMemoryReadModelStore(), Component, Authority(), clock);
        var worker = new DurableTaskExecution(admission, clock, Duration, FailureBudget);
        CommittedDurableTaskAdmission task = (await admission.AdmitAsync(Scope, RequestHash)).Task.ShouldNotBeNull();
        IDurableTaskEffect unavailable = Substitute.For<IDurableTaskEffect>();
        unavailable.LookupAsync(Arg.Any<DurableTaskEffectStep>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<(DurableTaskEffectLookup, string?)>(new IOException("Owner is unavailable.")));

        DurableTaskLease lease = (await worker.TryAcquireAsync(task.TaskId)).ShouldNotBeNull();
        for (int failure = 1; failure < FailureBudget; failure++)
        {
            await Should.ThrowAsync<IOException>(() => worker.RunStepAsync(lease, 1, unavailable));
            (await admission.GetStatusAsync(task.TaskId)).ShouldNotBeNull().Progress.Attempts.ShouldBe(failure);
        }

        (await worker.RunStepAsync(lease, 1, Owner(DurableTaskEffectLookup.Committed, "receipt-1"))).ShouldBe("receipt-1");
        (await admission.GetStatusAsync(task.TaskId)).ShouldNotBeNull().Progress.Attempts.ShouldBe(0);
        for (int failure = 1; failure <= FailureBudget; failure++)
        {
            clock.Advance(Duration);
            lease = (await worker.TryAcquireAsync(task.TaskId)).ShouldNotBeNull();
            await Should.ThrowAsync<IOException>(() => worker.RunStepAsync(lease, 2, unavailable));
        }

        DurableTaskProgress attention = (await admission.GetStatusAsync(task.TaskId)).ShouldNotBeNull().Progress;
        attention.Status.ShouldBe(DurableTaskStatus.NeedsAttention);
        attention.Attempts.ShouldBe(FailureBudget);
        attention.EffectPending.ShouldBeTrue();
        attention.Receipts.ShouldBe(["receipt-1"]);
        clock.Advance(Duration);
        (await worker.TryAcquireAsync(task.TaskId)).ShouldBeNull();
        await unavailable.DidNotReceive().ExecuteAsync(Arg.Any<DurableTaskEffectStep>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Completion requires a terminal outcome, the current lease and no pending owner step, and stays immutable.</summary>
    [Fact]
    public async Task CompletionIsFencedAndImmutable()
    {
        var clock = new ManualTimeProvider(StartedAt);
        var admission = new DurableTaskAdmission(new InMemoryReadModelStore(), Component, Authority(), clock);
        var worker = new DurableTaskExecution(admission, clock, Duration, FailureBudget);
        CommittedDurableTaskAdmission task = (await admission.AdmitAsync(Scope, RequestHash)).Task.ShouldNotBeNull();
        DurableTaskLease stale = (await worker.TryAcquireAsync(task)).ShouldNotBeNull();
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => worker.CompleteAsync(stale, DurableTaskStatus.Cancelled));
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => worker.CompleteAsync(stale, DurableTaskStatus.Running));
        clock.Advance(Duration);
        await Should.ThrowAsync<InvalidOperationException>(() => worker.CompleteAsync(stale, DurableTaskStatus.Succeeded));

        DurableTaskLease lease = (await worker.TryAcquireAsync(task)).ShouldNotBeNull();
        (await worker.RunStepAsync(lease, 1, Owner(DurableTaskEffectLookup.Committed, "receipt-1"))).ShouldBe("receipt-1");
        DurableTaskProgress rejected = await worker.CompleteAsync(lease, DurableTaskStatus.Rejected);
        rejected.Status.ShouldBe(DurableTaskStatus.Rejected);
        rejected.Receipts.ShouldBe(["receipt-1"]);
        await Should.ThrowAsync<InvalidOperationException>(() => worker.CompleteAsync(lease, DurableTaskStatus.Succeeded));
        await Should.ThrowAsync<InvalidOperationException>(() => worker.RunStepAsync(lease, 2, Owner(DurableTaskEffectLookup.Absent, null)));
        clock.Advance(Duration);
        (await worker.TryAcquireAsync(task)).ShouldBeNull();
        (await admission.CancelAsync(task.TaskId)).Current.ShouldNotBeNull().Progress.Status.ShouldBe(DurableTaskStatus.Rejected);
        DurableTaskProgress committed = (await admission.GetProgressAsync(Scope, RequestHash)).ShouldNotBeNull();
        committed.Status.ShouldBe(DurableTaskStatus.Rejected);
        committed.Receipts.ShouldBe(["receipt-1"]);
    }

    /// <summary>An admitted task that was never leased cancels at checkpoint zero and cannot be leased afterwards.</summary>
    [Fact]
    public async Task UnleasedTaskCancelsAtCheckpointZero()
    {
        var clock = new ManualTimeProvider(StartedAt);
        var admission = new DurableTaskAdmission(new InMemoryReadModelStore(), Component, Authority(), clock);
        var worker = new DurableTaskExecution(admission, clock, Duration, FailureBudget);
        CommittedDurableTaskAdmission task = (await admission.AdmitAsync(Scope, RequestHash)).Task.ShouldNotBeNull();

        DurableTaskCancellationResult cancelled = await admission.CancelAsync(task.TaskId);
        cancelled.Outcome.ShouldBe(DurableTaskCancellationOutcome.Cancelled);
        DurableTaskSnapshot snapshot = cancelled.Current.ShouldNotBeNull();
        snapshot.Task.ShouldBe(task);
        snapshot.Progress.Status.ShouldBe(DurableTaskStatus.Cancelled);
        snapshot.Progress.Checkpoint.ShouldBe(0);
        snapshot.Progress.EffectPending.ShouldBeFalse();
        (await admission.GetStatusAsync(task.TaskId)).ShouldBeEquivalentTo(snapshot);
        (await worker.TryAcquireAsync(task.TaskId)).ShouldBeNull();
    }

    /// <summary>A persisted row the lifecycle graph cannot produce fails closed for callers and workers.</summary>
    /// <param name="rule">The lifecycle rule the seeded row violates.</param>
    [Theory]
    [InlineData("ExecutionWithoutTask")]
    [InlineData("UnknownStatus")]
    [InlineData("NegativeFence")]
    [InlineData("BlankReceipt")]
    [InlineData("IrreversibleStepZero")]
    [InlineData("IrreversibleStepBeyondNext")]
    [InlineData("IrreversibleNextStepNotPending")]
    [InlineData("PendingWithIrreversibleStep")]
    [InlineData("PendingWithExpiry")]
    [InlineData("PendingWithFence")]
    [InlineData("PendingWithOwner")]
    [InlineData("PendingWithReceipt")]
    [InlineData("PendingWithPendingStep")]
    [InlineData("RunningWithoutOwner")]
    [InlineData("RunningWithoutExpiry")]
    [InlineData("RunningWithoutFence")]
    [InlineData("WaitingWithoutOwner")]
    [InlineData("WaitingWithoutPendingStep")]
    [InlineData("NeedsAttentionWithOwner")]
    [InlineData("SucceededWithOwner")]
    [InlineData("RejectedWithExpiry")]
    [InlineData("FailedWithPendingStep")]
    [InlineData("SucceededWithoutFence")]
    [InlineData("CancelledWithIrreversibleStep")]
    [InlineData("CancelledUnleasedWithReceipt")]
    [InlineData("NegativeAttempts")]
    [InlineData("PendingWithAttempts")]
    [InlineData("RunningWithAttemptsWithoutPendingStep")]
    [InlineData("NeedsAttentionWithAttemptsWithoutPendingStep")]
    [InlineData("RejectedWithAttempts")]
    [InlineData("CancelledWithAttemptsWithoutPendingStep")]
    [InlineData("SucceededWithIrreversibleNextStep")]
    [InlineData("FailedWithIrreversibleNextStep")]
    public async Task OffGraphPersistedStateFailsClosed(string rule)
    {
        var store = new InMemoryReadModelStore();
        var clock = new ManualTimeProvider(StartedAt);
        var admission = new DurableTaskAdmission(store, Component, Authority(), clock);
        var worker = new DurableTaskExecution(admission, clock, Duration, FailureBudget);
        CommittedDurableTaskAdmission task = (await admission.AdmitAsync(Scope, RequestHash)).Task.ShouldNotBeNull();
        string key = store.SnapshotRawEntries(Component).Keys.Single(key => key.StartsWith("durable-task-admission-v1-", StringComparison.Ordinal));
        DurableTaskAdmissionState state = (await store.GetAsync<DurableTaskAdmissionState>(Component, key)).Value.ShouldNotBeNull();
        store.SeedRaw(Component, key, rule == "ExecutionWithoutTask"
            ? state with { Task = null, Execution = DurableTaskExecutionState.Initial }
            : state with { Execution = OffGraph(rule) });

        await Should.ThrowAsync<InvalidOperationException>(() => admission.GetStatusAsync(task.TaskId));
        await Should.ThrowAsync<InvalidOperationException>(() => worker.TryAcquireAsync(task.TaskId));
    }

    /// <summary>Runs the lifecycle checks through two independently connected stores.</summary>
    internal static async Task VerifyLifecycleAcrossReplicasAsync(IReadModelStore primary, IReadModelStore replica, string component)
    {
        var clock = new ManualTimeProvider(StartedAt);
        IDurableTaskAdmissionAuthorizer authority = Authority();
        var first = new DurableTaskAdmission(primary, component, authority, clock);
        var second = new DurableTaskAdmission(replica, component, authority, clock);
        var worker = new DurableTaskExecution(first, clock, Duration, FailureBudget);
        var replacement = new DurableTaskExecution(second, clock, Duration, FailureBudget);
        IDurableTaskEffect owner = Owner(DurableTaskEffectLookup.Absent, null);
        owner.ExecuteAsync(Arg.Any<DurableTaskEffectStep>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult<(DurableTaskEffectLookup, string?)>((DurableTaskEffectLookup.Committed, call.Arg<DurableTaskEffectStep>().Key)));

        // Cancellation before the irreversible checkpoint fences the running worker and retains its receipt.
        CommittedDurableTaskAdmission cancelled = (await first.AdmitAsync(Scope with { IdempotencyKey = "lifecycle-cancel" }, RequestHash)).Task.ShouldNotBeNull();
        DurableTaskSnapshot pending = (await second.GetStatusAsync(cancelled.TaskId)).ShouldNotBeNull();
        pending.Task.ShouldBe(cancelled);
        pending.Progress.Status.ShouldBe(DurableTaskStatus.Pending);
        DurableTaskLease lease = (await worker.TryAcquireAsync(cancelled.TaskId)).ShouldNotBeNull();
        (await second.GetStatusAsync(cancelled.TaskId)).ShouldNotBeNull().Progress.Status.ShouldBe(DurableTaskStatus.Running);
        string firstReceipt = $"{cancelled.TaskId}/step/1";
        (await worker.RunStepAsync(lease, 1, owner)).ShouldBe(firstReceipt);
        DurableTaskCancellationResult cancel = await second.CancelAsync(cancelled.TaskId);
        cancel.Outcome.ShouldBe(DurableTaskCancellationOutcome.Cancelled);
        cancel.Current.ShouldNotBeNull().Progress.Receipts.ShouldBe([firstReceipt]);
        await Should.ThrowAsync<InvalidOperationException>(() => worker.RunStepAsync(lease, 2, owner));
        clock.Advance(Duration);
        (await replacement.TryAcquireAsync(cancelled.TaskId)).ShouldBeNull();
        (await first.CancelAsync(cancelled.TaskId)).Outcome.ShouldBe(DurableTaskCancellationOutcome.Cancelled);
        DurableTaskProgress final = (await first.GetStatusAsync(cancelled.TaskId)).ShouldNotBeNull().Progress;
        final.Status.ShouldBe(DurableTaskStatus.Cancelled);
        final.Receipts.ShouldBe([firstReceipt]);
        final.EffectPending.ShouldBeFalse();

        // After the irreversible checkpoint, cancellation returns a conflict with the committed status.
        CommittedDurableTaskAdmission committed = (await second.AdmitAsync(Scope with { IdempotencyKey = "lifecycle-irreversible" }, RequestHash)).Task.ShouldNotBeNull();
        DurableTaskLease owned = (await replacement.TryAcquireAsync(committed.TaskId)).ShouldNotBeNull();
        string irreversibleReceipt = $"{committed.TaskId}/step/1";
        (await replacement.RunIrreversibleStepAsync(owned, 1, owner)).ShouldBe(irreversibleReceipt);
        DurableTaskCancellationResult refused = await first.CancelAsync(committed.TaskId);
        refused.Outcome.ShouldBe(DurableTaskCancellationOutcome.Conflict);
        refused.Current.ShouldNotBeNull().Progress.Status.ShouldBe(DurableTaskStatus.Running);
        refused.Current.Progress.IrreversibleStep.ShouldBe(1);
        (await replacement.CompleteAsync(owned, DurableTaskStatus.Succeeded)).Status.ShouldBe(DurableTaskStatus.Succeeded);
        await Should.ThrowAsync<InvalidOperationException>(() => replacement.CompleteAsync(owned, DurableTaskStatus.Failed));
        DurableTaskCancellationResult late = await second.CancelAsync(committed.TaskId);
        late.Outcome.ShouldBe(DurableTaskCancellationOutcome.Conflict);
        late.Current.ShouldNotBeNull().Progress.Status.ShouldBe(DurableTaskStatus.Succeeded);
        late.Current.Progress.Receipts.ShouldBe([irreversibleReceipt]);
        clock.Advance(Duration);
        (await worker.TryAcquireAsync(committed.TaskId)).ShouldBeNull();
        await owner.Received(2).ExecuteAsync(Arg.Any<DurableTaskEffectStep>(), Arg.Any<CancellationToken>());

        // Unknown identifiers and callers without current authority observe no task.
        (await second.GetStatusAsync(UnknownTaskId)).ShouldBeNull();
        (await second.CancelAsync(UnknownTaskId)).ShouldBe(new DurableTaskCancellationResult(DurableTaskCancellationOutcome.NotFound));
        SetDecision(authority, new(false, false, null));
        (await second.GetStatusAsync(committed.TaskId)).ShouldBeNull();
        (await first.CancelAsync(cancelled.TaskId)).ShouldBe(new DurableTaskCancellationResult(DurableTaskCancellationOutcome.NotFound));
    }

    private static DurableTaskExecutionState OffGraph(string rule)
    {
        DurableTaskExecutionState pending = DurableTaskExecutionState.Initial;
        var running = new DurableTaskExecutionState(1, "worker", StartedAt + Duration, new(["receipt-1"], false, DurableTaskStatus.Running));
        DurableTaskExecutionState released = running with { Owner = null, ExpiresAt = null };
        DurableTaskExecutionState With(DurableTaskExecutionState state, Func<DurableTaskProgress, DurableTaskProgress> change)
            => state with { Progress = change(state.Progress) };
        return rule switch
        {
            "UnknownStatus" => With(running, p => p with { Status = DurableTaskStatus.Unknown }),
            "NegativeFence" => running with { Fence = -1 },
            "BlankReceipt" => With(running, p => p with { Receipts = [" "] }),
            "IrreversibleStepZero" => With(running, p => p with { IrreversibleStep = 0 }),
            "IrreversibleStepBeyondNext" => With(running, p => p with { IrreversibleStep = 3, EffectPending = true }),
            "IrreversibleNextStepNotPending" => With(running, p => p with { IrreversibleStep = 2 }),
            "PendingWithIrreversibleStep" => With(pending, p => p with { IrreversibleStep = 1 }),
            "PendingWithExpiry" => pending with { ExpiresAt = StartedAt },
            "PendingWithFence" => pending with { Fence = 1 },
            "PendingWithOwner" => pending with { Owner = "worker" },
            "PendingWithReceipt" => With(pending, p => p with { Receipts = ["receipt-1"] }),
            "PendingWithPendingStep" => With(pending, p => p with { EffectPending = true }),
            "RunningWithoutOwner" => running with { Owner = null },
            "RunningWithoutExpiry" => running with { ExpiresAt = null },
            "RunningWithoutFence" => running with { Fence = 0 },
            "WaitingWithoutOwner" => With(running with { Owner = null }, p => p with { Status = DurableTaskStatus.WaitingForDependency, EffectPending = true }),
            "WaitingWithoutPendingStep" => With(running, p => p with { Status = DurableTaskStatus.WaitingForDependency }),
            "NeedsAttentionWithOwner" => With(released with { Owner = "worker" }, p => p with { Status = DurableTaskStatus.NeedsAttention, EffectPending = true }),
            "SucceededWithOwner" => With(running, p => p with { Status = DurableTaskStatus.Succeeded }),
            "RejectedWithExpiry" => With(released with { ExpiresAt = StartedAt }, p => p with { Status = DurableTaskStatus.Rejected }),
            "FailedWithPendingStep" => With(released, p => p with { Status = DurableTaskStatus.Failed, EffectPending = true }),
            "SucceededWithoutFence" => With(released with { Fence = 0 }, p => p with { Status = DurableTaskStatus.Succeeded }),
            "CancelledWithIrreversibleStep" => With(released, p => p with { Status = DurableTaskStatus.Cancelled, IrreversibleStep = 1 }),
            "CancelledUnleasedWithReceipt" => With(released with { Fence = 0 }, p => p with { Status = DurableTaskStatus.Cancelled }),
            "NegativeAttempts" => With(running, p => p with { EffectPending = true, Attempts = -1 }),
            "PendingWithAttempts" => With(pending, p => p with { Attempts = 1 }),
            "RunningWithAttemptsWithoutPendingStep" => With(running, p => p with { Attempts = 1 }),
            "NeedsAttentionWithAttemptsWithoutPendingStep" => With(released, p => p with { Status = DurableTaskStatus.NeedsAttention, Attempts = 1 }),
            "RejectedWithAttempts" => With(released, p => p with { Status = DurableTaskStatus.Rejected, Attempts = 1 }),
            "CancelledWithAttemptsWithoutPendingStep" => With(released, p => p with { Status = DurableTaskStatus.Cancelled, Attempts = 1 }),
            "SucceededWithIrreversibleNextStep" => With(released, p => p with { Status = DurableTaskStatus.Succeeded, IrreversibleStep = 2 }),
            "FailedWithIrreversibleNextStep" => With(released, p => p with { Status = DurableTaskStatus.Failed, IrreversibleStep = 2 }),
            _ => throw new ArgumentOutOfRangeException(nameof(rule), rule, "Unknown off-graph rule."),
        };
    }

    private static IDurableTaskAdmissionAuthorizer Authority()
    {
        IDurableTaskAdmissionAuthorizer authorizer = Substitute.For<IDurableTaskAdmissionAuthorizer>();
        SetDecision(authorizer, new(true, false, null));
        return authorizer;
    }

    private static void SetDecision(IDurableTaskAdmissionAuthorizer authorizer, DurableTaskAdmissionAuthority decision)
        => authorizer.AuthorizeAsync(Arg.Any<DurableTaskScope>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(decision);

    private static IDurableTaskEffect Owner(DurableTaskEffectLookup lookup, string? receipt)
    {
        IDurableTaskEffect owner = Substitute.For<IDurableTaskEffect>();
        owner.LookupAsync(Arg.Any<DurableTaskEffectStep>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(DurableTaskEffectLookup, string?)>((lookup, receipt)));
        return owner;
    }
}
