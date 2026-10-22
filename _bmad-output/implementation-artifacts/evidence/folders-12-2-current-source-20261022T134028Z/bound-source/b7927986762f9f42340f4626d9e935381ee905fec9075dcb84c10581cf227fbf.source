using System.Buffers;
using System.Text.Json;

namespace Hexalith.EventStore.Client.Handlers;

/// <summary>Pre-admits fixed serializer capacity and scratch before writing optional snapshot JSON.</summary>
/// <remarks>Application converters and callbacks remain subject to their deployment qualification.</remarks>
internal sealed class BoundedSnapshotJsonBuffer : IBufferWriter<byte>
{
    private readonly byte[] _buffer;
    private readonly int _maximumBytes;
    private int _written;

    private BoundedSnapshotJsonBuffer(int maximumBytes, JsonSerializerOptions options, long scratchBytes, long maximumInputBytes)
    {
        if (!CanSerialize(maximumBytes, options, scratchBytes, maximumInputBytes))
        {
            throw new InvalidOperationException("SnapshotCaptureLimitExceeded");
        }
        _maximumBytes = maximumBytes;
        _buffer = new byte[checked((int)BufferCapacity(maximumBytes, options))];
    }

    /// <summary>Checks the fixed output buffer, encoder scratch, and depth stack without allocating them.</summary>
    internal static bool CanSerialize(int maximumBytes, JsonSerializerOptions options, long scratchBytes, long maximumInputBytes)
    {
        if (maximumBytes <= 0 || maximumInputBytes <= 0) { return false; }
        try
        {
            // Escaping runs before the writer can reject oversized output. The input
            // maximum therefore comes from the qualified working graph or admitted
            // UTF8 JSON, whose byte bound conservatively bounds UTF16 string length.
            // The encoder can rent six UTF16 chars per input char, rounded by the
            // pool to less than twice that request, at two bytes per char. Reserve
            // that 24x input allowance beside the fixed output and writer/depth state.
            long required = checked(BufferCapacity(maximumBytes, options) + 24L * maximumInputBytes
                + 4096 + 64L * (options.MaxDepth == 0 ? 64 : options.MaxDepth));
            return required <= scratchBytes;
        }
        catch (OverflowException) { return false; }
    }

    /// <summary>Writes canonical JSON with the caller's options using only admitted serializer capacity.</summary>
    internal static void Serialize(Stream output, object value, Type type, JsonSerializerOptions options,
        int maximumBytes, long scratchBytes, long maximumInputBytes)
    {
        var buffer = new BoundedSnapshotJsonBuffer(maximumBytes, options, scratchBytes, maximumInputBytes);
        using var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions
        {
            Encoder = options.Encoder,
            Indented = options.WriteIndented,
            MaxDepth = options.MaxDepth == 0 ? 64 : options.MaxDepth,
        });
        JsonSerializer.Serialize(writer, value, type, options);
        writer.Flush();
        output.Write(buffer._buffer.AsSpan(0, buffer._written));
    }

    private static long BufferCapacity(int maximumBytes, JsonSerializerOptions options)
        => Math.Max(options.DefaultBufferSize, checked(3L * maximumBytes + 4096));

    /// <inheritdoc/>
    public void Advance(int count)
    {
        if (count < 0 || count > _maximumBytes - _written)
        {
            throw new InvalidOperationException("SnapshotCaptureLimitExceeded");
        }
        _written += count;
    }

    /// <inheritdoc/>
    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        if (sizeHint < 0 || sizeHint > _buffer.Length - _written)
        {
            throw new InvalidOperationException("SnapshotCaptureLimitExceeded");
        }
        return _buffer.AsMemory(_written);
    }

    /// <inheritdoc/>
    public Span<byte> GetSpan(int sizeHint = 0) => GetMemory(sizeHint).Span;
}
