namespace Hexalith.EventStore.Server.Events;

using System.Diagnostics;

using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Identity;

using Microsoft.Extensions.Logging;

/// <summary>
/// Reads events from the actor state store and rehydrates aggregate state by replaying all events.
/// Created per-call (not DI-registered) -- same pattern as IdempotencyChecker and TenantValidator.
/// </summary>
public class EventStreamReader(
    IActorStateManager stateManager,
    ILogger<EventStreamReader> logger) : IEventStreamReader
{
    /// <inheritdoc/>
    public async Task<object?> RehydrateAsync(AggregateIdentity identity)
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
            logger.LogDebug("New aggregate detected: no events found for {ActorId}", identity.ActorId);
            return null; // AC #1: new aggregate
        }

        AggregateMetadata metadata = metadataResult.Value;
        if (metadata.CurrentSequence <= 0)
        {
            throw new InvalidOperationException(
                $"Invalid aggregate metadata: CurrentSequence={metadata.CurrentSequence} for {identity.ActorId}");
        }

        // Load all events from sequence 1 to currentSequence using parallel reads (F5: NFR6 performance)
        string keyPrefix = identity.EventStreamKeyPrefix;
        int eventCount = checked((int)metadata.CurrentSequence);

        var loadTasks = Enumerable.Range(1, eventCount)
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

        // Sort by sequence to ensure strict order
        var events = loadedEvents
            .OrderBy(x => x.Sequence)
            .Select(x => x.Event)
            .ToList();

        sw.Stop();
        logger.LogDebug(
            "State rehydrated: {EventCount} events in {ElapsedMs}ms for {ActorId}",
            events.Count,
            sw.ElapsedMilliseconds,
            identity.ActorId);

        // F4: For Story 3.4, return the list of events as the "state"
        // Stories 3.5+ will apply domain-specific state reconstruction
        return events;
    }
}
