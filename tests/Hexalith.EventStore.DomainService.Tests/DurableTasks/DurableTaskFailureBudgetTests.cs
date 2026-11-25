using Hexalith.EventStore.DomainService.DurableTasks;
using Hexalith.EventStore.DomainService.Tests.Fixtures;
using Hexalith.EventStore.Testing.Fakes;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests.DurableTasks;

/// <summary>Charges every attempt at a pending step against the dependency failure budget.</summary>
public sealed class DurableTaskFailureBudgetTests
{
    private const string Component = "g1-task-state";
    private static readonly string RequestHash = new('7', 64);
    private static readonly DurableTaskScope Scope = new("tenant", "original-actor", "sample.provision@1", "budget-key");
    private static readonly DateTimeOffset StartedAt = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Duration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    /// <summary>A budget must allow at least one attempt.</summary>
    /// <param name="budget">The rejected budget.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveBudgetIsRejected(int budget)
    {
        var clock = new ManualTimeProvider(StartedAt);
        var admission = new DurableTaskAdmission(new InMemoryReadModelStore(), Component, Authority(), clock);

        Should.Throw<ArgumentOutOfRangeException>(() => new DurableTaskExecution(admission, clock, Duration, budget))
            .ParamName.ShouldBe("dependencyFailureBudget");
    }

    /// <summary>A budget of one allows a single attempt, whose reported failure fences execution and allows cancellation.</summary>
    [Fact]
    public async Task BudgetOfOneAllowsOneAttempt()
    {
        (DurableTaskAdmission admission, DurableTaskExecution worker, ManualTimeProvider clock, CommittedDurableTaskAdmission task) = await StartAsync(1);
        IDurableTaskEffect unavailable = Unavailable();

        DurableTaskLease lease = (await worker.TryAcquireAsync(task.TaskId)).ShouldNotBeNull();
        await Should.ThrowAsync<IOException>(() => worker.RunStepAsync(lease, 1, unavailable));

        DurableTaskProgress attention = await ProgressAsync(admission, task);
        attention.Status.ShouldBe(DurableTaskStatus.NeedsAttention);
        attention.Attempts.ShouldBe(1);
        attention.EffectPending.ShouldBeTrue();
        clock.Advance(Duration);
        (await worker.TryAcquireAsync(task.TaskId)).ShouldBeNull();
        (await admission.CancelAsync(task.TaskId)).Outcome.ShouldBe(DurableTaskCancellationOutcome.Cancelled);
        await unavailable.Received(1).LookupAsync(Arg.Any<DurableTaskEffectStep>(), Arg.Any<CancellationToken>());
    }

    /// <summary>A failed owner call consumes the budget like a failed lookup.</summary>
    [Fact]
    public async Task ExecuteFailureConsumesTheBudget()
    {
        (DurableTaskAdmission admission, DurableTaskExecution worker, ManualTimeProvider clock, CommittedDurableTaskAdmission task) = await StartAsync(2);
        IDurableTaskEffect owner = Owner(DurableTaskEffectLookup.Absent);
        owner.ExecuteAsync(Arg.Any<DurableTaskEffectStep>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<(DurableTaskEffectLookup, string?)>(new IOException("Owner is unavailable.")));

        DurableTaskLease lease = (await worker.TryAcquireAsync(task.TaskId)).ShouldNotBeNull();
        await Should.ThrowAsync<IOException>(() => worker.RunStepAsync(lease, 1, owner));
        DurableTaskProgress waiting = await ProgressAsync(admission, task);
        waiting.Status.ShouldBe(DurableTaskStatus.WaitingForDependency);
        waiting.Attempts.ShouldBe(1);

        clock.Advance(Duration);
        lease = (await worker.TryAcquireAsync(task.TaskId)).ShouldNotBeNull();
        await Should.ThrowAsync<IOException>(() => worker.RunStepAsync(lease, 1, owner));
        DurableTaskProgress attention = await ProgressAsync(admission, task);
        attention.Status.ShouldBe(DurableTaskStatus.NeedsAttention);
        attention.Attempts.ShouldBe(2);
        clock.Advance(Duration);
        (await worker.TryAcquireAsync(task.TaskId)).ShouldBeNull();
        await owner.Received(2).ExecuteAsync(Arg.Any<DurableTaskEffectStep>(), Arg.Any<CancellationToken>());
    }

    /// <summary>An owner failure reported after the lease expired is not recorded, and the attempts still consume the budget.</summary>
    [Fact]
    public async Task OwnerFailureAfterTheLeaseExpiresConsumesTheBudget()
    {
        (DurableTaskAdmission admission, DurableTaskExecution worker, ManualTimeProvider clock, CommittedDurableTaskAdmission task) = await StartAsync(2);
        IDurableTaskEffect slow = Substitute.For<IDurableTaskEffect>();
        slow.LookupAsync(Arg.Any<DurableTaskEffectStep>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            clock.Advance(Duration);
            return Task.FromException<(DurableTaskEffectLookup, string?)>(new IOException("Owner timed out."));
        });

        for (int attempt = 1; attempt <= 2; attempt++)
        {
            DurableTaskLease lease = (await worker.TryAcquireAsync(task.TaskId)).ShouldNotBeNull();
            await Should.ThrowAsync<IOException>(() => worker.RunStepAsync(lease, 1, slow));
            DurableTaskProgress unrecorded = await ProgressAsync(admission, task);
            unrecorded.Status.ShouldBe(DurableTaskStatus.Running);
            unrecorded.Attempts.ShouldBe(attempt);
        }

        DurableTaskLease last = (await worker.TryAcquireAsync(task.TaskId)).ShouldNotBeNull();
        (await worker.RunStepAsync(last, 1, slow)).ShouldBeNull();
        DurableTaskProgress attention = await ProgressAsync(admission, task);
        attention.Status.ShouldBe(DurableTaskStatus.NeedsAttention);
        attention.Attempts.ShouldBe(2);
        attention.EffectPending.ShouldBeTrue();
        await slow.Received(2).LookupAsync(Arg.Any<DurableTaskEffectStep>(), Arg.Any<CancellationToken>());
        (await admission.CancelAsync(task.TaskId)).Outcome.ShouldBe(DurableTaskCancellationOutcome.Cancelled);
    }

    /// <summary>Workers that abandon the pending step consume the budget without reporting a failure.</summary>
    [Fact]
    public async Task AbandonedAttemptsConsumeTheBudget()
    {
        (DurableTaskAdmission admission, DurableTaskExecution worker, ManualTimeProvider clock, CommittedDurableTaskAdmission task) = await StartAsync(2);
        IDurableTaskEffect owner = Owner(DurableTaskEffectLookup.Absent);
        TaskCompletionSource[] started = [new(TaskCreationOptions.RunContinuationsAsynchronously), new(TaskCreationOptions.RunContinuationsAsynchronously)];
        TaskCompletionSource<(DurableTaskEffectLookup, string?)>[] replies = [new(TaskCreationOptions.RunContinuationsAsynchronously), new(TaskCreationOptions.RunContinuationsAsynchronously)];
        int calls = -1;
        owner.ExecuteAsync(Arg.Any<DurableTaskEffectStep>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            int call = Interlocked.Increment(ref calls);
            started[call].SetResult();
            return replies[call].Task;
        });

        var abandoned = new List<Task<string?>>();
        for (int attempt = 0; attempt < 2; attempt++)
        {
            DurableTaskLease lease = (await worker.TryAcquireAsync(task.TaskId)).ShouldNotBeNull();
            abandoned.Add(worker.RunStepAsync(lease, 1, owner));
            await started[attempt].Task.WaitAsync(Timeout);
            clock.Advance(Duration);
        }

        DurableTaskLease last = (await worker.TryAcquireAsync(task.TaskId)).ShouldNotBeNull();
        (await worker.RunStepAsync(last, 1, owner)).ShouldBeNull();
        DurableTaskProgress attention = await ProgressAsync(admission, task);
        attention.Status.ShouldBe(DurableTaskStatus.NeedsAttention);
        attention.Attempts.ShouldBe(2);
        await owner.Received(2).ExecuteAsync(Arg.Any<DurableTaskEffectStep>(), Arg.Any<CancellationToken>());

        foreach (TaskCompletionSource<(DurableTaskEffectLookup, string?)> reply in replies)
        {
            reply.SetResult((DurableTaskEffectLookup.Committed, "late-receipt"));
        }

        foreach (Task<string?> late in abandoned)
        {
            await Should.ThrowAsync<InvalidOperationException>(() => late);
        }

        (await ProgressAsync(admission, task)).Checkpoint.ShouldBe(0);
    }

    /// <summary>An attempt the caller cancels is refunded, so a budget of one still allows the next attempt.</summary>
    [Fact]
    public async Task CallerCancellationConsumesNoBudget()
    {
        (DurableTaskAdmission admission, DurableTaskExecution worker, _, CommittedDurableTaskAdmission task) = await StartAsync(1);
        using var cancellation = new CancellationTokenSource();
        IDurableTaskEffect cancelled = Substitute.For<IDurableTaskEffect>();
        cancelled.LookupAsync(Arg.Any<DurableTaskEffectStep>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            cancellation.Cancel();
            return Task.FromException<(DurableTaskEffectLookup, string?)>(new OperationCanceledException(cancellation.Token));
        });

        DurableTaskLease lease = (await worker.TryAcquireAsync(task.TaskId)).ShouldNotBeNull();
        await Should.ThrowAsync<OperationCanceledException>(() => worker.RunStepAsync(lease, 1, cancelled, cancellation.Token));
        DurableTaskProgress refunded = await ProgressAsync(admission, task);
        refunded.Status.ShouldBe(DurableTaskStatus.Running);
        refunded.EffectPending.ShouldBeTrue();
        refunded.Attempts.ShouldBe(0);

        IDurableTaskEffect owner = Owner(DurableTaskEffectLookup.Committed, "receipt-1");
        (await worker.RunStepAsync(lease, 1, owner)).ShouldBe("receipt-1");
        DurableTaskProgress committed = await ProgressAsync(admission, task);
        committed.Receipts.ShouldBe(["receipt-1"]);
        committed.Attempts.ShouldBe(0);
    }

    private static async Task<(DurableTaskAdmission Admission, DurableTaskExecution Worker, ManualTimeProvider Clock, CommittedDurableTaskAdmission Task)> StartAsync(int budget)
    {
        var clock = new ManualTimeProvider(StartedAt);
        var admission = new DurableTaskAdmission(new InMemoryReadModelStore(), Component, Authority(), clock);
        CommittedDurableTaskAdmission task = (await admission.AdmitAsync(Scope, RequestHash)).Task.ShouldNotBeNull();
        return (admission, new DurableTaskExecution(admission, clock, Duration, budget), clock, task);
    }

    private static async Task<DurableTaskProgress> ProgressAsync(DurableTaskAdmission admission, CommittedDurableTaskAdmission task)
        => (await admission.GetStatusAsync(task.TaskId)).ShouldNotBeNull().Progress;

    private static IDurableTaskAdmissionAuthorizer Authority()
    {
        IDurableTaskAdmissionAuthorizer authorizer = Substitute.For<IDurableTaskAdmissionAuthorizer>();
        authorizer.AuthorizeAsync(Arg.Any<DurableTaskScope>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new DurableTaskAdmissionAuthority(true, false, null));
        return authorizer;
    }

    private static IDurableTaskEffect Owner(DurableTaskEffectLookup lookup, string? receipt = null)
    {
        IDurableTaskEffect owner = Substitute.For<IDurableTaskEffect>();
        owner.LookupAsync(Arg.Any<DurableTaskEffectStep>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(DurableTaskEffectLookup, string?)>((lookup, receipt)));
        return owner;
    }

    private static IDurableTaskEffect Unavailable()
    {
        IDurableTaskEffect owner = Substitute.For<IDurableTaskEffect>();
        owner.LookupAsync(Arg.Any<DurableTaskEffectStep>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<(DurableTaskEffectLookup, string?)>(new IOException("Owner is unavailable.")));
        return owner;
    }
}
