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
