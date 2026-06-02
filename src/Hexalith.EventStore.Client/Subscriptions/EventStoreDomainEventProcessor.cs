using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Events;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Client.Subscriptions;

/// <summary>
/// Receives domain-event envelopes, resolves the event type, deserializes the payload, deduplicates by
/// message ID, optionally validates payload/aggregate consistency, and dispatches to registered
/// <see cref="IEventStoreDomainEventHandler{TEvent}"/> implementations.
/// </summary>
/// <remarks>
/// This is the platform generalization of the per-domain event processors domain modules previously
/// hand-wrote (e.g. <c>TenantEventProcessor</c>). The deduplication set grows unboundedly; consuming
/// services are typically restarted periodically. Production deployments that cannot tolerate that should
/// front this with a bounded/external deduplication store.
/// </remarks>
public class EventStoreDomainEventProcessor {
    private enum ProcessingState {
        InProgress,
        Completed,
    }

    private static readonly MethodInfo s_dispatchMethod = typeof(EventStoreDomainEventProcessor)
        .GetMethod(nameof(DispatchAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly ConcurrentDictionary<Type, PropertyInfo?> s_payloadIdProperties = new();

    private readonly IReadOnlyDictionary<string, Type> _eventTypeRegistry;
    private readonly ILogger<EventStoreDomainEventProcessor> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly string? _payloadAggregateIdPropertyName;

    private readonly ConcurrentDictionary<string, ProcessingState> _processedMessageIds = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="EventStoreDomainEventProcessor"/> class.
    /// </summary>
    /// <param name="serviceScopeFactory">The service scope factory for resolving event handlers.</param>
    /// <param name="eventTypeRegistry">The mapping of event type names to their CLR types.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="payloadAggregateIdPropertyName">
    /// Optional payload property name whose value must equal the envelope's
    /// <see cref="EventStoreDomainEventEnvelope.AggregateId"/>; <see langword="null"/> disables the check.
    /// </param>
    public EventStoreDomainEventProcessor(
        IServiceScopeFactory serviceScopeFactory,
        IReadOnlyDictionary<string, Type> eventTypeRegistry,
        ILogger<EventStoreDomainEventProcessor> logger,
        string? payloadAggregateIdPropertyName = null) {
        ArgumentNullException.ThrowIfNull(serviceScopeFactory);
        ArgumentNullException.ThrowIfNull(eventTypeRegistry);
        ArgumentNullException.ThrowIfNull(logger);
        _serviceScopeFactory = serviceScopeFactory;
        _eventTypeRegistry = eventTypeRegistry;
        _logger = logger;
        _payloadAggregateIdPropertyName = string.IsNullOrWhiteSpace(payloadAggregateIdPropertyName)
            ? null
            : payloadAggregateIdPropertyName;
    }

    /// <summary>
    /// Processes a domain-event envelope: deduplicates, resolves the type, deserializes, optionally
    /// validates payload/aggregate consistency, and dispatches to handlers.
    /// </summary>
    /// <param name="envelope">The domain-event envelope.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The event processing outcome.</returns>
    public async Task<EventStoreDomainEventProcessingResult> ProcessAsync(
        EventStoreDomainEventEnvelope envelope,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(envelope);

        if (!_processedMessageIds.TryAdd(envelope.MessageId, ProcessingState.InProgress)) {
            _logger.LogDebug("Skipping duplicate event {MessageId}", envelope.MessageId);
            return EventStoreDomainEventProcessingResult.Duplicate;
        }

        try {
            if (!_eventTypeRegistry.TryGetValue(envelope.EventTypeName, out Type? eventType)) {
                _logger.LogWarning("Unknown event type '{EventTypeName}' — skipping", envelope.EventTypeName);
                _processedMessageIds[envelope.MessageId] = ProcessingState.Completed;
                return EventStoreDomainEventProcessingResult.SkippedUnknownEventType;
            }

            object? deserialized;
            try {
                deserialized = JsonSerializer.Deserialize(envelope.Payload, eventType);
            }
            catch (JsonException exception) {
                _logger.LogWarning(exception, "Failed to deserialize event {MessageId} as {EventTypeName}", envelope.MessageId, envelope.EventTypeName);
                _ = _processedMessageIds.TryRemove(envelope.MessageId, out _);
                return EventStoreDomainEventProcessingResult.FailedInvalidPayload;
            }
            catch (NotSupportedException exception) {
                _logger.LogWarning(exception, "Failed to deserialize event {MessageId} as {EventTypeName}", envelope.MessageId, envelope.EventTypeName);
                _ = _processedMessageIds.TryRemove(envelope.MessageId, out _);
                return EventStoreDomainEventProcessingResult.FailedInvalidPayload;
            }

            if (deserialized is not IEventPayload @event) {
                _logger.LogWarning("Failed to deserialize event {MessageId} as {EventTypeName}", envelope.MessageId, envelope.EventTypeName);
                _ = _processedMessageIds.TryRemove(envelope.MessageId, out _);
                return EventStoreDomainEventProcessingResult.FailedInvalidPayload;
            }

            if (_payloadAggregateIdPropertyName is not null
                && (!TryGetPayloadId(@event, _payloadAggregateIdPropertyName, out string? payloadId)
                    || !string.Equals(payloadId, envelope.AggregateId, StringComparison.Ordinal))) {
                _logger.LogWarning(
                    "Rejected event {MessageId} because payload '{PropertyName}' does not match aggregate ID",
                    envelope.MessageId,
                    _payloadAggregateIdPropertyName);
                _ = _processedMessageIds.TryRemove(envelope.MessageId, out _);
                return EventStoreDomainEventProcessingResult.FailedInvalidPayload;
            }

            var context = new EventStoreDomainEventContext(
                envelope.TenantId,
                envelope.AggregateId,
                envelope.MessageId,
                envelope.SequenceNumber,
                envelope.Timestamp,
                envelope.CorrelationId);

            MethodInfo genericDispatch = s_dispatchMethod.MakeGenericMethod(eventType);
            using IServiceScope scope = _serviceScopeFactory.CreateScope();
            int handlerCount = await ((Task<int>)genericDispatch.Invoke(null, [scope.ServiceProvider, @event, context, cancellationToken])!).ConfigureAwait(false);
            if (handlerCount == 0) {
                _logger.LogWarning("No handlers registered for event type '{EventTypeName}' — skipping", envelope.EventTypeName);
                _processedMessageIds[envelope.MessageId] = ProcessingState.Completed;
                return EventStoreDomainEventProcessingResult.SkippedNoHandlers;
            }

            _processedMessageIds[envelope.MessageId] = ProcessingState.Completed;
            return EventStoreDomainEventProcessingResult.Processed;
        }
        catch {
            _ = _processedMessageIds.TryRemove(envelope.MessageId, out _);
            throw;
        }
    }

    private static async Task<int> DispatchAsync<TEvent>(
        IServiceProvider serviceProvider,
        TEvent @event,
        EventStoreDomainEventContext context,
        CancellationToken cancellationToken)
        where TEvent : IEventPayload {
        IEventStoreDomainEventHandler<TEvent>[] handlers = serviceProvider.GetServices<IEventStoreDomainEventHandler<TEvent>>().ToArray();
        foreach (IEventStoreDomainEventHandler<TEvent> handler in handlers) {
            await handler.HandleAsync(@event, context, cancellationToken).ConfigureAwait(false);
        }

        return handlers.Length;
    }

    private static bool TryGetPayloadId(IEventPayload @event, string propertyName, out string? id) {
        PropertyInfo? property = s_payloadIdProperties.GetOrAdd(
            @event.GetType(),
            static (eventType, name) => eventType.GetProperty(name, BindingFlags.Instance | BindingFlags.Public),
            propertyName);

        id = property?.GetValue(@event) as string;
        return !string.IsNullOrWhiteSpace(id);
    }
}
