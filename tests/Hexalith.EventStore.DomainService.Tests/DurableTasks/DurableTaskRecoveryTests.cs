using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.DomainService.DurableTasks;
using Hexalith.EventStore.Testing.Fakes;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests.DurableTasks;

/// <summary>Checks the recovery boundary when lookups fail or finish after ownership changes.</summary>
public sealed class DurableTaskRecoveryTests
{
    private const string Component = "task-state";
    private const string StepName = "write@1";
    private static readonly DurableTaskScope Scope = new("tenant", "actor", "operate@1", "original-key");
    private static readonly string Intent = new('a', 64);
    private static readonly DateTimeOffset Start = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(30);

    /// <summary>A lookup or submission response received after takeover cannot advance the old claim.</summary>
    /// <param name="holdSubmission">Whether to pause the submission response instead of the absence lookup.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LateOwnerResponseCannotAdvanceAfterTakeover(bool holdSubmission)
    {
        var store = new InMemoryReadModelStore();
        var clock = Substitute.For<TimeProvider>();
        clock.GetUtcNow().Returns(Start);
        CommittedDurableTaskAdmission task = await AdmitAsync(store, clock);
        var first = new DurableTaskRecovery(store, Component, clock, LeaseDuration);
        var takeoverClock = Substitute.For<TimeProvider>();
        takeoverClock.GetUtcNow().Returns(Start + LeaseDuration);
        var second = new DurableTaskRecovery(store, Component, takeoverClock, LeaseDuration);
        DurableTaskLease firstLease = (await first.TryClaimAsync(task, "worker-a")).ShouldNotBeNull();
        var pending = new TaskCompletionSource<DurableTaskStep>(TaskCreationOptions.RunContinuationsAsynchronously);
        var lookup = new TaskCompletionSource<DurableTaskOwnerReceipt?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var submission = new TaskCompletionSource<DurableTaskOwnerReceipt>(TaskCreationOptions.RunContinuationsAsynchronously);
        IDurableTaskStepOwner owner = Substitute.For<IDurableTaskStepOwner>();
        owner.LookupAsync(Arg.Any<DurableTaskStep>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            if (!holdSubmission)
            {
                pending.SetResult(call.Arg<DurableTaskStep>());
            }

            return lookup.Task;
        });
        owner.CommitAsync(Arg.Any<DurableTaskStep>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            pending.SetResult(call.Arg<DurableTaskStep>());
            return submission.Task;
        });
        if (holdSubmission)
        {
            lookup.SetResult(null);
        }

        Task<bool> firstRun = first.RunStepAsync(task, firstLease, StepName, owner);
        DurableTaskStep step = await pending.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var receipt = new DurableTaskOwnerReceipt(step, new string('b', 64));
        DurableTaskLease secondLease;
        try
        {
            secondLease = (await second.TryClaimAsync(task, "worker-b")).ShouldNotBeNull();
        }
        finally
        {
            lookup.TrySetResult(null);
            submission.SetResult(receipt);
            (await firstRun).ShouldBeFalse();
        }

        string key = DurableTaskAdmission.ValidateAndGetKey(task.Scope, task.RequestSha256);
        DurableTaskExecutionState retained = (await store.GetAsync<DurableTaskAdmissionState>(Component, key))
            .Value.ShouldNotBeNull().Execution.ShouldNotBeNull();
        retained.ShouldBe(new DurableTaskExecutionState(
            secondLease.Fence,
            secondLease.Owner,
            Start + LeaseDuration + LeaseDuration,
            DurableTaskExecutionState.InitialProgress with { Status = DurableTaskStatus.Running, EffectPending = true },
            step));
        _ = owner.Received(holdSubmission ? 1 : 0).CommitAsync(step, Arg.Any<CancellationToken>());
    }

    /// <summary>Unavailable lookup leaves the checkpoint prepared and requires authoritative absence before submission.</summary>
    [Fact]
    public async Task LookupFailureLeavesThePreparedStepForAnotherLease()
    {
        var store = new InMemoryReadModelStore();
        var clock = Substitute.For<TimeProvider>();
        clock.GetUtcNow().Returns(Start);
        CommittedDurableTaskAdmission task = await AdmitAsync(store, clock);
        var recovery = new DurableTaskRecovery(store, Component, clock, LeaseDuration);
        IDurableTaskStepOwner owner = Substitute.For<IDurableTaskStepOwner>();
        owner.LookupAsync(Arg.Any<DurableTaskStep>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<DurableTaskOwnerReceipt?>(new IOException("Owner status is unavailable.")));
        DurableTaskLease first = (await recovery.TryClaimAsync(task, "worker-a")).ShouldNotBeNull();

        await Should.ThrowAsync<IOException>(() => recovery.RunStepAsync(task, first, StepName, owner));
        _ = owner.DidNotReceive().CommitAsync(Arg.Any<DurableTaskStep>(), Arg.Any<CancellationToken>());
        string key = DurableTaskAdmission.ValidateAndGetKey(task.Scope, task.RequestSha256);
        DurableTaskExecutionState before = (await store.GetAsync<DurableTaskAdmissionState>(Component, key))
            .Value.ShouldNotBeNull().Execution.ShouldNotBeNull();
        before.Progress.IsTerminal.ShouldBeFalse();
        DurableTaskStep prepared = before.PreparedStep.ShouldNotBeNull();
        prepared.Task.ShouldBe(task);

        clock.GetUtcNow().Returns(Start + LeaseDuration);
        DurableTaskLease second = (await recovery.TryClaimAsync(task, "worker-b")).ShouldNotBeNull();
        await Should.ThrowAsync<InvalidOperationException>(() => recovery.RunStepAsync(task, second, "changed-step@1", owner));
        owner.LookupAsync(prepared, Arg.Any<CancellationToken>()).Returns(Task.FromResult<DurableTaskOwnerReceipt?>(null));
        var receipt = new DurableTaskOwnerReceipt(prepared, new string('c', 64));
        owner.CommitAsync(prepared, Arg.Any<CancellationToken>()).Returns(receipt);
        (await recovery.RunStepAsync(task, second, StepName, owner)).ShouldBeTrue();
        Received.InOrder(() =>
        {
            _ = owner.LookupAsync(prepared, Arg.Any<CancellationToken>());
            _ = owner.LookupAsync(prepared, Arg.Any<CancellationToken>());
            _ = owner.CommitAsync(prepared, Arg.Any<CancellationToken>());
        });
    }

    /// <summary>A receipt under a different original actor cannot complete the prepared task.</summary>
    [Fact]
    public async Task ForeignOwnerReceiptLeavesOriginalCheckpointUnchanged()
    {
        var store = new InMemoryReadModelStore();
        CommittedDurableTaskAdmission task = await AdmitAsync(store, TimeProvider.System);
        var recovery = new DurableTaskRecovery(store, Component, TimeProvider.System, LeaseDuration);
        DurableTaskLease lease = (await recovery.TryClaimAsync(task, "worker-a")).ShouldNotBeNull();
        var observed = new List<DurableTaskStep>();
        IDurableTaskStepOwner owner = Substitute.For<IDurableTaskStepOwner>();
        owner.LookupAsync(Arg.Any<DurableTaskStep>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            DurableTaskStep step = call.Arg<DurableTaskStep>();
            observed.Add(step);
            DurableTaskStep foreign = step with { Task = task with { Scope = Scope with { ActorId = "other-actor" } } };
            return new DurableTaskOwnerReceipt(foreign, new string('b', 64));
        });

        await Should.ThrowAsync<InvalidOperationException>(() => recovery.RunStepAsync(task, lease, StepName, owner));
        _ = owner.DidNotReceive().CommitAsync(Arg.Any<DurableTaskStep>(), Arg.Any<CancellationToken>());
        string key = DurableTaskAdmission.ValidateAndGetKey(task.Scope, task.RequestSha256);
        DurableTaskAdmissionState retained = (await store.GetAsync<DurableTaskAdmissionState>(Component, key)).Value.ShouldNotBeNull();
        retained.Task.ShouldBe(task);
        DurableTaskExecutionState execution = retained.Execution.ShouldNotBeNull();
        execution.Owner.ShouldBe(lease.Owner);
        execution.Fence.ShouldBe(lease.Fence);
        execution.PreparedStep.ShouldBe(observed.ShouldHaveSingleItem());
        execution.Progress.EffectPending.ShouldBeTrue();
        execution.Progress.IsTerminal.ShouldBeFalse();
    }

    private static async Task<CommittedDurableTaskAdmission> AdmitAsync(IReadModelStore store, TimeProvider clock)
    {
        IDurableTaskAdmissionAuthorizer authorization = Substitute.For<IDurableTaskAdmissionAuthorizer>();
        authorization.AuthorizeAsync(Scope, Intent, Arg.Any<CancellationToken>())
            .Returns(new DurableTaskAdmissionAuthority(true, false, null));
        var admission = new DurableTaskAdmission(store, Component, authorization, clock);
        return (await admission.AdmitAsync(Scope, Intent)).Task.ShouldNotBeNull();
    }
}
