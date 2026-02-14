namespace Hexalith.EventStore.Server.Events;

using System.Diagnostics;

using Dapr.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Telemetry;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Publishes dead-letter messages to per-tenant dead-letter topics via DAPR pub/sub.
/// Returns false for publication failures and propagates OperationCanceledException for cancellation.
/// No custom retry logic -- DAPR resiliency handles transient failures (rule #4).
/// SEC-5: Never logs command payload or event payload data.
/// </summary>
public class DeadLetterPublisher(
    DaprClient daprClient,
    IOptions<EventPublisherOptions> options,
    ILogger<DeadLetterPublisher> logger) : IDeadLetterPublisher
{
    /// <inheritdoc/>
    public async Task<bool> PublishDeadLetterAsync(
        AggregateIdentity identity,
        DeadLetterMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(message);

        string deadLetterTopic = options.Value.GetDeadLetterTopic(identity);

        using Activity? activity = EventStoreActivitySource.Instance.StartActivity(
            EventStoreActivitySource.EventsPublishDeadLetter);
        activity?.SetTag(EventStoreActivitySource.TagCorrelationId, message.CorrelationId);
        activity?.SetTag(EventStoreActivitySource.TagTenantId, identity.TenantId);
        activity?.SetTag(EventStoreActivitySource.TagDomain, identity.Domain);
        activity?.SetTag(EventStoreActivitySource.TagAggregateId, identity.AggregateId);
        activity?.SetTag(EventStoreActivitySource.TagCommandType, message.CommandType);
        activity?.SetTag(EventStoreActivitySource.TagFailureStage, message.FailureStage);
        activity?.SetTag(EventStoreActivitySource.TagExceptionType, message.ExceptionType);
        activity?.SetTag(EventStoreActivitySource.TagDeadLetterTopic, deadLetterTopic);

        try
        {
            var metadata = new Dictionary<string, string>
            {
                ["cloudevent.type"] = "deadletter.command.failed",
                ["cloudevent.source"] = $"eventstore/{identity.TenantId}/{identity.Domain}",
                ["cloudevent.id"] = message.CorrelationId,
                ["cloudevent.datacontenttype"] = "application/json",
            };

            await daprClient.PublishEventAsync(
                options.Value.PubSubName,
                deadLetterTopic,
                message,
                metadata,
                cancellationToken).ConfigureAwait(false);

            // Rule #5: Never log command payload or event payload data (SEC-5).
            // Rule #9: correlationId in every structured log entry.
            logger.LogWarning(
                "Dead-letter published: CorrelationId={CorrelationId}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, CommandType={CommandType}, FailureStage={FailureStage}, ExceptionType={ExceptionType}, ErrorMessage={ErrorMessage}, DeadLetterTopic={DeadLetterTopic}",
                message.CorrelationId,
                identity.TenantId,
                identity.Domain,
                identity.AggregateId,
                message.CommandType,
                message.FailureStage,
                message.ExceptionType,
                message.ErrorMessage,
                deadLetterTopic);

            activity?.SetStatus(ActivityStatusCode.Ok);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Dead-letter publication failure is non-blocking (AC #7).
            // Rule #5: Never log command payload data.
            logger.LogError(
                ex,
                "Dead-letter publication failed: CorrelationId={CorrelationId}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, CommandType={CommandType}, FailureStage={FailureStage}, ExceptionType={ExceptionType}, ErrorMessage={ErrorMessage}, DeadLetterTopic={DeadLetterTopic}",
                message.CorrelationId,
                identity.TenantId,
                identity.Domain,
                identity.AggregateId,
                message.CommandType,
                message.FailureStage,
                message.ExceptionType,
                message.ErrorMessage,
                deadLetterTopic);

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return false;
        }
    }
}
