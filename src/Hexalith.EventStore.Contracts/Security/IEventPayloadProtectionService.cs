using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Contracts.Security;

/// <summary>
/// Optional infrastructure hook for protecting event payloads and snapshot state before storage,
/// and unprotecting them before publication or replay.
/// </summary>
public interface IEventPayloadProtectionService {
    /// <summary>
    /// Applies optional protection to an event payload before it is persisted.
    /// </summary>
    /// <param name="identity">The aggregate identity owning the payload.</param>
    /// <param name="eventPayload">The domain event payload.</param>
    /// <param name="eventTypeName">The fully-qualified event type name.</param>
    /// <param name="payloadBytes">The serialized payload bytes.</param>
    /// <param name="serializationFormat">The serialization format identifier.</param>
    /// <param name="cancellationToken">The caller's cancellation token.</param>
    /// <returns>A <see cref="PayloadProtectionResult"/> with transformed bytes, format, and metadata.</returns>
    Task<PayloadProtectionResult> ProtectEventPayloadAsync(
        AggregateIdentity identity,
        IEventPayload eventPayload,
        string eventTypeName,
        byte[] payloadBytes,
        string serializationFormat,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes optional protection from an event payload before it is published or replayed.
    /// </summary>
    /// <param name="identity">The aggregate identity owning the payload.</param>
    /// <param name="eventTypeName">The fully-qualified event type name.</param>
    /// <param name="payloadBytes">The protected payload bytes.</param>
    /// <param name="serializationFormat">The serialization format identifier.</param>
    /// <param name="cancellationToken">The caller's cancellation token.</param>
    /// <returns>A <see cref="PayloadProtectionResult"/> with transformed bytes, format, and metadata.</returns>
    Task<PayloadProtectionResult> UnprotectEventPayloadAsync(
        AggregateIdentity identity,
        string eventTypeName,
        byte[] payloadBytes,
        string serializationFormat,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes optional protection from an event payload before it is published or replayed, using
    /// the persisted metadata recorded alongside the payload. The default implementation delegates
    /// to the legacy event unprotect method for backward compatibility with providers that do not
    /// need metadata.
    /// </summary>
    /// <param name="identity">The aggregate identity owning the payload.</param>
    /// <param name="eventTypeName">The fully-qualified event type name.</param>
    /// <param name="payloadBytes">The protected payload bytes.</param>
    /// <param name="serializationFormat">The serialization format identifier.</param>
    /// <param name="metadata">The protection metadata persisted alongside the payload.</param>
    /// <param name="cancellationToken">The caller's cancellation token.</param>
    /// <returns>A <see cref="PayloadProtectionResult"/> with transformed bytes, format, and metadata.</returns>
    Task<PayloadProtectionResult> UnprotectEventPayloadAsync(
        AggregateIdentity identity,
        string eventTypeName,
        byte[] payloadBytes,
        string serializationFormat,
        EventStorePayloadProtectionMetadata? metadata,
        CancellationToken cancellationToken = default)
        => UnprotectEventPayloadAsync(identity, eventTypeName, payloadBytes, serializationFormat, cancellationToken);

    /// <summary>
    /// Applies optional protection to snapshot state before it is written to storage.
    /// Object-based legacy entry point kept for backward compatibility; new callers should use
    /// <see cref="ProtectSnapshotAsync"/> so state and metadata travel together.
    /// </summary>
    /// <param name="identity">The aggregate identity owning the snapshot.</param>
    /// <param name="state">The snapshot state object.</param>
    /// <param name="cancellationToken">The caller's cancellation token.</param>
    /// <returns>The (possibly protected) snapshot state object.</returns>
    Task<object> ProtectSnapshotStateAsync(
        AggregateIdentity identity,
        object state,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes optional protection from snapshot state after it is loaded from storage.
    /// Object-based legacy entry point kept for backward compatibility; new callers should use
    /// <see cref="UnprotectSnapshotAsync"/> so the protection metadata recorded at write time can
    /// be passed back to the provider.
    /// </summary>
    /// <param name="identity">The aggregate identity owning the snapshot.</param>
    /// <param name="state">The (possibly protected) snapshot state object.</param>
    /// <param name="cancellationToken">The caller's cancellation token.</param>
    /// <returns>The unprotected snapshot state object.</returns>
    Task<object> UnprotectSnapshotStateAsync(
        AggregateIdentity identity,
        object state,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Typed snapshot protection entry point. Returns the snapshot state together with the
    /// protection metadata that describes how it was produced. The default implementation
    /// delegates to <see cref="ProtectSnapshotStateAsync"/> and wraps the result in
    /// <see cref="PayloadProtectionState.Unprotected"/> metadata for backward compatibility with
    /// implementers that have not yet migrated.
    /// </summary>
    /// <param name="identity">The aggregate identity owning the snapshot.</param>
    /// <param name="state">The snapshot state object.</param>
    /// <param name="cancellationToken">The caller's cancellation token.</param>
    /// <returns>A <see cref="SnapshotProtectionResult"/> with state and metadata.</returns>
    async Task<SnapshotProtectionResult> ProtectSnapshotAsync(
        AggregateIdentity identity,
        object state,
        CancellationToken cancellationToken = default) {
        object protectedState = await ProtectSnapshotStateAsync(identity, state, cancellationToken).ConfigureAwait(false);
        return new SnapshotProtectionResult(protectedState, EventStorePayloadProtectionMetadata.Unprotected());
    }

    /// <summary>
    /// Typed snapshot unprotection entry point. Accepts the protection metadata recorded at
    /// write time so providers can route to the correct key or scheme. The default
    /// implementation delegates to <see cref="UnprotectSnapshotStateAsync"/> and ignores the
    /// metadata for backward compatibility.
    /// </summary>
    /// <param name="identity">The aggregate identity owning the snapshot.</param>
    /// <param name="state">The (possibly protected) snapshot state object.</param>
    /// <param name="metadata">The protection metadata persisted alongside the snapshot, or <see langword="null"/> for legacy records.</param>
    /// <param name="cancellationToken">The caller's cancellation token.</param>
    /// <returns>The unprotected snapshot state object.</returns>
    Task<object> UnprotectSnapshotAsync(
        AggregateIdentity identity,
        object state,
        EventStorePayloadProtectionMetadata? metadata,
        CancellationToken cancellationToken = default)
        => UnprotectSnapshotStateAsync(identity, state, cancellationToken);

    /// <summary>
    /// Story 22.7b — typed unprotection that returns a <see cref="PayloadUnprotectionOutcome"/>
    /// classifying the result as readable or unreadable. Providers that need to signal "missing
    /// key", "provider unavailable", "consistency mismatch" or other unreadable conditions MUST
    /// use this entry point: exceptions are reserved for cancellation or unexpected infrastructure
    /// failure and must not be parsed for public unreadable-data classification. The default
    /// implementation delegates to the metadata-aware <see cref="UnprotectEventPayloadAsync(AggregateIdentity, string, byte[], string, EventStorePayloadProtectionMetadata?, CancellationToken)"/>
    /// overload and reports any non-cancellation exception as a generic provider-unavailable
    /// readable failure mapped to <see cref="UnreadableProtectedDataReason.ProviderUnavailable"/>.
    /// </summary>
    /// <param name="identity">The aggregate identity owning the payload.</param>
    /// <param name="eventTypeName">The fully-qualified event type name.</param>
    /// <param name="payloadBytes">The protected payload bytes.</param>
    /// <param name="serializationFormat">The serialization format identifier.</param>
    /// <param name="metadata">The protection metadata persisted alongside the payload.</param>
    /// <param name="cancellationToken">The caller's cancellation token.</param>
    /// <returns>A typed <see cref="PayloadUnprotectionOutcome"/>.</returns>
    async Task<PayloadUnprotectionOutcome> TryUnprotectEventPayloadAsync(
        AggregateIdentity identity,
        string eventTypeName,
        byte[] payloadBytes,
        string serializationFormat,
        EventStorePayloadProtectionMetadata? metadata,
        CancellationToken cancellationToken = default) {
        try {
            PayloadProtectionResult result = await UnprotectEventPayloadAsync(
                identity,
                eventTypeName,
                payloadBytes,
                serializationFormat,
                metadata,
                cancellationToken).ConfigureAwait(false);
            return PayloadUnprotectionOutcome.FromResult(result);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch {
            // Default implementation is conservative: any non-cancellation failure becomes a
            // transient provider-unavailable outcome. Custom providers should override this
            // entry point and return the precise unreadable category they observe.
            return PayloadUnprotectionOutcome.Unreadable(UnreadableProtectedDataReason.ProviderUnavailable, metadata);
        }
    }

    /// <summary>
    /// Story 22.7b — typed snapshot unprotection entry point with explicit unreadable handling.
    /// Mirrors <see cref="TryUnprotectEventPayloadAsync"/> for snapshot state.
    /// </summary>
    /// <param name="identity">The aggregate identity owning the snapshot.</param>
    /// <param name="state">The (possibly protected) snapshot state object.</param>
    /// <param name="metadata">The protection metadata persisted alongside the snapshot, or <see langword="null"/> for legacy records.</param>
    /// <param name="cancellationToken">The caller's cancellation token.</param>
    /// <returns>A typed <see cref="SnapshotUnprotectionOutcome"/>.</returns>
    async Task<SnapshotUnprotectionOutcome> TryUnprotectSnapshotAsync(
        AggregateIdentity identity,
        object state,
        EventStorePayloadProtectionMetadata? metadata,
        CancellationToken cancellationToken = default) {
        try {
            object unprotected = await UnprotectSnapshotAsync(identity, state, metadata, cancellationToken)
                .ConfigureAwait(false);
            return SnapshotUnprotectionOutcome.Readable(unprotected, metadata ?? EventStorePayloadProtectionMetadata.Unprotected());
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch {
            return SnapshotUnprotectionOutcome.Unreadable(UnreadableProtectedDataReason.ProviderUnavailable, metadata);
        }
    }
}
