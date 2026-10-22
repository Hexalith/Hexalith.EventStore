namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Stops synchronous pending-state serialization before allocating beyond its approved byte carrier bound.</summary>
internal sealed class BoundedPendingStateStream(int maximumBytes) : MemoryStream
{
    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count)
    {
        Grow(count); base.Write(buffer, offset, count);
    }
    /// <inheritdoc/>
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        Grow(buffer.Length); base.Write(buffer);
    }
    /// <inheritdoc/>
    public override void WriteByte(byte value)
    {
        Grow(1); base.WriteByte(value);
    }
    private void Grow(int count)
    {
        long required = Position + count;
        if (required > maximumBytes) { throw new InvalidOperationException("Pending state exceeds its bounded carrier."); }
        if (required > Capacity) { Capacity = (int)Math.Min(maximumBytes, Math.Max(required, Math.Max(256L, Capacity * 2L))); }
    }
}
