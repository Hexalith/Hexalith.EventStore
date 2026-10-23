// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; base sections 10.5 and 15; amendment section 3.
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Owns one bounded operation lease and refuses authority after suspension, cancellation or disposal.</summary>
internal sealed class PayloadProtectionLease(TimeProvider clock, long started, TimeSpan duration, ulong epoch,
    Func<ValueTask> release) : IAsyncDisposable
{
    private int _released;
    /// <summary>Gets the strongly admitted lifecycle epoch.</summary>
    internal ulong Epoch { get; } = epoch;

    /// <summary>Checks the full monotonic lifetime before any returned key or plaintext is observed.</summary>
    internal void AssertCurrent(CancellationToken cancellationToken = default)
    {
        AssertDeadline(cancellationToken);
        if (Volatile.Read(ref _released) != 0)
        {
            throw new PayloadProtectionStateException(UnreadableProtectedDataReason.ProviderUnavailable);
        }
    }

    /// <summary>Checks a late release response without granting any authority to an already disposed lease.</summary>
    internal void AssertDeadline(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (clock.GetElapsedTime(started) >= duration) { throw new PayloadProtectionStateException(UnreadableProtectedDataReason.ProviderUnavailable); }
    }

    /// <summary>Releases an exact lease once; expiry alone never asserts the persistence outcome.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _released, 1) == 0) { await release().ConfigureAwait(false); }
    }
}
