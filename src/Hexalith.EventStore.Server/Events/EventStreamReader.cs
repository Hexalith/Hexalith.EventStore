
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
    private const int MaxConcurrentStateReads = 32;

    /// <inheritdoc/>
    public async Task<RehydrationResult?> RehydrateAsync(AggregateIdentity identity, SnapshotRecord? snapshot = null) {
        ArgumentNullException.ThrowIfNull(identity);

        var sw = Stopwatch.StartNew();

        // Load metadata to get current sequence number
        ConditionalValue<AggregateMetadata> metadataResult;
        try {
            metadataResult = await stateManager
                .TryGetStateAsync<AggregateMetadata>(identity.MetadataKey)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            throw new EventDeserializationException(-1, identity.ActorId, ex);
        }

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

        // Determine read range based on snapshot presence
        int startSequence;
        int eventCount;

        if (snapshot is not null) {
            // AC #8: snapshot at current sequence -- no tail events needed
            if (snapshot.SequenceNumber >= currentSequence) {
                sw.Stop();
                Log.RehydrationCompleteSnapshotAtCurrent(logger, identity.TenantId, identity.Domain, identity.AggregateId, sw.ElapsedMilliseconds);

                return new RehydrationResult(
                    SnapshotState: snapshot.State,
                    Events: [],
                    LastSnapshotSequence: lastSnapshotSequence,
                    CurrentSequence: currentSequence);
            }

            // AC #4: read only tail events from snapshot.SequenceNumber + 1
            startSequence = checked((int)(snapshot.SequenceNumber + 1));
            eventCount = checked((int)(currentSequence - snapshot.SequenceNumber));
        }
        else {
            // AC #3: no snapshot, full replay from sequence 1
            startSequence = 1;
            eventCount = checked((int)currentSequence);
        }

        // Load events using parallel reads (F5: NFR6 performance)
        string keyPrefix = identity.EventStreamKeyPrefix;

        var events = new List<EventEnvelope>(eventCount);
        int cursor = startSequence;
        int endExclusive = startSequence + eventCount;

        while (cursor < endExclusive) {
            int batchSize = Math.Min(MaxConcurrentStateReads, endExclusive - cursor);
            Task<(int Sequence, EventEnvelope Event)>[] loadTasks = Enumerable.Range(cursor, batchSize)
                .Select(async seq => {
                    ConditionalValue<EventEnvelope> eventResult;
                    try {
                        eventResult = await stateManager
                            .TryGetStateAsync<EventEnvelope>($"{keyPrefix}{seq}")
                            .ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException) {
                        throw new EventDeserializationException(seq, identity.ActorId, ex);
                    }

                    if (!eventResult.HasValue) {
                        throw new MissingEventException(seq, identity.TenantId, identity.Domain, identity.AggregateId);
                    }

                    return (Sequence: seq, Event: eventResult.Value);
                })
                .ToArray();

            (int Sequence, EventEnvelope Event)[] loadedBatch = await Task.WhenAll(loadTasks).ConfigureAwait(false);
            foreach ((int _, EventEnvelope evt) in loadedBatch.OrderBy(x => x.Sequence)) {
                events.Add(evt);
            }

            cursor += batchSize;
        }

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