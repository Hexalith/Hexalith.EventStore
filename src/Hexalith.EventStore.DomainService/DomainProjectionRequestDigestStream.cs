using System.Security.Cryptography;

using Hexalith.EventStore.Client.Projections;

namespace Hexalith.EventStore.DomainService;

/// <summary>Hashes original request bytes without retaining another complete serialized event array.</summary>
internal sealed class DomainProjectionRequestDigestStream : Stream
{
    private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

    /// <summary>Reads the exact completed request digest.</summary>
    internal string Digest => Convert.ToHexStringLower(_hash.GetHashAndReset());

    /// <inheritdoc/>
    public override bool CanRead => false;
    /// <inheritdoc/>
    public override bool CanSeek => false;
    /// <inheritdoc/>
    public override bool CanWrite => true;
    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException();
    /// <inheritdoc/>
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));
    /// <inheritdoc/>
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ReplayRequestDigestSerializationBytes, buffer.Length);
        _hash.AppendData(buffer);
    }
    /// <inheritdoc/>
    public override void Flush() { }
    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();
    /// <inheritdoc/>
    protected override void Dispose(bool disposing) { if (disposing) _hash.Dispose(); base.Dispose(disposing); }
}
