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
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            long startedAt = timeProvider.GetTimestamp();
            ArgumentNullException.ThrowIfNull(identity);
            TimeSpan bound = readTimeout ?? TimeSpan.FromSeconds(30);
            if (bound <= TimeSpan.Zero || bound > TimeSpan.FromSeconds(30))
            {
                cancellationToken.ThrowIfCancellationRequested();
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

                deadline.ThrowIfCancellationRequested();
                return result;
            }
            catch (OperationCanceledException)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new(null, deadline.IsExpired ? "source-time-bound-exceeded" : "source-unavailable");
            }
        }
        catch (Exception)
        {
            // Even unexpected provider/clock/snapshot faults preserve caller-first cancellation.
            // Without caller cancellation, unexpected faults keep their original type and identity.
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
                StreamReadPage supplied = await deadline.ReadAsync(token => gateway.ReadStreamAsync(request, token)).ConfigureAwait(false);
                StreamReadPage page = CapturePage(supplied, request.PageSize, deadline, ref bytes);
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
                    if (item.SequenceNumber != ++cursor)
                    {
                        return new(null, "source-gap-or-unreadable");
                    }

                    events.Add(item);
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
        StreamReadPage supplied = await deadline.ReadAsync(token => gateway.ReadStreamAsync(request, token)).ConfigureAwait(false);
        long bytes = 0;
        StreamReadPage page = CapturePage(supplied, request.PageSize, deadline, ref bytes);
        _ = StreamReadPageValidator.ValidateAndGetNextSequence(request, page);
        if (page.Events.Count != 0 || page.Metadata.IsTruncated)
        {
            throw new InvalidOperationException("Invalid head observation.");
        }

        return page.Metadata.LatestSequence;
    }

    private static StreamReadPage CapturePage(StreamReadPage supplied, int limit, AuthoritativeStreamReadDeadline deadline, ref long bytes)
    {
        deadline.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(supplied);
        var metadata = supplied.Metadata ?? throw new InvalidOperationException("Missing page metadata.");
        var providerEvents = supplied.Events ?? throw new InvalidOperationException("Missing page events.");
        if (providerEvents.Count < 0 || providerEvents.Count > limit || metadata.EventCount < 0 || metadata.EventCount > limit)
        {
            throw new InvalidOperationException("Page event bound exceeded.");
        }

        var owned = new List<StreamReadEvent>();
        foreach (StreamReadEvent item in providerEvents)
        {
            deadline.ThrowIfCancellationRequested();
            if (owned.Count >= limit || item is null || item.Payload is null || item.Payload.Length > MaxPayloadBytes - bytes)
            {
                throw new InvalidOperationException("Page payload or event bound exceeded.");
            }

            byte[] payload = item.Payload.ToArray();
            bytes += payload.Length;
            var protection = CaptureProtection(item.ProtectionMetadata, deadline);
            deadline.ThrowIfCancellationRequested();
            owned.Add(item with { Payload = payload, ProtectionMetadata = protection });
        }

        deadline.ThrowIfCancellationRequested();
        return supplied with { Metadata = metadata with { }, Events = owned.AsReadOnly() };
    }

    private static EventStorePayloadProtectionMetadata? CaptureProtection(EventStorePayloadProtectionMetadata? metadata,
        AuthoritativeStreamReadDeadline deadline)
    {
        if (metadata is null) { return null; }
        deadline.ThrowIfCancellationRequested();
        if (metadata.Scheme?.Length > EventStorePayloadProtectionMetadata.MaxSchemeLength
            || metadata.KeyAlias?.Length > EventStorePayloadProtectionMetadata.MaxKeyAliasLength
            || metadata.ContentHint?.Length > EventStorePayloadProtectionMetadata.MaxContentHintLength)
        {
            throw new InvalidOperationException("Protection metadata field bound exceeded.");
        }

        IReadOnlyDictionary<string, string>? flags = null;
        if (metadata.CompatibilityFlags is { } supplied)
        {
            if (supplied.Count < 0 || supplied.Count > EventStorePayloadProtectionMetadata.MaxCompatibilityFlagCount)
            {
                throw new InvalidOperationException("Protection metadata flag bound exceeded.");
            }

            var captured = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in supplied)
            {
                deadline.ThrowIfCancellationRequested();
                if (captured.Count >= EventStorePayloadProtectionMetadata.MaxCompatibilityFlagCount
                    || pair.Key is null || pair.Key.Length > EventStorePayloadProtectionMetadata.MaxCompatibilityFlagKeyLength
                    || pair.Value is null || pair.Value.Length > EventStorePayloadProtectionMetadata.MaxCompatibilityFlagValueLength
                    || !captured.TryAdd(pair.Key, pair.Value))
                {
                    throw new InvalidOperationException("Protection metadata flag is invalid.");
                }
            }

            flags = new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(captured);
        }

        var owned = metadata with { CompatibilityFlags = flags };
        if (owned.State is not (PayloadProtectionState.Unprotected or PayloadProtectionState.Protected)
            || !EventStorePayloadProtectionMetadataCarrier.TryValidate(owned, out _))
        {
            throw new InvalidOperationException("Protection metadata is unreadable or malformed.");
        }

        deadline.ThrowIfCancellationRequested();
        return owned;
    }

}
