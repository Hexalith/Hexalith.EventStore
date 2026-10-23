// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; base sections 10.5-10.7, 12 and 15.
using System.Security.Cryptography;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Owns one operation's mutable DEK or one closed failure, preserving denial and deletion classifications.</summary>
internal sealed class PayloadKeyResolution : IDisposable, IAsyncDisposable
{
    private byte[]? _bytes;
    private Action? _released;
    private Func<ValueTask>? _releasedAsync;
    private readonly Action<CancellationToken>? _assertCurrent;
    private readonly Func<CancellationToken, ValueTask>? _authenticated;

    private PayloadKeyResolution(byte[]? bytes, UnreadableProtectedDataReason? reason, Action? released,
        Action<CancellationToken>? assertCurrent = null, Func<CancellationToken, ValueTask>? authenticated = null,
        Func<ValueTask>? releasedAsync = null)
    {
        _bytes = bytes;
        Reason = reason;
        _released = released;
        _assertCurrent = assertCurrent;
        _authenticated = authenticated;
        _releasedAsync = releasedAsync;
    }

    /// <summary>Gets the closed unreadability reason; null denotes an owned complete key.</summary>
    internal UnreadableProtectedDataReason? Reason { get; }

    /// <summary>Gets whether successful plaintext transfer must await distributed lease release.</summary>
    internal bool RequiresAsyncRelease => _releasedAsync is not null;

    /// <summary>Takes ownership of an exact 32-byte DEK, clearing malformed buffers before rejecting them.</summary>
    internal static PayloadKeyResolution Success(byte[] bytes, Action? released = null,
        Action<CancellationToken>? assertCurrent = null, Func<CancellationToken, ValueTask>? authenticated = null,
        Func<ValueTask>? releasedAsync = null)
    {
        if (bytes is not { Length: 32 })
        {
            if (bytes is not null) { CryptographicOperations.ZeroMemory(bytes); }
            return new(null, UnreadableProtectedDataReason.ConsistencyMismatch, released, assertCurrent, authenticated, releasedAsync);
        }
        return new(bytes, null, released, assertCurrent, authenticated, releasedAsync);
    }

    /// <summary>Creates a bounded failure that can never carry key bytes or provider detail.</summary>
    internal static PayloadKeyResolution Failure(UnreadableProtectedDataReason reason)
        => new(null, Enum.IsDefined(reason) ? reason : UnreadableProtectedDataReason.ConsistencyMismatch, null);

    /// <summary>Adapts the pre-lifecycle test/interaction seam while preserving its transferred-buffer ownership.</summary>
    internal static async ValueTask<PayloadKeyResolution> FromLegacyAsync(
        Func<string, uint, CancellationToken, ValueTask<byte[]?>> resolver, string keyReference, uint version, CancellationToken cancellationToken)
    {
        byte[]? bytes = await resolver(keyReference, version, cancellationToken).ConfigureAwait(false);
        return bytes is null ? Failure(UnreadableProtectedDataReason.MissingKey) : new(bytes, null, null);
    }

    /// <summary>Transfers the array to the cryptographic operation; this lifetime must remain open until it clears the array.</summary>
    internal byte[] TakeBytes() => Interlocked.Exchange(ref _bytes, null) ?? throw new PayloadProtectionFormatException();

    /// <summary>Refuses a suspended, cancelled or expired operation before each cryptographic use.</summary>
    internal void AssertCurrent(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _assertCurrent?.Invoke(cancellationToken);
    }

    /// <summary>Confirms current authority after complete authentication and before returning any plaintext.</summary>
    internal async ValueTask ConfirmAuthenticatedAsync(CancellationToken cancellationToken)
    {
        AssertCurrent(cancellationToken);
        if (_authenticated is not null) { await _authenticated(cancellationToken).ConfigureAwait(false); }
        AssertCurrent(cancellationToken);
    }

    /// <summary>Clears untransferred bytes and releases local drain accounting exactly once.</summary>
    public void Dispose() => DisposeAsync().GetAwaiter().GetResult();

    /// <summary>Clears owned bytes, releases local-copy accounting and awaits exact distributed lease release.</summary>
    public async ValueTask DisposeAsync()
    {
        byte[]? bytes = Interlocked.Exchange(ref _bytes, null);
        try { if (bytes is not null) { CryptographicOperations.ZeroMemory(bytes); } }
        finally
        {
            try { Interlocked.Exchange(ref _released, null)?.Invoke(); }
            finally
            {
                Func<ValueTask>? release = Interlocked.Exchange(ref _releasedAsync, null);
                if (release is not null) { await release().ConfigureAwait(false); }
            }
        }
    }

    /// <summary>Never formats cryptographic material or identities.</summary>
    public override string ToString() => nameof(PayloadKeyResolution);
}
