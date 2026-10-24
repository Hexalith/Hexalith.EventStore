// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; amendment section 3.
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Enforces the full lease maximum even when a system timer fires slightly before the requested monotonic duration.</summary>
internal static class PayloadMonotonicDrain
{
    /// <summary>Waits a fresh sixty seconds after confirmed durable authority; injected short waits fail qualification.</summary>
    internal static async ValueTask WaitAsync(TimeProvider clock, Func<TimeSpan, CancellationToken, ValueTask>? delay, CancellationToken token)
    {
        long started = clock.GetTimestamp();
        TimeSpan duration = TimeSpan.FromSeconds(60);
        if (delay is not null)
        {
            await delay(duration, token).ConfigureAwait(false);
            if (clock.GetElapsedTime(started) < duration) { throw new PayloadProtectionStateException(UnreadableProtectedDataReason.ProviderUnavailable); }
            return;
        }
        while (clock.GetElapsedTime(started) < duration)
        {
            TimeSpan remaining = duration - clock.GetElapsedTime(started);
            if (remaining > TimeSpan.Zero) { await Task.Delay(remaining, clock, token).ConfigureAwait(false); }
        }
        token.ThrowIfCancellationRequested();
    }
}
