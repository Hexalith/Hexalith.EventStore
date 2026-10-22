using System.Diagnostics;

namespace Hexalith.EventStore.ProviderVerification;

internal sealed class ProviderVerificationTimeline
{
    private const long MaximumDurationMilliseconds = 24L * 60L * 60L * 1000L;

    private readonly DateTimeOffset _runStartedAt = DateTimeOffset.UtcNow;
    private readonly Stopwatch _runStopwatch = Stopwatch.StartNew();
    private DateTimeOffset? _cleanupCompletedAt;
    private long _cleanupDurationMilliseconds;
    private string _cleanupResultCode = "cleanup.not-run";
    private DateTimeOffset? _cleanupStartedAt;
    private Stopwatch? _cleanupStopwatch;
    private DateTimeOffset? _readinessCompletedAt;
    private long _readinessDurationMilliseconds;
    private string _readinessResultCode = "readiness.not-run";
    private DateTimeOffset? _readinessStartedAt;
    private Stopwatch? _readinessStopwatch;
    private DateTimeOffset? _startupCompletedAt;
    private long _startupDurationMilliseconds;
    private string _startupResultCode = "startup.not-run";
    private DateTimeOffset? _startupStartedAt;
    private Stopwatch? _startupStopwatch;

    public bool CleanupStarted => _cleanupStartedAt.HasValue;

    public bool HostBound { get; private set; }

    public bool HostStopped { get; private set; }

    public bool PortClosed { get; private set; }

    public string CleanupResultCode => _cleanupResultCode;

    public string ReadinessResultCode => _readinessResultCode;

    public string StartupResultCode => _startupResultCode;

    public void MarkHostBound()
        => HostBound = true;

    public void MarkHostCleanup(bool hostStopped, bool portClosed)
    {
        HostStopped = hostStopped;
        PortClosed = portClosed;
    }

    public void BeginStartup()
    {
        if (_startupStartedAt.HasValue)
        {
            return;
        }

        _startupStartedAt = DateTimeOffset.UtcNow;
        _startupStopwatch = Stopwatch.StartNew();
    }

    public void CompleteStartup(string resultCode)
    {
        if (!_startupStartedAt.HasValue || _startupCompletedAt.HasValue)
        {
            return;
        }

        _startupCompletedAt = DateTimeOffset.UtcNow;
        _startupDurationMilliseconds = BoundedElapsed(_startupStopwatch);
        _startupResultCode = resultCode;
    }

    public void BeginReadiness()
    {
        if (_readinessStartedAt.HasValue)
        {
            return;
        }

        _readinessStartedAt = DateTimeOffset.UtcNow;
        _readinessStopwatch = Stopwatch.StartNew();
    }

    public void CompleteReadiness(string resultCode)
    {
        if (!_readinessStartedAt.HasValue || _readinessCompletedAt.HasValue)
        {
            return;
        }

        _readinessCompletedAt = DateTimeOffset.UtcNow;
        _readinessDurationMilliseconds = BoundedElapsed(_readinessStopwatch);
        _readinessResultCode = resultCode;
    }

    public void BeginCleanup()
    {
        if (_cleanupStartedAt.HasValue)
        {
            return;
        }

        _cleanupStartedAt = DateTimeOffset.UtcNow;
        _cleanupStopwatch = Stopwatch.StartNew();
    }

    public void CompleteCleanup(string resultCode)
    {
        if (!_cleanupStartedAt.HasValue || _cleanupCompletedAt.HasValue)
        {
            return;
        }

        _cleanupCompletedAt = DateTimeOffset.UtcNow;
        _cleanupDurationMilliseconds = BoundedElapsed(_cleanupStopwatch);
        _cleanupResultCode = resultCode;
    }

    public void CompletePendingFailures()
    {
        CompleteStartup("startup.failed");
        CompleteReadiness("readiness.failed");
    }

    public ProviderVerificationTiming CompleteRun(string resultCode)
    {
        DateTimeOffset completedAt = DateTimeOffset.UtcNow;
        return new ProviderVerificationTiming(
            new VerificationPhaseTiming(
                resultCode,
                _runStartedAt,
                completedAt,
                BoundedElapsed(_runStopwatch)),
            new VerificationPhaseTiming(
                _startupResultCode,
                _startupStartedAt,
                _startupCompletedAt,
                _startupDurationMilliseconds),
            new VerificationPhaseTiming(
                _readinessResultCode,
                _readinessStartedAt,
                _readinessCompletedAt,
                _readinessDurationMilliseconds),
            new VerificationPhaseTiming(
                _cleanupResultCode,
                _cleanupStartedAt,
                _cleanupCompletedAt,
                _cleanupDurationMilliseconds));
    }

    private static long BoundedElapsed(Stopwatch? stopwatch)
        => Math.Clamp(stopwatch?.ElapsedMilliseconds ?? 0, 0, MaximumDurationMilliseconds);
}
