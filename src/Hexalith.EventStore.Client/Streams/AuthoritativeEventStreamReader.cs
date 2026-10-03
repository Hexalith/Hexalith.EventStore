using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Client.Streams;

/// <summary>Verifies an authorized contiguous prefix between two equal sampled heads.</summary>
/// <param name="gateway">The authenticated gateway client; each page is independently authorized.</param>
/// <param name="timeProvider">The observation clock.</param>
/// <param name="readTimeout">Optional stricter whole-read deadline, capped at thirty seconds.</param>
public sealed class AuthoritativeEventStreamReader(IEventStoreGatewayClient gateway, TimeProvider timeProvider, TimeSpan? readTimeout = null)
    : IAuthoritativeEventStreamReader
{
    private const int PageSize = 100;
    private const int MaxEvents = 10_000;
    private const int MaxAttempts = 3;
    private const long MaxPayloadBytes = 16 * 1024 * 1024;

    /// <inheritdoc/>
    public async Task<AuthoritativeStreamReadResult> ReadAsync(AggregateIdentity identity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        TimeSpan bound = readTimeout is { } requested && requested > TimeSpan.Zero && requested < TimeSpan.FromSeconds(30)
            ? requested : TimeSpan.FromSeconds(30);
        timeout.CancelAfter(bound);
        CancellationToken readToken = timeout.Token;
        try
        {
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                long head = await ReadHeadAsync(identity, readToken).ConfigureAwait(false);
                if (head > MaxEvents)
                {
                    return new(null, "source-bound-exceeded");
                }

                var events = new List<StreamReadEvent>();
                long cursor = 0;
                long bytes = 0;
                bool changed = false;
                while (cursor < head)
                {
                    var request = new StreamReadRequest(identity.TenantId, identity.Domain, identity.AggregateId,
                        cursor, head, PageSize: PageSize);
                    StreamReadPage page = await gateway.ReadStreamAsync(request, readToken).WaitAsync(readToken).ConfigureAwait(false);
                    long next = StreamReadPageValidator.ValidateAndGetNextSequence(request, page);
                    if (page.Metadata.LatestSequence != head)
                    {
                        changed = true;
                        break;
                    }

                    if (page.Events.Count == 0)
                    {
                        return new(null, "source-gap");
                    }

                    foreach (StreamReadEvent item in page.Events)
                    {
                        if (item.SequenceNumber != ++cursor
                            || item.ProtectionMetadata?.State == PayloadProtectionState.ProviderOpaque)
                        {
                            return new(null, "source-gap-or-unreadable");
                        }

                        bytes += item.Payload.Length;
                        if (bytes > MaxPayloadBytes)
                        {
                            return new(null, "source-bound-exceeded");
                        }

                        events.Add(item);
                    }

                    if (next != cursor || (!page.Metadata.IsTruncated && cursor != head))
                    {
                        return new(null, "source-incomplete");
                    }
                }

                if (!changed && await ReadHeadAsync(identity, readToken).ConfigureAwait(false) == head)
                {
                    return new(new AuthoritativeEventStream(identity, head, timeProvider.GetUtcNow(),
                        events.AsReadOnly(), Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16))), null);
                }
            }

            return new(null, "source-head-changed");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(null, "source-time-bound-exceeded");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or ArgumentException or System.Text.Json.JsonException)
        {
            return new(null, "source-unavailable");
        }
    }

    private async Task<long> ReadHeadAsync(AggregateIdentity identity, CancellationToken cancellationToken)
    {
        var request = new StreamReadRequest(identity.TenantId, identity.Domain, identity.AggregateId,
            ToSequence: 0, PageSize: 1);
        StreamReadPage page = await gateway.ReadStreamAsync(request, cancellationToken).WaitAsync(cancellationToken).ConfigureAwait(false);
        _ = StreamReadPageValidator.ValidateAndGetNextSequence(request, page);
        if (page.Events.Count != 0 || page.Metadata.IsTruncated)
        {
            throw new InvalidOperationException("Invalid head observation.");
        }

        return page.Metadata.LatestSequence;
    }
}
