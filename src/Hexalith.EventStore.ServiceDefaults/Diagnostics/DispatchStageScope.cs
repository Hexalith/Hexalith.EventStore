using System.Diagnostics;

namespace Hexalith.EventStore.ServiceDefaults.Diagnostics;

/// <summary>Pairs an await start with its actual successful completion or unwind, without observing exception content.</summary>
internal sealed class DispatchStageScope : IDisposable
{
    private readonly DispatchDiagnostics _diagnostics;
    private readonly DispatchStageName _stage;
    private readonly CancellationToken _operationCancellation;
    private readonly long _started = Stopwatch.GetTimestamp();
    private bool _completed;

    /// <summary>Records the start before production code invokes the awaited operation.</summary>
    internal DispatchStageScope(DispatchDiagnostics diagnostics, DispatchStageName stage, CancellationToken operationCancellation)
    {
        _diagnostics = diagnostics;
        _stage = stage;
        _operationCancellation = operationCancellation;
        _diagnostics.Write(stage, null, 0, operationCancellation);
    }

    /// <summary>Marks success only after the original await returns.</summary>
    internal void Complete()
    {
        _completed = true;
        Write(true);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_completed) Write(false);
    }

    private void Write(bool completed)
        => _diagnostics.Write(_stage, completed,
            (long)Math.Clamp(Stopwatch.GetElapsedTime(_started).TotalMilliseconds, 0, int.MaxValue), _operationCancellation);
}
