using System.Globalization;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.DomainService.DurableTasks;
using Hexalith.EventStore.DomainService.Tests.Fixtures;
using Hexalith.EventStore.Testing.Fakes;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests.DurableTasks;

/// <summary>Resolves delegated owner steps to the committed admission that binds their original actor.</summary>
public sealed class DurableTaskStepBindingTests
{
    private const string Component = "g1-task-state";
    private const string UnknownTaskId = "01KAZZZZZZZZZZZZZZZZZZZZZZ";
    private const int FailureBudget = 2;
    private static readonly string RequestHash = new('e', 64);
    private static readonly DurableTaskScope Scope = new("tenant", "original-actor", "sample.provision@1", "binding-key");
    private static readonly DateTimeOffset StartedAt = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Duration = TimeSpan.FromSeconds(30);

    /// <summary>Delegated step bindings, the failure budget and an owner refusal converge across replicas.</summary>
    [Fact]
    public Task StepBindingsConvergeAcrossReplicas()
    {
        var store = new InMemoryReadModelStore();
        return VerifyStepBindingsAcrossReplicasAsync(store, store, Component);
    }

    /// <summary>Only the canonical key of a started step of the same task resolves to a binding.</summary>
    /// <param name="format">The presented key, with the task identifier as argument zero.</param>
    [Theory]
    [InlineData("{0}/step/0")]
    [InlineData("{0}/step/01")]
    [InlineData("{0}/step/+1")]
    [InlineData("{0}/step/-1")]
    [InlineData("{0}/step/ 1")]
    [InlineData("{0}/step/1 ")]
    [InlineData("{0}/step/")]
    [InlineData("{0}/step/1/2")]
    [InlineData("{0}/STEP/1")]
    [InlineData("{0}/step/2147483648")]
    [InlineData("{0}/step/2")]
    [InlineData("01KAZZZZZZZZZZZZZZZZZZZZZZ/step/1")]
    public async Task NoncanonicalOrUnstartedStepKeysResolveToNoBinding(string format)
    {
        var clock = new ManualTimeProvider(StartedAt);
        var admission = new DurableTaskAdmission(new InMemoryReadModelStore(), Component, Authority(), clock);
        var worker = new DurableTaskExecution(admission, clock, Duration, FailureBudget);
        CommittedDurableTaskAdmission task = (await admission.AdmitAsync(Scope, RequestHash)).Task.ShouldNotBeNull();
        DurableTaskLease lease = (await worker.TryAcquireAsync(task.TaskId)).ShouldNotBeNull();
        await Should.ThrowAsync<IOException>(() => worker.RunStepAsync(lease, 1, Unavailable()));

        (await admission.ResolveStepAsync(task.TaskId, string.Format(CultureInfo.InvariantCulture, format, task.TaskId))).ShouldBeNull();
        (await admission.ResolveStepAsync(task.TaskId, $"{task.TaskId}/step/1")).ShouldNotBeNull().AcceptsExecution(lease.Fence).ShouldBeTrue();
        await Should.ThrowAsync<ArgumentException>(() => admission.ResolveStepAsync(task.TaskId, " "));
    }

    /// <summary>Runs the delegated binding, failure budget and owner refusal checks through two independently connected stores.</summary>
    internal static async Task VerifyStepBindingsAcrossReplicasAsync(IReadModelStore primary, IReadModelStore replica, string component)
    {
        var clock = new ManualTimeProvider(StartedAt);
        IDurableTaskAdmissionAuthorizer authority = Authority();
        var first = new DurableTaskAdmission(primary, component, authority, clock);
        var second = new DurableTaskAdmission(replica, component, authority, clock);
        var worker = new DurableTaskExecution(first, clock, Duration, FailureBudget);
        var replacement = new DurableTaskExecution(second, clock, Duration, FailureBudget);
        IDurableTaskEffect unavailable = Unavailable();

        // An admitted task binds no step until a worker records the step as pending.
        CommittedDurableTaskAdmission task = (await first.AdmitAsync(Scope with { IdempotencyKey = "binding-delegated" }, RequestHash)).Task.ShouldNotBeNull();
        string firstKey = $"{task.TaskId}/step/1";
        string secondKey = $"{task.TaskId}/step/2";
        (await second.ResolveStepAsync(task.TaskId, firstKey)).ShouldBeNull();
        DurableTaskLease stale = (await worker.TryAcquireAsync(task.TaskId)).ShouldNotBeNull();
        await Should.ThrowAsync<IOException>(() => worker.RunStepAsync(stale, 1, unavailable));
        DurableTaskStepBinding pending = (await second.ResolveStepAsync(task.TaskId, firstKey)).ShouldNotBeNull();
        pending.ShouldBe(new DurableTaskStepBinding(task, 1, firstKey, stale.Fence, DurableTaskStatus.WaitingForDependency, null));
        pending.Task.Scope.ShouldBe(Scope with { IdempotencyKey = "binding-delegated" });
        pending.Task.RequestSha256.ShouldBe(RequestHash);
        pending.AcceptsExecution(stale.Fence).ShouldBeTrue();
        (await second.ResolveStepAsync(task.TaskId, secondKey)).ShouldBeNull();

        // A takeover advances the fence, so the owner refuses the stale worker's delegated call.
        clock.Advance(Duration);
        DurableTaskLease lease = (await replacement.TryAcquireAsync(task.TaskId)).ShouldNotBeNull();
        DurableTaskStepBinding current = (await first.ResolveStepAsync(task.TaskId, firstKey)).ShouldNotBeNull();
        current.Status.ShouldBe(DurableTaskStatus.Running);
        current.AcceptsExecution(stale.Fence).ShouldBeFalse();
        current.AcceptsExecution(lease.Fence).ShouldBeTrue();
        int verifiedCalls = 0;
        IDurableTaskEffect owner = Owner(DurableTaskEffectLookup.Absent, null);
        owner.ExecuteAsync(Arg.Any<DurableTaskEffectStep>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            DurableTaskEffectStep step = call.Arg<DurableTaskEffectStep>();
            DurableTaskStepBinding? binding = await first.ResolveStepAsync(step.Task.TaskId, step.Key);
            if (binding is null || binding.Task != step.Task || !binding.AcceptsExecution(step.Fence))
            {
                // A stale or unbound call fails without reporting the authoritative Rejected outcome.
                throw new InvalidOperationException("The step binding does not accept this call.");
            }

            verifiedCalls++;
            return (DurableTaskEffectLookup.Committed, (string?)"owner-receipt-1");
        });
        (await replacement.RunStepAsync(lease, 1, owner)).ShouldBe("owner-receipt-1");
        verifiedCalls.ShouldBe(1);
        DurableTaskStepBinding committed = (await second.ResolveStepAsync(task.TaskId, firstKey)).ShouldNotBeNull();
        committed.Receipt.ShouldBe("owner-receipt-1");
        committed.AcceptsExecution(lease.Fence).ShouldBeFalse();

        // Exhausting the failure budget fences execution, keeps the pending binding for reconciliation and allows cancellation.
        await Should.ThrowAsync<IOException>(() => replacement.RunStepAsync(lease, 2, unavailable));
        DurableTaskProgress waiting = (await first.GetStatusAsync(task.TaskId)).ShouldNotBeNull().Progress;
        waiting.Status.ShouldBe(DurableTaskStatus.WaitingForDependency);
        waiting.Attempts.ShouldBe(1);
        (await first.CancelAsync(task.TaskId)).Outcome.ShouldBe(DurableTaskCancellationOutcome.Conflict);
        clock.Advance(Duration);
        DurableTaskLease last = (await worker.TryAcquireAsync(task.TaskId)).ShouldNotBeNull();
        await Should.ThrowAsync<IOException>(() => worker.RunStepAsync(last, 2, unavailable));
        DurableTaskProgress attention = (await second.GetStatusAsync(task.TaskId)).ShouldNotBeNull().Progress;
        attention.Status.ShouldBe(DurableTaskStatus.NeedsAttention);
        attention.Attempts.ShouldBe(FailureBudget);
        attention.EffectPending.ShouldBeTrue();
        attention.Receipts.ShouldBe(["owner-receipt-1"]);
        DurableTaskStepBinding unresolved = (await first.ResolveStepAsync(task.TaskId, secondKey)).ShouldNotBeNull();
        unresolved.Status.ShouldBe(DurableTaskStatus.NeedsAttention);
        unresolved.AcceptsExecution(last.Fence).ShouldBeFalse();
        clock.Advance(Duration);
        (await replacement.TryAcquireAsync(task.TaskId)).ShouldBeNull();
        DurableTaskCancellationResult cancelled = await second.CancelAsync(task.TaskId);
        cancelled.Outcome.ShouldBe(DurableTaskCancellationOutcome.Cancelled);
        cancelled.Current.ShouldNotBeNull().Progress.Receipts.ShouldBe(["owner-receipt-1"]);
        cancelled.Current.Progress.EffectPending.ShouldBeTrue();
        (await first.ResolveStepAsync(task.TaskId, secondKey)).ShouldNotBeNull().Status.ShouldBe(DurableTaskStatus.Cancelled);
        await unavailable.DidNotReceive().ExecuteAsync(Arg.Any<DurableTaskEffectStep>(), Arg.Any<CancellationToken>());
        await owner.Received(1).ExecuteAsync(Arg.Any<DurableTaskEffectStep>(), Arg.Any<CancellationToken>());

        // An owner's refusal of an irreversible step ends the task as Rejected with no effect and no binding.
        CommittedDurableTaskAdmission refusedTask = (await second.AdmitAsync(Scope with { IdempotencyKey = "binding-refused" }, RequestHash)).Task.ShouldNotBeNull();
        DurableTaskLease refusedLease = (await replacement.TryAcquireAsync(refusedTask.TaskId)).ShouldNotBeNull();
        IDurableTaskEffect refusing = Owner(DurableTaskEffectLookup.Absent, null);
        refusing.ExecuteAsync(Arg.Any<DurableTaskEffectStep>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(DurableTaskEffectLookup, string?)>((DurableTaskEffectLookup.Rejected, null)));
        (await replacement.RunIrreversibleStepAsync(refusedLease, 1, refusing)).ShouldBeNull();
        DurableTaskProgress refused = (await first.GetStatusAsync(refusedTask.TaskId)).ShouldNotBeNull().Progress;
        refused.Status.ShouldBe(DurableTaskStatus.Rejected);
        refused.IsTerminal.ShouldBeTrue();
        refused.EffectPending.ShouldBeFalse();
        refused.Checkpoint.ShouldBe(0);
        refused.IrreversibleStep.ShouldBe(1);
        (await first.ResolveStepAsync(refusedTask.TaskId, $"{refusedTask.TaskId}/step/1")).ShouldBeNull();
        await Should.ThrowAsync<InvalidOperationException>(() => replacement.RunIrreversibleStepAsync(refusedLease, 1, refusing));
        clock.Advance(Duration);
        (await worker.TryAcquireAsync(refusedTask.TaskId)).ShouldBeNull();
        (await first.CancelAsync(refusedTask.TaskId)).Outcome.ShouldBe(DurableTaskCancellationOutcome.Conflict);
        await refusing.Received(1).ExecuteAsync(Arg.Any<DurableTaskEffectStep>(), Arg.Any<CancellationToken>());

        // A committed step older than the latest checkpoint binds its own receipt and accepts no execution.
        CommittedDurableTaskAdmission history = (await first.AdmitAsync(Scope with { IdempotencyKey = "binding-history" }, RequestHash)).Task.ShouldNotBeNull();
        DurableTaskLease historyLease = (await worker.TryAcquireAsync(history.TaskId)).ShouldNotBeNull();
        IDurableTaskEffect recording = Owner(DurableTaskEffectLookup.Absent, null);
        recording.ExecuteAsync(Arg.Any<DurableTaskEffectStep>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult<(DurableTaskEffectLookup, string?)>((DurableTaskEffectLookup.Committed, $"history-receipt-{call.Arg<DurableTaskEffectStep>().Number}")));
        (await worker.RunStepAsync(historyLease, 1, recording)).ShouldBe("history-receipt-1");
        (await worker.RunStepAsync(historyLease, 2, recording)).ShouldBe("history-receipt-2");
        string olderKey = $"{history.TaskId}/step/1";
        DurableTaskStepBinding older = (await second.ResolveStepAsync(history.TaskId, olderKey)).ShouldNotBeNull();
        older.ShouldBe(new DurableTaskStepBinding(history, 1, olderKey, historyLease.Fence, DurableTaskStatus.Running, "history-receipt-1"));
        older.AcceptsExecution(historyLease.Fence).ShouldBeFalse();
        (await second.ResolveStepAsync(history.TaskId, $"{history.TaskId}/step/2")).ShouldNotBeNull().Receipt.ShouldBe("history-receipt-2");

        // Unknown tasks bind no step.
        (await second.ResolveStepAsync(UnknownTaskId, $"{UnknownTaskId}/step/1")).ShouldBeNull();
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

    private static IDurableTaskEffect Unavailable()
    {
        IDurableTaskEffect owner = Substitute.For<IDurableTaskEffect>();
        owner.LookupAsync(Arg.Any<DurableTaskEffectStep>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<(DurableTaskEffectLookup, string?)>(new IOException("Owner is unavailable.")));
        return owner;
    }
}
