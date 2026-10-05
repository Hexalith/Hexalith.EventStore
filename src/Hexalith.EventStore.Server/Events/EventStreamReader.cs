
using System.Diagnostics;

using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Identity;

using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Server.Events;
/// <summary>
/// Reads events from the actor state store and rehydrates aggregate state.
/// Supports snapshot-aware rehydration: when a snapshot is provided, only tail events
/// after the snapshot sequence are loaded (Story 3.10).
/// Created per-call (not DI-registered) -- same pattern as IdempotencyChecker and TenantValidator.
/// </summary>
public partial class EventStreamReader(
    IActorStateManager stateManager,
    ILogger<EventStreamReader> logger) : IEventStreamReader {
    /// <inheritdoc/>
    public Task<RehydrationResult?> RehydrateAsync(AggregateIdentity identity, SnapshotRecord? snapshot = null)
        => RehydrateAsync(identity, snapshot, CancellationToken.None);

    /// <summary>Rehydrates the legacy stream while forwarding the originating cancellation token.</summary>
    public async Task<RehydrationResult?> RehydrateAsync(
        AggregateIdentity identity, SnapshotRecord? snapshot, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(identity);
        cancellationToken.ThrowIfCancellationRequested();

        var sw = Stopwatch.StartNew();

        // Load metadata to get current sequence number
        ConditionalValue<AggregateMetadata> metadataResult;
        try {
            metadataResult = await stateManager
                .TryGetStateAsync<AggregateMetadata>(identity.MetadataKey, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            throw new EventDeserializationException(-1, identity.ActorId, ex);
        }
        cancellationToken.ThrowIfCancellationRequested();

        if (!metadataResult.HasValue) {
            // AC #3 / AC #8: new aggregate with no events
            if (snapshot is not null) {
                // Snapshot exists but no events -- return snapshot state directly
                sw.Stop();
                Log.RehydrationCompleteSnapshotOnly(logger, identity.TenantId, identity.Domain, identity.AggregateId, sw.ElapsedMilliseconds);

                return new RehydrationResult(
                    SnapshotState: snapshot.State,
                    Events: [],
                    LastSnapshotSequence: snapshot.SequenceNumber,
                    CurrentSequence: snapshot.SequenceNumber);
            }

            Log.NewAggregateDetected(logger, identity.TenantId, identity.Domain, identity.AggregateId);
            return null; // New aggregate, no snapshot, no events
        }

        AggregateMetadata metadata = metadataResult.Value;
        if (metadata.CurrentSequence <= 0) {
            throw new InvalidOperationException(
                $"Invalid aggregate metadata: CurrentSequence={metadata.CurrentSequence} for {identity.ActorId}");
        }

        long currentSequence = metadata.CurrentSequence;
        long lastSnapshotSequence = snapshot?.SequenceNumber ?? 0;
        if (metadata.RetainedFloor < 1
            || (metadata.RetainedFloor > currentSequence && metadata.RetainedFloor - currentSequence > 1)) {
            throw new InvalidOperationException("SourceHeadChanged: actor metadata has an invalid retained floor.");
        }

        // Determine read range based on snapshot presence
        long startSequence;
        long requestedCount;

        if (snapshot is not null) {
            // AC #8: snapshot at current sequence -- no tail events needed
            if (snapshot.SequenceNumber >= currentSequence) {
                await RequireUnchangedMetadataAsync(identity, metadata, cancellationToken).ConfigureAwait(false);
                sw.Stop();
                Log.RehydrationCompleteSnapshotAtCurrent(logger, identity.TenantId, identity.Domain, identity.AggregateId, sw.ElapsedMilliseconds);

                return new RehydrationResult(
                    SnapshotState: snapshot.State,
                    Events: [],
                    LastSnapshotSequence: lastSnapshotSequence,
                    CurrentSequence: currentSequence);
            }

            // AC #4: read only tail events from snapshot.SequenceNumber + 1
            startSequence = checked(snapshot.SequenceNumber + 1);
            requestedCount = checked(currentSequence - snapshot.SequenceNumber);
        }
        else {
            // AC #3: no snapshot, full replay from sequence 1
            startSequence = 1;
            requestedCount = currentSequence;
        }

        if (startSequence < metadata.RetainedFloor) {
            throw new InvalidOperationException("ReplayRestartRequired: the requested prefix is below the retained floor.");
        }

        var arrayBudget = new LegacyEventArrayBudget(requestedCount);
        int eventCount = checked((int)requestedCount);

        // IActorStateManager is scoped to the actor turn and is not safe for concurrent access.
        // Keep reads ordered to avoid corrupting the actor state's internal change-tracking cache.
        string keyPrefix = identity.EventStreamKeyPrefix;

        var events = new List<EventEnvelope>(eventCount);
        for (int offset = 0; offset < eventCount; offset++) {
            long seq = startSequence + offset;
            cancellationToken.ThrowIfCancellationRequested();
            ConditionalValue<EventEnvelope> eventResult;
            try {
                eventResult = await stateManager
                    .TryGetStateAsync<EventEnvelope>($"{keyPrefix}{seq}", cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException) {
                throw new EventDeserializationException(seq, identity.ActorId, ex);
            }
            cancellationToken.ThrowIfCancellationRequested();

            if (!eventResult.HasValue) {
                throw new MissingEventException(seq, identity.TenantId, identity.Domain, identity.AggregateId);
            }

            EventEnvelope stored = eventResult.Value;
            if (!string.Equals(stored.TenantId, identity.TenantId, StringComparison.Ordinal)
                || !string.Equals(stored.Domain, identity.Domain, StringComparison.Ordinal)
                || !string.Equals(stored.AggregateId, identity.AggregateId, StringComparison.Ordinal)
                || stored.SequenceNumber != seq || stored.Payload is null) {
                throw new InvalidOperationException("AddressMismatch: actor event identity or sequence disagrees with its key.");
            }

            LegacyEventReadGuard.RequireUnversioned(stored);
            arrayBudget.Add(stored);
            events.Add(stored);
        }

        cancellationToken.ThrowIfCancellationRequested();
        await RequireUnchangedMetadataAsync(identity, metadata, cancellationToken).ConfigureAwait(false);

        sw.Stop();

        // Log rehydration mode (AC #7 logging)
        string mode = snapshot is not null ? "snapshot+tail" : "full-replay";
        Log.StateRehydrated(logger, identity.TenantId, identity.Domain, identity.AggregateId, mode, events.Count, sw.ElapsedMilliseconds);

        return new RehydrationResult(
            SnapshotState: snapshot?.State,
            Events: events,
            LastSnapshotSequence: lastSnapshotSequence,
            CurrentSequence: currentSequence);
    }

    private async Task RequireUnchangedMetadataAsync(AggregateIdentity identity, AggregateMetadata expected,
        CancellationToken cancellationToken) {
        ConditionalValue<AggregateMetadata> observed;
        try {
            observed = await stateManager.TryGetStateAsync<AggregateMetadata>(identity.MetadataKey, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OperationCanceledException) {
            throw new EventDeserializationException(-1, identity.ActorId, error);
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!observed.HasValue || observed.Value.CurrentSequence != expected.CurrentSequence
            || observed.Value.RetainedFloor != expected.RetainedFloor
            || !string.Equals(observed.Value.ETag, expected.ETag, StringComparison.Ordinal)) {
            throw new InvalidOperationException("SourceHeadChanged: actor metadata changed during stream rehydration.");
        }
    }

    private static partial class Log {
        [LoggerMessage(
            EventId = 6000,
            Level = LogLevel.Debug,
            Message = "Rehydration complete (snapshot-only, no events): TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, ElapsedMs={ElapsedMs}, Stage=RehydrationSnapshotOnly")]
        public static partial void RehydrationCompleteSnapshotOnly(
            ILogger logger,
            string tenantId,
            string domain,
            string aggregateId,
            long elapsedMs);

        [LoggerMessage(
            EventId = 6001,
            Level = LogLevel.Debug,
            Message = "New aggregate detected: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, Stage=NewAggregateDetected")]
        public static partial void NewAggregateDetected(
            ILogger logger,
            string tenantId,
            string domain,
            string aggregateId);

        [LoggerMessage(
            EventId = 6002,
            Level = LogLevel.Debug,
            Message = "Rehydration complete (snapshot at current sequence, no tail events): TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, ElapsedMs={ElapsedMs}, Stage=RehydrationSnapshotAtCurrent")]
        public static partial void RehydrationCompleteSnapshotAtCurrent(
            ILogger logger,
            string tenantId,
            string domain,
            string aggregateId,
            long elapsedMs);

        [LoggerMessage(
            EventId = 6003,
            Level = LogLevel.Debug,
            Message = "State rehydrated: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, Mode={Mode}, EventCount={EventCount}, ElapsedMs={ElapsedMs}, Stage=StateRehydrated")]
        public static partial void StateRehydrated(
            ILogger logger,
            string tenantId,
            string domain,
            string aggregateId,
            string mode,
            int eventCount,
            long elapsedMs);
    }
}
