namespace Hexalith.EventStore.DomainService;

/// <summary>Bounds workers used to isolate synchronous blocking in read-only adapters.</summary>
internal static class ProcessReadDeadline
{
    private static readonly SemaphoreSlim AdapterSlots = new(16, 16);
    private static readonly SemaphoreSlim ControlSlots = new(8, 8);
    private static readonly SemaphoreSlim ConvergenceSlots = new(8, 8);
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(object Scope, string ActorId), byte> Converging = new();

    internal static async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> operation, TimeSpan timeout, CancellationToken cancellationToken)
        => await RunAsync(AdapterSlots, operation, timeout, cancellationToken).ConfigureAwait(false);

    /// <summary>Isolates control-state reads from exhausted domain-adapter workers.</summary>
    internal static async Task<T> RunControlAsync<T>(Func<CancellationToken, Task<T>> operation, TimeSpan timeout, CancellationToken cancellationToken)
        => await RunAsync(ControlSlots, operation, timeout, cancellationToken).ConfigureAwait(false);

    /// <summary>Bounds actor convergence separately so abandoned turns cannot exhaust control-state service.</summary>
    internal static async Task<T> RunConvergenceAsync<T>(object scope, string actorId, Func<CancellationToken, Task<T>> operation, TimeSpan timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Converging.TryAdd((scope, actorId), 0)) throw new ProcessReadAdmissionException();
        return await RunAsync(ConvergenceSlots, operation, timeout, cancellationToken,
            () => Converging.TryRemove((scope, actorId), out _)).ConfigureAwait(false);
    }

    private static async Task<T> RunAsync<T>(SemaphoreSlim slots, Func<CancellationToken, Task<T>> operation, TimeSpan timeout,
        CancellationToken cancellationToken, Action? completed = null)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await slots.WaitAsync(0, cancellationToken).ConfigureAwait(false))
                throw new ProcessReadAdmissionException();
        }
        catch
        {
            completed?.Invoke();
            throw;
        }
        var work = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        work.CancelAfter(timeout);
        Task<T> task = Task.Run(async () =>
        {
            try
            {
                return await operation(work.Token).ConfigureAwait(false);
            }
            finally
            {
                slots.Release();
                completed?.Invoke();
                work.Dispose();
            }
        }, CancellationToken.None);
        try
        {
            return await task.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // A worker can fault after its caller's deadline. Observe that fault
            // without releasing its slot before the adapter actually finishes.
            _ = task.ContinueWith(static completed => _ = completed.Exception,
                CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            if (!task.IsCompleted)
            {
                try
                {
                    work.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // The worker finished and disposed its cancellation source concurrently.
                }
            }
            throw;
        }
    }
}
