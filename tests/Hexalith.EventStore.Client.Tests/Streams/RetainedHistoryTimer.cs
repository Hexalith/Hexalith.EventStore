namespace Hexalith.EventStore.Client.Tests.Streams;

/// <summary>Owned manual timer implementing the time-provider deadline lifecycle.</summary>
/// <param name="clock">The manually advanced observation clock.</param>
/// <param name="callback">The SDK cancellation callback.</param>
/// <param name="state">The callback state supplied by the SDK.</param>
internal sealed class RetainedHistoryTimer(TimeProvider clock, TimerCallback callback, object? state) : ITimer
{
    private readonly object _sync = new();
    private DateTimeOffset? _dueAt;
    private TimeSpan _period;
    private bool _disposed;

    /// <inheritdoc/>
    public bool Change(TimeSpan dueTime, TimeSpan period)
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return false;
            }

            _dueAt = dueTime == Timeout.InfiniteTimeSpan ? null : clock.GetUtcNow() + dueTime;
            _period = period;
            return true;
        }
    }

    /// <summary>Invokes the due callback outside the timer lock.</summary>
    public void FireIfDue(DateTimeOffset now)
    {
        lock (_sync)
        {
            if (_disposed || _dueAt is null || _dueAt > now)
            {
                return;
            }

            _dueAt = _period == Timeout.InfiniteTimeSpan || _period == TimeSpan.Zero ? null : now + _period;
        }

        callback(state);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_sync)
        {
            _disposed = true;
            _dueAt = null;
        }
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
