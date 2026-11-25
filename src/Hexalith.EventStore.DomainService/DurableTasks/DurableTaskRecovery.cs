using System.Text.Json;

using Hexalith.EventStore.Client.Projections;

namespace Hexalith.EventStore.DomainService.DurableTasks;

/// <summary>Runs one owner step for a known admitted Durable Task under a resumable, fenced checkpoint.</summary>
/// <param name="store">The conditional state store used by admission.</param>
/// <param name="storeName">The same strong-read, first-write CAS component used by admission.</param>
/// <param name="timeProvider">The shared UTC clock used to determine when workers may take over.</param>
/// <param name="leaseDuration">The time budget for one worker attempt.</param>
/// <remarks>
/// A trusted host supplies the committed task and the registered step. This type schedules no work and exposes no transport.
/// Lease expiry gates new work on the worker clock. A takeover changes the row ETag and fences writes from older claims.
/// </remarks>
public sealed class DurableTaskRecovery(
    IReadModelStore store,
    string storeName,
    TimeProvider timeProvider,
    TimeSpan leaseDuration)
{
    private const int MaxWrites = 8;

    private readonly IReadModelStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly string _storeName = !string.IsNullOrWhiteSpace(storeName)
        ? storeName : throw new ArgumentException("A state component is required.", nameof(storeName));
    private readonly TimeProvider _time = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    private readonly TimeSpan _leaseDuration = leaseDuration > TimeSpan.Zero
        ? leaseDuration : throw new ArgumentOutOfRangeException(nameof(leaseDuration));

    /// <summary>Claims the committed task, or returns null if it is unavailable, already leased, or complete.</summary>
    public async Task<DurableTaskLease?> TryClaimAsync(
        CommittedDurableTaskAdmission task,
        string workerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
        string key = DurableTaskAdmission.ValidateAndGetKey(task.Scope, task.RequestSha256);
        for (int attempt = 0; attempt < MaxWrites; attempt++)
        {
            ReadModelEntry<DurableTaskAdmissionState> entry = await ReadAsync(task, key, cancellationToken).ConfigureAwait(false);
            DurableTaskAdmissionState? current = entry.Value;
            DurableTaskExecutionState execution = current?.Execution ?? DurableTaskExecutionState.Initial;
            DateTimeOffset now = _time.GetUtcNow();
            if (current is null
                || execution.Progress.IsTerminal
                || execution.ExpiresAt > now
                || !DurableTaskExecutionState.CanAcquire(execution.Progress.Status))
            {
                return null;
            }

            var next = execution with
            {
                Owner = workerId,
                ExpiresAt = now + _leaseDuration,
                Fence = checked(execution.Fence + 1),
                Progress = execution.Progress with { Status = DurableTaskStatus.Running },
            };
            if (await TrySaveAsync(key, entry, next, cancellationToken).ConfigureAwait(false))
            {
                return new DurableTaskLease(task, workerId, next.Fence);
            }
        }

        throw new InvalidOperationException("Durable Task claim contention exceeded the retry budget.");
    }

    /// <summary>
    /// Prepares or resumes the committed step, consulting the owner before any submission.
    /// Returns false if the claim can no longer advance the task.
    /// </summary>
    /// <remarks>
    /// Run one attempt per claim. An ambiguous owner response leaves the step prepared.
    /// A subsequent claim resolves it by authoritative lookup under the same binding.
    /// </remarks>
    public async Task<bool> RunStepAsync(
        CommittedDurableTaskAdmission task,
        DurableTaskLease lease,
        string stepName,
        IDurableTaskStepOwner owner,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(stepName);
        string key = DurableTaskAdmission.ValidateAndGetKey(task.Scope, task.RequestSha256);
        ReadModelEntry<DurableTaskAdmissionState> entry = await ReadAsync(task, key, cancellationToken).ConfigureAwait(false);
        if (!Owns(entry, lease))
        {
            return false;
        }

        DurableTaskExecutionState execution = entry.Value!.Execution!;
        DurableTaskStep requested = CreateStep(entry.Value.Task!, stepName);
        DurableTaskStep step = execution.PreparedStep ?? requested;
        if (step != requested)
        {
            throw new InvalidOperationException("The registered step differs from the committed Durable Task checkpoint.");
        }

        if (execution.PreparedStep is null
            && !await TrySaveAsync(
                key,
                entry,
                execution with
                {
                    PreparedStep = step,
                    Progress = execution.Progress with { EffectPending = true, Status = DurableTaskStatus.Running },
                },
                cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        entry = await ReadAsync(task, key, cancellationToken).ConfigureAwait(false);
        if (!Owns(entry, lease))
        {
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();
        DurableTaskOwnerReceipt? receipt = await owner.LookupAsync(step, cancellationToken).ConfigureAwait(false);
        if (receipt is null)
        {
            entry = await ReadAsync(task, key, cancellationToken).ConfigureAwait(false);
            if (!Owns(entry, lease))
            {
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();
            receipt = await owner.CommitAsync(step, cancellationToken).ConfigureAwait(false);
        }

        entry = await ReadAsync(task, key, cancellationToken).ConfigureAwait(false);
        if (!Owns(entry, lease))
        {
            return false;
        }

        if (receipt?.Step != step || !DurableTaskAdmission.IsSha256(receipt.ReceiptSha256))
        {
            throw new InvalidOperationException("The owner receipt does not match the committed Durable Task step.");
        }

        DurableTaskExecutionState completed = entry.Value!.Execution! with
        {
            Owner = null,
            ExpiresAt = null,
            PreparedStep = step,
            Progress = entry.Value.Execution.Progress with
            {
                Receipts = entry.Value.Execution.Progress.Receipts.Add(receipt.ReceiptSha256),
                EffectPending = false,
                Status = DurableTaskStatus.Succeeded,
            },
        };
        return await TrySaveAsync(key, entry, completed, cancellationToken).ConfigureAwait(false);
    }

    private static DurableTaskStep CreateStep(CommittedDurableTaskAdmission task, string name)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new[] { task.TaskId, name });
        return new(task, name, "durable-task-step-v1-" + DurableTaskAdmission.Hash(bytes));
    }

    private async Task<ReadModelEntry<DurableTaskAdmissionState>> ReadAsync(
        CommittedDurableTaskAdmission task, string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ReadModelEntry<DurableTaskAdmissionState> entry = await DurableTaskAdmission
            .ReadAsync(_store, _storeName, task.Scope, key, cancellationToken).ConfigureAwait(false);
        if (entry.Value?.Task != task)
        {
            return new(null, null);
        }

        if (entry.Value.Execution is { } execution
            && (execution.PreparedStep is { } step && step != CreateStep(task, step.Name)
                || execution.Progress.IsTerminal
                    && (execution.PreparedStep is null
                        || execution.Progress.Receipts.IsDefaultOrEmpty
                        || !DurableTaskAdmission.IsSha256(execution.Progress.Receipts[^1]))))
        {
            throw new InvalidOperationException("The persisted Durable Task execution checkpoint is inconsistent.");
        }

        return entry;
    }

    private bool Owns(ReadModelEntry<DurableTaskAdmissionState> entry, DurableTaskLease lease)
        => entry.Value?.Execution is { } execution
            && !execution.Progress.IsTerminal
            && execution.Owner == lease.Owner
            && execution.Fence == lease.Fence
            && lease.Task == entry.Value.Task
            && DurableTaskExecutionState.IsLeased(execution.Progress.Status)
            && execution.ExpiresAt > _time.GetUtcNow();

    private Task<bool> TrySaveAsync(
        string key,
        ReadModelEntry<DurableTaskAdmissionState> entry,
        DurableTaskExecutionState execution,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _store.TrySaveAsync(_storeName, key, entry.Value! with { Execution = execution }, entry.ETag!, cancellationToken);
    }
}
