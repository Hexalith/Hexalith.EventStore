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
    Task<PayloadProtectionResult> UnprotectEventPayloadAsync(
        AggregateIdentity identity,
        string eventTypeName,
        byte[] payloadBytes,
        string serializationFormat,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies optional protection to snapshot state before it is written to storage.
    /// </summary>
    Task<object> ProtectSnapshotStateAsync(
        AggregateIdentity identity,
        object state,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes optional protection from snapshot state after it is loaded from storage.
    /// </summary>
    Task<object> UnprotectSnapshotStateAsync(
        AggregateIdentity identity,
        object state,
        CancellationToken cancellationToken = default);
}