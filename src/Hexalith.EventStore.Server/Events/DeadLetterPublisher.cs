
using System.Diagnostics;

using Dapr.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Diagnostics;
using Hexalith.EventStore.Server.Telemetry;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Events;
/// <summary>
/// Publishes dead-letter messages to per-tenant dead-letter topics via DAPR pub/sub.
/// Returns false for publication failures and propagates OperationCanceledException for cancellation.
/// No custom retry logic -- DAPR resiliency handles transient failures (rule #4).
/// SEC-5: Never logs command payload or event payload data.
/// </summary>
public partial class DeadLetterPublisher(
    DaprClient daprClient,
    IOptions<EventPublisherOptions> options,
    ILogger<DeadLetterPublisher> logger) : IDeadLetterPublisher {
    /// <inheritdoc/>
    public async Task<bool> PublishDeadLetterAsync(
        AggregateIdentity identity,
        DeadLetterMessage message,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(message);

        string deadLetterTopic = options.Value.GetDeadLetterTopic(identity);
        string causationId = message.CausationId ?? message.CorrelationId;
        DeadLetterMessage safeMessage = message with {
            ErrorMessage = ProtectedDataDiagnosticRedactor.NormalizeDiagnosticText(
                message.ErrorMessage,
                message.FailureStage),
        };

        using Activity? activity = EventStoreActivitySource.Instance.StartActivity(
            EventStoreActivitySource.EventsPublishDeadLetter, ActivityKind.Producer);
        _ = (activity?.SetTag(EventStoreActivitySource.TagCorrelationId, safeMessage.CorrelationId));
        _ = (activity?.SetTag(EventStoreActivitySource.TagTenantId, identity.TenantId));
        _ = (activity?.SetTag(EventStoreActivitySource.TagDomain, identity.Domain));
        _ = (activity?.SetTag(EventStoreActivitySource.TagAggregateId, identity.AggregateId));
        _ = (activity?.SetTag(EventStoreActivitySource.TagCommandType, safeMessage.CommandType));
        _ = (activity?.SetTag(EventStoreActivitySource.TagFailureStage, safeMessage.FailureStage));
        _ = (activity?.SetTag(EventStoreActivitySource.TagExceptionType, safeMessage.ExceptionType));
        _ = (activity?.SetTag(EventStoreActivitySource.TagDeadLetterTopic, deadLetterTopic));

        try {
            var metadata = new Dictionary<string, string> {
                ["cloudevent.type"] = "deadletter.command.failed",
                ["cloudevent.source"] = $"eventstore/{identity.TenantId}/{identity.Domain}",
                ["cloudevent.id"] = safeMessage.CorrelationId,
                ["cloudevent.datacontenttype"] = "application/json",
            };

            await daprClient.PublishEventAsync(
                options.Value.PubSubName,
                deadLetterTopic,
                safeMessage,
                metadata,
                cancellationToken).ConfigureAwait(false);

            // Rule #5: Never log command payload or event payload data (SEC-5).
            // Rule #9: correlationId in every structured log entry.
            Log.DeadLetterPublished(logger, safeMessage.CorrelationId, causationId, identity.TenantId, identity.Domain, identity.AggregateId, safeMessage.CommandType, safeMessage.FailureStage, safeMessage.ExceptionType, safeMessage.ErrorMessage, deadLetterTopic);

            _ = (activity?.SetStatus(ActivityStatusCode.Ok));
            return true;
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            string safeFailureMessage = ProtectedDataDiagnosticRedactor.RedactException(ex, "dead-letter-publication");
            // Dead-letter publication failure is non-blocking (AC #7).
            // Rule #5: Never log command payload data.
            Log.DeadLetterPublicationFailed(logger, safeMessage.CorrelationId, causationId, identity.TenantId, identity.Domain, identity.AggregateId, safeMessage.CommandType, safeMessage.FailureStage, safeMessage.ExceptionType, safeMessage.ErrorMessage, deadLetterTopic, ex.GetType().Name, safeFailureMessage);

            ProtectedDataDiagnosticRedactor.RecordActivityException(activity, ex, "dead-letter-publication");
            return false;
        }
    }

    private static partial class Log {
        [LoggerMessage(
            EventId = 3200,
            Level = LogLevel.Warning,
            Message = "Dead-letter published: CorrelationId={CorrelationId}, CausationId={CausationId}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, CommandType={CommandType}, FailureStage={FailureStage}, ExceptionType={ExceptionType}, ErrorMessage={ErrorMessage}, DeadLetterTopic={DeadLetterTopic}, Stage=DeadLetterPublished")]
        public static partial void DeadLetterPublished(
            ILogger logger,
            string correlationId,
            string causationId,
            string tenantId,
            string domain,
            string aggregateId,
            string commandType,
            string failureStage,
            string? exceptionType,
            string? errorMessage,
            string deadLetterTopic);

        [LoggerMessage(
            EventId = 3201,
            Level = LogLevel.Error,
            Message = "Dead-letter publication failed: CorrelationId={CorrelationId}, CausationId={CausationId}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, CommandType={CommandType}, FailureStage={FailureStage}, ExceptionType={ExceptionType}, ErrorMessage={ErrorMessage}, DeadLetterTopic={DeadLetterTopic}, PublicationExceptionType={PublicationExceptionType}, FailureReason={FailureReason}, Stage=DeadLetterPublicationFailed")]
        public static partial void DeadLetterPublicationFailed(
            ILogger logger,
            string correlationId,
            string causationId,
            string tenantId,
            string domain,
            string aggregateId,
            string commandType,
            string failureStage,
            string? exceptionType,
            string? errorMessage,
            string deadLetterTopic,
            string publicationExceptionType,
            string failureReason);
    }
}
