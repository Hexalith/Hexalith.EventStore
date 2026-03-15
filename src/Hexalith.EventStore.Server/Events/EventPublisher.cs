
using System.Diagnostics;

using Dapr.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Telemetry;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Events;
/// <summary>
/// Publishes persisted events to DAPR pub/sub with CloudEvents 1.0 metadata (FR17).
/// Uses DaprClient.PublishEventAsync which natively wraps data in CloudEvents format.
/// No custom retry logic -- DAPR resiliency policies handle transient failures (rule #4).
/// </summary>
public partial class EventPublisher(
    DaprClient daprClient,
    IOptions<EventPublisherOptions> options,
    ILogger<EventPublisher> logger,
    IEventPayloadProtectionService payloadProtectionService,
    ITopicNameValidator? topicNameValidator = null) : IEventPublisher {
    /// <inheritdoc/>
    public async Task<EventPublishResult> PublishEventsAsync(
        AggregateIdentity identity,
        IReadOnlyList<EventEnvelope> events,
        string correlationId,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        if (events.Count == 0) {
            return new EventPublishResult(true, 0, null);
        }

        string pubSubName = options.Value.PubSubName;
        string topic = identity.PubSubTopic;

        if (topicNameValidator is not null && !topicNameValidator.IsValidTopicName(topic)) {
            logger.LogError(
                "Invalid topic name: CorrelationId={CorrelationId}, TenantId={TenantId}, Domain={Domain}, Topic={Topic}",
                correlationId,
                identity.TenantId,
                identity.Domain,
                topic);
            return new EventPublishResult(false, 0, $"Invalid topic name: {topic}");
        }

        // Extract CausationId from first event envelope (all events in batch share same CausationId)
        string causationId = events[0].CausationId ?? correlationId;

        using Activity? activity = EventStoreActivitySource.Instance.StartActivity(
            EventStoreActivitySource.EventsPublish, ActivityKind.Producer);
        _ = (activity?.SetTag(EventStoreActivitySource.TagCorrelationId, correlationId));
        _ = (activity?.SetTag(EventStoreActivitySource.TagTenantId, identity.TenantId));
        _ = (activity?.SetTag(EventStoreActivitySource.TagDomain, identity.Domain));
        _ = (activity?.SetTag(EventStoreActivitySource.TagAggregateId, identity.AggregateId));
        _ = (activity?.SetTag(EventStoreActivitySource.TagEventCount, events.Count));
        _ = (activity?.SetTag(EventStoreActivitySource.TagTopic, topic));

        long startTicks = Stopwatch.GetTimestamp();
        int publishedCount = 0;

        try {
            for (int i = 0; i < events.Count; i++) {
                EventEnvelope eventEnvelope = events[i];
                PayloadProtectionResult protectionResult = await payloadProtectionService
                    .UnprotectEventPayloadAsync(
                        identity,
                        eventEnvelope.EventTypeName,
                        eventEnvelope.Payload,
                        eventEnvelope.SerializationFormat,
                        cancellationToken)
                    .ConfigureAwait(false);

                EventEnvelope publishEnvelope = new(
                    MessageId: eventEnvelope.MessageId,
                    AggregateId: eventEnvelope.AggregateId,
                    AggregateType: eventEnvelope.AggregateType,
                    TenantId: eventEnvelope.TenantId,
                    Domain: eventEnvelope.Domain,
                    SequenceNumber: eventEnvelope.SequenceNumber,
                    GlobalPosition: eventEnvelope.GlobalPosition,
                    Timestamp: eventEnvelope.Timestamp,
                    CorrelationId: eventEnvelope.CorrelationId,
                    CausationId: eventEnvelope.CausationId,
                    UserId: eventEnvelope.UserId,
                    DomainServiceVersion: eventEnvelope.DomainServiceVersion,
                    EventTypeName: eventEnvelope.EventTypeName,
                    MetadataVersion: eventEnvelope.MetadataVersion,
                    SerializationFormat: protectionResult.SerializationFormat,
                    Payload: protectionResult.PayloadBytes,
                    Extensions: eventEnvelope.Extensions);

                var metadata = new Dictionary<string, string> {
                    ["cloudevent.type"] = eventEnvelope.EventTypeName,
                    ["cloudevent.source"] = $"hexalith-eventstore/{identity.TenantId}/{identity.Domain}",
                    ["cloudevent.id"] = $"{correlationId}:{eventEnvelope.SequenceNumber}",
                };

                await daprClient.PublishEventAsync(
                    pubSubName,
                    topic,
                    publishEnvelope,
                    metadata,
                    cancellationToken).ConfigureAwait(false);

                publishedCount++;
            }

            double durationMs = Stopwatch.GetElapsedTime(startTicks).TotalMilliseconds;

            // Rule #5: Never log event payload data -- only envelope metadata fields.
            // Rule #9: correlationId in every structured log entry.
            Log.EventsPublished(logger, correlationId, causationId, identity.TenantId, identity.Domain, identity.AggregateId, events.Count, topic, durationMs);

            _ = (activity?.SetStatus(ActivityStatusCode.Ok));
            return new EventPublishResult(true, publishedCount, null);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            // Rule #13: No stack traces in error responses.
            // Rule #5: Never log event payload data.
            Log.EventPublicationFailed(logger, ex, correlationId, causationId, identity.TenantId, identity.Domain, identity.AggregateId, topic, publishedCount, events.Count);

            _ = (activity?.AddException(ex));
            _ = (activity?.SetStatus(ActivityStatusCode.Error, ex.Message));
            return new EventPublishResult(false, publishedCount, ex.Message);
        }
    }

    private static partial class Log {
        [LoggerMessage(
            EventId = 3100,
            Level = LogLevel.Information,
            Message = "Events published: CorrelationId={CorrelationId}, CausationId={CausationId}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, EventCount={EventCount}, Topic={Topic}, DurationMs={DurationMs}, Stage=EventsPublished")]
        public static partial void EventsPublished(
            ILogger logger,
            string correlationId,
            string causationId,
            string tenantId,
            string domain,
            string aggregateId,
            int eventCount,
            string topic,
            double durationMs);

        [LoggerMessage(
            EventId = 3101,
            Level = LogLevel.Error,
            Message = "Event publication failed: CorrelationId={CorrelationId}, CausationId={CausationId}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, Topic={Topic}, PublishedCount={PublishedCount}, TotalCount={TotalCount}, Stage=EventPublicationFailed")]
        public static partial void EventPublicationFailed(
            ILogger logger,
            Exception ex,
            string correlationId,
            string causationId,
            string tenantId,
            string domain,
            string aggregateId,
            string topic,
            int publishedCount,
            int totalCount);
    }
}
