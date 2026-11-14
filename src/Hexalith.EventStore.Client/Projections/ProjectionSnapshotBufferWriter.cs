using System.Buffers;

namespace Hexalith.EventStore.Client.Projections;

/// <summary>Bounds both actual JSON bytes and the writer's temporary span requests.</summary>
internal sealed class ProjectionSnapshotBufferWriter : IBufferWriter<byte>
{
    private readonly int _maximum;
    private readonly byte[] _buffer;
    private int _written;

    /// <summary>Initializes a buffer with bounded room for the writer's worst-case escaping estimate.</summary>
    /// <param name="maximum">The maximum actual encoded byte count.</param>
    internal ProjectionSnapshotBufferWriter(int maximum)
    {
        _maximum = maximum;
        // Utf8JsonWriter can reserve six bytes per source character before escaping it.
        // A valid payload fits that reservation even close to the exact encoded-byte limit.
        _buffer = new byte[checked((maximum * 6) + 4096)];
    }

    /// <inheritdoc/>
    public void Advance(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (count > _maximum - _written)
        {
            throw new ProjectionSnapshotLimitExceededException("serialized-bytes", (long)_written + count, _maximum);
        }

        _written += count;
    }

    /// <inheritdoc/>
    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        ValidateRequest(sizeHint);
        return _buffer.AsMemory(_written);
    }

    /// <inheritdoc/>
    public Span<byte> GetSpan(int sizeHint = 0)
    {
        ValidateRequest(sizeHint);
        return _buffer.AsSpan(_written);
    }

    private void ValidateRequest(int sizeHint)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);
        if (sizeHint > _buffer.Length - _written)
        {
            throw new ProjectionSnapshotLimitExceededException("serialized-bytes", (long)_maximum + 1, _maximum);
        }
    }
}
