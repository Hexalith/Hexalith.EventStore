using System.Net;
using System.Net.Http.Headers;

namespace Hexalith.EventStore.DomainService.Tests;

internal sealed class DelayedOwnerResponseContent(byte[] payload, TimeSpan bodyDelay, TimeSpan disposalDelay) : HttpContent
{
    private bool _disposed;

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        => SerializeToStreamAsync(stream, context, CancellationToken.None);

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
    {
        Headers.ContentType = new MediaTypeHeaderValue("application/json");
        await Task.Delay(bodyDelay, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
    }

    protected override bool TryComputeLength(out long length)
    {
        length = payload.Length;
        return true;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            Thread.Sleep(disposalDelay);
        }
        base.Dispose(disposing);
    }
}
