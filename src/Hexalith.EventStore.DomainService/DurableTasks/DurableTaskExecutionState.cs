namespace Hexalith.EventStore.DomainService.DurableTasks;

/// <summary>The execution state retained on the admission row.</summary>
/// <param name="Fence">The monotonically increasing lease generation.</param>
/// <param name="Owner">The identity of the worker lease, when held.</param>
/// <param name="ExpiresAt">The exclusive expiry of the worker lease.</param>
/// <param name="Progress">The checkpoint and pending effect state.</param>
internal sealed record DurableTaskExecutionState(long Fence, string? Owner, DateTimeOffset? ExpiresAt, DurableTaskProgress Progress)
{
    /// <summary>Progress of an admitted task before any lease.</summary>
    internal static DurableTaskProgress InitialProgress { get; } = new([], false, false);

    /// <summary>Execution state of an admitted task before any lease.</summary>
    internal static DurableTaskExecutionState Initial { get; } = new(0, null, null, InitialProgress);
}
