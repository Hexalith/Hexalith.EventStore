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
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventTypeName);
        ArgumentNullException.ThrowIfNull(payloadBytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(serializationFormat);

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
        EventStorePayloadProtectionMetadata? metadata,
        CancellationToken cancellationToken = default)
        => UnprotectEventPayloadAsync(identity, eventTypeName, payloadBytes, serializationFormat, cancellationToken);

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
        return Task.FromResult(effectiveMetadata.State == PayloadProtectionState.Protected
            ? PayloadUnprotectionOutcome.Unreadable(UnreadableProtectedDataReason.MissingKey, effectiveMetadata)
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
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(state);

        return Task.FromResult(state);
    }

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
    public Task<object> UnprotectSnapshotAsync(
        AggregateIdentity identity,
        object state,
        EventStorePayloadProtectionMetadata? metadata,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(state);

        return Task.FromResult(state);
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
        return Task.FromResult(effectiveMetadata.State == PayloadProtectionState.Protected
            ? SnapshotUnprotectionOutcome.Unreadable(UnreadableProtectedDataReason.MissingKey, effectiveMetadata)
            : SnapshotUnprotectionOutcome.Readable(state, effectiveMetadata));
    }
}
