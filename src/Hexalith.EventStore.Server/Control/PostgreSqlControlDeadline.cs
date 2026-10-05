using System.Diagnostics;

namespace Hexalith.EventStore.Server.Control;

/// <summary>One monotonic remaining budget shared by acquisition, transaction, rollback and reconciliation.</summary>
internal sealed class PostgreSqlControlDeadline
{
    private readonly long _started = Stopwatch.GetTimestamp();
    private readonly TimeSpan _budget;

    internal PostgreSqlControlDeadline(TimeSpan remainingRecoveryBudget)
    {
        if (remainingRecoveryBudget <= TimeSpan.Zero || remainingRecoveryBudget > TimeSpan.FromSeconds(30))
        {
            throw new ArgumentOutOfRangeException(nameof(remainingRecoveryBudget));
        }
        _budget = remainingRecoveryBudget;
    }

    internal static ReadOnlySpan<int> RetryDelaysMilliseconds => [0, 5, 10, 20, 40, 80, 160, 320];

    /// <summary>Returns a positive millisecond timeout no greater than the shared remaining budget.</summary>
    internal int RequireRemainingMilliseconds()
    {
        TimeSpan remaining = _budget - Stopwatch.GetElapsedTime(_started);
        if (remaining < TimeSpan.FromMilliseconds(1))
        {
            throw new TimeoutException("EvidenceHold: the shared PostgreSQL metadata recovery deadline is exhausted.");
        }
        return checked((int)Math.Floor(Math.Min(remaining.TotalMilliseconds, int.MaxValue)));
    }

    /// <summary>Links a caller token to the existing deadline without resetting it.</summary>
    internal CancellationTokenSource Link(CancellationToken cancellationToken)
    {
        int milliseconds = RequireRemainingMilliseconds();
        CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(milliseconds);
        return linked;
    }

    internal async Task DelayAttemptAsync(int attempt, CancellationToken cancellationToken)
    {
        if (attempt < 0 || attempt >= RetryDelaysMilliseconds.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(attempt));
        }
        using CancellationTokenSource linked = Link(cancellationToken);
        await Task.Delay(RetryDelaysMilliseconds[attempt], linked.Token).ConfigureAwait(false);
    }
}
