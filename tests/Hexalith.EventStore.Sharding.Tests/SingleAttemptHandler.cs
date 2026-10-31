namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>Observes one actor HTTP send and can discard a fully received successful response.</summary>
/// <param name="discardSuccess">Whether to lose a successful response before the caller sees it.</param>
/// <param name="inner">An optional deterministic transport.</param>
internal sealed class SingleAttemptHandler(bool discardSuccess, HttpMessageHandler? inner = null)
    : DelegatingHandler(inner ?? new HttpClientHandler())
{
    private int _sendCount;

    /// <summary>Gets how many SDK sends were attempted, including forbidden retries.</summary>
    internal int SendCount => _sendCount;

    /// <summary>Gets whether a successful response was actually consumed and discarded.</summary>
    internal bool DiscardedSuccess { get; private set; }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (Interlocked.Increment(ref _sendCount) != 1)
            throw new InvalidOperationException("An ambiguous actor reservation must never be retried.");
        HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
        if (!discardSuccess || !response.IsSuccessStatusCode) return response;
        try
        {
            await response.Content.LoadIntoBufferAsync(cancellationToken);
            DiscardedSuccess = true;
            throw new HttpRequestException("Injected lost successful actor response; durable outcome must be inspected.");
        }
        finally
        {
            response.Dispose();
        }
    }
}
