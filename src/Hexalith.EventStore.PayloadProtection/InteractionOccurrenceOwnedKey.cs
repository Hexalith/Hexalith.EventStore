using System.Security.Cryptography;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Private owned key buffer; exact target/purpose/version are independently resolved and all bytes are cleared on disposal.</summary>
public sealed class InteractionOccurrenceOwnedKey(ProtectionTarget target, string purpose, string version, byte[] bytes) : IDisposable
{
    private int _disposed;
    /// <summary>Gets whether the consumer has completed its entire key-use scope. Zeroed root bytes alone do not establish disposal of derived material.</summary>
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;
    /// <summary>Gets the independently authenticated complete target.</summary>
    public ProtectionTarget Target { get; } = target;
    /// <summary>Gets the exact root or tenant-digest purpose.</summary>
    public string Purpose { get; } = purpose;
    /// <summary>Gets the retained version.</summary>
    public string Version { get; } = version;
    /// <summary>Gets the transferred mutable 32-byte buffer.</summary>
    public byte[] Bytes { get; } = bytes;
    /// <inheritdoc/>
    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(Bytes);
        Interlocked.Exchange(ref _disposed, 1);
    }
    /// <inheritdoc/>
    public override string ToString() => nameof(InteractionOccurrenceOwnedKey);
}
