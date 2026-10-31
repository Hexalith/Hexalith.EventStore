using System.Globalization;

namespace Hexalith.EventStore.Sharding.TestHost;

/// <summary>Fences new writes and cancels state operations at the exclusive run expiry.</summary>
public sealed class RunFence : IDisposable
{
    private readonly CancellationTokenSource _deadline = new();
    private readonly DateTimeOffset _expiry;

    /// <summary>Initializes a fence from a validated binding.</summary>
    /// <param name="binding">The validated binding.</param>
    public RunFence(RunBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        _expiry = DateTimeOffset.ParseExact(binding.ExpiresUtc, "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        TimeSpan remaining = _expiry - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("The run binding has expired.");
        }

        _deadline.CancelAfter(remaining);
    }

    /// <summary>Gets whether new writes are fenced.</summary>
    public bool IsExpired => DateTimeOffset.UtcNow >= _expiry || _deadline.IsCancellationRequested;

    /// <summary>Gets the state-operation deadline token.</summary>
    public CancellationToken Token => _deadline.Token;

    /// <inheritdoc/>
    public void Dispose() => _deadline.Dispose();
}
