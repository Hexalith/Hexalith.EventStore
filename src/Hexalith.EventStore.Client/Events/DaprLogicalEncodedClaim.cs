using System.Security.Cryptography;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Retains an encoded logical claim's private capacity through its signing lifetime.</summary>
internal sealed class DaprLogicalEncodedClaim : IDisposable
{
    private byte[]? _bytes;
    private EventBufferReservation? _charge;

    /// <summary>Encodes only after admission of both writer and detached output capacity.</summary>
    internal DaprLogicalEncodedClaim(Func<byte[]> encode, EventBufferBudget budget, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); _charge = budget.Reserve(2 * 1024 * 1024 + 256);
        try { _bytes = encode(); token.ThrowIfCancellationRequested(); _charge.ShrinkTo(_bytes.Length + 256); }
        catch { Dispose(); throw; }
    }

    /// <summary>Gets the exact private image during its charged lifetime.</summary>
    internal ReadOnlyMemory<byte> Bytes => _bytes ?? throw new ObjectDisposedException(nameof(DaprLogicalEncodedClaim));

    /// <inheritdoc/>
    public void Dispose()
    {
        byte[]? bytes = Interlocked.Exchange(ref _bytes, null); if (bytes is not null) { CryptographicOperations.ZeroMemory(bytes); }
        Interlocked.Exchange(ref _charge, null)?.Dispose();
    }
}
