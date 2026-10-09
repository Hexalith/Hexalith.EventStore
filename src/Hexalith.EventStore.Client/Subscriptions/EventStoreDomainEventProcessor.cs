using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;

using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Serialization;

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
/// hand-wrote (e.g. <c>TenantEventProcessor</c>). Deduplication is delegated to
/// <see cref="IEventStoreDomainEventMarkerStore"/> and keyed by the EventStore event message ID.
/// </remarks>
public class EventStoreDomainEventProcessor {
    private readonly record struct PayloadIdPropertyKey(Type EventType, string PropertyName);

    private static readonly MethodInfo s_dispatchMethod = typeof(EventStoreDomainEventProcessor)
        .GetMethod(nameof(DispatchAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly ConcurrentDictionary<PayloadIdPropertyKey, PropertyInfo?> s_payloadIdProperties = new();

    private readonly IReadOnlyDictionary<string, Type> _eventTypeRegistry;
    private readonly ILogger<EventStoreDomainEventProcessor> _logger;
    private readonly IEventStoreDomainEventMarkerStore _markerStore;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly string? _payloadAggregateIdPropertyName;
    private readonly EventPayloadEvolutionRegistry _evolution;

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
        string? payloadAggregateIdPropertyName = null)
        : this(
            serviceScopeFactory,
            eventTypeRegistry,
            new InMemoryEventStoreDomainEventMarkerStore(),
            logger,
            payloadAggregateIdPropertyName) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EventStoreDomainEventProcessor"/> class.
    /// </summary>
    /// <param name="serviceScopeFactory">The service scope factory for resolving event handlers.</param>
    /// <param name="eventTypeRegistry">The mapping of event type names to their CLR types.</param>
    /// <param name="markerStore">The marker store used for consumed-message idempotency.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="payloadAggregateIdPropertyName">
    /// Optional payload property name whose value must equal the envelope's
    /// <see cref="EventStoreDomainEventEnvelope.AggregateId"/>; <see langword="null"/> disables the check.
    /// </param>
    /// <param name="evolution">The validated event evolution registry, when configured.</param>
    public EventStoreDomainEventProcessor(
        IServiceScopeFactory serviceScopeFactory,
        IReadOnlyDictionary<string, Type> eventTypeRegistry,
        IEventStoreDomainEventMarkerStore markerStore,
        ILogger<EventStoreDomainEventProcessor> logger,
        string? payloadAggregateIdPropertyName = null,
        EventPayloadEvolutionRegistry? evolution = null) {
        ArgumentNullException.ThrowIfNull(serviceScopeFactory);
        ArgumentNullException.ThrowIfNull(eventTypeRegistry);
        ArgumentNullException.ThrowIfNull(markerStore);
        ArgumentNullException.ThrowIfNull(logger);
        _serviceScopeFactory = serviceScopeFactory;
        _eventTypeRegistry = eventTypeRegistry;
        _markerStore = markerStore;
        _logger = logger;
        _payloadAggregateIdPropertyName = string.IsNullOrWhiteSpace(payloadAggregateIdPropertyName)
            ? null
            : payloadAggregateIdPropertyName;
        _evolution = evolution ?? new EventPayloadEvolutionRegistry(eventTypeRegistry.Values, []);
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
        cancellationToken.ThrowIfCancellationRequested();

        // Metadata must be admitted before a completed marker can hide an incompatible
        // redelivery. This legacy route cannot verify an effective versioned view.
        if (envelope.MetadataVersion is not (null or 1)
            || envelope.EventContractType is not null || envelope.PayloadVersion is < 1 or > 1024
            || envelope.StoredEventContractType is not null || envelope.StoredPayloadVersion is not null
            || envelope.StoredSerializationFormat is not null || envelope.StoredEventTypeName is not null
            || envelope.StoredDigest is not null || envelope.RegistryFingerprint is not null || envelope.IsAdapted is not null
            || envelope.EffectiveEventContractType is not null || envelope.EffectivePayloadVersion is not null
            || envelope.EffectiveSerializationFormat is not null || envelope.EffectivePayload is not null
            || envelope.ReadableSerializationFormat is not null || envelope.ReadableExtensions is not null
            || envelope.VerifiedEffectiveEvent is not null) {
            return EventStoreDomainEventProcessingResult.RetryableCapabilityMismatch;
        }

        try
        {
            EventIdentityValidator.ValidateSubscription(envelope.TenantId, envelope.Domain, envelope.AggregateId,
                envelope.AggregateType, envelope.EventTypeName, envelope.MessageId,
                envelope.CorrelationId, envelope.CausationId, envelope.SequenceNumber);
        }
        catch (EventIdentityValidationException error)
        {
            _logger.LogWarning("Retrying domain event with invalid identity component {ComponentName}.", error.ComponentName);
            return EventStoreDomainEventProcessingResult.RetryableCapabilityMismatch;
        }

        if (envelope.Payload is null)
        {
            return EventStoreDomainEventProcessingResult.RetryableCapabilityMismatch;
        }

        EventStoreDomainEventMarkerAcquisitionResult acquisition = await _markerStore
            .TryAcquireAsync(envelope.MessageId, cancellationToken)
            .ConfigureAwait(false);
        switch (acquisition) {
            case EventStoreDomainEventMarkerAcquisitionResult.Acquired:
                break;
            case EventStoreDomainEventMarkerAcquisitionResult.Completed:
                _logger.LogDebug("Skipping duplicate event {MessageId}", envelope.MessageId);
                return EventStoreDomainEventProcessingResult.Duplicate;
            case EventStoreDomainEventMarkerAcquisitionResult.InProgress:
                _logger.LogWarning("Event {MessageId} is already being processed; keeping delivery retryable.", envelope.MessageId);
                return EventStoreDomainEventProcessingResult.RetryableInProgress;
            case EventStoreDomainEventMarkerAcquisitionResult.CompletionPending:
                await MarkCompletedStrictAsync(envelope.MessageId).ConfigureAwait(false);

                // Information rather than Debug: this branch only runs after an earlier attempt dispatched
                // handlers and failed to complete its marker. It is the signal an operator needs when
                // draining Dispatched markers before a rollback, and Debug is off in production.
                _logger.LogInformation("Completed previously dispatched event {MessageId} without redispatching handlers.", envelope.MessageId);
                return EventStoreDomainEventProcessingResult.Duplicate;
            default:
                _logger.LogWarning(
                    "Marker store returned unsupported acquisition result {AcquisitionResult} for event {MessageId}; keeping delivery retryable.",
                    acquisition,
                    envelope.MessageId);
                return EventStoreDomainEventProcessingResult.RetryableInProgress;
        }

        bool releaseMarkerOnFailure = true;

        try {
            if (!ValidateEnvelopeAfterAcquisition(envelope)) {
                _logger.LogWarning("Skipping invalid domain-event envelope with missing or unsupported metadata.");
                await ReleaseSafelyAsync(envelope.MessageId).ConfigureAwait(false);
                return EventStoreDomainEventProcessingResult.FailedInvalidPayload;
            }

            if (!IsSupportedSerializationFormat(envelope.SerializationFormat)) {
                _logger.LogWarning(
                    "Skipping event {MessageId}: unsupported serialization format '{SerializationFormat}'",
                    envelope.MessageId,
                    envelope.SerializationFormat);
                await MarkCompletedSafelyAsync(envelope.MessageId).ConfigureAwait(false);
                return EventStoreDomainEventProcessingResult.FailedInvalidPayload;
            }

            ResolvedEventPayload resolved;
            try
            {
                resolved = _evolution.Read(envelope.EventTypeName, envelope.PayloadVersion, envelope.Payload,
                    envelope.SequenceNumber);
            }
            catch (EventPayloadEvolutionException error)
            {
                _logger.LogWarning("Cannot read known event {EventTypeName} version {PayloadVersion} at sequence {SequenceNumber}; UpcasterType={UpcasterType}",
                    error.EventTypeName, error.StoredVersion, error.SequenceNumber, error.UpcasterTypeName);
                await ReleaseSafelyAsync(envelope.MessageId).ConfigureAwait(false);
                return EventStoreDomainEventProcessingResult.RetryableCapabilityMismatch;
            }

            Type? eventType = resolved.EventType;
            if (eventType is null && !_eventTypeRegistry.TryGetValue(envelope.EventTypeName, out eventType)) {
                _logger.LogWarning("Unknown event type '{EventTypeName}' — skipping", envelope.EventTypeName);
                await MarkCompletedSafelyAsync(envelope.MessageId).ConfigureAwait(false);
                return EventStoreDomainEventProcessingResult.SkippedUnknownEventType;
            }

            object? deserialized;
            try {
                deserialized = JsonSerializer.Deserialize(resolved.Payload, eventType, EventStorePayloadSerialization.Options);
            }
            catch (JsonException exception) {
                _logger.LogWarning(
                    "Failed to deserialize event {MessageId} as {EventTypeName}; ExceptionType={ExceptionType}",
                    envelope.MessageId,
                    envelope.EventTypeName,
                    exception.GetType().Name);
                await ReleaseSafelyAsync(envelope.MessageId).ConfigureAwait(false);
                return EventStoreDomainEventProcessingResult.RetryableCapabilityMismatch;
            }
            catch (NotSupportedException exception) {
                _logger.LogWarning(
                    "Failed to deserialize event {MessageId} as {EventTypeName}; ExceptionType={ExceptionType}",
                    envelope.MessageId,
                    envelope.EventTypeName,
                    exception.GetType().Name);
                await ReleaseSafelyAsync(envelope.MessageId).ConfigureAwait(false);
                return EventStoreDomainEventProcessingResult.RetryableCapabilityMismatch;
            }

            if (deserialized is not IEventPayload @event) {
                _logger.LogWarning("Failed to deserialize event {MessageId} as {EventTypeName}", envelope.MessageId, envelope.EventTypeName);
                await ReleaseSafelyAsync(envelope.MessageId).ConfigureAwait(false);
                return EventStoreDomainEventProcessingResult.RetryableCapabilityMismatch;
            }

            if (_payloadAggregateIdPropertyName is not null
                && (!TryGetPayloadId(@event, _payloadAggregateIdPropertyName, out string? payloadId)
                    || !string.Equals(payloadId, envelope.AggregateId, StringComparison.Ordinal))) {
                // The payload's identity property does not match the stream the event was delivered on.
                // This is expected when a single pub/sub topic carries events from multiple aggregate types
                // whose identity conventions differ (e.g. a tenants topic carrying both tenant-aggregate
                // events, where payload TenantId == AggregateId, and global-administrators events, where it
                // does not). Treat it as a terminal skip — the event is not addressed to this projection —
                // and mark it completed so it is acknowledged and not redelivered. Logged at Information
                // rather than Warning because on a shared topic this is routine; a genuine integrity
                // violation is also skipped here (never dispatched), so it cannot corrupt downstream state.
                _logger.LogInformation(
                    "Skipping event {MessageId}: payload '{PropertyName}' does not match aggregate ID '{AggregateId}'",
                    envelope.MessageId,
                    _payloadAggregateIdPropertyName,
                    envelope.AggregateId);
                await MarkCompletedSafelyAsync(envelope.MessageId).ConfigureAwait(false);
                return EventStoreDomainEventProcessingResult.SkippedAggregateMismatch;
            }

            var context = new EventStoreDomainEventContext(
                envelope.TenantId,
                envelope.AggregateId,
                envelope.MessageId,
                envelope.SequenceNumber,
                envelope.Timestamp,
                envelope.CorrelationId) {
                Domain = envelope.Domain,
                GlobalPosition = envelope.GlobalPosition,
                CausationId = envelope.CausationId,
                UserId = envelope.UserId,
            };

            MethodInfo genericDispatch = s_dispatchMethod.MakeGenericMethod(eventType);
            using IServiceScope scope = _serviceScopeFactory.CreateScope();
            int handlerCount = await ((Task<int>)genericDispatch.Invoke(null, [scope.ServiceProvider, @event, context, cancellationToken])!).ConfigureAwait(false);
            if (handlerCount == 0) {
                _logger.LogWarning("No handlers registered for event type '{EventTypeName}' — skipping", envelope.EventTypeName);
                await MarkCompletedSafelyAsync(envelope.MessageId).ConfigureAwait(false);
                return EventStoreDomainEventProcessingResult.SkippedNoHandlers;
            }

            // Once handlers have completed successfully, do not delete the marker if the completion write
            // fails. Releasing here would let a redelivery run side effects a second time.
            releaseMarkerOnFailure = false;
            bool completionPending = await MarkDispatchedStrictAsync(envelope.MessageId).ConfigureAwait(false);
            if (completionPending) {
                await MarkCompletedStrictAsync(envelope.MessageId).ConfigureAwait(false);
            }

            return EventStoreDomainEventProcessingResult.Processed;
        }
        catch {
            if (releaseMarkerOnFailure) {
                await ReleaseSafelyAsync(envelope.MessageId).ConfigureAwait(false);
            }

            throw;
        }
    }

    private static bool ValidateEnvelopeAfterAcquisition(EventStoreDomainEventEnvelope envelope)
        => !string.IsNullOrWhiteSpace(envelope.AggregateId)
        && !string.IsNullOrWhiteSpace(envelope.TenantId)
        && !string.IsNullOrWhiteSpace(envelope.EventTypeName)
        && !string.IsNullOrWhiteSpace(envelope.CorrelationId)
        && !string.IsNullOrWhiteSpace(envelope.SerializationFormat)
        && envelope.Payload is { Length: > 0 };

    private static bool IsSupportedSerializationFormat(string serializationFormat)
        => string.Equals(serializationFormat, "json", StringComparison.OrdinalIgnoreCase);

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
        var key = new PayloadIdPropertyKey(@event.GetType(), propertyName);
        PropertyInfo? property = s_payloadIdProperties.GetOrAdd(
            key,
            static key => key.EventType.GetProperty(key.PropertyName, BindingFlags.Instance | BindingFlags.Public));

        id = property?.GetValue(@event) as string;
        return !string.IsNullOrWhiteSpace(id);
    }

    private async Task MarkCompletedSafelyAsync(string messageId) {
        try {
            // Terminal completions acknowledge the message regardless of the current delivery's cancellation.
            // Tying the completion write to the request token could leave the marker un-completed after a
            // client/sidecar abort — on the in-memory store the key stays InProgress, so every redelivery
            // returns RetryableInProgress (500) and wedges a terminal skip into a poison loop. Always
            // complete with CancellationToken.None, matching the successful-dispatch path.
            await _markerStore.MarkCompletedAsync(messageId, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) {
            _logger.LogWarning(
                "Failed to mark domain event {MessageId} as completed; ExceptionType={ExceptionType}",
                messageId,
                exception.GetType().Name);
        }
    }

    private async Task<bool> MarkDispatchedStrictAsync(string messageId) {
        try {
            return await _markerStore.MarkDispatchedAsync(messageId, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) {
            _logger.LogWarning(
                "Failed to mark domain event {MessageId} as dispatched; ExceptionType={ExceptionType}",
                messageId,
                exception.GetType().Name);
            throw;
        }
    }

    private async Task MarkCompletedStrictAsync(string messageId) {
        try {
            await _markerStore.MarkCompletedAsync(messageId, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) {
            _logger.LogWarning(
                "Failed to mark dispatched domain event {MessageId} as completed; ExceptionType={ExceptionType}",
                messageId,
                exception.GetType().Name);
            throw;
        }
    }

    private async Task ReleaseSafelyAsync(string messageId) {
        try {
            await _markerStore.ReleaseAsync(messageId, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) {
            _logger.LogWarning(
                "Failed to release domain event marker {MessageId}; ExceptionType={ExceptionType}",
                messageId,
                exception.GetType().Name);
        }
    }
}
