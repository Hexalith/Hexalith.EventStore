using System.Security.Cryptography;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Owns exact logical claim/signature bytes and their composed charge until disposal.</summary>
internal sealed class DaprLogicalSignedClaim : IDisposable
{
    private byte[]? _claim;
    private byte[]? _signature;
    private EventBufferReservation? _reservation;

    /// <summary>Takes ownership of already admitted private bytes.</summary>
    internal DaprLogicalSignedClaim(byte[] claim, string keyId, byte[] signature, EventBufferReservation reservation) { _claim = claim; KeyId = keyId; _signature = signature; _reservation = reservation; }

    /// <summary>Gets exact private claim bytes; caller-created carriers still require verification.</summary>
    internal ReadOnlyMemory<byte> Claim => _claim ?? throw new ObjectDisposedException(nameof(DaprLogicalSignedClaim));

    /// <summary>Gets the exact current logical-model key identifier.</summary>
    internal string KeyId { get; }

    /// <summary>Gets the fixed P1363 signature.</summary>
    internal ReadOnlyMemory<byte> Signature => _signature ?? throw new ObjectDisposedException(nameof(DaprLogicalSignedClaim));

    /// <inheritdoc/>
    public void Dispose()
    {
        byte[]? claim = Interlocked.Exchange(ref _claim, null); byte[]? signature = Interlocked.Exchange(ref _signature, null);
        if (claim is not null) { CryptographicOperations.ZeroMemory(claim); }
        if (signature is not null) { CryptographicOperations.ZeroMemory(signature); }
        Interlocked.Exchange(ref _reservation, null)?.Dispose();
    }
}
