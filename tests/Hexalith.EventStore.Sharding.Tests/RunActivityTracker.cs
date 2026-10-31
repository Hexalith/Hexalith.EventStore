namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>Tracks actual asynchronous operations and fences additions before teardown drains them.</summary>
internal sealed class RunActivityTracker
{
    private readonly object _gate = new();
    private readonly HashSet<Task> _tasks = [];
    private bool _fenced;

    /// <summary>Starts and retains the actual task while accepting work.</summary>
    /// <typeparam name="T">The operation result.</typeparam>
    /// <param name="start">The underlying asynchronous operation.</param>
    /// <returns>The actual task, with no detached cancellation wrapper.</returns>
    internal Task<T> Start<T>(Func<Task<T>> start)
    {
        lock (_gate)
        {
            if (_fenced) throw new InvalidOperationException("The run no longer accepts operations.");
            Task<T> task = start();
            _tasks.Add(task);
            return task;
        }
    }

    /// <summary>Fences new operations and captures every outstanding actual task.</summary>
    /// <returns>The tasks teardown must await before stopping owned resources.</returns>
    internal Task[] Fence()
    {
        lock (_gate)
        {
            _fenced = true;
            return _tasks.ToArray();
        }
    }
}
