using System.Net;

namespace Hexalith.EventStore.Operations.Tests;

/// <summary>Produces an action body without a declared length.</summary>
/// <param name="body">The test body.</param>
internal sealed class HttpHostSecurityStreamingContent(byte[] body) : HttpContent
{
    /// <inheritdoc />
    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        foreach (ReadOnlyMemory<byte> chunk in body.Chunk(128).Select(static bytes => bytes.AsMemory()))
        {
            await stream.WriteAsync(chunk).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    protected override bool TryComputeLength(out long length)
    {
        length = 0;
        return false;
    }
}
