// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base sections 8, 9 and 15.
using System.Buffers;
using System.Security.Cryptography;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Bounds UTF-8 writer allocation requests and clears every replaced or disposed engine-owned output buffer.</summary>
internal sealed class PayloadSerializationBuffer(ISensitiveBufferObserver? observer = null, CancellationToken cancellationToken = default) : IBufferWriter<byte>, IDisposable
{
    // Utf8JsonWriter requests worst-case UTF-8/escaping spans, which can exceed the
    // actual JSON size. Bound staging separately so legal exact-size output still fits.
    internal const int MaximumAllocationBytes = PayloadProtectionLimits.PayloadBytes * 6 + 4096;
    private byte[] _buffer = [];
    private int _written;
    /// <summary>Returns a bounded owned copy of the completed serialization.</summary>
    internal byte[] ToArray() => _buffer.AsSpan(0, _written).ToArray();
    /// <inheritdoc/>
    public void Advance(int count)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (count < 0 || count > _buffer.Length - _written || count > PayloadProtectionLimits.PayloadBytes - _written) { throw new PayloadProtectionFormatException(); }
        _written = checked(_written + count);
    }
    /// <inheritdoc/>
    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int requested = Math.Max(1, sizeHint);
        if (_written >= PayloadProtectionLimits.PayloadBytes || requested > MaximumAllocationBytes - _written) { throw new PayloadProtectionFormatException(); }
        int required = checked(_written + requested);
        if (required > _buffer.Length)
        {
            int ceiling = required <= PayloadProtectionLimits.PayloadBytes ? PayloadProtectionLimits.PayloadBytes : MaximumAllocationBytes;
            int capacity = checked((int)Math.Min(ceiling, Math.Max(required, Math.Max(256L, _buffer.Length * 2L))));
            byte[] replacement = new byte[capacity]; _buffer.AsSpan(0, _written).CopyTo(replacement);
            Clear(); _buffer = replacement;
        }
        return _buffer.AsMemory(_written);
    }
    /// <inheritdoc/>
    public Span<byte> GetSpan(int sizeHint = 0) => GetMemory(sizeHint).Span;
    /// <inheritdoc/>
    public void Dispose() { Clear(); _buffer = []; _written = 0; }
    private void Clear()
    {
        CryptographicOperations.ZeroMemory(_buffer); observer?.BufferCleared(SensitiveBufferKind.InputSnapshot, _buffer);
    }
}
