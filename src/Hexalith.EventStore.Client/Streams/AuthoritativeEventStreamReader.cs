using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Client.Streams;

/// <summary>Verifies an authorized contiguous prefix between two equal sampled heads.</summary>
/// <param name="gateway">The authenticated gateway client; each page is independently authorized.</param>
/// <param name="timeProvider">The observation clock.</param>
/// <param name="readTimeout">Optional positive whole-read deadline of at most thirty seconds; invalid bounds fail closed.</param>
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
        long startedAt = timeProvider.GetTimestamp();
        ArgumentNullException.ThrowIfNull(identity);
        cancellationToken.ThrowIfCancellationRequested();
        TimeSpan bound = readTimeout ?? TimeSpan.FromSeconds(30);
        if (bound <= TimeSpan.Zero || bound > TimeSpan.FromSeconds(30))
        {
            return new(null, "source-invalid-time-bound");
        }

        using var deadline = new AuthoritativeStreamReadDeadline(bound, timeProvider, cancellationToken, startedAt);
        try
        {
            AuthoritativeStreamReadResult result;
            try
            {
                result = await ReadCoreAsync(identity, deadline).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or ArgumentException or System.Text.Json.JsonException)
            {
                result = new(null, "source-unavailable");
            }

            // Every success, validation denial and provider-fault verdict observes the same
            // caller-first completion boundary; elapsed processing cannot release evidence.
            deadline.ThrowIfCancellationRequested();
            return result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new(null, "source-time-bound-exceeded");
        }
        catch (OperationCanceledException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw;
        }
    }

    private async Task<AuthoritativeStreamReadResult> ReadCoreAsync(AggregateIdentity identity, AuthoritativeStreamReadDeadline deadline)
    {
        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            deadline.ThrowIfCancellationRequested();
            long head = await ReadHeadAsync(identity, deadline).ConfigureAwait(false);
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
                StreamReadPage page = await deadline.ReadAsync(token => gateway.ReadStreamAsync(request, token)).ConfigureAwait(false);
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
                    deadline.ThrowIfCancellationRequested();
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

                    events.Add(item with
                    {
                        Payload = item.Payload.ToArray(),
                        ProtectionMetadata = item.ProtectionMetadata is { } metadata ? metadata with
                        {
                            CompatibilityFlags = metadata.CompatibilityFlags is { } flags
                                ? new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(
                                    flags.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)) : null,
                        } : null,
                    });
                }

                if (next != cursor || (!page.Metadata.IsTruncated && cursor != head))
                {
                    return new(null, "source-incomplete");
                }
            }

            if (!changed && await ReadHeadAsync(identity, deadline).ConfigureAwait(false) == head)
            {
                deadline.ThrowIfCancellationRequested();
                return new(new AuthoritativeEventStream(identity, head, timeProvider.GetUtcNow(),
                    events.AsReadOnly(), Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16))), null);
            }
        }

        return new(null, "source-head-changed");
    }

    private async Task<long> ReadHeadAsync(AggregateIdentity identity, AuthoritativeStreamReadDeadline deadline)
    {
        var request = new StreamReadRequest(identity.TenantId, identity.Domain, identity.AggregateId,
            ToSequence: 0, PageSize: 1);
        StreamReadPage page = await deadline.ReadAsync(token => gateway.ReadStreamAsync(request, token)).ConfigureAwait(false);
        _ = StreamReadPageValidator.ValidateAndGetNextSequence(request, page);
        if (page.Events.Count != 0 || page.Metadata.IsTruncated)
        {
            throw new InvalidOperationException("Invalid head observation.");
        }

        return page.Metadata.LatestSequence;
    }
}
