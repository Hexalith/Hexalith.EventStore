// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; amendment section 3.
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
