using System.Security.Cryptography;

namespace Hexalith.EventStore.Contracts.Events;

/// <summary>Owns one detached raw-page buffer until its page releases and clears it.</summary>
internal sealed class OwnedRawEventPayload : IReadOnlyPayload, IDisposable
{
    private readonly object _lifetimeLock = new();
    private byte[]? _bytes;

    /// <summary>Copies the measured source once, clearing any partial copy on failure.</summary>
    internal OwnedRawEventPayload(IReadOnlyPayload source, int length)
    {
        byte[] bytes = new byte[length];
        try
        {
            source.CopyTo(0, bytes);
            if (source.Length != length)
            {
                throw new ArgumentException("An event evidence payload changed length while being copied.", nameof(source));
            }

            _bytes = bytes;
            Length = length;
        }
        catch
        {
            CryptographicOperations.ZeroMemory(bytes);
            throw;
        }
    }

    /// <inheritdoc/>
    public int Length { get; }

    /// <inheritdoc/>
    public void CopyTo(int sourceOffset, Span<byte> destination)
    {
        lock (_lifetimeLock)
        {
            ObjectDisposedException.ThrowIf(_bytes is null, this);
            if (sourceOffset < 0 || sourceOffset > Length || destination.Length > Length - sourceOffset)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceOffset));
            }

            _bytes.AsSpan(sourceOffset, destination.Length).CopyTo(destination);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_lifetimeLock)
        {
            if (_bytes is not null)
            {
                CryptographicOperations.ZeroMemory(_bytes);
                _bytes = null;
            }
        }
    }
}
