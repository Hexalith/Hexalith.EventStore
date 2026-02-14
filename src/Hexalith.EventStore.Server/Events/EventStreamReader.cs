namespace Hexalith.EventStore.Server.Events;

using System.Diagnostics;

using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Identity;

using Microsoft.Extensions.Logging;

/// <summary>
/// Reads events from the actor state store and rehydrates aggregate state.
/// Supports snapshot-aware rehydration: when a snapshot is provided, only tail events
/// after the snapshot sequence are loaded (Story 3.10).
/// Created per-call (not DI-registered) -- same pattern as IdempotencyChecker and TenantValidator.
/// </summary>
public class EventStreamReader(
    IActorStateManager stateManager,
    ILogger<EventStreamReader> logger) : IEventStreamReader
{
    /// <inheritdoc/>
    public async Task<RehydrationResult?> RehydrateAsync(AggregateIdentity identity, SnapshotRecord? snapshot = null)
    {
        ArgumentNullException.ThrowIfNull(identity);

        var sw = Stopwatch.StartNew();

        // Load metadata to get current sequence number
        ConditionalValue<AggregateMetadata> metadataResult;
        try
        {
            metadataResult = await stateManager
                .TryGetStateAsync<AggregateMetadata>(identity.MetadataKey)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new EventDeserializationException(-1, identity.ActorId, ex);
        }

        if (!metadataResult.HasValue)
        {
            // AC #3 / AC #8: new aggregate with no events
            if (snapshot is not null)
            {
                // Snapshot exists but no events -- return snapshot state directly
                sw.Stop();
                logger.LogDebug(
                    "Rehydration complete (snapshot-only, no events): {ElapsedMs}ms for {ActorId}",
                    sw.ElapsedMilliseconds,
                    identity.ActorId);

                return new RehydrationResult(
                    SnapshotState: snapshot.State,
                    Events: [],
                    LastSnapshotSequence: snapshot.SequenceNumber,
                    CurrentSequence: snapshot.SequenceNumber);
            }

            logger.LogDebug("New aggregate detected: no events found for {ActorId}", identity.ActorId);
            return null; // New aggregate, no snapshot, no events
        }

        AggregateMetadata metadata = metadataResult.Value;
        if (metadata.CurrentSequence <= 0)
        {
            throw new InvalidOperationException(
                $"Invalid aggregate metadata: CurrentSequence={metadata.CurrentSequence} for {identity.ActorId}");
        }

        long currentSequence = metadata.CurrentSequence;
        long lastSnapshotSequence = snapshot?.SequenceNumber ?? 0;

        // Determine read range based on snapshot presence
        int startSequence;
        int eventCount;

        if (snapshot is not null)
        {
            // AC #8: snapshot at current sequence -- no tail events needed
            if (snapshot.SequenceNumber >= currentSequence)
            {
                sw.Stop();
                logger.LogDebug(
                    "Rehydration complete (snapshot at current sequence, no tail events): {ElapsedMs}ms for {ActorId}",
                    sw.ElapsedMilliseconds,
                    identity.ActorId);

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
        else
        {
            // AC #3: no snapshot, full replay from sequence 1
            startSequence = 1;
            eventCount = checked((int)currentSequence);
        }

        // Load events using parallel reads (F5: NFR6 performance)
        string keyPrefix = identity.EventStreamKeyPrefix;

        var loadTasks = Enumerable.Range(startSequence, eventCount)
            .Select(async seq =>
            {
                ConditionalValue<EventEnvelope> eventResult;
                try
                {
                    eventResult = await stateManager
                        .TryGetStateAsync<EventEnvelope>($"{keyPrefix}{seq}")
                        .ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    throw new EventDeserializationException(seq, identity.ActorId, ex);
                }

                if (!eventResult.HasValue)
                {
                    throw new MissingEventException(seq, identity.TenantId, identity.Domain, identity.AggregateId);
                }

                return (Sequence: seq, Event: eventResult.Value);
            })
            .ToArray();

        var loadedEvents = await Task.WhenAll(loadTasks).ConfigureAwait(false);

        // Sort by sequence to ensure strict order (AC #9)
        var events = loadedEvents
            .OrderBy(x => x.Sequence)
            .Select(x => x.Event)
            .ToList();

        sw.Stop();

        // Log rehydration mode (AC #7 logging)
        string mode = snapshot is not null ? "snapshot+tail" : "full-replay";
        logger.LogDebug(
            "State rehydrated ({Mode}): {EventCount} events in {ElapsedMs}ms for {ActorId}",
            mode,
            events.Count,
            sw.ElapsedMilliseconds,
            identity.ActorId);

        return new RehydrationResult(
            SnapshotState: snapshot?.State,
            Events: events,
            LastSnapshotSequence: lastSnapshotSequence,
            CurrentSequence: currentSequence);
    }
}
