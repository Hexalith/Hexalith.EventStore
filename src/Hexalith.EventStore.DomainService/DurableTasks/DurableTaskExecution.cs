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

    /// <summary>Acquires an exclusive worker lease, returning null for a live lease or a task in NeedsAttention.</summary>
    public async Task<DurableTaskLease?> TryAcquireAsync(CommittedDurableTaskAdmission task, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        string owner = UniqueIdHelper.GenerateSortableUniqueStringId();
        DurableTaskExecutionState committed = await _admission.UpdateExecutionAsync(task, current =>
        {
            DateTimeOffset now = _time.GetUtcNow();
            if (current.ExpiresAt > now || current.Progress.NeedsAttention)
            {
                return current;
            }

            return current with { Owner = owner, ExpiresAt = now + _leaseDuration, Fence = checked(current.Fence + 1) };
        }, cancellationToken).ConfigureAwait(false);
        return committed.Owner == owner ? new(task, owner, committed.Fence) : null;
    }

    /// <summary>Runs the next owner step or returns an already committed checkpoint without invoking its owner.</summary>
    /// <remarks>Run one step at a time with each lease. A missing result from this method requires reconciliation. An owner exception keeps the pending step for authoritative lookup under a later lease.</remarks>
    public async Task<string?> RunStepAsync(
        DurableTaskLease lease,
        int number,
        IDurableTaskEffect effect,
        CancellationToken cancellationToken = default)
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

            return current with { Progress = current.Progress with { EffectPending = true } };
        }, cancellationToken).ConfigureAwait(false);
        if (number <= started.Progress.Checkpoint)
        {
            return started.Progress.Receipts[number - 1];
        }

        var step = new DurableTaskEffectStep(
            lease.Task, number, FormattableString.Invariant($"{lease.Task.TaskId}/step/{number}"), lease.Fence);
        (DurableTaskEffectLookup outcome, string? receipt) = await effect.LookupAsync(step, cancellationToken).ConfigureAwait(false);
        switch (outcome)
        {
            case DurableTaskEffectLookup.Unresolved when receipt is null:
                DurableTaskExecutionState attention = await UpdateOwnedAsync(lease, current =>
                    current.Progress.Checkpoint >= number ? current : current with
                    {
                        Owner = null,
                        ExpiresAt = null,
                        Progress = current.Progress with { NeedsAttention = true },
                    }, cancellationToken).ConfigureAwait(false);
                return attention.Progress.Checkpoint >= number ? attention.Progress.Receipts[number - 1] : null;

            case DurableTaskEffectLookup.Absent when receipt is null:
                DurableTaskExecutionState beforeEffect = await UpdateOwnedAsync(lease, static current => current, cancellationToken).ConfigureAwait(false);
                if (beforeEffect.Progress.Checkpoint >= number)
                {
                    return beforeEffect.Progress.Receipts[number - 1];
                }

                receipt = await effect.ExecuteAsync(step, cancellationToken).ConfigureAwait(false);
                break;

            case DurableTaskEffectLookup.Committed when !string.IsNullOrWhiteSpace(receipt):
                break;

            default:
                throw new InvalidOperationException("The Durable Task owner lookup returned inconsistent evidence.");
        }

        if (string.IsNullOrWhiteSpace(receipt))
        {
            throw new InvalidOperationException("A Durable Task owner effect requires a committed receipt.");
        }

        await UpdateOwnedAsync(lease, current =>
        {
            if (current.Progress.Checkpoint >= number)
            {
                return current.Progress.Receipts[number - 1] == receipt ? current
                    : throw new InvalidOperationException("The owner receipt differs from the committed checkpoint.");
            }

            return current with { Progress = new(current.Progress.Receipts.Add(receipt), false, false) };
        }, cancellationToken).ConfigureAwait(false);
        return receipt;
    }

    private Task<DurableTaskExecutionState> UpdateOwnedAsync(
        DurableTaskLease lease,
        Func<DurableTaskExecutionState, DurableTaskExecutionState> update,
        CancellationToken cancellationToken)
        => _admission.UpdateExecutionAsync(lease.Task, current =>
        {
            if (current.Owner != lease.Owner || current.Fence != lease.Fence
                || current.ExpiresAt is not { } expiresAt || expiresAt <= _time.GetUtcNow())
            {
                throw new InvalidOperationException("The Durable Task lease was lost.");
            }

            return update(current);
        }, cancellationToken);
}
