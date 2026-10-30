namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Expires the enclosing test deadline exactly when the cancellation-ignoring mutation is submitted, independent of serializer/JIT scheduling.</summary>
internal sealed class SubmittedMutationDeadlineClock : TimeProvider, ITimer
{
    private TimerCallback? _callback;
    private object? _state;
    private long _ticks;
    private TimeSpan _due;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() => _ticks;
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    { _callback = callback; _state = state; _due = dueTime; return this; }
    internal void Expire()
    {
        _ticks = _due.Ticks;
        _callback!(_state);
    }
    public bool Change(TimeSpan dueTime, TimeSpan period) { _due = dueTime; return true; }
    public void Dispose() { _callback = null; _state = null; }
    public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
}
