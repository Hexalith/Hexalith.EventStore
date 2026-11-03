namespace Hexalith.EventStore.SignalR;

/// <summary>Owns callback entry, revocation and completion, including captured dispatch snapshots.</summary>
/// <typeparam name="T">The signal payload.</typeparam>
/// <param name="original">The caller's callback identity.</param>
/// <param name="invoke">The callback invocation.</param>
internal sealed class ProjectionCallback<T>(Delegate original, Action<T> invoke) : IProjectionCallback
{
    [ThreadStatic]
    private static Stack<ProjectionCallback<T>>? _executing;

    private readonly object _gate = new();
    private readonly TaskCompletionSource _drained = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _revoked;
    private int _active;

    /// <summary>Gets whether removal is occurring inside this synchronous callback.</summary>
    public bool IsExecuting => _executing?.Contains(this) == true;

    /// <inheritdoc/>
    public Task Completion => _drained.Task;

    /// <summary>Gets the original callback identity used for removal.</summary>
    public Delegate Original { get; } = original;

    /// <summary>Enters atomically with respect to revocation, and accounts for completion even on failure.</summary>
    public void Invoke(T value)
    {
        lock (_gate)
        {
            if (_revoked) { return; }
            _active++;
        }
        (_executing ??= new()).Push(this);
        try { invoke(value); }
        finally
        {
            _executing.Pop();
            lock (_gate)
            {
                _active--;
                if (_revoked && _active == 0) { _drained.TrySetResult(); }
            }
        }
    }

    /// <summary>Revokes future entry and drains entered calls. Self-removal cannot await its own stack frame.</summary>
    public Task RevokeAndDrainAsync()
    {
        lock (_gate)
        {
            _revoked = true;
            if (_active == 0) { _drained.TrySetResult(); }
            return _executing?.Contains(this) == true ? Task.CompletedTask : _drained.Task;
        }
    }
}
