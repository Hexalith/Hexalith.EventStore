using System.Net;

namespace Hexalith.EventStore.Testing;

/// <summary>Keeps streamed response bodies within their fixture lifetime and finite request budget.</summary>
internal sealed class TestEnvironmentResponseContent : HttpContent
{
    private readonly HttpContent _content;
    private readonly CancellationTokenSource _request;
    private readonly CancellationTokenRegistration _cancellation;

    /// <summary>Owns a response body and the request lifetime until the response is disposed.</summary>
    /// <param name="content">The transport-owned response body.</param>
    /// <param name="request">The linked fixture/request lifetime and timeout.</param>
    internal TestEnvironmentResponseContent(HttpContent content, CancellationTokenSource request)
    {
        _content = content;
        _request = request;
        foreach (KeyValuePair<string, IEnumerable<string>> header in content.Headers)
        {
            Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        _cancellation = request.Token.UnsafeRegister(static value => ((HttpContent)value!).Dispose(), content);
    }

    /// <inheritdoc/>
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        => SerializeToStreamAsync(stream, context, CancellationToken.None);

    /// <inheritdoc/>
    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
    {
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(_request.Token, cancellationToken);
        await _content.CopyToAsync(stream, context, linked.Token).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override Task<Stream> CreateContentReadStreamAsync()
        => CreateContentReadStreamAsync(CancellationToken.None);

    /// <inheritdoc/>
    protected override async Task<Stream> CreateContentReadStreamAsync(CancellationToken cancellationToken)
        => new TestEnvironmentResponseStream(await _content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false), _request.Token);

    /// <inheritdoc/>
    protected override bool TryComputeLength(out long length)
    {
        length = _content.Headers.ContentLength ?? 0;
        return _content.Headers.ContentLength.HasValue;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                _cancellation.Dispose();
                _content.Dispose();
            }
            finally
            {
                _request.Dispose();
            }
        }
        base.Dispose(disposing);
    }
}
