namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>An exact bound key, independent trigger marker and explicitly broken one-key gate.</summary>
/// <param name="Key">The full provider key.</param>
/// <param name="Marker">The exact trigger NOTICE.</param>
/// <param name="Boundary">The selected implicit transaction boundary.</param>
internal sealed class PostgresOneKeyFault(string Key, string Marker, PostgresOneKeyBoundary Boundary)
{
    private readonly TaskCompletionSource _reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _break = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _executions;
    private int _forwarded;
    private int _notices;
    private int _commands;
    private int _idle;
    private int _gates;

    /// <summary>Gets the exact provider key.</summary>
    internal string Key { get; } = Key;
    /// <summary>Gets the exact independent marker.</summary>
    internal string Marker { get; } = Marker;
    /// <summary>Gets the required boundary.</summary>
    internal PostgresOneKeyBoundary Boundary { get; } = Boundary;
    /// <summary>Gets every selected execution, including retries.</summary>
    internal int Executions => Volatile.Read(ref _executions);
    /// <summary>Gets complete forwarded execution frames.</summary>
    internal int Forwarded => Volatile.Read(ref _forwarded);
    /// <summary>Gets exact trigger observations.</summary>
    internal int Notices => Volatile.Read(ref _notices);
    /// <summary>Gets successful write command completions.</summary>
    internal int Commands => Volatile.Read(ref _commands);
    /// <summary>Gets successful idle completions.</summary>
    internal int Idle => Volatile.Read(ref _idle);
    /// <summary>Gets every gate invocation, including retries.</summary>
    internal int Gates => Volatile.Read(ref _gates);
    /// <summary>Gets whether a real boundary was observed.</summary>
    internal bool Reached => _reached.Task.IsCompletedSuccessfully;
    /// <summary>Waits without manufacturing readiness on cancellation.</summary>
    internal Task WaitAsync(CancellationToken token) => _reached.Task.WaitAsync(token);
    /// <summary>Records an execution-capable selected frame.</summary>
    internal void ObserveExecution() => Interlocked.Increment(ref _executions);
    /// <summary>Records a complete forwarded frame.</summary>
    internal void ObserveForwarded() => Interlocked.Increment(ref _forwarded);
    /// <summary>Records the independent trigger.</summary>
    internal void ObserveNotice() => Interlocked.Increment(ref _notices);
    /// <summary>Records a successful command.</summary>
    internal void ObserveCommand() => Interlocked.Increment(ref _commands);
    /// <summary>Records successful idle completion.</summary>
    internal void ObserveIdle() => Interlocked.Increment(ref _idle);
    /// <summary>Breaks only a boundary already observed on the wire.</summary>
    internal void BreakConnection()
    {
        if (!Reached) throw new InvalidOperationException("No one-key provider boundary was observed.");
        _break.TrySetResult();
    }
    /// <summary>Holds the first observation, retaining later retries in the ledger.</summary>
    internal async Task GateAsync(CancellationToken token)
    {
        if (Interlocked.Increment(ref _gates) != 1) return;
        _reached.TrySetResult();
        await _break.Task.WaitAsync(token);
        throw new PostgresFaultDisconnectException();
    }
}
