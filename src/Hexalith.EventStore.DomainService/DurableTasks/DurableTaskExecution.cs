using Hexalith.Commons.UniqueIds;

namespace Hexalith.EventStore.DomainService.DurableTasks;

/// <summary>Fences workers and records owner receipts with the task checkpoint.</summary>
/// <param name="admission">The admission store shared by the trusted workers.</param>
/// <param name="time">The runtime's UTC lease clock.</param>
/// <param name="leaseDuration">The selected runtime's lease duration.</param>
/// <param name="dependencyFailureBudget">The attempts that may leave one pending step uncommitted before the task needs attention.</param>
/// <remarks>The worker host selects the owner operation and binds its credentials to the admitted step. The owner enforces current actor authority and idempotency across calls.</remarks>
public sealed class DurableTaskExecution(DurableTaskAdmission admission, TimeProvider time, TimeSpan leaseDuration, int dependencyFailureBudget)
{
    private readonly DurableTaskAdmission _admission = admission ?? throw new ArgumentNullException(nameof(admission));
    private readonly TimeProvider _time = time ?? throw new ArgumentNullException(nameof(time));
    private readonly TimeSpan _leaseDuration = leaseDuration > TimeSpan.Zero ? leaseDuration
        : throw new ArgumentOutOfRangeException(nameof(leaseDuration));

    private readonly int _dependencyFailureBudget = dependencyFailureBudget > 0 ? dependencyFailureBudget
        : throw new ArgumentOutOfRangeException(nameof(dependencyFailureBudget));

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
    /// <remarks>Run one step at a time with each lease. Each attempt at a pending step is charged against the dependency
    /// failure budget when it starts, so an attempt that outlives its lease or is abandoned still counts. A committed receipt
    /// or a refusal resets the charge, and an attempt the caller cancels is refunded. A reported owner failure on the last
    /// allowed attempt, or an attempt beyond the budget, moves the task to NeedsAttention without calling the owner again.
    /// A null result means the step did not commit and the task is in NeedsAttention or Rejected; read its committed status.
    /// An owner exception keeps the pending step for authoritative lookup under a later lease.</remarks>
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
            DurableTaskProgress progress = current.Progress;
            if (number <= progress.Checkpoint)
            {
                return current;
            }

            if (progress.Checkpoint != number - 1)
            {
                throw new InvalidOperationException("A Durable Task step must follow its committed checkpoint.");
            }

            int attempts = progress.EffectPending ? checked(progress.Attempts + 1) : 1;
            if (attempts > _dependencyFailureBudget)
            {
                // Every allowed attempt left the step pending, including attempts that outlived their lease or were abandoned.
                return current with
                {
                    Owner = null,
                    ExpiresAt = null,
                    Progress = progress with { Status = DurableTaskStatus.NeedsAttention },
                };
            }

            return current with
            {
                Progress = progress with
                {
                    EffectPending = true,
                    Status = DurableTaskStatus.Running,
                    IrreversibleStep = irreversible ? progress.IrreversibleStep ?? number : progress.IrreversibleStep,
                    Attempts = attempts,
                },
            };
        }, cancellationToken).ConfigureAwait(false);
        if (number <= started.Progress.Checkpoint)
        {
            return started.Progress.Receipts[number - 1];
        }

        if (started.Progress.Status == DurableTaskStatus.NeedsAttention)
        {
            return null;
        }

        try
        {
            return await AttemptAsync(lease, number, effect, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await RefundAttemptAsync(lease).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Resolves the started step through the owner and records its outcome.</summary>
    /// <param name="lease">The current worker lease.</param>
    /// <param name="number">The started step.</param>
    /// <param name="effect">The owner operations for the step.</param>
    /// <param name="cancellationToken">The caller cancellation token.</param>
    /// <returns>The committed receipt, or null when the task is in NeedsAttention or Rejected.</returns>
    private async Task<string?> AttemptAsync(
        DurableTaskLease lease,
        int number,
        IDurableTaskEffect effect,
        CancellationToken cancellationToken)
    {
        var step = new DurableTaskEffectStep(lease.Task, number, DurableTaskEffectStep.KeyFor(lease.Task.TaskId, number), lease.Fence);
        (DurableTaskEffectLookup outcome, string? receipt) = await CallOwnerAsync(
            lease, () => effect.LookupAsync(step, cancellationToken), cancellationToken).ConfigureAwait(false);
        bool executed = false;
        if (outcome == DurableTaskEffectLookup.Absent && receipt is null)
        {
            DurableTaskExecutionState beforeEffect = await UpdateOwnedAsync(lease, static current => current, cancellationToken).ConfigureAwait(false);
            if (beforeEffect.Progress.Checkpoint >= number)
            {
                return beforeEffect.Progress.Receipts[number - 1];
            }

            (outcome, receipt) = await CallOwnerAsync(lease, () => effect.ExecuteAsync(step, cancellationToken), cancellationToken).ConfigureAwait(false);
            executed = true;
        }

        switch (outcome)
        {
            case DurableTaskEffectLookup.Committed when !string.IsNullOrWhiteSpace(receipt):
                break;

            case DurableTaskEffectLookup.Unresolved when receipt is null:
                DurableTaskExecutionState attention = await RequireAttentionAsync(lease, number, false, cancellationToken).ConfigureAwait(false);
                return attention.Progress.Checkpoint >= number ? attention.Progress.Receipts[number - 1] : null;

            case DurableTaskEffectLookup.Rejected when receipt is null:
                if ((await RejectAsync(lease, number, cancellationToken).ConfigureAwait(false)).Progress.Status == DurableTaskStatus.Rejected)
                {
                    return null;
                }

                await RequireAttentionAsync(lease, number, true, cancellationToken).ConfigureAwait(false);
                throw new InvalidOperationException("The owner rejected a step that has a committed receipt.");

            default:
                await RequireAttentionAsync(lease, number, false, cancellationToken).ConfigureAwait(false);
                throw new InvalidOperationException(executed
                    ? "The Durable Task owner effect returned inconsistent evidence."
                    : "The Durable Task owner lookup returned inconsistent evidence.");
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
                    Receipts = current.Progress.Receipts.Add(receipt!),
                    EffectPending = false,
                    Status = DurableTaskStatus.Running,
                    Attempts = 0,
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

    /// <summary>Records the owner's authoritative refusal of the pending step as the immutable Rejected outcome.</summary>
    /// <param name="lease">The current worker lease.</param>
    /// <param name="number">The refused step.</param>
    /// <param name="cancellationToken">The caller cancellation token.</param>
    /// <returns>The committed state, unchanged when the step already has a committed receipt.</returns>
    private Task<DurableTaskExecutionState> RejectAsync(DurableTaskLease lease, int number, CancellationToken cancellationToken)
        => UpdateOwnedAsync(lease, current => current.Progress.Checkpoint >= number ? current : current with
        {
            Owner = null,
            ExpiresAt = null,
            Progress = current.Progress with { EffectPending = false, Status = DurableTaskStatus.Rejected, Attempts = 0 },
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
            _ = await UpdateOwnedAsync(lease, current =>
            {
                if (!current.Progress.EffectPending)
                {
                    return current;
                }

                // The attempt was charged when it started, so the failure itself consumes no further budget.
                return current.Progress.Attempts >= _dependencyFailureBudget
                    ? current with
                    {
                        Owner = null,
                        ExpiresAt = null,
                        Progress = current.Progress with { Status = DurableTaskStatus.NeedsAttention },
                    }
                    : current with { Progress = current.Progress with { Status = DurableTaskStatus.WaitingForDependency } };
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // The pending step stays recorded for authoritative lookup. The caller receives the owner failure.
        }
    }

    /// <summary>Refunds the charge of an attempt the caller cancelled. The step stays pending for authoritative lookup.</summary>
    /// <param name="lease">The current worker lease.</param>
    private async Task RefundAttemptAsync(DurableTaskLease lease)
    {
        try
        {
            // The caller's token is already cancelled, so the refund write cannot use it.
            _ = await UpdateOwnedAsync(lease, static current => current.Progress is { EffectPending: true, Attempts: > 0 }
                ? current with { Progress = current.Progress with { Attempts = current.Progress.Attempts - 1 } }
                : current, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Without the refund the cancelled attempt stays charged against the budget.
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
