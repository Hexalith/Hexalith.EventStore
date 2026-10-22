namespace Hexalith.EventStore.Server.Tests.DomainServices;

/// <summary>Splits response bytes into bounded reads, including through multibyte UTF8 characters.</summary>
internal sealed class BoundedReadDomainResponseStream(byte[] bytes, int maximumRead) : MemoryStream(bytes)
{
    /// <inheritdoc/>
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        => base.ReadAsync(buffer[..Math.Min(maximumRead, buffer.Length)], cancellationToken);
}
