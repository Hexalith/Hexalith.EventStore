using System.Diagnostics;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Waits only for Redis's exact transient SAVE conflict under one original command budget.</summary>
internal static class QualificationRedisSavePolicy
{
    /// <summary>Gets the original finite SAVE budget, shared by every actual attempt and retry delay.</summary>
    internal static TimeSpan Budget { get; } = TimeSpan.FromMinutes(3);

    /// <summary>Gets the sole transient reply that can precede an actual acknowledgement.</summary>
    internal const string BusyReply = "ERR Background save already in progress";

    /// <summary>Requires an actual final OK; operation and monotonic clock seams permit deterministic controls without a backend.</summary>
    internal static async Task RequireAcknowledgementAsync(Func<CancellationToken, Task<CommandObservation>> execute,
        CancellationToken cancellationToken = default, Func<TimeSpan>? elapsed = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        long started = Stopwatch.GetTimestamp();
        elapsed ??= () => Stopwatch.GetElapsedTime(started);
        delay ??= Task.Delay;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(Budget);
        void CheckDeadline()
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (deadline.IsCancellationRequested || elapsed() >= Budget)
                throw new TimeoutException("The owned Redis SAVE exceeded its original three-minute deadline.");
        }
        try
        {
            while (true)
            {
                CheckDeadline();
                CommandObservation result = await execute(deadline.Token);
                CheckDeadline();
                if (result.ExitCode != 0 || result.OutputTruncated || result.Interruption is not null
                    || !string.IsNullOrWhiteSpace(result.StandardError))
                    throw new InvalidOperationException("The owned Redis did not acknowledge its actual SAVE.");
                string reply = result.StandardOutput.Trim();
                if (reply == "OK") return;
                if (reply != BusyReply)
                    throw new InvalidOperationException("The owned Redis did not acknowledge its actual SAVE.");
                TimeSpan remaining = Budget - elapsed();
                CheckDeadline();
                await delay(remaining < TimeSpan.FromMilliseconds(100) ? remaining : TimeSpan.FromMilliseconds(100), deadline.Token);
            }
        }
        catch (OperationCanceledException error) when (!cancellationToken.IsCancellationRequested && deadline.IsCancellationRequested)
        {
            throw new TimeoutException("The owned Redis SAVE exceeded its original three-minute deadline.", error);
        }
    }
}
