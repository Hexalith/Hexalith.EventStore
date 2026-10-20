namespace Hexalith.EventStore.Client.Projections;

public sealed partial class SharedProjectionEpochCoordinator
{
    private readonly object _promotedCleanupGate = new();
    private readonly Dictionary<string, Task> _promotedCleanupTasks = new(StringComparer.Ordinal);

    private void SchedulePromotedCleanup(SharedProjectionScope scope)
    {
        string hash = scope.ComputeHash();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_promotedCleanupGate)
        {
            if (_promotedCleanupTasks.ContainsKey(hash)) return;
            _promotedCleanupTasks.Add(hash, completion.Task);
        }
        _ = Task.Run(async () =>
        {
            try
            {
                await ReconcileChunkCleanupAsync(scope, CancellationToken.None).ConfigureAwait(false);
                await ReconcileStreamMetadataCleanupAsync(scope, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Durable cleanup intent remains available to the next foreground reconciliation.
            }
            finally
            {
                lock (_promotedCleanupGate) _promotedCleanupTasks.Remove(hash);
                completion.TrySetResult();
            }
        });
    }

    /// <summary>Drains this coordinator's scheduled cleanup before a foreground phase advance.</summary>
    /// <param name="scope">The exact owning shared scope.</param>
    /// <param name="cancellationToken">Cancellation of this wait, without cancelling durable cleanup.</param>
    /// <returns>The already scheduled work, or a completed task when no cleanup is active.</returns>
    internal Task WaitForPromotedCleanupAsync(SharedProjectionScope scope, CancellationToken cancellationToken = default)
    {
        Task pending;
        lock (_promotedCleanupGate)
            pending = _promotedCleanupTasks.GetValueOrDefault(scope.ComputeHash()) ?? Task.CompletedTask;
        return pending.WaitAsync(cancellationToken);
    }
}
