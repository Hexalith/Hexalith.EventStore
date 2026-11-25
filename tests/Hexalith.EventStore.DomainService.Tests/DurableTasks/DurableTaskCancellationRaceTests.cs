using Hexalith.EventStore.DomainService.DurableTasks;
using Hexalith.EventStore.DomainService.Tests.Fixtures;
using Hexalith.EventStore.Testing.Fakes;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests.DurableTasks;

/// <summary>Races cancellation against the worker's conditional writes on the same admission row.</summary>
public sealed class DurableTaskCancellationRaceTests
{
    private const string Component = "g1-task-state";
    private const int FailureBudget = 3;
    private static readonly string RequestHash = new('f', 64);
    private static readonly DurableTaskScope Scope = new("tenant", "original-actor", "sample.provision@1", "race-key");
    private static readonly DateTimeOffset StartedAt = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Duration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    /// <summary>A cancellation committed while the step start is in flight fences the worker before any owner call.</summary>
    [Fact]
    public async Task CancellationCommittedBeforeTheStepStartFencesTheWorker()
    {
        var store = new PausingReadModelStore(new InMemoryReadModelStore());
        var clock = new ManualTimeProvider(StartedAt);
        var admission = new DurableTaskAdmission(store, Component, Authority(), clock);
        var worker = new DurableTaskExecution(admission, clock, Duration, FailureBudget);
        CommittedDurableTaskAdmission task = (await admission.AdmitAsync(Scope, RequestHash)).Task.ShouldNotBeNull();
        DurableTaskLease lease = (await worker.TryAcquireAsync(task.TaskId)).ShouldNotBeNull();
        IDurableTaskEffect owner = CommittingOwner();

        (Task reached, Action release) = store.PauseNextConditionalWrite();
        Task<string?> running = worker.RunStepAsync(lease, 1, owner);
        await reached.WaitAsync(Timeout);
        (await admission.CancelAsync(task.TaskId)).Outcome.ShouldBe(DurableTaskCancellationOutcome.Cancelled);
        release();

        (await Should.ThrowAsync<InvalidOperationException>(() => running)).Message.ShouldContain("lease was lost");
        await owner.DidNotReceive().LookupAsync(Arg.Any<DurableTaskEffectStep>(), Arg.Any<CancellationToken>());
        await owner.DidNotReceive().ExecuteAsync(Arg.Any<DurableTaskEffectStep>(), Arg.Any<CancellationToken>());
        DurableTaskProgress progress = (await admission.GetStatusAsync(task.TaskId)).ShouldNotBeNull().Progress;
        progress.Status.ShouldBe(DurableTaskStatus.Cancelled);
        progress.EffectPending.ShouldBeFalse();
        progress.Checkpoint.ShouldBe(0);
    }

    /// <summary>A step start committed while the cancellation write is in flight turns the cancellation into a conflict.</summary>
    [Fact]
    public async Task StepStartCommittedBeforeTheCancellationRefusesIt()
    {
        var store = new PausingReadModelStore(new InMemoryReadModelStore());
        var clock = new ManualTimeProvider(StartedAt);
        var admission = new DurableTaskAdmission(store, Component, Authority(), clock);
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

        (Task reached, Action release) = store.PauseNextConditionalWrite();
        Task<DurableTaskCancellationResult> cancelling = admission.CancelAsync(task.TaskId);
        await reached.WaitAsync(Timeout);
        Task<string?> running = worker.RunStepAsync(lease, 1, owner);
        await requestStarted.Task.WaitAsync(Timeout);
        release();

        DurableTaskCancellationResult refused = await cancelling;
        refused.Outcome.ShouldBe(DurableTaskCancellationOutcome.Conflict);
        refused.Current.ShouldNotBeNull().Progress.Status.ShouldBe(DurableTaskStatus.Running);
        refused.Current.Progress.EffectPending.ShouldBeTrue();
        reply.SetResult((DurableTaskEffectLookup.Committed, "receipt-1"));
        (await running).ShouldBe("receipt-1");
        await owner.Received(1).ExecuteAsync(Arg.Any<DurableTaskEffectStep>(), Arg.Any<CancellationToken>());
        DurableTaskCancellationResult cancelled = await admission.CancelAsync(task.TaskId);
        cancelled.Outcome.ShouldBe(DurableTaskCancellationOutcome.Cancelled);
        cancelled.Current.ShouldNotBeNull().Progress.Receipts.ShouldBe(["receipt-1"]);
    }

    /// <summary>A cancellation committed while a lease acquisition is in flight leaves nothing to acquire.</summary>
    [Fact]
    public async Task CancellationCommittedBeforeTheLeaseAcquisitionWins()
    {
        var store = new PausingReadModelStore(new InMemoryReadModelStore());
        var clock = new ManualTimeProvider(StartedAt);
        var admission = new DurableTaskAdmission(store, Component, Authority(), clock);
        var worker = new DurableTaskExecution(admission, clock, Duration, FailureBudget);
        CommittedDurableTaskAdmission task = (await admission.AdmitAsync(Scope, RequestHash)).Task.ShouldNotBeNull();

        (Task reached, Action release) = store.PauseNextConditionalWrite();
        Task<DurableTaskLease?> acquiring = worker.TryAcquireAsync(task.TaskId);
        await reached.WaitAsync(Timeout);
        (await admission.CancelAsync(task.TaskId)).Outcome.ShouldBe(DurableTaskCancellationOutcome.Cancelled);
        release();

        (await acquiring).ShouldBeNull();
        DurableTaskProgress progress = (await admission.GetStatusAsync(task.TaskId)).ShouldNotBeNull().Progress;
        progress.Status.ShouldBe(DurableTaskStatus.Cancelled);
        progress.Checkpoint.ShouldBe(0);
    }

    /// <summary>A lease acquired while the cancellation write is in flight is fenced once the cancellation commits.</summary>
    [Fact]
    public async Task LeaseAcquiredBeforeTheCancellationIsFenced()
    {
        var store = new PausingReadModelStore(new InMemoryReadModelStore());
        var clock = new ManualTimeProvider(StartedAt);
        var admission = new DurableTaskAdmission(store, Component, Authority(), clock);
        var worker = new DurableTaskExecution(admission, clock, Duration, FailureBudget);
        CommittedDurableTaskAdmission task = (await admission.AdmitAsync(Scope, RequestHash)).Task.ShouldNotBeNull();
        IDurableTaskEffect owner = CommittingOwner();

        (Task reached, Action release) = store.PauseNextConditionalWrite();
        Task<DurableTaskCancellationResult> cancelling = admission.CancelAsync(task.TaskId);
        await reached.WaitAsync(Timeout);
        DurableTaskLease lease = (await worker.TryAcquireAsync(task.TaskId)).ShouldNotBeNull();
        release();

        (await cancelling).Outcome.ShouldBe(DurableTaskCancellationOutcome.Cancelled);
        await Should.ThrowAsync<InvalidOperationException>(() => worker.RunStepAsync(lease, 1, owner));
        await owner.DidNotReceive().LookupAsync(Arg.Any<DurableTaskEffectStep>(), Arg.Any<CancellationToken>());
        await owner.DidNotReceive().ExecuteAsync(Arg.Any<DurableTaskEffectStep>(), Arg.Any<CancellationToken>());
    }

    /// <summary>A completion committed while the cancellation write is in flight stays the immutable outcome.</summary>
    [Fact]
    public async Task CompletionCommittedBeforeTheCancellationIsImmutable()
    {
        var store = new PausingReadModelStore(new InMemoryReadModelStore());
        var clock = new ManualTimeProvider(StartedAt);
        var admission = new DurableTaskAdmission(store, Component, Authority(), clock);
        var worker = new DurableTaskExecution(admission, clock, Duration, FailureBudget);
        CommittedDurableTaskAdmission task = (await admission.AdmitAsync(Scope, RequestHash)).Task.ShouldNotBeNull();
        DurableTaskLease lease = (await worker.TryAcquireAsync(task.TaskId)).ShouldNotBeNull();
        (await worker.RunStepAsync(lease, 1, CommittingOwner())).ShouldBe("receipt-1");

        (Task reached, Action release) = store.PauseNextConditionalWrite();
        Task<DurableTaskCancellationResult> cancelling = admission.CancelAsync(task.TaskId);
        await reached.WaitAsync(Timeout);
        (await worker.CompleteAsync(lease, DurableTaskStatus.Succeeded)).Status.ShouldBe(DurableTaskStatus.Succeeded);
        release();

        DurableTaskCancellationResult late = await cancelling;
        late.Outcome.ShouldBe(DurableTaskCancellationOutcome.Conflict);
        late.Current.ShouldNotBeNull().Progress.Status.ShouldBe(DurableTaskStatus.Succeeded);
        late.Current.Progress.Receipts.ShouldBe(["receipt-1"]);
    }

    private static IDurableTaskAdmissionAuthorizer Authority()
    {
        IDurableTaskAdmissionAuthorizer authorizer = Substitute.For<IDurableTaskAdmissionAuthorizer>();
        authorizer.AuthorizeAsync(Arg.Any<DurableTaskScope>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new DurableTaskAdmissionAuthority(true, false, null));
        return authorizer;
    }

    private static IDurableTaskEffect Owner(DurableTaskEffectLookup lookup, string? receipt)
    {
        IDurableTaskEffect owner = Substitute.For<IDurableTaskEffect>();
        owner.LookupAsync(Arg.Any<DurableTaskEffectStep>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(DurableTaskEffectLookup, string?)>((lookup, receipt)));
        return owner;
    }

    private static IDurableTaskEffect CommittingOwner()
    {
        IDurableTaskEffect owner = Owner(DurableTaskEffectLookup.Absent, null);
        owner.ExecuteAsync(Arg.Any<DurableTaskEffectStep>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(DurableTaskEffectLookup, string?)>((DurableTaskEffectLookup.Committed, "receipt-1")));
        return owner;
    }
}
