using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.DomainService.DurableTasks;
using Hexalith.EventStore.DomainService.Tests.Fixtures;
using Hexalith.EventStore.Testing.Fakes;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests.DurableTasks;

/// <summary>Exercises fenced execution and authoritative effect recovery against the conditional store contract.</summary>
public sealed class DurableTaskExecutionTests
{
    private const string Component = "g1-task-state";
    private const string LeaseKey = "g1-execution-fixture-lease";
    private static readonly string RequestHash = new('a', 64);
    private static readonly DurableTaskScope Scope = new("tenant", "original-actor", "sample.provision@1", "execution-key");
    private static readonly DateTimeOffset StartedAt = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Duration = TimeSpan.FromSeconds(30);

    /// <summary>A replacement worker resumes after a lost owner reply and writes the original receipt once.</summary>
    [Fact]
    public async Task TakeoverRecoversTheCommittedOwnerReceipt()
    {
        var store = new InMemoryReadModelStore();
        await CommitWithLostReplyAsync(store, store);
        await RecoverAfterLostReplyAsync(store, store);
    }

    /// <summary>An in-flight reply under an expired lease cannot checkpoint after takeover.</summary>
    [Fact]
    public async Task LateOwnerReplyCannotAdvanceTheStaleWorker()
    {
        var store = new InMemoryReadModelStore();
        var clock = new ManualTimeProvider(StartedAt);
        var admission = new DurableTaskAdmission(store, Component, Authority(), clock);
        var first = new DurableTaskExecution(admission, clock, Duration);
        var second = new DurableTaskExecution(admission, clock, Duration);
        CommittedDurableTaskAdmission task = (await admission.AdmitAsync(Scope, RequestHash)).Task.ShouldNotBeNull();
        DurableTaskLease stale = (await first.TryAcquireAsync(task)).ShouldNotBeNull();
        IDurableTaskEffect owner = Substitute.For<IDurableTaskEffect>();
        owner.LookupAsync(Arg.Any<DurableTaskEffectStep>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(DurableTaskEffectLookup, string?)>((DurableTaskEffectLookup.Absent, null)));
        var requestStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reply = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        owner.ExecuteAsync(Arg.Any<DurableTaskEffectStep>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            requestStarted.SetResult();
            return reply.Task;
        });

        Task<string?> running = first.RunStepAsync(stale, 1, owner);
        await requestStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        clock.Advance(Duration);
        DurableTaskLease lease = (await second.TryAcquireAsync(task)).ShouldNotBeNull();
        reply.SetResult("committed");

        await Should.ThrowAsync<InvalidOperationException>(async () => await running);
        (await admission.GetProgressAsync(Scope, RequestHash)).ShouldNotBeNull().Checkpoint.ShouldBe(0);
        owner.LookupAsync(Arg.Is<DurableTaskEffectStep>(step => step.Fence == lease.Fence), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(DurableTaskEffectLookup, string?)>((DurableTaskEffectLookup.Committed, "committed")));
        (await second.RunStepAsync(lease, 1, owner)).ShouldBe("committed");
        await owner.Received(1).ExecuteAsync(Arg.Any<DurableTaskEffectStep>(), Arg.Any<CancellationToken>());
    }

    /// <summary>An unresolved partial effect blocks automatic takeover and unauthorized reads hide progress.</summary>
    [Fact]
    public async Task UnresolvedEffectRequiresAttentionAndReauthorizesStatus()
    {
        var store = new InMemoryReadModelStore();
        var clock = new ManualTimeProvider(StartedAt);
        IDurableTaskAdmissionAuthorizer authorizer = Authority();
        var admission = new DurableTaskAdmission(store, Component, authorizer, clock);
        var execution = new DurableTaskExecution(admission, clock, Duration);
        CommittedDurableTaskAdmission task = (await admission.AdmitAsync(Scope, RequestHash)).Task.ShouldNotBeNull();
        DurableTaskProgress initial = (await admission.GetProgressAsync(Scope, RequestHash)).ShouldNotBeNull();
        initial.Checkpoint.ShouldBe(0);
        initial.EffectPending.ShouldBeFalse();
        DurableTaskLease lease = (await execution.TryAcquireAsync(task)).ShouldNotBeNull();
        IDurableTaskEffect owner = Substitute.For<IDurableTaskEffect>();
        owner.LookupAsync(Arg.Any<DurableTaskEffectStep>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(DurableTaskEffectLookup, string?)>((DurableTaskEffectLookup.Unresolved, null)));

        (await execution.RunStepAsync(lease, 1, owner)).ShouldBeNull();
        DurableTaskProgress progress = (await admission.GetProgressAsync(Scope, RequestHash)).ShouldNotBeNull();
        progress.NeedsAttention.ShouldBeTrue();
        progress.Status.ShouldBe(DurableTaskStatus.NeedsAttention);
        progress.EffectPending.ShouldBeTrue();
        progress.Checkpoint.ShouldBe(0);
        clock.Advance(Duration);
        (await execution.TryAcquireAsync(task)).ShouldBeNull();
        await owner.DidNotReceive().ExecuteAsync(Arg.Any<DurableTaskEffectStep>(), Arg.Any<CancellationToken>());

        authorizer.AuthorizeAsync(Scope, RequestHash, Arg.Any<CancellationToken>())
            .Returns(new DurableTaskAdmissionAuthority(false, false, null));
        (await admission.GetProgressAsync(Scope, RequestHash)).ShouldBeNull();
    }

    /// <summary>A receipt that differs from the committed checkpoint fences the task in NeedsAttention.</summary>
    [Fact]
    public async Task MismatchedOwnerReceiptRequiresAttention()
    {
        var clock = new ManualTimeProvider(StartedAt);
        var admission = new DurableTaskAdmission(new InMemoryReadModelStore(), Component, Authority(), clock);
        var execution = new DurableTaskExecution(admission, clock, Duration);
        CommittedDurableTaskAdmission task = (await admission.AdmitAsync(Scope, RequestHash)).Task.ShouldNotBeNull();
        DurableTaskLease lease = (await execution.TryAcquireAsync(task)).ShouldNotBeNull();
        IDurableTaskEffect owner = Substitute.For<IDurableTaskEffect>();
        owner.LookupAsync(Arg.Any<DurableTaskEffectStep>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(DurableTaskEffectLookup, string?)>((DurableTaskEffectLookup.Absent, null)));
        TaskCompletionSource[] started = [new(TaskCreationOptions.RunContinuationsAsynchronously), new(TaskCreationOptions.RunContinuationsAsynchronously)];
        TaskCompletionSource<string>[] replies = [new(TaskCreationOptions.RunContinuationsAsynchronously), new(TaskCreationOptions.RunContinuationsAsynchronously)];
        int calls = -1;
        owner.ExecuteAsync(Arg.Any<DurableTaskEffectStep>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            int call = Interlocked.Increment(ref calls);
            started[call].SetResult();
            return replies[call].Task;
        });

        Task<string?> first = execution.RunStepAsync(lease, 1, owner);
        await started[0].Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task<string?> second = execution.RunStepAsync(lease, 1, owner);
        await started[1].Task.WaitAsync(TimeSpan.FromSeconds(5));
        replies[0].SetResult("receipt-1");
        (await first).ShouldBe("receipt-1");
        replies[1].SetResult("receipt-other");

        (await Should.ThrowAsync<InvalidOperationException>(async () => await second)).Message.ShouldContain("differs");
        DurableTaskProgress progress = (await admission.GetProgressAsync(Scope, RequestHash)).ShouldNotBeNull();
        progress.Status.ShouldBe(DurableTaskStatus.NeedsAttention);
        progress.Receipts.ShouldBe(["receipt-1"]);
        clock.Advance(Duration);
        (await execution.TryAcquireAsync(task)).ShouldBeNull();
    }

    /// <summary>Persists a checkpoint followed by an owner commit whose reply is lost.</summary>
    internal static async Task CommitWithLostReplyAsync(IReadModelStore primary, IReadModelStore replica)
    {
        var clock = new ManualTimeProvider(StartedAt);
        var first = new DurableTaskAdmission(primary, Component, Authority(), clock);
        var second = new DurableTaskAdmission(replica, Component, Authority(), clock);
        var worker = new DurableTaskExecution(first, clock, Duration);
        CommittedDurableTaskAdmission task = (await first.AdmitAsync(Scope, RequestHash)).Task.ShouldNotBeNull();
        DurableTaskLease lease = (await worker.TryAcquireAsync(task)).ShouldNotBeNull();
        (await new DurableTaskExecution(second, clock, Duration).TryAcquireAsync(task)).ShouldBeNull();
        (await primary.TrySaveAsync(Component, LeaseKey, lease, string.Empty)).ShouldBeTrue();

        IDurableTaskEffect owner = PersistedOwner(primary, loseSecondReply: true);
        (await worker.RunStepAsync(lease, 1, owner)).ShouldBe("receipt-1");
        await Should.ThrowAsync<IOException>(() => worker.RunStepAsync(lease, 2, owner));

        DurableTaskProgress pending = (await second.GetProgressAsync(Scope, RequestHash)).ShouldNotBeNull();
        pending.Receipts.ShouldBe(["receipt-1"]);
        pending.EffectPending.ShouldBeTrue();
        pending.NeedsAttention.ShouldBeFalse();
        pending.Status.ShouldBe(DurableTaskStatus.WaitingForDependency);
        (await replica.GetAsync<string>(Component, OwnerKey(task, 2))).Value.ShouldBe("receipt-2");
    }

    /// <summary>Recovers the owner commit from the retained provider after replacing the worker and both sidecars.</summary>
    internal static async Task RecoverAfterLostReplyAsync(IReadModelStore primary, IReadModelStore replica)
    {
        var clock = new ManualTimeProvider(StartedAt + Duration);
        var first = new DurableTaskAdmission(primary, Component, Authority(), clock);
        var second = new DurableTaskAdmission(replica, Component, Authority(), clock);
        var worker = new DurableTaskExecution(second, clock, Duration);
        DurableTaskLease stale = (await primary.GetAsync<DurableTaskLease>(Component, LeaseKey)).Value.ShouldNotBeNull();
        CommittedDurableTaskAdmission task = (await second.GetCommittedAsync(Scope, RequestHash)).ShouldNotBeNull();
        task.ShouldBe(stale.Task);
        DurableTaskLease lease = (await worker.TryAcquireAsync(task)).ShouldNotBeNull();
        lease.Fence.ShouldBe(stale.Fence + 1);
        lease.Owner.ShouldNotBe(stale.Owner);
        IDurableTaskEffect unavailableOwner = Substitute.For<IDurableTaskEffect>();
        unavailableOwner.LookupAsync(Arg.Any<DurableTaskEffectStep>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<(DurableTaskEffectLookup, string?)>(new IOException("Owner is unavailable.")));

        var staleWorker = new DurableTaskExecution(first, new ManualTimeProvider(StartedAt), Duration);
        await Should.ThrowAsync<InvalidOperationException>(() => staleWorker.RunStepAsync(stale, 2, unavailableOwner));
        await unavailableOwner.DidNotReceive().LookupAsync(Arg.Any<DurableTaskEffectStep>(), Arg.Any<CancellationToken>());
        await Should.ThrowAsync<IOException>(() => worker.RunStepAsync(lease, 2, unavailableOwner));
        await unavailableOwner.DidNotReceive().ExecuteAsync(Arg.Any<DurableTaskEffectStep>(), Arg.Any<CancellationToken>());
        DurableTaskProgress pending = (await first.GetProgressAsync(Scope, RequestHash)).ShouldNotBeNull();
        pending.Receipts.ShouldBe(["receipt-1"]);
        pending.EffectPending.ShouldBeTrue();
        pending.Status.ShouldBe(DurableTaskStatus.WaitingForDependency);

        IDurableTaskEffect owner = PersistedOwner(replica, loseSecondReply: false);
        (await worker.RunStepAsync(lease, 2, owner)).ShouldBe("receipt-2");
        (await worker.RunStepAsync(lease, 2, owner)).ShouldBe("receipt-2");
        await owner.Received(1).LookupAsync(new DurableTaskEffectStep(task, 2, $"{task.TaskId}/step/2", lease.Fence), Arg.Any<CancellationToken>());
        await owner.DidNotReceive().ExecuteAsync(Arg.Any<DurableTaskEffectStep>(), Arg.Any<CancellationToken>());
        DurableTaskProgress completed = (await first.GetProgressAsync(Scope, RequestHash)).ShouldNotBeNull();
        completed.Receipts.ShouldBe(["receipt-1", "receipt-2"]);
        completed.EffectPending.ShouldBeFalse();
        completed.NeedsAttention.ShouldBeFalse();
        completed.Status.ShouldBe(DurableTaskStatus.Running);
        DurableTaskProgress byId = (await second.GetStatusAsync(task.TaskId)).ShouldNotBeNull().Progress;
        byId.Receipts.ShouldBe(completed.Receipts);
        byId.Status.ShouldBe(DurableTaskStatus.Running);
        (await first.AdmitAsync(Scope, RequestHash)).Task.ShouldBe(task);
    }

    private static IDurableTaskAdmissionAuthorizer Authority()
    {
        IDurableTaskAdmissionAuthorizer authorizer = Substitute.For<IDurableTaskAdmissionAuthorizer>();
        authorizer.AuthorizeAsync(Scope, RequestHash, Arg.Any<CancellationToken>())
            .Returns(new DurableTaskAdmissionAuthority(true, false, null));
        return authorizer;
    }

    private static IDurableTaskEffect PersistedOwner(IReadModelStore store, bool loseSecondReply)
    {
        IDurableTaskEffect owner = Substitute.For<IDurableTaskEffect>();
        owner.LookupAsync(Arg.Any<DurableTaskEffectStep>(), Arg.Any<CancellationToken>())
            .Returns(call => LookupAsync(call.Arg<DurableTaskEffectStep>(), call.Arg<CancellationToken>()));
        owner.ExecuteAsync(Arg.Any<DurableTaskEffectStep>(), Arg.Any<CancellationToken>())
            .Returns(call => ExecuteAsync(call.Arg<DurableTaskEffectStep>(), call.Arg<CancellationToken>()));
        return owner;

        async Task<(DurableTaskEffectLookup, string?)> LookupAsync(DurableTaskEffectStep step, CancellationToken cancellationToken)
        {
            string? receipt = (await store.GetAsync<string>(Component, "owner-" + step.Key, cancellationToken)).Value;
            return receipt is null ? (DurableTaskEffectLookup.Absent, null) : (DurableTaskEffectLookup.Committed, receipt);
        }

        async Task<string> ExecuteAsync(DurableTaskEffectStep step, CancellationToken cancellationToken)
        {
            string receipt = $"receipt-{step.Number}";
            (await store.TrySaveAsync(Component, "owner-" + step.Key, receipt, string.Empty, cancellationToken)).ShouldBeTrue();
            return loseSecondReply && step.Number == 2 ? throw new IOException("Lost owner reply after commit.") : receipt;
        }
    }

    private static string OwnerKey(CommittedDurableTaskAdmission task, int number) => $"owner-{task.TaskId}/step/{number}";
}
