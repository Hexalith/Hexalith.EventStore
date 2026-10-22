using System.Buffers;

namespace Hexalith.EventStore.Client.Tests.Aggregates;

/// <summary>Models input whose complete backing capacity is unavailable at capture.</summary>
internal sealed class OpaqueReplayStateMemory : MemoryManager<byte>
{
    private readonly byte[] _bytes = [1, 2, 3];

    /// <inheritdoc/>
    public override Span<byte> GetSpan() => _bytes;

    /// <inheritdoc/>
    public override MemoryHandle Pin(int elementIndex = 0) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Unpin() { }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing) => Array.Clear(_bytes);
}
