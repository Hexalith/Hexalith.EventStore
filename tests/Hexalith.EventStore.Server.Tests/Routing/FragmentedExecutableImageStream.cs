namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Exposes deterministic short reads, growth, failures and inaccurate allocation hints without permitting rereads.</summary>
internal sealed class FragmentedExecutableImageStream(byte[] bytes, int maximumRead, long lengthHint = -1) : Stream
{
    private int _position;
    /// <summary>Gets or sets how many source bytes have become available before EOF is observed.</summary>
    internal int VisibleLength { get; set; } = bytes.Length;
    /// <summary>Gets or sets a callback after each successful original read.</summary>
    internal Action<FragmentedExecutableImageStream>? AfterRead { get; set; }
    /// <summary>Gets or sets a deterministic read failure threshold.</summary>
    internal int? ThrowAfterBytes { get; set; }
    /// <summary>Gets the complete source bytes consumed once.</summary>
    internal int BytesRead => _position;
    /// <summary>Gets the number of actual EOF observations.</summary>
    internal int EofReads { get; private set; }
    /// <summary>Gets the count of forbidden rewind/seek attempts.</summary>
    internal int SeekCalls { get; private set; }
    /// <inheritdoc/>
    public override bool CanRead => true;
    /// <inheritdoc/>
    public override bool CanSeek => lengthHint >= 0;
    /// <inheritdoc/>
    public override bool CanWrite => false;
    /// <inheritdoc/>
    public override long Length => lengthHint >= 0 ? lengthHint : throw new NotSupportedException();
    /// <inheritdoc/>
    public override long Position { get => _position; set { SeekCalls++; throw new NotSupportedException(); } }
    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
    /// <inheritdoc/>
    public override int Read(Span<byte> buffer)
    {
        if (ThrowAfterBytes is { } threshold && _position >= threshold) throw new IOException("Controlled fresh executable read failure.");
        int count = Math.Min(Math.Min(buffer.Length, maximumRead), VisibleLength - _position);
        if (count == 0) { EofReads++; return 0; }
        bytes.AsSpan(_position, count).CopyTo(buffer);
        _position += count;
        AfterRead?.Invoke(this);
        return count;
    }
    /// <inheritdoc/>
    public override int ReadByte()
    {
        Span<byte> value = stackalloc byte[1];
        return Read(value) == 0 ? -1 : value[0];
    }
    /// <inheritdoc/>
    public override void Flush() { }
    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) { SeekCalls++; throw new NotSupportedException(); }
    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();
    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
