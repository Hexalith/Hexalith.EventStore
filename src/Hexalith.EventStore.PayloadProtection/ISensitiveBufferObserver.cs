// Normative authority: de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e; sections 8, 14, and 15.
namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Observes already-cleared engine buffers in tests without changing production ownership semantics.
/// </summary>
internal interface ISensitiveBufferObserver
{
    /// <summary>
    /// Observes a buffer only after <see cref="System.Security.Cryptography.CryptographicOperations.ZeroMemory(Span{byte})"/> completed.
    /// </summary>
    void BufferCleared(SensitiveBufferKind kind, ReadOnlySpan<byte> buffer);
}
