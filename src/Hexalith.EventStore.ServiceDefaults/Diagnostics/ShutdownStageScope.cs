using System.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.ServiceDefaults.Diagnostics;

/// <summary>Observes existing shutdown calls without owning or changing their execution.</summary>
internal sealed partial class ShutdownStageScope : IDisposable
{
    private const string EnableVariable = "HEXALITH_EVENTSTORE_SHUTDOWN_TRACE";
    private static long _nextSequence;
    private readonly ILogger _logger;
    private readonly ShutdownStage _stage;
    private readonly CancellationToken _observedCancellation;
    private readonly long _started;
    private readonly long _sequence;
    private int _ended;

    private ShutdownStageScope(ILogger logger, ShutdownStage stage, CancellationToken observedCancellation)
    {
        _logger = logger;
        _stage = stage;
        _observedCancellation = observedCancellation;
        _started = Stopwatch.GetTimestamp();
        _sequence = Interlocked.Increment(ref _nextSequence);
        Write("Started");
    }

    /// <summary>Starts only for an exact process opt-in and an enabled existing Debug logger.</summary>
    /// <remarks>The token is observed only; it is never registered, canceled, or passed to an operation.</remarks>
    internal static ShutdownStageScope? TryStart(ILogger logger, ShutdownStage stage, CancellationToken observedCancellation)
    {
        try
        {
            if (!string.Equals(Environment.GetEnvironmentVariable(EnableVariable), "true", StringComparison.Ordinal)
                || !Enum.IsDefined(stage) || !logger.IsEnabled(LogLevel.Debug)) return null;
            return new ShutdownStageScope(logger, stage, observedCancellation);
        }
        catch
        {
            // Advisory logging must not replace a shutdown result, even if a provider throws.
            return null;
        }
    }

    /// <summary>Records that the observed call returned, without asserting its business or provider success.</summary>
    internal void Complete()
    {
        if (Interlocked.Exchange(ref _ended, 1) == 0) Write("Returned");
    }

    /// <summary>Records an unfinished scope unwinding without inspecting its exception.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _ended, 1) == 0) Write("Unwound");
    }

    private void Write(string state)
    {
        try
        {
            int elapsedMs = (int)Math.Clamp(Stopwatch.GetElapsedTime(_started).TotalMilliseconds, 0, int.MaxValue);
            StageObserved(_logger, _stage.ToString(), state, elapsedMs, Environment.ProcessId, _sequence,
                _observedCancellation.CanBeCanceled, _observedCancellation.IsCancellationRequested);
        }
        catch
        {
            // Do not read exceptions, ambient activities, scopes, identifiers, or payloads.
        }
    }

    [LoggerMessage(EventId = 6210, Level = LogLevel.Debug,
        Message = "SDK shutdown observation: Stage={Stage}, State={State}, ElapsedMs={ElapsedMs}, ProcessId={ProcessId}, Sequence={Sequence}, ObservedCanBeCanceled={ObservedCanBeCanceled}, ObservedCanceled={ObservedCanceled}")]
    private static partial void StageObserved(ILogger logger, string stage, string state, int elapsedMs,
        int processId, long sequence, bool observedCanBeCanceled, bool observedCanceled);
}
