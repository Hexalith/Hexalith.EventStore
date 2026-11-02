namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Records timer arming and fires only when requested by a test.</summary>
internal sealed class CacheManualTimer(TimerCallback callback, object? state) : ITimer
{
    /// <summary>Gets the most recent due time.</summary>
    internal TimeSpan DueTime { get; private set; } = Timeout.InfiniteTimeSpan;

    /// <summary>Sets the next due time.</summary>
    public bool Change(TimeSpan dueTime, TimeSpan period)
    {
        DueTime = dueTime;
        return true;
    }

    /// <summary>Invokes the callback once.</summary>
    internal void Fire()
    {
        DueTime = Timeout.InfiniteTimeSpan;
        callback(state);
    }

    /// <inheritdoc/>
    public void Dispose() => DueTime = Timeout.InfiniteTimeSpan;

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
