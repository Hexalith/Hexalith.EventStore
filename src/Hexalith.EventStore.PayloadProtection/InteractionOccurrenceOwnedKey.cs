using System.Security.Cryptography;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Private owned key buffer; exact target/purpose/version are independently resolved and all bytes are cleared on disposal.</summary>
internal sealed class InteractionOccurrenceOwnedKey(ProtectionTarget target, string purpose, string version, byte[] bytes) : IDisposable
{
    /// <summary>Gets the independently authenticated complete target.</summary>
    internal ProtectionTarget Target { get; } = target;
    /// <summary>Gets the exact root or tenant-digest purpose.</summary>
    internal string Purpose { get; } = purpose;
    /// <summary>Gets the retained version.</summary>
    internal string Version { get; } = version;
    /// <summary>Gets the transferred mutable 32-byte buffer.</summary>
    internal byte[] Bytes { get; } = bytes;
    /// <inheritdoc/>
    public void Dispose() => CryptographicOperations.ZeroMemory(Bytes);
    /// <inheritdoc/>
    public override string ToString() => nameof(InteractionOccurrenceOwnedKey);
}
