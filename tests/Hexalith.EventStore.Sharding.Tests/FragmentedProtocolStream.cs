namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>Supplies one byte per read to exercise TCP fragmentation deterministically.</summary>
/// <param name="bytes">The complete test wire bytes.</param>
internal sealed class FragmentedProtocolStream(byte[] bytes) : MemoryStream(bytes)
{
    /// <inheritdoc/>
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        => base.ReadAsync(buffer[..Math.Min(1, buffer.Length)], cancellationToken);
}
