using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Testing.Fakes;

/// <summary>
/// Story 22.7b — provider-neutral, deterministic protection service that returns configurable
/// readable or unreadable outcomes. Used to exercise the unreadable-data taxonomy without depending
/// on a real encryption provider, key vault, DAPR secret store, or cloud KMS.
/// </summary>
/// <remarks>
/// By default the fake behaves like the no-op service. Call
/// <see cref="ConfigureEventUnreadable"/> or <see cref="ConfigureSnapshotUnreadable"/> to make
/// specific operations return an unreadable outcome with the supplied reason.
/// </remarks>
public sealed class FakeUnreadableProtectionService : IEventPayloadProtectionService {
    private readonly List<UnreadableProtectedDataReason> _eventUnreadableQueue = [];
    private readonly List<UnreadableProtectedDataReason> _snapshotUnreadableQueue = [];
    private UnreadableProtectedDataReason? _persistentEventReason;
    private UnreadableProtectedDataReason? _persistentSnapshotReason;
    private readonly List<(string EventTypeName, byte[] PayloadBytes, EventStorePayloadProtectionMetadata? Metadata)> _eventUnprotectInvocations = [];
    private readonly List<(EventStorePayloadProtectionMetadata? Metadata, CancellationToken Token)> _snapshotUnprotectInvocations = [];

    /// <summary>
    /// Queues a single unreadable outcome for the next <see cref="TryUnprotectEventPayloadAsync"/>
    /// call. Once the queue empties subsequent calls return readable outcomes unless
    /// <see cref="ConfigureEventUnreadablePersistent"/> is also set.
    /// </summary>
    /// <param name="reason">The reason category to return.</param>
    public void ConfigureEventUnreadable(UnreadableProtectedDataReason reason)
        => _eventUnreadableQueue.Add(reason);

    /// <summary>
    /// Makes every subsequent event unprotect call return an unreadable outcome with the supplied
    /// reason until <see cref="ResetEventBehavior"/> is called.
    /// </summary>
    /// <param name="reason">The reason category.</param>
    public void ConfigureEventUnreadablePersistent(UnreadableProtectedDataReason reason)
        => _persistentEventReason = reason;

    /// <summary>Queues a single unreadable outcome for the next snapshot unprotect call.</summary>
    /// <param name="reason">The reason category.</param>
    public void ConfigureSnapshotUnreadable(UnreadableProtectedDataReason reason)
        => _snapshotUnreadableQueue.Add(reason);

    /// <summary>Makes every subsequent snapshot unprotect call return an unreadable outcome.</summary>
    /// <param name="reason">The reason category.</param>
    public void ConfigureSnapshotUnreadablePersistent(UnreadableProtectedDataReason reason)
        => _persistentSnapshotReason = reason;

    /// <summary>Clears any configured event unreadable behavior.</summary>
    public void ResetEventBehavior() {
        _eventUnreadableQueue.Clear();
        _persistentEventReason = null;
    }

    /// <summary>Clears any configured snapshot unreadable behavior.</summary>
    public void ResetSnapshotBehavior() {
        _snapshotUnreadableQueue.Clear();
        _persistentSnapshotReason = null;
    }

    /// <summary>Gets the captured invocations of the typed event unprotect entry point.</summary>
    public IReadOnlyList<(string EventTypeName, byte[] PayloadBytes, EventStorePayloadProtectionMetadata? Metadata)> EventUnprotectInvocations
        => _eventUnprotectInvocations;

    /// <summary>Gets the captured invocations of the typed snapshot unprotect entry point.</summary>
    public IReadOnlyList<(EventStorePayloadProtectionMetadata? Metadata, CancellationToken Token)> SnapshotUnprotectInvocations
        => _snapshotUnprotectInvocations;

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
        ArgumentNullException.ThrowIfNull(payloadBytes);

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
        ArgumentNullException.ThrowIfNull(payloadBytes);

        return Task.FromResult(new PayloadProtectionResult(
            payloadBytes,
            serializationFormat,
            EventStorePayloadProtectionMetadata.Unprotected()));
    }

    /// <inheritdoc/>
    public async Task<PayloadUnprotectionOutcome> TryUnprotectEventPayloadAsync(
        AggregateIdentity identity,
        string eventTypeName,
        byte[] payloadBytes,
        string serializationFormat,
        EventStorePayloadProtectionMetadata? metadata,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(payloadBytes);
        cancellationToken.ThrowIfCancellationRequested();

        _eventUnprotectInvocations.Add((eventTypeName, payloadBytes, metadata));

        UnreadableProtectedDataReason? next = DequeueOrPersistent(_eventUnreadableQueue, _persistentEventReason);
        if (next.HasValue) {
            return await Task.FromResult(PayloadUnprotectionOutcome.Unreadable(next.Value, metadata)).ConfigureAwait(false);
        }

        return PayloadUnprotectionOutcome.Readable(
            payloadBytes,
            serializationFormat,
            metadata ?? EventStorePayloadProtectionMetadata.Unprotected());
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
    public async Task<SnapshotUnprotectionOutcome> TryUnprotectSnapshotAsync(
        AggregateIdentity identity,
        object state,
        EventStorePayloadProtectionMetadata? metadata,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(state);
        cancellationToken.ThrowIfCancellationRequested();

        _snapshotUnprotectInvocations.Add((metadata, cancellationToken));

        UnreadableProtectedDataReason? next = DequeueOrPersistent(_snapshotUnreadableQueue, _persistentSnapshotReason);
        if (next.HasValue) {
            return await Task.FromResult(SnapshotUnprotectionOutcome.Unreadable(next.Value, metadata)).ConfigureAwait(false);
        }

        return SnapshotUnprotectionOutcome.Readable(state, metadata ?? EventStorePayloadProtectionMetadata.Unprotected());
    }

    private static UnreadableProtectedDataReason? DequeueOrPersistent(
        List<UnreadableProtectedDataReason> queue,
        UnreadableProtectedDataReason? persistent) {
        if (queue.Count > 0) {
            UnreadableProtectedDataReason head = queue[0];
            queue.RemoveAt(0);
            return head;
        }

        return persistent;
    }
}
