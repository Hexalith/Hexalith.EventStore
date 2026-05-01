
using System.Diagnostics;

using Dapr.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Telemetry;

using Microsoft.Extensions.Hosting;
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
    IProjectionUpdateOrchestrator projectionOrchestrator,
    ITopicNameValidator? topicNameValidator = null,
    IHostEnvironment? hostEnvironment = null) : IEventPublisher {
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
        EventPublisherOptions publisherOptions = options.Value;

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
            if (IsTestPublishFaultActive(publisherOptions, correlationId, hostEnvironment)) {
                string failureReason = $"Configured test publish fault is active for correlation id {correlationId}.";
                Log.TestPublishFaultInjected(logger, correlationId, identity.TenantId, identity.Domain, identity.AggregateId, topic);
                return new EventPublishResult(false, 0, failureReason);
            }

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

            // Fire-and-forget projection update (Mode B immediate trigger)
            // NOTE: Unbounded concurrency -- high-throughput aggregates may spawn many concurrent tasks.
            // Acceptable for current scope; checkpoint tracker + SemaphoreSlim would bound this in a follow-up.
            _ = Task.Run(async () => {
                try {
                    await projectionOrchestrator.UpdateProjectionAsync(identity, CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch (Exception ex) {
                    Log.ProjectionUpdateFailed(logger, ex, identity.TenantId, identity.Domain, identity.AggregateId, correlationId);
                }
            }, CancellationToken.None);

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

    private static bool IsTestPublishFaultActive(
        EventPublisherOptions options,
        string correlationId,
        IHostEnvironment? hostEnvironment) {
        // Test fault injection is gated on Development to keep the production binary inert
        // even if the configuration option is accidentally set in a non-Development environment.
        if (hostEnvironment is null || !hostEnvironment.IsDevelopment()) {
            return false;
        }

        if (string.IsNullOrWhiteSpace(options.TestPublishFaultFilePath)
            || !File.Exists(options.TestPublishFaultFilePath)) {
            return false;
        }

        return string.IsNullOrWhiteSpace(options.TestPublishFaultCorrelationIdPrefix)
            || (correlationId is not null
                && correlationId.StartsWith(options.TestPublishFaultCorrelationIdPrefix, StringComparison.Ordinal));
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

        [LoggerMessage(
            EventId = 3102,
            Level = LogLevel.Warning,
            Message = "Fire-and-forget projection update failed: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, CorrelationId={CorrelationId}, Stage=ProjectionUpdateFailed")]
        public static partial void ProjectionUpdateFailed(
            ILogger logger,
            Exception ex,
            string tenantId,
            string domain,
            string aggregateId,
            string correlationId);

        [LoggerMessage(
            EventId = 3103,
            Level = LogLevel.Warning,
            Message = "Test publish fault injected: CorrelationId={CorrelationId}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, Topic={Topic}, Stage=TestPublishFaultInjected")]
        public static partial void TestPublishFaultInjected(
            ILogger logger,
            string correlationId,
            string tenantId,
            string domain,
            string aggregateId,
            string topic);
    }
}
