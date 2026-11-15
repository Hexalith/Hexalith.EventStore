using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Client.Streams;

/// <summary>Owns one bounded native page and its protection metadata before invoking a selector.</summary>
internal static class NativeSourceScanPageCapture
{
    private const long MaximumPagePayloadBytes = 2 * 1024 * 1024;

    /// <summary>Copies and bounds one page before any consumer observes its events.</summary>
    internal static StreamReadPage Capture(StreamReadPage supplied, int limit, AuthoritativeStreamReadDeadline deadline)
    {
        long bytes = 0;
        return Capture(supplied, limit, deadline, ref bytes);
    }

    /// <summary>Copies and bounds one page before any consumer observes its events.</summary>
    internal static StreamReadPage Capture(StreamReadPage supplied, int limit, AuthoritativeStreamReadDeadline deadline, ref long bytes)
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
            if (owned.Count >= limit || item is null || item.Payload is null || item.Payload.Length > MaximumPagePayloadBytes - bytes)
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
