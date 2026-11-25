using Hexalith.Commons.UniqueIds;

namespace Hexalith.EventStore.DomainService.DurableTasks;

/// <summary>Fences workers and records owner receipts with the task checkpoint.</summary>
/// <param name="admission">The admission store shared by the trusted workers.</param>
/// <param name="time">The runtime's UTC lease clock.</param>
/// <param name="leaseDuration">The selected runtime's lease duration.</param>
/// <remarks>The worker host selects the owner operation and binds its credentials to the admitted step. The owner enforces current actor authority and idempotency across calls.</remarks>
public sealed class DurableTaskExecution(DurableTaskAdmission admission, TimeProvider time, TimeSpan leaseDuration)
{
    private readonly DurableTaskAdmission _admission = admission ?? throw new ArgumentNullException(nameof(admission));
    private readonly TimeProvider _time = time ?? throw new ArgumentNullException(nameof(time));
    private readonly TimeSpan _leaseDuration = leaseDuration > TimeSpan.Zero ? leaseDuration
        : throw new ArgumentOutOfRangeException(nameof(leaseDuration));

    /// <summary>Acquires an exclusive worker lease, returning null for a live lease, a task in NeedsAttention or a terminal task.</summary>
    public async Task<DurableTaskLease?> TryAcquireAsync(CommittedDurableTaskAdmission task, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        string owner = UniqueIdHelper.GenerateSortableUniqueStringId();
        DurableTaskExecutionState committed = await _admission.UpdateExecutionAsync(task, current =>
        {
            DateTimeOffset now = _time.GetUtcNow();
            if (current.ExpiresAt > now || !DurableTaskExecutionState.CanAcquire(current.Progress.Status))
            {
                return current;
            }

            return current with
            {
                Owner = owner,
                ExpiresAt = now + _leaseDuration,
                Fence = checked(current.Fence + 1),
                Progress = current.Progress with { Status = DurableTaskStatus.Running },
            };
        }, cancellationToken).ConfigureAwait(false);
        return committed.Owner == owner ? new(task, owner, committed.Fence) : null;
    }

    /// <summary>Resolves a scheduled task identifier and acquires its exclusive worker lease.</summary>
    /// <param name="taskId">The task identifier delivered by the runtime scheduler.</param>
    /// <param name="cancellationToken">The caller cancellation token.</param>
    /// <returns>The lease, or null for an unknown task, a live lease, a task in NeedsAttention or a terminal task.</returns>
    public async Task<DurableTaskLease?> TryAcquireAsync(string taskId, CancellationToken cancellationToken = default)
        => await _admission.ResolveCommittedAsync(taskId, cancellationToken).ConfigureAwait(false) is { } task
            ? await TryAcquireAsync(task, cancellationToken).ConfigureAwait(false)
            : null;

    /// <summary>Runs the next owner step or returns an already committed checkpoint without invoking its owner.</summary>
    /// <remarks>Run one step at a time with each lease. A missing result from this method requires reconciliation. An owner exception keeps the pending step for authoritative lookup under a later lease.</remarks>
    public Task<string?> RunStepAsync(
        DurableTaskLease lease,
        int number,
        IDurableTaskEffect effect,
        CancellationToken cancellationToken = default)
        => RunAsync(lease, number, effect, irreversible: false, cancellationToken);

    /// <summary>Runs an irreversible owner step, recording the irreversible checkpoint before the owner is called.</summary>
    /// <remarks>Cancellation is refused from the moment this step may reach its owner. Otherwise this behaves as <see cref="RunStepAsync"/>.</remarks>
    public Task<string?> RunIrreversibleStepAsync(
        DurableTaskLease lease,
        int number,
        IDurableTaskEffect effect,
        CancellationToken cancellationToken = default)
        => RunAsync(lease, number, effect, irreversible: true, cancellationToken);

    /// <summary>Records an immutable terminal outcome for a leased task with no pending owner step.</summary>
    /// <param name="lease">The current worker lease.</param>
    /// <param name="outcome"><see cref="DurableTaskStatus.Succeeded"/>, <see cref="DurableTaskStatus.Rejected"/> or <see cref="DurableTaskStatus.Failed"/>.</param>
    /// <param name="cancellationToken">The caller cancellation token.</param>
    /// <returns>The committed terminal progress.</returns>
    /// <remarks>Completion clears the lease. After an ambiguous completion, read the committed status instead of repeating the call.</remarks>
    public async Task<DurableTaskProgress> CompleteAsync(
        DurableTaskLease lease,
        DurableTaskStatus outcome,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lease);
        if (outcome is not (DurableTaskStatus.Succeeded or DurableTaskStatus.Rejected or DurableTaskStatus.Failed))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "A Durable Task completes as Succeeded, Rejected or Failed.");
        }

        DurableTaskExecutionState completed = await UpdateOwnedAsync(lease, current => current.Progress.EffectPending
            ? throw new InvalidOperationException("A Durable Task cannot complete while an owner step is pending.")
            : current with
            {
                Owner = null,
                ExpiresAt = null,
                Progress = current.Progress with { Status = outcome },
            }, cancellationToken).ConfigureAwait(false);
        return completed.Progress;
    }

    private async Task<string?> RunAsync(
        DurableTaskLease lease,
        int number,
        IDurableTaskEffect effect,
        bool irreversible,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(effect);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(number);

        DurableTaskExecutionState started = await UpdateOwnedAsync(lease, current =>
        {
            if (number <= current.Progress.Checkpoint)
            {
                return current;
            }

            if (current.Progress.Checkpoint != number - 1)
            {
                throw new InvalidOperationException("A Durable Task step must follow its committed checkpoint.");
            }

            return current with
            {
                Progress = current.Progress with
                {
                    EffectPending = true,
                    Status = DurableTaskStatus.Running,
                    IrreversibleStep = irreversible ? current.Progress.IrreversibleStep ?? number : current.Progress.IrreversibleStep,
                },
            };
        }, cancellationToken).ConfigureAwait(false);
        if (number <= started.Progress.Checkpoint)
        {
            return started.Progress.Receipts[number - 1];
        }

        var step = new DurableTaskEffectStep(
            lease.Task, number, FormattableString.Invariant($"{lease.Task.TaskId}/step/{number}"), lease.Fence);
        (DurableTaskEffectLookup outcome, string? receipt) = await CallOwnerAsync(
            lease, () => effect.LookupAsync(step, cancellationToken), cancellationToken).ConfigureAwait(false);
        switch (outcome)
        {
            case DurableTaskEffectLookup.Unresolved when receipt is null:
                DurableTaskExecutionState attention = await RequireAttentionAsync(lease, number, false, cancellationToken).ConfigureAwait(false);
                return attention.Progress.Checkpoint >= number ? attention.Progress.Receipts[number - 1] : null;

            case DurableTaskEffectLookup.Absent when receipt is null:
                DurableTaskExecutionState beforeEffect = await UpdateOwnedAsync(lease, static current => current, cancellationToken).ConfigureAwait(false);
                if (beforeEffect.Progress.Checkpoint >= number)
                {
                    return beforeEffect.Progress.Receipts[number - 1];
                }

                receipt = await CallOwnerAsync(lease, () => effect.ExecuteAsync(step, cancellationToken), cancellationToken).ConfigureAwait(false);
                break;

            case DurableTaskEffectLookup.Committed when !string.IsNullOrWhiteSpace(receipt):
                break;

            default:
                await RequireAttentionAsync(lease, number, false, cancellationToken).ConfigureAwait(false);
                throw new InvalidOperationException("The Durable Task owner lookup returned inconsistent evidence.");
        }

        if (string.IsNullOrWhiteSpace(receipt))
        {
            await RequireAttentionAsync(lease, number, false, cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException("A Durable Task owner effect requires a committed receipt.");
        }

        DurableTaskExecutionState committed = await UpdateOwnedAsync(lease, current =>
        {
            if (current.Progress.Checkpoint >= number)
            {
                return current;
            }

            return current with
            {
                Progress = current.Progress with
                {
                    Receipts = current.Progress.Receipts.Add(receipt),
                    EffectPending = false,
                    Status = DurableTaskStatus.Running,
                },
            };
        }, cancellationToken).ConfigureAwait(false);
        if (committed.Progress.Receipts[number - 1] != receipt)
        {
            await RequireAttentionAsync(lease, number, true, cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException("The owner receipt differs from the committed checkpoint.");
        }

        return receipt;
    }

    /// <summary>Fences automatic execution in NeedsAttention and releases the lease.</summary>
    /// <param name="lease">The current worker lease.</param>
    /// <param name="number">The step being resolved.</param>
    /// <param name="evenIfCommitted">Whether to fence when the step already has a committed checkpoint.</param>
    /// <param name="cancellationToken">The caller cancellation token.</param>
    private Task<DurableTaskExecutionState> RequireAttentionAsync(
        DurableTaskLease lease, int number, bool evenIfCommitted, CancellationToken cancellationToken)
        => UpdateOwnedAsync(lease, current => current.Progress.Checkpoint >= number && !evenIfCommitted ? current : current with
        {
            Owner = null,
            ExpiresAt = null,
            Progress = current.Progress with { Status = DurableTaskStatus.NeedsAttention },
        }, cancellationToken);

    private async Task<T> CallOwnerAsync<T>(DurableTaskLease lease, Func<Task<T>> call, CancellationToken cancellationToken)
    {
        try
        {
            return await call().ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            await RecordDependencyDelayAsync(lease, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private async Task RecordDependencyDelayAsync(DurableTaskLease lease, CancellationToken cancellationToken)
    {
        try
        {
            _ = await UpdateOwnedAsync(lease, static current => current.Progress.Status == DurableTaskStatus.Running
                ? current with { Progress = current.Progress with { Status = DurableTaskStatus.WaitingForDependency } }
                : current, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // The pending step stays recorded for authoritative lookup. The caller receives the owner failure.
        }
    }

    private Task<DurableTaskExecutionState> UpdateOwnedAsync(
        DurableTaskLease lease,
        Func<DurableTaskExecutionState, DurableTaskExecutionState> update,
        CancellationToken cancellationToken)
        => _admission.UpdateExecutionAsync(lease.Task, current =>
        {
            if (current.Owner != lease.Owner || current.Fence != lease.Fence || !DurableTaskExecutionState.IsLeased(current.Progress.Status)
                || current.ExpiresAt is not { } expiresAt || expiresAt <= _time.GetUtcNow())
            {
                throw new InvalidOperationException("The Durable Task lease was lost.");
            }

            return update(current);
        }, cancellationToken);
}
