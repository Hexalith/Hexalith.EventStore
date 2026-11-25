using System.Text.Json.Serialization;

namespace Hexalith.EventStore.DomainService.DurableTasks;

/// <summary>The execution state retained on the admission row.</summary>
/// <param name="Fence">The monotonically increasing lease generation.</param>
/// <param name="Owner">The identity of the worker lease, when held.</param>
/// <param name="ExpiresAt">The exclusive expiry of the worker lease.</param>
/// <param name="Progress">The lifecycle state, checkpoint and pending effect state.</param>
/// <param name="PreparedStep">The named owner step prepared before the first submission, when recovery retains it.</param>
internal sealed record DurableTaskExecutionState(
    long Fence,
    string? Owner,
    DateTimeOffset? ExpiresAt,
    DurableTaskProgress Progress,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DurableTaskStep? PreparedStep = null)
{
    /// <summary>Progress of an admitted task before any lease.</summary>
    internal static DurableTaskProgress InitialProgress { get; } = new([], false, DurableTaskStatus.Pending);

    /// <summary>Execution state of an admitted task before any lease.</summary>
    internal static DurableTaskExecutionState Initial { get; } = new(0, null, null, InitialProgress);

    /// <summary>Whether a worker lease may be acquired once any current lease has expired.</summary>
    internal static bool CanAcquire(DurableTaskStatus status)
        => status is DurableTaskStatus.Pending or DurableTaskStatus.Running or DurableTaskStatus.WaitingForDependency;

    /// <summary>Whether a lease holder may advance or complete the task.</summary>
    internal static bool IsLeased(DurableTaskStatus status)
        => status is DurableTaskStatus.Running or DurableTaskStatus.WaitingForDependency;

    /// <summary>Whether cancellation is allowed before the irreversible checkpoint.</summary>
    /// <remarks>An owner step in flight blocks cancellation. From NeedsAttention the unresolved step stays recorded as pending.</remarks>
    internal static bool CanCancel(DurableTaskProgress progress)
        => progress.IrreversibleStep is null && progress.Status switch
        {
            DurableTaskStatus.Pending or DurableTaskStatus.Running or DurableTaskStatus.WaitingForDependency => !progress.EffectPending,
            DurableTaskStatus.NeedsAttention => true,
            _ => false,
        };

    /// <summary>Checks that a persisted state is one the lifecycle graph can produce.</summary>
    internal static bool IsConsistent(DurableTaskExecutionState state)
    {
        DurableTaskProgress progress = state.Progress;
        if (progress is null || state.Fence < 0 || progress.Attempts < 0 || !progress.EffectPending && progress.Attempts != 0
            || progress.Receipts.IsDefault || progress.Receipts.Any(string.IsNullOrWhiteSpace)
            || progress.IrreversibleStep is { } irreversible && (irreversible <= 0 || irreversible > progress.Checkpoint + 1
                || irreversible == progress.Checkpoint + 1 && !progress.EffectPending && progress.Status != DurableTaskStatus.Rejected))
        {
            return false;
        }

        bool leased = !string.IsNullOrWhiteSpace(state.Owner) && state.ExpiresAt is not null && state.Fence > 0;
        bool released = state.Owner is null && state.ExpiresAt is null;
        return progress.Status switch
        {
            DurableTaskStatus.Pending => released && state.Fence == 0 && progress.Checkpoint == 0
                && !progress.EffectPending && progress.IrreversibleStep is null,
            DurableTaskStatus.Running => leased,
            DurableTaskStatus.WaitingForDependency => leased && progress.EffectPending,
            DurableTaskStatus.NeedsAttention => released && state.Fence > 0,
            DurableTaskStatus.Succeeded or DurableTaskStatus.Rejected or DurableTaskStatus.Failed
                => released && state.Fence > 0 && !progress.EffectPending,
            DurableTaskStatus.Cancelled => released && progress.IrreversibleStep is null
                && (state.Fence > 0 || progress.Checkpoint == 0 && !progress.EffectPending),
            _ => false,
        };
    }
}
