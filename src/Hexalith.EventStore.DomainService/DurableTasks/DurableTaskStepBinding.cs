namespace Hexalith.EventStore.DomainService.DurableTasks;

/// <summary>The committed admission bound to one started owner step.</summary>
/// <param name="Task">The admitted task with its original Tenant, actor, operation and request hash.</param>
/// <param name="Number">The one-based step number.</param>
/// <param name="Key">The stable step key.</param>
/// <param name="Fence">The task's current lease generation.</param>
/// <param name="Status">The committed lifecycle state.</param>
/// <param name="Receipt">The committed owner receipt, absent while the step is pending.</param>
/// <remarks>An owner verifying a delegated step checks the target it derives from the step, then re-evaluates the original
/// actor's current authority for the operation. The binding grants no authority. An owner refuses a binding that does not
/// accept the presented fence by failing the call, without reporting <see cref="DurableTaskEffectLookup.Rejected"/>.</remarks>
public sealed record DurableTaskStepBinding(
    CommittedDurableTaskAdmission Task,
    int Number,
    string Key,
    long Fence,
    DurableTaskStatus Status,
    string? Receipt)
{
    /// <summary>Whether a call presenting this fence may invoke the pending step.</summary>
    /// <param name="fence">The lease generation presented with the owner call.</param>
    /// <returns>True when the step has no receipt, the task is Running or WaitingForDependency and the fence is current.</returns>
    /// <remarks>Acceptance is fence-based. It also accepts a worker whose lease expired before another worker took the task
    /// over. The step key's idempotency and the next lease's lookup recover such a call.</remarks>
    public bool AcceptsExecution(long fence)
        => Receipt is null && Fence == fence && DurableTaskExecutionState.IsLeased(Status);
}
