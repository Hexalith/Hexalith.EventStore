using System.Text.Json;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Server.Events;

/// <summary>
/// Default implementation that leaves payloads and snapshots unchanged while emitting well-formed
/// <see cref="PayloadProtectionState.Unprotected"/> metadata so downstream consumers can always
/// distinguish "explicitly unprotected" from "legacy / no metadata".
/// </summary>
public sealed class NoOpEventPayloadProtectionService : IEventPayloadProtectionService {
    /// <inheritdoc/>
    public Task<PayloadProtectionResult> ProtectEventPayloadAsync(
        AggregateIdentity identity,
        IEventPayload eventPayload,
        string eventTypeName,
        byte[] payloadBytes,
        string serializationFormat,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(eventPayload);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventTypeName);
        ArgumentNullException.ThrowIfNull(payloadBytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(serializationFormat);
        cancellationToken.ThrowIfCancellationRequested();

        if (HasProtectedMarker(serializationFormat))
        {
            throw new InvalidOperationException("Protected serialization format requires a protection provider.");
        }

        return Task.FromResult(new PayloadProtectionResult(
            payloadBytes,
            serializationFormat,
            EventStorePayloadProtectionMetadata.Unprotected()));
    }

    /// <inheritdoc/>
    public Task<PayloadProtectionResult> UnprotectEventPayloadAsync(
        AggregateIdentity identity,
        string eventTypeName,
        byte[] payloadBytes,
        string serializationFormat,
        CancellationToken cancellationToken = default)
        => UnprotectEventPayloadAsync(identity, eventTypeName, payloadBytes, serializationFormat, null, cancellationToken);

    /// <inheritdoc/>
    public async Task<PayloadProtectionResult> UnprotectEventPayloadAsync(
        AggregateIdentity identity,
        string eventTypeName,
        byte[] payloadBytes,
        string serializationFormat,
        EventStorePayloadProtectionMetadata? metadata,
        CancellationToken cancellationToken = default)
    {
        PayloadUnprotectionOutcome outcome = await TryUnprotectEventPayloadAsync(
            identity, eventTypeName, payloadBytes, serializationFormat, metadata, cancellationToken).ConfigureAwait(false);
        if (outcome.UnreadableReason is { } reason)
        {
            throw new ProtectedDataUnreadableException(reason);
        }

        return new PayloadProtectionResult(outcome.PayloadBytes!, outcome.SerializationFormat!, outcome.Metadata);
    }

    /// <inheritdoc/>
    public Task<PayloadUnprotectionOutcome> TryUnprotectEventPayloadAsync(
        AggregateIdentity identity,
        string eventTypeName,
        byte[] payloadBytes,
        string serializationFormat,
        EventStorePayloadProtectionMetadata? metadata,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventTypeName);
        ArgumentNullException.ThrowIfNull(payloadBytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(serializationFormat);
        cancellationToken.ThrowIfCancellationRequested();

        EventStorePayloadProtectionMetadata effectiveMetadata = metadata ?? EventStorePayloadProtectionMetadata.Unprotected();
        UnreadableProtectedDataReason? failure = MetadataFailure(effectiveMetadata);
        if (failure is null && HasProtectedMarker(serializationFormat))
        {
            failure = UnreadableProtectedDataReason.BytesMetadataMismatch;
        }

        return Task.FromResult(failure is { } reason
            ? PayloadUnprotectionOutcome.Unreadable(reason, effectiveMetadata)
            : PayloadUnprotectionOutcome.Readable(payloadBytes, serializationFormat, effectiveMetadata));
    }

    /// <inheritdoc/>
    public Task<object> ProtectSnapshotStateAsync(
        AggregateIdentity identity,
        object state,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(state);

        return Task.FromResult(state);
    }

    /// <inheritdoc/>
    public Task<object> UnprotectSnapshotStateAsync(
        AggregateIdentity identity,
        object state,
        CancellationToken cancellationToken = default)
        => UnprotectSnapshotAsync(identity, state, null, cancellationToken);

    /// <inheritdoc/>
    public Task<SnapshotProtectionResult> ProtectSnapshotAsync(
        AggregateIdentity identity,
        object state,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(state);

        return Task.FromResult(new SnapshotProtectionResult(state, EventStorePayloadProtectionMetadata.Unprotected()));
    }

    /// <inheritdoc/>
    public async Task<object> UnprotectSnapshotAsync(
        AggregateIdentity identity,
        object state,
        EventStorePayloadProtectionMetadata? metadata,
        CancellationToken cancellationToken = default)
    {
        SnapshotUnprotectionOutcome outcome = await TryUnprotectSnapshotAsync(
            identity, state, metadata, cancellationToken).ConfigureAwait(false);
        if (outcome.UnreadableReason is { } reason)
        {
            throw new ProtectedDataUnreadableException(reason);
        }

        return outcome.State!;
    }

    /// <inheritdoc/>
    public Task<SnapshotUnprotectionOutcome> TryUnprotectSnapshotAsync(
        AggregateIdentity identity,
        object state,
        EventStorePayloadProtectionMetadata? metadata,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(state);
        cancellationToken.ThrowIfCancellationRequested();

        EventStorePayloadProtectionMetadata effectiveMetadata = metadata ?? EventStorePayloadProtectionMetadata.Unprotected();
        UnreadableProtectedDataReason? failure = MetadataFailure(effectiveMetadata);
        if (failure is null && IsProtectedSnapshot(state))
        {
            failure = UnreadableProtectedDataReason.BytesMetadataMismatch;
        }
        return Task.FromResult(failure is { } reason
            ? SnapshotUnprotectionOutcome.Unreadable(reason, effectiveMetadata)
            : SnapshotUnprotectionOutcome.Readable(state, effectiveMetadata));
    }

    private static bool IsProtectedSnapshot(object state) {
        switch (state) {
            case ProtectedSnapshotPayloadV2:
                return true;
            case JsonElement { ValueKind: JsonValueKind.Object } json:
                return json.EnumerateObject().Any(property =>
                    string.Equals(property.Name, "format", StringComparison.OrdinalIgnoreCase)
                    && IsProtectedFormatValue(property.Value));
            case System.Collections.IDictionary dictionary:
                foreach (System.Collections.DictionaryEntry entry in dictionary) {
                    if (entry.Key is string key && string.Equals(key, "format", StringComparison.OrdinalIgnoreCase)
                        && IsProtectedFormatValue(entry.Value)) { return true; }
                }
                return false;
            case IReadOnlyDictionary<string, object?> dictionary:
                return dictionary.Any(entry => string.Equals(entry.Key, "format", StringComparison.OrdinalIgnoreCase)
                    && IsProtectedFormatValue(entry.Value));
            case IReadOnlyDictionary<string, string> dictionary:
                return dictionary.Any(entry => string.Equals(entry.Key, "format", StringComparison.OrdinalIgnoreCase)
                    && IsProtectedFormatValue(entry.Value));
            case IReadOnlyDictionary<string, JsonElement> dictionary:
                return dictionary.Any(entry => string.Equals(entry.Key, "format", StringComparison.OrdinalIgnoreCase)
                    && IsProtectedFormatValue(entry.Value));
            default:
                return false;
        }
    }

    private static bool IsProtectedFormatValue(object? value)
        => value is string format ? HasProtectedMarker(format)
            : value is JsonElement { ValueKind: JsonValueKind.String } json && HasProtectedMarker(json.GetString()!);

    private static bool HasProtectedMarker(string serializationFormat)
        => serializationFormat.StartsWith("protected+", StringComparison.OrdinalIgnoreCase)
            || serializationFormat.Contains("+pdenc-", StringComparison.OrdinalIgnoreCase);

    private static UnreadableProtectedDataReason? MetadataFailure(EventStorePayloadProtectionMetadata metadata)
    {
        if (metadata.MetadataVersion > EventStorePayloadProtectionMetadata.CurrentMetadataVersion)
        {
            return UnreadableProtectedDataReason.UnknownMetadataVersion;
        }

        if (!Enum.IsDefined(metadata.State) || !EventStorePayloadProtectionMetadataCarrier.TryValidate(metadata, out _))
        {
            return UnreadableProtectedDataReason.MalformedMetadata;
        }

        return metadata.State switch
        {
            PayloadProtectionState.Protected => UnreadableProtectedDataReason.MissingKey,
            PayloadProtectionState.ProviderOpaque => UnreadableProtectedDataReasonMapper.FromProviderOpaqueMetadata(metadata),
            _ => null,
        };
    }
}
