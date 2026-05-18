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
}
